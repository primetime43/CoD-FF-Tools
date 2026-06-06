// =============================================================================
// IW4 (MW2 PS3) zone reader — ported from Jacob Schroeder's FastFile
//   https://github.com/jacob-schroeder/FastFile
// Ports: FastFile.Models/Assets/XModels/XModel.cs and
//        FastFile.Logic/Assets/Readers/XModelReader.cs (+ Vec3/Bounds).
// Reached via a weapon's inline hand-xmodel. xmodelsurfs / xsurface / colltri are
// referenced as pointers (placeholders, never resolved); surface materials resolve
// through the ported MaterialReader when inline; physpreset/physcollmap are stubs.
// =============================================================================

namespace FastFileLib.Iw4;

public struct Vec3 { public float X, Y, Z; }
public sealed class Bounds { public Vec3 MidPoint; public Vec3 HalfSize; }

public readonly struct XModelParent { public byte BoneIndex { get; } public XModelParent(byte b) => BoneIndex = b; }
public readonly struct XModelPartClassification { public byte Value { get; } public XModelPartClassification(byte v) => Value = v; }
public readonly struct XModelQuat
{
    public short X { get; } public short Y { get; } public short Z { get; } public short W { get; }
    public XModelQuat(short x, short y, short z, short w) { X = x; Y = y; Z = z; W = w; }
}

public sealed class DObjAnimMat { public Vec4 Quat; public Vec3 Trans; public float TransWeight; }

public sealed class XSurface { public byte TileMode; }
public sealed class XModelCollTri { public Vec4 Plane, SVec, TVec; }

public sealed class XModelLodInfo
{
    public float Dist;
    public ushort NumSurfs, SurfIndex;
    public ZonePointer<XModelSurfs>? ModelSurfs { get; set; }
    public int[] PartBits { get; set; } = new int[6];
    public ZonePointer<XSurface[]>? Surfs { get; set; }
}

public sealed class XModelCollSurf
{
    public ZonePointer<XModelCollTri[]>? CollTris { get; set; }
    public int NumCollTris;
    public Bounds? Bounds { get; set; }
    public int BoneIdx, Contents, SurfFlags;
}

public sealed class XBoneInfo { public Bounds? Bounds { get; set; } public float RadiusSquared; }

public sealed class XModelSurfs : BaseAsset
{
    public XModelSurfs() : base(XAssetType.XModelSurfs) { }
    public ZonePointer<string>? NamePtr { get; set; }
    public string Name => NamePtr is { IsResolved: true } ? NamePtr.Result ?? string.Empty : string.Empty;
    public override string? GetDisplayName => Name;
}

public class XModel : BaseAsset
{
    public XModel() : base(XAssetType.XModel) { }
    public ZonePointer<string>? NamePtr { get; set; }
    public string Name => NamePtr is { IsResolved: true } ? NamePtr.Result ?? string.Empty : string.Empty;
    public byte NumBones, NumRootBones, NumSurfs, LodRampType;
    public float Scale;
    public int[] NoScalePartBits { get; set; } = new int[6];
    public ZonePointer<ushort[]>? BoneNames { get; set; }
    public ZonePointer<XModelParent[]>? ParentList { get; set; }
    public ZonePointer<XModelQuat[]>? Quats { get; set; }
    public ZonePointer<Vec3[]>? Trans { get; set; }
    public ZonePointer<XModelPartClassification[]>? PartClassification { get; set; }
    public ZonePointer<DObjAnimMat[]>? BaseMat { get; set; }
    public ZonePointer<ZonePointer<Material>[]>? MaterialHandles { get; set; }
    public XModelLodInfo[] LodInfo { get; set; } = new XModelLodInfo[4];
    public byte MaxLoadedLod, NumLods, CollLod, Flags;
    public ZonePointer<XModelCollSurf[]>? CollSurfs { get; set; }
    public int NumCollSurfs, Contents;
    public ZonePointer<XBoneInfo[]>? BoneInfo { get; set; }
    public float Radius;
    public Bounds? Bounds { get; set; }
    public int MemUsage;
    public bool Bad;
    public byte BadPadding0, BadPadding1, BadPadding2;
    public ZonePointer<PhysPreset>? PhysPreset { get; set; }
    public ZonePointer<PhysCollmap>? PhysCollmap { get; set; }
    public override string? GetDisplayName => Name;
}

internal static class XModelReader
{
    private const int NoScalePartBitsCount = 6;
    private const int LodInfoCount = 4;
    private const int LodInfoSize = 40;

