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

            int scan = scanStart < 0 ? 0 : scanStart;
            var headers  = GhostsZoneLayout.LocateAllHeaders(zoneData, scan);
            var luaFiles = GhostsZoneLayout.LocateAllLuaFiles(zoneData, scan);

            // Base zones: no pool to pair against. Surface every located header
            // (wrapped + luafile) as a rawfile-like node so the editor's rawfile
            // tree still populates. Per docs/Ghosts_FastFile_Format.md, "almost
            // all" wrapped assets in base zones are rawfiles.
            if (records == null || records.Count == 0)
            {
                foreach (var h in headers)
                    result.RawFileNodes.Add(BuildWrappedNode(zoneData, h));
                foreach (var lua in luaFiles)
                    result.RawFileNodes.Add(BuildLuaFileNode(zoneData, lua));
                result.UnpairedHeaders = headers.Count + luaFiles.Count;
                Debug.WriteLine($"[GhostsAssetWalker] No pool — surfaced {headers.Count} wrapped + {luaFiles.Count} luafile headers as rawfiles");
                return result;
            }

            // Pair wrapped types (rawfile/scriptfile/mptype/aitype) with the
            // located zlib-wrapped headers.
            var poolDtos = new List<GhostsPoolEntry>(records.Count);
            foreach (var r in records)
            {
                poolDtos.Add(new GhostsPoolEntry(
                    recordOffset: r.AssetPoolRecordOffset,
                    type: r.AssetType_Ghosts,
                    pointerKind: GhostsPointerKind.Placeholder));
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
                    result.RawFileNodes.Add(BuildWrappedNode(zoneData, h));
            }

            // Pair luafile pool entries with located luafiles — separate pass
            // because the on-disk format is different (flat 16-byte header, no
            // zlib wrapper). Same positional rule: i-th luafile pool entry ↔
            // i-th located luafile body.
            int luaIdx = 0;
            int luaPaired = 0;
            for (int i = 0; i < records.Count && luaIdx < luaFiles.Count; i++)
            {
                if (records[i].AssetType_Ghosts != GhostsAssetTypePS3.luafile) continue;
                var lua = luaFiles[luaIdx++];
                var record = records[i];

                record.HeaderStartOffset      = lua.HeaderOffset;
                record.HeaderEndOffset        = lua.BodyStart;
                record.AssetDataStartPosition = lua.BodyStart;
                record.AssetDataEndOffset     = lua.BodyEnd;
                record.AssetRecordEndOffset   = lua.BodyEnd;
                record.Name                   = lua.Name;
                record.Size                   = lua.ByteCodeLen;
                record.AdditionalData         = "Ghosts luafile (flat header)";
                records[i] = record;
                luaPaired++;

                result.RawFileNodes.Add(BuildLuaFileNode(zoneData, lua));
            }

            result.Resolved        = pairing.PairedCount + luaPaired;
            result.Unresolved      = Math.Max(0, records.Count - result.Resolved);
            result.UnpairedHeaders = pairing.UnpairedHeaders + Math.Max(0, luaFiles.Count - luaIdx);

            Debug.WriteLine($"[GhostsAssetWalker] Paired {pairing.PairedCount} wrapped + {luaPaired} luafile pool entries; {result.RawFileNodes.Count} rawfile-like nodes");
            return result;
        }

        private static RawFileNode BuildWrappedNode(byte[] zone, GhostsAssetHeader h)
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

        private static RawFileNode BuildLuaFileNode(byte[] zone, GhostsLuaFile lua)
        {
            byte[] body = new byte[lua.ByteCodeLen];
            Buffer.BlockCopy(zone, lua.BodyStart, body, 0, lua.ByteCodeLen);
            // IW6 HavokScript bytecode. The original Lua source isn't shipped —
            // IW6 ships only compiled bytecode in a non-standard custom format
            // (format byte 0x0D) whose chunk layout isn't fully reverse-engineered.
            // Show the format-agnostic extracted-strings summary so the user
            // still gets useful signal (menu/widget/function names) without
            // depending on a working disassembler.
            //
            // FastFileLib's IW6LuaBytecodeReader / IW6LuaDisassembler hold the
            // partial format work for future investigation; they don't drive
            // the viewer until the format is mapped end-to-end.
            var summary = LuaBytecodeInspector.Inspect(body);
            string content = LuaBytecodeInspector.FormatSummaryText(lua.Name, summary);
            return new RawFileNode
            {
                FileName          = lua.Name,
                StartOfFileHeader = lua.HeaderOffset,
                HeaderSize        = lua.BodyStart - lua.HeaderOffset,
                MaxSize           = lua.ByteCodeLen,
                CompressedSize    = lua.ByteCodeLen,
                IsCompressed      = false,
                RawFileBytes      = body,
                RawFileContent    = content,
                AdditionalData    = "Ghosts luafile (Lua 5.1 HavokScript bytecode)",
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
