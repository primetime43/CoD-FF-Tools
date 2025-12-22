using Call_of_Duty_FastFile_Editor.Constants;
using Call_of_Duty_FastFile_Editor.Services;
using Call_of_Duty_FastFile_Editor.ZoneParsers;
using System.Diagnostics;

namespace Call_of_Duty_FastFile_Editor.Models
{
    public class ZoneFile
    {
        public FastFile ParentFastFile { get; set; }

        /// <summary>The full path to the .zone file.</summary>
        public string FilePath { get; private set; }

        /// <summary>All bytes of the .zone file.</summary>
        public byte[] Data { get; internal set; }

        /// <summary>
        /// Constructs the wrapper; actual loading is done in Load().
        /// </summary>
        public ZoneFile(string path, FastFile currentFF)
        {
            FilePath = path ?? throw new ArgumentNullException(nameof(path));
            ParentFastFile = currentFF ?? throw new ArgumentNullException(nameof(currentFF));
        }

        /// <summary>
        /// Creates a ZoneFile, loads its bytes, and reads its header fields.
        /// </summary>
        public static ZoneFile Load(string path, FastFile fastFile)
        {
            if (fastFile == null)
                throw new ArgumentNullException(nameof(fastFile));

            var z = new ZoneFile(path, fastFile);
            z.LoadData();
            z.ReadHeaderFields();
            z.ParseAssetPool();
            return z;
        }

