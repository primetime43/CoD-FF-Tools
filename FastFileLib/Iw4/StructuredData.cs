// =============================================================================
// IW4 (MW2 PS3) zone reader — ported from Jacob Schroeder's FastFile
//   https://github.com/jacob-schroeder/FastFile
// Ports: FastFile.Models/Assets/StructuredData/StructuredDataDefSet.cs and
//        FastFile.Logic/Assets/Readers/StructuredDataReader.cs. Self-contained.
// =============================================================================

namespace FastFileLib.Iw4;

public enum StructuredDataTypeCategory
{
    DataInt = 0, DataByte = 1, DataBool = 2, DataString = 3, DataEnum = 4,
    DataStruct = 5, DataIndexedArray = 6, DataEnumArray = 7, DataFloat = 8,
    DataShort = 9, DataCount = 10,
}

public sealed class StructuredDataType
{
    public StructuredDataTypeCategory Type;
    public int UnionValue;
}

public sealed class StructuredDataEnumEntry
{
    public ZonePointer<string>? StringPtr { get; set; }
    public ushort Index, Padding;
}

public sealed class StructuredDataEnum
{
    public int EntryCount, ReservedEntryCount;
    public ZonePointer<StructuredDataEnumEntry[]>? EntriesPtr { get; set; }
}

public sealed class StructuredDataStructProperty
{
    public ZonePointer<string>? NamePtr { get; set; }
    public StructuredDataType? Type { get; set; }
    public uint Offset;
}

public sealed class StructuredDataStruct
{
    public int PropertyCount;
    public ZonePointer<StructuredDataStructProperty[]>? PropertiesPtr { get; set; }
    public int Size;
    public uint BitOffset;
}

public sealed class StructuredDataIndexedArray
{
    public int ArraySize;
    public StructuredDataType? ElementType { get; set; }
    public uint ElementSize;
}

public sealed class StructuredDataEnumedArray
{
    public int EnumIndex;
    public StructuredDataType? ElementType { get; set; }
    public uint ElementSize;
}

public sealed class StructuredDataDef
{
    public int Version;
    public uint FormatChecksum;
    public int EnumCount;
    public ZonePointer<StructuredDataEnum[]>? EnumsPtr { get; set; }
    public int StructCount;
    public ZonePointer<StructuredDataStruct[]>? StructsPtr { get; set; }
    public int IndexedArrayCount;
    public ZonePointer<StructuredDataIndexedArray[]>? IndexedArraysPtr { get; set; }
    public int EnumedArrayCount;
    public ZonePointer<StructuredDataEnumedArray[]>? EnumedArraysPtr { get; set; }
    public StructuredDataType? RootType { get; set; }
    public uint Size;
}

public sealed class StructuredDataDefSet : BaseAsset
{
    public StructuredDataDefSet() : base(XAssetType.StructuredDataDef) { }
    public ZonePointer<string>? NamePtr { get; set; }
    public string Name => NamePtr is { IsResolved: true } ? NamePtr.Result ?? string.Empty : string.Empty;
    public int DefCount;
    public ZonePointer<StructuredDataDef[]>? DefsPtr { get; set; }
    public override string? GetDisplayName => string.IsNullOrWhiteSpace(Name) ? $"StructuredDataDef 0x{Offset:X8}" : Name;
}

internal static class StructuredDataReader
{
    public static StructuredDataDefSet Read(ref ZoneReadContext context)
    {
        var asset = new StructuredDataDefSet
        {
            Offset = context.Position,
            NamePtr = GenericReader.ReadStringPointer(ref context),
            DefCount = context.ReadInt32(),
        };

        asset.DefsPtr = context.ReadPointer<StructuredDataDef[]>(
            (ref ZoneReadContext pc, ZonePointer<StructuredDataDef[]> p) =>
                p.SetResult(ReadArray(ref pc, asset.DefCount, ReadStructuredDataDef)));

        return asset;
    }

