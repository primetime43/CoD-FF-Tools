using System;
using System.IO;
using Call_of_Duty_FastFile_Editor.Models;
using FastFileLib;

namespace Call_of_Duty_FastFile_Editor.IO
{
    /// <summary>
    /// Single canonical "save zone to FastFile" path for the editor. Every save flow
    /// — File > Save, File > Save As, post-inject save, post-resize save, Tools >
    /// Compress Zone to FF — routes through here. Previously each callsite picked
    /// its own platform/game arguments and the "Compress Zone to FF" flow defaulted
    /// to PS3, which corrupted PC ui.ff (issue: "version -2097086464, expecting 387").
    /// Consolidating in one place keeps platform/endianness decisions in one spot;
    /// any future bug here fixes every save path at once.
    /// </summary>
    /// <remarks>
    /// Lives in the editor for now. Long-term this should move into FastFileLib so
    /// the CLI's `compress` command can use the same entry point.
    /// </remarks>
    public static class FastFileSaveService
    {
        /// <summary>
        /// Saves the zone at <paramref name="zoneFilePath"/> back to a FastFile at
        /// <paramref name="ffFilePath"/>, using game + platform info from
        /// <paramref name="context"/>. This is what the regular "I'm editing an opened
        /// FastFile" callsites use — the context's IsPC/IsXbox360/IsWii were set by
        /// FastFileInfo when the .ff was opened, so endianness is whatever the source
        /// FF actually is.
        /// </summary>
        public static void Save(string zoneFilePath, string ffFilePath, FastFile context)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));

            string platform = ResolvePlatform(context);
            GameVersion gameVersion = ResolveGameVersion(context);

            LogSave("Save(context)", ffFilePath, gameVersion, platform, context.IsSigned);

            FastFileProcessor.Recompress(
                zoneFilePath,
                ffFilePath,
                gameVersion,
                platform,
                context.IsSigned,
                context.FfFilePath);
        }

        /// <summary>
        /// Saves a zone byte array to a FastFile, detecting game + platform from the
        /// zone bytes themselves. Use this for "Compress Zone to FF" where there's no
        /// opened FastFile context to carry the platform info. Detection uses
        /// FastFileInfo's ZoneSize-plausibility heuristic so PC zones with non-magic
        /// MemAlloc (e.g. ui.zone with BlockSizeTemp=0x01E0) are still recognized.
        /// </summary>
        /// <param name="fallbackGame">Used if zone-byte detection returns Unknown.</param>
        public static void SaveDetectingFromZone(byte[] zoneData, string ffFilePath, GameVersion fallbackGame = GameVersion.WaW)
        {
            if (zoneData is null) throw new ArgumentNullException(nameof(zoneData));

            GameVersion gameVersion = FastFileInfo.DetectGameFromZoneData(zoneData);
            if (gameVersion == GameVersion.Unknown) gameVersion = fallbackGame;
            string platform = FastFileInfo.IsZoneDataPC(zoneData) ? "PC" : "PS3";

            LogSave("SaveDetectingFromZone", ffFilePath, gameVersion, platform, signed: false);

            new Compiler(gameVersion, platform).CompileToFile(zoneData, ffFilePath, saveZone: false);
        }

        private static string ResolvePlatform(FastFile context) =>
            context.IsPC ? "PC" :
            context.IsWii ? "Wii" :
            context.IsXbox360 ? "Xbox360" :
            "PS3";

        private static GameVersion ResolveGameVersion(FastFile context) =>
            context.IsCod4File ? GameVersion.CoD4 :
            context.IsCod5File ? GameVersion.WaW :
            context.IsMW2File ? GameVersion.MW2 :
            throw new InvalidOperationException($"FastFile '{context.FastFileName}' has no recognized game.");

        private static void LogSave(string source, string ffFilePath, GameVersion game, string platform, bool signed)
        {
            FastFileLib.Logging.LogService.Info("Save",
                $"src={source} file='{Path.GetFileName(ffFilePath)}' game={game} platform={platform} signed={signed}");
        }
    }
}
