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

        // Xbox 360 memory allocation values (different from PS3)
        public static readonly byte[] Xbox360MemAlloc1 = { 0x00, 0x00, 0x0A, 0x90 };
        public static readonly byte[] Xbox360MemAlloc2 = { 0x00, 0x00, 0x00, 0x00 };

        // PC memory allocation values (little-endian) - same numeric value as PS3 but LE byte order
        // PC zone files use little-endian byte order for all multi-byte values
        public static readonly byte[] PCMemAlloc1 = { 0xB0, 0x10, 0x00, 0x00 };  // 0x10B0 in LE
        public static readonly byte[] PCMemAlloc2 = { 0xF0, 0xF8, 0x05, 0x00 };  // 0x05F8F0 in LE

        // PC MemAlloc1 value when read as little-endian uint32
        public const uint PCMemAlloc1ValueLE = 0x000010B0;
    }

    /// <summary>
    /// Asset types for CoD5/WaW zone files (PS3).
    /// PS3 includes vertexshader at 0x08, so all types after are shifted +1 from Xbox 360.
    /// </summary>
    public enum CoD5AssetTypePS3
    {
        xmodelpieces = 0x00,
        physpreset = 0x01,
        physconstraints = 0x02,
        destructibledef = 0x03,
        xanim = 0x04,
        xmodel = 0x05,
        material = 0x06,
        pixelshader = 0x07,
        vertexshader = 0x08,  // PS3 only - not present on Xbox 360
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

    /// <summary>
    /// Asset types for CoD5/WaW zone files (Xbox 360).
    /// Xbox 360 does NOT have vertexshader, so all types >= 0x08 are shifted by -1 from PS3.
    /// </summary>
    public enum CoD5AssetTypeXbox360
    {
        xmodelpieces = 0x00,
        physpreset = 0x01,
        physconstraints = 0x02,
        destructibledef = 0x03,
        xanim = 0x04,
        xmodel = 0x05,
        material = 0x06,
        pixelshader = 0x07,
        // No vertexshader on Xbox 360
        techset = 0x08,
        image = 0x09,
        sound = 0x0A,
        loaded_sound = 0x0B,
        col_map_sp = 0x0C,
        col_map_mp = 0x0D,
        com_map = 0x0E,
        game_map_sp = 0x0F,
        game_map_mp = 0x10,
        map_ents = 0x11,
        gfx_map = 0x12,
        lightdef = 0x13,
        ui_map = 0x14,
        font = 0x15,
        menufile = 0x16,
        menu = 0x17,
        localize = 0x18,
        weapon = 0x19,
        snddriverglobals = 0x1A,
        fx = 0x1B,
        impactfx = 0x1C,
        aitype = 0x1D,
        mptype = 0x1E,
        character = 0x1F,
        xmodelalias = 0x20,
        rawfile = 0x21,
        stringtable = 0x22,
        packindex = 0x23
    }

    /// <summary>
    /// Asset types for CoD5/WaW zone files (PC).
    /// PC has neither pixelshader nor vertexshader, so all types >= 0x07 are shifted by -2 from PS3.
    /// </summary>
    public enum CoD5AssetTypePC
    {
        xmodelpieces = 0x00,
        physpreset = 0x01,
        physconstraints = 0x02,
        destructibledef = 0x03,
        xanim = 0x04,
        xmodel = 0x05,
        material = 0x06,
        // No pixelshader on PC
        // No vertexshader on PC
        techset = 0x07,
        image = 0x08,
        sound = 0x09,
        loaded_sound = 0x0A,
        col_map_sp = 0x0B,
        col_map_mp = 0x0C,
        com_map = 0x0D,
        game_map_sp = 0x0E,
        game_map_mp = 0x0F,
        map_ents = 0x10,
        gfx_map = 0x11,
        lightdef = 0x12,
        ui_map = 0x13,
        font = 0x14,
        menufile = 0x15,
        menu = 0x16,
        localize = 0x17,
        weapon = 0x18,
        snddriverglobals = 0x19,
        fx = 0x1A,
        impactfx = 0x1B,
        aitype = 0x1C,
        mptype = 0x1D,
        character = 0x1E,
        xmodelalias = 0x1F,
        rawfile = 0x20,
        stringtable = 0x21,
        packindex = 0x22
    }
}
