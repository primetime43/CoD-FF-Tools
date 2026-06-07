namespace Call_of_Duty_FastFile_Editor.Models
{
    /// <summary>
    /// Represents a Weapon asset from a zone file.
    ///
    /// WeaponDef structure for WaW/CoD5 (0x9AC bytes header on Xbox 360/PS3):
    /// The structure contains 400+ fields for weapon properties.
    ///
    /// Key fields extracted:
    /// - szInternalName: Internal weapon identifier
    /// - szDisplayName: Localized display name reference
    /// - weapType: Weapon type (bullet, grenade, projectile, etc.)
    /// - weapClass: Weapon class (rifle, SMG, pistol, etc.)
    /// - Various damage, timing, and behavior parameters
    /// </summary>
    public class WeaponAsset
    {
        /// <summary>
        /// Internal weapon name (e.g., "mp40_mp", "kar98k_scoped").
        /// </summary>
        public string InternalName { get; set; } = string.Empty;

        /// <summary>
        /// Display name reference (localized string key).
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Weapon type enum value.
        /// </summary>
        public WeaponType WeapType { get; set; }

        /// <summary>
        /// Weapon class enum value.
        /// </summary>
        public WeaponClass WeapClass { get; set; }

        /// <summary>
        /// Fire type (full auto, semi-auto, burst, etc.).
        /// </summary>
        public WeaponFireType FireType { get; set; }

        /// <summary>
        /// Penetrate type - how much the weapon can penetrate surfaces.
        /// </summary>
        public PenetrateType PenetrateType { get; set; }

        /// <summary>
        /// Impact type - the effect when bullets hit surfaces.
        /// </summary>
        public ImpactType ImpactType { get; set; }

        /// <summary>
        /// Inventory type - how the weapon is stored/carried.
        /// </summary>
        public WeaponInventoryType InventoryType { get; set; }

        /// <summary>
        /// Base damage value.
        /// </summary>
        public int Damage { get; set; }

        /// <summary>
        /// Minimum damage at max range.
        /// </summary>
        public int MinDamage { get; set; }

        /// <summary>
        /// Melee damage value.
        /// </summary>
        public int MeleeDamage { get; set; }

        /// <summary>
        /// Fire time in milliseconds.
        /// </summary>
        public int FireTime { get; set; }

        /// <summary>
        /// Reload time (add) in milliseconds.
        /// </summary>
        public int ReloadAddTime { get; set; }

        /// <summary>
        /// Reload time (empty) in milliseconds.
        /// </summary>
        public int ReloadEmptyAddTime { get; set; }

        /// <summary>
        /// Magazine size (clip size).
        /// </summary>
        public int ClipSize { get; set; }

        /// <summary>
        /// Maximum ammo in reserve.
        /// </summary>
        public int MaxAmmo { get; set; }

        /// <summary>
        /// ADS (aim down sight) transition time in milliseconds.
        /// </summary>
        public int AdsTransInTime { get; set; }

        /// <summary>
        /// ADS zoom field of view.
        /// </summary>
        public float AdsZoomFov { get; set; }

        /// <summary>
        /// Hipfire spread minimum.
        /// </summary>
        public float HipSpreadMin { get; set; }

        /// <summary>
        /// Hipfire spread maximum.
        /// </summary>
        public float HipSpreadMax { get; set; }

        /// <summary>
        /// Movement speed scale when holding weapon.
        /// </summary>
        public float MoveSpeedScale { get; set; }

        /// <summary>
        /// Start offset in the zone file.
        /// </summary>
        public int StartOffset { get; set; }

        /// <summary>
        /// End offset in the zone file.
        /// </summary>
        public int EndOffset { get; set; }

        /// <summary>
        /// Header size (0x9AC for WaW).
        /// </summary>
        public int HeaderSize { get; set; }

        /// <summary>
        /// Additional parsing information.
        /// </summary>
        public string AdditionalData { get; set; } = string.Empty;

        /// <summary>
        /// True when this weapon was read by the IW4 pointer-walk (MW2 PS3) rather than the
        /// WaW-tuned pattern parser. In that case the classic weapType/weapClass enums aren't
        /// recovered (they live in opaque field blocks), the rich structure is exposed via
        /// <see cref="DetailFields"/> instead, and the byte-offset editor must NOT be used (it
        /// would write WaW offsets into an IW4 layout and corrupt the zone).
        /// </summary>
        public bool IsStructuredView { get; set; }

        /// <summary>
        /// Ordered (label, value) pairs of the parsed weapon structure, for the read-only detail
        /// view. Populated for <see cref="IsStructuredView"/> weapons from the IW4 WeaponVariantDef
        /// + WeaponDef (clip/fire/ADS, arcs, ranges, accuracy, turn speeds, hint/script strings,
        /// boolean flags). A blank label denotes a section header.
        /// </summary>
        public List<(string Label, string Value)> DetailFields { get; set; } = new();

        // IW4 (structured-view) enum field names, decoded from the WeaponDef enum block + the variant
        // impactType using the authoritative IW4 enum value lists (weapType/weapClass from
        // OpenAssetTools). Shown in the Weapons grid for IsStructuredView weapons instead of the
        // WaW-flavoured enum columns (whose numeric values differ from IW4's). Empty when unknown.
        public string TypeName { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string FireTypeName { get; set; } = string.Empty;
        public string PenetrateName { get; set; } = string.Empty;
        public string ImpactName { get; set; } = string.Empty;
        public string InventoryName { get; set; } = string.Empty;

        /// <summary>
        /// Zone byte offset of the inner WeaponDef (for IW4 structured-view weapons). The variant's
        /// offset is <see cref="StartOffset"/>; this is where the WeaponDef scalar fields live
        /// (damage/ammo/enums). 0 when the WeaponDef wasn't resolved inline.
        /// </summary>
        public int WeaponDefOffset { get; set; }

        /// <summary>
        /// Gets a summary of the weapon properties.
        /// </summary>
        public string GetSummary()
        {
            if (IsStructuredView)
                return $"clip {ClipSize}, fire {FireTime}ms, adsFov {AdsZoomFov:0.#}";
            return $"{WeapClass} ({WeapType}), DMG: {Damage}-{MinDamage}, Clip: {ClipSize}, Fire: {FireType}";
        }
    }

    /// <summary>
    /// Weapon type enumeration (weapType_t).
    /// </summary>
    public enum WeaponType
    {
        Bullet = 0,
        Grenade = 1,
        Projectile = 2,
        Binoculars = 3,
        Gas = 4,
        Bomb = 5,
        Mine = 6,
        Num = 7
    }

    /// <summary>
    /// Weapon class enumeration (weapClass_t).
    /// </summary>
    public enum WeaponClass
    {
        Rifle = 0,
        MG = 1,
        SMG = 2,
        Spread = 3,      // Shotgun
        Pistol = 4,
        Grenade = 5,
        RocketLauncher = 6,
        Turret = 7,
        NonPlayer = 8,
        Gas = 9,
        Item = 10,
        Num = 11
    }

    /// <summary>
    /// Weapon fire type enumeration (weapFireType_t).
    /// </summary>
    public enum WeaponFireType
    {
        FullAuto = 0,
        SingleShot = 1,
        Burst2 = 2,
        Burst3 = 3,
        Burst4 = 4,
        Num = 5
    }

    /// <summary>
    /// Penetration type enumeration (PenetrateType).
    /// Determines how much the weapon can shoot through surfaces.
    /// </summary>
    public enum PenetrateType
    {
        None = 0,
        Small = 1,
        Medium = 2,
        Large = 3,
        Count = 4
    }

    /// <summary>
    /// Impact type enumeration (ImpactType).
    /// Determines the visual/audio effect when bullets hit surfaces.
    /// </summary>
    public enum ImpactType
    {
        None = 0,
        Bullet_Small = 1,
        Bullet_Large = 2,
        Bullet_AP = 3,
        Shotgun = 4,
        Grenade_Bounce = 5,
        Grenade_Explode = 6,
        Rifle_Explode = 7,
        Projectile_Dud = 8,
        Count = 9
    }

    /// <summary>
    /// Weapon inventory type enumeration (weapInventoryType_t).
    /// Determines how the weapon is stored in inventory.
    /// </summary>
    public enum WeaponInventoryType
    {
        Primary = 0,
        Offhand = 1,
        Item = 2,
        AltMode = 3,
        Count = 4
    }
}
