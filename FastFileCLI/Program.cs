using System.Text;
using FastFileLib;

namespace FastFileCLI;

class Program
{
    static int Main(string[] args)
    {
        // Interactive mode when no arguments provided
        if (args.Length == 0)
        {
            return InteractiveMode();
        }

        string command = args[0].ToLower();
        int result;

        try
        {
            result = command switch
            {
                "info" => InfoCommand(args.Skip(1).ToArray()),
                "decompress" or "d" => DecompressCommand(args.Skip(1).ToArray()),
                "compress" or "c" => CompressCommand(args.Skip(1).ToArray()),
                "list" or "ls" => ListCommand(args.Skip(1).ToArray()),
                "extract" or "x" => ExtractCommand(args.Skip(1).ToArray()),
                "patch" or "p" => PatchCommand(args.Skip(1).ToArray()),
                "help" or "-h" or "--help" => Help(args.Skip(1).ToArray()),
                _ => UnknownCommand(command)
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (Environment.GetEnvironmentVariable("FFCLI_DEBUG") != null)
            {
                Console.Error.WriteLine(ex.StackTrace);
            }
            result = 1;
        }

        return result;
    }

    static int InteractiveMode()
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine("===========================================");
            Console.WriteLine("  FastFile CLI Tool - Interactive Mode");
            Console.WriteLine("===========================================");
            Console.WriteLine();
            Console.WriteLine("  1. Show FastFile Info");
            Console.WriteLine("  2. Decompress FastFile to Zone");
            Console.WriteLine("  3. Compress Zone to FastFile");
            Console.WriteLine("  4. List Raw Files");
            Console.WriteLine("  5. Extract Raw Files");
            Console.WriteLine("  6. Patch Raw File");
            Console.WriteLine();
            Console.WriteLine("  0. Exit");
            Console.WriteLine();
            Console.Write("Select an option: ");

            var key = Console.ReadKey();
            Console.WriteLine();
            Console.WriteLine();

            try
            {
                switch (key.KeyChar)
                {
                    case '1':
                        InteractiveInfo();
                        break;
                    case '2':
                        InteractiveDecompress();
                        break;
                    case '3':
                        InteractiveCompress();
                        break;
                    case '4':
                        InteractiveList();
                        break;
                    case '5':
                        InteractiveExtract();
                        break;
                    case '6':
                        InteractivePatch();
                        break;
                    case '0':
                    case 'q':
                    case 'Q':
                        return 0;
                    default:
                        Console.WriteLine("Invalid option. Press any key to continue...");
                        Console.ReadKey();
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                if (Environment.GetEnvironmentVariable("FFCLI_DEBUG") != null)
                {
                    Console.Error.WriteLine(ex.StackTrace);
                }
                Console.WriteLine();
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }
        }
    }

    static string PromptForFile(string prompt, bool mustExist = true)
    {
        Console.Write(prompt);
        string? path = Console.ReadLine()?.Trim().Trim('"');

        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("No file path provided.");

        if (mustExist && !File.Exists(path))
            throw new FileNotFoundException($"File not found: {path}");

        return path;
    }

    static string PromptForDirectory(string prompt)
    {
        Console.Write(prompt);
        string? path = Console.ReadLine()?.Trim().Trim('"');

        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("No directory path provided.");

        return path;
    }

    static string? PromptOptional(string prompt)
    {
        Console.Write(prompt);
        string? input = Console.ReadLine()?.Trim();
        return string.IsNullOrEmpty(input) ? null : input;
    }

    static void WaitForKey()
    {
        Console.WriteLine();
        Console.WriteLine("Press any key to continue...");
        Console.ReadKey();
    }

    static void InteractiveInfo()
    {
        Console.WriteLine("--- Show FastFile Info ---");
        string path = PromptForFile("Enter FastFile path: ");
        Console.WriteLine();
        InfoCommand(new[] { path });
        WaitForKey();
    }

