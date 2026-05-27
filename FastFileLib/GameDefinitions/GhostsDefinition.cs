namespace FastFileLib.GameDefinitions
{
    /// <summary>
    /// Game-specific constants and definitions for Call of Duty: Ghosts (IW6).
    ///
    /// Read-only support so far: PS3 retail .ff files (both patch FFs and base FFs)
    /// can be decompressed to a zone, and the asset pool can be walked. Per-asset
    /// content parsing (rawfile bodies, weapon structs, etc.) is not implemented.
    ///
    /// See docs/Ghosts_FastFile_Format.md for the format reference and
    /// docs/Ghosts_Extraction_Howto.md for the extraction algorithm.
    /// </summary>
    public static class GhostsDefinition
    {
        public const string GameName = "Call of Duty: Ghosts";
        public const string ShortName = "Ghosts";

        /// <summary>Ghosts FastFile version stamp (big-endian). 558 decimal.</summary>
        public const int VersionValue = 0x22E;

        /// <summary>Inner-magic that anchors decompression. Capital 'S' distinguishes
        /// Ghosts from MW3 (which uses lowercase IWffs100).</summary>
        public const string InnerMagic = "IWffS100";

        /// <summary>
        /// Offset from IWffS100 to the start of the compressed deflate-block stream.
        /// IWffS100 + DB_AuthHeader (8 KB) + padding (48 B) + LO region (112 KB) =
        /// exactly 128 KB. The same rule holds for both patch FFs (IWffS100 at file
        /// offset 0x24) and base FFs (IWffS100 deeper in, after a 12 KB index table).
        /// </summary>
        public const int DeflateBlockStartOffsetFromInnerMagic = 0x20000;

        /// <summary>Each block decompresses to exactly 64 KB. The 2-byte big-endian
        /// header in front of every block is the *compressed* size only.</summary>
        public const int BlockUncompressedSize = 0x10000;
    }

    /// <summary>
    /// Asset type IDs for IW6 (Ghosts) on PS3. Each platform (Xbox 360 / PS3 / Wii-U /
    /// PC) uses a slightly different numbering because some types are platform-only
    /// (e.g. computeshader / hullshader / domainshader / vertexdecl exist only on PC).
    /// This enum is PS3-specific; other platforms haven't been wired up yet.
    /// </summary>
    public enum GhostsAssetTypePS3
    {
        physpreset           = 0x00,
        phys_collmap         = 0x01,
        xanim                = 0x02,
        xmodelsurfs          = 0x03,
        xmodel               = 0x04,
        material             = 0x05,
        vertexshader         = 0x06,  // PS3/Xbox 360/Wii-U only
        pixelshader          = 0x07,
        techset              = 0x08,
        image                = 0x09,
        sound                = 0x0A,
        sndcurve             = 0x0B,
        lpfcurve             = 0x0C,
        reverbsendcurve      = 0x0D,
        loaded_sound         = 0x0E,
        col_map              = 0x0F,  // col_map_sp / col_map_mp share this slot
        com_map              = 0x10,
        glass_map            = 0x11,
        aipaths              = 0x12,
        vehicle_track        = 0x13,
        map_ents             = 0x14,
        fx_map               = 0x15,
        gfx_map              = 0x16,
        lightdef             = 0x17,
        ui_map               = 0x18,
        font                 = 0x19,
        menufile             = 0x1A,
        menu                 = 0x1B,
        animclass            = 0x1C,
        localize             = 0x1D,
        attachment           = 0x1E,
        weapon               = 0x1F,
        snddriverglobals     = 0x20,
        fx                   = 0x21,
        impactfx             = 0x22,
        surfacefx            = 0x23,
        aitype               = 0x24,
        mptype               = 0x25,
        character            = 0x26,
        xmodelalias          = 0x27,
        rawfile              = 0x28,
        scriptfile           = 0x29,
        stringtable          = 0x2A,
        leaderboarddef       = 0x2B,
        structureddatadef    = 0x2C,
        tracer               = 0x2D,
        vehicle              = 0x2E,
        addon_map_ents       = 0x2F,
        netconststrings      = 0x30,
        reverbpreset         = 0x31,
        luafile              = 0x32,
        scriptable           = 0x33,
        equipsndtable        = 0x34,
        dopplerpreset        = 0x35,
    }
}