    private static StructuredDataDef ReadStructuredDataDef(ref ZoneReadContext context)
    {
        var value = new StructuredDataDef
        {
            Version = context.ReadInt32(),
            FormatChecksum = context.ReadUInt32(),
            EnumCount = context.ReadInt32(),
        };

        value.EnumsPtr = context.ReadPointer<StructuredDataEnum[]>(
            (ref ZoneReadContext pc, ZonePointer<StructuredDataEnum[]> p) =>
                p.SetResult(ReadArray(ref pc, value.EnumCount, ReadStructuredDataEnum)));

        value.StructCount = context.ReadInt32();
        value.StructsPtr = context.ReadPointer<StructuredDataStruct[]>(
            (ref ZoneReadContext pc, ZonePointer<StructuredDataStruct[]> p) =>
                p.SetResult(ReadArray(ref pc, value.StructCount, ReadStructuredDataStruct)));

        value.IndexedArrayCount = context.ReadInt32();
        value.IndexedArraysPtr = context.ReadPointer<StructuredDataIndexedArray[]>(
            (ref ZoneReadContext pc, ZonePointer<StructuredDataIndexedArray[]> p) =>
                p.SetResult(ReadArray(ref pc, value.IndexedArrayCount, ReadStructuredDataIndexedArray)));

        value.EnumedArrayCount = context.ReadInt32();
        value.EnumedArraysPtr = context.ReadPointer<StructuredDataEnumedArray[]>(
            (ref ZoneReadContext pc, ZonePointer<StructuredDataEnumedArray[]> p) =>
                p.SetResult(ReadArray(ref pc, value.EnumedArrayCount, ReadStructuredDataEnumedArray)));

        value.RootType = ReadStructuredDataType(ref context);
        value.Size = context.ReadUInt32();
        return value;
    }

    private static StructuredDataEnum ReadStructuredDataEnum(ref ZoneReadContext context)
    {
        var value = new StructuredDataEnum
        {
            EntryCount = context.ReadInt32(),
            ReservedEntryCount = context.ReadInt32(),
        };
        value.EntriesPtr = context.ReadPointer<StructuredDataEnumEntry[]>(
            (ref ZoneReadContext pc, ZonePointer<StructuredDataEnumEntry[]> p) =>
                p.SetResult(ReadArray(ref pc, value.EntryCount, ReadStructuredDataEnumEntry)));
        return value;
    }

    private static StructuredDataEnumEntry ReadStructuredDataEnumEntry(ref ZoneReadContext context)
        => new()
        {
            StringPtr = GenericReader.ReadStringPointer(ref context),
            Index = context.ReadUInt16(),
            Padding = context.ReadUInt16(),
        };

    private static StructuredDataStruct ReadStructuredDataStruct(ref ZoneReadContext context)
    {
        var value = new StructuredDataStruct { PropertyCount = context.ReadInt32() };
        value.PropertiesPtr = context.ReadPointer<StructuredDataStructProperty[]>(
            (ref ZoneReadContext pc, ZonePointer<StructuredDataStructProperty[]> p) =>
                p.SetResult(ReadArray(ref pc, value.PropertyCount, ReadStructuredDataStructProperty)));
        value.Size = context.ReadInt32();
        value.BitOffset = context.ReadUInt32();
        return value;
    }

    private static StructuredDataStructProperty ReadStructuredDataStructProperty(ref ZoneReadContext context)
        => new()
        {
            NamePtr = GenericReader.ReadStringPointer(ref context),
            Type = ReadStructuredDataType(ref context),
            Offset = context.ReadUInt32(),
        };

    private static StructuredDataIndexedArray ReadStructuredDataIndexedArray(ref ZoneReadContext context)
        => new()
        {
            ArraySize = context.ReadInt32(),
            ElementType = ReadStructuredDataType(ref context),
            ElementSize = context.ReadUInt32(),
        };

    private static StructuredDataEnumedArray ReadStructuredDataEnumedArray(ref ZoneReadContext context)
        => new()
        {
            EnumIndex = context.ReadInt32(),
            ElementType = ReadStructuredDataType(ref context),
            ElementSize = context.ReadUInt32(),
        };

    private static StructuredDataType ReadStructuredDataType(ref ZoneReadContext context)
        => new()
        {
            Type = (StructuredDataTypeCategory)context.ReadInt32(),
            UnionValue = context.ReadInt32(),
        };

    private static T[] ReadArray<T>(ref ZoneReadContext context, int count, ZoneValueReader<T> reader)
    {
        var values = new T[Math.Max(0, count)];
        for (var i = 0; i < values.Length; i++)
            values[i] = reader(ref context);
        return values;
    }
}