    public static ZonePointer<XModel> ReadXModelPointer(ref ZoneReadContext context)
    {
        var pointer = context.ReadPointer<XModel>();
        context.ResolveInlinePointer(pointer, ReadXModelPointerValue);
        return pointer;
    }

    public static ZonePointer<ZonePointer<XModel>[]> ReadXModelPointerArrayPointer(ref ZoneReadContext context, int count)
    {
        var pointer = context.ReadPointer<ZonePointer<XModel>[]>();
        context.ResolveInlinePointer(pointer,
            (ref ZoneReadContext pc, ZonePointer<ZonePointer<XModel>[]> p) =>
            {
                var values = new ZonePointer<XModel>[Math.Max(0, count)];
                for (var i = 0; i < values.Length; i++)
                    values[i] = pc.ReadPointer<XModel>();
                p.SetResult(values);

                foreach (var value in values)
                {
                    if (value.Kind == PointerKind.Inline)
                        pc.ResolveInlinePointerDeferred(value, ReadXModelPointerValue);
                    else
                        value.SetResult(default);
                }
            });
        return pointer;
    }

    private static void ReadXModelPointerValue(ref ZoneReadContext context, ZonePointer<XModel> pointer)
        => pointer.SetResult(context.ReadPointerValue(pointer, Read));

    public static XModel Read(ref ZoneReadContext context)
    {
        var offset = context.Position;
        var name = GenericReader.ReadStringPointer(ref context, resolve: false);
        var numBones = context.ReadByte();
        var numRootBones = context.ReadByte();
        var numSurfs = context.ReadByte();
        var lodRampType = context.ReadByte();
        var scale = context.ReadFloat();
        var noScalePartBits = ReadInt32Array(ref context, NoScalePartBitsCount);

        var boneNames = context.ReadPointer<ushort[]>();
        var parentList = context.ReadPointer<XModelParent[]>();
        var quats = context.ReadPointer<XModelQuat[]>();
        var trans = context.ReadPointer<Vec3[]>();
        var partClassification = context.ReadPointer<XModelPartClassification[]>();
        var baseMat = context.ReadPointer<DObjAnimMat[]>();
        var materialHandles = context.ReadPointer<ZonePointer<Material>[]>();

        var lodInfo = new XModelLodInfo[LodInfoCount];
        for (var i = 0; i < lodInfo.Length; i++)
            lodInfo[i] = ReadXModelLodInfo(ref context);

        var maxLoadedLod = context.ReadByte();
        var numLods = context.ReadByte();
        var collLod = context.ReadByte();
        var flags = context.ReadByte();
        var collSurfs = context.ReadPointer<XModelCollSurf[]>();
        var numCollSurfs = context.ReadInt32();
        var contents = context.ReadInt32();
        var boneInfo = context.ReadPointer<XBoneInfo[]>();
        var radius = context.ReadFloat();
        var bounds = ReadBounds(ref context);
        var memUsage = context.ReadInt32();
        var bad = context.ReadBool();
        var badPadding0 = context.ReadByte();
        var badPadding1 = context.ReadByte();
        var badPadding2 = context.ReadByte();
        var physPreset = PhysicsReader.ReadPhysPresetPointer(ref context);
        var physCollmap = PhysicsReader.ReadPhysCollmapPointer(ref context);

        GenericReader.ResolveStringPointerNow(ref context, name);
        ResolveInlineUShortArray(ref context, boneNames, numBones);
        ResolveParentArray(ref context, parentList, Math.Max(0, numBones - numRootBones));
        ResolveQuatArray(ref context, quats, Math.Max(0, numBones - numRootBones));
        ResolveVec3Array(ref context, trans, Math.Max(0, numBones - numRootBones));
        ResolvePartClassificationArray(ref context, partClassification, numBones);
        ResolveDObjAnimMatArray(ref context, baseMat, numBones);
        ResolveMaterialHandleArray(ref context, materialHandles, numSurfs);
        ResolveXModelCollSurfArray(ref context, collSurfs, numCollSurfs);
        ResolveXBoneInfoArray(ref context, boneInfo, numBones);

        return new XModel
        {
            Offset = offset, NamePtr = name, NumBones = numBones, NumRootBones = numRootBones,
            NumSurfs = numSurfs, LodRampType = lodRampType, Scale = scale, NoScalePartBits = noScalePartBits,
            BoneNames = boneNames, ParentList = parentList, Quats = quats, Trans = trans,
            PartClassification = partClassification, BaseMat = baseMat, MaterialHandles = materialHandles,
            LodInfo = lodInfo, MaxLoadedLod = maxLoadedLod, NumLods = numLods, CollLod = collLod, Flags = flags,
            CollSurfs = collSurfs, NumCollSurfs = numCollSurfs, Contents = contents, BoneInfo = boneInfo,
            Radius = radius, Bounds = bounds, MemUsage = memUsage, Bad = bad,
            BadPadding0 = badPadding0, BadPadding1 = badPadding1, BadPadding2 = badPadding2,
            PhysPreset = physPreset, PhysCollmap = physCollmap,
        };
    }

