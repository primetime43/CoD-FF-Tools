using System.IO.Compression;
using System.Text;

namespace FastFileLib;

/// <summary>
/// Handles FastFile compression and decompression for all supported games.
/// </summary>
public static class FastFileProcessor
{
    private const int BlockSize = 0x10000; // 64KB blocks

    /// <summary>
    /// Decompresses a FastFile to a zone file.
    /// </summary>
    /// <param name="inputPath">Path to the .ff file</param>
    /// <param name="outputPath">Path to output the .zone file</param>
    /// <returns>Number of blocks decompressed</returns>
    public static int Decompress(string inputPath, string outputPath)
    {
        using var br = new BinaryReader(new FileStream(inputPath, FileMode.Open, FileAccess.Read), Encoding.Default);
        using var bw = new BinaryWriter(new FileStream(outputPath, FileMode.Create, FileAccess.Write), Encoding.Default);

        // Read header info
        var info = FastFileInfo.FromReader(br);

        // Skip to compressed data based on game version
        SkipToCompressedData(br, info);

        // For Wii files, use single zlib stream decompression (no block structure)
        if (info.IsWii)
        {
            br.BaseStream.Position = 12; // After header
            int blocks = TryDecompressWiiZlib(br, bw);
            if (blocks > 0)
                return blocks;

            // If single zlib failed, reset and try standard methods
            br.BaseStream.Position = 12;
        }

        // For signed Xbox 360 files or dev build versions, try scanning for zlib header
        if (info.IsSigned || IsDevBuildVersion(info.Version))
        {
            // Try 1: Scan for zlib header (works for signed files and dev build FFM files)
            br.BaseStream.Position = 0;
            int blocks = TryDecompressSignedXbox360(br, bw);
            if (blocks > 0)
                return blocks;

            // Try 2: XBlock format (4-byte lengths)
            br.BaseStream.Position = 12;
            SkipToCompressedData(br, info);
            blocks = TryDecompressXBlocks(br, bw);
            if (blocks > 0)
                return blocks;

            // Try 3: Single compressed stream (no block structure)
            br.BaseStream.Position = 12;
            SkipToCompressedData(br, info);
            blocks = TryDecompressSingleStream(br, bw);
            if (blocks > 0)
                return blocks;

            // Try 4: Standard blocks
            br.BaseStream.Position = 12;
            SkipToCompressedData(br, info);
        }

        return DecompressStandardBlocks(br, bw, info.IsPC);
    }

    /// <summary>
    /// Checks if the version is a dev build version that requires special handling.
    /// Dev build FFM files have a large metadata header before the compressed data.
    /// </summary>
    private static bool IsDevBuildVersion(uint version)
    {
        // MW2 dev build version (0xFD = 253)
        return version == GameDefinitions.MW2Definition.DevBuildVersionValue;
    }

    /// <summary>
    /// Try to decompress PC FastFiles which use full zlib compression.
    /// PC WaW uses zlib with header (0x78) as a single stream or with different block structure.
    /// </summary>
    private static int TryDecompressPCZlib(BinaryReader br, BinaryWriter bw)
    {
        long startPos = br.BaseStream.Position;
        long outputStartPos = bw.BaseStream.Position;

        try
        {
            // Check if the data starts with a zlib header
            byte[] header = br.ReadBytes(2);
            br.BaseStream.Position = startPos;

            if (header.Length >= 2 && header[0] == 0x78 &&
                (header[1] == 0x01 || header[1] == 0x5E || header[1] == 0x9C || header[1] == 0xDA))
            {
                // Single zlib stream - decompress entire remainder
                byte[] compressedData = br.ReadBytes((int)(br.BaseStream.Length - startPos));

                using var input = new MemoryStream(compressedData);
                using (var zlib = new ZLibStream(input, CompressionMode.Decompress))
                {
                    zlib.CopyTo(bw.BaseStream);
                }
                return 1;
            }

            // Try PC format with 4-byte little-endian block lengths + zlib data
            br.BaseStream.Position = startPos;
            using var tempOutput = new MemoryStream();
            int blockCount = 0;

            while (br.BaseStream.Position < br.BaseStream.Length - 4)
            {
                // Read 4-byte little-endian length
                byte[] lengthBytes = br.ReadBytes(4);
                if (lengthBytes.Length < 4) break;

                int chunkLength = lengthBytes[0] | (lengthBytes[1] << 8) |
                                  (lengthBytes[2] << 16) | (lengthBytes[3] << 24);

                // Check for end marker or invalid length
                if (chunkLength <= 0 || chunkLength > 0x100000) // Max 1MB block
                    break;

                if (br.BaseStream.Position + chunkLength > br.BaseStream.Length)
                    break;

                byte[] compressedBlock = br.ReadBytes(chunkLength);

                // Try to decompress with zlib
                try
                {
                    byte[] decompressed = DecompressBlock(compressedBlock);
                    tempOutput.Write(decompressed, 0, decompressed.Length);
                    blockCount++;
                }
                catch
                {
                    // If decompression fails, this format doesn't work
                    br.BaseStream.Position = startPos;
                    bw.BaseStream.Position = outputStartPos;
                    return 0;
                }
            }

            if (blockCount > 0)
            {
                tempOutput.Position = 0;
                tempOutput.CopyTo(bw.BaseStream);
                return blockCount;
            }

            return 0;
        }
        catch
        {
            br.BaseStream.Position = startPos;
            bw.BaseStream.Position = outputStartPos;
            return 0;
        }
    }

