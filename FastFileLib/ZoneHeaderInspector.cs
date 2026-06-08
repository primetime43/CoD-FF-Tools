using FastFileLib.GameDefinitions;

namespace FastFileLib
{
    /// <summary>
    /// Severity used to colour Zone Header rows and to drive the overall health banner.
    /// </summary>
    public enum ZoneFieldSeverity
    {
        /// <summary>A non-data section divider row.</summary>
        Section,
        /// <summary>Informational field with no pass/fail meaning.</summary>
        Normal,
        /// <summary>A validated field whose value is correct.</summary>
        Good,
        /// <summary>Suspicious but not guaranteed fatal.</summary>
        Warning,
        /// <summary>Wrong value that can black-screen / fail to load.</summary>
        Critical
    }

    /// <summary>A single rendered row in a zone header report.</summary>
    public sealed class ZoneHeaderField
    {
        public string Name { get; init; } = "";
        public string Decimal { get; init; } = "";
        public string Hex { get; init; } = "";
        public string Offset { get; init; } = "";
        public string Meaning { get; init; } = "";
        public string Status { get; init; } = "";
        public ZoneFieldSeverity Severity { get; init; }
    }

    /// <summary>The full result of inspecting a zone header: a summary banner plus annotated rows.</summary>
    public sealed class ZoneHeaderReport
    {
        public string Headline { get; set; } = "";
        public string Health { get; set; } = "";
        public ZoneFieldSeverity OverallSeverity { get; set; } = ZoneFieldSeverity.Normal;
        public List<ZoneHeaderField> Fields { get; } = new();
    }

    /// <summary>
    /// Turns a loaded zone's XFile / XAssetList header into a human-readable, validated report.
    /// Shared by the editor's Zone Header tab, the File Report form, and the CLI so the same
    /// validation rules apply everywhere. The validation focuses on the fields that black-screen
    /// the game when wrong: the MemAlloc magic values (BlockSizeTemp / BlockSizeVertex), the asset
    /// count, and the asset/script pointer placeholders. Everything else is informational context.
    ///
    /// Header fields are read straight from the zone bytes using <see cref="FastFileConstants"/>
    /// offsets, so callers only need the decompressed zone and its game/platform flags.
    /// </summary>
    public static class ZoneHeaderInspector
    {
        private const uint NullPointer = 0xFFFFFFFF;

