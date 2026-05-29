using System.Text;
using FastFileLib.GameDefinitions;

namespace FastFileLib;

/// <summary>
/// A single 8-byte entry from a Ghosts (IW6) asset pool.
/// </summary>
public readonly struct GhostsPoolEntry
{
    public GhostsPoolEntry(int recordOffset, GhostsAssetTypePS3 type, GhostsPointerKind pointerKind)
    {
        RecordOffset = recordOffset;
        Type = type;
        PointerKind = pointerKind;
    }
    /// <summary>Byte offset of the 8-byte entry in the zone.</summary>
    public int RecordOffset { get; }
    /// <summary>Asset type ID (PS3 enum).</summary>
    public GhostsAssetTypePS3 Type { get; }
    /// <summary>Which pointer convention the entry's first 4 bytes use.</summary>
    public GhostsPointerKind PointerKind { get; }
}

public enum GhostsPointerKind
{
    /// <summary><c>FF FF FF FF</c> — standard inline placeholder.</summary>
    Placeholder,
    /// <summary><c>00 00 00 00</c> — NULL (seen e.g. first scriptfile entry in patch_mp_prisonbreak).</summary>
    Null,
    /// <summary>High-bit-set u32 — pre-link resolved pointer (same convention WaW menu fields use).</summary>
    Resolved,
}

/// <summary>
/// A located zlib-wrapped asset header within an inflated Ghosts zone.
/// Both short (4 trailing FFs before name) and long (8 trailing FFs, with
/// an extra unknown u32) shapes are produced by the same scanner.
/// </summary>
public readonly struct GhostsAssetHeader
{
    public GhostsAssetHeader(int headerOffset, int bodyStart, int bodyEnd,
                             int compressedLen, int decompressedLen, string name, bool isLong)
    {
        HeaderOffset = headerOffset;
        BodyStart = bodyStart;
        BodyEnd = bodyEnd;
        CompressedLen = compressedLen;
        DecompressedLen = decompressedLen;
        Name = name;
        IsLong = isLong;
    }
    /// <summary>Offset of the first byte of the header (the first <c>0xFF</c>).</summary>
    public int HeaderOffset { get; }
    /// <summary>Offset of the first byte of the inflated body (one byte past the name's null).</summary>
    public int BodyStart { get; }
    /// <summary>Exclusive end of the inflated body (<c>BodyStart + DecompressedLen</c>).</summary>
    public int BodyEnd { get; }
    /// <summary>The compressed-length value declared in the header (size of the *original* zlib stream).</summary>
    public int CompressedLen { get; }
    /// <summary>The decompressed-length value declared in the header (and the actual length of the inflated body).</summary>
    public int DecompressedLen { get; }
    /// <summary>The asset name (null-terminator excluded), e.g. <c>maps/mp/mp_prisonbreak</c>.</summary>
    public string Name { get; }
    /// <summary>True for the long-shape (8 trailing FFs + extra u32), false for short-shape (4 trailing FFs).</summary>
    public bool IsLong { get; }
}

/// <summary>
/// A located luafile asset inside an inflated Ghosts zone. Unlike the
/// rawfile/scriptfile zlib-wrapped format, luafiles use a flat 16-byte
/// header and the body is Lua bytecode (not zlib-compressed).
/// </summary>
public readonly struct GhostsLuaFile
{
    public GhostsLuaFile(int headerOffset, int bodyStart, int bodyEnd, int byteCodeLen, string name)
    {
        HeaderOffset = headerOffset;
        BodyStart = bodyStart;
        BodyEnd = bodyEnd;
        ByteCodeLen = byteCodeLen;
        Name = name;
    }
    /// <summary>Offset of the first byte of the 16-byte header.</summary>
    public int HeaderOffset { get; }
    /// <summary>Offset of the first byte of the Lua bytecode body (one byte past the name's null).</summary>
    public int BodyStart { get; }
    /// <summary>Exclusive end of the body (<c>BodyStart + ByteCodeLen</c>).</summary>
    public int BodyEnd { get; }
    /// <summary>Bytecode length declared in the header (and the actual length of the body).</summary>
    public int ByteCodeLen { get; }
    /// <summary>The asset name (null terminator excluded), e.g. <c>ui/lui/menuautonav.lua</c>.</summary>
    public string Name { get; }
}

