// =============================================================================
// IW4 (MW2 PS3) zone reader — ported from Jacob Schroeder's FastFile
//   https://github.com/jacob-schroeder/FastFile
// Ports: FastFile.Logic/Assets/Readers/MaterialReader.cs and ImageReader.cs.
// PS3 branch (the extra padding byte, the Ushorts[38] array + UshortArray pointer,
// MaterialInfo.Padding, and the GfxStateBits LoadBits pointer). Reached only via inline
// material references (e.g. a menu window background); the techset / image / water
// sub-pointers are resolved only when they too are inline.
// =============================================================================

namespace FastFileLib.Iw4;

internal static class MaterialReader
{
    public static Material Read(ref ZoneReadContext context)
    {
        var material = new Material
        {
            Offset = context.Position,
            Info = ReadMaterialInfo(ref context),
            StateBitsEntry = context.ReadBytes(Material.TECHNIQUE_COUNT),
            TextureCount = context.ReadByte(),
            ConstantCount = context.ReadByte(),
            StateBitsCount = context.ReadByte(),
            StateFlags = context.ReadByte(),
            CameraRegion = context.ReadByte(),
            UnknownXStringCount = context.ReadByte(),
        };

        // PS3 (#if PS3): material padding byte, ushort[TECHNIQUE_COUNT], 2 padding bytes, ushort[] pointer.
        material.MaterialPadding = context.ReadByte();
        for (var i = 0; i < material.Ushorts.Length; i++)
            material.Ushorts[i] = context.ReadUInt16();
        material.UshortPadding = context.ReadBytes(2);

        // EBOOT pushes a block before this table loads, so it can't be followed with the inline
        // cursor; the reference reads the pointer field and leaves the table empty. Real PS3 zones
        // store it as an Offset (block) pointer, so the inline engine never consumes any bytes here.
        material.UshortArray = context.ReadPointer<ushort[]>();
        material.UshortArray.SetResult(Array.Empty<ushort>());

        // Per-field stream blocks (reference ResolveMaterialChildren): TechniqueSet from TEMP, the
        // texture/constant/state tables + unknown-xstring array from LARGE. Read each pointer field
        // in root order, then queue its block-scoped resolver.
        material.TechniqueSet = context.ReadPointer<MaterialTechniqueSet>();
        context.ResolvePointerInBlock(material.TechniqueSet, ZoneStreamBlock.Temp,
            (ref ZoneReadContext c, ZonePointer<MaterialTechniqueSet> p) =>
                p.SetResult(c.ReadPointerValue(p, TechsetReader.Read)));

        material.TextureTable = context.ReadPointer<MaterialTextureDef[]>();
        context.ResolvePointerInBlock(material.TextureTable, ZoneStreamBlock.Large,
            (ref ZoneReadContext c, ZonePointer<MaterialTextureDef[]> p) =>
                p.SetResult(ReadArray(ref c, material.TextureCount, ReadMaterialTextureDef)));

        material.ConstantTable = context.ReadPointer<MaterialConstantDef[]>();
        context.ResolvePointerInBlock(material.ConstantTable, ZoneStreamBlock.Large,
            (ref ZoneReadContext c, ZonePointer<MaterialConstantDef[]> p) =>
                p.SetResult(ReadArray(ref c, material.ConstantCount, ReadMaterialConstantDef)));

        material.StateBitTable = context.ReadPointer<GfxStateBits[]>();
        context.ResolvePointerInBlock(material.StateBitTable, ZoneStreamBlock.Large,
            (ref ZoneReadContext c, ZonePointer<GfxStateBits[]> p) =>
                p.SetResult(ReadArray(ref c, material.StateBitsCount, ReadGfxStateBits)));

        material.UnknownXStringArray = context.ReadPointer<ZonePointer<string>[]>();
        int xStringCount = material.UnknownXStringCount;
        context.ResolvePointerInBlock(material.UnknownXStringArray, ZoneStreamBlock.Large,
            (ref ZoneReadContext c, ZonePointer<ZonePointer<string>[]> p) =>
            {
                var values = new ZonePointer<string>[Math.Max(0, xStringCount)];
                for (var i = 0; i < values.Length; i++)
                    values[i] = c.ReadPointer<string>();
                p.SetResult(values);
                foreach (var v in values)
                    GenericReader.ResolveStringPointerNow(ref c, v);
            });

        return material;
    }

