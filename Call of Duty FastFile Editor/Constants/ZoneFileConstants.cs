namespace Call_of_Duty_FastFile_Editor.Constants
{
    /// <summary>
    /// Zone file header offsets based on XFile and XAssetList structures.
    /// Reference: https://codresearch.dev/index.php/FastFiles_and_Zone_files_(MW2)
    ///
    /// IMPORTANT: Xbox 360 has 6 block sizes, PS3/PC has 7-8 block sizes.
    /// This affects the offsets of XAssetList fields!
    /// - Xbox 360: XFile = 32 bytes (6 blocks), XAssetList starts at 0x20
    /// - PS3: XFile = 36 bytes (7 blocks), XAssetList starts at 0x24
    /// - PC: XFile = 40 bytes (8 blocks), XAssetList starts at 0x28
    /// </summary>
    public static class ZoneFileHeaderConstants
    {
        // XFile structure (common to all platforms)
        public const int ZoneSizeOffset = 0x00;              // 4 bytes - Total zone data size
        public const int ExternalSizeOffset = 0x04;          // 4 bytes - External resource allocation size
        public const int BlockSizeTempOffset = 0x08;         // 4 bytes - XFILE_BLOCK_TEMP allocation
        public const int BlockSizePhysicalOffset = 0x0C;     // 4 bytes - XFILE_BLOCK_PHYSICAL allocation
        public const int BlockSizeRuntimeOffset = 0x10;      // 4 bytes - XFILE_BLOCK_RUNTIME allocation
        public const int BlockSizeVirtualOffset = 0x14;      // 4 bytes - XFILE_BLOCK_VIRTUAL allocation
        public const int BlockSizeLargeOffset = 0x18;        // 4 bytes - XFILE_BLOCK_LARGE allocation
        public const int BlockSizeCallbackOffset = 0x1C;     // 4 bytes - XFILE_BLOCK_CALLBACK allocation
        public const int BlockSizeVertexOffset = 0x20;       // 4 bytes - XFILE_BLOCK_VERTEX allocation (PS3/PC only)

        // XAssetList offsets for PS3 (7 blocks = 36 bytes XFile header)
        public const int ScriptStringCountOffset = 0x24;     // 4 bytes - Number of script strings (tags)
        public const int ScriptStringsPtrOffset = 0x28;      // 4 bytes - Pointer to script strings (0xFFFFFFFF placeholder)
        public const int AssetCountOffset = 0x2C;            // 4 bytes - Number of assets in zone
        public const int AssetsPtrOffset = 0x30;             // 4 bytes - Pointer to assets array (0xFFFFFFFF placeholder)

        // XAssetList offsets for Xbox 360 (6 blocks = 32 bytes XFile header)
        public const int Xbox360_ScriptStringCountOffset = 0x20;
        public const int Xbox360_ScriptStringsPtrOffset = 0x24;
        public const int Xbox360_AssetCountOffset = 0x28;
        public const int Xbox360_AssetsPtrOffset = 0x2C;

        // XAssetList offsets for PC (8 blocks = 40 bytes XFile header)
        public const int PC_ScriptStringCountOffset = 0x28;
        public const int PC_ScriptStringsPtrOffset = 0x2C;
        public const int PC_AssetCountOffset = 0x30;
        public const int PC_AssetsPtrOffset = 0x34;
    }
}