/// <summary>
/// Result of pairing pool entries with located headers.
/// </summary>
public sealed class GhostsPoolPairing
{
    public List<GhostsPoolEntry> Entries { get; init; } = new();
    public List<GhostsAssetHeader> Headers { get; init; } = new();

    /// <summary>For each pool index, the matched header index in <see cref="Headers"/>, or -1 if unpaired.</summary>
    public int[] PoolToHeader { get; init; } = Array.Empty<int>();

    /// <summary>Number of pool entries that got paired with a header.</summary>
    public int PairedCount { get; init; }

    /// <summary>Number of located headers that didn't get paired with a pool entry.</summary>
    public int UnpairedHeaders { get; init; }
}

/// <summary>
/// Layout of a fully-inflated Call of Duty: Ghosts (IW6) PS3 zone.
///
/// Two responsibilities:
/// <list type="bullet">
///   <item>Locate the asset pool — header-counts-driven so it works for both
///         patch FFs (pool at <c>0x38</c>) and DLC / base zones (pool after
///         tag-string region). See <see cref="LocatePool"/>.</item>
///   <item>Locate all zlib-wrapped asset headers in the zone body — both
///         short (rawfile-style) and long (scriptfile-style) shapes. See
///         <see cref="LocateAllHeaders"/>.</item>
/// </list>
///
/// Verified zone-header layout (offsets relative to the start of the inflated zone):
/// <code>
///   0x00..0x27   Fixed XFile fields (zone size, block sizes, …)
///   0x28..0x2B   tagCount (BE u32)
///   0x2C..0x2F   placeholder
///   0x30..0x33   assetCount (BE u32)
///   0x34..0x37   placeholder
///   0x38..?      Either count3+placeholder (DLC/base zones) or first pool entry (patch FFs)
///   ...          Tag pointer placeholders (4 bytes × tagCount)
///   ...          Tag strings (tagCount null-terminated ASCII)
///   ...          Asset pool (8 bytes × assetCount)
/// </code>
///
/// Pool entry: <c>[ptr u32][type BE u32]</c>. Pointer = placeholder (<c>FF*4</c>),
/// NULL (<c>00*4</c>), or pre-link-resolved (high bit set).
///
/// Wrapped asset header (short shape):
/// <code>
///   [FF*4][compLen BE u32][decLen BE u32][FF*4]&lt;name&gt;\0&lt;body&gt;
/// </code>
/// Wrapped asset header (long shape):
/// <code>
///   [FF*4][compLen BE u32][decLen BE u32][??? u32][FF*8]&lt;name&gt;\0&lt;body&gt;
/// </code>
///
/// <see cref="FastFileProcessor.InflateGhostsZoneAssets"/> inflates every inner
/// zlib stream during decompression, so by the time callers see the zone, the
/// body is plain bytes of length <c>DecompressedLen</c>.
/// </summary>
public static class GhostsZoneLayout
{
    // -------- header offsets --------
    public const int OffsetTagCount   = 0x28;
    public const int OffsetAssetCount = 0x30;
    public const int PoolEarliestOffset = 0x38;
    public const int TagPlaceholdersStart = 0x3C;

    // -------- pool record --------
    public const int PoolRecordSize = 8;
    public const byte MaxValidTypeId = 0x35;

    // -------- wrapped header --------
    public const int ShortHeaderGross = 16; // [FF*4][comp][dec][FF*4]
    public const int LongHeaderGross  = 24; // [FF*4][comp][dec][???][FF*8]
    public const int MaxBodyLen = 32 * 1024 * 1024;
    public const int MaxNameLen = 127;

    // -------- luafile (flat 16-byte header) --------
    public const int LuaFileHeaderSize = 16; // [FF*4][size BE][unk u32][FF*4]
    /// <summary>Lua 5.1 bytecode signature: <c>ESC + "LuaQ"</c>.</summary>
    private static readonly byte[] LuaSignature = { 0x1B, (byte)'L', (byte)'u', (byte)'a', 0x51 };

    // ===================================================================
    // Pool location + walk
    // ===================================================================

    /// <summary>
    /// Read the tag and asset counts from the XFile header. No bounds checking
    /// beyond minimum length — callers should pre-validate <paramref name="zone"/>.
    /// </summary>
    public static (uint tagCount, uint assetCount) ReadHeaderCounts(byte[] zone)
    {
        if (zone == null || zone.Length < OffsetAssetCount + 4)
            return (0, 0);
        return (ReadBE32(zone, OffsetTagCount), ReadBE32(zone, OffsetAssetCount));
    }

