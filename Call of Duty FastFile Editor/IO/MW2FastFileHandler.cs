using Call_of_Duty_FastFile_Editor.GameDefinitions;
using Call_of_Duty_FastFile_Editor.Models;
using FastFileLib;
using FastFileLib.GameDefinitions;
using System.Text;

namespace Call_of_Duty_FastFile_Editor.IO
{
    /// <summary>
    /// Handler for MW2 FastFiles. MW2 has an extended header after the version bytes.
    /// </summary>
    public class MW2FastFileHandler : FastFileHandlerBase
    {
        protected override byte[] HeaderBytes => FastFileConstants.UnsignedHeaderBytes;
        protected override byte[] VersionBytes => MW2Definition.VersionBytes;

        /// <summary>
        /// Decompresses MW2 FastFile, handling the extended header.
        /// For Xbox 360 signed files or dev build FFM files, uses FastFileProcessor.
        /// </summary>
        public override void Decompress(string inputFilePath, string outputFilePath)
        {
            // Check if this is a signed Xbox 360 file or dev build FFM - if so, use FastFileProcessor
            var fileInfo = FastFileInfo.FromFile(inputFilePath);
            if (fileInfo.IsSigned || fileInfo.Version == MW2Definition.DevBuildVersionValue)
            {
                // Xbox 360 signed files have XBlock structure with hash blocks
                // Dev build FFM files have a large metadata header before compressed data
                // Use FastFileProcessor which handles these formats
                FastFileProcessor.Decompress(inputFilePath, outputFilePath);
                return;
            }

            // PS3/unsigned format: 2-byte block lengths
            using var binaryReader = new BinaryReader(new FileStream(inputFilePath, FileMode.Open, FileAccess.Read), Encoding.Default);
            using var binaryWriter = new BinaryWriter(new FileStream(outputFilePath, FileMode.Create, FileAccess.Write), Encoding.Default);

            // Skip standard header (8 magic + 4 version = 12 bytes)
            binaryReader.BaseStream.Position = HeaderBytes.Length + VersionBytes.Length;

            // Skip MW2 extended header
            SkipExtendedHeader(binaryReader);

            try
            {
                for (int i = 1; i < 5000; i++)
                {
                    byte[] array = binaryReader.ReadBytes(2);
                    if (array.Length < 2) break;

                    string text = BitConverter.ToString(array).Replace("-", "");
                    int count = int.Parse(text, System.Globalization.NumberStyles.AllowHexSpecifier);
                    if (count == 0 || count == 1) break;

                    byte[] compressedData = binaryReader.ReadBytes(count);
                    byte[] decompressedData = DecompressFF(compressedData);
                    binaryWriter.Write(decompressedData);
                }
            }
            catch (Exception ex)
            {
                if (!(ex is FormatException))
                    throw;
            }
        }

        /// <summary>
        /// Recompresses a zone file back to MW2 FastFile format.
        /// Uses FastFileLib.FastFileProcessor.CompressMW2 which handles platform differences:
        /// - PS3 uses block compression (64KB blocks with 2-byte length markers)
        /// - Xbox 360/PC uses single zlib stream compression (no block structure)
        /// </summary>
        public override void Recompress(string ffFilePath, string zoneFilePath, FastFile openedFastFile)
        {
            // Build version bytes from original file's version
            int originalVersion = openedFastFile.GameVersion;
            byte[] versionBytes = new byte[4];
            versionBytes[0] = (byte)((originalVersion >> 24) & 0xFF);
            versionBytes[1] = (byte)((originalVersion >> 16) & 0xFF);
            versionBytes[2] = (byte)((originalVersion >> 8) & 0xFF);
            versionBytes[3] = (byte)(originalVersion & 0xFF);

            // Use library method which handles platform-specific compression
            bool isXbox360 = openedFastFile.IsXbox360;
            FastFileProcessor.CompressMW2(zoneFilePath, ffFilePath, versionBytes, isXbox360);
        }

        /// <summary>
        /// Skips the MW2 extended header structure.
        /// </summary>
        private void SkipExtendedHeader(BinaryReader br)
        {
            // MW2 extended header structure:
            // allowOnlineUpdate (1 byte)
            // fileCreationTime (8 bytes)
            // region (4 bytes)
            // entryCount (4 bytes, big-endian)
            // entries (entryCount * 0x14 bytes)
            // fileSizes (8 bytes)

            br.ReadByte();           // allowOnlineUpdate
            br.ReadBytes(8);         // fileCreationTime
            br.ReadBytes(4);         // region

            byte[] entryCountBytes = br.ReadBytes(4);
            int entryCount = (entryCountBytes[0] << 24) | (entryCountBytes[1] << 16) |
                            (entryCountBytes[2] << 8) | entryCountBytes[3];

            // Skip entries (each entry is 0x14 = 20 bytes on PS3)
            if (entryCount > 0 && entryCount < 10000)
            {
                br.ReadBytes(entryCount * 0x14);
            }

            br.ReadBytes(8);         // fileSizes
        }

    }
}