    private static XModelLodInfo ReadXModelLodInfo(ref ZoneReadContext context)
    {
        var start = context.Position;
        var lodInfo = new XModelLodInfo
        {
            Dist = context.ReadFloat(),
            NumSurfs = context.ReadUInt16(),
            SurfIndex = context.ReadUInt16(),
            ModelSurfs = context.ReadPointer<XModelSurfs>(),
        };
        for (var i = 0; i < lodInfo.PartBits.Length; i++)
            lodInfo.PartBits[i] = context.ReadInt32();
        lodInfo.Surfs = context.ReadPointer<XSurface[]>();

        var bytesRead = context.Position - start;
        if (bytesRead != LodInfoSize)
            throw new InvalidDataException($"XModelLodInfo read {bytesRead:N0} bytes; expected {LodInfoSize:N0}.");
        return lodInfo;
    }

    private static void ResolveParentArray(ref ZoneReadContext context, ZonePointer<XModelParent[]> pointer, int count)
    {
        if (count <= 0 || pointer.Kind != PointerKind.Inline) { pointer.SetResult(Array.Empty<XModelParent>()); return; }
        context.ResolveInlinePointerNow(pointer, (ref ZoneReadContext pc, ZonePointer<XModelParent[]> p) =>
            p.SetResult(pc.ReadPointerValue(p, (ref ZoneReadContext v) =>
            {
                var a = new XModelParent[count];
                for (var i = 0; i < a.Length; i++) a[i] = new XModelParent(v.ReadByte());
                return a;
            })));
    }

    private static void ResolveQuatArray(ref ZoneReadContext context, ZonePointer<XModelQuat[]> pointer, int count)
    {
        if (count <= 0 || pointer.Kind != PointerKind.Inline) { pointer.SetResult(Array.Empty<XModelQuat>()); return; }
        context.ResolveInlinePointerNow(pointer, (ref ZoneReadContext pc, ZonePointer<XModelQuat[]> p) =>
            p.SetResult(pc.ReadPointerValue(p, (ref ZoneReadContext v) =>
            {
                var a = new XModelQuat[count];
                for (var i = 0; i < a.Length; i++)
                    a[i] = new XModelQuat(unchecked((short)v.ReadUInt16()), unchecked((short)v.ReadUInt16()),
                        unchecked((short)v.ReadUInt16()), unchecked((short)v.ReadUInt16()));
                return a;
            })));
    }

    private static void ResolveVec3Array(ref ZoneReadContext context, ZonePointer<Vec3[]> pointer, int count)
    {
        if (count <= 0 || pointer.Kind != PointerKind.Inline) { pointer.SetResult(Array.Empty<Vec3>()); return; }
        context.ResolveInlinePointerNow(pointer, (ref ZoneReadContext pc, ZonePointer<Vec3[]> p) =>
            p.SetResult(pc.ReadPointerValue(p, (ref ZoneReadContext v) =>
            {
                var a = new Vec3[count];
                for (var i = 0; i < a.Length; i++) a[i] = ReadVec3(ref v);
                return a;
            })));
    }

    private static void ResolvePartClassificationArray(ref ZoneReadContext context, ZonePointer<XModelPartClassification[]> pointer, int count)
    {
        if (count <= 0 || pointer.Kind != PointerKind.Inline) { pointer.SetResult(Array.Empty<XModelPartClassification>()); return; }
        context.ResolveInlinePointerNow(pointer, (ref ZoneReadContext pc, ZonePointer<XModelPartClassification[]> p) =>
            p.SetResult(pc.ReadPointerValue(p, (ref ZoneReadContext v) =>
            {
                var a = new XModelPartClassification[count];
                for (var i = 0; i < a.Length; i++) a[i] = new XModelPartClassification(v.ReadByte());
                return a;
            })));
    }

