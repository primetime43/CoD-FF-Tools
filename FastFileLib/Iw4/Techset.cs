// =============================================================================
// IW4 (MW2 PS3) zone reader — ported from Jacob Schroeder's FastFile
//   https://github.com/jacob-schroeder/FastFile
// Ports: FastFile.Logic/Assets/Readers/TechsetReader.cs (technique/pass/shader walk)
//        + FastFile.Models/Assets/TechniqueSet/MaterialTechniqueSet.cs (models).
//
// A techset's techniques[37] are pointers; in a self-contained zone they're stored INLINE
// (technique → passes → vertex decl / vertex+pixel shaders / shader args). This data is
// allocated from different XFILE_BLOCKs — vertex decl / args / names / literal consts from
// LARGE, and the shaders' compiled bytecode from TEMP (4-byte aligned to the TEMP cursor).
// Consuming it with the multi-block engine (ResolvePointerInBlock / …AlignedInBlock) keeps the
// walk aligned so the assets after the techset (the materials) parse correctly.
// =============================================================================

namespace FastFileLib.Iw4;

public class MaterialTechnique
{
    public int Offset { get; set; }
    public ZonePointer<string>? NamePtr { get; set; }
    public string Name => NamePtr is { IsResolved: true } ? NamePtr.Result ?? string.Empty : string.Empty;
    public ushort Flags { get; set; }
    public ushort PassCount { get; set; }
    public MaterialPass[] Passes { get; set; } = Array.Empty<MaterialPass>();
}

public class MaterialPass
{
    public int Offset { get; set; }
    public ZonePointer<MaterialVertexDeclaration>? VertexDecl { get; set; }
    public ZonePointer<MaterialVertexShader>? VertexShader { get; set; }
    public ZonePointer<MaterialPixelShader>? PixelShader { get; set; }
    public byte PerPrimArgCount, PerObjArgCount, StableArgCount, CustomSamplerFlags, PrecompiledIndex;
    public byte[] Padding { get; set; } = new byte[3];
    public ZonePointer<MaterialShaderArgument[]>? Args { get; set; }
    public int ArgCount => PerPrimArgCount + PerObjArgCount + StableArgCount;
}

public enum MaterialShaderArgumentType : ushort
{
    MTL_ARG_MATERIAL_VERTEX_CONST = 0x0,
    MTL_ARG_LITERAL_VERTEX_CONST = 0x1,
    MTL_ARG_MATERIAL_PIXEL_SAMPLER = 0x2,
    MTL_ARG_CODE_VERTEX_CONST = 0x3,
    MTL_ARG_CODE_PIXEL_SAMPLER = 0x4,
    MTL_ARG_CODE_PIXEL_CONST = 0x5,
    MTL_ARG_MATERIAL_PIXEL_CONST = 0x6,
    MTL_ARG_LITERAL_PIXEL_CONST = 0x7,
}

public class MaterialShaderArgument
{
    public MaterialShaderArgumentType Type { get; set; }
    public ushort Dest { get; set; }
    public int Raw { get; set; }
    public ZonePointer<float[]>? LiteralConst { get; set; }
}

public class MaterialVertexDeclaration
{
    public int Offset { get; set; }
    public byte[] Raw { get; set; } = new byte[0x1C];
}

public class MaterialVertexShader : BaseAsset
{
    public MaterialVertexShader() : base(XAssetType.VertexShader) { }
    public ZonePointer<string>? NamePtr { get; set; }
    public string Name => NamePtr is { IsResolved: true } ? NamePtr.Result ?? string.Empty : string.Empty;
    public ZonePointer<byte[]>? ProgramData { get; set; }
    public int ProgramDataSize { get; set; }
    public override string? GetDisplayName => string.IsNullOrWhiteSpace(Name) ? $"VertexShader 0x{Offset:X8}" : Name;
}