    static void InteractiveDecompress()
    {
        Console.WriteLine("--- Decompress FastFile ---");
        string input = PromptForFile("Enter FastFile path: ");
        string? output = PromptOptional("Output zone path (Enter for default): ");
        Console.WriteLine();

        var args = string.IsNullOrEmpty(output)
            ? new[] { input }
            : new[] { input, output };
        DecompressCommand(args);
        WaitForKey();
    }

    static void InteractiveCompress()
    {
        Console.WriteLine("--- Compress Zone to FastFile ---");
        string input = PromptForFile("Enter zone file path: ");
        string output = PromptForFile("Enter output FastFile path: ", mustExist: false);

        Console.WriteLine();
        Console.WriteLine("Select game:");
        Console.WriteLine("  1. World at War (default)");
        Console.WriteLine("  2. Call of Duty 4");
        Console.WriteLine("  3. Modern Warfare 2");
        Console.Write("Choice [1]: ");
        var gameKey = Console.ReadKey();
        Console.WriteLine();

        string game = gameKey.KeyChar switch
        {
            '2' => "cod4",
            '3' => "mw2",
            _ => "waw"
        };

        Console.WriteLine();
        Console.WriteLine("Select platform:");
        Console.WriteLine("  1. PS3 (default)");
        Console.WriteLine("  2. Xbox 360");
        Console.WriteLine("  3. PC");
        Console.Write("Choice [1]: ");
        var platKey = Console.ReadKey();
        Console.WriteLine();

        string platform = platKey.KeyChar switch
        {
            '2' => "xbox",
            '3' => "pc",
            _ => "ps3"
        };

        Console.WriteLine();
        CompressCommand(new[] { input, output, "--game", game, "--platform", platform });
        WaitForKey();
    }

    static void InteractiveList()
    {
        Console.WriteLine("--- List Raw Files ---");
        string path = PromptForFile("Enter FastFile or zone path: ");
        Console.Write("Show detailed info? (y/N): ");
        var verboseKey = Console.ReadKey();
        Console.WriteLine();
        Console.WriteLine();

        var args = (verboseKey.KeyChar == 'y' || verboseKey.KeyChar == 'Y')
            ? new[] { path, "-v" }
            : new[] { path };
        ListCommand(args);
        WaitForKey();
    }

    static void InteractiveExtract()
    {
        Console.WriteLine("--- Extract Raw Files ---");
        string input = PromptForFile("Enter FastFile or zone path: ");
        string output = PromptForDirectory("Enter output directory: ");
        string? filter = PromptOptional("Filter pattern (Enter for all): ");
        Console.WriteLine();

        var args = new List<string> { input, output };
        if (!string.IsNullOrEmpty(filter))
        {
            args.Add("--filter");
            args.Add(filter);
        }

        ExtractCommand(args.ToArray());
        WaitForKey();
    }

    static void InteractivePatch()
    {
        Console.WriteLine("--- Patch Raw File ---");
        string zone = PromptForFile("Enter zone file path: ");

        // Show available files first
        Console.WriteLine();
        Console.WriteLine("Loading raw files...");
        byte[] zoneData = File.ReadAllBytes(zone);
        var rawFiles = FindRawFiles(zoneData);

        Console.WriteLine($"Found {rawFiles.Count} raw file(s):");
        for (int i = 0; i < Math.Min(rawFiles.Count, 20); i++)
        {
            Console.WriteLine($"  {rawFiles[i].Name}");
        }
        if (rawFiles.Count > 20)
        {
            Console.WriteLine($"  ... and {rawFiles.Count - 20} more");
        }
        Console.WriteLine();

        Console.Write("Enter raw file name to patch: ");
        string? rawFileName = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(rawFileName))
            throw new ArgumentException("No raw file name provided.");

        string contentPath = PromptForFile("Enter content file path: ");
        Console.WriteLine();

