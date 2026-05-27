using Call_of_Duty_FastFile_Editor.Models;
using System.Diagnostics;
using System.Text;

namespace Call_of_Duty_FastFile_Editor.ZoneParsers
{
    /// <summary>
    /// Scans a *fully-inflated* Ghosts (IW6) PS3 zone for rawfile assets.
    ///
    /// In the on-disk format a rawfile entry is a "short"-shape header
    /// followed by the file body wrapped in a zlib stream:
    /// <code>
    ///     [FF*4][compLen u32 BE][decLen u32 BE][FF*4]&lt;name&gt;\0&lt;zlib bytes&gt;
    /// </code>
    /// The library's <c>FastFileProcessor.TryDecompressGhosts</c> already
    /// expands every inner zlib stream inline during decompression, so by
    /// the time this scanner runs, the rawfile body has been replaced with
    /// its plaintext bytes — exactly <c>decLen</c> bytes long, starting
    /// right after the name's null terminator.
    ///
    /// This scanner ignores the asset pool. It walks the zone byte-by-byte
    /// looking for the rawfile signature directly. False-positive risk is
    /// small because the recognition requires (a) two FF*4 blocks bracketing
    /// two u32 size fields, (b) a printable-ASCII null-terminated name with
    /// a path-like character set (slash, dot, alphanumerics), and (c) the
    /// declared <c>decLen</c> fitting inside the remaining zone bytes.
    /// </summary>
    public static class GhostsRawFileScanner
    {
        private const int HeaderSize = 16;  // 4 FF + compLen u32 + decLen u32 + 4 FF

        /// <summary>
        /// Walks <paramref name="zoneData"/> and returns one
        /// <see cref="RawFileNode"/> per recognised rawfile entry.
        /// </summary>
        /// <param name="zoneData">Fully-inflated Ghosts zone bytes.</param>
        /// <param name="scanStart">Lower bound for the scan; passes 0x40 if unsure
        /// (skips XFile header). Caller can use the asset-pool end offset for a
        /// tighter window.</param>
        public static List<RawFileNode> Scan(byte[] zoneData, int scanStart = 0x40)
        {
            var nodes = new List<RawFileNode>();
            if (zoneData == null || zoneData.Length < scanStart + HeaderSize + 2)
                return nodes;

            int i = scanStart;
            while (i <= zoneData.Length - HeaderSize - 2)
            {
                if (!IsHeaderStart(zoneData, i)) { i++; continue; }

                int compLen = ReadBE32(zoneData, i + 4);
                int decLen  = ReadBE32(zoneData, i + 8);

                // Bytes 12..15 of the header must also be 4 FFs.
                if (zoneData[i + 12] != 0xFF || zoneData[i + 13] != 0xFF
                    || zoneData[i + 14] != 0xFF || zoneData[i + 15] != 0xFF)
                { i++; continue; }

                // Sanity on size fields. decLen must fit in the remaining zone bytes
                // (with room for a name and null terminator); compLen must be small
                // enough to be a real zlib stream length.
                if (decLen <= 0 || decLen > 16 * 1024 * 1024) { i++; continue; }
                if (compLen <= 0 || compLen > 16 * 1024 * 1024) { i++; continue; }

                // Read the name (null-terminated, path-like printable ASCII).
                int nameStart = i + HeaderSize;
                int nameEnd = nameStart;
                while (nameEnd < zoneData.Length && nameEnd - nameStart < 256)
                {
                    byte b = zoneData[nameEnd];
                    if (b == 0x00) break;
                    if (!IsRawFileNameChar(b)) { nameEnd = -1; break; }
                    nameEnd++;
                }
                if (nameEnd < 0 || nameEnd >= zoneData.Length || zoneData[nameEnd] != 0x00)
                { i++; continue; }
                int nameLen = nameEnd - nameStart;
                if (nameLen < 1) { i++; continue; }

                // Body should fit in the zone.
                int bodyStart = nameEnd + 1;
                if (bodyStart + decLen > zoneData.Length) { i++; continue; }

                string name = Encoding.ASCII.GetString(zoneData, nameStart, nameLen);
                byte[] body = new byte[decLen];
                Buffer.BlockCopy(zoneData, bodyStart, body, 0, decLen);

                bool isText = LooksTextual(body);
                var node = new RawFileNode
                {
                    FileName = name,
                    StartOfFileHeader = i,
                    HeaderSize = HeaderSize,
                    MaxSize = decLen,
                    CompressedSize = compLen,
                    IsCompressed = false,            // already inflated by the library
                    RawFileBytes = body,             // always the source of truth
                    // RawFileContent populates the editor's text viewer. Only set it for
                    // text content — binary rawfiles (e.g. .bin baselines) get left null
                    // so the text viewer stays empty instead of rendering bytes as garbled
                    // text. The raw bytes are still available via RawFileBytes for the
                    // export-to-disk path.
                    RawFileContent = isText ? Encoding.UTF8.GetString(body) : null,
                };

                nodes.Add(node);
                i = bodyStart + decLen;
            }

            Debug.WriteLine($"[GhostsRawFileScanner] Found {nodes.Count} rawfiles from offset 0x{scanStart:X}");
            return nodes;
        }

        private static bool IsHeaderStart(byte[] zone, int off)
            => zone[off]     == 0xFF && zone[off + 1] == 0xFF
            && zone[off + 2] == 0xFF && zone[off + 3] == 0xFF;

        private static int ReadBE32(byte[] zone, int off)
            => (zone[off] << 24) | (zone[off + 1] << 16) | (zone[off + 2] << 8) | zone[off + 3];

        /// <summary>
        /// Recognises bytes that can plausibly appear in a Ghosts rawfile name:
        /// alphanumerics, underscore, dash, dot, forward slash. Forbids spaces and
        /// other printable punctuation that don't appear in path-style names — keeps
        /// false-positive matches in dense binary regions low.
        /// </summary>
        private static bool IsRawFileNameChar(byte b)
            => (b >= (byte)'a' && b <= (byte)'z')
            || (b >= (byte)'A' && b <= (byte)'Z')
            || (b >= (byte)'0' && b <= (byte)'9')
            || b == (byte)'_' || b == (byte)'-' || b == (byte)'.' || b == (byte)'/';

        /// <summary>
        /// Heuristic: ≥ 90% of bytes are printable ASCII or common whitespace
        /// (`\t`, `\n`, `\r`). Used to decide whether to populate the editor's
        /// text viewer or leave it empty (for binary content).
        /// </summary>
        private static bool LooksTextual(byte[] body)
        {
            if (body.Length == 0) return true;
            int printable = 0;
            for (int i = 0; i < body.Length; i++)
            {
                byte b = body[i];
                if (b == 0x09 || b == 0x0A || b == 0x0D || (b >= 0x20 && b < 0x7F))
                    printable++;
            }
            return printable * 10 >= body.Length * 9;
        }
    }
}
