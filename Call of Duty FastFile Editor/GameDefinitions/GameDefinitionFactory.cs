using Call_of_Duty_FastFile_Editor.Models;

namespace Call_of_Duty_FastFile_Editor.GameDefinitions
{
    /// <summary>
    /// Factory for creating game-specific definition instances.
    /// Provides the appropriate IGameDefinition based on the FastFile being opened.
    /// Creates platform-specific instances (PS3 vs Xbox 360 have different asset type IDs).
    /// </summary>
    public static class GameDefinitionFactory
    {
        // Singleton instances for PS3 (default platform)
        private static readonly CoD4GameDefinition _cod4Ps3 = new();
        private static readonly CoD5GameDefinition _cod5Ps3 = new(isXbox360: false);
        private static readonly MW2GameDefinition _mw2Ps3 = new();

        // Singleton instances for Xbox 360 (different asset type IDs)
        private static readonly CoD4GameDefinition _cod4Xbox = new(); // TODO: Add Xbox 360 support for CoD4
        private static readonly CoD5GameDefinition _cod5Xbox = new(isXbox360: true);
        private static readonly MW2GameDefinition _mw2Xbox = new(); // TODO: Add Xbox 360 support for MW2

        /// <summary>
        /// Gets the appropriate game definition for the given FastFile.
        /// Automatically selects PS3 or Xbox 360 variant based on the file's signature.
        /// </summary>
        /// <param name="fastFile">The opened FastFile.</param>
        /// <returns>The game-specific definition for the correct platform.</returns>
        /// <exception cref="NotSupportedException">Thrown when the game is not supported.</exception>
        public static IGameDefinition GetDefinition(FastFile fastFile)
        {
            bool isXbox360 = fastFile.IsSigned;

            if (fastFile.IsCod4File)
                return isXbox360 ? _cod4Xbox : _cod4Ps3;
            if (fastFile.IsCod5File)
                return isXbox360 ? _cod5Xbox : _cod5Ps3;
            if (fastFile.IsMW2File)
                return isXbox360 ? _mw2Xbox : _mw2Ps3;

            throw new NotSupportedException($"Unsupported game version: 0x{fastFile.GameVersion:X}");
        }

        /// <summary>
        /// Gets the appropriate game definition by version value.
        /// Returns PS3 variant by default; use GetDefinitionByVersion(versionValue, isXbox360) for platform-specific.
        /// </summary>
        /// <param name="versionValue">The game version value from the FastFile header.</param>
        /// <returns>The game-specific definition (PS3), or null if not recognized.</returns>
        public static IGameDefinition? GetDefinitionByVersion(int versionValue)
        {
            return GetDefinitionByVersion(versionValue, isXbox360: false);
        }

        /// <summary>
        /// Gets the appropriate game definition by version value and platform.
        /// </summary>
        /// <param name="versionValue">The game version value from the FastFile header.</param>
        /// <param name="isXbox360">True for Xbox 360, false for PS3.</param>
        /// <returns>The game-specific definition for the platform, or null if not recognized.</returns>
        public static IGameDefinition? GetDefinitionByVersion(int versionValue, bool isXbox360)
        {
            // CoD4
            if (versionValue == CoD4Definition.VersionValue || versionValue == CoD4Definition.PCVersionValue)
                return isXbox360 ? _cod4Xbox : _cod4Ps3;

            // CoD5/WaW
            if (versionValue == CoD5Definition.VersionValue)
                return isXbox360 ? _cod5Xbox : _cod5Ps3;

            // MW2
            if (versionValue == MW2Definition.VersionValue || versionValue == MW2Definition.PCVersionValue)
                return isXbox360 ? _mw2Xbox : _mw2Ps3;

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
        /// Gets the CoD5/WaW game definition for Xbox 360.
        /// </summary>
        public static IGameDefinition CoD5Xbox360 => _cod5Xbox;
    }
}
