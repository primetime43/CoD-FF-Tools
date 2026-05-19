using System.Text;
using FastFileLib;
using FastFileLib.Models;
using Xunit;

namespace FastFileCLI.Tests;

/// <summary>
/// ZoneBuilder layout tests. For each game/platform combo we build a minimal zone
/// and check the bytes at the header offsets that the engine actually uses to load
/// the zone (header size, asset count position, asset table type byte position,
/// endianness). Catches regressions like "wrote a 52-byte BE header for PC" or
/// "used PS3 rawfile asset ID on Xbox 360".
/// </summary>
public class ZoneBuilderPlatformTests
{
    [Fact]
    public void Build_WaWPs3_WritesFiftyTwoByteBeHeaderWithCorrectAssetType()
    {
        byte[] zone = new ZoneBuilder(GameVersion.WaW, "patch_mp", "PS3")
            .AddRawFile(new RawFile("test.gsc", Encoding.ASCII.GetBytes("a")))
            .Build();

        // PS3 layout: 52-byte header, AssetCount @ 0x2C BE, asset table starts @ 0x34.
        // 2 entries (1 rawfile + 1 trailing rawfile sentinel).
        Assert.Equal(2u, ReadUInt32Be(zone, 0x2C));

        // Asset entry type word at 0x34 — BE, low byte = rawfile type.
        // WaW PS3 rawfile = 0x22.
        Assert.Equal(0x22u, ReadUInt32Be(zone, 0x34));
        Assert.Equal(0xFFFFFFFFu, ReadUInt32Be(zone, 0x38));

        // BlockSizeTemp @ 0x08 = WaW PS3 MemAlloc1 (0x10B0).
        Assert.Equal(0x10B0u, ReadUInt32Be(zone, 0x08));
    }

    [Fact]
    public void Build_WaWXbox360_UsesXbox360MemAllocAndRawfileType()
    {
        byte[] zone = new ZoneBuilder(GameVersion.WaW, "patch_mp", "Xbox360")
            .AddRawFile(new RawFile("test.gsc", Encoding.ASCII.GetBytes("a")))
            .Build();

        // Xbox 360 WaW still uses the 52-byte BE layout, but a different MemAlloc1
        // (0x0A90 instead of PS3's 0x10B0) and rawfile asset type shifts -1 (0x21 vs 0x22)
        // because Xbox 360 drops the vertexshader asset slot.
        Assert.Equal(0x0A90u, ReadUInt32Be(zone, 0x08));
        Assert.Equal(0x21u, ReadUInt32Be(zone, 0x34));
    }

    [Fact]
    public void Build_WaWPc_WritesFiftyTwoByteLeHeader()
    {
        byte[] zone = new ZoneBuilder(GameVersion.WaW, "patch_mp", "PC")
            .AddRawFile(new RawFile("test.gsc", Encoding.ASCII.GetBytes("a")))
            .Build();

        // PC: same 52-byte layout but LE size fields.
        // MemAlloc1 stored as `B0 10 00 00` (LE) instead of `00 00 10 B0` (BE).
        Assert.Equal(0xB0u, zone[0x08]);
        Assert.Equal(0x10u, zone[0x09]);
        Assert.Equal(0x00u, zone[0x0A]);
        Assert.Equal(0x00u, zone[0x0B]);

        // AssetCount @ 0x2C LE.
        Assert.Equal(2u, ReadUInt32Le(zone, 0x2C));

        // Asset entry type word @ 0x34 LE — low byte (offset 0) = rawfile type.
        // WaW PC rawfile = 0x20 (PC drops both pixelshader AND vertexshader).
        Assert.Equal(0x20u, zone[0x34]);
        Assert.Equal(0x00u, zone[0x35]);
    }

