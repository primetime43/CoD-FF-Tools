using Call_of_Duty_FastFile_Editor.Models;
using FastFileLib.GameDefinitions;

namespace Call_of_Duty_FastFile_Editor.GameDefinitions
{
    /// <summary>
    /// Represents the target platform for FastFile processing.
    /// </summary>
    public enum FastFilePlatform
    {
        PS3,
        Xbox360,
        PC,
        Wii
    }

    /// <summary>
    /// Factory for creating game-specific definition instances.
    /// Provides the appropriate IGameDefinition based on the FastFile being opened.
    /// Creates platform-specific instances (PS3, Xbox 360, PC have different asset type IDs and byte orders).
    /// </summary>
    public static class GameDefinitionFactory
    {
        // Singleton instances for PS3 (default platform)
        private static readonly CoD4GameDefinition _cod4Ps3 = new();
        private static readonly CoD5GameDefinition _cod5Ps3 = new(isXbox360: false);
        private static readonly MW2GameDefinition _mw2Ps3 = new(isXbox360: false, isPC: false);

        // Singleton instances for Xbox 360 (different asset type IDs)
        private static readonly CoD4Xbox360GameDefinition _cod4Xbox = new();
        private static readonly CoD5GameDefinition _cod5Xbox = new(isXbox360: true);
        private static readonly MW2GameDefinition _mw2Xbox = new(isXbox360: true, isPC: false);

        // Singleton instances for PC (little-endian, different asset type IDs)
        private static readonly CoD4PCGameDefinition _cod4PC = new();
        private static readonly CoD5PCGameDefinition _cod5PC = new();
        private static readonly MW2GameDefinition _mw2PC = new(isXbox360: false, isPC: true);

        // Wii: big-endian (PowerPC) with 56-byte zone header.
        //   - CoD5 Wii uses the WaW PC asset type enum (no shader slots).
        //   - CoD4 Wii (Reflex) uses the CoD4 Xbox 360 asset type enum (drops vertexshader,
        //     keeps pixelshader). Asymmetric vs CoD5 Wii — IW and Treyarch made different
        //     enum-cleanup choices for the Wii port.
        private static readonly CoD5WiiGameDefinition _cod5Wii = new();
        private static readonly CoD4WiiGameDefinition _cod4Wii = new();

        // Ghosts (IW6) — PS3 only. Pool-listing only; per-asset content parsers
        // aren't implemented yet (see GhostsGameDefinition).
        private static readonly GhostsGameDefinition _ghostsPs3 = new();

        /// <summary>
        /// Gets the appropriate game definition for the given FastFile.
        /// Automatically selects PS3 or Xbox 360 variant based on the file's signature.
        /// For PC detection, use GetDefinition(FastFile, FastFilePlatform) overload.
        /// </summary>
        /// <param name="fastFile">The opened FastFile.</param>
        /// <returns>The game-specific definition for the correct platform.</returns>
        /// <exception cref="NotSupportedException">Thrown when the game is not supported.</exception>
        public static IGameDefinition GetDefinition(FastFile fastFile)
        {
            bool isXbox360 = fastFile.IsSigned;

            // Wii first - it's big-endian like PS3 but uses PC-style asset type IDs and a
            // 56-byte zone header. Detected via FastFile.IsWii (set when the version bytes
            // match a known Wii version, e.g. 0x19B for WaW Wii).
            if (fastFile.IsWii)
            {
                if (fastFile.IsCod5File)
                    return _cod5Wii;
                if (fastFile.IsCod4File)
                    return _cod4Wii;
            }

            // Check if PC platform was explicitly set
            if (fastFile.IsPC)
            {
                if (fastFile.IsCod4File)
                    return _cod4PC;
                if (fastFile.IsCod5File)
                    return _cod5PC;
                if (fastFile.IsMW2File)
                    return _mw2PC;
                // Fall through to PS3 for unsupported PC games
            }

            if (fastFile.IsCod4File)
                return isXbox360 ? _cod4Xbox : _cod4Ps3;
            if (fastFile.IsCod5File)
                return isXbox360 ? _cod5Xbox : _cod5Ps3;
            if (fastFile.IsMW2File)
                return isXbox360 ? _mw2Xbox : _mw2Ps3;
            if (fastFile.IsGhostsFile)
                return _ghostsPs3;  // PS3 only for now

            throw new NotSupportedException($"Unsupported game version: 0x{fastFile.GameVersion:X}");
        }

        /// <summary>
        /// Gets the appropriate game definition for the given FastFile with explicit platform selection.
        /// Use this overload when you need to specify the platform (e.g., for PC files).
        /// </summary>
        /// <param name="fastFile">The opened FastFile.</param>
        /// <param name="platform">The target platform.</param>
        /// <returns>The game-specific definition for the specified platform.</returns>
        /// <exception cref="NotSupportedException">Thrown when the game/platform combination is not supported.</exception>
        public static IGameDefinition GetDefinition(FastFile fastFile, FastFilePlatform platform)
        {
            if (fastFile.IsCod4File)
            {
                return platform switch
                {
                    FastFilePlatform.Xbox360 => _cod4Xbox,
                    FastFilePlatform.PC => _cod4PC,
                    FastFilePlatform.Wii => _cod4Wii,
                    _ => _cod4Ps3
                };
            }

            if (fastFile.IsCod5File)
            {
                return platform switch
                {
                    FastFilePlatform.Xbox360 => _cod5Xbox,
                    FastFilePlatform.PC => _cod5PC,
                    FastFilePlatform.Wii => _cod5Wii,
                    _ => _cod5Ps3
                };
            }

            if (fastFile.IsMW2File)
            {
                return platform switch
                {
                    FastFilePlatform.Xbox360 => _mw2Xbox,
                    FastFilePlatform.PC => _mw2PC,
                    _ => _mw2Ps3
                };
            }

            throw new NotSupportedException($"Unsupported game version: 0x{fastFile.GameVersion:X}");
        }

