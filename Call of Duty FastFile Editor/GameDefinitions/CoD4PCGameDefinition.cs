using Call_of_Duty_FastFile_Editor.Models;
using FastFileLib.GameDefinitions;
using System.Diagnostics;

namespace Call_of_Duty_FastFile_Editor.GameDefinitions
{
    /// <summary>
    /// Game definition implementation for Call of Duty 4: Modern Warfare PC version.
    /// Uses little-endian byte order for all multi-byte values (unlike PS3/Xbox 360 which use big-endian).
    /// Uses PC-specific asset type IDs (shifted -2 from console due to missing pixelshader/vertexshader).
    ///
    /// NOTE: PC zone asset parsing is not yet supported. This definition is used for:
    /// - PC FastFile detection and decompression
    /// - Asset pool display (showing what assets exist in the zone)
    /// - Asset type name lookup
    ///
    /// The parsing methods return null/empty results as parsing is skipped in AssetRecordProcessor.
    /// </summary>
    public class CoD4PCGameDefinition : GameDefinitionBase
    {
        public CoD4PCGameDefinition()
        {
            IsPC = true;
            IsXbox360 = false;
        }

        public override string GameName => CoD4Definition.GameName;
        public override string ShortName => "COD4 (PC)";
        public override int VersionValue => CoD4Definition.VersionValue;
        public override int PCVersionValue => CoD4Definition.PCVersionValue;
        public override byte[] VersionBytes => CoD4Definition.VersionBytes;

        // PC-specific asset type IDs (shifted -2 from console due to missing pixelshader and vertexshader)
        public override byte RawFileAssetType => (byte)CoD4AssetTypePC.rawfile;
        public override byte LocalizeAssetType => (byte)CoD4AssetTypePC.localize;
        public override byte MenuFileAssetType => (byte)CoD4AssetTypePC.menufile;
        public override byte XAnimAssetType => (byte)CoD4AssetTypePC.xanim;
        public override byte StringTableAssetType => (byte)CoD4AssetTypePC.stringtable;
        public override byte WeaponAssetType => (byte)CoD4AssetTypePC.weapon;
        public override byte ImageAssetType => (byte)CoD4AssetTypePC.image;

        public override string GetAssetTypeName(int assetType)
        {
            if (Enum.IsDefined(typeof(CoD4AssetTypePC), assetType))
            {
                return ((CoD4AssetTypePC)assetType).ToString();
            }
            return $"unknown_0x{assetType:X2}";
        }

        public override bool IsSupportedAssetType(int assetType)
        {
            // PC asset parsing is not yet supported - return false for all types
            // This ensures AssetRecordProcessor doesn't try to parse PC assets
            return false;
        }

        // NOTE: All parsing methods return null because PC zone parsing is not yet supported.
        // The AssetRecordProcessor.cs returns early for PC files, so these methods won't be called.
        // They are implemented here to satisfy the interface contract.

        public override RawFileNode? ParseRawFile(byte[] zoneData, int offset)
        {
            Debug.WriteLine($"[{ShortName}] ParseRawFile called but PC parsing not supported");
            return null;
        }

        public override (LocalizedEntry? entry, int nextOffset) ParseLocalizedEntry(byte[] zoneData, int offset)
        {
            Debug.WriteLine($"[{ShortName}] ParseLocalizedEntry called but PC parsing not supported");
            return (null, offset);
        }

        public override XAnimParts? ParseXAnim(byte[] zoneData, int offset)
        {
            Debug.WriteLine($"[{ShortName}] ParseXAnim called but PC parsing not supported");
            return null;
        }

        public override StringTable? ParseStringTable(byte[] zoneData, int offset)
        {
            Debug.WriteLine($"[{ShortName}] ParseStringTable called but PC parsing not supported");
            return null;
        }

        public override WeaponAsset? ParseWeapon(byte[] zoneData, int offset)
        {
            Debug.WriteLine($"[{ShortName}] ParseWeapon called but PC parsing not supported");
            return null;
        }

        public override MenuList? ParseMenuFile(byte[] zoneData, int offset)
        {
            Debug.WriteLine($"[{ShortName}] ParseMenuFile called but PC parsing not supported");
            return null;
        }

        public override MaterialAsset? ParseMaterial(byte[] zoneData, int offset)
        {
            Debug.WriteLine($"[{ShortName}] ParseMaterial called but PC parsing not supported");
            return null;
        }

        public override TechSetAsset? ParseTechSet(byte[] zoneData, int offset)
        {
            Debug.WriteLine($"[{ShortName}] ParseTechSet called but PC parsing not supported");
            return null;
        }

        public override ImageAsset? ParseImage(byte[] zoneData, int offset)
        {
            Debug.WriteLine($"[{ShortName}] ParseImage called but PC parsing not supported");
            return null;
        }
    }
}
