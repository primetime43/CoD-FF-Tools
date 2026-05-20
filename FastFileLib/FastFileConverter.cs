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
/// Converts FastFiles between platforms (PS3, Xbox 360, PC, Wii).
///
/// Two conversion modes:
///   <see cref="Convert"/> — in-place header patch + asset-type-ID remap. Requires
///     source and target zone-header layouts to match (52-byte ↔ 52-byte, 56-byte ↔
///     56-byte, etc). Refuses cross-layout with a clear error. Preserves per-zone
///     MemAlloc values when targeting PC or Wii (uses magic constants for PS3/Xbox 360).
///   <see cref="ConvertUsingBaseZone"/> — rebuilds the zone from scratch via
///     <see cref="ZoneBuilder"/>. Use this for cross-layout conversions (e.g. MW2 PS3
///     52-byte → MW2 PC 56-byte, or anything to/from Wii's 56-byte from a 52-byte source).
///
/// Asset-type-ID remapping is enum-shift aware: Xbox 360 drops vertexshader (−1 from
/// PS3), CoD4/WaW PC drops both pixelshader and vertexshader (−2), MW2 PC keeps both
/// and adds vertexdecl (+1 from PS3). Wii uses the PC enum.
/// </summary>
public static class FastFileConverter
{
    /// <summary>
    /// Converts a FastFile from one platform to another via in-place header/asset-ID
    /// patching. Throws (returns failure) if the source and target use different
    /// zone-header layouts — use <see cref="ConvertUsingBaseZone"/> for those cases.
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

            // Cross-endianness (BE console/Wii ↔ LE PC) is refused inside PatchZoneHeaderForPlatform
            // with a precise, actionable error pointing at ConvertUsingBaseZone — no pre-emptive
            // warning needed here, and the in-place same-endianness paths don't need one either.

            // Create temp file for zone data
            string tempZonePath = Path.GetTempFileName();

