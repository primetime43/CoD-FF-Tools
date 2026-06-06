// =============================================================================
// IW4 (MW2 PS3) zone reader — ported from Jacob Schroeder's FastFile
//   https://github.com/jacob-schroeder/FastFile
// Ports: FastFile.Models/Assets/Weapons/{WeaponVariantDef,WeaponDef}.cs and
//        FastFile.Logic/Assets/Readers/WeaponReader.cs.
// The weapon's xmodel / fx / tracer / physcollmap sub-assets are external references
// (read as pointers, resolved only when inline). Rather than port those four full
// readers, the *Pointer helpers below read the pointer and stop cleanly if it is ever
// inline (which a weapon referencing shared assets never is). His EnsureFixedSize checks
// (WeaponVariantDef = 0x74, WeaponDef = 0x684) validate the byte layout.
// =============================================================================

namespace FastFileLib.Iw4;

// placeholder sub-asset types (referenced as pointers only; XModel/FxEffectDef/TracerDef are real)
public sealed class PhysCollmap { }
public sealed class PhysPreset { }

public struct Vec2 { public float a; public float b; }

public enum ImpactType { Default = 0 }
public enum WeaponIconRatioType { Default = 0 }

public sealed class WeaponBooleanFlags
{
    public bool NoAdsWhenMagEmpty, AvoidDropCleanup, InheritsPerks, CrosshairColorChange, RifleBullet,
        ArmorPiercing, BoltAction, AimDownSight, RechamberWhileAds, BulletExplosiveDamage, CookOffHold,
        ClipOnly, NoAmmoPickup, AdsFireOnly, CancelAutoHolsterWhenEmpty, DisableSwitchToWhenEmpty,
        SuppressAmmoReserveDisplay, LaserSightDuringNightvision, MarkableViewmodel, NoDualWield, FlipKillIcon,
        NoPartialReload, SegmentedReload, BlocksProne, Silenced, IsRollingGrenade, ProjExplosionEffectForceNormalUp,
        ProjImpactExplode, StickToPlayers, HasDetonator, DisableFiring, TimedDetonation, Rotate, HoldButtonToThrow,
        FreezeMovementWhenFiring, ThermalScope, AltModeSameWeapon, TurretBarrelSpinEnabled, MissileConeSoundEnabled,
        MissileConeSoundPitchshiftEnabled, MissileConeSoundCrossfadeEnabled, OffhandHoldIsCancelable;
    public byte Ps3TailFlag0, Ps3TailFlag1;
}

