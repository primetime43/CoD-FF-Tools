using System.Text;
using FastFileLib.GameDefinitions;

namespace FastFileLib;

/// <summary>
/// Contains information about a FastFile's header and format.
/// </summary>
public class FastFileInfo
{
    public string Magic { get; set; } = "";
    public uint Version { get; set; }
    public GameVersion GameVersion { get; set; }
    public bool IsSigned { get; set; }
    public bool IsPC { get; set; }
    public bool IsWii { get; set; }
    public string GameName { get; set; } = "Unknown";
    public string[] Platforms { get; set; } = Array.Empty<string>();
    public int HeaderSize { get; set; }

    /// <summary>
    /// Gets the specific platform detected from the header (PS3, Xbox 360, PC, or Wii).
    /// Unlike Platforms array which lists all possible platforms, this returns the actual detected platform.
    /// </summary>
    public string Platform { get; set; } = "Unknown";

    /// <summary>
    /// Gets the studio that developed the game (Infinity Ward, Treyarch, Sledgehammer, etc.).
    /// Note: The "IW" in the magic header refers to the engine format, not the studio.
    /// </summary>
    public string Studio { get; set; } = "Unknown";

    // Header magic constants
    public const string UnsignedMagic = "IWffu100";
    public const string SignedMagic = "IWff0100";
    public const string TreyarchMagic = "TAff0100";

    // Version constants - reference the game definitions
    public const uint CoD4_PS3_Version = (uint)CoD4Definition.VersionValue;
    public const uint CoD4_PC_Version = (uint)CoD4Definition.PCVersionValue;
    public const uint CoD4_Wii_Version = (uint)CoD4Definition.WiiVersionValue;
    public const uint WaW_Console_PC_Version = (uint)CoD5Definition.VersionValue;
    public const uint WaW_Wii_Version = (uint)CoD5Definition.WiiVersionValue;
    public const uint MW2_Console_Version = (uint)MW2Definition.VersionValue;
    public const uint MW2_PC_Version = (uint)MW2Definition.PCVersionValue;
    public const uint MW2_DevBuild_Version = (uint)MW2Definition.DevBuildVersionValue;

    /// <summary>
    /// Reads FastFile header information from a file.
    /// </summary>
    public static FastFileInfo FromFile(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var br = new BinaryReader(fs);
        return FromReader(br);
    }

    /// <summary>
    /// Reads FastFile header information from a BinaryReader.
    /// </summary>
    public static FastFileInfo FromReader(BinaryReader br)
    {
        var info = new FastFileInfo();

        byte[] magicBytes = br.ReadBytes(8);
        info.Magic = Encoding.ASCII.GetString(magicBytes);

        byte[] versionBytes = br.ReadBytes(4);

        // Try big-endian first (console format)
        uint versionBE = (uint)((versionBytes[0] << 24) | (versionBytes[1] << 16) |
                                (versionBytes[2] << 8) | versionBytes[3]);
        // Little-endian (PC format)
        uint versionLE = (uint)(versionBytes[0] | (versionBytes[1] << 8) |
                                (versionBytes[2] << 16) | (versionBytes[3] << 24));

        // Determine if signed
        info.IsSigned = info.Magic == SignedMagic || info.Magic == TreyarchMagic;

        // Try big-endian first
        info.Version = versionBE;
        DetectGameVersion(info);

        // If big-endian didn't work and file is unsigned, try little-endian (PC)
        if (info.GameVersion == GameVersion.Unknown && !info.IsSigned && info.Magic == UnsignedMagic)
        {
            info.Version = versionLE;
            DetectGameVersion(info);
            if (info.GameVersion != GameVersion.Unknown)
            {
                info.IsPC = true;
            }
        }

        // Set the specific platform
        // Check for PC (little-endian) or Wii (specific version) first, otherwise use magic-based detection
        if (info.IsPC)
        {
            info.Platform = "PC";
        }
        else if (info.IsWii)
        {
            info.Platform = "Wii";
        }
        else
        {
            info.Platform = GetPlatform(info.Version, info.Magic);
        }

        return info;
    }

