using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Be.Windows.Forms;
using Call_of_Duty_FastFile_Editor.GameDefinitions;
using Call_of_Duty_FastFile_Editor.Models;

namespace Call_of_Duty_FastFile_Editor.UI
{
    public partial class ZoneHexViewForm : Form
    {
        private readonly List<ZoneAssetRecord> _assetRecords;
        private readonly ZoneFile _zoneFile;
        private readonly bool _isCod4;
        private readonly bool _isCod5;
        private readonly bool _isMW2;

        // Parsed asset collections for name lookups
        private readonly List<RawFileNode> _rawFiles;
        private readonly List<LocalizedEntry> _localizedEntries;
        private readonly List<StringTable> _stringTables;
        private readonly List<WeaponAsset> _weapons;
        private readonly List<XAnimParts> _xanims;
        private readonly List<ImageAsset> _images;
        private readonly List<TechSetAsset> _techSets;
        private readonly List<MenuList> _menuLists;

        // Color mapping for asset types
        private static readonly Dictionary<string, Color> AssetTypeColors = new Dictionary<string, Color>
        {
            { "rawfile", Color.FromArgb(144, 238, 144) },      // Light green
            { "localize", Color.FromArgb(173, 216, 230) },     // Light blue
            { "stringtable", Color.FromArgb(255, 182, 193) },  // Light pink
            { "menufile", Color.FromArgb(255, 218, 185) },     // Peach
            { "material", Color.FromArgb(221, 160, 221) },     // Plum
            { "techset", Color.FromArgb(176, 196, 222) },      // Light steel blue
            { "xanim", Color.FromArgb(255, 255, 224) },        // Light yellow
            { "weapon", Color.FromArgb(255, 160, 122) },       // Light salmon
            { "image", Color.FromArgb(152, 251, 152) },        // Pale green
            { "xmodel", Color.FromArgb(230, 230, 250) },       // Lavender
            { "sound", Color.FromArgb(255, 228, 196) },        // Bisque
            { "fx", Color.FromArgb(240, 128, 128) },           // Light coral
            { "font", Color.FromArgb(135, 206, 250) },         // Light sky blue
            { "physpreset", Color.FromArgb(216, 191, 216) },   // Thistle
            { "col_map_sp", Color.FromArgb(188, 143, 143) },   // Rosy brown
            { "col_map_mp", Color.FromArgb(188, 143, 143) },   // Rosy brown
            { "map_ents", Color.FromArgb(144, 238, 144) },     // Light green
            { "gfx_map", Color.FromArgb(175, 238, 238) },      // Pale turquoise
        };

        private static readonly Color DefaultAssetColor = Color.FromArgb(211, 211, 211); // Light gray

        /// <summary>
        /// Creates a new ZoneHexViewForm with just raw data (legacy support).
        /// </summary>
        public ZoneHexViewForm(byte[] data) : this(data, null, null, null, null, null, null, null, null, null, null)
        {
        }

