using System.IO.Compression;
using System.Text;
using FastFileLib;
using Xunit;

namespace FastFileCLI.Tests;

/// <summary>
/// Synthetic zone tests for RawFileScanner. We hand-build entry bytes that match
/// the on-disk formats documented in CLAUDE.md / docs/ZoneFileFormat.md and check
/// the scanner finds them. This is the surface that the Compiler GUI relies on
/// when it loads an existing FastFile.
/// </summary>
public class RawFileScannerTests
{
    [Fact]
    public void FindRawFiles_CoD4Ps3_ParsesTwelveByteBeEntry()
    {
        // CoD4/WaW format: [FF FF FF FF][len BE][FF FF FF FF][name\0][data][\0]
        byte[] payload = Encoding.ASCII.GetBytes("level.lookat = 1.0\n");
        byte[] zone = BuildStandardEntry("animscripts/test.gsc", payload);

        var found = RawFileScanner.FindRawFiles(zone, GameVersion.CoD4, isPC: false);

        var entry = Assert.Single(found);
        Assert.Equal("animscripts/test.gsc", entry.Name);
        Assert.Equal(12, entry.HeaderSize);
        Assert.False(entry.WasCompressed);
        Assert.Equal(payload, entry.Data);
    }

    [Fact]
    public void FindRawFiles_WaWPc_ParsesTwelveByteBeEntry()
    {
        // PC WaW zones still use BE size fields in the rawfile header (only the
        // outer zone header switches endianness on PC).
        byte[] payload = Encoding.ASCII.GetBytes("// pc waw\nset r_fullbright 1\n");
        byte[] zone = BuildStandardEntry("scripts/pc_only.cfg", payload);

        var found = RawFileScanner.FindRawFiles(zone, GameVersion.WaW, isPC: true);

        var entry = Assert.Single(found);
        Assert.Equal("scripts/pc_only.cfg", entry.Name);
        Assert.Equal(payload, entry.Data);
    }

    [Fact]
    public void FindRawFiles_Mw2Ps3_ParsesSixteenByteCompressedEntry()
    {
        // MW2 PS3/Xbox 360 subsequent files: 16-byte BE header with zlib payload.
        byte[] payload = Encoding.ASCII.GetBytes("main()\n{\n\tprintln(\"hi\");\n}\n");
        byte[] zone = BuildMw2Entry("maps/mp/_load.gsc", payload, isPC: false, withLeadingMarker: false);

        var found = RawFileScanner.FindRawFiles(zone, GameVersion.MW2, isPC: false);

        var entry = Assert.Single(found);
        Assert.Equal("maps/mp/_load.gsc", entry.Name);
        Assert.Equal(16, entry.HeaderSize);
        Assert.True(entry.WasCompressed);
        Assert.Equal(payload, entry.Data);
        Assert.Equal(payload.Length, entry.DataSize);
        Assert.True(entry.CompressedSize > 0);
        Assert.True(entry.CompressedSize < payload.Length * 2);
    }

    [Fact]
    public void FindRawFiles_Mw2Ps3FirstFile_ParsesTwentyByteHeader()
    {
        // First MW2 rawfile uses a 20-byte header with an extra leading FF marker.
        byte[] payload = Encoding.ASCII.GetBytes("// first file in zone\n");
        byte[] zone = BuildMw2Entry("first.gsc", payload, isPC: false, withLeadingMarker: true);

        var found = RawFileScanner.FindRawFiles(zone, GameVersion.MW2, isPC: false);

        var entry = Assert.Single(found);
        Assert.Equal("first.gsc", entry.Name);
        Assert.Equal(20, entry.HeaderSize);
        Assert.True(entry.WasCompressed);
        Assert.Equal(payload, entry.Data);
    }

