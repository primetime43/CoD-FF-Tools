// =============================================================================
// IW4 (MW2 PS3) zone reader — ported from Jacob Schroeder's FastFile
//   https://github.com/jacob-schroeder/FastFile
// Ports: FastFile.Models/Assets/Tracers/TracerDef.cs and
//        FastFile.Logic/Assets/Readers/TracerReader.cs. Material is the only dep (ported).
// =============================================================================

namespace FastFileLib.Iw4;

public class TracerDef : BaseAsset
{
    public TracerDef() : base(XAssetType.Tracer) { }
    public ZonePointer<string>? NamePtr { get; set; }
    public string Name => NamePtr is { IsResolved: true } ? NamePtr.Result ?? string.Empty : string.Empty;
    public ZonePointer<Material>? Material { get; set; }
    public uint DrawInterval;
    public float Speed, BeamLength, BeamWidth, ScrewRadius, ScrewDist;
    public Vec4[] Colors { get; set; } = new Vec4[5];
    public override string? GetDisplayName => Name;
}

internal static class TracerReader
{
    public static TracerDef Read(ref ZoneReadContext context)
    {
        var asset = new TracerDef
        {
            Offset = context.Position,
            NamePtr = GenericReader.ReadStringPointer(ref context),
            Material = MaterialReader.ReadMaterialPointer(ref context),
            DrawInterval = context.ReadUInt32(),
            Speed = context.ReadFloat(),
            BeamLength = context.ReadFloat(),
            BeamWidth = context.ReadFloat(),
            ScrewRadius = context.ReadFloat(),
            ScrewDist = context.ReadFloat(),
        };

        for (var i = 0; i < asset.Colors.Length; i++)
            asset.Colors[i] = context.ReadVec4();

        return asset;
    }

    public static ZonePointer<TracerDef> ReadTracerPointer(ref ZoneReadContext context)
        => context.ReadPointer<TracerDef>(
            (ref ZoneReadContext pc, ZonePointer<TracerDef> p) => p.SetResult(pc.ReadPointerValue(p, Read)));
}
