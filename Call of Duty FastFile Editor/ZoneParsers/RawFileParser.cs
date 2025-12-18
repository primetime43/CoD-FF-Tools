using Call_of_Duty_FastFile_Editor.GameDefinitions;
using Call_of_Duty_FastFile_Editor.Models;
using Call_of_Duty_FastFile_Editor.Services;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Text;
using Call_of_Duty_FastFile_Editor.Constants;

namespace Call_of_Duty_FastFile_Editor.ZoneParsers
{
    public static class RawFileParser
    {
        #region Structure Parsing
        /// <summary>
        /// Parses a raw file at the given offset using the console 12-byte header format:
        /// [marker][len][marker][name][data]
        /// NOTE: PC parsing is not currently supported - AssetRecordProcessor returns early for PC files.
        /// </summary>
        public static RawFileNode ExtractSingleRawFileNodeNoPattern(FastFile openedFastFile, int offset)
        {
            Debug.WriteLine($"================================ Start of raw file node search =============================================");
            Debug.WriteLine($"[ExtractSingleRawFileNodeNoPattern] Starting raw file scan at offset 0x{offset:X}.");

            RawFileNode node = new RawFileNode();
            byte[] fileData = openedFastFile.OpenedFastFileZone.Data;

            Debug.WriteLine($"[ExtractSingleRawFileNodeNoPattern] Read file '{openedFastFile.ZoneFilePath}' ({fileData.Length} bytes).");

            // Console uses 12-byte header
            const int headerSize = 12;

            if (offset > fileData.Length - headerSize)
            {
                Debug.WriteLine($"[RawFile] Not enough bytes remaining for a {headerSize}-byte header at offset 0x{offset:X}.");
                return null;
            }

            // Console 12-byte header: [marker][len][marker]
            uint marker1 = Utilities.ReadUInt32BigEndian(fileData, offset);
            if (marker1 != 0xFFFFFFFF)
            {
                Debug.WriteLine($"[RawFile] Unexpected marker at offset 0x{offset:X}: 0x{marker1:X}.");
                return null;
            }

            int dataLength = (int)Utilities.ReadUInt32BigEndian(fileData, offset + 4);

            uint marker2 = Utilities.ReadUInt32BigEndian(fileData, offset + 8);
            if (marker2 != 0xFFFFFFFF)
            {
                Debug.WriteLine($"[RawFile] Unexpected second marker at offset 0x{offset + 8:X}: 0x{marker2:X}.");
                return null;
            }

            int fileNameOffset = offset + 12;

            if (dataLength == 0)
            {
                Debug.WriteLine($"[RawFile] dataLength is 0. Probably not a rawfile. Returning null.");
                return null;
            }

            // Validate size is reasonable - rawfiles are scripts/configs, not multi-megabyte assets
            const int MAX_RAWFILE_SIZE = 5 * 1024 * 1024;
            if (dataLength > MAX_RAWFILE_SIZE || dataLength < 0)
            {
                Debug.WriteLine($"[RawFile] dataLength {dataLength} (0x{dataLength:X}) is unreasonably large. Not a valid rawfile.");
                return null;
            }

            // Record the start of the header and file size
            node.StartOfFileHeader = offset;
            node.MaxSize = dataLength;
            node.HeaderSize = headerSize;

            string inlineName = Utilities.ReadNullTerminatedString(fileData, fileNameOffset);

            // Validate the filename looks reasonable
            if (!IsValidRawFileName(inlineName))
            {
                Debug.WriteLine($"[RawFile] Invalid filename '{inlineName}' at offset 0x{fileNameOffset:X}. Not a valid rawfile.");
                return null;
            }

            node.FileName = inlineName;
            Debug.WriteLine($"[RawFile] Inline name read: '{inlineName}'.");

            int nameByteCount = inlineName.Length + 1;
            int fileDataOffset = fileNameOffset + nameByteCount;

            // Ensure there's enough data for the file data
            if (fileDataOffset + dataLength <= fileData.Length)
            {
                byte[] rawBytes = new byte[dataLength];
                Array.Copy(fileData, fileDataOffset, rawBytes, 0, dataLength);

                node.RawFileBytes = rawBytes;
                node.RawFileContent = Encoding.UTF8.GetString(rawBytes);
                node.RawFileEndPosition = fileDataOffset + dataLength + 1;
                Debug.WriteLine($"[RawFile] Inline file data read: {rawBytes.Length} bytes.");
            }
            else
            {
                Debug.WriteLine($"[RawFile] Data length {dataLength} exceeds available file data; skipping file data read.");
                node.RawFileBytes = new byte[0];
                node.RawFileContent = string.Empty;
            }

            Debug.WriteLine($"================================ End of raw file node search =============================================");

            return node;
        }
        #endregion

