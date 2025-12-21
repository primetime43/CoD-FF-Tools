using Call_of_Duty_FastFile_Editor.Models;
using FastFileLib.GameDefinitions;
using System.Diagnostics;
using System.Text;

namespace Call_of_Duty_FastFile_Editor.GameDefinitions
{
    /// <summary>
    /// Game definition implementation for Call of Duty: Modern Warfare 2.
    /// Supports PS3, Xbox 360, and PC with platform-specific asset type IDs.
    /// MW2 uses a different rawfile structure compared to CoD4/CoD5 (16-byte header with optional compression).
    /// </summary>
    public class MW2GameDefinition : GameDefinitionBase
    {
        /// <summary>
        /// Creates a MW2 game definition for PS3 (default).
        /// </summary>
        public MW2GameDefinition() : this(isXbox360: false, isPC: false) { }

        /// <summary>
        /// Creates a MW2 game definition for the specified platform.
        /// </summary>
        /// <param name="isXbox360">True for Xbox 360.</param>
        /// <param name="isPC">True for PC.</param>
        public MW2GameDefinition(bool isXbox360, bool isPC = false)
        {
            IsXbox360 = isXbox360;
            IsPC = isPC;
        }

        public override string GameName => MW2Definition.GameName;
        public override string ShortName => IsPC ? "MW2 (PC)" : (IsXbox360 ? "MW2 (Xbox)" : "MW2");
        public override int VersionValue => MW2Definition.VersionValue;
        public override int PCVersionValue => MW2Definition.PCVersionValue;
        public override byte[] VersionBytes => MW2Definition.VersionBytes;

        // Platform-aware asset type IDs
        // Xbox 360 doesn't have vertexshader (0x07), so all types >= 0x07 are shifted by -1 from PS3
        // PC has both vertexshader (0x07) and vertexdecl (0x08), so types >= 0x09 are shifted by +1 from PS3
        public override byte RawFileAssetType => IsPC
            ? (byte)MW2AssetTypePC.rawfile
            : (IsXbox360 ? (byte)MW2AssetTypeXbox360.rawfile : (byte)MW2AssetTypePS3.rawfile);

        public override byte LocalizeAssetType => IsPC
            ? (byte)MW2AssetTypePC.localize
            : (IsXbox360 ? (byte)MW2AssetTypeXbox360.localize : (byte)MW2AssetTypePS3.localize);

        public override byte MenuFileAssetType => IsPC
            ? (byte)MW2AssetTypePC.menufile
            : (IsXbox360 ? (byte)MW2AssetTypeXbox360.menufile : (byte)MW2AssetTypePS3.menufile);

        /// <summary>
        /// MW2 has a separate 'menu' asset type for individual menu definitions (distinct from menufile/MenuList).
        /// </summary>
        public byte MenuAssetType => IsPC
            ? (byte)MW2AssetTypePC.menu
            : (IsXbox360 ? (byte)MW2AssetTypeXbox360.menu : (byte)MW2AssetTypePS3.menu);

        public override byte XAnimAssetType => IsPC
            ? (byte)MW2AssetTypePC.xanim
            : (IsXbox360 ? (byte)MW2AssetTypeXbox360.xanim : (byte)MW2AssetTypePS3.xanim);

        public override byte StringTableAssetType => IsPC
            ? (byte)MW2AssetTypePC.stringtable
            : (IsXbox360 ? (byte)MW2AssetTypeXbox360.stringtable : (byte)MW2AssetTypePS3.stringtable);

        public override byte WeaponAssetType => IsPC
            ? (byte)MW2AssetTypePC.weapon
            : (IsXbox360 ? (byte)MW2AssetTypeXbox360.weapon : (byte)MW2AssetTypePS3.weapon);

        public override byte ImageAssetType => IsPC
            ? (byte)MW2AssetTypePC.image
            : (IsXbox360 ? (byte)MW2AssetTypeXbox360.image : (byte)MW2AssetTypePS3.image);

        public override string GetAssetTypeName(int assetType)
        {
            if (IsPC)
            {
                if (Enum.IsDefined(typeof(MW2AssetTypePC), assetType))
                    return ((MW2AssetTypePC)assetType).ToString();
            }
            else if (IsXbox360)
            {
                if (Enum.IsDefined(typeof(MW2AssetTypeXbox360), assetType))
                    return ((MW2AssetTypeXbox360)assetType).ToString();
            }
            else
            {
                if (Enum.IsDefined(typeof(MW2AssetTypePS3), assetType))
                    return ((MW2AssetTypePS3)assetType).ToString();
            }
            return $"unknown_0x{assetType:X2}";
        }

        public override bool IsSupportedAssetType(int assetType)
        {
            return assetType == RawFileAssetType ||
                   assetType == LocalizeAssetType ||
                   assetType == MenuFileAssetType ||
                   assetType == MenuAssetType ||  // Include 'menu' as supported to prevent it being treated as rawfile
                   assetType == XAnimAssetType ||
                   assetType == StringTableAssetType ||
                   assetType == WeaponAssetType ||
                   assetType == ImageAssetType;
        }

