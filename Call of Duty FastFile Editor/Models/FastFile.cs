using Call_of_Duty_FastFile_Editor.GameDefinitions;
using FastFileLib;
using FastFileLib.GameDefinitions;

namespace Call_of_Duty_FastFile_Editor.Models
{
    public enum GameType
    {
        CoD4,
        CoD5,
        MW2,
        Ghosts
    }

    public class FastFile
    {
        public string FfFilePath { get; }
        public string ZoneFilePath { get; }
        public ZoneFile OpenedFastFileZone { get; private set; }
        public FastFileHeader OpenedFastFileHeader { get; }

        /// <summary>
        /// Indicates if this FastFile was loaded from a zone file directly (no original .ff exists).
        /// </summary>
        public bool IsFromZoneFile { get; }

        public string FastFileName => Path.GetFileName(FfFilePath);
        public string FastFileMagic => OpenedFastFileHeader.FastFileMagic;
        public int GameVersion => OpenedFastFileHeader.GameVersion;
        public int FileLength => OpenedFastFileHeader.FileLength;
        public bool IsValid => OpenedFastFileHeader.IsValid;
        public bool IsCod4File => OpenedFastFileHeader.IsCod4File;
        public bool IsCod5File => OpenedFastFileHeader.IsCod5File;
        public bool IsMW2File => OpenedFastFileHeader.IsMW2File;
        public bool IsGhostsFile => OpenedFastFileHeader.IsGhostsFile;
        public bool IsSigned => OpenedFastFileHeader.IsSigned;

        /// <summary>
        /// Indicates if this FastFile is from Xbox 360.
        /// Signed files are typically Xbox 360, EXCEPT for MW2 PC retail which also uses
        /// IWff0100 + LE version. PC and Wii files are detected separately by FastFileInfo, so
        /// exclude them here. Dev-build FFM (version 0xFD) is always Xbox 360 (no PC variant).
        /// </summary>
        public bool IsXbox360 => !IsPC && !IsWii && !IsGhostsFile && (
            OpenedFastFileHeader.IsSigned ||
            OpenedFastFileHeader.GameVersion == FastFileLib.GameDefinitions.MW2Definition.DevBuildVersionValue);

        /// <summary>
        /// Indicates if this FastFile is from a PC version.
        /// PC files use little-endian byte order (unlike PS3/Xbox which use big-endian).
        /// Auto-detected from header, but can be overridden if needed.
        /// </summary>
        public bool IsPC
        {
            get => _isPC ?? OpenedFastFileHeader?.IsPC ?? false;
            set => _isPC = value;
        }
        private bool? _isPC;

        /// <summary>
        /// Indicates if this FastFile is from a Wii version.
        /// </summary>
        public bool IsWii
        {
            get => _isWii ?? OpenedFastFileHeader?.IsWii ?? false;
            set => _isWii = value;
        }
        private bool? _isWii;

        /// <summary>
        /// Gets the platform string for this FastFile.
        /// </summary>
        public string Platform => IsPC ? "PC"
                                : IsWii ? "Wii"
                                : IsGhostsFile ? "PS3"  // Ghosts is signed on PS3 retail; no Xbox 360 samples tested yet
                                : OpenedFastFileHeader.IsSigned ? "Xbox 360"
                                : "PS3";

        /// <summary>
        /// Gets the FastFileLib.GameVersion enum value for this FastFile.
        /// Use this when calling shared library APIs that take a GameVersion parameter.
        /// </summary>
        public FastFileLib.GameVersion GameVersionEnum =>
            IsCod4File   ? FastFileLib.GameVersion.CoD4 :
            IsCod5File   ? FastFileLib.GameVersion.WaW :
            IsMW2File    ? FastFileLib.GameVersion.MW2 :
            IsGhostsFile ? FastFileLib.GameVersion.Ghosts :
                           FastFileLib.GameVersion.Unknown;

