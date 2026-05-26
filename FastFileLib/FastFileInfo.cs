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
    /// Reads FastFile header information from a file. For unsigned BE console FFs
    /// — where the FF header alone can't tell PS3 from Xbox 360 — this also peeks
    /// into the decompressed zone to refine the <see cref="Platform"/> string when
    /// the underlying zone bytes are unambiguous (WaW: distinct MemAlloc1 magic;
    /// MW2: distinct header layout). CoD4 zone bytes are identical between PS3 and
    /// Xbox 360, so CoD4 stays "PS3/Xbox 360".
    /// </summary>
    public static FastFileInfo FromFile(string filePath)
    {
        FastFileInfo info;
        using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
        using (var br = new BinaryReader(fs))
        {
            info = FromReader(br);
        }

        // Refine ambiguous unsigned BE console label by peeking into the zone.
        if (info.Platform == "PS3/Xbox 360")
        {
            string? refined = TryRefinePlatformByZonePeek(filePath, info);
            if (refined != null) info.Platform = refined;
        }
        return info;
    }

    /// <summary>
    /// Peeks the first decompressed block of an unsigned BE console FF to read
    /// the zone header and resolve PS3 vs Xbox 360. Returns null when the zone
    /// bytes can't disambiguate (CoD4: identical MemAlloc) or when peeking fails
    /// (e.g. MW2 Xbox 360 single-zlib-stream format — block peek won't decode).
    /// Failure mode is "leave the ambiguous label alone", never throw.
    /// </summary>
    private static string? TryRefinePlatformByZonePeek(string ffPath, FastFileInfo info)
    {
        try
        {
            byte[]? zoneHead = PeekZoneHeader(ffPath, info, headerBytes: 64);
            if (zoneHead == null || zoneHead.Length < 12) return null;

            // WaW: MemAlloc1 magic is the strongest disambiguator. Real PS3 retail zones
            // can use per-zone MemAlloc (e.g. ui.ff has 0x258 instead of the documented
            // 0x10B0 magic), so we accept a magic match as definitive but also fall through
            // to "PS3" for unsigned non-magic cases — retail Xbox 360 WaW is signed (caught
            // by IsSigned), and converter output from Xbox 360 preserves the 0x0A90 magic.
            if (info.GameVersion == GameVersion.WaW)
            {
                uint memAlloc1BE = (uint)((zoneHead[8] << 24) | (zoneHead[9] << 16)
                                        | (zoneHead[10] << 8) | zoneHead[11]);
                if (memAlloc1BE == CoD5Definition.MemAlloc1Value) return "PS3";
                if (memAlloc1BE == CoD5Definition.Xbox360MemAlloc1Value) return "Xbox 360";
                return "PS3";  // unsigned WaW + non-magic MemAlloc → PS3 retail
            }

            // MW2: header layout differs (PS3 = 52-byte, Xbox 360 = 48-byte). The 48-byte
            // layout has FFFFFFFF at 0x24 + 0x2C but NOT at 0x28; 52-byte has it at 0x28 + 0x30.
            if (info.GameVersion == GameVersion.MW2)
            {
                bool has24 = Has(zoneHead, 0x24);
                bool has28 = Has(zoneHead, 0x28);
                bool has2C = Has(zoneHead, 0x2C);
                bool has30 = Has(zoneHead, 0x30);
                if (has24 && has2C && !has28) return "Xbox 360";
                if (has28 && has30) return "PS3";
            }

            // CoD4: zone MemAlloc and layout are identical between PS3 and Xbox 360. The
            // peek can't tell them apart from zone bytes, but unsigned CoD4 BE is realistically
            // always PS3 (Xbox 360 retail CoD4 is signed → caught by IsSigned).
            if (info.GameVersion == GameVersion.CoD4)
                return "PS3";

            return null;
        }
        catch
        {
            return null;
        }

        static bool Has(byte[] data, int offset) =>
            offset + 4 <= data.Length &&
            data[offset] == 0xFF && data[offset + 1] == 0xFF &&
            data[offset + 2] == 0xFF && data[offset + 3] == 0xFF;
    }

    /// <summary>
    /// Decompresses just the first block of a block-format FF and returns the
    /// first <paramref name="headerBytes"/> bytes of the zone. Used by
    /// <see cref="TryRefinePlatformByZonePeek"/> to inspect the zone header
    /// without writing a full decompression to disk.
    /// </summary>
    private static byte[]? PeekZoneHeader(string ffPath, FastFileInfo info, int headerBytes)
    {
        using var fs = new FileStream(ffPath, FileMode.Open, FileAccess.Read);
        using var br = new BinaryReader(fs);

        // FF header is 12 bytes. MW2 console (PS3/Xbox 360) adds a 25-byte DB_Header
        // (allowOnlineUpdate + fileCreationTime + region + entryCount + fileSizes)
        // before the compressed stream starts.
        long startPos = 12;
        if (info.GameVersion == GameVersion.MW2 && !info.IsPC && !info.IsWii)
            startPos += 25;

        if (startPos + 2 > fs.Length) return null;
        fs.Position = startPos;

        // Read blocks until we have enough zone bytes for the header peek. Most FFs
        // give us 64 KB on the first block; the loop is here for WaW PS3 UI zones
        // where the first blocks can be Treyarch's 0x0000 raw-uncompressed extension
        // (used for already-compressed payloads like Bink video).
        //   length = 1     → end marker, give up
        //   length = 0     → next 64 KB are stored raw (or EOF if file is shorter)
        //   length = N     → next N bytes are raw deflate, decompress them
        const int BlockSize = 0x10000;
        using var collected = new MemoryStream();
        for (int i = 0; i < 8 && collected.Length < headerBytes; i++)
        {
            byte[] lenBytes = br.ReadBytes(2);
            if (lenBytes.Length < 2) return null;
            int chunkLen = (lenBytes[0] << 8) | lenBytes[1];
            if (chunkLen == 1) break;
            if (chunkLen == 0)
            {
                long remaining = fs.Length - fs.Position;
                if (remaining < BlockSize) break;
                byte[] rawBlock = br.ReadBytes(BlockSize);
                collected.Write(rawBlock, 0, rawBlock.Length);
                continue;
            }
            if (chunkLen > 131072) return null;
            if (fs.Position + chunkLen > fs.Length) return null;
            byte[] compressed = br.ReadBytes(chunkLen);
            if (compressed.Length < chunkLen) return null;
            byte[] decompressed = FastFileProcessor.DecompressBlock(compressed);
            collected.Write(decompressed, 0, decompressed.Length);
        }

        byte[] all = collected.ToArray();
        if (all.Length < 12) return null;
        if (all.Length < headerBytes) return all;
        byte[] head = new byte[headerBytes];
        Array.Copy(all, 0, head, 0, headerBytes);
        return head;
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
    /// Header-only detection: signed magic uniquely identifies Xbox 360; unsigned BE
    /// console FFs are genuinely ambiguous from the header bytes alone (PS3 retail and
    /// signed-converted-to-unsigned Xbox 360 produce identical FF headers), so those
    /// return "PS3/Xbox 360" and <see cref="FromFile"/> tries to refine that label by
    /// peeking into the decompressed zone (different MemAlloc1 magic for WaW; different
    /// header layout for MW2). CoD4 can't be refined — its zone bytes are identical
    /// between PS3 and Xbox 360.
    /// </summary>
    /// <param name="version">The version number from the header</param>
    /// <param name="magic">The magic string from the header</param>
    /// <returns>Platform name: "PS3/Xbox 360", "Xbox 360", "PC", "Wii", or "Console"</returns>
    public static string GetPlatform(uint version, string magic)
    {
        // PC versions have specific version numbers
        if (version == CoD4_PC_Version || version == MW2_PC_Version)
            return "PC";

        // Wii versions
        if (version == CoD4_Wii_Version || version == WaW_Wii_Version)
            return "Wii";

        // Signed magic is exclusively Xbox 360 for console games (CoD4/WaW/MW2 console).
        // MW2 PC signed (IWff0100 + LE version) is caught above by the PC version check.
        if (magic == SignedMagic || magic == TreyarchMagic || magic == "IWffs100")
            return "Xbox 360";

        // Unsigned BE console magic is genuinely ambiguous: both PS3 retail and unsigned
        // Xbox 360 (converted from signed, or rare unsigned MW2 SP) produce identical bytes.
        // Report both rather than picking one — callers that need to distinguish should
        // look at the decompressed zone's MemAlloc1 (PS3 / Xbox 360 use different magics).
        if (magic == UnsignedMagic)
            return "PS3/Xbox 360";

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