        PatchCommand(new[] { zone, rawFileName, contentPath });
        WaitForKey();
    }

    static void PrintUsage()
    {
        Console.WriteLine("FastFile CLI Tool - CoD FastFile manipulation utility");
        Console.WriteLine();
        Console.WriteLine("Usage: ffcli <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  info <file.ff>                    Show FastFile information");
        Console.WriteLine("  decompress, d <file.ff> [out]     Decompress FastFile to zone");
        Console.WriteLine("  compress, c <file.zone> <out.ff>  Compress zone to FastFile");
        Console.WriteLine("  list, ls <file.ff|zone>           List raw files");
        Console.WriteLine("  extract, x <file> <dir>           Extract raw files to directory");
        Console.WriteLine("  patch, p <file.zone> <name> <in>  Patch raw file content");
        Console.WriteLine("  help [command]                    Show help for a command");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  ffcli info patch_mp.ff");
        Console.WriteLine("  ffcli d patch_mp.ff");
        Console.WriteLine("  ffcli ls patch_mp.zone");
        Console.WriteLine("  ffcli x patch_mp.zone ./extracted");
        Console.WriteLine();
        Console.WriteLine("Set FFCLI_DEBUG=1 for verbose error output.");
    }

    static int Help(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 0;
        }

        string command = args[0].ToLower();
        switch (command)
        {
            case "info":
                Console.WriteLine("Usage: ffcli info <file.ff>");
                Console.WriteLine();
                Console.WriteLine("Display information about a FastFile:");
                Console.WriteLine("  - Game (CoD4, WaW, MW2)");
                Console.WriteLine("  - Platform (PS3, Xbox 360, PC)");
                Console.WriteLine("  - Version number");
                Console.WriteLine("  - File size");
                Console.WriteLine("  - Signed status (Xbox 360)");
                break;

            case "decompress":
            case "d":
                Console.WriteLine("Usage: ffcli decompress <file.ff> [output.zone]");
                Console.WriteLine();
                Console.WriteLine("Decompress a FastFile to a zone file.");
                Console.WriteLine("If output is not specified, uses <filename>.zone");
                break;

            case "compress":
            case "c":
                Console.WriteLine("Usage: ffcli compress <file.zone> <output.ff> [options]");
                Console.WriteLine();
                Console.WriteLine("Options:");
                Console.WriteLine("  --game <cod4|waw|mw2>      Target game (default: auto-detect)");
                Console.WriteLine("  --platform <ps3|xbox|pc>   Target platform (default: ps3)");
                Console.WriteLine("  --signed                   Create Xbox 360 signed format");
                Console.WriteLine("  --original <file.ff>       Original FF for signed format hash table");
                break;

            case "list":
            case "ls":
                Console.WriteLine("Usage: ffcli list <file.ff|file.zone> [options]");
                Console.WriteLine();
                Console.WriteLine("Options:");
                Console.WriteLine("  --verbose, -v    Show detailed info (offsets, sizes)");
                Console.WriteLine();
                Console.WriteLine("List all raw files in a FastFile or zone file.");
                break;

            case "extract":
            case "x":
                Console.WriteLine("Usage: ffcli extract <file.ff|file.zone> <output_dir> [options]");
                Console.WriteLine();
                Console.WriteLine("Options:");
                Console.WriteLine("  --filter <pattern>   Only extract files matching pattern");
                Console.WriteLine("  --flat               Don't preserve directory structure");
                Console.WriteLine();
                Console.WriteLine("Extract raw files from a FastFile or zone file.");
                break;

            case "patch":
            case "p":
                Console.WriteLine("Usage: ffcli patch <file.zone> <rawfile_name> <content_file>");
                Console.WriteLine();
                Console.WriteLine("Patch a raw file's content in a zone file.");
                Console.WriteLine("The content file will replace the existing raw file data.");
                Console.WriteLine();
                Console.WriteLine("Note: Content must fit in the existing slot. Use the GUI");
                Console.WriteLine("editor if you need to increase file sizes.");
                break;

            default:
                Console.WriteLine($"Unknown command: {command}");
                return 1;
        }
        return 0;
    }

    static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        Console.Error.WriteLine("Run 'ffcli help' for usage information.");
        return 1;
    }

    static int InfoCommand(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: ffcli info <file.ff>");
            return 1;
        }

        string filePath = args[0];
        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"File not found: {filePath}");
            return 1;
        }

        var info = FastFileInfo.FromFile(filePath);
        var fileInfo = new FileInfo(filePath);

        Console.WriteLine($"File:      {Path.GetFileName(filePath)}");
        Console.WriteLine($"Size:      {FastFileInfo.FormatFileSize(fileInfo.Length)}");
        Console.WriteLine($"Game:      {info.GameName}");
        Console.WriteLine($"Platform:  {info.Platform}");
        Console.WriteLine($"Version:   0x{info.Version:X8}");
        Console.WriteLine($"Signed:    {(info.IsSigned ? "Yes" : "No")}");

        return 0;
    }

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

        string outputPath = args.Length > 1
            ? args[1]
            : Path.ChangeExtension(inputPath, ".zone");

        Console.WriteLine($"Decompressing: {Path.GetFileName(inputPath)}");

        var info = FastFileInfo.FromFile(inputPath);
        Console.WriteLine($"  Game: {info.GameName}, Platform: {info.Platform}");

        byte[] zoneData = new Decompressor().Decompress(inputPath);
        File.WriteAllBytes(outputPath, zoneData);

        Console.WriteLine($"  Output: {outputPath}");
        Console.WriteLine($"  Zone size: {FastFileInfo.FormatFileSize(zoneData.Length)}");

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

        // Parse options
        GameVersion gameVersion = GameVersion.WaW; // default
        string platform = "PS3";
        bool signed = false;
        string? originalFf = null;

        for (int i = 2; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "--game":
                    if (i + 1 >= args.Length)
                    {
                        Console.Error.WriteLine("--game requires a value (cod4, waw, mw2)");
                        return 1;
                    }
                    gameVersion = args[++i].ToLower() switch
                    {
                        "cod4" => GameVersion.CoD4,
                        "waw" or "cod5" => GameVersion.WaW,
                        "mw2" => GameVersion.MW2,
                        _ => throw new ArgumentException($"Unknown game: {args[i]}")
                    };
                    break;

                case "--platform":
                    if (i + 1 >= args.Length)
                    {
                        Console.Error.WriteLine("--platform requires a value (ps3, xbox, pc)");
                        return 1;
                    }
                    platform = args[++i].ToLower() switch
                    {
                        "ps3" => "PS3",
                        "xbox" or "xbox360" => "Xbox360",
                        "pc" => "PC",
                        _ => throw new ArgumentException($"Unknown platform: {args[i]}")
                    };
                    break;

                case "--signed":
                    signed = true;
                    platform = "Xbox360";
                    break;

                case "--original":
                    if (i + 1 >= args.Length)
                    {
                        Console.Error.WriteLine("--original requires a file path");
                        return 1;
                    }
                    originalFf = args[++i];
                    break;
            }
        }

        Console.WriteLine($"Compressing: {Path.GetFileName(inputPath)}");
        Console.WriteLine($"  Game: {gameVersion}, Platform: {platform}");

        byte[] zoneData = File.ReadAllBytes(inputPath);

        Compiler compiler;
        if (signed)
        {
            if (string.IsNullOrEmpty(originalFf))
            {
                Console.Error.WriteLine("Signed format requires --original <file.ff> for hash table");
                return 1;
            }
            compiler = Compiler.ForXbox360Signed(gameVersion, originalFf);
        }
        else
        {
            compiler = new Compiler(gameVersion, platform);
        }

        byte[] ffData = compiler.Compile(zoneData);
        File.WriteAllBytes(outputPath, ffData);

        Console.WriteLine($"  Output: {outputPath}");
        Console.WriteLine($"  FF size: {FastFileInfo.FormatFileSize(ffData.Length)}");

        return 0;
    }

    static int ListCommand(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: ffcli list <file.ff|file.zone>");
            return 1;
        }

        string inputPath = args[0];
        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"File not found: {inputPath}");
            return 1;
        }

        bool verbose = args.Any(a => a == "-v" || a == "--verbose");

        // Determine if FF or zone
        byte[] zoneData;
        string extension = Path.GetExtension(inputPath).ToLower();

        if (extension == ".ff" || extension == ".ffm")
        {
            Console.WriteLine($"Decompressing {Path.GetFileName(inputPath)}...");
            zoneData = new Decompressor().Decompress(inputPath);
        }
        else
        {
            zoneData = File.ReadAllBytes(inputPath);
        }

        // Find raw files using pattern matching
        var rawFiles = FindRawFiles(zoneData);

        Console.WriteLine();
        Console.WriteLine($"Found {rawFiles.Count} raw file(s):");
        Console.WriteLine();

        if (verbose)
        {
            Console.WriteLine($"{"Name",-50} {"Offset",10} {"Size",10}");
            Console.WriteLine(new string('-', 72));
            foreach (var rf in rawFiles)
            {
                Console.WriteLine($"{rf.Name,-50} {rf.DataOffset,10} {rf.Size,10}");
            }
        }
        else
        {
            foreach (var rf in rawFiles)
            {
                Console.WriteLine($"  {rf.Name}");
            }
        }

        return 0;
    }

    static int ExtractCommand(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: ffcli extract <file.ff|file.zone> <output_dir>");
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
                    if (i + 1 >= args.Length)
                    {
                        Console.Error.WriteLine("--filter requires a pattern");
                        return 1;
                    }
                    filter = args[++i];
                    break;
                case "--flat":
                    flat = true;
                    break;
            }
        }

        // Determine if FF or zone
        byte[] zoneData;
        string extension = Path.GetExtension(inputPath).ToLower();

        if (extension == ".ff" || extension == ".ffm")
        {
            Console.WriteLine($"Decompressing {Path.GetFileName(inputPath)}...");
            zoneData = new Decompressor().Decompress(inputPath);
        }
        else
        {
            zoneData = File.ReadAllBytes(inputPath);
        }

        var rawFiles = FindRawFiles(zoneData);

        // Apply filter
        if (!string.IsNullOrEmpty(filter))
        {
            rawFiles = rawFiles.Where(rf => rf.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        Console.WriteLine($"Extracting {rawFiles.Count} file(s) to {outputDir}");

        Directory.CreateDirectory(outputDir);
        int extracted = 0;

        foreach (var rf in rawFiles)
        {
            string outputPath;
            if (flat)
            {
                outputPath = Path.Combine(outputDir, Path.GetFileName(rf.Name));
            }
            else
            {
                outputPath = Path.Combine(outputDir, rf.Name.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            }

            byte[] content = new byte[rf.Size];
            Array.Copy(zoneData, rf.DataOffset, content, 0, rf.Size);
            File.WriteAllBytes(outputPath, content);

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

        if (!File.Exists(zonePath))
        {
            Console.Error.WriteLine($"Zone file not found: {zonePath}");
            return 1;
        }

        if (!File.Exists(contentPath))
        {
            Console.Error.WriteLine($"Content file not found: {contentPath}");
            return 1;
        }

        byte[] zoneData = File.ReadAllBytes(zonePath);
        byte[] newContent = File.ReadAllBytes(contentPath);

        var rawFiles = FindRawFiles(zoneData);
        var target = rawFiles.FirstOrDefault(rf =>
            rf.Name.Equals(rawFileName, StringComparison.OrdinalIgnoreCase) ||
            rf.Name.EndsWith("/" + rawFileName, StringComparison.OrdinalIgnoreCase));

        if (target == null)
        {
            Console.Error.WriteLine($"Raw file not found: {rawFileName}");
            Console.Error.WriteLine("Available files:");
            foreach (var rf in rawFiles.Take(10))
            {
                Console.Error.WriteLine($"  {rf.Name}");
            }
            if (rawFiles.Count > 10)
            {
                Console.Error.WriteLine($"  ... and {rawFiles.Count - 10} more");
            }
            return 1;
        }

        if (newContent.Length > target.Size)
        {
            Console.Error.WriteLine($"Content too large: {newContent.Length} bytes > {target.Size} bytes (max)");
            Console.Error.WriteLine("Use the GUI editor to increase file sizes.");
            return 1;
        }

        Console.WriteLine($"Patching: {target.Name}");
        Console.WriteLine($"  Original size: {target.Size}");
        Console.WriteLine($"  New size: {newContent.Length}");

        // Patch the content
        for (int i = 0; i < target.Size; i++)
        {
            zoneData[target.DataOffset + i] = i < newContent.Length ? newContent[i] : (byte)0;
        }

        // Write back
        File.WriteAllBytes(zonePath, zoneData);
        Console.WriteLine($"  Patched successfully.");

        return 0;
    }

    // Simple raw file finder using pattern matching
    static List<RawFileInfo> FindRawFiles(byte[] zoneData)
    {
        var files = new List<RawFileInfo>();
        var extensions = new[] { ".gsc", ".csc", ".cfg", ".vision", ".arena", ".str", ".csv", ".txt", ".menu" };

        foreach (var ext in extensions)
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

                // Found extension, now find the start of the filename
                int nameEnd = i + ext.Length;
                int nameStart = i;

                // Walk backwards to find FF FF FF FF marker or start of name
                while (nameStart > 0 && zoneData[nameStart - 1] != 0xFF && zoneData[nameStart - 1] != 0x00)
                {
                    nameStart--;
                    if (i - nameStart > 256) break; // sanity check
                }

                if (nameStart >= i) continue;

                string name = Encoding.ASCII.GetString(zoneData, nameStart, nameEnd - nameStart);
                if (string.IsNullOrWhiteSpace(name) || !name.EndsWith(ext)) continue;

                // Find size from header (look for FF FF FF FF pattern before name)
                int headerOffset = nameStart - 4;
                while (headerOffset > 4)
                {
                    if (zoneData[headerOffset] == 0xFF &&
                        zoneData[headerOffset + 1] == 0xFF &&
                        zoneData[headerOffset + 2] == 0xFF &&
                        zoneData[headerOffset + 3] == 0xFF)
                    {
                        break;
                    }
                    headerOffset--;
                    if (nameStart - headerOffset > 20) break;
                }

                if (headerOffset < 4) continue;

                // Read size (big-endian, 4 bytes before the FF marker)
                int sizeOffset = headerOffset - 4;
                if (sizeOffset < 0) continue;

                int size = (zoneData[sizeOffset] << 24) |
                          (zoneData[sizeOffset + 1] << 16) |
                          (zoneData[sizeOffset + 2] << 8) |
                          zoneData[sizeOffset + 3];

                if (size <= 0 || size > 10_000_000) continue;

                int dataOffset = nameEnd + 1; // +1 for null terminator

                // Avoid duplicates
                if (files.Any(f => f.Name == name && f.DataOffset == dataOffset)) continue;

                files.Add(new RawFileInfo
                {
                    Name = name,
                    Size = size,
                    DataOffset = dataOffset
                });
            }
        }

        return files.OrderBy(f => f.Name).ToList();
    }

    class RawFileInfo
    {
        public string Name { get; set; } = "";
        public int Size { get; set; }
        public int DataOffset { get; set; }
    }
}