        /// <summary>
        /// Creates a new ZoneHexViewForm with zone data, asset records, and zone file reference.
        /// </summary>
        public ZoneHexViewForm(byte[] data, List<ZoneAssetRecord> assetRecords, ZoneFile zoneFile,
            List<RawFileNode> rawFiles = null, List<LocalizedEntry> localizedEntries = null,
            List<StringTable> stringTables = null, List<WeaponAsset> weapons = null,
            List<XAnimParts> xanims = null, List<ImageAsset> images = null,
            List<TechSetAsset> techSets = null, List<MenuList> menuLists = null)
        {
            InitializeComponent();

            _assetRecords = assetRecords ?? new List<ZoneAssetRecord>();
            _zoneFile = zoneFile;

            // Store parsed asset collections
            _rawFiles = rawFiles ?? new List<RawFileNode>();
            _localizedEntries = localizedEntries ?? new List<LocalizedEntry>();
            _stringTables = stringTables ?? new List<StringTable>();
            _weapons = weapons ?? new List<WeaponAsset>();
            _xanims = xanims ?? new List<XAnimParts>();
            _images = images ?? new List<ImageAsset>();
            _techSets = techSets ?? new List<TechSetAsset>();
            _menuLists = menuLists ?? new List<MenuList>();

            // Detect game version
            if (_zoneFile?.ParentFastFile != null)
            {
                _isCod4 = _zoneFile.ParentFastFile.IsCod4File;
                _isCod5 = _zoneFile.ParentFastFile.IsCod5File;
                _isMW2 = _zoneFile.ParentFastFile.IsMW2File;
            }

            // Default to big-endian
            bigEndianItem.Checked = true;
            littleEndianItem.Checked = false;

            // Configure HexBox
            var dp = new DynamicByteProvider(data);
            hexBox.ByteProvider = dp;
            hexBox.ReadOnly = true;
            hexBox.StringViewVisible = true;
            hexBox.LineInfoVisible = true;
            hexBox.VScrollBarVisible = true;
            hexBox.BytesPerLine = 16;
            hexBox.GroupSize = 4;

            // Update status on click or key
            hexBox.MouseClick += (s, e) => RefreshStatus();
            hexBox.KeyUp += (s, e) => RefreshStatus();

            // File menu actions
            saveAsToolStripMenuItem.Click += SaveAs;
            closeToolStripMenuItem.Click += (s, e) => Close();

            // Edit menu actions
            copyHexToolStripMenuItem.Click += (s, e) => hexBox.CopyHex();
            copyAsciiToolStripMenuItem.Click += (s, e) => hexBox.Copy();
            selectAllToolStripMenuItem.Click += (s, e) => hexBox.SelectAll();

            // Go To...
            goToToolStripMenuItem.Click += (s, e) => ShowGotoDialog();

            // Byte Order toggle
            littleEndianItem.Click += (s, e) =>
            {
                littleEndianItem.Checked = true;
                bigEndianItem.Checked = false;
                RefreshStatus();
            };
            bigEndianItem.Click += (s, e) =>
            {
                bigEndianItem.Checked = true;
                littleEndianItem.Checked = false;
                RefreshStatus();
            };

            // View menu actions
            showAssetPanelMenuItem.Click += (s, e) =>
            {
                mainSplitContainer.Panel2Collapsed = !showAssetPanelMenuItem.Checked;
            };

            // Asset panel events
            assetListView.SelectedIndexChanged += AssetListView_SelectedIndexChanged;
            assetListView.DoubleClick += AssetListView_DoubleClick;
            jumpToAssetButton.Click += JumpToAssetButton_Click;

            // Populate asset list
            PopulateAssetList();

            // Update title with asset count
            if (_assetRecords.Count > 0)
            {
                this.Text = $"Zone File Hex Viewer - {_assetRecords.Count} Assets";
            }

            // Set splitter distance after form is loaded
            this.Load += (s, e) =>
            {
                // Set splitter to show ~70% hex view, ~30% asset panel
                if (mainSplitContainer.Width > 500)
                {
                    mainSplitContainer.SplitterDistance = mainSplitContainer.Width - 300;
                }
            };

            RefreshStatus();
        }

        private bool BigEndianSelected => bigEndianItem.Checked;

