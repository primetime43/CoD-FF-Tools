using System.Buffers.Binary;
using System.Text;

namespace FastFileLib;

/// <summary>
/// Metadata extracted from an IW6 (Ghosts) <c>GfxImage</c> struct that
/// was located inline in a zone body. Sufficient for displaying
/// "image: name 256×256 (color map)" in the asset pool listview;
/// not sufficient for rendering pixels (those usually live in
/// <c>imagefile*.pak</c> streaming files, which this codebase doesn't
/// parse yet — Greyhound's pixel path requires PAK lookup).
/// </summary>
public readonly struct IW6GfxImageInfo
{
    public IW6GfxImageInfo(int structOffset, int nameOffset, string name,
                           int width, int height, byte mapType, byte semantic)
    {
        StructOffset = structOffset;
        NameOffset = nameOffset;
        Name = name;
        Width = width;
        Height = height;
        MapType = mapType;
        Semantic = semantic;
    }
    public int StructOffset { get; }    // start of the 80-byte struct
    public int NameOffset { get; }      // first byte of the inline name
    public string Name { get; }
    public int Width { get; }
    public int Height { get; }
    public byte MapType { get; }        // 3 = MAPTYPE_2D (most common)
    public byte Semantic { get; }       // 2 = color, 5 = normal, 8 = specular
}

/// <summary>
/// Scans an inflated IW6 zone for <c>GfxImage</c> structs and extracts
/// metadata. Pattern: a <c>FFFFFFFF</c> NamePtr placeholder followed by
/// an inline null-terminated ASCII name, with a plausible width/height
/// pair at <c>NamePtr-16/-14</c> (u16 BE) inside what would be the
/// struct's last mip-level entry.
///
/// Layout reverse-engineered against retail PS3
/// <c>mp_character_room_dlc_updated.zone</c> (622 image entries) using
/// JariK's <c>GhostsGfxImage</c> definition from
/// <see href="https://github.com/Scobalula/Greyhound">Greyhound</see> as
/// the x64 baseline, then adapted for PS3 32-bit pointers (4 bytes
/// instead of 8 for <c>NextHead</c> and <c>NamePtr</c>, total struct
/// 80 bytes instead of x64's 104).
///
/// What this <b>cannot</b> do:
/// <list type="bullet">
///   <item>Decode pixel data (lives in <c>imagefile*.pak</c> streaming
///         files in retail layouts; not in the FF zone).</item>
///   <item>Determine the engine's "ImageFormat" field — its exact byte
///         position in IW6 PS3 isn't pinned down yet. Empirically the
///         byte at the Greyhound-stated offset (struct +16) reads 0 in
///         every sample, so the field is probably stored elsewhere or
///         is one of the unidentified bytes around the texture-semantic
///         byte at struct +25.</item>
/// </list>
/// </summary>
public static class IW6GfxImageScanner
{
    /// <summary>Total bytes of an IW6 PS3 GfxImage struct (NextHead 4 +
    /// fields + NamePtr 4). Determined empirically from retail samples.</summary>
    public const int StructSize = 80;

    private const int OffsetNamePtr  = 76;  // FFFFFFFF placeholder at end
    private const int OffsetWidth    = 60;  // u16 BE, largest-stored mip width
    private const int OffsetHeight   = 62;  // u16 BE
    private const int OffsetMapType  = 24;  // byte, typically 3 = MAPTYPE_2D
    private const int OffsetSemantic = 25;  // byte: 2 color, 5 normal, 8 specular

    private const int MinDimension = 4;
    private const int MaxDimension = 4096;
    private const int MinNameLen   = 4;
    private const int MaxNameLen   = 128;

