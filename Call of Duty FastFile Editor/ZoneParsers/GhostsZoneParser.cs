using Call_of_Duty_FastFile_Editor.Models;
using FastFileLib.GameDefinitions;
using System.Diagnostics;

namespace Call_of_Duty_FastFile_Editor.ZoneParsers
{
    /// <summary>
    /// Pool-only zone parser for Call of Duty: Ghosts (IW6) PS3 zones.
    ///
    /// Unlike CoD4/WaW/MW2, the Ghosts XFile header doesn't sit at a fixed
    /// 52-byte offset and the AssetCount field isn't at a known location yet,
    /// so this parser doesn't try to read those header fields. Instead it walks
    /// the asset pool by pattern, starting at offset 0x40 and reading 8-byte
    /// `[FFFFFFFF][type BE u32]` entries until the pattern breaks.
    ///
    /// Per-asset content (rawfile bodies, weapon structs, stringtables, etc.)
    /// is NOT parsed here — only the pool listing. The asset-pool tab in the
    /// editor will populate from this parser's output; the per-asset trees
    /// (raw files, localized entries, etc.) stay empty for Ghosts.
    /// </summary>
    public class GhostsZoneParser
    {
        // The asset pool sits after the XFile header. Patch FFs put it at 0x40; base
        // FFs (e.g. common.ff) have a longer XFile header — search the first 0x200
        // bytes for the start of the FFFFFFFF+type pattern instead of assuming.
        private const int PoolSearchWindow = 0x200;

        // IW6 PS3 enum tops out at 0x35; any byte at or above this can't be a real
        // asset type and signals the end of the pool / start of asset content.
        private const byte MaxValidTypeId = 0x36;

        private readonly ZoneFile _zone;
        private readonly byte[] _data;

        public GhostsZoneParser(ZoneFile zone)
        {
            _zone = zone;
            _data = zone.Data;
        }

        /// <summary>
        /// Walks the asset pool and populates <see cref="ZoneFile.ZoneFileAssets"/>
        /// with one <see cref="ZoneAssetRecord"/> per entry. Records' AssetType_Ghosts
        /// field is populated; per-asset content offsets (HeaderStartOffset etc.)
        /// stay zero since content parsing is not implemented.
        /// </summary>
        public bool Parse()
        {
            int poolStart = FindAssetPoolStart();
            if (poolStart < 0)
            {
                Debug.WriteLine("[GhostsZoneParser] Could not locate asset pool");
                return false;
            }

            var records = new List<ZoneAssetRecord>();
            int offset = poolStart;
            while (offset + 8 <= _data.Length && IsPoolEntry(offset))
            {
                records.Add(new ZoneAssetRecord
                {
                    AssetPoolRecordOffset = offset,
                    AssetType_Ghosts = (GhostsAssetTypePS3)_data[offset + 7],
                });
                offset += 8;
            }

            if (records.Count == 0) return false;

            _zone.ZoneFileAssets.ZoneAssetRecords = records;
            _zone.AssetPoolStartOffset = poolStart;
            _zone.AssetPoolEndOffset = offset;

            Debug.WriteLine($"[GhostsZoneParser] Pool 0x{poolStart:X}..0x{offset:X}: {records.Count} entries");
            return true;
        }

        /// <summary>
        /// Looks for the longest contiguous run of valid pool entries within the
        /// first <see cref="PoolSearchWindow"/> bytes of the zone. Returns the
        /// offset of that run, or -1 if no plausible pool can be located.
        /// </summary>
        private int FindAssetPoolStart()
        {
            int bestStart = -1;
            int bestRun = 0;
            int limit = Math.Min(_data.Length - 8, PoolSearchWindow);
            for (int candidate = 0x20; candidate <= limit; candidate += 4)
            {
                int run = 0;
                int p = candidate;
                while (p + 8 <= _data.Length && IsPoolEntry(p))
                {
                    run++;
                    p += 8;
                }
                if (run > bestRun)
                {
                    bestRun = run;
                    bestStart = candidate;
                }
            }
            return bestRun > 0 ? bestStart : -1;
        }

        private bool IsPoolEntry(int offset)
        {
            // [FF FF FF FF] ptr placeholder
            if (_data[offset] != 0xFF || _data[offset + 1] != 0xFF
                || _data[offset + 2] != 0xFF || _data[offset + 3] != 0xFF)
                return false;
            // [type BE u32]: high 3 bytes zero, low byte ≤ MaxValidTypeId
            if (_data[offset + 4] != 0x00 || _data[offset + 5] != 0x00 || _data[offset + 6] != 0x00)
                return false;
            return _data[offset + 7] <= MaxValidTypeId;
        }
    }
}
