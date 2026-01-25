using System.IO.Compression;

namespace FastFileLib;

/// <summary>
/// Result of applying changes to zone data.
/// </summary>
public class ZoneSaveResult
{
    public bool Success { get; set; }
    public int RawFileChangeCount { get; set; }
    public int MenuChangeCount { get; set; }
    public int LocalizeChangeCount { get; set; }
    public bool RequiresRebuild { get; set; }
    public string? RebuildReason { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public bool HasChanges => RawFileChangeCount > 0 || MenuChangeCount > 0 || LocalizeChangeCount > 0;
}

/// <summary>
/// Represents a raw file that can be patched into zone data.
/// </summary>
public class RawFilePatchInfo
{
    public string FileName { get; set; } = string.Empty;
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public int CodeStartPosition { get; set; }
    public int MaxSize { get; set; }
    public bool IsCompressed { get; set; }
    public int CompressedSize { get; set; }
    public int StartOfFileHeader { get; set; }
}

/// <summary>
/// Service for applying changes to zone data and saving FastFiles.
/// This contains the core save logic that can be reused across different UI projects.
/// </summary>
public static class ZoneSaveService
{
    /// <summary>
    /// Applies raw file content changes to zone data.
    /// Handles both compressed (MW2 16-byte header) and uncompressed raw files.
    /// </summary>
    /// <param name="zoneData">The zone data buffer to modify.</param>
    /// <param name="files">List of raw files with their new content.</param>
    /// <returns>Result indicating success and any files that couldn't be patched.</returns>
    public static ZoneSaveResult ApplyRawFileChanges(byte[] zoneData, IEnumerable<RawFilePatchInfo> files)
    {
        var result = new ZoneSaveResult { Success = true };

        foreach (var file in files)
        {
            if (file.Content == null || file.Content.Length == 0)
                continue;

            try
            {
                if (file.IsCompressed && file.CompressedSize > 0)
                {
                    // Handle compressed raw files (MW2 16-byte header format)
                    var compressResult = ApplyCompressedRawFile(zoneData, file);
                    if (!compressResult.Success)
                    {
                        if (compressResult.RequiresRebuild)
                        {
                            result.RequiresRebuild = true;
                            result.RebuildReason = $"Compressed content for '{file.FileName}' ({compressResult.NewCompressedSize} bytes) exceeds original slot ({file.CompressedSize} bytes).";
                            return result;
                        }
                        result.Errors.Add($"Failed to patch '{file.FileName}': {compressResult.Error}");
                        result.Success = false;
                        continue;
                    }
                }
                else
                {
                    // Handle uncompressed raw files
                    if (file.Content.Length > file.MaxSize)
                    {
                        result.Errors.Add($"Raw file '{file.FileName}' content ({file.Content.Length} bytes) exceeds max size ({file.MaxSize} bytes).");
                        result.Success = false;
                        continue;
                    }

                    // Patch content directly into zone data
                    for (int i = 0; i < file.MaxSize && file.CodeStartPosition + i < zoneData.Length; i++)
                    {
                        zoneData[file.CodeStartPosition + i] = i < file.Content.Length ? file.Content[i] : (byte)0;
                    }
                }

                result.RawFileChangeCount++;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error patching '{file.FileName}': {ex.Message}");
                result.Success = false;
            }
        }

        return result;
    }

    private class CompressResult
    {
        public bool Success { get; set; }
        public bool RequiresRebuild { get; set; }
        public int NewCompressedSize { get; set; }
        public string? Error { get; set; }
    }

    private static CompressResult ApplyCompressedRawFile(byte[] zoneData, RawFilePatchInfo file)
    {
        // Compress the content
        byte[] compressedContent;
        using (var ms = new MemoryStream())
        {
            using (var zlib = new ZLibStream(ms, CompressionLevel.Optimal, true))
            {
                zlib.Write(file.Content, 0, file.Content.Length);
            }
            compressedContent = ms.ToArray();
        }

        // Check if compressed content fits in the original slot
        if (compressedContent.Length > file.CompressedSize)
        {
            return new CompressResult
            {
                Success = false,
                RequiresRebuild = true,
                NewCompressedSize = compressedContent.Length
            };
        }

        // Update header fields (16-byte format: [FFFF][compLen][len][FFFF])
        int hdrOff = file.StartOfFileHeader;

        // compressedLen at offset +4
        zoneData[hdrOff + 4] = (byte)(compressedContent.Length >> 24);
        zoneData[hdrOff + 5] = (byte)(compressedContent.Length >> 16);
        zoneData[hdrOff + 6] = (byte)(compressedContent.Length >> 8);
        zoneData[hdrOff + 7] = (byte)(compressedContent.Length);

        // len at offset +8
        zoneData[hdrOff + 8] = (byte)(file.Content.Length >> 24);
        zoneData[hdrOff + 9] = (byte)(file.Content.Length >> 16);
        zoneData[hdrOff + 10] = (byte)(file.Content.Length >> 8);
        zoneData[hdrOff + 11] = (byte)(file.Content.Length);

        // Write compressed data (pad with zeros if smaller)
        for (int i = 0; i < file.CompressedSize && file.CodeStartPosition + i < zoneData.Length; i++)
        {
            zoneData[file.CodeStartPosition + i] = i < compressedContent.Length ? compressedContent[i] : (byte)0;
        }

        return new CompressResult { Success = true, NewCompressedSize = compressedContent.Length };
    }

}