    /// <summary>
    /// Standard block decompression (2-byte length prefix, 64KB blocks).
    /// </summary>
    /// <param name="br">BinaryReader positioned at start of compressed blocks</param>
    /// <param name="bw">BinaryWriter to write decompressed data</param>
    /// <param name="isLittleEndian">True for PC files (little-endian), false for console (big-endian)</param>
    private static int DecompressStandardBlocks(BinaryReader br, BinaryWriter bw, bool isLittleEndian = false)
    {
        // For PC files, try single zlib stream first (PC WaW uses full zlib, not block structure)
        if (isLittleEndian)
        {
            int pcBlocks = TryDecompressPCZlib(br, bw);
            if (pcBlocks > 0)
                return pcBlocks;
            // Reset position if PC method failed
            br.BaseStream.Position = 12; // After header
        }

        int blockCount = 0;
        int errorCount = 0;
        long lastGoodPosition = br.BaseStream.Position;

        for (int i = 0; i < 5000; i++)
        {
            if (br.BaseStream.Position >= br.BaseStream.Length - 1)
                break;

            byte[] lengthBytes = br.ReadBytes(2);
            if (lengthBytes.Length < 2) break;

            // PC uses little-endian, console uses big-endian
            int chunkLength = isLittleEndian
                ? (lengthBytes[0] | (lengthBytes[1] << 8))
                : ((lengthBytes[0] << 8) | lengthBytes[1]);

            // Check for end marker (0x00 0x01 or 0x00 0x00)
            if (chunkLength == 0 || chunkLength == 1) break;

            // Sanity check: block size should be reasonable (max ~128KB for safety)
            if (chunkLength > 131072 || chunkLength < 0)
            {
                // Invalid block size - might be corrupted or at end of file
                break;
            }

            // Check if we have enough data
            if (br.BaseStream.Position + chunkLength > br.BaseStream.Length)
                break;

            byte[] compressedData = br.ReadBytes(chunkLength);
            if (compressedData.Length < chunkLength) break;

            try
            {
                byte[] decompressedData = DecompressBlock(compressedData);
                bw.Write(decompressedData);
                blockCount++;
                lastGoodPosition = br.BaseStream.Position;
            }
            catch (Exception ex)
            {
                errorCount++;
                System.Diagnostics.Debug.WriteLine(
                    $"[FastFileProcessor] Block {i} decompression failed at position 0x{lastGoodPosition:X}: {ex.Message}");

                if (errorCount > 3)
                {
                    throw new InvalidDataException(
                        $"Too many decompression errors ({errorCount}). Last successful block: {blockCount}. " +
                        $"File position: 0x{lastGoodPosition:X}. Error: {ex.Message}", ex);
                }
            }
        }

        if (blockCount == 0)
        {
            throw new InvalidDataException(
                $"No blocks could be decompressed from the FastFile. " +
                $"File may be corrupted, encrypted, or in an unsupported format.");
        }

        return blockCount;
    }

    /// <summary>
    /// Try to decompress Xbox 360 signed files or dev build FFM files.
    /// These files have an auth/metadata header followed by zlib-compressed stream(s).
    /// The key is to find a valid zlib header and decompress from there.
    /// Dev build files may have many false positive zlib headers, so we test each one.
    /// </summary>
    private static int TryDecompressSignedXbox360(BinaryReader br, BinaryWriter bw)
    {
        br.BaseStream.Position = 0;
        byte[] fileData = br.ReadBytes((int)br.BaseStream.Length);

        // Limit search to first 256KB where compressed data typically starts
        // Dev build files have headers up to ~120KB, so 256KB gives good margin
        int searchLimit = Math.Min(fileData.Length - 2, 256 * 1024);

        // Find potential zlib headers in the header area
        var zlibOffsets = new List<int>();
        for (int i = 12; i < searchLimit; i++) // Start after magic+version
        {
            if (fileData[i] == 0x78 &&
                (fileData[i + 1] == 0x9C || fileData[i + 1] == 0xDA ||
                 fileData[i + 1] == 0x01 || fileData[i + 1] == 0x5E))
            {
                zlibOffsets.Add(i);
            }
        }

        if (zlibOffsets.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine("[FastFileProcessor] No zlib header found in first 256KB");
            return 0;
        }

        System.Diagnostics.Debug.WriteLine($"[FastFileProcessor] Found {zlibOffsets.Count} potential zlib headers in first 256KB");

        // Try each zlib offset until one successfully decompresses
        foreach (int zlibOffset in zlibOffsets)
        {
            // Reset output stream for each attempt
            bw.BaseStream.Position = 0;
            bw.BaseStream.SetLength(0);

            // Try DeflateStream first (more reliable for dev builds)
            try
            {
                int deflateOffset = zlibOffset + 2;
                using var input = new MemoryStream(fileData, deflateOffset, fileData.Length - deflateOffset);
                using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
                {
                    deflate.CopyTo(bw.BaseStream);
                }

                // Verify we got meaningful data (at least 10KB for a valid zone)
                if (bw.BaseStream.Length > 10240)
                {
                    System.Diagnostics.Debug.WriteLine($"[FastFileProcessor] DeflateStream succeeded from offset 0x{zlibOffset:X}, output size: {bw.BaseStream.Length}");
                    return 1;
                }
            }
            catch
            {
                // DeflateStream failed, try ZLibStream
            }

            // Reset and try ZLibStream
            bw.BaseStream.Position = 0;
            bw.BaseStream.SetLength(0);

            try
            {
                using var input = new MemoryStream(fileData, zlibOffset, fileData.Length - zlibOffset);
                using (var zlib = new ZLibStream(input, CompressionMode.Decompress))
                {
                    zlib.CopyTo(bw.BaseStream);
                }

                // Verify we got meaningful data
                if (bw.BaseStream.Length > 10240)
                {
                    System.Diagnostics.Debug.WriteLine($"[FastFileProcessor] ZLibStream succeeded from offset 0x{zlibOffset:X}, output size: {bw.BaseStream.Length}");
                    return 1;
                }
            }
            catch
            {
                // This offset didn't work, try next one
            }
        }

        System.Diagnostics.Debug.WriteLine("[FastFileProcessor] No valid zlib stream found after testing candidates");
        return 0;
    }

