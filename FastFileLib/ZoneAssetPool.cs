namespace FastFileLib;

/// <summary>The order of the two halves of an 8-byte asset-pool record.</summary>
public enum AssetRecordOrder
{
    /// <summary>[type word][0xFFFFFFFF] — "Format A".</summary>
    TypeFirst,
    /// <summary>[0xFFFFFFFF][type word] — "Format B".</summary>
    PointerFirst,
}

/// <summary>
/// Shared knowledge of the zone asset-pool's 8-byte record layout. Used by the editor's
/// pool mapper, the converter's type-id remapper, and the CLI's report — each has its own
/// walking algorithm (scan-and-validate, find-start-then-walk, known-count walk), but they
/// all agree on what a single record looks like, which is what lives here.
///
/// A record is a 4-byte asset type word plus a 4-byte pointer placeholder (0xFFFFFFFF), in
/// either order. The type word holds a small asset-type id with its high 3 bytes zero; it is
/// read in the platform's byte order (PC = little-endian, console/Wii = big-endian). Callers
/// validate the id themselves (against per-game enums, a range, etc.).
/// </summary>
public static class ZoneAssetPool
{
    /// <summary>Size of one asset-pool record, in bytes.</summary>
    public const int RecordSize = 8;

    /// <summary>True if an 8-byte all-0xFF end-of-pool marker sits at <paramref name="offset"/>.</summary>
    public static bool IsEndMarker(byte[] data, int offset)
    {
        if (data == null || offset < 0 || offset + RecordSize > data.Length) return false;
        for (int k = 0; k < RecordSize; k++)
            if (data[offset + k] != 0xFF) return false;
        return true;
    }

    /// <summary>
    /// If an 8-byte asset record sits at <paramref name="offset"/>, outputs its type id (the
    /// low byte of the type word) and which order the type/pointer halves appear in, and
    /// returns true. The pointer half must be exactly 0xFFFFFFFF and the type word's high 3
    /// bytes must be zero (read in the given byte order). The all-FF end marker is NOT a record.
    /// Validation of the id itself is left to the caller.
    /// </summary>
    public static bool TryReadRecord(byte[] data, int offset, bool littleEndian,
        out uint typeId, out AssetRecordOrder order)
    {
        typeId = 0;
        order = AssetRecordOrder.TypeFirst;
        if (data == null || offset < 0 || offset + RecordSize > data.Length) return false;

        bool PtrAt(int o) =>
            data[o] == 0xFF && data[o + 1] == 0xFF && data[o + 2] == 0xFF && data[o + 3] == 0xFF;

        bool leftIsPtr = PtrAt(offset);
        bool rightIsPtr = PtrAt(offset + 4);

        // PointerFirst: [FFFFFFFF][type]. Require the right half NOT also be all-FF so the
        // all-FF end marker doesn't masquerade as a record.
        if (leftIsPtr && !rightIsPtr)
        {
            uint t = FastFileConstants.ReadUInt32(data, offset + 4, littleEndian);
            if (t <= 0xFF) { typeId = t; order = AssetRecordOrder.PointerFirst; return true; }
            return false;
        }

        // TypeFirst: [type][FFFFFFFF].
        if (rightIsPtr && !leftIsPtr)
        {
            uint t = FastFileConstants.ReadUInt32(data, offset, littleEndian);
            if (t <= 0xFF) { typeId = t; order = AssetRecordOrder.TypeFirst; return true; }
            return false;
        }

        return false;
    }

    /// <summary>
    /// Returns the byte offset of the type word within a record, by inspecting which half of
    /// the record at <paramref name="poolStart"/> holds the 0xFFFFFFFF pointer placeholder:
    /// 0 for [type][ptr] (TypeFirst), 4 for [ptr][type] (PointerFirst). Used by callers that
    /// walk a fixed-size pool and want to read every slot's type word at a constant offset.
    /// </summary>
    public static int DetectTypeFieldOffset(byte[] data, int poolStart)
    {
        bool PtrAt(int o) =>
            o + 4 <= data.Length &&
            data[o] == 0xFF && data[o + 1] == 0xFF && data[o + 2] == 0xFF && data[o + 3] == 0xFF;

        bool leftIsPtr = PtrAt(poolStart);
        bool rightIsPtr = PtrAt(poolStart + 4);
        return (leftIsPtr && !rightIsPtr) ? 4 : 0;
    }
}
