using Call_of_Duty_FastFile_Editor.Models;
using Call_of_Duty_FastFile_Editor.ZoneParsers;
using FastFileLib.GameDefinitions;
using System.Diagnostics;

namespace Call_of_Duty_FastFile_Editor.GameDefinitions
{
    /// <summary>
    /// Game definition implementation for Call of Duty: World at War (CoD5) PC version.
    /// Uses little-endian byte order for all multi-byte values (unlike PS3/Xbox 360 which use big-endian).
    /// Uses PC-specific asset type IDs (shifted -2 from PS3 due to missing pixelshader/vertexshader).
    ///
    /// NOTE: PC zone asset parsing is not yet supported. This definition is used for:
    /// - PC FastFile detection and decompression
    /// - Asset pool display (showing what assets exist in the zone)
    /// - Asset type name lookup
    ///
    /// The parsing methods return null/empty results as parsing is skipped in AssetRecordProcessor.
    /// </summary>
    public class CoD5PCGameDefinition : GameDefinitionBase
    {
        public CoD5PCGameDefinition()
        {
            IsPC = true;
            IsXbox360 = false;
        }

        public override string GameName => CoD5Definition.GameName;
        public override string ShortName => "COD5 (PC)";
        public override int VersionValue => CoD5Definition.VersionValue;
        public override int PCVersionValue => CoD5Definition.PCVersionValue;
        public override byte[] VersionBytes => CoD5Definition.VersionBytes;

        // PC-specific asset type IDs (shifted -2 from PS3 due to missing pixelshader and vertexshader)
        public override byte RawFileAssetType => (byte)CoD5AssetTypePC.rawfile;
        public override byte LocalizeAssetType => (byte)CoD5AssetTypePC.localize;
        public override byte MenuFileAssetType => (byte)CoD5AssetTypePC.menufile;
        public override byte XAnimAssetType => (byte)CoD5AssetTypePC.xanim;
        public override byte StringTableAssetType => (byte)CoD5AssetTypePC.stringtable;
        public override byte WeaponAssetType => (byte)CoD5AssetTypePC.weapon;
        public override byte ImageAssetType => (byte)CoD5AssetTypePC.image;
        public byte MaterialAssetType => (byte)CoD5AssetTypePC.material;
        public byte TechSetAssetType => (byte)CoD5AssetTypePC.techset;

        public override string GetAssetTypeName(int assetType)
        {
            if (Enum.IsDefined(typeof(CoD5AssetTypePC), assetType))
            {
                return ((CoD5AssetTypePC)assetType).ToString();
            }
            return $"unknown_0x{assetType:X2}";
        }

        public override bool IsSupportedAssetType(int assetType)
        {
            // PC support is incremental. Currently supported:
            //   - rawfile  (text scripts/CSVs - the primary need for issue #21 model swap workflow)
            //   - localize (string entries - byte-order-independent, same parser as console)
            //   - menufile (PC menuDef_t = 288 bytes vs console's 312 — see Cod5MenuDeserializer)
            // Other types fall through to false and will be skipped with a warning.
            if (assetType == RawFileAssetType) return true;
            if (assetType == LocalizeAssetType) return true;
            if (assetType == MenuFileAssetType) return true;
            return false;
        }

        public override bool IsMaterialType(int assetType) => assetType == MaterialAssetType;
        public override bool IsTechSetType(int assetType) => assetType == TechSetAssetType;

        // NOTE: All parsing methods return null because PC zone parsing is not yet supported.
        // The AssetRecordProcessor.cs returns early for PC files, so these methods won't be called.
        // They are implemented here to satisfy the interface contract.

