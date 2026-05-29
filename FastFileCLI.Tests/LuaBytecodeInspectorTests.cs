using System.Text;
using FastFileLib;
using Xunit;

namespace FastFileCLI.Tests;

public class LuaBytecodeInspectorTests
{
    private static byte[] Concat(params byte[][] parts)
    {
        var ms = new MemoryStream();
        foreach (var p in parts) ms.Write(p, 0, p.Length);
        return ms.ToArray();
    }

    /// <summary>Build a 12-byte Lua 5.1 header with the standard / IW6 size bytes.</summary>
    private static byte[] LuaHeader(byte format = 0, byte endian = 1)
        => new byte[] {
            0x1B, (byte)'L', (byte)'u', (byte)'a', 0x51,
            format,
            endian,
            4, // sizeof(int)
            4, // sizeof(size_t)
            4, // sizeof(Instruction)
            4, // sizeof(lua_Number)
            0, // integral flag
        };

    private static byte[] AsciiZ(string s)
        => Concat(Encoding.ASCII.GetBytes(s), new byte[] { 0 });

    [Fact]
    public void Inspect_RejectsNonLuaBytes()
    {
        byte[] body = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        var s = LuaBytecodeInspector.Inspect(body);
        Assert.False(s.IsValidLua51);
        Assert.Null(s.Header);
    }

    [Fact]
    public void Inspect_RejectsTooShort()
    {
        var s = LuaBytecodeInspector.Inspect(new byte[] { 0x1B, (byte)'L', (byte)'u' });
        Assert.False(s.IsValidLua51);
    }

    [Fact]
    public void Inspect_ParsesStandardHeader()
    {
        byte[] body = LuaHeader();
        var s = LuaBytecodeInspector.Inspect(body);
        Assert.True(s.IsValidLua51);
        Assert.NotNull(s.Header);
        Assert.Equal(0x51, s.Header!.VersionByte);
        Assert.Equal(0, s.Header.FormatByte);
        Assert.False(s.Header.IsCustomFormat);
        Assert.Equal("5.1", s.Header.VersionLabel);
    }

    [Fact]
    public void Inspect_FlagsIw6CustomFormat()
    {
        // IW6 ships format byte 0x0D.
        byte[] body = LuaHeader(format: 0x0D, endian: 0);
        var s = LuaBytecodeInspector.Inspect(body);
        Assert.True(s.IsValidLua51);
        Assert.Equal(0x0D, s.Header!.FormatByte);
        Assert.True(s.Header.IsCustomFormat);
    }

    [Fact]
    public void Inspect_ExtractsNullTerminatedStrings()
    {
        byte[] body = Concat(
            LuaHeader(),
            new byte[] { 0x05 },            // 1-byte length prefix (IW6 type registry style)
            AsciiZ("TNIL"),
            new byte[] { 0x01, 0x02, 0x03 }, // some non-ASCII bytes
            new byte[] { 0x09 },
            AsciiZ("TBOOLEAN"),
            new byte[] { 0x00, 0x00 },
            AsciiZ("ui/button.label"));     // a longer realistic string

        var s = LuaBytecodeInspector.Inspect(body);

        Assert.Contains(s.Strings, x => x.Value == "TNIL");
        Assert.Contains(s.Strings, x => x.Value == "TBOOLEAN");
        Assert.Contains(s.Strings, x => x.Value == "ui/button.label");
    }

    [Fact]
    public void Inspect_FiltersTooShortStrings()
    {
        // Single-char ASCII runs shouldn't be classified as strings.
        byte[] body = Concat(LuaHeader(), AsciiZ("a"), AsciiZ("xyz"), AsciiZ("longer_string"));
        var s = LuaBytecodeInspector.Inspect(body);
        Assert.DoesNotContain(s.Strings, x => x.Value == "a");
        Assert.Contains(s.Strings,    x => x.Value == "xyz");
        Assert.Contains(s.Strings,    x => x.Value == "longer_string");
    }

    [Fact]
    public void FormatSummaryText_IncludesHeaderInfoAndStrings()
    {
        byte[] body = Concat(LuaHeader(format: 0x0D),
            AsciiZ("hello"),
            AsciiZ("world"));
        var s = LuaBytecodeInspector.Inspect(body);

        string text = LuaBytecodeInspector.FormatSummaryText("test.lua", s);

        Assert.Contains("test.lua", text);
        Assert.Contains("Lua 5.1", text);
        Assert.Contains("custom format 0x0D", text);
        Assert.Contains("hello", text);
        Assert.Contains("world", text);
    }

    [Fact]
    public void FormatSummaryText_HandlesEmptyStrings()
    {
        byte[] body = LuaHeader(); // no body strings
        var s = LuaBytecodeInspector.Inspect(body);
        string text = LuaBytecodeInspector.FormatSummaryText("empty.lua", s);
        Assert.Contains("0 extracted strings", text);
        Assert.Contains("(no printable strings found", text);
    }

    [Fact]
    public void FormatSummaryText_HandlesNonLuaBody()
    {
        var s = LuaBytecodeInspector.Inspect(new byte[] { 0x00, 0x01, 0x02 });
        string text = LuaBytecodeInspector.FormatSummaryText("garbage.lua", s);
        Assert.Contains("Not a valid Lua 5.1", text);
    }
}
