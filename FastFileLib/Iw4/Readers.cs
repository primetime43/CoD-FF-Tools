// =============================================================================
// IW4 (MW2 PS3) zone reader — ported from Jacob Schroeder's FastFile
//   https://github.com/jacob-schroeder/FastFile
// Ports: FastFile.Logic/Assets/Readers/Generic/GenericReader.cs and the flat-type
// body readers RawFileReader / LocalizeReader / TechsetReader / StringTableReader,
// plus XAssetReaderRegistry. Faithful ports.
//
// The heavy readers (Weapon, Menu, Material, StructuredData, …) are ported and registered
// below. Remaining top-level types without a body reader (Image, XModel, Sound, Fx as a
// pool entry, …) make the asset-body walk stop at the first asset of that type (see
// Iw4ZoneReader) — add them to XAssetReaderRegistry to walk further.
// =============================================================================

namespace FastFileLib.Iw4;

internal static class GenericReader
{
    public static ZonePointer<string> ReadStringPointer(ref ZoneReadContext context, bool resolve = true)
    {
        // Offset (deduplicated/shared) strings are left unresolved — like the reference reader.
        // Resolving them correctly requires demultiplexing the zone into the 7 block streams and
        // indexing block[offset]; the file position is NOT a simple base+offset formula (a pointer
        // refers to runtime block memory, and an asset is split across VIRTUAL/LARGE blocks).
        // See docs/MW2_PS3_Pointer_Fixup_Comparison.md.
        return resolve
            ? context.ReadPointer<string>(ReadStringPointerValue)
            : context.ReadPointer<string>();
    }

    public static void ReadStringPointerValue(ref ZoneReadContext context, ZonePointer<string> pointer)
    {
        pointer.SetResult(context.ReadPointerValue(pointer, ReadCString));
    }

    public static string ReadCString(ref ZoneReadContext context) => context.ReadCString();

    public static ZonePointer<ZonePointer<string>[]> ReadStringPointerArrayPointer(ref ZoneReadContext context, int count)
    {
        var pointer = context.ReadPointer<ZonePointer<string>[]>();
        context.ResolveInlinePointer(pointer,
            (ref ZoneReadContext pc, ZonePointer<ZonePointer<string>[]> p) =>
            {
                var values = new ZonePointer<string>[Math.Max(0, count)];
                for (var i = 0; i < values.Length; i++)
                    values[i] = pc.ReadPointer<string>();
                p.SetResult(values);
                foreach (var v in values)
                    ResolveStringPointerNow(ref pc, v);
            });
        return pointer;
    }

    public static void ResolveStringPointerNow(ref ZoneReadContext context, ZonePointer<string> pointer)
    {
        if (pointer.Kind != PointerKind.Inline) { pointer.SetResult(default); return; }
        context.ResolveInlinePointerNow(pointer, ReadStringPointerValue);
    }
}

internal static class RawFileReader
{
    public static BaseAsset Read(ref ZoneReadContext context)
    {
        var asset = new RawFile
        {
            Offset = context.Position,
            NamePtr = GenericReader.ReadStringPointer(ref context),
            CompressedLen = context.ReadInt32(),
            Len = context.ReadInt32(),
        };

        asset.BufferPtr = context.ReadPointer<byte[]>(
            (ref ZoneReadContext pointerContext, ZonePointer<byte[]> pointer) =>
            {
                asset.DataOffset = pointerContext.Position; // zone offset where the body begins
                var contentLength = asset.CompressedLen > 0 ? asset.CompressedLen : asset.Len;
                asset.OnDiskSize = contentLength;
                var value = pointerContext.ReadPointerValue(
                    pointer,
                    (ref ZoneReadContext bufferContext) =>
                    {
                        var bytes = bufferContext.ReadBytes(contentLength);
                        // Uncompressed rawfile buffers are stored as content + a trailing null
                        // (EBOOT reads Len+1, e.g. savedata/icon0.png). Consume that null so the
                        // next asset stays aligned; it's not part of the content. Compressed buffers
                        // (CompressedLen > 0) are exactly CompressedLen bytes with no trailing null.
                        if (asset.CompressedLen == 0)
                            bufferContext.ReadByte();
                        return bytes;
                    });
                pointer.SetResult(value);
            });

        return asset;
    }
}

internal static class LocalizeReader
{
    public static BaseAsset Read(ref ZoneReadContext context)
    {
        return new LocalizeEntry
        {
            Offset = context.Position,
            ValuePtr = GenericReader.ReadStringPointer(ref context),
            NamePtr = GenericReader.ReadStringPointer(ref context),
        };
    }
}

