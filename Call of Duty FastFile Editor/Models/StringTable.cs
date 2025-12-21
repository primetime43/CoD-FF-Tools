using Call_of_Duty_FastFile_Editor.Services;
using System.Diagnostics;
using System.Text;

namespace Call_of_Duty_FastFile_Editor.Models
{
    public class StringTable : IAssetRecordUpdatable
    {
        public StringTable()
        {

        }

        public int RowCount { get; set; }
        public int RowCountOffset { get; set; }
        public int ColumnCount { get; set; }
        public int ColumnCountOffset { get; set; }
        public required string TableName { get; set; }
        public int TableNameOffset { get; set; }
        public int DataStartPosition { get; set; }
        public int DataEndPosition { get; set; }

        public int StartOfFileHeader { get; set; }
        public int EndOfFileHeader { get; set; }


        // Each item is (Offset, Text).
        public List<(int Offset, string Text)>? Cells { get; set; }

        public string AdditionalData { get; set; }

        public void UpdateAssetRecord(ref ZoneAssetRecord assetRecord)
        {
            assetRecord.Name = this.TableName;

            // Use the new Cells list to build a single string of all cell text
            if (Cells != null)
            {
                // If you only want the text:
                assetRecord.Content = string.Join(", ", Cells.Select(c => c.Text));

                // Alternatively, if you want offsets too (in hex):
                // assetRecord.Content = string.Join("; ", Cells.Select(c => $"0x{c.Offset:X}: {c.Text}"));
            }
            else
            {
                assetRecord.Content = string.Empty;
            }

            assetRecord.HeaderStartOffset = this.StartOfFileHeader;
            assetRecord.HeaderEndOffset = this.EndOfFileHeader;
            assetRecord.AdditionalData = this.AdditionalData;
            assetRecord.AssetDataStartPosition = this.DataStartPosition;
            assetRecord.AssetDataEndOffset = this.DataEndPosition;
            assetRecord.AssetRecordEndOffset = this.DataEndPosition + 1;
        }

