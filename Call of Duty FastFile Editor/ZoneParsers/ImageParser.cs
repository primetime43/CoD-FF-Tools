using Call_of_Duty_FastFile_Editor.Models;
using System.Diagnostics;
using System.Text;

namespace Call_of_Duty_FastFile_Editor.ZoneParsers
{
    /// <summary>
    /// Parser for GfxImage assets in zone files.
    /// Reference: https://codresearch.dev/index.php/Image_Asset
    ///
    /// GfxImage structure (PS3) - reading backwards from name:
    /// - name (null-terminated string)
    /// - name pointer: 4 bytes (FF FF FF FF when inline)
    /// - data pointer: 4 bytes (FF FF FF FF when inline)
    /// - streaming: 1 byte
    /// - category: 1 byte
    /// - depth: 2 bytes (big-endian)
    /// - height: 2 bytes (big-endian)
    /// - width: 2 bytes (big-endian)
    /// - size: 4 bytes (big-endian)
    /// - ... more header data before
    /// </summary>
    public static class ImageParser
    {
        // Offsets from name position (negative = before name)
        private const int NAME_PTR_OFFSET = -4;      // FF FF FF FF
        private const int DATA_PTR_OFFSET = -8;      // FF FF FF FF
        private const int STREAMING_OFFSET = -9;     // 1 byte
        private const int CATEGORY_OFFSET = -10;     // 1 byte
        private const int DEPTH_OFFSET = -12;        // 2 bytes BE
        private const int HEIGHT_OFFSET = -14;       // 2 bytes BE
        private const int WIDTH_OFFSET = -16;        // 2 bytes BE
        private const int SIZE_OFFSET = -20;         // 4 bytes BE

        // CellGcmTexture format byte location
        // Based on hex analysis: format byte (0x87 for DXT3) is at offset -48 from name
        // Layout before name: format(1) + mipmap(1) + dimension(1) + ... + size(4) + w(2) + h(2) + d(2) + cat(1) + stream(1) + dataptr(4) + nameptr(4)
        private const int FORMAT_OFFSET = -48;       // Texture format byte

        // Minimum header size before name
        private const int MIN_HEADER_SIZE = 56;

        /// <summary>
        /// Parses a GfxImage asset from zone data by finding the name and reading header backwards.
        /// </summary>
        /// <param name="zoneData">The zone file data.</param>
        /// <param name="nameOffset">Offset where the image name string starts.</param>
        /// <param name="isBigEndian">Whether data is big-endian (PS3).</param>
        /// <returns>Parsed ImageAsset, or null if parsing failed.</returns>
        public static ImageAsset? ParseImageAtName(byte[] zoneData, int nameOffset, bool isBigEndian = true)
        {
            // Need space for header before name
            if (nameOffset < MIN_HEADER_SIZE || nameOffset >= zoneData.Length)
            {
                return null;
            }

            // Check for FF FF FF FF markers at data and name pointer positions
            if (zoneData[nameOffset + DATA_PTR_OFFSET] != 0xFF ||
                zoneData[nameOffset + DATA_PTR_OFFSET + 1] != 0xFF ||
                zoneData[nameOffset + DATA_PTR_OFFSET + 2] != 0xFF ||
                zoneData[nameOffset + DATA_PTR_OFFSET + 3] != 0xFF)
            {
                return null;
            }

            if (zoneData[nameOffset + NAME_PTR_OFFSET] != 0xFF ||
                zoneData[nameOffset + NAME_PTR_OFFSET + 1] != 0xFF ||
                zoneData[nameOffset + NAME_PTR_OFFSET + 2] != 0xFF ||
                zoneData[nameOffset + NAME_PTR_OFFSET + 3] != 0xFF)
            {
                return null;
            }

            // Read the name string
            string name = ReadNullTerminatedString(zoneData, nameOffset);

            // Validate the name
            if (!IsValidImageName(name))
            {
                return null;
            }

            // Read header fields (backwards from name)
            bool streaming = zoneData[nameOffset + STREAMING_OFFSET] != 0;
            byte category = zoneData[nameOffset + CATEGORY_OFFSET];
            ushort depth = ReadUInt16(zoneData, nameOffset + DEPTH_OFFSET, isBigEndian);
            ushort height = ReadUInt16(zoneData, nameOffset + HEIGHT_OFFSET, isBigEndian);
            ushort width = ReadUInt16(zoneData, nameOffset + WIDTH_OFFSET, isBigEndian);
            int dataSize = ReadInt32(zoneData, nameOffset + SIZE_OFFSET, isBigEndian);

            // Read texture format from CellGcmTexture structure
            byte textureFormat = zoneData[nameOffset + FORMAT_OFFSET];

            // Validate dimensions
            if (width == 0 || height == 0 || width > 8192 || height > 8192)
            {
                Debug.WriteLine($"[ImageParser] Invalid dimensions {width}x{height} for '{name}'");
                return null;
            }

            if (depth == 0 || depth > 512)
            {
                Debug.WriteLine($"[ImageParser] Invalid depth {depth} for '{name}'");
                return null;
            }

            // Validate data size
            int maxReasonableSize = width * height * 8;
            if (dataSize <= 0 || dataSize > maxReasonableSize)
            {
                Debug.WriteLine($"[ImageParser] Invalid data size {dataSize} for '{name}' ({width}x{height})");
                return null;
            }

            int nameEndOffset = nameOffset + Encoding.ASCII.GetByteCount(name) + 1;

            // For non-streaming images, image data follows the name
            int dataOffset = nameEndOffset;
            byte[]? rawData = null;

            if (!streaming && dataSize > 0 && dataOffset + dataSize <= zoneData.Length)
            {
                rawData = new byte[dataSize];
                Array.Copy(zoneData, dataOffset, rawData, 0, dataSize);
            }

            // Calculate approximate start offset (we don't know exact header start)
            int startOffset = nameOffset + SIZE_OFFSET - 36; // Approximate based on structure

            var asset = new ImageAsset
            {
                Name = name,
                MapType = 0, // We can't reliably read this with backwards parsing
                Width = width,
                Height = height,
                Depth = depth,
                DataSize = dataSize,
                Category = category,
                IsStreaming = streaming,
                TextureFormat = textureFormat,
                RawData = rawData,
                DataOffset = dataOffset,
                StartOffset = startOffset,
                EndOffset = streaming ? nameEndOffset : nameEndOffset + dataSize,
                AdditionalData = $"Parsed from name at 0x{nameOffset:X}, format=0x{textureFormat:X2}"
            };

            Debug.WriteLine($"[ImageParser] Found image: '{name}' ({width}x{height}, {dataSize} bytes, streaming={streaming}, format=0x{textureFormat:X2})");
            return asset;
        }

