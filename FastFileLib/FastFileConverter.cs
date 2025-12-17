using System.Text;

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
                PatchZoneHeaderForPlatform(tempZonePath, sourceInfo.GameVersion, targetPlatform);
                result.Warnings.Add("Zone header memory allocation values patched for target platform.");

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
    /// </summary>
    private static void PatchZoneHeaderForPlatform(string zonePath, GameVersion gameVersion, Platform targetPlatform)
    {
        byte[] zoneData = File.ReadAllBytes(zonePath);

        if (zoneData.Length < 0x34)
            return; // Zone too small to have header

        // Get the correct memory allocation values for the target platform and game
        (uint blockSizeTemp, uint blockSizeVertex) = GetMemoryAllocationValues(gameVersion, targetPlatform);

        // Patch BlockSizeTemp at offset 0x08 (4 bytes, big-endian)
        WriteUInt32BE(zoneData, 0x08, blockSizeTemp);

        // Patch BlockSizeVertex at offset 0x20 (4 bytes, big-endian)
        WriteUInt32BE(zoneData, 0x20, blockSizeVertex);

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
        // Both Xbox and PS3 use [4-byte ptr][4-byte type] format - no swap needed
        // Only type IDs differ (Xbox 0x21 = PS3 0x22 for rawfile, etc.)
        ConvertAssetTypeIDs(zoneData, targetPlatform);

        File.WriteAllBytes(zonePath, zoneData);
    }

    /// <summary>
    /// Converts asset type IDs between platforms.
    /// Both Xbox and PS3 use [ptr][type] field order - NO swap needed.
    /// Xbox and PS3 use different asset type IDs for WaW (Xbox is -1 from PS3 for types >= 0x21)
    /// </summary>
    private static void ConvertAssetTypeIDs(byte[] zoneData, Platform targetPlatform)
    {
        int scriptStringCount = (int)ReadUInt32BE(zoneData, 0x24);
        int assetCount = (int)ReadUInt32BE(zoneData, 0x2C);

        if (assetCount <= 0 || assetCount > 10000)
            return; // Invalid asset count

        // Find where script strings start (first non-FF/00 after header)
        int dataStart = 0x34;
        for (int i = 0x34; i < Math.Min(zoneData.Length, 0x2000); i++)
        {
            if (zoneData[i] != 0xFF && zoneData[i] != 0x00)
            {
                dataStart = i;
                break;
            }
        }

        // Skip past script strings to find asset table
        int offset = dataStart;
        int stringsSkipped = 0;
        while (stringsSkipped < scriptStringCount && offset < zoneData.Length)
        {
            while (offset < zoneData.Length && zoneData[offset] != 0) offset++;
            offset++; // skip null terminator
            stringsSkipped++;
        }

        // Align to 4 bytes
        while ((offset % 4) != 0 && offset < zoneData.Length) offset++;

        // Asset record format (same for both Xbox and PS3): [4-byte ptr][4-byte type]
        // Only convert type IDs, no field swap needed
        for (int i = 0; i < assetCount; i++)
        {
            if (offset + 8 > zoneData.Length) break;

            // Type is in the second field (offset + 4), low byte is at offset + 7 (big-endian)
            byte assetType = zoneData[offset + 7];

            // Convert asset type ID
            // Xbox asset types >= 0x21 are PS3 types - 1
            if (targetPlatform == Platform.PS3 || targetPlatform == Platform.PC)
            {
                // Converting Xbox -> PS3: add 1 to types >= 0x21
                if (assetType >= 0x21 && assetType < 0xFF)
                {
                    zoneData[offset + 7] = (byte)(assetType + 1);
                }
            }
            else if (targetPlatform == Platform.Xbox360)
            {
                // Converting PS3 -> Xbox: subtract 1 from types >= 0x22
                if (assetType >= 0x22 && assetType <= 0x24)
                {
                    zoneData[offset + 7] = (byte)(assetType - 1);
                }
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
        // Values from Zone.md documentation
        return gameVersion switch
        {
            GameVersion.CoD4 => platform switch
            {
                Platform.PS3 => (0x0F70u, 0x0u),
                Platform.Xbox360 => (0x0F70u, 0x0u),
                Platform.PC => (0x0F70u, 0x0u),
                _ => (0x0F70u, 0x0u)
            },
            GameVersion.WaW => platform switch
            {
                Platform.PS3 => (0x10B0u, 0x05F8F0u),
                Platform.Xbox360 => (0x10B0u, 0x0u), // Xbox doesn't use BlockSizeVertex
                Platform.PC => (0x10B0u, 0x05F8F0u),
                _ => (0x10B0u, 0x05F8F0u)
            },
            GameVersion.MW2 => platform switch
            {
                Platform.PS3 => (0x03B4u, 0x1000u),
                Platform.Xbox360 => (0x03B4u, 0x0u),
                Platform.PC => (0x03B4u, 0x1000u),
                _ => (0x03B4u, 0x1000u)
            },
            _ => (0x10B0u, 0x05F8F0u) // Default to WaW PS3 values
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
