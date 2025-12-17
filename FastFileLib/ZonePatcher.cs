using System.Text;
using FastFileLib.Models;

namespace FastFileLib;

/// <summary>
/// Result of a zone patching operation.
/// </summary>
public class PatchResult
{
    public byte[] PatchedZone { get; set; } = Array.Empty<byte>();
    public List<string> ReplacedFiles { get; set; } = new();
    public List<string> SkippedFiles { get; set; } = new();
    public bool HasSkippedFiles => SkippedFiles.Count > 0;
}

/// <summary>
/// Patches an existing zone file by replacing raw files while preserving all other zone structure.
/// NOTE: Only REPLACEMENTS are supported. Adding new rawfiles is not possible due to zone structure constraints.
/// </summary>
public class ZonePatcher
{
    private readonly byte[] _originalZone;
    private readonly GameVersion _gameVersion;

    private static readonly string[] ValidExtensions = {
        ".cfg", ".gsc", ".atr", ".csc", ".rmb", ".arena", ".vision", ".txt", ".str", ".menu"
    };

    public ZonePatcher(byte[] originalZone, GameVersion gameVersion)
    {
        _originalZone = originalZone;
        _gameVersion = gameVersion;
    }

    /// <summary>
    /// Patches the zone by replacing existing raw files.
    /// Files that don't exist in the zone will be skipped (additions not supported).
    /// </summary>
    public PatchResult PatchWithResult(List<RawFile> files)
    {
        var result = new PatchResult();
        result.PatchedZone = PatchInternal(files, result.ReplacedFiles, result.SkippedFiles);
        return result;
    }

    /// <summary>
    /// Patches the zone by replacing existing raw files.
    /// Preserves all other zone structure (asset pool, localize entries, footer, etc.)
    /// </summary>
    public byte[] Patch(List<RawFile> files)
    {
        return PatchInternal(files, null, null);
    }