public class MaterialPixelShader : BaseAsset
{
    public MaterialPixelShader() : base(XAssetType.PixelShader) { }
    public ZonePointer<string>? NamePtr { get; set; }
    public string Name => NamePtr is { IsResolved: true } ? NamePtr.Result ?? string.Empty : string.Empty;
    public ZonePointer<byte[]>? ProgramData { get; set; }
    public int ProgramDataSize { get; set; }
    public byte[] RootSuffix { get; set; } = new byte[0x0C];
    public override string? GetDisplayName => string.IsNullOrWhiteSpace(Name) ? $"PixelShader 0x{Offset:X8}" : Name;
}

internal static class TechniqueReader
{
    private const int MaxPassCount = 64;
    private const int MaxArgCount = 128;
    private const int MaxShaderProgramBytes = 2 * 1024 * 1024;

    // Resolve a techset technique pointer (LARGE block): if inline, the MaterialTechnique body follows.
    public static void ResolveTechnique(ref ZoneReadContext context, ZonePointer<MaterialTechnique> pointer)
        => pointer.SetResult(context.ReadPointerValue(pointer, ReadTechnique));

    private static MaterialTechnique ReadTechnique(ref ZoneReadContext context)
    {
        var technique = new MaterialTechnique
        {
            Offset = context.Position,
            NamePtr = GenericReader.ReadStringPointer(ref context, resolve: false),
            Flags = context.ReadUInt16(),
            PassCount = context.ReadUInt16(),
        };

        if (technique.PassCount > MaxPassCount)
            throw new InvalidDataException(
                $"MaterialTechnique.PassCount {technique.PassCount} exceeds {MaxPassCount} at zone offset 0x{technique.Offset:X8}; technique root desynced.");

        // passCount * 0x18-byte MaterialPass rows follow the technique header inline.
        technique.Passes = new MaterialPass[technique.PassCount];
        for (var i = 0; i < technique.Passes.Length; i++)
            technique.Passes[i] = ReadPass(ref context);

        context.ResolvePointerInBlock(technique.NamePtr!, ZoneStreamBlock.Large, GenericReader.ReadStringPointerValue);
        return technique;
    }

    private static MaterialPass ReadPass(ref ZoneReadContext context)
    {
        var pass = new MaterialPass
        {
            Offset = context.Position,
            VertexDecl = context.ReadPointer<MaterialVertexDeclaration>(),
            VertexShader = context.ReadPointer<MaterialVertexShader>(),
            PixelShader = context.ReadPointer<MaterialPixelShader>(),
            PerPrimArgCount = context.ReadByte(),
            PerObjArgCount = context.ReadByte(),
            StableArgCount = context.ReadByte(),
            CustomSamplerFlags = context.ReadByte(),
            PrecompiledIndex = context.ReadByte(),
            Padding = context.ReadBytes(3),
            Args = context.ReadPointer<MaterialShaderArgument[]>(),
        };

        var argCount = pass.ArgCount;
        if (argCount > MaxArgCount)
            throw new InvalidDataException(
                $"MaterialPass arg count {argCount} exceeds {MaxArgCount} at zone offset 0x{pass.Offset:X8}; pass root desynced.");

        // Vertex decl + shader args + literal consts come from LARGE; the shaders themselves (name +
        // bytecode) come from TEMP. Resolution order matches the reference (= the inline layout order).
        context.ResolvePointerInBlock(pass.VertexDecl!, ZoneStreamBlock.Large,
            (ref ZoneReadContext c, ZonePointer<MaterialVertexDeclaration> p) =>
                p.SetResult(c.ReadPointerValue(p, ReadVertexDecl)));
        context.ResolvePointerInBlock(pass.VertexShader!, ZoneStreamBlock.Temp,
            (ref ZoneReadContext c, ZonePointer<MaterialVertexShader> p) =>
                p.SetResult(c.ReadPointerValue(p, ReadVertexShader)));
        context.ResolvePointerInBlock(pass.PixelShader!, ZoneStreamBlock.Temp,
            (ref ZoneReadContext c, ZonePointer<MaterialPixelShader> p) =>
                p.SetResult(c.ReadPointerValue(p, ReadPixelShader)));
        context.ResolvePointerInBlock(pass.Args!, ZoneStreamBlock.Large,
            (ref ZoneReadContext c, ZonePointer<MaterialShaderArgument[]> p) =>
            {
                var values = new MaterialShaderArgument[Math.Max(0, argCount)];
                for (var i = 0; i < values.Length; i++)
                    values[i] = ReadShaderArgument(ref c);
                p.SetResult(values);
            });

        return pass;
    }

