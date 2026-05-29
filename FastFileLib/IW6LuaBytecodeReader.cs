using System.Buffers.Binary;
using System.Text;

namespace FastFileLib;

/// <summary>
/// <b>EXPERIMENTAL — operand values not verified.</b> The IW6 HavokScript
/// chunk format isn't fully mapped: this reader successfully consumes bytes
/// for every sample tested without throwing, but trace-debugging the upstream
/// CoDLuaDecompiler (which is itself broken on IW6 — see fork attempt notes
/// in the repo history) showed that the function-header field positions and
/// the constants-pool encoding don't line up with what this reader currently
/// assumes. Concretely:
/// <list type="bullet">
///   <item>File header + type-name table parsing: <b>verified correct</b>
///         (counts match expected values across all 86 luafiles in
///         patch_ui_mp.zone).</item>
///   <item>Function header: reads UpvaluesCount / ParameterCount correctly,
///         but RegisterCount + InstructionCount appear to come out of the
///         wrong bytes — sample squadprepartylobby.lua decodes as regs=25
///         ins=389 against the actual bytes, but neither value walks the
///         downstream constant pool cleanly.</item>
///   <item>Constants pool: <b>encoding unmapped</b>. The pool starts with a
///         u32 that's neither a count nor a type tag in any layout I tried;
///         strings appear in the bytes but reading them via stock
///         <c>[type byte][length BE u32][content bytes]</c> doesn't walk
///         cleanly through the pool.</item>
///   <item>Instruction bit-packing: A:B:C:OpCode positions produce
///         out-of-range register references (R182 in a function with 25
///         declared registers, etc.).</item>
/// </list>
/// The DTO tree this returns is structurally complete — useful for future
/// work where you trace one sample end-to-end against a known compiled
/// counterpart — but the operand values in <see cref="IW6LuaInstruction"/>
/// and the per-pool <see cref="IW6LuaConstant"/> values past the first
/// entries are <b>not trustworthy yet</b>. Don't surface them to users.
///
/// The editor's luafile viewer uses <see cref="LuaBytecodeInspector"/>
/// (format-agnostic ASCII-strings extraction) instead of this reader's
/// output for that reason.
///
/// Parses an IW6 (Ghosts) HavokScript bytecode body into an in-memory function
/// prototype tree. The format is a stock Lua 5.1 chunk extended with:
/// <list type="bullet">
///   <item>13-byte Lua header (12 stock + 1 <c>GameByte</c> identifying the dialect; 0x03 = IW6).</item>
///   <item>Skipped byte, then a u32 BE <c>ConstantTypeCount</c> followed by a type-name
///         table (used at runtime to dispatch constant decoding; we read it
///         to skip past it).</item>
///   <item>Recursive function prototypes with a packed 4-byte instruction encoding
///         (A:C:B:OpCode bit-packed across the four bytes).</item>
/// </list>
///
/// Format reverse-engineered by tracing real bytes from <c>patch_ui_mp.zone</c>
/// luafiles against the structure described in JariK's
/// <c>CoDLuaDecompiler</c> (specifically <c>HavokLuaFile</c> + <c>HavokLuaFunctionIW</c>).
/// Code in this file is original — only the on-disk layout is shared.
///
/// All multi-byte values are <b>big-endian</b> (the Endianness header byte reads
/// 0 in retail samples; PS3 is BE so this is expected). The upstream decoder
/// achieves this via a ReaderExtension wrapper; we use
/// <see cref="BinaryPrimitives.ReadInt32BigEndian"/> directly.
/// </summary>
public static class IW6LuaBytecodeReader
{
    // -------- header constants --------

    /// <summary><c>\x1B Lua</c> magic prefix (4 bytes).</summary>
    private static readonly byte[] LuaMagic = { 0x1B, (byte)'L', (byte)'u', (byte)'a' };

    /// <summary>Lua 5.1 version byte (5th byte of the file header).</summary>
    public const byte Lua51Version = 0x51;

    /// <summary>IW6 (Ghosts) game identifier (13th byte of the extended header).</summary>
    public const byte Iw6GameByte = 0x03;

    /// <summary>Number of fixed header bytes before <c>ConstantTypeCount</c>.</summary>
    private const int HeaderSize = 14;

    // -------- public API --------

    public static IW6LuaModule? TryParse(byte[] body, out string? error)
    {
        try
        {
            error = null;
            return Parse(body);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return null;
        }
    }

