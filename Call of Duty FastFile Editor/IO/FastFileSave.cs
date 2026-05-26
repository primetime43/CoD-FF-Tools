using System;
using Call_of_Duty_FastFile_Editor.Models;
using FastFileLib;

namespace Call_of_Duty_FastFile_Editor.IO
{
    /// <summary>
    /// Editor-side thin shim over <see cref="FastFileLib.FastFileSaveService"/>.
    /// Pulls game + platform + signed/original info out of the editor's
    /// <see cref="FastFile"/> model and forwards to the lib. All actual
    /// compression logic lives in the lib so the CLI and editor share one
    /// canonical save path. Named without the "Service" suffix to avoid a
    /// collision with the lib type when both namespaces are imported.
    /// </summary>
    public static class FastFileSave
    {
        /// <summary>
        /// Saves the zone at <paramref name="zoneFilePath"/> back to a FastFile
        /// at <paramref name="ffFilePath"/>, deriving game + platform + signed
        /// state from the editor's <see cref="FastFile"/> model. Use when an
        /// FF is currently open and we're persisting in-memory edits.
        /// </summary>
        public static void Save(string zoneFilePath, string ffFilePath, FastFile context)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));

            FastFileLib.FastFileSaveService.Save(
                zoneFilePath,
                ffFilePath,
                ResolveGameVersion(context),
                ResolvePlatform(context),
                context.IsSigned,
                context.FfFilePath);
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
    }
}
