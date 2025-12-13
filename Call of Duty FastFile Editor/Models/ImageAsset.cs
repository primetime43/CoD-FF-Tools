namespace Call_of_Duty_FastFile_Editor.Models
{
    /// <summary>
    /// Represents a GfxImage asset parsed from a zone file.
    /// Reference: https://codresearch.dev/index.php/Image_Asset
    ///
    /// Structure (PS3):
    /// - MapType mapType (4 bytes)
    /// - CellGcmTexture texture (0x18 = 24 bytes)
    /// - CardMemory cardMemory (8 bytes)
    /// - int size (4 bytes)
    /// - ushort width (2 bytes)
    /// - ushort height (2 bytes)
    /// - ushort depth (2 bytes)
    /// - char category (1 byte)
    /// - bool streaming (1 byte)
    /// - char *data (4 bytes pointer)
    /// - const char *name (4 bytes pointer)
    /// </summary>
    public class ImageAsset
    {
        /// <summary>
        /// The name of the image asset.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Map type (semantic, 2D, cube, etc.)
        /// </summary>
        public int MapType { get; set; }

        /// <summary>
        /// Image width in pixels.
        /// </summary>
        public ushort Width { get; set; }

        /// <summary>
        /// Image height in pixels.
        /// </summary>
        public ushort Height { get; set; }

        /// <summary>
        /// Image depth (for 3D/volume textures).
        /// </summary>
        public ushort Depth { get; set; }

        /// <summary>
        /// Size of image data in bytes.
        /// </summary>
        public int DataSize { get; set; }

        /// <summary>
        /// Image category.
        /// </summary>
        public byte Category { get; set; }

        /// <summary>
        /// Whether the image data is streamed (loaded from end of zone file).
        /// </summary>
        public bool IsStreaming { get; set; }

        /// <summary>
        /// Texture format from CellGcmTexture header.
        /// Common values: 0x85 = DXT1, 0x86 = DXT3, 0x88 = DXT5, 0x85 = A8R8G8B8
        /// </summary>
        public byte TextureFormat { get; set; }

        /// <summary>
        /// Raw image data bytes (for non-streaming images).
        /// </summary>
        public byte[]? RawData { get; set; }

        /// <summary>
        /// Offset where the image data starts in the zone file.
        /// </summary>
        public int DataOffset { get; set; }

        /// <summary>
        /// Starting offset of this asset in the zone file.
        /// </summary>
        public int StartOffset { get; set; }

        /// <summary>
        /// Ending offset of this asset in the zone file.
        /// </summary>
        public int EndOffset { get; set; }

        /// <summary>
        /// Additional parsing information.
        /// </summary>
        public string AdditionalData { get; set; } = string.Empty;

        /// <summary>
        /// Gets a formatted resolution string (e.g., "512x512").
        /// </summary>
        public string Resolution => $"{Width}x{Height}";

        /// <summary>
        /// Gets a formatted size string (e.g., "256 KB").
        /// </summary>
        public string FormattedSize
        {
            get
            {
                if (DataSize < 1024)
                    return $"{DataSize} B";
                else if (DataSize < 1024 * 1024)
                    return $"{DataSize / 1024.0:F1} KB";
                else
                    return $"{DataSize / (1024.0 * 1024.0):F2} MB";
            }
        }

        public override string ToString()
        {
            return $"{Name} ({Resolution}, {FormattedSize})";
        }
    }
}