    public static IW6LuaModule Parse(byte[] body)
    {
        if (body == null) throw new ArgumentNullException(nameof(body));
        if (body.Length < HeaderSize + 4)
            throw new InvalidDataException("Lua bytecode body too short to contain a header.");
        if (!StartsWith(body, 0, LuaMagic))
            throw new InvalidDataException("Lua magic (1B 4C 75 61) not found at offset 0.");
        if (body[4] != Lua51Version)
            throw new InvalidDataException($"Expected Lua version 0x51 (5.1) at offset 4, got 0x{body[4]:X2}.");
        if (body[12] != Iw6GameByte)
            throw new InvalidDataException(
                $"Expected IW6 GameByte 0x03 at offset 12, got 0x{body[12]:X2}. This reader is IW6-specific.");

        var cursor = new Cursor(body);
        var header = ReadFileHeader(cursor);

        // Read but don't retain the type-name table — its sole purpose is to
        // map constant-type ints to readable names for runtime dispatch, and we
        // already know the canonical enum values.
        SkipTypeNameTable(cursor, header.ConstantTypeCount);

        var root = ReadFunction(cursor);

        return new IW6LuaModule(header, root, body.Length, cursor.Position);
    }

    // -------- header --------

    private static IW6FileHeader ReadFileHeader(Cursor cursor)
    {
        cursor.Skip(4); // magic
        byte version    = cursor.U8();
        byte format     = cursor.U8();
        byte endianness = cursor.U8();
        byte sizeofInt  = cursor.U8();
        byte sizeofSizeT = cursor.U8();
        byte sizeofInst = cursor.U8();
        byte sizeofNum  = cursor.U8();
        byte integral   = cursor.U8();
        byte gameByte   = cursor.U8();
        cursor.Skip(1); // upstream's "skipped byte" — observed 0x00 in samples
        int typeCount = cursor.S32BE();
        return new IW6FileHeader(
            version, format, endianness,
            sizeofInt, sizeofSizeT, sizeofInst, sizeofNum, integral, gameByte,
            typeCount);
    }

    private static void SkipTypeNameTable(Cursor cursor, int typeCount)
    {
        if (typeCount < 0 || typeCount > 1024)
            throw new InvalidDataException($"Implausible ConstantTypeCount: {typeCount}");
        for (int i = 0; i < typeCount; i++)
        {
            cursor.S32BE();         // type id (0=TNil, 1=TBoolean, …, 12=TStruct)
            int length = cursor.S32BE();
            if (length < 0 || length > 4096)
                throw new InvalidDataException($"Implausible type-name length at index {i}: {length}");
            cursor.Skip(length);
        }
    }

    // -------- function prototype (recursive) --------

    private static IW6LuaFunction ReadFunction(Cursor cursor)
    {
        long start = cursor.Position;

        int upvalues   = cursor.S32BE();
        int parameters = cursor.S32BE();
        bool varArg    = cursor.U8() != 0;

        // IW6 layout (reverse-engineered from squadprepartylobby.lua):
        // u32 RegisterCount, u32 InstructionCount (both unaligned), no Unknown int
        // following — that's a T6/T7 thing. Pad after the last u32 to the next
        // 4-byte boundary.
        int registers     = cursor.S32BE();
        int instructionCt = cursor.S32BE();
        int unknownU32    = 0;

        int pad = (int)((4 - cursor.Position % 4) % 4);
        cursor.Skip(pad);

        if (instructionCt < 0 || instructionCt > 1_000_000)
            throw new InvalidDataException($"Implausible instruction count: {instructionCt}");
        if (registers < 0 || registers > 4096)
            throw new InvalidDataException($"Implausible register count: {registers}");

        var instructions = new IW6LuaInstruction[instructionCt];
        for (int i = 0; i < instructionCt; i++)
            instructions[i] = ReadInstruction(cursor);

        var constants = ReadConstants(cursor);

        int footerUnk = cursor.S32BE();
        int subFuncCt = cursor.S32BE();
        if (subFuncCt < 0 || subFuncCt > 100_000)
            throw new InvalidDataException($"Implausible sub-function count: {subFuncCt}");

        var subFunctions = new IW6LuaFunction[subFuncCt];
        for (int i = 0; i < subFuncCt; i++)
            subFunctions[i] = ReadFunction(cursor);

        return new IW6LuaFunction(
            upvaluesCount: upvalues,
            parameterCount: parameters,
            usesVarArg: varArg,
            registerCount: registers,
            unknownHeaderU32: unknownU32,
            instructions: instructions,
            constants: constants,
            footerUnknown: footerUnk,
            subFunctions: subFunctions,
            sourceStartOffset: (int)start,
            sourceEndOffset: (int)cursor.Position);
    }

    /// <summary>
    /// HavokScript's packed 4-byte instruction encoding:
    /// <code>
    ///   byte0 = A           (8 bits)
    ///   byte1 = C low       (8 bits)
    ///   byte2 = B low &lt;&lt; 1 | ExtraCBit
    ///   byte3 = OpCode &lt;&lt; 1 | BHighBit
    /// </code>
    /// Decoded as: A:u8, C:u9 (low byte + ExtraCBit), B:u8 (low 7 bits + high bit
    /// from byte3 low bit), OpCode:u7 (upper 7 bits of byte3). Composite operands
    /// Bx and SBx are computed for instructions that use them.
    /// </summary>
    private static IW6LuaInstruction ReadInstruction(Cursor cursor)
    {
        byte b0 = cursor.U8();
        byte b1 = cursor.U8();
        byte b2 = cursor.U8();
        byte b3 = cursor.U8();

        uint a = b0;
        bool extraCBit = (b2 & 1) != 0;
        uint c = (uint)b1 | (extraCBit ? 0x100u : 0u);

        uint bLow = (uint)(b2 >> 1);
        uint bHigh = (uint)(b3 & 1) << 7;
        uint b = bLow | bHigh;

        uint opcode = (uint)(b3 >> 1);

        uint bx = (b * 512) + c + (extraCBit ? 256u : 0u);
        int sbx = (int)bx - 65536 + 1;

        return new IW6LuaInstruction(opcode, a, b, c, bx, sbx, extraCBit);
    }