    [Fact]
    public void FindRawFiles_Mw2Pc_ReadsSizeFieldsLittleEndian()
    {
        // MW2 PC: 16-byte header with LE size fields and a zlib payload. If the
        // scanner reads BE here, uncompressedLen comes out as e.g. 0x18000000 and
        // the entry is rejected as out-of-range; this test catches that regression.
        byte[] payload = Encoding.ASCII.GetBytes("[\"name\"] = \"value\"\n");
        byte[] zone = BuildMw2Entry("ui_mp/test.menu", payload, isPC: true, withLeadingMarker: false);

        var foundLe = RawFileScanner.FindRawFiles(zone, GameVersion.MW2, isPC: true);
        Assert.Single(foundLe);
        Assert.Equal("ui_mp/test.menu", foundLe[0].Name);
        Assert.Equal(payload, foundLe[0].Data);

        // Sanity check: reading the same zone as console (BE) must NOT spuriously
        // succeed. If it does, our endianness branching isn't actually doing anything.
        var foundBe = RawFileScanner.FindRawFiles(zone, GameVersion.MW2, isPC: false);
        Assert.Empty(foundBe);
    }

    [Fact]
    public void FindRawFiles_MultipleEntries_ReturnedInZoneOrder()
    {
        byte[] a = Encoding.ASCII.GetBytes("first content\n");
        byte[] b = Encoding.ASCII.GetBytes("second content with more bytes\n");
        var zone = new List<byte>();
        zone.AddRange(new byte[16]);  // padding so HeaderOffset > 0
        zone.AddRange(BuildStandardEntry("first.gsc", a));
        zone.AddRange(BuildStandardEntry("second.cfg", b));

        var found = RawFileScanner.FindRawFiles(zone.ToArray(), GameVersion.WaW, isPC: false);

        Assert.Equal(2, found.Count);
        Assert.Equal("first.gsc", found[0].Name);
        Assert.Equal("second.cfg", found[1].Name);
        Assert.True(found[0].HeaderOffset < found[1].HeaderOffset);
    }

    [Fact]
    public void FindRawFiles_EmptyZone_ReturnsEmpty()
    {
        var found = RawFileScanner.FindRawFiles(Array.Empty<byte>(), GameVersion.WaW, isPC: false);
        Assert.Empty(found);
    }

    // ------------ helpers ------------

    private static byte[] BuildStandardEntry(string name, byte[] payload)
    {
        var ms = new MemoryStream();
        ms.Write(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });
        WriteInt32Be(ms, payload.Length);
        ms.Write(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });
        ms.Write(Encoding.ASCII.GetBytes(name));
        ms.WriteByte(0x00);
        ms.Write(payload);
        ms.WriteByte(0x00);
        return ms.ToArray();
    }

    private static byte[] BuildMw2Entry(string name, byte[] payload, bool isPC, bool withLeadingMarker)
    {
        // Compress payload with zlib (full header) — what real MW2 zones store.
        byte[] compressed;
        using (var raw = new MemoryStream())
        {
            using (var zlib = new ZLibStream(raw, CompressionLevel.Optimal, leaveOpen: true))
                zlib.Write(payload, 0, payload.Length);
            compressed = raw.ToArray();
        }

        var ms = new MemoryStream();
        if (withLeadingMarker)
            ms.Write(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });
        ms.Write(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });
        if (isPC)
        {
            WriteInt32Le(ms, compressed.Length);
            WriteInt32Le(ms, payload.Length);
        }
        else
        {
            WriteInt32Be(ms, compressed.Length);
            WriteInt32Be(ms, payload.Length);
        }
        ms.Write(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });
        ms.Write(Encoding.ASCII.GetBytes(name));
        ms.WriteByte(0x00);
        ms.Write(compressed);
        return ms.ToArray();
    }

    private static void WriteInt32Be(Stream s, int value)
    {
        s.WriteByte((byte)((value >> 24) & 0xFF));
        s.WriteByte((byte)((value >> 16) & 0xFF));
        s.WriteByte((byte)((value >> 8) & 0xFF));
        s.WriteByte((byte)(value & 0xFF));
    }

    private static void WriteInt32Le(Stream s, int value)
    {
        s.WriteByte((byte)(value & 0xFF));
        s.WriteByte((byte)((value >> 8) & 0xFF));
        s.WriteByte((byte)((value >> 16) & 0xFF));
        s.WriteByte((byte)((value >> 24) & 0xFF));
    }
}