        private void PopulateAssetList()
        {
            assetListView.Items.Clear();
            assetListView.BeginUpdate();

            // Index counters for matching parsed assets (same as MainWindowForm)
            int rawFileIndex = 0;
            int localizeIndex = 0;
            int menuIndex = 0;
            int techSetIndex = 0;
            int xanimIndex = 0;
            int stringTableIndex = 0;
            int weaponIndex = 0;
            int imageIndex = 0;

            // Get game definition for asset type checking
            IGameDefinition gameDefinition = null;
            if (_zoneFile?.ParentFastFile != null)
            {
                gameDefinition = GameDefinitionFactory.GetDefinition(_zoneFile.ParentFastFile);
            }

            foreach (var record in _assetRecords)
            {
                string typeName = GetAssetTypeName(record);
                string name = "(unnamed)";
                int startOffset = 0;
                int endOffset = 0;

                // Get name and offsets from parsed collections using index-based matching
                if (gameDefinition != null)
                {
                    int assetTypeValue = GetAssetTypeValue(record);
                    bool isRawFile = gameDefinition.IsRawFileType(assetTypeValue);
                    bool isLocalize = gameDefinition.IsLocalizeType(assetTypeValue);
                    bool isMenuFile = gameDefinition.IsMenuFileType(assetTypeValue);
                    bool isTechSet = gameDefinition.IsTechSetType(assetTypeValue);
                    bool isXAnim = gameDefinition.IsXAnimType(assetTypeValue);
                    bool isStringTable = gameDefinition.IsStringTableType(assetTypeValue);
                    bool isWeapon = gameDefinition.IsWeaponType(assetTypeValue);
                    bool isImage = gameDefinition.IsImageType(assetTypeValue);

                    if (isRawFile && rawFileIndex < _rawFiles.Count)
                    {
                        var node = _rawFiles[rawFileIndex++];
                        name = node.FileName ?? "(unnamed)";
                        startOffset = node.StartOfFileHeader;
                        endOffset = node.RawFileEndPosition;
                    }
                    else if (isLocalize && localizeIndex < _localizedEntries.Count)
                    {
                        var entry = _localizedEntries[localizeIndex++];
                        name = entry.Key ?? "(unnamed)";
                        startOffset = entry.StartOfFileHeader;
                        endOffset = entry.EndOfFileHeader;
                    }
                    else if (isMenuFile && menuIndex < _menuLists.Count)
                    {
                        var menu = _menuLists[menuIndex++];
                        name = menu.Name ?? "(unnamed)";
                        startOffset = menu.DataStartOffset;
                        endOffset = menu.DataEndOffset;
                    }
                    else if (isTechSet && techSetIndex < _techSets.Count)
                    {
                        var techSet = _techSets[techSetIndex++];
                        name = techSet.Name ?? "(unnamed)";
                        startOffset = techSet.StartOffset;
                        endOffset = techSet.EndOffset;
                    }
                    else if (isXAnim && xanimIndex < _xanims.Count)
                    {
                        var xanim = _xanims[xanimIndex++];
                        name = xanim.Name ?? "(unnamed)";
                        startOffset = xanim.StartOffset;
                        endOffset = xanim.EndOffset;
                    }
                    else if (isStringTable && stringTableIndex < _stringTables.Count)
                    {
                        var table = _stringTables[stringTableIndex++];
                        name = table.TableName ?? "(unnamed)";
                        startOffset = table.StartOfFileHeader;
                        endOffset = table.DataEndPosition;
                    }
                    else if (isWeapon && weaponIndex < _weapons.Count)
                    {
                        var weapon = _weapons[weaponIndex++];
                        name = weapon.InternalName ?? "(unnamed)";
                        startOffset = weapon.StartOffset;
                        // Use header size + reasonable inline data estimate to avoid overlap
                        // The EndOffset from parser can overshoot into next weapon
                        int conservativeEnd = weapon.StartOffset + weapon.HeaderSize + 256; // Header + inline strings
                        // Check if next weapon exists and would overlap
                        if (weaponIndex < _weapons.Count)
                        {
                            int nextWeaponStart = _weapons[weaponIndex].StartOffset;
                            if (weapon.EndOffset > nextWeaponStart)
                            {
                                // EndOffset overshoots, use next weapon's start as boundary
                                endOffset = nextWeaponStart;
                            }
                            else
                            {
                                endOffset = weapon.EndOffset;
                            }
                        }
                        else
                        {
                            endOffset = weapon.EndOffset;
                        }
                    }
                    else if (isImage && imageIndex < _images.Count)
                    {
                        var image = _images[imageIndex++];
                        name = image.Name ?? "(unnamed)";
                        startOffset = image.StartOffset;
                        endOffset = image.EndOffset;
                    }
                }

                // Fallback to record data if not matched by parsed collections
                if (startOffset == 0)
                {
                    // Use record offsets if available, otherwise fall back to asset pool offset
                    startOffset = record.HeaderStartOffset > 0 ? record.HeaderStartOffset : record.AssetPoolRecordOffset;
                    endOffset = record.AssetRecordEndOffset > 0 ? record.AssetRecordEndOffset :
                               (record.AssetDataEndOffset > 0 ? record.AssetDataEndOffset : startOffset + 8);

                    if (!string.IsNullOrEmpty(record.Name))
                        name = record.Name;
                }

                var item = new ListViewItem(typeName);
                item.SubItems.Add(name);
                item.SubItems.Add($"0x{startOffset:X}");
                item.SubItems.Add($"0x{endOffset:X}");

                // Store both record and computed offsets in tag
                item.Tag = new AssetDisplayInfo
                {
                    Record = record,
                    Name = name,
                    StartOffset = startOffset,
                    EndOffset = endOffset
                };

                // Apply color coding
                Color typeColor = GetAssetColor(typeName);
                item.BackColor = typeColor;

                assetListView.Items.Add(item);
            }

            assetListView.EndUpdate();

            // Update panel label with count
            assetPanelLabel.Text = $"Asset Pool ({_assetRecords.Count})";
        }