        /// <param name="zoneData">Decompressed .zone bytes.</param>
        /// <param name="zoneFileLength">Length of the decompressed zone (for the size banner / sanity check). Pass <c>zoneData.LongLength</c> when unknown.</param>
        /// <param name="parsedAssetCount">
        /// Independently parsed asset count, if the caller has walked the pool. When supplied, the
        /// header's AssetCount is validated against it (a mismatch is the classic infinite-load bug).
        /// When null, AssetCount is shown as declared-only (not independently verified).
        /// </param>
        public static ZoneHeaderReport Build(
            byte[]? zoneData,
            GameVersion gv,
            bool isXbox360,
            bool isPC,
            bool isWii,
            bool isGhosts,
            long zoneFileLength,
            int? parsedAssetCount = null)
        {
            var report = new ZoneHeaderReport();

            int hdrSize = FastFileConstants.GetZoneHeaderSize(gv, isXbox360, isPC, isWii);
            string gameName = gv switch
            {
                GameVersion.CoD4 => "Call of Duty 4",
                GameVersion.WaW => "World at War",
                GameVersion.MW2 => "Modern Warfare 2",
                GameVersion.Ghosts => "Ghosts",
                _ => "Unknown game"
            };
            string platform = isPC ? "PC" : isWii ? "Wii" : isGhosts ? "PS3" : isXbox360 ? "Xbox 360" : "PS3";
            string endian = isPC ? "little-endian" : "big-endian";

            string assetsLabel = parsedAssetCount.HasValue ? $"{parsedAssetCount.Value:N0} assets  |  " : "";
            report.Headline =
                $"{gameName}  |  {platform} ({endian})  |  {hdrSize}-byte header  |  " +
                $"{assetsLabel}zone {FastFileInfo.FormatFileSize(zoneFileLength)}";

            // Ghosts uses a different XFile layout; its header fields aren't at these offsets.
            if (isGhosts || zoneData == null || zoneData.Length < hdrSize)
            {
                report.Health = isGhosts
                    ? "Ghosts (IW6) uses a different XFile layout - individual header fields aren't parsed."
                    : "Zone is too small / truncated to contain a full header.";
                report.OverallSeverity = ZoneFieldSeverity.Normal;
                return report;
            }

            uint Read(int offset) =>
                (offset >= 0 && zoneData.Length >= offset + 4)
                    ? FastFileConstants.ReadUInt32(zoneData, offset, littleEndian: isPC)
                    : 0;

            int scriptCountOff = FastFileConstants.GetScriptStringCountOffset(gv, isXbox360, isPC, isWii);
            int assetCountOff = FastFileConstants.GetAssetCountOffset(gv, isXbox360, isPC, isWii);

            int critical = 0, warnings = 0;

            void Add(string name, uint value, int offset, string meaning, string status, ZoneFieldSeverity sev)
            {
                if (sev == ZoneFieldSeverity.Critical) critical++;
                else if (sev == ZoneFieldSeverity.Warning) warnings++;
                report.Fields.Add(new ZoneHeaderField
                {
                    Name = name,
                    Decimal = value.ToString("N0"),
                    Hex = "0x" + value.ToString("X8"),
                    Offset = $"0x{offset:X2}",
                    Meaning = meaning,
                    Status = status,
                    Severity = sev
                });
            }

            void Section(string title) =>
                report.Fields.Add(new ZoneHeaderField { Name = title, Severity = ZoneFieldSeverity.Section });

            var (expTemp, expVertex, fixedMemAlloc) = GetExpectedMemAlloc(gv, isXbox360, isPC, isWii);

            // ---- Memory allocation (XFile header) ----
            Section("MEMORY ALLOCATION  (XFile header)");

            uint zoneSize = Read(FastFileConstants.ZoneSizeOffset);
            ZoneFieldSeverity zoneSev;
            string zoneStatus;
            if (zoneSize == 0) { zoneSev = ZoneFieldSeverity.Critical; zoneStatus = "zero - zone is empty/corrupt"; }
            else if (zoneFileLength > 0 && zoneSize > zoneFileLength) { zoneSev = ZoneFieldSeverity.Warning; zoneStatus = $"larger than the {FastFileInfo.FormatFileSize(zoneFileLength)} zone on disk"; }
            else { zoneSev = ZoneFieldSeverity.Normal; zoneStatus = $"zone on disk is {FastFileInfo.FormatFileSize(zoneFileLength)}"; }
            Add("ZoneSize", zoneSize, FastFileConstants.ZoneSizeOffset, "Decompressed zone payload size.", zoneStatus, zoneSev);

            uint ext = Read(FastFileConstants.ExternalSizeOffset);
            Add("ExternalSize", ext, FastFileConstants.ExternalSizeOffset, "External / streamed data size (normally 0).",
                ext == 0 ? "" : "non-zero - unusual for this game", ext == 0 ? ZoneFieldSeverity.Normal : ZoneFieldSeverity.Warning);

            uint temp = Read(FastFileConstants.BlockSizeTempOffset);
            AddMemAlloc(Add, "BlockSizeTemp", temp, FastFileConstants.BlockSizeTempOffset, expTemp, fixedMemAlloc,
                "Temp memory pool size (MemAlloc1).");

            Add("BlockSizePhysical", Read(FastFileConstants.BlockSizePhysicalOffset), FastFileConstants.BlockSizePhysicalOffset, "Physical memory pool size (computed per zone).", "", ZoneFieldSeverity.Normal);
            Add("BlockSizeRuntime", Read(FastFileConstants.BlockSizeRuntimeOffset), FastFileConstants.BlockSizeRuntimeOffset, "Runtime memory pool size (computed per zone).", "", ZoneFieldSeverity.Normal);
            Add("BlockSizeVirtual", Read(FastFileConstants.BlockSizeVirtualOffset), FastFileConstants.BlockSizeVirtualOffset, "Virtual memory pool size (computed per zone).", "", ZoneFieldSeverity.Normal);
            Add("BlockSizeLarge", Read(FastFileConstants.BlockSizeLargeOffset), FastFileConstants.BlockSizeLargeOffset, "Large-allocation pool size (computed per zone).", "", ZoneFieldSeverity.Normal);
            Add("BlockSizeCallback", Read(FastFileConstants.BlockSizeCallbackOffset), FastFileConstants.BlockSizeCallbackOffset, "Callback / streamed pool size (computed per zone).", "", ZoneFieldSeverity.Normal);

            // BlockSizeVertex = MemAlloc2 - crash-critical (absent in MW2 Xbox 360's 48-byte layout).
            if (gv == GameVersion.MW2 && isXbox360)
            {
                Add("BlockSizeVertex", Read(FastFileConstants.BlockSizeVertexOffset), FastFileConstants.BlockSizeVertexOffset,
                    "Not present in MW2 Xbox 360's 48-byte layout (this slot overlaps the asset list).", "n/a", ZoneFieldSeverity.Normal);
            }
            else
            {
                uint vertex = Read(FastFileConstants.BlockSizeVertexOffset);
                AddMemAlloc(Add, "BlockSizeVertex", vertex, FastFileConstants.BlockSizeVertexOffset, expVertex, fixedMemAlloc,
                    "Vertex memory pool size (MemAlloc2).");
            }

            // ---- Asset list (XAssetList) ----
            Section("ASSET LIST  (XAssetList)");

            uint scriptCount = Read(scriptCountOff);
            Add("ScriptStringCount", scriptCount, scriptCountOff, "Number of script / tag strings declared.", "", ZoneFieldSeverity.Normal);

            uint scriptPtr = Read(scriptCountOff + 4);
            bool scriptPtrOk = scriptPtr == NullPointer || (scriptCount == 0 && scriptPtr == 0);
            Add("ScriptStringsPtr", scriptPtr, scriptCountOff + 4, "Script-strings pointer placeholder.",
                scriptPtrOk ? "valid placeholder" : "expected 0xFFFFFFFF",
                scriptPtrOk ? ZoneFieldSeverity.Good : ZoneFieldSeverity.Warning);

            uint assetCount = Read(assetCountOff);
            if (parsedAssetCount.HasValue)
            {
                bool assetOk = assetCount == (uint)parsedAssetCount.Value;
                Add("AssetCount", assetCount, assetCountOff,
                    "Total assets in the pool - must match the entries that follow.",
                    assetOk ? $"matches {parsedAssetCount.Value:N0} parsed assets"
                            : $"parser found {parsedAssetCount.Value:N0} - a mismatch causes infinite load / black screen",
                    assetOk ? ZoneFieldSeverity.Good : ZoneFieldSeverity.Critical);
            }
            else
            {
                Add("AssetCount", assetCount, assetCountOff,
                    "Total assets in the pool - must match the entries that follow.",
                    "declared in header (not independently verified)", ZoneFieldSeverity.Normal);
            }

            uint assetsPtr = Read(assetCountOff + 4);
            bool assetsPtrOk = assetsPtr == NullPointer;
            Add("AssetsPtr", assetsPtr, assetCountOff + 4, "Asset-list pointer placeholder.",
                assetsPtrOk ? "valid placeholder" : "expected 0xFFFFFFFF",
                assetsPtrOk ? ZoneFieldSeverity.Good : ZoneFieldSeverity.Warning);

            // ---- Health banner ----
            // Health carries no status glyph/marker - each renderer prefixes its own
            // (the editor uses ✓/⚠/✕, the CLI uses [OK]/[WARN]/[CRIT]).
            if (critical > 0)
            {
                report.OverallSeverity = ZoneFieldSeverity.Critical;
                report.Health = $"{critical} critical issue(s) - this file may black-screen or fail to load. See the highlighted rows.";
            }
            else if (warnings > 0)
            {
                report.OverallSeverity = ZoneFieldSeverity.Warning;
                report.Health = $"{warnings} warning(s) - review the highlighted rows.";
            }
            else
            {
                report.OverallSeverity = ZoneFieldSeverity.Good;
                report.Health = "All validated header fields look correct.";
            }

            return report;
        }

