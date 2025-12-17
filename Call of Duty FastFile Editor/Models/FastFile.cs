using System.Net;
using System.Text;
using Call_of_Duty_FastFile_Editor.Constants;
using Call_of_Duty_FastFile_Editor.GameDefinitions;

namespace Call_of_Duty_FastFile_Editor.Models
{
    public enum GameType
    {
        CoD4,
        CoD5,
        MW2
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
        public bool IsSigned => OpenedFastFileHeader.IsSigned;
        public bool IsXbox360 => OpenedFastFileHeader.IsSigned; // Signed files are Xbox 360
        public string Platform => OpenedFastFileHeader.IsSigned ? "Xbox 360" : "PS3";

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
        /// </summary>
        /// <param name="zonePath">Path to the zone file</param>
        /// <returns>Detected game type, or null if detection failed</returns>
        public static GameType? DetectGameTypeFromZone(string zonePath)
        {
            try
            {
                byte[] header = new byte[12];
                using (var fs = new FileStream(zonePath, FileMode.Open, FileAccess.Read))
                {
                    if (fs.Read(header, 0, 12) < 12)
                        return null;
                }

                // MemAlloc1 is at offset 0x08, big-endian
                uint memAlloc1 = (uint)((header[8] << 24) | (header[9] << 16) | (header[10] << 8) | header[11]);

                return memAlloc1 switch
                {
                    0x000010B0 => GameType.CoD5,  // WaW
                    0x00000F70 => GameType.CoD4,  // CoD4
                    0x000003B4 => GameType.MW2,   // MW2
                    _ => null
                };
            }
            catch
            {
                return null;
            }
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
            public bool IsSigned { get; private set; }

            public FastFileHeader(string filePath)
            {
                using var br = new BinaryReader(new FileStream(filePath, FileMode.Open, FileAccess.Read), Encoding.Default);
                if (br.BaseStream.Length < 12)
                {
                    IsValid = false;
                    return;
                }

                FastFileMagic = new string(br.ReadChars(8)).TrimEnd('\0');
                GameVersion = IPAddress.NetworkToHostOrder(br.ReadInt32());
                FileLength = (int)new FileInfo(filePath).Length;

                // Check if signed (Xbox 360)
                IsSigned = FastFileMagic == FastFileHeaderConstants.SignedFF;

                ValidateHeader();
            }

            /// <summary>
            /// Creates a virtual FastFile header based on game type.
            /// Used when loading from a zone file directly.
            /// </summary>
            public FastFileHeader(GameType gameType)
            {
                FastFileMagic = FastFileHeaderConstants.UnSignedFF;
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
                }
            }

            /// <summary>
            /// Validates the Fast File header.
            /// </summary>
            private void ValidateHeader()
            {
                // Initial validation
                IsValid = false;

                // Check the FastFileMagic and GameVersion to determine validity
                // Accept both unsigned (PS3) and signed (Xbox 360) files
                if (FastFileMagic == FastFileHeaderConstants.UnSignedFF ||
                    FastFileMagic == FastFileHeaderConstants.SignedFF)
                {
                    if (GameVersion == CoD4Definition.VersionValue ||
                        GameVersion == CoD4Definition.PCVersionValue)
                    {
                        IsCod4File = true;
                        IsValid = true;
                    }
                    else if (GameVersion == CoD5Definition.VersionValue ||
                             GameVersion == CoD5Definition.PCVersionValue)
                    {
                        IsCod5File = true;
                        IsValid = true;
                    }
                    else if (GameVersion == MW2Definition.VersionValue ||
                             GameVersion == MW2Definition.PCVersionValue)
                    {
                        IsMW2File = true;
                        IsValid = true;
                    }
                }
            }
        }
    }
}