        private int GetAssetTypeValue(ZoneAssetRecord record)
        {
            if (_isCod4)
                return (int)record.AssetType_COD4;
            if (_isCod5)
                return (int)record.AssetType_COD5;
            if (_isMW2)
                return (int)record.AssetType_MW2;
            return 0;
        }

        // Helper class to store display info with computed offsets
        private class AssetDisplayInfo
        {
            public ZoneAssetRecord Record { get; set; }
            public string Name { get; set; }
            public int StartOffset { get; set; }
            public int EndOffset { get; set; }
        }

        private string GetAssetTypeName(ZoneAssetRecord record)
        {
            if (_isCod4)
                return record.AssetType_COD4.ToString();
            else if (_isCod5)
                return record.AssetType_COD5.ToString();
            else if (_isMW2)
                return record.AssetType_MW2.ToString();
            return "unknown";
        }

        private Color GetAssetColor(string typeName)
        {
            string lowerType = typeName.ToLowerInvariant();
            if (AssetTypeColors.TryGetValue(lowerType, out Color color))
                return color;
            return DefaultAssetColor;
        }

        private void AssetListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (assetListView.SelectedItems.Count == 0)
                return;

            var selectedItem = assetListView.SelectedItems[0];
            var info = selectedItem.Tag as AssetDisplayInfo;
            if (info == null) return;

            // Update status bar with asset info
            string typeName = GetAssetTypeName(info.Record);
            assetStatusLabel.Text = $"Asset: {typeName} - {info.Name}";

            // Auto-highlight the asset region in the hex view when selected
            if (highlightAssetsMenuItem.Checked)
            {
                HighlightAssetRegion(info);
            }
        }

        private void HighlightAssetRegion(AssetDisplayInfo info)
        {
            int startOffset = info.StartOffset;
            int endOffset = info.EndOffset;

            // Ensure we have valid offsets
            if (startOffset <= 0)
            {
                startOffset = info.Record.AssetPoolRecordOffset;
                endOffset = startOffset + 8;
            }

            // Calculate selection length
            long selectionLength = endOffset - startOffset;
            if (selectionLength <= 0) selectionLength = 8;
            if (selectionLength > 0x10000) selectionLength = 0x10000; // Cap at 64KB

            // Select the region and scroll to it
            hexBox.SelectionStart = startOffset;
            hexBox.SelectionLength = selectionLength;
            hexBox.ScrollByteIntoView(startOffset);

            // Update inspector with asset info
            inspectorTextBox.Text = $"Asset: {info.Name}\r\nStart: 0x{startOffset:X}\r\nEnd: 0x{endOffset:X}\r\nSize: {selectionLength} bytes";
        }

        private void AssetListView_DoubleClick(object sender, EventArgs e)
        {
            JumpToSelectedAsset();
        }

        private void JumpToAssetButton_Click(object sender, EventArgs e)
        {
            JumpToSelectedAsset();
        }