        #region Pattern Parsing

        /// <summary>
        /// Scans the zone file (at zoneFilePath) starting at startOffset for the first raw file entry
        /// that matches one of the defined patterns. When found, extracts its size, file name, content,
        /// and returns a RawFileNode. If no match is found, returns null.
        /// </summary>
        /// <param name="zoneFilePath">Path to the zone file.</param>
        /// <param name="startOffset">
        /// The offset in the zone file at which to start scanning for the raw file header.
        /// (Pass 0 to search from the beginning.)
        /// </param>
        /// <returns>The first matching RawFileNode or null if none is found.</returns>

        public static RawFileNode ExtractSingleRawFileNodeWithPattern(byte[] fileData, int startOffset = 0)
        {
            Debug.WriteLine("================================ Start of raw file node search =============================================");
            Debug.WriteLine($"[ExtractSingleRawFileNodeWithPattern] Starting search at 0x{startOffset:X}");

            // Use the plain text patterns from our constants.
            string[] patternStrings = RawFileConstants.FileNamePatternStrings;

            var patternBytes = patternStrings.Select(s => Encoding.ASCII.GetBytes(s + "\0")).ToList();
            // We'll also use this same array for extension filtering.
            string[] validExtensions = patternStrings;

            int foundIndex = -1;
            string foundPatternStr = null;

            // Scan the file data from startOffset to the end.
            for (int i = startOffset; i < fileData.Length; i++)
            {
                for (int p = 0; p < patternBytes.Count; p++)
                {
                    var pattern = patternBytes[p];
                    // Only attempt if there is enough room left in the file.
                    if (i <= fileData.Length - pattern.Length)
                    {
                        if (fileData.AsSpan(i, pattern.Length).SequenceEqual(pattern))
                        {
                            foundIndex = i;
                            foundPatternStr = patternStrings[p];
                            Debug.WriteLine($"[PatternFound] Pattern '{foundPatternStr}' found at offset 0x{i:X}");
                            goto FoundMatch;
                        }
                    }
                }
            }
        FoundMatch:
            if (foundIndex < 0)
            {
                Debug.WriteLine("[ExtractSingleRawFileNodeWithPattern] No matching pattern found.");
                return null;
            }

            int patternIndex = foundIndex;
            // Back up to locate the preceding 4-byte 0xFF marker sequence.
            int ffffPositionBeforeName = patternIndex - 1;
            while (ffffPositionBeforeName >= 4 &&
                   !(fileData[ffffPositionBeforeName] == 0xFF &&
                     fileData[ffffPositionBeforeName - 1] == 0xFF &&
                     fileData[ffffPositionBeforeName - 2] == 0xFF &&
                     fileData[ffffPositionBeforeName - 3] == 0xFF))
            {
                ffffPositionBeforeName--;
                if (ffffPositionBeforeName < 4)
                {
                    Debug.WriteLine($"WARN: Could not find valid FF FF FF FF header for file at offset 0x{patternIndex:X}");
                    return null;
                }
            }

            // The file size is stored just before the FF sequence.
            int sizePosition = ffffPositionBeforeName - 7;
            if (sizePosition < 0)
            {
                Debug.WriteLine($"[ExtractSingleRawFileNodeWithPattern] Size position invalid (sizePosition = {sizePosition}). Returning null.");
                return null;
            }

            // Calculate the start-of-header (assuming a 4-byte FF marker precedes the size field).
            int startOfHeaderPosition = sizePosition - 4;

            using (BinaryReader binaryReader = new BinaryReader(new MemoryStream(fileData), Encoding.Default))
            {
                binaryReader.BaseStream.Position = sizePosition;
                int maxSize = IPAddress.NetworkToHostOrder(binaryReader.ReadInt32());
                Debug.WriteLine($"[ExtractSingleRawFileNodeWithPattern] Found size = {maxSize} at offset 0x{sizePosition:X}.");

                // Extract the file name starting immediately after the FF sequence.
                int fileNameStart = ffffPositionBeforeName + 1;
                string fileName = ExtractFullFileName(fileData, fileNameStart);
                Debug.WriteLine($"[ExtractSingleRawFileNodeWithPattern] Extracted name: '{fileName}' at offset 0x{fileNameStart:X}.");

                // Validate that the file name ends with one of the expected extensions.
                bool isValidExtension = false;
                foreach (var ext in validExtensions)
                {
                    if (fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                    {
                        isValidExtension = true;
                        break;
                    }
                }
                if (!isValidExtension)
                {
                    Debug.WriteLine($"[ExtractSingleRawFileNodeWithPattern] Skipping file '{fileName}' because its extension is not recognized.");
                    return null;
                }

                Debug.WriteLine("================================ End of raw file node search =============================================");

                string fileContent = ReadFileContentAfterName(fileData, patternIndex, maxSize);
                byte[] fileBytes = ExtractBinaryContent(fileData, patternIndex, maxSize);

                RawFileNode node = new RawFileNode
                {
                    PatternIndexPosition = patternIndex,
                    MaxSize = maxSize,
                    StartOfFileHeader = startOfHeaderPosition,
                    FileName = fileName,
                    RawFileContent = fileContent,
                    RawFileBytes = fileBytes
                };

                Debug.WriteLine($"DEBUG: Detected file header for '{node.FileName}' at offset 0x{node.StartOfFileHeader:X}");
                return node;
            }
        }

        /// <summary>
        /// Game-aware pattern matching for MW2 which uses a 16-byte header with optional compression.
        /// MW2 RawFile header: [FF FF FF FF] [compressedLen BE] [len BE] [FF FF FF FF] [name\0] [data]
        /// </summary>
        public static RawFileNode ExtractSingleRawFileNodeWithPattern(byte[] fileData, int startOffset, IGameDefinition gameDefinition)
        {
            // For non-MW2 games, use the standard pattern matching
            if (gameDefinition.ShortName != "MW2")
            {
                return ExtractSingleRawFileNodeWithPattern(fileData, startOffset);
            }

            Debug.WriteLine("================================ Start of MW2 raw file node search =============================================");
            Debug.WriteLine($"[MW2PatternMatch] Starting search at 0x{startOffset:X}");

            // Use the plain text patterns from our constants.
            string[] patternStrings = RawFileConstants.FileNamePatternStrings;
            var patternBytes = patternStrings.Select(s => Encoding.ASCII.GetBytes(s + "\0")).ToList();
            string[] validExtensions = patternStrings;

            int foundIndex = -1;
            string foundPatternStr = null;

            // Scan the file data from startOffset to the end.
            for (int i = startOffset; i < fileData.Length; i++)
            {
                for (int p = 0; p < patternBytes.Count; p++)
                {
                    var pattern = patternBytes[p];
                    if (i <= fileData.Length - pattern.Length)
                    {
                        if (fileData.AsSpan(i, pattern.Length).SequenceEqual(pattern))
                        {
                            foundIndex = i;
                            foundPatternStr = patternStrings[p];
                            Debug.WriteLine($"[MW2PatternMatch] Pattern '{foundPatternStr}' found at offset 0x{i:X}");
                            goto FoundMatch;
                        }
                    }
                }
            }
        FoundMatch:
            if (foundIndex < 0)
            {
                Debug.WriteLine("[MW2PatternMatch] No matching pattern found.");
                return null;
            }

            int patternIndex = foundIndex;
            // Back up to locate the preceding 4-byte 0xFF marker sequence (second marker before name).
            int ffffPositionBeforeName = patternIndex - 1;
            while (ffffPositionBeforeName >= 4 &&
                   !(fileData[ffffPositionBeforeName] == 0xFF &&
                     fileData[ffffPositionBeforeName - 1] == 0xFF &&
                     fileData[ffffPositionBeforeName - 2] == 0xFF &&
                     fileData[ffffPositionBeforeName - 3] == 0xFF))
            {
                ffffPositionBeforeName--;
                if (ffffPositionBeforeName < 4)
                {
                    Debug.WriteLine($"WARN: Could not find valid FF FF FF FF header for file at offset 0x{patternIndex:X}");
                    return null;
                }
            }

            // MW2 16-byte header:
            // [FF FF FF FF] [compressedLen BE] [len BE] [FF FF FF FF] [name\0] [data]
            //   offset 0         offset 4       offset 8    offset 12
            // ffffPositionBeforeName points to the LAST byte of the second FF marker (offset 15)

            // len is at marker2End - 7 (same as CoD4/WaW calculation, but it's the second size field)
            int lenPosition = ffffPositionBeforeName - 7;
            // compressedLen is 4 bytes before len
            int compressedLenPosition = lenPosition - 4;
            // Header starts 4 bytes before compressedLen (first FF marker)
            int startOfHeaderPosition = compressedLenPosition - 4;

            if (startOfHeaderPosition < 0 || compressedLenPosition < 0)
            {
                Debug.WriteLine($"[MW2PatternMatch] Header position invalid. Returning null.");
                return null;
            }

            // Verify the first FF FF FF FF marker
            if (!(fileData[startOfHeaderPosition] == 0xFF && fileData[startOfHeaderPosition + 1] == 0xFF &&
                  fileData[startOfHeaderPosition + 2] == 0xFF && fileData[startOfHeaderPosition + 3] == 0xFF))
            {
                Debug.WriteLine($"[MW2PatternMatch] First marker not found at expected position 0x{startOfHeaderPosition:X}");
                // Fall back to standard pattern matching
                return ExtractSingleRawFileNodeWithPattern(fileData, startOffset);
            }

            // Read the header fields
            int compressedLen = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(fileData, compressedLenPosition));
            int len = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(fileData, lenPosition));

