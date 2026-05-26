using System;
using System.IO;
using FastFileLib.Logging;

namespace FastFileLib;

/// <summary>
/// Canonical "save zone to FastFile" entry point shared by every GUI and the CLI.
/// All callers route through here so platform-aware decisions (LE/BE version
/// bytes, single-zlib vs 64KB-block compression, MW2 PC's quirky preamble,
/// Xbox 360 signed streaming) live in one place. Adding a new "produce FF on
/// disk" callsite elsewhere is the bug pattern that caused the PC ui.ff
/// "version -2097086464" regression — don't do it.
/// </summary>
/// <remarks>
/// Three entry points by detection contract:
///   * <see cref="Save"/> — caller already knows game + platform. Used by the
///     editor when an opened FastFile carries the source's IsPC/IsXbox360/etc.
///   * <see cref="SaveDetectingFromZone"/> — auto-detects game + platform from
///     zone bytes. Used by the CLI's <c>compress</c> command and the editor's
///     Tools &gt; Compress Zone to FF flow.
///   * <see cref="SaveDetecting"/> — path-based convenience wrapper around
///     SaveDetectingFromZone.
/// </remarks>
public static class FastFileSaveService
{
    /// <summary>
    /// Saves the zone at <paramref name="zoneFilePath"/> back to a FastFile at
    /// <paramref name="ffFilePath"/> using the explicitly-supplied game and
    /// platform. Use when you already know what was loaded (e.g. from a
    /// previously-opened FF header).
    /// </summary>
    /// <param name="signed">True for the Xbox 360 signed streaming format
    /// (IWff0100 + IWffs100 + hash table). Requires <paramref name="originalFfPath"/>
    /// so the hash table can be copied from the source.</param>
    /// <param name="originalFfPath">Path to the original FF (for signed Xbox 360
    /// hash table copy, or for MW2 to preserve the extended-header timestamp).
    /// Pass null for fresh builds.</param>
    public static void Save(
        string zoneFilePath,
        string ffFilePath,
        GameVersion gameVersion,
        string platform,
        bool signed = false,
        string? originalFfPath = null)
    {
        if (zoneFilePath is null) throw new ArgumentNullException(nameof(zoneFilePath));
        if (ffFilePath is null) throw new ArgumentNullException(nameof(ffFilePath));
        if (platform is null) throw new ArgumentNullException(nameof(platform));

        LogSave("Save", ffFilePath, gameVersion, platform, signed);

        FastFileProcessor.Recompress(
            zoneFilePath,
            ffFilePath,
            gameVersion,
            platform,
            signed,
            originalFfPath);
    }

    /// <summary>
    /// Detects game + platform from zone bytes. Detection chain: PC (LE) → Wii
    /// (BE + 56-byte layout) → Xbox 360 (BE + WaW magic MemAlloc1) → PS3 fallback.
    /// PS3 and unsigned Xbox 360 produce byte-identical FF output, so the PS3
    /// default is correct for unsigned 360 zones that fall through.
    /// </summary>
    /// <param name="fallbackGame">Used if game detection returns Unknown
    /// (e.g. a custom mod zone with non-magic MemAlloc).</param>
    public static (GameVersion gameVersion, string platform) DetectFromZone(
        byte[] zoneData,
        GameVersion fallbackGame = GameVersion.WaW)
    {
        if (zoneData is null) throw new ArgumentNullException(nameof(zoneData));

        GameVersion gameVersion = FastFileInfo.DetectGameFromZoneData(zoneData);
        if (gameVersion == GameVersion.Unknown) gameVersion = fallbackGame;

        string platform =
            FastFileInfo.IsZoneDataPC(zoneData) ? "PC" :
            FastFileInfo.IsZoneDataWii(zoneData) ? "Wii" :
            FastFileInfo.IsZoneDataXbox360(zoneData) ? "Xbox360" :
            "PS3";

        return (gameVersion, platform);
    }

    /// <summary>
    /// Saves a zone byte array to a FastFile, detecting game + platform from
    /// the zone bytes themselves via <see cref="DetectFromZone"/>.
    /// </summary>
    /// <param name="fallbackGame">Used if game detection returns Unknown
    /// (e.g. a custom mod zone with non-magic MemAlloc).</param>
    public static void SaveDetectingFromZone(
        byte[] zoneData,
        string ffFilePath,
        GameVersion fallbackGame = GameVersion.WaW)
    {
        if (ffFilePath is null) throw new ArgumentNullException(nameof(ffFilePath));

        var (gameVersion, platform) = DetectFromZone(zoneData, fallbackGame);
        LogSave("SaveDetectingFromZone", ffFilePath, gameVersion, platform, signed: false);
        new Compiler(gameVersion, platform).CompileToFile(zoneData, ffFilePath, saveZone: false);
    }

    /// <summary>
    /// Path-based wrapper around <see cref="SaveDetectingFromZone"/>: reads the
    /// zone from <paramref name="zoneFilePath"/> and auto-detects game/platform
    /// before compressing to <paramref name="ffFilePath"/>.
    /// </summary>
    public static void SaveDetecting(
        string zoneFilePath,
        string ffFilePath,
        GameVersion fallbackGame = GameVersion.WaW)
    {
        if (zoneFilePath is null) throw new ArgumentNullException(nameof(zoneFilePath));
        SaveDetectingFromZone(File.ReadAllBytes(zoneFilePath), ffFilePath, fallbackGame);
    }

    private static void LogSave(string source, string ffFilePath, GameVersion game, string platform, bool signed)
    {
        LogService.Info("Save",
            $"src={source} file='{Path.GetFileName(ffFilePath)}' game={game} platform={platform} signed={signed}");
    }
}
