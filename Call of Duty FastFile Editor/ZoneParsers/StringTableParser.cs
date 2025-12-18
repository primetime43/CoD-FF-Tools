using Call_of_Duty_FastFile_Editor.Models;
using Call_of_Duty_FastFile_Editor.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Call_of_Duty_FastFile_Editor.ZoneParsers
{
    /// <summary>
    /// Parser for StringTable assets in CoD4/WaW zone files.
    ///
    /// Structure (from wiki https://codresearch.dev/index.php/StringTable_Asset):
    /// struct StringTable {
    ///   const char *name;       // 4 bytes - pointer (FF FF FF FF if inline)
    ///   int columnCount;        // 4 bytes
    ///   int rowCount;           // 4 bytes
    ///   const char **values;    // 4 bytes - pointer (FF FF FF FF if inline)
    /// };
    ///
    /// When pointers are inline (FF FF FF FF), data follows immediately.
    /// </summary>
    public class StringTableParser
    {
        /// <summary>
        /// Parses a StringTable asset at the given offset using structure-based parsing.
        /// NOTE: PC parsing is not currently supported - AssetRecordProcessor returns early for PC files.
        /// This parser uses big-endian (console) byte order.
        /// </summary>
        public static StringTable? ParseStringTable(FastFile openedFastFile, int startingOffset)
        {
            byte[] fileData = openedFastFile.OpenedFastFileZone.Data;

            Debug.WriteLine($"[StringTableParser] Starting parse at offset 0x{startingOffset:X}");

            if (startingOffset + 16 > fileData.Length)
            {
                Debug.WriteLine($"[StringTableParser] Not enough data for header at 0x{startingOffset:X}");
                return null;
            }

            // Read the 16-byte header (big-endian for console)
            // [0-3]: name pointer
            // [4-7]: columnCount
            // [8-11]: rowCount
            // [12-15]: values pointer
            uint namePointer = Utilities.ReadUInt32BigEndian(fileData, startingOffset);
            int columnCount = (int)Utilities.ReadUInt32BigEndian(fileData, startingOffset + 4);
            int rowCount = (int)Utilities.ReadUInt32BigEndian(fileData, startingOffset + 8);
            uint valuesPointer = Utilities.ReadUInt32BigEndian(fileData, startingOffset + 12);

            Debug.WriteLine($"[StringTableParser] Header: namePtr=0x{namePointer:X}, cols={columnCount}, rows={rowCount}, valuesPtr=0x{valuesPointer:X}");

            // Name pointer must be inline (FF FF FF FF) for embedded data
            if (namePointer != 0xFFFFFFFF)
            {
                Debug.WriteLine($"[StringTableParser] Name pointer not inline (0x{namePointer:X}), skipping.");
                return null;
            }

            // Validate row/column counts are reasonable
            if (columnCount <= 0 || columnCount > 1000 || rowCount <= 0 || rowCount > 100000)
            {
                Debug.WriteLine($"[StringTableParser] Invalid dimensions: {columnCount} x {rowCount}");
                return null;
            }

            int cellCount = rowCount * columnCount;

            // Table name follows the 16-byte header (null-terminated string)
            int tableNameOffset = startingOffset + 16;
            string tableName = Utilities.ReadNullTerminatedString(fileData, tableNameOffset);

            if (string.IsNullOrEmpty(tableName))
            {
                Debug.WriteLine($"[StringTableParser] Empty table name at 0x{tableNameOffset:X}");
                return null;
            }

            Debug.WriteLine($"[StringTableParser] Table name: '{tableName}'");

            // Calculate where the header ends (after the name string + null terminator)
            int headerLength = 16 + tableName.Length + 1;
            int endOfHeader = startingOffset + headerLength;

            // Values pointer determines where cell pointer array starts
            // If inline (0xFFFFFFFF), it follows the name
            int cellDataBlockOffset;
            if (valuesPointer == 0xFFFFFFFF)
            {
                cellDataBlockOffset = endOfHeader;
            }
            else
            {
                // External pointer - data is elsewhere (rare case)
                cellDataBlockOffset = (int)valuesPointer;
            }

            // Cell pointer array: cellCount * 4 bytes
            // Each cell has a 4-byte pointer to its string
            int cellBytesNeeded = cellCount * 4;
            if (cellDataBlockOffset + cellBytesNeeded > fileData.Length)
            {
                Debug.WriteLine($"[StringTableParser] Not enough data for cell pointers at 0x{cellDataBlockOffset:X}");
                return null;
            }

            // String data pool starts after the cell pointer array
            int stringBlockOffset = cellDataBlockOffset + cellBytesNeeded;
            if (stringBlockOffset >= fileData.Length)
            {
                Debug.WriteLine($"[StringTableParser] No room for strings at 0x{stringBlockOffset:X}");
                return null;
            }

            Debug.WriteLine($"[StringTableParser] Cell pointers at 0x{cellDataBlockOffset:X}, strings at 0x{stringBlockOffset:X}");

            // Read strings from the string data pool
            List<(int Offset, string Text)> cells = new List<(int Offset, string Text)>();
            int dataStartPos = stringBlockOffset;
            int dataEndPos = stringBlockOffset;

            int currentOffset = stringBlockOffset;
            int stringsRead = 0;

            // Read strings until we hit the next asset marker or reach expected count
            while (currentOffset < fileData.Length && stringsRead < cellCount)
            {
                // Check for next asset marker (FF FF FF FF)
                if (currentOffset + 4 <= fileData.Length &&
                    fileData[currentOffset] == 0xFF &&
                    fileData[currentOffset + 1] == 0xFF &&
                    fileData[currentOffset + 2] == 0xFF &&
                    fileData[currentOffset + 3] == 0xFF)
                {
                    Debug.WriteLine($"[StringTableParser] Hit next asset marker at 0x{currentOffset:X}");
                    break;
                }

                // Read the null-terminated string
                string cellValue = Utilities.ReadNullTerminatedString(fileData, currentOffset);
                cells.Add((currentOffset, cellValue));
                stringsRead++;

                // Move past the string and its null terminator
                currentOffset += cellValue.Length + 1;
                dataEndPos = currentOffset - 1;
            }

            Debug.WriteLine($"[StringTableParser] Read {cells.Count} strings (expected {cellCount} cells)");

            // Build and return the StringTable
            return new StringTable
            {
                RowCount = rowCount,
                ColumnCount = columnCount,
                TableName = tableName,
                RowCountOffset = startingOffset + 8,
                ColumnCountOffset = startingOffset + 4,
                TableNameOffset = tableNameOffset,
                DataStartPosition = dataStartPos,
                DataEndPosition = dataEndPos,
                StartOfFileHeader = startingOffset,
                EndOfFileHeader = endOfHeader,
                Cells = cells,
                AdditionalData = $"Structure-based parse; {columnCount}x{rowCount}={cellCount} cells, {cells.Count} strings read."
            };
        }
    }
}
