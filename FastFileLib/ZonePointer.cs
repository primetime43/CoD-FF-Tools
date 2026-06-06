namespace FastFileLib;

// -----------------------------------------------------------------------------
// IW4 (and IW-lineage) zone-serialized pointer model.
//
// Ported from Jacob Schroeder's FastFile reference implementation:
//   https://github.com/jacob-schroeder/FastFile
//   - FastFile.Models/Data/Pointer.cs       (raw -> kind/block/offset decode)
//   - FastFile.Models/Data/PointerKind.cs   (Null / Inline / Insert / Offset)
//   - FastFile.Models/Zone/XFILE_BLOCK.cs   (stream-block enum)
//   - EBOOT_ZONE_LOAD_MODEL.md              (PS3 EBOOT.ELF authority)
//
// This replaces the old "high bit set, mask 0x7FFFFFFF = zone offset" heuristic
// (still used by Cod5MenuDeserializer for WaW menu *validation*), which decodes
// IW4 pointers incorrectly: it folds the top-nibble block index into the offset.
//
// EBOOT-authoritative encoding (from the PS3 EBOOT.ELF trace, June 2026):
//   encoded = ((blockIndex << 28) | offset) + 1     // there IS a +1 bias
//   decode:  raw = encoded - 1
//            blockIndex = raw >> 28        (top nibble)
//            offset     = raw & 0x0FFFFFFF (low 28 bits)
// The earlier "no +1 bias" / "<< 29" notes were superseded by the EBOOT trace.
// See docs/MW2_PS3_EBOOT_Zone_Load_Model.md and docs/MW2_PS3_Pointer_Fixup_Comparison.md.
// -----------------------------------------------------------------------------

/// <summary>
/// What a 32-bit zone-serialized pointer field represents.
/// Mirrors Jacob Schroeder's <c>PointerKind</c> (EBOOT-confirmed marker meanings).
/// </summary>
public enum ZonePointerKind
{
    /// <summary><c>0</c> — null pointer (no data, no resolution).</summary>
    Null,
    /// <summary><c>-1</c> (0xFFFFFFFF) — the referenced data is written inline immediately
    /// after the parent struct (read it from the current stream position).</summary>
    Inline,
    /// <summary><c>-2</c> (0xFFFFFFFE) — <b>insert pointer</b>. Like <see cref="Inline"/> the data
    /// follows now, but the loader also reserves a 4-byte alias cell in stream block 4 (LARGE) and
    /// writes the loaded pointer into it, so other (alias) pointers can reference the cell rather than
    /// the data body. For pure reading the data is still consumed inline; the distinction only matters
    /// to a writer/rebaser that must reproduce the reserved cell.</summary>
    Insert,
    /// <summary>Any other value — an offset pointer. After subtracting the <c>+1</c> bias, the top
    /// nibble is the stream block index and the low 28 bits are the byte offset within that block.</summary>
    Offset,
}

/// <summary>
/// XFile stream memory blocks, in header order. The blockSize[] slots in the zone
/// header are indexed by these. Mirrors Jacob Schroeder's <c>XFILE_BLOCK</c>.
/// (Console/PS3 zones expose Temp..Vertex; PC adds Index.)
/// </summary>
public enum ZoneStreamBlock
{
    Temp = 0,
    Physical = 1,
    Runtime = 2,
    Virtual = 3,
    Large = 4,
    Callback = 5,
    Vertex = 6,   // PS3 / PC
    Index = 7,    // PC only
}

/// <summary>
/// A decoded IW4-style zone pointer. Construct from the raw 32-bit field value;
/// <see cref="Kind"/>, <see cref="StreamBlockIndex"/>, and <see cref="Offset"/> are
/// derived. Use <see cref="ZoneBlockLayout"/> to turn an <see cref="ZonePointerKind.Offset"/>
/// pointer into a physical position inside the zone.
///
/// Faithful port of Jacob Schroeder's <c>Pointer.SetRaw</c>
/// (https://github.com/jacob-schroeder/FastFile).
/// </summary>
public readonly struct ZonePointer
{
    /// <summary>Top nibble of an offset pointer holds the block index.</summary>
    private const int StreamBlockMask = 0xF;
    /// <summary>Low 28 bits of an offset pointer hold the within-block byte offset.</summary>
    public const int StreamOffsetMask = 0x0FFFFFFF;
    /// <summary>EBOOT null-avoidance bias: the stored value is the encoded (block|offset) plus one.</summary>
    private const int EncodeBias = 1;

    /// <summary>The raw 32-bit field value, as stored in the zone.</summary>
    public int Raw { get; }

    public ZonePointerKind Kind { get; }

    /// <summary>Block index (0..15) for an <see cref="ZonePointerKind.Offset"/> pointer; 0 otherwise.</summary>
    public int StreamBlockIndex { get; }

    /// <summary>Within-block byte offset for an <see cref="ZonePointerKind.Offset"/> pointer; 0 otherwise.</summary>
    public int Offset { get; }

    public ZonePointer(int raw)
    {
        Raw = raw;

        if (raw == 0)
        {
            Kind = ZonePointerKind.Null;
            StreamBlockIndex = 0;
            Offset = 0;
        }
        else if (raw == -1)
        {
            Kind = ZonePointerKind.Inline;
            StreamBlockIndex = 0;
            Offset = 0;
        }
        else if (raw == -2)
        {
            Kind = ZonePointerKind.Insert;
            StreamBlockIndex = 0;
            Offset = 0;
        }
        else
        {
            // EBOOT: the stored value carries a +1 bias; strip it before splitting block/offset.
            int decoded = raw - EncodeBias;
            Kind = ZonePointerKind.Offset;
            StreamBlockIndex = (decoded >> 28) & StreamBlockMask;
            Offset = decoded & StreamOffsetMask;
        }
    }

    /// <summary>Convenience: decode from an unsigned field value.</summary>
    public ZonePointer(uint raw) : this(unchecked((int)raw)) { }

    public bool IsNull => Kind == ZonePointerKind.Null;
    public bool IsInline => Kind == ZonePointerKind.Inline;
    public bool IsInsert => Kind == ZonePointerKind.Insert;
    /// <summary>True for both <see cref="ZonePointerKind.Inline"/> and <see cref="ZonePointerKind.Insert"/> —
    /// i.e. the data follows inline in the stream and is read from the current position.</summary>
    public bool IsInlineData => Kind is ZonePointerKind.Inline or ZonePointerKind.Insert;
    public bool IsOffset => Kind == ZonePointerKind.Offset;

    /// <summary>
    /// Encodes a (block, within-block offset) pair back into a raw pointer value, the
    /// inverse of the offset decode. Mirrors the EBOOT encoding
    /// <c>raw = ((blockIndex &lt;&lt; 28) | newOffset) + 1</c> (the <c>+1</c> bias the loader strips
    /// back off at decode time).
    /// </summary>
    public static int Encode(int streamBlockIndex, int offsetWithinBlock)
        => (((streamBlockIndex & StreamBlockMask) << 28) | (offsetWithinBlock & StreamOffsetMask)) + EncodeBias;

    public override string ToString() => Kind switch
    {
        ZonePointerKind.Null => "Null",
        ZonePointerKind.Inline => $"Inline(0x{Raw:X8})",
        ZonePointerKind.Insert => $"Insert(0x{Raw:X8})",
        _ => $"Offset(block={StreamBlockIndex}, +0x{Offset:X})",
    };
}
