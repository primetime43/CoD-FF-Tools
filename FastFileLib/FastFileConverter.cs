using System.Text;
using FastFileLib.GameDefinitions;
using FastFileLib.Models;

namespace FastFileLib;

/// <summary>
/// Supported platforms for FastFile conversion.
/// </summary>
public enum Platform
{
    PS3,
    Xbox360,
    PC,
    Wii
}

/// <summary>
/// Result of a FastFile conversion operation.
/// </summary>
public class ConversionResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string SourcePlatform { get; set; } = "";
    public string TargetPlatform { get; set; } = "";
    public GameVersion GameVersion { get; set; }
    public bool WasSignedFile { get; set; }
    public int BlocksProcessed { get; set; }
    public long OriginalSize { get; set; }
    public long ConvertedSize { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<string> ReplacedFiles { get; set; } = new();
    public List<string> SkippedFiles { get; set; } = new();
}

/// <summary>
/// Converts FastFiles between platforms (PS3, Xbox 360, PC).
/// </summary>
public static class FastFileConverter
{
    /// <summary>
    /// Converts a FastFile from one platform to another.
    /// </summary>
    /// <param name="inputPath">Path to source FastFile</param>
    /// <param name="outputPath">Path for converted FastFile</param>
    /// <param name="targetPlatform">Target platform</param>
    /// <returns>Conversion result with details</returns>
    public static ConversionResult Convert(string inputPath, string outputPath, Platform targetPlatform)
    {
        var result = new ConversionResult();

        try
        {
            // Read source file info
            var sourceInfo = FastFileInfo.FromFile(inputPath);
            result.GameVersion = sourceInfo.GameVersion;
            result.WasSignedFile = sourceInfo.IsSigned;
            result.SourcePlatform = DetectPlatform(sourceInfo);
            result.TargetPlatform = targetPlatform.ToString();
            result.OriginalSize = new FileInfo(inputPath).Length;

            // Check for signed files
            if (sourceInfo.IsSigned)
            {
                result.Warnings.Add("Source file is signed (Xbox 360 MP). Converting to unsigned format.");
            }

            // Check for PC source (little-endian zone data)
            if (result.SourcePlatform == "PC")
            {
                result.Warnings.Add("PC FastFiles use little-endian zone data. Conversion may not work correctly for all asset types.");
            }

            // Check for PC target
            if (targetPlatform == Platform.PC)
            {
                result.Warnings.Add("Converting to PC requires little-endian zone data. Only rawfiles/localization will work correctly.");
            }

            // Create temp file for zone data
            string tempZonePath = Path.GetTempFileName();

            try
            {
                // Decompress to zone
                int blocksDecompressed = DecompressWithSignatureHandling(inputPath, tempZonePath, sourceInfo);
                result.BlocksProcessed = blocksDecompressed;

                // Patch zone header for target platform (memory allocation values differ between platforms)
                // Also replaces platform-specific asset references (xenon_ -> ps3_, etc.)
                int assetReplacements = PatchZoneHeaderForPlatform(tempZonePath, sourceInfo.GameVersion, targetPlatform);
                result.Warnings.Add("Zone header memory allocation values patched for target platform.");

                if (assetReplacements > 0)
                {
                    result.Warnings.Add($"Replaced {assetReplacements} platform-specific asset reference(s) (e.g., xenon_controller -> ps3_controller).");
                }

                // Recompress for target platform
                CompressForPlatform(tempZonePath, outputPath, sourceInfo.GameVersion, targetPlatform);

                result.ConvertedSize = new FileInfo(outputPath).Length;
                result.Success = true;
                result.Message = $"Successfully converted {Path.GetFileName(inputPath)} from {result.SourcePlatform} to {result.TargetPlatform}";
            }
            finally
            {
                // Clean up temp file
                if (File.Exists(tempZonePath))
                    File.Delete(tempZonePath);
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Conversion failed: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Converts a mod FastFile by building a fresh PS3 zone from the extracted raw files.
    /// This approach extracts raw files from the source mod and builds a clean PS3 zone
    /// using ZoneBuilder (same as the FFCompiler tool).
    /// </summary>
    /// <param name="sourceModPath">Path to the source mod FastFile (Xbox/PC)</param>
    /// <param name="basePs3ZonePath">Not used in this version - kept for API compatibility</param>
    /// <param name="outputPath">Path for the converted FastFile</param>
    /// <param name="zoneName">Optional zone name override (if null, auto-detected from filename)</param>
    /// <returns>Conversion result with details</returns>
    public static ConversionResult ConvertUsingBaseZone(string sourceModPath, string basePs3ZonePath, string outputPath, string? zoneName = null)
    {
        var result = new ConversionResult();
        result.TargetPlatform = "PS3";

        try
        {
            // Validate inputs
            if (!File.Exists(sourceModPath))
                throw new FileNotFoundException($"Source mod file not found: {sourceModPath}");

            // Read source mod info
            var sourceInfo = FastFileInfo.FromFile(sourceModPath);
            result.GameVersion = sourceInfo.GameVersion;
            result.WasSignedFile = sourceInfo.IsSigned;
            result.SourcePlatform = DetectPlatform(sourceInfo);
            result.OriginalSize = new FileInfo(sourceModPath).Length;

            if (sourceInfo.IsSigned)
            {
                result.Warnings.Add("Source file is signed (Xbox 360 MP). Extracting from unsigned portion.");
            }

            // Create temp files
            string tempSourceZonePath = Path.GetTempFileName();
            string tempNewZonePath = Path.GetTempFileName();

            try
            {
                // Step 1: Decompress source mod to zone
                result.Warnings.Add("Decompressing source mod...");
                int blocksDecompressed = DecompressWithSignatureHandling(sourceModPath, tempSourceZonePath, sourceInfo);
                result.BlocksProcessed = blocksDecompressed;

                // Step 2: Extract raw files from source mod zone
                result.Warnings.Add("Extracting raw files from source mod...");
                byte[] sourceZoneData = File.ReadAllBytes(tempSourceZonePath);
                var rawFiles = ExtractRawFilesFromZone(sourceZoneData);
                result.Warnings.Add($"Found {rawFiles.Count} raw files in source mod.");

                if (rawFiles.Count == 0)
                {
                    throw new InvalidOperationException("No raw files found in source mod. Cannot convert.");
                }

                // Step 3: Build a fresh PS3 zone using ZoneBuilder (same approach as FFCompiler)
                result.Warnings.Add("Building fresh PS3 zone with extracted raw files...");
                // Use provided zone name or auto-detect from input filename
                string effectiveZoneName = !string.IsNullOrWhiteSpace(zoneName)
                    ? zoneName
                    : GetZoneNameFromPath(sourceModPath);
                result.Warnings.Add($"Using zone name: {effectiveZoneName}");
                var zoneBuilder = new ZoneBuilder(result.GameVersion, effectiveZoneName);
                zoneBuilder.AddRawFiles(rawFiles);
                byte[] newZone = zoneBuilder.Build();

                result.Warnings.Add($"Built new zone with {rawFiles.Count} raw files ({newZone.Length} bytes).");

                // Save new zone
                File.WriteAllBytes(tempNewZonePath, newZone);

                // Track all files as "replaced" (they're all included in the new zone)
                result.ReplacedFiles = rawFiles.Select(f => f.Name).ToList();

                // Step 4: Compress new zone to PS3 FastFile
                result.Warnings.Add("Compressing to PS3 FastFile...");
                CompressForPlatform(tempNewZonePath, outputPath, result.GameVersion, Platform.PS3);

                result.ConvertedSize = new FileInfo(outputPath).Length;
                result.Success = true;
                result.Message = $"Successfully converted {Path.GetFileName(sourceModPath)}. " +
                                $"Built fresh PS3 zone with {rawFiles.Count} raw files.";
            }
            finally
            {
                // Clean up temp files
                if (File.Exists(tempSourceZonePath))
                    File.Delete(tempSourceZonePath);
                if (File.Exists(tempNewZonePath))
                    File.Delete(tempNewZonePath);
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Conversion failed: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Extracts all raw files from a zone file.
    /// </summary>
    private static List<RawFile> ExtractRawFilesFromZone(byte[] zoneData)
    {
        var rawFiles = new List<RawFile>();
        var validExtensions = new[] { ".cfg", ".gsc", ".atr", ".csc", ".rmb", ".arena", ".vision", ".txt", ".str", ".menu" };
        var foundOffsets = new HashSet<int>();

        foreach (var ext in validExtensions)
        {
            byte[] pattern = Encoding.ASCII.GetBytes(ext + "\0");

            for (int i = 0; i <= zoneData.Length - pattern.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (zoneData[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (!match) continue;

                // Find the FF FF FF FF marker before the filename
                int markerEnd = i - 1;
                while (markerEnd >= 4)
                {
                    if (zoneData[markerEnd] == 0xFF &&
                        zoneData[markerEnd - 1] == 0xFF &&
                        zoneData[markerEnd - 2] == 0xFF &&
                        zoneData[markerEnd - 3] == 0xFF)
                        break;
                    markerEnd--;
                    if (i - markerEnd > 300)
                    {
                        markerEnd = -1;
                        break;
                    }
                }

                if (markerEnd < 4) continue;
                if (zoneData[markerEnd + 1] == 0x00) continue;

                int sizeOffset = markerEnd - 7;
                if (sizeOffset < 0) continue;

                int headerOffset = sizeOffset - 4;
                if (headerOffset < 0) continue;
                if (foundOffsets.Contains(headerOffset)) continue;

                // Read size (big-endian)
                int size = (zoneData[sizeOffset] << 24) |
                          (zoneData[sizeOffset + 1] << 16) |
                          (zoneData[sizeOffset + 2] << 8) |
                          zoneData[sizeOffset + 3];

                if (size <= 0 || size > 10_000_000) continue;

                // Read filename
                int nameStart = markerEnd + 1;
                int nameEnd = nameStart;
                while (nameEnd < zoneData.Length && zoneData[nameEnd] != 0)
                    nameEnd++;

                if (nameEnd <= nameStart) continue;

                string name = Encoding.ASCII.GetString(zoneData, nameStart, nameEnd - nameStart);
                if (!name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) continue;

                // Read data
                int dataOffset = nameEnd + 1;
                if (dataOffset + size > zoneData.Length) continue;

                byte[] data = new byte[size];
                Array.Copy(zoneData, dataOffset, data, 0, size);

                rawFiles.Add(new RawFile(name, data));
                foundOffsets.Add(headerOffset);
            }
        }

        return rawFiles;
    }

    /// <summary>
    /// Analyzes a FastFile and returns information about it.
    /// </summary>
    public static FastFileAnalysis Analyze(string inputPath)
    {
        var analysis = new FastFileAnalysis();

        try
        {
            var info = FastFileInfo.FromFile(inputPath);
            analysis.IsValid = true;
            analysis.Magic = info.Magic;
            analysis.GameVersion = info.GameVersion;
            analysis.GameName = info.GameName;
            analysis.IsSigned = info.IsSigned;
            analysis.DetectedPlatform = DetectPlatform(info);
            analysis.FileSize = new FileInfo(inputPath).Length;
            analysis.CanConvertToPS3 = true;
            analysis.CanConvertToXbox360 = true;
            analysis.CanConvertToPC = info.GameVersion != GameVersion.Unknown;

            // Add notes
            if (info.IsSigned)
            {
                analysis.Notes.Add("Signed Xbox 360 MP file - will be converted to unsigned");
            }

            if (analysis.DetectedPlatform == "PC")
            {
                analysis.Notes.Add("PC files use little-endian - console conversion may have issues with complex assets");
            }

            // Check for unsupported versions
            if (info.GameVersion == GameVersion.Unknown)
            {
                analysis.IsValid = false;
                analysis.Notes.Add($"Unknown game version: 0x{info.Version:X}");
                analysis.CanConvertToPS3 = false;
                analysis.CanConvertToXbox360 = false;
                analysis.CanConvertToPC = false;
            }
        }
        catch (Exception ex)
        {
            analysis.IsValid = false;
            analysis.Notes.Add($"Error reading file: {ex.Message}");
        }

        return analysis;
    }

    /// <summary>
    /// Detects the source platform from FastFile info.
    /// </summary>
    private static string DetectPlatform(FastFileInfo info)
    {
        // PC has different version numbers
        if (info.Version == FastFileInfo.CoD4_PC_Version ||
            info.Version == FastFileInfo.MW2_PC_Version)
        {
            return "PC";
        }

        // Wii has different version numbers
        if (info.Version == FastFileInfo.CoD4_Wii_Version ||
            info.Version == FastFileInfo.WaW_Wii_Version)
        {
            return "Wii";
        }

        // Signed files are Xbox 360 MP
        if (info.IsSigned)
        {
            return "Xbox 360";
        }

        // PS3 and Xbox 360 share the same version bytes for unsigned files
        // We can't definitively tell them apart, so return "Console (PS3/Xbox 360)"
        return "Console (PS3/Xbox 360)";
    }

    /// <summary>
    /// Decompresses a FastFile, handling signed file signatures.
    /// </summary>
    private static int DecompressWithSignatureHandling(string inputPath, string outputPath, FastFileInfo info)
    {
        // For signed files, we need to skip the signature data
        // The standard decompressor should handle this via SkipToCompressedData
        return FastFileProcessor.Decompress(inputPath, outputPath);
    }

    /// <summary>
    /// Patches the zone for the target platform.
    /// This includes:
    /// - Memory allocation values in header
    /// - Asset record field order (Xbox uses [ptr][type], PS3 uses [type][ptr])
    /// - Platform-specific asset name replacements
    /// </summary>
    private static int PatchZoneHeaderForPlatform(string zonePath, GameVersion gameVersion, Platform targetPlatform)
    {
        byte[] zoneData = File.ReadAllBytes(zonePath);

        // Get proper header size for the game (CoD4 always uses PS3-style 52-byte header)
        int minHeaderSize = gameVersion == GameVersion.CoD4
            ? FastFileConstants.ZoneHeaderSize_PS3
            : FastFileConstants.ZoneHeaderSize_Xbox360;

        if (zoneData.Length < minHeaderSize)
            return 0; // Zone too small to have header

        // Get the correct memory allocation values for the target platform and game
        (uint blockSizeTemp, uint blockSizeVertex) = GetMemoryAllocationValues(gameVersion, targetPlatform);

        // Patch BlockSizeTemp at offset 0x08 (4 bytes, big-endian)
        WriteUInt32BE(zoneData, FastFileConstants.BlockSizeTempOffset, blockSizeTemp);

        // Patch BlockSizeVertex at offset 0x20 (4 bytes, big-endian)
        // Note: For CoD4 all platforms have this field. For WaW/MW2 Xbox 360, this offset is ScriptStringCount.
        // Only write BlockSizeVertex for games/platforms that have it.
        if (gameVersion == GameVersion.CoD4 || targetPlatform == Platform.PS3 || targetPlatform == Platform.PC)
        {
            WriteUInt32BE(zoneData, FastFileConstants.BlockSizeVertexOffset, blockSizeVertex);
        }

        // Handle BlockSizeVirtual (0x14) and BlockSizeCallback (0x1C) swap
        uint blockSizeVirtual = ReadUInt32BE(zoneData, 0x14);
        uint blockSizeCallback = ReadUInt32BE(zoneData, 0x1C);

        if (targetPlatform == Platform.PS3 || targetPlatform == Platform.PC)
        {
            if (blockSizeVirtual == 0 && blockSizeCallback != 0)
            {
                WriteUInt32BE(zoneData, 0x14, blockSizeCallback);
                WriteUInt32BE(zoneData, 0x1C, 0);
            }
        }
        else if (targetPlatform == Platform.Xbox360)
        {
            if (blockSizeCallback == 0 && blockSizeVirtual != 0)
            {
                WriteUInt32BE(zoneData, 0x1C, blockSizeVirtual);
                WriteUInt32BE(zoneData, 0x14, 0);
            }
        }

        // Convert asset type IDs between platforms
        // Xbox 360 lacks vertexshader (0x08), so types >= 0x08 are shifted by 1
        ConvertAssetTypeIDs(zoneData, targetPlatform, gameVersion);

        // Replace platform-specific asset references (e.g., xenon_controller -> ps3_controller)
        int replacements = ReplacePlatformAssetReferences(zoneData, targetPlatform);

        File.WriteAllBytes(zonePath, zoneData);
        return replacements;
    }

    /// <summary>
    /// Platform-specific asset name mappings.
    /// Xbox 360 uses "xenon_" prefix (Xenon is the Xbox 360 CPU codename).
    /// PS3 uses "ps3_" prefix.
    /// </summary>
    private static readonly Dictionary<string, string> XboxToPs3AssetMappings = new()
    {
        // Controller UI images
        { "xenon_controller_top", "ps3_controller_top" },
        { "xenon_controller_lines_classic_mp", "ps3_controller_lines_classic_mp" },
        { "xenon_controller_lines_classic_sp", "ps3_controller_lines_classic_sp" },
        { "xenon_controller_lines_default_mp", "ps3_controller_lines_default_mp" },
        { "xenon_controller_lines_default_sp", "ps3_controller_lines_default_sp" },
        { "xenon_controller_lines_experimental_mp", "ps3_controller_lines_experimental_mp" },
        { "xenon_controller_lines_experimental_sp", "ps3_controller_lines_experimental_sp" },
        { "xenon_controller_lines_lefty_mp", "ps3_controller_lines_lefty_mp" },
        { "xenon_controller_lines_lefty_sp", "ps3_controller_lines_lefty_sp" },
        { "xenon_controller_lines_nomad_mp", "ps3_controller_lines_nomad_mp" },
        { "xenon_controller_lines_nomad_sp", "ps3_controller_lines_nomad_sp" },
    };

    private static readonly Dictionary<string, string> Ps3ToXboxAssetMappings = new()
    {
        // Reverse mappings
        { "ps3_controller_top", "xenon_controller_top" },
        { "ps3_controller_lines_classic_mp", "xenon_controller_lines_classic_mp" },
        { "ps3_controller_lines_classic_sp", "xenon_controller_lines_classic_sp" },
        { "ps3_controller_lines_default_mp", "xenon_controller_lines_default_mp" },
        { "ps3_controller_lines_default_sp", "xenon_controller_lines_default_sp" },
        { "ps3_controller_lines_experimental_mp", "xenon_controller_lines_experimental_mp" },
        { "ps3_controller_lines_experimental_sp", "xenon_controller_lines_experimental_sp" },
        { "ps3_controller_lines_lefty_mp", "xenon_controller_lines_lefty_mp" },
        { "ps3_controller_lines_lefty_sp", "xenon_controller_lines_lefty_sp" },
        { "ps3_controller_lines_nomad_mp", "xenon_controller_lines_nomad_mp" },
        { "ps3_controller_lines_nomad_sp", "xenon_controller_lines_nomad_sp" },
    };

    /// <summary>
    /// Replaces platform-specific asset references in zone data.
    /// For Xbox -> PS3: replaces "xenon_" prefixed assets with "ps3_" equivalents.
    /// For PS3 -> Xbox: replaces "ps3_" prefixed assets with "xenon_" equivalents.
    /// </summary>
    /// <returns>Number of replacements made</returns>
    private static int ReplacePlatformAssetReferences(byte[] zoneData, Platform targetPlatform)
    {
        int totalReplacements = 0;

        // Select the appropriate mapping based on target platform
        var mappings = targetPlatform switch
        {
            Platform.PS3 => XboxToPs3AssetMappings,
            Platform.PC => XboxToPs3AssetMappings, // PC uses same assets as PS3
            Platform.Xbox360 => Ps3ToXboxAssetMappings,
            _ => null
        };

        if (mappings == null)
            return 0;

        foreach (var mapping in mappings)
        {
            string searchStr = mapping.Key;
            string replaceStr = mapping.Value;

            byte[] searchBytes = Encoding.ASCII.GetBytes(searchStr);
            byte[] replaceBytes = Encoding.ASCII.GetBytes(replaceStr);

            // Find and replace all occurrences
            int index = 0;
            while ((index = FindBytes(zoneData, searchBytes, index)) >= 0)
            {
                // Replace the bytes, handling length differences
                ReplaceStringInPlace(zoneData, index, searchStr.Length, replaceBytes);
                totalReplacements++;
                index += replaceBytes.Length;
            }
        }

        return totalReplacements;
    }

    /// <summary>
    /// Finds a byte sequence in an array.
    /// </summary>
    private static int FindBytes(byte[] data, byte[] pattern, int startIndex)
    {
        for (int i = startIndex; i <= data.Length - pattern.Length; i++)
        {
            bool found = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j])
                {
                    found = false;
                    break;
                }
            }
            if (found)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Replaces a string in place, padding with nulls if the replacement is shorter.
    /// </summary>
    private static void ReplaceStringInPlace(byte[] data, int offset, int originalLength, byte[] replacement)
    {
        // Copy the replacement bytes
        for (int i = 0; i < replacement.Length && (offset + i) < data.Length; i++)
        {
            data[offset + i] = replacement[i];
        }

        // If replacement is shorter, pad with null bytes
        for (int i = replacement.Length; i < originalLength && (offset + i) < data.Length; i++)
        {
            data[offset + i] = 0x00;
        }
    }

    /// <summary>
    /// Converts asset type IDs between platforms.
    /// WaW Xbox 360 doesn't have vertexshader (0x08), so types >= 0x08 are shifted by 1.
    /// Xbox 360: techset=0x08, image=0x09, rawfile=0x21, stringtable=0x22
    /// PS3:      vertexshader=0x08, techset=0x09, image=0x0A, rawfile=0x22, stringtable=0x23
    /// Both platforms use [4-byte type][4-byte ptr] field order - NO swap needed.
    /// Note: CoD4 uses PS3-style offsets on ALL platforms.
    /// </summary>
    private static void ConvertAssetTypeIDs(byte[] zoneData, Platform targetPlatform, GameVersion gameVersion)
    {
        // MW2 may have different handling - skip for now
        if (gameVersion == GameVersion.MW2)
        {
            return;
        }

        // CoD4 uses PS3-style offsets on all platforms
        // For WaW, we assume the source is PS3-style (most common case)
        int assetCountOffset = FastFileConstants.GetAssetCountOffset(gameVersion, isXbox360: false, isPC: false);
        int assetCount = (int)ReadUInt32BE(zoneData, assetCountOffset);

        if (assetCount <= 0 || assetCount > 10000)
            return; // Invalid asset count

        // Find asset pool by pattern matching: look for consecutive valid asset records
        // Asset record pattern: [00 00 00 XX][FF FF FF FF] where XX is a valid asset type (0x01-0x24)
        int assetPoolStart = -1;
        for (int i = 0x100; i < Math.Min(zoneData.Length - 16, 0x3000); i++)
        {
            // Check for pattern: 00 00 00 XX FF FF FF FF (where XX is valid asset type 0x01-0x24)
            if (zoneData[i] == 0x00 && zoneData[i + 1] == 0x00 && zoneData[i + 2] == 0x00 &&
                zoneData[i + 3] >= 0x01 && zoneData[i + 3] <= 0x24 &&
                zoneData[i + 4] == 0xFF && zoneData[i + 5] == 0xFF && zoneData[i + 6] == 0xFF && zoneData[i + 7] == 0xFF)
            {
                // Verify next record also looks valid
                if (zoneData[i + 8] == 0x00 && zoneData[i + 9] == 0x00 && zoneData[i + 10] == 0x00 &&
                    zoneData[i + 11] >= 0x01 && zoneData[i + 11] <= 0x24 &&
                    zoneData[i + 12] == 0xFF && zoneData[i + 13] == 0xFF && zoneData[i + 14] == 0xFF && zoneData[i + 15] == 0xFF)
                {
                    assetPoolStart = i;
                    break;
                }
            }
        }

        if (assetPoolStart < 0)
            return; // Asset pool not found

        // Convert asset type IDs
        // Asset record format: [4-byte type BE][4-byte ptr]
        int offset = assetPoolStart;
        for (int i = 0; i < assetCount; i++)
        {
            if (offset + 8 > zoneData.Length) break;

            // Type is in the first field, low byte is at offset + 3 (big-endian)
            byte assetType = zoneData[offset + 3];

            // WaW and CoD4: Xbox 360 lacks vertexshader at 0x08
            // Types 0x00-0x07 are same on both platforms
            // Starting from 0x08, PS3 has vertexshader which Xbox 360 doesn't have
            // So Xbox types >= 0x08 need +1 for PS3, and PS3 types >= 0x09 need -1 for Xbox
            if (targetPlatform == Platform.PS3 || targetPlatform == Platform.PC)
            {
                // Converting Xbox -> PS3: add 1 to types >= 0x08
                if (assetType >= 0x08 && assetType < 0xFF)
                {
                    zoneData[offset + 3] = (byte)(assetType + 1);
                }
            }
            else if (targetPlatform == Platform.Xbox360)
            {
                // Converting PS3 -> Xbox: subtract 1 from types >= 0x09
                // (skip vertexshader 0x08 which doesn't exist on Xbox)
                if (assetType >= 0x09 && assetType <= 0x24)
                {
                    zoneData[offset + 3] = (byte)(assetType - 1);
                }
                // Note: vertexshader (0x08) doesn't exist on Xbox 360 - would be an error
            }

            offset += 8;
        }
    }

    /// <summary>
    /// Reads a 32-bit unsigned integer in big-endian format.
    /// </summary>
    private static uint ReadUInt32BE(byte[] data, int offset)
    {
        return (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
    }

    /// <summary>
    /// Gets the memory allocation values for a specific game and platform.
    /// </summary>
    private static (uint blockSizeTemp, uint blockSizeVertex) GetMemoryAllocationValues(GameVersion gameVersion, Platform platform)
    {
        // Values from game definitions
        return gameVersion switch
        {
            GameVersion.CoD4 => (CoD4Definition.MemAlloc1Value, CoD4Definition.MemAlloc2Value),
            GameVersion.WaW => platform switch
            {
                Platform.Xbox360 => (CoD5Definition.Xbox360MemAlloc1Value, CoD5Definition.Xbox360MemAlloc2Value),
                _ => (CoD5Definition.MemAlloc1Value, CoD5Definition.MemAlloc2Value)
            },
            GameVersion.MW2 => (MW2Definition.MemAlloc1Value, MW2Definition.MemAlloc2Value),
            _ => (CoD5Definition.MemAlloc1Value, CoD5Definition.MemAlloc2Value) // Default to WaW PS3 values
        };
    }

    /// <summary>
    /// Writes a 32-bit unsigned integer in big-endian format.
    /// </summary>
    private static void WriteUInt32BE(byte[] data, int offset, uint value)
    {
        data[offset] = (byte)(value >> 24);
        data[offset + 1] = (byte)(value >> 16);
        data[offset + 2] = (byte)(value >> 8);
        data[offset + 3] = (byte)(value & 0xFF);
    }

    /// <summary>
    /// Compresses zone data for a specific platform.
    /// </summary>
    private static void CompressForPlatform(string zonePath, string outputPath, GameVersion gameVersion, Platform platform)
    {
        string platformStr = platform switch
        {
            Platform.PS3 => "PS3",
            Platform.Xbox360 => "Xbox360",
            Platform.PC => "PC",
            Platform.Wii => "Wii",
            _ => "PS3"
        };

        // Use the existing compress method with platform parameter
        FastFileProcessor.Compress(zonePath, outputPath, gameVersion, platformStr);
    }

    /// <summary>
    /// Extracts the zone name from a file path.
    /// Handles common naming patterns like "xbox modname patch_mp.ff" -> "patch_mp"
    /// </summary>
    private static string GetZoneNameFromPath(string filePath)
    {
        // Get filename without extension
        string filename = Path.GetFileNameWithoutExtension(filePath);

        // Known zone name suffixes that appear at the end of mod filenames
        string[] knownZoneNames = {
            "patch_mp", "patch", "common_mp", "common", "code_post_gfx_mp", "code_post_gfx",
            "localized_common_mp", "localized_code_post_gfx_mp", "ui_mp", "ui"
        };

        // Check if filename ends with a known zone name (case-insensitive)
        foreach (var zoneName in knownZoneNames)
        {
            if (filename.EndsWith(zoneName, StringComparison.OrdinalIgnoreCase))
            {
                return zoneName;
            }
            // Also check with underscore prefix (e.g., "modname_patch_mp")
            if (filename.EndsWith("_" + zoneName, StringComparison.OrdinalIgnoreCase))
            {
                return zoneName;
            }
        }

        // Check if filename contains a known zone name with space before it
        foreach (var zoneName in knownZoneNames)
        {
            int idx = filename.IndexOf(" " + zoneName, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                return zoneName;
            }
        }

        // Fallback: clean up the filename and use it as zone name
        // Remove common prefixes like "xbox ", "ps3 ", "converted_", etc.
        string cleaned = filename
            .Replace("xbox ", "", StringComparison.OrdinalIgnoreCase)
            .Replace("ps3 ", "", StringComparison.OrdinalIgnoreCase)
            .Replace("xbox_", "", StringComparison.OrdinalIgnoreCase)
            .Replace("ps3_", "", StringComparison.OrdinalIgnoreCase)
            .Replace("_converted", "", StringComparison.OrdinalIgnoreCase)
            .Replace("converted_", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        return string.IsNullOrEmpty(cleaned) ? filename : cleaned;
    }
}

/// <summary>
/// Analysis information about a FastFile.
/// </summary>
public class FastFileAnalysis
{
    public bool IsValid { get; set; }
    public string Magic { get; set; } = "";
    public GameVersion GameVersion { get; set; }
    public string GameName { get; set; } = "";
    public bool IsSigned { get; set; }
    public string DetectedPlatform { get; set; } = "";
    public long FileSize { get; set; }
    public bool CanConvertToPS3 { get; set; }
    public bool CanConvertToXbox360 { get; set; }
    public bool CanConvertToPC { get; set; }
    public List<string> Notes { get; set; } = new();
}