        /// <summary>
        /// PC rawfile structure - same shape as console but size field is little-endian:
        ///   [FF FF FF FF] [4-byte size LE] [FF FF FF FF] [null-terminated name] [data]
        /// </summary>
        public override RawFileNode? ParseRawFile(byte[] zoneData, int offset)
        {
            Debug.WriteLine($"[{ShortName}] ParseRawFile at offset 0x{offset:X}");

            if (offset > zoneData.Length - 12)
            {
                Debug.WriteLine($"[{ShortName}] Not enough bytes for header at 0x{offset:X}");
                return null;
            }

            // First marker - 0xFFFFFFFF is endian-agnostic
            uint marker1 = ReadUInt32BE(zoneData, offset);
            if (marker1 != 0xFFFFFFFF)
            {
                Debug.WriteLine($"[{ShortName}] Unexpected marker1 at 0x{offset:X}: 0x{marker1:X}");
                return null;
            }

            // Data length - LE on PC
            int dataLength = (int)ReadUInt32LE(zoneData, offset + 4);
            if (dataLength < 0)
            {
                Debug.WriteLine($"[{ShortName}] Negative dataLength: {dataLength}");
                return null;
            }

            const int MAX_RAWFILE_SIZE = 5 * 1024 * 1024;
            if (dataLength > MAX_RAWFILE_SIZE)
            {
                Debug.WriteLine($"[{ShortName}] dataLength {dataLength} unreasonably large at 0x{offset + 4:X}");
                return null;
            }

            // Second marker - 0xFFFFFFFF endian-agnostic
            uint marker2 = ReadUInt32BE(zoneData, offset + 8);
            if (marker2 != 0xFFFFFFFF)
            {
                Debug.WriteLine($"[{ShortName}] Unexpected marker2 at 0x{offset + 8:X}: 0x{marker2:X}");
                return null;
            }

            var node = new RawFileNode
            {
                StartOfFileHeader = offset,
                MaxSize = dataLength
            };

            int fileNameOffset = offset + 12;
            string fileName = ReadNullTerminatedString(zoneData, fileNameOffset);

            if (!IsValidRawFileName(fileName))
            {
                Debug.WriteLine($"[{ShortName}] Invalid filename '{fileName}' at 0x{fileNameOffset:X}");
                return null;
            }

            node.FileName = fileName;
            int fileDataOffset = fileNameOffset + fileName.Length + 1;

            int availableData = zoneData.Length - fileDataOffset;
            int actualDataLength = Math.Min(dataLength, availableData);

            if (actualDataLength > 0)
            {
                byte[] rawBytes = new byte[actualDataLength];
                Array.Copy(zoneData, fileDataOffset, rawBytes, 0, actualDataLength);
                node.RawFileBytes = rawBytes;
                node.RawFileContent = System.Text.Encoding.UTF8.GetString(rawBytes);
                node.RawFileEndPosition = Math.Min(fileDataOffset + dataLength + 1, zoneData.Length);
            }
            else
            {
                node.RawFileBytes = Array.Empty<byte>();
                node.RawFileContent = string.Empty;
                node.RawFileEndPosition = fileDataOffset;
            }

            Debug.WriteLine($"[{ShortName}] Parsed rawfile '{fileName}' size={dataLength}");
            return node;
        }

        /// <summary>
        /// Localize entries are byte-order-independent (markers + null-terminated strings),
        /// so delegate to the base implementation.
        /// </summary>
        public override (LocalizedEntry? entry, int nextOffset) ParseLocalizedEntry(byte[] zoneData, int offset)
        {
            return base.ParseLocalizedEntry(zoneData, offset);
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

        /// <summary>
        /// WaW PC menulist parsing — routes to the CoD5 deserializer in PC layout mode
        /// (288-byte menuDef_t, little-endian). Same scan strategy as the console path.
        /// </summary>
        public override MenuList? ParseMenuFile(byte[] zoneData, int offset)
        {
            Debug.WriteLine($"[{ShortName}] ParseMenuFile at 0x{offset:X} (CoD5 PC layout)");
            return MenuListParser.ParseMenuList(zoneData, offset, isBigEndian: false, layout: MenuBinaryLayout.Cod5PC);
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
