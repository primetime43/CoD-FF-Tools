namespace FastFileLib;

// -----------------------------------------------------------------------------
// IW4 (MW2 PS3) offset-pointer resolution model — Direct vs Alias.
//
// Source of truth: Jacob Schroeder's PS3 EBOOT.ELF trace (FastFile repo,
//   EBOOT_ZONE_LOAD_MODEL.md + the "EBOOT.ELF Fastfile / XFile Loader Summary").
//
// An *offset* pointer (ZonePointerKind.Offset) is not resolved one way. The EBOOT
// has two distinct fixup helpers, and which one a field uses is a property of that
// field's loader path, NOT of its C# type:
//
//   OffsetDirect (EBOOT 0x0011DC00):
//     target = g_streamBlocks[block] + offset
//     The decoded offset points straight at the pointed-to data.
//
//   OffsetAlias  (EBOOT 0x0011DBD0):
//     cell   = g_streamBlocks[block] + offset
//     target = *(uint32*)cell
//     The decoded offset points at a 4-byte pointer cell that CONTAINS the data
//     pointer — one level of indirection. Used by root asset-wrapper references
//     and by fields that reach another asset through its wrapper.
//
// A writer must encode Direct pointers to the data span and Alias pointers to the
// 4-byte alias cell; getting this wrong is the suspected PS3 black-screen cause for
// edited fastfiles that move data (the decoded offset still lands in a valid block
// range, so range-only audits pass, but the EBOOT dereferences the wrong thing).
//
// PROOF GATE: a field path is only Direct/Alias if the exact EBOOT loader call that
// handles it was traced. Everything else stays Unknown and must NOT be rebased by a
// writer. Do not classify from C# value type, decoded block, or "reasonable-looking"
// offset alone — those can find the next branch to trace, but are not authority.
//
// See docs/MW2_PS3_EBOOT_Zone_Load_Model.md.
// -----------------------------------------------------------------------------

/// <summary>
/// Which EBOOT fixup helper resolves an <see cref="ZonePointerKind.Offset"/> pointer field.
/// </summary>
public enum PointerResolutionKind
{
    /// <summary>No EBOOT evidence yet for this field path. A writer must not relocate it.</summary>
    Unknown,
    /// <summary>EBOOT calls <c>OffsetDirect</c>: the offset points straight at the data.</summary>
    Direct,
    /// <summary>EBOOT calls <c>OffsetAlias</c>: the offset points at a 4-byte cell holding the data pointer.</summary>
    Alias,
}

