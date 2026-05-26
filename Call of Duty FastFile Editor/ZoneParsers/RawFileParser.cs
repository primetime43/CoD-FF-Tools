using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Call_of_Duty_FastFile_Editor.GameDefinitions;
using Call_of_Duty_FastFile_Editor.Models;
using FastFileLib;

namespace Call_of_Duty_FastFile_Editor.ZoneParsers
{
    /// <summary>
    /// Thin shim over <see cref="FastFileLib.RawFileScanner"/>. The lib is the
    /// canonical parser — this just adapts <see cref="RawFileLocation"/> results
    /// into Editor-flavored <see cref="RawFileNode"/>s so the rest of the Editor
    /// (tree views, asset record processor, raw file service) doesn't have to
    /// change. Adding format support or fixing parsing bugs goes in the lib,
    /// not here.
    /// </summary>
    public static class RawFileParser
    {
        /// <summary>
        /// Finds the next rawfile entry at or after <paramref name="startOffset"/>.
        /// Used as a fallback during sequential walking when the structure-based
        /// parser loses its place (e.g. after an unsupported asset type).
        /// </summary>
        public static RawFileNode ExtractSingleRawFileNodeWithPattern(byte[] fileData, int startOffset, IGameDefinition gameDefinition)
        {
            var (gv, isPC) = MapGameDefinition(gameDefinition);
            var loc = RawFileScanner.FindRawFiles(fileData, gv, isPC)
                .FirstOrDefault(l => l.HeaderOffset >= startOffset);
            return loc is null ? null : ToRawFileNode(loc);
        }

        /// <summary>
        /// Bulk-scans a zone file for every rawfile entry it can find. Game/platform
        /// is auto-detected from the zone header; falls back to WaW/console when
        /// detection can't determine the format (matches the legacy parser's
        /// CoD4/WaW-era assumption — keeps existing callers working).
        /// </summary>
        public static List<RawFileNode> ExtractAllRawFilesSizeAndName(string zoneFilePath)
        {
            var gv = FastFileInfo.DetectGameFromZone(zoneFilePath);
            var isPC = FastFileInfo.IsZonePC(zoneFilePath);
            if (gv == GameVersion.Unknown) gv = GameVersion.WaW;
            return ExtractAllRawFilesSizeAndName(zoneFilePath, gv, isPC);
        }

        /// <summary>
        /// Bulk-scans a zone file with explicit game + platform. Prefer this overload
        /// when the caller already knows the FastFile context (e.g. an opened FF).
        /// </summary>
        public static List<RawFileNode> ExtractAllRawFilesSizeAndName(string zoneFilePath, GameVersion gameVersion, bool isPC)
        {
            byte[] zoneData = File.ReadAllBytes(zoneFilePath);
            return RawFileScanner.FindRawFiles(zoneData, gameVersion, isPC)
                .Select(ToRawFileNode)
                .ToList();
        }

        /// <summary>
        /// Adapts a lib <see cref="RawFileLocation"/> into an Editor
        /// <see cref="RawFileNode"/>. PatternIndexPosition is set to the name
        /// offset so it remains a stable per-entry identifier (the Editor uses
        /// it to match tree-view nodes back to rawfile entries).
        /// </summary>
        private static RawFileNode ToRawFileNode(RawFileLocation loc)
        {
            int onDiskSize = loc.CompressedSize > 0 ? loc.CompressedSize : loc.DataSize;
            // CoD4/WaW entries have a trailing null byte between adjacent entries; MW2
            // is packed tightly. RawFileEndPosition points just past whatever follows.
            int trailingNull = loc.HeaderSize == 12 ? 1 : 0;

            return new RawFileNode
            {
                PatternIndexPosition = loc.NameOffset,
                StartOfFileHeader = loc.HeaderOffset,
                HeaderSize = loc.HeaderSize,
                FileName = loc.Name,
                MaxSize = loc.DataSize,
                RawFileBytes = loc.Data,
                RawFileContent = Encoding.UTF8.GetString(loc.Data),
                RawFileEndPosition = loc.DataOffset + onDiskSize + trailingNull,
                IsCompressed = loc.WasCompressed,
                CompressedSize = loc.CompressedSize,
            };
        }

        private static (GameVersion gv, bool isPC) MapGameDefinition(IGameDefinition def)
        {
            // ShortName is "CoD4", "WaW", "MW2" (sometimes with a platform suffix like "(PC)").
            GameVersion gv =
                def.ShortName.StartsWith("MW2") ? GameVersion.MW2 :
                def.ShortName.StartsWith("WaW") || def.ShortName.StartsWith("CoD5") ? GameVersion.WaW :
                def.ShortName.StartsWith("CoD4") ? GameVersion.CoD4 :
                GameVersion.Unknown;
            return (gv, def.IsPC);
        }
    }
}
