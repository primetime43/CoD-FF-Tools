using Call_of_Duty_FastFile_Editor.Constants;
using Call_of_Duty_FastFile_Editor.GameDefinitions;
using Call_of_Duty_FastFile_Editor.Models;
using Call_of_Duty_FastFile_Editor.Services;
using FastFileLib.GameDefinitions;
using System.Diagnostics;
using System.Text;

namespace Call_of_Duty_FastFile_Editor.ZoneParsers
{
    /// <summary>
    /// Structure-based zone parser that uses header field counts instead of pattern matching.
    /// Reference: https://codresearch.dev/index.php/FastFiles_and_Zone_files_(MW2)
    ///
    /// Zone Structure:
    /// [0x00-0x33] XFile Header + XAssetList header (52 bytes)
    /// [0x34+]     Script strings (tags) - null-terminated, count from ScriptStringCount
    /// [after tags] Asset pool records - 8 bytes each, count from AssetCount
    /// [after pool] Asset data
    /// </summary>
    public class StructureBasedZoneParser
    {
        private readonly ZoneFile _zone;
        private readonly byte[] _data;
        private readonly bool _isCod4;
        private readonly bool _isCod5;
        private readonly bool _isMW2;
        private readonly bool _isXbox360;
        private readonly bool _isPC;
        private readonly bool _isBigEndian;
        private readonly int _headerSize;

        // Platform-specific header sizes:
        // Xbox 360: XFile (32 bytes = 6 blocks) + XAssetList (16 bytes) = 48 bytes (0x30)
        // PS3: XFile (36 bytes = 7 blocks) + XAssetList (16 bytes) = 52 bytes (0x34)
        // PC: XFile (40 bytes = 8 blocks) + XAssetList (16 bytes) = 56 bytes (0x38)
        private const int HEADER_SIZE_XBOX360 = 0x30;
        private const int HEADER_SIZE_PS3 = 0x34;
        private const int HEADER_SIZE_PC = 0x38;

        public StructureBasedZoneParser(ZoneFile zone)
        {
            _zone = zone;
            _data = zone.Data;
            _isCod4 = zone.ParentFastFile?.IsCod4File ?? false;
            _isCod5 = zone.ParentFastFile?.IsCod5File ?? false;
            _isMW2 = zone.ParentFastFile?.IsMW2File ?? false;
            _isXbox360 = zone.ParentFastFile?.IsXbox360 ?? false;
            _isPC = zone.ParentFastFile?.IsPC ?? false;
            _isBigEndian = !_isPC; // PC uses little-endian, consoles use big-endian

            // Set header size based on platform
            if (_isXbox360)
                _headerSize = HEADER_SIZE_XBOX360;
            else if (_isPC)
                _headerSize = HEADER_SIZE_PC;
            else
                _headerSize = HEADER_SIZE_PS3;

            Debug.WriteLine($"[StructureParser] Platform: {(_isXbox360 ? "Xbox 360" : (_isPC ? "PC" : "PS3"))}, Header size: 0x{_headerSize:X}");
        }

