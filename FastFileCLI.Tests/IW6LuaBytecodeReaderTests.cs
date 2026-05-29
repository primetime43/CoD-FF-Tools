using System.Buffers.Binary;
using System.Text;
using FastFileLib;
using Xunit;

namespace FastFileCLI.Tests;

/// <summary>
/// Synthetic-byte tests for <see cref="IW6LuaBytecodeReader"/>. Builds minimal
/// valid IW6 HavokScript bytecode bodies by hand so the expected output is
/// directly visible in the source.
/// </summary>
public class IW6LuaBytecodeReaderTests
{
    private static byte[] Concat(params byte[][] parts)
    {
        var ms = new MemoryStream();
        foreach (var p in parts) ms.Write(p, 0, p.Length);
        return ms.ToArray();
    }

    private static byte[] Be32(uint v)
    {
        byte[] b = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(b, v);
        return b;
    }

    private static byte[] Be32(int v) => Be32((uint)v);

    /// <summary>14-byte IW6 file header + 1 skipped byte + ConstantTypeCount=0.</summary>
    private static byte[] MinimalHeader(byte gameByte = 0x03)
    {
        return Concat(
            new byte[] { 0x1B, (byte)'L', (byte)'u', (byte)'a', 0x51 }, // magic + version
            new byte[] { 0x0D }, // format
            new byte[] { 0x00 }, // endianness (BE flagged)
            new byte[] { 0x04, 0x04, 0x04, 0x04 }, // sizes: int / size_t / instruction / lua_Number
            new byte[] { 0x00 }, // integral
            new byte[] { gameByte },
            new byte[] { 0x00 }, // skip byte
            Be32(0));            // ConstantTypeCount = 0 (no type-name table)
    }

    /// <summary>
    /// Build a function-prototype prefix matching what
    /// <see cref="IW6LuaBytecodeReader"/> actually consumes: zero
    /// upvalues/params, non-vararg, given register/instruction values, and
    /// the alignment pad bytes needed to land the cursor on a 4-byte boundary
    /// given the function's absolute start position. IW6's variant has no
    /// "Unknown" int between InstructionCount and instructions (that's T6/T7).
    /// </summary>
    /// <param name="functionStartOffset">Absolute byte offset where this
    /// function starts in the body. Drives the pad-byte calculation.</param>
    private static byte[] FunctionPrefix(int registers, int instructionCount, int functionStartOffset)
    {
        // Parser consumes: 4 (upvalues) + 4 (params) + 1 (varArg) + 4 (regs) +
        // 4 (ins) = 17 info bytes. Then pads to next 4-byte boundary.
        int after17 = functionStartOffset + 17;
        int padBytes = (4 - after17 % 4) % 4;
        return Concat(
            Be32(0), Be32(0), new byte[] { 0x00 }, // upvalues, params, varArg=false
            Be32(registers),
            Be32(instructionCount),
            new byte[padBytes]);
    }

    /// <summary>A single zeroed HavokScript instruction word (4 bytes).</summary>
    private static byte[] ZeroInstruction() => new byte[4];

    /// <summary>IW6 string constant body: <c>[type=TString][length BE u32][length bytes including null]</c>.</summary>
    private static byte[] StringConstant(string s)
    {
        var name = Encoding.ASCII.GetBytes(s);
        return Concat(new byte[] { (byte)IW6LuaConstantType.TString }, Be32(name.Length + 1), name, new byte[] { 0 });
    }

    /// <summary>Footer: [Unknown BE u32 = 0][SubFunctionCount BE u32 = 0].</summary>
    private static byte[] EmptyFooter() => Concat(Be32(0), Be32(0));

    // ============================================================
    // Header / module-level
    // ============================================================