    /// <summary>
    /// Try to decompress as a single compressed stream (no block structure).
    /// Some signed files may have the entire zone as one deflate stream.
    /// </summary>
    private static int TryDecompressSingleStream(BinaryReader br, BinaryWriter bw)
    {
        long startPos = br.BaseStream.Position;

        try
        {
            // Read all remaining data
            int remainingBytes = (int)(br.BaseStream.Length - br.BaseStream.Position);
            if (remainingBytes < 10)
                return 0;

            byte[] compressedData = br.ReadBytes(remainingBytes);

            // Try to decompress as a single stream
            byte[] decompressed = DecompressBlock(compressedData);

            if (decompressed.Length > 0)
            {
                bw.Write(decompressed);
                return 1; // Treated as 1 block
            }

            return 0;
        }
        catch
        {
            br.BaseStream.Position = startPos;
            return 0;
        }
    }

    /// <summary>
    /// Try to decompress Xbox 360 XBlock format (4-byte lengths, larger blocks).
    /// Xbox 360 signed files may use different block structures.
    /// </summary>
    private static int TryDecompressXBlocks(BinaryReader br, BinaryWriter bw)
    {
        long startPos = br.BaseStream.Position;
        long outputStartPos = bw.BaseStream.Position;

        // Buffer decompressed data - only write to output if fully successful
        using var tempOutput = new MemoryStream();
        int blockCount = 0;

        try
        {
            // Xbox 360 XBlocks use 4-byte block sizes (big-endian)
            while (br.BaseStream.Position < br.BaseStream.Length - 4)
            {
                // Read 4-byte big-endian length
                byte[] lengthBytes = br.ReadBytes(4);
                if (lengthBytes.Length < 4) break;

                int chunkLength = (lengthBytes[0] << 24) | (lengthBytes[1] << 16) |
                                  (lengthBytes[2] << 8) | lengthBytes[3];

                // Sanity check
                if (chunkLength <= 0 || chunkLength > 0x200000) // Max 2MB XBlock
                {
                    br.BaseStream.Position = startPos;
                    return 0;
                }

                if (br.BaseStream.Position + chunkLength > br.BaseStream.Length)
                {
                    br.BaseStream.Position = startPos;
                    return 0;
                }

                byte[] compressedData = br.ReadBytes(chunkLength);

                try
                {
                    byte[] decompressedData = DecompressBlock(compressedData);
                    tempOutput.Write(decompressedData, 0, decompressedData.Length);
                    blockCount++;
                }
                catch
                {
                    br.BaseStream.Position = startPos;
                    return 0;
                }

                // Check for end marker
                if (br.BaseStream.Position >= br.BaseStream.Length - 4)
                    break;

                // Peek at next bytes
                byte[] peek = br.ReadBytes(4);
                br.BaseStream.Position -= 4;

                int nextLen = (peek[0] << 24) | (peek[1] << 16) | (peek[2] << 8) | peek[3];
                if (nextLen == 0 || nextLen == 1)
                    break;
            }

            // Success - write buffered data to actual output
            if (blockCount > 0)
            {
                tempOutput.Position = 0;
                tempOutput.CopyTo(bw.BaseStream);
            }

            return blockCount;
        }
        catch
        {
            br.BaseStream.Position = startPos;
            return 0;
        }
    }

