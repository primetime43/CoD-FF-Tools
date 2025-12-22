namespace FastFileLib.GameDefinitions
{
    /// <summary>
    /// Game-specific constants and definitions for Call of Duty: Modern Warfare 2.
    /// </summary>
    public static class MW2Definition
    {
        public const string GameName = "Call of Duty: Modern Warfare 2";
        public const string ShortName = "MW2";

        // Version values
        public const int VersionValue = 0x10D;         // PS3/Xbox 360 (269)
        public const int PCVersionValue = 0x114;       // PC (276)
        public const int DevBuildVersionValue = 0xFD;   // Pre-release dev build (253)
        public static readonly byte[] VersionBytes = { 0x00, 0x00, 0x01, 0x0D };
        public static readonly byte[] PCVersionBytes = { 0x00, 0x00, 0x01, 0x14 };

        // Memory allocation values (for zone building)
        public static readonly byte[] MemAlloc1 = { 0x00, 0x00, 0x03, 0xB4 };
        public static readonly byte[] MemAlloc2 = { 0x00, 0x00, 0x10, 0x00 };

        // Memory allocation as uint values
        public const uint MemAlloc1Value = 0x03B4;
        public const uint MemAlloc2Value = 0x1000;

        // MW2-specific: Extended header info
        public const int ExtendedHeaderEntrySize = 0x14;  // 20 bytes per entry on PS3

        /// <summary>
        /// MW2 uses zlib-wrapped deflate compression (has 0x78 header)
        /// instead of raw deflate like CoD4/WaW.
        /// </summary>
        public const bool UsesZlibCompression = true;
    }

    /// <summary>
    /// Asset types for MW2 zone files (PS3).
    /// PS3 includes vertexshader at 0x07, so types after are shifted +1 from Xbox 360.
    /// Reference: https://codresearch.dev/index.php/Category:Assets
    /// </summary>
    public enum MW2AssetTypePS3
    {
        physpreset = 0x00,
        phys_collmap = 0x01,
        xanim = 0x02,
        xmodelsurfs = 0x03,
        xmodel = 0x04,
        material = 0x05,
        pixelshader = 0x06,
        vertexshader = 0x07,  // PS3 only - not on Xbox 360
        techset = 0x08,
        image = 0x09,
        sound = 0x0A,
        sndcurve = 0x0B,
        loaded_sound = 0x0C,
        col_map_sp = 0x0D,
        col_map_mp = 0x0E,
        com_map = 0x0F,
        game_map_sp = 0x10,
        game_map_mp = 0x11,
        map_ents = 0x12,
        fx_map = 0x13,
        gfx_map = 0x14,
        lightdef = 0x15,
        ui_map = 0x16,
        font = 0x17,
        menufile = 0x18,
        menu = 0x19,
        localize = 0x1A,
        weapon = 0x1B,
        snddriverglobals = 0x1C,
        fx = 0x1D,
        impactfx = 0x1E,
        aitype = 0x1F,
        mptype = 0x20,
        character = 0x21,
        xmodelalias = 0x22,
        rawfile = 0x23,
        stringtable = 0x24,
        leaderboarddef = 0x25,
        structureddatadef = 0x26,
        tracer = 0x27,
        vehicle = 0x28,
        addon_map_ents = 0x29
    }

    /// <summary>
    /// Asset types for MW2 zone files (Xbox 360).
    /// Xbox 360 does NOT have vertexshader, so all types >= 0x07 are shifted by -1 from PS3.
    /// Reference: https://codresearch.dev/index.php/Category:Assets
    /// </summary>
    public enum MW2AssetTypeXbox360
    {
        physpreset = 0x00,
        phys_collmap = 0x01,
        xanim = 0x02,
        xmodelsurfs = 0x03,
        xmodel = 0x04,
        material = 0x05,
        pixelshader = 0x06,
        // No vertexshader on Xbox 360
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
        fx_map = 0x12,
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
        leaderboarddef = 0x24,
        structureddatadef = 0x25,
        tracer = 0x26,
        vehicle = 0x27,
        addon_map_ents = 0x28
    }

    /// <summary>
    /// Asset types for MW2 zone files (PC).
    /// PC has both vertexshader (0x07) and vertexdecl (0x08), so types after are shifted +2 from Xbox 360.
    /// Reference: https://codresearch.dev/index.php/Category:Assets
    /// </summary>
    public enum MW2AssetTypePC
    {
        physpreset = 0x00,
        phys_collmap = 0x01,
        xanim = 0x02,
        xmodelsurfs = 0x03,
        xmodel = 0x04,
        material = 0x05,
        pixelshader = 0x06,
        vertexshader = 0x07,
        vertexdecl = 0x08,  // PC only
        techset = 0x09,
        image = 0x0A,
        sound = 0x0B,
        sndcurve = 0x0C,
        loaded_sound = 0x0D,
        col_map_sp = 0x0E,
        col_map_mp = 0x0F,
        com_map = 0x10,
        game_map_sp = 0x11,
        game_map_mp = 0x12,
        map_ents = 0x13,
        fx_map = 0x14,
        gfx_map = 0x15,
        lightdef = 0x16,
        ui_map = 0x17,
        font = 0x18,
        menufile = 0x19,
        menu = 0x1A,
        localize = 0x1B,
        weapon = 0x1C,
        snddriverglobals = 0x1D,
        fx = 0x1E,
        impactfx = 0x1F,
        aitype = 0x20,
        mptype = 0x21,
        character = 0x22,
        xmodelalias = 0x23,
        rawfile = 0x24,
        stringtable = 0x25,
        leaderboarddef = 0x26,
        structureddatadef = 0x27,
        tracer = 0x28,
        vehicle = 0x29,
        addon_map_ents = 0x2A
    }

}
