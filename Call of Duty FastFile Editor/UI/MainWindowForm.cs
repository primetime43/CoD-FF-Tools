using Call_of_Duty_FastFile_Editor.CodeOperations;
using Call_of_Duty_FastFile_Editor.Constants;
using Call_of_Duty_FastFile_Editor.IO;
using Call_of_Duty_FastFile_Editor.Models;
using Call_of_Duty_FastFile_Editor.Services;
using Call_of_Duty_FastFile_Editor.UI;
using Call_of_Duty_FastFile_Editor.ZoneParsers;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using static Call_of_Duty_FastFile_Editor.Service.GitHubReleaseChecker;

namespace Call_of_Duty_FastFile_Editor
{
    public partial class MainWindowForm : Form
    {
        private TreeNode _previousSelectedNode;
        private readonly IRawFileService _rawFileService;

        /// <summary>
        /// List of raw file nodes extracted from the zone file.
        /// </summary>
        private List<RawFileNode> _rawFileNodes;

        private List<LocalizedEntry> _localizedEntries;

        /// <summary>
        /// Tracks whether the zone contains unsupported asset types.
        /// If true, full zone rebuild would lose these assets.
        /// </summary>
        private bool _hasUnsupportedAssets;

        /// <summary>
        /// Tracks whether any modifications have been made that require saving.
        /// </summary>
        private bool _hasUnsavedChanges;

        /// <summary>
        /// Forces a full zone rebuild on save (set after import or when changes can't be patched in place).
        /// </summary>
        private bool _localizeNeedsRebuild;

        /// <summary>
        /// Original count of localize entries when file was loaded.
        /// Used to detect if new entries have been added.
        /// </summary>
        private int _originalLocalizeCount;

        /// <summary>
        /// List of menu lists extracted from the zone file.
        /// </summary>
        private List<MenuList> _menuLists;

        /// <summary>
        /// List of techsets extracted from the zone file.
        /// </summary>
        private List<TechSetAsset> _techSets;

        /// <summary>
        /// List of XAnim assets extracted from the zone file.
        /// </summary>
        private List<XAnimParts> _xanims;

        /// <summary>
        /// List of StringTable assets extracted from the zone file.
        /// </summary>
        private List<StringTable> _stringTables;

        /// <summary>
        /// List of tags extracted from the zone file.
        /// </summary>
        private TagCollection? _tags;

        /// <summary>
        /// Offset where the assset pool starts in the zone file.
        /// </summary>
        private int _assetPoolStartOffset;

        /// <summary>
        /// Offset where the assset pool ends in the zone file.
        /// </summary>
        private int _assetPoolEndOffset;

        /// <summary>
        /// FastFile instance representing the opened Fast File.
        /// </summary>
        private FastFile _openedFastFile;

        /// <summary>
        /// List of ZoneAssetRecords extracted from the opened Fast File's zone.
        /// </summary>
        private List<ZoneAssetRecord> _zoneAssetRecords;

        private AssetRecordCollection _processResult;
        private IFastFileHandler _fastFileHandler;

        /// <summary>
        /// Currently selected MenuList in the menu files tree.
        /// </summary>
        private MenuList? _selectedMenuList;

        /// <summary>
        /// Currently selected MenuDef in the menu files tree.
        /// </summary>
        private MenuDef? _selectedMenuDef;

        public MainWindowForm(IRawFileService rawFileService)
        {
            InitializeComponent();
            _rawFileService = rawFileService;
            textEditorControlEx1.SyntaxHighlighting = "C#";
            this.SetProgramTitle();
            localizeListView.DoubleClick += localizeListView_DoubleClick;

            // Universal toolstrip menu item
            copyToolStripMenuItem.Click += copyToolStripMenuItem_Click;
            universalContextMenu.Opening += universalContextMenu_Opening;
            this.FormClosing += MainWindowForm_FormClosing;
        }

        #region Right Click Context Menu initialization
        private string _rightClickedItemText = string.Empty;

