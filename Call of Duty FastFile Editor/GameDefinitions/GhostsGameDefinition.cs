using Call_of_Duty_FastFile_Editor.Models;
using FastFileLib.GameDefinitions;

namespace Call_of_Duty_FastFile_Editor.GameDefinitions
{
    /// <summary>
    /// Game definition implementation for Call of Duty: Ghosts (IW6), PS3 platform.
    /// Pool-listing only — per-asset content parsers (rawfile bodies, weapon structs,
    /// stringtables, etc.) are NOT implemented for Ghosts. The asset pool tab will
    /// show type names and offsets, but individual asset content trees won't populate.
    ///
    /// Only PS3 is implemented so far; Xbox 360 / Wii-U / PC variants of IW6 use
    /// shifted asset type IDs and aren't wired up.
    /// </summary>
    public class GhostsGameDefinition : GameDefinitionBase
    {
        public GhostsGameDefinition()
        {
            IsXbox360 = false;
            IsPC = false;
            IsWii = false;
        }

        public override string GameName => GhostsDefinition.GameName;
        public override string ShortName => "Ghosts";
        public override int VersionValue => GhostsDefinition.VersionValue;
        public override int PCVersionValue => GhostsDefinition.VersionValue;  // PC variant not implemented
        public override byte[] VersionBytes => new byte[] { 0x00, 0x00, 0x02, 0x2E };

        // Asset type IDs (PS3)
        public override byte RawFileAssetType    => (byte)GhostsAssetTypePS3.rawfile;
        public override byte LocalizeAssetType   => (byte)GhostsAssetTypePS3.localize;
        public override byte MenuFileAssetType   => (byte)GhostsAssetTypePS3.menufile;
        public override byte XAnimAssetType      => (byte)GhostsAssetTypePS3.xanim;
        public override byte StringTableAssetType => (byte)GhostsAssetTypePS3.stringtable;
        public override byte WeaponAssetType     => (byte)GhostsAssetTypePS3.weapon;
        public override byte ImageAssetType      => (byte)GhostsAssetTypePS3.image;

        // Helper: scriptfile type (IW6 has both rawfile and scriptfile; this is unique
        // to Ghosts/IW6+, not present in CoD4/WaW/MW2). Exposed so callers that want
        // to highlight scripts can do so.
        public byte ScriptFileAssetType => (byte)GhostsAssetTypePS3.scriptfile;

        public override bool IsMaterialType(int assetType) => assetType == (int)GhostsAssetTypePS3.material;
        public override bool IsTechSetType(int assetType) => assetType == (int)GhostsAssetTypePS3.techset;

        public override string GetAssetTypeName(int assetType)
        {
            if (Enum.IsDefined(typeof(GhostsAssetTypePS3), assetType))
                return ((GhostsAssetTypePS3)assetType).ToString();
            return $"unknown_0x{assetType:X2}";
        }

        // -------- Content parsers --------
        // Ghosts asset-content layouts aren't mapped yet. The base class's
        // ParseRawFile/ParseLocalizedEntry/etc. assume CoD4/WaW header shapes,
        // which don't apply, so override them all to return null.  The asset
        // pool tab still populates from the pool walker — these only fire when
        // the editor tries to actually read individual asset bodies.

        public override RawFileNode? ParseRawFile(byte[] zoneData, int offset) => null;
        public override (LocalizedEntry? entry, int nextOffset) ParseLocalizedEntry(byte[] zoneData, int offset)
            => (null, offset);
        public override MenuList? ParseMenuFile(byte[] zoneData, int offset) => null;
        public override MaterialAsset? ParseMaterial(byte[] zoneData, int offset) => null;
        public override TechSetAsset? ParseTechSet(byte[] zoneData, int offset) => null;
        public override XAnimParts? ParseXAnim(byte[] zoneData, int offset) => null;
        public override StringTable? ParseStringTable(byte[] zoneData, int offset) => null;
        public override WeaponAsset? ParseWeapon(byte[] zoneData, int offset) => null;
        public override ImageAsset? ParseImage(byte[] zoneData, int offset) => null;
    }
}
