using System.IO.Compression;
using System.Text;

namespace FastFileLib;

/// <summary>
/// Location and (optionally decompressed) contents of a single rawfile entry inside a zone.
/// Used by the compiler/patcher to enumerate existing rawfiles without re-implementing the
/// per-platform header parsing in each caller.
/// </summary>
public sealed class RawFileLocation
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Offset of the first 0xFF marker byte (start of the entry header).</summary>
    public int HeaderOffset { get; set; }

    /// <summary>Offset of the uncompressed-length size field.</summary>
    public int SizeOffset { get; set; }

    /// <summary>Offset of the first byte of the null-terminated name.</summary>
    public int NameOffset { get; set; }

    /// <summary>Offset of the first byte of the (possibly compressed) payload.</summary>
    public int DataOffset { get; set; }

    /// <summary>Uncompressed payload length declared in the header.</summary>
    public int DataSize { get; set; }

    /// <summary>Compressed payload length (MW2 only). 0 for uncompressed entries.</summary>
    public int CompressedSize { get; set; }

    /// <summary>12 for CoD4/WaW, 16 for MW2 subsequent files, 20 for the MW2 first file.</summary>
    public int HeaderSize { get; set; }

    /// <summary>True if the on-disk payload was zlib-compressed (MW2 path).</summary>
    public bool WasCompressed { get; set; }

    /// <summary>The decompressed payload (or raw payload for uncompressed entries).</summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// Locates rawfile entries inside an already-decompressed zone, across all
/// supported game/platform combinations.
///
/// Rawfile entry layouts handled:
///   CoD4/WaW (all platforms):
///     [FF FF FF FF][len BE][FF FF FF FF][name\0][data][\0]
///     (PC zones use the same shape — sizes happen to be BE here because the
///     rawfile header is stored as-shipped by the engine; only the zone header
///     ints switch endianness on PC.)
///
///   MW2 PS3/Xbox 360 (subsequent files, 16-byte header):
///     [FF FF FF FF][compressedLen BE][len BE][FF FF FF FF][name\0][zlib data]
///
///   MW2 PS3/Xbox 360 (first file, 20-byte header):
///     [FF FF FF FF][FF FF FF FF][compressedLen BE][len BE][FF FF FF FF][name\0][zlib data]
///
///   MW2 PC (16-byte header, LE size fields):
///     [FF FF FF FF][compressedLen LE][len LE][FF FF FF FF][name\0][zlib data]
/// </summary>
public static class RawFileScanner
{
    private const int MaxRawFileSize = 10_000_000;

    /// <summary>
    /// Enumerates every rawfile entry in <paramref name="zoneData"/>.
    /// </summary>
    /// <param name="zoneData">Decompressed zone bytes.</param>
    /// <param name="gameVersion">Detected game (drives header layout).</param>
    /// <param name="isPC">True for PC zones (drives endianness on MW2 size fields).</param>
    public static IReadOnlyList<RawFileLocation> FindRawFiles(
        byte[] zoneData,
        GameVersion gameVersion,
        bool isPC)
    {
        if (zoneData is null) throw new ArgumentNullException(nameof(zoneData));

        var results = new List<RawFileLocation>();
        var seenHeaders = new HashSet<int>();

        // Pattern set: scan for the trailing "<ext>\0" of each known name so we can
        // backtrack to the FF marker. Same approach the Editor's RawFileParser uses.
        var patterns = FastFileConstants.ValidRawFileExtensions
            .Select(ext => Encoding.ASCII.GetBytes(ext + "\0"))
            .ToArray();

        for (int i = 0; i < zoneData.Length; i++)
        {
            byte[]? matched = null;
            foreach (var p in patterns)
            {
                if (i + p.Length > zoneData.Length) continue;
                if (!zoneData.AsSpan(i, p.Length).SequenceEqual(p)) continue;
                matched = p;
                break;
            }
            if (matched is null) continue;

            // Back up to the FF FF FF FF marker preceding the filename.
            int markerEnd = FindPreviousFfMarker(zoneData, i, maxScanBack: 300);
            if (markerEnd < 0) continue;

            // markerEnd is the index of the *last* byte of the FF marker.
            // The filename starts at markerEnd + 1; reject a "marker" that's
            // actually padding (i.e. immediately followed by 0x00).
            if (zoneData[markerEnd + 1] == 0x00) continue;

            // Read the name and double-check it ends with the matched extension.
            int nameStart = markerEnd + 1;
            int nameEnd = nameStart;
            while (nameEnd < zoneData.Length && zoneData[nameEnd] != 0) nameEnd++;
            if (nameEnd <= nameStart || nameEnd >= zoneData.Length) continue;

            string name = Encoding.ASCII.GetString(zoneData, nameStart, nameEnd - nameStart);
            string extension = Encoding.ASCII.GetString(matched, 0, matched.Length - 1);
            if (!name.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) continue;
            if (!LooksLikeValidName(name)) continue;

            // Try to parse the header backwards from markerEnd, picking the layout
            // by game. Validate by reading the size field and checking it stays in
            // range; an out-of-range read just means this candidate isn't a rawfile.
            var loc = gameVersion == GameVersion.MW2
                ? TryParseMw2Header(zoneData, markerEnd, nameStart, nameEnd, isPC)
                : TryParseStandardHeader(zoneData, markerEnd, nameStart, nameEnd, isPC);

            if (loc is null) continue;
            if (!seenHeaders.Add(loc.HeaderOffset)) continue;

            loc.Name = name;
            results.Add(loc);
        }

        results.Sort((a, b) => a.HeaderOffset.CompareTo(b.HeaderOffset));
        return results;
    }