    // -------- constants --------

    private static IW6LuaConstant[] ReadConstants(Cursor cursor)
    {
        int count = cursor.S32BE();
        if (count < 0 || count > 1_000_000)
            throw new InvalidDataException($"Implausible constant count: {count} at offset 0x{cursor.Position - 4:X}");
        var result = new IW6LuaConstant[count];
        for (int i = 0; i < count; i++)
            result[i] = ReadConstant(cursor);
        return result;
    }

    private static IW6LuaConstant ReadConstant(Cursor cursor)
    {
        var type = (IW6LuaConstantType)cursor.U8();
        return type switch
        {
            IW6LuaConstantType.TNil => new IW6LuaConstant(type, null),
            IW6LuaConstantType.TBoolean => new IW6LuaConstant(type, cursor.U8() != 0),
            IW6LuaConstantType.TNumber => new IW6LuaConstant(type, cursor.F32BE()),
            IW6LuaConstantType.TUI64 or IW6LuaConstantType.THash
                => new IW6LuaConstant(type, cursor.U64BE()),
            IW6LuaConstantType.TString => new IW6LuaConstant(type, ReadConstantString(cursor)),
            _ => throw new InvalidDataException(
                $"Unsupported constant type 0x{(byte)type:X2} at offset 0x{cursor.Position - 1:X}. " +
                $"Extend IW6LuaBytecodeReader.ReadConstant if this is a real format extension."),
        };
    }

    /// <summary>
    /// IW6 string constant: <c>[length BE u32][length bytes including null]</c>.
    /// Stock-Lua-5.1 shape; T6/T7 add an extra u32 between length and bytes
    /// which IW6 does not appear to use (verified against squadprepartylobby.lua).
    /// </summary>
    private static string ReadConstantString(Cursor cursor)
    {
        int length = cursor.S32BE();
        if (length < 0 || length > 16 * 1024 * 1024)
            throw new InvalidDataException($"Implausible string-constant length: {length}");
        if (length == 0) return string.Empty;
        // length includes the trailing null
        int strLen = length - 1;
        string s = cursor.Ascii(strLen);
        byte terminator = cursor.U8();
        if (terminator != 0)
            throw new InvalidDataException(
                $"String constant not null-terminated (got 0x{terminator:X2} at offset 0x{cursor.Position - 1:X}).");
        return s;
    }

    // -------- helpers --------

    private static bool StartsWith(byte[] body, int off, byte[] sig)
    {
        if (off < 0 || off + sig.Length > body.Length) return false;
        for (int i = 0; i < sig.Length; i++)
            if (body[off + i] != sig[i]) return false;
        return true;
    }

    /// <summary>Minimal mutable big-endian byte-stream reader. Throws on overrun.</summary>
    private sealed class Cursor
    {
        private readonly byte[] _data;
        private int _pos;
        public Cursor(byte[] data) { _data = data; _pos = 0; }
        public long Position => _pos;

        public void Skip(int n)
        {
            EnsureAvailable(n);
            _pos += n;
        }
        public byte U8()
        {
            EnsureAvailable(1);
            return _data[_pos++];
        }
        public int S32BE()
        {
            EnsureAvailable(4);
            int v = BinaryPrimitives.ReadInt32BigEndian(_data.AsSpan(_pos, 4));
            _pos += 4;
            return v;
        }
        public float F32BE()
        {
            EnsureAvailable(4);
            float v = BinaryPrimitives.ReadSingleBigEndian(_data.AsSpan(_pos, 4));
            _pos += 4;
            return v;
        }
        public ulong U64BE()
        {
            EnsureAvailable(8);
            ulong v = BinaryPrimitives.ReadUInt64BigEndian(_data.AsSpan(_pos, 8));
            _pos += 8;
            return v;
        }
        public string Ascii(int n)
        {
            EnsureAvailable(n);
            string s = Encoding.ASCII.GetString(_data, _pos, n);
            _pos += n;
            return s;
        }
        private void EnsureAvailable(int n)
        {
            if (_pos + n > _data.Length)
                throw new InvalidDataException(
                    $"Read past end of bytecode (need {n} bytes at offset 0x{_pos:X}, only {_data.Length - _pos} remain).");
        }
    }
}
