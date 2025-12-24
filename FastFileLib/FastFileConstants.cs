namespace FastFileLib;

/// <summary>
/// Constants for FastFile compilation and processing.
/// </summary>
public static class FastFileConstants
{
    /// <summary>
    /// Valid file extensions for raw files in FastFiles.
    /// </summary>
    public static readonly string[] ValidRawFileExtensions = {
        ".cfg", ".gsc", ".atr", ".csc", ".rmb", ".arena", ".vision", ".txt",
        ".str", ".menu", ".def", ".lua", ".csv", ".graph", ".ai_bt"
    };

    /// <summary>
    /// Checks if a file extension is a valid raw file extension.
    /// </summary>
    public static bool IsValidRawFileExtension(string extension)
    {
        return ValidRawFileExtensions.Contains(extension.ToLowerInvariant());
    }

    /// <summary>
    /// Known path prefixes for flattened asset names.
    /// </summary>
    public static readonly string[] KnownAssetPrefixes = {
        "maps_mp_animscripts_",
        "maps_mp_gametypes_",
        "maps_mp_",
        "maps_",
        "clientscripts_mp_",
        "clientscripts_",
        "common_scripts_",
        "zzzz_zz_",
        "animscripts_"
    };

    /// <summary>
    /// Converts flattened asset names back to proper game paths.
    /// Example: maps_mp_gametypes__globallogic.gsc -> maps/mp/gametypes/_globallogic.gsc
    /// </summary>
    /// <param name="assetName">The flattened asset name</param>
    /// <returns>The proper game path</returns>
    public static string FixAssetPath(string assetName)
    {
        // Don't fix if it already contains forward slashes (already a path)
        if (assetName.Contains('/'))
            return assetName;

        bool hasKnownPrefix = KnownAssetPrefixes.Any(p =>
            assetName.StartsWith(p, StringComparison.OrdinalIgnoreCase));

        // If no known prefix, don't change
        if (!hasKnownPrefix)
            return assetName;

        // Get extension
        string extension = Path.GetExtension(assetName);
        string nameOnly = Path.GetFileNameWithoutExtension(assetName);

        // The conversion logic:
        // Original path: clientscripts/mp/_vehicle.csc
        // Flattened:     clientscripts_mp__vehicle.csc
        // Rules:
        //   - Single underscore (_) was a path separator (/)
        //   - Double underscore (__) was path separator + underscore (/_)
        //
        // To reverse:
        // 1. Replace __ with a placeholder that represents /_
        // 2. Replace _ with /
        // 3. Replace placeholder with _

        const string placeholder = "\x01\x02"; // Unique placeholder

        // Step 1: Replace __ with /<placeholder> (this represents /_)
        string result = nameOnly.Replace("__", "/" + placeholder);

        // Step 2: Replace remaining single _ with /
        result = result.Replace("_", "/");

        // Step 3: Replace placeholder back with underscore
        result = result.Replace(placeholder, "_");

        return result + extension;
    }

    // Header magic strings
    public const string UnsignedHeader = "IWffu100";
    public const string SignedHeader = "IWff0100";
    public const string TreyarchHeader = "TAff0100";
    public const string SledgehammerHeader = "S1ff0100";

    // Header magic as byte arrays
    public static readonly byte[] UnsignedHeaderBytes = System.Text.Encoding.ASCII.GetBytes(UnsignedHeader);
    public static readonly byte[] SignedHeaderBytes = System.Text.Encoding.ASCII.GetBytes(SignedHeader);

    // Header structure offsets and sizes
    public const int HeaderSize = 12;      // 8 bytes magic + 4 bytes version
    public const int MagicLength = 8;      // Magic string length
    public const int VersionOffset = 8;    // Version starts at byte 8
    public const int VersionLength = 4;    // Version is 4 bytes

    // Compression block size
    public const int BlockSize = 65536;    // 0x10000 - 64KB blocks

    #region Zone Header Offsets