        private void universalContextMenu_Opening(object sender, CancelEventArgs e)
        {
            copyToolStripMenuItem.Enabled = !string.IsNullOrEmpty(_rightClickedItemText);
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_rightClickedItemText))
            {
                Clipboard.SetText(_rightClickedItemText);
            }
        }
        #endregion

        /// <summary>
        /// Parses and processes the asset records from the opened Fast File's zone.
        /// </summary>
        private void LoadAssetRecordsData(bool forcePatternMatching = false, bool loadRawFiles = true, bool loadLocalizedEntries = true, bool loadTags = true)
        {
            var zone = _openedFastFile.OpenedFastFileZone;

            // Set the zone asset records to this form's field
            _zoneAssetRecords = zone.ZoneFileAssets.ZoneAssetRecords;

            // Set these so it's shorter/easier to use them later
            _assetPoolStartOffset = zone.AssetPoolStartOffset;
            _assetPoolEndOffset = zone.AssetPoolEndOffset;

            // Process asset records - uses structure-based parsing first, then pattern matching fallback
            _processResult = AssetRecordProcessor.ProcessAssetRecords(_openedFastFile, _zoneAssetRecords, forcePatternMatching);

            // store the typed lists based on user selection
            _rawFileNodes = loadRawFiles ? _processResult.RawFileNodes : new List<RawFileNode>();
            RawFileNode.CurrentZone = zone;
            _localizedEntries = loadLocalizedEntries ? _processResult.LocalizedEntries : new List<LocalizedEntry>();
            _menuLists = _processResult.MenuLists ?? new List<MenuList>();
            _techSets = _processResult.TechSets ?? new List<TechSetAsset>();
            _xanims = _processResult.XAnims ?? new List<XAnimParts>();
            _stringTables = _processResult.StringTables ?? new List<StringTable>();

            // Track unsupported assets and original counts for safe save detection
            _hasUnsupportedAssets = !ZoneFileBuilder.ContainsOnlySupportedAssets(zone, _openedFastFile);
            _originalLocalizeCount = _localizedEntries?.Count ?? 0;
            _hasUnsavedChanges = false; // Reset - no changes made yet
            _localizeNeedsRebuild = false; // Reset - no rebuild needed yet

            // also store updated records
            _zoneAssetRecords = _processResult.UpdatedRecords;

            // Parse and populate tags if selected
            if (loadTags)
            {
                PopulateTags();
                // Ensure tags tab is visible
                if (!mainTabControl.TabPages.Contains(tagsTabPage))
                {
                    mainTabControl.TabPages.Insert(2, tagsTabPage); // Insert after Asset Pool and Raw Files
                }
            }
            else
            {
                _tags = null;
                // Hide tags tab
                if (mainTabControl.TabPages.Contains(tagsTabPage))
                {
                    mainTabControl.TabPages.Remove(tagsTabPage);
                }
            }
        }

        /// <summary>
        /// Loads all parsed zone data into the UI components for display.
        /// </summary>
        private void LoadZoneDataToUI()
        {
            // Load the asset pool into the ListView
            // The data LoadAssetRecordsData gets
            LoadAssetPoolIntoListView();

            // Load the raw files into the TreeView
            LoadRawFilesTreeView();

            // Load the values parsed from the zone header (tag count, asset record count)
            LoadZoneHeaderValues(_openedFastFile.OpenedFastFileZone);

            PopulateLocalizeAssets();
            PopulateMenuFiles();
            PopulateTechSets();
            PopulateXAnims();
            PopulateStringTables();
            PopulateCollision_Map_Asset_StringData();
        }

        /// <summary>
        /// Once all data has been loaded to the UI, show UI elements that were previously hidden/disabled.
        /// </summary>
        private void EnableUI_Elements()
        {
            // Enable relevant menu items
            renameRawFileToolStripMenuItem.Enabled = true;
            saveFastFileToolStripMenuItem.Enabled = true;
            saveFastFileAsToolStripMenuItem.Enabled = true;
            localizeToolsMenuItem.Enabled = _localizedEntries != null && _localizedEntries.Count > 0;
        }

        /// <summary>
        /// Populates the TreeView with TreeNodes corresponding to RawFileNodes.
        /// </summary>
        private void LoadRawFilesTreeView()
        {
            // Clear existing nodes to avoid duplication
            filesTreeView.Nodes.Clear();

            var treeNodes = _rawFileNodes.Select(node =>
            {
                var treeNode = new TreeNode(node.FileName)
                {
                    Tag = node // Associate TreeNode with RawFileNode via Tag
                };
                return treeNode;
            }).ToArray();

            filesTreeView.Nodes.AddRange(treeNodes);
            UIManager.SetRawFileTreeNodeColors(filesTreeView);
        }

        /// <summary>
        /// Handles actions before selecting a new TreeView node, prompting to save unsaved changes.
        /// </summary>
        private void filesTreeView_BeforeSelect(object sender, TreeViewCancelEventArgs e)
        {
            if (_previousSelectedNode?.Tag is RawFileNode prevNode && prevNode.HasUnsavedChanges)
            {
                var result = MessageBox.Show(
                    "You have unsaved changes to this file. Do you want to save before switching?",
                    "Unsaved Changes",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    // Save the previous node
                    _rawFileService.SaveZoneRawFileChanges(
                        filesTreeView,
                        _openedFastFile.FfFilePath,
                        _openedFastFile.ZoneFilePath,
                        _rawFileNodes,
                        prevNode.RawFileContent,
                        _openedFastFile
                    );
                    prevNode.HasUnsavedChanges = false;
                }
                else if (result == DialogResult.Cancel)
                {
                    // Cancel the switch entirely
                    e.Cancel = true;
                    return;
                }
                else // DialogResult.No → discard changes
                {
                    // Revert the node’s content back to the last‐loaded bytes
                    var originalText = Encoding.UTF8.GetString(prevNode.RawFileBytes);
                    prevNode.RawFileContent = originalText;
                    prevNode.HasUnsavedChanges = false;

                    // Immediately update the editor so the user sees the discard
                    textEditorControlEx1.TextChanged -= textEditorControlEx1_TextChanged;
                    textEditorControlEx1.SetTextAndRefresh(originalText);
                    textEditorControlEx1.TextChanged += textEditorControlEx1_TextChanged;
                }
            }

            // Now allow the selection to change
            _previousSelectedNode = filesTreeView.SelectedNode;
        }

        /// <summary>
        /// Handles actions after selecting a new TreeView node, loading the corresponding file content.
        /// </summary>
        private void filesTreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node?.Tag is RawFileNode selectedNode)
            {
                // Load the file content into the editor without retriggering TextChanged
                textEditorControlEx1.TextChanged -= textEditorControlEx1_TextChanged;
                textEditorControlEx1.SetTextAndRefresh(selectedNode.RawFileContent ?? string.Empty);
                textEditorControlEx1.TextChanged += textEditorControlEx1_TextChanged;

                // Update UI
                UIManager.UpdateSelectedFileStatusStrip(selectedItemStatusLabel, selectedNode.FileName);
                UIManager.UpdateStatusStrip(
                    selectedFileMaxSizeStatusLabel,
                    selectedFileCurrentSizeStatusLabel,
                    selectedNode.MaxSize,
                    textEditorControlEx1.Text.Length
                );

                // Reset this node’s dirty flag now that its content is in sync
                selectedNode.HasUnsavedChanges = false;

                // Track for BeforeSelect logic
                _previousSelectedNode = e.Node;
            }
        }

        /// <summary>
        /// Handles text changes in the editor, marking the content as unsaved.
        /// </summary>
        private void textEditorControlEx1_TextChanged(object sender, EventArgs e)
        {
            // Fetch the selected node from the TreeView
            if (filesTreeView.SelectedNode?.Tag is RawFileNode selectedNode)
            {
                // Update the RawFileContent in memory
                selectedNode.RawFileContent = textEditorControlEx1.Text;
                // Mark the file as having unsaved changes (dirty)
                selectedNode.HasUnsavedChanges = true;
                _hasUnsavedChanges = true; // Mark form-level dirty flag

                // The "current size" is simply the length of the editor text
                int currentSize = textEditorControlEx1.Text.Length;

                // Update the status strip to reflect the new size
                UIManager.UpdateStatusStrip(
                    selectedFileMaxSizeStatusLabel,       // The label displaying "Max Size: XYZ"
                    selectedFileCurrentSizeStatusLabel,   // The label displaying "Current Size: XYZ"
                    selectedNode.MaxSize,                 // The raw file's maximum allowed size
                    currentSize                           // The new size in the editor
                );
            }
        }

        /// <summary>
        /// Saves the current Fast File, recompressing it.
        /// Patches changes in place to preserve all assets.
        /// Only rebuilds zone when injecting files or increasing sizes.
        /// </summary>
        private void saveFastFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_openedFastFile == null || _fastFileHandler == null)
            {
                MessageBox.Show("No Fast File is currently opened.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                // Sync current raw file editor text to the selected node (if any)
                if (filesTreeView.SelectedNode?.Tag is RawFileNode selectedNode)
                {
                    selectedNode.RawFileContent = textEditorControlEx1.Text;
                }

                // Sync current menu file editor text to the selected menu (if any)
                if (_selectedMenuDef != null)
                {
                    _selectedMenuDef.StringContent = menuFilesTextEditor.Text;
                }

                // Count changes
                int rawFileChangeCount = 0;
                int menuChangeCount = 0;
                int localizeChangeCount = 0;

                // Debug: trace through save conditions
                System.Diagnostics.Debug.WriteLine($"[SAVE] _hasUnsavedChanges={_hasUnsavedChanges}, _localizedEntries={_localizedEntries?.Count ?? -1}, _originalLocalizeCount={_originalLocalizeCount}");

                // Apply raw file changes in place (patch directly into zone data)
                if (_rawFileNodes != null)
                {
                    byte[] zoneData = _openedFastFile.OpenedFastFileZone.Data;

                    foreach (var node in _rawFileNodes)
                    {
                        if (node.HasUnsavedChanges && !string.IsNullOrEmpty(node.RawFileContent))
                        {
                            // Check if content fits within the allocated size
                            byte[] newContent = Encoding.ASCII.GetBytes(node.RawFileContent);
                            if (newContent.Length > node.MaxSize)
                            {
                                MessageBox.Show($"Raw file '{node.FileName}' content ({newContent.Length} bytes) exceeds max size ({node.MaxSize} bytes).\n\n" +
                                                "Use 'Increase File Size' option first, or reduce content size.",
                                                "Content Too Large",
                                                MessageBoxButtons.OK,
                                                MessageBoxIcon.Warning);
                                return;
                            }

                            // Patch the content directly into the zone data at the file's offset
                            // Clear the area first, then write new content
                            for (int i = 0; i < node.MaxSize && node.CodeStartPosition + i < zoneData.Length; i++)
                            {
                                zoneData[node.CodeStartPosition + i] = i < newContent.Length ? newContent[i] : (byte)0;
                            }

                            node.RawFileBytes = newContent;
                            node.HasUnsavedChanges = false;
                            rawFileChangeCount++;
                        }
                    }
                }

                // Apply menu file changes in place
                if (_menuLists != null)
                {
                    foreach (var menuList in _menuLists)
                    {
                        menuChangeCount += menuList.Menus.Count(m => m.HasUnsavedChanges);
                    }
                }

                if (menuChangeCount > 0)
                {
                    ApplyMenuFileChangesToZone();

                    // Reset menu dirty flags
                    foreach (var menuList in _menuLists)
                    {
                        foreach (var menu in menuList.Menus)
                        {
                            menu.HasUnsavedChanges = false;
                        }
                    }
                }

                // Apply localize changes in place (if the form-level dirty flag indicates changes)
                System.Diagnostics.Debug.WriteLine($"[SAVE] Checking localize save: _hasUnsavedChanges={_hasUnsavedChanges}, _localizeNeedsRebuild={_localizeNeedsRebuild}, entries={_localizedEntries?.Count ?? -1}");
                if (_hasUnsavedChanges && _localizedEntries != null && _localizedEntries.Count > 0)
                {
                    // If import was done, force rebuild; otherwise check if we can patch in place
                    bool canPatch = !_localizeNeedsRebuild && CanPatchLocalizeInPlace();
                    System.Diagnostics.Debug.WriteLine($"[SAVE] CanPatchLocalizeInPlace={canPatch}");
                    // Try to patch localize entries in place
                    if (canPatch)
                    {
                        if (PatchLocalizeEntriesInPlace())
                        {
                            localizeChangeCount = _localizedEntries.Count; // Count all as changed (we don't track individual entries)
                        }
                    }
                    else
                    {
                        // Can't patch in place - need to rebuild zone
                        if (_hasUnsupportedAssets)
                        {
                            // Warn about unsupported assets that will be lost
                            var unsupportedTypes = ZoneFileBuilder.GetUnsupportedAssetInfo(
                                _openedFastFile.OpenedFastFileZone, _openedFastFile);
                            var typeList = string.Join(", ", unsupportedTypes.Distinct().Take(5));

                            var result = MessageBox.Show(
                                $"Localize text size increased - zone must be rebuilt.\n\n" +
                                $"This zone contains unsupported asset types ({typeList}).\n" +
                                $"These assets will be LOST if you continue.\n\n" +
                                $"Do you want to rebuild the zone anyway?",
                                "Rebuild Zone",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Warning);

                            if (result != DialogResult.Yes)
                                return;
                        }

                        // Rebuild the zone with updated localize entries
                        if (RebuildZoneWithCurrentData())
                        {
                            localizeChangeCount = _localizedEntries.Count;
                            System.Diagnostics.Debug.WriteLine($"[SAVE] Zone rebuilt successfully with localize changes");
                        }
                        else
                        {
                            MessageBox.Show("Failed to rebuild zone with localize changes.",
                                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                    }
                }

                // Check if there were any changes
                if (rawFileChangeCount == 0 && menuChangeCount == 0 && localizeChangeCount == 0)
                {
                    MessageBox.Show("No changes to save.", "Save", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Write the modified zone data directly (preserves all assets)
                File.WriteAllBytes(_openedFastFile.ZoneFilePath, _openedFastFile.OpenedFastFileZone.Data);

                // Recompress to FF
                _fastFileHandler?.Recompress(_openedFastFile.FfFilePath, _openedFastFile.ZoneFilePath, _openedFastFile);

                // Build save message
                var changes = new List<string>();
                if (rawFileChangeCount > 0) changes.Add($"{rawFileChangeCount} raw file(s)");
                if (menuChangeCount > 0) changes.Add($"{menuChangeCount} menu(s)");
                if (localizeChangeCount > 0) changes.Add("localize entries");

                MessageBox.Show($"Fast File saved to:\n\n{_openedFastFile.FfFilePath}\n\n" +
                                $"Patched {string.Join(" and ", changes)} in place. All assets preserved.",
                                "Saved",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Asterisk);

                // Reset dirty flags after successful save
                _hasUnsavedChanges = false;
                _localizeNeedsRebuild = false;

                // Remove asterisk from title
                if (this.Text.EndsWith("*"))
                {
                    this.Text = this.Text.TrimEnd('*');
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save Fast File: {ex.Message}",
                                "Save Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Saves the Fast File as a new file.
        /// Patches changes in place to preserve all assets.
        /// </summary>
        private void saveFastFileAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_openedFastFile == null || _fastFileHandler == null)
            {
                MessageBox.Show("No Fast File is currently opened.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using (var saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "Fast Files (*.ff)|*.ff|All Files (*.*)|*.*";
                saveFileDialog.Title = "Save Fast File As";

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        // Sync current raw file editor text to the selected node (if any)
                        if (filesTreeView.SelectedNode?.Tag is RawFileNode selectedNode)
                        {
                            selectedNode.RawFileContent = textEditorControlEx1.Text;
                        }

                        // Sync current menu file editor text to the selected menu (if any)
                        if (_selectedMenuDef != null)
                        {
                            _selectedMenuDef.StringContent = menuFilesTextEditor.Text;
                        }

                        string newFilePath = saveFileDialog.FileName;
                        string zoneName = Path.GetFileNameWithoutExtension(newFilePath);
                        string tempZonePath = Path.Combine(Path.GetTempPath(), zoneName + ".zone");

                        // Count changes
                        int rawFileChangeCount = 0;
                        int menuChangeCount = 0;

                        // Apply raw file changes in place (patch directly into zone data)
                        if (_rawFileNodes != null)
                        {
                            byte[] zoneData = _openedFastFile.OpenedFastFileZone.Data;

                            foreach (var node in _rawFileNodes)
                            {
                                if (node.HasUnsavedChanges && !string.IsNullOrEmpty(node.RawFileContent))
                                {
                                    // Check if content fits within the allocated size
                                    byte[] newContent = Encoding.ASCII.GetBytes(node.RawFileContent);
                                    if (newContent.Length > node.MaxSize)
                                    {
                                        MessageBox.Show($"Raw file '{node.FileName}' content ({newContent.Length} bytes) exceeds max size ({node.MaxSize} bytes).\n\n" +
                                                        "Use 'Increase File Size' option first, or reduce content size.",
                                                        "Content Too Large",
                                                        MessageBoxButtons.OK,
                                                        MessageBoxIcon.Warning);
                                        return;
                                    }

                                    // Patch the content directly into the zone data at the file's offset
                                    for (int i = 0; i < node.MaxSize && node.CodeStartPosition + i < zoneData.Length; i++)
                                    {
                                        zoneData[node.CodeStartPosition + i] = i < newContent.Length ? newContent[i] : (byte)0;
                                    }

                                    node.RawFileBytes = newContent;
                                    node.HasUnsavedChanges = false;
                                    rawFileChangeCount++;
                                }
                            }
                        }

                        // Apply menu file changes in place
                        if (_menuLists != null)
                        {
                            foreach (var menuList in _menuLists)
                            {
                                menuChangeCount += menuList.Menus.Count(m => m.HasUnsavedChanges);
                            }

                            if (menuChangeCount > 0)
                            {
                                ApplyMenuFileChangesToZone();

                                // Reset menu dirty flags
                                foreach (var menuList in _menuLists)
                                {
                                    foreach (var menu in menuList.Menus)
                                    {
                                        menu.HasUnsavedChanges = false;
                                    }
                                }
                            }
                        }

                        // Build save message
                        var changes = new List<string>();
                        if (rawFileChangeCount > 0) changes.Add($"{rawFileChangeCount} raw file(s)");
                        if (menuChangeCount > 0) changes.Add($"{menuChangeCount} menu(s)");
                        string saveMessage = changes.Count > 0
                            ? $"Patched {string.Join(" and ", changes)} in place. All assets preserved."
                            : "Zone saved with all assets preserved.";

                        // Write the modified zone data to temp file
                        File.WriteAllBytes(tempZonePath, _openedFastFile.OpenedFastFileZone.Data);

                        // Recompress to new FF path
                        _fastFileHandler?.Recompress(newFilePath, tempZonePath, _openedFastFile);

                        // Clean up temp file
                        if (File.Exists(tempZonePath))
                            File.Delete(tempZonePath);

                        MessageBox.Show($"Fast File saved to:\n\n{newFilePath}\n\n{saveMessage}",
                                        "Saved",
                                        MessageBoxButtons.OK,
                                        MessageBoxIcon.Asterisk);

                        // Then close out
                        SaveCloseFastFileAndCleanUp();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to save Fast File As: {ex.Message}",
                                        "Save Error",
                                        MessageBoxButtons.OK,
                                        MessageBoxIcon.Error);
                    }
                }
            }
        }

        /// <summary>
        /// Exits the application.
        /// </summary>
        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveCloseFastFileAndCleanUp(!keepZoneFileToolStripMenuItem.Checked);
            Close();
        }

        /// <summary>
        /// Removes comments from the code in the editor.
        /// </summary>
        private void removeCommentsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                textEditorControlEx1.Text = CommentRemover.RemoveCStyleComments(textEditorControlEx1.Text);
                textEditorControlEx1.Text = CommentRemover.RemoveCustomComments(textEditorControlEx1.Text);
                textEditorControlEx1.Text = Regex.Replace(textEditorControlEx1.Text, "(\\r\\n){2,}", "\r\n\r\n");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to remove comments: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Compresses the code in the editor.
        /// </summary>
        private void compressCodeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                textEditorControlEx1.Text = CommentRemover.RemoveCStyleComments(textEditorControlEx1.Text);
                textEditorControlEx1.Text = CommentRemover.RemoveCustomComments(textEditorControlEx1.Text);
                textEditorControlEx1.Text = CodeCompressor.CompressCode(textEditorControlEx1.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to compress code: {ex.Message}", "Compression Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Checks the syntax of the code in the editor.
        /// </summary>
        private void checkSyntaxToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                SyntaxChecker.CheckSyntax(textEditorControlEx1.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Syntax check failed: {ex.Message}", "Syntax Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void checkForUpdate()
        {
            try
            {
                var release = await ReleaseChecker.CheckForNewRelease("primetime43", "CoD-FF-Tools");

                if (release != null)
                {
                    int latestReleaseInt = ReleaseChecker.convertVersionToInt(release.tag_name);
                    int localProgramVersionInt = ReleaseChecker.convertVersionToInt(ApplicationConstants.ProgramVersion);

                    if (latestReleaseInt > localProgramVersionInt)
                    {
                        DialogResult result = MessageBox.Show(
                            "Current version: " + ApplicationConstants.ProgramVersion + "\nNew release available: " + release.name + " (" + release.tag_name + ")\nDo you want to download it?",
                            "New Release",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question
                        );

                        if (result == DialogResult.Yes)
                        {
                            try
                            {
                                var startInfo = new ProcessStartInfo
                                {
                                    FileName = ReleaseChecker.releaseURL,
                                    UseShellExecute = true
                                };

                                Process.Start(startInfo);
                            }
                            catch (System.ComponentModel.Win32Exception ex)
                            {
                                MessageBox.Show("An error occurred: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }
                    else if (latestReleaseInt == localProgramVersionInt)
                    {
                        MessageBox.Show("You are using the latest version.", "No Update Available", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        Debug.WriteLine("Local version is newer than the latest release.");
                    }
                }
                else
                {
                    MessageBox.Show("No new releases available.", "Update Check", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to check for updates: {ex.Message}", "Update Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void injectFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Inject a File";
                ofd.Filter = "Allowed Files (*.cfg;*.gsc;*.atr;*.csc;*.rmb;*.arena;*.vision)|*.cfg;*.gsc;*.atr;*.csc;*.rmb;*.arena;*.vision|All Files (*.*)|*.*";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    string selectedFilePath = ofd.FileName;
                    string rawFileName = Path.GetFileName(selectedFilePath);

                    // Detect if file has zone header or is a plain file
                    bool hasZoneHeader = DetectZoneHeader(selectedFilePath);

                    string rawFileNameFromHeader;
                    byte[] rawFileContent;
                    int newFileMaxSize;

                    if (hasZoneHeader)
                    {
                        // Parse the file to obtain expected header details.
                        var parsedNodes = RawFileParser.ExtractAllRawFilesSizeAndName(selectedFilePath);
                        if (parsedNodes == null || parsedNodes.Count == 0)
                        {
                            MessageBox.Show("Failed to parse the file header. The file may be corrupted.",
                                "Parse Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                        RawFileNode newRawFileNode = parsedNodes[0];
                        rawFileNameFromHeader = newRawFileNode.FileName;
                        rawFileContent = newRawFileNode.RawFileBytes;
                        newFileMaxSize = newRawFileNode.MaxSize;
                    }
                    else
                    {
                        // Plain file - prompt user for game path
                        string gamePath = PromptForGamePath(rawFileName);
                        if (string.IsNullOrWhiteSpace(gamePath))
                        {
                            MessageBox.Show("Injection canceled. A game path is required for plain files.",
                                "Injection Canceled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }
                        rawFileNameFromHeader = gamePath;
                        rawFileContent = File.ReadAllBytes(selectedFilePath);
                        newFileMaxSize = rawFileContent.Length;
                    }

                    // Check if a file with the same header name already exists.
                    RawFileNode existingNode = _rawFileNodes
                        .FirstOrDefault(node => node.FileName.Equals(rawFileNameFromHeader, StringComparison.OrdinalIgnoreCase));

                    if (existingNode != null)
                    {
                        try
                        {
                            if (newFileMaxSize > existingNode.MaxSize)
                                _rawFileService.IncreaseSize(_openedFastFile.ZoneFilePath, existingNode, rawFileContent);
                            else
                                _rawFileService.UpdateFileContent(_openedFastFile.ZoneFilePath, existingNode, rawFileContent);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Failed to update file: {ex.Message}",
                                "Injection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                    }
                    else
                    {
                        // File doesn't exist in the FastFile - cannot add new raw files here
                        MessageBox.Show(
                            $"The raw file '{rawFileNameFromHeader}' does not exist in this FastFile.\n\n" +
                            "This tool can only update existing raw files.\n\n" +
                            "To add custom raw files, use the FF Compiler to build a custom FastFile.",
                            "File Not Found",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }

                    RefreshZoneData();
                    ReloadAllRawFileNodesAndUI();
                    MessageBox.Show($"File '{rawFileName}' was successfully updated in the zone file.",
                        "Update Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        /// <summary>
        /// Detects if a file has a zone header (FF FF FF FF markers).
        /// </summary>
        private bool DetectZoneHeader(string filePath)
        {
            try
            {
                byte[] header = new byte[12];
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    if (fs.Length < 12)
                        return false;
                    fs.Read(header, 0, 12);
                }

                // Check for first marker (FF FF FF FF) at offset 0
                bool hasFirstMarker = header[0] == 0xFF && header[1] == 0xFF &&
                                       header[2] == 0xFF && header[3] == 0xFF;

                // Check for second marker (FF FF FF FF) at offset 8
                bool hasSecondMarker = header[8] == 0xFF && header[9] == 0xFF &&
                                        header[10] == 0xFF && header[11] == 0xFF;

                return hasFirstMarker && hasSecondMarker;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Prompts the user to enter a game path for a plain file.
        /// Automatically converts filename conventions like "maps__load.gsc" to "maps/_load.gsc"
        /// where "__" represents "/" in the game path.
        /// </summary>
        private string PromptForGamePath(string defaultFileName)
        {
            // Convert filename to game path by replacing "__" with "/"
            // e.g., "maps__load.gsc" -> "maps/_load.gsc"
            // e.g., "maps_mp_gametypes__dm.gsc" -> "maps/mp/gametypes/_dm.gsc"
            string suggestedPath = ConvertFileNameToGamePath(defaultFileName);

            using (var dialog = new RenameDialog(suggestedPath))
            {
                dialog.Text = "Enter Game Path";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    return dialog.NewFileName;
                }
                return null;
            }
        }

        /// <summary>
        /// Converts a filename with underscore conventions to a game path.
        /// - Single underscore followed by another underscore ("__") becomes "/_" (underscore file/folder name)
        /// - Single underscore at word boundary becomes "/" (path separator)
        /// Examples:
        /// - "maps__load.gsc" -> "maps/_load.gsc"
        /// - "maps_mp_gametypes_dm.gsc" -> "maps/mp/gametypes/dm.gsc"
        /// </summary>
        private string ConvertFileNameToGamePath(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return fileName;

            // Replace "__" with a placeholder first (for underscore-prefixed names like "_load")
            const string placeholder = "\x00UNDERSCORE\x00";
            string result = fileName.Replace("__", placeholder);

            // Replace remaining single underscores with forward slashes
            result = result.Replace("_", "/");

            // Restore the underscore-prefixed names
            result = result.Replace(placeholder, "/_");

            return result;
        }

        /// <summary>
        /// Exports the selected file, including its header information, to a chosen location.
        /// This format can be re-injected back into a FastFile.
        /// </summary>
        private void exportFileMenuItem_Click(object sender, EventArgs e)
        {
            RawFileNode selectedNode = GetSelectedRawFileNode();
            if (selectedNode == null)
                return;

            string fileExtension = Path.GetExtension(selectedNode.FileName);
            _rawFileService.ExportRawFile(selectedNode, fileExtension);
        }

        /// <summary>
        /// Exports only the content of the selected file without zone header.
        /// This is a clean script file for external editing.
        /// </summary>
        private void exportContentOnlyMenuItem_Click(object sender, EventArgs e)
        {
            RawFileNode selectedNode = GetSelectedRawFileNode();
            if (selectedNode == null)
                return;

            string fileExtension = Path.GetExtension(selectedNode.FileName);
            _rawFileService.ExportRawFileContentOnly(selectedNode, fileExtension);
        }

        /// <summary>
        /// Close the opened fast file, clear the tree view, and reset the form.
        /// Recompresses the zone file back into the fast file. (saves changes)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void closeFastFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveCloseFastFileAndCleanUp(!keepZoneFileToolStripMenuItem.Checked);
        }

        private void renameFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PerformRawFileRename();
        }

        /// <summary>
        /// Handles the Edit menu Rename Raw File click.
        /// </summary>
        private void renameRawFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PerformRawFileRename();
        }

        /// <summary>
        /// Performs the raw file rename operation.
        /// Used by both the Edit menu and context menu rename options.
        /// </summary>
        private void PerformRawFileRename()
        {
            RawFileNode selectedNode = GetSelectedRawFileNode();
            if (selectedNode == null)
                return;

            _rawFileService.RenameRawFile(filesTreeView, _openedFastFile.FfFilePath, _openedFastFile.ZoneFilePath, _rawFileNodes, _openedFastFile);
            ReloadAllRawFileNodesAndUI();
        }

        /// <summary>
        /// Populates the DataGridView with Zone decimal values.
        /// </summary>
        private void LoadZoneHeaderValues(ZoneFile zone)
        {
            if (zone == null || zone.HeaderFieldValues == null)
            {
                _openedFastFile.OpenedFastFileZone.ReadHeaderFields();
            }

            // Convert the dictionary to a list of objects with matching property names
            var dataSource = zone.HeaderFieldValues.Select(kvp => new
            {
                ZoneName = kvp.Key,
                ZoneDecValue = kvp.Value,
                ZoneHexValue = Utilities.ConvertToBigEndianHex(kvp.Value),
                ZoneOffset = _openedFastFile.OpenedFastFileZone.GetZoneOffset(kvp.Key)
            }).ToList();

            // Assign the data source to the DataGridView
            zoneInfoDataGridView.DataSource = dataSource;
        }

        /// <summary>
        /// Populates the Tags page view with the tags extracted from the zone file.
        /// </summary>
        private void PopulateTags()
        {
            // Fetch the results
            _tags = TagOperations.FindTags(_openedFastFile.OpenedFastFileZone);

            if (_tags == null)
                return;

            tagsListView.View = View.Details;
            tagsListView.Columns.Clear();
            tagsListView.Items.Clear();
            tagsListView.MultiSelect = true;
            tagsListView.FullRowSelect = true;
            tagsListView.Columns.Add("Tag (" + _tags.TagEntries.Count + ")", 100);
            tagsListView.Columns.Add("Offset", 100);

            // Sort the TagEntries by OffsetDec in ascending order
            var sortedTagEntries = _tags.TagEntries
                .OrderBy(entry => entry.OffsetDec)
                .ToList();

            // Now tagsInfo.TagEntries holds all entries
            foreach (var entry in _tags.TagEntries)
            {
                // 1) Tag
                // 2) Hex offset
                var lvi = new ListViewItem(entry.Tag);

                // Hex offset (for example "0x1AC4AC0")
                lvi.SubItems.Add("0x" + entry.OffsetHex);

                tagsListView.Items.Add(lvi);
            }

            tagsListView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
        }

        private void PopulateLocalizeAssets()
        {
            // Check if we have any localized entries in our processed results.
            if (_localizedEntries == null || _localizedEntries.Count <= 0)
            {
                mainTabControl.TabPages.Remove(localizeTabPage); // hide the tab page if there's no data to show
                return;
            }

            // Re-add the tab page if it was previously removed
            if (!mainTabControl.TabPages.Contains(localizeTabPage))
                mainTabControl.TabPages.Add(localizeTabPage);

            // Clear any existing items and columns.
            localizeListView.Items.Clear();
            localizeListView.Columns.Clear();

            // Set up the ListView.
            localizeListView.View = View.Details;
            localizeListView.FullRowSelect = true;
            localizeListView.GridLines = true;

            // Add the required columns with "Text" as the last column.
            localizeListView.Columns.Add("Key", 120);
            localizeListView.Columns.Add("Start Offset", 100);
            localizeListView.Columns.Add("End Offset", 100);
            localizeListView.Columns.Add("Size", 80);
            localizeListView.Columns.Add("Text", 300);

            // Loop through each localized entry.
            foreach (var entry in _localizedEntries)
            {
                // Calculate the size difference.
                int size = entry.EndOfFileHeader - entry.StartOfFileHeader;

                // Create a new ListViewItem with the Key as the main text.
                ListViewItem lvi = new ListViewItem(entry.Key);

                // Add subitems in the new order.
                lvi.SubItems.Add($"0x{entry.StartOfFileHeader:X}");
                lvi.SubItems.Add($"0x{entry.EndOfFileHeader:X}");
                lvi.SubItems.Add($"0x{size:X}");
                lvi.SubItems.Add(entry.LocalizedText);

                // Add the ListViewItem to the ListView.
                localizeListView.Items.Add(lvi);
            }

            // Auto-resize columns to fit header size.
            localizeListView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
        }

        private void PopulateMenuFiles()
        {
            // Check if we have any parsed MenuList assets
            bool hasMenuLists = _menuLists != null && _menuLists.Count > 0;

            if (!hasMenuLists)
            {
                mainTabControl.TabPages.Remove(menuFilesTabPage); // hide the tab page if there's no data to show
                return;
            }

            // Ensure the tab is shown
            if (!mainTabControl.TabPages.Contains(menuFilesTabPage))
            {
                mainTabControl.TabPages.Add(menuFilesTabPage);
            }

            // Clear existing items
            menuFilesTreeView.Nodes.Clear();
            menuFilesTextEditor.Text = string.Empty;
            _selectedMenuList = null;

            // Add selection handler if not already added
            menuFilesTreeView.AfterSelect -= MenuFilesTreeView_AfterSelect;
            menuFilesTreeView.AfterSelect += MenuFilesTreeView_AfterSelect;

            // Add text change handler for saving modifications
            menuFilesTextEditor.TextChanged -= MenuFilesTextEditor_TextChanged;
            menuFilesTextEditor.TextChanged += MenuFilesTextEditor_TextChanged;

            // Create decompiler for formatting menu data (PS3 = big endian)
            var decompiler = new ZoneParsers.MenuDecompiler(
                _openedFastFile.OpenedFastFileZone.Data,
                isBigEndian: true);

            // Populate the tree view with MenuLists and decompile each menu individually
            foreach (var menuList in _menuLists)
            {
                // Create MenuList node
                TreeNode menuListNode = new TreeNode($"{menuList.Name} ({menuList.Menus.Count} menus)");
                menuListNode.Tag = menuList;

                // Decompile each menu individually
                foreach (var menu in menuList.Menus)
                {
                    // Decompile this specific menu
                    var (formattedText, strings) = decompiler.DecompileMenuDef(menu);
                    menu.ExtractedStrings = strings;
                    menu.StringContent = formattedText;

                    string menuName = menu.Name ?? "(unnamed)";
                    string itemInfo = menu.ItemCount > 0 ? $" [{menu.ItemCount} items]" : "";
                    TreeNode menuNode = new TreeNode($"{menuName}{itemInfo}");
                    menuNode.Tag = menu;
                    menuListNode.Nodes.Add(menuNode);
                }

                menuFilesTreeView.Nodes.Add(menuListNode);
            }

            // Expand all nodes
            menuFilesTreeView.ExpandAll();

            // Select first item if available
            if (menuFilesTreeView.Nodes.Count > 0)
            {
                menuFilesTreeView.SelectedNode = menuFilesTreeView.Nodes[0];
            }
        }

        private void MenuFilesTreeView_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            // Save current menu content before switching
            if (_selectedMenuDef != null && _selectedMenuDef.HasUnsavedChanges)
            {
                _selectedMenuDef.StringContent = menuFilesTextEditor.Text;
            }

            if (e.Node?.Tag == null)
            {
                menuFilesTextEditor.TextChanged -= MenuFilesTextEditor_TextChanged;
                menuFilesTextEditor.Text = string.Empty;
                menuFilesTextEditor.TextChanged += MenuFilesTextEditor_TextChanged;
                _selectedMenuList = null;
                _selectedMenuDef = null;
                return;
            }

            // Detach handler while loading text
            menuFilesTextEditor.TextChanged -= MenuFilesTextEditor_TextChanged;

            if (e.Node.Tag is MenuList menuList)
            {
                _selectedMenuList = menuList;
                _selectedMenuDef = null;

                // For MenuList, show info about selecting a menu
                menuFilesTextEditor.Text = $"// {menuList.Name}\n// Select a menu from the list to view and edit its code.\n//\n// Menus in this file:\n" +
                    string.Join("\n", menuList.Menus.Select(m => $"//   - {m.Name}"));

                // Update status bar
                selectedItemStatusLabel.Text = $"MenuList: {menuList.Name}";
                selectedItemStatusLabel.Visible = true;
                selectedFileMaxSizeStatusLabel.Text = $"Menus: {menuList.Menus.Count}";
                selectedFileMaxSizeStatusLabel.Visible = true;
            }
            else if (e.Node.Tag is MenuDef menu)
            {
                // Find the parent MenuList
                if (e.Node.Parent?.Tag is MenuList parentList)
                {
                    _selectedMenuList = parentList;
                }

                _selectedMenuDef = menu;

                // Load only this menu's code
                menuFilesTextEditor.Text = menu.StringContent;

                // Update status bar
                selectedItemStatusLabel.Text = $"Menu: {menu.Name}";
                selectedItemStatusLabel.Visible = true;
                selectedFileMaxSizeStatusLabel.Text = $"Strings: {menu.ExtractedStrings?.Count ?? 0}";
                selectedFileMaxSizeStatusLabel.Visible = true;
            }

            // Reattach handler
            menuFilesTextEditor.TextChanged += MenuFilesTextEditor_TextChanged;
        }

        /// <summary>
        /// Handles text changes in the menu files text editor.
        /// </summary>
        private void MenuFilesTextEditor_TextChanged(object? sender, EventArgs e)
        {
            if (_selectedMenuDef == null)
                return;

            // Mark as having unsaved changes
            _selectedMenuDef.HasUnsavedChanges = true;
            _selectedMenuDef.StringContent = menuFilesTextEditor.Text;

            // Update the title to show unsaved changes
            if (!this.Text.EndsWith("*"))
            {
                this.Text += "*";
            }
        }

        /// <summary>
        /// Applies menu file string changes to the zone data.
        /// Modified strings are written back to their original offsets.
        /// Parses the formatted text to match strings regardless of formatting.
        /// </summary>
        private void ApplyMenuFileChangesToZone()
        {
            if (_menuLists == null || _openedFastFile?.OpenedFastFileZone?.Data == null)
                return;

            byte[] zoneData = _openedFastFile.OpenedFastFileZone.Data;
            bool isBigEndian = true; // PS3 is big-endian

            // Iterate over all menus in all menu lists
            foreach (var menuList in _menuLists)
            {
                foreach (var menu in menuList.Menus)
                {
                    if (!menu.HasUnsavedChanges)
                        continue;

                    // Parse and apply MenuValue changes (colors, rect, floats, etc.)
                    if (menu.EditableValues != null && menu.EditableValues.Count > 0)
                    {
                        var modifiedValues = ZoneParsers.MenuDecompiler.ParseEditedValues(
                            menu.StringContent, menu.EditableValues);

                        // Apply the float/color/rect changes to zone data
                        ZoneParsers.MenuDecompiler.ApplyMenuValueChanges(zoneData, modifiedValues, isBigEndian);
                    }

                    // Parse and apply string changes
                    if (menu.ExtractedStrings != null && menu.ExtractedStrings.Count > 0)
                    {
                        var stringChanges = ZoneParsers.MenuDecompiler.ParseEditedText(
                            menu.StringContent, menu.ExtractedStrings);

                        // Apply each string change to the zone data
                        foreach (var (original, newValue) in stringChanges)
                        {
                            if (string.IsNullOrEmpty(newValue))
                                continue;

                            // Write the new string to the zone data at the original offset
                            // Pad with nulls if shorter, truncate if longer
                            int maxLength = original.OriginalLength;
                            string valueToWrite = newValue.Length <= maxLength
                                ? newValue.PadRight(maxLength, '\0')
                                : newValue.Substring(0, maxLength);

                            byte[] newBytes = Encoding.ASCII.GetBytes(valueToWrite);

                            // Copy the new string bytes to the zone data
                            for (int j = 0; j < maxLength && original.Offset + j < zoneData.Length; j++)
                            {
                                zoneData[original.Offset + j] = j < newBytes.Length ? newBytes[j] : (byte)0;
                            }

                            original.Value = newValue;
                            original.IsModified = true;
                        }
                    }

                    // Reset the unsaved changes flag
                    menu.HasUnsavedChanges = false;
                }
            }
        }

        private void PopulateTechSets()
        {
            // Check if we have any techsets in our processed results.
            if (_techSets == null || _techSets.Count <= 0)
            {
                mainTabControl.TabPages.Remove(techSetsTabPage); // hide the tab page if there's no data to show
                return;
            }

            // Ensure the tab is shown
            if (!mainTabControl.TabPages.Contains(techSetsTabPage))
            {
                mainTabControl.TabPages.Add(techSetsTabPage);
            }

            // Clear any existing items and columns.
            techSetsListView.Items.Clear();
            techSetsListView.Columns.Clear();

            // Set up the ListView.
            techSetsListView.View = View.Details;
            techSetsListView.FullRowSelect = true;
            techSetsListView.GridLines = true;

            // Add the required columns.
            techSetsListView.Columns.Add("Name", 200);
            techSetsListView.Columns.Add("Active Techniques", 120);
            techSetsListView.Columns.Add("World Vert Format", 120);
            techSetsListView.Columns.Add("Start Offset", 100);
            techSetsListView.Columns.Add("End Offset", 100);
            techSetsListView.Columns.Add("Size", 80);
            techSetsListView.Columns.Add("Technique List", 400);

            // Loop through each techset.
            foreach (var techSet in _techSets)
            {
                // Create a new ListViewItem with the Name as the main text.
                ListViewItem lvi = new ListViewItem(techSet.Name);

                // Add subitems.
                lvi.SubItems.Add($"{techSet.ActiveTechniqueCount}/{TechSetAsset.TECHNIQUE_COUNT}");
                lvi.SubItems.Add($"0x{techSet.WorldVertFormat:X2}");
                lvi.SubItems.Add($"0x{techSet.StartOffset:X}");
                lvi.SubItems.Add($"0x{techSet.EndOffset:X}");
                int size = techSet.EndOffset - techSet.StartOffset;
                lvi.SubItems.Add($"0x{size:X}");

                // Build list of active technique names (show just the names for cleaner display)
                var activeTechniqueNames = new List<string>();
                if (techSet.Techniques != null)
                {
                    for (int i = 0; i < techSet.Techniques.Length; i++)
                    {
                        if (techSet.Techniques[i] != null && techSet.Techniques[i].IsPresent)
                        {
                            activeTechniqueNames.Add(techSet.Techniques[i].Name);
                        }
                    }
                }
                lvi.SubItems.Add(string.Join(", ", activeTechniqueNames));

                // Store the techset reference for potential selection handling
                lvi.Tag = techSet;

                // Add the ListViewItem to the ListView.
                techSetsListView.Items.Add(lvi);
            }

            // Auto-resize columns to fit header size.
            techSetsListView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
        }

        private void PopulateXAnims()
        {
            // Check if we have any xanims in our processed results.
            if (_xanims == null || _xanims.Count <= 0)
            {
                mainTabControl.TabPages.Remove(xAnimsTabPage); // hide the tab page if there's no data to show
                return;
            }

            // Ensure the tab is shown
            if (!mainTabControl.TabPages.Contains(xAnimsTabPage))
            {
                mainTabControl.TabPages.Add(xAnimsTabPage);
            }

            // Clear any existing items and columns.
            xAnimsListView.Items.Clear();
            xAnimsListView.Columns.Clear();

            // Set up the ListView.
            xAnimsListView.View = View.Details;
            xAnimsListView.FullRowSelect = true;
            xAnimsListView.GridLines = true;

            // Add the required columns.
            xAnimsListView.Columns.Add("Name", 250);
            xAnimsListView.Columns.Add("Frames", 70);
            xAnimsListView.Columns.Add("Framerate", 80);
            xAnimsListView.Columns.Add("Duration", 80);
            xAnimsListView.Columns.Add("Bones", 60);
            xAnimsListView.Columns.Add("Loops", 50);
            xAnimsListView.Columns.Add("Delta", 50);
            xAnimsListView.Columns.Add("Start Offset", 100);
            xAnimsListView.Columns.Add("End Offset", 100);
            xAnimsListView.Columns.Add("Size", 80);

            // Loop through each xanim.
            foreach (var xanim in _xanims)
            {
                // Create a new ListViewItem with the Name as the main text.
                ListViewItem lvi = new ListViewItem(xanim.Name);

                // Add subitems.
                lvi.SubItems.Add(xanim.NumFrames.ToString());
                lvi.SubItems.Add(float.IsNaN(xanim.Framerate) ? "N/A" : $"{xanim.Framerate:F1}");
                lvi.SubItems.Add(float.IsNaN(xanim.Duration) ? "N/A" : $"{xanim.Duration:F2}s");
                lvi.SubItems.Add(xanim.TotalBoneCount.ToString());
                lvi.SubItems.Add(xanim.IsLooping ? "Yes" : "No");
                lvi.SubItems.Add(xanim.HasDelta ? "Yes" : "No");
                lvi.SubItems.Add($"0x{xanim.StartOffset:X}");
                lvi.SubItems.Add($"0x{xanim.EndOffset:X}");
                int size = xanim.EndOffset - xanim.StartOffset;
                lvi.SubItems.Add($"0x{size:X}");

                // Store the xanim reference for potential selection handling
                lvi.Tag = xanim;

                // Add the ListViewItem to the ListView.
                xAnimsListView.Items.Add(lvi);
            }

            // Auto-resize columns to fit header size.
            xAnimsListView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
        }

        /// <summary>
        /// Exports the selected XAnim's raw binary data from the zone file.
        /// </summary>
        private void exportXAnimMenuItem_Click(object? sender, EventArgs e)
        {
            if (xAnimsListView.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select an XAnim to export.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedItem = xAnimsListView.SelectedItems[0];
            var xanim = selectedItem.Tag as XAnimParts;
            if (xanim == null)
            {
                MessageBox.Show("Could not get XAnim data.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Clean up name for filename
            string safeName = xanim.Name ?? "xanim";
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                safeName = safeName.Replace(c, '_');
            }

            using (SaveFileDialog saveDialog = new SaveFileDialog())
            {
                saveDialog.Title = "Export XAnim Data";
                saveDialog.Filter = "Binary Data (*.bin)|*.bin|XAnim Data (*.xanim)|*.xanim|All Files (*.*)|*.*";
                saveDialog.FileName = $"{safeName}.bin";

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        // Get the zone data
                        byte[] zoneData = _openedFastFile.OpenedFastFileZone.Data;
                        int startOffset = xanim.StartOffset;
                        int length = xanim.EndOffset - xanim.StartOffset;

                        if (startOffset < 0 || startOffset + length > zoneData.Length)
                        {
                            MessageBox.Show("Invalid XAnim data offsets.", "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                        byte[] xanimData = new byte[length];
                        Array.Copy(zoneData, startOffset, xanimData, 0, length);

                        File.WriteAllBytes(saveDialog.FileName, xanimData);

                        MessageBox.Show($"XAnim '{xanim.Name}' exported successfully.\n\n" +
                                       $"Size: {length} bytes (0x{length:X})\n" +
                                       $"Frames: {xanim.NumFrames}\n" +
                                       $"Framerate: {xanim.Framerate:F1} fps",
                                       "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to export XAnim: {ex.Message}", "Export Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        /// <summary>
        /// Populates the String Tables tab with parsed StringTable assets.
        /// </summary>
        private void PopulateStringTables()
        {
            // Check if we have any string tables in our processed results.
            if (_stringTables == null || _stringTables.Count <= 0)
            {
                mainTabControl.TabPages.Remove(stringTablesTabPage); // hide the tab page if there's no data to show
                return;
            }

            // Ensure the tab is shown
            if (!mainTabControl.TabPages.Contains(stringTablesTabPage))
            {
                mainTabControl.TabPages.Add(stringTablesTabPage);
            }

            // Clear any existing items and columns.
            stringTablesListView.Items.Clear();
            stringTablesListView.Columns.Clear();

            // Set up the ListView.
            stringTablesListView.View = View.Details;
            stringTablesListView.FullRowSelect = true;
            stringTablesListView.GridLines = true;

            // Add the required columns.
            stringTablesListView.Columns.Add("Table Name", 350);
            stringTablesListView.Columns.Add("Rows", 70);
            stringTablesListView.Columns.Add("Columns", 70);
            stringTablesListView.Columns.Add("Cells", 80);
            stringTablesListView.Columns.Add("Start Offset", 100);
            stringTablesListView.Columns.Add("End Offset", 100);

            // Loop through each string table.
            foreach (var table in _stringTables)
            {
                // Create a new ListViewItem with the TableName as the main text.
                ListViewItem lvi = new ListViewItem(table.TableName);

                // Add subitems.
                lvi.SubItems.Add(table.RowCount.ToString());
                lvi.SubItems.Add(table.ColumnCount.ToString());
                lvi.SubItems.Add((table.Cells?.Count ?? 0).ToString());
                lvi.SubItems.Add($"0x{table.StartOfFileHeader:X}");
                lvi.SubItems.Add($"0x{table.DataEndPosition:X}");

                // Store the string table reference for double-click handling
                lvi.Tag = table;

                // Add the ListViewItem to the ListView.
                stringTablesListView.Items.Add(lvi);
            }

            // Auto-resize columns to fit header size.
            stringTablesListView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
        }

        /// <summary>
        /// Opens the StringTableViewerForm when a string table is double-clicked.
        /// </summary>
        private void stringTablesListView_DoubleClick(object? sender, EventArgs e)
        {
            if (stringTablesListView.SelectedItems.Count == 0)
                return;

            var selectedItem = stringTablesListView.SelectedItems[0];
            var stringTable = selectedItem.Tag as StringTable;

            if (stringTable != null)
            {
                using (var viewer = new StringTableViewerForm(stringTable))
                {
                    viewer.ShowDialog(this);
                }
            }
        }

        private void PopulateCollision_Map_Asset_StringData()
        {
            int offset = Collision_Map_Operations.FindCollision_Map_DataOffsetViaFF(_openedFastFile.OpenedFastFileZone);
            if (offset == -1)
            {
                //MessageBox.Show("No map header found near large FF blocks.");
                mainTabControl.TabPages.Remove(collision_Map_AssetTabPage); // hide the tab page if there's no data to show
                return;
            }

            // Parse entities from that offset
            List<MapEntity> mapTest = Collision_Map_Operations.ParseMapEntsAtOffset(_openedFastFile.OpenedFastFileZone, offset);

            if (mapTest.Count == 0)
            {
                MessageBox.Show("Found an offset, but parsing yielded no entities.", "Empty",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Sort by "classname"
            mapTest.Sort((entA, entB) =>
            {
                entA.Properties.TryGetValue("classname", out string aClass);
                entB.Properties.TryGetValue("classname", out string bClass);
                aClass ??= "";
                bClass ??= "";
                return string.Compare(aClass, bClass, StringComparison.OrdinalIgnoreCase);
            });

            // Clear the TreeView
            treeViewMapEnt.Nodes.Clear();

            // Attempt to get both the map size and its offset
            var sizeInfo = Collision_Map_Operations.GetMapDataSizeAndOffset(_openedFastFile.OpenedFastFileZone, mapTest);
            if (sizeInfo.HasValue)
            {
                // Destructure the tuple
                (int mapSize, int offsetOfSize) = sizeInfo.Value;

                // Create a top-level node for the map size
                TreeNode sizeNode = new TreeNode($"Map Data Size = {mapSize} bytes (dec)");
                treeViewMapEnt.Nodes.Add(sizeNode);

                // Create another node for where that size is stored (in hex)
                string sizeOffsetHex = offsetOfSize.ToString("X");
                TreeNode sizeOffsetNode = new TreeNode($"Map Size AssetPoolRecordOffset = 0x{sizeOffsetHex}");
                treeViewMapEnt.Nodes.Add(sizeOffsetNode);
            }
            else
            {
                // If we can't find the size, optionally show a node or just skip
                treeViewMapEnt.Nodes.Add("Could not detect map size.");
            }

            // Now add each entity
            for (int i = 0; i < mapTest.Count; i++)
            {
                MapEntity entity = mapTest[i];

                // Parent label from "classname" or fallback
                string parentLabel = entity.Properties.TryGetValue("classname", out string classNameVal)
                    ? classNameVal
                    : $"Entity {i}";

                TreeNode parentNode = new TreeNode(parentLabel);

                // Show offset in hex
                string offsetHex = entity.SourceOffset.ToString("X");
                parentNode.Nodes.Add($"AssetPoolRecordOffset = 0x{offsetHex}");

                // Then the key-value pairs
                foreach (var kvp in entity.Properties)
                {
                    if (!kvp.Key.Equals("classname", StringComparison.OrdinalIgnoreCase))
                    {
                        parentNode.Nodes.Add($"{kvp.Key} = {kvp.Value}");
                    }
                }

                treeViewMapEnt.Nodes.Add(parentNode);
            }
        }

        private void listView_MouseDownCopy(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var lv = (ListView)sender;
                ListViewHitTestInfo hit = lv.HitTest(e.Location);
                if (hit.Item != null)
                {
                    _rightClickedItemText = hit.SubItem.Text;
                }
                else
                {
                    _rightClickedItemText = string.Empty;
                }
            }
        }

        private void treeView_MouseDownCopy(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var tv = (TreeView)sender;
                TreeNode node = tv.GetNodeAt(e.X, e.Y);
                if (node != null)
                {
                    tv.SelectedNode = node;
                    _rightClickedItemText = node.Text;
                }
                else
                {
                    _rightClickedItemText = string.Empty;
                }
            }
        }

        private void dataGrid_MouseDownCopy(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var dgv = (DataGridView)sender;
                // Get row/column of the clicked cell
                DataGridView.HitTestInfo hit = dgv.HitTest(e.X, e.Y);

                if (hit.RowIndex >= 0 && hit.ColumnIndex >= 0)
                {
                    // Optionally select the clicked row/cell
                    dgv.ClearSelection();
                    dgv.Rows[hit.RowIndex].Selected = true;
                    dgv.CurrentCell = dgv[hit.ColumnIndex, hit.RowIndex];

                    // Store the cell's value in our right-clicked text
                    object cellValue = dgv[hit.ColumnIndex, hit.RowIndex].Value;
                    _rightClickedItemText = cellValue?.ToString() ?? string.Empty;
                }
                else
                {
                    // Right-clicked outside a valid cell
                    _rightClickedItemText = string.Empty;
                }
            }
        }

        private void SaveCloseFastFileAndCleanUp(bool deleteZoneFile = false)
        {
            try
            {
                if (_openedFastFile != null && File.Exists(_openedFastFile.FfFilePath))
                {
                    // Store the path before we null it out
                    string savedPath = _openedFastFile.FfFilePath;

                    // If no changes were made, just close without saving
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] SaveCloseFastFileAndCleanUp: _hasUnsavedChanges = {_hasUnsavedChanges}");
                    if (!_hasUnsavedChanges)
                    {
                        System.Diagnostics.Debug.WriteLine("[DEBUG] No unsaved changes, closing without saving");
                        if (deleteZoneFile)
                        {
                            try { File.Delete(_openedFastFile.ZoneFilePath); }
                            catch { }
                        }
                        ResetAllViews();
                        _openedFastFile = null;
                        return;
                    }

                    bool useInPlacePatching = false;
                    bool proceedWithSave = true;

                    // Determine save strategy based on unsupported assets and changes
                    if (_hasUnsupportedAssets)
                    {
                        if (CanPatchLocalizeInPlace())
                        {
                            // Safe to patch in place - preserves unsupported assets
                            useInPlacePatching = true;
                        }
                        else
                        {
                            // Need full rebuild but will lose unsupported assets - warn user
                            var unsupportedTypes = ZoneFileBuilder.GetUnsupportedAssetInfo(
                                _openedFastFile.OpenedFastFileZone, _openedFastFile);
                            var typeList = string.Join(", ", unsupportedTypes.Distinct().Take(5));
                            if (unsupportedTypes.Count > 5) typeList += ", ...";

                            var result = MessageBox.Show(
                                $"This zone contains unsupported asset types ({typeList}).\n\n" +
                                $"Your changes require a full zone rebuild, which will REMOVE these unsupported assets.\n\n" +
                                $"Do you want to continue saving?",
                                "Warning: Unsupported Assets Will Be Lost",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Warning);

                            proceedWithSave = (result == DialogResult.Yes);
                        }
                    }

                    if (!proceedWithSave)
                        return;

                    // Apply changes to zone file
                    bool saveSuccess;
                    if (useInPlacePatching)
                    {
                        // Patch localize entries in place (preserves unsupported assets)
                        saveSuccess = PatchLocalizeEntriesInPlace();
                    }
                    else
                    {
                        // Full zone rebuild
                        saveSuccess = RebuildZoneWithCurrentData();
                    }

                    if (!saveSuccess)
                    {
                        MessageBox.Show("Failed to save zone file changes; FastFile was not saved.", "Save Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    // Recompress zone -> ff
                    _fastFileHandler?.Recompress(_openedFastFile.FfFilePath, _openedFastFile.ZoneFilePath, _openedFastFile);

                    // Clear the dirty flag after successful save
                    _hasUnsavedChanges = false;
                    ResetAllViews();

                    if (deleteZoneFile)
                    {
                        try { File.Delete(_openedFastFile.ZoneFilePath); }
                        catch { }
                    }

                    _openedFastFile = null;
                    string patchNote = useInPlacePatching ? " (in-place patch)" : "";
                    MessageBox.Show($"Fast File Saved & Closed{patchNote}.\n\nSaved to:\n{savedPath}", "Close Complete",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to close fastfile: {ex.Message}",
                                "Close Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
            }
        }

        private bool RebuildZoneWithCurrentData()
        {
            if (_openedFastFile == null)
                return false;

            // Allow rebuilding with just localize entries (raw files can be empty)
            var rawFiles = _rawFileNodes ?? new List<RawFileNode>();
            var localizeEntries = _localizedEntries ?? new List<LocalizedEntry>();

            // Need at least some content to rebuild
            if (rawFiles.Count == 0 && localizeEntries.Count == 0)
                return false;

            // Debug: log what we're about to build
            System.Diagnostics.Debug.WriteLine($"[RebuildZone] Building with {rawFiles.Count} rawfiles, {localizeEntries.Count} localize entries");
            foreach (var le in localizeEntries.Take(5))
            {
                System.Diagnostics.Debug.WriteLine($"[RebuildZone]   Entry: {le.Key}, TextLen={le.TextBytes?.Length ?? -1}, Text='{le.LocalizedText?.Substring(0, Math.Min(30, le.LocalizedText?.Length ?? 0))}'");
            }

            var newZoneData = ZoneFileBuilder.BuildFreshZone(
                rawFiles,
                localizeEntries,
                _openedFastFile,
                Path.GetFileNameWithoutExtension(_openedFastFile.FastFileName));

            if (newZoneData == null)
                return false;

            // Update the in-memory zone data so subsequent save logic uses the new data
            _openedFastFile.OpenedFastFileZone.Data = newZoneData;
            File.WriteAllBytes(_openedFastFile.ZoneFilePath, newZoneData);
            return true;
        }

        /// <summary>
        /// Checks if all localize edits can be patched in place (no size increases, no new entries).
        /// </summary>
        private bool CanPatchLocalizeInPlace()
        {
            if (_localizedEntries == null || _openedFastFile?.OpenedFastFileZone?.Data == null)
            {
                System.Diagnostics.Debug.WriteLine($"[CanPatch] null check failed: entries={_localizedEntries != null}, zoneData={_openedFastFile?.OpenedFastFileZone?.Data != null}");
                return false;
            }

            // If new entries were added, can't patch in place
            if (_localizedEntries.Count > _originalLocalizeCount)
            {
                System.Diagnostics.Debug.WriteLine($"[CanPatch] new entries added: current={_localizedEntries.Count}, original={_originalLocalizeCount}");
                return false;
            }

            // Check each entry - if any has increased in size, can't patch
            foreach (var entry in _localizedEntries)
            {
                // New entries (StartOfFileHeader == 0) can't be patched
                if (entry.StartOfFileHeader == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[CanPatch] entry has StartOfFileHeader=0: {entry.Key}");
                    return false;
                }

                // For Case B entries (empty text), there's no text field - nothing to check
                int textLen = entry.TextBytes?.Length ?? 0;
                if (textLen == 0)
                    continue; // Case B entries don't need size check - key stays in place

                // For Case A entries: check if new text + null fits before the key
                // Available space for text = KeyStartOffset - (StartOfFileHeader + 8)
                int textAreaSize = entry.KeyStartOffset - entry.StartOfFileHeader - 8;
                int newTextSize = textLen + 1; // text + null terminator

                if (newTextSize > textAreaSize)
                {
                    System.Diagnostics.Debug.WriteLine($"[CanPatch] text too large: {entry.Key}, available={textAreaSize}, new={newTextSize}");
                    return false;
                }
            }

            System.Diagnostics.Debug.WriteLine($"[CanPatch] all checks passed, returning true");
            return true;
        }

        /// <summary>
        /// Patches localize entries in place within the existing zone data.
        /// Only works when new data fits within original space.
        /// </summary>
        private bool PatchLocalizeEntriesInPlace()
        {
            if (_openedFastFile?.OpenedFastFileZone?.Data == null || _localizedEntries == null)
                return false;

            byte[] zoneData = _openedFastFile.OpenedFastFileZone.Data;

            foreach (var entry in _localizedEntries)
            {
                if (entry.StartOfFileHeader == 0)
                    continue; // Skip new entries (shouldn't happen if CanPatchLocalizeInPlace was checked)

                int textStart = entry.StartOfFileHeader + 8; // After the FF marker
                byte[] textBytes = entry.TextBytes ?? Array.Empty<byte>();

                // Case B (empty text): Key stays at its original position, nothing to patch
                if (textBytes.Length == 0)
                    continue;

                // Case A (has text): Write new text, pad with nulls until key position
                // The key stays at its original position (KeyStartOffset) - we don't touch it
                int pos = textStart;

                // Write new text bytes
                for (int i = 0; i < textBytes.Length && pos < zoneData.Length; i++, pos++)
                    zoneData[pos] = textBytes[i];

                // Pad with spaces from end of new text up to where the key starts (minus 1 for null terminator)
                // Using spaces (0x20) instead of nulls to avoid parser issues
                while (pos < entry.KeyStartOffset - 1 && pos < zoneData.Length)
                    zoneData[pos++] = 0x20; // Space character

                // Write text null terminator right before the key
                if (pos < zoneData.Length)
                    zoneData[pos++] = 0x00;

                // Key remains untouched at its original position
            }

            // Write the patched zone data back to file
            File.WriteAllBytes(_openedFastFile.ZoneFilePath, zoneData);
            return true;
        }

        /// <summary>
        /// Determines if a full zone rebuild is needed (vs in-place patching).
        /// </summary>
        private bool NeedsFullRebuild()
        {
            // If no unsupported assets, always safe to rebuild
            if (!_hasUnsupportedAssets)
                return true; // Use rebuild (it's safe)

            // If can patch in place, don't need rebuild
            if (CanPatchLocalizeInPlace())
                return false;

            // Need rebuild but have unsupported assets - caller should warn
            return true;
        }

        /// <summary>
        /// Opens a Form to search for text throughout all of the raw files
        /// </summary>
        private void searchRawFileTxtMenuItem_Click(object sender, EventArgs e)
        {
            if (_rawFileNodes?.Count > 0)
                new RawFileSearcherForm(_rawFileNodes).Show();
            else
                MessageBox.Show("No raw files found to search through.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void exportLocalizeToTxtMenuItem_Click(object sender, EventArgs e)
        {
            if (_localizedEntries == null || _localizedEntries.Count == 0)
            {
                MessageBox.Show("No localize entries are loaded.", "Export Localize", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string defaultName = _openedFastFile != null
                ? $"{Path.GetFileNameWithoutExtension(_openedFastFile.FastFileName)}_localize.txt"
                : "localize.txt";

            using SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                FileName = defaultName
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            var lines = new List<string>
            {
                "# Format: key<TAB>text (one entry per line)",
                "# Escape sequences: \\t = tab, \\n = newline, \\r = carriage return, \\\\ = backslash"
            };

            foreach (var entry in _localizedEntries)
            {
                string key = EscapeLocalizeValue(entry.Key ?? string.Empty);
                string text = EscapeLocalizeValue(entry.LocalizedText ?? string.Empty);
                lines.Add($"{key}\t{text}");
            }

            File.WriteAllLines(dialog.FileName, lines, Encoding.UTF8);
            MessageBox.Show($"Exported {_localizedEntries.Count} entries to:\n{dialog.FileName}", "Export Localize", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void importLocalizeFromTxtMenuItem_Click(object sender, EventArgs e)
        {
            if (_processResult == null)
            {
                MessageBox.Show("No FastFile is currently loaded.", "Import Localize", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            if (_localizedEntries == null)
                _localizedEntries = new List<LocalizedEntry>();

            var lines = File.ReadAllLines(dialog.FileName, Encoding.UTF8);
            var existingKeys = new HashSet<string>(
                _localizedEntries.Where(e => !string.IsNullOrWhiteSpace(e.Key)).Select(e => e.Key),
                StringComparer.OrdinalIgnoreCase);

            // First pass: count how many new entries would be added
            int wouldAdd = 0;
            foreach (var rawLine in lines)
            {
                if (string.IsNullOrWhiteSpace(rawLine)) continue;
                var trimmed = rawLine.TrimStart();
                if (trimmed.StartsWith("#")) continue;
                var parts = rawLine.Split('\t', 2);
                if (parts.Length < 2) continue;
                string key = UnescapeLocalizeValue(parts[0].Trim());
                if (string.IsNullOrWhiteSpace(key)) continue;
                if (!existingKeys.Contains(key)) wouldAdd++;
            }

            // Warn if adding new entries to a zone with unsupported assets
            if (wouldAdd > 0 && _hasUnsupportedAssets)
            {
                var unsupportedTypes = ZoneFileBuilder.GetUnsupportedAssetInfo(
                    _openedFastFile.OpenedFastFileZone, _openedFastFile);
                var typeList = string.Join(", ", unsupportedTypes.Distinct().Take(5));
                if (unsupportedTypes.Count > 5) typeList += ", ...";

                var result = MessageBox.Show(
                    $"This import would add {wouldAdd} new localize entries.\n\n" +
                    $"WARNING: This zone contains unsupported asset types ({typeList}).\n" +
                    $"Saving after this import will require a zone rebuild, which will REMOVE these unsupported assets.\n\n" +
                    $"Do you want to continue?",
                    "Warning: Unsupported Assets",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result != DialogResult.Yes)
                    return;
            }

            var map = _localizedEntries
                .Where(e => !string.IsNullOrWhiteSpace(e.Key))
                .ToDictionary(e => e.Key, StringComparer.OrdinalIgnoreCase);

            int updated = 0, added = 0, skipped = 0;

            foreach (var rawLine in lines)
            {
                if (string.IsNullOrWhiteSpace(rawLine))
                    continue;

                var trimmed = rawLine.TrimStart();
                if (trimmed.StartsWith("#"))
                    continue;

                var parts = rawLine.Split('\t', 2);
                if (parts.Length < 2)
                {
                    skipped++;
                    continue;
                }

                string key = UnescapeLocalizeValue(parts[0].Trim());
                string text = UnescapeLocalizeValue(parts[1]);

                if (string.IsNullOrWhiteSpace(key))
                {
                    skipped++;
                    continue;
                }

                if (map.TryGetValue(key, out var existing))
                {
                    existing.LocalizedText = text;
                    updated++;
                }
                else
                {
                    var newEntry = new LocalizedEntry
                    {
                        Key = key,
                        LocalizedText = text,
                        StartOfFileHeader = 0,
                        EndOfFileHeader = 0,
                        StartOfFileData = 0,
                        EndOfFileData = 0
                    };
                    _localizedEntries.Add(newEntry);
                    map[key] = newEntry;
                    added++;
                }
            }

            _processResult.LocalizedEntries = _localizedEntries;
            PopulateLocalizeAssets();
            localizeToolsMenuItem.Enabled = _localizedEntries.Count > 0;

            // Mark as modified if any entries were changed
            if (updated > 0 || added > 0)
            {
                _hasUnsavedChanges = true;
                _localizeNeedsRebuild = true; // Force full zone rebuild after import
            }

            MessageBox.Show($"Import complete.\nUpdated: {updated}\nAdded: {added}\nSkipped: {skipped}", "Import Localize", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void localizeListView_DoubleClick(object sender, EventArgs e)
        {
            if (_localizedEntries == null || _localizedEntries.Count == 0)
                return;
            if (localizeListView.SelectedItems.Count == 0)
                return;

            int index = localizeListView.SelectedItems[0].Index;
            if (index < 0 || index >= _localizedEntries.Count)
                return;

            var entry = _localizedEntries[index];
            string currentKey = entry.Key ?? "(no key)";
            string currentText = entry.LocalizedText ?? string.Empty;

            string? newText = PromptForLocalizeEdit(currentKey, currentText);
            if (newText == null)
                return;

            entry.LocalizedText = newText;
            UpdateLocalizeListViewRow(index, entry);
            _processResult.LocalizedEntries = _localizedEntries;
            localizeToolsMenuItem.Enabled = _localizedEntries.Count > 0;
            _hasUnsavedChanges = true; // Mark as modified
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Localize entry modified: {entry.Key}");
            System.Diagnostics.Debug.WriteLine($"[DEBUG]   NewText='{newText}'");
            System.Diagnostics.Debug.WriteLine($"[DEBUG]   TextBytes.Length={entry.TextBytes?.Length ?? -1}");
            System.Diagnostics.Debug.WriteLine($"[DEBUG]   LocalizedText='{entry.LocalizedText}'");
        }

        private static string? PromptForLocalizeEdit(string key, string currentText)
        {
            int originalLength = currentText?.Length ?? 0;

            using var form = new Form
            {
                Text = $"Edit Localize: {key}",
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                MaximizeBox = false,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                ClientSize = new Size(640, 360)
            };

            var label = new Label
            {
                Text = $"Key: {key}",
                AutoSize = true,
                Location = new Point(10, 10)
            };

            var charCountLabel = new Label
            {
                AutoSize = true,
                Location = new Point(10, 310)
            };

            var textbox = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(10, 35),
                Size = new Size(620, 270),
                Text = currentText
            };

            // Update character count label
            void UpdateCharCount()
            {
                int current = textbox.Text?.Length ?? 0;
                string status = current > originalLength ? " (will rebuild zone)" :
                               current < originalLength ? " (will pad with spaces)" : "";
                charCountLabel.Text = $"Characters: {current} / Original: {originalLength}{status}";
                charCountLabel.ForeColor = current > originalLength ? Color.OrangeRed : SystemColors.ControlText;
            }

            textbox.TextChanged += (s, e) => UpdateCharCount();
            UpdateCharCount(); // Initial update

            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(455, 320), Width = 80 };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(550, 320), Width = 80 };

            form.Controls.Add(label);
            form.Controls.Add(charCountLabel);
            form.Controls.Add(textbox);
            form.Controls.Add(ok);
            form.Controls.Add(cancel);
            form.AcceptButton = ok;
            form.CancelButton = cancel;

            return form.ShowDialog() == DialogResult.OK ? textbox.Text : null;
        }

        private void UpdateLocalizeListViewRow(int index, LocalizedEntry entry)
        {
            if (index < 0 || index >= localizeListView.Items.Count)
                return;

            var item = localizeListView.Items[index];
            if (item.SubItems.Count < 5)
                return;

            item.SubItems[0].Text = entry.Key ?? "-";
            item.SubItems[4].Text = entry.LocalizedText ?? string.Empty;
        }

        private static string EscapeLocalizeValue(string value)
        {
            return value.Replace("\\", "\\\\")
                        .Replace("\t", "\\t")
                        .Replace("\r", "\\r")
                        .Replace("\n", "\\n");
        }

        private static string UnescapeLocalizeValue(string value)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\\' && i + 1 < value.Length)
                {
                    char next = value[++i];
                    switch (next)
                    {
                        case 't': sb.Append('\t'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case '\\': sb.Append('\\'); break;
                        default:
                            sb.Append('\\').Append(next);
                            break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Populates the Zone Asset Pool list view from the _zoneAssetRecords.
        /// Shows ALL assets from the asset pool, with parsed data where available.
        /// </summary>
        private void LoadAssetPoolIntoListView()
        {
            // Make sure we have valid data
            if (_openedFastFile == null ||
                _openedFastFile.OpenedFastFileZone == null ||
                _zoneAssetRecords == null)
            {
                return;
            }

            // Clear existing items and columns
            assetPoolListView.Items.Clear();
            assetPoolListView.Columns.Clear();

            // Use "Details" view with full-row select
            assetPoolListView.View = View.Details;
            assetPoolListView.FullRowSelect = true;
            assetPoolListView.GridLines = true;

            // Get game definition for asset type names
            var gameDefinition = GameDefinitions.GameDefinitionFactory.GetDefinition(_openedFastFile);

            // Count total parsed assets
            int totalParsed = (_rawFileNodes?.Count ?? 0) + (_localizedEntries?.Count ?? 0) +
                              (_menuLists?.Count ?? 0) + (_techSets?.Count ?? 0) + (_xanims?.Count ?? 0);
            int totalAssets = _zoneAssetRecords.Count;

            // Columns for the list view
            assetPoolListView.Columns.Add($"Index", 60);
            assetPoolListView.Columns.Add("Asset Type", 120);
            assetPoolListView.Columns.Add("Pool Offset", 90);
            assetPoolListView.Columns.Add("Data Start", 90);
            assetPoolListView.Columns.Add("Data End", 90);
            assetPoolListView.Columns.Add("Size", 70);
            assetPoolListView.Columns.Add("Name", 250);
            assetPoolListView.Columns.Add("Status", 200);

            // Place the asset pool header info at the top
            var pool = new ListViewItem("--");
            pool.SubItems.Add("ASSET POOL");
            pool.SubItems.Add($"0x{_assetPoolStartOffset:X}");
            pool.SubItems.Add($"0x{_assetPoolEndOffset:X}");
            pool.SubItems.Add("");
            pool.SubItems.Add($"0x{(_assetPoolEndOffset - _assetPoolStartOffset):X}");
            pool.SubItems.Add($"Total: {totalAssets} assets, {totalParsed} parsed");
            pool.SubItems.Add("");
            pool.Font = new System.Drawing.Font(assetPoolListView.Font, System.Drawing.FontStyle.Bold);
            assetPoolListView.Items.Add(pool);

            // Create lookup sets for quick matching against parsed data
            int rawFileIndex = 0;
            int localizeIndex = 0;
            int menuIndex = 0;
            int techSetIndex = 0;
            int xanimIndex = 0;

            // Iterate through ALL asset records from the asset pool
            for (int i = 0; i < _zoneAssetRecords.Count; i++)
            {
                var record = _zoneAssetRecords[i];

                // Get the asset type based on game
                int assetTypeValue = GetAssetTypeValue(record);
                string assetTypeName = gameDefinition.GetAssetTypeName(assetTypeValue);
                bool isRawFile = gameDefinition.IsRawFileType(assetTypeValue);
                bool isLocalize = gameDefinition.IsLocalizeType(assetTypeValue);
                bool isMenuFile = gameDefinition.IsMenuFileType(assetTypeValue);
                bool isTechSet = gameDefinition.IsTechSetType(assetTypeValue);
                bool isXAnim = gameDefinition.IsXAnimType(assetTypeValue);

                var lvi = new ListViewItem((i + 1).ToString());
                lvi.SubItems.Add(assetTypeName);
                lvi.SubItems.Add($"0x{record.AssetPoolRecordOffset:X}");

                // Check if this record has been parsed by matching against parsed lists
                bool isParsed = false;
                string name = "-";
                string dataStart = "-";
                string dataEnd = "-";
                string size = "-";
                string status = "Not parsed (unsupported type)";

                if (isRawFile && _rawFileNodes != null && rawFileIndex < _rawFileNodes.Count)
                {
                    var node = _rawFileNodes[rawFileIndex];
                    isParsed = true;
                    name = node.FileName ?? "-";
                    dataStart = $"0x{node.CodeStartPosition:X}";
                    dataEnd = $"0x{node.CodeEndPosition:X}";
                    size = $"0x{node.MaxSize:X}";
                    status = node.AdditionalData ?? "Parsed";
                    rawFileIndex++;
                }
                else if (isLocalize && _localizedEntries != null && localizeIndex < _localizedEntries.Count)
                {
                    var entry = _localizedEntries[localizeIndex];
                    isParsed = true;
                    name = entry.Key ?? "-";
                    dataStart = $"0x{entry.StartOfFileHeader:X}";
                    dataEnd = $"0x{entry.EndOfFileHeader:X}";
                    int entrySize = entry.EndOfFileHeader - entry.StartOfFileHeader;
                    size = $"0x{entrySize:X}";
                    status = "Localized asset parsed";
                    localizeIndex++;
                }
                else if (isMenuFile && _menuLists != null && menuIndex < _menuLists.Count)
                {
                    var menu = _menuLists[menuIndex];
                    isParsed = true;
                    name = menu.Name ?? "-";
                    dataStart = $"0x{menu.DataStartOffset:X}";
                    dataEnd = $"0x{menu.DataEndOffset:X}";
                    int menuSize = menu.DataEndOffset - menu.DataStartOffset;
                    size = $"0x{menuSize:X}";
                    status = $"MenuList parsed ({menu.MenuCount} menus)";
                    menuIndex++;
                }
                else if (isTechSet && _techSets != null && techSetIndex < _techSets.Count)
                {
                    var techSet = _techSets[techSetIndex];
                    isParsed = true;
                    name = techSet.Name ?? "-";
                    dataStart = $"0x{techSet.StartOffset:X}";
                    dataEnd = $"0x{techSet.EndOffset:X}";
                    int techSetSize = techSet.EndOffset - techSet.StartOffset;
                    size = $"0x{techSetSize:X}";
                    status = $"TechSet parsed ({techSet.ActiveTechniqueCount} techniques)";
                    techSetIndex++;
                }
                else if (isXAnim && _xanims != null && xanimIndex < _xanims.Count)
                {
                    var xanim = _xanims[xanimIndex];
                    isParsed = true;
                    name = xanim.Name ?? "-";
                    dataStart = $"0x{xanim.StartOffset:X}";
                    dataEnd = $"0x{xanim.EndOffset:X}";
                    int xanimSize = xanim.EndOffset - xanim.StartOffset;
                    size = $"0x{xanimSize:X}";
                    status = $"XAnim parsed ({xanim.GetSummary()})";
                    xanimIndex++;
                }

                lvi.SubItems.Add(dataStart);
                lvi.SubItems.Add(dataEnd);
                lvi.SubItems.Add(size);
                lvi.SubItems.Add(name);
                lvi.SubItems.Add(status);

                if (!isParsed)
                {
                    lvi.ForeColor = System.Drawing.Color.Gray;
                }

                assetPoolListView.Items.Add(lvi);
            }

            // Auto-resize columns to fit header size or content
            assetPoolListView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
        }

        /// <summary>
        /// Gets the asset type value from a record based on the current game.
        /// </summary>
        private int GetAssetTypeValue(Models.ZoneAssetRecord record)
        {
            if (_openedFastFile.IsCod4File)
                return (int)record.AssetType_COD4;
            if (_openedFastFile.IsCod5File)
                return (int)record.AssetType_COD5;
            if (_openedFastFile.IsMW2File)
                return (int)record.AssetType_MW2;
            return 0;
        }

        /// <summary>
        /// Extracts all raw file content (without zone headers) to a chosen folder.
        /// </summary>
        private void extractAllRawFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Check that there are raw files available
            if (_rawFileNodes == null || _rawFileNodes.Count == 0)
            {
                MessageBox.Show("No raw files available for extraction.", "No Files", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Prompt the user to choose a destination folder
            using (FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog())
            {
                folderBrowserDialog.Description = "Select the destination folder to extract all raw files";
                if (folderBrowserDialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                string destinationFolder = folderBrowserDialog.SelectedPath;

                // Loop through all raw file nodes and write each one to the selected folder
                foreach (var rawFileNode in _rawFileNodes)
                {
                    try
                    {
                        // Replace any forward or backslashes in the file name with underscores to avoid invalid characters
                        string safeFileName = rawFileNode.FileName.Replace("/", "_").Replace("\\", "_");

                        // Construct the destination file path using the sanitized file name
                        string destFilePath = Path.Combine(destinationFolder, safeFileName);

                        // Write the raw content bytes to the destination file (no zone headers)
                        File.WriteAllBytes(destFilePath, rawFileNode.RawFileBytes);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to extract {rawFileNode.FileName}: {ex.Message}",
                            "Extraction Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }

                MessageBox.Show("All raw files extracted successfully.",
                    "Extraction Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        // move this eventually maybe
        private void RefreshZoneData()
        {
            if (_openedFastFile == null)
                return;

            // 1) Fully re-read the zone file bytes from disk
            _openedFastFile.OpenedFastFileZone.LoadData();

            // 2) Re-run your asset record processing logic
            //    This updates _rawFileNodes, _zoneAssetRecords, _stringTables, etc.
            LoadAssetRecordsData();

            // 3) Rebuild the entire UI
            //    (Clears the TreeView/ListViews and reloads all data)
            ResetAllViews();
            LoadZoneDataToUI();
        }

        /// <summary>
        /// Clears out the relevant UI elements, so they can be repopulated cleanly.
        /// </summary>
        private void ResetAllViews()
        {
            filesTreeView.Nodes.Clear();
            assetPoolListView.Items.Clear();
            assetPoolListView.Columns.Clear();
            tagsListView.Items.Clear();
            tagsListView.Columns.Clear();
            localizeListView.Items.Clear();
            localizeListView.Columns.Clear();
            techSetsListView.Items.Clear();
            techSetsListView.Columns.Clear();
            treeViewMapEnt.Nodes.Clear();
            foreach (var lv in new[] { tagsListView, assetPoolListView, localizeListView, techSetsListView })
            {
                lv.Items.Clear();
                lv.Columns.Clear();
            }

            // Clear menu files UI and hide the tab
            menuFilesTreeView.Nodes.Clear();
            menuFilesTextEditor.ResetText();
            _selectedMenuList = null;
            _selectedMenuDef = null;
            _menuLists?.Clear();
            if (mainTabControl.TabPages.Contains(menuFilesTabPage))
            {
                mainTabControl.TabPages.Remove(menuFilesTabPage);
            }

            zoneInfoDataGridView.DataSource = null;
            textEditorControlEx1.ResetText();
            loadedFileNameStatusLabel.Visible = false;
            selectedFileMaxSizeStatusLabel.Visible = false;
            selectedItemStatusLabel.Visible = false;
            selectedFileCurrentSizeStatusLabel.Visible = false;
            saveFastFileToolStripMenuItem.Enabled = false;
            saveFastFileAsToolStripMenuItem.Enabled = false;
            this.SetProgramTitle();
        }


        private void MainWindowForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && _rawFileNodes != null)
            {
                // Find any raw files with unsaved changes
                var dirtyNodes = _rawFileNodes.Where(n => n.HasUnsavedChanges).ToList();
                if (dirtyNodes.Count > 0)
                {
                    var result = MessageBox.Show(
                        $"You have unsaved changes in {dirtyNodes.Count} file(s). Save before exiting?",
                        "Unsaved Changes",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Warning);

                    if (result == DialogResult.Yes)
                    {
                        // Save each dirty file
                        foreach (var node in dirtyNodes)
                        {
                            // Select the corresponding TreeNode so SaveZoneRawFileChanges targets it
                            var treeNode = filesTreeView.Nodes
                                .OfType<TreeNode>()
                                .First(t => ReferenceEquals(t.Tag, node));
                            filesTreeView.SelectedNode = treeNode;

                            _rawFileService.SaveZoneRawFileChanges(
                                filesTreeView,
                                _openedFastFile.FfFilePath,
                                _openedFastFile.ZoneFilePath,
                                _rawFileNodes,
                                node.RawFileContent,
                                _openedFastFile
                            );
                            node.HasUnsavedChanges = false;
                        }
                    }
                    else if (result == DialogResult.Cancel)
                    {
                        // Cancel the close
                        e.Cancel = true;
                        return;
                    }
                    // if No, proceed and discard unsaved changes
                }

                // Clean up the temp zone file (unless user wants to keep it)
                if (!keepZoneFileToolStripMenuItem.Checked &&
                    _openedFastFile != null && File.Exists(_openedFastFile.ZoneFilePath))
                {
                    try
                    {
                        File.Delete(_openedFastFile.ZoneFilePath);
                    }
                    catch
                    {
                        // ignore any deletion errors
                    }
                }
            }
        }

        /// <summary>
        /// Adjust the size of the selected raw file node.
        /// Rebuilds the entire zone file to avoid data corruption from byte shifting.
        /// </summary>
        private void increaseFileSizeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RawFileNode selectedNode = GetSelectedRawFileNode();
            if (selectedNode == null)
                return;

            // Create the size adjust dialog and pass in the current file size.
            using (RawFileSizeAdjust sizeAdjustDialog = new RawFileSizeAdjust())
            {
                sizeAdjustDialog.CurrentFileSize = selectedNode.MaxSize;
                if (sizeAdjustDialog.ShowDialog(this) == DialogResult.OK)
                {
                    int newSize = sizeAdjustDialog.NewFileSize;
                    if (newSize <= selectedNode.MaxSize)
                    {
                        MessageBox.Show("The new size must be greater than the current size.",
                            "Invalid Size", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    try
                    {
                        // Update the selected node's max size and pad its content
                        int oldSize = selectedNode.MaxSize;
                        byte[] currentContent = selectedNode.RawFileBytes ?? Array.Empty<byte>();
                        byte[] newContent = new byte[newSize];
                        Array.Copy(currentContent, newContent, Math.Min(currentContent.Length, newSize));

                        selectedNode.MaxSize = newSize;
                        selectedNode.RawFileBytes = newContent;
                        selectedNode.RawFileContent = System.Text.Encoding.Default.GetString(newContent);

                        // Rebuild the zone file from all raw file nodes
                        byte[]? newZoneData = ZoneFileBuilder.BuildFreshZone(
                            _rawFileNodes,
                            _localizedEntries,
                            _openedFastFile,
                            Path.GetFileNameWithoutExtension(_openedFastFile.FastFileName));

                        if (newZoneData == null)
                        {
                            // Revert the changes
                            selectedNode.MaxSize = oldSize;
                            selectedNode.RawFileBytes = currentContent;
                            selectedNode.RawFileContent = System.Text.Encoding.Default.GetString(currentContent);

                            MessageBox.Show("Failed to rebuild zone file.",
                                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                        // Write the new zone data
                        File.WriteAllBytes(_openedFastFile.ZoneFilePath, newZoneData);

                        MessageBox.Show($"File '{selectedNode.FileName}' size increased to {newSize} bytes successfully.\nZone file rebuilt.",
                            "Size Increase Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        RefreshZoneData();
                        ReloadAllRawFileNodesAndUI();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error increasing file size: {ex.Message}",
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        /// <summary>
        /// Returns the currently selected RawFileNode from the TreeView.
        /// If no node is selected or the selected node does not have a valid RawFileNode,
        /// a message box is shown and the method returns null.
        /// </summary>
        private RawFileNode GetSelectedRawFileNode()
        {
            if (filesTreeView.SelectedNode == null || !(filesTreeView.SelectedNode.Tag is RawFileNode selectedNode))
            {
                MessageBox.Show("Please select a raw file.", "No File Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            return selectedNode;
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show(ApplicationConstants.About, "About Call of Duty Fast File Editor");
        }

        private void CheckForUpdatesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            checkForUpdate();
        }

        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            if (_openedFastFile == null)
            {
                MessageBox.Show("Open a .ff first", "No Zone Loaded", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // make sure bytes are loaded
            var zoneData = _openedFastFile.OpenedFastFileZone.Data;
            var hexForm = new ZoneHexViewForm(zoneData);
            hexForm.Show();
        }

        /// <summary>
        /// Creates a backup of the FastFile if one doesn't already exist.
        /// </summary>
        private void CreateBackupIfNeeded(string filePath)
        {
            try
            {
                string backupPath = filePath + ".bak";
                if (!File.Exists(backupPath))
                {
                    File.Copy(filePath, backupPath);
                    Debug.WriteLine($"[Backup] Created backup: {backupPath}");
                }
                else
                {
                    Debug.WriteLine($"[Backup] Backup already exists: {backupPath}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Backup] Failed to create backup: {ex.Message}");
                // Don't block opening the file if backup fails
            }
        }

        private void COD5ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_openedFastFile != null)
            {
                SaveCloseFastFileAndCleanUp();
            }

            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "Select a COD5 Fast File",
                Filter = "Fast Files (*.ff)|*.ff"
            };

            if (openFileDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            // Create a backup of the original FastFile before any modifications
            CreateBackupIfNeeded(openFileDialog.FileName);

            try
            {
                _openedFastFile = new FastFile(openFileDialog.FileName);
                UIManager.UpdateLoadedFileNameStatusStrip(loadedFileNameStatusLabel, _openedFastFile);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to read FastFile header: {ex.Message}", "Header Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (_openedFastFile.IsValid)
            {
                try
                {
                    // Assign the correct handler for the opened file
                    _fastFileHandler = FastFileHandlerFactory.GetHandler(_openedFastFile);

                    // Show the opened FF path in the program's title text
                    this.SetProgramTitle(_openedFastFile.FfFilePath);

                    // Decompress the Fast File to get the zone file
                    _fastFileHandler.Decompress(_openedFastFile.FfFilePath, _openedFastFile.ZoneFilePath);

                    // Load & parse that zone in one go
                    _openedFastFile.LoadZone();

                    // Get tag count for the dialog
                    int tagCount = TagOperations.GetTagCount(_openedFastFile.OpenedFastFileZone);

                    // Show asset selection dialog
                    bool loadRawFiles = true;
                    bool loadLocalizedEntries = true;
                    bool loadTags = true;

                    using (var assetDialog = new AssetSelectionDialog(
                        _openedFastFile.OpenedFastFileZone.ZoneFileAssets.ZoneAssetRecords,
                        _openedFastFile,
                        tagCount))
                    {
                        if (assetDialog.ShowDialog(this) == DialogResult.Cancel)
                        {
                            SaveCloseFastFileAndCleanUp();
                            return;
                        }
                        loadRawFiles = assetDialog.LoadRawFiles;
                        loadLocalizedEntries = assetDialog.LoadLocalizedEntries;
                        loadTags = assetDialog.LoadTags;
                    }

                    // Here is where the asset records actual data is parsed throughout the zone
                    LoadAssetRecordsData(loadRawFiles: loadRawFiles, loadLocalizedEntries: loadLocalizedEntries, loadTags: loadTags);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to parse zone: {ex.Message}", "Zone Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                try
                {
                    // Load all the parsed data from the zone file to the UI
                    LoadZoneDataToUI();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Loading data failed: {ex.Message}", "Data Loading Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            else
            {
                MessageBox.Show("Invalid FastFile!\n\nThe FastFile you have selected is not a valid PS3 .ff!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                return;
            }
            EnableUI_Elements();
        }

        private void cOD4ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_openedFastFile != null)
            {
                SaveCloseFastFileAndCleanUp();
            }

            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "Select a COD4 Fast File",
                Filter = "Fast Files (*.ff)|*.ff"
            };

            if (openFileDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            // Create a backup of the original FastFile before any modifications
            CreateBackupIfNeeded(openFileDialog.FileName);

            try
            {
                _openedFastFile = new FastFile(openFileDialog.FileName);
                UIManager.UpdateLoadedFileNameStatusStrip(loadedFileNameStatusLabel, _openedFastFile);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to read FastFile header: {ex.Message}", "Header Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (_openedFastFile.IsValid)
            {
                try
                {
                    // Assign the correct handler for the opened file
                    _fastFileHandler = FastFileHandlerFactory.GetHandler(_openedFastFile);

                    // Show the opened FF path in the program's title text
                    this.SetProgramTitle(_openedFastFile.FfFilePath);

                    // Decompress the Fast File to get the zone file
                    _fastFileHandler.Decompress(_openedFastFile.FfFilePath, _openedFastFile.ZoneFilePath);

                    // Load & parse that zone in one go
                    _openedFastFile.LoadZone();

                    // Get tag count for the dialog
                    int tagCount = TagOperations.GetTagCount(_openedFastFile.OpenedFastFileZone);

                    // Show asset selection dialog
                    bool loadRawFiles = true;
                    bool loadLocalizedEntries = true;
                    bool loadTags = true;

                    using (var assetDialog = new AssetSelectionDialog(
                        _openedFastFile.OpenedFastFileZone.ZoneFileAssets.ZoneAssetRecords,
                        _openedFastFile,
                        tagCount))
                    {
                        if (assetDialog.ShowDialog(this) == DialogResult.Cancel)
                        {
                            SaveCloseFastFileAndCleanUp();
                            return;
                        }
                        loadRawFiles = assetDialog.LoadRawFiles;
                        loadLocalizedEntries = assetDialog.LoadLocalizedEntries;
                        loadTags = assetDialog.LoadTags;
                    }

                    // Here is where the asset records actual data is parsed throughout the zone
                    LoadAssetRecordsData(loadRawFiles: loadRawFiles, loadLocalizedEntries: loadLocalizedEntries, loadTags: loadTags);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to parse zone: {ex.Message}", "Zone Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                try
                {
                    // Load all the parsed data from the zone file to the UI
                    LoadZoneDataToUI();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Loading data failed: {ex.Message}", "Data Loading Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            else
            {
                MessageBox.Show("Invalid FastFile!\n\nThe FastFile you have selected is not a valid PS3 .ff!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                return;
            }
            EnableUI_Elements();
        }

        private void mW2ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_openedFastFile != null)
            {
                SaveCloseFastFileAndCleanUp();
            }

            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "Select a MW2 Fast File",
                Filter = "Fast Files (*.ff)|*.ff"
            };

            if (openFileDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            // Create a backup of the original FastFile before any modifications
            CreateBackupIfNeeded(openFileDialog.FileName);

            try
            {
                _openedFastFile = new FastFile(openFileDialog.FileName);
                UIManager.UpdateLoadedFileNameStatusStrip(loadedFileNameStatusLabel, _openedFastFile);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to read FastFile header: {ex.Message}", "Header Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (_openedFastFile.IsValid)
            {
                try
                {
                    // Assign the correct handler for the opened file
                    _fastFileHandler = FastFileHandlerFactory.GetHandler(_openedFastFile);

                    // Show the opened FF path in the program's title text
                    this.SetProgramTitle(_openedFastFile.FfFilePath);

                    // Decompress the Fast File to get the zone file
                    _fastFileHandler.Decompress(_openedFastFile.FfFilePath, _openedFastFile.ZoneFilePath);

                    // Load & parse that zone in one go
                    _openedFastFile.LoadZone();

                    // Get tag count for the dialog
                    int tagCount = TagOperations.GetTagCount(_openedFastFile.OpenedFastFileZone);

                    // Show asset selection dialog
                    bool loadRawFiles = true;
                    bool loadLocalizedEntries = true;
                    bool loadTags = true;

                    using (var assetDialog = new AssetSelectionDialog(
                        _openedFastFile.OpenedFastFileZone.ZoneFileAssets.ZoneAssetRecords,
                        _openedFastFile,
                        tagCount))
                    {
                        if (assetDialog.ShowDialog(this) == DialogResult.Cancel)
                        {
                            SaveCloseFastFileAndCleanUp();
                            return;
                        }
                        loadRawFiles = assetDialog.LoadRawFiles;
                        loadLocalizedEntries = assetDialog.LoadLocalizedEntries;
                        loadTags = assetDialog.LoadTags;
                    }

                    // Here is where the asset records actual data is parsed throughout the zone
                    LoadAssetRecordsData(loadRawFiles: loadRawFiles, loadLocalizedEntries: loadLocalizedEntries, loadTags: loadTags);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to parse zone: {ex.Message}", "Zone Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                try
                {
                    // Load all the parsed data from the zone file to the UI
                    LoadZoneDataToUI();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Loading data failed: {ex.Message}", "Data Loading Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            else
            {
                MessageBox.Show("Invalid FastFile!\n\nThe FastFile you have selected is not a valid PS3 MW2 .ff!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                return;
            }
            EnableUI_Elements();
        }

        // There's a lot of duplicate code around this issue. This needs revisited & fixed/cleaned up
        private void ReloadAllRawFileNodesAndUI()
        {
            // Reparse the raw file nodes from disk
            _rawFileNodes = RawFileParser.ExtractAllRawFilesSizeAndName(_openedFastFile.ZoneFilePath);
            RawFileNode.CurrentZone = _openedFastFile.OpenedFastFileZone;

            // Rebuild UI for files list
            LoadRawFilesTreeView();
        }

        private void reloadRawFilesPatternMatchingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LoadAssetRecordsData(forcePatternMatching: true);
            ResetAllViews();
            LoadZoneDataToUI();
        }

        private void reportIssuesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string url = "https://github.com/primetime43/CoD-FF-Tools/issues";
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An unexpected error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