    private static void DetectGameVersion(FastFileInfo info)
    {
        switch (info.Version)
        {
            case CoD4_PS3_Version:
                info.GameVersion = GameVersion.CoD4;
                info.GameName = "CoD4";
                info.Studio = "Infinity Ward";
                info.Platforms = new[] { "PS3", "Xbox 360" };
                info.HeaderSize = 12;
                break;
            case CoD4_PC_Version:
                info.GameVersion = GameVersion.CoD4;
                info.GameName = "CoD4";
                info.Studio = "Infinity Ward";
                info.Platforms = new[] { "PC" };
                info.HeaderSize = 12;
                break;
            case CoD4_Wii_Version:
                info.GameVersion = GameVersion.CoD4;
                info.GameName = "CoD4";
                info.Studio = "Infinity Ward";
                info.Platforms = new[] { "Wii" };
                info.HeaderSize = 12;
                info.IsWii = true;
                break;
            case WaW_Console_PC_Version:
                info.GameVersion = GameVersion.WaW;
                info.GameName = "WaW";
                info.Studio = "Treyarch";
                info.Platforms = new[] { "PS3", "Xbox 360", "PC" };
                info.HeaderSize = 12;
                break;
            case WaW_Wii_Version:
                info.GameVersion = GameVersion.WaW;
                info.GameName = "WaW";
                info.Studio = "Treyarch";
                info.Platforms = new[] { "Wii" };
                info.HeaderSize = 12;
                info.IsWii = true;
                break;
            case MW2_Console_Version:
                info.GameVersion = GameVersion.MW2;
                info.GameName = "MW2";
                info.Studio = "Infinity Ward";
                info.Platforms = new[] { "PS3", "Xbox 360" };
                info.HeaderSize = -1; // Variable, needs to be calculated
                break;
            case MW2_PC_Version:
                info.GameVersion = GameVersion.MW2;
                info.GameName = "MW2";
                info.Studio = "Infinity Ward";
                info.Platforms = new[] { "PC" };
                info.HeaderSize = -1; // Variable, needs to be calculated
                break;
            case MW2_DevBuild_Version:
                info.GameVersion = GameVersion.MW2;
                info.GameName = "MW2 (Dev Build)";
                info.Studio = "Infinity Ward";
                info.Platforms = new[] { "Xbox 360" };
                info.HeaderSize = -1; // Variable, needs to be calculated
                break;
            default:
                info.GameVersion = GameVersion.Unknown;
                info.GameName = "Unknown";
                info.Studio = "Unknown";
                info.Platforms = new[] { "Unknown" };
                info.HeaderSize = 12;
                break;
        }
    }

    /// <summary>
    /// Gets the version bytes for packing a FastFile.
    /// </summary>
    /// <param name="version">Game version</param>
    /// <param name="platform">Target platform (PS3, Xbox360, PC, Wii)</param>
    public static byte[] GetVersionBytes(GameVersion version, string platform = "PS3")
    {
        // Normalize platform string
        string normalizedPlatform = platform.ToUpperInvariant() switch
        {
            "XBOX360" or "XBOX 360" or "360" => "Xbox360",
            "PS3" or "PLAYSTATION3" or "PLAYSTATION 3" => "PS3",
            "PC" or "WINDOWS" => "PC",
            "WII" => "Wii",
            _ => platform
        };

        return version switch
        {
            // CoD4 versions
            GameVersion.CoD4 when normalizedPlatform == "PC" => new byte[] { 0x00, 0x00, 0x00, 0x05 },
            GameVersion.CoD4 when normalizedPlatform == "Wii" => new byte[] { 0x00, 0x00, 0x01, 0xA2 },
            GameVersion.CoD4 => new byte[] { 0x00, 0x00, 0x00, 0x01 }, // PS3/Xbox 360 share same version

            // WaW versions
            GameVersion.WaW when normalizedPlatform == "Wii" => new byte[] { 0x00, 0x00, 0x01, 0x9B },
            GameVersion.WaW => new byte[] { 0x00, 0x00, 0x01, 0x83 }, // PS3/Xbox 360/PC share same version

            // MW2 versions
            GameVersion.MW2 when normalizedPlatform == "PC" => new byte[] { 0x00, 0x00, 0x01, 0x14 },
            GameVersion.MW2 => new byte[] { 0x00, 0x00, 0x01, 0x0D }, // PS3/Xbox 360 share same version

            _ => new byte[] { 0x00, 0x00, 0x00, 0x01 }
        };
    }

    /// <summary>
    /// Gets the magic bytes for the header.
    /// </summary>
    public static byte[] GetMagicBytes(bool signed = false)
    {
        return Encoding.ASCII.GetBytes(signed ? SignedMagic : UnsignedMagic);
    }

