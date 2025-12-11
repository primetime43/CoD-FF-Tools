using Call_of_Duty_FastFile_Editor.Models;
using Call_of_Duty_FastFile_Editor.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Call_of_Duty_FastFile_Editor.ZoneParsers
{
    public class LocalizeAssetParser
    {
        /// <summary>
        /// Parses a single localized asset starting at the given offset in the zone file data.
        ///
        /// Localize entry structure uses two 4-byte pointers:
        ///   [4-byte text pointer] [4-byte key pointer] [text if inline] [key if inline]
        ///
        /// Case A - Both inline (8 consecutive FFs):
        ///   [FF FF FF FF FF FF FF FF] [LocalizedText\0] [Key\0]
        ///
        /// Case B - Key only inline (first 4 bytes != FF):
        ///   [XX XX XX XX FF FF FF FF] [Key\0]
        ///   Text is empty/external when first 4 bytes are not all FF.
        ///
        /// This method first tries to parse at the exact offset. If that fails,
        /// it will search forward up to 64 bytes to find a valid marker (handles alignment/padding).
        /// </summary>
        public static (LocalizedEntry entry, int nextOffset) ParseSingleLocalizeAssetNoPattern(FastFile openedFastFile, int startingOffset)
        {
            Debug.WriteLine($"[LocalizeAssetParser] Starting parse at offset 0x{startingOffset:X}.");
            byte[] fileData = openedFastFile.OpenedFastFileZone.Data;

            // Log the bytes at the starting offset for debugging
            if (startingOffset + 16 < fileData.Length)
            {
                Debug.WriteLine($"[LocalizeAssetParser] Bytes at 0x{startingOffset:X}: " +
                    $"{fileData[startingOffset]:X2} {fileData[startingOffset + 1]:X2} {fileData[startingOffset + 2]:X2} {fileData[startingOffset + 3]:X2} " +
                    $"{fileData[startingOffset + 4]:X2} {fileData[startingOffset + 5]:X2} {fileData[startingOffset + 6]:X2} {fileData[startingOffset + 7]:X2} " +
                    $"{fileData[startingOffset + 8]:X2} {fileData[startingOffset + 9]:X2} {fileData[startingOffset + 10]:X2} {fileData[startingOffset + 11]:X2}");
            }

            // First try exact position
            var result = TryParseLocalizeAtOffset(fileData, startingOffset);
            if (result.entry != null)
                return result;

            // If exact position failed, search forward for a valid marker (handles alignment/padding)
            // Search up to 64 bytes forward
            for (int offset = startingOffset + 1; offset <= startingOffset + 64 && offset + 10 < fileData.Length; offset++)
            {
                if (IsValidLocalizeMarker(fileData, offset))
                {
                    Debug.WriteLine($"[LocalizeAssetParser] Found marker at adjusted offset 0x{offset:X} (was 0x{startingOffset:X}, delta={offset - startingOffset})");
                    result = TryParseLocalizeAtOffset(fileData, offset);
                    if (result.entry != null)
                        return result;
                }
            }

            Debug.WriteLine($"[LocalizeAssetParser] Failed to parse localize asset at 0x{startingOffset:X} (searched 64 bytes forward)");
            return (null, startingOffset);
        }

        /// <summary>
        /// Checks if there's a valid localize marker at the given offset.
        /// Valid markers:
        ///   - 8 consecutive FFs (both text and key inline)
        ///   - 4 non-FF bytes followed by 4 FFs (key only inline, empty text)
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

            // Check if first 4 bytes are also FF (both inline) or not (key only)
            bool textPointerIsFF = data[offset] == 0xFF && data[offset + 1] == 0xFF &&
                                   data[offset + 2] == 0xFF && data[offset + 3] == 0xFF;

            // Verify there's valid data after the marker
            if (offset + 8 >= data.Length)
                return false;

            byte nextByte = data[offset + 8];

            // The byte after marker should be printable ASCII (start of text or key)
            // or 0x00 (empty text followed by key)
            if (nextByte == 0xFF)
                return false; // Still in padding

            // If both pointers are FF, next byte starts the text (can be printable or 0x00 for empty)
            // If only key pointer is FF, next byte starts the key (should be printable)
            if (!textPointerIsFF && nextByte == 0x00)
                return false; // Key-only but next byte is null - not valid

            return true;
        }

        /// <summary>
        /// Attempts to parse a localize entry at the exact given offset.
        /// Handles both full inline (8 FFs) and key-only inline (4 bytes + 4 FFs) cases.
        /// </summary>
        private static (LocalizedEntry entry, int nextOffset) TryParseLocalizeAtOffset(byte[] fileData, int offset)
        {
            if (offset + 9 > fileData.Length) // Need at least marker + 1 null terminator
            {
                Debug.WriteLine($"[LocalizeAssetParser] Not enough data at 0x{offset:X}");
                return (null, offset);
            }

            // Check if last 4 bytes are FF (key pointer must be inline)
            bool keyPointerIsFF = fileData[offset + 4] == 0xFF && fileData[offset + 5] == 0xFF &&
                                  fileData[offset + 6] == 0xFF && fileData[offset + 7] == 0xFF;

            if (!keyPointerIsFF)
            {
                Debug.WriteLine($"[LocalizeAssetParser] No key pointer marker at 0x{offset:X}, found: " +
                    $"{fileData[offset + 4]:X2} {fileData[offset + 5]:X2} {fileData[offset + 6]:X2} {fileData[offset + 7]:X2}");
                return (null, offset);
            }

            // Check if first 4 bytes are also FF (both inline)
            bool textPointerIsFF = fileData[offset] == 0xFF && fileData[offset + 1] == 0xFF &&
                                   fileData[offset + 2] == 0xFF && fileData[offset + 3] == 0xFF;

            using (MemoryStream ms = new MemoryStream(fileData))
            using (BinaryReader br = new BinaryReader(ms, Encoding.ASCII))
            {
                // Position after the 8-byte marker
                ms.Position = offset + 8;

                byte[] textBytes;
                byte[] keyBytes;

                if (textPointerIsFF)
                {
                    // Case A: Both pointers are FF - read text then key
                    textBytes = ReadNullTerminatedBytes(br);
                    if (textBytes == null)
                    {
                        Debug.WriteLine($"[LocalizeAssetParser] Failed to read localized text at 0x{ms.Position:X}");
                        return (null, (int)ms.Position);
                    }

                    keyBytes = ReadNullTerminatedBytes(br);
                    if (keyBytes == null)
                    {
                        Debug.WriteLine($"[LocalizeAssetParser] Failed to read key after text at 0x{ms.Position:X}");
                        return (null, (int)ms.Position);
                    }
                }
                else
                {
                    // Case B: Only key pointer is FF - text is empty, read only key
                    textBytes = Array.Empty<byte>();
                    keyBytes = ReadNullTerminatedBytes(br);
                    if (keyBytes == null)
                    {
                        Debug.WriteLine($"[LocalizeAssetParser] Failed to read key at 0x{ms.Position:X}");
                        return (null, (int)ms.Position);
                    }
                    Debug.WriteLine($"[LocalizeAssetParser] Key-only entry (empty text)");
                }

                // Validate key is not empty
                if (keyBytes.Length == 0)
                {
                    Debug.WriteLine($"[LocalizeAssetParser] Empty key at 0x{offset:X}");
                    return (null, (int)ms.Position);
                }

                int entryEnd = (int)ms.Position;

                LocalizedEntry entry = new LocalizedEntry
                {
                    KeyBytes = keyBytes,
                    TextBytes = textBytes,
                    StartOfFileHeader = offset, // Include the marker in the range
                    EndOfFileHeader = entryEnd
                };

                Debug.WriteLine($"[LocalizeAssetParser] Parsed: Key={entry.Key}, TextLen={textBytes.Length}, Range=0x{offset:X}-0x{entryEnd:X}");
                return (entry, entryEnd);
            }
        }

        /// <summary>
        /// Searches for a valid localize marker starting from the given offset,
        /// then parses the localized asset entry if found.
        ///
        /// Valid markers:
        ///   - 8 consecutive FFs (both value and name inline)
        ///   - 4 non-FF bytes + 4 FFs (name only inline, value is null/external)
        ///
        /// Returns a tuple of (LocalizedEntry, nextOffset). If no valid marker found, returns (null, startingOffset).
        /// </summary>
        public static (LocalizedEntry entry, int nextOffset) ParseSingleLocalizeAssetWithPattern(FastFile openedFastFile, int startingOffset)
        {
            Debug.WriteLine($"[LocalizeAssetParser] Starting pattern-based parse at offset 0x{startingOffset:X}.");
            byte[] fileData = openedFastFile.OpenedFastFileZone.Data;

            // Search for a valid localize marker starting at startingOffset
            for (int pos = startingOffset; pos <= fileData.Length - 8; pos++)
            {
                // Check if last 4 bytes are FF (name pointer must be inline)
                bool namePointerIsFF = fileData[pos + 4] == 0xFF && fileData[pos + 5] == 0xFF &&
                                       fileData[pos + 6] == 0xFF && fileData[pos + 7] == 0xFF;

                if (!namePointerIsFF)
                    continue;

                // Check if first 4 bytes are also FF (both inline)
                bool valuePointerIsFF = fileData[pos] == 0xFF && fileData[pos + 1] == 0xFF &&
                                        fileData[pos + 2] == 0xFF && fileData[pos + 3] == 0xFF;

                // Validate there's data after the marker
                if (pos + 8 >= fileData.Length)
                    continue;

                byte nextByte = fileData[pos + 8];

                // Skip if still in padding
                if (nextByte == 0xFF)
                    continue;

                // If key-only (value not inline), next byte should be printable (start of key)
                if (!valuePointerIsFF && nextByte == 0x00)
                    continue;

                // Try to parse this potential marker
                using (MemoryStream ms = new MemoryStream(fileData))
                using (BinaryReader br = new BinaryReader(ms, Encoding.ASCII))
                {
                    ms.Position = pos + 8; // Skip the 8-byte marker

                    byte[] textBytes;
                    byte[] keyBytes;

                    if (valuePointerIsFF)
                    {
                        // Both value and name are inline
                        textBytes = ReadNullTerminatedBytes(br);
                        if (textBytes == null)
                            continue; // Invalid, try next position

                        keyBytes = ReadNullTerminatedBytes(br);
                        if (keyBytes == null)
                            continue; // Invalid, try next position
                    }
                    else
                    {
                        // Only name is inline, value is empty/external
                        textBytes = Array.Empty<byte>();
                        keyBytes = ReadNullTerminatedBytes(br);
                        if (keyBytes == null)
                            continue; // Invalid, try next position
                    }

                    // Create entry to get the Key string for validation
                    var tempEntry = new LocalizedEntry { KeyBytes = keyBytes, TextBytes = textBytes };

                    // Validate the key looks like a proper localization key
                    if (!IsValidLocalizeKey(tempEntry.Key))
                    {
                        Debug.WriteLine($"[LocalizeAssetParser] Invalid key format at 0x{pos:X}: '{tempEntry.Key}' - skipping");
                        continue; // Not a valid localization key, try next position
                    }

                    int entryEnd = (int)ms.Position;
                    LocalizedEntry entry = new LocalizedEntry
                    {
                        KeyBytes = keyBytes,
                        TextBytes = textBytes,
                        StartOfFileHeader = pos,
                        EndOfFileHeader = entryEnd
                    };

                    Debug.WriteLine($"[LocalizeAssetParser] Parsed entry with key: {entry.Key}, range: 0x{pos:X}-0x{entryEnd:X}.");
                    return (entry, entryEnd);
                }
            }

            Debug.WriteLine("[LocalizeAssetParser] Valid marker not found. Returning null.");
            return (null, startingOffset);
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

        /// <summary>
        /// Reads a null-terminated ASCII string from the current position of the BinaryReader.
        /// Returns null if no bytes are available.
        /// </summary>
        /// <param name="br">The BinaryReader instance.</param>
        /// <returns>The read string, or null if unable to read.</returns>
        private static string ReadNullTerminatedString(BinaryReader br)
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                while (true)
                {
                    if (br.BaseStream.Position >= br.BaseStream.Length)
                        return null;
                    byte b = br.ReadByte();
                    if (b == 0x00)
                        break;
                    sb.Append((char)b);
                }
            }
            catch (EndOfStreamException)
            {
                return null;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Reads null-terminated bytes from the current position of the BinaryReader.
        /// Returns the raw bytes (without the null terminator), or null if unable to read.
        /// </summary>
        private static byte[] ReadNullTerminatedBytes(BinaryReader br)
        {
            var bytes = new List<byte>();
            try
            {
                while (true)
                {
                    if (br.BaseStream.Position >= br.BaseStream.Length)
                        return null;
                    byte b = br.ReadByte();
                    if (b == 0x00)
                        break;
                    bytes.Add(b);
                }
            }
            catch (EndOfStreamException)
            {
                return null;
            }
            return bytes.ToArray();
        }
    }
}
