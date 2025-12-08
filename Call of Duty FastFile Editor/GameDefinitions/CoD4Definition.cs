namespace Call_of_Duty_FastFile_Editor.GameDefinitions
{
    /// <summary>
    /// Game-specific constants and definitions for Call of Duty 4: Modern Warfare.
    /// </summary>
    public static class CoD4Definition
    {
        public const string GameName = "Call of Duty 4: Modern Warfare";
        public const string ShortName = "CoD4";

        // Version values
        public const int VersionValue = 0x1;           // PS3/Xbox 360
        public const int PCVersionValue = 0x5;         // PC
        public static readonly byte[] VersionBytes = { 0x00, 0x00, 0x00, 0x01 };
        public static readonly byte[] PCVersionBytes = { 0x00, 0x00, 0x00, 0x05 };

        // Memory allocation values (for zone building)
        public static readonly byte[] MemAlloc1 = { 0x00, 0x00, 0x0F, 0x70 };
        public static readonly byte[] MemAlloc2 = { 0x00, 0x00, 0x00, 0x00 };

        // Asset type IDs
        public const byte RawFileAssetType = 0x21;     // 33
        public const byte LocalizeAssetType = 0x18;    // 24
        public const byte StringTableAssetType = 0x22; // 34
        public const byte XAnimAssetType = 0x02;       // 2
    }

    /// <summary>
    /// Asset types for CoD4 zone files.
    /// </summary>
    public enum CoD4AssetType
    {
        physpreset = 0x01,
        xanim = 0x02,
        xmodel = 0x03,
        material = 0x04,
        pixelshader = 0x05,
        vertexshader = 0x06,
        techset = 0x07,
        image = 0x08,
        sound = 0x09,
        sndcurve = 0x0A,
        loaded_sound = 0x0B,
        col_map_sp = 0x0C,
        col_map_mp = 0x0D,
        com_map = 0x0E,
        game_map_sp = 0x0F,
        game_map_mp = 0x10,
        map_ents = 0x11,
        gfx_map = 0x12,
        lightdef = 0x13,
        font = 0x15,
        menufile = 0x16,
        menu = 0x17,
        localize = 0x18,
        weapon = 0x19,
        snddriverglobals = 0x1A,
        fx = 0x1B,
        impactfx = 0x1C,
        rawfile = 0x21,
        stringtable = 0x22
    }
}
