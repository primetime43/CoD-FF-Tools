using Call_of_Duty_FastFile_Editor.Models;

namespace Call_of_Duty_FastFile_Editor.GameDefinitions
{
    /// <summary>
    /// Game definition implementation for Call of Duty 4: Modern Warfare.
    /// Uses the default CoD4/CoD5 rawfile parsing structure.
    /// </summary>
    public class CoD4GameDefinition : GameDefinitionBase
    {
        public override string GameName => CoD4Definition.GameName;
        public override string ShortName => "COD4";
        public override int VersionValue => CoD4Definition.VersionValue;
        public override int PCVersionValue => CoD4Definition.PCVersionValue;
        public override byte[] VersionBytes => CoD4Definition.VersionBytes;
        public override byte RawFileAssetType => CoD4Definition.RawFileAssetType;
        public override byte LocalizeAssetType => CoD4Definition.LocalizeAssetType;
        public override byte XAnimAssetType => CoD4Definition.XAnimAssetType;

        public override string GetAssetTypeName(int assetType)
        {
            if (Enum.IsDefined(typeof(CoD4AssetType), assetType))
            {
                return ((CoD4AssetType)assetType).ToString();
            }
            return $"unknown_0x{assetType:X2}";
        }

        public override bool IsSupportedAssetType(int assetType)
        {
            return assetType == RawFileAssetType ||
                   assetType == LocalizeAssetType ||
                   assetType == XAnimAssetType;
        }
    }
}