public sealed class WeaponDef
{
    public int Offset;
    public ZonePointer<string>? InternalNamePtr { get; set; }
    public ZonePointer<ZonePointer<XModel>[]>? gunXModel { get; set; }
    public ZonePointer<XModel>? handXModel { get; set; }
    public ZonePointer<ZonePointer<string>[]>? szXAnimsR { get; set; }
    public ZonePointer<ZonePointer<string>[]>? szXAnimsL { get; set; }
    public ZonePointer<string>? ModeNamePtr { get; set; }
    public ZonePointer<ushort[]>[] NoteTrackMaps { get; set; } = new ZonePointer<ushort[]>[4];
    public int[] PlayerAnimTypeThroughStance { get; set; } = new int[8];
    public ZonePointer<FxEffectDef>[] FlashEffects { get; set; } = new ZonePointer<FxEffectDef>[2];
    public ZonePointer<string>[] SoundAliases { get; set; } = new ZonePointer<string>[47];
    public ZonePointer<ZonePointer<string>[]>? BounceSound { get; set; }
    public ZonePointer<FxEffectDef>[] EffectPointersA { get; set; } = new ZonePointer<FxEffectDef>[4];
    public ZonePointer<Material>[] MaterialPointersA { get; set; } = new ZonePointer<Material>[2];
    public int[] ReticleFields { get; set; } = new int[4];
    public int[] ViewMovementRotationFields { get; set; } = new int[30];
    public int[] PositionalMovementRotationFields { get; set; } = new int[10];
    public ZonePointer<ZonePointer<XModel>[]>? WorldGunXModel { get; set; }
    public ZonePointer<XModel>[] WorldModelPointers { get; set; } = new ZonePointer<XModel>[4];
    public ZonePointer<Material>? AmmoCounterIcon { get; set; }
    public int AmmoCounterIconRatio;
    public ZonePointer<Material>? CompassIcon { get; set; }
    public int CompassIconRatio;
    public ZonePointer<Material>? OverlayMaterial { get; set; }
    public int[] OverlayFieldsA { get; set; } = new int[3];
    public ZonePointer<string>? OverlayReticle { get; set; }
    public int OverlayReticleField;
    public ZonePointer<string>? OverlayInterface { get; set; }
    public int[] OverlayFieldsB { get; set; } = new int[3];
    public ZonePointer<string>? ModeNameAlt { get; set; }
    public int[] ModeFields { get; set; } = new int[6];
    public int[] WeaponTimingFields { get; set; } = new int[40];
    public int[] AimMovementTuningFields { get; set; } = new int[10];
    public ZonePointer<Material>[] OverlayMaterials { get; set; } = new ZonePointer<Material>[4];
    public int[] OverlayDimensionFields { get; set; } = new int[6];
    public int[] BobSpreadIdleSwayAdsViewErrorFields { get; set; } = new int[38];
    public ZonePointer<PhysCollmap>? PhysCollmap { get; set; }
    public int[] PhysicsFieldsA { get; set; } = new int[2];
    public int[] PhysicsFieldsB { get; set; } = new int[5];
    public int[] PhysicsFieldsC { get; set; } = new int[7];
    public int[] PhysicsFieldsD { get; set; } = new int[7];
    public ZonePointer<XModel>? ProjectileModel { get; set; }
    public int ProjectileModelField;
    public ZonePointer<FxEffectDef>[] ProjectileEffects { get; set; } = new ZonePointer<FxEffectDef>[2];
    public ZonePointer<string>[] ProjectileSoundAliases { get; set; } = new ZonePointer<string>[2];
    public int[] ProjectileFieldsA { get; set; } = new int[3];
    public ZonePointer<float[]>? ParallelBounce { get; set; }
    public ZonePointer<float[]>? PerpendicularBounce { get; set; }
    public ZonePointer<FxEffectDef>[] ImpactEffects { get; set; } = new ZonePointer<FxEffectDef>[2];
    public int[] ImpactFieldsA { get; set; } = new int[3];
    public int ImpactFieldB;
    public int[] ImpactFieldsC { get; set; } = new int[2];
    public ZonePointer<FxEffectDef>? ViewShellEjectEffect { get; set; }
    public ZonePointer<string>? ShellEjectSound { get; set; }
    public int[] ShellEjectFields { get; set; } = new int[3];
    public int[] AdsHipGunKickAiDistanceFields { get; set; } = new int[35];
    public ZonePointer<string>? AccuracyGraphName0 { get; set; }
    public ZonePointer<string>? AccuracyGraphName1 { get; set; }
    public ZonePointer<Vec2[]>? accuracyGraphKnots { get; set; }
    public ZonePointer<Vec2[]>? originalAccuracyGraphKnots { get; set; }
    public ushort accuracyGraphKnotCount, originalAccuracyGraphKnotCount;
    public int AccuracyGraphField;
    public float LeftArc, RightArc, TopArc, BottomArc, Accuracy, AiSpread, PlayerSpread;
    public float[] MinTurnSpeed { get; set; } = new float[2];
    public float[] MaxTurnSpeed { get; set; } = new float[2];
    public float PitchConvergenceTime, YawConvergenceTime, SuppressTime, MaxRange, AnimHorizontalRotateInc, PlayerPositionDist;
    public ZonePointer<string>? UseHintString { get; set; }
    public ZonePointer<string>? DropHintString { get; set; }
    public int[] HintFieldsA { get; set; } = new int[2];
    public int[] HintFieldsB { get; set; } = new int[5];
    public ZonePointer<string>? ScriptName { get; set; }
    public int[] ScriptFieldsA { get; set; } = new int[2];
    public int[] ScriptFieldsB { get; set; } = new int[6];
    public int HitLocationField;
    public ZonePointer<float[]>? LocationDamageMultipliers { get; set; }
    public ZonePointer<string>? FireRumble { get; set; }
    public ZonePointer<string>? MeleeImpactRumble { get; set; }
    public ZonePointer<TracerDef>? Tracer { get; set; }
    public int[] TracerFields { get; set; } = new int[6];
    public ZonePointer<string>? TurretOverheatSound { get; set; }
    public ZonePointer<FxEffectDef>? TurretOverheatEffect { get; set; }
    public ZonePointer<string>? TurretBarrelSpinRumble { get; set; }
    public int[] TurretFields { get; set; } = new int[3];
    public ZonePointer<string>? TurretBarrelSpinMaxSnd { get; set; }
    public ZonePointer<string>[] TurretBarrelSpinUpSnd { get; set; } = new ZonePointer<string>[4];
    public ZonePointer<string>[] TurretBarrelSpinDownSnd { get; set; } = new ZonePointer<string>[4];
    public ZonePointer<string>? MissileConeSoundAlias { get; set; }
    public ZonePointer<string>? MissileConeSoundAliasAtBase { get; set; }
    public float MissileConeSoundRadiusAtTop, MissileConeSoundRadiusAtBase, MissileConeSoundHeight,
        MissileConeSoundOriginOffset, MissileConeSoundVolumescaleAtCore, MissileConeSoundVolumescaleAtEdge,
        MissileConeSoundVolumescaleCoreSize, MissileConeSoundPitchAtTop, MissileConeSoundPitchAtBottom,
        MissileConeSoundPitchTopSize, MissileConeSoundPitchBottomSize, MissileConeSoundCrossfadeTopSize,
        MissileConeSoundCrossfadeBottomSize;
    public bool SharedAmmo, LockonSupported, RequireLockonToFire, BigExplosion;
    public WeaponBooleanFlags BooleanFlags { get; set; } = new();
    public string InternalName => InternalNamePtr is { IsResolved: true } ? InternalNamePtr.Result ?? string.Empty : string.Empty;
}

