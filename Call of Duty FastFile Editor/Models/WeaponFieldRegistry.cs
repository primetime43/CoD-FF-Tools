using System.Collections.Generic;

namespace Call_of_Duty_FastFile_Editor.Models
{
    /// <summary>
    /// Static registry of all weapon field definitions.
    /// Contains metadata for 50+ weapon fields organized by category.
    /// Offsets are verified for WaW (CoD5) with placeholders for CoD4/MW2.
    /// </summary>
    public static class WeaponFieldRegistry
    {
        /// <summary>
        /// All registered weapon field definitions.
        /// </summary>
        public static IReadOnlyList<WeaponFieldDefinition> AllFields => _allFields;

        private static readonly List<WeaponFieldDefinition> _allFields = new()
        {
            // ===== GENERAL CATEGORY =====
            new WeaponFieldDefinition
            {
                InternalName = "weapType",
                DisplayName = "Weapon Type",
                OffsetWaW = 0x144,
                OffsetCoD4 = 0x144,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Enum,
                EnumType = typeof(WeaponType),
                Category = WeaponFieldCategory.General,
                Tooltip = "Primary weapon classification (Bullet, Grenade, Projectile, etc.)"
            },
            new WeaponFieldDefinition
            {
                InternalName = "weapClass",
                DisplayName = "Weapon Class",
                OffsetWaW = 0x148,
                OffsetCoD4 = 0x148,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Enum,
                EnumType = typeof(WeaponClass),
                Category = WeaponFieldCategory.General,
                Tooltip = "Weapon class for UI/gameplay grouping (Rifle, SMG, Pistol, etc.)"
            },
            new WeaponFieldDefinition
            {
                InternalName = "penetrateType",
                DisplayName = "Penetration Type",
                OffsetWaW = 0x14C,
                OffsetCoD4 = 0x14C,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Enum,
                EnumType = typeof(PenetrateType),
                Category = WeaponFieldCategory.General,
                Tooltip = "How much the weapon can shoot through surfaces (None, Small, Medium, Large)"
            },
            new WeaponFieldDefinition
            {
                InternalName = "impactType",
                DisplayName = "Impact Type",
                OffsetWaW = 0x150,
                OffsetCoD4 = 0x150,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Enum,
                EnumType = typeof(ImpactType),
                Category = WeaponFieldCategory.General,
                Tooltip = "Visual/audio effect when bullets hit surfaces"
            },
            new WeaponFieldDefinition
            {
                InternalName = "inventoryType",
                DisplayName = "Inventory Type",
                OffsetWaW = 0x154,
                OffsetCoD4 = 0x154,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Enum,
                EnumType = typeof(WeaponInventoryType),
                Category = WeaponFieldCategory.General,
                Tooltip = "How the weapon is stored in inventory (Primary, Offhand, Item, AltMode)"
            },
            new WeaponFieldDefinition
            {
                InternalName = "fireType",
                DisplayName = "Fire Type",
                OffsetWaW = 0x158,
                OffsetCoD4 = 0x158,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Enum,
                EnumType = typeof(WeaponFireType),
                Category = WeaponFieldCategory.General,
                Tooltip = "Firing mode (Full Auto, Semi-Auto, Burst2, Burst3, etc.)"
            },
            new WeaponFieldDefinition
            {
                InternalName = "twoHanded",
                DisplayName = "Two Handed",
                OffsetWaW = 0x16C,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Bool,
                Category = WeaponFieldCategory.General,
                Tooltip = "Whether the weapon requires two hands"
            },
            new WeaponFieldDefinition
            {
                InternalName = "rifleBullet",
                DisplayName = "Rifle Bullet",
                OffsetWaW = 0x6EC,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Bool,
                Category = WeaponFieldCategory.General,
                Tooltip = "Whether the weapon uses rifle-type bullets (affects penetration)"
            },
            new WeaponFieldDefinition
            {
                InternalName = "armorPiercing",
                DisplayName = "Armor Piercing",
                OffsetWaW = 0x6F0,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Bool,
                Category = WeaponFieldCategory.General,
                Tooltip = "Whether the weapon has armor-piercing rounds"
            },
            new WeaponFieldDefinition
            {
                InternalName = "boltAction",
                DisplayName = "Bolt Action",
                OffsetWaW = 0x6F4,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Bool,
                Category = WeaponFieldCategory.General,
                Tooltip = "Whether the weapon is bolt-action (requires rechamber between shots)"
            },
            new WeaponFieldDefinition
            {
                InternalName = "aimDownSight",
                DisplayName = "Aim Down Sight",
                OffsetWaW = 0x6F8,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Bool,
                Category = WeaponFieldCategory.General,
                Tooltip = "Whether the weapon supports ADS (aim down sight)"
            },
            new WeaponFieldDefinition
            {
                InternalName = "silenced",
                DisplayName = "Silenced",
                OffsetWaW = 0x788,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Bool,
                Category = WeaponFieldCategory.General,
                Tooltip = "Whether the weapon is silenced (affects sound and radar)"
            },

            // ===== AMMUNITION CATEGORY =====
            new WeaponFieldDefinition
            {
                InternalName = "iMaxAmmo",
                DisplayName = "Max Ammo",
                OffsetWaW = 0x404,
                OffsetCoD4 = 0x404,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Int32,
                Category = WeaponFieldCategory.Ammunition,
                Tooltip = "Maximum reserve ammunition",
                MinValue = 0,
                MaxValue = 999
            },
            new WeaponFieldDefinition
            {
                InternalName = "iClipSize",
                DisplayName = "Clip Size",
                OffsetWaW = 0x408,
                OffsetCoD4 = 0x408,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Int32,
                Category = WeaponFieldCategory.Ammunition,
                Tooltip = "Magazine capacity",
                MinValue = 0,
                MaxValue = 500
            },
            new WeaponFieldDefinition
            {
                InternalName = "iStartAmmo",
                DisplayName = "Start Ammo",
                OffsetWaW = 0x400,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Int32,
                Category = WeaponFieldCategory.Ammunition,
                Tooltip = "Starting reserve ammunition",
                MinValue = 0,
                MaxValue = 999
            },
            new WeaponFieldDefinition
            {
                InternalName = "iDropAmmoMin",
                DisplayName = "Drop Ammo Min",
                OffsetWaW = 0x40C,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Int32,
                Category = WeaponFieldCategory.Ammunition,
                Tooltip = "Minimum ammo dropped when weapon is picked up",
                MinValue = 0,
                MaxValue = 500
            },
            new WeaponFieldDefinition
            {
                InternalName = "iDropAmmoMax",
                DisplayName = "Drop Ammo Max",
                OffsetWaW = 0x410,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Int32,
                Category = WeaponFieldCategory.Ammunition,
                Tooltip = "Maximum ammo dropped when weapon is picked up",
                MinValue = 0,
                MaxValue = 500
            },
            new WeaponFieldDefinition
            {
                InternalName = "iShotCount",
                DisplayName = "Shot Count",
                OffsetWaW = 0x414,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Int32,
                Category = WeaponFieldCategory.Ammunition,
                Tooltip = "Number of projectiles per shot (1 for normal, multiple for shotguns)",
                MinValue = 1,
                MaxValue = 20
            },
            new WeaponFieldDefinition
            {
                InternalName = "iReloadAmmoAdd",
                DisplayName = "Reload Ammo Add",
                OffsetWaW = 0x418,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Int32,
                Category = WeaponFieldCategory.Ammunition,
                Tooltip = "Ammo added during reload",
                MinValue = 0,
                MaxValue = 500
            },
            new WeaponFieldDefinition
            {
                InternalName = "iReloadStartAdd",
                DisplayName = "Reload Start Add",
                OffsetWaW = 0x41C,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Int32,
                Category = WeaponFieldCategory.Ammunition,
                Tooltip = "Ammo added at start of reload animation",
                MinValue = 0,
                MaxValue = 500
            },

            // ===== DAMAGE CATEGORY =====
            new WeaponFieldDefinition
            {
                InternalName = "iDamage",
                DisplayName = "Damage",
                OffsetWaW = 0x3EC,
                OffsetCoD4 = 0x3EC,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Int32,
                Category = WeaponFieldCategory.Damage,
                Tooltip = "Base damage per bullet at close range",
                MinValue = 0,
                MaxValue = 500
            },
            new WeaponFieldDefinition
            {
                InternalName = "iMinDamage",
                DisplayName = "Min Damage",
                OffsetWaW = 0x3F0,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Int32,
                Category = WeaponFieldCategory.Damage,
                Tooltip = "Minimum damage per bullet at max range",
                MinValue = 0,
                MaxValue = 500
            },
            new WeaponFieldDefinition
            {
                InternalName = "iMaxDamageRange",
                DisplayName = "Max Damage Range",
                OffsetWaW = 0x3F4,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Int32,
                Category = WeaponFieldCategory.Damage,
                Tooltip = "Distance at which max damage is dealt (inches)",
                MinValue = 0,
                MaxValue = 10000
            },
            new WeaponFieldDefinition
            {
                InternalName = "iMinDamageRange",
                DisplayName = "Min Damage Range",
                OffsetWaW = 0x3F8,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Int32,
                Category = WeaponFieldCategory.Damage,
                Tooltip = "Distance at which min damage is dealt (inches)",
                MinValue = 0,
                MaxValue = 10000
            },
            new WeaponFieldDefinition
            {
                InternalName = "iMeleeDamage",
                DisplayName = "Melee Damage",
                OffsetWaW = 0x3FC,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Int32,
                Category = WeaponFieldCategory.Damage,
                Tooltip = "Damage dealt by melee attack",
                MinValue = 0,
                MaxValue = 500
            },
            new WeaponFieldDefinition
            {
                InternalName = "fPlayerDamage",
                DisplayName = "Player Damage",
                OffsetWaW = 0x784,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.Damage,
                Tooltip = "Damage multiplier when AI shoots player",
                MinValue = 0.0,
                MaxValue = 10.0,
                DecimalPlaces = 2
            },
            new WeaponFieldDefinition
            {
                InternalName = "fLocNone",
                DisplayName = "Loc Multiplier: None",
                OffsetWaW = 0x760,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.Damage,
                Tooltip = "Damage multiplier for unspecified hit location",
                MinValue = 0.0,
                MaxValue = 10.0,
                DecimalPlaces = 2
            },
            new WeaponFieldDefinition
            {
                InternalName = "fLocHead",
                DisplayName = "Loc Multiplier: Head",
                OffsetWaW = 0x764,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.Damage,
                Tooltip = "Damage multiplier for headshots",
                MinValue = 0.0,
                MaxValue = 10.0,
                DecimalPlaces = 2
            },
            new WeaponFieldDefinition
            {
                InternalName = "fLocTorsoUpper",
                DisplayName = "Loc Multiplier: Upper Torso",
                OffsetWaW = 0x768,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.Damage,
                Tooltip = "Damage multiplier for upper torso hits",
                MinValue = 0.0,
                MaxValue = 10.0,
                DecimalPlaces = 2
            },
            new WeaponFieldDefinition
            {
                InternalName = "fLocTorsoLower",
                DisplayName = "Loc Multiplier: Lower Torso",
                OffsetWaW = 0x76C,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.Damage,
                Tooltip = "Damage multiplier for lower torso hits",
                MinValue = 0.0,
                MaxValue = 10.0,
                DecimalPlaces = 2
            },
            new WeaponFieldDefinition
            {
                InternalName = "fLocRightArmUpper",
                DisplayName = "Loc Multiplier: Right Arm Upper",
                OffsetWaW = 0x770,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.Damage,
                Tooltip = "Damage multiplier for right upper arm hits",
                MinValue = 0.0,
                MaxValue = 10.0,
                DecimalPlaces = 2
            },
            new WeaponFieldDefinition
            {
                InternalName = "fLocLeftArmUpper",
                DisplayName = "Loc Multiplier: Left Arm Upper",
                OffsetWaW = 0x774,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.Damage,
                Tooltip = "Damage multiplier for left upper arm hits",
                MinValue = 0.0,
                MaxValue = 10.0,
                DecimalPlaces = 2
            },

            // ===== STATE TIMERS CATEGORY =====
            new WeaponFieldDefinition
            {
                InternalName = "fFireTime",
                DisplayName = "Fire Time",
                OffsetWaW = 0x420,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.StateTimers,
                Tooltip = "Time between shots in seconds (lower = faster fire rate)",
                MinValue = 0.0,
                MaxValue = 5.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fFireDelay",
                DisplayName = "Fire Delay",
                OffsetWaW = 0x424,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.StateTimers,
                Tooltip = "Delay before first shot (used for charge weapons)",
                MinValue = 0.0,
                MaxValue = 5.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fMeleeTime",
                DisplayName = "Melee Time",
                OffsetWaW = 0x438,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.StateTimers,
                Tooltip = "Duration of melee attack animation",
                MinValue = 0.0,
                MaxValue = 5.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fReloadTime",
                DisplayName = "Reload Time",
                OffsetWaW = 0x464,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.StateTimers,
                Tooltip = "Time to reload with ammo remaining",
                MinValue = 0.0,
                MaxValue = 10.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fReloadEmptyTime",
                DisplayName = "Reload Empty Time",
                OffsetWaW = 0x468,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.StateTimers,
                Tooltip = "Time to reload from empty magazine",
                MinValue = 0.0,
                MaxValue = 10.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fReloadAddTime",
                DisplayName = "Reload Add Time",
                OffsetWaW = 0x46C,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.StateTimers,
                Tooltip = "Time into reload when ammo is added",
                MinValue = 0.0,
                MaxValue = 10.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fRechamberTime",
                DisplayName = "Rechamber Time",
                OffsetWaW = 0x43C,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.StateTimers,
                Tooltip = "Time to rechamber between shots (bolt-action weapons)",
                MinValue = 0.0,
                MaxValue = 5.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fRaiseTime",
                DisplayName = "Raise Time",
                OffsetWaW = 0x44C,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.StateTimers,
                Tooltip = "Time to bring weapon up after switching",
                MinValue = 0.0,
                MaxValue = 5.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fDropTime",
                DisplayName = "Drop Time",
                OffsetWaW = 0x458,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.StateTimers,
                Tooltip = "Time to lower weapon when switching",
                MinValue = 0.0,
                MaxValue = 5.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fSprintInTime",
                DisplayName = "Sprint In Time",
                OffsetWaW = 0x48C,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.StateTimers,
                Tooltip = "Time to transition into sprint",
                MinValue = 0.0,
                MaxValue = 2.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fSprintOutTime",
                DisplayName = "Sprint Out Time",
                OffsetWaW = 0x490,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.StateTimers,
                Tooltip = "Time to transition out of sprint",
                MinValue = 0.0,
                MaxValue = 2.0,
                DecimalPlaces = 4
            },

            // ===== ADS CATEGORY =====
            new WeaponFieldDefinition
            {
                InternalName = "fAdsZoomFov",
                DisplayName = "ADS Zoom FOV",
                OffsetWaW = 0x4A8,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.ADS,
                Tooltip = "Field of view when aiming down sights (lower = more zoom)",
                MinValue = 10.0,
                MaxValue = 90.0,
                DecimalPlaces = 2
            },
            new WeaponFieldDefinition
            {
                InternalName = "fAdsTransInTime",
                DisplayName = "ADS Trans In Time",
                OffsetWaW = 0x4B0,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.ADS,
                Tooltip = "Time to aim down sights",
                MinValue = 0.0,
                MaxValue = 2.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fAdsTransOutTime",
                DisplayName = "ADS Trans Out Time",
                OffsetWaW = 0x4B4,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.ADS,
                Tooltip = "Time to exit aim down sights",
                MinValue = 0.0,
                MaxValue = 2.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fAdsSpread",
                DisplayName = "ADS Spread",
                OffsetWaW = 0x580,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.ADS,
                Tooltip = "Bullet spread when aiming down sights",
                MinValue = 0.0,
                MaxValue = 10.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fAdsBobFactor",
                DisplayName = "ADS Bob Factor",
                OffsetWaW = 0x4C8,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.ADS,
                Tooltip = "Weapon bob amount when aiming (lower = steadier)",
                MinValue = 0.0,
                MaxValue = 2.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fAdsViewBobMult",
                DisplayName = "ADS View Bob Mult",
                OffsetWaW = 0x4CC,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.ADS,
                Tooltip = "View bob multiplier when aiming",
                MinValue = 0.0,
                MaxValue = 2.0,
                DecimalPlaces = 4
            },

            // ===== SPREAD CATEGORY =====
            new WeaponFieldDefinition
            {
                InternalName = "fHipSpreadStandMin",
                DisplayName = "Hip Spread Stand Min",
                OffsetWaW = 0x568,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.Spread,
                Tooltip = "Minimum hip-fire spread when standing still",
                MinValue = 0.0,
                MaxValue = 20.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fHipSpreadMax",
                DisplayName = "Hip Spread Max",
                OffsetWaW = 0x574,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.Spread,
                Tooltip = "Maximum hip-fire spread",
                MinValue = 0.0,
                MaxValue = 30.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fHipSpreadDecayRate",
                DisplayName = "Hip Spread Decay Rate",
                OffsetWaW = 0x578,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.Spread,
                Tooltip = "How fast hip spread recovers (higher = faster)",
                MinValue = 0.0,
                MaxValue = 50.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fHipSpreadDuckedMin",
                DisplayName = "Hip Spread Ducked Min",
                OffsetWaW = 0x56C,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.Spread,
                Tooltip = "Minimum hip-fire spread when crouching",
                MinValue = 0.0,
                MaxValue = 20.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fHipSpreadProneMin",
                DisplayName = "Hip Spread Prone Min",
                OffsetWaW = 0x570,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.Spread,
                Tooltip = "Minimum hip-fire spread when prone",
                MinValue = 0.0,
                MaxValue = 20.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fHipSpreadFireAdd",
                DisplayName = "Hip Spread Fire Add",
                OffsetWaW = 0x57C,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.Spread,
                Tooltip = "Spread added per shot",
                MinValue = 0.0,
                MaxValue = 10.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fHipSpreadMoveAdd",
                DisplayName = "Hip Spread Move Add",
                OffsetWaW = 0x584,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.Spread,
                Tooltip = "Spread added when moving",
                MinValue = 0.0,
                MaxValue = 10.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fHipSpreadTurnAdd",
                DisplayName = "Hip Spread Turn Add",
                OffsetWaW = 0x588,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.Spread,
                Tooltip = "Spread added when turning",
                MinValue = 0.0,
                MaxValue = 10.0,
                DecimalPlaces = 4
            },

            // ===== KICK CATEGORY =====
            new WeaponFieldDefinition
            {
                InternalName = "fHipGunKickReducedKickBullets",
                DisplayName = "Hip Reduced Kick Bullets",
                OffsetWaW = 0x5C0,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Int32,
                Category = WeaponFieldCategory.Kick,
                Tooltip = "Number of bullets before full kick is applied",
                MinValue = 0,
                MaxValue = 100
            },
            new WeaponFieldDefinition
            {
                InternalName = "fHipGunKickReducedKickPercent",
                DisplayName = "Hip Reduced Kick Percent",
                OffsetWaW = 0x5C4,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.Kick,
                Tooltip = "Kick percentage for first few bullets (0-1)",
                MinValue = 0.0,
                MaxValue = 1.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fHipGunKickPitchMin",
                DisplayName = "Hip Gun Kick Pitch Min",
                OffsetWaW = 0x5C8,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.Kick,
                Tooltip = "Minimum vertical kick (negative = down, positive = up)",
                MinValue = -10.0,
                MaxValue = 10.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fHipGunKickPitchMax",
                DisplayName = "Hip Gun Kick Pitch Max",
                OffsetWaW = 0x5CC,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.Kick,
                Tooltip = "Maximum vertical kick",
                MinValue = -10.0,
                MaxValue = 10.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fHipGunKickYawMin",
                DisplayName = "Hip Gun Kick Yaw Min",
                OffsetWaW = 0x5D0,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.Kick,
                Tooltip = "Minimum horizontal kick (negative = left, positive = right)",
                MinValue = -10.0,
                MaxValue = 10.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fHipGunKickYawMax",
                DisplayName = "Hip Gun Kick Yaw Max",
                OffsetWaW = 0x5D4,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.Kick,
                Tooltip = "Maximum horizontal kick",
                MinValue = -10.0,
                MaxValue = 10.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fAdsGunKickPitchMin",
                DisplayName = "ADS Gun Kick Pitch Min",
                OffsetWaW = 0x5F0,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.Kick,
                Tooltip = "Minimum vertical kick when ADS",
                MinValue = -10.0,
                MaxValue = 10.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fAdsGunKickPitchMax",
                DisplayName = "ADS Gun Kick Pitch Max",
                OffsetWaW = 0x5F4,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.Kick,
                Tooltip = "Maximum vertical kick when ADS",
                MinValue = -10.0,
                MaxValue = 10.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fAdsGunKickYawMin",
                DisplayName = "ADS Gun Kick Yaw Min",
                OffsetWaW = 0x5F8,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.Kick,
                Tooltip = "Minimum horizontal kick when ADS",
                MinValue = -10.0,
                MaxValue = 10.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fAdsGunKickYawMax",
                DisplayName = "ADS Gun Kick Yaw Max",
                OffsetWaW = 0x5FC,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.Kick,
                Tooltip = "Maximum horizontal kick when ADS",
                MinValue = -10.0,
                MaxValue = 10.0,
                DecimalPlaces = 4
            },

            // ===== SWAY CATEGORY =====
            new WeaponFieldDefinition
            {
                InternalName = "fSwayMaxAngle",
                DisplayName = "Sway Max Angle",
                OffsetWaW = 0x638,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.Sway,
                Tooltip = "Maximum angle the weapon can sway",
                MinValue = 0.0,
                MaxValue = 30.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fSwayLerpSpeed",
                DisplayName = "Sway Lerp Speed",
                OffsetWaW = 0x63C,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.Sway,
                Tooltip = "Speed of sway interpolation",
                MinValue = 0.0,
                MaxValue = 20.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fSwayPitchScale",
                DisplayName = "Sway Pitch Scale",
                OffsetWaW = 0x640,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.Sway,
                Tooltip = "Vertical sway scale",
                MinValue = 0.0,
                MaxValue = 5.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fSwayYawScale",
                DisplayName = "Sway Yaw Scale",
                OffsetWaW = 0x644,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.Sway,
                Tooltip = "Horizontal sway scale",
                MinValue = 0.0,
                MaxValue = 5.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fSwayHorizScale",
                DisplayName = "Sway Horiz Scale",
                OffsetWaW = 0x648,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.Sway,
                Tooltip = "Horizontal sway movement scale",
                MinValue = 0.0,
                MaxValue = 5.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fSwayVertScale",
                DisplayName = "Sway Vert Scale",
                OffsetWaW = 0x64C,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.Sway,
                Tooltip = "Vertical sway movement scale",
                MinValue = 0.0,
                MaxValue = 5.0,
                DecimalPlaces = 4
            },

            // ===== MOVEMENT CATEGORY =====
            new WeaponFieldDefinition
            {
                InternalName = "fMoveSpeedScale",
                DisplayName = "Move Speed Scale",
                OffsetWaW = 0x6E8,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.Movement,
                Tooltip = "Movement speed multiplier when holding weapon",
                MinValue = 0.0,
                MaxValue = 2.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fAdsMoveSpeedScale",
                DisplayName = "ADS Move Speed Scale",
                OffsetWaW = 0x6D4,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.Movement,
                Tooltip = "Movement speed multiplier when aiming down sights",
                MinValue = 0.0,
                MaxValue = 2.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fSprintDurationScale",
                DisplayName = "Sprint Duration Scale",
                OffsetWaW = 0x6E4,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.Movement,
                Tooltip = "Sprint stamina multiplier with this weapon",
                MinValue = 0.0,
                MaxValue = 5.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fGunMaxPitch",
                DisplayName = "Gun Max Pitch",
                OffsetWaW = 0x6A8,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.Movement,
                Tooltip = "Maximum pitch angle for gun model",
                MinValue = 0.0,
                MaxValue = 90.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fGunMaxYaw",
                DisplayName = "Gun Max Yaw",
                OffsetWaW = 0x6AC,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.Movement,
                Tooltip = "Maximum yaw angle for gun model",
                MinValue = 0.0,
                MaxValue = 90.0,
                DecimalPlaces = 4
            },

            // ===== AI CATEGORY =====
            new WeaponFieldDefinition
            {
                InternalName = "fAIFireTime",
                DisplayName = "AI Fire Time",
                OffsetWaW = 0x7B8,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.AI,
                Tooltip = "Time between AI shots",
                MinValue = 0.0,
                MaxValue = 10.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fAIBurstFireCooldown",
                DisplayName = "AI Burst Fire Cooldown",
                OffsetWaW = 0x7BC,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.AI,
                Tooltip = "AI cooldown between bursts",
                MinValue = 0.0,
                MaxValue = 10.0,
                DecimalPlaces = 4
            },
            new WeaponFieldDefinition
            {
                InternalName = "fAIMeleeDamage",
                DisplayName = "AI Melee Damage",
                OffsetWaW = 0x7C0,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.AI,
                Tooltip = "Melee damage when used by AI",
                MinValue = 0.0,
                MaxValue = 1000.0,
                DecimalPlaces = 2
            },
            new WeaponFieldDefinition
            {
                InternalName = "fAISuppressTime",
                DisplayName = "AI Suppress Time",
                OffsetWaW = 0x7C4,
                OffsetCoD4 = -1,
                OffsetMW2 = -1,
                FieldType = WeaponFieldType.Float,
                Category = WeaponFieldCategory.AI,
                Tooltip = "Time AI is suppressed when hit",
                MinValue = 0.0,
                MaxValue = 30.0,
                DecimalPlaces = 4
            }
        };

