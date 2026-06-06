namespace FastFileLib;

/// <summary>
/// Maps the XFile stream blocks onto byte positions within a zone, and resolves
/// <see cref="ZonePointerKind.Offset"/> pointers to physical zone offsets.
///
/// Ported from Jacob Schroeder's FastFile reference implementation
/// (https://github.com/jacob-schroeder/FastFile),
/// <c>FastFile.Logic/Zone/ZonePointerRebaser.cs</c> — specifically <c>GetBlockBases</c>
/// and <c>oldPhysical = pointer.Offset + blockBases[block]</c>.
///
/// Key fact (verified against a real MW2 PS3 zone): the LARGE block (index 4) is laid
/// out LAST, so its base is <c>XFile.Size - blockSize[LARGE]</c>. All other blocks are
/// laid out sequentially starting at <c>XFile.Size - sum(blockSizes)</c>.
/// </summary>
public sealed class ZoneBlockLayout
{
    private readonly int[] _blockSizes;
    private readonly int[] _blockBases;

    /// <summary>The XFile.Size header field (decompressed zone payload size) this layout was built from.</summary>
    public int XFileSize { get; }

    /// <summary>Per-block allocation sizes, indexed by <see cref="ZoneStreamBlock"/>.</summary>
    public IReadOnlyList<int> BlockSizes => _blockSizes;

    /// <summary>Computed base byte position of each block within the zone.</summary>
    public IReadOnlyList<int> BlockBases => _blockBases;

    /// <param name="xfileSize">The XFile.Size header field.</param>
    /// <param name="blockSizes">The blockSize[] slots from the header, in <see cref="ZoneStreamBlock"/> order.</param>
    public ZoneBlockLayout(int xfileSize, int[] blockSizes)
    {
        XFileSize = xfileSize;
        _blockSizes = (int[])(blockSizes ?? throw new ArgumentNullException(nameof(blockSizes))).Clone();
        _blockBases = ComputeBlockBases(xfileSize, _blockSizes);
    }

    /// <summary>Base position of a specific block (or 0 if the block index is out of range).</summary>
    public int BaseOf(ZoneStreamBlock block)
    {
        int i = (int)block;
        return (i >= 0 && i < _blockBases.Length) ? _blockBases[i] : 0;
    }

    /// <summary>
    /// Resolves an offset pointer to a physical byte position inside the zone.
    /// Returns false for null/inline pointers, out-of-range blocks, or offsets that
    /// fall outside their block's allocation (the same guards the rebaser applies).
    /// </summary>
    public bool TryResolve(ZonePointer pointer, out int physicalOffset)
    {
        physicalOffset = -1;

        if (pointer.Kind != ZonePointerKind.Offset)
            return false;

        int block = pointer.StreamBlockIndex;
        if (block < 0 || block >= _blockSizes.Length || _blockSizes[block] <= 0)
            return false;

        if (pointer.Offset < 0 || pointer.Offset >= _blockSizes[block])
            return false;

        physicalOffset = pointer.Offset + _blockBases[block];
        return true;
    }

    /// <summary>Convenience overload that decodes a raw field value first.</summary>
    public bool TryResolve(int rawPointer, out int physicalOffset)
        => TryResolve(new ZonePointer(rawPointer), out physicalOffset);

    /// <summary>
    /// Mirror of Jacob Schroeder's <c>GetBlockBases</c>: lay every block out sequentially
    /// from <c>xfileSize - sum(blockSizes)</c>, skipping LARGE, then place LARGE last.
    /// </summary>
    private static int[] ComputeBlockBases(int xfileSize, int[] blockSizes)
    {
        var bases = new int[blockSizes.Length];
        int sum = 0;
        foreach (var s in blockSizes) sum += s;

        int position = Math.Max(0, xfileSize - sum);
        int largeIndex = (int)ZoneStreamBlock.Large;

        for (int i = 0; i < blockSizes.Length; i++)
        {
            if (i == largeIndex) continue;
            bases[i] = position;
            position += blockSizes[i];
        }

        if (largeIndex >= 0 && largeIndex < bases.Length)
            bases[largeIndex] = position;

        return bases;
    }

    /// <summary>
    /// Builds a layout straight from a decompressed zone's header bytes. Reads XFile.Size
    /// at 0x00 and the blockSize[] slots at 0x08, in the platform's byte order. The number
    /// of block slots is derived from the header size (header = 8-byte size/external +
    /// N*4 block slots + 16-byte XAssetList).
    /// </summary>
    public static ZoneBlockLayout? FromZoneHeader(
        byte[] zoneData, GameVersion gv, bool isXbox360, bool isPC, bool isWii)
    {
        if (zoneData == null) return null;

        int headerSize = FastFileConstants.GetZoneHeaderSize(gv, isXbox360, isPC, isWii);
        if (zoneData.Length < headerSize) return null;

        // header layout: [Size:4][ExternalSize:4][blockSize[]:N*4][XAssetList:16]
        int blockCount = (headerSize - 8 - 16) / 4;
        if (blockCount <= (int)ZoneStreamBlock.Large) return null;

        int size = (int)FastFileConstants.ReadUInt32(zoneData, FastFileConstants.ZoneSizeOffset, littleEndian: isPC);

        var blockSizes = new int[blockCount];
        for (int i = 0; i < blockCount; i++)
            blockSizes[i] = (int)FastFileConstants.ReadUInt32(
                zoneData, FastFileConstants.BlockSizeTempOffset + i * 4, littleEndian: isPC);

        return new ZoneBlockLayout(size, blockSizes);
    }
}