public sealed class WeaponVariantDef : BaseAsset
{
    public WeaponVariantDef() : base(XAssetType.Weapon) { }
    public ZonePointer<string>? InternalNamePtr { get; set; }
    public ZonePointer<WeaponDef>? WeaponDefPtr { get; set; }
    public ZonePointer<string>? DisplayNamePtr { get; set; }
    public ZonePointer<ushort[]>? HideTags { get; set; }
    public ZonePointer<ZonePointer<string>[]>? XAnims { get; set; }
    public float fAdsZoomFov;
    public int iAdsTransInTime, iAdsTransOutTime, iClipSize;
    public ImpactType impactType;
    public int iFireTime;
    public WeaponIconRatioType dpadIconRatio;
    public float fPenetrateMultiplier, fAdsViewKickCenterSpeed, fHipViewKickCenterSpeed;
    public ZonePointer<string>? szAltWeaponName { get; set; }
    public uint altWeaponIndex;
    public int iAltRaiseTime;
    public ZonePointer<Material>? killIcon { get; set; }
    public ZonePointer<Material>? dpadIcon { get; set; }
    public int unknown8, iFirstRaiseTime, iDropAmmoMax;
    public float adsDofStart, adsDofEnd;
    public short accuracyGraphKnotCount, originalAccuracyGraphKnotCount;
    public ZonePointer<Vec2[]>? accuracyGraphKnots { get; set; }
    public ZonePointer<Vec2[]>? originalAccuracyGraphKnots { get; set; }
    public bool motionTracker, enhanced, dpadIconShowsAmmo;
    public byte DpadIconShowsAmmoPadding;

    public string InternalName => InternalNamePtr is { IsResolved: true } ? InternalNamePtr.Result ?? string.Empty : string.Empty;
    public override string? GetDisplayName => string.IsNullOrWhiteSpace(InternalName) ? $"Weapon 0x{Offset:X8}" : InternalName;
}

// ---- stub sub-asset pointer readers (external references only; inline => clean stop) ----
internal static class PhysicsReader
{
    public static ZonePointer<PhysCollmap> ReadPhysCollmapPointer(ref ZoneReadContext context)
    {
        var p = context.ReadPointer<PhysCollmap>();
        if (p.Kind == PointerKind.Inline)
            throw new InvalidDataException("inline PhysCollmap parsing is not ported (IW4 reader).");
        return p;
    }

    public static ZonePointer<PhysPreset> ReadPhysPresetPointer(ref ZoneReadContext context)
    {
        var p = context.ReadPointer<PhysPreset>();
        if (p.Kind == PointerKind.Inline)
            throw new InvalidDataException("inline PhysPreset parsing is not ported (IW4 reader).");
        return p;
    }
}

