// =============================================================================
// IW4 (MW2 PS3) zone reader — ported from Jacob Schroeder's FastFile
//   https://github.com/jacob-schroeder/FastFile
// Ports: FastFile.Models/Assets/Material/Material.cs (Material + Image + Water models).
// PS3 branch (#if PS3 of the reference): Material.TECHNIQUE_COUNT = 37, MaterialInfo
// has a trailing Padding, the root carries UnknownXStringCount + MaterialPadding + the
// ushort[37] table + UshortPadding[2] + a (block) ushort[] pointer, and GfxStateBits
// uses a LoadBits pointer + Unknown. The material root is 144 bytes (after MaterialInfo).
// =============================================================================

namespace FastFileLib.Iw4;

public class Material : BaseAsset
{
    public const int TECHNIQUE_COUNT = 37; // PS3 (#elif XBOX 33, #else 48 in the reference)

    public Material() : base(XAssetType.Material) { }
    public MaterialInfo? Info { get; set; }
    public byte[] StateBitsEntry { get; set; } = new byte[TECHNIQUE_COUNT];
    public byte TextureCount, ConstantCount, StateBitsCount, StateFlags, CameraRegion, UnknownXStringCount;
    public byte MaterialPadding;                                       // PS3
    public ushort[] Ushorts { get; set; } = new ushort[TECHNIQUE_COUNT]; // PS3
    public byte[] UshortPadding { get; set; } = new byte[2];           // PS3
    public ZonePointer<ushort[]>? UshortArray { get; set; }            // PS3 (lives in a pushed block; left empty)
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
    TS_FUNCTION = 0x1,
    TS_COLOR_MAP = 0x2,
    TS_UNUSED_1 = 0x3,
    TS_UNUSED_2 = 0x4,
    TS_NORMAL_MAP = 0x5,
    TS_UNUSED_3 = 0x6,
    TS_UNUSED_4 = 0x7,
    TS_SPECULAR_MAP = 0x8,
    TS_UNUSED_5 = 0x9,
    TS_UNUSED_6 = 0xA,
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
    // Present in the reference model but NOT in the IW4 on-disk struct — the reader does
    // not read them (the entry is 12 bytes: NameHash + 4 bytes + the 4-byte Info union).
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
    // PS3 EBOOT GfxImage root is 0x50: a 0x28 prefix (width/height/format/resourceSize/…), the
    // LoadDef pointer @0x28, a 0x20 suffix, and the name pointer @0x4C.
    public const int EBOOT_ROOT_SIZE = 0x50;
    public const int EBOOT_LOAD_DEF_POINTER_OFFSET = 0x28;
    public const int EBOOT_NAME_POINTER_OFFSET = 0x4C;

    public GfxImage() : base(XAssetType.Image) { }

    public byte[] EbootRootPrefix { get; set; } = new byte[EBOOT_LOAD_DEF_POINTER_OFFSET];
    public ZonePointer<GfxImageLoadDef>? LoadDef { get; set; }
    public byte[] EbootRootSuffix { get; set; } = new byte[EBOOT_NAME_POINTER_OFFSET - EBOOT_LOAD_DEF_POINTER_OFFSET - 4];
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