        /// <summary>
        /// Checks if the asset type is the 'menu' type (individual menu definitions, NOT menufile/MenuList).
        /// </summary>
        public bool IsMenuType(int assetType) => assetType == MenuAssetType;

        /// <summary>
        /// MW2 rawfile parsing with 16-byte header structure.
        ///
        /// MW2 RawFile structure (different from CoD4/WaW):
        /// struct RawFile {
        ///     const char *name;      // 4 bytes - 0xFFFFFFFF pointer placeholder
        ///     int compressedLen;     // 4 bytes - compressed size (0 if uncompressed)
        ///     int len;               // 4 bytes - decompressed/actual length
        ///     const char *buffer;    // 4 bytes - 0xFFFFFFFF pointer placeholder
        /// };
        ///
        /// Zone layout: [FF FF FF FF] [compressedLen BE] [len BE] [FF FF FF FF] [name\0] [data]
        /// </summary>
        public override RawFileNode? ParseRawFile(byte[] zoneData, int offset)
        {
            Debug.WriteLine($"[MW2] ParseRawFile at offset 0x{offset:X}");

            // Try MW2 16-byte header format first
            var result = TryParseMW2Format(zoneData, offset);
            if (result != null)
            {
                Debug.WriteLine($"[MW2] Parsed using MW2 16-byte format");
                return result;
            }

            // Fallback to standard 12-byte format (for compatibility)
            result = TryParseStandardFormat(zoneData, offset);
            if (result != null)
            {
                Debug.WriteLine($"[MW2] Parsed using standard 12-byte format (fallback)");
                return result;
            }

            Debug.WriteLine($"[MW2] Failed to parse rawfile at 0x{offset:X}");
            return null;
        }

        /// <summary>
        /// MW2 16-byte header format:
        /// [FF FF FF FF] [compressedLen BE] [len BE] [FF FF FF FF] [name\0] [data]
        ///
        /// If compressedLen > 0, data is zlib compressed.
        /// If compressedLen == 0, data is uncompressed with length = len.
        /// </summary>
        private RawFileNode? TryParseMW2Format(byte[] zoneData, int offset)
        {
            // Need at least 16 bytes for header
            if (offset + 16 > zoneData.Length) return null;

            // First marker (name pointer placeholder)
            uint marker1 = ReadUInt32BE(zoneData, offset);
            if (marker1 != 0xFFFFFFFF) return null;

            // Compressed length (0 if uncompressed)
            int compressedLen = ReadInt32BE(zoneData, offset + 4);

            // Decompressed/actual length
            int len = ReadInt32BE(zoneData, offset + 8);

            // Second marker (buffer pointer placeholder)
            uint marker2 = ReadUInt32BE(zoneData, offset + 12);
            if (marker2 != 0xFFFFFFFF) return null;

            // Validate lengths
            if (len <= 0 || len > 10_000_000) return null;
            if (compressedLen < 0 || compressedLen > 10_000_000) return null;

            // The actual data size in the zone
            int dataSize = compressedLen > 0 ? compressedLen : len;

            // Read filename (starts after 16-byte header)
            int fileNameOffset = offset + 16;
            if (fileNameOffset >= zoneData.Length) return null;

            // Check for valid filename start
            byte firstChar = zoneData[fileNameOffset];
            if (firstChar < 0x20 || firstChar > 0x7E) return null;

            string fileName = ReadNullTerminatedString(zoneData, fileNameOffset);
            if (string.IsNullOrEmpty(fileName)) return null;

            // Validate filename looks like a file path
            if (!fileName.Contains('/') && !fileName.Contains('.') && !fileName.Contains('\\'))
            {
                // Allow some common filenames without paths
                if (fileName.Length < 3) return null;
            }

            // IMPORTANT: Reject MenuList structures that look like rawfiles
            // MenuList has a 12-byte header but could be misinterpreted as 16-byte with small values
            // If the name ends with .menu and the "size" is small, this is a MenuList asset
            if (fileName.EndsWith(".menu", StringComparison.OrdinalIgnoreCase) && len < 500)
            {
                Debug.WriteLine($"[MW2] Rejecting '{fileName}' as rawfile (16-byte) - likely a MenuList (len={len})");
                return null;
            }

            int nameByteCount = Encoding.ASCII.GetByteCount(fileName) + 1; // +1 for null terminator
            int fileDataOffset = fileNameOffset + nameByteCount;

            if (fileDataOffset + dataSize > zoneData.Length) return null;

            var node = new RawFileNode
            {
                StartOfFileHeader = offset,
                MaxSize = len, // Use decompressed length as MaxSize
                FileName = fileName,
                HeaderSize = 16 // MW2 uses 16-byte header
            };

            // Extract data
            byte[] rawBytes;
            if (compressedLen > 0)
            {
                // Data is compressed - decompress it
                byte[] compressedData = new byte[compressedLen];
                Array.Copy(zoneData, fileDataOffset, compressedData, 0, compressedLen);
                rawBytes = DecompressZlib(compressedData, len);

                // Store additional info about compression
                node.AdditionalData = $"Compressed: {compressedLen} -> {len} bytes";
            }
            else
            {
                // Data is uncompressed
                rawBytes = new byte[len];
                Array.Copy(zoneData, fileDataOffset, rawBytes, 0, len);
            }

            node.RawFileBytes = rawBytes;
            node.RawFileContent = Encoding.UTF8.GetString(rawBytes);
            // End position is after the data in the zone (use actual data size, not decompressed)
            node.RawFileEndPosition = fileDataOffset + dataSize + 1; // +1 for null terminator

            Debug.WriteLine($"[MW2] Found rawfile: '{fileName}' len={len} compressedLen={compressedLen}");
            return node;
        }

