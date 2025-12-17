using Call_of_Duty_FastFile_Editor.Models;

namespace Call_of_Duty_FastFile_Editor.GameDefinitions
{
    /// <summary>
    /// Interface defining game-specific constants and parsing behavior.
    /// Each supported game (CoD4, CoD5, MW2, etc.) implements this interface.
    /// </summary>
    public interface IGameDefinition
    {
        /// <summary>
        /// Full game name (e.g., "Call of Duty 4: Modern Warfare").
        /// </summary>
        string GameName { get; }

        /// <summary>
        /// Short game name for display (e.g., "COD4", "MW2").
        /// </summary>
        string ShortName { get; }

        /// <summary>
        /// Platform this definition is configured for (PS3, Xbox 360, PC).
        /// Asset type IDs differ between platforms.
        /// </summary>
        string Platform { get; }

        /// <summary>
        /// Whether this is an Xbox 360 definition (asset types are shifted).
        /// </summary>
        bool IsXbox360 { get; }

        /// <summary>
        /// Console/PS3 version value.
        /// </summary>
        int VersionValue { get; }

        /// <summary>
        /// PC version value (may differ from console).
        /// </summary>
        int PCVersionValue { get; }

        /// <summary>
        /// Version bytes for the FastFile header (big-endian).
        /// </summary>
        byte[] VersionBytes { get; }

        /// <summary>
        /// Asset type ID for rawfile assets.
        /// </summary>
        byte RawFileAssetType { get; }

        /// <summary>
        /// Asset type ID for localize assets.
        /// </summary>
        byte LocalizeAssetType { get; }

        /// <summary>
        /// Asset type ID for menufile assets.
        /// </summary>
        byte MenuFileAssetType { get; }

        /// <summary>
        /// Asset type ID for xanim assets.
        /// </summary>
        byte XAnimAssetType { get; }

        /// <summary>
        /// Asset type ID for stringtable assets.
        /// </summary>
        byte StringTableAssetType { get; }

        /// <summary>
        /// Checks if the given asset type value is a rawfile.
        /// </summary>
        bool IsRawFileType(int assetType);

        /// <summary>
        /// Checks if the given asset type value is an xanim.
        /// </summary>
        bool IsXAnimType(int assetType);

        /// <summary>
        /// Checks if the given asset type value is a menufile.
        /// </summary>
        bool IsMenuFileType(int assetType);

        /// <summary>
        /// Checks if the given asset type value is a localize entry.
        /// </summary>
        bool IsLocalizeType(int assetType);

        /// <summary>
        /// Checks if the given asset type value is supported for parsing.
        /// </summary>
        bool IsSupportedAssetType(int assetType);

        /// <summary>
        /// Gets the name of the asset type for display purposes.
        /// </summary>
        string GetAssetTypeName(int assetType);

        /// <summary>
        /// Parses a rawfile from the zone data at the given offset.
        /// </summary>
        /// <param name="zoneData">The zone file data.</param>
        /// <param name="offset">Starting offset to parse from.</param>
        /// <returns>Parsed RawFileNode, or null if parsing failed.</returns>
        RawFileNode? ParseRawFile(byte[] zoneData, int offset);

        /// <summary>
        /// Parses a localized entry from the zone data at the given offset.
        /// </summary>
        /// <param name="zoneData">The zone file data.</param>
        /// <param name="offset">Starting offset to parse from.</param>
        /// <returns>Tuple of parsed LocalizedEntry and the next offset, or null entry if parsing failed.</returns>
        (LocalizedEntry? entry, int nextOffset) ParseLocalizedEntry(byte[] zoneData, int offset);

        /// <summary>
        /// Parses a menufile/menulist asset from the zone data at the given offset.
        /// </summary>
        /// <param name="zoneData">The zone file data.</param>
        /// <param name="offset">Starting offset to parse from.</param>
        /// <returns>Parsed MenuList, or null if parsing failed.</returns>
        MenuList? ParseMenuFile(byte[] zoneData, int offset);

        /// <summary>
        /// Checks if the given asset type value is a material.
        /// </summary>
        bool IsMaterialType(int assetType);

        /// <summary>
        /// Checks if the given asset type value is a techset.
        /// </summary>
        bool IsTechSetType(int assetType);

        /// <summary>
        /// Checks if the given asset type value is a stringtable.
        /// </summary>
        bool IsStringTableType(int assetType);

        /// <summary>
        /// Parses a material asset from the zone data at the given offset.
        /// </summary>
        /// <param name="zoneData">The zone file data.</param>
        /// <param name="offset">Starting offset to parse from.</param>
        /// <returns>Parsed MaterialAsset, or null if parsing failed.</returns>
        MaterialAsset? ParseMaterial(byte[] zoneData, int offset);

        /// <summary>
        /// Parses a techset asset from the zone data at the given offset.
        /// </summary>
        /// <param name="zoneData">The zone file data.</param>
        /// <param name="offset">Starting offset to parse from.</param>
        /// <returns>Parsed TechSetAsset, or null if parsing failed.</returns>
        TechSetAsset? ParseTechSet(byte[] zoneData, int offset);

        /// <summary>
        /// Parses an xanim asset from the zone data at the given offset.
        /// </summary>
        /// <param name="zoneData">The zone file data.</param>
        /// <param name="offset">Starting offset to parse from.</param>
        /// <returns>Parsed XAnimParts, or null if parsing failed.</returns>
        XAnimParts? ParseXAnim(byte[] zoneData, int offset);

        /// <summary>
        /// Parses a stringtable asset from the zone data at the given offset.
        /// </summary>
        /// <param name="zoneData">The zone file data.</param>
        /// <param name="offset">Starting offset to parse from.</param>
        /// <returns>Parsed StringTable, or null if parsing failed.</returns>
        StringTable? ParseStringTable(byte[] zoneData, int offset);

        /// <summary>
        /// Asset type ID for weapon assets.
        /// </summary>
        byte WeaponAssetType { get; }

        /// <summary>
        /// Checks if the given asset type value is a weapon.
        /// </summary>
        bool IsWeaponType(int assetType);

        /// <summary>
        /// Parses a weapon asset from the zone data at the given offset.
        /// </summary>
        /// <param name="zoneData">The zone file data.</param>
        /// <param name="offset">Starting offset to parse from.</param>
        /// <returns>Parsed WeaponAsset, or null if parsing failed.</returns>
        WeaponAsset? ParseWeapon(byte[] zoneData, int offset);

        /// <summary>
        /// Asset type ID for image assets.
        /// </summary>
        byte ImageAssetType { get; }

        /// <summary>
        /// Checks if the given asset type value is an image.
        /// </summary>
        bool IsImageType(int assetType);

        /// <summary>
        /// Parses an image asset from the zone data at the given offset.
        /// </summary>
        /// <param name="zoneData">The zone file data.</param>
        /// <param name="offset">Starting offset to parse from.</param>
        /// <returns>Parsed ImageAsset, or null if parsing failed.</returns>
        ImageAsset? ParseImage(byte[] zoneData, int offset);
    }
}
