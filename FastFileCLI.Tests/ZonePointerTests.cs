using FastFileLib;
using Xunit;

namespace FastFileCLI.Tests;

/// <summary>
/// Tests for the IW4 zone-pointer model (<see cref="ZonePointer"/> / <see cref="ZoneBlockLayout"/>),
/// ported from Jacob Schroeder's FastFile (https://github.com/jacob-schroeder/FastFile).
///
/// The block-base oracle is a real MW2 PS3 zone
/// (patch_mp - Elite Mossy v1 1.14): XFile.Size = 1,439,165, blockSize[LARGE] = 1,367,070,
/// blockSize[TEMP] = 948, blockSize[VERTEX] = 4,096, all others 0.
/// </summary>
public class ZonePointerTests
{
    // ---- decode ----

    [Fact]
    public void Zero_IsNull()
    {
        var p = new ZonePointer(0);
        Assert.Equal(ZonePointerKind.Null, p.Kind);
    }

    [Fact]
    public void MinusOne_IsInline()
    {
        var p = new ZonePointer(-1);
        Assert.Equal(ZonePointerKind.Inline, p.Kind);
        Assert.True(p.IsInlineData);
        Assert.False(p.IsInsert);
    }

    [Fact]
    public void MinusTwo_IsInsert()
    {
        // -2 is the EBOOT insert marker (inline data + reserved block-4 alias cell), distinct from -1.
        var p = new ZonePointer(-2);
        Assert.Equal(ZonePointerKind.Insert, p.Kind);
        Assert.True(p.IsInsert);
        Assert.True(p.IsInlineData);
    }

    [Fact]
    public void UnsignedInlineAndInsertMarkers_Decode()
    {
        Assert.Equal(ZonePointerKind.Inline, new ZonePointer(0xFFFFFFFFu).Kind);
        Assert.Equal(ZonePointerKind.Insert, new ZonePointer(0xFFFFFFFEu).Kind);
    }

    [Fact]
    public void OffsetPointer_StripsPlusOneBiasThenSplitsTopNibbleAndLow28Bits()
    {
        // EBOOT: stored value 0x40000101 -> minus 1 -> 0x40000100 -> block 4 (LARGE), offset 0x100.
        var p = new ZonePointer(0x40000101);
        Assert.Equal(ZonePointerKind.Offset, p.Kind);
        Assert.Equal(4, p.StreamBlockIndex);
        Assert.Equal(0x100, p.Offset);
    }

    [Fact]
    public void OffsetPointer_HighBitDoesNotLeakIntoOffset()
    {
        // The old WaW heuristic masked 0x7FFFFFFF; the correct IW4 decode is (raw-1) then >>28 / &0x0FFFFFFF.
        // stored 0x12345678 -> 0x12345677 -> block 1, offset 0x2345677.
        var p = new ZonePointer(0x12345678);
        Assert.Equal(0x1, p.StreamBlockIndex);
        Assert.Equal(0x2345677, p.Offset);
    }

    [Fact]
    public void Encode_IsInverseOfOffsetDecode_AndCarriesPlusOneBias()
    {
        int raw = ZonePointer.Encode((int)ZoneStreamBlock.Large, 0x100);
        Assert.Equal(0x40000101, raw);

        var p = new ZonePointer(raw);
        Assert.Equal(4, p.StreamBlockIndex);
        Assert.Equal(0x100, p.Offset);
    }

    // ---- block bases (real MW2 PS3 numbers) ----

    private const int RealXFileSize = 1_439_165;
    private static int[] RealBlockSizes() => new[]
    {
        948,        // 0 TEMP
        0,          // 1 PHYSICAL
        0,          // 2 RUNTIME
        0,          // 3 VIRTUAL
        1_367_070,  // 4 LARGE
        0,          // 5 CALLBACK
        4_096,      // 6 VERTEX
    };

    [Fact]
    public void LargeBlockBase_EqualsSizeMinusLargeBlockSize()
    {
        var layout = new ZoneBlockLayout(RealXFileSize, RealBlockSizes());

        // The friend's formula: base[LARGE] = XFile.Size - blockSize[LARGE].
        Assert.Equal(RealXFileSize - 1_367_070, layout.BaseOf(ZoneStreamBlock.Large));
        Assert.Equal(72_095, layout.BaseOf(ZoneStreamBlock.Large));
    }

    [Fact]
    public void NonLargeBlocks_AreLaidOutSequentiallyFromFrontOfBlockRegion()
    {
        var layout = new ZoneBlockLayout(RealXFileSize, RealBlockSizes());

        // position starts at Size - sum(blocks) = 1,439,165 - 1,372,114 = 67,051
        Assert.Equal(67_051, layout.BaseOf(ZoneStreamBlock.Temp));
        // after TEMP (948), the zero-sized blocks share the same base, VERTEX sits at +948
        Assert.Equal(67_999, layout.BaseOf(ZoneStreamBlock.Vertex));
    }

