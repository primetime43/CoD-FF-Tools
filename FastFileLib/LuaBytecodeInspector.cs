using System.Text;

namespace FastFileLib;

/// <summary>
/// Lua 5.1 bytecode header fields (the standard 12-byte prefix).
/// </summary>
public sealed class LuaBytecodeHeader
{
    public byte VersionByte { get; init; }            // 0x51 for Lua 5.1
    public byte FormatByte { get; init; }             // 0 = official, anything else = custom (IW6 = 0x0D)
    public byte EndianByte { get; init; }             // 0 = big-endian (declared), 1 = little-endian (declared)
    public byte SizeofInt { get; init; }
    public byte SizeofSizeT { get; init; }
    public byte SizeofInstruction { get; init; }
    public byte SizeofLuaNumber { get; init; }
    public byte IntegralFlag { get; init; }           // 0 = lua_Number is floating point, 1 = integer
    public string VersionLabel => $"5.{(VersionByte & 0x0F)}";
    public bool IsCustomFormat => FormatByte != 0;
}

/// <summary>
/// A printable-ASCII string located inside a Lua bytecode body.
/// </summary>
public readonly struct LuaExtractedString
{
    public LuaExtractedString(int offset, string value) { Offset = offset; Value = value; }
    /// <summary>Byte offset of the first character (within the body, not the original zone).</summary>
    public int Offset { get; }
    public string Value { get; }
}

/// <summary>
/// Summary produced by <see cref="LuaBytecodeInspector.Inspect"/>.
/// </summary>
public sealed class LuaBytecodeSummary
{
    public bool IsValidLua51 { get; init; }
    public LuaBytecodeHeader? Header { get; init; }
    public List<LuaExtractedString> Strings { get; init; } = new();
    public int BodySize { get; init; }
}

/// <summary>
/// Format-agnostic Lua bytecode inspector. Parses the standard 12-byte Lua 5.1
/// header (the part that's still standard even for IW6's customized format byte
/// <c>0x0D</c>), then scans the body for null-terminated printable ASCII runs.
///
/// Why scan instead of properly parse: IW6 ships a customized Lua 5.1 dialect
/// (format byte 0x0D, mixed length encodings) — modelling its exact chunk
/// structure would be a large reverse-engineering task. The ASCII-run scan
/// works regardless of how strings are length-prefixed, and the user gets the
/// useful signal (menu names, button labels, source paths, identifiers) that
/// a string-table dump from a proper parser would also produce. False
/// positives are filtered by minimum length and printability constraints.
/// </summary>
public static class LuaBytecodeInspector
{
    private const int MinStringLen = 3;
    private const int MaxStringLen = 256;

    /// <summary>Lua 5.1 bytecode magic: <c>ESC + "LuaQ"</c>.</summary>
    public static readonly byte[] Lua51Signature = { 0x1B, (byte)'L', (byte)'u', (byte)'a', 0x51 };

    public static LuaBytecodeSummary Inspect(byte[] body)
    {
        if (body == null || body.Length < 12 || !StartsWith(body, 0, Lua51Signature))
            return new LuaBytecodeSummary { IsValidLua51 = false, BodySize = body?.Length ?? 0 };

        var header = new LuaBytecodeHeader
        {
            VersionByte       = body[4],
            FormatByte        = body[5],
            EndianByte        = body[6],
            SizeofInt         = body[7],
            SizeofSizeT       = body[8],
            SizeofInstruction = body[9],
            SizeofLuaNumber   = body[10],
            IntegralFlag      = body[11],
        };

        return new LuaBytecodeSummary
        {
            IsValidLua51 = true,
            Header = header,
            Strings = ExtractStrings(body, startOffset: 12),
            BodySize = body.Length,
        };
    }

    /// <summary>
    /// Find every null-terminated printable-ASCII run of length
    /// [<see cref="MinStringLen"/>, <see cref="MaxStringLen"/>] in the body.
    /// Adjacent runs are returned as separate strings.
    /// </summary>
    private static List<LuaExtractedString> ExtractStrings(byte[] body, int startOffset)
    {
        var found = new List<LuaExtractedString>();
        int i = startOffset;
        while (i < body.Length)
        {
            if (!IsPrintable(body[i])) { i++; continue; }

            int start = i;
            while (i < body.Length && IsPrintable(body[i])) i++;
            int len = i - start;

            // Require a null terminator and a plausible length range. The
            // terminator constraint is what makes false positives rare —
            // arbitrary instruction-byte patterns rarely happen to end in a
            // printable run capped by 0x00.
            if (i < body.Length && body[i] == 0x00 && len >= MinStringLen && len <= MaxStringLen)
            {
                found.Add(new LuaExtractedString(start, Encoding.ASCII.GetString(body, start, len)));
            }
            i++; // skip the null (or non-printable byte)
        }
        return found;
    }

    /// <summary>
    /// Format the summary as a multi-line text block suitable for a code-viewer
    /// panel. First line is a comment-style header line; then a blank line;
    /// then one extracted string per line.
    /// </summary>
    public static string FormatSummaryText(string assetName, LuaBytecodeSummary s)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"// {assetName} — {s.BodySize:N0} bytes of Lua bytecode");
        if (!s.IsValidLua51 || s.Header is null)
        {
            sb.AppendLine("// Not a valid Lua 5.1 bytecode body.");
            return sb.ToString();
        }
        var h = s.Header;
        string fmtNote = h.IsCustomFormat ? $" (custom format 0x{h.FormatByte:X2})" : "";
        string endianNote = h.EndianByte == 0 ? "BE-declared" : "LE-declared";
        sb.AppendLine($"// Lua {h.VersionLabel}{fmtNote}, {endianNote}, " +
                      $"int={h.SizeofInt} size_t={h.SizeofSizeT} instr={h.SizeofInstruction} number={h.SizeofLuaNumber}");
        sb.AppendLine($"// {s.Strings.Count} extracted strings");
        sb.AppendLine();
        if (s.Strings.Count == 0)
        {
            sb.AppendLine("// (no printable strings found — heavily packed bytecode)");
            return sb.ToString();
        }
        foreach (var str in s.Strings)
            sb.AppendLine(str.Value);
        return sb.ToString();
    }

    private static bool IsPrintable(byte b) => b >= 0x20 && b < 0x7F;

    private static bool StartsWith(byte[] body, int off, byte[] sig)
    {
        if (off < 0 || off + sig.Length > body.Length) return false;
        for (int i = 0; i < sig.Length; i++)
            if (body[off + i] != sig[i]) return false;
        return true;
    }
}
