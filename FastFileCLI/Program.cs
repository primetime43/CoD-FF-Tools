using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FastFileLib;

namespace FastFileCLI;

/// <summary>
/// FastFile CLI - a text-based debugging and batch-processing tool for CoD FastFiles.
///
/// Design philosophy: this is NOT a second GUI. It exists to do things the GUIs can't:
///   - paste-into-a-bug-report style diagnostics  (`ffcli report file.ff`)
///   - shell-loop batch operations               (`ffcli info *.ff`)
///   - machine-readable output for scripts       (`ffcli info file.ff --json`)
/// Interactive prompts have been removed in favor of these focused use cases.
/// </summary>
class Program
{
    // Set when we detect a drag-and-drop launch (a file was dropped onto ffcli.exe in
    // Windows Explorer). Used to decide whether to pause at the end so the user can
    // read the report before the auto-launched console window closes.
    private static bool _dragDropMode;

    static int Main(string[] args)
    {
        try
        {
            return Dispatch(args);
        }
        finally
        {
            // Pause when:
            //   - debugger attached (F5 in VS),
            //   - launched via drag-and-drop and the output isn't redirected.
            // Ctrl+F5 in VS injects its own pause, so we don't need to handle that.
            // Terminal use and piped output stay clean.
            bool shouldPause = Debugger.IsAttached
                            || (_dragDropMode && !Console.IsOutputRedirected);
            if (shouldPause)
            {
                Console.WriteLine();
                Console.Write("Press any key to continue...");
                Console.ReadKey(intercept: true);
            }
        }
    }

    static int Dispatch(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 0;
        }

        // Drag-and-drop shortcut: if every arg looks like a FastFile/zone path, run
        // 'report' on them. Windows passes dropped files as positional args, so
        // dropping patch_mp.ff onto ffcli.exe lands here as `ffcli "C:\...\patch_mp.ff"`.
        if (args.All(LooksLikeDroppedFile))
        {
            _dragDropMode = true;
            Console.WriteLine($"Detected {args.Length} dropped file(s) - running 'report'");
            Console.WriteLine();
            return ReportCommand(args);
        }

