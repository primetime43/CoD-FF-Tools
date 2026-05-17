using System.IO.Compression;
using System.Text;

namespace FastFileCLI.Tests;

/// <summary>
/// Builds minimal synthetic FastFile bytes for tests. These aren't game-loadable -
/// they exist purely to round-trip through our header parsers and decompression
/// code without needing real proprietary game files in the repo.
/// </summary>
public static class FfBuilder
{
    /// <summary>
    /// Builds a CoD4 PS3 unsigned FastFile (block-format) wrapping the given zone bytes.
    /// Magic IWffu100 + version 0x00000001 (BE) + zlib blocks (stripped header) + 00 01.
    /// </summary>
    public static byte[] BuildCoD4Ps3(byte[] zoneBytes)
        => BuildBlockFormat("IWffu100", new byte[] { 0x00, 0x00, 0x00, 0x01 }, zoneBytes);

    /// <summary>
    /// Builds a WaW PS3 unsigned FastFile.
    /// Magic IWffu100 + version 0x00000183.
    /// </summary>
    public static byte[] BuildWaWPs3(byte[] zoneBytes)
        => BuildBlockFormat("IWffu100", new byte[] { 0x00, 0x00, 0x01, 0x83 }, zoneBytes);

    /// <summary>
    /// Builds a CoD4 PC FastFile (little-endian version 0x00000005).
    /// </summary>
    public static byte[] BuildCoD4Pc(byte[] zoneBytes)
        => BuildBlockFormat("IWffu100", new byte[] { 0x05, 0x00, 0x00, 0x00 }, zoneBytes);

    /// <summary>
    /// Builds a synthetic MW2 PS3 FastFile with a minimal 25-byte extended header.
    /// </summary>
    public static byte[] BuildMW2Ps3(byte[] zoneBytes)
    {
        var ms = new MemoryStream();
        ms.Write(Encoding.ASCII.GetBytes("IWffu100"));
        ms.Write(new byte[] { 0x00, 0x00, 0x01, 0x0D });   // version
        // MW2 extended header (entryCount=0 path, 25 bytes total)
        ms.WriteByte(0x01);                                 // allowOnlineUpdate
        ms.Write(new byte[8]);                              // fileCreationTime
        ms.Write(new byte[] { 0x00, 0x00, 0x00, 0x01 });    // region
        ms.Write(new byte[] { 0x00, 0x00, 0x00, 0x00 });    // entryCount = 0
        ms.Write(new byte[] { 0x00, 0x00, 0x00, 0x00 });    // fileSize placeholder
        ms.Write(new byte[] { 0x00, 0x00, 0x00, 0x00 });    // maxFileSize placeholder
        WriteBlocks(ms, zoneBytes);
        return ms.ToArray();
    }

    /// <summary>
    /// Builds an Xbox 360 signed streaming FastFile: IWff0100 + version +
    /// IWffs100 at 0x0C + 16KB hash/auth blob + single zlib stream at 0x400C.
    /// </summary>
    public static byte[] BuildWaWXbox360Signed(byte[] zoneBytes)
    {
        var ms = new MemoryStream();
        ms.Write(Encoding.ASCII.GetBytes("IWff0100"));               // outer signed magic
        ms.Write(new byte[] { 0x00, 0x00, 0x01, 0x83 });            // WaW version
        ms.Write(Encoding.ASCII.GetBytes("IWffs100"));               // streaming magic at 0x0C
        ms.Write(new byte[0x400C - 0x14]);                           // 16KB hash + 12B auth (zeros)

        // Single zlib stream from 0x400C onwards.
        using (var zlib = new ZLibStream(ms, CompressionLevel.SmallestSize, leaveOpen: true))
            zlib.Write(zoneBytes, 0, zoneBytes.Length);

        return ms.ToArray();
    }