    /// <summary>
    /// Locate the asset pool start. Returns -1 if no pool can be found.
    ///
    /// Strategy:
    /// <list type="number">
    ///   <item><c>tagCount == 0</c> → pool starts at <c>0x38</c> (patch FFs).</item>
    ///   <item><c>tagCount &gt; 0</c> → skip tagCount placeholders + tagCount
    ///         null-terminated strings, then scan a small forward window for the
    ///         first valid pool entry (some zones have a 4-byte trailing field
    ///         between the last tag and the pool, e.g.
    ///         <c>mp_character_room_dlc_updated.zone</c>).</item>
    ///   <item>Fallback brute scan for the longest run of valid pool entries — used
    ///         when header counts are missing or the layout is unexpected.</item>
    /// </list>
    /// </summary>
    public static int LocatePool(byte[] zone)
    {
        if (zone == null || zone.Length < PoolEarliestOffset + PoolRecordSize) return -1;

        uint tagCount   = ReadBE32(zone, OffsetTagCount);
        uint assetCount = ReadBE32(zone, OffsetAssetCount);

        // Candidate A: tagCount == 0 → pool starts at 0x38.
        if (tagCount == 0 && IsPoolEntry(zone, PoolEarliestOffset))
            return PoolEarliestOffset;

        // Candidate B: tagCount > 0 → walk past placeholders + tag strings.
        if (tagCount > 0 && tagCount < 100_000)
        {
            int stringsStart = TagPlaceholdersStart + 4 * (int)tagCount;
            int p = stringsStart;
            bool stringsOk = true;
            for (int i = 0; i < tagCount; i++)
            {
                while (p < zone.Length && zone[p] != 0) p++;
                if (p >= zone.Length) { stringsOk = false; break; }
                p++; // skip null
            }
            if (stringsOk)
            {
                // Probe 32 bytes forward for the first valid pool entry —
                // covers any small trailing field / alignment between the
                // tag-string region and the pool.
                for (int probe = p; probe <= p + 32 && probe + PoolRecordSize <= zone.Length; probe++)
                {
                    if (IsPoolEntry(zone, probe))
                        return probe;
                }
            }
        }

        // Candidate C: fallback brute scan — find a run starting at any 4-byte
        // aligned offset in the first MB.
        int scanLimit = Math.Min(zone.Length - PoolRecordSize, 1_000_000);
        int targetRun = assetCount > 0 && assetCount < 1_000_000
            ? Math.Max(2, (int)assetCount - 1)
            : 2;
        int bestStart = -1, bestRun = 0;
        for (int candidate = PoolEarliestOffset; candidate <= scanLimit; candidate += 4)
        {
            int run = 0, q = candidate;
            while (q + PoolRecordSize <= zone.Length && IsPoolEntry(zone, q))
            {
                run++;
                q += PoolRecordSize;
                if (assetCount > 0 && run >= assetCount) break;
            }
            if (run > bestRun)
            {
                bestRun = run;
                bestStart = candidate;
                if (bestRun >= targetRun) break;
            }
        }
        return bestRun >= 2 ? bestStart : -1;
    }

    /// <summary>
    /// Walk the asset pool starting at <paramref name="poolStart"/> and stopping at
    /// the first invalid record (or after <c>assetCount</c> entries from the header,
    /// whichever comes first). Pass -1 / 0 to disable the count cap.
    /// </summary>
    public static List<GhostsPoolEntry> WalkPool(byte[] zone, int poolStart, int? maxEntries = null)
    {
        var entries = new List<GhostsPoolEntry>();
        if (zone == null || poolStart < 0) return entries;
        int cap = (maxEntries is int m && m > 0 && m < 1_000_000) ? m : int.MaxValue;
        int p = poolStart;
        while (entries.Count < cap && p + PoolRecordSize <= zone.Length && IsPoolEntry(zone, p))
        {
            entries.Add(new GhostsPoolEntry(
                recordOffset: p,
                type: (GhostsAssetTypePS3)zone[p + 7],
                pointerKind: ClassifyPointer(zone, p)));
            p += PoolRecordSize;
        }
        return entries;
    }

