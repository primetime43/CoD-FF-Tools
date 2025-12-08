using Call_of_Duty_FastFile_Editor.Models;
using System.Diagnostics;
using System.Text;

namespace Call_of_Duty_FastFile_Editor.GameDefinitions
{
    /// <summary>
    /// Game definition implementation for Call of Duty: Modern Warfare 2.
    /// MW2 may have different rawfile structure compared to CoD4/CoD5.
    /// </summary>
    public class MW2GameDefinition : GameDefinitionBase
    {
        public override string GameName => MW2Definition.GameName;
        public override string ShortName => "MW2";
        public override int VersionValue => MW2Definition.VersionValue;
        public override int PCVersionValue => MW2Definition.PCVersionValue;
        public override byte[] VersionBytes => MW2Definition.VersionBytes;
        public override byte RawFileAssetType => MW2Definition.RawFileAssetType;
        public override byte LocalizeAssetType => MW2Definition.LocalizeAssetType;
        public override byte MenuFileAssetType => 0x19; // menufile type for MW2
        public override byte XAnimAssetType => MW2Definition.XAnimAssetType;
        public override byte StringTableAssetType => MW2Definition.StringTableAssetType;

        public override string GetAssetTypeName(int assetType)
        {
            if (Enum.IsDefined(typeof(MW2AssetType), assetType))
            {
                return ((MW2AssetType)assetType).ToString();
            }
            return $"unknown_0x{assetType:X2}";
        }

        public override bool IsSupportedAssetType(int assetType)
        {
            return assetType == RawFileAssetType ||
                   assetType == LocalizeAssetType ||
                   assetType == MenuFileAssetType ||
                   assetType == XAnimAssetType ||
                   assetType == StringTableAssetType;
        }

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
