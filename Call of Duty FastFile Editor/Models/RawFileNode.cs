using Call_of_Duty_FastFile_Editor.Services;
using System;
using System.Net;
using System.Text;

namespace Call_of_Duty_FastFile_Editor.Models
{
    /// <summary>
    /// Represents a raw file stored within a FastFile, containing metadata and content.
    /// </summary>
    public class RawFileNode : IAssetRecordUpdatable
    {
        // Backing field for the header bytes.
        private byte[] _header;

        /// <summary>
        /// Has this file’s content been edited (but not yet saved) in the UI?
        /// </summary>
        public bool HasUnsavedChanges { get; set; }

        public RawFileNode() { }

        // Constructor that initializes the necessary properties.
        public RawFileNode(string name, byte[] buffer)
        {
            FileName = name;
            RawFileBytes = buffer;
            MaxSize = buffer.Length;  // Use MaxSize consistently
        }

        /// <summary>
        /// Static property to hold the currently loaded zone.
        /// </summary>
        public static ZoneFile CurrentZone { get; set; }

        public byte[] Header => Utilities.GetBytesAtOffset(StartOfFileHeader, CurrentZone, EndOfFileHeader - StartOfFileHeader);

        /// <summary>
        /// The position where one of the patterns was found in the file.
        /// </summary>
        public int PatternIndexPosition { get; set; }

        /// <summary>
        /// Maximum allowed size for the file's content.
        /// </summary>
        public int MaxSize { get; set; }

        /// <summary>
        /// Maximum allowed size for the file's content in hex format.
        /// Computed as the big-endian representation of MaxSize.
        /// </summary>
        public string MaxSizeHex => Utilities.ConvertToBigEndianHex(MaxSize);

        public int StartOfFileHeader { get; set; }

        /// <summary>
        /// Size of the rawfile header in bytes.
        /// CoD4/WaW use 12-byte header: [FF FF FF FF] [len] [FF FF FF FF]
        /// MW2 uses 16-byte header: [FF FF FF FF] [compressedLen] [len] [FF FF FF FF]
        /// Default is 12 for backwards compatibility.
        /// </summary>
        public int HeaderSize { get; set; } = 12;

        /// <summary>
        /// Indicates whether this raw file's data is internally zlib compressed.
        /// This is common in MW2 PS3 FastFiles where raw files use the 16-byte header format
        /// with compressedLen != len.
        /// </summary>
        public bool IsCompressed { get; set; }

        /// <summary>
        /// The compressed size of the raw file data in bytes.
        /// Only relevant when IsCompressed is true.
        /// This is the actual size of the data stored in the zone file.
        /// </summary>
        public int CompressedSize { get; set; }

        /// <summary>
        /// Backing field for CodeStartPosition when set explicitly by game-specific parsers.
        /// </summary>
        private int? _codeStartPosition;

        /// <summary>
        /// Gets or sets the position where the code starts.
        /// When set explicitly, uses that value. Otherwise, calculated as StartOfFileHeader + HeaderSize + FileName.Length + 1.
        /// </summary>
        public int CodeStartPosition
        {
            get => _codeStartPosition ?? (StartOfFileHeader + HeaderSize + (FileName?.Length ?? 0) + 1);
            set => _codeStartPosition = value;
        }

        /// <summary>
        /// Gets the position where the code ends, calculated as CodeStartPosition + MaxSize
        /// Minus 1 because the last byte is a null terminator.
        /// </summary>
        public int CodeEndPosition => CodeStartPosition + MaxSize - 1;

        /// <summary>
        /// Where the data ends
        /// Includes null byte
        /// </summary>
        public int EndOfFileHeader => CodeStartPosition - 1;

        /// <summary>
        /// The name of the file.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Backing field for RawFileEndPosition when set explicitly by game-specific parsers.
        /// </summary>
        private int? _rawFileEndPosition;

        /// <summary>
        /// Gets or sets the position where the next asset starts.
        /// When set explicitly, uses that value. Otherwise, calculated as CodeStartPosition + MaxSize + 1.
        /// The +1 accounts for the null terminator at the end of the buffer data.
        /// Per wiki: "Buffer's length is len plus one for the null byte at the end."
        /// </summary>
        public int RawFileEndPosition
        {
            get => _rawFileEndPosition ?? (CodeStartPosition + MaxSize + 1);
            set => _rawFileEndPosition = value;
        }

        /// <summary>
        /// The content of the file as a string.
        /// </summary>
        public string RawFileContent { get; set; }

        /// <summary>
        /// The content of the file as a byte array.
        /// </summary>
        public byte[] RawFileBytes { get; set; }

        public string AdditionalData { get; set; }

        /// <summary>
        /// Converts the provided new file name to its ASCII byte representation without appending a null terminator.
        /// </summary>
        /// <param name="newFileName">The new file name.</param>
        /// <returns>A byte array representing the new file name without a null terminator.</returns>
        public byte[] GetFileNameBytes(string newFileName)
        {
            // Convert the new file name to ASCII bytes without adding a null terminator.
            return Encoding.ASCII.GetBytes(newFileName);
        }

        public void UpdateAssetRecord(ref ZoneAssetRecord assetRecord)
        {
            assetRecord.HeaderStartOffset = this.StartOfFileHeader;
            assetRecord.HeaderEndOffset = this.EndOfFileHeader;
            assetRecord.AssetDataStartPosition = this.CodeStartPosition;
            assetRecord.AssetDataEndOffset = this.CodeEndPosition;
            assetRecord.AssetRecordEndOffset = this.RawFileEndPosition;
            assetRecord.Name = this.FileName;
            assetRecord.RawDataBytes = this.RawFileBytes;
            assetRecord.Size = this.MaxSize;
            assetRecord.Content = this.RawFileContent;
            assetRecord.AdditionalData = this.AdditionalData;
        }
    }
}
