using FastFileLib;
using Xunit;

namespace FastFileCLI.Tests;

public class ZoneAssetPoolTests
{
    private static byte[] Concat(params byte[][] parts)
    {
        var ms = new MemoryStream();
        foreach (var p in parts) ms.Write(p, 0, p.Length);
        return ms.ToArray();
    }

    private static readonly byte[] Ptr = { 0xFF, 0xFF, 0xFF, 0xFF };

    [Fact]
    public void IsEndMarker_DetectsEightFf()
    {
        byte[] data = Concat(new byte[] { 0x00, 0x00, 0x00, 0x23 }, Ptr, Ptr, Ptr);
        Assert.False(ZoneAssetPool.IsEndMarker(data, 0));   // [type][ptr] is not the marker
        Assert.True(ZoneAssetPool.IsEndMarker(data, 4));    // [ptr][ptr] = 8 x FF
    }

    [Fact]
    public void IsEndMarker_FalseNearEnd()
    {
        Assert.False(ZoneAssetPool.IsEndMarker(new byte[] { 0xFF, 0xFF, 0xFF }, 0));
    }

    [Fact]
    public void TryReadRecord_TypeFirst_BigEndian()
    {
        // [00 00 00 23][FF FF FF FF] — console rawfile (PS3 WaW = 0x22 etc.)
        byte[] data = Concat(new byte[] { 0x00, 0x00, 0x00, 0x23 }, Ptr);
        Assert.True(ZoneAssetPool.TryReadRecord(data, 0, littleEndian: false, out uint id, out var order));
        Assert.Equal(0x23u, id);
        Assert.Equal(AssetRecordOrder.TypeFirst, order);
    }

    [Fact]
    public void TryReadRecord_TypeFirst_LittleEndian_PC()
    {
        // [24 00 00 00][FF FF FF FF] — MW2 PC rawfile id 0x24, little-endian
        byte[] data = Concat(new byte[] { 0x24, 0x00, 0x00, 0x00 }, Ptr);
        Assert.True(ZoneAssetPool.TryReadRecord(data, 0, littleEndian: true, out uint id, out var order));
        Assert.Equal(0x24u, id);
        Assert.Equal(AssetRecordOrder.TypeFirst, order);
    }

    [Fact]
    public void TryReadRecord_PointerFirst_BigEndian()
    {
        // [FF FF FF FF][00 00 00 23] — "Format B" pointer-first
        byte[] data = Concat(Ptr, new byte[] { 0x00, 0x00, 0x00, 0x23 });
        Assert.True(ZoneAssetPool.TryReadRecord(data, 0, littleEndian: false, out uint id, out var order));
        Assert.Equal(0x23u, id);
        Assert.Equal(AssetRecordOrder.PointerFirst, order);
    }

    [Fact]
    public void TryReadRecord_RejectsEndMarker()
    {
        byte[] data = Concat(Ptr, Ptr);
        Assert.False(ZoneAssetPool.TryReadRecord(data, 0, littleEndian: false, out _, out _));
    }

    [Fact]
    public void TryReadRecord_RejectsHighTypeBytes()
    {
        // Type word with non-zero high bytes is not a valid record (id must fit in a byte).
        byte[] data = Concat(new byte[] { 0x12, 0x34, 0x56, 0x78 }, Ptr);
        Assert.False(ZoneAssetPool.TryReadRecord(data, 0, littleEndian: false, out _, out _));
    }

    [Fact]
    public void TryReadRecord_RejectsNoPointerHalf()
    {
        // Neither half is FFFFFFFF.
        byte[] data = Concat(new byte[] { 0x00, 0x00, 0x00, 0x23 }, new byte[] { 0x00, 0x00, 0x00, 0x01 });
        Assert.False(ZoneAssetPool.TryReadRecord(data, 0, littleEndian: false, out _, out _));
    }

    [Fact]
    public void TryReadRecord_OutOfBounds()
    {
        Assert.False(ZoneAssetPool.TryReadRecord(new byte[] { 0x00, 0x00 }, 0, false, out _, out _));
    }

    [Fact]
    public void DetectTypeFieldOffset_TypeFirst_ReturnsZero()
    {
        byte[] data = Concat(new byte[] { 0x00, 0x00, 0x00, 0x23 }, Ptr);
        Assert.Equal(0, ZoneAssetPool.DetectTypeFieldOffset(data, 0));
    }

    [Fact]
    public void DetectTypeFieldOffset_PointerFirst_ReturnsFour()
    {
        byte[] data = Concat(Ptr, new byte[] { 0x00, 0x00, 0x00, 0x23 });
        Assert.Equal(4, ZoneAssetPool.DetectTypeFieldOffset(data, 0));
    }
}