    [Fact]
    public void Parse_RejectsNonLuaMagic()
    {
        byte[] body = Concat(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x51, 0x0D, 0x00, 0x04, 0x04, 0x04, 0x04, 0x00, 0x03, 0x00 }, Be32(0));
        Assert.Throws<InvalidDataException>(() => IW6LuaBytecodeReader.Parse(body));
    }

    [Fact]
    public void Parse_RejectsWrongLuaVersion()
    {
        byte[] body = Concat(
            new byte[] { 0x1B, (byte)'L', (byte)'u', (byte)'a', 0x52 /* not 5.1 */ },
            new byte[] { 0x0D, 0x00, 0x04, 0x04, 0x04, 0x04, 0x00, 0x03, 0x00 },
            Be32(0));
        Assert.Throws<InvalidDataException>(() => IW6LuaBytecodeReader.Parse(body));
    }

    [Fact]
    public void Parse_RejectsNonIw6GameByte()
    {
        byte[] body = Concat(
            new byte[] { 0x1B, (byte)'L', (byte)'u', (byte)'a', 0x51, 0x0D, 0x00, 0x04, 0x04, 0x04, 0x04, 0x00 },
            new byte[] { 0x00 /* T6 game byte, not 0x03 */, 0x00 },
            Be32(0));
        Assert.Throws<InvalidDataException>(() => IW6LuaBytecodeReader.Parse(body));
    }

    [Fact]
    public void Parse_TryParseSurfacesErrorAndReturnsNull()
    {
        byte[] body = new byte[] { 0x00, 0x01, 0x02 }; // too short
        var module = IW6LuaBytecodeReader.TryParse(body, out string? error);
        Assert.Null(module);
        Assert.NotNull(error);
    }

    /// <summary>Byte size of <see cref="MinimalHeader"/> — root function starts here.</summary>
    private const int MinimalHeaderSize = 18;

    [Fact]
    public void Parse_HeaderFieldsPopulated()
    {
        byte[] body = Concat(
            MinimalHeader(),
            FunctionPrefix(registers: 0, instructionCount: 0, functionStartOffset: MinimalHeaderSize),
            Be32(0), // constant count
            EmptyFooter());

        var module = IW6LuaBytecodeReader.Parse(body);

        Assert.Equal(0x51, module.Header.Version);
        Assert.Equal(0x0D, module.Header.Format);
        Assert.True(module.Header.IsCustomFormat());
        Assert.Equal(0x03, module.Header.GameByte);
        Assert.Equal(0, module.Header.ConstantTypeCount);
    }

    // ============================================================
    // Type-name table skip
    // ============================================================

    [Fact]
    public void Parse_SkipsTypeNameTable()
    {
        // Header with ConstantTypeCount = 2, then 2 type-name entries, then
        // an empty function. The parser must skip the table without choking.
        var headerWithTable = Concat(
            new byte[] { 0x1B, (byte)'L', (byte)'u', (byte)'a', 0x51, 0x0D, 0x00, 0x04, 0x04, 0x04, 0x04, 0x00, 0x03, 0x00 },
            Be32(2),
            Be32(0), Be32(5), Encoding.ASCII.GetBytes("TNIL\0"),     // type 0 + length 5 + content
            Be32(1), Be32(9), Encoding.ASCII.GetBytes("TBOOLEAN\0"));

        byte[] body = Concat(
            headerWithTable,
            FunctionPrefix(0, 0, functionStartOffset: headerWithTable.Length),
            Be32(0),
            EmptyFooter());

        var module = IW6LuaBytecodeReader.Parse(body);
        Assert.Equal(2, module.Header.ConstantTypeCount);
        Assert.Empty(module.Root.Instructions);
    }

    [Fact]
    public void Parse_RejectsImplausibleConstantTypeCount()
    {
        byte[] body = Concat(
            new byte[] { 0x1B, (byte)'L', (byte)'u', (byte)'a', 0x51, 0x0D, 0x00, 0x04, 0x04, 0x04, 0x04, 0x00, 0x03, 0x00 },
            Be32(99_999),
            new byte[100]);
        Assert.Throws<InvalidDataException>(() => IW6LuaBytecodeReader.Parse(body));
    }

    // ============================================================
    // Function prototype tree
    // ============================================================

    [Fact]
    public void Parse_ReadsInstructionsAndConstants()
    {
        // 3 instructions, 2 string constants.
        byte[] body = Concat(
            MinimalHeader(),
            FunctionPrefix(registers: 4, instructionCount: 3, functionStartOffset: MinimalHeaderSize),
            ZeroInstruction(), ZeroInstruction(), ZeroInstruction(),
            Be32(2),
            StringConstant("module"),
            StringConstant("package"),
            EmptyFooter());

        var module = IW6LuaBytecodeReader.Parse(body);
        Assert.Equal(3, module.Root.Instructions.Length);
        Assert.Equal(2, module.Root.Constants.Length);
        Assert.Equal(IW6LuaConstantType.TString, module.Root.Constants[0].Type);
        Assert.Equal("module", module.Root.Constants[0].Value);
        Assert.Equal("package", module.Root.Constants[1].Value);
    }

    /// <summary>
    /// Build a leaf function at <paramref name="startOffset"/>: 1 instruction,
    /// no constants, no sub-functions.
    /// </summary>
    private static byte[] LeafFunction(int startOffset)
        => Concat(
            FunctionPrefix(registers: 1, instructionCount: 1, functionStartOffset: startOffset),
            ZeroInstruction(),
            Be32(0),
            EmptyFooter());

    [Fact]
    public void Parse_RecursesIntoSubFunctions()
    {
        // Root with 2 sub-functions, each leaf with 1 instruction + 0 constants.
        // Compute offsets stepwise so each function's pad lands correctly.
        var rootPrefix = FunctionPrefix(0, 0, MinimalHeaderSize);
        int afterRootHeader = MinimalHeaderSize + rootPrefix.Length;
        int afterRootConsts = afterRootHeader + 4;   // constant count
        int afterRootFooter = afterRootConsts + 8;   // footer
        int leaf1Start = afterRootFooter;
        var leaf1 = LeafFunction(leaf1Start);
        int leaf2Start = leaf1Start + leaf1.Length;
        var leaf2 = LeafFunction(leaf2Start);

        byte[] body = Concat(
            MinimalHeader(),
            rootPrefix,
            Be32(0),
            Concat(Be32(0), Be32(2)),                 // footer: Unknown=0, SubFunctionCount=2
            leaf1, leaf2);

        var module = IW6LuaBytecodeReader.Parse(body);
        Assert.Equal(3, module.FunctionCount);
        Assert.Equal(2, module.Root.SubFunctions.Length);
        Assert.Single(module.Root.SubFunctions[0].Instructions);
        Assert.Single(module.Root.SubFunctions[1].Instructions);
    }

    [Fact]
    public void Parse_TotalCountsAggregateAcrossTree()
    {
        var rootPrefix = FunctionPrefix(0, 1, MinimalHeaderSize);
        // root body: instruction (4) + const count (4) + StringConstant("root") + footer (8)
        var rootConst = StringConstant("root");
        int afterRoot = MinimalHeaderSize + rootPrefix.Length + 4 + 4 + rootConst.Length + 8;
        var subPrefix = FunctionPrefix(1, 2, afterRoot);
        var subConst  = StringConstant("foo");
        byte[] body = Concat(
            MinimalHeader(),
            rootPrefix,
            ZeroInstruction(),
            Be32(1),
            rootConst,
            Concat(Be32(0), Be32(1)),                 // root footer: SubFunctionCount=1
            subPrefix,
            ZeroInstruction(), ZeroInstruction(),
            Be32(1),
            subConst,
            EmptyFooter());

        var module = IW6LuaBytecodeReader.Parse(body);
        Assert.Equal(2, module.FunctionCount);
        Assert.Equal(3, module.TotalInstructionCount);
        Assert.Equal(2, module.TotalConstantCount);
    }

    [Fact]
    public void Parse_RejectsImplausibleInstructionCount()
    {
        byte[] body = Concat(
            MinimalHeader(),
            FunctionPrefix(0, 9_999_999, MinimalHeaderSize),
            new byte[100]);
        Assert.Throws<InvalidDataException>(() => IW6LuaBytecodeReader.Parse(body));
    }

    // ============================================================
    // Constants
    // ============================================================

    [Fact]
    public void Parse_NilConstantConsumesOneByte()
    {
        byte[] body = Concat(
            MinimalHeader(),
            FunctionPrefix(1, 0, MinimalHeaderSize),
            Be32(1),
            new byte[] { (byte)IW6LuaConstantType.TNil },
            EmptyFooter());

        var module = IW6LuaBytecodeReader.Parse(body);
        Assert.Single(module.Root.Constants);
        Assert.Equal(IW6LuaConstantType.TNil, module.Root.Constants[0].Type);
        Assert.Null(module.Root.Constants[0].Value);
    }

    [Fact]
    public void Parse_BooleanConstantConsumesTwoBytes()
    {
        byte[] body = Concat(
            MinimalHeader(),
            FunctionPrefix(1, 0, MinimalHeaderSize),
            Be32(2),
            new byte[] { (byte)IW6LuaConstantType.TBoolean, 0x01 },
            new byte[] { (byte)IW6LuaConstantType.TBoolean, 0x00 },
            EmptyFooter());

        var module = IW6LuaBytecodeReader.Parse(body);
        Assert.Equal(true, module.Root.Constants[0].Value);
        Assert.Equal(false, module.Root.Constants[1].Value);
    }

    [Fact]
    public void Parse_ThrowsOnUnsupportedConstantType()
    {
        byte[] body = Concat(
            MinimalHeader(),
            FunctionPrefix(1, 0, MinimalHeaderSize),
            Be32(1),
            new byte[] { (byte)IW6LuaConstantType.TTable }, // not yet supported
            EmptyFooter());

        Assert.Throws<InvalidDataException>(() => IW6LuaBytecodeReader.Parse(body));
    }
}

/// <summary>Extension to access the IsCustomFormat flag from tests cleanly.</summary>
internal static class IW6FileHeaderTestExtensions
{
    public static bool IsCustomFormat(this IW6FileHeader h) => h.Format != 0;
}
