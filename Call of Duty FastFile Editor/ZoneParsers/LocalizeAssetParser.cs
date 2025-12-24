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
        /// Expected pattern:
        ///   [8 bytes marker: 0xFF 0xFF 0xFF 0xFF 0xFF 0xFF 0xFF 0xFF]
        ///   [LocalizedText: null-terminated ASCII string]
        ///   [Key: null-terminated ASCII string]
        /// Returns a tuple containing the parsed LocalizedEntry (or null if not found)
        /// and the new offset immediately after the entry.
        /// </summary>
        /// <param name="openedFastFile">The FastFile object holding zone data.</param>
        /// <param name="startingOffset">Offset in the zone data where the localized item is expected to start.</param>
        /// <returns>
        /// A tuple of (LocalizedEntry entry, int nextOffset). If no valid entry is found, entry is null.
        /// </returns>
        public static (LocalizedEntry entry, int nextOffset) ParseSingleLocalizeAssetNoPattern(FastFile openedFastFile, int startingOffset)
        {
            Debug.WriteLine($"[LocalizeAssetParser] Starting parse at offset 0x{startingOffset:X}.");
            byte[] fileData = openedFastFile.OpenedFastFileZone.Data;

            using (MemoryStream ms = new MemoryStream(fileData))
            using (BinaryReader br = new BinaryReader(ms, Encoding.ASCII))
            {
                ms.Position = startingOffset;

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
                    if (b != 0xFF)
                    {
                        Debug.WriteLine($"[LocalizeAssetParser] Expected eight 0xFF bytes at position 0x{markerPos:X} but marker is invalid. Returning null.");
                        return (null, markerPos);
                    }
                }

                // After the marker, the localized text begins.
                int entryStart = (int)ms.Position;

                // Read the localized text (null-terminated).
                string localizedText = ReadNullTerminatedString(br);
                if (localizedText == null)
                {
                    Debug.WriteLine("[LocalizeAssetParser] Localized text string not found. Returning null.");
                    return (null, (int)ms.Position);
                }

                // Read the key (null-terminated).
                string key = ReadNullTerminatedString(br);
                if (key == null)
                {
                    Debug.WriteLine("[LocalizeAssetParser] Key string not found. Returning null.");
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
        /// Searches for the eight 0xFF bytes pattern starting from the given offset,
        /// then parses a single localized asset entry if found.
        /// Expected pattern:
        ///   [8 bytes marker: 0xFF 0xFF 0xFF 0xFF 0xFF 0xFF 0xFF 0xFF]
        ///   [LocalizedText: null-terminated ASCII string]
        ///   [Key: null-terminated ASCII string]
        /// Returns a tuple of (LocalizedEntry, nextOffset). If the pattern is not found or not followed by valid data, returns (null, startingOffset).
        /// </summary>
        public static (LocalizedEntry entry, int nextOffset) ParseSingleLocalizeAssetWithPattern(FastFile openedFastFile, int startingOffset)
        {
            Debug.WriteLine($"[LocalizeAssetParser] Starting pattern-based parse at offset 0x{startingOffset:X}.");
            byte[] fileData = openedFastFile.OpenedFastFileZone.Data;

            // Search for eight consecutive 0xFF bytes starting at startingOffset.
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

                // Read the key (null-terminated).
                string key = ReadNullTerminatedString(br);
                if (key == null)
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