            try
            {
                // Decompress to zone
                int blocksDecompressed = DecompressWithSignatureHandling(inputPath, tempZonePath, sourceInfo);
                result.BlocksProcessed = blocksDecompressed;

                // Patch zone header for target platform (memory allocation values, BlockSize
                // block swap, asset-enum shifts). Cross-byte-layout changes (e.g. MW2 PS3 52-byte
                // → MW2 Xbox 360 48-byte, or anything ↔ MW2 PC 56-byte) require shifting bytes
                // around the zone header, which the in-place patcher can't do — that case throws
                // with a clear error and the caller should use ConvertUsingBaseZone instead.
                int assetReplacements = PatchZoneHeaderForPlatform(tempZonePath, sourceInfo, targetPlatform);
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
    /// Converts a mod FastFile by extracting its rawfiles + localize entries and building a
    /// fresh zone for <paramref name="targetPlatform"/> via <see cref="ZoneBuilder"/>. This is
    /// the "rebuild from scratch" path — it drops every non-rawfile/localize asset (xmodels,
    /// materials, menus, weapons, sounds), which is fine for script-only mod patches but wrong
    /// for content zones. ZoneBuilder is platform-aware so the output uses the right header
    /// layout + endianness + asset type IDs for the chosen target.
    /// </summary>
    /// <param name="sourceModPath">Path to the source mod FastFile (any platform)</param>
    /// <param name="basePs3ZonePath">Not used — kept for API compatibility</param>
    /// <param name="outputPath">Path for the converted FastFile</param>
    /// <param name="zoneName">Optional zone name override (if null, auto-detected from filename)</param>
    /// <param name="targetPlatform">Target platform for the rebuilt zone. Defaults to PS3
    /// since that's the historical default behavior of this method.</param>
    public static ConversionResult ConvertUsingBaseZone(
        string sourceModPath,
        string basePs3ZonePath,
        string outputPath,
        string? zoneName = null,
        Platform targetPlatform = Platform.PS3)
    {
        var result = new ConversionResult();
        result.TargetPlatform = targetPlatform.ToString();

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

                // Step 2: Extract raw files and localized strings from source mod zone
                result.Warnings.Add("Extracting raw files from source mod...");
                byte[] sourceZoneData = File.ReadAllBytes(tempSourceZonePath);
                var rawFiles = ExtractRawFilesFromZone(sourceZoneData, sourceInfo);
                result.Warnings.Add($"Found {rawFiles.Count} raw files in source mod.");

                var localizedEntries = ExtractLocalizedEntriesFromZone(sourceZoneData);
                if (localizedEntries.Count > 0)
                    result.Warnings.Add($"Found {localizedEntries.Count} localized strings in source mod.");

                if (rawFiles.Count == 0 && localizedEntries.Count == 0)
                {
                    throw new InvalidOperationException("No raw files or localized strings found in source mod. Cannot convert.");
                }

                // Step 3: Build a fresh zone for the target platform using ZoneBuilder.
                string targetPlatformStr = PlatformToString(targetPlatform);
                result.Warnings.Add($"Building fresh {targetPlatformStr} zone with extracted assets...");
                string effectiveZoneName = !string.IsNullOrWhiteSpace(zoneName)
                    ? zoneName
                    : GetZoneNameFromPath(sourceModPath);
                result.Warnings.Add($"Using zone name: {effectiveZoneName}");
                var zoneBuilder = new ZoneBuilder(result.GameVersion, effectiveZoneName, targetPlatformStr);
                zoneBuilder.AddRawFiles(rawFiles);
                zoneBuilder.AddLocalizedEntries(localizedEntries);

                // PC and Wii use per-zone MemAlloc values, not the console magic constants. When the
                // rebuild is SAME-platform (e.g. re-packing a PC zone as PC), the source zone's values
                // are valid target values, so preserve them verbatim — same as the editor's IncreaseSize
                // path and the in-place Convert() path. We deliberately do NOT preserve cross-platform
                // (e.g. PS3 -> PC): the source carries console magic (0x10B0 etc.) which isn't a valid
                // PC/Wii per-zone value, and MW2 PC additionally requires vertex=0 — so cross-platform
                // falls back to ZoneBuilder's platform defaults, which already special-case those.
                bool samePlatformPcWii =
                    (targetPlatform == Platform.PC && sourceInfo.IsPC) ||
                    (targetPlatform == Platform.Wii && sourceInfo.IsWii);
                if (samePlatformPcWii && sourceZoneData.Length >= 0x24)
                {
                    bool srcLE = sourceInfo.IsPC;  // PC = little-endian; Wii = big-endian
                    zoneBuilder.WithBlockSizeTemp(ReadUInt32(sourceZoneData, FastFileConstants.BlockSizeTempOffset, srcLE));
                    zoneBuilder.WithBlockSizeVertex(ReadUInt32(sourceZoneData, FastFileConstants.BlockSizeVertexOffset, srcLE));
                }

                byte[] newZone = zoneBuilder.Build();

                result.Warnings.Add($"Built new zone with {rawFiles.Count} raw files + {localizedEntries.Count} localized strings ({newZone.Length} bytes).");

                File.WriteAllBytes(tempNewZonePath, newZone);

                result.ReplacedFiles = rawFiles.Select(f => f.Name)
                    .Concat(localizedEntries.Select(e => $"localize:{e.Reference}"))
                    .ToList();

                // Step 4: Compress new zone to the target's FastFile format.
                result.Warnings.Add($"Compressing to {targetPlatformStr} FastFile...");
                CompressForPlatform(tempNewZonePath, outputPath, result.GameVersion, targetPlatform);

                result.ConvertedSize = new FileInfo(outputPath).Length;
                result.Success = true;
                result.Message = $"Successfully converted {Path.GetFileName(sourceModPath)}. " +
                                $"Built fresh {targetPlatformStr} zone with {rawFiles.Count} raw files.";
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
    /// Extracts all raw files from a zone file by delegating to the canonical
    /// <see cref="RawFileScanner.FindRawFiles"/>. That locator is game- and
    /// endianness-aware (CoD4/WaW 12-byte BE on console / LE on PC, MW2 16/20-byte
    /// with inline zlib), so this works for PC (little-endian) sources too — a
    /// bespoke big-endian-only scanner here previously found 0 rawfiles in PC zones,
    /// which broke PC→PS3 conversion.
    /// </summary>
    private static List<RawFile> ExtractRawFilesFromZone(byte[] zoneData, FastFileInfo sourceInfo)
    {
        var locations = RawFileScanner.FindRawFiles(zoneData, sourceInfo.GameVersion, sourceInfo.IsPC);
        return locations
            .Select(loc => new RawFile(loc.Name, loc.Data))
            .ToList();
    }

    /// <summary>
    /// Extracts all localized string entries from a zone file by scanning for the
    /// 8-byte FF FF FF FF FF FF FF FF marker followed by [value\0][reference\0].
    /// Rawfile headers use only 4 FF bytes followed by a non-FF size field, so 8
    /// consecutive FFs is essentially unique to localize entries.
    /// </summary>
    private static List<LocalizedEntry> ExtractLocalizedEntriesFromZone(byte[] zoneData)
    {
        var entries = new List<LocalizedEntry>();
        var seenKeys = new HashSet<string>();

        for (int i = 0; i <= zoneData.Length - 10; i++)
        {
            // Look for 8 consecutive FF bytes
            bool isMarker = true;
            for (int j = 0; j < 8; j++)
            {
                if (zoneData[i + j] != 0xFF) { isMarker = false; break; }
            }
            if (!isMarker) continue;

            // Next byte after marker is the first byte of the value string. If it's FF, we're
            // still in padding/markers — keep scanning.
            int valueStart = i + 8;
            if (valueStart >= zoneData.Length || zoneData[valueStart] == 0xFF)
                continue;

            // Read value (null-terminated)
            int valueEnd = valueStart;
            while (valueEnd < zoneData.Length && zoneData[valueEnd] != 0x00)
                valueEnd++;
            if (valueEnd == valueStart || valueEnd >= zoneData.Length)
                continue;
            // Latin1, NOT Encoding.Default (UTF-8 on .NET 8). CoD localized values are
            // single-byte; decoding accented bytes (e.g. 0xE9 'é') as UTF-8 yields U+FFFD,
            // which then re-encodes to different bytes and corrupts the string on rebuild.
            // Latin1 round-trips bytes 0x00..0xFF exactly. See ZoneBuilder.BuildLocalizedSection.
            string value = Encoding.Latin1.GetString(zoneData, valueStart, valueEnd - valueStart);

            // Read reference key (null-terminated)
            int keyStart = valueEnd + 1;
            int keyEnd = keyStart;
            while (keyEnd < zoneData.Length && zoneData[keyEnd] != 0x00)
                keyEnd++;
            if (keyEnd == keyStart || keyEnd >= zoneData.Length)
                continue;
            string key = Encoding.ASCII.GetString(zoneData, keyStart, keyEnd - keyStart);

            // Validate the key looks like a localize key (SCREAMING_SNAKE_CASE).
            // This filters out matches that happen to land inside rawfile data or padding.
            if (!IsValidLocalizeKey(key)) continue;
            if (!seenKeys.Add(key)) continue;

            entries.Add(new LocalizedEntry(key, value));

            // Advance past this entry to avoid re-matching its trailing FFs.
            i = keyEnd;
        }

        return entries;
    }

    /// <summary>
    /// Lightweight localize-key validator. Keys are SCREAMING_SNAKE_CASE with digits
    /// and underscores (e.g. RANK_PRESTIGE_10, MENU_CONTROLS). Mirrors the editor's
    /// AssetRecordProcessor.IsValidLocalizeKey heuristics.
    /// </summary>
    private static bool IsValidLocalizeKey(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length < 3 || key.Length > 150)
            return false;
        if (key[0] < 'A' || key[0] > 'Z')
            return false;

        int uppercaseCount = 0;
        int underscoreCount = 0;
        int consecutiveSame = 1;
        char prev = '\0';

        foreach (char c in key)
        {
            bool isUpper = c >= 'A' && c <= 'Z';
            bool isDigit = c >= '0' && c <= '9';
            bool isUnderscore = c == '_';
            if (!isUpper && !isDigit && !isUnderscore) return false;
            if (isUpper) uppercaseCount++;
            if (isUnderscore) underscoreCount++;
            if (c == prev) { consecutiveSame++; if (consecutiveSame > 3) return false; }
            else consecutiveSame = 1;
            prev = c;
        }

        // Real keys have at least one underscore and a few uppercase letters.
        return underscoreCount >= 1 && uppercaseCount >= 2;
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
    /// Detects the source platform from FastFile info. Thin alias over
    /// <see cref="FastFileInfo.Platform"/> kept for callers that pass a
    /// <see cref="FastFileInfo"/> rather than the raw fields.
    /// </summary>
    private static string DetectPlatform(FastFileInfo info) => info.Platform;

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
    private static int PatchZoneHeaderForPlatform(string zonePath, FastFileInfo sourceInfo, Platform targetPlatform)
    {
        byte[] zoneData = File.ReadAllBytes(zonePath);
        GameVersion gameVersion = sourceInfo.GameVersion;
        bool sourceIsPC = sourceInfo.IsPC;
        bool sourceIsWii = sourceInfo.IsWii;
        bool targetIsPC = targetPlatform == Platform.PC;
        bool targetIsWii = targetPlatform == Platform.Wii;

        // Source and target layouts differ for some (game, platform) pairs. The in-place patcher
        // can only shift values within the SAME-size header — anything that needs to insert or
        // remove the BlockSizeIndex slot (MW2 PC's 56-byte vs MW2 Xbox 360's 48-byte vs MW2 PS3's
        // 52-byte) requires rebuilding the zone. Refuse those rather than silently corrupting.
        int sourceHeaderSize = FastFileConstants.GetZoneHeaderSize(gameVersion,
            isXbox360: !sourceIsPC && !sourceIsWii && sourceInfo.Platform == "Xbox 360",
            isPC: sourceIsPC, isWii: sourceIsWii);
        int targetHeaderSize = FastFileConstants.GetZoneHeaderSize(gameVersion,
            isXbox360: targetPlatform == Platform.Xbox360,
            isPC: targetIsPC, isWii: targetIsWii);
        if (sourceHeaderSize != targetHeaderSize)
        {
            throw new InvalidOperationException(
                $"Cross-layout conversion not supported by Convert(): source uses {sourceHeaderSize}-byte zone " +
                $"header, target needs {targetHeaderSize}-byte. Use ConvertUsingBaseZone (rebuilds the zone) " +
                "or the FF Editor's in-place edit flow.");
        }

        // Cross-endianness conversion (BE console/Wii ↔ LE PC) can't be done in-place: the patch
        // only byte-swaps the zone HEADER, but the entire zone BODY (asset pointers, rawfile size
        // headers, string counts, etc.) is also in the source's byte order. Swapping every field
        // in the body requires fully parsing the zone, which is what ZoneBuilder does on rebuild.
        // Refuse rather than emit a header-correct-but-body-wrong-endianness file.
        bool sourceIsLE = sourceIsPC;
        bool targetIsLE = targetIsPC;
        if (sourceIsLE != targetIsLE)
        {
            string srcOrder = sourceIsLE ? "little-endian (PC)" : "big-endian (PS3/Xbox 360/Wii)";
            string tgtOrder = targetIsLE ? "little-endian (PC)" : "big-endian (PS3/Xbox 360/Wii)";
            throw new InvalidOperationException(
                $"Cross-endianness conversion not supported by Convert(): source is {srcOrder}, target is " +
                $"{tgtOrder}. The in-place patcher only byte-swaps the zone header, not the body, so the output " +
                "would be corrupt. Use ConvertUsingBaseZone (rebuilds the zone in the target byte order).");
        }

        if (zoneData.Length < sourceHeaderSize) return 0;  // zone too small to have header

        // Read existing block-size fields in SOURCE endianness so we can preserve them (or use
        // them in subsequent decisions). Endianness is the source's byte order, not the target's.
        uint existingTemp     = ReadUInt32(zoneData, FastFileConstants.BlockSizeTempOffset, sourceIsPC);
        uint existingVertex   = sourceHeaderSize >= 0x24
            ? ReadUInt32(zoneData, FastFileConstants.BlockSizeVertexOffset, sourceIsPC)
            : 0u;
        uint existingVirtual  = ReadUInt32(zoneData, 0x14, sourceIsPC);
        uint existingCallback = ReadUInt32(zoneData, 0x1C, sourceIsPC);

        // Pick MemAlloc values to write. For PS3/Xbox 360 targets the documented magic constants
        // are correct; for PC/Wii targets retail uses per-zone values, so preserve from source.
        (uint blockSizeTemp, uint blockSizeVertex) = (targetIsPC || targetIsWii)
            ? (existingTemp, existingVertex)
            : GetMemoryAllocationValues(gameVersion, targetPlatform);

        // Write block-size fields in TARGET endianness.
        WriteUInt32(zoneData, FastFileConstants.BlockSizeTempOffset, blockSizeTemp, targetIsLE);

        // BlockSizeVertex exists in every layout EXCEPT MW2 Xbox 360's 48-byte header (where 0x20
        // is ScriptStringCount instead). Only write the slot for layouts that have it.
        bool targetHasVertexSlot = !(gameVersion == GameVersion.MW2 && targetPlatform == Platform.Xbox360);
        if (targetHasVertexSlot)
        {
            WriteUInt32(zoneData, FastFileConstants.BlockSizeVertexOffset, blockSizeVertex, targetIsLE);
        }

        // BlockSizeVirtual (0x14) vs BlockSizeCallback (0x1C) swap. Some Xbox 360 zones use 0x1C
        // for what PS3 puts at 0x14 (or vice versa). The heuristic: if one slot is zero and the
        // other isn't, move the non-zero value into the slot the target convention uses.
        if (targetPlatform == Platform.PS3 || targetIsPC || targetIsWii)
        {
            if (existingVirtual == 0 && existingCallback != 0)
            {
                WriteUInt32(zoneData, 0x14, existingCallback, targetIsLE);
                WriteUInt32(zoneData, 0x1C, 0, targetIsLE);
            }
            else
            {
                // Already in PS3-style layout — just rewrite in target endianness to handle
                // BE→LE / LE→BE swap when source and target byte orders differ.
                WriteUInt32(zoneData, 0x14, existingVirtual, targetIsLE);
                WriteUInt32(zoneData, 0x1C, existingCallback, targetIsLE);
            }
        }
        else if (targetPlatform == Platform.Xbox360)
        {
            if (existingCallback == 0 && existingVirtual != 0)
            {
                WriteUInt32(zoneData, 0x1C, existingVirtual, targetIsLE);
                WriteUInt32(zoneData, 0x14, 0, targetIsLE);
            }
            else
            {
                WriteUInt32(zoneData, 0x14, existingVirtual, targetIsLE);
                WriteUInt32(zoneData, 0x1C, existingCallback, targetIsLE);
            }
        }

        ConvertAssetTypeIDs(zoneData, sourceInfo, targetPlatform);

        // Replace platform-specific asset references (e.g., xenon_controller -> ps3_controller).
        // String-level replacement, endianness-independent.
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
    private static void ConvertAssetTypeIDs(byte[] zoneData, FastFileInfo sourceInfo, Platform targetPlatform)
    {
        GameVersion gameVersion = sourceInfo.GameVersion;
        bool sourceIsPC = sourceInfo.IsPC;
        bool sourceIsWii = sourceInfo.IsWii;
        bool sourceIsXbox360 = !sourceIsPC && !sourceIsWii && sourceInfo.Platform == "Xbox 360";
        bool targetIsPC = targetPlatform == Platform.PC;
        bool targetIsWii = targetPlatform == Platform.Wii;
        bool targetIsXbox360 = targetPlatform == Platform.Xbox360;

        // No-op when source and target use the same asset enum: nothing to shift.
        int sourceEnumKey = EnumKey(sourceIsXbox360, sourceIsPC, sourceIsWii);
        int targetEnumKey = EnumKey(targetIsXbox360, targetIsPC, targetIsWii);
        if (sourceEnumKey == targetEnumKey) return;

        // Locate AssetCount + AssetsPtr using the source's layout offsets, and read AssetCount
        // in the source's endianness. Without these the asset pool walker can't bound its loop.
        int assetCountOffset = FastFileConstants.GetAssetCountOffset(
            gameVersion, sourceIsXbox360, sourceIsPC, sourceIsWii);
        if (assetCountOffset + 4 > zoneData.Length) return;
        int assetCount = (int)ReadUInt32(zoneData, assetCountOffset, sourceIsPC);
        if (assetCount <= 0 || assetCount > 100_000) return;

        // Asset pool starts immediately after the header (or after the script string region,
        // when ScriptStringCount > 0). Pattern-scan for it using the source's byte order:
        //   BE source: [00 00 00 XX] [FF FF FF FF]  (type word + ptr)
        //   LE source: [XX 00 00 00] [FF FF FF FF]
        // Scanning two consecutive records reduces false positives on padding bytes.
        int assetPoolStart = FindAssetPoolStart(zoneData, sourceIsPC, scanMax: 0x4000);
        if (assetPoolStart < 0) return;

        // For each entry, remap the type ID from source enum to target enum, then write it back
        // in the target's endianness. (Source and target type-byte positions differ when
        // endianness differs — BE puts the type ID at offset+3, LE puts it at offset+0.)
        int offset = assetPoolStart;
        for (int i = 0; i < assetCount; i++)
        {
            if (offset + 8 > zoneData.Length) break;

            byte srcType = sourceIsPC ? zoneData[offset] : zoneData[offset + 3];
            int? dstType = RemapAssetTypeId(srcType, gameVersion, sourceEnumKey, targetEnumKey);
            if (dstType is not null)
            {
                // Clear all four type-word bytes, then write the new type ID at the position
                // the target endianness uses. Avoids leaving stale high bytes from the source.
                zoneData[offset]     = 0;
                zoneData[offset + 1] = 0;
                zoneData[offset + 2] = 0;
                zoneData[offset + 3] = 0;
                zoneData[targetIsPC ? offset : offset + 3] = (byte)dstType.Value;
            }
            // If dstType is null the source type doesn't exist on the target (e.g. PS3
            // vertexshader on Xbox 360); leave the slot as-is. The asset itself will likely
            // be invalid post-conversion, which is documented as a Convert() limitation.

            offset += 8;
        }
    }

    /// <summary>
    /// Per-game asset-enum identifier so we can compare source vs target asset enums without
    /// stringly-typed keys. 0=PS3, 1=Xbox360, 2=PC(or Wii — Wii uses PC enum).
    /// </summary>
    private static int EnumKey(bool isXbox360, bool isPC, bool isWii)
        => isPC || isWii ? 2 : isXbox360 ? 1 : 0;

    /// <summary>
    /// Scans for the start of the zone's asset pool. Each pool entry is 8 bytes:
    /// [type word 4B][ptr 4B = FFFFFFFF]. We look for two consecutive entries to reduce
    /// false positives on stray markers in the script-string region.
    /// </summary>
    private static int FindAssetPoolStart(byte[] zoneData, bool isLE, int scanMax)
    {
        int end = Math.Min(zoneData.Length - 16, scanMax);
        for (int i = 0x40; i < end; i++)
        {
            if (!LooksLikeAssetEntry(zoneData, i, isLE)) continue;
            if (!LooksLikeAssetEntry(zoneData, i + 8, isLE)) continue;
            return i;
        }
        return -1;
    }

    private static bool LooksLikeAssetEntry(byte[] zoneData, int offset, bool isLE)
    {
        if (offset + 8 > zoneData.Length) return false;
        byte typeId = isLE ? zoneData[offset] : zoneData[offset + 3];
        byte hi0 = isLE ? zoneData[offset + 1] : zoneData[offset];
        byte hi1 = isLE ? zoneData[offset + 2] : zoneData[offset + 1];
        byte hi2 = isLE ? zoneData[offset + 3] : zoneData[offset + 2];
        if (hi0 != 0 || hi1 != 0 || hi2 != 0) return false;
        if (typeId < 0x01 || typeId > 0x2A) return false;  // valid asset-type-id range
        // ptr placeholder must be FFFFFFFF
        return zoneData[offset + 4] == 0xFF && zoneData[offset + 5] == 0xFF
            && zoneData[offset + 6] == 0xFF && zoneData[offset + 7] == 0xFF;
    }

    /// <summary>
    /// Maps a single asset type ID from the source enum to the target enum, per game.
    /// Returns null if the source type doesn't exist on the target (e.g. PS3-only
    /// vertexshader when converting PS3→Xbox 360, or PS3 pixelshader→PC).
    /// </summary>
    private static int? RemapAssetTypeId(byte srcType, GameVersion gv, int srcEnumKey, int dstEnumKey)
    {
        // Decode the source type id to a canonical "asset name" index using the per-game,
        // per-platform enum, then encode it for the target. Implemented as a switch on the
        // few asset types we actually care about (rawfile, localize, menufile, etc.) plus
        // a generic shift for the rest.
        //
        // CoD4 enum shifts (relative to PS3):
        //   PS3:     reference (has pixelshader 0x05, vertexshader 0x06)
        //   Xbox360: drops vertexshader 0x06 → IDs >= 0x06 shift -1
        //   PC:      drops both pixelshader 0x05 AND vertexshader 0x06 → IDs >= 0x05 shift -2
        //
        // WaW enum shifts (relative to PS3):
        //   PS3:     reference (has pixelshader 0x07, vertexshader 0x08)
        //   Xbox360: drops vertexshader 0x08 → IDs >= 0x08 shift -1
        //   PC:      drops both → IDs >= 0x07 shift -2
        //
        // MW2 enum shifts (relative to PS3):
        //   PS3:     reference (has vertexshader 0x07, no vertexdecl)
        //   Xbox360: drops vertexshader 0x07 → IDs >= 0x07 shift -1
        //   PC:      adds vertexdecl at 0x08 → IDs >= 0x08 shift +1
        //
        // To translate src→dst, normalize to PS3, then shift to dst.

        int ps3Type = ToPs3Type(srcType, gv, srcEnumKey);
        if (ps3Type < 0) return null;  // doesn't exist on source (shouldn't happen if input is valid)

        return FromPs3Type(ps3Type, gv, dstEnumKey);
    }

    private static int ToPs3Type(byte srcType, GameVersion gv, int srcEnumKey)
    {
        // srcEnumKey: 0=PS3, 1=Xbox360, 2=PC/Wii
        if (srcEnumKey == 0) return srcType;

        int dropAt = DropAtId(gv);  // first id that PS3 has and Xbox 360 doesn't
        int xboxToPs3Shift = 1;
        int pcDropOffset = gv == GameVersion.MW2 ? -1 : 1;  // MW2 PC ADDS at 0x08 (instead of dropping)

        if (srcEnumKey == 1)
        {
            // Xbox 360 → PS3: IDs >= dropAt shift +1
            return srcType < dropAt ? srcType : srcType + xboxToPs3Shift;
        }
        else
        {
            // PC/Wii → PS3.
            if (gv == GameVersion.MW2)
            {
                // PC adds vertexdecl at 0x08. PC IDs >= 0x09 shift -1 to PS3.
                if (srcType < 0x08) return srcType;
                if (srcType == 0x08) return -1;  // vertexdecl is PC-only
                return srcType - 1;
            }
            // CoD4/WaW PC drops BOTH pixelshader and vertexshader. PC IDs >= dropAt-1 shift +2.
            int pcDropAt = dropAt - 1;  // PC's first shifted id (one earlier than Xbox 360's)
            return srcType < pcDropAt ? srcType : srcType + 2;
        }
    }

    private static int? FromPs3Type(int ps3Type, GameVersion gv, int dstEnumKey)
    {
        if (dstEnumKey == 0) return ps3Type;

        int dropAt = DropAtId(gv);
        if (dstEnumKey == 1)
        {
            // PS3 → Xbox 360: vertexshader (id == dropAt) doesn't exist; IDs > dropAt shift -1.
            if (ps3Type == dropAt) return null;
            return ps3Type < dropAt ? ps3Type : ps3Type - 1;
        }
        // PS3 → PC/Wii.
        if (gv == GameVersion.MW2)
        {
            // PC adds vertexdecl at 0x08; PS3 IDs >= 0x08 shift +1.
            return ps3Type < 0x08 ? ps3Type : ps3Type + 1;
        }
        // CoD4/WaW PC drops pixelshader AND vertexshader; IDs >= pixelshader shift -2.
        int pcDropAt = dropAt - 1;
        if (ps3Type == pcDropAt || ps3Type == dropAt) return null;  // pixelshader/vertexshader
        return ps3Type < pcDropAt ? ps3Type : ps3Type - 2;
    }

    /// <summary>
    /// The id (in the PS3 enum) of the asset type that Xbox 360 drops — i.e. vertexshader.
    /// Used as the pivot point for all enum-shift calculations.
    /// </summary>
    private static int DropAtId(GameVersion gv) => gv switch
    {
        GameVersion.CoD4 => 0x06,
        GameVersion.WaW  => 0x08,
        GameVersion.MW2  => 0x07,
        _ => 0x08,
    };

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

    // Endian read/write primitives live in FastFileConstants (shared with the editor,
    // CLI, and CompilerGUI). These thin aliases keep the call sites in this file terse.
    private static uint ReadUInt32(byte[] data, int offset, bool isLE)
        => FastFileConstants.ReadUInt32(data, offset, isLE);

    private static void WriteUInt32(byte[] data, int offset, uint value, bool isLE)
        => FastFileConstants.WriteUInt32(data, offset, value, isLE);

    /// <summary>
    /// Compresses zone data for a specific platform. Routes through the canonical
    /// FastFileSaveService.Save (which dispatches to the right compressor internally)
    /// so each (game, platform) combo lands on the right format:
    ///   - CoD4/WaW PS3 + Xbox 360 (unsigned) -> block format
    ///   - CoD4/WaW PC                         -> single zlib stream (Compiler.CompilePc)
    ///   - MW2 PS3                             -> block format + 25-byte extended header
    ///   - MW2 Xbox 360                        -> single zlib stream + extended header
    ///   - MW2 PC                              -> single zlib stream + 9-byte preamble
    /// Always writes unsigned outputs (signed=false) — RSA re-signing is not supported.
    /// </summary>
    private static void CompressForPlatform(string zonePath, string outputPath, GameVersion gameVersion, Platform platform)
    {
        // Route through the canonical save service so every "produce FF on disk" path in the
        // codebase goes through one place (consistent logging, future format fixes apply once).
        FastFileSaveService.Save(zonePath, outputPath, gameVersion, PlatformToString(platform));
    }

    private static string PlatformToString(Platform platform) => platform switch
    {
        Platform.PS3 => "PS3",
        Platform.Xbox360 => "Xbox360",
        Platform.PC => "PC",
        Platform.Wii => "Wii",
        _ => "PS3"
    };

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
