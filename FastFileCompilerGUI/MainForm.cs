using FastFileLib;
using FastFileLib.Models;

namespace FastFileCompilerGUI;

public partial class MainForm : Form
{
    private readonly List<RawFileEntry> _rawFiles = new();
    private readonly List<RawFileEntry> _existingFiles = new();
    private string? _loadedFastFilePath;
    private byte[]? _loadedZoneData; // Original zone data for patching

    public MainForm()
    {
        InitializeComponent();
        UpdateStatus("Ready - Add files to compile into a FastFile");

        // Set initial tooltip for platform dropdown
        comboBoxPlatform_SelectedIndexChanged(this, EventArgs.Empty);
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

        var ffPath = dialog.FileName;
        SetUIEnabled(false);
        UpdateStatus("Loading FastFile...");

        try
        {
            await Task.Run(() =>
            {
                // Load and decompress the FastFile
                Invoke(() => UpdateStatus("Decompressing..."));

                var decompressor = new Decompressor();
                var zonePath = Path.ChangeExtension(ffPath, ".zone");
                decompressor.DecompressToFile(ffPath, zonePath);

                Invoke(() => UpdateStatus("Parsing zone file..."));

                // Parse the zone to get raw files
                var zoneData = File.ReadAllBytes(zonePath);
                var parsedFiles = ParseZoneRawFiles(zoneData);

                Invoke(() =>
                {
                    _existingFiles.Clear();
                    foreach (var file in parsedFiles)
                    {
                        _existingFiles.Add(file);
                    }

                    _loadedFastFilePath = ffPath;
                    _loadedZoneData = zoneData; // Store original zone for patching
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
        _loadedZoneData = null;
        checkBoxIncludeExisting.Enabled = false;
        checkBoxIncludeExisting.Checked = false;
        labelLoadedFF.Text = "(No FF loaded)";
        labelLoadedFF.ForeColor = System.Drawing.Color.Gray;
    }

    /// <summary>
    /// Valid file extensions for raw files in zone data.
    /// </summary>
    private static readonly string[] ValidExtensions = {
        ".cfg", ".gsc", ".atr", ".csc", ".rmb", ".arena", ".vision", ".txt", ".str", ".menu"
    };

    /// <summary>
    /// Parses raw files from zone data using pattern-based search.
    /// Looks for file extensions then backtracks to find the header.
    /// </summary>
    private List<RawFileEntry> ParseZoneRawFiles(byte[] zoneData)
    {
        var result = new List<RawFileEntry>();
        var foundOffsets = new HashSet<int>(); // Track found files to avoid duplicates

        // Search for each file extension pattern
        foreach (var ext in ValidExtensions)
        {
            byte[] pattern = System.Text.Encoding.ASCII.GetBytes(ext + "\0");

            for (int i = 0; i <= zoneData.Length - pattern.Length; i++)
            {
                // Check if pattern matches at this position
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (zoneData[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (!match) continue;

                // Found extension pattern at position i
                // Now backtrack to find the FF FF FF FF marker before the filename
                int ffffPosition = i - 1;
                while (ffffPosition >= 4)
                {
                    if (zoneData[ffffPosition] == 0xFF &&
                        zoneData[ffffPosition - 1] == 0xFF &&
                        zoneData[ffffPosition - 2] == 0xFF &&
                        zoneData[ffffPosition - 3] == 0xFF)
                    {
                        break;
                    }
                    ffffPosition--;

                    // Don't backtrack too far (max filename length ~256)
                    if (i - ffffPosition > 300)
                    {
                        ffffPosition = -1;
                        break;
                    }
                }

                if (ffffPosition < 4) continue;

                // Check the byte after FF marker isn't 0x00 (would indicate end of filename area)
                if (zoneData[ffffPosition + 1] == 0x00) continue;

                // Size is 7 bytes before the FF marker position
                // Structure: [FF FF FF FF] [4-byte size] [FF FF FF FF] [name\0] [data]
                int sizePosition = ffffPosition - 7;
                if (sizePosition < 0) continue;

                // Calculate header start (4 bytes before size)
                int headerStart = sizePosition - 4;
                if (headerStart < 0) continue;

                // Skip if we already found a file at this header position
                if (foundOffsets.Contains(headerStart)) continue;

                // Read size (big-endian)
                int size = (zoneData[sizePosition] << 24) | (zoneData[sizePosition + 1] << 16) |
                           (zoneData[sizePosition + 2] << 8) | zoneData[sizePosition + 3];

                if (size <= 0 || size > 10_000_000) continue; // Sanity check

                // Extract filename (starts right after FF FF FF FF marker)
                int nameStart = ffffPosition + 1;
                int nameEnd = nameStart;
                while (nameEnd < zoneData.Length && zoneData[nameEnd] != 0)
                    nameEnd++;

                if (nameEnd <= nameStart) continue;

                string name = System.Text.Encoding.ASCII.GetString(zoneData, nameStart, nameEnd - nameStart);

                // Validate the name has the extension we were looking for
                if (!name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) continue;

                // Data starts after null terminator
                int dataStart = nameEnd + 1;
                if (dataStart + size > zoneData.Length)
                {
                    // Adjust size if it exceeds bounds
                    size = zoneData.Length - dataStart;
                    if (size <= 0) continue;
                }

                // Extract data (remove trailing zero padding)
                byte[] data = new byte[size];
                Array.Copy(zoneData, dataStart, data, 0, size);
                data = RemoveZeroPadding(data);

                result.Add(new RawFileEntry
                {
                    AssetName = name,
                    SourcePath = "[from loaded FF]",
                    Size = data.Length,
                    Data = data
                });

                foundOffsets.Add(headerStart);
            }
        }

        // Sort by asset name for consistent ordering
        result.Sort((a, b) => string.Compare(a.AssetName, b.AssetName, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    /// <summary>
    /// Removes trailing zero bytes from data.
    /// </summary>
    private static byte[] RemoveZeroPadding(byte[] content)
    {
        int i = content.Length - 1;
        while (i >= 0 && content[i] == 0x00)
            i--;

        if (i < 0) return Array.Empty<byte>();

        byte[] trimmed = new byte[i + 1];
        Array.Copy(content, 0, trimmed, 0, i + 1);
        return trimmed;
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
            var basePath = dialog.SelectedPath;
            var files = Directory.GetFiles(basePath, "*.*", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                // Create relative path as asset name
                var assetName = Path.GetRelativePath(basePath, file).Replace('\\', '/');
                AddFile(file, assetName);
            }
            UpdateFileCount();
        }
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

    private void fileListView_DragDrop(object sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] files) return;

        foreach (var path in files)
        {
            if (Directory.Exists(path))
            {
                // It's a folder - add all files
                var basePath = path;
                var innerFiles = Directory.GetFiles(basePath, "*.*", SearchOption.AllDirectories);
                foreach (var file in innerFiles)
                {
                    var assetName = Path.GetRelativePath(basePath, file).Replace('\\', '/');
                    AddFile(file, assetName);
                }
            }
            else if (File.Exists(path))
            {
                AddFile(path, Path.GetFileName(path));
            }
        }
        UpdateFileCount();
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

        // Disable UI during compile
        SetUIEnabled(false);
        progressBar.Value = 0;
        UpdateStatus("Compiling...");

        try
        {
            await Task.Run(() =>
            {
                byte[] zoneData;

                // If we have a loaded zone and want to preserve it, use patching
                if (includeExisting && _loadedZoneData != null)
                {
                    Invoke(() => UpdateStatus("Patching zone file..."));
                    Invoke(() => progressBar.Value = 20);

                    // Build list of files to replace (from user's added files)
                    var filesToReplace = new List<RawFile>();
                    int totalFiles = _rawFiles.Count;
                    int processed = 0;

                    foreach (var entry in _rawFiles)
                    {
                        byte[] fileData = entry.Data ?? File.ReadAllBytes(entry.SourcePath);

                        var rawFile = new RawFile
                        {
                            Name = entry.AssetName,
                            Data = fileData
                        };
                        rawFile.StripHeaderIfPresent();
                        filesToReplace.Add(rawFile);

                        processed++;
                        int progress = 20 + (int)(30.0 * processed / Math.Max(totalFiles, 1));
                        Invoke(() => progressBar.Value = progress);
                    }

                    Invoke(() => progressBar.Value = 50);
                    Invoke(() => UpdateStatus("Applying patches..."));

                    // Patch the original zone - preserves all structure, replaces/adds raw files
                    var patcher = new ZonePatcher(_loadedZoneData, gameVersion);
                    zoneData = patcher.Patch(filesToReplace);
                }
                else
                {
                    // No existing zone loaded - build from scratch
                    Invoke(() => UpdateStatus("Building zone file..."));
                    Invoke(() => progressBar.Value = 20);

                    var builder = new ZoneBuilder(gameVersion, zoneName);
                    int totalFiles = _rawFiles.Count;
                    int processed = 0;

                    foreach (var entry in _rawFiles)
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
                        Invoke(() => progressBar.Value = progress);
                    }

                    Invoke(() => progressBar.Value = 50);
                    zoneData = builder.Build();
                }

                Invoke(() => UpdateStatus("Compressing..."));
                Invoke(() => progressBar.Value = 70);

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

                Invoke(() => progressBar.Value = 100);
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
        return comboBoxPlatform.SelectedIndex switch
        {
            0 => ("PS3", false),
            1 => ("Xbox360", false),      // Xbox 360 Unsigned
            2 => ("Xbox360", true),       // Xbox 360 Signed
            3 => ("PC", false),
            4 => ("Wii", false),
            _ => ("PS3", false)
        };
    }

    private void comboBoxPlatform_SelectedIndexChanged(object sender, EventArgs e)
    {
        string tooltipText = comboBoxPlatform.SelectedIndex switch
        {
            0 => "PlayStation 3 - Standard unsigned format",
            1 => "Xbox 360 Unsigned - For unsigned/retail Xbox 360 files",
            2 => "Xbox 360 Signed - Requires loading an existing signed FF first to preserve the hash table",
            3 => "PC - Windows platform format",
            4 => "Wii - Nintendo Wii platform format",
            _ => ""
        };
        toolTip.SetToolTip(comboBoxPlatform, tooltipText);
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
