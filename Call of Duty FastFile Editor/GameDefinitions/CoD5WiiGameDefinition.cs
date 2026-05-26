using Call_of_Duty_FastFile_Editor.Models;
using FastFileLib.GameDefinitions;

namespace Call_of_Duty_FastFile_Editor.GameDefinitions
{
    /// <summary>
    /// Game definition for Call of Duty: World at War (CoD5) Wii.
    ///
    /// Wii is structurally a hybrid:
    ///   - Big-endian (PowerPC) like PS3/Xbox 360
    ///   - 56-byte zone header with 8 blockSize slots (vs 52-byte/7-block on PS3) - has BlockSizeIndex
    ///   - PC-style asset type IDs (no pixelshader, no vertexshader) - verified against
    ///     credits.zone where the dominant type 0x17 maps to localize (899 entries for credits
    ///     text), 0x20 to rawfile, etc.
    ///
    /// So we reuse the PC asset type enum for ID mapping, but inherit BE parsing behaviour
    /// (the base class methods already read BE, which matches Wii).
    /// </summary>
    public class CoD5WiiGameDefinition : GameDefinitionBase
    {
        public CoD5WiiGameDefinition()
        {
            IsPC = false;       // big-endian, not little-endian
            IsXbox360 = false;
            IsWii = true;
        }

        public override string GameName => CoD5Definition.GameName;
        public override string ShortName => "COD5 (Wii)";
        public override int VersionValue => CoD5Definition.VersionValue;
        public override int PCVersionValue => CoD5Definition.PCVersionValue;
        public override byte[] VersionBytes => CoD5Definition.WiiVersionBytes;

        // Wii uses the PC-style asset type enum (no shader asset slots).
        public override byte RawFileAssetType    => (byte)CoD5AssetTypePC.rawfile;
        public override byte LocalizeAssetType   => (byte)CoD5AssetTypePC.localize;
        public override byte MenuFileAssetType   => (byte)CoD5AssetTypePC.menufile;
        public override byte XAnimAssetType      => (byte)CoD5AssetTypePC.xanim;
        public override byte StringTableAssetType => (byte)CoD5AssetTypePC.stringtable;
        public override byte WeaponAssetType     => (byte)CoD5AssetTypePC.weapon;
        public override byte ImageAssetType      => (byte)CoD5AssetTypePC.image;
        public byte MaterialAssetType            => (byte)CoD5AssetTypePC.material;
        public byte TechSetAssetType             => (byte)CoD5AssetTypePC.techset;

        public override string GetAssetTypeName(int assetType)
        {
            if (System.Enum.IsDefined(typeof(CoD5AssetTypePC), assetType))
                return ((CoD5AssetTypePC)assetType).ToString();
            return $"unknown_0x{assetType:X2}";
        }

        // Start small: enable parsers that don't need byte-order overrides beyond what the base
        // class does. The base ParseRawFile / ParseLocalizedEntry both read BE which matches Wii.
        public override bool IsSupportedAssetType(int assetType)
        {
            if (assetType == RawFileAssetType) return true;
            if (assetType == LocalizeAssetType) return true;
            return false;
        }

        public override bool IsMaterialType(int assetType) => assetType == MaterialAssetType;
        public override bool IsTechSetType(int assetType) => assetType == TechSetAssetType;
    }
}