        /// <summary>
        /// Gets all fields for a specific category.
        /// </summary>
        public static IEnumerable<WeaponFieldDefinition> GetFieldsByCategory(WeaponFieldCategory category)
        {
            return _allFields.Where(f => f.Category == category);
        }

        /// <summary>
        /// Gets all fields that are available for the specified game.
        /// </summary>
        public static IEnumerable<WeaponFieldDefinition> GetFieldsForGame(string gameShortName)
        {
            return _allFields.Where(f => f.IsAvailableFor(gameShortName));
        }

        /// <summary>
        /// Searches fields by display name or internal name (case-insensitive).
        /// </summary>
        public static IEnumerable<WeaponFieldDefinition> Search(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return _allFields;

            string term = searchTerm.ToLowerInvariant();
            return _allFields.Where(f =>
                f.DisplayName.ToLowerInvariant().Contains(term) ||
                f.InternalName.ToLowerInvariant().Contains(term) ||
                f.Tooltip.ToLowerInvariant().Contains(term));
        }

        /// <summary>
        /// Gets a field by its internal name.
        /// </summary>
        public static WeaponFieldDefinition? GetByInternalName(string internalName)
        {
            return _allFields.FirstOrDefault(f =>
                f.InternalName.Equals(internalName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets the display-friendly name for a category.
        /// </summary>
        public static string GetCategoryDisplayName(WeaponFieldCategory category)
        {
            return category switch
            {
                WeaponFieldCategory.General => "General",
                WeaponFieldCategory.Ammunition => "Ammunition",
                WeaponFieldCategory.Damage => "Damage",
                WeaponFieldCategory.StateTimers => "State Timers",
                WeaponFieldCategory.ADS => "ADS",
                WeaponFieldCategory.Spread => "Spread",
                WeaponFieldCategory.Kick => "Kick",
                WeaponFieldCategory.Sway => "Sway",
                WeaponFieldCategory.Movement => "Movement",
                WeaponFieldCategory.AI => "AI",
                _ => category.ToString()
            };
        }
    }
}
