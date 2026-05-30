using System.Text;
using FastFileLib;
using FastFileLib.GameDefinitions;
using Xunit;

namespace FastFileCLI.Tests;

/// <summary>
/// Synthetic-byte tests for <see cref="GhostsZoneLayout"/>. All test zones are
/// built by hand so the expected pool offset / type IDs / asset names are
/// directly visible in the source.
/// </summary>
public class GhostsZoneLayoutTests
{
    private static readonly byte[] FF4 = { 0xFF, 0xFF, 0xFF, 0xFF };
    private static readonly byte[] FF8 = { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
    private static readonly byte[] Zero4 = { 0x00, 0x00, 0x00, 0x00 };

    private static byte[] Be32(uint v) => new[] {
        (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v };

    private static byte[] PoolRecord(byte typeId, byte[] ptr)
    {
        var rec = new byte[8];
        Array.Copy(ptr, 0, rec, 0, 4);
        rec[7] = typeId;
        return rec;
    }

    private static byte[] Concat(params byte[][] parts)
    {
        var ms = new MemoryStream();
        foreach (var p in parts) ms.Write(p, 0, p.Length);
        return ms.ToArray();
    }

    /// <summary>
    /// Build a minimal-looking XFile header (56 bytes) whose only meaningful
    /// fields are tagCount @0x28 and assetCount @0x30.
    /// </summary>
    private static byte[] Header(uint tagCount, uint assetCount)
    {
        var h = new byte[0x38];
        Array.Copy(Be32(tagCount),   0, h, GhostsZoneLayout.OffsetTagCount,   4);
        Array.Copy(Be32(assetCount), 0, h, GhostsZoneLayout.OffsetAssetCount, 4);
        return h;
    }

    // =========================================================
    // Pool location
    // =========================================================

    [Fact]
    public void LocatePool_PatchFFShape_PoolStartsAt0x38()
    {
        // tagCount=0, assetCount=2. Pool sits immediately at 0x38.
        byte[] zone = Concat(
            Header(0, 2),
            PoolRecord(0x28, FF4),   // rawfile
            PoolRecord(0x29, Zero4), // scriptfile with NULL pointer (real-world pattern)
            new byte[1024]);

        int poolStart = GhostsZoneLayout.LocatePool(zone);
        Assert.Equal(0x38, poolStart);
    }

    [Fact]
    public void LocatePool_DlcShape_SkipsTagPlaceholdersAndStrings()
    {
        // tagCount=3, assetCount=1. Pool is past header + placeholders + strings.
        byte[] placeholders = Concat(FF4, FF4, FF4); // 3 × 4 bytes
        byte[] strings_ = Concat(
            new byte[] { (byte)'j', (byte)'_', (byte)'a', 0 },           // j_a\0
            new byte[] { (byte)'j', (byte)'_', (byte)'b', (byte)'c', 0 }, // j_bc\0
            new byte[] { (byte)'t', (byte)'a', (byte)'g', 0 });           // tag\0
        byte[] zone = Concat(
            Header(3, 1),                            // 56-byte header
            Zero4,                                   // count3=0
            placeholders,                            // tag pointer placeholders
            strings_,                                // tag strings
            PoolRecord(0x28, FF4),                   // pool entry
            new byte[256]);

        int poolStart = GhostsZoneLayout.LocatePool(zone);
        int expected = 0x38 + 4 /*count3*/ + placeholders.Length + strings_.Length;
        Assert.Equal(expected, poolStart);
    }

    [Fact]
    public void LocatePool_DlcShape_ToleratesTrailingPaddingBeforePool()
    {
        // Real DLC zones have ~4 trailing bytes between tag strings and pool start.
        // The probe-forward window should find the pool anyway.
        byte[] placeholders = Concat(FF4, FF4);
        byte[] strings_ = Concat(
            new byte[] { (byte)'a', 0 },
            new byte[] { (byte)'b', 0 });
        byte[] padding = new byte[] { 0x00, 0x00, 0x00, 0x30 }; // observed real-world trailing field
        byte[] zone = Concat(
            Header(2, 1),
            Zero4,
            placeholders,
            strings_,
            padding,
            PoolRecord(0x05, FF4), // material
            new byte[256]);

        int poolStart = GhostsZoneLayout.LocatePool(zone);
        int expected = 0x38 + 4 + placeholders.Length + strings_.Length + padding.Length;
        Assert.Equal(expected, poolStart);
    }

    [Fact]
    public void LocatePool_AvoidsFalsePositiveInHeader()
    {
        // The XFile header's [FFFFFFFF][00 00 00 00] at 0x34..0x3B looks like
        // one physpreset pool entry (type 0x00). Without header-counts driving
        // the search, the locator would lock onto that and stop.
        byte[] header = Header(0, 4);
        byte[] zone = Concat(
            header,
            PoolRecord(0x28, FF4),
            PoolRecord(0x28, FF4),
            PoolRecord(0x28, FF4),
            PoolRecord(0x29, Zero4),
            new byte[256]);

        int poolStart = GhostsZoneLayout.LocatePool(zone);
        var entries = GhostsZoneLayout.WalkPool(zone, poolStart, 4);
        Assert.Equal(4, entries.Count);
        Assert.Equal(GhostsAssetTypePS3.scriptfile, entries[3].Type);
    }

    // =========================================================
    // Pool walking + pointer conventions
    // =========================================================

    [Fact]
    public void WalkPool_ReadsAllThreePointerConventions()
    {
        byte[] header = Header(0, 3);
        byte[] resolvedPtr = { 0x82, 0x57, 0x9F, 0xFE }; // high-bit-set
        byte[] zone = Concat(
            header,
            PoolRecord(0x28, FF4),         // Placeholder
            PoolRecord(0x29, Zero4),       // Null
            PoolRecord(0x05, resolvedPtr), // Resolved
            new byte[256]);

        var entries = GhostsZoneLayout.ParsePool(zone, out int poolStart, out int poolEnd);
        Assert.Equal(0x38, poolStart);
        Assert.Equal(3, entries.Count);
        Assert.Equal(GhostsPointerKind.Placeholder, entries[0].PointerKind);
        Assert.Equal(GhostsPointerKind.Null,        entries[1].PointerKind);
        Assert.Equal(GhostsPointerKind.Resolved,    entries[2].PointerKind);
        Assert.Equal(0x38 + 3 * 8, poolEnd);
    }

    [Fact]
    public void IsPoolEntry_RejectsTypeIdAboveMax()
    {
        byte[] zone = Concat(Header(0, 0), PoolRecord(0x36, FF4));
        Assert.False(GhostsZoneLayout.IsPoolEntry(zone, 0x38));
    }

    [Fact]
    public void IsPoolEntry_RejectsNonZeroHighTypeBytes()
    {
        // Type word with high bytes non-zero shouldn't be accepted.
        byte[] zone = Concat(Header(0, 0), FF4, new byte[] { 0x01, 0x00, 0x00, 0x28 });
        Assert.False(GhostsZoneLayout.IsPoolEntry(zone, 0x38));
    }

    // =========================================================
    // Wrapped asset header scan
    // =========================================================

    [Fact]
    public void LocateAllHeaders_FindsShortShape()
    {
        // [FF*4][compLen=10][decLen=5][FF*4]name\0body(5 bytes)
        byte[] body = { 1, 2, 3, 4, 5 };
        byte[] name = { (byte)'r', (byte)'a', (byte)'w', 0 };
        byte[] entry = Concat(FF4, Be32(10), Be32((uint)body.Length), FF4, name, body);
        byte[] zone = Concat(new byte[0x100], entry, new byte[256]);

        var headers = GhostsZoneLayout.LocateAllHeaders(zone, 0x100);
        Assert.Single(headers);
        Assert.Equal("raw", headers[0].Name);
        Assert.Equal(5, headers[0].DecompressedLen);
        Assert.False(headers[0].IsLong);
    }

    [Fact]
    public void LocateAllHeaders_FindsLongShape()
    {
        // [FF*4][compLen=20][decLen=4][??? u32][FF*8]name\0body(4 bytes)
        byte[] body = { 0x11, 0x22, 0x33, 0x44 };
        byte[] name = { (byte)'s', (byte)'c', (byte)'r', (byte)'i', (byte)'p', (byte)'t', 0 };
        byte[] extraU32 = { 0xDE, 0xAD, 0xBE, 0xEF };
        byte[] entry = Concat(FF4, Be32(20), Be32((uint)body.Length), extraU32, FF8, name, body);
        byte[] zone = Concat(new byte[0x40], entry, new byte[256]);

        var headers = GhostsZoneLayout.LocateAllHeaders(zone, 0x40);
        Assert.Single(headers);
        Assert.Equal("script", headers[0].Name);
        Assert.Equal(4, headers[0].DecompressedLen);
        Assert.True(headers[0].IsLong);
    }

    [Fact]
    public void LocateAllHeaders_LongShapePreferredWhenAmbiguous()
    {
        // Long shape comes first in TryParseHeader because a short shape
        // followed by 4 bytes of FF in the name region could otherwise be
        // misread as long. Make sure long wins when both shape's trailing-FF
        // patterns are present.
        byte[] body = { 0xAA };
        byte[] name = { (byte)'a', 0 };
        // Build 8 trailing FFs at off+16, then name immediately.
        byte[] entry = Concat(FF4, Be32(2), Be32((uint)body.Length), Zero4, FF8, name, body);
        byte[] zone = Concat(new byte[0x40], entry, new byte[64]);

        var headers = GhostsZoneLayout.LocateAllHeaders(zone, 0x40);
        Assert.Single(headers);
        Assert.True(headers[0].IsLong);
    }

    [Fact]
    public void LocateAllHeaders_SkipsImpossibleSizes()
    {
        // compLen = 0 — should be rejected.
        byte[] entry = Concat(FF4, Be32(0), Be32(10), FF4, new byte[] { (byte)'x', 0 }, new byte[10]);
        byte[] zone = Concat(new byte[0x40], entry, new byte[64]);

        var headers = GhostsZoneLayout.LocateAllHeaders(zone, 0x40);
        Assert.Empty(headers);
    }

    [Fact]
    public void LocateAllHeaders_StridesPastInflatedBody()
    {
        // Two back-to-back short-shape entries; the second must be found via
        // stride past the first (not via brute scan over the first's body).
        byte[] body1 = new byte[64];
        for (int i = 0; i < body1.Length; i++) body1[i] = 0xAB;
        byte[] body2 = { 9, 9, 9 };
        byte[] e1 = Concat(FF4, Be32(40), Be32((uint)body1.Length), FF4,
            new byte[] { (byte)'a', 0 }, body1);
        byte[] e2 = Concat(FF4, Be32(15), Be32((uint)body2.Length), FF4,
            new byte[] { (byte)'b', 0 }, body2);
        byte[] zone = Concat(new byte[0x40], e1, e2, new byte[16]);

        var headers = GhostsZoneLayout.LocateAllHeaders(zone, 0x40);
        Assert.Equal(2, headers.Count);
        Assert.Equal("a", headers[0].Name);
        Assert.Equal("b", headers[1].Name);
    }

    // =========================================================
    // Pool ↔ header pairing
    // =========================================================

    [Fact]
    public void PairPoolWithHeaders_OnlyWrappedTypesConsumeHeaders()
    {
        // Pool order: xmodel (flat), rawfile (wrapped), techset (flat), scriptfile (wrapped).
        // Located headers (in zone order): "asset_one", "asset_two".
        // Pairing should give: rawfile ↔ asset_one, scriptfile ↔ asset_two.
        var pool = new[]
        {
            new GhostsPoolEntry(0, GhostsAssetTypePS3.xmodel,     GhostsPointerKind.Placeholder),
            new GhostsPoolEntry(8, GhostsAssetTypePS3.rawfile,    GhostsPointerKind.Placeholder),
            new GhostsPoolEntry(16, GhostsAssetTypePS3.techset,   GhostsPointerKind.Placeholder),
            new GhostsPoolEntry(24, GhostsAssetTypePS3.scriptfile, GhostsPointerKind.Null),
        };
        var headers = new[]
        {
            new GhostsAssetHeader(100, 120, 130, 5, 10, "asset_one.txt", isLong: false),
            new GhostsAssetHeader(200, 220, 230, 6, 10, "asset_two.txt", isLong: true),
        };

        var pairing = GhostsZoneLayout.PairPoolWithHeaders(pool, headers);

        Assert.Equal(2, pairing.PairedCount);
        Assert.Equal(0, pairing.UnpairedHeaders);
        Assert.Equal(-1, pairing.PoolToHeader[0]); // xmodel skipped
        Assert.Equal(0,  pairing.PoolToHeader[1]); // rawfile → asset_one
        Assert.Equal(-1, pairing.PoolToHeader[2]); // techset skipped
        Assert.Equal(1,  pairing.PoolToHeader[3]); // scriptfile → asset_two
    }

    [Fact]
    public void PairPoolWithHeaders_ImageNamedHeadersGoToImagePool()
    {
        // Pool: rawfile, image, rawfile, image (in pool order).
        // Headers (in zone order): foo.txt (rawfile-ish), bar.jpg (image-ish),
        //                          baz.lua (rawfile-ish), qux.png (image-ish).
        // Without name-awareness: foo→rawfile[0], bar→rawfile[2], image entries blank.
        // With name-awareness: foo→rawfile[0], bar→image[1], baz→rawfile[2], qux→image[3].
        var pool = new[]
        {
            new GhostsPoolEntry(0,  GhostsAssetTypePS3.rawfile, GhostsPointerKind.Placeholder),
            new GhostsPoolEntry(8,  GhostsAssetTypePS3.image,   GhostsPointerKind.Placeholder),
            new GhostsPoolEntry(16, GhostsAssetTypePS3.rawfile, GhostsPointerKind.Placeholder),
            new GhostsPoolEntry(24, GhostsAssetTypePS3.image,   GhostsPointerKind.Placeholder),
        };
        var headers = new[]
        {
            new GhostsAssetHeader(100, 110, 120, 5, 10, "vision/foo.vision", isLong: false),
            new GhostsAssetHeader(200, 210, 220, 5, 10, "ui_mp/ingamestore/img_bar.jpg", isLong: false),
            new GhostsAssetHeader(300, 310, 320, 5, 10, "scripts/baz.gsc", isLong: false),
            new GhostsAssetHeader(400, 410, 420, 5, 10, "ui_mp/qux.png", isLong: false),
        };

        var pairing = GhostsZoneLayout.PairPoolWithHeaders(pool, headers);

        Assert.Equal(4, pairing.PairedCount);
        Assert.Equal(0, pairing.UnpairedHeaders);
        Assert.Equal(0, pairing.PoolToHeader[0]); // rawfile[0] → foo.vision
        Assert.Equal(1, pairing.PoolToHeader[1]); // image[1]   → img_bar.jpg
        Assert.Equal(2, pairing.PoolToHeader[2]); // rawfile[2] → baz.gsc
        Assert.Equal(3, pairing.PoolToHeader[3]); // image[3]   → qux.png
    }

    [Fact]
    public void PairPoolWithHeaders_SpillsToOtherBucketWhenPrimaryExhausted()
    {
        // Pool has only rawfile entries but headers include a JPG. The JPG
        // should spill to a rawfile slot rather than getting stranded.
        var pool = new[]
        {
            new GhostsPoolEntry(0, GhostsAssetTypePS3.rawfile, GhostsPointerKind.Placeholder),
            new GhostsPoolEntry(8, GhostsAssetTypePS3.rawfile, GhostsPointerKind.Placeholder),
        };
        var headers = new[]
        {
            new GhostsAssetHeader(100, 110, 120, 5, 10, "foo.txt", isLong: false),
            new GhostsAssetHeader(200, 210, 220, 5, 10, "img.jpg", isLong: false),
        };

        var pairing = GhostsZoneLayout.PairPoolWithHeaders(pool, headers);

        Assert.Equal(2, pairing.PairedCount);
        Assert.Equal(0, pairing.UnpairedHeaders);
        Assert.Equal(0, pairing.PoolToHeader[0]); // rawfile → foo.txt
        Assert.Equal(1, pairing.PoolToHeader[1]); // rawfile (spill) → img.jpg
    }

    [Fact]
    public void IsWrappedType_OnlyKnownWrappedTypes()
    {
        Assert.True(GhostsZoneLayout.IsWrappedType(GhostsAssetTypePS3.rawfile));
        Assert.True(GhostsZoneLayout.IsWrappedType(GhostsAssetTypePS3.scriptfile));
        Assert.True(GhostsZoneLayout.IsWrappedType(GhostsAssetTypePS3.mptype));
        Assert.True(GhostsZoneLayout.IsWrappedType(GhostsAssetTypePS3.aitype));

        Assert.False(GhostsZoneLayout.IsWrappedType(GhostsAssetTypePS3.xmodel));
        Assert.False(GhostsZoneLayout.IsWrappedType(GhostsAssetTypePS3.image));
        Assert.False(GhostsZoneLayout.IsWrappedType(GhostsAssetTypePS3.techset));
        Assert.False(GhostsZoneLayout.IsWrappedType(GhostsAssetTypePS3.weapon));
    }

    // =========================================================
    // Permissive pointer convention (0x40-flagged)
    // =========================================================

    [Fact]
    public void WalkPool_AcceptsZero40FlaggedPointer()
    {
        // IW6 patch_ui_mp.zone has pool entries like [40 1F DF 85][00 00 00 05]
        // (material ptr with the 0x40000000 flag bit set). Verify they parse.
        byte[] flaggedPtr = { 0x40, 0x1F, 0xDF, 0x85 };
        byte[] zone = Concat(
            Header(0, 2),
            PoolRecord(0x05, flaggedPtr), // material with 0x40-flagged ptr
            PoolRecord(0x09, FF4),        // image with placeholder
            new byte[256]);

        var entries = GhostsZoneLayout.ParsePool(zone, out int poolStart, out _);
        Assert.Equal(0x38, poolStart);
        Assert.Equal(2, entries.Count);
        Assert.Equal(GhostsPointerKind.Resolved, entries[0].PointerKind);
        Assert.Equal(GhostsAssetTypePS3.material, entries[0].Type);
    }

    [Fact]
    public void WalkPool_HeaderCountCapsAtAssetCount()
    {
        // Make the bytes past `assetCount` entries also look like a pool entry.
        // The header-driven cap should stop the walk at the declared count.
        byte[] zone = Concat(
            Header(0, 2),
            PoolRecord(0x28, FF4),
            PoolRecord(0x29, Zero4),
            PoolRecord(0x05, FF4), // extra would be walkable but capped
            new byte[256]);

        var entries = GhostsZoneLayout.ParsePool(zone, out _, out _);
        Assert.Equal(2, entries.Count);
    }

    // =========================================================
    // Luafile scan
    // =========================================================

    private static byte[] LuaFile(string name, byte[] body)
    {
        var nameBytes = Encoding.ASCII.GetBytes(name);
        var nullTerm = new byte[] { 0 };
        var unk = Be32(0x02000000); // observed-fixed value in real zones
        return Concat(FF4, Be32((uint)body.Length), unk, FF4, nameBytes, nullTerm, body);
    }

    private static byte[] LuaBytecode(int totalSize)
    {
        var b = new byte[totalSize];
        b[0] = 0x1B; b[1] = (byte)'L'; b[2] = (byte)'u'; b[3] = (byte)'a'; b[4] = 0x51;
        return b;
    }

    [Fact]
    public void LocateAllLuaFiles_FindsSingleEntry()
    {
        byte[] zone = Concat(new byte[0x100], LuaFile("ui/main.lua", LuaBytecode(128)), new byte[256]);

        var lua = GhostsZoneLayout.LocateAllLuaFiles(zone, 0x100);

        Assert.Single(lua);
        Assert.Equal("ui/main.lua", lua[0].Name);
        Assert.Equal(128, lua[0].ByteCodeLen);
        Assert.Equal(0x100, lua[0].HeaderOffset);
    }

    [Fact]
    public void LocateAllLuaFiles_FindsConsecutiveEntries()
    {
        // Three back-to-back luafiles. Stride should walk from one to the next.
        byte[] zone = Concat(
            new byte[0x40],
            LuaFile("a.lua", LuaBytecode(64)),
            LuaFile("ui/b.lua", LuaBytecode(96)),
            LuaFile("c.lua", LuaBytecode(48)),
            new byte[16]);

        var lua = GhostsZoneLayout.LocateAllLuaFiles(zone, 0x40);

        Assert.Equal(3, lua.Count);
        Assert.Equal("a.lua",    lua[0].Name);
        Assert.Equal("ui/b.lua", lua[1].Name);
        Assert.Equal("c.lua",    lua[2].Name);
    }

    [Fact]
    public void LocateAllLuaFiles_RejectsNonLuaSuffix()
    {
        // The 16-byte header pattern looks like luafile but the name doesn't
        // end in .lua, so it must be rejected (would otherwise collide with
        // other flat-header asset types in the same zone).
        byte[] zone = Concat(new byte[0x40], LuaFile("ui/main.txt", LuaBytecode(64)), new byte[256]);

        var lua = GhostsZoneLayout.LocateAllLuaFiles(zone, 0x40);

        Assert.Empty(lua);
    }

    [Fact]
    public void LocateAllLuaFiles_RejectsMissingLuaSignature()
    {
        // Header + name look fine but the body doesn't start with \x1B LuaQ.
        // The signature check is the primary defense against false positives.
        byte[] nameBytes = Encoding.ASCII.GetBytes("ui/main.lua");
        byte[] notLua = new byte[64]; // all zeros, no Lua magic
        byte[] entry = Concat(FF4, Be32(64), Be32(0x02000000), FF4, nameBytes, new byte[] { 0 }, notLua);
        byte[] zone = Concat(new byte[0x40], entry, new byte[256]);

        var lua = GhostsZoneLayout.LocateAllLuaFiles(zone, 0x40);

        Assert.Empty(lua);
    }

    [Fact]
    public void LocateAllLuaFiles_SkipsPastNonMatchBytes()
    {
        // Pad with random bytes between two luafiles. The scan must byte-walk
        // through the gap and recover the second entry.
        byte[] zone = Concat(
            new byte[0x40],
            LuaFile("a.lua", LuaBytecode(32)),
            new byte[] { 0xAB, 0xCD, 0xEF, 0x12, 0x34 }, // 5 bytes of junk
            LuaFile("b.lua", LuaBytecode(48)),
            new byte[16]);

        var lua = GhostsZoneLayout.LocateAllLuaFiles(zone, 0x40);

        Assert.Equal(2, lua.Count);
        Assert.Equal("a.lua", lua[0].Name);
        Assert.Equal("b.lua", lua[1].Name);
    }
}