            Debug.WriteLine($"[MW2PatternMatch] compressedLen={compressedLen}, len={len}");

            // Validate lengths
            if (len <= 0 || len > 10_000_000)
            {
                Debug.WriteLine($"[MW2PatternMatch] Invalid len={len}. Returning null.");
                return null;
            }
            if (compressedLen < 0 || compressedLen > 10_000_000)
            {
                Debug.WriteLine($"[MW2PatternMatch] Invalid compressedLen={compressedLen}. Returning null.");
                return null;
            }

            // Extract the file name starting immediately after the FF sequence.
            int fileNameStart = ffffPositionBeforeName + 1;
            string fileName = ExtractFullFileName(fileData, fileNameStart);
            Debug.WriteLine($"[MW2PatternMatch] Extracted name: '{fileName}' at offset 0x{fileNameStart:X}");

            // Validate extension
            bool isValidExtension = validExtensions.Any(ext => fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
            if (!isValidExtension)
            {
                Debug.WriteLine($"[MW2PatternMatch] Skipping file '{fileName}' because its extension is not recognized.");
                return null;
            }

            // Calculate data start position
            int nameByteCount = Encoding.ASCII.GetByteCount(fileName) + 1; // +1 for null terminator
            int dataStartOffset = fileNameStart + nameByteCount;
            int dataSize = compressedLen > 0 ? compressedLen : len;

            if (dataStartOffset + dataSize > fileData.Length)
            {
                Debug.WriteLine($"[MW2PatternMatch] Data extends beyond file. Returning null.");
                return null;
            }

            // Extract and optionally decompress data
            byte[] rawBytes;
            string additionalData = "";

            if (compressedLen > 0)
            {
                // Data is zlib compressed
                byte[] compressedData = new byte[compressedLen];
                Array.Copy(fileData, dataStartOffset, compressedData, 0, compressedLen);

                try
                {
                    using var inputStream = new MemoryStream(compressedData);
                    using var zlibStream = new ZLibStream(inputStream, CompressionMode.Decompress);
                    using var outputStream = new MemoryStream();
                    zlibStream.CopyTo(outputStream);
                    rawBytes = outputStream.ToArray();
                    additionalData = $"Compressed: {compressedLen} -> {len} bytes (pattern match)";
                    Debug.WriteLine($"[MW2PatternMatch] Decompressed {compressedLen} -> {rawBytes.Length} bytes");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MW2PatternMatch] Zlib decompression failed: {ex.Message}. Using raw data.");
                    rawBytes = compressedData;
                    additionalData = $"Decompression failed: {ex.Message}";
                }
            }
            else
            {
                // Data is uncompressed
                rawBytes = new byte[len];
                Array.Copy(fileData, dataStartOffset, rawBytes, 0, len);
            }