        /// <summary>
        /// Fallback: Standard 12-byte format (same as CoD4/WaW):
        /// [FF FF FF FF] [len BE] [FF FF FF FF] [name\0] [data]
        /// </summary>
        private RawFileNode? TryParseStandardFormat(byte[] zoneData, int offset)
        {
            if (offset + 12 > zoneData.Length) return null;

            uint marker1 = ReadUInt32BE(zoneData, offset);
            if (marker1 != 0xFFFFFFFF) return null;

            int dataLength = ReadInt32BE(zoneData, offset + 4);
            if (dataLength <= 0 || dataLength > 10_000_000) return null;

            uint marker2 = ReadUInt32BE(zoneData, offset + 8);
            if (marker2 != 0xFFFFFFFF) return null;

            int fileNameOffset = offset + 12;
            if (fileNameOffset >= zoneData.Length) return null;

            byte firstChar = zoneData[fileNameOffset];
            if (firstChar < 0x20 || firstChar > 0x7E) return null;

            string fileName = ReadNullTerminatedString(zoneData, fileNameOffset);
            if (string.IsNullOrEmpty(fileName)) return null;

            // IMPORTANT: Reject MenuList structures that look like rawfiles
            // MenuList has the same 12-byte header: [FF FF FF FF] [menuCount] [FF FF FF FF] [name]
            // If the name ends with .menu and the "size" (actually menuCount) is small,
            // this is a MenuList asset, not a rawfile. MenuList assets should be handled by ParseMenuFile.
            // Real .menu rawfiles (source scripts) are much larger than menu counts (typically 1-50).
            if (fileName.EndsWith(".menu", StringComparison.OrdinalIgnoreCase) && dataLength < 500)
            {
                Debug.WriteLine($"[MW2] Rejecting '{fileName}' as rawfile - likely a MenuList (size={dataLength})");
                return null;
            }

            // IMPORTANT: Also reject files ending with .txt that have suspiciously small sizes
            // and look like they could be menu-related (e.g., "ui_mp/patch_mp_menus.txt")
            // The 'menu' asset type data can have various names, not just .menu extension
            if (fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) &&
                fileName.Contains("menu", StringComparison.OrdinalIgnoreCase) &&
                dataLength < 500)
            {
                Debug.WriteLine($"[MW2] Rejecting '{fileName}' as rawfile - likely a menu asset (size={dataLength})");
                return null;
            }

            var node = new RawFileNode
            {
                StartOfFileHeader = offset,
                MaxSize = dataLength,
                FileName = fileName
            };

            int nameByteCount = Encoding.ASCII.GetByteCount(fileName) + 1;
            int fileDataOffset = fileNameOffset + nameByteCount;

            if (fileDataOffset + dataLength <= zoneData.Length)
            {
                byte[] rawBytes = new byte[dataLength];
                Array.Copy(zoneData, fileDataOffset, rawBytes, 0, dataLength);
                node.RawFileBytes = rawBytes;
                node.RawFileContent = Encoding.UTF8.GetString(rawBytes);
                node.RawFileEndPosition = fileDataOffset + dataLength + 1;
            }
            else
            {
                node.RawFileBytes = Array.Empty<byte>();
                node.RawFileContent = string.Empty;
            }

            Debug.WriteLine($"[MW2] Found rawfile (standard format): '{fileName}' size={dataLength}");
            return node;
        }

        /// <summary>
        /// Decompress zlib-compressed data.
        /// </summary>
        private byte[] DecompressZlib(byte[] compressedData, int expectedLength)
        {
            try
            {
                using var inputStream = new MemoryStream(compressedData);
                using var zlibStream = new System.IO.Compression.ZLibStream(inputStream, System.IO.Compression.CompressionMode.Decompress);
                using var outputStream = new MemoryStream();
                zlibStream.CopyTo(outputStream);
                return outputStream.ToArray();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MW2] Zlib decompression failed: {ex.Message}");
                // Return the raw compressed data if decompression fails
                return compressedData;
            }
        }
    }
}