    /// <summary>
    /// Scan <paramref name="zone"/> for IW6 GfxImage structs. Returns one
    /// <see cref="IW6GfxImageInfo"/> per validated hit, in zone-byte order
    /// (which matches IW6's pool ordering for image-typed assets — useful
    /// for positional pool-pairing by callers).
    /// </summary>
    public static List<IW6GfxImageInfo> Locate(byte[] zone, int scanStart = 0)
    {
        var results = new List<IW6GfxImageInfo>();
        if (zone == null || zone.Length < StructSize + 4) return results;
        if (scanStart < 0) scanStart = 0;

        int p = Math.Max(scanStart, StructSize);
        int limit = zone.Length - 5;
        while (p < limit)
        {
            // Look for the NamePtr FFFFFFFF placeholder. The struct preceding
            // it lives at zone[p - 76 .. p - 1]; the inline name follows at
            // zone[p + 4 ..].
            if (zone[p] != 0xFF || zone[p+1] != 0xFF || zone[p+2] != 0xFF || zone[p+3] != 0xFF)
            {
                p++;
                continue;
            }
            if (p < OffsetNamePtr + 4)
            {
                p++;
                continue;
            }

            int structStart = p - OffsetNamePtr;
            // Width / Height — sanity-bounded + power-of-two; combined this
            // is a tight enough filter to rule out random FFFFFFFF runs.
            int width  = BinaryPrimitives.ReadUInt16BigEndian(zone.AsSpan(structStart + OffsetWidth, 2));
            int height = BinaryPrimitives.ReadUInt16BigEndian(zone.AsSpan(structStart + OffsetHeight, 2));
            if (!IsPlausibleTextureDim(width) || !IsPlausibleTextureDim(height))
            {
                p++;
                continue;
            }

            // MapType: accept the common values seen in retail samples
            // (1D / 2D / 3D / CUBE per Greyhound's GfxImageMapType enum).
            byte mapType = zone[structStart + OffsetMapType];
            if (mapType > 5)
            {
                p++;
                continue;
            }

            // Inline name. Must be printable ASCII, terminated by null.
            int nameStart = p + 4;
            int nameEnd = nameStart;
            while (nameEnd < zone.Length && nameEnd - nameStart < MaxNameLen)
            {
                byte b = zone[nameEnd];
                if (b == 0x00) break;
                if (!IsNameChar(b))
                {
                    nameEnd = -1;
                    break;
                }
                nameEnd++;
            }
            if (nameEnd < 0 || nameEnd >= zone.Length || zone[nameEnd] != 0x00
                || nameEnd - nameStart < MinNameLen)
            {
                p++;
                continue;
            }

            byte semantic = zone[structStart + OffsetSemantic];
            string name = Encoding.ASCII.GetString(zone, nameStart, nameEnd - nameStart);

            results.Add(new IW6GfxImageInfo(
                structOffset: structStart,
                nameOffset: nameStart,
                name: name,
                width: width,
                height: height,
                mapType: mapType,
                semantic: semantic));

            // Advance past the name's null. The next struct's NamePtr lives
            // a struct-and-name later.
            p = nameEnd + 1;
        }

        return results;
    }

    private static bool IsPlausibleTextureDim(int v)
    {
        if (v < MinDimension || v > MaxDimension) return false;
        return (v & (v - 1)) == 0; // power of two
    }

    /// <summary>
    /// CoD image asset name characters. Path-style ASCII (alnum + a few
    /// punctuation) — same set the rawfile scanner uses but with `~` added
    /// for IW6's hash-suffixed texture variants (e.g.
    /// <c>foo_x_spc_3~6abe4f98</c>).
    /// </summary>
    private static bool IsNameChar(byte b)
        => (b >= (byte)'a' && b <= (byte)'z')
        || (b >= (byte)'A' && b <= (byte)'Z')
        || (b >= (byte)'0' && b <= (byte)'9')
        || b == (byte)'_' || b == (byte)'-' || b == (byte)'.' || b == (byte)'/' || b == (byte)'~';

    /// <summary>Human-readable label for the <see cref="IW6GfxImageInfo.Semantic"/> byte.</summary>
    public static string SemanticName(byte semantic) => semantic switch
    {
        0 => "2D",
        1 => "function",
        2 => "color",
        3 => "detail",
        5 => "normal",
        8 => "specular",
        11 => "water",
        12 => "displacement",
        _ => $"sem 0x{semantic:X2}",
    };

    /// <summary>Human-readable label for the <see cref="IW6GfxImageInfo.MapType"/> byte.</summary>
    public static string MapTypeName(byte mapType) => mapType switch
    {
        0 => "none",
        1 => "1D",
        2 => "2D",
        3 => "2D",  // most common — Greyhound calls 3 the "MAPTYPE_2D" value
        4 => "3D",
        5 => "cube",
        _ => $"mt 0x{mapType:X2}",
    };
}