    private byte[] PatchInternal(List<RawFile> files, List<string>? replacedFiles, List<string>? skippedFiles)
    {
        // Find all existing raw files in the zone
        var existingFiles = FindRawFilesInZone(_originalZone);

        // Separate files into replacements vs additions
        var replacements = new List<(RawFile file, RawFileLocation loc)>();
        var additions = new List<RawFile>();

        foreach (var file in files)
        {
            var existing = existingFiles.FirstOrDefault(f =>
                f.Name.Equals(file.Name, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
                replacements.Add((file, existing));
            else
                additions.Add(file);
        }

        // Work with a copy of the zone
        var zone = new List<byte>(_originalZone);
        int totalShift = 0;

        // Sort replacements by offset
        replacements.Sort((a, b) => a.loc.DataOffset.CompareTo(b.loc.DataOffset));

        // Step 1: Replace existing files
        foreach (var (file, loc) in replacements)
        {
            int adjSizeOffset = loc.SizeOffset + totalShift;
            int adjDataOffset = loc.DataOffset + totalShift;

            int oldSize = loc.DataSize;
            int newSize = file.Data.Length;
            int sizeDiff = newSize - oldSize;

            // Update size field (big-endian)
            zone[adjSizeOffset] = (byte)(newSize >> 24);
            zone[adjSizeOffset + 1] = (byte)(newSize >> 16);
            zone[adjSizeOffset + 2] = (byte)(newSize >> 8);
            zone[adjSizeOffset + 3] = (byte)newSize;

            if (sizeDiff == 0)
            {
                for (int i = 0; i < newSize; i++)
                    zone[adjDataOffset + i] = file.Data[i];
            }
            else if (sizeDiff < 0)
            {
                for (int i = 0; i < newSize; i++)
                    zone[adjDataOffset + i] = file.Data[i];
                zone.RemoveRange(adjDataOffset + newSize, -sizeDiff);
                totalShift += sizeDiff;
            }
            else
            {
                for (int i = 0; i < oldSize; i++)
                    zone[adjDataOffset + i] = file.Data[i];
                var extra = new byte[sizeDiff];
                Array.Copy(file.Data, oldSize, extra, 0, sizeDiff);
                zone.InsertRange(adjDataOffset + oldSize, extra);
                totalShift += sizeDiff;
            }

            // Track replaced file
            replacedFiles?.Add(file.Name);
        }

        // Step 2: Handle additions (new files that don't exist in zone)
        if (additions.Count > 0)
        {
            // LIMITATION: Adding new rawfiles to existing zones is NOT supported.
            // Track skipped files for reporting
            foreach (var file in additions)
            {
                skippedFiles?.Add(file.Name);
            }
        }

        // Step 3: Update header sizes based on the actual size change
        byte[] result = zone.ToArray();
        UpdateZoneHeaderSizes(result, totalShift);

        return result;
    }

    private List<RawFileLocation> FindRawFilesInZone(byte[] zoneData)
    {
        var files = new List<RawFileLocation>();
        var foundOffsets = new HashSet<int>();

        foreach (var ext in ValidExtensions)
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

                int markerEnd = i - 1;
                while (markerEnd >= 4)
                {
                    if (zoneData[markerEnd] == 0xFF &&
                        zoneData[markerEnd - 1] == 0xFF &&
                        zoneData[markerEnd - 2] == 0xFF &&
                        zoneData[markerEnd - 3] == 0xFF)
                        break;
                    markerEnd--;
                    if (i - markerEnd > 300)
                    {
                        markerEnd = -1;
                        break;
                    }
                }

                if (markerEnd < 4) continue;
                if (zoneData[markerEnd + 1] == 0x00) continue;

                int sizeOffset = markerEnd - 7;
                if (sizeOffset < 0) continue;

                int headerOffset = sizeOffset - 4;
                if (headerOffset < 0) continue;
                if (foundOffsets.Contains(headerOffset)) continue;

                int size = (zoneData[sizeOffset] << 24) |
                          (zoneData[sizeOffset + 1] << 16) |
                          (zoneData[sizeOffset + 2] << 8) |
                          zoneData[sizeOffset + 3];

                if (size <= 0 || size > 10_000_000) continue;

                int nameStart = markerEnd + 1;
                int nameEnd = nameStart;
                while (nameEnd < zoneData.Length && zoneData[nameEnd] != 0)
                    nameEnd++;

                if (nameEnd <= nameStart) continue;

                string name = Encoding.ASCII.GetString(zoneData, nameStart, nameEnd - nameStart);
                if (!name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) continue;

                int dataOffset = nameEnd + 1;

                files.Add(new RawFileLocation
                {
                    Name = name,
                    HeaderOffset = headerOffset,
                    SizeOffset = sizeOffset,
                    NameOffset = nameStart,
                    DataOffset = dataOffset,
                    DataSize = size
                });

                foundOffsets.Add(headerOffset);
            }
        }

        return files;
    }

    private void UpdateZoneHeaderSizes(byte[] zoneData, int sizeChange)
    {
        // Read the original ZoneSize from the header (big-endian)
        int originalZoneSize = (_originalZone[0] << 24) |
                               (_originalZone[1] << 16) |
                               (_originalZone[2] << 8) |
                               _originalZone[3];

        // Calculate the new ZoneSize by adding the size change
        // This preserves the correct relationship with padding at the end of the zone
        int newZoneSize = originalZoneSize + sizeChange;

        // Write the new ZoneSize (big-endian)
        zoneData[0] = (byte)(newZoneSize >> 24);
        zoneData[1] = (byte)(newZoneSize >> 16);
        zoneData[2] = (byte)(newZoneSize >> 8);
        zoneData[3] = (byte)newZoneSize;

        // Also update BlockSizeLarge (offset 0x18) - raw file data is stored in the LARGE memory block
        // When raw file content sizes change, this allocation hint must also be updated
        int originalBlockSizeLarge = (_originalZone[0x18] << 24) |
                                     (_originalZone[0x19] << 16) |
                                     (_originalZone[0x1A] << 8) |
                                     _originalZone[0x1B];

        int newBlockSizeLarge = originalBlockSizeLarge + sizeChange;

        zoneData[0x18] = (byte)(newBlockSizeLarge >> 24);
        zoneData[0x19] = (byte)(newBlockSizeLarge >> 16);
        zoneData[0x1A] = (byte)(newBlockSizeLarge >> 8);
        zoneData[0x1B] = (byte)newBlockSizeLarge;
    }

    private class RawFileLocation
    {
        public string Name { get; set; } = "";
        public int HeaderOffset { get; set; }
        public int SizeOffset { get; set; }
        public int NameOffset { get; set; }
        public int DataOffset { get; set; }
        public int DataSize { get; set; }
    }
}