        /// <summary>
        /// Parses the zone file using structure-based approach.
        /// Returns true if successful, false otherwise.
        /// </summary>
        public bool Parse()
        {
            try
            {
                int expectedTagCount = (int)_zone.ScriptStringCount;
                int expectedAssetCount = (int)_zone.AssetCount;
                int tagSectionStart;
                int tagSectionEnd;
                List<ZoneAsset_TagEntry> tags;

                Debug.WriteLine($"[StructureParser] Expected tags: {expectedTagCount}, Expected assets: {expectedAssetCount}");
                Debug.WriteLine($"[StructureParser] IsXbox360={_isXbox360}, IsPC={_isPC}, IsBigEndian={_isBigEndian}");

                // Step 1: Handle tags
                if (expectedTagCount == 0)
                {
                    // No tags - asset pool starts right after header
                    tagSectionStart = _headerSize;
                    tagSectionEnd = _headerSize;
                    tags = new List<ZoneAsset_TagEntry>();
                    Debug.WriteLine($"[StructureParser] No tags expected, starting asset pool at header end (0x{_headerSize:X})");
                }
                else
                {
                    // Find where script strings (tags) start
                    tagSectionStart = FindTagSectionStart();
                    Debug.WriteLine($"[StructureParser] FindTagSectionStart returned 0x{tagSectionStart:X}");
                    if (tagSectionStart < 0)
                    {
                        Debug.WriteLine("[StructureParser] Could not find tag section start");
                        return false;
                    }

                    // Step 2: Read exactly ScriptStringCount tags
                    (tags, tagSectionEnd) = ReadTags(tagSectionStart, expectedTagCount);
                    Debug.WriteLine($"[StructureParser] ReadTags: got {tags.Count} tags, endOffset=0x{tagSectionEnd:X}");

                    if (tags.Count != expectedTagCount)
                    {
                        Debug.WriteLine($"[StructureParser] Tag count mismatch: expected {expectedTagCount}, got {tags.Count}");
                        // Continue anyway - some zones may have padding
                    }
                }

                _zone.TagSectionStartOffset = tagSectionStart;
                _zone.TagSectionEndOffset = tagSectionEnd;

                // Step 3: Asset pool starts right after tags
                int assetPoolStart = tagSectionEnd;

                // Skip any padding between tags and asset pool, looking for either format
                while (assetPoolStart + 8 <= _data.Length)
                {
                    // Format A (Big-Endian Console): 00 00 00 XX FF FF FF FF (type first)
                    if (_isBigEndian &&
                        _data[assetPoolStart] == 0x00 && _data[assetPoolStart + 1] == 0x00 &&
                        _data[assetPoolStart + 2] == 0x00 &&
                        _data[assetPoolStart + 4] == 0xFF && _data[assetPoolStart + 5] == 0xFF &&
                        _data[assetPoolStart + 6] == 0xFF && _data[assetPoolStart + 7] == 0xFF)
                    {
                        Debug.WriteLine($"[StructureParser] Found Format A (BE) asset pool at 0x{assetPoolStart:X}");
                        break;
                    }

                    // Format A (Little-Endian PC): XX 00 00 00 FF FF FF FF (type first, LE)
                    if (!_isBigEndian &&
                        _data[assetPoolStart + 1] == 0x00 && _data[assetPoolStart + 2] == 0x00 &&
                        _data[assetPoolStart + 3] == 0x00 &&
                        _data[assetPoolStart + 4] == 0xFF && _data[assetPoolStart + 5] == 0xFF &&
                        _data[assetPoolStart + 6] == 0xFF && _data[assetPoolStart + 7] == 0xFF)
                    {
                        Debug.WriteLine($"[StructureParser] Found Format A (LE) asset pool at 0x{assetPoolStart:X}");
                        break;
                    }

                    // Format B (Big-Endian Console): FF FF FF FF 00 00 00 XX (pointer first)
                    if (_isBigEndian &&
                        _data[assetPoolStart] == 0xFF && _data[assetPoolStart + 1] == 0xFF &&
                        _data[assetPoolStart + 2] == 0xFF && _data[assetPoolStart + 3] == 0xFF &&
                        _data[assetPoolStart + 4] == 0x00 && _data[assetPoolStart + 5] == 0x00 &&
                        _data[assetPoolStart + 6] == 0x00)
                    {
                        Debug.WriteLine($"[StructureParser] Found Format B (BE) asset pool at 0x{assetPoolStart:X}");
                        break;
                    }

                    // Format B (Little-Endian PC): FF FF FF FF XX 00 00 00 (pointer first, LE)
                    if (!_isBigEndian &&
                        _data[assetPoolStart] == 0xFF && _data[assetPoolStart + 1] == 0xFF &&
                        _data[assetPoolStart + 2] == 0xFF && _data[assetPoolStart + 3] == 0xFF &&
                        _data[assetPoolStart + 5] == 0x00 && _data[assetPoolStart + 6] == 0x00 &&
                        _data[assetPoolStart + 7] == 0x00)
                    {
                        Debug.WriteLine($"[StructureParser] Found Format B (LE) asset pool at 0x{assetPoolStart:X}");
                        break;
                    }

                    // Skip padding bytes (0x00 or other non-pattern bytes)
                    assetPoolStart++;
                }

                // Step 4: Read exactly AssetCount asset records
                var (records, assetPoolEnd) = ReadAssetRecords(assetPoolStart, expectedAssetCount);

                if (records.Count != expectedAssetCount)
                {
                    Debug.WriteLine($"[StructureParser] Asset count mismatch: expected {expectedAssetCount}, got {records.Count}");
                }

                // Store results
                _zone.ZoneFileAssets.ZoneAssetRecords = records;
                _zone.AssetPoolStartOffset = assetPoolStart;
                _zone.AssetPoolEndOffset = assetPoolEnd;

                Debug.WriteLine($"[StructureParser] Success: {tags.Count} tags, {records.Count} assets");
                Debug.WriteLine($"[StructureParser] Tag section: 0x{tagSectionStart:X} - 0x{tagSectionEnd:X}");
                Debug.WriteLine($"[StructureParser] Asset pool: 0x{assetPoolStart:X} - 0x{assetPoolEnd:X}");

                return records.Count > 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StructureParser] Exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Finds the start of the tag section by looking for the FF padding after the header,
        /// then finding where ASCII strings begin.
        /// </summary>
        private int FindTagSectionStart()
        {
            // Start after the header
            int offset = _headerSize;

            // Skip any initial zeros or FF padding
            while (offset < _data.Length)
            {
                byte b = _data[offset];

                // Skip 0x00 and 0xFF padding
                if (b == 0x00 || b == 0xFF)
                {
                    offset++;
                    continue;
                }

                // Check if this looks like the start of a tag (printable ASCII)
                if (b >= 0x20 && b <= 0x7E)
                {
                    // Validate it's a reasonable tag by checking for null terminator within 128 bytes
                    int nullPos = FindNullTerminator(offset, Math.Min(offset + 128, _data.Length));
                    if (nullPos > offset)
                    {
                        return offset;
                    }
                }

                offset++;
            }

            return -1;
        }

        /// <summary>
        /// Reads the specified number of null-terminated tags starting from the given offset.
        /// </summary>
        private (List<ZoneAsset_TagEntry> tags, int endOffset) ReadTags(int startOffset, int count)
        {
            var tags = new List<ZoneAsset_TagEntry>();
            int offset = startOffset;

            for (int i = 0; i < count && offset < _data.Length; i++)
            {
                // Skip any null padding between tags
                while (offset < _data.Length && _data[offset] == 0x00)
                {
                    // Check if we've hit the asset pool - Format A (00 00 00 XX FF FF FF FF pattern)
                    if (offset + 8 <= _data.Length &&
                        _data[offset + 1] == 0x00 && _data[offset + 2] == 0x00 &&
                        _data[offset + 4] == 0xFF && _data[offset + 5] == 0xFF &&
                        _data[offset + 6] == 0xFF && _data[offset + 7] == 0xFF)
                    {
                        Debug.WriteLine($"[ReadTags] Hit Format A asset pool at 0x{offset:X} after {tags.Count} tags");
                        return (tags, offset);
                    }
                    offset++;
                }

                // Also check for Format B asset pool (FF FF FF FF 00 00 00 XX) used by Xbox 360
                if (offset + 8 <= _data.Length &&
                    _data[offset] == 0xFF && _data[offset + 1] == 0xFF &&
                    _data[offset + 2] == 0xFF && _data[offset + 3] == 0xFF &&
                    _data[offset + 4] == 0x00 && _data[offset + 5] == 0x00 &&
                    _data[offset + 6] == 0x00)
                {
                    Debug.WriteLine($"[ReadTags] Hit Format B asset pool at 0x{offset:X} after {tags.Count} tags");
                    return (tags, offset);
                }

                if (offset >= _data.Length)
                    break;

                // Read the tag string
                int tagStart = offset;
                string tag = ReadNullTerminatedString(offset, out int bytesRead);

                if (string.IsNullOrEmpty(tag))
                    break;

                tags.Add(new ZoneAsset_TagEntry
                {
                    Tag = tag,
                    OffsetDec = tagStart,
                    OffsetHex = tagStart.ToString("X")
                });

                offset += bytesRead;
            }

            return (tags, offset);
        }

        /// <summary>
        /// Reads the specified number of 8-byte asset records starting from the given offset.
        /// Supports two formats:
        /// - Format A: [4-byte type] [FF FF FF FF] (type first)
        /// - Format B: [FF FF FF FF] [4-byte type] (pointer first, used in PS3 WAW zones)
        /// </summary>
        private (List<ZoneAssetRecord> records, int endOffset) ReadAssetRecords(int startOffset, int count)
        {
            var records = new List<ZoneAssetRecord>();
            int offset = startOffset;

            // Detect format by checking first record
            // Format A: FF FF FF FF at bytes 4-7
            // Format B: FF FF FF FF at bytes 0-3
            bool isFormatB = offset + 8 <= _data.Length &&
                             _data[offset] == 0xFF && _data[offset + 1] == 0xFF &&
                             _data[offset + 2] == 0xFF && _data[offset + 3] == 0xFF &&
                             !(_data[offset + 4] == 0xFF && _data[offset + 5] == 0xFF); // Not end marker

            if (isFormatB)
            {
                Debug.WriteLine($"[ReadAssetRecords] Detected Format B (pointer-first) at 0x{offset:X}");
            }
            Debug.WriteLine($"[ReadAssetRecords] Using endianness: {(_isBigEndian ? "Big-Endian" : "Little-Endian")}");

            for (int i = 0; i < count && offset + 8 <= _data.Length; i++)
            {
                int assetTypeInt;
                bool validRecord;

                if (isFormatB)
                {
                    // Format B: [FF FF FF FF] [4-byte type]
                    validRecord = _data[offset] == 0xFF && _data[offset + 1] == 0xFF &&
                                  _data[offset + 2] == 0xFF && _data[offset + 3] == 0xFF;

                    // Check for end marker (all FF)
                    if (validRecord && _data[offset + 4] == 0xFF && _data[offset + 5] == 0xFF &&
                        _data[offset + 6] == 0xFF && _data[offset + 7] == 0xFF)
                    {
                        Debug.WriteLine($"[ReadAssetRecords] Hit end marker at 0x{offset:X} after {records.Count} records");
                        offset += 8; // Skip end marker
                        break;
                    }

                    assetTypeInt = (int)Utilities.ReadUInt32AtOffset(offset + 4, _zone, isBigEndian: _isBigEndian);
                }
                else
                {
                    // Format A: [4-byte type] [FF FF FF FF]
                    validRecord = _data[offset + 4] == 0xFF && _data[offset + 5] == 0xFF &&
                                  _data[offset + 6] == 0xFF && _data[offset + 7] == 0xFF;

                    // Check for end marker (FF FF FF FF at start)
                    if (!validRecord && _data[offset] == 0xFF && _data[offset + 1] == 0xFF &&
                        _data[offset + 2] == 0xFF && _data[offset + 3] == 0xFF)
                    {
                        Debug.WriteLine($"[ReadAssetRecords] Hit end marker at 0x{offset:X} after {records.Count} records");
                        break;
                    }

                    assetTypeInt = (int)Utilities.ReadUInt32AtOffset(offset, _zone, isBigEndian: _isBigEndian);
                }

                if (!validRecord)
                {
                    Debug.WriteLine($"[ReadAssetRecords] Invalid record format at 0x{offset:X}, record #{i}");
                    break;
                }

                var record = new ZoneAssetRecord { AssetPoolRecordOffset = offset };

                if (_isCod4)
                {
                    if (_isPC)
                        record.AssetType_COD4_PC = (CoD4AssetTypePC)assetTypeInt;
                    else if (_isXbox360)
                        record.AssetType_COD4_Xbox360 = (CoD4AssetTypeXbox360)assetTypeInt;
                    else
                        record.AssetType_COD4 = (CoD4AssetTypePS3)assetTypeInt;
                }
                else if (_isCod5)
                {
                    if (_isPC)
                        record.AssetType_COD5_PC = (CoD5AssetTypePC)assetTypeInt;
                    else if (_isXbox360)
                        record.AssetType_COD5_Xbox360 = (CoD5AssetTypeXbox360)assetTypeInt;
                    else
                        record.AssetType_COD5 = (CoD5AssetTypePS3)assetTypeInt;
                }
                else if (_isMW2)
                {
                    if (_isPC)
                        record.AssetType_MW2_PC = (MW2AssetTypePC)assetTypeInt;
                    else if (_isXbox360)
                        record.AssetType_MW2_Xbox360 = (MW2AssetTypeXbox360)assetTypeInt;
                    else
                        record.AssetType_MW2 = (MW2AssetTypePS3)assetTypeInt;
                }

                records.Add(record);
                offset += 8;
            }

            // After loop, check if we're at end marker and skip it
            // This handles the case where we process exactly 'count' records
            // and exit the loop without encountering the end marker check
            if (offset + 8 <= _data.Length &&
                _data[offset] == 0xFF && _data[offset + 1] == 0xFF &&
                _data[offset + 2] == 0xFF && _data[offset + 3] == 0xFF &&
                _data[offset + 4] == 0xFF && _data[offset + 5] == 0xFF &&
                _data[offset + 6] == 0xFF && _data[offset + 7] == 0xFF)
            {
                Debug.WriteLine($"[ReadAssetRecords] Skipping end marker at 0x{offset:X} after processing {records.Count} records");
                offset += 8;
            }

            return (records, offset);
        }

        /// <summary>
        /// Reads a null-terminated ASCII string from the given offset.
        /// </summary>
        private string ReadNullTerminatedString(int offset, out int bytesRead)
        {
            bytesRead = 0;
            var sb = new StringBuilder();

            while (offset + bytesRead < _data.Length)
            {
                byte b = _data[offset + bytesRead];
                bytesRead++;

                if (b == 0x00)
                    break;

                if (b >= 0x20 && b <= 0x7E)
                    sb.Append((char)b);
                else
                    break; // Invalid character for a tag
            }

            return sb.ToString();
        }

        /// <summary>
        /// Finds the position of the first null byte starting from offset.
        /// </summary>
        private int FindNullTerminator(int start, int limit)
        {
            for (int i = start; i < limit && i < _data.Length; i++)
            {
                if (_data[i] == 0x00)
                    return i;
            }
            return -1;
        }

    }
}
