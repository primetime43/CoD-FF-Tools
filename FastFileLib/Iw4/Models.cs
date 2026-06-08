// =============================================================================
// IW4 (MW2 PS3) zone reader — ported from Jacob Schroeder's FastFile
//   https://github.com/jacob-schroeder/FastFile
// Ports the zone/asset model classes used by the ported readers:
//   FastFile.Models/Zone/{XAssetType,XFile,XAssetList,XAsset}.cs
//   FastFile.Models/Assets/{BaseAsset,UnknownAsset}.cs
//   FastFile.Models/Assets/RawFiles/RawFile.cs, Localize/LocalizeEntry.cs,
//   StringTables/{StringTable,StringTableCell}.cs,
//   TechniqueSet/MaterialTechniqueSet.cs (trimmed — only the techset fields
//   the reader walks; MaterialTechnique is a placeholder type-arg).
// =============================================================================

namespace FastFileLib.Iw4;

/// <summary>IW4 PS3 asset type IDs (port of his <c>XAssetType</c>, <c>#if PS3</c> variant).</summary>
public enum XAssetType
{
    PhysPreset = 0x00,
    PhysCollmap = 0x01,
    XAnim = 0x02,
    XModelSurfs = 0x03,
    XModel = 0x04,
    Material = 0x05,
    PixelShader = 0x06,
    VertexShader = 0x07,
    Techset = 0x08,
    Image = 0x09,
    Sound = 0x0A,
    SndCurve = 0x0B,
    LoadedSound = 0x0C,
    ColMapSp = 0x0D,
    ColMapMp = 0x0E,
    ComMap = 0x0F,
    GameMapSp = 0x10,
    GameMapMp = 0x11,
    MapEnts = 0x12,
    FxMap = 0x13,
    GfxMap = 0x14,
    LightDef = 0x15,
    UiMap = 0x16,
    Font = 0x17,
    MenuFile = 0x18,
    Menu = 0x19,
    Localize = 0x1A,
    Weapon = 0x1B,
    SndDriverGlobals = 0x1C,
    Fx = 0x1D,
    ImpactFx = 0x1E,
    AiType = 0x1F,
    MpType = 0x20,
    Character = 0x21,
    XModelAlias = 0x22,
    RawFile = 0x23,
    StringTable = 0x24,
    LeaderboardDef = 0x25,
    StructuredDataDef = 0x26,
    Tracer = 0x27,
    Vehicle = 0x28,
    AddonMapEnts = 0x29,
}

public class XFile
{
    public int Size { get; set; }
    public int ExternalSize { get; set; }
    public int[] BlockSize { get; set; } = Array.Empty<int>();
}

public class XAssetList
{
    public int ScriptStringCount { get; set; }
    public ZonePointer<ZonePointer<string?>[]>? ScriptStringsPtr { get; set; }
    public string?[] ScriptStrings => ScriptStringsPtr is { IsResolved: true, Result: not null }
        ? ScriptStringsPtr.Result.Select(p => p.Result).ToArray()
        : Array.Empty<string?>();

    public int AssetCount { get; set; }
    public ZonePointer<XAsset[]>? AssetsPtr { get; set; }
    public XAsset[] Assets => AssetsPtr is { IsResolved: true, Result: not null }
        ? AssetsPtr.Result
        : Array.Empty<XAsset>();
}

public class XAsset
{
    public XAssetType Type { get; set; }
    public ZonePointer<BaseAsset>? XAssetPtr { get; set; }
}

public interface IBaseAsset
{
    string? GetDisplayName { get; }
}

public abstract class BaseAsset : IBaseAsset
{
    public XAssetType Type { get; }
    public int Offset { get; init; }
    public abstract string? GetDisplayName { get; }

    protected BaseAsset(XAssetType type) => Type = type;
}

public sealed class UnknownAsset : BaseAsset
{
    public UnknownAsset(XAssetType type) : base(type) { }
    public override string? GetDisplayName => "unknown";
}

public class RawFile : BaseAsset
{
    public RawFile() : base(XAssetType.RawFile) { }
    public ZonePointer<string>? NamePtr { get; set; }
    public string Name => NamePtr is { IsResolved: true } ? NamePtr.Result ?? string.Empty : string.Empty;
    public int CompressedLen { get; set; }
    public int Len { get; set; }
    public ZonePointer<byte[]>? BufferPtr { get; set; }
    /// <summary>Zone byte offset where the (possibly zlib-compressed) body begins.</summary>
    public int DataOffset { get; set; }
    /// <summary>On-disk body size in bytes (CompressedLen if compressed, else Len).</summary>
    public int OnDiskSize { get; set; }
    public override string? GetDisplayName => Name;
}

public class LocalizeEntry : BaseAsset
{
    public LocalizeEntry() : base(XAssetType.Localize) { }
    public ZonePointer<string>? ValuePtr { get; set; }
    public string Value => ValuePtr is { IsResolved: true } ? ValuePtr.Result ?? string.Empty : string.Empty;
    public ZonePointer<string>? NamePtr { get; set; }
    public string Name => NamePtr is { IsResolved: true } ? NamePtr.Result ?? string.Empty : string.Empty;
    public override string? GetDisplayName => Name;
}

public class StringTableCell
{
    public ZonePointer<string>? StringPtr { get; set; }
    public string String => StringPtr is { IsResolved: true } ? StringPtr.Result ?? string.Empty : string.Empty;
    public int Hash { get; set; }
}

public class StringTable : BaseAsset
{
    public StringTable() : base(XAssetType.StringTable) { }
    public ZonePointer<string>? NamePtr { get; set; }
    public string Name => NamePtr is { IsResolved: true } ? NamePtr.Result ?? string.Empty : string.Empty;
    public int ColumnCount { get; set; }
    public int RowCount { get; set; }
    public ZonePointer<StringTableCell[]>? StringsPtr { get; set; }
    public StringTableCell[] Strings => StringsPtr is { IsResolved: true, Result: not null }
        ? StringsPtr.Result
        : Array.Empty<StringTableCell>();
    public override string? GetDisplayName => Name;
}

public struct Vec4
{
    public float A { get; set; }
    public float R { get; set; }
    public float G { get; set; }
    public float B { get; set; }
}

public enum MaterialWorldVertexFormat : byte { }

// MaterialTechnique (+ MaterialPass / shaders / args) live in Techset.cs — resolved inline via the
// multi-block engine so a techset that owns its techniques doesn't misalign the walk.

public class MaterialTechniqueSet : BaseAsset
{
    // PS3 technique count (his MaterialTechniqueSet: MAX_TECHNIQUES = 37 under #if PS3).
    public const int MaxTechniques = 37;

    public MaterialTechniqueSet() : base(XAssetType.Techset) { }
    public ZonePointer<string>? NamePtr { get; set; }
    public string Name => NamePtr is { IsResolved: true } ? NamePtr.Result ?? string.Empty : string.Empty;
    public MaterialWorldVertexFormat WorldVertexFormat { get; set; }
    public bool HasBeenUploaded { get; set; }
    public byte[] Unused { get; set; } = new byte[2];
    public ZonePointer<MaterialTechnique>[] Techniques { get; set; } = new ZonePointer<MaterialTechnique>[MaxTechniques];
    public override string? GetDisplayName => string.IsNullOrWhiteSpace(Name) ? $"Techset 0x{Offset:X8}" : Name;
}