    private static void ResolveDObjAnimMatArray(ref ZoneReadContext context, ZonePointer<DObjAnimMat[]> pointer, int count)
    {
        if (count <= 0 || pointer.Kind != PointerKind.Inline) { pointer.SetResult(Array.Empty<DObjAnimMat>()); return; }
        context.ResolveInlinePointerNow(pointer, (ref ZoneReadContext pc, ZonePointer<DObjAnimMat[]> p) =>
            p.SetResult(pc.ReadPointerValue(p, (ref ZoneReadContext v) =>
            {
                var a = new DObjAnimMat[count];
                for (var i = 0; i < a.Length; i++)
                    a[i] = new DObjAnimMat { Quat = v.ReadVec4(), Trans = ReadVec3(ref v), TransWeight = v.ReadFloat() };
                return a;
            })));
    }

    private static void ResolveXModelCollSurfArray(ref ZoneReadContext context, ZonePointer<XModelCollSurf[]> pointer, int count)
    {
        if (count <= 0 || pointer.Kind != PointerKind.Inline) { pointer.SetResult(Array.Empty<XModelCollSurf>()); return; }
        context.ResolveInlinePointerNow(pointer, (ref ZoneReadContext pc, ZonePointer<XModelCollSurf[]> p) =>
            p.SetResult(pc.ReadPointerValue(p, (ref ZoneReadContext v) =>
            {
                var a = new XModelCollSurf[count];
                for (var i = 0; i < a.Length; i++)
                    a[i] = new XModelCollSurf
                    {
                        CollTris = v.ReadPointer<XModelCollTri[]>(),
                        NumCollTris = v.ReadInt32(),
                        Bounds = ReadBounds(ref v),
                        BoneIdx = v.ReadInt32(),
                        Contents = v.ReadInt32(),
                        SurfFlags = v.ReadInt32(),
                    };
                return a;
            })));
    }

    private static void ResolveXBoneInfoArray(ref ZoneReadContext context, ZonePointer<XBoneInfo[]> pointer, int count)
    {
        if (count <= 0 || pointer.Kind != PointerKind.Inline) { pointer.SetResult(Array.Empty<XBoneInfo>()); return; }
        context.ResolveInlinePointerNow(pointer, (ref ZoneReadContext pc, ZonePointer<XBoneInfo[]> p) =>
            p.SetResult(pc.ReadPointerValue(p, (ref ZoneReadContext v) =>
            {
                var a = new XBoneInfo[count];
                for (var i = 0; i < a.Length; i++)
                    a[i] = new XBoneInfo { Bounds = ReadBounds(ref v), RadiusSquared = v.ReadFloat() };
                return a;
            })));
    }

    private static void ResolveInlineUShortArray(ref ZoneReadContext context, ZonePointer<ushort[]> pointer, int count)
    {
        if (count <= 0 || pointer.Kind != PointerKind.Inline) { pointer.SetResult(Array.Empty<ushort>()); return; }
        context.ResolveInlinePointerNow(pointer, (ref ZoneReadContext pc, ZonePointer<ushort[]> p) =>
            p.SetResult(pc.ReadPointerValue(p, (ref ZoneReadContext v) =>
            {
                var a = new ushort[count];
                for (var i = 0; i < a.Length; i++) a[i] = v.ReadUInt16();
                return a;
            })));
    }

    private static void ResolveMaterialHandleArray(ref ZoneReadContext context, ZonePointer<ZonePointer<Material>[]> pointer, int count)
    {
        if (count <= 0 || pointer.Kind != PointerKind.Inline) { pointer.SetResult(Array.Empty<ZonePointer<Material>>()); return; }
        context.ResolveInlinePointerNow(pointer, (ref ZoneReadContext pc, ZonePointer<ZonePointer<Material>[]> p) =>
        {
            var values = new ZonePointer<Material>[count];
            for (var i = 0; i < values.Length; i++)
                values[i] = pc.ReadPointer<Material>();
            p.SetResult(values);
            foreach (var value in values)
                pc.ResolveInlinePointer(value,
                    (ref ZoneReadContext mc, ZonePointer<Material> mp) => mp.SetResult(mc.ReadPointerValue(mp, MaterialReader.Read)));
        });
    }

    private static int[] ReadInt32Array(ref ZoneReadContext context, int count)
    {
        var a = new int[count];
        for (var i = 0; i < a.Length; i++) a[i] = context.ReadInt32();
        return a;
    }

    private static Bounds ReadBounds(ref ZoneReadContext context)
        => new() { MidPoint = ReadVec3(ref context), HalfSize = ReadVec3(ref context) };

    private static Vec3 ReadVec3(ref ZoneReadContext context)
        => new() { X = context.ReadFloat(), Y = context.ReadFloat(), Z = context.ReadFloat() };
}
