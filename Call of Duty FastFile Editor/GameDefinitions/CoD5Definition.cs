namespace Call_of_Duty_FastFile_Editor.GameDefinitions
{
    /// <summary>
    /// Game-specific constants and definitions for Call of Duty: World at War.
    /// </summary>
    public static class CoD5Definition
    {
        public const string GameName = "Call of Duty: World at War";
        public const string ShortName = "WaW";

        // Version values
        public const int VersionValue = 0x183;         // PS3/Xbox 360/PC
        public const int PCVersionValue = 0x183;       // Same as console
        public static readonly byte[] VersionBytes = { 0x00, 0x00, 0x01, 0x83 };

        // Memory allocation values (for zone building)
        public static readonly byte[] MemAlloc1 = { 0x00, 0x00, 0x10, 0xB0 };
        public static readonly byte[] MemAlloc2 = { 0x00, 0x05, 0xF8, 0xF0 };

        // Asset type IDs
        public const byte RawFileAssetType = 0x22;     // 34
        public const byte LocalizeAssetType = 0x19;    // 25
        public const byte StringTableAssetType = 0x23; // 35
        public const byte MenuFileAssetType = 0x17;    // 23
        public const byte MaterialAssetType = 0x06;    // 6
        public const byte TechSetAssetType = 0x09;     // 9
        public const byte XAnimAssetType = 0x04;       // 4
        public const byte WeaponAssetType = 0x1A;      // 26
        public const byte ImageAssetType = 0x0A;       // 10
    }

    /// <summary>
    /// Asset types for CoD5/WaW zone files.
    /// </summary>
    public enum CoD5AssetType
    {
        physpreset = 0x01,
        physconstraints = 0x02,
        destructibledef = 0x03,
        xanim = 0x04,
        xmodel = 0x05,
        material = 0x06,
        pixelshader = 0x07,
        vertexshader = 0x08,
        techset = 0x09,
        image = 0x0A,
        sound = 0x0B,
        loaded_sound = 0x0C,
        col_map_sp = 0x0D,
        col_map_mp = 0x0E,
        com_map = 0x0F,
        game_map_sp = 0x10,
        game_map_mp = 0x11,
        map_ents = 0x12,
        gfx_map = 0x13,
        lightdef = 0x14,
        ui_map = 0x15,
        font = 0x16,
        menufile = 0x17,
        menu = 0x18,
        localize = 0x19,
        weapon = 0x1A,
        snddriverglobals = 0x1B,
        fx = 0x1C,
        impactfx = 0x1D,
        aitype = 0x1E,
        mptype = 0x1F,
        character = 0x20,
        xmodelalias = 0x21,
        rawfile = 0x22,
        stringtable = 0x23,
        packindex = 0x24
    }
}
