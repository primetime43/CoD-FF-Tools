namespace Call_of_Duty_FastFile_Editor.Models
{
    /// <summary>
    /// Defines the type of value stored in a weapon field.
    /// </summary>
    public enum WeaponFieldType
    {
        /// <summary>32-bit signed integer</summary>
        Int32,
        /// <summary>32-bit unsigned integer</summary>
        UInt32,
        /// <summary>16-bit signed integer</summary>
        Int16,
        /// <summary>16-bit unsigned integer</summary>
        UInt16,
        /// <summary>8-bit unsigned integer (byte)</summary>
        Byte,
        /// <summary>32-bit floating point</summary>
        Float,
        /// <summary>Boolean (stored as 32-bit integer, 0 or 1)</summary>
        Bool,
        /// <summary>Enumeration value (stored as 32-bit integer)</summary>
        Enum
    }

    /// <summary>
    /// Categories for organizing weapon fields in the editor UI.
    /// </summary>
    public enum WeaponFieldCategory
    {
        /// <summary>Type, Class, Penetration, Fire Type, Inventory, boolean flags</summary>
        General,
        /// <summary>Max Ammo, Clip Size, Start Ammo, Drop Min/Max, Shot Count</summary>
        Ammunition,
        /// <summary>Min/Max Damage, Damage Range, Melee, Location Multipliers</summary>
        Damage,
        /// <summary>Fire Time, Reload Times, Rechamber, Raise/Drop, Sprint</summary>
        StateTimers,
        /// <summary>Zoom FOV, Trans In/Out Time, Spread, Bob Factor</summary>
        ADS,
        /// <summary>Hip spread for Stand/Crouch/Prone, Fire/Move/Turn Add</summary>
        Spread,
        /// <summary>Gun Kick Pitch/Yaw, Speed, Decay for Hip and ADS</summary>
        Kick,
        /// <summary>Max Angle, Lerp Speed, Pitch/Yaw Scale</summary>
        Sway,
        /// <summary>Stance offsets/rotations, Move Speed Scale</summary>
        Movement,
        /// <summary>Fight Distance, Accuracy values</summary>
        AI
    }

    /// <summary>
    /// Defines metadata for a single weapon field, including offsets for different games.
    /// </summary>
    public class WeaponFieldDefinition
    {
        /// <summary>
        /// Internal field name used in code/game files (e.g., "iDamage", "fFireTime").
        /// </summary>
        public string InternalName { get; set; } = string.Empty;

        /// <summary>
        /// User-friendly display name shown in the editor (e.g., "Damage", "Fire Time").
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Offset within the weapon structure for World at War (CoD5).
        /// -1 indicates the field is not available for this game.
        /// </summary>
        public int OffsetWaW { get; set; } = -1;

        /// <summary>
        /// Offset within the weapon structure for CoD4.
        /// -1 indicates the field is not available for this game.
        /// </summary>
        public int OffsetCoD4 { get; set; } = -1;

        /// <summary>
        /// Offset within the weapon structure for MW2.
        /// -1 indicates the field is not available for this game.
        /// </summary>
        public int OffsetMW2 { get; set; } = -1;

        /// <summary>
        /// The data type of this field.
        /// </summary>
        public WeaponFieldType FieldType { get; set; } = WeaponFieldType.Int32;

        /// <summary>
        /// For enum fields, the .NET Type of the enum.
        /// </summary>
        public Type? EnumType { get; set; }

        /// <summary>
        /// Description/tooltip explaining what this field controls.
        /// </summary>
        public string Tooltip { get; set; } = string.Empty;

        /// <summary>
        /// Category for organizing fields in tabbed UI.
        /// </summary>
        public WeaponFieldCategory Category { get; set; } = WeaponFieldCategory.General;

        /// <summary>
        /// Whether this field should be read-only in the editor.
        /// </summary>
        public bool IsReadOnly { get; set; } = false;

        /// <summary>
        /// Minimum value for numeric fields. Null means no minimum.
        /// </summary>
        public double? MinValue { get; set; }

        /// <summary>
        /// Maximum value for numeric fields. Null means no maximum.
        /// </summary>
        public double? MaxValue { get; set; }

        /// <summary>
        /// For float fields, the number of decimal places to display.
        /// </summary>
        public int DecimalPlaces { get; set; } = 2;

        /// <summary>
        /// Gets the offset for the specified game.
        /// </summary>
        /// <param name="gameShortName">Game identifier: "COD4", "COD5", or "MW2"</param>
        /// <returns>The offset, or -1 if not available for this game.</returns>
        public int GetOffset(string gameShortName)
        {
            return gameShortName.ToUpperInvariant() switch
            {
                "COD4" => OffsetCoD4,
                "COD5" => OffsetWaW,
                "WAW" => OffsetWaW,
                "MW2" => OffsetMW2,
                _ => throw new ArgumentException($"Unknown game: {gameShortName}. Supported: COD4, COD5, WAW, MW2", nameof(gameShortName))
            };
        }

        /// <summary>
        /// Checks if this field is available for the specified game.
        /// </summary>
        /// <param name="gameShortName">Game identifier: "COD4", "COD5", or "MW2"</param>
        /// <returns>True if the field has a valid offset for this game.</returns>
        public bool IsAvailableFor(string gameShortName)
        {
            return GetOffset(gameShortName) >= 0;
        }
    }
}
