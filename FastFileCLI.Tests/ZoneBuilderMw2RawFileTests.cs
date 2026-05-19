using System.Text;
using FastFileLib;
using FastFileLib.Models;
using Xunit;

namespace FastFileCLI.Tests;

/// <summary>
/// Round-trip tests for MW2 rawfile entries. We build a zone with ZoneBuilder and
/// then re-parse it with RawFileScanner — the scanner is the canonical reader, so
/// if it can find the entries and the decompressed payloads match, the on-disk
/// format is correct. Catches regressions like "first MW2 file got 16-byte header
/// instead of 20-byte" or "MW2 PC size fields written BE".
/// </summary>
public class ZoneBuilderMw2RawFileTests
{
    [Fact]
    public void BuildMw2Ps3_TwoRawfiles_RoundTripsThroughScanner()
    {
        var a = new RawFile("animscripts/a.gsc", Encoding.ASCII.GetBytes("// payload a — small text payload\n"));
        var b = new RawFile("animscripts/b.gsc", Encoding.ASCII.GetBytes("// payload b — slightly different content\n"));

        byte[] zone = new ZoneBuilder(GameVersion.MW2, "patch_mp", "PS3")
            .AddRawFile(a).AddRawFile(b).Build();

        var found = RawFileScanner.FindRawFiles(zone, GameVersion.MW2, isPC: false);

        Assert.Equal(2, found.Count);

        // First file on PS3/Xbox 360 MW2 carries an extra leading FF marker (20-byte header).
        Assert.Equal(20, found[0].HeaderSize);
        Assert.Equal(16, found[1].HeaderSize);

        Assert.Equal("animscripts/a.gsc", found[0].Name);
        Assert.Equal("animscripts/b.gsc", found[1].Name);
        Assert.Equal(a.Data, found[0].Data);
        Assert.Equal(b.Data, found[1].Data);

        // Payloads must be zlib-compressed (compressedLen > 0 and < uncompressedLen for any
        // payload long enough to be worth compressing).
        Assert.True(found[0].WasCompressed);
        Assert.True(found[0].CompressedSize > 0);
    }

    [Fact]
    public void BuildMw2Xbox360_FirstFileGetsTwentyByteHeader()
    {
        var rf = new RawFile("test.gsc", Encoding.ASCII.GetBytes("payload"));

        byte[] zone = new ZoneBuilder(GameVersion.MW2, "patch_mp", "Xbox360")
            .AddRawFile(rf).Build();

        var found = RawFileScanner.FindRawFiles(zone, GameVersion.MW2, isPC: false);

        var entry = Assert.Single(found);
        Assert.Equal(20, entry.HeaderSize);
        Assert.Equal("test.gsc", entry.Name);
        Assert.Equal(rf.Data, entry.Data);
    }

    [Fact]
    public void BuildMw2Pc_AllFilesUseSixteenByteHeaderWithLeSizes()
    {
        var a = new RawFile("a.cfg", Encoding.ASCII.GetBytes("seta r_test 1\n"));
        var b = new RawFile("b.cfg", Encoding.ASCII.GetBytes("seta r_test 2\n"));

        byte[] zone = new ZoneBuilder(GameVersion.MW2, "patch_mp", "PC")
            .AddRawFile(a).AddRawFile(b).Build();

        // MW2 PC: 16-byte header always (no 20-byte first-file variant), LE size fields.
        var found = RawFileScanner.FindRawFiles(zone, GameVersion.MW2, isPC: true);

        Assert.Equal(2, found.Count);
        Assert.All(found, e => Assert.Equal(16, e.HeaderSize));
        Assert.Equal(a.Data, found[0].Data);
        Assert.Equal(b.Data, found[1].Data);

        // Reading the same bytes as console (BE) must fail — proves sizes really are LE.
        var foundBe = RawFileScanner.FindRawFiles(zone, GameVersion.MW2, isPC: false);
        Assert.Empty(foundBe);
    }

    [Fact]
    public void BuildMw2_LargePayload_CompressesAndRoundTrips()
    {
        // Generate a payload that should compress well (repeated text). Verifies the
        // CompressionHelper integration and that compressedLen < uncompressedLen actually
        // gets honored by the scanner's compressed-path branch.
        string text = string.Concat(Enumerable.Repeat("function foo() { return 42; }\n", 200));
        var rf = new RawFile("scripts/big.gsc", Encoding.ASCII.GetBytes(text));

        byte[] zone = new ZoneBuilder(GameVersion.MW2, "patch_mp", "PS3")
            .AddRawFile(rf).Build();

        var entry = Assert.Single(RawFileScanner.FindRawFiles(zone, GameVersion.MW2, isPC: false));

        Assert.Equal(rf.Data, entry.Data);
        Assert.True(entry.WasCompressed);
        Assert.True(entry.CompressedSize < rf.Data.Length,
            $"expected compressedSize ({entry.CompressedSize}) < uncompressedLen ({rf.Data.Length}) for repetitive text");
    }

    [Fact]
    public void BuildMw2_AssetCountStillIncludesTrailingSentinel()
    {
        // Sanity: MW2 ZoneBuilder must still emit `count + 1` asset pool entries (the
        // trailing rawfile sentinel). Without it the engine hangs at load. Regression
        // check that adding the MW2 rawfile-entry format didn't accidentally break the
        // asset pool math.
        var rf = new RawFile("a.gsc", Encoding.ASCII.GetBytes("x"));

        byte[] zonePs3  = new ZoneBuilder(GameVersion.MW2, "patch_mp", "PS3").AddRawFile(rf).Build();
        byte[] zone360  = new ZoneBuilder(GameVersion.MW2, "patch_mp", "Xbox360").AddRawFile(rf).Build();
        byte[] zonePc   = new ZoneBuilder(GameVersion.MW2, "patch_mp", "PC").AddRawFile(rf).Build();

        Assert.Equal(2u, ReadUInt32Be(zonePs3, 0x2C));   // 52-byte BE
        Assert.Equal(2u, ReadUInt32Be(zone360, 0x28));   // 48-byte BE (MW2 Xbox 360)
        Assert.Equal(2u, ReadUInt32Le(zonePc, 0x30));    // 56-byte LE (MW2 PC)
    }

    private static uint ReadUInt32Be(byte[] data, int offset)
        => (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);

    private static uint ReadUInt32Le(byte[] data, int offset)
        => (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
}
