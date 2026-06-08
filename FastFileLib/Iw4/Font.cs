// =============================================================================
// IW4 (MW2 PS3) zone reader — ported from Jacob Schroeder's FastFile
//   https://github.com/jacob-schroeder/FastFile
// Ports: FastFile.Logic/Assets/Readers/FontReader.cs and
//        FastFile.Models/Assets/Fonts/FontAsset.cs (FontAsset + FontGlyph).
//
// Font_s is a 24-byte root (name + pixelHeight + glyphCount + material + glowMaterial +
// glyphs pointers); the name, glyph table, and materials all live in other (LARGE/shared)
// blocks, i.e. Offset pointers. The local inline engine resolves only inline (-1) pointers,
// so for a typical font nothing is consumed past the 24-byte root and the walk simply
// advances to the next asset. This is what lets the body walk step past the fonts that sit
// right before the big localize block in zones like code_post_gfx_mp.ff.
// =============================================================================

namespace FastFileLib.Iw4;

public sealed class FontAsset : BaseAsset
{
    public const int RootSize = 0x18;  // 24
    public const int GlyphSize = 0x18; // 24

    public FontAsset() : base(XAssetType.Font) { }

    public ZonePointer<string>? NamePtr { get; set; }
    public string Name => NamePtr is { IsResolved: true } ? NamePtr.Result ?? string.Empty : string.Empty;
    public int PixelHeight { get; set; }
    public int GlyphCount { get; set; }
    public ZonePointer<Material>? Material { get; set; }
    public ZonePointer<Material>? GlowMaterial { get; set; }
    public ZonePointer<FontGlyph[]>? Glyphs { get; set; }

    public override string? GetDisplayName => string.IsNullOrWhiteSpace(Name) ? Type.ToString() : Name;
}

public sealed class FontGlyph
{
    public ushort Letter;
    public byte X0, Y0, Dx, PixelWidth, PixelHeight, Padding;
    public float S0, T0, S1, T1;
}

internal static class FontReader
{
    public static FontAsset Read(ref ZoneReadContext context)
    {
        var asset = new FontAsset
        {
            Offset = context.Position,
            NamePtr = GenericReader.ReadStringPointer(ref context),
            PixelHeight = context.ReadInt32(),
            GlyphCount = context.ReadInt32(),
            Material = MaterialReader.ReadMaterialPointer(ref context),
            GlowMaterial = MaterialReader.ReadMaterialPointer(ref context),
        };

        asset.Glyphs = context.ReadPointer<FontGlyph[]>(
            (ref ZoneReadContext pointerContext, ZonePointer<FontGlyph[]> pointer) =>
            {
                var glyphs = new FontGlyph[Math.Max(0, asset.GlyphCount)];
                for (var i = 0; i < glyphs.Length; i++)
                    glyphs[i] = ReadGlyph(ref pointerContext);
                pointer.SetResult(glyphs);
            });

        return asset;
    }

    private static FontGlyph ReadGlyph(ref ZoneReadContext context) => new()
    {
        Letter = context.ReadUInt16(),
        X0 = context.ReadByte(),
        Y0 = context.ReadByte(),
        Dx = context.ReadByte(),
        PixelWidth = context.ReadByte(),
        PixelHeight = context.ReadByte(),
        Padding = context.ReadByte(),
        S0 = context.ReadFloat(),
        T0 = context.ReadFloat(),
        S1 = context.ReadFloat(),
        T1 = context.ReadFloat(),
    };
}
