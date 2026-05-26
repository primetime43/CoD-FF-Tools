using Call_of_Duty_FastFile_Editor.GameDefinitions;
using Call_of_Duty_FastFile_Editor.Models;
using FastFileLib;
using FastFileLib.GameDefinitions;
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
        /// Supported asset types for COD4 (PS3).
        /// </summary>
        private static readonly HashSet<CoD4AssetTypePS3> SupportedTypesCOD4 = new HashSet<CoD4AssetTypePS3>
        {
            CoD4AssetTypePS3.rawfile,
            CoD4AssetTypePS3.localize
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
        /// Supported asset types for MW2 (PS3).
        /// </summary>
        private static readonly HashSet<MW2AssetTypePS3> SupportedTypesMW2 = new HashSet<MW2AssetTypePS3>
        {
            MW2AssetTypePS3.rawfile,
            MW2AssetTypePS3.localize
        };

        /// <summary>
        /// Supported asset types for MW2 (Xbox 360).
        /// </summary>
        private static readonly HashSet<MW2AssetTypeXbox360> SupportedTypesMW2Xbox360 = new HashSet<MW2AssetTypeXbox360>
        {
            MW2AssetTypeXbox360.rawfile,
            MW2AssetTypeXbox360.localize
        };

        /// <summary>
        /// Supported asset types for MW2 (PC).
        /// </summary>
        private static readonly HashSet<MW2AssetTypePC> SupportedTypesMW2PC = new HashSet<MW2AssetTypePC>
        {
            MW2AssetTypePC.rawfile,
            MW2AssetTypePC.localize
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
                if (fastFile.IsMW2File && fastFile.IsPC && !SupportedTypesMW2PC.Contains(record.AssetType_MW2_PC))
                    return false;
                if (fastFile.IsMW2File && fastFile.IsXbox360 && !SupportedTypesMW2Xbox360.Contains(record.AssetType_MW2_Xbox360))
                    return false;
                if (fastFile.IsMW2File && !fastFile.IsPC && !fastFile.IsXbox360 && !SupportedTypesMW2.Contains(record.AssetType_MW2))
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
                else if (fastFile.IsMW2File && fastFile.IsPC)
                    isSupported = SupportedTypesMW2PC.Contains(record.AssetType_MW2_PC);
                else if (fastFile.IsMW2File && fastFile.IsXbox360)
                    isSupported = SupportedTypesMW2Xbox360.Contains(record.AssetType_MW2_Xbox360);
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
                else if (fastFile.IsMW2File && fastFile.IsPC)
                {
                    isSupported = SupportedTypesMW2PC.Contains(record.AssetType_MW2_PC);
                    typeName = record.AssetType_MW2_PC.ToString();
                }
                else if (fastFile.IsMW2File && fastFile.IsXbox360)
                {
                    isSupported = SupportedTypesMW2Xbox360.Contains(record.AssetType_MW2_Xbox360);
                    typeName = record.AssetType_MW2_Xbox360.ToString();
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

                    // 1. Copy header (size dispatched per game/platform: 48/52/56 bytes)
                    int headerSize = FastFileConstants.GetZoneHeaderSize(
                        fastFile.GameVersionEnum, fastFile.IsXbox360, fastFile.IsPC, fastFile.IsWii);
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

                        // Write asset entry in [ptr][type] format for console, [type][ptr] for PC
                        // Console mod files use: FF FF FF FF 00 00 00 XX
                        // PC uses: XX 00 00 00 FF FF FF FF
                        if (isPC)
                        {
                            // PC: [type][ptr] - little-endian type
                            ms.WriteByte((byte)assetType);
                            ms.WriteByte(0x00);
                            ms.WriteByte(0x00);
                            ms.WriteByte(0x00);
                            ms.WriteByte(0xFF);
                            ms.WriteByte(0xFF);
                            ms.WriteByte(0xFF);
                            ms.WriteByte(0xFF);
                        }
                        else
                        {
                            // Console: [ptr][type] - big-endian type
                            ms.WriteByte(0xFF);
                            ms.WriteByte(0xFF);
                            ms.WriteByte(0xFF);
                            ms.WriteByte(0xFF);
                            ms.WriteByte(0x00);
                            ms.WriteByte(0x00);
                            ms.WriteByte(0x00);
                            ms.WriteByte((byte)assetType);
                        }
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

                    // 6. Update header fields (offsets + endianness dispatched per platform)
                    byte[] newZoneData = ms.ToArray();
                    int assetCountOffset = FastFileConstants.GetAssetCountOffset(
                        fastFile.GameVersionEnum, fastFile.IsXbox360, fastFile.IsPC, fastFile.IsWii);

                    uint newAssetCount = (uint)supportedRecords.Count;
                    // ZoneSize is total size minus 4 (the size field itself isn't counted)
                    uint newZoneSize = (uint)(newZoneData.Length - 4);

                    if (fastFile.IsPC)
                    {
                        WriteLittleEndianUInt32(newZoneData, assetCountOffset, newAssetCount);
                        WriteLittleEndianUInt32(newZoneData, FastFileConstants.ZoneSizeOffset, newZoneSize);
                    }
                    else
                    {
                        WriteBigEndianUInt32(newZoneData, assetCountOffset, newAssetCount);
                        WriteBigEndianUInt32(newZoneData, FastFileConstants.ZoneSizeOffset, newZoneSize);
                    }

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

        private static void WriteLittleEndianUInt32(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)(value & 0xFF);
            data[offset + 1] = (byte)((value >> 8) & 0xFF);
            data[offset + 2] = (byte)((value >> 16) & 0xFF);
            data[offset + 3] = (byte)((value >> 24) & 0xFF);
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
                // Delegate to the platform-aware FastFileLib.ZoneBuilder instead of building the
                // header/asset-table/rawfile sections by hand. The old hand-rolled builder was
                // big-endian + console-only: it hardcoded a 48-byte MW2 header (wrong for MW2 PC's
                // 56-byte layout), wrote BE MemAlloc magic constants (wrong for PC/Wii which use
                // per-zone little-endian values), and never handled PC byte order at all. The lib
                // builder gets header size, endianness, MemAlloc, asset type IDs, and MW2 rawfile
                // framing right for every (game, platform) pair.
                string platform = fastFile.IsPC ? "PC"
                    : fastFile.IsWii ? "Wii"
                    : fastFile.IsXbox360 ? "Xbox360"
                    : "PS3";

                var builder = new FastFileLib.ZoneBuilder(fastFile.GameVersionEnum, zoneName, platform);
                foreach (var node in rawFileNodes)
                    builder.AddRawFile(new FastFileLib.Models.RawFile(
                        node.FileName ?? "unknown", node.RawFileBytes ?? Array.Empty<byte>()));
                foreach (var entry in localizedEntries)
                    builder.AddLocalizedEntry(new FastFileLib.Models.LocalizedEntry(entry.Key, entry.LocalizedText));

                // PC and Wii use per-zone MemAlloc values (not the console magic constants), so
                // preserve them from the original loaded zone to keep the engine-correct sizes.
                // Same platform here (you load a PC file and rebuild it as PC), so the shared
                // reader handles byte order + the missing-vertex-slot rule.
                if (fastFile.IsPC || fastFile.IsWii)
                {
                    byte[]? srcZone = fastFile.OpenedFastFileZone?.Data;
                    if (srcZone != null)
                    {
                        var (memTemp, memVertex) = FastFileLib.FastFileConstants.ReadZoneMemAlloc(
                            srcZone, fastFile.GameVersionEnum,
                            isXbox360: false, isPC: fastFile.IsPC, isWii: fastFile.IsWii);
                        if (memTemp.HasValue) builder.WithBlockSizeTemp(memTemp);
                        if (memVertex.HasValue) builder.WithBlockSizeVertex(memVertex);
                    }
                }

                byte[] zoneData = builder.Build();
                Debug.WriteLine($"[ZoneFileBuilder] Built fresh zone via FastFileLib.ZoneBuilder ({platform}): " +
                    $"{rawFileNodes.Count} rawfiles, {localizedEntries.Count} localized, {zoneData.Length} bytes");
                return zoneData;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ZoneFileBuilder] Build failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Transfers allocated space from one raw file to another by modifying the zone in-place.
        /// This preserves ALL assets in the zone, not just raw files and localized entries.
        /// </summary>
        /// <param name="zoneData">The original zone data.</param>
        /// <param name="donor">The raw file giving up space.</param>
        /// <param name="recipient">The raw file receiving space.</param>
        /// <param name="bytesToTransfer">Number of bytes to transfer.</param>
        /// <param name="allRawFiles">All raw file nodes for position tracking.</param>
        /// <returns>Modified zone data, or null if transfer failed.</returns>
        public static byte[]? TransferSpaceInPlace(
            byte[] zoneData,
            RawFileNode donor,
            RawFileNode recipient,
            int bytesToTransfer,
            List<RawFileNode> allRawFiles)
        {
            if (zoneData == null || donor == null || recipient == null || allRawFiles == null)
            {
                Debug.WriteLine("[ZoneFileBuilder] TransferSpaceInPlace: Invalid parameters.");
                return null;
            }

            // Validate transfer amount
            int donorFreeSpace = donor.MaxSize - (donor.RawFileBytes?.Length ?? 0);
            if (bytesToTransfer > donorFreeSpace)
            {
                Debug.WriteLine($"[ZoneFileBuilder] Transfer amount ({bytesToTransfer}) exceeds donor free space ({donorFreeSpace}).");
                return null;
            }

            try
            {
                // Sort raw files by their position in the zone
                var sortedFiles = allRawFiles.OrderBy(f => f.CodeStartPosition).ToList();

                // Find positions of donor and recipient
                int donorIndex = sortedFiles.FindIndex(f => f.FileName == donor.FileName);
                int recipientIndex = sortedFiles.FindIndex(f => f.FileName == recipient.FileName);

                if (donorIndex < 0 || recipientIndex < 0)
                {
                    Debug.WriteLine("[ZoneFileBuilder] Could not find donor or recipient in file list.");
                    return null;
                }

                Debug.WriteLine($"[TransferSpaceInPlace] Donor '{donor.FileName}' at index {donorIndex}, position 0x{donor.CodeStartPosition:X}");
                Debug.WriteLine($"[TransferSpaceInPlace] Recipient '{recipient.FileName}' at index {recipientIndex}, position 0x{recipient.CodeStartPosition:X}");
                Debug.WriteLine($"[TransferSpaceInPlace] Transferring {bytesToTransfer} bytes");

                // Create output buffer - same size as input since we're just moving space around
                byte[] newZoneData = new byte[zoneData.Length];

                // Calculate the positions where we need to make changes
                // Donor's content ends at CodeStartPosition + MaxSize (plus null byte)
                // Recipient's content ends at CodeStartPosition + MaxSize (plus null byte)

                int donorContentEnd = donor.CodeStartPosition + donor.MaxSize; // End of donor's allocated space
                int recipientContentEnd = recipient.CodeStartPosition + recipient.MaxSize; // End of recipient's allocated space

                if (donorIndex < recipientIndex)
                {
                    // Donor is before recipient in the zone
                    // 1. Copy everything up to donor's new end position
                    // 2. Shift data between donor and recipient LEFT by bytesToTransfer
                    // 3. Copy recipient with expanded size
                    // 4. Copy everything after recipient

                    int newDonorContentEnd = donorContentEnd - bytesToTransfer;

                    // Copy everything before donor's content end change
                    Array.Copy(zoneData, 0, newZoneData, 0, newDonorContentEnd);

                    // Update donor's size in header (at StartOfFileHeader + 4)
                    int newDonorSize = donor.MaxSize - bytesToTransfer;
                    WriteBigEndianUInt32(newZoneData, donor.StartOfFileHeader + 4, (uint)newDonorSize);

                    // Copy data from after donor's old end to recipient's content start, shifted left
                    int shiftSourceStart = donorContentEnd;
                    int shiftDestStart = newDonorContentEnd;
                    int shiftLength = recipient.CodeStartPosition - donorContentEnd;

                    if (shiftLength > 0)
                    {
                        Array.Copy(zoneData, shiftSourceStart, newZoneData, shiftDestStart, shiftLength);
                    }

                    // Calculate recipient's new position (shifted left)
                    int newRecipientHeaderStart = recipient.StartOfFileHeader - bytesToTransfer;
                    int newRecipientCodeStart = recipient.CodeStartPosition - bytesToTransfer;

                    // Copy recipient's header and filename (shifted left)
                    int recipientHeaderAndNameLen = recipient.CodeStartPosition - recipient.StartOfFileHeader;
                    Array.Copy(zoneData, recipient.StartOfFileHeader, newZoneData, newRecipientHeaderStart, recipientHeaderAndNameLen);

                    // Update recipient's size in header
                    int newRecipientSize = recipient.MaxSize + bytesToTransfer;
                    WriteBigEndianUInt32(newZoneData, newRecipientHeaderStart + 4, (uint)newRecipientSize);

                    // Copy recipient's original content
                    Array.Copy(zoneData, recipient.CodeStartPosition, newZoneData, newRecipientCodeStart, recipient.MaxSize);

                    // Fill the extra space with nulls (the transferred bytes)
                    int extraSpaceStart = newRecipientCodeStart + recipient.MaxSize;
                    for (int i = 0; i < bytesToTransfer; i++)
                    {
                        newZoneData[extraSpaceStart + i] = 0x00;
                    }

                    // Copy null terminator
                    newZoneData[extraSpaceStart + bytesToTransfer] = 0x00;

                    // Copy everything after recipient (no shift needed, total size unchanged)
                    int afterRecipientSrc = recipientContentEnd + 1; // +1 for null terminator
                    int afterRecipientDst = extraSpaceStart + bytesToTransfer + 1;
                    int afterRecipientLen = zoneData.Length - afterRecipientSrc;

                    if (afterRecipientLen > 0 && afterRecipientDst + afterRecipientLen <= newZoneData.Length)
                    {
                        Array.Copy(zoneData, afterRecipientSrc, newZoneData, afterRecipientDst, afterRecipientLen);
                    }
                }
                else
                {
                    // Recipient is before donor in the zone
                    // 1. Copy everything up to recipient's content end
                    // 2. Expand recipient's allocated area
                    // 3. Shift data between recipient and donor RIGHT by bytesToTransfer
                    // 4. Shrink donor's allocated area
                    // 5. Copy everything after donor

                    // Copy everything up to recipient's header
                    Array.Copy(zoneData, 0, newZoneData, 0, recipient.StartOfFileHeader);

                    // Copy recipient header with updated size
                    int recipientHeaderLen = recipient.CodeStartPosition - recipient.StartOfFileHeader;
                    Array.Copy(zoneData, recipient.StartOfFileHeader, newZoneData, recipient.StartOfFileHeader, recipientHeaderLen);

                    int newRecipientSize = recipient.MaxSize + bytesToTransfer;
                    WriteBigEndianUInt32(newZoneData, recipient.StartOfFileHeader + 4, (uint)newRecipientSize);

                    // Copy recipient's original content
                    Array.Copy(zoneData, recipient.CodeStartPosition, newZoneData, recipient.CodeStartPosition, recipient.MaxSize);

                    // Add extra space (transferred bytes) as nulls
                    int extraSpaceStart = recipient.CodeStartPosition + recipient.MaxSize;
                    for (int i = 0; i < bytesToTransfer; i++)
                    {
                        newZoneData[extraSpaceStart + i] = 0x00;
                    }
                    newZoneData[extraSpaceStart + bytesToTransfer] = 0x00; // null terminator

                    // Copy data between recipient and donor, shifted RIGHT
                    int shiftSourceStart = recipientContentEnd + 1;
                    int shiftDestStart = extraSpaceStart + bytesToTransfer + 1;
                    int shiftLength = donor.StartOfFileHeader - (recipientContentEnd + 1);

                    if (shiftLength > 0)
                    {
                        Array.Copy(zoneData, shiftSourceStart, newZoneData, shiftDestStart, shiftLength);
                    }

                    // Calculate donor's new position (shifted right)
                    int newDonorHeaderStart = donor.StartOfFileHeader + bytesToTransfer;
                    int newDonorCodeStart = donor.CodeStartPosition + bytesToTransfer;

                    // Copy donor's header and filename (shifted right)
                    int donorHeaderAndNameLen = donor.CodeStartPosition - donor.StartOfFileHeader;
                    Array.Copy(zoneData, donor.StartOfFileHeader, newZoneData, newDonorHeaderStart, donorHeaderAndNameLen);

                    // Update donor's size in header
                    int newDonorSize = donor.MaxSize - bytesToTransfer;
                    WriteBigEndianUInt32(newZoneData, newDonorHeaderStart + 4, (uint)newDonorSize);

                    // Copy donor's content (only up to new size)
                    Array.Copy(zoneData, donor.CodeStartPosition, newZoneData, newDonorCodeStart, newDonorSize);
                    newZoneData[newDonorCodeStart + newDonorSize] = 0x00; // null terminator

                    // Copy everything after donor (positions stay the same since total size unchanged)
                    int afterDonorSrc = donorContentEnd + 1;
                    int afterDonorDst = newDonorCodeStart + newDonorSize + 1;
                    int afterDonorLen = zoneData.Length - afterDonorSrc;

                    if (afterDonorLen > 0 && afterDonorDst + afterDonorLen <= newZoneData.Length)
                    {
                        Array.Copy(zoneData, afterDonorSrc, newZoneData, afterDonorDst, afterDonorLen);
                    }
                }

                Debug.WriteLine($"[TransferSpaceInPlace] Transfer complete. Zone size: {newZoneData.Length} bytes");
                return newZoneData;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ZoneFileBuilder] TransferSpaceInPlace failed: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }
    }
}
