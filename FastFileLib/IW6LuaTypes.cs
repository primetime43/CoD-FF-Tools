// EXPERIMENTAL — DTOs for the IW6 HavokScript reader / disassembler in
// FastFileLib. See IW6LuaBytecodeReader's doc-comment for the full status
// (parser+disassembler are preserved for future format work, but their
// per-instruction operand values aren't trustworthy and the editor doesn't
// use them). Don't surface values from these DTOs in UX without a disclaimer.

namespace FastFileLib;

/// <summary>
/// IW6 (Ghosts) HavokScript constant types. Values 0–8 match stock Lua 5.1's
/// <c>LUA_T*</c> type tags; 9–14 are HavokScript extensions.
/// </summary>
public enum IW6LuaConstantType : byte
{
    TNil           = 0,
    TBoolean       = 1,
    TLightUserData = 2,
    TNumber        = 3,
    TString        = 4,
    TTable         = 5,
    TFunction      = 6,
    TUserData      = 7,
    TThread        = 8,
    TIFunction     = 9,
    TCFunction     = 10,
    TUI64          = 11,
    TStruct        = 12,
    THash          = 13,
    TUnk           = 14,
}

/// <summary>
/// File-level header parsed from the 14 bytes preceding the type-name table.
/// Most fields are informational; the parser only consults
/// <see cref="ConstantTypeCount"/> for control flow.
/// </summary>
public sealed class IW6FileHeader
{
    public IW6FileHeader(
        byte version, byte format, byte endianness,
        byte sizeofInt, byte sizeofSizeT, byte sizeofInstruction, byte sizeofLuaNumber,
        byte integralFlag, byte gameByte, int constantTypeCount)
    {
        Version = version;
        Format = format;
        Endianness = endianness;
        SizeofInt = sizeofInt;
        SizeofSizeT = sizeofSizeT;
        SizeofInstruction = sizeofInstruction;
        SizeofLuaNumber = sizeofLuaNumber;
        IntegralFlag = integralFlag;
        GameByte = gameByte;
        ConstantTypeCount = constantTypeCount;
    }
    public byte Version { get; }            // 0x51 = Lua 5.1
    public byte Format { get; }             // 0x0D for IW6 (custom)
    public byte Endianness { get; }         // 0 = BE, 1 = LE
    public byte SizeofInt { get; }
    public byte SizeofSizeT { get; }
    public byte SizeofInstruction { get; }
    public byte SizeofLuaNumber { get; }
    public byte IntegralFlag { get; }       // 0 = floating point lua_Number
    public byte GameByte { get; }           // 0x03 = IW6 (Ghosts)
    public int ConstantTypeCount { get; }   // 13 for IW6 — pre-declared type registry
}

/// <summary>
/// A constant entry from a function prototype's constant pool. <see cref="Value"/>
/// is one of: <c>null</c> (TNil), <c>bool</c>, <c>float</c>, <c>ulong</c>, or <c>string</c>.
/// </summary>
public readonly struct IW6LuaConstant
{
    public IW6LuaConstant(IW6LuaConstantType type, object? value)
    {
        Type = type;
        Value = value;
    }
    public IW6LuaConstantType Type { get; }
    public object? Value { get; }

    public override string ToString() => Type switch
    {
        IW6LuaConstantType.TNil => "nil",
        IW6LuaConstantType.TBoolean => Value is bool b ? (b ? "true" : "false") : "?",
        IW6LuaConstantType.TString => $"\"{Escape(Value as string ?? "")}\"",
        IW6LuaConstantType.TNumber => Value is float f ? f.ToString("R", System.Globalization.CultureInfo.InvariantCulture) : "?",
        IW6LuaConstantType.TUI64 or IW6LuaConstantType.THash => Value is ulong u ? $"0x{u:X16}" : "?",
        _ => $"<{Type}>",
    };

    private static string Escape(string s)
    {
        // Minimal escape for viewer readability — newline/tab/quote/backslash.
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new System.Text.StringBuilder(s.Length + 4);
        foreach (char c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"':  sb.Append("\\\""); break;
                case '\n': sb.Append("\\n");  break;
                case '\r': sb.Append("\\r");  break;
                case '\t': sb.Append("\\t");  break;
                default:
                    if (c < 0x20 || c == 0x7F) sb.Append($"\\x{(int)c:X2}");
                    else sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }
}