    public static ZonePointer<Material> ReadMaterialPointer(ref ZoneReadContext context)
    {
        var pointer = context.ReadPointer<Material>();
        context.ResolveInlinePointer(pointer,
            (ref ZoneReadContext pointerContext, ZonePointer<Material> p) =>
                p.SetResult(pointerContext.ReadPointerValue(p, Read)));
        return pointer;
    }

    private static MaterialInfo ReadMaterialInfo(ref ZoneReadContext context)
    {
        var info = new MaterialInfo
        {
            NamePtr = GenericReader.ReadStringPointer(ref context),
            GameFlags = context.ReadByte(),
            SortKey = context.ReadByte(),
            TextureAtlasRowCount = context.ReadByte(),
            TextureAtlasColumnCount = context.ReadByte(),
        };

        var packed = context.ReadUInt64();
        info.DrawSurf = new GfxDrawSurf { Packed = packed, Fields = new GfxDrawSurfFields { Packed = packed } };
        info.SurfaceTypeBits = context.ReadInt32();
        info.Padding = context.ReadInt32(); // PS3
        return info;
    }

    private static MaterialTextureDef ReadMaterialTextureDef(ref ZoneReadContext context)
    {
        // IW4 materialTextureDef is 12 bytes: NameHash (4) + NameStart/NameEnd/SampleState/Semantic
        // (4) + the 4-byte Info union. There is no isMatureContent/pad on disk.
        var texture = new MaterialTextureDef
        {
            NameHash = context.ReadUInt32(),
            NameStart = context.ReadByte(),
            NameEnd = context.ReadByte(),
            SampleState = context.ReadByte(),
            Semantic = (MaterialTextureSemantic)context.ReadByte(),
        };

        var raw = context.ReadInt32();
        texture.Info = new MaterialTextureDefInfo
        {
            Raw = raw,
            Image = new ZonePointer<GfxImage>(raw),
            Water = new ZonePointer<Water>(raw),
        };

        if (texture.Semantic == MaterialTextureSemantic.TS_WATER_MAP)
        {
            context.ResolvePointer(texture.Info.Water!, ReadWaterPointerValue);
        }
        else
        {
            // The image is allocated from TEMP (its name from LARGE, its pixel data from the
            // physical/runtime block) — see ImageReader.
            ImageReader.ResolveImagePointer(ref context, texture.Info.Image!);
        }

        return texture;
    }

    private static MaterialConstantDef ReadMaterialConstantDef(ref ZoneReadContext context)
    {
        return new MaterialConstantDef
        {
            NameHash = context.ReadInt32(),
            Name = context.ReadString(12),
            Literal = context.ReadVec4(),
        };
    }

    private static GfxStateBits ReadGfxStateBits(ref ZoneReadContext context)
    {
        var stateBits = new GfxStateBits
        {
            LoadBits = context.ReadPointer<int[]>(),
            Unknown = context.ReadInt32(),
        };
        // LoadBits (int[2]) is allocated from TEMP on PS3.
        context.ResolvePointerInBlock(stateBits.LoadBits!, ZoneStreamBlock.Temp,
            (ref ZoneReadContext c, ZonePointer<int[]> p) =>
            {
                var values = new int[2];
                for (var i = 0; i < values.Length; i++)
                    values[i] = c.ReadInt32();
                p.SetResult(values);
            });
        return stateBits;
    }