internal static class WeaponReader
{
    private const int WeaponVariantDefSize = 0x74;
    private const int WeaponDefSize = 0x684;
    private const int GunModelCount = 16;
    private const int WeaponAnimCount = 37;
    private const int HideTagCount = 32;
    private const int NoteTrackMapCount = 16;
    private const int SurfaceCount = 31;
    private const int HitLocationCount = 20;
    private const int WeaponSoundAliasCount = 47;

    public static WeaponVariantDef Read(ref ZoneReadContext context)
    {
        var start = context.Position;
        var weapon = new WeaponVariantDef
        {
            Offset = start,
            InternalNamePtr = GenericReader.ReadStringPointer(ref context),
            WeaponDefPtr = ReadWeaponDefPointer(ref context),
            DisplayNamePtr = GenericReader.ReadStringPointer(ref context),
            HideTags = ReadUShortArrayPointer(ref context, HideTagCount),
            XAnims = GenericReader.ReadStringPointerArrayPointer(ref context, WeaponAnimCount),
            fAdsZoomFov = context.ReadFloat(),
            iAdsTransInTime = context.ReadInt32(),
            iAdsTransOutTime = context.ReadInt32(),
            iClipSize = context.ReadInt32(),
            impactType = (ImpactType)context.ReadInt32(),
            iFireTime = context.ReadInt32(),
            dpadIconRatio = (WeaponIconRatioType)context.ReadInt32(),
            fPenetrateMultiplier = context.ReadFloat(),
            fAdsViewKickCenterSpeed = context.ReadFloat(),
            fHipViewKickCenterSpeed = context.ReadFloat(),
            szAltWeaponName = GenericReader.ReadStringPointer(ref context),
            altWeaponIndex = context.ReadUInt32(),
            iAltRaiseTime = context.ReadInt32(),
            killIcon = MaterialReader.ReadMaterialPointer(ref context),
            dpadIcon = MaterialReader.ReadMaterialPointer(ref context),
            unknown8 = context.ReadInt32(),
            iFirstRaiseTime = context.ReadInt32(),
            iDropAmmoMax = context.ReadInt32(),
            adsDofStart = context.ReadFloat(),
            adsDofEnd = context.ReadFloat(),
            accuracyGraphKnotCount = (short)context.ReadUInt16(),
            originalAccuracyGraphKnotCount = (short)context.ReadUInt16(),
        };

        weapon.accuracyGraphKnots = ReadVec2ArrayPointer(ref context, weapon.accuracyGraphKnotCount);
        weapon.originalAccuracyGraphKnots = ReadVec2ArrayPointer(ref context, weapon.originalAccuracyGraphKnotCount);
        weapon.motionTracker = context.ReadByte() != 0;
        weapon.enhanced = context.ReadByte() != 0;
        weapon.dpadIconShowsAmmo = context.ReadByte() != 0;
        weapon.DpadIconShowsAmmoPadding = context.ReadByte();
        EnsureFixedSize(context.Position - start, WeaponVariantDefSize, "WeaponVariantDef");

        return weapon;
    }

    private static ZonePointer<WeaponDef> ReadWeaponDefPointer(ref ZoneReadContext context)
        => context.ReadPointer<WeaponDef>(
            (ref ZoneReadContext pc, ZonePointer<WeaponDef> p) => p.SetResult(pc.ReadPointerValue(p, ReadWeaponDef)));

