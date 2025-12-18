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

        // For signed Xbox 360 files, try different decompression strategies
        if (info.IsSigned)
        {
            // Try 1: Xbox 360 signed format (16KB signature + concatenated zlib streams)
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
    /// Try to decompress Xbox 360 signed files with concatenated zlib streams.
    /// These files have 0x4000 bytes of signature data after the 12-byte header,
    /// followed by multiple zlib streams (with full headers, no length prefixes).
    /// </summary>
    private static int TryDecompressSignedXbox360(BinaryReader br, BinaryWriter bw)
    {
        // Xbox 360 signed files: 12 byte header + 0x4000 signature + zlib streams
        const int SignatureSize = 0x4000;
        const int HeaderSize = 12;

        br.BaseStream.Position = HeaderSize + SignatureSize;

        if (br.BaseStream.Position >= br.BaseStream.Length)
            return 0;

        int blockCount = 0;
        byte[] fileData = new byte[br.BaseStream.Length - br.BaseStream.Position];
        br.Read(fileData, 0, fileData.Length);

        int offset = 0;
        while (offset < fileData.Length - 2)
        {
            // Look for zlib header (78 9C, 78 DA, 78 01, 78 5E)
            if (fileData[offset] != 0x78)
            {
                offset++;
                continue;
            }

            byte level = fileData[offset + 1];
            if (level != 0x9C && level != 0xDA && level != 0x01 && level != 0x5E)
            {
                offset++;
                continue;
            }

            // Found zlib header - try to decompress from this point
            try
            {
                using var input = new MemoryStream(fileData, offset, fileData.Length - offset);
                using var output = new MemoryStream();
                using (var zlib = new ZLibStream(input, CompressionMode.Decompress))
                {
                    zlib.CopyTo(output);
                }

                byte[] decompressed = output.ToArray();
                if (decompressed.Length > 0)
                {
                    bw.Write(decompressed);
                    blockCount++;

                    // Move offset past the compressed data we just read
                    // The ZLibStream consumed some bytes - calculate how many
                    long consumed = input.Position;
                    offset += (int)consumed;
                }
                else
                {
                    offset++;
                }
            }
            catch
            {
                // Not a valid zlib stream at this position, skip
                offset++;
            }
        }

        return blockCount;
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
    /// Compresses a zone file to a FastFile.
    /// </summary>
    /// <param name="inputPath">Path to the .zone file</param>
    /// <param name="outputPath">Path to output the .ff file</param>
    /// <param name="gameVersion">Target game version</param>
    /// <param name="platform">Target platform (PS3, PC, Wii, etc.)</param>
    /// <returns>Number of blocks compressed</returns>
    public static int Compress(string inputPath, string outputPath, GameVersion gameVersion, string platform = "PS3")
    {
        using var br = new BinaryReader(new FileStream(inputPath, FileMode.Open, FileAccess.Read), Encoding.Default);
        using var bw = new BinaryWriter(new FileStream(outputPath, FileMode.Create, FileAccess.Write), Encoding.Default);

        // Write header
        bw.Write(FastFileInfo.GetMagicBytes());
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
}
