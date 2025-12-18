using Call_of_Duty_FastFile_Editor.Constants;
using Call_of_Duty_FastFile_Editor.GameDefinitions;
using Call_of_Duty_FastFile_Editor.Models;
using System.Diagnostics;
using System.Text;

namespace Call_of_Duty_FastFile_Editor.Services
{
    /// <summary>
    /// Rebuilds a zone file containing only supported asset types.
    /// This is necessary for structure-based parsing since we can't determine
    /// the size of unsupported assets.
    /// </summary>
    public static class ZoneFileBuilder
    {
        /// <summary>
        /// Supported asset types for COD4.
        /// </summary>
        private static readonly HashSet<CoD4AssetType> SupportedTypesCOD4 = new HashSet<CoD4AssetType>
        {
            CoD4AssetType.rawfile,
            CoD4AssetType.localize
        };

        /// <summary>
        /// Supported asset types for COD5 (PS3).
        /// </summary>
        private static readonly HashSet<CoD5AssetTypePS3> SupportedTypesCOD5 = new HashSet<CoD5AssetTypePS3>
        {
            CoD5AssetTypePS3.rawfile,
            CoD5AssetTypePS3.localize
        };

        /// <summary>
        /// Supported asset types for COD5 (Xbox 360).
        /// </summary>
        private static readonly HashSet<CoD5AssetTypeXbox360> SupportedTypesCOD5Xbox360 = new HashSet<CoD5AssetTypeXbox360>
        {
            CoD5AssetTypeXbox360.rawfile,
            CoD5AssetTypeXbox360.localize
        };

        /// <summary>
        /// Supported asset types for COD5 (PC).
        /// </summary>
        private static readonly HashSet<CoD5AssetTypePC> SupportedTypesCOD5PC = new HashSet<CoD5AssetTypePC>
        {
            CoD5AssetTypePC.rawfile,
            CoD5AssetTypePC.localize
        };

        /// <summary>
        /// Supported asset types for MW2.
        /// </summary>
        private static readonly HashSet<MW2AssetType> SupportedTypesMW2 = new HashSet<MW2AssetType>
        {
            MW2AssetType.rawfile,
            MW2AssetType.localize
        };

