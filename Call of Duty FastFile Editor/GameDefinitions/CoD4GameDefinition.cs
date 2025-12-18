using Call_of_Duty_FastFile_Editor.Models;

namespace Call_of_Duty_FastFile_Editor.GameDefinitions
{
    /// <summary>
    /// Game definition implementation for Call of Duty 4: Modern Warfare (PS3).
    /// Uses PS3-specific asset type IDs (has both pixelshader and vertexshader).
    /// </summary>
    public class CoD4GameDefinition : GameDefinitionBase
    {
        public CoD4GameDefinition()
        {
            IsPC = false;
            IsXbox360 = false;
        }

        public override string GameName => CoD4Definition.GameName;
        public override string ShortName => "COD4 (PS3)";
        public override int VersionValue => CoD4Definition.VersionValue;
        public override int PCVersionValue => CoD4Definition.PCVersionValue;
        public override byte[] VersionBytes => CoD4Definition.VersionBytes;

        // PS3-specific asset type IDs
        public override byte RawFileAssetType => (byte)CoD4AssetTypePS3.rawfile;
        public override byte LocalizeAssetType => (byte)CoD4AssetTypePS3.localize;
        public override byte XAnimAssetType => (byte)CoD4AssetTypePS3.xanim;
        public override byte StringTableAssetType => (byte)CoD4AssetTypePS3.stringtable;
        public override byte MenuFileAssetType => (byte)CoD4AssetTypePS3.menufile;
        public override byte WeaponAssetType => (byte)CoD4AssetTypePS3.weapon;
        public override byte ImageAssetType => (byte)CoD4AssetTypePS3.image;

        public override string GetAssetTypeName(int assetType)
        {
            if (Enum.IsDefined(typeof(CoD4AssetTypePS3), assetType))
            {
                return ((CoD4AssetTypePS3)assetType).ToString();
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