    /// <summary>
    /// Convenience: locate + walk in one call. Returns an empty list when the
    /// pool can't be located.
    /// </summary>
    public static List<GhostsPoolEntry> ParsePool(byte[] zone, out int poolStart, out int poolEnd)
    {
        poolStart = LocatePool(zone);
        if (poolStart < 0) { poolEnd = -1; return new List<GhostsPoolEntry>(); }
        (uint _, uint assetCount) = ReadHeaderCounts(zone);
        var entries = WalkPool(zone, poolStart, assetCount > 0 ? (int)assetCount : (int?)null);
        poolEnd = entries.Count > 0 ? entries[^1].RecordOffset + PoolRecordSize : poolStart;
        return entries;
    }

    /// <summary>
    /// One 8-byte pool entry: <c>[ptr u32][type BE u32 with low byte ≤ 0x35]</c>.
    /// Accepts the three pointer conventions in <see cref="GhostsPointerKind"/>.
    /// </summary>
    public static bool IsPoolEntry(byte[] zone, int offset)
    {
        if (zone == null || offset < 0 || offset + PoolRecordSize > zone.Length) return false;
        if (!IsValidPoolPointer(zone, offset)) return false;
        if (zone[offset + 4] != 0x00 || zone[offset + 5] != 0x00 || zone[offset + 6] != 0x00)
            return false;
        return zone[offset + 7] <= MaxValidTypeId;
    }

    /// <summary>
    /// Pool entries accept any 4-byte pointer value — IW6 zones use
    /// 0x40-flagged pointers (e.g. <c>0x401FDF85</c>) in addition to the
    /// 0x80-flagged "resolved" form seen in earlier games. The strict
    /// type-byte check + the header's <c>assetCount</c> cap are what
    /// actually delimit the pool, so this method just returns true.
    /// </summary>
    private static bool IsValidPoolPointer(byte[] zone, int offset) => true;

    private static GhostsPointerKind ClassifyPointer(byte[] zone, int offset)
    {
        byte b0 = zone[offset], b1 = zone[offset + 1], b2 = zone[offset + 2], b3 = zone[offset + 3];
        if (b0 == 0xFF && b1 == 0xFF && b2 == 0xFF && b3 == 0xFF) return GhostsPointerKind.Placeholder;
        if (b0 == 0x00 && b1 == 0x00 && b2 == 0x00 && b3 == 0x00) return GhostsPointerKind.Null;
        return GhostsPointerKind.Resolved;
    }

    // ===================================================================
    // Wrapped asset header scan
    // ===================================================================

    /// <summary>
    /// Single-pass scan of the inflated zone for every locatable wrapped-asset
    /// header (short or long shape). Pass the asset pool end offset as
    /// <paramref name="scanStart"/> to avoid scanning over pool entries (whose
    /// <c>[FF*4]</c>+type bytes can otherwise fool the heuristic).
    /// </summary>
    public static List<GhostsAssetHeader> LocateAllHeaders(byte[] zone, int scanStart)
    {
        var found = new List<GhostsAssetHeader>();
        if (zone == null) return found;
        if (scanStart < 0) scanStart = 0;

        int p = scanStart;
        int limit = zone.Length - ShortHeaderGross - 2;
        while (p <= limit)
        {
            if (!LooksLikeHeader(zone, p)) { p++; continue; }
            if (!TryParseHeader(zone, p, out var hdr)) { p++; continue; }
            int bodyStart = hdr.NameEnd + 1;
            int bodyEnd   = bodyStart + hdr.DecompressedLen;
            if (bodyEnd > zone.Length) { p++; continue; }
            found.Add(new GhostsAssetHeader(
                headerOffset: p,
                bodyStart: bodyStart,
                bodyEnd: bodyEnd,
                compressedLen: hdr.CompressedLen,
                decompressedLen: hdr.DecompressedLen,
                name: hdr.Name,
                isLong: hdr.IsLong));
            p = bodyEnd; // stride past inflated body
        }
        return found;
    }

