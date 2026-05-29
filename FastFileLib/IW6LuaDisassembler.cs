using System.Globalization;
using System.Text;

namespace FastFileLib;

/// <summary>
/// HavokScript instruction operand format. Determines how a decoded
/// <see cref="IW6LuaInstruction"/>'s A / B / C / Bx / SBx fields are rendered.
/// </summary>
public enum IW6LuaInstructionFormat
{
    /// <summary>Three operands: A, B, C.</summary>
    iABC,
    /// <summary>Two operands: A and the combined Bx (B*512 + C + ExtraCBit).</summary>
    iABx,
    /// <summary>Two operands: A and the signed combined SBx (Bx - 65535).</summary>
    iAsBx,
}

/// <summary>
/// HavokScript opcode metadata: mnemonic + operand-format hint. Sourced from
/// JariK's CoDLuaDecompiler opcode table (paths: <c>LuaHavokOpCode.cs</c> and
/// <c>HavokDefaultHavokLuaOpCodeTable.cs</c>). The format classification was
/// chosen here based on which fields each instruction actually uses — that's
/// not encoded in the upstream's table, only the opcode → mnemonic mapping is.
/// </summary>
public static class IW6LuaOpCodes
{
    public sealed class OpInfo
    {
        public OpInfo(string mnemonic, IW6LuaInstructionFormat format, bool bxIsKIndex = false)
        {
            Mnemonic = mnemonic;
            Format = format;
            BxIsKIndex = bxIsKIndex;
        }
        public string Mnemonic { get; }
        public IW6LuaInstructionFormat Format { get; }
        /// <summary>True when Bx (for iABx ops) is a constant-pool index that
        /// should be annotated with the constant value.</summary>
        public bool BxIsKIndex { get; }
    }

    /// <summary>Sentinel for opcodes outside the known table.</summary>
    public static readonly OpInfo Unknown = new("HKS_UNK", IW6LuaInstructionFormat.iABC);

    private static readonly OpInfo[] _table = BuildTable();

    public static OpInfo Get(uint opcode) =>
        opcode < _table.Length ? _table[(int)opcode] : Unknown;