        /// <summary>
        /// Gets the appropriate game definition by version value.
        /// Returns PS3 variant by default; use GetDefinitionByVersion(versionValue, platform) for platform-specific.
        /// </summary>
        /// <param name="versionValue">The game version value from the FastFile header.</param>
        /// <returns>The game-specific definition (PS3), or null if not recognized.</returns>
        public static IGameDefinition? GetDefinitionByVersion(int versionValue)
        {
            return GetDefinitionByVersion(versionValue, FastFilePlatform.PS3);
        }

        /// <summary>
        /// Gets the appropriate game definition by version value and platform (legacy overload).
        /// </summary>
        /// <param name="versionValue">The game version value from the FastFile header.</param>
        /// <param name="isXbox360">True for Xbox 360, false for PS3.</param>
        /// <returns>The game-specific definition for the platform, or null if not recognized.</returns>
        public static IGameDefinition? GetDefinitionByVersion(int versionValue, bool isXbox360)
        {
            return GetDefinitionByVersion(versionValue, isXbox360 ? FastFilePlatform.Xbox360 : FastFilePlatform.PS3);
        }

        /// <summary>
        /// Gets the appropriate game definition by version value and platform.
        /// </summary>
        /// <param name="versionValue">The game version value from the FastFile header.</param>
        /// <param name="platform">The target platform.</param>
        /// <returns>The game-specific definition for the platform, or null if not recognized.</returns>
        public static IGameDefinition? GetDefinitionByVersion(int versionValue, FastFilePlatform platform)
        {
            // CoD4
            if (versionValue == CoD4Definition.VersionValue ||
                versionValue == CoD4Definition.PCVersionValue ||
                versionValue == CoD4Definition.WiiVersionValue)
            {
                return platform switch
                {
                    FastFilePlatform.Xbox360 => _cod4Xbox,
                    FastFilePlatform.PC => _cod4PC,
                    FastFilePlatform.Wii => _cod4Wii,
                    _ => _cod4Ps3
                };
            }

            // CoD5/WaW
            if (versionValue == CoD5Definition.VersionValue ||
                versionValue == CoD5Definition.WiiVersionValue)
            {
                return platform switch
                {
                    FastFilePlatform.Xbox360 => _cod5Xbox,
                    FastFilePlatform.PC => _cod5PC,
                    FastFilePlatform.Wii => _cod5Wii,
                    _ => _cod5Ps3
                };
            }

            // MW2
            if (versionValue == MW2Definition.VersionValue ||
                versionValue == MW2Definition.PCVersionValue ||
                versionValue == MW2Definition.DevBuildVersionValue)
            {
                return platform switch
                {
                    FastFilePlatform.Xbox360 => _mw2Xbox,
                    FastFilePlatform.PC => _mw2PC,
                    _ => _mw2Ps3
                };
            }

            return null;
        }

        /// <summary>
        /// Checks if a game version is supported.
        /// </summary>
        public static bool IsSupported(int versionValue)
        {
            return GetDefinitionByVersion(versionValue) != null;
        }

        /// <summary>
        /// Gets the CoD4 game definition (PS3).
        /// </summary>
        public static IGameDefinition CoD4 => _cod4Ps3;

        /// <summary>
        /// Gets the CoD5/WaW game definition (PS3).
        /// </summary>
        public static IGameDefinition CoD5 => _cod5Ps3;

        /// <summary>
        /// Gets the MW2 game definition (PS3).
        /// </summary>
        public static IGameDefinition MW2 => _mw2Ps3;

        /// <summary>
        /// Gets the CoD4 game definition for Xbox 360.
        /// </summary>
        public static IGameDefinition CoD4Xbox360 => _cod4Xbox;

        /// <summary>
        /// Gets the CoD5/WaW game definition for Xbox 360.
        /// </summary>
        public static IGameDefinition CoD5Xbox360 => _cod5Xbox;

        /// <summary>
        /// Gets the CoD4 game definition for PC.
        /// </summary>
        public static IGameDefinition CoD4PC => _cod4PC;

        /// <summary>
        /// Gets the CoD5/WaW game definition for PC.
        /// </summary>
        public static IGameDefinition CoD5PC => _cod5PC;

        /// <summary>
        /// Gets the MW2 game definition for Xbox 360.
        /// </summary>
        public static IGameDefinition MW2Xbox360 => _mw2Xbox;

        /// <summary>
        /// Gets the MW2 game definition for PC.
        /// </summary>
        public static IGameDefinition MW2PC => _mw2PC;
    }
}