        private static void AddMemAlloc(
            Action<string, uint, int, string, string, ZoneFieldSeverity> add,
            string name, uint value, int offset, uint? expected, bool fixedMemAlloc, string meaning)
        {
            if (!fixedMemAlloc || expected == null)
            {
                // MW2 / PC / Wii allocate these per zone, so there is no single correct value to check against.
                add(name, value, offset, meaning + " Computed per zone — not a fixed value, so not validated.", "per-zone (not validated)", ZoneFieldSeverity.Normal);
                return;
            }

            bool ok = value == expected.Value;
            add(name, value, offset, meaning,
                ok ? $"matches the expected 0x{expected.Value:X} for this game/platform"
                   : $"expected 0x{expected.Value:X} - a wrong value can black-screen the game",
                ok ? ZoneFieldSeverity.Good : ZoneFieldSeverity.Critical);
        }

        /// <summary>
        /// Returns the known-good MemAlloc1/MemAlloc2 magic values for the fixed-value platforms.
        /// PC and Wii compute these per zone, so <c>fixedMemAlloc</c> is false there.
        /// </summary>
        private static (uint? temp, uint? vertex, bool fixedMemAlloc) GetExpectedMemAlloc(
            GameVersion gv, bool isXbox360, bool isPC, bool isWii)
        {
            if (isPC || isWii)
                return (null, null, false);

            return gv switch
            {
                GameVersion.CoD4 => (CoD4Definition.MemAlloc1Value, CoD4Definition.MemAlloc2Value, true),
                GameVersion.WaW => isXbox360
                    ? (CoD5Definition.Xbox360MemAlloc1Value, CoD5Definition.Xbox360MemAlloc2Value, true)
                    : (CoD5Definition.MemAlloc1Value, CoD5Definition.MemAlloc2Value, true),
                // MW2 (IW4) computes these per zone — they scale with the zone's content (patch zones
                // happen to be 0x3B4/0x1000, but retail map/load zones are larger and load fine). There
                // is no single fixed value to validate against, so don't flag them.
                _ => (null, null, false)
            };
        }
    }
}