        /// <summary>
        /// Scans the zone file for string table entries that look like:
        ///
        ///   [ rowCount (4 bytes, big-endian) ]
        ///   [ columnCount (4 bytes, big-endian) ]
        ///   [ 0xFF FF FF FF ]
        ///   [ null-terminated ASCII filename ending in .csv ]
        ///
        /// This is more robust than scanning for ".csv" first.
        /// TEMP EVENTUALLY DELETE
        /// </summary>
        /// <param name="zone">The zone file to search.</param>
        /// <param name="startingOffset">Offset to start searching from.</param>
        /// <param name="bytesPerCell">Bytes per cell: 4 for CoD4/WaW, 8 for MW2 (string pointer + hash).</param>
        public static StringTable? FindSingleCsvStringTableWithPattern(ZoneFile zone, int startingOffset, int bytesPerCell = 4)
        {
            byte[] zoneBytes = zone.Data;
            if (startingOffset < 0 || startingOffset >= zoneBytes.Length)
            {
                Debug.WriteLine($"[FindSingleCsvStringTableWithPattern] Starting offset {startingOffset} is out of range.");
                throw new ArgumentOutOfRangeException(nameof(startingOffset), "Starting offset is outside the zone file data range.");
            }

            Debug.WriteLine($"[FindSingleCsvStringTableWithPattern] Searching for \".csv\\0\" pattern starting at offset 0x{startingOffset:X}");

            // Search for the pattern: .csv\0 (0x2E, 0x63, 0x73, 0x76, 0x00)
            for (int i = startingOffset; i < zoneBytes.Length - 4; i++)
            {
                if (zoneBytes[i] == 0x2E &&   // '.'
                    zoneBytes[i + 1] == 0x63 && // 'c'
                    zoneBytes[i + 2] == 0x73 && // 's'
                    zoneBytes[i + 3] == 0x76 && // 'v'
                    zoneBytes[i + 4] == 0x00)   // null terminator
                {
                    Debug.WriteLine($"[FindSingleCsvStringTableWithPattern] Found \".csv\\0\" at offset 0x{i:X}");
                    int foundPatternOffset = i;

                    // Walk backward from the found pattern until we hit a 4-byte FF marker.
                    int nameStart = -1;
                    for (int j = foundPatternOffset; j >= startingOffset + 4; j--)
                    {
                        if (zoneBytes[j - 4] == 0xFF &&
                            zoneBytes[j - 3] == 0xFF &&
                            zoneBytes[j - 2] == 0xFF &&
                            zoneBytes[j - 1] == 0xFF)
                        {
                            nameStart = j;
                            Debug.WriteLine($"[FindSingleCsvStringTableWithPattern] Found marker FF FF FF FF ending at offset 0x{j:X}; name starts at 0x{nameStart:X}");
                            break;
                        }
                    }
                    if (nameStart == -1)
                    {
                        Debug.WriteLine($"[FindSingleCsvStringTableWithPattern] Marker not found before .csv pattern at offset 0x{foundPatternOffset:X}. Skipping.");
                        continue;
                    }

                    // Read the null-terminated table name starting at nameStart.
                    string tableName = Utilities.ReadNullTerminatedString(zoneBytes, nameStart);
                    Debug.WriteLine($"[FindSingleCsvStringTableWithPattern] Read table name: \"{tableName}\" from offset 0x{nameStart:X}");
                    if (!tableName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ||
                        !tableName.Contains('/'))
                    {
                        Debug.WriteLine($"[FindSingleCsvStringTableWithPattern] Table name \"{tableName}\" failed validation. Skipping.");
                        continue;
                    }

                    // The header is expected to start 16 bytes before the table name.
                    int headerStart = nameStart - 16;
                    if (headerStart < 0)
                    {
                        Debug.WriteLine($"[FindSingleCsvStringTableWithPattern] Calculated header start (0x{headerStart:X}) is negative. Skipping.");
                        continue;
                    }
                    Debug.WriteLine($"[FindSingleCsvStringTableWithPattern] Assumed header start at offset 0x{headerStart:X}");

                    // Verify marker at headerStart: should be 0xFFFFFFFF in big-endian.
                    uint headerMarker = Utilities.ReadUInt32BigEndian(zoneBytes, headerStart);
                    Debug.WriteLine($"[FindSingleCsvStringTableWithPattern] Marker at header start: 0x{headerMarker:X}");
                    if (headerMarker != 0xFFFFFFFF)
                    {
                        Debug.WriteLine($"[FindSingleCsvStringTableWithPattern] Marker does not equal 0xFFFFFFFF. Skipping.");
                        continue;
                    }

                    // Read column count (headerStart+4) and row count (headerStart+8) as big-endian.
                    int columnCount = (int)Utilities.ReadUInt32BigEndian(zoneBytes, headerStart + 4);
                    int rowCount = (int)Utilities.ReadUInt32BigEndian(zoneBytes, headerStart + 8);
                    Debug.WriteLine($"[FindSingleCsvStringTableWithPattern] Read column count: {columnCount}, row count: {rowCount}");

                    // Read the "valuesPointer" field (at headerStart+12).
                    int valuesPointer = (int)Utilities.ReadUInt32BigEndian(zoneBytes, headerStart + 12);
                    Debug.WriteLine($"[FindSingleCsvStringTableWithPattern] Read valuesPointer: 0x{valuesPointer:X} from header offset 0x{headerStart + 12:X}");

                    // Calculate header length: 16 bytes plus table name length plus the null terminator.
                    int headerLength = 16 + tableName.Length + 1;
                    int endOfHeader = headerStart + headerLength;
                    Debug.WriteLine($"[FindSingleCsvStringTableWithPattern] Calculated header length: {headerLength} bytes, end of header at offset 0x{endOfHeader:X}");

                    // Calculate cell count.
                    int cellCount = rowCount * columnCount;
                    if (cellCount <= 0)
                    {
                        Debug.WriteLine($"[FindSingleCsvStringTableWithPattern] Invalid cell count: {cellCount} (rowCount {rowCount} * columnCount {columnCount}). Skipping.");
                        continue;
                    }

                    // Determine the cell data block offset.
                    int cellDataBlockOffset = (valuesPointer != -1 && valuesPointer != 0)
                        ? valuesPointer
                        : endOfHeader;
                    Debug.WriteLine($"[FindSingleCsvStringTableWithPattern] Using cellDataBlockOffset = 0x{cellDataBlockOffset:X}");

                    int cellBytesNeeded = cellCount * bytesPerCell;
                    Debug.WriteLine($"[FindSingleCsvStringTableWithPattern] Using {bytesPerCell} bytes per cell, cellBytesNeeded = {cellBytesNeeded}");
                    if (cellDataBlockOffset + cellBytesNeeded > zoneBytes.Length)
                    {
                        Debug.WriteLine($"[FindSingleCsvStringTableWithPattern] Not enough data for cell block at 0x{cellDataBlockOffset:X}.");
                        continue;
                    }

                    // The string block follows the cell data block.
                    int stringBlockOffset = cellDataBlockOffset + cellBytesNeeded;
                    if (stringBlockOffset >= zoneBytes.Length)
                    {
                        Debug.WriteLine($"[FindSingleCsvStringTableWithPattern] No room left for strings at offset 0x{stringBlockOffset:X}.");
                        continue;
                    }
                    Debug.WriteLine($"[FindSingleCsvStringTableWithPattern] String block starts at offset 0x{stringBlockOffset:X}");

                    // Build the cells list by reading null-terminated strings until a sentinel pattern is encountered.
                    List<(int Offset, string Text)> cells = new List<(int Offset, string Text)>();
                    int codeStartPos = stringBlockOffset;
                    int codeEndPos = codeStartPos;
                    int currentStringOffset = stringBlockOffset;
                    while (currentStringOffset < zoneBytes.Length)
                    {
                        int offsetForThisString = currentStringOffset;
                        string cellValue = Utilities.ReadNullTerminatedString(zoneBytes, offsetForThisString);
                        cells.Add((offsetForThisString, cellValue));
                        //Debug.WriteLine($"[FindSingleCsvStringTableWithPattern] Read cell string \"{cellValue}\" at offset 0x{offsetForThisString:X}");
                        currentStringOffset += (cellValue.Length + 1);
                        codeEndPos = currentStringOffset - 1;
                        if (currentStringOffset + 4 > zoneBytes.Length)
                        {
                            break;
                        }
                        // Check for sentinel: 00 followed by 4 FFs.
                        if (zoneBytes[currentStringOffset] == 0xFF &&
                            zoneBytes[currentStringOffset + 1] == 0xFF &&
                            zoneBytes[currentStringOffset + 2] == 0xFF &&
                            zoneBytes[currentStringOffset + 3] == 0xFF &&
                            zoneBytes[currentStringOffset - 1] == 0x00)
                        {
                            Debug.WriteLine($"[FindSingleCsvStringTableWithPattern] Sentinel pattern found at offset 0x{currentStringOffset:X}. Ending cell read.");
                            break;
                        }
                    }
                    Debug.WriteLine($"[FindSingleCsvStringTableWithPattern] Total cells read: {cells.Count} (expected {cellCount}).");

                    return new StringTable
                    {
                        RowCount = rowCount,
                        ColumnCount = columnCount,
                        TableName = tableName,
                        RowCountOffset = headerStart + 8,
                        ColumnCountOffset = headerStart + 4,
                        TableNameOffset = nameStart,
                        DataStartPosition = codeStartPos,
                        DataEndPosition = codeEndPos,
                        StartOfFileHeader = headerStart,
                        EndOfFileHeader = endOfHeader,
                        Cells = cells,
                        AdditionalData = $"Found .csv at offset 0x{foundPatternOffset:X}; table name starts at 0x{nameStart:X}; header starts at 0x{headerStart:X}; totalCellCount={cellCount}, read {cells.Count} strings."
                    };
                }
            }

            Debug.WriteLine("[FindSingleCsvStringTableWithPattern] No valid string table found.");
            return null;
        }
    }
}