    /// <summary>
    /// Gets the specific platform name based on the magic string and version.
    /// Uses magic to distinguish PS3 (unsigned) from Xbox 360 (signed).
    /// </summary>
    /// <param name="version">The version number from the header</param>
    /// <param name="magic">The magic string from the header</param>
    /// <returns>Platform name: PS3, Xbox 360, PC, or Wii</returns>
    public static string GetPlatform(uint version, string magic)
    {
        // PC versions have specific version numbers
        if (version == CoD4_PC_Version || version == MW2_PC_Version)
            return "PC";

        // Wii versions
        if (version == CoD4_Wii_Version || version == WaW_Wii_Version)
            return "Wii";

        // For console versions, use magic to distinguish PS3 vs Xbox 360
        // IWffu100 = unsigned (PS3)
        // IWffs100 = signed (Xbox 360)
        // IWff0100 = signed (Xbox 360)
        if (magic == UnsignedMagic)
            return "PS3";
        else if (magic == SignedMagic || magic == "IWffs100")
            return "Xbox 360";

        return "Console";
    }

    /// <summary>
    /// Gets the specific platform name for this FastFileInfo instance.
    /// </summary>
    public string GetPlatform()
    {
        return GetPlatform(Version, Magic);
    }

    /// <summary>
    /// Formats a file size in bytes to a human-readable string.
    /// </summary>
    /// <param name="bytes">File size in bytes</param>
    /// <returns>Formatted string like "1.5 MB"</returns>
    public static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }

    #region Zone Detection Utilities

    /// <summary>
    /// Detects the game type from a zone file by reading the MemAlloc1 value at offset 0x08.
    /// </summary>
    /// <param name="zonePath">Path to the zone file</param>
    /// <returns>Detected game version, or Unknown if detection failed</returns>
    public static GameVersion DetectGameFromZone(string zonePath)
    {
        try
        {
            byte[] header = new byte[12];
            using (var fs = new FileStream(zonePath, FileMode.Open, FileAccess.Read))
            {
                if (fs.Read(header, 0, 12) < 12)
                    return GameVersion.Unknown;
            }

            return DetectGameFromZoneData(header);
        }
        catch
        {
            return GameVersion.Unknown;
        }
    }

    /// <summary>
    /// Detects the game type from zone data by reading the MemAlloc1 value at offset 0x08.
    /// </summary>
    /// <param name="zoneData">The zone file data (at least 12 bytes)</param>
    /// <returns>Detected game version, or Unknown if detection failed</returns>
    public static GameVersion DetectGameFromZoneData(byte[] zoneData)
    {
        if (zoneData == null || zoneData.Length < 12)
            return GameVersion.Unknown;

        // MemAlloc1 is at offset 0x08, try big-endian first (console)
        uint memAlloc1BE = (uint)((zoneData[8] << 24) | (zoneData[9] << 16) | (zoneData[10] << 8) | zoneData[11]);
        uint memAlloc1LE = (uint)(zoneData[8] | (zoneData[9] << 8) | (zoneData[10] << 16) | (zoneData[11] << 24));

        // Check big-endian values first (console)
        return memAlloc1BE switch
        {
            CoD5Definition.MemAlloc1Value => GameVersion.WaW,           // 0x10B0 - WaW PS3
            CoD5Definition.Xbox360MemAlloc1Value => GameVersion.WaW,    // 0x0A90 - WaW Xbox 360
            CoD4Definition.MemAlloc1Value => GameVersion.CoD4,          // 0x0F70 - CoD4
            MW2Definition.MemAlloc1Value => GameVersion.MW2,            // 0x03B4 - MW2
            _ => memAlloc1LE switch
            {
                // Check little-endian values (PC)
                CoD5Definition.MemAlloc1Value => GameVersion.WaW,
                CoD4Definition.MemAlloc1Value => GameVersion.CoD4,
                MW2Definition.MemAlloc1Value => GameVersion.MW2,
                _ => GameVersion.Unknown
            }
        };
    }

    /// <summary>
    /// Detects if a zone file is from a PC version by checking endianness.
    /// PC files use little-endian byte order, while PS3/Xbox use big-endian.
    /// </summary>
    /// <param name="zonePath">Path to the zone file</param>
    /// <returns>True if the zone appears to be PC (little-endian), false otherwise</returns>
    public static bool IsZonePC(string zonePath)
    {
        try
        {
            byte[] header = new byte[12];
            using (var fs = new FileStream(zonePath, FileMode.Open, FileAccess.Read))
            {
                if (fs.Read(header, 0, 12) < 12)
                    return false;
            }

            return IsZoneDataPC(header);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Detects if zone data is from a PC version by checking endianness.
    /// PC files use little-endian byte order, while PS3/Xbox use big-endian.
    /// </summary>
    /// <param name="zoneData">The zone file data (at least 12 bytes)</param>
    /// <returns>True if the zone appears to be PC (little-endian), false otherwise</returns>
    public static bool IsZoneDataPC(byte[] zoneData)
    {
        if (zoneData == null || zoneData.Length < 12)
            return false;

        // Read MemAlloc1 at offset 0x08 as both big-endian and little-endian
        uint memAlloc1BE = (uint)((zoneData[8] << 24) | (zoneData[9] << 16) | (zoneData[10] << 8) | zoneData[11]);
        uint memAlloc1LE = (uint)(zoneData[8] | (zoneData[9] << 8) | (zoneData[10] << 16) | (zoneData[11] << 24));

        // Known console MemAlloc1 values (big-endian)
        uint[] consoleValues = {
            CoD5Definition.MemAlloc1Value,        // 0x10B0 - WaW PS3
            CoD5Definition.Xbox360MemAlloc1Value, // 0x0A90 - WaW Xbox 360
            CoD4Definition.MemAlloc1Value,        // 0x0F70 - CoD4
            MW2Definition.MemAlloc1Value          // 0x03B4 - MW2
        };

        // If we read as BE and get a known console value, it's console (not PC)
        foreach (var val in consoleValues)
        {
            if (memAlloc1BE == val)
                return false; // Big-endian match = console
        }

        // Known PC MemAlloc1 values - same numeric values but stored as little-endian
        // If we read as LE and get a known value, it's PC
        foreach (var val in consoleValues)
        {
            if (memAlloc1LE == val)
                return true; // Little-endian match = PC
        }

        return false;
    }

    /// <summary>
    /// Detects if a zone file is from Xbox 360 by checking the MemAlloc1 value.
    /// Xbox 360 WaW uses different MemAlloc values than PS3.
    /// </summary>
    /// <param name="zonePath">Path to the zone file</param>
    /// <returns>True if the zone appears to be Xbox 360, false otherwise</returns>
    public static bool IsZoneXbox360(string zonePath)
    {
        try
        {
            byte[] header = new byte[12];
            using (var fs = new FileStream(zonePath, FileMode.Open, FileAccess.Read))
            {
                if (fs.Read(header, 0, 12) < 12)
                    return false;
            }

            return IsZoneDataXbox360(header);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Detects if zone data is from Xbox 360 by checking the MemAlloc1 value.
    /// Xbox 360 WaW uses different MemAlloc values than PS3.
    /// </summary>
    /// <param name="zoneData">The zone file data (at least 12 bytes)</param>
    /// <returns>True if the zone appears to be Xbox 360, false otherwise</returns>
    public static bool IsZoneDataXbox360(byte[] zoneData)
    {
        if (zoneData == null || zoneData.Length < 12)
            return false;

        // Read MemAlloc1 at offset 0x08 as big-endian
        uint memAlloc1BE = (uint)((zoneData[8] << 24) | (zoneData[9] << 16) | (zoneData[10] << 8) | zoneData[11]);

        // Xbox 360 WaW uses 0x0A90, PS3 uses 0x10B0
        return memAlloc1BE == CoD5Definition.Xbox360MemAlloc1Value;
    }

    #endregion

    /// <summary>
    /// Extracts the zone name from a file path by matching known zone name patterns.
    /// </summary>
    /// <param name="filePath">Path to the zone or FastFile</param>
    /// <returns>The detected zone name or cleaned filename</returns>
    public static string GetZoneNameFromPath(string filePath)
    {
        string filename = Path.GetFileNameWithoutExtension(filePath);

        // Known zone name suffixes
        string[] knownZoneNames = {
            "patch_mp", "patch", "common_mp", "common", "code_post_gfx_mp", "code_post_gfx",
            "localized_common_mp", "localized_code_post_gfx_mp", "ui_mp", "ui"
        };

        foreach (var zoneName in knownZoneNames)
        {
            if (filename.EndsWith(zoneName, StringComparison.OrdinalIgnoreCase))
                return zoneName;
            if (filename.EndsWith("_" + zoneName, StringComparison.OrdinalIgnoreCase))
                return zoneName;
        }

        foreach (var zoneName in knownZoneNames)
        {
            int idx = filename.IndexOf(" " + zoneName, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
                return zoneName;
        }

        // Fallback: clean up filename by removing platform prefixes
        string cleaned = filename
            .Replace("xbox ", "", StringComparison.OrdinalIgnoreCase)
            .Replace("ps3 ", "", StringComparison.OrdinalIgnoreCase)
            .Replace("xbox_", "", StringComparison.OrdinalIgnoreCase)
            .Replace("ps3_", "", StringComparison.OrdinalIgnoreCase)
            .Replace("_converted", "", StringComparison.OrdinalIgnoreCase)
            .Replace("converted_", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        return string.IsNullOrEmpty(cleaned) ? filename : cleaned;
    }
}
