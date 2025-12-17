using System.Text;

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
    public string GameName { get; set; } = "Unknown";
    public string[] Platforms { get; set; } = Array.Empty<string>();
    public int HeaderSize { get; set; }

    // Header magic constants
    public const string UnsignedMagic = "IWffu100";
    public const string SignedMagic = "IWff0100";
    public const string TreyarchMagic = "TAff0100";

    // Version constants
    public const uint CoD4_PS3_Version = 0x01;
    public const uint CoD4_PC_Version = 0x05;
    public const uint CoD4_Wii_Version = 0x1A2;
    public const uint WaW_Console_PC_Version = 0x183;
    public const uint WaW_Wii_Version = 0x19B;
    public const uint MW2_Console_Version = 0x10D;
    public const uint MW2_PC_Version = 0x114;

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
        info.Version = (uint)((versionBytes[0] << 24) | (versionBytes[1] << 16) |
                              (versionBytes[2] << 8) | versionBytes[3]);

        // Determine if signed
        info.IsSigned = info.Magic == SignedMagic || info.Magic == TreyarchMagic;

        // Detect game version and set header size
        DetectGameVersion(info);

        return info;
    }

    private static void DetectGameVersion(FastFileInfo info)
    {
        switch (info.Version)
        {
            case CoD4_PS3_Version:
                info.GameVersion = GameVersion.CoD4;
                info.GameName = "CoD4";
                info.Platforms = new[] { "PS3", "Xbox 360" };
                info.HeaderSize = 12;
                break;
            case CoD4_PC_Version:
                info.GameVersion = GameVersion.CoD4;
                info.GameName = "CoD4";
                info.Platforms = new[] { "PC" };
                info.HeaderSize = 12;
                break;
            case CoD4_Wii_Version:
                info.GameVersion = GameVersion.CoD4;
                info.GameName = "CoD4";
                info.Platforms = new[] { "Wii" };
                info.HeaderSize = 12;
                break;
            case WaW_Console_PC_Version:
                info.GameVersion = GameVersion.WaW;
                info.GameName = "WaW";
                info.Platforms = new[] { "PS3", "Xbox 360", "PC" };
                info.HeaderSize = 12;
                break;
            case WaW_Wii_Version:
                info.GameVersion = GameVersion.WaW;
                info.GameName = "WaW";
                info.Platforms = new[] { "Wii" };
                info.HeaderSize = 12;
                break;
            case MW2_Console_Version:
                info.GameVersion = GameVersion.MW2;
                info.GameName = "MW2";
                info.Platforms = new[] { "PS3", "Xbox 360" };
                info.HeaderSize = -1; // Variable, needs to be calculated
                break;
            case MW2_PC_Version:
                info.GameVersion = GameVersion.MW2;
                info.GameName = "MW2";
                info.Platforms = new[] { "PC" };
                info.HeaderSize = -1; // Variable, needs to be calculated
                break;
            default:
                info.GameVersion = GameVersion.Unknown;
                info.GameName = "Unknown";
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
}