    [Fact]
    public void Resolve_OffsetPointerIntoLarge_LandsAtBasePlusOffset()
    {
        var layout = new ZoneBlockLayout(RealXFileSize, RealBlockSizes());
        int raw = ZonePointer.Encode((int)ZoneStreamBlock.Large, 0x100);

        Assert.True(layout.TryResolve(raw, out int physical));
        Assert.Equal(72_095 + 0x100, physical);
    }

    [Fact]
    public void Resolve_RejectsNullInlineAndOutOfRange()
    {
        var layout = new ZoneBlockLayout(RealXFileSize, RealBlockSizes());

        Assert.False(layout.TryResolve(new ZonePointer(0), out _));            // null
        Assert.False(layout.TryResolve(new ZonePointer(-1), out _));           // inline
        // offset past the LARGE block's allocation
        Assert.False(layout.TryResolve(ZonePointer.Encode((int)ZoneStreamBlock.Large, 1_367_070), out _));
        // a block that has no allocation (PHYSICAL = 0)
        Assert.False(layout.TryResolve(ZonePointer.Encode((int)ZoneStreamBlock.Physical, 0), out _));
    }

    // ---- FromZoneHeader (synthetic 52-byte big-endian MW2 header) ----

    [Fact]
    public void FromZoneHeader_ReadsSizeAndBlocks_BigEndian()
    {
        var zone = new byte[52];
        WriteBE(zone, 0x00, (uint)RealXFileSize); // Size
        WriteBE(zone, 0x04, 0);                   // ExternalSize
        WriteBE(zone, 0x08, 948);                 // TEMP
        WriteBE(zone, 0x18, 1_367_070);           // LARGE
        WriteBE(zone, 0x20, 4_096);               // VERTEX

        var layout = ZoneBlockLayout.FromZoneHeader(zone, GameVersion.MW2, isXbox360: false, isPC: false, isWii: false);

        Assert.NotNull(layout);
        Assert.Equal(RealXFileSize, layout!.XFileSize);
        Assert.Equal(72_095, layout.BaseOf(ZoneStreamBlock.Large));
    }

    private static void WriteBE(byte[] buf, int offset, uint value)
    {
        buf[offset + 0] = (byte)(value >> 24);
        buf[offset + 1] = (byte)(value >> 16);
        buf[offset + 2] = (byte)(value >> 8);
        buf[offset + 3] = (byte)value;
    }
}

/// <summary>
/// Tests for the EBOOT-proofed Direct/Alias offset-pointer resolution table
/// (<see cref="ZonePointerResolution"/>), from Jacob Schroeder's PS3 EBOOT trace.
/// </summary>
public class ZonePointerResolutionTests
{
    [Theory]
    // Root asset header pointers are alias cells.
    [InlineData("XAsset.Header", PointerResolutionKind.Alias)]
    // Strings and rawfile buffers are direct.
    [InlineData("XString", PointerResolutionKind.Direct)]
    [InlineData("RawFile.Buffer", PointerResolutionKind.Direct)]
    // Material reaches its techset/image through the asset wrapper (alias) but owns its tables (direct).
    [InlineData("Material.TechniqueSet", PointerResolutionKind.Alias)]
    [InlineData("MaterialTextureDef.Image", PointerResolutionKind.Alias)]
    [InlineData("Material.TextureTable", PointerResolutionKind.Direct)]
    // Menu event-handler unconditional script is direct (XString path), traced via EBOOT 0x0010C160.
    [InlineData("MenuEventHandler.UnconditionalScript", PointerResolutionKind.Direct)]
    // Weapon: the array spine is direct; per-element xmodel refs go through the alias wrapper.
    [InlineData("WeaponDef.GunXModel", PointerResolutionKind.Direct)]
    [InlineData("WeaponDef.GunXModel.Element", PointerResolutionKind.Alias)]
    [InlineData("WeaponDef.szXAnimsR.Element", PointerResolutionKind.Direct)]
    [InlineData("StringList.Strings.Element", PointerResolutionKind.Direct)]
    public void Resolve_ReturnsProofedKind(string fieldPath, PointerResolutionKind expected)
    {
        Assert.Equal(expected, ZonePointerResolution.Resolve(fieldPath));
        Assert.True(ZonePointerResolution.IsProofed(fieldPath));
    }

    [Theory]
    [InlineData("Some.Untraced.Field")]
    [InlineData("")]
    public void Resolve_UnknownPath_StaysUnknown(string fieldPath)
    {
        // Proof gate: anything without traced EBOOT evidence must NOT be classified.
        Assert.Equal(PointerResolutionKind.Unknown, ZonePointerResolution.Resolve(fieldPath));
        Assert.False(ZonePointerResolution.IsProofed(fieldPath));
    }

    [Fact]
    public void Resolve_NullPath_IsUnknownNotThrow()
    {
        Assert.Equal(PointerResolutionKind.Unknown, ZonePointerResolution.Resolve(null!));
    }

    [Fact]
    public void Table_HasTheProofedFieldPaths()
    {
        // The two official patch zones resolve to zero Unknown across this set.
        Assert.True(ZonePointerResolution.RuleCount >= 60);
    }
}
