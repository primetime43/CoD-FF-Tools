using Call_of_Duty_FastFile_Editor.Models;
using FastFileLib;
using System.Diagnostics;

namespace Call_of_Duty_FastFile_Editor.ZoneParsers
{
    /// <summary>
    /// Thin shim around <see cref="FastFileLib.GhostsZoneLayout"/>: translates
    /// the library's pure DTO output into the editor's
    /// <see cref="ZoneAssetRecord"/> shape and writes it onto <see cref="ZoneFile"/>.
    /// All Ghosts pool-layout knowledge lives in the lib so the CLI / tests / future
    /// tools can share it.
    /// </summary>
    public class GhostsZoneParser
    {
        private readonly ZoneFile _zone;

        public GhostsZoneParser(ZoneFile zone)
        {
            _zone = zone;
        }

        public bool Parse()
        {
            var entries = GhostsZoneLayout.ParsePool(_zone.Data, out int poolStart, out int poolEnd);
            if (entries.Count == 0)
            {
                Debug.WriteLine("[GhostsZoneParser] Could not locate asset pool");
                return false;
            }

            var records = new List<ZoneAssetRecord>(entries.Count);
            foreach (var e in entries)
            {
                records.Add(new ZoneAssetRecord
                {
                    AssetPoolRecordOffset = e.RecordOffset,
                    AssetType_Ghosts = e.Type,
                });
            }

            _zone.ZoneFileAssets.ZoneAssetRecords = records;
            _zone.AssetPoolStartOffset = poolStart;
            _zone.AssetPoolEndOffset = poolEnd;

            var (tagCount, assetCount) = GhostsZoneLayout.ReadHeaderCounts(_zone.Data);
            Debug.WriteLine($"[GhostsZoneParser] Pool 0x{poolStart:X}..0x{poolEnd:X}: {records.Count} entries (header tagCount={tagCount}, assetCount={assetCount})");
            return true;
        }
    }
}
