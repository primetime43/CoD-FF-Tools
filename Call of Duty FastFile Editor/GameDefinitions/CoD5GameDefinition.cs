using Call_of_Duty_FastFile_Editor.Models;
using System.Diagnostics;
using System.Text;

namespace Call_of_Duty_FastFile_Editor.GameDefinitions
{
    /// <summary>
    /// Game definition implementation for Call of Duty: World at War (CoD5).
    /// Uses the default CoD4/CoD5 rawfile parsing structure.
    /// </summary>
    public class CoD5GameDefinition : GameDefinitionBase
    {
        public override string GameName => CoD5Definition.GameName;
        public override string ShortName => "COD5";
        public override int VersionValue => CoD5Definition.VersionValue;
        public override int PCVersionValue => CoD5Definition.PCVersionValue;
        public override byte[] VersionBytes => CoD5Definition.VersionBytes;
        public override byte RawFileAssetType => CoD5Definition.RawFileAssetType;
        public override byte LocalizeAssetType => CoD5Definition.LocalizeAssetType;
        public override byte MenuFileAssetType => CoD5Definition.MenuFileAssetType;
        public override byte XAnimAssetType => CoD5Definition.XAnimAssetType;
        public override byte StringTableAssetType => CoD5Definition.StringTableAssetType;
        public override byte WeaponAssetType => CoD5Definition.WeaponAssetType;
        public override byte ImageAssetType => CoD5Definition.ImageAssetType;
        public byte MaterialAssetType => CoD5Definition.MaterialAssetType;
        public byte TechSetAssetType => CoD5Definition.TechSetAssetType;

        // Maximum bytes to search forward for alignment/padding
        // Reduced from 512 to 64 for better performance - alignment issues are typically small
        private const int MAX_ALIGNMENT_SEARCH = 64;

        public override string GetAssetTypeName(int assetType)
        {
            if (Enum.IsDefined(typeof(CoD5AssetType), assetType))
            {
                return ((CoD5AssetType)assetType).ToString();
            }
            return $"unknown_0x{assetType:X2}";
        }

        public override bool IsSupportedAssetType(int assetType)
        {
            return assetType == RawFileAssetType ||
                   assetType == LocalizeAssetType ||
                   assetType == MenuFileAssetType ||
                   assetType == MaterialAssetType ||
                   assetType == TechSetAssetType ||
                   assetType == XAnimAssetType ||
                   assetType == StringTableAssetType ||
                   assetType == WeaponAssetType ||
                   assetType == ImageAssetType;
        }

        public override bool IsMaterialType(int assetType) => assetType == MaterialAssetType;
        public override bool IsTechSetType(int assetType) => assetType == TechSetAssetType;

        /// <summary>
        /// CoD5/WaW localize parsing with alignment handling.
        ///
        /// Localize entry structure uses two 4-byte pointers:
        ///   [4-byte value pointer] [4-byte key pointer] [value if inline] [key if inline]
        ///
        /// Case A - Both inline (8 consecutive FFs):
        ///   [FF FF FF FF FF FF FF FF] [LocalizedValue\0] [Key\0]
        ///
        /// Case B - Key only inline (first 4 bytes != FF):
        ///   [XX XX XX XX FF FF FF FF] [Key\0]
        ///   Value is empty/external when first 4 bytes are not all FF.
        ///
        /// This method first tries to parse at the exact offset. If that fails,
        /// it will search forward up to 64 bytes to find a valid marker (handles alignment/padding).
        /// </summary>
        public override (LocalizedEntry? entry, int nextOffset) ParseLocalizedEntry(byte[] zoneData, int offset)
        {
            // First try exact position (most common case)
            var result = TryParseLocalizeAtOffset(zoneData, offset);
            if (result.entry != null)
            {
                return result;
            }

            // If exact position failed, search forward for a valid marker (handles alignment/padding)
            // Skip the forward search loop if we already found a marker but it failed validation
            // This prevents redundant searching when FindFirstLocalizeMarker already found this marker
            for (int searchOffset = offset + 1; searchOffset <= offset + MAX_ALIGNMENT_SEARCH && searchOffset + 8 < zoneData.Length; searchOffset++)
            {
                if (IsValidLocalizeMarker(zoneData, searchOffset))
                {
                    result = TryParseLocalizeAtOffset(zoneData, searchOffset);
                    if (result.entry != null)
                    {
                        return result;
                    }
                }
            }

            return (null, offset);
        }

