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

        material.TechniqueSet = context.ReadPointer<MaterialTechniqueSet>(
            (ref ZoneReadContext pointerContext, ZonePointer<MaterialTechniqueSet> pointer) =>
                pointer.SetResult(pointerContext.ReadPointerValue(pointer, TechsetReader.Read)));
        material.TextureTable = context.ReadPointer<MaterialTextureDef[]>(
            (ref ZoneReadContext pointerContext, ZonePointer<MaterialTextureDef[]> pointer) =>
                pointer.SetResult(ReadArray(ref pointerContext, material.TextureCount, ReadMaterialTextureDef)));
        material.ConstantTable = context.ReadPointer<MaterialConstantDef[]>(
            (ref ZoneReadContext pointerContext, ZonePointer<MaterialConstantDef[]> pointer) =>
                pointer.SetResult(ReadArray(ref pointerContext, material.ConstantCount, ReadMaterialConstantDef)));
        material.StateBitTable = context.ReadPointer<GfxStateBits[]>(
            (ref ZoneReadContext pointerContext, ZonePointer<GfxStateBits[]> pointer) =>
                pointer.SetResult(ReadArray(ref pointerContext, material.StateBitsCount, ReadGfxStateBits)));
        material.UnknownXStringArray =
            GenericReader.ReadStringPointerArrayPointer(ref context, material.UnknownXStringCount);

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
            context.ResolvePointer(texture.Info.Image!,
                (ref ZoneReadContext pointerContext, ZonePointer<GfxImage> pointer) =>
                    pointer.SetResult(pointerContext.ReadPointerValue(pointer, ImageReader.Read)));
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
        var stateBits = new GfxStateBits();
        stateBits.LoadBits = context.ReadPointer<int[]>(
            (ref ZoneReadContext pointerContext, ZonePointer<int[]> pointer) =>
            {
                var values = new int[2];
                for (var i = 0; i < values.Length; i++)
                    values[i] = pointerContext.ReadInt32();
                pointer.SetResult(values);
            });
        stateBits.Unknown = context.ReadInt32();
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

internal static class ImageReader
{
    public static GfxImage Read(ref ZoneReadContext context)
    {
        var asset = new GfxImage { Offset = context.Position };

        asset.LoadDef = context.ReadPointer<GfxImageLoadDef>(ReadImageLoadDef);
        asset.MapType = context.ReadByte();
        asset.Semantic = context.ReadByte();
        asset.Category = context.ReadByte();
        asset.UseSrgbReads = context.ReadByte();
        asset.Picmip = context.ReadBytes(2);
        asset.NoPicmip = context.ReadByte();
        asset.Track = context.ReadByte();
        asset.CardMemory = new[] { context.ReadInt32(), context.ReadInt32() };
        asset.Width = context.ReadUInt16();
        asset.Height = context.ReadUInt16();
        asset.Depth = context.ReadUInt16();
        asset.DelayLoadPixels = context.ReadByte();
        asset.Pad = context.ReadBytes(3);
        asset.NamePtr = GenericReader.ReadStringPointer(ref context);

        return asset;
    }

    public static ZonePointer<GfxImage> ReadImagePointer(ref ZoneReadContext context)
    {
        return context.ReadPointer<GfxImage>(
            (ref ZoneReadContext pointerContext, ZonePointer<GfxImage> pointer) =>
                pointer.SetResult(pointerContext.ReadPointerValue(pointer, Read)));
    }

    private static GfxImageLoadDef ReadImageLoadDef(ref ZoneReadContext context)
    {
        var loadDef = new GfxImageLoadDef
        {
            LevelCount = context.ReadByte(),
            Pad = context.ReadBytes(3),
            Flags = context.ReadInt32(),
            Format = context.ReadInt32(),
            ResourceSize = context.ReadInt32(),
        };

        if (loadDef.ResourceSize > 0)
            loadDef.Data = context.ReadBytes(loadDef.ResourceSize);

        return loadDef;
    }
}
