// =============================================================================
// IW4 (MW2 PS3) zone reader — ported from Jacob Schroeder's FastFile
//   https://github.com/jacob-schroeder/FastFile
// Ports: FastFile.Models/Assets/Material/Material.cs (Material + Image + Water models).
// PS3 branch: Material.TECHNIQUE_COUNT = 38 (note: distinct from the techset's 37),
// MaterialInfo has trailing Padding, GfxStateBits uses a LoadBits pointer + Unknown.
// =============================================================================

namespace FastFileLib.Iw4;

public class Material : BaseAsset
{
    public const int TECHNIQUE_COUNT = 38; // PS3

    public Material() : base(XAssetType.Material) { }
    public MaterialInfo? Info { get; set; }
    public byte[] StateBitsEntry { get; set; } = new byte[TECHNIQUE_COUNT];
    public byte TextureCount, ConstantCount, StateBitsCount, StateFlags, CameraRegion;
    public ushort[] Ushorts { get; set; } = new ushort[TECHNIQUE_COUNT];
    public ZonePointer<ushort[]>? UshortArray { get; set; }
    public ZonePointer<MaterialTechniqueSet>? TechniqueSet { get; set; }
    public ZonePointer<MaterialTextureDef[]>? TextureTable { get; set; }
    public ZonePointer<MaterialConstantDef[]>? ConstantTable { get; set; }
    public ZonePointer<GfxStateBits[]>? StateBitTable { get; set; }
    public ZonePointer<ZonePointer<string>[]>? UnknownXStringArray { get; set; }

    public override string? GetDisplayName => Info?.Name ?? string.Empty;
}

public class MaterialConstantDef
{
    public int NameHash;
    public string Name { get; set; } = string.Empty;
    public Vec4 Literal;
}

public class GfxStateBits
{
    public ZonePointer<int[]>? LoadBits { get; set; } // PS3
    public int Unknown;
}

public class WaterWritable { public float FloatTime; }

public class Water
{
    public WaterWritable? Writable { get; set; }
    public ZonePointer<float[]>? H0X { get; set; }
    public ZonePointer<float[]>? H0Y { get; set; }
    public ZonePointer<float[]>? WTerm { get; set; }
    public int M, N;
    public float Lx, Lz, Gravity, Windvel;
    public float[] Winddir { get; set; } = new float[2];
    public float Amplitude;
    public float[] CodeConstant { get; set; } = new float[4];
    public ZonePointer<GfxImage>? Image { get; set; }
}

public enum MaterialTextureSemantic : byte
{
    TS_2D = 0x0,
    TS_WATER_MAP = 0xB,
}

public class MaterialTextureDefInfo
{
    public int Raw;
    public ZonePointer<GfxImage>? Image { get; set; }
    public ZonePointer<Water>? Water { get; set; }
}

public class MaterialTextureDef
{
    public uint NameHash;
    public byte NameStart, NameEnd, SampleState;
    public MaterialTextureSemantic Semantic;
    public byte IsMatureContent;
    public byte[] Pad { get; set; } = new byte[3];
    public MaterialTextureDefInfo? Info { get; set; }
}

public class GfxDrawSurfFields { public ulong Packed; }
public class GfxDrawSurf { public GfxDrawSurfFields? Fields { get; set; } public ulong Packed; }

public class MaterialInfo
{
    public ZonePointer<string>? NamePtr { get; set; }
    public string Name => NamePtr is { IsResolved: true } ? NamePtr.Result ?? string.Empty : string.Empty;
    public byte GameFlags, SortKey, TextureAtlasRowCount, TextureAtlasColumnCount;
    public GfxDrawSurf? DrawSurf { get; set; }
    public int SurfaceTypeBits;
    public int Padding; // PS3
}

public class GfxImage : BaseAsset
{
    public GfxImage() : base(XAssetType.Image) { }
    public ZonePointer<GfxImageLoadDef>? LoadDef { get; set; }
    public byte MapType, Semantic, Category, UseSrgbReads;
    public byte[] Picmip { get; set; } = new byte[2];
    public byte NoPicmip, Track;
    public int[] CardMemory { get; set; } = new int[2];
    public ZonePointer<string>? NamePtr { get; set; }
    public string Name => NamePtr is { IsResolved: true } ? NamePtr.Result ?? string.Empty : string.Empty;
    public ushort Width, Height, Depth;
    public byte DelayLoadPixels;
    public byte[] Pad { get; set; } = new byte[3];

    public override string? GetDisplayName => Name;
}

public class GfxImageLoadDef
{
    public byte LevelCount;
    public byte[] Pad { get; set; } = new byte[3];
    public int Flags, Format, ResourceSize;
    public byte[] Data { get; set; } = Array.Empty<byte>();
}