    private static WeaponDef ReadWeaponDef(ref ZoneReadContext context)
    {
        var start = context.Position;
        var weaponDef = new WeaponDef
        {
            Offset = start,
            InternalNamePtr = GenericReader.ReadStringPointer(ref context),
            gunXModel = XModelReader.ReadXModelPointerArrayPointer(ref context, GunModelCount),
            handXModel = XModelReader.ReadXModelPointer(ref context),
            szXAnimsR = GenericReader.ReadStringPointerArrayPointer(ref context, WeaponAnimCount),
            szXAnimsL = GenericReader.ReadStringPointerArrayPointer(ref context, WeaponAnimCount),
            ModeNamePtr = GenericReader.ReadStringPointer(ref context),
        };

        for (var i = 0; i < weaponDef.NoteTrackMaps.Length; i++)
            weaponDef.NoteTrackMaps[i] = ReadUShortArrayPointer(ref context, NoteTrackMapCount);

        weaponDef.PlayerAnimTypeThroughStance = ReadInt32Array(ref context, weaponDef.PlayerAnimTypeThroughStance.Length);
        for (var i = 0; i < weaponDef.FlashEffects.Length; i++)
            weaponDef.FlashEffects[i] = FxReader.ReadFxPointer(ref context);

        for (var i = 0; i < WeaponSoundAliasCount; i++)
            weaponDef.SoundAliases[i] = GenericReader.ReadStringPointer(ref context);
        weaponDef.BounceSound = GenericReader.ReadStringPointerArrayPointer(ref context, SurfaceCount);

        for (var i = 0; i < weaponDef.EffectPointersA.Length; i++)
            weaponDef.EffectPointersA[i] = FxReader.ReadFxPointer(ref context);
        for (var i = 0; i < weaponDef.MaterialPointersA.Length; i++)
            weaponDef.MaterialPointersA[i] = MaterialReader.ReadMaterialPointer(ref context);
        weaponDef.ReticleFields = ReadInt32Array(ref context, weaponDef.ReticleFields.Length);
        weaponDef.ViewMovementRotationFields = ReadInt32Array(ref context, weaponDef.ViewMovementRotationFields.Length);
        weaponDef.PositionalMovementRotationFields = ReadInt32Array(ref context, weaponDef.PositionalMovementRotationFields.Length);

        weaponDef.WorldGunXModel = XModelReader.ReadXModelPointerArrayPointer(ref context, GunModelCount);
        for (var i = 0; i < weaponDef.WorldModelPointers.Length; i++)
            weaponDef.WorldModelPointers[i] = XModelReader.ReadXModelPointer(ref context);
        weaponDef.AmmoCounterIcon = MaterialReader.ReadMaterialPointer(ref context);
        weaponDef.AmmoCounterIconRatio = context.ReadInt32();
        weaponDef.CompassIcon = MaterialReader.ReadMaterialPointer(ref context);
        weaponDef.CompassIconRatio = context.ReadInt32();
        weaponDef.OverlayMaterial = MaterialReader.ReadMaterialPointer(ref context);
        weaponDef.OverlayFieldsA = ReadInt32Array(ref context, weaponDef.OverlayFieldsA.Length);
        weaponDef.OverlayReticle = GenericReader.ReadStringPointer(ref context);
        weaponDef.OverlayReticleField = context.ReadInt32();
        weaponDef.OverlayInterface = GenericReader.ReadStringPointer(ref context);
        weaponDef.OverlayFieldsB = ReadInt32Array(ref context, weaponDef.OverlayFieldsB.Length);
        weaponDef.ModeNameAlt = GenericReader.ReadStringPointer(ref context);
        weaponDef.ModeFields = ReadInt32Array(ref context, weaponDef.ModeFields.Length);
        weaponDef.WeaponTimingFields = ReadInt32Array(ref context, weaponDef.WeaponTimingFields.Length);
        weaponDef.AimMovementTuningFields = ReadInt32Array(ref context, weaponDef.AimMovementTuningFields.Length);

        for (var i = 0; i < weaponDef.OverlayMaterials.Length; i++)
            weaponDef.OverlayMaterials[i] = MaterialReader.ReadMaterialPointer(ref context);
        weaponDef.OverlayDimensionFields = ReadInt32Array(ref context, weaponDef.OverlayDimensionFields.Length);
        weaponDef.BobSpreadIdleSwayAdsViewErrorFields = ReadInt32Array(ref context, weaponDef.BobSpreadIdleSwayAdsViewErrorFields.Length);

        weaponDef.PhysCollmap = PhysicsReader.ReadPhysCollmapPointer(ref context);
        weaponDef.PhysicsFieldsA = ReadInt32Array(ref context, weaponDef.PhysicsFieldsA.Length);
        weaponDef.PhysicsFieldsB = ReadInt32Array(ref context, weaponDef.PhysicsFieldsB.Length);
        weaponDef.PhysicsFieldsC = ReadInt32Array(ref context, weaponDef.PhysicsFieldsC.Length);
        weaponDef.PhysicsFieldsD = ReadInt32Array(ref context, weaponDef.PhysicsFieldsD.Length);
        weaponDef.ProjectileModel = XModelReader.ReadXModelPointer(ref context);
        weaponDef.ProjectileModelField = context.ReadInt32();
        for (var i = 0; i < weaponDef.ProjectileEffects.Length; i++)
            weaponDef.ProjectileEffects[i] = FxReader.ReadFxPointer(ref context);
        for (var i = 0; i < weaponDef.ProjectileSoundAliases.Length; i++)
            weaponDef.ProjectileSoundAliases[i] = GenericReader.ReadStringPointer(ref context);
        weaponDef.ProjectileFieldsA = ReadInt32Array(ref context, weaponDef.ProjectileFieldsA.Length);
        weaponDef.ParallelBounce = ReadFloatArrayPointer(ref context, SurfaceCount);
        weaponDef.PerpendicularBounce = ReadFloatArrayPointer(ref context, SurfaceCount);
        for (var i = 0; i < weaponDef.ImpactEffects.Length; i++)
            weaponDef.ImpactEffects[i] = FxReader.ReadFxPointer(ref context);
        weaponDef.ImpactFieldsA = ReadInt32Array(ref context, weaponDef.ImpactFieldsA.Length);
        weaponDef.ImpactFieldB = context.ReadInt32();
        weaponDef.ImpactFieldsC = ReadInt32Array(ref context, weaponDef.ImpactFieldsC.Length);
        weaponDef.ViewShellEjectEffect = FxReader.ReadFxPointer(ref context);
        weaponDef.ShellEjectSound = GenericReader.ReadStringPointer(ref context);
        weaponDef.ShellEjectFields = ReadInt32Array(ref context, weaponDef.ShellEjectFields.Length);
        weaponDef.AdsHipGunKickAiDistanceFields = ReadInt32Array(ref context, weaponDef.AdsHipGunKickAiDistanceFields.Length);

        weaponDef.AccuracyGraphName0 = GenericReader.ReadStringPointer(ref context);
        weaponDef.AccuracyGraphName1 = GenericReader.ReadStringPointer(ref context);
        weaponDef.accuracyGraphKnots = context.ReadPointer<Vec2[]>();
        weaponDef.originalAccuracyGraphKnots = context.ReadPointer<Vec2[]>();
        weaponDef.accuracyGraphKnotCount = context.ReadUInt16();
        weaponDef.originalAccuracyGraphKnotCount = context.ReadUInt16();
        ResolveVec2ArrayPointer(ref context, weaponDef.accuracyGraphKnots, weaponDef.accuracyGraphKnotCount);
        ResolveVec2ArrayPointer(ref context, weaponDef.originalAccuracyGraphKnots, weaponDef.originalAccuracyGraphKnotCount);

        weaponDef.AccuracyGraphField = context.ReadInt32();
        weaponDef.LeftArc = context.ReadFloat();
        weaponDef.RightArc = context.ReadFloat();
        weaponDef.TopArc = context.ReadFloat();
        weaponDef.BottomArc = context.ReadFloat();
        weaponDef.Accuracy = context.ReadFloat();
        weaponDef.AiSpread = context.ReadFloat();
        weaponDef.PlayerSpread = context.ReadFloat();
        weaponDef.MinTurnSpeed = ReadFloatArray(ref context, weaponDef.MinTurnSpeed.Length);
        weaponDef.MaxTurnSpeed = ReadFloatArray(ref context, weaponDef.MaxTurnSpeed.Length);
        weaponDef.PitchConvergenceTime = context.ReadFloat();
        weaponDef.YawConvergenceTime = context.ReadFloat();
        weaponDef.SuppressTime = context.ReadFloat();
        weaponDef.MaxRange = context.ReadFloat();
        weaponDef.AnimHorizontalRotateInc = context.ReadFloat();
        weaponDef.PlayerPositionDist = context.ReadFloat();
        weaponDef.UseHintString = GenericReader.ReadStringPointer(ref context);
        weaponDef.DropHintString = GenericReader.ReadStringPointer(ref context);
        weaponDef.HintFieldsA = ReadInt32Array(ref context, weaponDef.HintFieldsA.Length);
        weaponDef.HintFieldsB = ReadInt32Array(ref context, weaponDef.HintFieldsB.Length);
        weaponDef.ScriptName = GenericReader.ReadStringPointer(ref context);
        weaponDef.ScriptFieldsA = ReadInt32Array(ref context, weaponDef.ScriptFieldsA.Length);
        weaponDef.ScriptFieldsB = ReadInt32Array(ref context, weaponDef.ScriptFieldsB.Length);
        weaponDef.HitLocationField = context.ReadInt32();
        weaponDef.LocationDamageMultipliers = ReadFloatArrayPointer(ref context, HitLocationCount);
        weaponDef.FireRumble = GenericReader.ReadStringPointer(ref context);
        weaponDef.MeleeImpactRumble = GenericReader.ReadStringPointer(ref context);
        weaponDef.Tracer = TracerReader.ReadTracerPointer(ref context);

        weaponDef.TracerFields = ReadInt32Array(ref context, weaponDef.TracerFields.Length);
        weaponDef.TurretOverheatSound = GenericReader.ReadStringPointer(ref context);
        weaponDef.TurretOverheatEffect = FxReader.ReadFxPointer(ref context);
        weaponDef.TurretBarrelSpinRumble = GenericReader.ReadStringPointer(ref context);
        weaponDef.TurretFields = ReadInt32Array(ref context, weaponDef.TurretFields.Length);
        weaponDef.TurretBarrelSpinMaxSnd = GenericReader.ReadStringPointer(ref context);
        for (var i = 0; i < 4; i++)
            weaponDef.TurretBarrelSpinUpSnd[i] = GenericReader.ReadStringPointer(ref context);
        for (var i = 0; i < 4; i++)
            weaponDef.TurretBarrelSpinDownSnd[i] = GenericReader.ReadStringPointer(ref context);
        weaponDef.MissileConeSoundAlias = GenericReader.ReadStringPointer(ref context);
        weaponDef.MissileConeSoundAliasAtBase = GenericReader.ReadStringPointer(ref context);
        weaponDef.MissileConeSoundRadiusAtTop = context.ReadFloat();
        weaponDef.MissileConeSoundRadiusAtBase = context.ReadFloat();
        weaponDef.MissileConeSoundHeight = context.ReadFloat();
        weaponDef.MissileConeSoundOriginOffset = context.ReadFloat();
        weaponDef.MissileConeSoundVolumescaleAtCore = context.ReadFloat();
        weaponDef.MissileConeSoundVolumescaleAtEdge = context.ReadFloat();
        weaponDef.MissileConeSoundVolumescaleCoreSize = context.ReadFloat();
        weaponDef.MissileConeSoundPitchAtTop = context.ReadFloat();
        weaponDef.MissileConeSoundPitchAtBottom = context.ReadFloat();
        weaponDef.MissileConeSoundPitchTopSize = context.ReadFloat();
        weaponDef.MissileConeSoundPitchBottomSize = context.ReadFloat();
        weaponDef.MissileConeSoundCrossfadeTopSize = context.ReadFloat();
        weaponDef.MissileConeSoundCrossfadeBottomSize = context.ReadFloat();
        weaponDef.SharedAmmo = context.ReadBool();
        weaponDef.LockonSupported = context.ReadBool();
        weaponDef.RequireLockonToFire = context.ReadBool();
        weaponDef.BigExplosion = context.ReadBool();
        weaponDef.BooleanFlags = ReadWeaponBooleanFlags(ref context);
        EnsureFixedSize(context.Position - start, WeaponDefSize, "WeaponDef");
        return weaponDef;
    }

