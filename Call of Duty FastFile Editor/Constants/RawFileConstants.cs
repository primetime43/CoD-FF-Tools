namespace Call_of_Duty_FastFile_Editor.Constants
{
    public static class RawFileConstants
    {
        // Define the byte patterns to search for.
        // Excluded extensions and why:
        //   .txt / .csv   - too many embedded string references inside other assets (false positives)
        //   .menu         - actually a binary 'menufile' asset, not a text rawfile
        //   .str          - likely localize/stringtable internal reference, not a rawfile
        public static readonly byte[][] FileNamePatterns = new byte[][]
        {
                new byte[] { 0x2E, 0x63, 0x66, 0x67, 0x00 },             // .cfg
                new byte[] { 0x2E, 0x67, 0x73, 0x63, 0x00 },             // .gsc
                new byte[] { 0x2E, 0x61, 0x74, 0x72, 0x00 },             // .atr
                new byte[] { 0x2E, 0x63, 0x73, 0x63, 0x00 },             // .csc
                new byte[] { 0x2E, 0x72, 0x6D, 0x62, 0x00 },             // .rmb
                new byte[] { 0x2E, 0x61, 0x72, 0x65, 0x6E, 0x61, 0x00 }, // .arena
                new byte[] { 0x2E, 0x76, 0x69, 0x73, 0x69, 0x6F, 0x6E, 0x00 } // .vision
        };

        // Individual patterns
        public static readonly byte[] Pattern_Cfg = new byte[] { 0x2E, 0x63, 0x66, 0x67 }; // .cfg
        public static readonly byte[] Pattern_Gsc = new byte[] { 0x2E, 0x67, 0x73, 0x63 }; // .gsc
        public static readonly byte[] Pattern_Atr = new byte[] { 0x2E, 0x61, 0x74, 0x72 }; // .atr
        public static readonly byte[] Pattern_Csc = new byte[] { 0x2E, 0x63, 0x73, 0x63 }; // .csc
        public static readonly byte[] Pattern_Rmb = new byte[] { 0x2E, 0x72, 0x6D, 0x62 }; // .rmb
        public static readonly byte[] Pattern_Arena = new byte[] { 0x2E, 0x61, 0x72, 0x65, 0x6E, 0x61 }; // .arena
        public static readonly byte[] Pattern_Vision = new byte[] { 0x2E, 0x76, 0x69, 0x73, 0x69, 0x6F, 0x6E }; // .vision
        public static readonly byte[] Pattern_Text = new byte[] { 0x2E, 0x74, 0x78, 0x74 }; // .txt

        // Define the file name patterns as plain text. Kept in sync with FileNamePatterns above.
        public static readonly string[] FileNamePatternStrings = new string[]
        {
            ".cfg",
            ".gsc",
            ".atr",
            ".csc",
            ".rmb",
            ".arena",
            ".vision"
        };
    }
}
