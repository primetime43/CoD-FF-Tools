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
    /// Non-throwing wrapper around <see cref="Decompress"/>. Returns false and writes a
    /// human-readable explanation to <paramref name="errorMessage"/> on any decompression
    /// failure (corrupt file, unsupported format, hybrid wrapper, etc). Use this from
    /// interactive GUIs so a known-bad file doesn't trigger a debugger break under
    /// "Just My Code" — the throw inside the library shows up as an unhandled exception
    /// in VS even when a user-code catch is waiting for it one frame up.
    /// </summary>
    public static bool TryDecompress(string inputPath, string outputPath, out int blockCount, out string? errorMessage)
    {
        try
        {
            blockCount = Decompress(inputPath, outputPath);
            errorMessage = null;
            return true;
        }
        catch (InvalidDataException ex)
        {
            blockCount = 0;
            errorMessage = ex.Message;
            System.Diagnostics.Debug.WriteLine($"[FastFileProcessor] TryDecompress: {ex.Message}");
            return false;
        }
    }

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

        // MW2 PC has its own layout — the 25-byte PS3 extended header (allowOnlineUpdate +
        // fileCreationTime + region + entryCount + fileSizes) is truncated to just 9 bytes on
        // PC (allowOnlineUpdate + fileCreationTime), and the zlib stream follows. The signed
        // variant places IWffs100 at 0x15 instead of Xbox 360's 0x0C, so the hash table also
        // shifts by 9 bytes. SP files (mp_rust_load.ff etc.) are still ~16KB but single-player
        // and gameplay files like common.ff / common_mp.ff are tens of megabytes; both follow
        // the same offsets.
        if (info.GameVersion == GameVersion.MW2 && info.IsPC)
        {
            int blocks = TryDecompressMW2PC(br, bw, info.IsSigned);
            if (blocks > 0)
                return blocks;

            // If MW2 PC fast path failed, fall through to generic strategies below
            br.BaseStream.Position = 12;
        }

        // MW2 Xbox 360 signed: same authed-chunks family as MW2 PC, but the full 25-byte
        // DB_Header pushes IWffs100 to 0x25 (vs PC's 0x15). The generic Xbox 360 streaming
        // path below only checks 0x0C, and the block decoder chokes on the auth data, so
        // handle it here. (MW2 PS3 is block format and is correctly left to the block decoder;
        // we gate on the IWffs100@0x25 marker so PS3 files don't enter this path.)
        if (info.GameVersion == GameVersion.MW2 && !info.IsPC && !info.IsWii
            && HasMW2Xbox360StreamingMagic(br))
        {
            int blocks = TryDecompressMW2Authed(br, bw, isSigned: true, mw2StreamStart: 0x25);
            if (blocks > 0)
                return blocks;

            br.BaseStream.Position = 12;
        }

        // Fast path: Xbox 360 signed streaming format (retail CoD4/WaW Xbox 360).
        // Layout: IWff0100 magic + 4B version + IWffs100 streaming magic at 0x0C +
        //         ~16KB hash table/auth from 0x14..0x400C + single deflate stream at 0x400C.
        // The generic signed scan below misses this when the inner stream is raw deflate
        // (no zlib header), so handle it directly here.
        bool isXbox360Streaming = HasXbox360StreamingMagic(br);
        if (isXbox360Streaming)
        {
            int blocks = TryDecompressXbox360Streaming(br, bw);
            if (blocks > 0)
                return blocks;

            // Streaming format detected but neither zlib nor raw deflate worked at 0x400C.
            // This is almost always an unsupported compression (e.g., Xbox 360 LZX) - bail
            // with a clear message rather than falling through to the generic block decoder,
            // which would produce a misleading "Too many decompression errors" message after
            // accidentally decoding hash-table noise.
            throw new InvalidDataException(
                "Xbox 360 signed streaming format detected (IWff0100 + IWffs100), but the " +
                $"compressed stream at offset 0x{FastFileConstants.Xbox360SignedZlibStart:X} " +
                "could not be decompressed with zlib or raw deflate. The file may use a " +
                "compression method (such as LZX) that this tool does not support, or the " +
                "stream may be corrupted.");
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
    /// Detects Xbox 360 signed streaming format by checking for the IWffs100 magic at offset 0x0C.
    /// Retail CoD4/WaW Xbox 360 files use this layout:
    ///   0x00 IWff0100  (outer signed magic)
    ///   0x08 version   (4 bytes, big-endian)
    ///   0x0C IWffs100  (streaming magic)
    ///   0x14 hash table + auth (~16KB, ends at 0x400C)
    ///   0x400C compressed stream
    /// </summary>
    private static bool HasXbox360StreamingMagic(BinaryReader br)
    {
        long savedPos = br.BaseStream.Position;
        try
        {
            if (br.BaseStream.Length < FastFileConstants.Xbox360SignedZlibStart + 2)
                return false;

            br.BaseStream.Position = 0x0C;
            byte[] bytes = br.ReadBytes(8);
            return bytes.Length == 8 &&
                   Encoding.ASCII.GetString(bytes) == FastFileConstants.StreamingHeader;
        }
        catch
        {
            return false;
        }
        finally
        {
            br.BaseStream.Position = savedPos;
        }
    }

    /// <summary>
    /// Detects signed MW2 Xbox 360 by checking for the IWffs100 streaming magic at offset
    /// 0x25 — right after the 12-byte ZoneHeader + 25-byte DB_Header. (MW2 PC puts it at
    /// 0x15 because it truncates the DB_Header to a 9-byte preamble; CoD4/WaW Xbox 360 put
    /// it at 0x0C.) This marker is what distinguishes a signed MW2 Xbox 360 stream from a
    /// block-format MW2 PS3 file, which has the same magic+version.
    /// </summary>
    private static bool HasMW2Xbox360StreamingMagic(BinaryReader br)
    {
        const int mw2Xbox360StreamingOffset = 0x25;
        long savedPos = br.BaseStream.Position;
        try
        {
            if (br.BaseStream.Length < mw2Xbox360StreamingOffset + 8)
                return false;

            br.BaseStream.Position = mw2Xbox360StreamingOffset;
            byte[] bytes = br.ReadBytes(8);
            return bytes.Length == 8 &&
                   Encoding.ASCII.GetString(bytes) == FastFileConstants.StreamingHeader;
        }
        catch
        {
            return false;
        }
        finally
        {
            br.BaseStream.Position = savedPos;
        }
    }

    /// <summary>
    /// Decompresses an Xbox 360 signed streaming FastFile. The compressed stream sits at a
    /// fixed offset (0x400C) after the hash table and auth blob, and may be either standard
    /// zlib (78 XX header) or raw deflate. We try both before giving up.
    /// </summary>
    private static int TryDecompressXbox360Streaming(BinaryReader br, BinaryWriter bw)
    {
        long outputStartPos = bw.BaseStream.Position;
        int streamStart = FastFileConstants.Xbox360SignedZlibStart;

        try
        {
            br.BaseStream.Position = 0;
            byte[] fileData = br.ReadBytes((int)br.BaseStream.Length);

            if (fileData.Length <= streamStart + 2)
                return 0;

            int streamLength = fileData.Length - streamStart;

            // Try ZLibStream first if the stream has a zlib header
            if (FastFileConstants.HasZlibHeader(fileData, streamStart))
            {
                bw.BaseStream.Position = outputStartPos;
                bw.BaseStream.SetLength(outputStartPos);
                try
                {
                    using var input = new MemoryStream(fileData, streamStart, streamLength);
                    using (var zlib = new ZLibStream(input, CompressionMode.Decompress))
                    {
                        zlib.CopyTo(bw.BaseStream);
                    }

                    if (bw.BaseStream.Length - outputStartPos > 10240)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[FastFileProcessor] Xbox 360 streaming (zlib) decompressed from 0x{streamStart:X}, output size: {bw.BaseStream.Length - outputStartPos}");
                        return 1;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[FastFileProcessor] Xbox 360 streaming zlib attempt failed: {ex.Message}");
                }
            }

            // Fall back to raw deflate (no zlib header)
            bw.BaseStream.Position = outputStartPos;
            bw.BaseStream.SetLength(outputStartPos);
            try
            {
                using var input = new MemoryStream(fileData, streamStart, streamLength);
                using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
                {
                    deflate.CopyTo(bw.BaseStream);
                }

                if (bw.BaseStream.Length - outputStartPos > 10240)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[FastFileProcessor] Xbox 360 streaming (raw deflate) decompressed from 0x{streamStart:X}, output size: {bw.BaseStream.Length - outputStartPos}");
                    return 1;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[FastFileProcessor] Xbox 360 streaming raw deflate attempt failed: {ex.Message}");
            }

            // Both attempts failed - reset output and let caller try other strategies
            bw.BaseStream.Position = outputStartPos;
            bw.BaseStream.SetLength(outputStartPos);
            return 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[FastFileProcessor] Xbox 360 streaming decompression error: {ex.Message}");
            bw.BaseStream.Position = outputStartPos;
            bw.BaseStream.SetLength(outputStartPos);
            return 0;
        }
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

        for (int i = 0; i < 50000; i++)
        {
            if (br.BaseStream.Position >= br.BaseStream.Length - 1)
                break;

            byte[] lengthBytes = br.ReadBytes(2);
            if (lengthBytes.Length < 2) break;

            // PC uses little-endian, console uses big-endian
            int chunkLength = isLittleEndian
                ? (lengthBytes[0] | (lengthBytes[1] << 8))
                : ((lengthBytes[0] << 8) | lengthBytes[1]);

            // End marker is 0x0001. 0x0000 has two meanings:
            //   - "next 64KB are stored uncompressed" — a Treyarch extension used on WaW PS3 UI
            //     zones to avoid wasting CPU on already-compressed payloads like bink video.
            //   - Some files end with 0x0000 instead of 0x0001 — treat as EOF only when there
            //     isn't a full raw block left in the file.
            if (chunkLength == 1) break;
            if (chunkLength == 0)
            {
                long remaining = br.BaseStream.Length - br.BaseStream.Position;
                if (remaining < BlockSize)
                    break; // treat as a tolerant end marker
                byte[] rawBlock = br.ReadBytes(BlockSize);
                bw.Write(rawBlock);
                blockCount++;
                lastGoodPosition = br.BaseStream.Position;
                continue;
            }

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

                // Any successful decompression that produces nontrivial output is real - both
                // ZLibStream and DeflateStream are strict about valid input, so false positives
                // are rare. The original 10KB threshold rejected small load files like MW2 PC's
                // mp_rust_load.ff (decompressed zone = 1735 bytes), making them appear "not valid".
                if (bw.BaseStream.Length >= 64)
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

                if (bw.BaseStream.Length >= 64)
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
    /// Decompresses an MW2 PC FastFile (both signed and unsigned variants).
    ///
    /// Unsigned (IWffu100): single zlib stream begins at offset 0x15 after a 9-byte preamble
    /// (1 byte allowOnlineUpdate + 8 bytes fileCreationTime). The PS3 extended header's
    /// region/entryCount/entries/fileSizes block is absent on PC.
    ///
    /// Signed (IWff0100): uses Infinity Ward's "authed chunks" format per OpenAssetTools'
    /// ProcessorAuthedBlocks. Layout:
    ///   0x00..0x0B  ZoneHeader (IWff0100 + version, 12 bytes)
    ///   0x0C..0x14  9-byte preamble (allowOnlineUpdate + fileCreationTime)
    ///   0x15..0x2024  DB_AuthHeader (8144 bytes: IWffs100 + hashes + RSA-2048 signature + sub-header)
    ///   0x2025..0x2054  48 bytes padding (AUTHED_CHUNK_SIZE - sizeof(DB_AuthHeader))
    ///   0x2055..  Stream of 8KB chunks. Each group of 257 chunks = 1 hash chunk (skipped) +
    ///             256 data chunks (concatenated and fed to zlib).
    /// Reference: github.com/Laupetin/OpenAssetTools src/ZoneLoading/Loading/Processor/
    ///   ProcessorAuthedBlocks.cpp + src/Common/Game/IW4/IW4.h DB_AuthHeader/DB_AuthSubHeader.
    /// </summary>
    private static int TryDecompressMW2PC(BinaryReader br, BinaryWriter bw, bool isSigned)
        => TryDecompressMW2Authed(br, bw, isSigned, mw2StreamStart: 0x15);

    /// <summary>
    /// Decompresses the MW2 authed-chunks / single-zlib family shared by PC and Xbox 360.
    /// The two differ only in where the stream begins: PC truncates the DB_Header to a
    /// 9-byte preamble so IWffs100/zlib starts at 0x15; Xbox 360 keeps the full 25-byte
    /// DB_Header so it starts at 0x25. Everything after that point (DB_AuthHeader, padding,
    /// 8 KB authed chunks in groups of 257) is identical.
    /// </summary>
    /// <param name="mw2StreamStart">Offset of IWffs100 (signed) or the zlib stream (unsigned):
    /// 0x15 for MW2 PC, 0x25 for MW2 Xbox 360.</param>
    private static int TryDecompressMW2Authed(BinaryReader br, BinaryWriter bw, bool isSigned, int mw2StreamStart)
    {
        long outputStartPos = bw.BaseStream.Position;

        try
        {
            br.BaseStream.Position = 0;
            byte[] fileData = br.ReadBytes((int)br.BaseStream.Length);

            byte[] zlibInput = isSigned
                ? ExtractMW2AuthedPayload(fileData, mw2StreamStart)
                : SliceMW2UnsignedZlibStream(fileData, mw2StreamStart);

            if (zlibInput == null || zlibInput.Length < 2 || !FastFileConstants.HasZlibHeader(zlibInput, 0))
                return 0;

            using var input = new MemoryStream(zlibInput, writable: false);
            using (var zlib = new ZLibStream(input, CompressionMode.Decompress))
            {
                zlib.CopyTo(bw.BaseStream);
            }

            System.Diagnostics.Debug.WriteLine(
                $"[FastFileProcessor] MW2 ({(isSigned ? "signed" : "unsigned")}, start 0x{mw2StreamStart:X}) decompressed, " +
                $"zlib input: {zlibInput.Length} bytes, output: {bw.BaseStream.Position - outputStartPos}");
            return 1;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[FastFileProcessor] MW2 authed/zlib decompression failed: {ex.Message}");
            bw.BaseStream.Position = outputStartPos;
            bw.BaseStream.SetLength(outputStartPos);
            return 0;
        }
    }

    // Unsigned MW2: zlib begins right at the stream start (0x15 PC / 0x25 Xbox 360).
    private static byte[] SliceMW2UnsignedZlibStream(byte[] fileData, int zlibStart)
    {
        if (fileData.Length <= zlibStart + 2) return Array.Empty<byte>();
        byte[] result = new byte[fileData.Length - zlibStart];
        Buffer.BlockCopy(fileData, zlibStart, result, 0, result.Length);
        return result;
    }

    private const int Mw2AuthedChunkSize = 0x2000;        // AUTHED_CHUNK_SIZE
    private const int Mw2AuthedChunksPerGroup = 256;      // AUTHED_CHUNK_COUNT_PER_GROUP

    /// <summary>
    /// Walks the authed-chunk stream of a signed MW2 FastFile (PC or Xbox 360) and returns
    /// the concatenated payload (per-group hash chunks stripped) ready for zlib. The
    /// DB_AuthHeader + padding occupy exactly one AUTHED_CHUNK_SIZE, so the chunk stream
    /// begins at <paramref name="authStart"/> + AUTHED_CHUNK_SIZE.
    /// </summary>
    /// <param name="authStart">Offset of IWffs100 / DB_AuthHeader (0x15 PC, 0x25 Xbox 360).</param>
    private static byte[] ExtractMW2AuthedPayload(byte[] fileData, int authStart)
    {
        int dataStart = authStart + Mw2AuthedChunkSize;
        if (fileData.Length <= dataStart + Mw2AuthedChunkSize)
            return Array.Empty<byte>();

        // Upper-bound the output: every 257 file chunks yields 256 payload chunks.
        int availableChunks = (fileData.Length - dataStart) / Mw2AuthedChunkSize;
        int maxPayloadBytes = availableChunks * Mw2AuthedChunkSize;
        var payload = new byte[maxPayloadBytes];
        int payloadLen = 0;

        int pos = dataStart;
        int chunkInGroup = 0; // 0 = hash chunk (skip); 1..256 = data chunks (keep)
        while (pos + Mw2AuthedChunkSize <= fileData.Length)
        {
            if (chunkInGroup == 0)
            {
                // Hash chunk for this group — skip (would normally be SHA-256-verified)
                chunkInGroup = 1;
            }
            else
            {
                Buffer.BlockCopy(fileData, pos, payload, payloadLen, Mw2AuthedChunkSize);
                payloadLen += Mw2AuthedChunkSize;
                chunkInGroup++;
                if (chunkInGroup > Mw2AuthedChunksPerGroup)
                    chunkInGroup = 0; // next group begins with a hash chunk
            }
            pos += Mw2AuthedChunkSize;
        }

        // Trailing partial chunk: only include if it's a data chunk (not a hash chunk).
        if (pos < fileData.Length && chunkInGroup != 0)
        {
            int remaining = fileData.Length - pos;
            Array.Resize(ref payload, payloadLen + remaining);
            Buffer.BlockCopy(fileData, pos, payload, payloadLen, remaining);
            payloadLen += remaining;
        }
        else if (payloadLen < payload.Length)
        {
            Array.Resize(ref payload, payloadLen);
        }

        return payload;
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
    /// Single entry point for recompressing a zone file back to a FastFile. Dispatches to
    /// the correct compressor (PC single-zlib, Xbox 360 signed streaming, MW2 extended
    /// header, or standard PS3/Xbox 360 block format) based on game/platform/signed.
    ///
    /// This is the consolidation point that the editor's per-game handlers all funnel
    /// through, so platform-specific compression logic lives in one place.
    /// </summary>
    /// <param name="zoneFilePath">Path to the decompressed .zone file</param>
    /// <param name="ffFilePath">Path where the FastFile should be written</param>
    /// <param name="gameVersion">CoD4, WaW, or MW2</param>
    /// <param name="platform">"PS3", "Xbox360", "PC", or "Wii"</param>
    /// <param name="signed">True for Xbox 360 signed (IWff0100 + IWffs100 streaming) format</param>
    /// <param name="originalFfPath">For signed Xbox 360 and MW2, the original FF to copy
    /// hash table / extended header from. Pass null for fresh builds.</param>
    public static void Recompress(string zoneFilePath, string ffFilePath, GameVersion gameVersion, string platform, bool signed, string? originalFfPath = null)
    {
        // PC and Wii: single zlib stream variants. Same compressor — only version
        // bytes differ (PC LE, Wii BE). MW2 PC has its own quirk layout (9-byte
        // preamble after the 12-byte header, then zlib at 0x15) and is handled by
        // CompressMW2PC. Signed MW2 PC inputs are saved as unsigned — RSA re-signing
        // isn't possible without IW's private key.
        bool isSingleStream = string.Equals(platform, "PC", StringComparison.OrdinalIgnoreCase)
                           || string.Equals(platform, "Wii", StringComparison.OrdinalIgnoreCase);
        if (isSingleStream)
        {
            if (gameVersion == GameVersion.MW2 &&
                string.Equals(platform, "PC", StringComparison.OrdinalIgnoreCase))
            {
                CompressMW2PC(zoneFilePath, ffFilePath, originalFfPath);
                return;
            }
            byte[] zoneData = File.ReadAllBytes(zoneFilePath);
            byte[] ffData = new Compiler(gameVersion, platform).Compile(zoneData);
            File.WriteAllBytes(ffFilePath, ffData);
            return;
        }

        // MW2 has its own extended-header compressor regardless of platform.
        // Detect Xbox 360 (vs PS3) from the platform string.
        if (gameVersion == GameVersion.MW2)
        {
            byte[] mw2VersionBytes = FastFileInfo.GetVersionBytes(gameVersion, platform);
            bool isXbox360 = string.Equals(platform, "Xbox360", StringComparison.OrdinalIgnoreCase);
            CompressMW2(zoneFilePath, ffFilePath, mw2VersionBytes, isXbox360, originalFfPath);
            return;
        }

        // CoD4/WaW Xbox 360 signed: IWffs100 streaming format with hash table preserved.
        if (signed)
        {
            byte[] sigVersionBytes = FastFileInfo.GetVersionBytes(gameVersion, "Xbox360");
            CompressXbox360Signed(zoneFilePath, ffFilePath, sigVersionBytes, originalFfPath);
            return;
        }

        // CoD4/WaW unsigned (PS3 or Xbox 360): standard 64KB block format.
        Compress(zoneFilePath, ffFilePath, gameVersion, platform, signed: false);
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
    public static int CompressMW2(string inputPath, string outputPath, byte[] versionBytes, bool isXbox360, string? originalFfPath)
    {
        // Read extended header from original file BEFORE opening output (in case they're the same file)
        byte[]? originalExtendedHeader = null;
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
    public static byte[]? ReadMW2ExtendedHeader(string ffPath)
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

    /// <summary>
    /// Compresses a zone file to MW2 PC FastFile format (unsigned).
    /// MW2 PC layout: 12-byte standard header (IWffu100 + 0x114 LE) + 9-byte preamble
    /// (allowOnlineUpdate + fileCreationTime) + single zlib stream of the entire zone.
    /// No end marker.
    ///
    /// Signed MW2 PC inputs are intentionally saved as unsigned: re-signing the
    /// DB_AuthHeader requires Infinity Ward's RSA-2048 private key. Unsigned MW2 PC FFs
    /// are a valid loadable variant (used for SP/campaign files in retail).
    /// </summary>
    /// <param name="inputPath">Path to the .zone file</param>
    /// <param name="outputPath">Path to output the .ff file</param>
    /// <param name="originalFfPath">Optional path to original FF to preserve the 9-byte preamble.
    /// Pass null for fresh builds; a default preamble (allowOnlineUpdate=1, time=0) is used.</param>
    /// <returns>1 (single stream)</returns>
    public static int CompressMW2PC(string inputPath, string outputPath, string? originalFfPath = null)
    {
        // Read original preamble BEFORE opening the output writer (input/output may be the same file).
        byte[]? originalPreamble = null;
        if (!string.IsNullOrEmpty(originalFfPath) && File.Exists(originalFfPath))
        {
            originalPreamble = ReadMW2PCPreamble(originalFfPath);
        }

        byte[] zoneData = File.ReadAllBytes(inputPath);

        using var bw = new BinaryWriter(new FileStream(outputPath, FileMode.Create, FileAccess.Write), Encoding.Default);

        // 12-byte standard header (IWffu100 + 0x114 LE)
        bw.Write(FastFileConstants.UnsignedHeaderBytes);
        bw.Write(FastFileInfo.GetVersionBytes(GameVersion.MW2, "PC"));

        // 9-byte preamble: allowOnlineUpdate (1) + fileCreationTime (8)
        if (originalPreamble != null && originalPreamble.Length == 9)
        {
            bw.Write(originalPreamble);
        }
        else
        {
            bw.Write((byte)0x01);    // allowOnlineUpdate = true
            bw.Write(new byte[8]);   // fileCreationTime = 0
        }

        // Single zlib stream covering the entire zone
        byte[] compressedData = CompressFullZlib(zoneData);
        bw.Write(compressedData);

        return 1;
    }

    /// <summary>
    /// Reads the 9-byte MW2 PC preamble (allowOnlineUpdate + fileCreationTime) from offset 0x0C.
    /// Works for both unsigned and signed MW2 PC files — both place the preamble at the same offset.
    /// </summary>
    /// <param name="ffPath">Path to the MW2 PC FF file</param>
    /// <returns>9-byte preamble, or null if the file can't be read</returns>
    public static byte[]? ReadMW2PCPreamble(string ffPath)
    {
        try
        {
            using var reader = new BinaryReader(File.OpenRead(ffPath));
            if (reader.BaseStream.Length < 0x15) return null;
            reader.BaseStream.Seek(12, SeekOrigin.Begin);
            return reader.ReadBytes(9);
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
    public static int CompressXbox360Signed(string inputPath, string outputPath, GameVersion gameVersion, string? originalFfPath)
    {
        // Read hash table from original file before opening output
        byte[]? hashTableAndAuth = null;
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
    public static int CompressXbox360Signed(string inputPath, string outputPath, byte[] versionBytes, string? originalFfPath)
    {
        // Read hash table from original file before opening output
        byte[]? hashTableAndAuth = null;
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
