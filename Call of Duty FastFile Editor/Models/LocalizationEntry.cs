using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Call_of_Duty_FastFile_Editor.Models
{
    public class LocalizedEntry : IAssetRecordUpdatable
    {
        /// <summary>
        /// Original key bytes from zone file. Use this for writing back to zone.
        /// </summary>
        public byte[] KeyBytes { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Original localized text bytes from zone file. Use this for writing back to zone.
        /// </summary>
        public byte[] TextBytes { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Key as string for UI display. Setting this updates KeyBytes.
        /// </summary>
        public string Key
        {
            get => KeyBytes.Length > 0 ? BytesToString(KeyBytes) : string.Empty;
            set => KeyBytes = StringToBytes(value ?? string.Empty);
        }

        /// <summary>
        /// Localized text as string for UI display. Setting this updates TextBytes.
        /// </summary>
        public string LocalizedText
        {
            get => TextBytes.Length > 0 ? BytesToString(TextBytes) : string.Empty;
            set => TextBytes = StringToBytes(value ?? string.Empty);
        }

        public int StartOfFileHeader { get; set; }
        public int EndOfFileHeader { get; set; }
        public int StartOfFileData { get; set; }
        public int EndOfFileData { get; set; }

        /// <summary>
        /// Original offset in zone where the key starts (after text null terminator).
        /// Used for in-place patching to keep key at its original position.
        /// </summary>
        public int KeyStartOffset { get; set; }

        /// <summary>
        /// Additional data about how this entry was parsed (e.g., "Structure-based" or "Pattern matching").
        /// </summary>
        public string AdditionalData { get; set; } = string.Empty;

        public void UpdateAssetRecord(ref ZoneAssetRecord assetRecord)
        {
            assetRecord.AssetDataStartPosition = this.StartOfFileHeader;
            assetRecord.AssetDataEndOffset = this.EndOfFileHeader;
            assetRecord.Name = this.Key;

            //this is needed for the loop in AssetRecordProcessor
            assetRecord.AssetRecordEndOffset = this.EndOfFileHeader;
        }

        /// <summary>
        /// Converts bytes to string using direct byte-to-char mapping (Latin1-style).
        /// Each byte value becomes the corresponding Unicode code point.
        /// </summary>
        private static string BytesToString(byte[] bytes)
        {
            var chars = new char[bytes.Length];
            for (int i = 0; i < bytes.Length; i++)
                chars[i] = (char)bytes[i];
            return new string(chars);
        }

        /// <summary>
        /// Converts string to bytes using direct char-to-byte mapping.
        /// Characters above 0xFF are truncated to their low byte.
        /// </summary>
        private static byte[] StringToBytes(string str)
        {
            var bytes = new byte[str.Length];
            for (int i = 0; i < str.Length; i++)
                bytes[i] = (byte)str[i];
            return bytes;
        }
    }
}