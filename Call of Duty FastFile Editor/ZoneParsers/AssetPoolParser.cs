using System.Diagnostics;
using Call_of_Duty_FastFile_Editor.GameDefinitions;
using Call_of_Duty_FastFile_Editor.Models;
using Call_of_Duty_FastFile_Editor.Services;

namespace Call_of_Duty_FastFile_Editor.ZoneParsers
{
    public class AssetPoolParser
    {
        private readonly ZoneFile _zone;

        public AssetPoolParser(ZoneFile zone)
        {
            _zone = zone;
        }

        public bool MapZoneAssetsPoolAndGetEndOffset()
        {
            var records = _zone.ZoneFileAssets.ZoneAssetRecords ?? new List<ZoneAssetRecord>();
            records.Clear();
            _zone.ZoneFileAssets.ZoneAssetRecords = records;

            byte[] data = _zone.Data;
            int fileLen = data.Length;
            int i;

            // Detect game and platform
            bool isCod4 = _zone.ParentFastFile?.IsCod4File ?? false;
            bool isCod5 = _zone.ParentFastFile?.IsCod5File ?? false;
            bool isMW2 = _zone.ParentFastFile?.IsMW2File ?? false;
            bool isXbox360 = _zone.ParentFastFile?.IsXbox360 ?? false;
            bool isPC = _zone.ParentFastFile?.IsPC ?? false;

            // PC uses little-endian, console uses big-endian
            bool isBigEndian = !isPC;

            // Start after tags, or at file start
            var tags = TagOperations.FindTags(_zone);
            i = (tags != null && tags.TagSectionEndOffset > 0) ? tags.TagSectionEndOffset : 0;

            bool foundAnyAsset = false;
            int assetPoolStart = -1;
            int endOfPoolOffset = -1;

            while (i <= fileLen - 8)
            {
                // End marker: 0xFF 0xFF 0xFF 0xFF 0xFF 0xFF 0xFF 0xFF (all FF)
                if (data[i] == 0xFF && data[i + 1] == 0xFF && data[i + 2] == 0xFF && data[i + 3] == 0xFF &&
                    data[i + 4] == 0xFF && data[i + 5] == 0xFF && data[i + 6] == 0xFF && data[i + 7] == 0xFF)
                {
                    Debug.WriteLine($"[DEBUG] END MARKER (all FF) at 0x{i:X}, breaking.");
                    endOfPoolOffset = i + 8; // Skip PAST the end marker
                    break;
                }

                // Format B: FF FF FF FF 00 00 00 XX (pointer first, used in PS3/PC WAW zones)
                // For PC little-endian, the pattern would be different, but we check based on byte patterns
                if (data[i] == 0xFF && data[i + 1] == 0xFF && data[i + 2] == 0xFF && data[i + 3] == 0xFF &&
                    data[i + 4] == 0x00 && data[i + 5] == 0x00 && data[i + 6] == 0x00)
                {
                    int assetTypeInt = (int)Utilities.ReadUInt32AtOffset(i + 4, _zone, isBigEndian: isBigEndian);
                    bool isDefined =
                        (isCod4 && Enum.IsDefined(typeof(CoD4AssetType), assetTypeInt)) ||
                        (isCod5 && isPC && Enum.IsDefined(typeof(CoD5AssetTypePC), assetTypeInt)) ||
                        (isCod5 && isXbox360 && Enum.IsDefined(typeof(CoD5AssetTypeXbox360), assetTypeInt)) ||
                        (isCod5 && !isXbox360 && !isPC && Enum.IsDefined(typeof(CoD5AssetTypePS3), assetTypeInt)) ||
                        (isMW2 && Enum.IsDefined(typeof(MW2AssetType), assetTypeInt));

                    Debug.WriteLine($"[DEBUG] Found Format B asset at 0x{i:X}: assetType=0x{assetTypeInt:X} defined={isDefined} isPC={isPC}");

                    if (isDefined)
                    {
                        if (!foundAnyAsset) assetPoolStart = i;

                        var record = new ZoneAssetRecord { AssetPoolRecordOffset = i };
                        if (isCod4) record.AssetType_COD4 = (CoD4AssetType)assetTypeInt;
                        else if (isCod5 && isPC) record.AssetType_COD5_PC = (CoD5AssetTypePC)assetTypeInt;
                        else if (isCod5 && isXbox360) record.AssetType_COD5_Xbox360 = (CoD5AssetTypeXbox360)assetTypeInt;
                        else if (isCod5) record.AssetType_COD5 = (CoD5AssetTypePS3)assetTypeInt;
                        else if (isMW2) record.AssetType_MW2 = (MW2AssetType)assetTypeInt;
                        records.Add(record);

                        foundAnyAsset = true;
                        i += 8;
                        continue;
                    }
                }

                // Format A: 00 00 00 XX FF FF FF FF (type first) - big-endian console format
                // For PC little-endian: XX 00 00 00 FF FF FF FF
                if (isBigEndian && data[i] == 0x00 && data[i + 1] == 0x00 && data[i + 2] == 0x00
                    && data[i + 4] == 0xFF && data[i + 5] == 0xFF && data[i + 6] == 0xFF && data[i + 7] == 0xFF)
                {
                    int assetTypeInt = (int)Utilities.ReadUInt32AtOffset(i, _zone, isBigEndian: true);
                    bool isDefined =
                        (isCod4 && Enum.IsDefined(typeof(CoD4AssetType), assetTypeInt)) ||
                        (isCod5 && isXbox360 && Enum.IsDefined(typeof(CoD5AssetTypeXbox360), assetTypeInt)) ||
                        (isCod5 && !isXbox360 && !isPC && Enum.IsDefined(typeof(CoD5AssetTypePS3), assetTypeInt)) ||
                        (isMW2 && Enum.IsDefined(typeof(MW2AssetType), assetTypeInt));

                    Debug.WriteLine($"[DEBUG] Found Format A (BE) asset at 0x{i:X}: assetType=0x{assetTypeInt:X} defined={isDefined}");

                    if (isDefined)
                    {
                        if (!foundAnyAsset) assetPoolStart = i;

                        var record = new ZoneAssetRecord { AssetPoolRecordOffset = i };
                        if (isCod4) record.AssetType_COD4 = (CoD4AssetType)assetTypeInt;
                        else if (isCod5 && isXbox360) record.AssetType_COD5_Xbox360 = (CoD5AssetTypeXbox360)assetTypeInt;
                        else if (isCod5) record.AssetType_COD5 = (CoD5AssetTypePS3)assetTypeInt;
                        else if (isMW2) record.AssetType_MW2 = (MW2AssetType)assetTypeInt;
                        records.Add(record);

                        foundAnyAsset = true;
                        i += 8;
                        continue;
                    }
                }

                // Format A (PC little-endian): XX 00 00 00 FF FF FF FF (type first, little-endian)
                if (!isBigEndian && data[i + 1] == 0x00 && data[i + 2] == 0x00 && data[i + 3] == 0x00
                    && data[i + 4] == 0xFF && data[i + 5] == 0xFF && data[i + 6] == 0xFF && data[i + 7] == 0xFF)
                {
                    int assetTypeInt = (int)Utilities.ReadUInt32AtOffset(i, _zone, isBigEndian: false);
                    bool isDefined =
                        (isCod5 && isPC && Enum.IsDefined(typeof(CoD5AssetTypePC), assetTypeInt));

                    Debug.WriteLine($"[DEBUG] Found Format A (LE) asset at 0x{i:X}: assetType=0x{assetTypeInt:X} defined={isDefined}");

                    if (isDefined)
                    {
                        if (!foundAnyAsset) assetPoolStart = i;

                        var record = new ZoneAssetRecord { AssetPoolRecordOffset = i };
                        record.AssetType_COD5_PC = (CoD5AssetTypePC)assetTypeInt;
                        records.Add(record);

                        foundAnyAsset = true;
                        i += 8;
                        continue;
                    }
                }
                i++;
            }

            if (endOfPoolOffset == -1)
                endOfPoolOffset = i;

            _zone.AssetPoolStartOffset = assetPoolStart;
            _zone.AssetPoolEndOffset = endOfPoolOffset;

            Debug.WriteLine($"[MapZoneAssetsPool] Found {records.Count} records.");
            Debug.WriteLine($"[MapZoneAssetsPool] Start Offset: 0x{_zone.AssetPoolStartOffset:X}");
            Debug.WriteLine($"[MapZoneAssetsPool] End Offset: 0x{_zone.AssetPoolEndOffset:X}");

            return foundAnyAsset;
        }
    }
}