    private static OpInfo[] BuildTable()
    {
        // 92 opcodes per HavokDefaultHavokLuaOpCodeTable. The format hints
        // mirror stock Lua 5.1 for the shared opcodes (MOVE/LOADK/GETGLOBAL
        // etc.) and best-guess for the HavokScript extensions.
        var iABC  = IW6LuaInstructionFormat.iABC;
        var iABx  = IW6LuaInstructionFormat.iABx;
        var iAsBx = IW6LuaInstructionFormat.iAsBx;
        return new[]
        {
            new OpInfo("GETFIELD",              iABC),                 //  0
            new OpInfo("TEST",                  iABC),                 //  1
            new OpInfo("CALL_I",                iABC),                 //  2
            new OpInfo("CALL_C",                iABC),                 //  3
            new OpInfo("EQ",                    iABC),                 //  4
            new OpInfo("EQ_BK",                 iABC),                 //  5
            new OpInfo("GETGLOBAL",             iABx, bxIsKIndex: true), //  6
            new OpInfo("MOVE",                  iABC),                 //  7
            new OpInfo("SELF",                  iABC),                 //  8
            new OpInfo("RETURN",                iABC),                 //  9
            new OpInfo("GETTABLE_S",            iABC),                 // 10
            new OpInfo("GETTABLE_N",            iABC),                 // 11
            new OpInfo("GETTABLE",              iABC),                 // 12
            new OpInfo("LOADBOOL",              iABC),                 // 13
            new OpInfo("TFORLOOP",              iABC),                 // 14
            new OpInfo("SETFIELD",              iABC),                 // 15
            new OpInfo("SETTABLE_S",            iABC),                 // 16
            new OpInfo("SETTABLE_S_BK",         iABC),                 // 17
            new OpInfo("SETTABLE_N",            iABC),                 // 18
            new OpInfo("SETTABLE_N_BK",         iABC),                 // 19
            new OpInfo("SETTABLE",              iABC),                 // 20
            new OpInfo("SETTABLE_BK",           iABC),                 // 21
            new OpInfo("TAILCALL_I",            iABC),                 // 22
            new OpInfo("TAILCALL_C",            iABC),                 // 23
            new OpInfo("TAILCALL_M",            iABC),                 // 24
            new OpInfo("LOADK",                 iABx, bxIsKIndex: true), // 25
            new OpInfo("LOADNIL",               iABC),                 // 26
            new OpInfo("SETGLOBAL",             iABx, bxIsKIndex: true), // 27
            new OpInfo("JMP",                   iAsBx),                // 28
            new OpInfo("CALL_M",                iABC),                 // 29
            new OpInfo("CALL",                  iABC),                 // 30
            new OpInfo("INTRINSIC_INDEX",       iABC),                 // 31
            new OpInfo("INTRINSIC_NEWINDEX",    iABC),                 // 32
            new OpInfo("INTRINSIC_SELF",        iABC),                 // 33
            new OpInfo("INTRINSIC_INDEX_LITERAL",    iABC),            // 34
            new OpInfo("INTRINSIC_NEWINDEX_LITERAL", iABC),            // 35
            new OpInfo("INTRINSIC_SELF_LITERAL",     iABC),            // 36
            new OpInfo("TAILCALL",              iABC),                 // 37
            new OpInfo("GETUPVAL",              iABC),                 // 38
            new OpInfo("SETUPVAL",              iABC),                 // 39
            new OpInfo("ADD",                   iABC),                 // 40
            new OpInfo("ADD_BK",                iABC),                 // 41
            new OpInfo("SUB",                   iABC),                 // 42
            new OpInfo("SUB_BK",                iABC),                 // 43
            new OpInfo("MUL",                   iABC),                 // 44
            new OpInfo("MUL_BK",                iABC),                 // 45
            new OpInfo("DIV",                   iABC),                 // 46
            new OpInfo("DIV_BK",                iABC),                 // 47
            new OpInfo("MOD",                   iABC),                 // 48
            new OpInfo("MOD_BK",                iABC),                 // 49
            new OpInfo("POW",                   iABC),                 // 50
            new OpInfo("POW_BK",                iABC),                 // 51
            new OpInfo("NEWTABLE",              iABC),                 // 52
            new OpInfo("UNM",                   iABC),                 // 53
            new OpInfo("NOT",                   iABC),                 // 54
            new OpInfo("LEN",                   iABC),                 // 55
            new OpInfo("LT",                    iABC),                 // 56
            new OpInfo("LT_BK",                 iABC),                 // 57
            new OpInfo("LE",                    iABC),                 // 58
            new OpInfo("LE_BK",                 iABC),                 // 59
            new OpInfo("CONCAT",                iABC),                 // 60
            new OpInfo("TESTSET",               iABC),                 // 61
            new OpInfo("FORPREP",               iAsBx),                // 62
            new OpInfo("FORLOOP",               iAsBx),                // 63
            new OpInfo("SETLIST",               iABC),                 // 64
            new OpInfo("CLOSE",                 iABC),                 // 65
            new OpInfo("CLOSURE",               iABx),                 // 66
            new OpInfo("VARARG",                iABC),                 // 67
            new OpInfo("TAILCALL_I_R1",         iABC),                 // 68
            new OpInfo("CALL_I_R1",             iABC),                 // 69
            new OpInfo("SETUPVAL_R1",           iABC),                 // 70
            new OpInfo("TEST_R1",               iABC),                 // 71
            new OpInfo("NOT_R1",                iABC),                 // 72
            new OpInfo("GETFIELD_R1",           iABC),                 // 73
            new OpInfo("SETFIELD_R1",           iABC),                 // 74
            new OpInfo("NEWSTRUCT",             iABC),                 // 75
            new OpInfo("DATA",                  iABx),                 // 76
            new OpInfo("SETSLOTN",              iABC),                 // 77
            new OpInfo("SETSLOTI",              iABC),                 // 78
            new OpInfo("SETSLOT",               iABC),                 // 79
            new OpInfo("SETSLOTS",              iABC),                 // 80
            new OpInfo("SETSLOTMT",             iABC),                 // 81
            new OpInfo("CHECKTYPE",             iABC),                 // 82
            new OpInfo("CHECKTYPES",            iABC),                 // 83
            new OpInfo("GETSLOT",               iABC),                 // 84
            new OpInfo("GETSLOTMT",             iABC),                 // 85
            new OpInfo("SELFSLOT",              iABC),                 // 86
            new OpInfo("SELFSLOTMT",            iABC),                 // 87
            new OpInfo("GETFIELD_MM",           iABC),                 // 88
            new OpInfo("CHECKTYPE_D",           iABC),                 // 89
            new OpInfo("GETSLOT_D",             iABC),                 // 90
            new OpInfo("GETGLOBAL_MEM",         iABx, bxIsKIndex: true), // 91
            new OpInfo("MAX",                   iABC),                 // 92
        };
    }
}

