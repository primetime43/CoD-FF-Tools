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

        // Try big-endian first (consoles use BE)
        info.Version = versionBE;
        DetectGameVersion(info);

        // If BE didn't match anything, try LE. Originally gated to UnsignedMagic only, but
        // MW2 PC files use IWff0100 (signed magic) + LE version bytes (e.g. `14 01 00 00` =
        // 0x114 LE), so retail MW2 PC files were being rejected as "not valid" because we
        // never got to the LE pass. Trying LE on any unrecognized BE version is safe: if BE
        // matched a known game we wouldn't be here, and LE only "succeeds" when it lands on
        // a real version constant.
        if (info.GameVersion == GameVersion.Unknown)
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

        // PC files store the version little-endian on disk; consoles/Wii are big-endian.
        // Verified against real PC WaW samples (`83 01 00 00` = LE 0x183).
        return version switch
        {
            // CoD4 versions
            GameVersion.CoD4 when normalizedPlatform == "PC"  => new byte[] { 0x05, 0x00, 0x00, 0x00 }, // 0x05 LE
            GameVersion.CoD4 when normalizedPlatform == "Wii" => new byte[] { 0x00, 0x00, 0x01, 0xA2 },
            GameVersion.CoD4 => new byte[] { 0x00, 0x00, 0x00, 0x01 }, // PS3/Xbox 360 share same version (BE)

            // WaW versions
            GameVersion.WaW when normalizedPlatform == "PC"  => new byte[] { 0x83, 0x01, 0x00, 0x00 }, // 0x183 LE
            GameVersion.WaW when normalizedPlatform == "Wii" => new byte[] { 0x00, 0x00, 0x01, 0x9B },
            GameVersion.WaW => new byte[] { 0x00, 0x00, 0x01, 0x83 }, // PS3/Xbox 360 (BE)

            // MW2 versions
            GameVersion.MW2 when normalizedPlatform == "PC"  => new byte[] { 0x14, 0x01, 0x00, 0x00 }, // 0x114 LE
            GameVersion.MW2 => new byte[] { 0x00, 0x00, 0x01, 0x0D }, // PS3/Xbox 360 (BE)

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
            // Read enough for layout detection (markers up through offset 0x37 on a 56-byte
            // header, plus a few bytes of slack). Plus need the file size for the ZoneSize
            // plausibility check in the fallback path.
            long fileSize = new FileInfo(zonePath).Length;
            if (fileSize < 12) return GameVersion.Unknown;

            int toRead = (int)Math.Min(fileSize, 0x40);
            byte[] header = new byte[toRead];
            using (var fs = new FileStream(zonePath, FileMode.Open, FileAccess.Read))
            {
                fs.Read(header, 0, toRead);
            }
            return DetectGameInternal(header, fileSize);
        }
        catch
        {
            return GameVersion.Unknown;
        }
    }

    /// <summary>
    /// Detects the game type from zone data. Fast path matches MemAlloc1 against known
    /// per-game magic constants; that covers all retail console zones and most PC zones
    /// that happen to share PS3 values. Fallback inspects the zone header *shape* —
    /// position of the 0xFFFFFFFF asset-pool/script-string-pool placeholders — combined
    /// with endianness from the ZoneSize field. This lets us recognize retail MW2 PC
    /// (e.g. patch_mp.zone) whose per-zone MemAlloc1 (0x020C) isn't in the magic table.
    /// </summary>
    /// <param name="zoneData">The zone file data (the whole zone — its length is used
    /// to validate ZoneSize plausibility in the fallback path)</param>
    public static GameVersion DetectGameFromZoneData(byte[] zoneData)
    {
        if (zoneData == null || zoneData.Length < 12)
            return GameVersion.Unknown;
        return DetectGameInternal(zoneData, zoneData.Length);
    }

    /// <summary>
    /// Internal detection that accepts the header bytes plus the actual file length
    /// separately — lets <see cref="DetectGameFromZone(string)"/> avoid loading huge
    /// retail zones into memory while still being able to validate ZoneSize.
    /// </summary>
    private static GameVersion DetectGameInternal(byte[] header, long fileSize)
    {
        // Fast path: known MemAlloc1 magic constants.
        var byMagic = DetectGameByMemAllocMagic(header);
        if (byMagic != GameVersion.Unknown) return byMagic;

        // Fallback: combine endianness (from ZoneSize plausibility) with header shape
        // (from 0xFFFFFFFF marker positions) to identify the game/layout.
        bool? isLE = DetectEndianness(header, fileSize);
        return DetectGameByHeaderShape(header, isLE);
    }

    private static GameVersion DetectGameByMemAllocMagic(byte[] header)
    {
        if (header.Length < 12) return GameVersion.Unknown;
        uint memAlloc1BE = (uint)((header[8] << 24) | (header[9] << 16) | (header[10] << 8) | header[11]);
        uint memAlloc1LE = (uint)(header[8] | (header[9] << 8) | (header[10] << 16) | (header[11] << 24));

        return memAlloc1BE switch
        {
            CoD5Definition.MemAlloc1Value => GameVersion.WaW,           // 0x10B0 - WaW PS3
            CoD5Definition.Xbox360MemAlloc1Value => GameVersion.WaW,    // 0x0A90 - WaW Xbox 360
            CoD4Definition.MemAlloc1Value => GameVersion.CoD4,          // 0x0F70 - CoD4
            MW2Definition.MemAlloc1Value => GameVersion.MW2,            // 0x03B4 - MW2
            _ => memAlloc1LE switch
            {
                CoD5Definition.MemAlloc1Value => GameVersion.WaW,
                CoD4Definition.MemAlloc1Value => GameVersion.CoD4,
                MW2Definition.MemAlloc1Value => GameVersion.MW2,
                _ => GameVersion.Unknown
            }
        };
    }

    /// <summary>
    /// Resolves endianness via the ZoneSize field at offset 0x00. ZoneSize is "everything
    /// after the zone header" so it's slightly less than the actual file size — the
    /// difference is at most one 64 KB padding block. Reading 4 bytes in the wrong
    /// endianness almost always produces a value far from the real size.
    /// </summary>
    private static bool? DetectEndianness(byte[] header, long fileSize)
    {
        if (header.Length < 4) return null;
        uint be = (uint)((header[0] << 24) | (header[1] << 16) | (header[2] << 8) | header[3]);
        uint le = (uint)(header[0] | (header[1] << 8) | (header[2] << 16) | (header[3] << 24));

        // ~64 KB padding tolerance + a small slop for unusual padding strategies.
        const long PaddingSlop = 0x10100;
        bool bePlausible = be > 0 && be <= fileSize + PaddingSlop && fileSize - (long)be < PaddingSlop;
        bool lePlausible = le > 0 && le <= fileSize + PaddingSlop && fileSize - (long)le < PaddingSlop;

        if (lePlausible && !bePlausible) return true;   // LE → PC
        if (bePlausible && !lePlausible) return false;  // BE → console/Wii
        return null;  // ambiguous (both or neither plausible)
    }

    /// <summary>
    /// Identifies the game by checking which header layout the 0xFFFFFFFF placeholders
    /// fit (48 / 52 / 56 bytes), combined with detected endianness.
    /// </summary>
    private static GameVersion DetectGameByHeaderShape(byte[] header, bool? isLE)
    {
        bool Has(int offset) =>
            offset + 4 <= header.Length &&
            header[offset] == 0xFF && header[offset + 1] == 0xFF &&
            header[offset + 2] == 0xFF && header[offset + 3] == 0xFF;

        // 56-byte layout (ScriptStringsPtr @ 0x2C, AssetsPtr @ 0x34): MW2 PC (LE) or WaW Wii (BE).
        // MW2 Xbox 360's 48-byte layout *coincidentally* puts FFFFFFFF at both 0x2C (AssetsPtr)
        // AND 0x34 (first asset entry's ptr placeholder), so we additionally require that the
        // value at 0x30 looks like an AssetCount (>0x29) rather than a small asset type id.
        if (Has(0x2C) && Has(0x34) && Plausible56ByteAssetCount(header))
        {
            if (isLE == true) return GameVersion.MW2;
            if (isLE == false) return GameVersion.WaW;
            return GameVersion.Unknown;  // ambiguous endianness
        }

        // 48-byte layout (ScriptStringsPtr @ 0x24, AssetsPtr @ 0x2C): MW2 Xbox 360 only.
        // Check before 52-byte since a 48-byte zone's 0x2C marker shouldn't collide —
        // 52-byte's 0x28 marker would also need to be set, but on 48-byte that slot is
        // ScriptStringCount (typically 0, not FFFFFFFF).
        if (Has(0x24) && Has(0x2C) && !Has(0x28)) return GameVersion.MW2;

        // 52-byte layout (ScriptStringsPtr @ 0x28, AssetsPtr @ 0x30): CoD4/WaW (any platform),
        // MW2 PS3. MemAlloc magic should have caught all retail console zones at this point,
        // so reaching here typically means CoD4/WaW PC with a non-magic MemAlloc.
        if (Has(0x28) && Has(0x30))
        {
            if (isLE == true) return GameVersion.WaW;   // PC: WaW is the common modding target
            // BE without a magic match is unusual — return Unknown rather than guess.
        }

        return GameVersion.Unknown;
    }

    /// <summary>
    /// Heuristic: does the byte pattern at 0x28+0x30 look like a 56-byte zone header
    /// (Wii / MW2 PC) rather than MW2 Xbox 360's 48-byte layout? Both layouts can have
    /// 0xFFFFFFFF at 0x2C and 0x34, so additional context is needed.
    ///   - 56-byte: 0x28 = ScriptStringCount, 0x30 = AssetCount.
    ///   - 48-byte (MW2 Xbox 360): 0x28 = AssetCount, 0x30 = first asset's type word
    ///     (low byte holds the type id, high 3 bytes zero — value ≤ 0x29).
    /// We accept the 56-byte interpretation when EITHER (a) the AssetCount at 0x30
    /// exceeds 0x29 (a real AssetCount; impossible for a type id) OR (b) the value at
    /// 0x28 is zero (likely ScriptStringCount=0, since AssetCount=0 means an empty
    /// pool and is improbable for any real zone).
    /// </summary>
    private static bool Plausible56ByteAssetCount(byte[] header)
    {
        if (header.Length < 0x34) return false;
        uint at30 = (uint)((header[0x30] << 24) | (header[0x31] << 16)
                          | (header[0x32] << 8) | header[0x33]);
        uint at28 = (uint)((header[0x28] << 24) | (header[0x29] << 16)
                          | (header[0x2A] << 8) | header[0x2B]);
        return at30 > 0x29 || at28 == 0;
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
            long fileSize = new FileInfo(zonePath).Length;
            if (fileSize < 12) return false;

            int toRead = (int)Math.Min(fileSize, 0x40);
            byte[] header = new byte[toRead];
            using (var fs = new FileStream(zonePath, FileMode.Open, FileAccess.Read))
            {
                fs.Read(header, 0, toRead);
            }
            return IsZonePCInternal(header, fileSize);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Detects if zone data is from a PC version by checking endianness. Tries the
    /// MemAlloc1 magic table first; falls back to the ZoneSize field plausibility
    /// check, which works for retail PC zones whose per-zone MemAlloc isn't in the
    /// magic table (e.g. MW2 PC patch_mp.zone has MemAlloc1 = 0x020C).
    /// </summary>
    /// <param name="zoneData">The zone file data (the whole zone — its length is used
    /// for the ZoneSize plausibility check in the fallback path)</param>
    public static bool IsZoneDataPC(byte[] zoneData)
    {
        if (zoneData == null || zoneData.Length < 12) return false;
        return IsZonePCInternal(zoneData, zoneData.Length);
    }

    private static bool IsZonePCInternal(byte[] header, long fileSize)
    {
        // Fast path: MemAlloc1 magic match (existing behavior).
        if (header.Length >= 12)
        {
            uint be = (uint)((header[8] << 24) | (header[9] << 16) | (header[10] << 8) | header[11]);
            uint le = (uint)(header[8] | (header[9] << 8) | (header[10] << 16) | (header[11] << 24));

            uint[] consoleValues = {
                CoD5Definition.MemAlloc1Value,        // 0x10B0 - WaW PS3
                CoD5Definition.Xbox360MemAlloc1Value, // 0x0A90 - WaW Xbox 360
                CoD4Definition.MemAlloc1Value,        // 0x0F70 - CoD4
                MW2Definition.MemAlloc1Value          // 0x03B4 - MW2
            };
            foreach (var v in consoleValues) if (be == v) return false;
            foreach (var v in consoleValues) if (le == v) return true;
        }

        // Fallback: pick endianness via ZoneSize plausibility. Catches retail PC zones
        // (and any other zone) whose MemAlloc1 isn't a known magic value.
        return DetectEndianness(header, fileSize) == true;
    }

    /// <summary>
    /// Detects if a zone file is from Wii. Wii zones are big-endian (like PS3/Xbox 360)
    /// but use a 56-byte header layout (8 blockSize slots — extra `BlockSizeIndex`).
    /// This distinguishes Wii from PS3/Xbox 360 which both use a 52-byte (or MW2 Xbox
    /// 360's 48-byte) layout.
    /// </summary>
    public static bool IsZoneWii(string zonePath)
    {
        try
        {
            long fileSize = new FileInfo(zonePath).Length;
            if (fileSize < 0x38) return false;

            int toRead = (int)Math.Min(fileSize, 0x40);
            byte[] header = new byte[toRead];
            using (var fs = new FileStream(zonePath, FileMode.Open, FileAccess.Read))
            {
                fs.Read(header, 0, toRead);
            }
            return IsZoneWiiInternal(header, fileSize);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Detects if zone data is from Wii: big-endian + 56-byte layout. The 56-byte
    /// layout signature is 0xFFFFFFFF markers at offsets 0x2C (ScriptStringsPtr) and
    /// 0x34 (AssetsPtr); the 52-byte layout has them at 0x28 and 0x30 instead.
    /// </summary>
    public static bool IsZoneDataWii(byte[] zoneData)
    {
        if (zoneData == null || zoneData.Length < 0x38) return false;
        return IsZoneWiiInternal(zoneData, zoneData.Length);
    }

    private static bool IsZoneWiiInternal(byte[] header, long fileSize)
    {
        // Wii is BE (PC is the only LE platform among the games we support).
        if (IsZonePCInternal(header, fileSize)) return false;

        // Need 56-byte layout: ScriptStringsPtr @0x2C, AssetsPtr @0x34 (both 0xFFFFFFFF),
        // and AssetCount at 0x30. The tricky part is MW2 Xbox 360's 48-byte layout
        // *coincidentally* has 0xFFFFFFFF at 0x34 — but that's the first asset entry's
        // ptr placeholder, not AssetsPtr; what sits at 0x30 there is the first asset's
        // type word (BE int with low byte = type ID, e.g. `00 00 00 07` for image=0x07).
        //
        // Distinguish them by reading 0x30 as a BE int:
        //   - Wii 56-byte: AssetCount — typically > 0x29 (any zone with more than a
        //     handful of assets has more than the max asset-type-id of 0x29).
        //   - MW2 Xbox 360 48-byte: first asset's type word — high 3 bytes are zero,
        //     low byte is a small type ID (≤ 0x29).
        bool HasMarker(int offset) =>
            offset + 4 <= header.Length &&
            header[offset] == 0xFF && header[offset + 1] == 0xFF &&
            header[offset + 2] == 0xFF && header[offset + 3] == 0xFF;

        if (!HasMarker(0x2C) || !HasMarker(0x34) || HasMarker(0x28)) return false;
        // Same 56-byte-vs-48-byte heuristic as DetectGameByHeaderShape uses — see
        // Plausible56ByteAssetCount comments for the reasoning.
        return Plausible56ByteAssetCount(header);
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