    /// <summary>
    /// Pair pool entries with located headers, filtering to only the
    /// zlib-wrapped pool types (<see cref="IsWrappedType"/>). Non-wrapped pool
    /// entries (xmodel / image / sound / techset / …) don't have a wrapper in
    /// the zone body and would skew positional pairing if not skipped.
    /// </summary>
    public static GhostsPoolPairing PairPoolWithHeaders(
        IReadOnlyList<GhostsPoolEntry> poolEntries,
        IReadOnlyList<GhostsAssetHeader> headers)
    {
        var poolToHeader = new int[poolEntries.Count];
        for (int i = 0; i < poolToHeader.Length; i++) poolToHeader[i] = -1;

        int paired = 0;
        int hIdx = 0;
        for (int i = 0; i < poolEntries.Count && hIdx < headers.Count; i++)
        {
            if (!IsWrappedType(poolEntries[i].Type)) continue;
            poolToHeader[i] = hIdx++;
            paired++;
        }

        return new GhostsPoolPairing
        {
            Entries = new List<GhostsPoolEntry>(poolEntries),
            Headers = new List<GhostsAssetHeader>(headers),
            PoolToHeader = poolToHeader,
            PairedCount = paired,
            UnpairedHeaders = Math.Max(0, headers.Count - hIdx),
        };
    }

    // ===================================================================
    // Luafile scan (flat 16-byte header, no zlib wrapper)
    // ===================================================================

    /// <summary>
    /// Scan the zone for luafile assets. Layout (verified against
    /// <c>patch_ui_mp.zone</c>):
    /// <code>
    ///   [FF*4][size BE u32][unk u32 = 0x02000000][FF*4]&lt;name&gt;\0&lt;Lua bytecode&gt;
    /// </code>
    /// Each candidate position is confirmed by the <c>\x1B LuaQ</c> bytecode
    /// signature at the body's first 5 bytes so dense binary regions that
    /// happen to match the FF/size/unk/FF pattern don't yield false positives.
    /// Strides past each hit by the declared bytecode length.
    /// </summary>
    public static List<GhostsLuaFile> LocateAllLuaFiles(byte[] zone, int scanStart)
    {
        var found = new List<GhostsLuaFile>();
        if (zone == null) return found;
        if (scanStart < 0) scanStart = 0;

        int p = scanStart;
        int limit = zone.Length - LuaFileHeaderSize - LuaSignature.Length - 2;
        while (p <= limit)
        {
            if (!TryReadLuaFileHeader(zone, p, out int sizeBytes, out string name, out int bodyStart))
            {
                p++;
                continue;
            }
            int bodyEnd = bodyStart + sizeBytes;
            if (bodyEnd > zone.Length) { p++; continue; }

            // Verify with Lua bytecode magic. This is what makes the scan safe
            // against random [FF*4][int][int][FF*4] sequences that can occur
            // anywhere in dense asset data.
            if (!StartsWith(zone, bodyStart, LuaSignature))
            {
                p++;
                continue;
            }

            found.Add(new GhostsLuaFile(
                headerOffset: p,
                bodyStart: bodyStart,
                bodyEnd: bodyEnd,
                byteCodeLen: sizeBytes,
                name: name));
            p = bodyEnd;
        }
        return found;
    }

    private static bool TryReadLuaFileHeader(byte[] zone, int off,
        out int sizeBytes, out string name, out int bodyStart)
    {
        sizeBytes = 0;
        name = string.Empty;
        bodyStart = -1;
        if (off + LuaFileHeaderSize >= zone.Length) return false;

        // [FF*4][size BE][unk u32][FF*4]
        if (!AllFF(zone, off, 4)) return false;
        if (!AllFF(zone, off + 12, 4)) return false;

        int size = (int)ReadBE32(zone, off + 4);
        if (size <= 0 || size > MaxBodyLen) return false;

        int nameStart = off + LuaFileHeaderSize;
        if (!TryReadName(zone, nameStart, out string n, out int nameEnd)) return false;
        // Restrict to ".lua" names to keep false-positive cost low.
        if (!n.EndsWith(".lua", StringComparison.OrdinalIgnoreCase)) return false;

        sizeBytes = size;
        name = n;
        bodyStart = nameEnd + 1;
        return true;
    }

    private static bool StartsWith(byte[] zone, int off, byte[] sig)
    {
        if (off < 0 || off + sig.Length > zone.Length) return false;
        for (int i = 0; i < sig.Length; i++)
            if (zone[off + i] != sig[i]) return false;
        return true;
    }