/// <summary>
/// <b>EXPERIMENTAL — output values not trustworthy.</b> Pairs with
/// <see cref="IW6LuaBytecodeReader"/>, which has known operand-decoding
/// issues (see that type's doc-comment for the full disclosure). Mnemonics
/// come from the verified upstream opcode table; <i>operand fields</i>
/// (R-indices, K-indices, Bx, SBx) reflect the bit-packing layout assumed
/// in the reader and are not yet correct. The editor's luafile viewer does
/// not call this disassembler; it's preserved here for future format work.
///
/// Disassembles an <see cref="IW6LuaModule"/> into a readable text listing.
/// Output format is one function per section: header (signature + counts),
/// instructions (mnemonic + operands, with constant-pool references resolved
/// inline), constants table, then any nested sub-functions recursively.
/// </summary>
public static class IW6LuaDisassembler
{
    public static string Disassemble(string assetName, IW6LuaModule module)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"-- {assetName}");
        sb.AppendLine($"-- Lua 5.1 HavokScript (IW6 / Ghosts), format byte 0x{module.Header.Format:X2}");
        sb.AppendLine($"-- {module.FunctionCount} function(s), {module.TotalInstructionCount} total instruction(s), " +
                      $"{module.TotalConstantCount} total constant(s)");
        if (module.BytesRead < module.BodySize)
            sb.AppendLine($"-- (parser consumed {module.BytesRead}/{module.BodySize} bytes; {module.BodySize - module.BytesRead} trailing bytes left)");
        sb.AppendLine("-- Disassembly note: operand decoding for the HavokScript bit-packed");
        sb.AppendLine("-- instruction word is partially reverse-engineered. Function structure +");
        sb.AppendLine("-- constants tables are reliable; per-instruction R/K indexes and");
        sb.AppendLine("-- the constants-vs-padding distinction in the pool may be off until the");
        sb.AppendLine("-- bit layout is fully nailed down.");
        sb.AppendLine();

        DisassembleFunction(sb, module.Root, path: "fn_0", depth: 0);
        return sb.ToString();
    }

    private static void DisassembleFunction(StringBuilder sb, IW6LuaFunction fn, string path, int depth)
    {
        sb.AppendLine($"function {path}({BuildParamList(fn)})");
        sb.AppendLine($"  -- upvalues={fn.UpvaluesCount} params={fn.ParameterCount} " +
                      $"varArg={fn.UsesVarArg} registers={fn.RegisterCount}");
        sb.AppendLine($"  -- {fn.Instructions.Length} instruction(s), {fn.Constants.Length} constant(s), " +
                      $"{fn.SubFunctions.Length} sub-function(s)");

        if (fn.Instructions.Length > 0)
        {
            sb.AppendLine("  -- code:");
            for (int i = 0; i < fn.Instructions.Length; i++)
            {
                sb.Append("  ").Append((i + 1).ToString("D4", CultureInfo.InvariantCulture)).Append("  ");
                sb.AppendLine(FormatInstruction(fn.Instructions[i], fn.Constants));
            }
        }

        if (fn.Constants.Length > 0)
        {
            sb.AppendLine("  -- constants:");
            for (int i = 0; i < fn.Constants.Length; i++)
            {
                sb.AppendLine($"    K{i,-4} {fn.Constants[i].Type,-10} {fn.Constants[i]}");
            }
        }
        sb.AppendLine("end");

        for (int i = 0; i < fn.SubFunctions.Length; i++)
        {
            sb.AppendLine();
            DisassembleFunction(sb, fn.SubFunctions[i], $"{path}_{i}", depth + 1);
        }
    }

    private static string BuildParamList(IW6LuaFunction fn)
    {
        var parts = new List<string>(fn.ParameterCount + 1);
        for (int i = 0; i < fn.ParameterCount; i++) parts.Add($"arg{i}");
        if (fn.UsesVarArg) parts.Add("...");
        return string.Join(", ", parts);
    }

    private static string FormatInstruction(IW6LuaInstruction ins, IW6LuaConstant[] constants)
    {
        var info = IW6LuaOpCodes.Get(ins.OpCode);
        var sb = new StringBuilder();
        sb.Append(info.Mnemonic.PadRight(18));

        switch (info.Format)
        {
            case IW6LuaInstructionFormat.iABC:
                sb.Append($"R{ins.A,-3} R{ins.B,-3} R{ins.C,-3}");
                AnnotateAbcConstantIfKish(sb, ins, constants);
                break;
            case IW6LuaInstructionFormat.iABx:
                sb.Append($"R{ins.A,-3} {(info.BxIsKIndex ? "K" : "")}{ins.Bx}");
                if (info.BxIsKIndex && ins.Bx < constants.Length)
                    sb.Append("   ; ").Append(constants[ins.Bx]);
                break;
            case IW6LuaInstructionFormat.iAsBx:
                sb.Append($"R{ins.A,-3} {ins.SBx:+0;-0;0}");
                break;
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// For arithmetic / comparison / table-access opcodes, B and C can encode
    /// register or constant indexes (the RK encoding in stock Lua uses the high
    /// bit as a discriminator). HavokScript uses a separate _BK variant for the
    /// "B is a constant" case rather than the bit trick, so the safer default
    /// here is to leave annotation off — we'd risk attributing wrong values.
    /// Override per-opcode if/when it's clear from real examples.
    /// </summary>
    private static void AnnotateAbcConstantIfKish(StringBuilder sb, IW6LuaInstruction ins, IW6LuaConstant[] constants)
    {
        // No-op for now — see XML doc.
        _ = sb; _ = ins; _ = constants;
    }
}