    private static Water ReadWater(ref ZoneReadContext context)
    {
        var water = new Water
        {
            Writable = new WaterWritable { FloatTime = context.ReadFloat() },
            H0X = context.ReadPointer<float[]>(),
            H0Y = context.ReadPointer<float[]>(),
            WTerm = context.ReadPointer<float[]>(),
            M = context.ReadInt32(),
            N = context.ReadInt32(),
            Lx = context.ReadFloat(),
            Lz = context.ReadFloat(),
            Gravity = context.ReadFloat(),
            Windvel = context.ReadFloat(),
        };

        for (var i = 0; i < water.Winddir.Length; i++)
            water.Winddir[i] = context.ReadFloat();
        water.Amplitude = context.ReadFloat();
        for (var i = 0; i < water.CodeConstant.Length; i++)
            water.CodeConstant[i] = context.ReadFloat();
        water.Image = ImageReader.ReadImagePointer(ref context);

        var sampleCount = water.M * water.N;
        context.ResolvePointer(water.H0X!, (ref ZoneReadContext c, ZonePointer<float[]> p) =>
            p.SetResult(c.ReadPointerValue(p, (ref ZoneReadContext v) => ReadFloatArray(ref v, sampleCount))));
        context.ResolvePointer(water.H0Y!, (ref ZoneReadContext c, ZonePointer<float[]> p) =>
            p.SetResult(c.ReadPointerValue(p, (ref ZoneReadContext v) => ReadFloatArray(ref v, sampleCount))));
        context.ResolvePointer(water.WTerm!, (ref ZoneReadContext c, ZonePointer<float[]> p) =>
            p.SetResult(c.ReadPointerValue(p, (ref ZoneReadContext v) => ReadFloatArray(ref v, sampleCount))));

        return water;
    }

    private static void ReadWaterPointerValue(ref ZoneReadContext context, ZonePointer<Water> pointer)
        => pointer.SetResult(context.ReadPointerValue(pointer, ReadWater));

    private static float[] ReadFloatArray(ref ZoneReadContext context, int count)
    {
        var values = new float[Math.Max(0, count)];
        for (var i = 0; i < values.Length; i++)
            values[i] = context.ReadFloat();
        return values;
    }

    private static T[] ReadArray<T>(ref ZoneReadContext context, int count, ZoneValueReader<T> reader)
    {
        var values = new T[Math.Max(0, count)];
        for (var i = 0; i < values.Length; i++)
            values[i] = reader(ref context);
        return values;
    }
}

// IW4 PS3 image reader — faithful port of the reference ImageReader.cs. The GfxImage root is the
// 0x50 EBOOT layout (0x28 prefix carrying width/height/format/resourceSize/…, the LoadDef pointer,
// a 0x20 suffix, then the name pointer). The name comes from LARGE; the pixel payload from the
// physical (or runtime, for cubemaps) block, 128-aligned in that block but stored contiguously in
// the file (AlignStreamOnly).
internal static class ImageReader
{
    private const int WidthOffset = 0x08, HeightOffset = 0x0A, DepthOffset = 0x0C;
    private const int UseSrgbReadsOffset = 0x18, MapTypeOffset = 0x19, SemanticOffset = 0x1A, CategoryOffset = 0x1B;
    private const int ResourceSizeOffset = 0x1C, CardMemoryOffset = 0x20;

    public static GfxImage Read(ref ZoneReadContext context)
    {
        var asset = new GfxImage
        {
            Offset = context.Position,
            EbootRootPrefix = context.ReadBytes(GfxImage.EBOOT_LOAD_DEF_POINTER_OFFSET),
        };
        ApplyRootPrefix(asset);

        asset.LoadDef = context.ReadPointer<GfxImageLoadDef>();
        asset.EbootRootSuffix = context.ReadBytes(
            GfxImage.EBOOT_NAME_POINTER_OFFSET - GfxImage.EBOOT_LOAD_DEF_POINTER_OFFSET - 4);
        asset.NamePtr = GenericReader.ReadStringPointer(ref context, resolve: false);

        context.ResolvePointerInBlock(asset.NamePtr, ZoneStreamBlock.Large, GenericReader.ReadStringPointerValue);
        ResolveImageLoadDef(ref context, asset);
        return asset;
    }

    public static ZonePointer<GfxImage> ReadImagePointer(ref ZoneReadContext context)
    {
        var pointer = context.ReadPointer<GfxImage>();
        ResolveImagePointer(ref context, pointer);
        return pointer;
    }