        public FastFile(string filePath)
        {
            FfFilePath = filePath
                ?? throw new ArgumentException("File path cannot be null.", nameof(filePath));
            ZoneFilePath = Path.ChangeExtension(filePath, ".zone");
            IsFromZoneFile = false;

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"FastFile not found: {filePath}", filePath);

            // Defer loading .zone until after you decompress it:
            OpenedFastFileZone = new ZoneFile(ZoneFilePath, this);

            OpenedFastFileHeader = new FastFileHeader(filePath);
        }

        /// <summary>
        /// Private constructor for creating a FastFile from a zone file directly.
        /// </summary>
        private FastFile(string zonePath, GameType gameType, bool isFromZone)
        {
            ZoneFilePath = zonePath
                ?? throw new ArgumentException("Zone path cannot be null.", nameof(zonePath));
            FfFilePath = Path.ChangeExtension(zonePath, ".ff");
            IsFromZoneFile = isFromZone;

            if (!File.Exists(zonePath))
                throw new FileNotFoundException($"Zone file not found: {zonePath}", zonePath);

            // Create a virtual header based on game type
            OpenedFastFileHeader = new FastFileHeader(gameType);

            // Auto-detect PC vs console from the zone bytes — `FastFileHeader(GameType)`
            // only knows the game, not the platform. Without this, IsPC stayed false even
            // for PC zones, so the save path used PS3's BE-version + 64KB-block compressor
            // and the engine saw -2097086464 instead of 387 at load time. We sniff ZoneSize
            // endianness + marker layout via FastFileInfo (same helper `ffcli info` uses).
            try { _isPC = FastFileInfo.IsZonePC(zonePath); }
            catch { /* leave as default; user can still re-save if needed */ }

            // Create zone file reference
            OpenedFastFileZone = new ZoneFile(ZoneFilePath, this);
        }

        /// <summary>
        /// Creates a FastFile instance from a zone file directly.
        /// Use this when you have a decompressed zone and want to load/edit it.
        /// </summary>
        /// <param name="zonePath">Path to the .zone file</param>
        /// <param name="gameType">The game type (CoD4, CoD5/WaW, MW2)</param>
        /// <returns>A FastFile instance ready for zone loading</returns>
        public static FastFile FromZoneFile(string zonePath, GameType gameType)
        {
            return new FastFile(zonePath, gameType, isFromZone: true);
        }

        /// <summary>
        /// Detects the game type from a zone file by reading the MemAlloc1 value at offset 0x08.
        /// Uses centralized detection from FastFileLib.
        /// </summary>
        /// <param name="zonePath">Path to the zone file</param>
        /// <returns>Detected game type, or null if detection failed</returns>
        public static GameType? DetectGameTypeFromZone(string zonePath)
        {
            var gameVersion = FastFileInfo.DetectGameFromZone(zonePath);
            return ConvertGameVersion(gameVersion);
        }

        /// <summary>
        /// Converts FastFileLib.GameVersion to Editor GameType.
        /// </summary>
        private static GameType? ConvertGameVersion(FastFileLib.GameVersion gameVersion)
        {
            return gameVersion switch
            {
                FastFileLib.GameVersion.CoD4   => GameType.CoD4,
                FastFileLib.GameVersion.WaW    => GameType.CoD5,
                FastFileLib.GameVersion.MW2    => GameType.MW2,
                FastFileLib.GameVersion.Ghosts => GameType.Ghosts,
                _ => null
            };
        }

        /// <summary>
        /// Detects if a zone file is from a PC version by checking endianness.
        /// Uses centralized detection from FastFileLib.
        /// </summary>
        /// <param name="zonePath">Path to the zone file</param>
        /// <returns>True if the zone appears to be PC (little-endian), false otherwise</returns>
        public static bool DetectPCFromZone(string zonePath)
        {
            return FastFileInfo.IsZonePC(zonePath);
        }