    private static MaterialVertexDeclaration ReadVertexDecl(ref ZoneReadContext context)
        => new() { Offset = context.Position, Raw = context.ReadBytes(0x1C) };

    private static MaterialShaderArgument ReadShaderArgument(ref ZoneReadContext context)
    {
        var arg = new MaterialShaderArgument
        {
            Type = (MaterialShaderArgumentType)context.ReadUInt16(),
            Dest = context.ReadUInt16(),
            Raw = context.ReadInt32(),
        };

        if (arg.Type is MaterialShaderArgumentType.MTL_ARG_LITERAL_VERTEX_CONST
            or MaterialShaderArgumentType.MTL_ARG_LITERAL_PIXEL_CONST)
        {
            arg.LiteralConst = new ZonePointer<float[]>(arg.Raw);
            context.ResolvePointerInBlock(arg.LiteralConst, ZoneStreamBlock.Large,
                (ref ZoneReadContext c, ZonePointer<float[]> p) =>
                {
                    var values = new float[4];
                    for (var i = 0; i < values.Length; i++)
                        values[i] = c.ReadFloat();
                    p.SetResult(values);
                });
        }

        return arg;
    }

    private static MaterialVertexShader ReadVertexShader(ref ZoneReadContext context)
    {
        var shader = new MaterialVertexShader
        {
            Offset = context.Position,
            NamePtr = GenericReader.ReadStringPointer(ref context, resolve: false),
            ProgramData = context.ReadPointer<byte[]>(),
            ProgramDataSize = context.ReadInt32(),
        };
        context.ResolvePointerInBlock(shader.NamePtr!, ZoneStreamBlock.Large, GenericReader.ReadStringPointerValue);
        ResolveProgramData(ref context, shader.ProgramData!, shader.ProgramDataSize);
        return shader;
    }

    private static MaterialPixelShader ReadPixelShader(ref ZoneReadContext context)
    {
        var shader = new MaterialPixelShader
        {
            Offset = context.Position,
            NamePtr = GenericReader.ReadStringPointer(ref context, resolve: false),
            ProgramData = context.ReadPointer<byte[]>(),
            ProgramDataSize = context.ReadInt32(),
            RootSuffix = context.ReadBytes(0x0C),
        };
        context.ResolvePointerInBlock(shader.NamePtr!, ZoneStreamBlock.Large, GenericReader.ReadStringPointerValue);
        ResolveProgramData(ref context, shader.ProgramData!, shader.ProgramDataSize);
        return shader;
    }

    // Shader bytecode is allocated from TEMP and 4-byte aligned against the TEMP cursor.
    private static void ResolveProgramData(ref ZoneReadContext context, ZonePointer<byte[]> pointer, int size)
    {
        if (size < 0 || size > MaxShaderProgramBytes)
            throw new InvalidDataException(
                $"Shader program data size {size} is out of range (0..{MaxShaderProgramBytes}); shader root desynced.");

        context.ResolvePointerAlignedInBlock(pointer, ZoneStreamBlock.Temp, alignment: 4,
            (ref ZoneReadContext c, ZonePointer<byte[]> p) => p.SetResult(c.ReadBytes(Math.Max(0, size))));
    }
}