    private static WeaponBooleanFlags ReadWeaponBooleanFlags(ref ZoneReadContext context)
    {
        return new WeaponBooleanFlags
        {
            NoAdsWhenMagEmpty = context.ReadBool(), AvoidDropCleanup = context.ReadBool(), InheritsPerks = context.ReadBool(),
            CrosshairColorChange = context.ReadBool(), RifleBullet = context.ReadBool(), ArmorPiercing = context.ReadBool(),
            BoltAction = context.ReadBool(), AimDownSight = context.ReadBool(), RechamberWhileAds = context.ReadBool(),
            BulletExplosiveDamage = context.ReadBool(), CookOffHold = context.ReadBool(), ClipOnly = context.ReadBool(),
            NoAmmoPickup = context.ReadBool(), AdsFireOnly = context.ReadBool(), CancelAutoHolsterWhenEmpty = context.ReadBool(),
            DisableSwitchToWhenEmpty = context.ReadBool(), SuppressAmmoReserveDisplay = context.ReadBool(),
            LaserSightDuringNightvision = context.ReadBool(), MarkableViewmodel = context.ReadBool(), NoDualWield = context.ReadBool(),
            FlipKillIcon = context.ReadBool(), NoPartialReload = context.ReadBool(), SegmentedReload = context.ReadBool(),
            BlocksProne = context.ReadBool(), Silenced = context.ReadBool(), IsRollingGrenade = context.ReadBool(),
            ProjExplosionEffectForceNormalUp = context.ReadBool(), ProjImpactExplode = context.ReadBool(), StickToPlayers = context.ReadBool(),
            HasDetonator = context.ReadBool(), DisableFiring = context.ReadBool(), TimedDetonation = context.ReadBool(),
            Rotate = context.ReadBool(), HoldButtonToThrow = context.ReadBool(), FreezeMovementWhenFiring = context.ReadBool(),
            ThermalScope = context.ReadBool(), AltModeSameWeapon = context.ReadBool(), TurretBarrelSpinEnabled = context.ReadBool(),
            MissileConeSoundEnabled = context.ReadBool(), MissileConeSoundPitchshiftEnabled = context.ReadBool(),
            MissileConeSoundCrossfadeEnabled = context.ReadBool(), OffhandHoldIsCancelable = context.ReadBool(),
            Ps3TailFlag0 = context.ReadByte(), Ps3TailFlag1 = context.ReadByte(),
        };
    }

