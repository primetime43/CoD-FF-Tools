using System.Diagnostics;
using Call_of_Duty_FastFile_Editor.GameDefinitions;
using Call_of_Duty_FastFile_Editor.Models;
using Call_of_Duty_FastFile_Editor.Services;
using FastFileLib.GameDefinitions;

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

            // Start after tags, or at file start
            var tags = TagOperations.FindTags(_zone);
            i = (tags != null && tags.TagSectionEndOffset > 0) ? tags.TagSectionEndOffset : 0;

            bool foundAnyAsset = false;
            int assetPoolStart = -1;
            int endOfPoolOffset = -1;

            while (i <= fileLen - 8)
            {
                // End-of-pool marker: 8 x 0xFF.
                if (FastFileLib.ZoneAssetPool.IsEndMarker(data, i))
                {
                    Debug.WriteLine($"[DEBUG] END MARKER (all FF) at 0x{i:X}, breaking.");
                    endOfPoolOffset = i + 8; // Skip PAST the end marker
                    break;
                }

                // An asset record is an 8-byte [type word][FFFFFFFF] (or [FFFFFFFF][type word])
                // pair; the shared walker reads the type id in the platform's byte order and
                // tells us nothing about validity. We still gate on the per-(game,platform)
                // enum so noise between the header and the real pool is skipped — that gating
                // is what locates the pool start, so the byte-by-byte advance below is kept.
                if (FastFileLib.ZoneAssetPool.TryReadRecord(data, i, littleEndian: isPC, out uint typeWord, out _))
                {
                    int assetTypeInt = (int)typeWord;
                    bool isDefined =
                        (isCod4 && isPC && Enum.IsDefined(typeof(CoD4AssetTypePC), assetTypeInt)) ||
                        (isCod4 && isXbox360 && Enum.IsDefined(typeof(CoD4AssetTypeXbox360), assetTypeInt)) ||
                        (isCod4 && !isXbox360 && !isPC && Enum.IsDefined(typeof(CoD4AssetTypePS3), assetTypeInt)) ||
                        (isCod5 && isPC && Enum.IsDefined(typeof(CoD5AssetTypePC), assetTypeInt)) ||
                        (isCod5 && isXbox360 && Enum.IsDefined(typeof(CoD5AssetTypeXbox360), assetTypeInt)) ||
                        (isCod5 && !isXbox360 && !isPC && Enum.IsDefined(typeof(CoD5AssetTypePS3), assetTypeInt)) ||
                        (isMW2 && isPC && Enum.IsDefined(typeof(MW2AssetTypePC), assetTypeInt)) ||
                        (isMW2 && isXbox360 && Enum.IsDefined(typeof(MW2AssetTypeXbox360), assetTypeInt)) ||
                        (isMW2 && !isXbox360 && !isPC && Enum.IsDefined(typeof(MW2AssetTypePS3), assetTypeInt));

                    if (isDefined)
                    {
                        if (!foundAnyAsset) assetPoolStart = i;

                        var record = new ZoneAssetRecord { AssetPoolRecordOffset = i };
                        if (isCod4 && isPC) record.AssetType_COD4_PC = (CoD4AssetTypePC)assetTypeInt;
                        else if (isCod4 && isXbox360) record.AssetType_COD4_Xbox360 = (CoD4AssetTypeXbox360)assetTypeInt;
                        else if (isCod4) record.AssetType_COD4 = (CoD4AssetTypePS3)assetTypeInt;
                        else if (isCod5 && isPC) record.AssetType_COD5_PC = (CoD5AssetTypePC)assetTypeInt;
                        else if (isCod5 && isXbox360) record.AssetType_COD5_Xbox360 = (CoD5AssetTypeXbox360)assetTypeInt;
                        else if (isCod5) record.AssetType_COD5 = (CoD5AssetTypePS3)assetTypeInt;
                        else if (isMW2 && isPC) record.AssetType_MW2_PC = (MW2AssetTypePC)assetTypeInt;
                        else if (isMW2 && isXbox360) record.AssetType_MW2_Xbox360 = (MW2AssetTypeXbox360)assetTypeInt;
                        else if (isMW2) record.AssetType_MW2 = (MW2AssetTypePS3)assetTypeInt;
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