            string fileContent = Encoding.UTF8.GetString(rawBytes);
            int rawFileEndPosition = dataStartOffset + dataSize + 1; // +1 for null terminator

            var node = new RawFileNode
            {
                PatternIndexPosition = patternIndex,
                MaxSize = len,
                StartOfFileHeader = startOfHeaderPosition,
                HeaderSize = 16, // MW2 uses 16-byte header
                FileName = fileName,
                RawFileContent = fileContent,
                RawFileBytes = rawBytes,
                RawFileEndPosition = rawFileEndPosition,
                AdditionalData = additionalData
            };

            Debug.WriteLine($"[MW2PatternMatch] Successfully parsed '{fileName}' at header 0x{startOfHeaderPosition:X}");
            Debug.WriteLine("================================ End of MW2 raw file node search =============================================");
            return node;
        }

        /// <summary>
        /// Extracts the raw file entries from the FastFile (.ff) zone with their size and name.
        /// Setting raw file objects to the list of extracted file entries.
        /// THIS IS USING PATTERN MATCHING (don't use eventually)
        /// </summary>
        /// <param name="zoneFilePath"></param>
        /// <returns></returns>
        public static List<RawFileNode> ExtractAllRawFilesSizeAndName(string zoneFilePath)
        {
            List<RawFileNode> rawFileNodes = new List<RawFileNode>();
            byte[] fileData = File.ReadAllBytes(zoneFilePath);

            byte[][] patterns = RawFileConstants.FileNamePatternStrings
            .Select(s => Encoding.ASCII.GetBytes(s + "\0"))
            .ToArray();

            using (BinaryReader binaryReader = new BinaryReader(new MemoryStream(fileData), Encoding.Default))
            {
                foreach (var pattern in patterns)
                {
                    for (int i = 0; i <= fileData.Length - pattern.Length; i++)
                    {
                        if (fileData.AsSpan(i, pattern.Length).SequenceEqual(pattern))
                        {
                            int patternIndex = i;
                            int ffffPosition = patternIndex - 1;
                            while (ffffPosition >= 4 &&
                                   !(fileData[ffffPosition] == 0xFF &&
                                     fileData[ffffPosition - 1] == 0xFF &&
                                     fileData[ffffPosition - 2] == 0xFF &&
                                     fileData[ffffPosition - 3] == 0xFF))
                            {
                                ffffPosition--;
                            }

                            if (ffffPosition < 4 || fileData[ffffPosition + 1] == 0x00)
                                continue;

                            int sizePosition = ffffPosition - 7;
                            if (sizePosition >= 0)
                            {
                                binaryReader.BaseStream.Position = sizePosition;
                                int maxSize = IPAddress.NetworkToHostOrder(binaryReader.ReadInt32());

                                int fileNameStart = ffffPosition + 1;
                                string fileName = ExtractFullFileName(fileData, fileNameStart);

                                if (!string.IsNullOrEmpty(fileName) && !fileName.Contains("\x00"))
                                {
                                    string fileContent = ReadFileContentAfterName(fileData, patternIndex, maxSize);
                                    byte[] fileBytes = ExtractBinaryContent(fileData, patternIndex, maxSize);
                                    int headerStart = sizePosition - 4;

                                    rawFileNodes.Add(new RawFileNode
                                    {
                                        PatternIndexPosition = patternIndex,
                                        MaxSize = maxSize,
                                        StartOfFileHeader = headerStart,
                                        FileName = fileName,
                                        RawFileContent = fileContent,
                                        RawFileBytes = fileBytes
                                    });
                                }
                            }
                        }
                    }
                }
            }

            rawFileNodes.Sort((a, b) => a.PatternIndexPosition.CompareTo(b.PatternIndexPosition));
            return rawFileNodes;
        }
        #endregion