        string command = args[0].ToLower();
        try
        {
            return command switch
            {
                "info"                       => InfoCommand(args.Skip(1).ToArray()),
                "report" or "r"              => ReportCommand(args.Skip(1).ToArray()),
                "decompress" or "d"          => DecompressCommand(args.Skip(1).ToArray()),
                "compress"   or "c"          => CompressCommand(args.Skip(1).ToArray()),
                "list"       or "ls"         => ListCommand(args.Skip(1).ToArray()),
                "extract"    or "x"          => ExtractCommand(args.Skip(1).ToArray()),
                "patch"      or "p"          => PatchCommand(args.Skip(1).ToArray()),
                "help"       or "-h" or "--help" => Help(args.Skip(1).ToArray()),
                _                            => UnknownCommand(command)
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (Environment.GetEnvironmentVariable("FFCLI_DEBUG") != null)
                Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    /// <summary>
    /// True if the arg is the absolute or relative path to an existing file with
    /// a FastFile-ish extension. Used to detect Windows Explorer drag-and-drop launches.
    /// </summary>
    static bool LooksLikeDroppedFile(string arg)
    {
        if (string.IsNullOrWhiteSpace(arg)) return false;
        string ext = Path.GetExtension(arg).ToLowerInvariant();
        if (ext != ".ff" && ext != ".ffm" && ext != ".zone") return false;
        return File.Exists(arg);
    }

    // -----------------------------------------------------------------
    //  Help / usage
    // -----------------------------------------------------------------

    static void PrintUsage()
    {
        Console.WriteLine("FastFile CLI - CoD FastFile debugging & batch tool");
        Console.WriteLine();
        Console.WriteLine("Usage: ffcli <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  info <file.ff> [--json]                Print FastFile header info");
        Console.WriteLine("  report, r <file.ff>                    Full diagnostic report (for bug reports)");
        Console.WriteLine("  decompress, d <file.ff> [out.zone]     Decompress to a zone file");
        Console.WriteLine("  compress, c <file.zone> <out.ff>       Compress a zone into a FastFile");
        Console.WriteLine("  list, ls <file.ff|.zone> [-v]          List raw files in a FF/zone");
        Console.WriteLine("  extract, x <file> <dir> [--filter X]   Extract raw files");
        Console.WriteLine("  patch, p <file.zone> <name> <content>  Patch a raw file in place");
        Console.WriteLine("  help [command]                         Detailed help for a command");
        Console.WriteLine();
        Console.WriteLine("Globs: most read commands accept shell-style wildcards.");
        Console.WriteLine("  ffcli info *.ff");
        Console.WriteLine("  ffcli report patch_*.ff");
        Console.WriteLine();
        Console.WriteLine("Bug reports: include the output of `ffcli report your.ff`.");
        Console.WriteLine("Debug: set FFCLI_DEBUG=1 for stack traces.");
    }

    static int Help(string[] args)
    {
        if (args.Length == 0) { PrintUsage(); return 0; }

        switch (args[0].ToLower())
        {
            case "info":
                Console.WriteLine("Usage: ffcli info <file.ff> [--json]");
                Console.WriteLine();
                Console.WriteLine("Prints header info: magic, version, game, platform, signed status, file size.");
                Console.WriteLine("--json emits a machine-readable object (array if multiple files).");
                Console.WriteLine("File argument accepts globs: ffcli info *.ff");
                break;

            case "report":
            case "r":
                Console.WriteLine("Usage: ffcli report <file.ff>");
                Console.WriteLine();
                Console.WriteLine("Prints a full diagnostic report including:");
                Console.WriteLine("  - FastFile header (magic, version, detected game/platform)");
                Console.WriteLine("  - MW2 extended header (if MW2)");
                Console.WriteLine("  - Xbox 360 streaming header + hash table layout (if signed)");
                Console.WriteLine("  - Compressed data stream start/length/end-marker");
                Console.WriteLine("  - Zone header (if decompression succeeds), with MemAlloc validation");
                Console.WriteLine("  - Asset pool type counts");
                Console.WriteLine("  - Detected format issues");
                Console.WriteLine();
                Console.WriteLine("Designed to be pasted into bug reports - it tells you everything a maintainer");
                Console.WriteLine("would need to ask for before diagnosing a decompression or format problem.");
                Console.WriteLine("File argument accepts globs: ffcli report patch_*.ff");
                break;

            case "decompress":
            case "d":
                Console.WriteLine("Usage: ffcli decompress <file.ff> [output.zone]");
                Console.WriteLine("Default output: <inputname>.zone");
                break;

            case "compress":
            case "c":
                Console.WriteLine("Usage: ffcli compress <file.zone> <output.ff> [options]");
                Console.WriteLine();
                Console.WriteLine("Options:");
                Console.WriteLine("  --game <cod4|waw|mw2>      Target game (default: waw)");
                Console.WriteLine("  --platform <ps3|xbox|pc>   Target platform (default: ps3)");
                Console.WriteLine("  --signed                   Use Xbox 360 signed format (requires --original)");
                Console.WriteLine("  --original <file.ff>       Original FF for signed-format hash table preservation");
                break;

            case "list":
            case "ls":
                Console.WriteLine("Usage: ffcli list <file.ff|.zone> [-v|--verbose]");
                Console.WriteLine("Lists raw files; -v adds offset + size columns.");
                break;

            case "extract":
            case "x":
                Console.WriteLine("Usage: ffcli extract <file.ff|.zone> <output_dir> [options]");
                Console.WriteLine();
                Console.WriteLine("Options:");
                Console.WriteLine("  --filter <pattern>    Only extract files whose name contains <pattern>");
                Console.WriteLine("  --flat                Don't preserve directory structure");
                break;

            case "patch":
            case "p":
                Console.WriteLine("Usage: ffcli patch <file.zone> <rawfile_name> <content_file>");
                Console.WriteLine();
                Console.WriteLine("Replaces the contents of a raw file in place. Content must fit in the");
                Console.WriteLine("existing slot - use the GUI editor to grow a raw file.");
                break;

            default:
                Console.WriteLine($"Unknown command: {args[0]}");
                return 1;
        }
        return 0;
    }

    static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        Console.Error.WriteLine("Run 'ffcli help' for usage.");
        return 1;
    }

    // -----------------------------------------------------------------
    //  info
    // -----------------------------------------------------------------

    static int InfoCommand(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: ffcli info <file.ff> [--json]");
            return 1;
        }

        bool json = args.Any(a => a == "--json");
        var paths = ExpandFileArgs(args.Where(a => !a.StartsWith("--")));
        if (paths.Count == 0)
        {
            Console.Error.WriteLine("No files matched.");
            return 1;
        }

        if (json)
        {
            var results = paths.Select(BuildInfoObject).ToList();
            // Single file: emit object. Multiple: emit array.
            object output = results.Count == 1 ? (object)results[0] : results;
            Console.WriteLine(JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        bool first = true;
        foreach (var path in paths)
        {
            if (!first) Console.WriteLine();
            first = false;
            PrintInfoText(path);
        }
        return 0;
    }

    static object BuildInfoObject(string path)
    {
        var info = FastFileInfo.FromFile(path);
        var fi = new FileInfo(path);
        return new
        {
            file = Path.GetFileName(path),
            path,
            size_bytes = fi.Length,
            size_pretty = FastFileInfo.FormatFileSize(fi.Length),
            magic = info.Magic,
            version_hex = $"0x{info.Version:X8}",
            game = info.GameName,
            platform = info.Platform,
            studio = info.Studio,
            signed = info.IsSigned,
            is_pc = info.IsPC,
            is_wii = info.IsWii
        };
    }

    static void PrintInfoText(string path)
    {
        var info = FastFileInfo.FromFile(path);
        var fi = new FileInfo(path);
        Console.WriteLine($"File:      {Path.GetFileName(path)}");
        Console.WriteLine($"Size:      {FastFileInfo.FormatFileSize(fi.Length)} ({fi.Length:N0} bytes)");
        Console.WriteLine($"Magic:     {info.Magic}");
        Console.WriteLine($"Version:   0x{info.Version:X8}");
        Console.WriteLine($"Game:      {info.GameName}");
        Console.WriteLine($"Platform:  {info.Platform}");
        Console.WriteLine($"Studio:    {info.Studio}");
        Console.WriteLine($"Signed:    {(info.IsSigned ? "Yes" : "No")}");
    }

    // -----------------------------------------------------------------
    //  report — the bug-report-friendly diagnostic dump
    // -----------------------------------------------------------------

    static int ReportCommand(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: ffcli report <file.ff>");
            return 1;
        }

        var paths = ExpandFileArgs(args);
        if (paths.Count == 0)
        {
            Console.Error.WriteLine("No files matched.");
            return 1;
        }

        bool first = true;
        foreach (var path in paths)
        {
            if (!first) { Console.WriteLine(); Console.WriteLine(new string('=', 70)); Console.WriteLine(); }
            first = false;
            PrintReport(path);
        }
        return 0;
    }

    static void PrintReport(string path)
    {
        byte[] ff;
        try { ff = File.ReadAllBytes(path); }
        catch (Exception ex) { Console.Error.WriteLine($"Cannot read {path}: {ex.Message}"); return; }

        Console.WriteLine($"=== FastFile Report ===");
        Console.WriteLine($"File: {Path.GetFileName(path)}");
        Console.WriteLine($"Path: {path}");
        Console.WriteLine($"Size: 0x{ff.Length:X} ({ff.Length:N0} bytes)");
        Console.WriteLine();

        // --- FastFile header ---
        Console.WriteLine("== FastFile Header ==");
        if (ff.Length < 12)
        {
            Console.WriteLine("  ERROR: file too small to contain a header (< 12 bytes).");
            return;
        }

        FastFileInfo info;
        try { info = FastFileInfo.FromFile(path); }
        catch (Exception ex)
        {
            Console.WriteLine($"  ERROR parsing header: {ex.Message}");
            Console.WriteLine($"  First 16 bytes: {HexBytes(ff, 0, Math.Min(16, ff.Length))}");
            return;
        }

        Console.WriteLine($"  Magic:    {info.Magic}");
        Console.WriteLine($"  Version:  0x{info.Version:X8}");
        Console.WriteLine($"  Game:     {info.GameName}");
        Console.WriteLine($"  Platform: {info.Platform}");
        Console.WriteLine($"  Studio:   {info.Studio}");
        Console.WriteLine($"  Signed:   {(info.IsSigned ? "Yes" : "No")}");
        Console.WriteLine($"  First 16 bytes: {HexBytes(ff, 0, 16)}");
        Console.WriteLine();

        var issues = new List<string>();

        // --- MW2 extended header ---
        if (info.GameName.StartsWith("MW2") && ff.Length >= 12 + 25)
        {
            Console.WriteLine("== MW2 Extended Header (DB_Header) ==");
            int pos = 12;
            byte allowOnlineUpdate = ff[pos];
            uint region = ReadU32BE(ff, pos + 9);
            uint entryCount = ReadU32BE(ff, pos + 13);

            Console.WriteLine($"  allowOnlineUpdate: 0x{allowOnlineUpdate:X2}");
            Console.WriteLine($"  region:            0x{region:X8}");
            Console.WriteLine($"  entryCount:        {entryCount}");

            if (entryCount < 10000)
            {
                int fileSizesOff = pos + 17 + (int)entryCount * 0x14;
                if (fileSizesOff + 8 <= ff.Length)
                {
                    uint storedFileSize = ReadU32BE(ff, fileSizesOff);
                    uint storedMaxFileSize = ReadU32BE(ff, fileSizesOff + 4);
                    Console.WriteLine($"  fileSize (stored): 0x{storedFileSize:X} ({storedFileSize:N0} bytes)");
                    Console.WriteLine($"  maxFileSize:       0x{storedMaxFileSize:X}");
                    if (storedFileSize != 0 && storedFileSize != ff.Length)
                        issues.Add($"MW2 stored fileSize (0x{storedFileSize:X}) != actual FF size (0x{ff.Length:X})");
                }
            }
            Console.WriteLine();
        }

        // --- Xbox 360 streaming header (IWff0100 + IWffs100) ---
        bool hasStreaming = ff.Length >= FastFileConstants.Xbox360SignedZlibStart
                         && Encoding.ASCII.GetString(ff, 0x0C, 8) == FastFileConstants.StreamingHeader;
        if (hasStreaming)
        {
            Console.WriteLine("== Xbox 360 Streaming Header ==");
            Console.WriteLine($"  IWffs100 magic at:    0x0C");
            Console.WriteLine($"  Hash table:           0x{FastFileConstants.Xbox360SignedHashTableStart:X} .. 0x{FastFileConstants.Xbox360SignedHashTableEnd:X}");
            Console.WriteLine($"  Auth data:            0x{FastFileConstants.Xbox360SignedHashTableEnd:X} .. 0x{FastFileConstants.Xbox360SignedAuthDataEnd:X}");
            Console.WriteLine($"  Compressed stream at: 0x{FastFileConstants.Xbox360SignedZlibStart:X}");
            int s = FastFileConstants.Xbox360SignedZlibStart;
            if (s + 2 <= ff.Length)
            {
                Console.WriteLine($"  Stream first 2 bytes: 0x{ff[s]:X2} 0x{ff[s + 1]:X2}" +
                    $" {(FastFileConstants.HasZlibHeader(ff, s) ? "(zlib header)" : "(raw deflate or other)")}");
            }
            Console.WriteLine();
        }

        // --- Compressed data ---
        int dataStart;
        if (hasStreaming) dataStart = FastFileConstants.Xbox360SignedZlibStart;
        else if (info.GameName.StartsWith("MW2")) dataStart = 12 + 25;
        else dataStart = 12;

        Console.WriteLine("== Compressed Data ==");
        Console.WriteLine($"  Stream start:  0x{dataStart:X}");
        Console.WriteLine($"  Stream length: 0x{ff.Length - dataStart:X} ({ff.Length - dataStart:N0} bytes)");
        if (ff.Length >= 2)
        {
            byte b0 = ff[^2], b1 = ff[^1];
            string marker = b0 == 0x00 && b1 == 0x01 ? "block-format end marker" : "(no end marker)";
            Console.WriteLine($"  Last 2 bytes:  0x{b0:X2} 0x{b1:X2}  {marker}");
        }

        // --- Decompression attempt ---
        byte[]? zone = null;
        string? decompressError = null;
        try { zone = DecompressFf(path); }
        catch (Exception ex) { decompressError = ex.Message; }

        if (zone != null)
            Console.WriteLine($"  Decompressed:  0x{zone.Length:X} ({zone.Length:N0} bytes) [OK]");
        else
            Console.WriteLine($"  Decompressed:  FAILED - {decompressError}");
        Console.WriteLine();

        if (zone == null)
        {
            Console.WriteLine("Cannot report on zone contents because decompression failed.");
            if (issues.Count > 0) PrintIssues(issues);
            return;
        }

        // --- Zone header ---
        Console.WriteLine("== Zone Header ==");
        bool isXbox360 = info.Platform == "Xbox 360";
        bool isPC = info.IsPC;
        bool isWii = info.IsWii;
        var gv = info.GameVersion;
        int hdrSize = FastFileConstants.GetZoneHeaderSize(gv, isXbox360, isPC, isWii);

        if (zone.Length < hdrSize)
        {
            Console.WriteLine($"  ERROR: zone too small for expected header size {hdrSize}");
        }
        else
        {
            // PC zones store everything little-endian; console zones are big-endian.
            Func<byte[], int, uint> readU32 = isPC ? (Func<byte[], int, uint>)ReadU32LE : ReadU32BE;

            uint zoneSize     = readU32(zone, 0x00);
            uint blockTemp    = readU32(zone, 0x08);
            uint blockLarge   = readU32(zone, 0x18);
            uint blockVertex  = hdrSize >= 0x34 ? readU32(zone, 0x20) : 0;
            int  slOffset     = FastFileConstants.GetScriptStringCountOffset(gv, isXbox360, isPC, isWii);
            uint scriptCount  = readU32(zone, slOffset);
            uint assetCount   = readU32(zone, slOffset + 8);

            Console.WriteLine($"  Header size:        {hdrSize} (0x{hdrSize:X})");
            Console.WriteLine($"  Endianness:         {(isPC ? "little-endian (PC)" : "big-endian (console)")}");
            Console.WriteLine($"  ZoneSize:           0x{zoneSize:X8} ({zoneSize:N0})");
            Console.WriteLine($"  BlockSizeTemp:      0x{blockTemp:X8}");
            Console.WriteLine($"  BlockSizeLarge:     0x{blockLarge:X8}");
            if (hdrSize >= 0x34)
                Console.WriteLine($"  BlockSizeVertex:    0x{blockVertex:X8}");
            Console.WriteLine($"  ScriptStringCount:  {scriptCount}");
            Console.WriteLine($"  AssetCount:         {assetCount}");

            // MemAlloc validation - PC computes per-zone, no fixed value to validate against.
            if (!isPC)
            {
                uint expected = ExpectedMemAlloc1(gv, isXbox360);
                if (expected != 0 && blockTemp != expected)
                    issues.Add($"Zone BlockSizeTemp 0x{blockTemp:X} != expected 0x{expected:X} for {info.GameName} {info.Platform}");
            }

            // --- Asset pool type counts ---
            int poolStart = hdrSize;
            int poolEnd = poolStart + (int)assetCount * 8;
            if (poolEnd <= zone.Length && assetCount > 0 && assetCount < 100_000)
            {
                Console.WriteLine();
                Console.WriteLine("== Asset Pool ==");
                Console.WriteLine($"  Range:    0x{poolStart:X} .. 0x{poolEnd:X}");
                Console.WriteLine($"  Entries:  {assetCount}");

                // Detect entry layout via the shared primitive: each 8-byte entry is a 4-byte
                // type word + a 4-byte FFFFFFFF pointer; offset 0 = [type][ptr], 4 = [ptr][type].
                // Type bytes are little-endian on PC, big-endian on console.
                int typeFieldOff = ZoneAssetPool.DetectTypeFieldOffset(zone, poolStart);
                Console.WriteLine($"  Layout:   {(typeFieldOff == 0 ? "[type][ptr]" : "[ptr][type]")}");

                var counts = new Dictionary<byte, int>();
                for (int i = 0; i + 8 <= poolEnd - poolStart; i += 8)
                {
                    uint typeWord = isPC
                        ? ReadU32LE(zone, poolStart + i + typeFieldOff)
                        : ReadU32BE(zone, poolStart + i + typeFieldOff);
                    byte t = (byte)(typeWord & 0xFF);
                    counts[t] = counts.GetValueOrDefault(t) + 1;
                }
                Console.WriteLine($"  Type counts:");
                foreach (var kv in counts.OrderByDescending(kv => kv.Value))
                    Console.WriteLine($"    0x{kv.Key:X2}  count={kv.Value}");
            }
        }

        if (issues.Count > 0)
        {
            Console.WriteLine();
            PrintIssues(issues);
        }
    }

    static void PrintIssues(List<string> issues)
    {
        Console.WriteLine("== Issues Detected ==");
        foreach (var s in issues)
            Console.WriteLine($"  ! {s}");
    }

    static uint ExpectedMemAlloc1(GameVersion gv, bool isXbox360)
    {
        return gv switch
        {
            GameVersion.CoD4 => FastFileLib.GameDefinitions.CoD4Definition.MemAlloc1Value,
            GameVersion.WaW  => isXbox360 ? FastFileLib.GameDefinitions.CoD5Definition.Xbox360MemAlloc1Value
                                          : FastFileLib.GameDefinitions.CoD5Definition.MemAlloc1Value,
            GameVersion.MW2  => FastFileLib.GameDefinitions.MW2Definition.MemAlloc1Value,
            _ => 0
        };
    }

    // -----------------------------------------------------------------
    //  decompress / compress
    // -----------------------------------------------------------------

    static int DecompressCommand(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: ffcli decompress <file.ff> [output.zone]");
            return 1;
        }

        string inputPath = args[0];
        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"File not found: {inputPath}");
            return 1;
        }

        string outputPath = args.Length > 1 ? args[1] : Path.ChangeExtension(inputPath, ".zone");

        Console.WriteLine($"Decompressing: {Path.GetFileName(inputPath)}");
        var info = FastFileInfo.FromFile(inputPath);
        Console.WriteLine($"  Game: {info.GameName}, Platform: {info.Platform}");

        // Use FastFileProcessor (writes to file directly) - handles all platforms
        FastFileProcessor.Decompress(inputPath, outputPath);
        long outputSize = new FileInfo(outputPath).Length;

        Console.WriteLine($"  Output: {outputPath}");
        Console.WriteLine($"  Zone size: {FastFileInfo.FormatFileSize(outputSize)}");
        return 0;
    }

    static int CompressCommand(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: ffcli compress <file.zone> <output.ff> [options]");
            return 1;
        }

        string inputPath = args[0];
        string outputPath = args[1];
        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"File not found: {inputPath}");
            return 1;
        }

        // Auto-detect game + platform via the lib's shared DetectFromZone (same detection
        // the editor's "Compress Zone to FF" flow uses). --game / --platform / --signed
        // overrides applied below — explicit user choice always wins.
        byte[] zoneBytes = File.ReadAllBytes(inputPath);
        var (gameVersion, platform) = FastFileSaveService.DetectFromZone(zoneBytes);
        bool signed = false;
        string? originalFf = null;

        for (int i = 2; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "--game":
                    if (i + 1 >= args.Length) { Console.Error.WriteLine("--game requires a value"); return 1; }
                    gameVersion = args[++i].ToLower() switch
                    {
                        "cod4" => GameVersion.CoD4,
                        "waw" or "cod5" => GameVersion.WaW,
                        "mw2" => GameVersion.MW2,
                        _ => throw new ArgumentException($"Unknown game: {args[i]}")
                    };
                    break;
                case "--platform":
                    if (i + 1 >= args.Length) { Console.Error.WriteLine("--platform requires a value"); return 1; }
                    platform = args[++i].ToLower() switch
                    {
                        "ps3" => "PS3",
                        "xbox" or "xbox360" => "Xbox360",
                        "pc" => "PC",
                        "wii" => "Wii",
                        _ => throw new ArgumentException($"Unknown platform: {args[i]}")
                    };
                    break;
                case "--signed":
                    signed = true;
                    platform = "Xbox360";
                    break;
                case "--original":
                    if (i + 1 >= args.Length) { Console.Error.WriteLine("--original requires a file path"); return 1; }
                    originalFf = args[++i];
                    break;
            }
        }

        if (signed && string.IsNullOrEmpty(originalFf))
        {
            Console.Error.WriteLine("Signed format requires --original <file.ff>");
            return 1;
        }

        Console.WriteLine($"Compressing: {Path.GetFileName(inputPath)}");
        Console.WriteLine($"  Game: {gameVersion}, Platform: {platform}");

        // The lib's save service is the single canonical entry point — it routes through
        // FastFileProcessor.Recompress which handles every platform variant (single zlib
        // for PC/Wii, block format for PS3/Xbox 360 unsigned, signed Xbox 360 streaming,
        // MW2 PC quirky preamble). Editor uses the same call.
        FastFileSaveService.Save(inputPath, outputPath, gameVersion, platform, signed, originalFf);

        Console.WriteLine($"  Output: {outputPath}");
        Console.WriteLine($"  FF size: {FastFileInfo.FormatFileSize(new FileInfo(outputPath).Length)}");
        return 0;
    }

    // -----------------------------------------------------------------
    //  list / extract / patch
    // -----------------------------------------------------------------

    static int ListCommand(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: ffcli list <file.ff|.zone>");
            return 1;
        }

        string inputPath = args[0];
        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"File not found: {inputPath}");
            return 1;
        }

        bool verbose = args.Any(a => a == "-v" || a == "--verbose");

        byte[] zoneData = LoadZoneData(inputPath);
        var rawFiles = ScanRawFiles(inputPath, zoneData);

        Console.WriteLine();
        Console.WriteLine($"Found {rawFiles.Count} raw file(s):");
        Console.WriteLine();

        if (verbose)
        {
            Console.WriteLine($"{"Name",-50} {"Offset",10} {"Size",10} {"OnDisk",10}");
            Console.WriteLine(new string('-', 83));
            foreach (var rf in rawFiles)
            {
                int onDisk = rf.CompressedSize > 0 ? rf.CompressedSize : rf.DataSize;
                Console.WriteLine($"{rf.Name,-50} {rf.DataOffset,10} {rf.DataSize,10} {onDisk,10}");
            }
        }
        else
        {
            foreach (var rf in rawFiles) Console.WriteLine($"  {rf.Name}");
        }
        return 0;
    }

    static int ExtractCommand(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: ffcli extract <file.ff|.zone> <output_dir>");
            return 1;
        }

        string inputPath = args[0];
        string outputDir = args[1];
        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"File not found: {inputPath}");
            return 1;
        }

        string? filter = null;
        bool flat = false;
        for (int i = 2; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "--filter":
                    if (i + 1 >= args.Length) { Console.Error.WriteLine("--filter requires a pattern"); return 1; }
                    filter = args[++i];
                    break;
                case "--flat":
                    flat = true;
                    break;
            }
        }

        byte[] zoneData = LoadZoneData(inputPath);
        var rawFiles = ScanRawFiles(inputPath, zoneData);
        if (!string.IsNullOrEmpty(filter))
            rawFiles = rawFiles.Where(rf => rf.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

        Console.WriteLine($"Extracting {rawFiles.Count} file(s) to {outputDir}");
        Directory.CreateDirectory(outputDir);

        int extracted = 0;
        foreach (var rf in rawFiles)
        {
            string outputPath;
            if (flat) outputPath = Path.Combine(outputDir, Path.GetFileName(rf.Name));
            else
            {
                outputPath = Path.Combine(outputDir, rf.Name.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            }

            // RawFileLocation.Data is already decompressed by the scanner — write the
            // decompressed payload, not the on-disk zlib bytes.
            File.WriteAllBytes(outputPath, rf.Data);
            Console.WriteLine($"  {rf.Name}");
            extracted++;
        }

        Console.WriteLine($"Extracted {extracted} file(s).");
        return 0;
    }

    static int PatchCommand(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Usage: ffcli patch <file.zone> <rawfile_name> <content_file>");
            return 1;
        }

        string zonePath = args[0];
        string rawFileName = args[1];
        string contentPath = args[2];

        if (!File.Exists(zonePath)) { Console.Error.WriteLine($"Zone file not found: {zonePath}"); return 1; }
        if (!File.Exists(contentPath)) { Console.Error.WriteLine($"Content file not found: {contentPath}"); return 1; }

        var (zoneData, gv, isPC) = LoadZoneContext(zonePath);
        byte[] newContent = File.ReadAllBytes(contentPath);

        var rawFiles = RawFileScanner.FindRawFiles(zoneData, gv, isPC);
        var target = rawFiles.FirstOrDefault(rf =>
            rf.Name.Equals(rawFileName, StringComparison.OrdinalIgnoreCase) ||
            rf.Name.EndsWith("/" + rawFileName, StringComparison.OrdinalIgnoreCase));

        if (target == null)
        {
            Console.Error.WriteLine($"Raw file not found: {rawFileName}");
            Console.Error.WriteLine("Available files:");
            foreach (var rf in rawFiles.Take(10)) Console.Error.WriteLine($"  {rf.Name}");
            if (rawFiles.Count > 10) Console.Error.WriteLine($"  ... and {rawFiles.Count - 10} more");
            return 1;
        }

        // Compute the on-disk payload for the new content. MW2 entries store payloads
        // zlib-compressed; preserve that compression state so the engine can read it.
        byte[] newPayload;
        uint newCompressedLen;
        if (target.WasCompressed)
        {
            newPayload = FastFileLib.CompressionHelper.CompressZlib(newContent);
            newCompressedLen = (uint)newPayload.Length;
        }
        else
        {
            newPayload = newContent;
            newCompressedLen = 0;  // 0 = stored uncompressed (engine reads len bytes raw)
        }

        int oldOnDiskSize = target.CompressedSize > 0 ? target.CompressedSize : target.DataSize;
        int newOnDiskSize = newPayload.Length;
        int delta = newOnDiskSize - oldOnDiskSize;

        Console.WriteLine($"Patching: {target.Name}");
        Console.WriteLine($"  Old uncompressed: {target.DataSize}  on-disk: {oldOnDiskSize}");
        Console.WriteLine($"  New uncompressed: {newContent.Length}  on-disk: {newOnDiskSize}  delta: {delta:+#;-#;0}");

        // Build the new zone with the payload swapped in. Tail (everything after the
        // old payload) shifts by `delta` so other rawfiles + footer stay intact.
        byte[] newZone;
        if (delta == 0)
        {
            newZone = zoneData;
            Buffer.BlockCopy(newPayload, 0, newZone, target.DataOffset, newOnDiskSize);
        }
        else
        {
            newZone = new byte[zoneData.Length + delta];
            Buffer.BlockCopy(zoneData, 0, newZone, 0, target.DataOffset);
            Buffer.BlockCopy(newPayload, 0, newZone, target.DataOffset, newOnDiskSize);
            int tailStart = target.DataOffset + oldOnDiskSize;
            Buffer.BlockCopy(zoneData, tailStart, newZone, target.DataOffset + newOnDiskSize, zoneData.Length - tailStart);
        }

        // Update the rawfile entry's size field(s). For MW2 (HeaderSize >= 16) we also
        // need to update compressedLen at SizeOffset - 4. Endianness is LE on PC.
        WriteSizeField(newZone, target.SizeOffset, (uint)newContent.Length, isPC);
        if (target.HeaderSize >= 16)
            WriteSizeField(newZone, target.SizeOffset - 4, newCompressedLen, isPC);

        // Update zone-level ZoneSize @ 0x00 and BlockSizeLarge @ 0x18 by delta.
        // Other XFile block sizes are pool hints for non-rawfile asset categories —
        // don't need to change when a rawfile grows/shrinks.
        if (delta != 0)
        {
            uint zoneSize = ReadSizeField(newZone, 0x00, isPC);
            uint blockLarge = ReadSizeField(newZone, 0x18, isPC);
            WriteSizeField(newZone, 0x00, (uint)((long)zoneSize + delta), isPC);
            WriteSizeField(newZone, 0x18, (uint)((long)blockLarge + delta), isPC);
        }

        File.WriteAllBytes(zonePath, newZone);
        Console.WriteLine($"  Patched successfully. Zone size: {zoneData.Length} -> {newZone.Length}");
        return 0;
    }

    /// <summary>
    /// Writes a 32-bit unsigned int to <paramref name="data"/> at <paramref name="offset"/>
    /// using little-endian for PC zones, big-endian otherwise. The rawfile scanner reads
    /// sizes with the same rule — keeps reads and writes in sync.
    /// </summary>
    static void WriteSizeField(byte[] data, int offset, uint value, bool isPC)
        => FastFileConstants.WriteUInt32(data, offset, value, littleEndian: isPC);

    static uint ReadSizeField(byte[] data, int offset, bool isPC)
        => FastFileConstants.ReadUInt32(data, offset, littleEndian: isPC);

    // -----------------------------------------------------------------
    //  Helpers
    // -----------------------------------------------------------------

    /// <summary>
    /// Expands shell-style globs (* and ?) in file path arguments. Non-glob paths are
    /// passed through unchanged. Resolves relative to the current working directory.
    /// </summary>
    static List<string> ExpandFileArgs(IEnumerable<string> patterns)
    {
        var results = new List<string>();
        foreach (var pat in patterns)
        {
            if (!pat.Contains('*') && !pat.Contains('?'))
            {
                if (File.Exists(pat)) results.Add(pat);
                else Console.Error.WriteLine($"File not found: {pat}");
                continue;
            }

            string dir = Path.GetDirectoryName(pat) ?? "";
            string filePattern = Path.GetFileName(pat);
            if (string.IsNullOrEmpty(dir)) dir = ".";
            if (!Directory.Exists(dir))
            {
                Console.Error.WriteLine($"Directory not found: {dir}");
                continue;
            }
            foreach (var f in Directory.EnumerateFiles(dir, filePattern))
                results.Add(f);
        }
        return results;
    }

    static byte[] LoadZoneData(string inputPath)
    {
        string ext = Path.GetExtension(inputPath).ToLower();
        if (ext == ".ff" || ext == ".ffm")
        {
            Console.WriteLine($"Decompressing {Path.GetFileName(inputPath)}...");
            return DecompressFf(inputPath);
        }
        return File.ReadAllBytes(inputPath);
    }

    /// <summary>
    /// Loads zone bytes AND detects game/platform so the rawfile scanner picks
    /// the right header layout. For .ff inputs we read the magic+version header;
    /// for .zone inputs we sniff MemAlloc1 endianness.
    /// </summary>
    static (byte[] data, GameVersion gv, bool isPC) LoadZoneContext(string inputPath)
    {
        string ext = Path.GetExtension(inputPath).ToLower();
        if (ext == ".ff" || ext == ".ffm")
        {
            var info = FastFileInfo.FromFile(inputPath);
            Console.WriteLine($"Decompressing {Path.GetFileName(inputPath)}...");
            return (DecompressFf(inputPath), info.GameVersion, info.IsPC);
        }
        var bytes = File.ReadAllBytes(inputPath);
        var gv = FastFileInfo.DetectGameFromZoneData(bytes);
        var isPC = FastFileInfo.IsZoneDataPC(bytes);
        return (bytes, gv, isPC);
    }

    /// <summary>
    /// Locates rawfile entries using the canonical <see cref="RawFileScanner"/>.
    /// Handles CoD4/WaW 12-byte BE/LE, MW2 console 16/20-byte BE, and MW2 PC 16-byte LE
    /// headers, with inline zlib decompression for MW2 payloads.
    /// </summary>
    static IReadOnlyList<RawFileLocation> ScanRawFiles(string inputPath, byte[] zoneData)
    {
        string ext = Path.GetExtension(inputPath).ToLower();
        GameVersion gv;
        bool isPC;
        if (ext == ".ff" || ext == ".ffm")
        {
            var info = FastFileInfo.FromFile(inputPath);
            gv = info.GameVersion;
            isPC = info.IsPC;
        }
        else
        {
            gv = FastFileInfo.DetectGameFromZoneData(zoneData);
            isPC = FastFileInfo.IsZoneDataPC(zoneData);
        }
        return RawFileScanner.FindRawFiles(zoneData, gv, isPC);
    }

    /// <summary>
    /// Decompresses an FF using FastFileProcessor (handles all platforms: PS3 blocks,
    /// PC single-stream, MW2, signed Xbox 360 streaming, Wii). The older Decompressor
    /// class only handles block format and chokes on PC files.
    /// </summary>
    static byte[] DecompressFf(string inputPath)
    {
        string tempZone = Path.GetTempFileName();
        try
        {
            FastFileProcessor.Decompress(inputPath, tempZone);
            return File.ReadAllBytes(tempZone);
        }
        finally
        {
            try { File.Delete(tempZone); } catch { /* ignore */ }
        }
    }

    static uint ReadU32BE(byte[] data, int offset) => FastFileConstants.ReadUInt32BigEndian(data, offset);

    static uint ReadU32LE(byte[] data, int offset) => FastFileConstants.ReadUInt32LittleEndian(data, offset);

    static string HexBytes(byte[] data, int offset, int length)
    {
        var sb = new StringBuilder(length * 3);
        for (int i = 0; i < length && offset + i < data.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(data[offset + i].ToString("X2"));
        }
        return sb.ToString();
    }

}