    /// <summary>Image asset pointers are alias and resolve from the TEMP block.</summary>
    public static void ResolveImagePointer(ref ZoneReadContext context, ZonePointer<GfxImage> pointer)
        => context.ResolvePointerInBlock(pointer, ZoneStreamBlock.Temp,
            (ref ZoneReadContext c, ZonePointer<GfxImage> p) => p.SetResult(c.ReadPointerValue(p, Read)));

    private static void ResolveImageLoadDef(ref ZoneReadContext context, GfxImage asset)
    {
        // mapType 11 (cubemap) pixel data is allocated from RUNTIME, everything else from PHYSICAL.
        var payloadBlock = asset.MapType == 11 ? ZoneStreamBlock.Runtime : ZoneStreamBlock.Physical;
        context.ResolvePointerInBlock(asset.LoadDef!, payloadBlock,
            (ref ZoneReadContext c, ZonePointer<GfxImageLoadDef> p) =>
            {
                c.AlignStreamOnly(128); // align the payload block only — no on-disk padding
                p.SetResult(c.ReadPointerValue(p, (ref ZoneReadContext dc) => ReadImageLoadDefBytes(ref dc, asset)));
            });
    }

    private static GfxImageLoadDef ReadImageLoadDefBytes(ref ZoneReadContext context, GfxImage asset)
    {
        var loadDef = CreateLoadDefFromRoot(asset);
        if (loadDef.ResourceSize > 0)
        {
            if (loadDef.ResourceSize > context.Span.Length - context.Position)
                throw new InvalidDataException(
                    $"Image pixel payload size 0x{loadDef.ResourceSize:X8} is outside the remaining zone stream at image offset 0x{asset.Offset:X8} ({asset.Width}x{asset.Height}x{asset.Depth}, map={asset.MapType}).");
            loadDef.Data = context.ReadBytes(loadDef.ResourceSize);
        }
        return loadDef;
    }

    private static GfxImageLoadDef CreateLoadDefFromRoot(GfxImage asset)
    {
        var prefix = asset.EbootRootPrefix;
        return new GfxImageLoadDef
        {
            LevelCount = GetByte(prefix, 0),
            Pad = GetBytes(prefix, 1, 3),
            Flags = ReadInt32(prefix, 4),
            Format = GetByte(prefix, 0), // PS3 packs the GCM format in byte 0
            ResourceSize = ReadInt32(prefix, ResourceSizeOffset),
        };
    }

    private static void ApplyRootPrefix(GfxImage asset)
    {
        var prefix = asset.EbootRootPrefix;
        asset.Width = ReadUInt16(prefix, WidthOffset);
        asset.Height = ReadUInt16(prefix, HeightOffset);
        asset.Depth = ReadUInt16(prefix, DepthOffset);
        asset.UseSrgbReads = GetByte(prefix, UseSrgbReadsOffset);
        asset.MapType = GetByte(prefix, MapTypeOffset);
        asset.Semantic = GetByte(prefix, SemanticOffset);
        asset.Category = GetByte(prefix, CategoryOffset);
        asset.Picmip = GetBytes(prefix, 1, 2);
        asset.NoPicmip = GetByte(prefix, 3);
        asset.Track = GetByte(prefix, UseSrgbReadsOffset);
        asset.CardMemory = new[] { ReadInt32(prefix, CardMemoryOffset), ReadInt32(prefix, CardMemoryOffset + 4) };
        asset.DelayLoadPixels = GetByte(prefix, 0x26);
        asset.Pad = GetBytes(prefix, 0x15, 3);
    }

    private static byte GetByte(byte[] v, int o) => o >= 0 && o < v.Length ? v[o] : (byte)0;

    private static byte[] GetBytes(byte[] v, int o, int count)
    {
        var b = new byte[count];
        if (o >= 0 && o < v.Length && count > 0)
            Array.Copy(v, o, b, 0, Math.Min(count, v.Length - o));
        return b;
    }

    private static int ReadInt32(byte[] v, int o)
        => o >= 0 && o + 4 <= v.Length ? System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(v.AsSpan(o, 4)) : 0;

    private static ushort ReadUInt16(byte[] v, int o)
        => o >= 0 && o + 2 <= v.Length ? System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(v.AsSpan(o, 2)) : (ushort)0;
}
