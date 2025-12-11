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
        /// Gets a summary of the weapon properties.
        /// </summary>
        public string GetSummary()
        {
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
        Melee = 7
    }

    /// <summary>
    /// Weapon class enumeration (weapClass_t).
    /// </summary>
    public enum WeaponClass
    {
        Rifle = 0,
        SMG = 1,
        MG = 2,
        Spread = 3,      // Shotgun
        Pistol = 4,
        Grenade = 5,
        RocketLauncher = 6,
        Turret = 7,
        ThrowingKnife = 8,
        NonPlayer = 9,
        Item = 10
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
        DoubleBarrel = 5,
        BoltAction = 6,
        PumpAction = 7
    }

    /// <summary>
    /// Penetration type enumeration (penetrateType_t).
    /// Determines how much the weapon can shoot through surfaces.
    /// </summary>
    public enum PenetrateType
    {
        None = 0,
        Small = 1,
        Medium = 2,
        Large = 3,
        /// <summary>Heavy penetration (LMGs, sniper rifles)</summary>
        Heavy = 4,
        /// <summary>Maximum penetration (anti-material)</summary>
        Max = 5,
        /// <summary>Rifle-class penetration</summary>
        Rifle = 6
    }

    /// <summary>
    /// Impact type enumeration (impactType_t).
    /// Determines the visual/audio effect when bullets hit surfaces.
    /// </summary>
    public enum ImpactType
    {
        None = 0,
        Bullet_Small = 1,
        Bullet_Large = 2,
        Shotgun = 3,
        Grenade = 4,
        Rocket = 5,
        Projectile = 6
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
        AltMode = 3
    }
}
