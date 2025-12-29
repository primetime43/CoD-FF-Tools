using System.IO.Compression;
using System.Text;

namespace FastFileLib;

/// <summary>
/// Compiles zone data into a FastFile (.ff) with proper header and zlib block compression.
/// </summary>
public class Compiler
{
    private readonly GameVersion _gameVersion;
    private readonly bool _xbox360Signed;
    private readonly string? _originalFfPath;

    public Compiler(GameVersion gameVersion)
    {
        _gameVersion = gameVersion;
        _xbox360Signed = false;
        _originalFfPath = null;
    }

    /// <summary>
    /// Creates a compiler for Xbox 360 signed format.
    /// Requires an original signed FF file to copy the hash table from.
    /// </summary>
    /// <param name="gameVersion">Target game version</param>
    /// <param name="originalFfPath">Path to original signed FF (to preserve hash table)</param>
    public Compiler(GameVersion gameVersion, string originalFfPath)
    {
        _gameVersion = gameVersion;
        _xbox360Signed = true;
        _originalFfPath = originalFfPath;
    }

    /// <summary>
    /// Compiles zone data into a complete FastFile.
    /// </summary>
    /// <param name="zoneData">The raw zone data from ZoneBuilder.Build()</param>
    /// <returns>The complete FastFile bytes ready to be written to disk.</returns>
    public byte[] Compile(byte[] zoneData)
    {
        if (_xbox360Signed)
        {
            return CompileXbox360Signed(zoneData);
        }

        var fastFile = new List<byte>();

        // Build FastFile header (12 bytes for CoD4/WaW)
        byte[] header = BuildFastFileHeader();
        fastFile.AddRange(header);

        // Compress zone data in 64KB blocks
        byte[] compressedBlocks = CompressZoneBlocks(zoneData);
        fastFile.AddRange(compressedBlocks);

        // End marker: 00 01
        fastFile.AddRange(new byte[] { 0x00, 0x01 });

        return fastFile.ToArray();
    }

    /// <summary>
    /// Compiles zone data into Xbox 360 signed format.
    /// Preserves the hash table from the original file.
    /// </summary>
    private byte[] CompileXbox360Signed(byte[] zoneData)
    {
        var fastFile = new List<byte>();

        // Write signed header (IWff0100)
        fastFile.AddRange(FastFileConstants.SignedHeaderBytes);

        // Write version (big-endian)
        fastFile.AddRange(FastFileConstants.GetVersionBytes(_gameVersion));

        // Write streaming header (IWffs100)
        fastFile.AddRange(FastFileConstants.StreamingHeaderBytes);

        // Read and write hash table from original file (or zeros if not available)
        byte[] hashTableAndAuth = new byte[FastFileConstants.Xbox360SignedHashTableSize];
        if (!string.IsNullOrEmpty(_originalFfPath) && File.Exists(_originalFfPath))
        {
            using var origReader = new BinaryReader(File.OpenRead(_originalFfPath));
            origReader.BaseStream.Seek(FastFileConstants.Xbox360SignedHashTableStart, SeekOrigin.Begin);
            origReader.Read(hashTableAndAuth, 0, hashTableAndAuth.Length);
        }
        fastFile.AddRange(hashTableAndAuth);

        // Compress entire zone as single zlib stream
        byte[] compressedData = FastFileProcessor.CompressFullZlib(zoneData);
        fastFile.AddRange(compressedData);

        // No end marker for signed format
        return fastFile.ToArray();
    }

    /// <summary>
    /// Builds the FastFile header.
    /// </summary>
    private byte[] BuildFastFileHeader()
    {
        var header = new List<byte>();

        // Magic: "IWffu100" (8 bytes)
        header.AddRange(Encoding.ASCII.GetBytes(FastFileConstants.UnsignedHeader));

        // Version (4 bytes, big-endian)
        header.AddRange(FastFileConstants.GetVersionBytes(_gameVersion));

        return header.ToArray();
    }

    /// <summary>
    /// Compresses the zone data into 64KB zlib blocks.
    /// Each block format: [2-byte length (big-endian)] + [compressed data (zlib without header)]
    /// </summary>
    private byte[] CompressZoneBlocks(byte[] zoneData)
    {
        var compressed = new List<byte>();

        using var reader = new MemoryStream(zoneData);
        int blockCount = (zoneData.Length + FastFileConstants.BlockSize - 1) / FastFileConstants.BlockSize;

        for (int i = 0; i < blockCount; i++)
        {
            // Read up to 64KB
            int bytesToRead = Math.Min(FastFileConstants.BlockSize, (int)(zoneData.Length - reader.Position));
            byte[] block = new byte[bytesToRead];
            reader.Read(block, 0, bytesToRead);

            // Compress the block using ZLibStream
            byte[] compressedBlock = CompressBlockWithZlib(block);

            // The compressed data includes a 2-byte ZLIB header (usually 78 DA for best compression)
            // We need to write: [length without header] + [compressed data without first 2 bytes]
            int compressedLength = compressedBlock.Length - 2;

            // Write length as 2-byte big-endian
            compressed.Add((byte)(compressedLength >> 8));
            compressed.Add((byte)(compressedLength & 0xFF));

            // Write compressed data (skip first 2 bytes - the ZLIB header)
            for (int j = 2; j < compressedBlock.Length; j++)
            {
                compressed.Add(compressedBlock[j]);
            }
        }

        return compressed.ToArray();
    }

    /// <summary>
    /// Compresses a block using ZLibStream.
    /// </summary>
    private static byte[] CompressBlockWithZlib(byte[] data)
    {
        using var outputStream = new MemoryStream();
        using (var zlibStream = new ZLibStream(outputStream, CompressionLevel.Optimal))
        {
            zlibStream.Write(data, 0, data.Length);
        }
        return outputStream.ToArray();
    }

    /// <summary>
    /// Compiles zone data and saves to a file.
    /// </summary>
    /// <param name="zoneData">The raw zone data from ZoneBuilder.Build()</param>
    /// <param name="outputPath">The output .ff file path.</param>
    /// <param name="saveZone">If true, also saves the uncompressed zone file.</param>
    public void CompileToFile(byte[] zoneData, string outputPath, bool saveZone = false)
    {
        byte[] fastFile = Compile(zoneData);
        File.WriteAllBytes(outputPath, fastFile);

        if (saveZone)
        {
            string zonePath = Path.ChangeExtension(outputPath, ".zone");
            File.WriteAllBytes(zonePath, zoneData);
        }
    }

    /// <summary>
    /// High-level method to compile from a ZoneBuilder directly.
    /// </summary>
    public byte[] CompileFromBuilder(ZoneBuilder builder)
    {
        byte[] zoneData = builder.Build();
        return Compile(zoneData);
    }

    /// <summary>
    /// High-level method to compile from a ZoneBuilder and save to file.
    /// </summary>
    public void CompileFromBuilderToFile(ZoneBuilder builder, string outputPath, bool saveZone = false)
    {
        byte[] zoneData = builder.Build();
        CompileToFile(zoneData, outputPath, saveZone);
    }
}
