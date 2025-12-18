using Call_of_Duty_FastFile_Editor.Models;
using FastFileLib.GameDefinitions;

namespace Call_of_Duty_FastFile_Editor.GameDefinitions
{
    /// <summary>
    /// Game definition implementation for Call of Duty 4: Modern Warfare (Xbox 360).
    /// Uses Xbox 360-specific asset type IDs (has pixelshader but no vertexshader).
    /// </summary>
    public class CoD4Xbox360GameDefinition : GameDefinitionBase
    {
        public CoD4Xbox360GameDefinition()
        {
            IsPC = false;
            IsXbox360 = true;
        }

        public override string GameName => CoD4Definition.GameName;
        public override string ShortName => "COD4 (Xbox 360)";
        public override int VersionValue => CoD4Definition.VersionValue;
        public override int PCVersionValue => CoD4Definition.PCVersionValue;
        public override byte[] VersionBytes => CoD4Definition.VersionBytes;

        // Xbox 360-specific asset type IDs
        public override byte RawFileAssetType => (byte)CoD4AssetTypeXbox360.rawfile;
        public override byte LocalizeAssetType => (byte)CoD4AssetTypeXbox360.localize;
        public override byte XAnimAssetType => (byte)CoD4AssetTypeXbox360.xanim;
        public override byte StringTableAssetType => (byte)CoD4AssetTypeXbox360.stringtable;
        public override byte MenuFileAssetType => (byte)CoD4AssetTypeXbox360.menufile;
        public override byte WeaponAssetType => (byte)CoD4AssetTypeXbox360.weapon;
        public override byte ImageAssetType => (byte)CoD4AssetTypeXbox360.image;

        public override string GetAssetTypeName(int assetType)
        {
            if (Enum.IsDefined(typeof(CoD4AssetTypeXbox360), assetType))
            {
                return ((CoD4AssetTypeXbox360)assetType).ToString();
            }
            return $"unknown_0x{assetType:X2}";
        }

        public override bool IsSupportedAssetType(int assetType)
        {
            return assetType == RawFileAssetType ||
                   assetType == LocalizeAssetType ||
                   assetType == XAnimAssetType ||
                   assetType == StringTableAssetType;
        }
    }
}
