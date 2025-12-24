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
    }

    public class ZoneAsset_TagEntry
    {
        public string Tag { get; set; }
        public int OffsetDec { get; set; }
        public string OffsetHex { get; set; }
    }

    // Eventually rewrite this to use the asset pool start offset to get the tags
    public static class TagOperations
    {
        /// <summary>
        /// Finds a large run (16 bytes) of 0xFF in the zone’s byte array.
        /// Returns the start offset of that run, or -1 if not found.
        /// Adjust if your format uses more or fewer 0xFF.
        /// </summary>
        private static int FindLargeFFBlock(byte[] zoneBytes)
        {
            const int runLength = 16;
            for (int i = 0; i <= zoneBytes.Length - runLength; i++)
            {
                bool foundRun = true;
                for (int j = 0; j < runLength; j++)
                {
                    if (zoneBytes[i + j] != 0xFF)
                    {
                        foundRun = false;
                        break;
                    }
                }
                if (foundRun) return i;
            }
            return -1;
        }

        /// <summary>
        /// Reads tags appearing after a large run of 0xFF, ending at the first
        /// occurrence of four consecutive 0x00 bytes (or out of file). Each tag 
        /// is null-terminated ASCII, and we store each tag's offset in both 
        /// decimal and hex.
        /// </summary>
        public static TagCollection? FindTags(ZoneFile zone)
        {
            byte[] zoneBytes = zone.Data;
            int blockStart = FindLargeFFBlock(zoneBytes);
            if (blockStart < 0) return null;

            int offset = blockStart;
            while (offset < zoneBytes.Length && zoneBytes[offset] == 0xFF)
                offset++;

            int tagSectionStart = offset;
            var tagEntries = new List<ZoneAsset_TagEntry>();

            // Keep going until we see asset record pool, not just zeros!
            while (offset + 8 <= zoneBytes.Length)
            {
                // Check if we are at the start of the asset record pool
                if (zoneBytes[offset] == 0x00 && zoneBytes[offset + 1] == 0x00 && zoneBytes[offset + 2] == 0x00 &&
                    zoneBytes[offset + 4] == 0xFF && zoneBytes[offset + 5] == 0xFF &&
                    zoneBytes[offset + 6] == 0xFF && zoneBytes[offset + 7] == 0xFF)
                {
                    // Found asset record pool start
                    break;
                }

                // Skip padding zeros between tags (not the 8-byte asset record pattern)
                if (zoneBytes[offset] == 0x00)
                {
                    offset++;
                    continue;
                }

                int currentOffset = offset;
                string tag = Utilities.ReadStringAtOffset(offset, zone);

                if (string.IsNullOrEmpty(tag) || tag.Length > 128)
                    break;

                tagEntries.Add(new ZoneAsset_TagEntry
                {
                    Tag = tag,
                    OffsetDec = currentOffset,
                    OffsetHex = currentOffset.ToString("X")
                });

                offset += tag.Length + 1;
            }
            int tagSectionEnd = offset;

            zone.TagSectionStartOffset = tagSectionStart;
            zone.TagSectionEndOffset = tagSectionEnd;

            return new TagCollection
            {
                TagEntries = tagEntries,
                TagSectionStartOffset = tagSectionStart,
                TagSectionEndOffset = tagSectionEnd
            };
        }
    }
}