        /// <summary>
        /// Checks if there's a valid localize marker at the given offset.
        /// Valid markers:
        ///   - 8 consecutive FFs (both value and key inline)
        ///   - 4 non-FF bytes followed by 4 FFs (key only inline, empty value)
        /// </summary>
        private static bool IsValidLocalizeMarker(byte[] data, int offset)
        {
            if (offset + 8 > data.Length)
                return false;

            // Check if last 4 bytes are FF (key pointer must be inline)
            bool keyPointerIsFF = data[offset + 4] == 0xFF && data[offset + 5] == 0xFF &&
                                  data[offset + 6] == 0xFF && data[offset + 7] == 0xFF;

            if (!keyPointerIsFF)
                return false;

            // Check if first 4 bytes are also FF (both inline)
            bool valuePointerIsFF = data[offset] == 0xFF && data[offset + 1] == 0xFF &&
                                    data[offset + 2] == 0xFF && data[offset + 3] == 0xFF;

            // Verify there's valid data after the marker
            if (offset + 8 >= data.Length)
                return false;

            byte nextByte = data[offset + 8];

            // The byte after marker should not be 0xFF (still in padding)
            if (nextByte == 0xFF)
                return false;

            // If only key pointer is FF (value is external), next byte starts the key (should be printable)
            if (!valuePointerIsFF && nextByte == 0x00)
                return false; // Key-only but next byte is null - not valid

            return true;
        }

        /// <summary>
        /// Attempts to parse a localize entry at the exact given offset.
        /// </summary>
        private (LocalizedEntry? entry, int nextOffset) TryParseLocalizeAtOffset(byte[] zoneData, int offset)
        {
            if (offset + 9 > zoneData.Length) // Need at least marker + 1 byte
            {
                return (null, offset);
            }

            // Check if last 4 bytes are FF (key pointer must be inline)
            bool keyPointerIsFF = zoneData[offset + 4] == 0xFF && zoneData[offset + 5] == 0xFF &&
                                  zoneData[offset + 6] == 0xFF && zoneData[offset + 7] == 0xFF;

            if (!keyPointerIsFF)
            {
                return (null, offset);
            }

            // Check if first 4 bytes are also FF (both inline)
            bool valuePointerIsFF = zoneData[offset] == 0xFF && zoneData[offset + 1] == 0xFF &&
                                    zoneData[offset + 2] == 0xFF && zoneData[offset + 3] == 0xFF;

            int currentOffset = offset + 8; // Position after the 8-byte marker

            string localizedValue;
            string key;
            int keyStartOffset;

            if (valuePointerIsFF)
            {
                // Case A: Both pointers are FF - read value then key
                localizedValue = ReadNullTerminatedString(zoneData, currentOffset);
                // Use Length, not UTF8.GetByteCount - we read byte-by-byte, each byte = one char
                currentOffset += localizedValue.Length + 1;

                keyStartOffset = currentOffset; // Track where key starts for in-place patching
                key = ReadNullTerminatedString(zoneData, currentOffset);
                currentOffset += key.Length + 1;
            }
            else
            {
                // Case B: Only key pointer is FF - value is empty, read only key
                localizedValue = string.Empty;
                keyStartOffset = currentOffset; // Key starts immediately after marker
                key = ReadNullTerminatedString(zoneData, currentOffset);
                currentOffset += key.Length + 1;
            }

            // Validate key is not empty and looks like a valid localize key
            if (string.IsNullOrEmpty(key) || !IsValidLocalizeKey(key))
            {
                return (null, currentOffset);
            }

            var entry = new LocalizedEntry
            {
                Key = key,
                LocalizedText = localizedValue,
                StartOfFileHeader = offset,
                EndOfFileHeader = currentOffset,
                KeyStartOffset = keyStartOffset
            };

            return (entry, currentOffset);
        }

        /// <summary>
        /// Validates that a string looks like a valid localization key.
        /// Keys are typically in SCREAMING_SNAKE_CASE (e.g., RANK_BGEN_FULL_N).
        /// Only ASCII letters, digits, and underscores are allowed.
        /// </summary>
        private static bool IsValidLocalizeKey(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length < 3 || key.Length > 150)
                return false;

            // Must start with an uppercase ASCII letter (A-Z)
            // Real localization keys are in SCREAMING_SNAKE_CASE
            char first = key[0];
            if (!(first >= 'A' && first <= 'Z'))
                return false;

            int uppercaseCount = 0;
            int underscoreCount = 0;
            int consecutiveSameChar = 1;
            char prevChar = '\0';

            foreach (char c in key)
            {
                bool isUppercase = (c >= 'A' && c <= 'Z');
                bool isDigit = (c >= '0' && c <= '9');
                bool isUnderscore = (c == '_');

                // Only allow uppercase letters, digits, and underscores
                // This filters out lowercase-only garbage like "wwpw", "pw", etc.
                if (!isUppercase && !isDigit && !isUnderscore)
                    return false;

                if (isUppercase) uppercaseCount++;
                if (isUnderscore) underscoreCount++;

                // Check for excessive repeated characters (e.g., "WWWWW")
                if (c == prevChar)
                {
                    consecutiveSameChar++;
                    if (consecutiveSameChar > 3)
                        return false; // More than 3 consecutive same characters is suspicious
                }
                else
                {
                    consecutiveSameChar = 1;
                }
                prevChar = c;
            }

            // Must have at least one underscore (keys are SCREAMING_SNAKE_CASE)
            // This filters out short garbage like "QG", "WGW"
            if (underscoreCount == 0)
                return false;

            // Must have at least 2 uppercase letters
            if (uppercaseCount < 2)
                return false;

            return true;
        }
    }
}