        #region Helper Methods

        /// <summary>
        /// Validates that a string looks like a valid rawfile name.
        /// Filters out garbage strings with excessive repeated characters or invalid patterns.
        /// </summary>
        private static bool IsValidRawFileName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length < 3 || name.Length > 256)
                return false;

            // Must contain at least one of the valid file extensions
            string[] validExtensions = { ".gsc", ".csc", ".rmb", ".def", ".str", ".cfg", ".menu", ".txt", ".csv", ".vision", ".arena", ".atr", ".sun" };
            bool hasValidExtension = validExtensions.Any(ext => name.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
            if (!hasValidExtension)
                return false;

            // Check for excessive repeated characters (like "yyyyyyyyy")
            int maxRepeats = 3;
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

            // All characters must be printable ASCII (0x20-0x7E)
            foreach (char c in name)
            {
                if (c < 0x20 || c > 0x7E)
                    return false;
            }

            // Must have at least 2 letters (not just special chars and numbers)
            int letterCount = name.Count(c => char.IsLetter(c));
            if (letterCount < 2)
                return false;

            return true;
        }

        private static byte[] ExtractBinaryContent(byte[] fileData, int patternIndex, int maxSize)
        {
            // Move to the position after the name's null terminator
            int contentStartPosition = patternIndex;
            while (contentStartPosition < fileData.Length && fileData[contentStartPosition] != 0x00)
            {
                contentStartPosition++;
            }
            // Skip the null terminator
            contentStartPosition++;

            // Calculate content length up to max size
            int contentLength = maxSize;
            if (contentStartPosition + contentLength > fileData.Length)
            {
                contentLength = fileData.Length - contentStartPosition;
            }

            byte[] contentBytes = new byte[contentLength];
            Array.Copy(fileData, contentStartPosition, contentBytes, 0, contentLength);

            return RemoveZeroPadding(contentBytes);
        }

        private static string ExtractFullFileName(byte[] data, int fileNameStart)
        {
            StringBuilder fileName = new StringBuilder();

            // Read the file name from fileNameStart until the null terminator
            for (int i = fileNameStart; i < data.Length; i++)
            {
                char c = (char)data[i];
                if (c == '\0')
                {
                    break;
                }
                fileName.Append(c);
            }

            return fileName.ToString();
        }

        private static string ReadFileContentAfterName(byte[] fileData, int startPosition, int maxSize)
        {
            // Move to the position after the name's null terminator
            int contentStartPosition = startPosition;
            while (contentStartPosition < fileData.Length && fileData[contentStartPosition] != 0x00)
            {
                contentStartPosition++;
            }
            // Skip the null terminator
            contentStartPosition++;

            // Calculate content length up to max size
            int contentLength = maxSize;
            if (contentStartPosition + contentLength > fileData.Length)
            {
                contentLength = fileData.Length - contentStartPosition;
            }

            byte[] contentBytes = new byte[contentLength];
            Array.Copy(fileData, contentStartPosition, contentBytes, 0, contentLength);

            byte[] trimmedBytes = RemoveZeroPadding(contentBytes);
            return Encoding.Default.GetString(trimmedBytes);
        }

        private static byte[] RemoveZeroPadding(byte[] content)
        {
            // Remove zero padding (0x00) from the content
            int i = content.Length - 1;
            while (i >= 0 && content[i] == 0x00)
            {
                i--;
            }

            byte[] trimmedContent = new byte[i + 1];
            Array.Copy(content, 0, trimmedContent, 0, i + 1);
            return trimmedContent;
        }
        #endregion
    }
}