    /// <summary>
    /// Zone header sizes vary by platform:
    /// - Xbox 360 (MW2 only): 48 bytes (0x30) - no BlockSizeVertex field
    /// - PS3/CoD4/WaW (all platforms): 52 bytes (0x34) - includes BlockSizeVertex
    /// - PC: 56 bytes (0x38) - includes additional field
    /// Note: CoD4 and WaW use the same 52-byte structure across ALL platforms.
    /// </summary>
    public const int ZoneHeaderSize_Xbox360 = 0x30;  // MW2 Xbox 360 only
    public const int ZoneHeaderSize_PS3 = 0x34;      // PS3, CoD4 (all platforms), WaW (all platforms)
    public const int ZoneHeaderSize_PC = 0x38;       // PC

    // XFile structure offsets (common to all platforms)
    public const int ZoneSizeOffset = 0x00;
    public const int ExternalSizeOffset = 0x04;
    public const int BlockSizeTempOffset = 0x08;
    public const int BlockSizePhysicalOffset = 0x0C;
    public const int BlockSizeRuntimeOffset = 0x10;
    public const int BlockSizeVirtualOffset = 0x14;
    public const int BlockSizeLargeOffset = 0x18;
    public const int BlockSizeCallbackOffset = 0x1C;
    public const int BlockSizeVertexOffset = 0x20;  // PS3/PC only, not on MW2 Xbox 360 (but present on CoD4/WaW Xbox 360)

    // XAssetList offsets - PS3, CoD4, and WaW (all platforms)
    public const int ScriptStringCountOffset_PS3 = 0x24;
    public const int ScriptStringsPtrOffset_PS3 = 0x28;
    public const int AssetCountOffset_PS3 = 0x2C;
    public const int AssetsPtrOffset_PS3 = 0x30;

    // XAssetList offsets - Xbox 360 (MW2 only, NOT CoD4 or WaW)
    public const int ScriptStringCountOffset_Xbox360 = 0x20;
    public const int ScriptStringsPtrOffset_Xbox360 = 0x24;
    public const int AssetCountOffset_Xbox360 = 0x28;
    public const int AssetsPtrOffset_Xbox360 = 0x2C;

    // XAssetList offsets - PC
    public const int ScriptStringCountOffset_PC = 0x28;
    public const int ScriptStringsPtrOffset_PC = 0x2C;
    public const int AssetCountOffset_PC = 0x30;
    public const int AssetsPtrOffset_PC = 0x34;

    /// <summary>
    /// Gets the zone header size for the given game and platform.
    /// CoD4 and WaW use PS3-style offsets on ALL platforms.
    /// Only MW2 Xbox 360 uses the smaller 48-byte header.
    /// </summary>
    public static int GetZoneHeaderSize(GameVersion version, bool isXbox360, bool isPC)
    {
        // CoD4 and WaW use PS3-style header on all platforms
        if (version == GameVersion.CoD4 || version == GameVersion.WaW)
            return ZoneHeaderSize_PS3;

        if (isPC)
            return ZoneHeaderSize_PC;
        if (isXbox360)
            return ZoneHeaderSize_Xbox360;
        return ZoneHeaderSize_PS3;
    }

    /// <summary>
    /// Gets the AssetCount offset for the given game and platform.
    /// CoD4 and WaW use PS3-style offsets on ALL platforms.
    /// Only MW2 Xbox 360 uses Xbox 360-specific offsets.
    /// </summary>
    public static int GetAssetCountOffset(GameVersion version, bool isXbox360, bool isPC)
    {
        // CoD4 and WaW use PS3-style offsets on all platforms
        if (version == GameVersion.CoD4 || version == GameVersion.WaW)
            return AssetCountOffset_PS3;

        if (isPC)
            return AssetCountOffset_PC;
        if (isXbox360)
            return AssetCountOffset_Xbox360;
        return AssetCountOffset_PS3;
    }