        /// <summary>
        /// Detects if zone data (byte array) is from a PC version by checking endianness.
        /// Uses centralized detection from FastFileLib.
        /// </summary>
        /// <param name="zoneData">The zone file data</param>
        /// <returns>True if the zone appears to be PC (little-endian), false otherwise</returns>
        public static bool DetectPCFromZoneData(byte[] zoneData)
        {
            return FastFileInfo.IsZoneDataPC(zoneData);
        }

        /// <summary>
        /// AFTER you’ve written the .zone to disk, call this to load it.
        /// </summary>
        public void LoadZone()
        {
            OpenedFastFileZone = ZoneFile.Load(ZoneFilePath, this);
        }

        public class FastFileHeader
        {
            public string FastFileMagic { get; private set; }
            public int GameVersion { get; private set; }
            public int FileLength { get; private set; }
            public bool IsValid { get; private set; }
            public bool IsCod4File { get; private set; }
            public bool IsCod5File { get; private set; }
            public bool IsMW2File { get; private set; }
            public bool IsGhostsFile { get; private set; }
            public bool IsSigned { get; private set; }

            /// <summary>
            /// Indicates if this is a PC FastFile (detected by little-endian version).
            /// </summary>
            public bool IsPC { get; private set; }

            /// <summary>
            /// Indicates if this is a Wii FastFile (detected by Wii-specific version).
            /// </summary>
            public bool IsWii { get; private set; }

            public FastFileHeader(string filePath)
            {
                // Single source of truth: delegate to FastFileLib.FastFileInfo so detection
                // logic lives in one place. Previously the editor duplicated the BE-then-LE
                // version-decoding logic and kept getting out of sync with FastFileInfo (the
                // MW2 PC and Wii detection fixes had to be applied twice).
                FastFileInfo info;
                try
                {
                    info = FastFileInfo.FromFile(filePath);
                }
                catch
                {
                    IsValid = false;
                    return;
                }

                FastFileMagic = info.Magic?.TrimEnd('\0') ?? "";
                GameVersion = (int)info.Version;
                FileLength = (int)new FileInfo(filePath).Length;
                IsSigned = info.IsSigned;
                IsPC = info.IsPC;
                IsWii = info.IsWii;

                switch (info.GameVersion)
                {
                    case FastFileLib.GameVersion.CoD4:
                        IsCod4File = true;
                        IsValid = true;
                        break;
                    case FastFileLib.GameVersion.WaW:
                        IsCod5File = true;
                        IsValid = true;
                        break;
                    case FastFileLib.GameVersion.MW2:
                        IsMW2File = true;
                        IsValid = true;
                        break;
                    case FastFileLib.GameVersion.Ghosts:
                        // Read-only support: we can decompress to a zone, but asset-pool
                        // parsing isn't implemented for IW6 yet. The editor will surface
                        // an empty / partial zone view; that's expected until per-asset
                        // parsing lands.
                        IsGhostsFile = true;
                        IsValid = true;
                        break;
                    default:
                        IsValid = false;
                        break;
                }
            }

            /// <summary>
            /// Creates a virtual FastFile header based on game type.
            /// Used when loading from a zone file directly.
            /// </summary>
            public FastFileHeader(GameType gameType)
            {
                FastFileMagic = FastFileConstants.UnsignedHeader;
                FileLength = 0; // Unknown when loading from zone
                IsValid = true;

                switch (gameType)
                {
                    case GameType.CoD4:
                        GameVersion = CoD4Definition.VersionValue;
                        IsCod4File = true;
                        break;
                    case GameType.CoD5:
                        GameVersion = CoD5Definition.VersionValue;
                        IsCod5File = true;
                        break;
                    case GameType.MW2:
                        GameVersion = MW2Definition.VersionValue;
                        IsMW2File = true;
                        break;
                    case GameType.Ghosts:
                        GameVersion = GhostsDefinition.VersionValue;
                        IsGhostsFile = true;
                        break;
                }
            }

        }
    }
}