    private static ZonePointer<ushort[]> ReadUShortArrayPointer(ref ZoneReadContext context, int count)
    {
        var pointer = context.ReadPointer<ushort[]>();
        context.ResolveInlinePointer(pointer, (ref ZoneReadContext pc, ZonePointer<ushort[]> p) =>
        {
            var values = new ushort[Math.Max(0, count)];
            for (var i = 0; i < values.Length; i++)
                values[i] = pc.ReadUInt16();
            p.SetResult(values);
        });
        return pointer;
    }

    private static ZonePointer<float[]> ReadFloatArrayPointer(ref ZoneReadContext context, int count)
    {
        var pointer = context.ReadPointer<float[]>();
        context.ResolveInlinePointer(pointer, (ref ZoneReadContext pc, ZonePointer<float[]> p) =>
        {
            var values = new float[Math.Max(0, count)];
            for (var i = 0; i < values.Length; i++)
                values[i] = pc.ReadFloat();
            p.SetResult(values);
        });
        return pointer;
    }

    private static ZonePointer<Vec2[]> ReadVec2ArrayPointer(ref ZoneReadContext context, int count)
    {
        var pointer = context.ReadPointer<Vec2[]>();
        ResolveVec2ArrayPointer(ref context, pointer, count);
        return pointer;
    }

    private static void ResolveVec2ArrayPointer(ref ZoneReadContext context, ZonePointer<Vec2[]> pointer, int count)
    {
        context.ResolveInlinePointer(pointer, (ref ZoneReadContext pc, ZonePointer<Vec2[]> p) =>
        {
            var values = new Vec2[Math.Max(0, count)];
            for (var i = 0; i < values.Length; i++)
                values[i] = new Vec2 { a = pc.ReadFloat(), b = pc.ReadFloat() };
            p.SetResult(values);
        });
    }

    private static int[] ReadInt32Array(ref ZoneReadContext context, int count)
    {
        var values = new int[Math.Max(0, count)];
        for (var i = 0; i < values.Length; i++)
            values[i] = context.ReadInt32();
        return values;
    }

    private static float[] ReadFloatArray(ref ZoneReadContext context, int count)
    {
        var values = new float[Math.Max(0, count)];
        for (var i = 0; i < values.Length; i++)
            values[i] = context.ReadFloat();
        return values;
    }

    private static void EnsureFixedSize(int read, int expectedSize, string typeName)
    {
        if (read != expectedSize)
            throw new InvalidDataException($"{typeName} read 0x{read:X} bytes; expected 0x{expectedSize:X}.");
    }
}