        /// <summary>
        /// Checks if a zone contains only supported asset types.
        /// </summary>
        public static bool ContainsOnlySupportedAssets(ZoneFile zone, FastFile fastFile)
        {
            if (zone.ZoneFileAssets?.ZoneAssetRecords == null)
                return false;

            foreach (var record in zone.ZoneFileAssets.ZoneAssetRecords)
            {
                if (fastFile.IsCod4File && !SupportedTypesCOD4.Contains(record.AssetType_COD4))
                    return false;
                if (fastFile.IsCod5File && fastFile.IsPC && !SupportedTypesCOD5PC.Contains(record.AssetType_COD5_PC))
                    return false;
                if (fastFile.IsCod5File && fastFile.IsXbox360 && !SupportedTypesCOD5Xbox360.Contains(record.AssetType_COD5_Xbox360))
                    return false;
                if (fastFile.IsCod5File && !fastFile.IsPC && !fastFile.IsXbox360 && !SupportedTypesCOD5.Contains(record.AssetType_COD5))
                    return false;
                if (fastFile.IsMW2File && !SupportedTypesMW2.Contains(record.AssetType_MW2))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Gets the list of supported asset records from the zone.
        /// </summary>
        public static List<ZoneAssetRecord> GetSupportedAssetRecords(ZoneFile zone, FastFile fastFile)
        {
            var supportedRecords = new List<ZoneAssetRecord>();

            if (zone.ZoneFileAssets?.ZoneAssetRecords == null)
                return supportedRecords;

            foreach (var record in zone.ZoneFileAssets.ZoneAssetRecords)
            {
                bool isSupported = false;

                if (fastFile.IsCod4File)
                    isSupported = SupportedTypesCOD4.Contains(record.AssetType_COD4);
                else if (fastFile.IsCod5File && fastFile.IsPC)
                    isSupported = SupportedTypesCOD5PC.Contains(record.AssetType_COD5_PC);
                else if (fastFile.IsCod5File && fastFile.IsXbox360)
                    isSupported = SupportedTypesCOD5Xbox360.Contains(record.AssetType_COD5_Xbox360);
                else if (fastFile.IsCod5File)
                    isSupported = SupportedTypesCOD5.Contains(record.AssetType_COD5);
                else if (fastFile.IsMW2File)
                    isSupported = SupportedTypesMW2.Contains(record.AssetType_MW2);

                if (isSupported)
                    supportedRecords.Add(record);
            }

            return supportedRecords;
        }

        /// <summary>
        /// Gets information about unsupported assets in the zone for display.
        /// </summary>
        public static List<string> GetUnsupportedAssetInfo(ZoneFile zone, FastFile fastFile)
        {
            var unsupportedInfo = new List<string>();

            if (zone.ZoneFileAssets?.ZoneAssetRecords == null)
                return unsupportedInfo;

            foreach (var record in zone.ZoneFileAssets.ZoneAssetRecords)
            {
                bool isSupported = false;
                string typeName = "unknown";

                if (fastFile.IsCod4File)
                {
                    isSupported = SupportedTypesCOD4.Contains(record.AssetType_COD4);
                    typeName = record.AssetType_COD4.ToString();
                }
                else if (fastFile.IsCod5File && fastFile.IsPC)
                {
                    isSupported = SupportedTypesCOD5PC.Contains(record.AssetType_COD5_PC);
                    typeName = record.AssetType_COD5_PC.ToString();
                }
                else if (fastFile.IsCod5File && fastFile.IsXbox360)
                {
                    isSupported = SupportedTypesCOD5Xbox360.Contains(record.AssetType_COD5_Xbox360);
                    typeName = record.AssetType_COD5_Xbox360.ToString();
                }
                else if (fastFile.IsCod5File)
                {
                    isSupported = SupportedTypesCOD5.Contains(record.AssetType_COD5);
                    typeName = record.AssetType_COD5.ToString();
                }
                else if (fastFile.IsMW2File)
                {
                    isSupported = SupportedTypesMW2.Contains(record.AssetType_MW2);
                    typeName = record.AssetType_MW2.ToString();
                }

                if (!isSupported)
                    unsupportedInfo.Add(typeName);
            }

            return unsupportedInfo;
        }

        /// <summary>
        /// Rebuilds the zone file data to only include supported asset types.
        /// Returns the new zone data as a byte array.
        /// </summary>
        /// <param name="zone">The original zone file.</param>
        /// <param name="fastFile">The parent fast file.</param>
        /// <param name="supportedRecords">The list of supported asset records with their parsed data.</param>
        /// <returns>New zone data containing only supported assets, or null if rebuild failed.</returns>
        public static byte[]? RebuildZoneWithSupportedAssets(
            ZoneFile zone,
            FastFile fastFile,
            List<ZoneAssetRecord> supportedRecords)
        {
            if (zone?.Data == null || supportedRecords == null || supportedRecords.Count == 0)
            {
                Debug.WriteLine("[ZoneFileBuilder] Cannot rebuild: missing data or no supported records.");
                return null;
            }

            try
            {
                using (var ms = new MemoryStream())
                {
                    byte[] originalData = zone.Data;

                    // 1. Copy header (52 bytes: 0x00-0x33)
                    const int headerSize = 0x34;
                    ms.Write(originalData, 0, headerSize);

                    // 2. Copy tag section (from header end to asset pool start)
                    int tagSectionStart = headerSize;
                    int tagSectionEnd = zone.AssetPoolStartOffset;
                    int tagSectionSize = tagSectionEnd - tagSectionStart;

                    if (tagSectionSize > 0)
                    {
                        ms.Write(originalData, tagSectionStart, tagSectionSize);
                    }

                    // 3. Write new asset pool (only supported assets)
                    int newAssetPoolStart = (int)ms.Position;
                    bool isPC = fastFile.IsPC;
                    foreach (var record in supportedRecords)
                    {
                        // Get asset type value based on game/platform
                        int assetType;
                        if (fastFile.IsCod4File)
                            assetType = (int)record.AssetType_COD4;
                        else if (fastFile.IsCod5File && isPC)
                            assetType = (int)record.AssetType_COD5_PC;
                        else if (fastFile.IsCod5File && fastFile.IsXbox360)
                            assetType = (int)record.AssetType_COD5_Xbox360;
                        else if (fastFile.IsCod5File)
                            assetType = (int)record.AssetType_COD5;
                        else if (fastFile.IsMW2File)
                            assetType = (int)record.AssetType_MW2;
                        else
                            assetType = 0;

                        // Write asset type (4 bytes) - little-endian for PC, big-endian for console
                        if (isPC)
                        {
                            ms.WriteByte((byte)assetType);
                            ms.WriteByte(0x00);
                            ms.WriteByte(0x00);
                            ms.WriteByte(0x00);
                        }
                        else
                        {
                            ms.WriteByte(0x00);
                            ms.WriteByte(0x00);
                            ms.WriteByte(0x00);
                            ms.WriteByte((byte)assetType);
                        }

                        // Write pointer placeholder (FF FF FF FF)
                        ms.WriteByte(0xFF);
                        ms.WriteByte(0xFF);
                        ms.WriteByte(0xFF);
                        ms.WriteByte(0xFF);
                    }

                    // 4. Write asset pool end marker (FF FF FF FF)
                    ms.WriteByte(0xFF);
                    ms.WriteByte(0xFF);
                    ms.WriteByte(0xFF);
                    ms.WriteByte(0xFF);

                    int newAssetPoolEnd = (int)ms.Position;

                    // 5. Copy asset data for supported assets only
                    foreach (var record in supportedRecords)
                    {
                        if (record.HeaderStartOffset > 0 && record.AssetRecordEndOffset > record.HeaderStartOffset)
                        {
                            int dataStart = record.HeaderStartOffset;
                            int dataLength = record.AssetRecordEndOffset - record.HeaderStartOffset;

                            if (dataStart + dataLength <= originalData.Length)
                            {
                                ms.Write(originalData, dataStart, dataLength);
                            }
                            else
                            {
                                Debug.WriteLine($"[ZoneFileBuilder] Asset data out of bounds: start=0x{dataStart:X}, len={dataLength}");
                            }
                        }
                    }

                    // 6. Update header fields
                    byte[] newZoneData = ms.ToArray();

                    // Update asset count at offset 0x2C (big-endian)
                    uint newAssetCount = (uint)supportedRecords.Count;
                    WriteBigEndianUInt32(newZoneData, ZoneFileHeaderConstants.AssetCountOffset, newAssetCount);

                    // Update zone size at offset 0x00 (big-endian)
                    // Zone size is total size minus 4 (doesn't include the size field itself)
                    uint newZoneSize = (uint)(newZoneData.Length - 4);
                    WriteBigEndianUInt32(newZoneData, ZoneFileHeaderConstants.ZoneSizeOffset, newZoneSize);

                    Debug.WriteLine($"[ZoneFileBuilder] Rebuilt zone: {supportedRecords.Count} assets, {newZoneData.Length} bytes");
                    return newZoneData;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ZoneFileBuilder] Rebuild failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Filters the zone's asset records to only include supported types.
        /// This modifies the ZoneFileAssets.ZoneAssetRecords in place.
        /// </summary>
        public static void FilterToSupportedAssetsOnly(ZoneFile zone, FastFile fastFile)
        {
            if (zone.ZoneFileAssets?.ZoneAssetRecords == null)
                return;

            var originalCount = zone.ZoneFileAssets.ZoneAssetRecords.Count;
            var filteredRecords = GetSupportedAssetRecords(zone, fastFile);

            zone.ZoneFileAssets.ZoneAssetRecords = filteredRecords;

            Debug.WriteLine($"[ZoneFileBuilder] Filtered asset records: {originalCount} -> {filteredRecords.Count}");
        }

        private static void WriteBigEndianUInt32(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)((value >> 24) & 0xFF);
            data[offset + 1] = (byte)((value >> 16) & 0xFF);
            data[offset + 2] = (byte)((value >> 8) & 0xFF);
            data[offset + 3] = (byte)(value & 0xFF);
        }

        /// <summary>
        /// Builds a fresh zone file from parsed RawFileNodes and LocalizedEntries.
        /// This creates a new zone structure similar to FastFileCompiler.
        /// </summary>
        /// <param name="rawFileNodes">List of parsed raw file nodes.</param>
        /// <param name="localizedEntries">List of parsed localized entries.</param>
        /// <param name="fastFile">The FastFile for game version info.</param>
        /// <param name="zoneName">Optional zone name for footer.</param>
        /// <returns>New zone data as byte array, or null if build failed.</returns>
        public static byte[]? BuildFreshZone(
            List<RawFileNode> rawFileNodes,
            List<LocalizedEntry> localizedEntries,
            FastFile fastFile,
            string zoneName = "patch_mp")
        {
            // Need at least some content to build a zone
            if ((rawFileNodes == null || rawFileNodes.Count == 0) &&
                (localizedEntries == null || localizedEntries.Count == 0))
            {
                Debug.WriteLine("[ZoneFileBuilder] Cannot build: no raw files or localized entries provided.");
                return null;
            }

            // Ensure lists are not null
            rawFileNodes ??= new List<RawFileNode>();
            localizedEntries ??= new List<LocalizedEntry>();

            try
            {
                // Build sections
                var rawFilesSection = BuildRawFilesSection(rawFileNodes);
                var localizedSection = BuildLocalizedSection(localizedEntries);
                var assetTableSection = BuildAssetTableSection(rawFileNodes.Count, localizedEntries?.Count ?? 0, fastFile);
                var footerSection = BuildFooterSection(zoneName);

                // Calculate sizes for header
                int assetTableSize = assetTableSection.Length;
                int rawFilesSize = rawFilesSection.Length;
                int localizedSize = localizedSection.Length;
                int footerSize = footerSection.Length;

                // Asset count includes: raw files + localized entries + 1 final entry
                int totalAssetCount = rawFileNodes.Count + (localizedEntries?.Count ?? 0) + 1;
                var headerSection = BuildHeaderSection(assetTableSize, rawFilesSize, localizedSize, footerSize, totalAssetCount, fastFile);

                // Combine all sections
                using (var ms = new MemoryStream())
                {
                    ms.Write(headerSection, 0, headerSection.Length);
                    ms.Write(assetTableSection, 0, assetTableSection.Length);
                    ms.Write(rawFilesSection, 0, rawFilesSection.Length);
                    ms.Write(localizedSection, 0, localizedSection.Length);
                    ms.Write(footerSection, 0, footerSection.Length);

                    // Pad to 64KB boundary
                    int currentSize = (int)ms.Length;
                    int blockSize = 0x10000; // 64KB
                    int padding = ((currentSize / blockSize) + 1) * blockSize - currentSize;
                    ms.Write(new byte[padding], 0, padding);

                    byte[] zoneData = ms.ToArray();
                    Debug.WriteLine($"[ZoneFileBuilder] Built fresh zone: {rawFileNodes.Count} rawfiles, {localizedEntries?.Count ?? 0} localized, {zoneData.Length} bytes");
                    return zoneData;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ZoneFileBuilder] Build failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Builds the zone header (52 bytes).
        /// </summary>
        private static byte[] BuildHeaderSection(int assetTableSize, int rawFilesSize, int localizedSize, int footerSize, int assetCount, FastFile? fastFile = null)
        {
            var header = new List<byte>();

            // Calculate total sizes
            int totalDataSize = assetTableSize + rawFilesSize + localizedSize + footerSize + 16;
            int totalZoneSize = 52 + assetTableSize + rawFilesSize + localizedSize + footerSize;

            // Get memory allocation values based on game version
            // WaW: MemAlloc1 = 0x10B0, MemAlloc2 = 0x05F8F0
            // CoD4: MemAlloc1 = 0x0F70, MemAlloc2 = 0x000000
            byte[] memAlloc1;
            byte[] memAlloc2;

            if (fastFile?.IsCod4File == true)
            {
                memAlloc1 = new byte[] { 0x00, 0x00, 0x0F, 0x70 };
                memAlloc2 = new byte[] { 0x00, 0x00, 0x00, 0x00 };
            }
            else if (fastFile?.IsMW2File == true)
            {
                memAlloc1 = new byte[] { 0x00, 0x00, 0x03, 0xB4 };
                memAlloc2 = new byte[] { 0x00, 0x00, 0x10, 0x00 };
            }
            else // Default to WaW
            {
                memAlloc1 = new byte[] { 0x00, 0x00, 0x10, 0xB0 };
                memAlloc2 = new byte[] { 0x00, 0x05, 0xF8, 0xF0 };
            }

            // Bytes 0-3: Total data size (big-endian)
            header.AddRange(GetBigEndianBytes(totalDataSize));

            // Bytes 4-23: Memory allocation block 1 (20 bytes, memAlloc1 at offset 4)
            byte[] allocBlock1 = new byte[20];
            memAlloc1.CopyTo(allocBlock1, 4); // Copy memAlloc1 to bytes 8-11 of final header
            header.AddRange(allocBlock1);

            // Bytes 24-27: Total zone size (big-endian)
            header.AddRange(GetBigEndianBytes(totalZoneSize));

            // Bytes 28-43: Memory allocation block 2 (16 bytes, memAlloc2 at offset 4)
            byte[] allocBlock2 = new byte[16];
            memAlloc2.CopyTo(allocBlock2, 4); // Copy memAlloc2 to bytes 32-35 of final header
            header.AddRange(allocBlock2);

            // Bytes 44-47: Asset count (big-endian)
            header.AddRange(GetBigEndianBytes(assetCount));

            // Bytes 48-51: 0xFFFFFFFF marker
            header.AddRange(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });

            return header.ToArray();
        }

        /// <summary>
        /// Builds the asset table section.
        /// Each asset entry is 8 bytes: 00 00 00 [type] FF FF FF FF
        /// </summary>
        private static byte[] BuildAssetTableSection(int rawFileCount, int localizedCount, FastFile fastFile)
        {
            var table = new List<byte>();

            byte rawFileType = fastFile.IsCod4File
                ? (byte)CoD4AssetType.rawfile
                : (byte)CoD5AssetTypePS3.rawfile;

            byte localizeType = fastFile.IsCod4File
                ? (byte)CoD4AssetType.localize
                : (byte)CoD5AssetTypePS3.localize;

            // Entry for each raw file
            for (int i = 0; i < rawFileCount; i++)
            {
                table.AddRange(new byte[] { 0x00, 0x00, 0x00, rawFileType, 0xFF, 0xFF, 0xFF, 0xFF });
            }

            // Entry for each localized string
            for (int i = 0; i < localizedCount; i++)
            {
                table.AddRange(new byte[] { 0x00, 0x00, 0x00, localizeType, 0xFF, 0xFF, 0xFF, 0xFF });
            }

            // Final rawfile entry (required by format)
            table.AddRange(new byte[] { 0x00, 0x00, 0x00, rawFileType, 0xFF, 0xFF, 0xFF, 0xFF });

            return table.ToArray();
        }

        /// <summary>
        /// Builds the raw files section.
        /// Each raw file: FF FF FF FF + [size] + FF FF FF FF + [name\0] + [data] + [\0]
        /// </summary>
        private static byte[] BuildRawFilesSection(List<RawFileNode> rawFileNodes)
        {
            var section = new List<byte>();

            foreach (var node in rawFileNodes)
            {
                // Marker: FF FF FF FF
                section.AddRange(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });

                // Data size (big-endian) - use the actual content length
                int dataSize = node.RawFileBytes?.Length ?? 0;
                section.AddRange(GetBigEndianBytes(dataSize));

                // Pointer placeholder: FF FF FF FF
                section.AddRange(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });

                // Filename (null-terminated)
                section.AddRange(Encoding.ASCII.GetBytes(node.FileName ?? "unknown"));
                section.Add(0x00);

                // Raw data
                if (node.RawFileBytes != null && node.RawFileBytes.Length > 0)
                {
                    section.AddRange(node.RawFileBytes);
                }

                // Null terminator
                section.Add(0x00);
            }

            return section.ToArray();
        }

        /// <summary>
        /// Builds the localized strings section.
        /// Each entry: FF FF FF FF FF FF FF FF + [value\0] + [reference\0]
        /// </summary>
        private static byte[] BuildLocalizedSection(List<LocalizedEntry>? localizedEntries)
        {
            var section = new List<byte>();

            if (localizedEntries == null)
                return section.ToArray();

            foreach (var entry in localizedEntries)
            {
                // Marker: FF FF FF FF FF FF FF FF
                section.AddRange(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF });

                // Localized value (null-terminated) - use raw bytes directly
                var textBytes = entry.TextBytes ?? Array.Empty<byte>();
                var keyBytes = entry.KeyBytes ?? Array.Empty<byte>();

                Debug.WriteLine($"[BuildLocalizedSection] Key={entry.Key}, TextLen={textBytes.Length}, Text='{entry.LocalizedText?.Substring(0, Math.Min(50, entry.LocalizedText?.Length ?? 0))}'");

                section.AddRange(textBytes);
                section.Add(0x00);

                // Reference key (null-terminated) - use raw bytes directly
                section.AddRange(keyBytes);
                section.Add(0x00);
            }

            return section.ToArray();
        }

        /// <summary>
        /// Builds the footer section.
        /// </summary>
        private static byte[] BuildFooterSection(string zoneName)
        {
            var footer = new List<byte>();

            // CoD4/WaW footer: 12 bytes
            footer.AddRange(new byte[]
            {
                0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00,
                0xFF, 0xFF, 0xFF, 0xFF
            });

            // Zone name (null-terminated with extra null)
            footer.AddRange(Encoding.ASCII.GetBytes(zoneName));
            footer.AddRange(new byte[] { 0x00, 0x00 });

            return footer.ToArray();
        }

        /// <summary>
        /// Converts an int to big-endian bytes.
        /// </summary>
        private static byte[] GetBigEndianBytes(int value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            return bytes;
        }
    }
}
