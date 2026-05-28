using Call_of_Duty_FastFile_Editor.Models;
using FastFileLib.GameDefinitions;

namespace Call_of_Duty_FastFile_Editor.GameDefinitions
{
    /// <summary>
    /// Game definition for Call of Duty 4: Modern Warfare — Reflex Edition (Wii).
    ///
    /// Wii FF version 0x1A2, big-endian (PowerPC), 56-byte zone header (same shape as
    /// WaW Wii — 8 blockSize slots including BlockSizeIndex).
    ///
    /// Asset type enum: CoD4 Wii inherits the **Xbox 360** asset type IDs (rawfile=0x20,
    /// techset=0x06, image=0x07), NOT the PC enum. This differs from WaW Wii which uses
    /// the WaW PC enum (no shader slots). Verified against retail Reflex zones —
    /// `ac130_load.zone` has the type distribution `1×0x07 + 3×0x06 + 1×0x20`, which only
    /// fits Xbox 360 enum (1 image + 3 techsets + 1 rawfile, exactly what a load-screen
    /// FF should contain). The PC enum gives nonsense (1 sound + 3 images + 1 stringtable
    /// for a loading-screen zone is implausible).
    ///
    /// CoD4 Wii also extends the enum with `packindex = 0x22` (used for `.pak` files —
    /// Reflex stores textures in packfiles rather than as inline image assets).
    /// </summary>
    public class CoD4WiiGameDefinition : GameDefinitionBase
    {
        public CoD4WiiGameDefinition()
        {
            IsPC = false;
            IsXbox360 = false;
            IsWii = true;
        }

        public override string GameName => CoD4Definition.GameName;
        public override string ShortName => "COD4 (Wii)";
        public override int VersionValue => CoD4Definition.VersionValue;
        public override int PCVersionValue => CoD4Definition.PCVersionValue;
        public override byte[] VersionBytes => CoD4Definition.WiiVersionBytes;

        // Wii uses the Xbox 360 enum (drops vertexshader, keeps pixelshader) — verified
        // against retail Reflex load FFs. See class comment for the type-distribution proof.
        public override byte RawFileAssetType    => (byte)CoD4AssetTypeXbox360.rawfile;
        public override byte LocalizeAssetType   => (byte)CoD4AssetTypeXbox360.localize;
        public override byte MenuFileAssetType   => (byte)CoD4AssetTypeXbox360.menufile;
        public override byte XAnimAssetType      => (byte)CoD4AssetTypeXbox360.xanim;
        public override byte StringTableAssetType => (byte)CoD4AssetTypeXbox360.stringtable;
        public override byte WeaponAssetType     => (byte)CoD4AssetTypeXbox360.weapon;
        public override byte ImageAssetType      => (byte)CoD4AssetTypeXbox360.image;

        public override string GetAssetTypeName(int assetType)
        {
            // Reflex-specific extension: 0x22 is packindex (.pak texture archives).
            if (assetType == 0x22) return "packindex";
            if (System.Enum.IsDefined(typeof(CoD4AssetTypeXbox360), assetType))
                return ((CoD4AssetTypeXbox360)assetType).ToString();
            return $"unknown_0x{assetType:X2}";
        }

        // Start with rawfile + localize editing (matches CoD5 Wii scope). The base BE
        // parsers in GameDefinitionBase already match CoD4 Wii's byte order and rawfile
        // header layout (12-byte uncompressed `[FFFFFFFF][size BE][FFFFFFFF][name\0][data]`).
        public override bool IsSupportedAssetType(int assetType)
        {
            if (assetType == RawFileAssetType) return true;
            if (assetType == LocalizeAssetType) return true;
            return false;
        }
    }
}