        private void JumpToSelectedAsset()
        {
            if (assetListView.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select an asset from the list.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedItem = assetListView.SelectedItems[0];
            var info = selectedItem.Tag as AssetDisplayInfo;
            if (info == null) return;

            // Use the pre-computed offsets
            HighlightAssetRegion(info);
            RefreshStatus();
        }


        private void RefreshStatus()
        {
            long pos = hexBox.SelectionStart;
            long len = hexBox.SelectionLength;

            offsetStatusLabel.Text = $"Offset: 0x{pos:X}";
            selStatusLabel.Text = $"Sel: {len} bytes";

            // Single-byte value
            if (len == 1)
            {
                byte b = ((DynamicByteProvider)hexBox.ByteProvider).Bytes[(int)pos];
                valueStatusLabel.Text = $"Value: 0x{b:X2}";
            }
            else
            {
                valueStatusLabel.Text = "Value: --";
            }

            // 4-byte inspector
            if (len == 4)
            {
                var buf = ((DynamicByteProvider)hexBox.ByteProvider)
                              .Bytes
                              .Skip((int)pos)
                              .Take(4)
                              .ToArray();

                if (BigEndianSelected)
                    Array.Reverse(buf);

                inspectorTextBox.Text =
                    $"UInt32: {BitConverter.ToUInt32(buf, 0)}\r\n" +
                    $"Int32 : {BitConverter.ToInt32(buf, 0)}\r\n" +
                    $"Float : {BitConverter.ToSingle(buf, 0):F6}";
            }
            else if (len > 0 && len <= 8)
            {
                var buf = ((DynamicByteProvider)hexBox.ByteProvider)
                              .Bytes
                              .Skip((int)pos)
                              .Take((int)len)
                              .ToArray();

                if (BigEndianSelected)
                    Array.Reverse(buf);

                // Show hex bytes
                string hexStr = BitConverter.ToString(buf).Replace("-", " ");
                inspectorTextBox.Text = $"Hex: {hexStr}";
            }
            else
            {
                inspectorTextBox.Clear();
            }

            // Find which asset contains the current offset
            var assetAtOffset = FindAssetAtOffset((int)pos);
            if (assetAtOffset.HasValue)
            {
                var record = assetAtOffset.Value;
                string typeName = GetAssetTypeName(record);
                assetStatusLabel.Text = $"Asset: {typeName} - {record.Name}";
            }
            else
            {
                assetStatusLabel.Text = "Asset: --";
            }
        }

        private ZoneAssetRecord? FindAssetAtOffset(int offset)
        {
            foreach (var record in _assetRecords)
            {
                int start = record.AssetDataStartPosition > 0 ? record.AssetDataStartPosition :
                            record.HeaderStartOffset > 0 ? record.HeaderStartOffset :
                            record.AssetPoolRecordOffset;

                int end = record.AssetDataEndOffset > 0 ? record.AssetDataEndOffset :
                          record.AssetRecordEndOffset > 0 ? record.AssetRecordEndOffset :
                          start + record.Size;

                if (end <= start) end = start + 8;

                if (offset >= start && offset < end)
                    return record;
            }
            return null;
        }

        private void SaveAs(object sender, EventArgs e)
        {
            using (var sfd = new SaveFileDialog { Filter = "Binary|*.*", Title = "Save Zone As" })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    var bytes = ((DynamicByteProvider)hexBox.ByteProvider).Bytes.ToArray();
                    System.IO.File.WriteAllBytes(sfd.FileName, bytes);
                }
            }
        }

        private void ShowGotoDialog()
        {
            // Ask for a hex offset (allow "0x" prefix)
            string input = Microsoft.VisualBasic.Interaction.InputBox(
                "Go to offset (hex)", "Go To", "0");

            if (string.IsNullOrEmpty(input))
                return;

            if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                input = input.Substring(2);

            if (long.TryParse(input,
                              System.Globalization.NumberStyles.HexNumber,
                              null,
                              out var off))
            {
                // Clamp to valid range
                var max = ((DynamicByteProvider)hexBox.ByteProvider).Length - 1;
                if (off < 0) off = 0;
                if (off > max) off = max;

                // Move the caret, select that single byte, scroll it into view
                hexBox.SelectionStart = off;
                hexBox.SelectionLength = 1;
                hexBox.ScrollByteIntoView(off);

                RefreshStatus();
            }
            else
            {
                MessageBox.Show($"\"{input}\" is not a valid hex number.",
                                "Invalid Offset",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
            }
        }
    }
}