    /// <summary>
    /// Builds a minimal valid WaW PS3 zone file with the right header MemAlloc values.
    /// Only the header fields the report tests inspect are populated meaningfully.
    /// Includes one fake asset pool entry so the report shows non-zero AssetCount.
    /// </summary>
    public static byte[] BuildMinimalWaWZone()
    {
        const int headerSize = 0x34;
        const int assetCount = 1;
        int totalSize = headerSize + assetCount * 8 + 32;  // header + 1 entry + filler
        var zone = new byte[totalSize];

        WriteBE(zone, 0x00, (uint)totalSize);              // ZoneSize
        WriteBE(zone, 0x08, 0x000010B0);                   // BlockSizeTemp (WaW PS3 expected value)
        WriteBE(zone, 0x18, (uint)(totalSize - 16));       // BlockSizeLarge
        WriteBE(zone, 0x20, 0x0005F8F0);                   // BlockSizeVertex (WaW PS3 expected value)
        WriteBE(zone, 0x2C, assetCount);                   // AssetCount @ 0x2C on PS3
        // Asset entry: [type=0x22 rawfile][ptr=FFFFFFFF]
        int poolOff = headerSize;
        zone[poolOff + 0] = 0x00; zone[poolOff + 1] = 0x00; zone[poolOff + 2] = 0x00; zone[poolOff + 3] = 0x22;
        zone[poolOff + 4] = 0xFF; zone[poolOff + 5] = 0xFF; zone[poolOff + 6] = 0xFF; zone[poolOff + 7] = 0xFF;
        return zone;
    }

    /// <summary>
    /// Builds a minimal valid MW2 PS3 zone file with MemAlloc values that match
    /// what FastFileInfo.DetectGameFromZoneData expects.
    /// </summary>
    public static byte[] BuildMinimalMW2Zone()
    {
        const int headerSize = 0x34;
        const int assetCount = 1;
        int totalSize = headerSize + assetCount * 8 + 32;
        var zone = new byte[totalSize];

        WriteBE(zone, 0x00, (uint)totalSize);
        WriteBE(zone, 0x08, 0x000003B4);                   // MW2 MemAlloc1
        WriteBE(zone, 0x18, (uint)(totalSize - 16));
        WriteBE(zone, 0x20, 0x00001000);                   // MW2 MemAlloc2
        WriteBE(zone, 0x2C, assetCount);
        int poolOff = headerSize;
        zone[poolOff + 0] = 0x00; zone[poolOff + 1] = 0x00; zone[poolOff + 2] = 0x00; zone[poolOff + 3] = 0x23; // MW2 rawfile
        zone[poolOff + 4] = 0xFF; zone[poolOff + 5] = 0xFF; zone[poolOff + 6] = 0xFF; zone[poolOff + 7] = 0xFF;
        return zone;
    }

    // ----- internals -----

    private static byte[] BuildBlockFormat(string magic, byte[] version, byte[] zoneBytes)
    {
        var ms = new MemoryStream();
        ms.Write(Encoding.ASCII.GetBytes(magic));
        ms.Write(version);
        WriteBlocks(ms, zoneBytes);
        return ms.ToArray();
    }

    /// <summary>
    /// Writes zoneBytes as block-format compressed data: each block is
    /// 2-byte BE length + zlib-deflate-stripped-header. Ends with 0x00 0x01.
    /// Matches what FastFileProcessor.Compress writes.
    /// </summary>
    private static void WriteBlocks(MemoryStream ms, byte[] zoneBytes)
    {
        const int BlockSize = 0x10000;
        int pos = 0;
        while (pos < zoneBytes.Length)
        {
            int chunkLen = Math.Min(BlockSize, zoneBytes.Length - pos);
            var chunk = new byte[chunkLen];
            Buffer.BlockCopy(zoneBytes, pos, chunk, 0, chunkLen);
            byte[] compressed = CompressBlock(chunk);

            // 2-byte BE length
            ms.WriteByte((byte)(compressed.Length >> 8));
            ms.WriteByte((byte)(compressed.Length & 0xFF));
            ms.Write(compressed, 0, compressed.Length);
            pos += chunkLen;
        }
        // End marker
        ms.WriteByte(0x00);
        ms.WriteByte(0x01);
    }

    private static byte[] CompressBlock(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal))
            zlib.Write(data, 0, data.Length);
        var raw = output.ToArray();
        // Strip the 2-byte zlib header (CMF/FLG), keep deflate data + Adler-32
        var result = new byte[raw.Length - 2];
        Buffer.BlockCopy(raw, 2, result, 0, result.Length);
        return result;
    }

    private static void WriteBE(byte[] dst, int offset, uint value)
    {
        dst[offset + 0] = (byte)((value >> 24) & 0xFF);
        dst[offset + 1] = (byte)((value >> 16) & 0xFF);
        dst[offset + 2] = (byte)((value >> 8) & 0xFF);
        dst[offset + 3] = (byte)(value & 0xFF);
    }

    private static void WriteBE(byte[] dst, int offset, int value) => WriteBE(dst, offset, (uint)value);
}
