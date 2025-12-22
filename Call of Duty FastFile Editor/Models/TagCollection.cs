using Call_of_Duty_FastFile_Editor.Services; // for Utilities.ReadStringAtOffset

namespace Call_of_Duty_FastFile_Editor.Models
{
    public class TagCollection
    {
        public List<ZoneAsset_TagEntry> TagEntries { get; set; } = new List<ZoneAsset_TagEntry>();
        public int TagSectionStartOffset { get; set; }
        /// <summary>
        /// The end offset of the tag section, which is the first 0x00 after the last tag.
        /// So this includes the last tag + the 0x00 terminator.
        /// </summary>
        public int TagSectionEndOffset { get; set; }

        /// <summary>
        /// Gets a tag string by its script string index.
        /// Script strings are indexed sequentially starting at 0.
        /// </summary>
        /// <param name="index">The script string index (0-based).</param>
        /// <returns>The tag string, or null if index is out of range.</returns>
        public string? GetTagByIndex(int index)
        {
            if (index < 0 || index >= TagEntries.Count)
                return null;
            return TagEntries[index].Tag;
        }

        /// <summary>
        /// Gets the total number of tags in this collection.
        /// </summary>
        public int Count => TagEntries.Count;
    }

    public class ZoneAsset_TagEntry
    {
        public string Tag { get; set; }
        public int OffsetDec { get; set; }
        public string OffsetHex { get; set; }
    }

    /// <summary>
    /// Parses script strings (tags) from the zone file.
    /// </summary>
    public static class TagOperations
    {
        // Zone header size: XFile (0x00-0x23) + XAssetList (0x24-0x33) = 0x34 bytes
        private const int ZoneHeaderSize = 0x34;

        /// <summary>
        /// Finds tags using known zone structure offsets.
        /// Uses pre-parsed offsets from StructureBasedZoneParser if available.
        /// </summary>
        public static TagCollection? FindTags(ZoneFile zone)
        {
            if (zone?.Data == null || zone.Data.Length < ZoneHeaderSize)
                return null;

            byte[] zoneBytes = zone.Data;

            // Use pre-parsed tag section offsets if available (set by StructureBasedZoneParser)
            int tagSectionStart = zone.TagSectionStartOffset > 0
                ? zone.TagSectionStartOffset
                : ZoneHeaderSize;

            // Tags end at the tag section end or asset pool start
            int tagSectionEnd = zone.TagSectionEndOffset > 0
                ? zone.TagSectionEndOffset
                : (zone.AssetPoolStartOffset > 0
                    ? zone.AssetPoolStartOffset
                    : FindAssetPoolStart(zoneBytes, tagSectionStart));

            if (tagSectionEnd <= tagSectionStart)
                return null;

            var tagEntries = new List<ZoneAsset_TagEntry>();
            int offset = tagSectionStart;

            // Parse null-terminated strings until we reach the asset pool
            while (offset < tagSectionEnd)
            {
                // Skip any null bytes (padding between tags or at start)
                if (zoneBytes[offset] == 0x00)
                {
                    offset++;
                    continue;
                }

                int currentOffset = offset;
                string tag = ReadNullTerminatedString(zoneBytes, offset, tagSectionEnd);

                if (string.IsNullOrEmpty(tag))
                    break;

                // Sanity check - tags shouldn't be too long
                if (tag.Length > 256)
                {
                    // Likely hit corrupted data, stop parsing
                    break;
                }

                tagEntries.Add(new ZoneAsset_TagEntry
                {
                    Tag = tag,
                    OffsetDec = currentOffset,
                    OffsetHex = currentOffset.ToString("X")
                });

                offset += tag.Length + 1; // +1 for null terminator
            }

            // Only update zone offsets if they weren't already set
            if (zone.TagSectionStartOffset == 0)
                zone.TagSectionStartOffset = tagSectionStart;
            if (zone.TagSectionEndOffset == 0)
                zone.TagSectionEndOffset = tagSectionEnd;

            return new TagCollection
            {
                TagEntries = tagEntries,
                TagSectionStartOffset = tagSectionStart,
                TagSectionEndOffset = tagSectionEnd
            };
        }

        /// <summary>
        /// Reads a null-terminated ASCII string from the byte array.
        /// </summary>
        private static string ReadNullTerminatedString(byte[] data, int offset, int maxOffset)
        {
            if (offset >= maxOffset || offset >= data.Length)
                return string.Empty;

            int end = offset;
            while (end < maxOffset && end < data.Length && data[end] != 0x00)
                end++;

            if (end == offset)
                return string.Empty;

            return System.Text.Encoding.ASCII.GetString(data, offset, end - offset);
        }

        /// <summary>
        /// Fallback: finds the asset pool start by looking for either format:
        /// Format A: 00 00 00 XX FF FF FF FF (type first)
        /// Format B: FF FF FF FF 00 00 00 XX (pointer first)
        /// </summary>
        private static int FindAssetPoolStart(byte[] zoneBytes, int startOffset)
        {
            for (int i = startOffset; i <= zoneBytes.Length - 8; i++)
            {
                // Format A: 00 00 00 [type] FF FF FF FF (type first)
                if (zoneBytes[i] == 0x00 && zoneBytes[i + 1] == 0x00 && zoneBytes[i + 2] == 0x00 &&
                    zoneBytes[i + 4] == 0xFF && zoneBytes[i + 5] == 0xFF &&
                    zoneBytes[i + 6] == 0xFF && zoneBytes[i + 7] == 0xFF)
                {
                    return i;
                }

                // Format B: FF FF FF FF 00 00 00 [type] (pointer first)
                if (zoneBytes[i] == 0xFF && zoneBytes[i + 1] == 0xFF &&
                    zoneBytes[i + 2] == 0xFF && zoneBytes[i + 3] == 0xFF &&
                    zoneBytes[i + 4] == 0x00 && zoneBytes[i + 5] == 0x00 &&
                    zoneBytes[i + 6] == 0x00)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Gets the count of tags in a zone (uses header field for accuracy).
        /// </summary>
        public static int GetTagCount(ZoneFile zone)
        {
            // Use the ScriptStringCount from the zone header - it's already parsed and accurate
            return (int)(zone?.ScriptStringCount ?? 0);
        }
    }
}
