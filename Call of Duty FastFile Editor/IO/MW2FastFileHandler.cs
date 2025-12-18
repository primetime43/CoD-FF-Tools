using Call_of_Duty_FastFile_Editor.GameDefinitions;
using Call_of_Duty_FastFile_Editor.Models;
using FastFileLib;
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
        /// </summary>
        public override void Decompress(string inputFilePath, string outputFilePath)
        {
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
        /// Note: MW2 uses zlib-wrapped deflate, not raw deflate.
        /// </summary>
        public override void Recompress(string ffFilePath, string zoneFilePath, FastFile openedFastFile)
        {
            using var binaryReader = new BinaryReader(new FileStream(zoneFilePath, FileMode.Open, FileAccess.Read), Encoding.Default);
            using var binaryWriter = new BinaryWriter(new FileStream(ffFilePath, FileMode.Create, FileAccess.Write), Encoding.Default);

            // Write header and version value
            binaryWriter.Write(HeaderBytes);
            binaryWriter.Write(VersionBytes);

            // MW2 needs a minimal extended header for the game to accept it
            WriteMinimalExtendedHeader(binaryWriter);

            int chunkSize = 65536;
            while (binaryReader.BaseStream.Position < binaryReader.BaseStream.Length)
            {
                byte[] chunk = binaryReader.ReadBytes(chunkSize);
                byte[] compressedChunk = CompressMW2(chunk);

                int compressedLength = compressedChunk.Length;
                byte[] lengthBytes = BitConverter.GetBytes(compressedLength);
                Array.Reverse(lengthBytes);
                binaryWriter.Write(lengthBytes, 2, 2);

                binaryWriter.Write(compressedChunk);
            }
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

        /// <summary>
        /// Writes a minimal extended header for MW2.
        /// </summary>
        private void WriteMinimalExtendedHeader(BinaryWriter bw)
        {
            bw.Write((byte)0x00);    // allowOnlineUpdate = false
            bw.Write(new byte[8]);   // fileCreationTime = 0
            bw.Write(new byte[4]);   // region = 0
            bw.Write(new byte[4]);   // entryCount = 0 (no entries)
            bw.Write(new byte[8]);   // fileSizes = 0
        }

        /// <summary>
        /// Compresses data using zlib-wrapped deflate for MW2.
        /// MW2 uses zlib format (0x78 header) instead of raw deflate.
        /// </summary>
        private byte[] CompressMW2(byte[] uncompressedData)
        {
            using var output = new MemoryStream();
            using (var zlib = new System.IO.Compression.ZLibStream(output, System.IO.Compression.CompressionLevel.Optimal))
            {
                zlib.Write(uncompressedData, 0, uncompressedData.Length);
            }
            return output.ToArray();
        }
    }
}
