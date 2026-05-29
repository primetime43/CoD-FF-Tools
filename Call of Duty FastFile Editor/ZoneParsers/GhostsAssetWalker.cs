using Call_of_Duty_FastFile_Editor.Models;
using FastFileLib;
using FastFileLib.GameDefinitions;
using System.Diagnostics;
using System.Text;

namespace Call_of_Duty_FastFile_Editor.ZoneParsers
{
    /// <summary>
    /// Thin shim around <see cref="FastFileLib.GhostsZoneLayout"/>: drives the
    /// library's header scan + pool pairing, then translates the results into
    /// the editor's models — mutates each <see cref="ZoneAssetRecord"/> with
    /// resolved offsets/names and emits <see cref="RawFileNode"/>s for the
    /// rawfile-typed entries.
    /// </summary>
    public static class GhostsAssetWalker
    {
        public sealed class WalkResult
        {
            public List<RawFileNode> RawFileNodes { get; } = new();
            public int Resolved { get; set; }
            public int Unresolved { get; set; }
            public int UnpairedHeaders { get; set; }
        }

        public static WalkResult Walk(byte[] zoneData, List<ZoneAssetRecord>? records, int scanStart)
        {
            var result = new WalkResult();
            if (zoneData == null || zoneData.Length == 0) return result;

            var headers = GhostsZoneLayout.LocateAllHeaders(zoneData, scanStart < 0 ? 0 : scanStart);

            // Base zones: no pool to pair against. Surface every located header
            // as a rawfile (per docs/Ghosts_FastFile_Format.md, "almost all"
            // wrapped assets in base zones are rawfiles).
            if (records == null || records.Count == 0)
            {
                foreach (var h in headers)
                    result.RawFileNodes.Add(BuildRawFileNode(zoneData, h));
                result.UnpairedHeaders = headers.Count;
                Debug.WriteLine($"[GhostsAssetWalker] No pool — surfaced {headers.Count} located headers as rawfiles");
                return result;
            }

            // Lift to library DTOs for pairing.
            var poolDtos = new List<GhostsPoolEntry>(records.Count);
            foreach (var r in records)
            {
                poolDtos.Add(new GhostsPoolEntry(
                    recordOffset: r.AssetPoolRecordOffset,
                    type: r.AssetType_Ghosts,
                    pointerKind: GhostsPointerKind.Placeholder)); // pointer kind isn't used during pairing
            }

            var pairing = GhostsZoneLayout.PairPoolWithHeaders(poolDtos, headers);

            for (int i = 0; i < records.Count; i++)
            {
                int hIdx = pairing.PoolToHeader[i];
                if (hIdx < 0) continue;
                var h = pairing.Headers[hIdx];
                var record = records[i];

                record.HeaderStartOffset      = h.HeaderOffset;
                record.HeaderEndOffset        = h.BodyStart;
                record.AssetDataStartPosition = h.BodyStart;
                record.AssetDataEndOffset     = h.BodyEnd;
                record.AssetRecordEndOffset   = h.BodyEnd;
                record.Name                   = h.Name;
                record.Size                   = h.DecompressedLen;
                record.AdditionalData         = $"Ghosts pool-walked ({(h.IsLong ? "long" : "short")} header)";
                records[i] = record;

                if (record.AssetType_Ghosts == GhostsAssetTypePS3.rawfile)
                    result.RawFileNodes.Add(BuildRawFileNode(zoneData, h));
            }

            result.Resolved        = pairing.PairedCount;
            result.Unresolved      = Math.Max(0, records.Count - pairing.PairedCount);
            result.UnpairedHeaders = pairing.UnpairedHeaders;

            Debug.WriteLine($"[GhostsAssetWalker] Paired {result.Resolved}/{records.Count} pool entries with {headers.Count} located headers ({result.UnpairedHeaders} unpaired); {result.RawFileNodes.Count} rawfiles");
            return result;
        }

        private static RawFileNode BuildRawFileNode(byte[] zone, GhostsAssetHeader h)
        {
            byte[] body = new byte[h.DecompressedLen];
            Buffer.BlockCopy(zone, h.BodyStart, body, 0, h.DecompressedLen);
            bool isText = LooksTextual(body);
            return new RawFileNode
            {
                FileName          = h.Name,
                StartOfFileHeader = h.HeaderOffset,
                HeaderSize        = h.BodyStart - h.HeaderOffset,
                MaxSize           = h.DecompressedLen,
                CompressedSize    = h.CompressedLen,
                IsCompressed      = false,
                RawFileBytes      = body,
                RawFileContent    = isText ? Encoding.UTF8.GetString(body) : null,
                AdditionalData    = $"Ghosts pre-scan ({(h.IsLong ? "long" : "short")} header)",
            };
        }

        private static bool LooksTextual(byte[] body)
        {
            if (body.Length == 0) return true;
            int printable = 0;
            for (int i = 0; i < body.Length; i++)
            {
                byte b = body[i];
                if (b == 0x09 || b == 0x0A || b == 0x0D || (b >= 0x20 && b < 0x7F))
                    printable++;
            }
            return printable * 10 >= body.Length * 9;
        }
    }
}
