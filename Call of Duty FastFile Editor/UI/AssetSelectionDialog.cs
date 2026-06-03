using Call_of_Duty_FastFile_Editor.GameDefinitions;
using Call_of_Duty_FastFile_Editor.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Call_of_Duty_FastFile_Editor.UI
{
    /// <summary>
    /// Dialog that displays available assets in a zone file and allows the user
    /// to select which asset types to load.
    /// </summary>
    public partial class AssetSelectionDialog : Form
    {
        private readonly List<AssetTypeInfo> _assetTypes;
        private readonly FastFile _fastFile;
        private readonly IGameDefinition _gameDefinition;
        private readonly int _tagCount;

        /// <summary>
        /// Gets whether to load rawfiles.
        /// </summary>
        public bool LoadRawFiles { get; private set; } = true;

        /// <summary>
        /// Gets whether to load localized entries.
        /// </summary>
        public bool LoadLocalizedEntries { get; private set; } = true;

        /// <summary>
        /// Gets whether to load tags (script strings).
        /// </summary>
        public bool LoadTags { get; private set; } = true;

        /// <summary>
        /// Gets whether to load menufiles.
        /// </summary>
        public bool LoadMenuFiles { get; private set; } = true;

        /// <summary>
        /// Creates a new AssetSelectionDialog.
        /// </summary>
        /// <param name="zoneAssetRecords">The asset records from the zone.</param>
        /// <param name="fastFile">The FastFile being opened.</param>
        /// <param name="tagCount">Number of tags in the zone.</param>
        public AssetSelectionDialog(List<ZoneAssetRecord> zoneAssetRecords, FastFile fastFile, int tagCount = 0)
        {
            InitializeComponent();
            _fastFile = fastFile;
            _gameDefinition = GameDefinitionFactory.GetDefinition(fastFile);
            _tagCount = tagCount;
            _assetTypes = AnalyzeAssets(zoneAssetRecords);
            PopulateFileInfo();
            PopulateAssetList();
        }

        /// <summary>
        /// Populates the source file information labels.
        /// </summary>
        private void PopulateFileInfo()
        {
            // Game name
            string gameName = _fastFile.IsCod4File ? "CoD4" :
                              _fastFile.IsCod5File ? "WaW" :
                              _fastFile.IsMW2File ? "MW2" :
                              _fastFile.IsGhostsFile ? "Ghosts" : "Unknown";
            gameLabel.Text = $"Game: {gameName}";

            // Platform - use FastFile's detected platform (handles PC detection)
            platformLabel.Text = $"Platform: {_fastFile.Platform}";

            // Signed status
            bool isSigned = _fastFile.FastFileMagic == FastFileLib.FastFileInfo.SignedMagic;
            signedLabel.Text = $"Signed: {(isSigned ? "Yes" : "No")}";

            // File size - use shared library method
            sizeLabel.Text = $"Size: {FastFileLib.FastFileInfo.FormatFileSize(_fastFile.FileLength)}";
        }

        private List<AssetTypeInfo> AnalyzeAssets(List<ZoneAssetRecord> records)
        {
            var assetCounts = new Dictionary<string, int>();

            foreach (var record in records)
            {
                string typeName;
                if (_fastFile.IsCod4File && _fastFile.IsPC)
                    typeName = record.AssetType_COD4_PC.ToString();
                // CoD4 Wii uses the Xbox 360 enum, verified against retail Reflex zones.
                else if (_fastFile.IsCod4File && (_fastFile.IsXbox360 || _fastFile.IsWii))
                    typeName = record.AssetType_COD4_Xbox360.ToString();
                else if (_fastFile.IsCod4File)
                    typeName = record.AssetType_COD4.ToString();
                // Wii reuses the PC enum (same shader-less layout) even though it's big-endian.
                else if (_fastFile.IsCod5File && (_fastFile.IsPC || _fastFile.IsWii))
                    typeName = record.AssetType_COD5_PC.ToString();
                else if (_fastFile.IsCod5File && _fastFile.IsXbox360)
                    typeName = record.AssetType_COD5_Xbox360.ToString();
                else if (_fastFile.IsCod5File)
                    typeName = record.AssetType_COD5.ToString();
                else if (_fastFile.IsMW2File && _fastFile.IsPC)
                    typeName = record.AssetType_MW2_PC.ToString();
                else if (_fastFile.IsMW2File && _fastFile.IsXbox360)
                    typeName = record.AssetType_MW2_Xbox360.ToString();
                else if (_fastFile.IsMW2File)
                    typeName = record.AssetType_MW2.ToString();
                else if (_fastFile.IsGhostsFile)
                    typeName = record.AssetType_Ghosts.ToString();
                else
                    typeName = "unknown";

                if (assetCounts.ContainsKey(typeName))
                    assetCounts[typeName]++;
                else
                    assetCounts[typeName] = 1;
            }

            var result = new List<AssetTypeInfo>();
            foreach (var kvp in assetCounts.OrderByDescending(x => x.Value))
            {
                bool isSupported = _gameDefinition.IsSupportedAssetTypeName(kvp.Key);
                result.Add(new AssetTypeInfo
                {
                    TypeName = kvp.Key,
                    Count = kvp.Value,
                    IsSupported = isSupported,
                    IsSelected = isSupported // Pre-select supported types
                });
            }

            return result;
        }

        private void PopulateAssetList()
        {
            assetListView.Items.Clear();

            int supportedCount = 0;
            int unsupportedCount = 0;

            // Add tags at the top (special item, not an asset type)
            if (_tagCount > 0)
            {
                var tagItem = new ListViewItem("tags (script strings)");
                tagItem.SubItems.Add(_tagCount.ToString());
                tagItem.SubItems.Add("Yes");
                tagItem.Tag = new AssetTypeInfo
                {
                    TypeName = "tags",
                    Count = _tagCount,
                    IsSupported = true,
                    IsSelected = true
                };
                tagItem.Checked = true;
                tagItem.ForeColor = Color.DarkGreen;
                assetListView.Items.Add(tagItem);
                supportedCount += _tagCount;
            }

            foreach (var asset in _assetTypes)
            {
                var item = new ListViewItem(asset.TypeName);
                item.SubItems.Add(asset.Count.ToString());
                item.SubItems.Add(asset.IsSupported ? "Yes" : "No");
                item.Tag = asset;
                item.Checked = asset.IsSelected;

                if (asset.IsSupported)
                {
                    item.ForeColor = Color.DarkGreen;
                    supportedCount += asset.Count;
                }
                else
                {
                    item.ForeColor = Color.Gray;
                    unsupportedCount += asset.Count;
                }

                assetListView.Items.Add(item);
            }

            // Update summary label
            summaryLabel.Text = $"Total: {supportedCount + unsupportedCount} items | " +
                               $"Supported: {supportedCount} | Unsupported: {unsupportedCount}";
        }

        // Ghosts viewable types all route through the same GhostsAssetWalker -> RawFiles tab,
        // which is gated by a single LoadRawFiles flag (no per-type Ghosts load path yet).
        private static readonly HashSet<string> GhostsRawFileTabTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "rawfile",
            "scriptfile",
            "luafile"
        };

        private void okButton_Click(object sender, EventArgs e)
        {
            // Sync each row's IsSelected with its checkbox.
            foreach (ListViewItem item in assetListView.Items)
            {
                if (item.Tag is AssetTypeInfo asset)
                    asset.IsSelected = item.Checked;
            }

            if (_fastFile.IsGhostsFile)
            {
                // rawfile / scriptfile / luafile are the only loadable Ghosts categories,
                // and they share one walker. Load it when any of them is checked.
                LoadRawFiles = assetListView.Items.Cast<ListViewItem>()
                    .Any(i => i.Checked && i.Tag is AssetTypeInfo a && GhostsRawFileTabTypes.Contains(a.TypeName));
                LoadLocalizedEntries = false;
                LoadTags = false;
                LoadMenuFiles = false;
            }
            else
            {
                // Update load flags based on asset type.
                foreach (ListViewItem item in assetListView.Items)
                {
                    if (item.Tag is not AssetTypeInfo asset)
                        continue;
                    switch (asset.TypeName)
                    {
                        case "rawfile":
                            LoadRawFiles = item.Checked;
                            break;
                        case "localize":
                            LoadLocalizedEntries = item.Checked;
                            break;
                        case "tags":
                            LoadTags = item.Checked;
                            break;
                        case "menufile":
                            LoadMenuFiles = item.Checked;
                            break;
                    }
                }
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void selectAllButton_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in assetListView.Items)
            {
                var asset = item.Tag as AssetTypeInfo;
                if (asset != null && asset.IsSupported)
                    item.Checked = true;
            }
        }

        private void selectNoneButton_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in assetListView.Items)
            {
                item.Checked = false;
            }
        }

        private void assetListView_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            // Prevent checking unsupported items
            var item = assetListView.Items[e.Index];
            var asset = item.Tag as AssetTypeInfo;

            if (asset != null && !asset.IsSupported && e.NewValue == CheckState.Checked)
            {
                e.NewValue = CheckState.Unchecked;
                MessageBox.Show($"'{asset.TypeName}' is not currently supported for parsing.",
                               "Unsupported Asset Type",
                               MessageBoxButtons.OK,
                               MessageBoxIcon.Information);
            }
        }

        /// <summary>
        /// Information about an asset type in the zone.
        /// </summary>
        private class AssetTypeInfo
        {
            public string TypeName { get; set; } = "";
            public int Count { get; set; }
            public bool IsSupported { get; set; }
            public bool IsSelected { get; set; }
        }
    }
}
