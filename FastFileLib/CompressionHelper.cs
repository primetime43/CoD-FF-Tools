using System.IO.Compression;

namespace FastFileLib;

/// <summary>
/// Helper methods for zlib compression/decompression used in FastFile processing.
/// </summary>
public static class CompressionHelper
{
    /// <summary>
    /// Compresses data using zlib (deflate with zlib header).
    /// Used for zone-level raw file compression in MW2.
    /// </summary>
    /// <param name="data">Uncompressed data</param>
    /// <param name="level">Compression level (default: Optimal)</param>
    /// <returns>Zlib-compressed data with 0x78 header</returns>
    public static byte[] CompressZlib(byte[] data, CompressionLevel level = CompressionLevel.Optimal)
    {
        if (data == null || data.Length == 0)
            return Array.Empty<byte>();

        using var outputStream = new MemoryStream();
        using (var zlibStream = new ZLibStream(outputStream, level, leaveOpen: true))
        {
            zlibStream.Write(data, 0, data.Length);
        }
        return outputStream.ToArray();
    }

    /// <summary>
    /// Decompresses zlib-compressed data.
    /// </summary>
    /// <param name="compressedData">Zlib-compressed data</param>
    /// <returns>Decompressed data</returns>
    public static byte[] DecompressZlib(byte[] compressedData)
    {
        if (compressedData == null || compressedData.Length == 0)
            return Array.Empty<byte>();

        using var inputStream = new MemoryStream(compressedData);
        using var zlibStream = new ZLibStream(inputStream, CompressionMode.Decompress);
        using var outputStream = new MemoryStream();
        zlibStream.CopyTo(outputStream);
        return outputStream.ToArray();
    }

    /// <summary>
    /// Compresses data using raw deflate (no zlib header).
    /// Used for FF-level block compression in MW2 PS3.
    /// </summary>
    /// <param name="data">Uncompressed data</param>
    /// <param name="level">Compression level (default: Optimal)</param>
    /// <returns>Raw deflate-compressed data (no header)</returns>
    public static byte[] CompressRawDeflate(byte[] data, CompressionLevel level = CompressionLevel.Optimal)
    {
        if (data == null || data.Length == 0)
            return Array.Empty<byte>();

        // Compress with zlib first
        byte[] zlibData = CompressZlib(data, level);

        // Strip the 2-byte zlib header (0x78 xx)
        if (zlibData.Length > 2)
        {
            byte[] rawDeflate = new byte[zlibData.Length - 2];
            Array.Copy(zlibData, 2, rawDeflate, 0, rawDeflate.Length);
            return rawDeflate;
        }

        return zlibData;
    }

    /// <summary>
    /// Tries to decompress zlib data, returning success status.
    /// </summary>
    /// <param name="compressedData">Zlib-compressed data</param>
    /// <param name="decompressedData">Decompressed data if successful</param>
    /// <returns>True if decompression succeeded</returns>
    public static bool TryDecompressZlib(byte[] compressedData, out byte[] decompressedData)
    {
        decompressedData = Array.Empty<byte>();

        try
        {
            decompressedData = DecompressZlib(compressedData);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