    /// <summary>
    /// Gets the ScriptStringCount offset for the given game and platform.
    /// CoD4 and WaW use PS3-style offsets on ALL platforms.
    /// Only MW2 Xbox 360 uses Xbox 360-specific offsets.
    /// </summary>
    public static int GetScriptStringCountOffset(GameVersion version, bool isXbox360, bool isPC)
    {
        // CoD4 and WaW use PS3-style offsets on all platforms
        if (version == GameVersion.CoD4 || version == GameVersion.WaW)
            return ScriptStringCountOffset_PS3;

        if (isPC)
            return ScriptStringCountOffset_PC;
        if (isXbox360)
            return ScriptStringCountOffset_Xbox360;
        return ScriptStringCountOffset_PS3;
    }

    #endregion

    // Version bytes (big-endian)
    public static readonly byte[] CoD4Version = { 0x00, 0x00, 0x00, 0x01 };
    public static readonly byte[] WaWVersion = { 0x00, 0x00, 0x01, 0x83 };
    public static readonly byte[] MW2Version = { 0x00, 0x00, 0x01, 0x0D };

    // Asset type IDs for rawfile
    public const byte CoD4RawFileAssetType = 0x21; // 33
    public const byte WaWRawFileAssetType = 0x22;  // 34
    public const byte MW2RawFileAssetType = 0x23;  // 35

    // Asset type IDs for localize
    public const byte CoD4LocalizeAssetType = 0x18; // 24
    public const byte WaWLocalizeAssetType = 0x19;  // 25
    public const byte MW2LocalizeAssetType = 0x1A;  // 26

    // Zone header memory allocation values (big-endian)
    public static readonly byte[] CoD4MemAlloc1 = { 0x00, 0x00, 0x0F, 0x70 };
    public static readonly byte[] WaWMemAlloc1 = { 0x00, 0x00, 0x10, 0xB0 };
    public static readonly byte[] MW2MemAlloc1 = { 0x00, 0x00, 0x03, 0xB4 };

    public static readonly byte[] CoD4MemAlloc2 = { 0x00, 0x00, 0x00, 0x00 };
    public static readonly byte[] WaWMemAlloc2 = { 0x00, 0x05, 0xF8, 0xF0 };
    public static readonly byte[] MW2MemAlloc2 = { 0x00, 0x00, 0x10, 0x00 };

    public static byte[] GetVersionBytes(GameVersion version) => version switch
    {
        GameVersion.CoD4 => CoD4Version,
        GameVersion.WaW => WaWVersion,
        GameVersion.MW2 => MW2Version,
        _ => throw new ArgumentOutOfRangeException(nameof(version))
    };

    public static byte GetRawFileAssetType(GameVersion version) => version switch
    {
        GameVersion.CoD4 => CoD4RawFileAssetType,
        GameVersion.WaW => WaWRawFileAssetType,
        GameVersion.MW2 => MW2RawFileAssetType,
        _ => throw new ArgumentOutOfRangeException(nameof(version))
    };

    public static byte GetLocalizeAssetType(GameVersion version) => version switch
    {
        GameVersion.CoD4 => CoD4LocalizeAssetType,
        GameVersion.WaW => WaWLocalizeAssetType,
        GameVersion.MW2 => MW2LocalizeAssetType,
        _ => throw new ArgumentOutOfRangeException(nameof(version))
    };

    public static byte[] GetMemAlloc1(GameVersion version) => version switch
    {
        GameVersion.CoD4 => CoD4MemAlloc1,
        GameVersion.WaW => WaWMemAlloc1,
        GameVersion.MW2 => MW2MemAlloc1,
        _ => throw new ArgumentOutOfRangeException(nameof(version))
    };

    public static byte[] GetMemAlloc2(GameVersion version) => version switch
    {
        GameVersion.CoD4 => CoD4MemAlloc2,
        GameVersion.WaW => WaWMemAlloc2,
        GameVersion.MW2 => MW2MemAlloc2,
        _ => throw new ArgumentOutOfRangeException(nameof(version))
    };
}
