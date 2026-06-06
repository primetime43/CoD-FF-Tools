// =============================================================================
// IW4 (MW2 PS3) zone reader — ported from Jacob Schroeder's FastFile
//   https://github.com/jacob-schroeder/FastFile
// This file ports: FastFile.Models/Data/PointerKind.cs and Pointer.cs
//   (Pointer raw->kind/block/offset decode, and the generic ZonePointer<T>).
// Faithful port; namespace + credit header are the only changes.
// =============================================================================

namespace FastFileLib.Iw4;

public enum PointerKind
{
    Null,
    Inline,
    Offset
}

/// <summary>
/// Port of Jacob Schroeder's <c>Pointer</c> (FastFile.Models/Data/Pointer.cs).
///
/// EBOOT-authoritative encoding (PS3 EBOOT.ELF trace, June 2026): the stored value carries a
/// <c>+1</c> bias, so <c>raw = stored - 1</c>, then <c>block = raw &gt;&gt; 28</c> (top nibble) and
/// <c>offset = raw &amp; 0x0FFFFFFF</c> (low 28 bits). The earlier "&lt;&lt; 29 / 29-bit offset" and
/// "no +1 bias" notes were superseded by the EBOOT trace (EBOOT helpers OffsetDirect @0x0011DC00 and
/// OffsetAlias @0x0011DBD0 both decode this way). It doesn't change THIS reader's behavior because
/// Offset pointers aren't dereferenced — real data is read by following inline (<c>-1</c>) / insert
/// (<c>-2</c>) markers in stream order. Correctly resolving an Offset pointer still needs the zone
/// demultiplexed into its block streams. See docs/MW2_PS3_EBOOT_Zone_Load_Model.md.
/// </summary>
public class Pointer
{
    private const int StreamBlockMask = 0xF;
    private const int StreamOffsetMask = 0x0FFFFFFF;
    private const int EncodeBias = 1;

    public int Raw { get; private set; }
    public PointerKind Kind { get; private set; }
    public int StreamBlockIndex { get; private set; }
    public int Offset { get; private set; }

    /// <summary>True when the inline marker was the insert form (<c>-2</c>) rather than plain inline
    /// (<c>-1</c>). The reader consumes both inline; a writer needs this to reserve the block-4 alias
    /// cell the EBOOT's InsertPointer helper (@0x0011DB88) creates for <c>-2</c>.</summary>
    public bool IsInsert { get; private set; }
    public int SourceOffset { get; private set; } = -1;
    public int SourceLength { get; private set; } = -1;
    public bool HasSourceSpan => SourceOffset >= 0 && SourceLength >= 0;

    public Pointer(int raw)
    {
        SetRaw(raw);
    }

    public void SetOffset(int address)
    {
        Offset = address;
        if (Kind == PointerKind.Inline && SourceOffset < 0)
            SourceOffset = address;
    }

    public void SetSourceSpan(int offset, int length)
    {
        SourceOffset = offset;
        SourceLength = length;
    }

    public void SetRaw(int raw)
    {
        Raw = raw;

        if (raw is 0)
        {
            Kind = PointerKind.Null;
            StreamBlockIndex = 0;
            Offset = 0;
            IsInsert = false;
            return;
        }

        if (raw is -1 or -2)
        {
            // -1 = inline, -2 = insert (inline data + a reserved block-4 alias cell). The reader
            // consumes both inline; IsInsert preserves the distinction for a future writer/rebaser.
            Kind = PointerKind.Inline;
            IsInsert = raw is -2;
            StreamBlockIndex = 0;
            Offset = SourceOffset >= 0 ? SourceOffset : 0;
            return;
        }

        // EBOOT: strip the +1 bias before splitting the block nibble and the 28-bit offset.
        int decoded = raw - EncodeBias;
        Kind = PointerKind.Offset;
        IsInsert = false;
        StreamBlockIndex = (decoded >> 28) & StreamBlockMask;
        Offset = decoded & StreamOffsetMask;
    }
}

/// <summary>Port of Jacob Schroeder's generic <c>ZonePointer&lt;T&gt;</c>.</summary>
public class ZonePointer<T> : Pointer
{
    public T? Result { get; private set; }
    public bool IsResolved { get; private set; }

    public ZonePointer(int raw) : base(raw)
    {
    }

    public void SetResult(T? result)
    {
        Result = result;
        IsResolved = true;
    }
}

/// <summary>
/// Sentinel raised by the asset walk when it reaches an asset type that has no body reader
/// ported yet. (This is an adaptation, not in the reference repo, which assumes every asset
/// type has a reader.) It propagates unwrapped through the resolver catch sites so the top
/// level can stop cleanly and return the assets resolved so far, instead of desyncing.
/// </summary>
public sealed class Iw4UnsupportedTypeException : Exception
{
    public XAssetType AssetType { get; }
    public int AssetIndex { get; }
    public int Position { get; }

    public Iw4UnsupportedTypeException(XAssetType assetType, int assetIndex, int position)
        : base($"No body reader ported for asset type {assetType} (asset #{assetIndex}) at 0x{position:X}.")
    {
        AssetType = assetType;
        AssetIndex = assetIndex;
        Position = position;
    }
}