/// <summary>
/// A single HavokScript instruction decoded from the packed 4-byte encoding.
/// Operand interpretation depends on the <see cref="OpCode"/> — values for fields
/// not used by a given instruction are present but meaningless.
/// </summary>
public readonly struct IW6LuaInstruction
{
    public IW6LuaInstruction(uint opcode, uint a, uint b, uint c, uint bx, int sbx, bool extraCBit)
    {
        OpCode = opcode;
        A = a;
        B = b;
        C = c;
        Bx = bx;
        SBx = sbx;
        ExtraCBit = extraCBit;
    }
    public uint OpCode { get; }       // 7 bits — HavokScript opcode index
    public uint A { get; }            // 8-bit register
    public uint B { get; }            // 8-bit register/operand
    public uint C { get; }            // 9-bit register/operand (low byte + ExtraCBit)
    public uint Bx { get; }           // combined B*512 + C + (ExtraCBit?256:0) — 18-bit constant index
    public int SBx { get; }           // signed Bx = Bx - 65535 (for jumps)
    public bool ExtraCBit { get; }
}

/// <summary>
/// One function prototype — possibly the root, possibly a nested closure body.
/// </summary>
public sealed class IW6LuaFunction
{
    public IW6LuaFunction(
        int upvaluesCount, int parameterCount, bool usesVarArg, int registerCount,
        int unknownHeaderU32,
        IW6LuaInstruction[] instructions, IW6LuaConstant[] constants,
        int footerUnknown, IW6LuaFunction[] subFunctions,
        int sourceStartOffset, int sourceEndOffset)
    {
        UpvaluesCount = upvaluesCount;
        ParameterCount = parameterCount;
        UsesVarArg = usesVarArg;
        RegisterCount = registerCount;
        UnknownHeaderU32 = unknownHeaderU32;
        Instructions = instructions;
        Constants = constants;
        FooterUnknown = footerUnknown;
        SubFunctions = subFunctions;
        SourceStartOffset = sourceStartOffset;
        SourceEndOffset = sourceEndOffset;
    }
    public int UpvaluesCount { get; }
    public int ParameterCount { get; }
    public bool UsesVarArg { get; }
    public int RegisterCount { get; }
    /// <summary>Unknown field from the function header (possibly DefinedLine, possibly a hash).</summary>
    public int UnknownHeaderU32 { get; }
    public IW6LuaInstruction[] Instructions { get; }
    public IW6LuaConstant[] Constants { get; }
    /// <summary>Unknown field from the function footer (possibly DefinedLine or local-count).</summary>
    public int FooterUnknown { get; }
    public IW6LuaFunction[] SubFunctions { get; }
    /// <summary>Byte offset within the body where this function's data starts.</summary>
    public int SourceStartOffset { get; }
    /// <summary>Byte offset (exclusive) where this function's data ends.</summary>
    public int SourceEndOffset { get; }
}

/// <summary>
/// Result of parsing an IW6 luafile body. <see cref="Root"/> is the top-level
/// chunk's function prototype; nested closures are reached via
/// <see cref="IW6LuaFunction.SubFunctions"/>.
/// </summary>
public sealed class IW6LuaModule
{
    public IW6LuaModule(IW6FileHeader header, IW6LuaFunction root, int bodySize, long bytesRead)
    {
        Header = header;
        Root = root;
        BodySize = bodySize;
        BytesRead = bytesRead;
    }
    public IW6FileHeader Header { get; }
    public IW6LuaFunction Root { get; }
    public int BodySize { get; }
    /// <summary>How many bytes the parser consumed. Equal to <see cref="BodySize"/>
    /// for well-formed inputs; less if trailing bytes are present (some samples
    /// pad to alignment after the last sub-function).</summary>
    public long BytesRead { get; }

    /// <summary>Count every function prototype in the tree, including root.</summary>
    public int FunctionCount => 1 + CountSubs(Root);
    /// <summary>Sum of instruction counts across all functions.</summary>
    public int TotalInstructionCount => CountInstructions(Root);
    /// <summary>Sum of constants across all functions (counts duplicates).</summary>
    public int TotalConstantCount => CountConstants(Root);

    private static int CountSubs(IW6LuaFunction f)
    {
        int n = f.SubFunctions.Length;
        foreach (var s in f.SubFunctions) n += CountSubs(s);
        return n;
    }
    private static int CountInstructions(IW6LuaFunction f)
    {
        int n = f.Instructions.Length;
        foreach (var s in f.SubFunctions) n += CountInstructions(s);
        return n;
    }
    private static int CountConstants(IW6LuaFunction f)
    {
        int n = f.Constants.Length;
        foreach (var s in f.SubFunctions) n += CountConstants(s);
        return n;
    }
}
