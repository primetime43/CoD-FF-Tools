using System.Buffers.Binary;
using System.Text;
using FastFileLib;
using Xunit;

namespace FastFileCLI.Tests;

/// <summary>
/// Synthetic-byte tests for <see cref="IW6GfxImageScanner"/>. Builds
/// minimal IW6 GfxImage structs (80 bytes: a sea of zeros with width/height
/// at the right offsets, MapType, Semantic, then a <c>FFFFFFFF</c> NamePtr
/// placeholder + inline name).
/// </summary>
public class IW6GfxImageScannerTests
{
    private static byte[] Concat(params byte[][] parts)
    {
        var ms = new MemoryStream();
        foreach (var p in parts) ms.Write(p, 0, p.Length);
        return ms.ToArray();
    }

    /// <summary>
    /// Build an 80-byte struct + inline name. <paramref name="width"/> /
    /// <paramref name="height"/> land at struct offset 60 / 62 (u16 BE),
    /// <paramref name="mapType"/> at 24, <paramref name="semantic"/> at 25;
    /// the NamePtr at offset 76 is <c>FFFFFFFF</c>; the name follows
    /// immediately and is null-terminated.
    /// </summary>
    private static byte[] BuildImage(string name, int width, int height,
                                     byte mapType = 3, byte semantic = 2)
    {
        var s = new byte[80];
        s[24] = mapType;
        s[25] = semantic;
        BinaryPrimitives.WriteUInt16BigEndian(s.AsSpan(60, 2), (ushort)width);
        BinaryPrimitives.WriteUInt16BigEndian(s.AsSpan(62, 2), (ushort)height);
        s[76] = 0xFF; s[77] = 0xFF; s[78] = 0xFF; s[79] = 0xFF;
        return Concat(s, Encoding.ASCII.GetBytes(name), new byte[] { 0 });
    }

    [Fact]
    public void Locate_FindsSingleImage()
    {
        byte[] zone = Concat(new byte[0x100], BuildImage("foo/bar_x_col_360", 256, 256), new byte[64]);

        var hits = IW6GfxImageScanner.Locate(zone);

        Assert.Single(hits);
        Assert.Equal("foo/bar_x_col_360", hits[0].Name);
        Assert.Equal(256, hits[0].Width);
        Assert.Equal(256, hits[0].Height);
        Assert.Equal((byte)3, hits[0].MapType);
        Assert.Equal((byte)2, hits[0].Semantic);
    }

    [Fact]
    public void Locate_FindsBackToBackImages()
    {
        byte[] zone = Concat(
            new byte[0x80],
            BuildImage("a/b_x_nml_360", 128, 128, mapType: 3, semantic: 5),
            BuildImage("a/b_x_col_360", 128, 128, mapType: 3, semantic: 2),
            BuildImage("a/c_x_spc_3~deadbeef", 64, 64, mapType: 3, semantic: 8),
            new byte[16]);

        var hits = IW6GfxImageScanner.Locate(zone);

        Assert.Equal(3, hits.Count);
        Assert.Equal("a/b_x_nml_360", hits[0].Name);
        Assert.Equal((byte)5, hits[0].Semantic); // normal
        Assert.Equal("a/b_x_col_360", hits[1].Name);
        Assert.Equal((byte)2, hits[1].Semantic); // color
        Assert.Equal("a/c_x_spc_3~deadbeef", hits[2].Name);
        Assert.Equal((byte)8, hits[2].Semantic); // specular
        Assert.Equal(64, hits[2].Width);
    }

    [Fact]
    public void Locate_RejectsNonPowerOfTwoDimensions()
    {
        byte[] zone = Concat(new byte[0x40], BuildImage("foo_col_360", 100, 100), new byte[16]);
        var hits = IW6GfxImageScanner.Locate(zone);
        Assert.Empty(hits);
    }

    [Fact]
    public void Locate_RejectsOutOfRangeDimensions()
    {
        byte[] zone = Concat(new byte[0x40], BuildImage("foo_col_360", 8192, 256), new byte[16]);
        var hits = IW6GfxImageScanner.Locate(zone);
        Assert.Empty(hits);
    }

    [Fact]
    public void Locate_RejectsImplausibleMapType()
    {
        byte[] zone = Concat(new byte[0x40], BuildImage("foo_col_360", 256, 256, mapType: 99), new byte[16]);
        var hits = IW6GfxImageScanner.Locate(zone);
        Assert.Empty(hits);
    }

    [Fact]
    public void Locate_RejectsTooShortNames()
    {
        byte[] zone = Concat(new byte[0x40], BuildImage("ab", 256, 256), new byte[16]);
        var hits = IW6GfxImageScanner.Locate(zone);
        Assert.Empty(hits);
    }

    [Fact]
    public void Locate_RejectsNamesWithIllegalChars()
    {
        // Name with a space — not in the path-style ASCII alphabet.
        byte[] zone = Concat(new byte[0x40], BuildImage("bad name", 256, 256), new byte[16]);
        var hits = IW6GfxImageScanner.Locate(zone);
        Assert.Empty(hits);
    }

    [Fact]
    public void Locate_SkipsRandomFFRunsWithoutValidStructPattern()
    {
        // Zone full of FF*4 placeholders with garbage names — the width/height
        // check should reject every position.
        var bytes = new byte[0x200];
        for (int i = 0; i < bytes.Length; i++) bytes[i] = 0xFF;
        Assert.Empty(IW6GfxImageScanner.Locate(bytes));
    }

    [Fact]
    public void SemanticName_KnownValues()
    {
        Assert.Equal("color",        IW6GfxImageScanner.SemanticName(2));
        Assert.Equal("normal",       IW6GfxImageScanner.SemanticName(5));
        Assert.Equal("specular",     IW6GfxImageScanner.SemanticName(8));
        Assert.Equal("displacement", IW6GfxImageScanner.SemanticName(12));
        Assert.StartsWith("sem ",    IW6GfxImageScanner.SemanticName(0xFF));
    }

    [Fact]
    public void MapTypeName_KnownValues()
    {
        Assert.Equal("2D",   IW6GfxImageScanner.MapTypeName(3));
        Assert.Equal("cube", IW6GfxImageScanner.MapTypeName(5));
        Assert.StartsWith("mt ", IW6GfxImageScanner.MapTypeName(0x42));
    }
}
