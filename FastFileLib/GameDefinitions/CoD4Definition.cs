namespace FastFileLib.GameDefinitions
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
        public const int WiiVersionValue = 0x1A2;      // Wii (418)
        public static readonly byte[] VersionBytes = { 0x00, 0x00, 0x00, 0x01 };
        public static readonly byte[] PCVersionBytes = { 0x00, 0x00, 0x00, 0x05 };
        public static readonly byte[] WiiVersionBytes = { 0x00, 0x00, 0x01, 0xA2 };

        // Memory allocation values (for zone building)
        public static readonly byte[] MemAlloc1 = { 0x00, 0x00, 0x0F, 0x70 };
        public static readonly byte[] MemAlloc2 = { 0x00, 0x00, 0x00, 0x00 };

        // Memory allocation as uint values
        public const uint MemAlloc1Value = 0x0F70;
        public const uint MemAlloc2Value = 0x0;
    }

    /// <summary>
    /// Asset types for CoD4 zone files (PS3).
    /// PS3 has both pixelshader (0x05) and vertexshader (0x06), so all types after are shifted +1 from Xbox 360.
    /// Reference: https://codresearch.dev/index.php/Category:Assets
    /// </summary>
    public enum CoD4AssetTypePS3
    {
        xmodelpieces = 0x00,
        physpreset = 0x01,
        xanim = 0x02,
        xmodel = 0x03,
        material = 0x04,
        pixelshader = 0x05,
        vertexshader = 0x06,  // PS3 only - not present on Xbox 360 or PC
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
        stringtable = 0x22
    }

    /// <summary>
    /// Asset types for CoD4 zone files (Xbox 360).
    /// Xbox 360 has pixelshader (0x05) but NO vertexshader, so all types >= 0x06 are shifted -1 from PS3.
    /// Reference: https://codresearch.dev/index.php/Category:Assets
    /// </summary>
    public enum CoD4AssetTypeXbox360
    {
        xmodelpieces = 0x00,
        physpreset = 0x01,
        xanim = 0x02,
        xmodel = 0x03,
        material = 0x04,
        pixelshader = 0x05,
        // No vertexshader on Xbox 360
        techset = 0x06,
        image = 0x07,
        sound = 0x08,
        sndcurve = 0x09,
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
        stringtable = 0x21
    }

    /// <summary>
    /// Asset types for CoD4 zone files (PC).
    /// PC has neither pixelshader nor vertexshader, so all types >= 0x05 are shifted -2 from PS3.
    /// Reference: https://codresearch.dev/index.php/Category:Assets
    /// </summary>
    public enum CoD4AssetTypePC
    {
        xmodelpieces = 0x00,
        physpreset = 0x01,
        xanim = 0x02,
        xmodel = 0x03,
        material = 0x04,
        // No pixelshader on PC
        // No vertexshader on PC
        techset = 0x05,
        image = 0x06,
        sound = 0x07,
        sndcurve = 0x08,
        loaded_sound = 0x09,
        col_map_sp = 0x0A,
        col_map_mp = 0x0B,
        com_map = 0x0C,
        game_map_sp = 0x0D,
        game_map_mp = 0x0E,
        map_ents = 0x0F,
        gfx_map = 0x10,
        lightdef = 0x11,
        ui_map = 0x12,
        font = 0x13,
        menufile = 0x14,
        menu = 0x15,
        localize = 0x16,
        weapon = 0x17,
        snddriverglobals = 0x18,
        fx = 0x19,
        impactfx = 0x1A,
        aitype = 0x1B,
        mptype = 0x1C,
        character = 0x1D,
        xmodelalias = 0x1E,
        rawfile = 0x1F,
        stringtable = 0x20
    }
}
