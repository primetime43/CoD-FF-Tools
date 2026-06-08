// =============================================================================
// IW4 (MW2 PS3) zone reader — ported from Jacob Schroeder's FastFile
//   https://github.com/jacob-schroeder/FastFile
// Port of FastFile.Logic/Zone/XFileReadStreamBlocks.cs.
//
// The decompressed zone is read with ONE sequential cursor (ZoneReadContext.Position),
// but the IW4 loader also tracks a position PER stream block (TEMP/LARGE/VERTEX/…). Reads
// advance both the sequential cursor and the *active* block's position. Alignment and
// insert-cell reservation operate on the active block's position — which differs from the
// sequential Position by a per-block constant — so e.g. a shader's bytecode (allocated from
// TEMP while the surrounding asset is in LARGE) aligns to the TEMP cursor, not to Position.
// Getting that wrong is exactly what misaligned standalone materials/techsets.
// =============================================================================

namespace FastFileLib.Iw4;

internal sealed class ZoneReadStreamBlocks
{
    // XFileWriteRules.Ps3LargeBlockInitialOffset.
    private const int Ps3LargeBlockInitialOffset = 0x16A;
    private const int MinBlockCount = 8; // XFILE_BLOCK.MAX_XFILE_COUNT

    private readonly int[] _blockSizes;
    private readonly int[] _positions;
    private readonly Stack<(int PreviousBlockIndex, int SavedPosition)> _stack = new();
    private int _activeBlockIndex;

    public ZoneReadStreamBlocks(int[] blockSizes)
    {
        int count = Math.Max(MinBlockCount, blockSizes.Length);
        _blockSizes = new int[count];
        _positions = new int[count];
        for (int i = 0; i < blockSizes.Length && i < count; i++)
            _blockSizes[i] = blockSizes[i];

        _positions[(int)ZoneStreamBlock.Temp] = BlockSize(ZoneStreamBlock.Temp);
        _positions[(int)ZoneStreamBlock.Large] = Ps3LargeBlockInitialOffset;
        _activeBlockIndex = (int)ZoneStreamBlock.Temp;
    }

    public int ActiveBlockIndex => _activeBlockIndex;
    public int ActivePosition => _positions[_activeBlockIndex];

    public void PushStreamBlock(ZoneStreamBlock block) => PushStreamBlock((int)block);

    public void PushStreamBlock(int blockIndex)
    {
        if (blockIndex < 0 || blockIndex >= _positions.Length)
            throw new InvalidDataException($"Invalid XFile stream block index {blockIndex:N0}.");

        var previousBlockIndex = _activeBlockIndex;
        _activeBlockIndex = blockIndex;
        // Save the block-being-pushed-to's current position (the reference records it AFTER switching).
        _stack.Push((previousBlockIndex, _positions[_activeBlockIndex]));
    }

    public void PopStreamBlock()
    {
        if (_stack.Count == 0)
            throw new InvalidDataException("Cannot pop an XFile stream block because the stack is empty.");

        var (previousBlockIndex, savedPosition) = _stack.Pop();
        // TEMP is scratch: its position is rolled back on pop (other blocks keep their advances).
        if (_activeBlockIndex == (int)ZoneStreamBlock.Temp)
            _positions[_activeBlockIndex] = savedPosition;

        _activeBlockIndex = previousBlockIndex;
    }

    /// <summary>Aligns the active block's position and returns the padding amount.</summary>
    public int AlignAndGetPadding(int alignment)
    {
        if (alignment <= 0)
            throw new InvalidDataException($"Cannot align XFile stream block with invalid alignment {alignment:N0}.");

        var position = ActivePosition;
        var remainder = position % alignment;
        if (remainder == 0)
            return 0;

        var padding = alignment - remainder;
        _positions[_activeBlockIndex] = position + padding;
        return padding;
    }

    public void Align(int alignment) => AlignAndGetPadding(alignment);

    public void Advance(int byteCount)
    {
        if (byteCount < 0)
            throw new InvalidDataException($"Cannot advance an XFile stream block by a negative byte count: {byteCount:N0}.");

        _positions[_activeBlockIndex] += byteCount;
    }

    private int BlockSize(ZoneStreamBlock block)
    {
        int index = (int)block;
        return index >= 0 && index < _blockSizes.Length ? _blockSizes[index] : 0;
    }
}