        /// <summary>
        /// Searches for images by finding potential image names in the zone data.
        /// </summary>
        public static ImageAsset? FindAndParseImage(byte[] zoneData, int startOffset, int maxSearchRange = 1000000)
        {
            int searchEnd = Math.Min(startOffset + maxSearchRange, zoneData.Length - 1);

            // Search for FF FF FF FF FF FF FF FF pattern (data + name pointers)
            for (int i = startOffset; i < searchEnd - 8; i++)
            {
                // Look for 8 consecutive FF bytes followed by printable ASCII
                if (zoneData[i] == 0xFF && zoneData[i + 1] == 0xFF &&
                    zoneData[i + 2] == 0xFF && zoneData[i + 3] == 0xFF &&
                    zoneData[i + 4] == 0xFF && zoneData[i + 5] == 0xFF &&
                    zoneData[i + 6] == 0xFF && zoneData[i + 7] == 0xFF)
                {
                    // Check if there's a valid string after the markers
                    int namePos = i + 8;
                    if (namePos < zoneData.Length && zoneData[namePos] >= 0x20 && zoneData[namePos] <= 0x7E)
                    {
                        var result = ParseImageAtName(zoneData, namePos);
                        if (result != null)
                            return result;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Legacy method for backwards compatibility.
        /// </summary>
        public static ImageAsset? ParseImage(byte[] zoneData, int offset, bool isBigEndian = true)
        {
            return FindAndParseImage(zoneData, offset, 256);
        }

        /// <summary>
        /// Validates that a string looks like a valid image name.
        /// </summary>
        private static bool IsValidImageName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length < 2 || name.Length > 256)
                return false;

            // All characters must be printable ASCII
            foreach (char c in name)
            {
                if (c < 0x20 || c > 0x7E)
                    return false;
            }

            // Reject names that are clearly rawfiles/scripts (not images)
            string nameLower = name.ToLowerInvariant();
            string[] invalidExtensions = { ".cfg", ".gsc", ".csc", ".txt", ".csv", ".menu", ".vision", ".arena", ".str", ".def" };
            foreach (var ext in invalidExtensions)
            {
                if (nameLower.EndsWith(ext))
                    return false;
            }

            // Check for excessive repeated characters
            int maxRepeats = 4;
            int currentRepeats = 1;
            char prevChar = '\0';
            foreach (char c in name)
            {
                if (c == prevChar)
                {
                    currentRepeats++;
                    if (currentRepeats > maxRepeats)
                        return false;
                }
                else
                {
                    currentRepeats = 1;
                }
                prevChar = c;
            }

            // Image names typically contain certain patterns
            // They often start with specific prefixes or contain underscores/slashes
            bool hasValidPattern = name.Contains("_") || name.Contains("/") || name.Contains("\\") ||
                                   name.StartsWith("~") || name.StartsWith("$") ||
                                   name.Contains("map") || name.Contains("gfx") ||
                                   name.Contains("mtl") || name.Contains("fx") ||
                                   char.IsLetter(name[0]);

            return hasValidPattern;
        }

        private static string ReadNullTerminatedString(byte[] data, int offset)
        {
            var sb = new StringBuilder();
            while (offset < data.Length && data[offset] != 0x00)
            {
                sb.Append((char)data[offset]);
                offset++;
            }
            return sb.ToString();
        }

        private static int ReadInt32(byte[] data, int offset, bool bigEndian)
        {
            if (bigEndian)
                return (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
            else
                return data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24);
        }

        private static ushort ReadUInt16(byte[] data, int offset, bool bigEndian)
        {
            if (bigEndian)
                return (ushort)((data[offset] << 8) | data[offset + 1]);
            else
                return (ushort)(data[offset] | (data[offset + 1] << 8));
        }
    }
}