/// <summary>
/// The EBOOT-proofed Direct/Alias rule table for MW2 PS3 (IW4) offset pointers, keyed by a
/// normalized field path (e.g. <c>"Material.TechniqueSet"</c>, <c>"WeaponDef.GunXModel.Element"</c>).
///
/// This table is proof-complete for the two official patch zones
/// (<c>patch_mp_case_1.zone</c> / <c>patch_mp_case_2.zone</c>): every decoded offset pointer in
/// them maps to Direct or Alias with zero Unknown. Any field path not in the table resolves to
/// <see cref="PointerResolutionKind.Unknown"/> by design — add it only after tracing the EBOOT
/// loader call for that field.
///
/// Ported from Jacob Schroeder's resolution rule map (FastFile repo).
/// </summary>
public static class ZonePointerResolution
{
    private static readonly IReadOnlyDictionary<string, PointerResolutionKind> Rules =
        new Dictionary<string, PointerResolutionKind>(StringComparer.Ordinal)
    {
        // ---- Root / list spine ----
        ["XAssetList.ScriptStrings"] = PointerResolutionKind.Direct,
        ["XAssetList.Assets"] = PointerResolutionKind.Direct,
        ["XAssetList.ScriptString"] = PointerResolutionKind.Direct,
        // The 4-byte asset header pointer of every root XAsset entry is an alias cell.
        ["XAsset.Header"] = PointerResolutionKind.Alias,

        // ---- Strings ----
        ["XString"] = PointerResolutionKind.Direct,

        // ---- RawFile ----
        // RawFile.buffer is written inline in block 4 for these patch files; when it IS an offset it's Direct.
        ["RawFile.Buffer"] = PointerResolutionKind.Direct,

        // ---- Material ----
        ["Material.TechniqueSet"] = PointerResolutionKind.Alias,
        ["Material.TextureTable"] = PointerResolutionKind.Direct,
        ["Material.ConstantTable"] = PointerResolutionKind.Direct,
        ["Material.StateBitTable"] = PointerResolutionKind.Direct,
        ["Material.Info.Name"] = PointerResolutionKind.Direct,
        ["MaterialAssetRef"] = PointerResolutionKind.Alias,
        ["MaterialTextureDef.Image"] = PointerResolutionKind.Alias,

        // ---- Menu / MenuList ----
        ["MenuList.Name"] = PointerResolutionKind.Direct,
        ["MenuList.Menus"] = PointerResolutionKind.Direct,
        ["MenuDef.Items"] = PointerResolutionKind.Direct,
        ["MenuDef.ExpressionData"] = PointerResolutionKind.Direct,
        ["Statement"] = PointerResolutionKind.Direct,
        ["Statement.Entries"] = PointerResolutionKind.Direct,
        ["ExpressionSupportingData"] = PointerResolutionKind.Direct,
        ["MenuEventHandlerSet"] = PointerResolutionKind.Direct,
        ["MenuEventHandler.UnconditionalScript"] = PointerResolutionKind.Direct,
        ["ItemKeyHandler"] = PointerResolutionKind.Direct,
        ["Window.Name"] = PointerResolutionKind.Direct,
        ["Window.Group"] = PointerResolutionKind.Direct,
        ["Window.Background"] = PointerResolutionKind.Alias,
        ["Operand.StringVal"] = PointerResolutionKind.Direct,
        ["Operand.Function"] = PointerResolutionKind.Direct,

        // ---- Weapon (WeaponVariantDef + WeaponDef) ----
        ["WeaponVariantDef.WeaponDef"] = PointerResolutionKind.Direct,
        ["WeaponVariantDef.HideTags"] = PointerResolutionKind.Direct,
        ["WeaponVariantDef.XAnims"] = PointerResolutionKind.Direct,
        ["WeaponVariantDef.XAnims.Element"] = PointerResolutionKind.Direct,
        ["WeaponVariantDef.AccuracyGraphKnots"] = PointerResolutionKind.Direct,
        ["WeaponVariantDef.OriginalAccuracyGraphKnots"] = PointerResolutionKind.Direct,
        ["Weapon.XString"] = PointerResolutionKind.Direct,
        ["Weapon.UShortArray"] = PointerResolutionKind.Direct,
        ["Weapon.FloatArray"] = PointerResolutionKind.Direct,
        ["Weapon.Vec2Array"] = PointerResolutionKind.Direct,
        ["Weapon.XStringArray"] = PointerResolutionKind.Direct,
        ["Weapon.Material"] = PointerResolutionKind.Alias,
        ["Weapon.XModel"] = PointerResolutionKind.Alias,
        ["Weapon.Fx"] = PointerResolutionKind.Alias,
        ["Weapon.PhysCollmap"] = PointerResolutionKind.Alias,
        ["Weapon.Tracer"] = PointerResolutionKind.Alias,
        ["WeaponDef.GunXModel"] = PointerResolutionKind.Direct,
        ["WeaponDef.GunXModel.Element"] = PointerResolutionKind.Alias,
        ["WeaponDef.szXAnimsR"] = PointerResolutionKind.Direct,
        ["WeaponDef.szXAnimsR.Element"] = PointerResolutionKind.Direct,
        ["WeaponDef.szXAnimsL"] = PointerResolutionKind.Direct,
        ["WeaponDef.szXAnimsL.Element"] = PointerResolutionKind.Direct,
        ["WeaponDef.NoteTrackMaps"] = PointerResolutionKind.Direct,
        ["WeaponDef.BounceSound"] = PointerResolutionKind.Direct,
        ["WeaponDef.BounceSound.Element"] = PointerResolutionKind.Direct,
        ["WeaponDef.WorldGunXModel"] = PointerResolutionKind.Direct,
        ["WeaponDef.WorldGunXModel.Element"] = PointerResolutionKind.Alias,
        ["WeaponDef.ParallelBounce"] = PointerResolutionKind.Direct,
        ["WeaponDef.PerpendicularBounce"] = PointerResolutionKind.Direct,
        ["WeaponDef.AccuracyGraphKnots"] = PointerResolutionKind.Direct,
        ["WeaponDef.OriginalAccuracyGraphKnots"] = PointerResolutionKind.Direct,
        ["WeaponDef.LocationDamageMultipliers"] = PointerResolutionKind.Direct,

        // ---- StringList (menu string arrays) ----
        ["StringList.Strings"] = PointerResolutionKind.Direct,
        ["StringList.Strings.Element"] = PointerResolutionKind.Direct,

        // ---- Shared asset-reference wrappers ----
        ["XModelAssetRef"] = PointerResolutionKind.Alias,
        ["XModelAssetRefArray"] = PointerResolutionKind.Direct,
        ["FxEffectAssetRef"] = PointerResolutionKind.Alias,
        ["PhysCollmapAssetRef"] = PointerResolutionKind.Alias,
        ["TracerAssetRef"] = PointerResolutionKind.Alias,
        ["XModelSurfsAssetRef"] = PointerResolutionKind.Alias,
    };

    /// <summary>Number of EBOOT-proofed field paths in the table.</summary>
    public static int RuleCount => Rules.Count;

    /// <summary>All proofed field paths (for diagnostics / auditing).</summary>
    public static IReadOnlyCollection<string> FieldPaths => (IReadOnlyCollection<string>)Rules.Keys;

    /// <summary>
    /// Looks up the EBOOT fixup behavior for a normalized field path. Returns
    /// <see cref="PointerResolutionKind.Unknown"/> for any path without traced EBOOT evidence —
    /// callers (writers/validators) must treat Unknown as "do not relocate".
    /// </summary>
    public static PointerResolutionKind Resolve(string fieldPath)
        => fieldPath is not null && Rules.TryGetValue(fieldPath, out var kind)
            ? kind
            : PointerResolutionKind.Unknown;

    /// <summary>True when the field path has traced EBOOT evidence (Direct or Alias).</summary>
    public static bool IsProofed(string fieldPath) => Resolve(fieldPath) != PointerResolutionKind.Unknown;
}