internal static class TechsetReader
{
    // Returns the concrete type (not BaseAsset) so MaterialReader can use it as a
    // ZoneValueReader<MaterialTechniqueSet>; the registry assignment relies on method-group
    // return-type covariance (MaterialTechniqueSet : BaseAsset).
    public static MaterialTechniqueSet Read(ref ZoneReadContext context)
    {
        var asset = new MaterialTechniqueSet
        {
            Offset = context.Position,
            NamePtr = GenericReader.ReadStringPointer(ref context),
            WorldVertexFormat = (MaterialWorldVertexFormat)context.ReadByte(),
            HasBeenUploaded = context.ReadByte() != 0,
            Unused = context.ReadBytes(2),
        };

        // Resolve each technique pointer in the LARGE block. Shared (offset) technique pointers
        // resolve to default; inline ones pull in the technique/pass/shader bodies (which the
        // multi-block engine reads from LARGE/TEMP), keeping the next asset aligned.
        for (var i = 0; i < asset.Techniques.Length; i++)
        {
            asset.Techniques[i] = context.ReadPointer<MaterialTechnique>();
            context.ResolvePointerInBlock(asset.Techniques[i], ZoneStreamBlock.Large, TechniqueReader.ResolveTechnique);
        }

        return asset;
    }
}

internal static class StringTableReader
{
    public static BaseAsset Read(ref ZoneReadContext context)
    {
        var asset = new StringTable
        {
            Offset = context.Position,
            NamePtr = GenericReader.ReadStringPointer(ref context),
            ColumnCount = context.ReadInt32(),
            RowCount = context.ReadInt32(),
        };

        asset.StringsPtr = context.ReadPointer<StringTableCell[]>(
            (ref ZoneReadContext pointerContext, ZonePointer<StringTableCell[]> pointer) =>
            {
                var valueCount = asset.ColumnCount * asset.RowCount;
                var cells = ReadCells(ref pointerContext, valueCount);
                pointer.SetResult(cells);
            });

        return asset;
    }

    private static StringTableCell[] ReadCells(ref ZoneReadContext context, int count)
    {
        var cells = new StringTableCell[Math.Max(0, count)];
        for (var i = 0; i < cells.Length; i++)
            cells[i] = ReadCell(ref context);
        return cells;
    }

    private static StringTableCell ReadCell(ref ZoneReadContext context)
    {
        return new StringTableCell
        {
            StringPtr = GenericReader.ReadStringPointer(ref context),
            Hash = context.ReadInt32(),
        };
    }
}

internal delegate BaseAsset XAssetReader(ref ZoneReadContext context);

internal static class XAssetReaderRegistry
{
    private static readonly IReadOnlyDictionary<XAssetType, XAssetReader> Readers =
        new Dictionary<XAssetType, XAssetReader>
        {
            [XAssetType.RawFile] = RawFileReader.Read,
            [XAssetType.Localize] = LocalizeReader.Read,
            [XAssetType.Techset] = TechsetReader.Read,
            [XAssetType.StringTable] = StringTableReader.Read,
            [XAssetType.MenuFile] = MenufileReader.Read,
            [XAssetType.StructuredDataDef] = StructuredDataReader.Read,
            [XAssetType.Weapon] = WeaponReader.Read,
            [XAssetType.Material] = MaterialReader.Read,
            [XAssetType.Font] = FontReader.Read, // 24-byte root; lets language zones walk past fonts to localize
            // NOTE: Material is intentionally NOT registered as a top-level reader. The material body
            // reader is correct for *size* (so it keeps weapon sub-asset walks aligned), but a standalone
            // material's technique set / shaders store their data in OTHER stream blocks (TEMP/LARGE),
            // which this single-cursor reader can't follow — so reading a standalone material misaligns
            // the walk and yields garbage (empty names, bogus counts). Registering it also made tiny
            // self-contained zones (e.g. mp_favela_load) *falsely* complete the walk, so the bridge
            // trusted garbage over the pattern scanner. Likewise XModel/Fx are not registered (XModel
            // mis-reads standalone xmodels). Reaching standalone materials needs the multi-block stream
            // engine the reference has but this port doesn't.
        };

    public static bool TryGetReader(XAssetType type, out XAssetReader reader)
        => Readers.TryGetValue(type, out reader!);
}
