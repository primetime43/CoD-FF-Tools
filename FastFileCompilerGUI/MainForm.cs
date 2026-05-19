using FastFileLib;
using FastFileLib.Models;

namespace FastFileCompilerGUI;

public partial class MainForm : Form
{
    private readonly List<RawFileEntry> _rawFiles = new();
    private readonly List<RawFileEntry> _existingFiles = new();
    private string? _loadedFastFilePath;

    // PC/Wii treat these as per-zone allocations rather than fixed constants;
    // preserve them verbatim when rebuilding. Null = no FF loaded.
    private uint? _loadedBlockSizeTemp;
    private uint? _loadedBlockSizeVertex;

    public MainForm()
    {
        InitializeComponent();
        UpdateStatus("Ready - Add files to compile into a FastFile");

        // Set initial tooltip for platform dropdown
        comboBoxPlatform_SelectedIndexChanged(this, EventArgs.Empty);
    }

    /// <summary>
    /// Marshals <paramref name="action"/> to the UI thread, but swallows the
    /// <see cref="ObjectDisposedException"/> that fires if the form is closed
    /// between the dispatched call and the actual invoke. Background workers
    /// (Task.Run lambdas) keep running briefly after the form is gone — without
    /// this guard the user gets an exception dialog on app close mid-compile.
    /// </summary>
    private void SafeInvoke(Action action)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try { Invoke(action); }
        catch (ObjectDisposedException) { /* form closed during dispatch */ }
        catch (InvalidOperationException) when (IsDisposed) { /* same race */ }
    }

    #region Load Existing FastFile

    private async void btnLoadExistingFF_Click(object sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select Existing FastFile to Load",
            Filter = "FastFile (*.ff;*.ffm)|*.ff;*.ffm|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != DialogResult.OK) return;
        await LoadFastFileAsync(dialog.FileName);
    }

    /// <summary>
    /// Loads, decompresses, and parses a FastFile at <paramref name="ffPath"/> in the
    /// background. Populates _existingFiles + the preserved MemAlloc values, sets the
    /// zone name, and re-enables the UI in a finally block. Both the "Load FF" button
    /// and the drag-drop handler use this so the two entry points stay in sync.
    /// </summary>
    private async Task LoadFastFileAsync(string ffPath)
    {
        SetUIEnabled(false);
        UpdateStatus("Loading FastFile...");

        try
        {
            await Task.Run(() =>
            {
                // Read the FF header up-front so we know the game/platform before
                // decompressing; the scanner needs both to pick the right rawfile header layout.
                var ffInfo = FastFileInfo.FromFile(ffPath);

                SafeInvoke(() => UpdateStatus("Decompressing..."));

                var zonePath = Path.ChangeExtension(ffPath, ".zone");
                // FastFileProcessor handles all platforms (PC single-stream, MW2 extended header,
                // signed Xbox 360 streaming, Wii). The older Decompressor class only knew about
                // block format and choked on PC files.
                FastFileProcessor.Decompress(ffPath, zonePath);

                SafeInvoke(() => UpdateStatus("Parsing zone file..."));

                // Parse the zone to get raw files
                var zoneData = File.ReadAllBytes(zonePath);
                var parsedFiles = ParseZoneRawFiles(zoneData, ffInfo);
                var (memTemp, memVertex) = ReadZoneMemAlloc(zoneData, ffInfo);

                SafeInvoke(() =>
                {
                    _existingFiles.Clear();
                    foreach (var file in parsedFiles)
                    {
                        _existingFiles.Add(file);
                    }

                    _loadedFastFilePath = ffPath;
                    _loadedBlockSizeTemp = memTemp;
                    _loadedBlockSizeVertex = memVertex;
                    checkBoxIncludeExisting.Enabled = true;
                    checkBoxIncludeExisting.Checked = true;
                    labelLoadedFF.Text = $"({_existingFiles.Count} assets)";
                    labelLoadedFF.ForeColor = System.Drawing.Color.Green;

                    // Set zone name from loaded file
                    textBoxZoneName.Text = Path.GetFileNameWithoutExtension(ffPath);
                });

                // Clean up temp zone file - we kept the data in memory
                try { File.Delete(zonePath); } catch { }
            });

            UpdateStatus($"Loaded {_existingFiles.Count} existing assets from {Path.GetFileName(ffPath)}");
            MessageBox.Show(
                $"Loaded {_existingFiles.Count} raw file assets from the FastFile.\n\n" +
                "You can now add/modify files. When compiling:\n" +
                "- Check 'Include existing assets' to rebuild with all existing + new files\n" +
                "- Uncheck to compile only the files you add",
                "FastFile Loaded",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            UpdateStatus("Failed to load FastFile");
            MessageBox.Show($"Failed to load FastFile:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            ClearLoadedFF();
        }
        finally
        {
            SetUIEnabled(true);
        }
    }

    private void ClearLoadedFF()
    {
        _existingFiles.Clear();
        _loadedFastFilePath = null;
        _loadedBlockSizeTemp = null;
        _loadedBlockSizeVertex = null;
        checkBoxIncludeExisting.Enabled = false;
        checkBoxIncludeExisting.Checked = false;
        labelLoadedFF.Text = "(No FF loaded)";
        labelLoadedFF.ForeColor = System.Drawing.Color.Gray;
    }

    /// <summary>
    /// Reads BlockSizeTemp (MemAlloc1 @ 0x08) and BlockSizeVertex (MemAlloc2 @ 0x20)
    /// from the decompressed zone header using the right endianness, so the rebuild
    /// path can preserve them verbatim. MW2 Xbox 360's 48-byte header omits the
    /// vertex slot — return null for that field there. Returns null on either field
    /// if the zone is too short.
    /// </summary>
    private static (uint? blockSizeTemp, uint? blockSizeVertex) ReadZoneMemAlloc(
        byte[] zoneData, FastFileInfo ffInfo)
    {
        if (zoneData.Length < 0x24) return (null, null);

        uint Read(int offset) => ffInfo.IsPC
            ? (uint)(zoneData[offset] | (zoneData[offset + 1] << 8)
                    | (zoneData[offset + 2] << 16) | (zoneData[offset + 3] << 24))
            : (uint)((zoneData[offset] << 24) | (zoneData[offset + 1] << 16)
                    | (zoneData[offset + 2] << 8) | zoneData[offset + 3]);

        uint temp = Read(0x08);

        bool isMw2Xbox360 = ffInfo.GameVersion == GameVersion.MW2 && ffInfo.Platform == "Xbox 360";
        uint? vertex = isMw2Xbox360 || zoneData.Length < 0x24
            ? null
            : Read(0x20);

        return (temp, vertex);
    }

    /// <summary>
    /// Parses raw files from zone data via the shared FastFileLib scanner so all
    /// game/platform header variants (CoD4/WaW 12-byte BE, MW2 16/20-byte BE,
    /// MW2 PC 16-byte LE with zlib payloads) are handled in one place.
    /// </summary>
    private static List<RawFileEntry> ParseZoneRawFiles(byte[] zoneData, FastFileInfo ffInfo)
    {
        var locations = RawFileScanner.FindRawFiles(zoneData, ffInfo.GameVersion, ffInfo.IsPC);

        var result = locations
            .Select(loc => new RawFileEntry
            {
                AssetName = loc.Name,
                SourcePath = "[from loaded FF]",
                Size = loc.Data.Length,
                Data = loc.Data,
            })
            .ToList();

        result.Sort((a, b) => string.Compare(a.AssetName, b.AssetName, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    #endregion

    #region File Management

    private void btnAddFiles_Click(object sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select Raw Files to Add",
            Filter = "All Files (*.*)|*.*|GSC Scripts (*.gsc)|*.gsc|Config Files (*.cfg)|*.cfg|String Tables (*.str)|*.str",
            Multiselect = true
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            foreach (var file in dialog.FileNames)
            {
                AddFile(file, Path.GetFileName(file));
            }
            UpdateFileCount();
        }
    }

    private void btnAddFolder_Click(object sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select a folder containing raw files",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            int added = AddFolderRecursive(dialog.SelectedPath);
            UpdateFileCount();
            if (added == 0)
            {
                MessageBox.Show(
                    "No supported rawfile extensions found in that folder.\n\n" +
                    $"Supported: {string.Join(", ", FastFileConstants.ValidRawFileExtensions)}",
                    "No Files Added",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
    }

    /// <summary>
    /// Enumerates <paramref name="basePath"/> recursively and adds files whose
    /// extension is in <see cref="FastFileConstants.ValidRawFileExtensions"/>.
    /// Skips junk like .git/, Thumbs.db, desktop.ini that's commonly nested next
    /// to script source. Returns the number of files added.
    /// </summary>
    private int AddFolderRecursive(string basePath)
    {
        int added = 0;
        var files = Directory.EnumerateFiles(basePath, "*.*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            var ext = Path.GetExtension(file);
            if (!FastFileConstants.IsValidRawFileExtension(ext)) continue;

            var assetName = Path.GetRelativePath(basePath, file).Replace('\\', '/');
            AddFile(file, assetName);
            added++;
        }
        return added;
    }

    private void AddFile(string sourcePath, string assetName)
    {
        // Automatically fix the asset path (convert flattened names to proper paths)
        assetName = FastFileConstants.FixAssetPath(assetName);

        // Check for duplicates
        if (_rawFiles.Any(f => f.AssetName.Equals(assetName, StringComparison.OrdinalIgnoreCase)))
        {
            var result = MessageBox.Show(
                $"Asset '{assetName}' already exists. Replace it?",
                "Duplicate Asset",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                RemoveByAssetName(assetName);
            }
            else
            {
                return;
            }
        }

        var entry = new RawFileEntry
        {
            AssetName = assetName,
            SourcePath = sourcePath,
            Size = new FileInfo(sourcePath).Length
        };

        _rawFiles.Add(entry);

        var item = new ListViewItem(new[]
        {
            entry.AssetName,
            FastFileInfo.FormatFileSize(entry.Size),
            entry.SourcePath
        })
        {
            Tag = entry
        };

        fileListView.Items.Add(item);
    }

    private void RemoveByAssetName(string assetName)
    {
        var entry = _rawFiles.FirstOrDefault(f => f.AssetName.Equals(assetName, StringComparison.OrdinalIgnoreCase));
        if (entry != null)
        {
            _rawFiles.Remove(entry);
            var item = fileListView.Items.Cast<ListViewItem>().FirstOrDefault(i => i.Tag == entry);
            if (item != null)
                fileListView.Items.Remove(item);
        }
    }

    private void btnRemove_Click(object sender, EventArgs e)
    {
        if (fileListView.SelectedItems.Count == 0) return;

        foreach (ListViewItem item in fileListView.SelectedItems)
        {
            if (item.Tag is RawFileEntry entry)
            {
                _rawFiles.Remove(entry);
            }
            fileListView.Items.Remove(item);
        }
        UpdateFileCount();
    }

    private void btnClear_Click(object sender, EventArgs e)
    {
        if (_rawFiles.Count == 0 && _existingFiles.Count == 0) return;

        var result = MessageBox.Show(
            "Clear all files from the list and unload any loaded FastFile?",
            "Confirm Clear",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
        {
            _rawFiles.Clear();
            fileListView.Items.Clear();
            ClearLoadedFF();
            UpdateFileCount();
        }
    }

    private void btnMoveUp_Click(object sender, EventArgs e)
    {
        if (fileListView.SelectedItems.Count != 1) return;

        var item = fileListView.SelectedItems[0];
        int index = item.Index;
        if (index > 0)
        {
            fileListView.Items.RemoveAt(index);
            fileListView.Items.Insert(index - 1, item);

            var entry = (RawFileEntry)item.Tag!;
            _rawFiles.Remove(entry);
            _rawFiles.Insert(index - 1, entry);

            item.Selected = true;
            item.Focused = true;
        }
    }

    private void btnMoveDown_Click(object sender, EventArgs e)
    {
        if (fileListView.SelectedItems.Count != 1) return;

        var item = fileListView.SelectedItems[0];
        int index = item.Index;
        if (index < fileListView.Items.Count - 1)
        {
            fileListView.Items.RemoveAt(index);
            fileListView.Items.Insert(index + 1, item);

            var entry = (RawFileEntry)item.Tag!;
            _rawFiles.Remove(entry);
            _rawFiles.Insert(index + 1, entry);

            item.Selected = true;
            item.Focused = true;
        }
    }

    private void RefreshFileListView()
    {
        fileListView.BeginUpdate();
        for (int i = 0; i < fileListView.Items.Count; i++)
        {
            var item = fileListView.Items[i];
            if (item.Tag is RawFileEntry entry)
            {
                item.SubItems[0].Text = entry.AssetName;
            }
        }
        fileListView.EndUpdate();
    }

    private void menuItemRename_Click(object sender, EventArgs e)
    {
        if (fileListView.SelectedItems.Count != 1) return;

        var item = fileListView.SelectedItems[0];
        var entry = (RawFileEntry)item.Tag!;

        using var dialog = new RenameDialog(entry.AssetName);
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            entry.AssetName = dialog.NewName;
            item.SubItems[0].Text = entry.AssetName;
        }
    }

    #endregion

    #region Drag and Drop

    private void fileListView_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            e.Effect = DragDropEffects.Copy;
        }
    }

    private async void fileListView_DragDrop(object sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] files) return;

        // If any of the dropped paths is a FastFile, treat the drop as a "Load FF"
        // gesture: load the first .ff/.ffm and ignore the others (with a warning if
        // there were any). Mixing "load FF" with "add raw files" in one drop is
        // ambiguous — better to make the user do it as two actions.
        var ffPath = files.FirstOrDefault(IsFastFilePath);
        if (ffPath != null)
        {
            int otherCount = files.Count(f => f != ffPath);
            if (otherCount > 0)
            {
                MessageBox.Show(
                    $"Loading FastFile '{Path.GetFileName(ffPath)}'.\n\n" +
                    $"{otherCount} other item(s) in this drop were ignored — drop them separately to add as raw files.",
                    "Mixed Drop",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            await LoadFastFileAsync(ffPath);
            return;
        }

        foreach (var path in files)
        {
            if (Directory.Exists(path))
            {
                // Folder drop — filter same way as the Add Folder button so .git/Thumbs.db
                // etc. don't sneak in. Single-file drops below stay permissive (user chose
                // them explicitly).
                AddFolderRecursive(path);
            }
            else if (File.Exists(path))
            {
                AddFile(path, Path.GetFileName(path));
            }
        }
        UpdateFileCount();
    }

    private static bool IsFastFilePath(string path)
    {
        if (!File.Exists(path)) return false;
        var ext = Path.GetExtension(path);
        return string.Equals(ext, ".ff", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ext, ".ffm", StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Compile

    private async void btnCompile_Click(object sender, EventArgs e)
    {
        // Check if we have any files to compile
        bool includeExisting = checkBoxIncludeExisting.Checked && _existingFiles.Count > 0;
        if (_rawFiles.Count == 0 && !includeExisting)
        {
            MessageBox.Show("Please add at least one file to compile.", "No Files", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var (platform, isXbox360Signed) = GetSelectedPlatform();

        // Xbox 360 Signed requires a loaded FF to copy hash table from
        if (isXbox360Signed && _loadedFastFilePath == null)
        {
            MessageBox.Show(
                "Xbox 360 Signed format requires loading an existing signed FastFile first.\n\n" +
                "The hash table from the original file is needed for the signed format.\n\n" +
                "Please load a signed FastFile using 'Load FF...' button, or select a different platform.",
                "Original FF Required",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Save FastFile",
            Filter = "FastFile (*.ff;*.ffm)|*.ff;*.ffm",
            FileName = textBoxZoneName.Text + ".ff"
        };

        if (dialog.ShowDialog() != DialogResult.OK) return;

        var outputPath = dialog.FileName;
        var gameVersion = GetSelectedGameVersion();
        var zoneName = textBoxZoneName.Text;
        var saveZone = checkBoxSaveZone.Checked;

        // Merge existing rawfiles (from loaded FF) with user-added files. User-added wins on
        // name collisions so users can replace a loaded file just by adding one with the same name.
        // Non-rawfile assets from the loaded zone (xmodels, materials, menus, weapons, sounds, etc.)
        // are intentionally dropped — this tool is for editing rawfiles only.
        var filesToBuild = new List<RawFileEntry>();
        if (includeExisting)
            filesToBuild.AddRange(_existingFiles);
        foreach (var userFile in _rawFiles)
        {
            int existingIdx = filesToBuild.FindIndex(f =>
                f.AssetName.Equals(userFile.AssetName, StringComparison.OrdinalIgnoreCase));
            if (existingIdx >= 0)
                filesToBuild[existingIdx] = userFile;
            else
                filesToBuild.Add(userFile);
        }

        // Disable UI during compile
        SetUIEnabled(false);
        progressBar.Value = 0;
        UpdateStatus("Compiling...");

        try
        {
            await Task.Run(() =>
            {
                SafeInvoke(() => UpdateStatus("Building zone file..."));
                SafeInvoke(() => progressBar.Value = 20);

                // When rebuilding on top of a loaded source, preserve its MemAlloc values
                // verbatim — they're per-zone allocations on PC/Wii, and even on console
                // matching the source avoids spurious differences vs retail.
                var builder = new ZoneBuilder(gameVersion, zoneName, platform);
                if (includeExisting)
                {
                    builder.WithBlockSizeTemp(_loadedBlockSizeTemp)
                           .WithBlockSizeVertex(_loadedBlockSizeVertex);
                }
                int totalFiles = filesToBuild.Count;
                int processed = 0;

                foreach (var entry in filesToBuild)
                {
                    byte[] fileData = entry.Data ?? File.ReadAllBytes(entry.SourcePath);

                    var rawFile = new RawFile
                    {
                        Name = entry.AssetName,
                        Data = fileData
                    };
                    rawFile.StripHeaderIfPresent();
                    builder.AddRawFile(rawFile);

                    processed++;
                    int progress = 20 + (int)(30.0 * processed / Math.Max(totalFiles, 1));
                    SafeInvoke(() => progressBar.Value = progress);
                }

                SafeInvoke(() => progressBar.Value = 50);
                byte[] zoneData = builder.Build();

                SafeInvoke(() => UpdateStatus("Compressing..."));
                SafeInvoke(() => progressBar.Value = 70);

                // Save zone file first if requested
                if (saveZone)
                {
                    string zonePath = Path.ChangeExtension(outputPath, ".zone");
                    File.WriteAllBytes(zonePath, zoneData);
                }

                // Write zone to temp file for compression
                string tempZonePath = Path.GetTempFileName();
                File.WriteAllBytes(tempZonePath, zoneData);

                try
                {
                    if (isXbox360Signed)
                    {
                        // Xbox 360 signed format - use streaming compression with hash table from original
                        FastFileProcessor.CompressXbox360Signed(tempZonePath, outputPath, gameVersion, _loadedFastFilePath!);
                    }
                    else
                    {
                        // Standard format - use block compression with platform-specific version
                        FastFileProcessor.Compress(tempZonePath, outputPath, gameVersion, platform);
                    }
                }
                finally
                {
                    // Clean up temp file
                    try { File.Delete(tempZonePath); } catch { }
                }

                SafeInvoke(() => progressBar.Value = 100);
            });

            UpdateStatus($"Successfully compiled: {Path.GetFileName(outputPath)}");

            var message = $"FastFile compiled successfully!\n\nOutput: {outputPath}";
            if (saveZone)
            {
                message += $"\nZone: {Path.ChangeExtension(outputPath, ".zone")}";
            }
            MessageBox.Show(message, "Compile Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            UpdateStatus("Compile failed");
            progressBar.Value = 0;
            MessageBox.Show($"Compile failed:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetUIEnabled(true);
        }
    }

    private GameVersion GetSelectedGameVersion()
    {
        return comboBoxGame.SelectedIndex switch
        {
            0 => GameVersion.CoD4,
            1 => GameVersion.WaW,
            2 => GameVersion.MW2,
            _ => GameVersion.CoD4
        };
    }

    private (string platform, bool isXbox360Signed) GetSelectedPlatform()
    {
        // Key off the label string rather than an index so dynamically removing/adding
        // entries (e.g. hiding Wii when MW2 is selected) doesn't shift selection meaning.
        string sel = comboBoxPlatform.SelectedItem?.ToString() ?? "PS3";
        return sel switch
        {
            "PS3"                 => ("PS3", false),
            "Xbox 360 (Unsigned)" => ("Xbox360", false),
            "Xbox 360 (Signed)"   => ("Xbox360", true),
            "PC"                  => ("PC", false),
            "Wii"                 => ("Wii", false),
            _                     => ("PS3", false),
        };
    }

    private void comboBoxPlatform_SelectedIndexChanged(object sender, EventArgs e)
    {
        string sel = comboBoxPlatform.SelectedItem?.ToString() ?? "";
        string tooltipText = sel switch
        {
            "PS3"                 => "PlayStation 3 - Standard unsigned format",
            "Xbox 360 (Unsigned)" => "Xbox 360 Unsigned - For unsigned/retail Xbox 360 files",
            "Xbox 360 (Signed)"   => "Xbox 360 Signed - Requires loading an existing signed FF first to preserve the hash table",
            "PC"                  => "PC - Windows platform format",
            "Wii"                 => "Wii - Nintendo Wii platform format",
            _                     => "",
        };
        toolTip.SetToolTip(comboBoxPlatform, tooltipText);
    }

    /// <summary>
    /// When the user picks a game, hide platforms that game wasn't released on.
    /// Currently the only unsupported combo is MW2 + Wii (MW2 never shipped on Wii).
    /// If the user had Wii selected and switches to MW2, snap selection to PS3.
    /// </summary>
    private void comboBoxGame_SelectedIndexChanged(object sender, EventArgs e)
    {
        bool isMw2 = GetSelectedGameVersion() == GameVersion.MW2;
        int wiiIdx = comboBoxPlatform.Items.IndexOf("Wii");
        string currentPlatform = comboBoxPlatform.SelectedItem?.ToString() ?? "";

        if (isMw2 && wiiIdx >= 0)
        {
            comboBoxPlatform.Items.RemoveAt(wiiIdx);
            if (currentPlatform == "Wii")
                comboBoxPlatform.SelectedItem = "PS3";
        }
        else if (!isMw2 && wiiIdx < 0)
        {
            comboBoxPlatform.Items.Add("Wii");
        }
    }

    #endregion

    #region UI Helpers

    private void UpdateStatus(string status)
    {
        labelStatus.Text = status;
    }

    private void UpdateFileCount()
    {
        long totalSize = _rawFiles.Sum(f => f.Size);
        UpdateStatus($"{_rawFiles.Count} file(s), {FastFileInfo.FormatFileSize(totalSize)} total");
    }

    private void SetUIEnabled(bool enabled)
    {
        btnLoadExistingFF.Enabled = enabled;
        btnAddFiles.Enabled = enabled;
        btnAddFolder.Enabled = enabled;
        btnRemove.Enabled = enabled;
        btnClear.Enabled = enabled;
        btnMoveUp.Enabled = enabled;
        btnMoveDown.Enabled = enabled;
        btnCompile.Enabled = enabled;
        comboBoxGame.Enabled = enabled;
        comboBoxPlatform.Enabled = enabled;
        textBoxZoneName.Enabled = enabled;
        checkBoxSaveZone.Enabled = enabled;
        fileListView.Enabled = enabled;
        // Keep checkBoxIncludeExisting enabled state based on whether FF is loaded
        if (enabled)
            checkBoxIncludeExisting.Enabled = _existingFiles.Count > 0;
    }

    #endregion

    #region Menu Events

    private void exitMenuItem_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void viewLogsMenuItem_Click(object sender, EventArgs e)
    {
        new FastFileLib.WinForms.LogViewerForm().Show(this);
    }

    private void aboutMenuItem_Click(object sender, EventArgs e)
    {
        MessageBox.Show(
            "FastFile Compiler GUI\n\n" +
            "A tool for creating Call of Duty FastFiles (.ff)\n" +
            "from raw game files.\n\n" +
            "Supported Games:\n" +
            "- Call of Duty 4: Modern Warfare\n" +
            "- Call of Duty: World at War\n" +
            "- Call of Duty: Modern Warfare 2\n\n" +
            "Supported Platforms:\n" +
            "- PS3\n" +
            "- Xbox 360 (Unsigned & Signed)\n" +
            "- PC\n" +
            "- Wii",
            "About",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    #endregion
}

/// <summary>
/// Represents a raw file entry in the list.
/// </summary>
public class RawFileEntry
{
    public string AssetName { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public long Size { get; set; }
    /// <summary>
    /// Cached data for files loaded from existing FastFile.
    /// Null for files that should be read from SourcePath.
    /// </summary>
    public byte[]? Data { get; set; }
}