    /// <summary>
    /// Try to decompress Wii FastFiles which use a single zlib stream (no block structure).
    /// Unlike PS3/Xbox which have 2-byte block length prefixes, Wii uses one continuous zlib stream.
    /// </summary>
    private static int TryDecompressWiiZlib(BinaryReader br, BinaryWriter bw)
    {
        long startPos = br.BaseStream.Position;
        long outputStartPos = bw.BaseStream.Position;

        try
        {
            // Peek at first 2 bytes to verify zlib header
            byte[] header = br.ReadBytes(2);
            if (header.Length < 2)
                return 0;

            // Check for zlib header (0x78 XX)
            bool hasZlibHeader = header[0] == 0x78 &&
                                 (header[1] == 0x01 || header[1] == 0x5E ||
                                  header[1] == 0x9C || header[1] == 0xDA);

            if (!hasZlibHeader)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[FastFileProcessor] Wii file doesn't have zlib header at position {startPos}: {header[0]:X2} {header[1]:X2}");
                br.BaseStream.Position = startPos;
                return 0;
            }

            // Reset to start of zlib data
            br.BaseStream.Position = startPos;

            // Read all remaining compressed data
            byte[] compressedData = br.ReadBytes((int)(br.BaseStream.Length - startPos));

            // Decompress as a single zlib stream
            using var input = new MemoryStream(compressedData);
            using (var zlib = new ZLibStream(input, CompressionMode.Decompress))
            {
                zlib.CopyTo(bw.BaseStream);
            }

            System.Diagnostics.Debug.WriteLine(
                $"[FastFileProcessor] Wii zlib decompression successful: {compressedData.Length} -> {bw.BaseStream.Position - outputStartPos} bytes");

            return 1; // Single stream counts as 1 block
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[FastFileProcessor] Wii zlib decompression failed: {ex.Message}");
            br.BaseStream.Position = startPos;
            bw.BaseStream.Position = outputStartPos;
            bw.BaseStream.SetLength(outputStartPos);
            return 0;
        }
    }

    /// <summary>
    /// Compresses a zone file to a FastFile.
    /// </summary>
    /// <param name="inputPath">Path to the .zone file</param>
    /// <param name="outputPath">Path to output the .ff file</param>
    /// <param name="gameVersion">Target game version</param>
    /// <param name="platform">Target platform (PS3, PC, Wii, Xbox360, etc.)</param>
    /// <param name="signed">Whether to use signed header (IWff0100) or unsigned (IWffu100).
    /// Xbox 360 MP files are signed, SP files are unsigned. PS3 files are unsigned.</param>
    /// <returns>Number of blocks compressed</returns>
    public static int Compress(string inputPath, string outputPath, GameVersion gameVersion, string platform = "PS3", bool signed = false)
    {
        using var br = new BinaryReader(new FileStream(inputPath, FileMode.Open, FileAccess.Read), Encoding.Default);
        using var bw = new BinaryWriter(new FileStream(outputPath, FileMode.Create, FileAccess.Write), Encoding.Default);

        // Write header - signed uses IWff0100, unsigned uses IWffu100
        bw.Write(FastFileInfo.GetMagicBytes(signed: signed));
        bw.Write(FastFileInfo.GetVersionBytes(gameVersion, platform));

        int blockCount = 0;
        while (br.BaseStream.Position < br.BaseStream.Length)
        {
            byte[] chunk = br.ReadBytes(BlockSize);
            byte[] compressedChunk = CompressBlock(chunk);

            // Write length as 2-byte big-endian
            int compressedLength = compressedChunk.Length;
            bw.Write((byte)(compressedLength >> 8));
            bw.Write((byte)(compressedLength & 0xFF));

            bw.Write(compressedChunk);
            blockCount++;
        }

        // Write end marker (0x00 0x01) followed by 4 bytes of padding
        // PS3 FastFiles require this padding after the end marker
        bw.Write((byte)0x00);
        bw.Write((byte)0x01);
        bw.Write((byte)0x00);
        bw.Write((byte)0x00);
        bw.Write((byte)0x00);
        bw.Write((byte)0x00);

        return blockCount;
    }

    /// <summary>
    /// Skips the header and positions the reader at the start of compressed data.
    /// </summary>
    private static void SkipToCompressedData(BinaryReader br, FastFileInfo info)
    {
        // Reader is already past magic (8) and version (4) = position 12

        // Handle signed Xbox 360 FastFiles
        if (info.IsSigned)
        {
            // Signed files have a DB_AuthHeader structure after the standard header
            // We need to scan for the start of compressed data
            // The auth header contains RSA signatures and hash data

            // Try to find the start of compressed data by scanning for valid block headers
            long startPos = br.BaseStream.Position;
            long fileLength = br.BaseStream.Length;

            // Signed files typically have auth data of varying sizes
            // Scan from current position looking for valid compressed block
            for (long offset = startPos; offset < Math.Min(startPos + 0x1000, fileLength - 2); offset++)
            {
                br.BaseStream.Position = offset;
                byte[] testBytes = br.ReadBytes(2);
                if (testBytes.Length < 2) break;

                int potentialLength = (testBytes[0] << 8) | testBytes[1];

                // Valid block length should be reasonable (between 10 bytes and 64KB compressed)
                if (potentialLength >= 10 && potentialLength <= 0x10000)
                {
                    // Check if this could be a valid deflate block
                    if (br.BaseStream.Position + potentialLength <= fileLength)
                    {
                        byte[] testData = br.ReadBytes(Math.Min(potentialLength, 10));
                        if (testData.Length >= 2)
                        {
                            // Try to verify this looks like deflate data
                            // Deflate blocks typically start with specific bit patterns
                            // Or we can try to decompress a small amount
                            bool looksValid = IsLikelyDeflateData(testData);

                            if (looksValid)
                            {
                                // Found likely start of compressed data
                                br.BaseStream.Position = offset;
                                return;
                            }
                        }
                    }
                }
            }

            // If scanning failed, try common signed header sizes
            // Xbox 360 signed files often have 0x100 (256) bytes of signature after version
            br.BaseStream.Position = 12 + 0x100; // Try offset 0x10C
            return;
        }

        if (info.GameVersion == GameVersion.MW2)
        {
            // MW2 extended header structure:
            // allowOnlineUpdate (1 byte)
            // fileCreationTime (8 bytes)
            // region (4 bytes)
            // entryCount (4 bytes)
            // entries (entryCount * 0x14 bytes for PS3)
            // fileSizes (8 bytes)

            br.ReadByte();           // allowOnlineUpdate
            br.ReadBytes(8);         // fileCreationTime
            br.ReadBytes(4);         // region

            byte[] entryCountBytes = br.ReadBytes(4);
            int entryCount = (entryCountBytes[0] << 24) | (entryCountBytes[1] << 16) |
                            (entryCountBytes[2] << 8) | entryCountBytes[3];

            // Skip entries (each entry is 0x14 = 20 bytes on PS3)
            if (entryCount > 0 && entryCount < 10000) // Sanity check
            {
                br.ReadBytes(entryCount * 0x14);
            }

            br.ReadBytes(8);         // fileSizes
        }
        // For CoD4/WaW unsigned, we're already at the correct position (12)
    }

    /// <summary>
    /// Checks if data looks like it could be valid deflate compressed data.
    /// </summary>
    private static bool IsLikelyDeflateData(byte[] data)
    {
        if (data == null || data.Length < 2)
            return false;

        // Check for zlib header (0x78 XX)
        if (data[0] == 0x78 && (data[1] == 0x01 || data[1] == 0x5E ||
                                data[1] == 0x9C || data[1] == 0xDA))
        {
            return true;
        }

        // Raw deflate blocks start with specific bit patterns
        // The first byte contains the BFINAL bit (bit 0) and BTYPE (bits 1-2)
        // BTYPE: 00 = no compression, 01 = fixed Huffman, 10 = dynamic Huffman, 11 = reserved
        int btype = (data[0] >> 1) & 0x03;

        // Valid BTYPE values are 0, 1, or 2 (not 3)
        // Most compressed data uses dynamic Huffman (10) or fixed Huffman (01)
        if (btype == 3)
            return false;

        // For dynamic Huffman (most common), check for reasonable values
        if (btype == 2 && data.Length >= 3)
        {
            // Dynamic Huffman has HLIT, HDIST, HCLEN encoded after the header bits
            return true;
        }

        // For fixed Huffman or stored blocks, assume valid
        if (btype == 0 || btype == 1)
        {
            return true;
        }

        // Try a quick decompression test
        try
        {
            using var input = new MemoryStream(data);
            using var output = new MemoryStream();
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);

            byte[] buffer = new byte[256];
            int read = deflate.Read(buffer, 0, buffer.Length);
            return read > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Decompresses a single block of data.
    /// Automatically detects zlib header vs raw deflate.
    /// </summary>
    public static byte[] DecompressBlock(byte[] compressedData)
    {
        if (compressedData == null || compressedData.Length == 0)
            return Array.Empty<byte>();

        // Check if data has zlib header (0x78 followed by compression level byte)
        // MW2 uses zlib-wrapped deflate, CoD4/WaW use raw deflate
        bool hasZlibHeader = compressedData.Length >= 2 &&
                             compressedData[0] == 0x78 &&
                             (compressedData[1] == 0x01 || compressedData[1] == 0x5E ||
                              compressedData[1] == 0x9C || compressedData[1] == 0xDA);

        using var input = new MemoryStream(compressedData);
        using var output = new MemoryStream();

        if (hasZlibHeader)
        {
            using (var zlib = new ZLibStream(input, CompressionMode.Decompress))
            {
                zlib.CopyTo(output);
            }
        }
        else
        {
            using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
            {
                deflate.CopyTo(output);
            }
        }

        return output.ToArray();
    }

    /// <summary>
    /// Compresses a single block of data.
    /// Uses ZLibStream and strips the 2-byte header, keeping the deflate data + Adler-32 checksum.
    /// This matches the format expected by the game engine.
    /// </summary>
    public static byte[] CompressBlock(byte[] uncompressedData)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal))
        {
            zlib.Write(uncompressedData, 0, uncompressedData.Length);
        }

        byte[] zlibData = output.ToArray();

        // ZLibStream produces: [2-byte header][deflate data][4-byte Adler-32 checksum]
        // Strip the 2-byte header, keep deflate data + checksum
        if (zlibData.Length > 2)
        {
            byte[] result = new byte[zlibData.Length - 2];
            Array.Copy(zlibData, 2, result, 0, result.Length);
            return result;
        }

        return zlibData;
    }

    /// <summary>
    /// Compresses data using full zlib format (with 78 DA header - best compression).
    /// Xbox 360 signed files use this format as a single continuous stream.
    /// </summary>
    /// <param name="uncompressedData">The data to compress</param>
    /// <returns>Full zlib stream including header and checksum</returns>
    public static byte[] CompressFullZlib(byte[] uncompressedData)
    {
        using var output = new MemoryStream();
        // Use SmallestSize to get 78 DA header (best compression) like original files
        using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize))
        {
            zlib.Write(uncompressedData, 0, uncompressedData.Length);
        }
        return output.ToArray();
    }

    #region MW2 Compression

    /// <summary>
    /// Compresses a zone file to MW2 FastFile format.
    /// Based on ZoneTool research:
    /// - PS3 uses block compression (64KB blocks with 2-byte length markers, full zlib)
    /// - Xbox 360/PC uses single zlib stream compression (no block structure)
    /// </summary>
    /// <param name="inputPath">Path to the .zone file</param>
    /// <param name="outputPath">Path to output the .ff file</param>
    /// <param name="versionBytes">Version bytes (4 bytes, big-endian)</param>
    /// <param name="isXbox360">True for Xbox 360, false for PS3</param>
    /// <returns>Number of blocks compressed (1 for Xbox 360 single stream)</returns>
    public static int CompressMW2(string inputPath, string outputPath, byte[] versionBytes, bool isXbox360)
    {
        using var br = new BinaryReader(new FileStream(inputPath, FileMode.Open, FileAccess.Read), Encoding.Default);
        using var bw = new BinaryWriter(new FileStream(outputPath, FileMode.Create, FileAccess.Write), Encoding.Default);

        // Write unsigned header (IWffu100)
        bw.Write(FastFileConstants.UnsignedHeaderBytes);

        // Write version bytes
        bw.Write(versionBytes);

        // Write MW2 extended header
        WriteMW2ExtendedHeader(bw);

        if (isXbox360)
        {
            // Xbox 360: Single zlib stream compression (no block structure)
            // Based on ZoneTool: compress_zlib(false) = standard single stream
            byte[] zoneData = br.ReadBytes((int)br.BaseStream.Length);
            byte[] compressedData = CompressFullZlib(zoneData);
            bw.Write(compressedData);
            // No end marker for single stream format
            return 1;
        }
        else
        {
            // PS3: Block-based compression with 2-byte length markers
            // Based on ZoneTool: compress_zlib(true) = block mode for PS3
            int blockCount = 0;
            while (br.BaseStream.Position < br.BaseStream.Length)
            {
                byte[] chunk = br.ReadBytes(BlockSize);
                byte[] compressedChunk = CompressBlockMW2(chunk);

                int compressedLength = compressedChunk.Length;
                // Write length as 2-byte big-endian
                bw.Write((byte)(compressedLength >> 8));
                bw.Write((byte)(compressedLength & 0xFF));

                bw.Write(compressedChunk);
                blockCount++;
            }

            // Write end marker for block format
            bw.Write((byte)0x00);
            bw.Write((byte)0x01);

            return blockCount;
        }
    }

    /// <summary>
    /// Compresses a zone file to MW2 FastFile format, preserving the extended header from the original FF.
    /// </summary>
    /// <param name="inputPath">Path to the .zone file</param>
    /// <param name="outputPath">Path to output the .ff file</param>
    /// <param name="versionBytes">Version bytes (4 bytes, big-endian)</param>
    /// <param name="isXbox360">True for Xbox 360, false for PS3</param>
    /// <param name="originalFfPath">Path to original FF file to preserve extended header from (can be same as outputPath)</param>
    /// <returns>Number of blocks compressed (1 for Xbox 360 single stream)</returns>
    public static int CompressMW2(string inputPath, string outputPath, byte[] versionBytes, bool isXbox360, string originalFfPath)
    {
        // Read extended header from original file BEFORE opening output (in case they're the same file)
        byte[] originalExtendedHeader = null;
        if (!string.IsNullOrEmpty(originalFfPath) && File.Exists(originalFfPath))
        {
            originalExtendedHeader = ReadMW2ExtendedHeader(originalFfPath);
        }

        using var br = new BinaryReader(new FileStream(inputPath, FileMode.Open, FileAccess.Read), Encoding.Default);
        using var bw = new BinaryWriter(new FileStream(outputPath, FileMode.Create, FileAccess.Write), Encoding.Default);

        // Write unsigned header (IWffu100)
        bw.Write(FastFileConstants.UnsignedHeaderBytes);

        // Write version bytes
        bw.Write(versionBytes);

        // Remember position of fileSizes field so we can update it later
        long fileSizesPosition = -1;

        // Write MW2 extended header (with placeholder fileSizes - will update after compression)
        if (originalExtendedHeader != null)
        {
            // Write everything except fileSizes (last 8 bytes)
            bw.Write(originalExtendedHeader, 0, originalExtendedHeader.Length - 8);
            // Remember where fileSizes starts
            fileSizesPosition = bw.BaseStream.Position;
            // Write placeholder fileSizes (will be updated after compression)
            bw.Write(new byte[8]);
        }
        else
        {
            WriteMW2ExtendedHeader(bw);
        }

        int blockCount = 0;
        if (isXbox360)
        {
            // Xbox 360: Single zlib stream compression (no block structure)
            byte[] zoneData = br.ReadBytes((int)br.BaseStream.Length);
            byte[] compressedData = CompressFullZlib(zoneData);
            bw.Write(compressedData);
            blockCount = 1;
        }
        else
        {
            // PS3: Block-based compression with 2-byte length markers
            while (br.BaseStream.Position < br.BaseStream.Length)
            {
                byte[] chunk = br.ReadBytes(BlockSize);
                byte[] compressedChunk = CompressBlockMW2(chunk);

                int compressedLength = compressedChunk.Length;
                // Write length as 2-byte big-endian
                bw.Write((byte)(compressedLength >> 8));
                bw.Write((byte)(compressedLength & 0xFF));

                bw.Write(compressedChunk);
                blockCount++;
            }

            // Write end marker for block format
            bw.Write((byte)0x00);
            bw.Write((byte)0x01);
        }

        // Now update the fileSizes field with the actual FF file size
        if (fileSizesPosition > 0)
        {
            long finalFileSize = bw.BaseStream.Position;
            bw.BaseStream.Seek(fileSizesPosition, SeekOrigin.Begin);
            // Write fileSize (big-endian)
            bw.Write((byte)((finalFileSize >> 24) & 0xFF));
            bw.Write((byte)((finalFileSize >> 16) & 0xFF));
            bw.Write((byte)((finalFileSize >> 8) & 0xFF));
            bw.Write((byte)(finalFileSize & 0xFF));
            // Write maxFileSize (big-endian) - same as fileSize
            bw.Write((byte)((finalFileSize >> 24) & 0xFF));
            bw.Write((byte)((finalFileSize >> 16) & 0xFF));
            bw.Write((byte)((finalFileSize >> 8) & 0xFF));
            bw.Write((byte)(finalFileSize & 0xFF));
        }

        return blockCount;
    }

    /// <summary>
    /// Compresses a zone file to MW2 FastFile format using GameVersion enum.
    /// </summary>
    public static int CompressMW2(string inputPath, string outputPath, GameVersion gameVersion, string platform)
    {
        byte[] versionBytes = FastFileInfo.GetVersionBytes(gameVersion, platform);
        bool isXbox360 = platform.ToUpperInvariant().Contains("XBOX") || platform == "360";
        return CompressMW2(inputPath, outputPath, versionBytes, isXbox360);
    }

    /// <summary>
    /// Compresses a single block for MW2 format.
    /// MW2 PS3 uses stripped deflate (no zlib header), same as CoD4/WaW.
    /// </summary>
    public static byte[] CompressBlockMW2(byte[] uncompressedData)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal))
        {
            zlib.Write(uncompressedData, 0, uncompressedData.Length);
        }

        byte[] zlibData = output.ToArray();

        // Strip the 2-byte zlib header, keep deflate data + Adler-32 checksum
        // This matches the format used by the original game files
        if (zlibData.Length > 2)
        {
            byte[] result = new byte[zlibData.Length - 2];
            Array.Copy(zlibData, 2, result, 0, result.Length);
            return result;
        }

        return zlibData;
    }

    /// <summary>
    /// Writes a minimal MW2 extended header with default values.
    /// Note: For PS3 compatibility, prefer using CompressMW2 overload that preserves original header.
    /// </summary>
    /// <param name="bw">BinaryWriter to write to</param>
    public static void WriteMW2ExtendedHeader(BinaryWriter bw)
    {
        // MW2 extended header structure:
        // allowOnlineUpdate (1 byte)
        // fileCreationTime (8 bytes)
        // region (4 bytes)
        // entryCount (4 bytes, big-endian)
        // entries (entryCount * 0x14 bytes) - none for minimal header
        // fileSizes (8 bytes)

        bw.Write((byte)0x01);    // allowOnlineUpdate = true (required for PS3 patch files)
        bw.Write(new byte[8]);   // fileCreationTime = 0
        bw.Write((byte)0x00);    // region byte 1
        bw.Write((byte)0x00);    // region byte 2
        bw.Write((byte)0x00);    // region byte 3
        bw.Write((byte)0x01);    // region byte 4 = 1 (common value)
        bw.Write(new byte[4]);   // entryCount = 0 (no entries)
        bw.Write(new byte[8]);   // fileSizes = 0 (will be calculated by game)
    }

    /// <summary>
    /// Reads the MW2 extended header from an FF file and returns it as a byte array.
    /// </summary>
    /// <param name="ffPath">Path to the FF file</param>
    /// <returns>Extended header bytes (25 bytes for no entries), or null if reading fails</returns>
    public static byte[] ReadMW2ExtendedHeader(string ffPath)
    {
        try
        {
            using var reader = new BinaryReader(File.OpenRead(ffPath));
            reader.BaseStream.Seek(12, SeekOrigin.Begin); // Skip magic (8) + version (4)

            byte allowOnlineUpdate = reader.ReadByte();
            byte[] fileCreationTime = reader.ReadBytes(8);
            byte[] region = reader.ReadBytes(4);
            byte[] entryCountBytes = reader.ReadBytes(4);
            int entryCount = (entryCountBytes[0] << 24) | (entryCountBytes[1] << 16) |
                            (entryCountBytes[2] << 8) | entryCountBytes[3];

            // Skip entries if any (each entry is 0x14 bytes)
            if (entryCount > 0 && entryCount < 10000)
            {
                reader.ReadBytes(entryCount * 0x14);
            }

            byte[] fileSizes = reader.ReadBytes(8);

            // Build the header: we preserve all fields but set entryCount to 0
            using var ms = new MemoryStream();
            ms.WriteByte(allowOnlineUpdate);
            ms.Write(fileCreationTime, 0, 8);
            ms.Write(region, 0, 4);
            ms.Write(new byte[4], 0, 4); // entryCount = 0
            ms.Write(fileSizes, 0, 8);
            return ms.ToArray();
        }
        catch
        {
            return null;
        }
    }

    #endregion

    /// <summary>
    /// Compresses a zone file to a FastFile with Xbox 360 signed format.
    /// Xbox 360 signed files use IWffs100 streaming format with a single zlib stream.
    /// </summary>
    /// <param name="inputPath">Path to the .zone file</param>
    /// <param name="outputPath">Path to output the .ff file</param>
    /// <param name="gameVersion">Target game version</param>
    /// <param name="originalFfPath">Path to original FF file (to preserve hash table)</param>
    /// <returns>1 (single stream compressed)</returns>
    public static int CompressXbox360Signed(string inputPath, string outputPath, GameVersion gameVersion, string originalFfPath)
    {
        // Read hash table from original file before opening output
        byte[] hashTableAndAuth = null;
        if (!string.IsNullOrEmpty(originalFfPath) && File.Exists(originalFfPath))
        {
            hashTableAndAuth = new byte[FastFileConstants.Xbox360SignedHashTableSize];
            using var origReader = new BinaryReader(File.OpenRead(originalFfPath));
            origReader.BaseStream.Seek(FastFileConstants.Xbox360SignedHashTableStart, SeekOrigin.Begin);
            origReader.Read(hashTableAndAuth, 0, hashTableAndAuth.Length);
        }

        using var br = new BinaryReader(new FileStream(inputPath, FileMode.Open, FileAccess.Read), Encoding.Default);
        using var bw = new BinaryWriter(new FileStream(outputPath, FileMode.Create, FileAccess.Write), Encoding.Default);

        // Write signed header (IWff0100)
        bw.Write(FastFileConstants.SignedHeaderBytes);

        // Write version (big-endian)
        bw.Write(FastFileInfo.GetVersionBytes(gameVersion, "Xbox360"));

        // Write streaming header (IWffs100)
        bw.Write(FastFileConstants.StreamingHeaderBytes);

        // Write hash table and auth data (preserved from original or zeros)
        if (hashTableAndAuth != null)
        {
            bw.Write(hashTableAndAuth);
        }
        else
        {
            bw.Write(new byte[FastFileConstants.Xbox360SignedHashTableSize]);
        }

        // Read entire zone file and compress as single stream
        byte[] zoneData = br.ReadBytes((int)br.BaseStream.Length);
        byte[] compressedData = CompressFullZlib(zoneData);
        bw.Write(compressedData);

        // No end marker for signed format
        return 1;
    }

    /// <summary>
    /// Compresses a zone file to a FastFile with Xbox 360 signed format using provided version bytes.
    /// </summary>
    /// <param name="inputPath">Path to the .zone file</param>
    /// <param name="outputPath">Path to output the .ff file</param>
    /// <param name="versionBytes">Version bytes (4 bytes, big-endian)</param>
    /// <param name="originalFfPath">Path to original FF file (to preserve hash table)</param>
    /// <returns>1 (single stream compressed)</returns>
    public static int CompressXbox360Signed(string inputPath, string outputPath, byte[] versionBytes, string originalFfPath)
    {
        // Read hash table from original file before opening output
        byte[] hashTableAndAuth = null;
        if (!string.IsNullOrEmpty(originalFfPath) && File.Exists(originalFfPath))
        {
            hashTableAndAuth = new byte[FastFileConstants.Xbox360SignedHashTableSize];
            using var origReader = new BinaryReader(File.OpenRead(originalFfPath));
            origReader.BaseStream.Seek(FastFileConstants.Xbox360SignedHashTableStart, SeekOrigin.Begin);
            origReader.Read(hashTableAndAuth, 0, hashTableAndAuth.Length);
        }

        using var br = new BinaryReader(new FileStream(inputPath, FileMode.Open, FileAccess.Read), Encoding.Default);
        using var bw = new BinaryWriter(new FileStream(outputPath, FileMode.Create, FileAccess.Write), Encoding.Default);

        // Write signed header (IWff0100)
        bw.Write(FastFileConstants.SignedHeaderBytes);

        // Write version bytes
        bw.Write(versionBytes);

        // Write streaming header (IWffs100)
        bw.Write(FastFileConstants.StreamingHeaderBytes);

        // Write hash table and auth data (preserved from original or zeros)
        if (hashTableAndAuth != null)
        {
            bw.Write(hashTableAndAuth);
        }
        else
        {
            bw.Write(new byte[FastFileConstants.Xbox360SignedHashTableSize]);
        }

        // Read entire zone file and compress as single stream
        byte[] zoneData = br.ReadBytes((int)br.BaseStream.Length);
        byte[] compressedData = CompressFullZlib(zoneData);
        bw.Write(compressedData);

        // No end marker for signed format
        return 1;
    }
}