    /// <summary>
    /// Parses a single rawfile entry whose header is known to start at
    /// <paramref name="headerOffset"/>. Returns null if the bytes there don't
    /// match the expected header layout for the given game/platform.
    /// Use this when sequential walking has located the next entry's start —
    /// it's faster than the bulk pattern scan and avoids false positives from
    /// rawfile-extension byte sequences embedded in other asset blobs.
    /// </summary>
    public static RawFileLocation? TryParseAt(byte[] zoneData, int headerOffset, GameVersion gameVersion, bool isPC)
    {
        if (zoneData is null) throw new ArgumentNullException(nameof(zoneData));
        if (headerOffset < 0 || headerOffset >= zoneData.Length) return null;

        // Expected layout:
        //   CoD4/WaW: [FFFF][size][FFFF][name\0][data][\0]   (12-byte header)
        //   MW2 cons: [FFFF][compLen][len][FFFF][name\0][zlib data]   (16-byte; first file may be 20)
        //   MW2 PC:   same as above with LE sizes
        // Find the second FF marker by skipping past the header to where the name starts.
        int markerEnd;
        int nameStart;

        if (gameVersion == GameVersion.MW2)
        {
            // Check the 20-byte first-file variant on console (extra leading FF marker).
            bool isFirstFileLayout = !isPC
                && headerOffset + 20 <= zoneData.Length
                && IsFfMarker(zoneData, headerOffset)
                && IsFfMarker(zoneData, headerOffset + 4);
            int headerSize = isFirstFileLayout ? 20 : 16;
            if (headerOffset + headerSize > zoneData.Length) return null;
            if (!IsFfMarker(zoneData, headerOffset)) return null;
            if (!IsFfMarker(zoneData, headerOffset + headerSize - 4)) return null;
            markerEnd = headerOffset + headerSize - 1;
        }
        else
        {
            if (headerOffset + 12 > zoneData.Length) return null;
            if (!IsFfMarker(zoneData, headerOffset)) return null;
            if (!IsFfMarker(zoneData, headerOffset + 8)) return null;
            markerEnd = headerOffset + 11;
        }

        nameStart = markerEnd + 1;
        int nameEnd = nameStart;
        while (nameEnd < zoneData.Length && zoneData[nameEnd] != 0) nameEnd++;
        if (nameEnd <= nameStart || nameEnd >= zoneData.Length) return null;

        string name = Encoding.ASCII.GetString(zoneData, nameStart, nameEnd - nameStart);
        if (!LooksLikeValidName(name)) return null;
        // We deliberately don't require a specific extension here — callers walking
        // by structure know they're sitting on a rawfile slot, so any plausible
        // filename should be accepted.

        var loc = gameVersion == GameVersion.MW2
            ? TryParseMw2Header(zoneData, markerEnd, nameStart, nameEnd, isPC)
            : TryParseStandardHeader(zoneData, markerEnd, nameStart, nameEnd, isPC);

        if (loc is null) return null;
        loc.Name = name;
        return loc;
    }

    private static int FindPreviousFfMarker(byte[] data, int searchStart, int maxScanBack)
    {
        for (int j = searchStart - 1; j >= 4 && (searchStart - j) <= maxScanBack; j--)
        {
            if (data[j] == 0xFF &&
                data[j - 1] == 0xFF &&
                data[j - 2] == 0xFF &&
                data[j - 3] == 0xFF)
            {
                return j;
            }
        }
        return -1;
    }

    /// <summary>
    /// CoD4/WaW 12-byte uncompressed header. markerEnd is the last byte of the
    /// trailing FF marker; the length sits at markerEnd - 7. Per
    /// docs/PC_WaW_FastFile_Format.md, PC WaW stores all multi-byte values
    /// little-endian (including the rawfile size); console is big-endian.
    /// </summary>
    private static RawFileLocation? TryParseStandardHeader(byte[] data, int markerEnd, int nameStart, int nameEnd, bool isPC)
    {
        int sizeOffset = markerEnd - 7;
        int headerStart = sizeOffset - 4;
        if (headerStart < 0) return null;

        // Confirm the leading FF marker is actually present.
        if (!IsFfMarker(data, headerStart)) return null;

        int size = isPC ? ReadInt32LittleEndian(data, sizeOffset) : ReadInt32BigEndian(data, sizeOffset);
        if (size <= 0 || size > MaxRawFileSize) return null;

        int dataOffset = nameEnd + 1;
        if (dataOffset + size > data.Length) return null;

        byte[] payload = new byte[size];
        Buffer.BlockCopy(data, dataOffset, payload, 0, size);

        return new RawFileLocation
        {
            HeaderOffset = headerStart,
            SizeOffset = sizeOffset,
            NameOffset = nameStart,
            DataOffset = dataOffset,
            DataSize = size,
            CompressedSize = 0,
            HeaderSize = 12,
            WasCompressed = false,
            Data = payload,
        };
    }