        /// <summary>Modify on-disk file, then refresh Data.</summary>
        public void ModifyZoneFile(Action<FileStream> modification)
        {
            using (FileStream fs = new FileStream(FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                modification(fs);
            }
            LoadData();
        }

        // XFile structure properties
        public uint ZoneSize { get; private set; }
        public uint ExternalSize { get; private set; }
        public uint BlockSizeTemp { get; private set; }
        public uint BlockSizePhysical { get; private set; }
        public uint BlockSizeRuntime { get; private set; }
        public uint BlockSizeVirtual { get; private set; }
        public uint BlockSizeLarge { get; private set; }
        public uint BlockSizeCallback { get; private set; }
        public uint BlockSizeVertex { get; private set; }

        // XAssetList structure properties
        public uint ScriptStringCount { get; private set; }
        public uint ScriptStringsPtr { get; private set; }
        public uint AssetCount { get; private set; }
        public uint AssetsPtr { get; private set; }

        // For display or debugging purposes.
        public Dictionary<string, uint>? HeaderFieldValues { get; private set; }

        // The asset mapping container.
        public ZoneFileAssetManifest ZoneFileAssets { get; set; } = new ZoneFileAssetManifest();

        public int AssetPoolStartOffset { get; internal set; }
        public int AssetPoolEndOffset { get; internal set; }

        public int TagSectionStartOffset { get; set; }
        public int TagSectionEndOffset { get; set; }

        /// <summary>
        /// Gets header field offsets based on platform.
        /// Xbox 360 has 6 block sizes, PS3 has 7, PC has 8.
        /// This affects where XAssetList fields are located.
        /// </summary>
        private Dictionary<string, int> GetHeaderFieldOffsets()
        {
            bool isXbox360 = ParentFastFile?.IsXbox360 ?? false;
            bool isPC = ParentFastFile?.IsPC ?? false;

            var offsets = new Dictionary<string, int>
            {
                // XFile structure (common)
                { "ZoneSize", ZoneFileHeaderConstants.ZoneSizeOffset },
                { "ExternalSize", ZoneFileHeaderConstants.ExternalSizeOffset },
                { "BlockSizeTemp", ZoneFileHeaderConstants.BlockSizeTempOffset },
                { "BlockSizePhysical", ZoneFileHeaderConstants.BlockSizePhysicalOffset },
                { "BlockSizeRuntime", ZoneFileHeaderConstants.BlockSizeRuntimeOffset },
                { "BlockSizeVirtual", ZoneFileHeaderConstants.BlockSizeVirtualOffset },
                { "BlockSizeLarge", ZoneFileHeaderConstants.BlockSizeLargeOffset },
                { "BlockSizeCallback", ZoneFileHeaderConstants.BlockSizeCallbackOffset },
                { "BlockSizeVertex", ZoneFileHeaderConstants.BlockSizeVertexOffset }
            };

            // XAssetList structure - offsets depend on platform
            if (isXbox360)
            {
                // Xbox 360: 6 blocks = 32 bytes XFile header
                offsets["ScriptStringCount"] = ZoneFileHeaderConstants.Xbox360_ScriptStringCountOffset;
                offsets["ScriptStringsPtr"] = ZoneFileHeaderConstants.Xbox360_ScriptStringsPtrOffset;
                offsets["AssetCount"] = ZoneFileHeaderConstants.Xbox360_AssetCountOffset;
                offsets["AssetsPtr"] = ZoneFileHeaderConstants.Xbox360_AssetsPtrOffset;
            }
            else if (isPC)
            {
                // PC: 8 blocks = 40 bytes XFile header
                offsets["ScriptStringCount"] = ZoneFileHeaderConstants.PC_ScriptStringCountOffset;
                offsets["ScriptStringsPtr"] = ZoneFileHeaderConstants.PC_ScriptStringsPtrOffset;
                offsets["AssetCount"] = ZoneFileHeaderConstants.PC_AssetCountOffset;
                offsets["AssetsPtr"] = ZoneFileHeaderConstants.PC_AssetsPtrOffset;
            }
            else
            {
                // PS3: 7 blocks = 36 bytes XFile header (default)
                offsets["ScriptStringCount"] = ZoneFileHeaderConstants.ScriptStringCountOffset;
                offsets["ScriptStringsPtr"] = ZoneFileHeaderConstants.ScriptStringsPtrOffset;
                offsets["AssetCount"] = ZoneFileHeaderConstants.AssetCountOffset;
                offsets["AssetsPtr"] = ZoneFileHeaderConstants.AssetsPtrOffset;
            }

            return offsets;
        }

        /// <summary>Reloads Data from disk.</summary>
        public void LoadData() => Data = File.ReadAllBytes(FilePath);

        /// <summary>Parses the zone's asset pool into ZoneFileAssets & offsets.</summary>
        public void ParseAssetPool()
        {
            // Use structure-based parsing first (uses header counts)
            var structureParser = new StructureBasedZoneParser(this);
            bool success = structureParser.Parse();

            if (!success)
            {
                Debug.WriteLine("Structure-based parsing failed, trying pattern-based fallback.");
                // Fallback is handled internally by StructureBasedZoneParser
                // If we still fail, show error
                if (ZoneFileAssets.ZoneAssetRecords == null || ZoneFileAssets.ZoneAssetRecords.Count == 0)
                {
                    Debug.WriteLine("Asset pool parse failed: No assets found.");
                    MessageBox.Show(
                        "Failed to parse asset pool!\n\nNo assets could be found in the zone file.",
                        "Parse Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }
            }
        }

        /// <summary>
        /// For UI: "0x…" hex offset of any header field.
        /// </summary>
        public string GetZoneOffset(string zoneName)
        {
            var offsets = GetHeaderFieldOffsets();
            if (offsets.TryGetValue(zoneName, out int offset))
            {
                return $"0x{offset:X2}";
            }
            else
            {
                return "N/A";
            }
        }

        /// <summary>
        /// Reads every header field into HeaderFieldValues and populates the strongly‑typed props.
        /// </summary>
        public void ReadHeaderFields()
        {
            var offsets = GetHeaderFieldOffsets();

            // Read every header field into a Dictionary<string,uint>
            HeaderFieldValues = offsets
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => ReadField(kvp.Key, offsets)
                );

            // Populate XFile structure properties
            ZoneSize = HeaderFieldValues[nameof(ZoneSize)];
            ExternalSize = HeaderFieldValues[nameof(ExternalSize)];
            BlockSizeTemp = HeaderFieldValues[nameof(BlockSizeTemp)];
            BlockSizePhysical = HeaderFieldValues[nameof(BlockSizePhysical)];
            BlockSizeRuntime = HeaderFieldValues[nameof(BlockSizeRuntime)];
            BlockSizeVirtual = HeaderFieldValues[nameof(BlockSizeVirtual)];
            BlockSizeLarge = HeaderFieldValues[nameof(BlockSizeLarge)];
            BlockSizeCallback = HeaderFieldValues[nameof(BlockSizeCallback)];
            BlockSizeVertex = HeaderFieldValues[nameof(BlockSizeVertex)];

            // Populate XAssetList structure properties
            ScriptStringCount = HeaderFieldValues[nameof(ScriptStringCount)];
            ScriptStringsPtr = HeaderFieldValues[nameof(ScriptStringsPtr)];
            AssetCount = HeaderFieldValues[nameof(AssetCount)];
            AssetsPtr = HeaderFieldValues[nameof(AssetsPtr)];

            Debug.WriteLine($"[ZoneFile] Platform: {(ParentFastFile?.IsXbox360 == true ? "Xbox 360" : (ParentFastFile?.IsPC == true ? "PC" : "PS3"))}");
            Debug.WriteLine($"[ZoneFile] ScriptStringCount: {ScriptStringCount}, AssetCount: {AssetCount}");
        }

        /// <summary>
        /// Helper that looks up the offset for a header‑field name and reads a uint from Data.
        /// Uses big-endian for console, little-endian for PC.
        /// </summary>
        private uint ReadField(string name, Dictionary<string, int> offsets)
        {
            int offset = offsets[name];
            bool isBigEndian = !(ParentFastFile?.IsPC ?? false);
            return Utilities.ReadUInt32AtOffset(offset, this, isBigEndian);
        }
    }
}
