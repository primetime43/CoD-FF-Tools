using Call_of_Duty_FastFile_Editor.GameDefinitions;

namespace Call_of_Duty_FastFile_Editor.Models
{
    public class ZoneFileAssetManifest
    {
        /// <summary>
        /// List of asset records in the zone file.
        /// Parsed from the asset pool.
        /// Order MUST stay the same as in the zone file.
        /// </summary>
        public List<ZoneAssetRecord>? ZoneAssetRecords { get; set; } = new List<ZoneAssetRecord>();
    }

    public struct ZoneAssetRecord
    {
        public CoD5AssetTypePS3 AssetType_COD5 { get; set; }
        public CoD5AssetTypeXbox360 AssetType_COD5_Xbox360 { get; set; }
        public CoD4AssetType AssetType_COD4 { get; set; }
        public MW2AssetType AssetType_MW2 { get; set; }
        public int HeaderStartOffset { get; set; }
        /// <summary>
        /// The offset where the header ends before the data. (Includes the null terminator)
        /// </summary>
        public int HeaderEndOffset { get; set; }
        public int AssetPoolRecordOffset { get; set; }
        /// <summary>
        /// The offset where the asset data starts after the header.
        /// </summary>
        public int AssetDataStartPosition { get; set; }
        /// <summary>
        /// The offset where the asset data ends. (Before the null terminator if it has one)
        /// </summary>
        public int AssetDataEndOffset { get; set; }

        /// <summary>
        /// The offset where the asset record ends. (After the null terminator if it has one)
        /// </summary>
        public int AssetRecordEndOffset { get; set; }
        public string Name { get; set; }
        public string Content { get; set; }
        public int Size { get; set; }
        public byte[] RawDataBytes { get; set; }
        public string AdditionalData { get; set; }
    }
}