    /// <summary>
    /// MW2 header. Subsequent files use 16 bytes:
    ///   [FF FF FF FF][compressedLen][len][FF FF FF FF]
    /// First file uses 20 bytes with an extra leading FF marker.
    /// markerEnd is the last byte of the FF marker just before the filename.
    /// </summary>
    private static RawFileLocation? TryParseMw2Header(byte[] data, int markerEnd, int nameStart, int nameEnd, bool isPC)
    {
        int lenOffset = markerEnd - 7;
        int compressedLenOffset = lenOffset - 4;
        int headerStart16 = compressedLenOffset - 4;
        int headerStart20 = compressedLenOffset - 8;

        if (headerStart16 < 0) return null;

        int headerSize;
        int headerStart;

        // MW2 PC always uses 16-byte rawfile headers per docs/MW2_PC_FastFile_Format.md.
        // The asset table sentinel preceding the first rawfile entry on PC is
        // `[type LE][FF FF FF FF]`, so a naive "20-byte header = two FF markers in a row"
        // check false-positives on PC. Gate the 20-byte detection to console-only.
        if (!isPC && headerStart20 >= 0 &&
            IsFfMarker(data, headerStart20) &&
            IsFfMarker(data, headerStart20 + 4))
        {
            headerSize = 20;
            headerStart = headerStart20;
        }
        else if (IsFfMarker(data, headerStart16))
        {
            headerSize = 16;
            headerStart = headerStart16;
        }
        else
        {
            return null;
        }

        int compressedLen = isPC
            ? ReadInt32LittleEndian(data, compressedLenOffset)
            : ReadInt32BigEndian(data, compressedLenOffset);
        int uncompressedLen = isPC
            ? ReadInt32LittleEndian(data, lenOffset)
            : ReadInt32BigEndian(data, lenOffset);

        if (uncompressedLen <= 0 || uncompressedLen > MaxRawFileSize) return null;
        if (compressedLen < 0 || compressedLen > MaxRawFileSize) return null;

        int dataOffset = nameEnd + 1;
        int onDiskSize = compressedLen > 0 ? compressedLen : uncompressedLen;
        if (dataOffset + onDiskSize > data.Length) return null;

        byte[] payload;
        bool wasCompressed = false;

        if (compressedLen > 0 && FastFileConstants.HasZlibHeader(data, dataOffset))
        {
            try
            {
                using var input = new MemoryStream(data, dataOffset, compressedLen, writable: false);
                using var zlib = new ZLibStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream(uncompressedLen);
                zlib.CopyTo(output);
                payload = output.ToArray();
                wasCompressed = true;
            }
            catch
            {
                // Decompression failure means the candidate wasn't actually a
                // compressed rawfile (false-positive on the pattern scan).
                return null;
            }
        }
        else
        {
            payload = new byte[onDiskSize];
            Buffer.BlockCopy(data, dataOffset, payload, 0, onDiskSize);
        }

        return new RawFileLocation
        {
            HeaderOffset = headerStart,
            SizeOffset = lenOffset,
            NameOffset = nameStart,
            DataOffset = dataOffset,
            DataSize = uncompressedLen,
            CompressedSize = compressedLen,
            HeaderSize = headerSize,
            WasCompressed = wasCompressed,
            Data = payload,
        };
    }

    private static bool IsFfMarker(byte[] data, int offset)
    {
        return offset >= 0 && offset + 4 <= data.Length &&
               data[offset] == 0xFF && data[offset + 1] == 0xFF &&
               data[offset + 2] == 0xFF && data[offset + 3] == 0xFF;
    }

    private static int ReadInt32BigEndian(byte[] data, int offset)
    {
        return (data[offset] << 24) |
               (data[offset + 1] << 16) |
               (data[offset + 2] << 8) |
               data[offset + 3];
    }

    private static int ReadInt32LittleEndian(byte[] data, int offset)
    {
        return data[offset] |
               (data[offset + 1] << 8) |
               (data[offset + 2] << 16) |
               (data[offset + 3] << 24);
    }

    private static bool LooksLikeValidName(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length < 3 || name.Length > 256) return false;

        foreach (char c in name)
        {
            if (c < 0x20 || c > 0x7E) return false;
        }

        int letters = 0;
        foreach (char c in name)
        {
            if (char.IsLetter(c)) letters++;
        }
        return letters >= 2;
    }
}