    /// <summary>
    /// True for pool types whose body is a zlib-wrapped block matching one of
    /// the header shapes scanned by <see cref="LocateAllHeaders"/>. Verified
    /// from sample zones for <c>rawfile</c> (short) and <c>scriptfile</c> (long);
    /// <c>mptype</c> + <c>aitype</c> are included on the basis of in-repo notes
    /// without direct sample verification. Adding more types is safe — over-
    /// inclusion just leaves them unpaired, it doesn't mis-pair other entries.
    /// </summary>
    public static bool IsWrappedType(GhostsAssetTypePS3 type) => type switch
    {
        GhostsAssetTypePS3.rawfile    => true,
        GhostsAssetTypePS3.scriptfile => true,
        GhostsAssetTypePS3.mptype     => true,
        GhostsAssetTypePS3.aitype     => true,
        _ => false,
    };

    private readonly struct ParsedHeader
    {
        public ParsedHeader(int compressedLen, int decompressedLen, string name, int nameEnd, bool isLong)
        {
            CompressedLen = compressedLen;
            DecompressedLen = decompressedLen;
            Name = name;
            NameEnd = nameEnd;
            IsLong = isLong;
        }
        public int CompressedLen { get; }
        public int DecompressedLen { get; }
        public string Name { get; }
        public int NameEnd { get; }
        public bool IsLong { get; }
    }

    private static bool LooksLikeHeader(byte[] zone, int off)
    {
        if (off < 0 || off + ShortHeaderGross + 2 > zone.Length) return false;
        if (zone[off] != 0xFF || zone[off + 1] != 0xFF || zone[off + 2] != 0xFF || zone[off + 3] != 0xFF)
            return false;
        int comp = (int)ReadBE32(zone, off + 4);
        if (comp <= 0 || comp > MaxBodyLen) return false;
        int dec = (int)ReadBE32(zone, off + 8);
        if (dec <= 0 || dec > MaxBodyLen) return false;
        return true;
    }

    private static bool TryParseHeader(byte[] zone, int off, out ParsedHeader hdr)
    {
        hdr = default;
        if (!LooksLikeHeader(zone, off)) return false;

        int compLen = (int)ReadBE32(zone, off + 4);
        int decLen  = (int)ReadBE32(zone, off + 8);

        // Long shape: try first (more specific — 8 trailing FFs) so a size field
        // legitimately ending in 0xFF can't be misread as part of a short-shape FF run.
        if (off + LongHeaderGross < zone.Length && AllFF(zone, off + 16, 8)
            && TryReadName(zone, off + LongHeaderGross, out string nameLong, out int endLong))
        {
            hdr = new ParsedHeader(compLen, decLen, nameLong, endLong, isLong: true);
            return true;
        }

        // Short shape: 4 trailing FFs.
        if (AllFF(zone, off + 12, 4)
            && TryReadName(zone, off + ShortHeaderGross, out string nameShort, out int endShort))
        {
            hdr = new ParsedHeader(compLen, decLen, nameShort, endShort, isLong: false);
            return true;
        }

        return false;
    }

    private static bool TryReadName(byte[] zone, int nameStart, out string name, out int nameEndOffset)
    {
        name = string.Empty;
        nameEndOffset = -1;
        int end = nameStart;
        while (end < zone.Length && end - nameStart < MaxNameLen)
        {
            byte b = zone[end];
            if (b == 0x00) break;
            if (!IsNameChar(b)) return false;
            end++;
        }
        if (end >= zone.Length || zone[end] != 0x00) return false;
        int len = end - nameStart;
        if (len < 1) return false;
        name = Encoding.ASCII.GetString(zone, nameStart, len);
        nameEndOffset = end;
        return true;
    }

    private static bool AllFF(byte[] zone, int off, int count)
    {
        if (off < 0 || off + count > zone.Length) return false;
        for (int j = 0; j < count; j++)
            if (zone[off + j] != 0xFF) return false;
        return true;
    }

    private static uint ReadBE32(byte[] data, int off)
        => ((uint)data[off] << 24) | ((uint)data[off + 1] << 16) | ((uint)data[off + 2] << 8) | data[off + 3];

    /// <summary>
    /// Path-style printable ASCII: alphanumerics, underscore, dash, dot,
    /// forward slash. Forbids spaces and other punctuation so dense binary
    /// regions don't accidentally parse as names.
    /// </summary>
    private static bool IsNameChar(byte b)
        => (b >= (byte)'a' && b <= (byte)'z')
        || (b >= (byte)'A' && b <= (byte)'Z')
        || (b >= (byte)'0' && b <= (byte)'9')
        || b == (byte)'_' || b == (byte)'-' || b == (byte)'.' || b == (byte)'/';
}