    [Fact]
    public void Build_WaWWii_WritesFiftySixByteBeHeader()
    {
        byte[] zone = new ZoneBuilder(GameVersion.WaW, "patch_mp", "Wii")
            .AddRawFile(new RawFile("test.gsc", Encoding.ASCII.GetBytes("a")))
            .Build();

        // Wii: 56-byte header (extra BlockSizeIndex slot at 0x24), AssetCount @ 0x30 BE.
        Assert.Equal(2u, ReadUInt32Be(zone, 0x30));

        // Asset table starts @ 0x38, BE, with the PC-style rawfile asset ID (Wii uses PC enum).
        Assert.Equal(0x20u, ReadUInt32Be(zone, 0x38));
    }

    [Fact]
    public void Build_Mw2Ps3_WritesFiftyTwoByteBeHeader()
    {
        byte[] zone = new ZoneBuilder(GameVersion.MW2, "patch_mp", "PS3")
            .AddRawFile(new RawFile("test.gsc", Encoding.ASCII.GetBytes("a")))
            .Build();

        // MW2 PS3 keeps the 52-byte BE layout. AssetCount @ 0x2C, rawfile type 0x23 @ 0x34.
        Assert.Equal(2u, ReadUInt32Be(zone, 0x2C));
        Assert.Equal(0x23u, ReadUInt32Be(zone, 0x34));
        Assert.Equal(0x03B4u, ReadUInt32Be(zone, 0x08));
    }

    [Fact]
    public void Build_Mw2Xbox360_WritesFortyEightByteBeHeader()
    {
        byte[] zone = new ZoneBuilder(GameVersion.MW2, "patch_mp", "Xbox360")
            .AddRawFile(new RawFile("test.gsc", Encoding.ASCII.GetBytes("a")))
            .Build();

        // MW2 Xbox 360 drops BlockSizeVertex entirely → 48-byte header, asset table @ 0x30.
        // AssetCount @ 0x28 BE; rawfile type 0x22 (MW2 Xbox 360, no vertexshader).
        Assert.Equal(2u, ReadUInt32Be(zone, 0x28));
        Assert.Equal(0x22u, ReadUInt32Be(zone, 0x30));

        // 0x20 must NOT be BlockSizeVertex on MW2 Xbox 360 — it's ScriptStringCount (0).
        Assert.Equal(0u, ReadUInt32Be(zone, 0x20));
    }

    [Fact]
    public void Build_Mw2Pc_WritesFiftySixByteLeHeader()
    {
        byte[] zone = new ZoneBuilder(GameVersion.MW2, "patch_mp", "PC")
            .AddRawFile(new RawFile("test.gsc", Encoding.ASCII.GetBytes("a")))
            .Build();

        // MW2 PC: 56-byte LE header (same shape as Wii but flipped endianness).
        // AssetCount @ 0x30 LE, asset table @ 0x38 LE.
        Assert.Equal(2u, ReadUInt32Le(zone, 0x30));

        // MW2 PC rawfile type = 0x24 (MW2 PC adds vertexdecl, shifting IDs +1).
        Assert.Equal(0x24u, zone[0x38]);
        Assert.Equal(0x00u, zone[0x39]);

        // BlockSizeVertex must be 0 on MW2 PC — verified by hex-dumping real retail
        // zones (patch_mp.zone, mp_rust_load.zone). Using PS3's 0x1000 default would
        // diverge from every observed sample.
        Assert.Equal(0u, ReadUInt32Le(zone, 0x20));
    }

    [Fact]
    public void Build_PreservesDefaultPlatform_WhenOmitted()
    {
        // Existing callers (FastFileConverter, older tests) construct ZoneBuilder
        // without specifying platform — they must keep getting the PS3 layout.
        byte[] zonePs3Default = new ZoneBuilder(GameVersion.WaW, "patch_mp")
            .AddRawFile(new RawFile("a.gsc", Encoding.ASCII.GetBytes("x")))
            .Build();
        byte[] zonePs3Explicit = new ZoneBuilder(GameVersion.WaW, "patch_mp", "PS3")
            .AddRawFile(new RawFile("a.gsc", Encoding.ASCII.GetBytes("x")))
            .Build();

        Assert.Equal(zonePs3Explicit, zonePs3Default);
    }

    private static uint ReadUInt32Be(byte[] data, int offset)
        => (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);

    private static uint ReadUInt32Le(byte[] data, int offset)
        => (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
}
