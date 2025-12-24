using Call_of_Duty_FastFile_Editor.Models;
using Call_of_Duty_FastFile_Editor.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Call_of_Duty_FastFile_Editor.ZoneParsers
{
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

            // Marker: typically 0xFFFFFFFF
            int marker = (int)Utilities.ReadUInt32BigEndian(fileData, startingOffset);
            if (marker != -1)
            {
                Debug.WriteLine($"Unexpected marker at offset 0x{startingOffset:X}: 0x{marker:X}. Aborting.");
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

            // "valuesPointer" field
            int valuesPointer = (int)Utilities.ReadUInt32BigEndian(fileData, startingOffset + 12);

            // Table name (null-terminated) at offset +16
            string tableName = Utilities.ReadNullTerminatedString(fileData, startingOffset + 16);

            // Calculate header length
            int headerLength = 16 + tableName.Length + 1;
            int endOfHeader = startingOffset + headerLength;

            // rowCount * columnCount
            int cellCount = rowCount * columnCount;
            if (cellCount <= 0)
            {
                Debug.WriteLine($"Bad rowCount/columnCount: {rowCount} x {columnCount} = {cellCount}.");
                return null;
            }

            // 4-byte "cell data" block offset
            int cellDataBlockOffset = (valuesPointer != -1 && valuesPointer != 0)
                ? valuesPointer
                : endOfHeader;

            int cellBytesNeeded = cellCount * 4;
            if (cellDataBlockOffset + cellBytesNeeded > fileData.Length)
            {
                Debug.WriteLine($"Not enough data for row/column block at 0x{cellDataBlockOffset:X}.");
                return null;
            }

            // (Optional) read these 4-byte values (IDs)
            // e.g. 
            // for (int i = 0; i < cellCount; i++)
            // {
            //     int offset = cellDataBlockOffset + (i * 4);
            //     uint cellID = Utilities.ReadUInt32AtOffset(offset, openedFastFile.OpenedFastFileZone, isBigEndian: true);
            // }

            // The actual strings: read until sentinel or end of file
            int stringBlockOffset = cellDataBlockOffset + cellBytesNeeded;
            if (stringBlockOffset >= fileData.Length)
            {
                Debug.WriteLine($"No room left for strings, offset=0x{stringBlockOffset:X} > file size.");
                return null;
            }

            // We'll store both offset & text in a single list
            List<(int Offset, string Text)> cells = new List<(int Offset, string Text)>();

            // CodeStartPosition => first string offset
            int codeStartPos = stringBlockOffset;

            // We'll keep updating DataEndPosition to the last null terminator read
            int codeEndPos = codeStartPos;

            int currentStringOffset = stringBlockOffset;
            while (currentStringOffset < fileData.Length)
            {
                // 1) Read the next null-terminated string
                int offsetForThisString = currentStringOffset;
                string cellValue = Utilities.ReadNullTerminatedString(fileData, offsetForThisString);
                cells.Add((offsetForThisString, cellValue));

                // 2) Advance offset to after the null terminator
                currentStringOffset += (cellValue.Length + 1);

                // The last character read was the string's null terminator
                codeEndPos = currentStringOffset - 1;
                // So codeEndPos points to the 0 of that string's end.

                // 3) Check for space to verify sentinel
                if (currentStringOffset + 4 > fileData.Length)
                {
                    // Not enough room to read sentinel => break
                    break;
                }

                // 4) Check sentinel pattern: 00 FF FF FF FF
                if (fileData[currentStringOffset] == 0xFF &&
                    fileData[currentStringOffset + 1] == 0xFF &&
                    fileData[currentStringOffset + 2] == 0xFF &&
                    fileData[currentStringOffset + 3] == 0xFF &&
                    fileData[currentStringOffset - 1] == 0x00)
                {
                    // We found 00 FF FF FF FF, meaning we break right here. (this might not be right. possibly fix)
                    // codeEndPos is already set to the 0 that precedes the FF FF FF FF.
                    break;
                }
            }

            // Build & return the final StringTable
            var stringTable = new StringTable
            {
                RowCount = rowCount,
                ColumnCount = columnCount,
                TableName = tableName,

                RowCountOffset = startingOffset + 8,
                ColumnCountOffset = startingOffset + 4,
                TableNameOffset = startingOffset + 16,

                DataStartPosition = codeStartPos,
                DataEndPosition = codeEndPos,

                StartOfFileHeader = startingOffset,
                EndOfFileHeader = endOfHeader,

                Cells = cells,
                AdditionalData = $"Parsed from offset 0x{startingOffset:X}; totalCellCount={cellCount}, read {cells.Count} strings."
            };

            return stringTable;
        }
    }
}
