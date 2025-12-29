using Call_of_Duty_FastFile_Editor.GameDefinitions;
using Call_of_Duty_FastFile_Editor.Models;
using FastFileLib;
using FastFileLib.GameDefinitions;
using System.IO.Compression;
using System.Text;

namespace Call_of_Duty_FastFile_Editor.IO
{
    public class CoD5FastFileHandler : FastFileHandlerBase
    {
        // Xbox 360 signed files use IWffs100 secondary header format
        private static readonly byte[] StreamingHeaderBytes = Encoding.ASCII.GetBytes("IWffs100");

        // Default to unsigned for decompression (actual header is read from file)
        protected override byte[] HeaderBytes => FastFileConstants.UnsignedHeaderBytes;
        protected override byte[] VersionBytes => CoD5Definition.VersionBytes;

        /// <summary>
        /// Compresses data using full zlib format (with 78 DA header - best compression).
        /// Xbox 360 signed files use this format with maximum compression.
        /// </summary>
        private static byte[] CompressFullZlib(byte[] uncompressedData)
        {
            using var output = new MemoryStream();
            // Use SmallestSize to get 78 DA header (best compression) like working files
            using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize))
            {
                zlib.Write(uncompressedData, 0, uncompressedData.Length);
            }
            return output.ToArray();
        }

        /// <summary>
        /// Recompresses the zone file back to FastFile format.
        /// Preserves the original file's signed/unsigned status from the header.
        /// Xbox 360 signed files use IWffs100 streaming format.
        /// Xbox 360 unsigned and PS3 files use standard block format.
        /// </summary>
        public override void Recompress(string ffFilePath, string zoneFilePath, FastFile openedFastFile)
        {
            // Determine header based on original file's signed status
            byte[] headerBytes = openedFastFile.IsSigned
                ? FastFileConstants.SignedHeaderBytes
                : FastFileConstants.UnsignedHeaderBytes;

            // Use original file's version (big-endian)
            int originalVersion = openedFastFile.GameVersion;
            byte[] versionBytes = new byte[4];
            versionBytes[0] = (byte)((originalVersion >> 24) & 0xFF);
            versionBytes[1] = (byte)((originalVersion >> 16) & 0xFF);
            versionBytes[2] = (byte)((originalVersion >> 8) & 0xFF);
            versionBytes[3] = (byte)(originalVersion & 0xFF);

            // For signed files, read the hash table BEFORE opening the output file
            // This avoids file lock conflicts when saving to the same file
            byte[] hashTableAndPrefix = null;
            if (openedFastFile.IsSigned && File.Exists(openedFastFile.FfFilePath))
            {
                hashTableAndPrefix = new byte[0x400C - 0x14]; // 16376 bytes (up to zlib start)
                using var origReader = new BinaryReader(File.OpenRead(openedFastFile.FfFilePath));
                origReader.BaseStream.Seek(0x14, SeekOrigin.Begin);
                origReader.Read(hashTableAndPrefix, 0, hashTableAndPrefix.Length);
            }

            using (BinaryReader binaryReader = new BinaryReader(new FileStream(zoneFilePath, FileMode.Open, FileAccess.Read), Encoding.Default))
            using (BinaryWriter binaryWriter = new BinaryWriter(new FileStream(ffFilePath, FileMode.Create, FileAccess.Write), Encoding.Default))
            {
                // Write header (signed or unsigned based on original) and version
                binaryWriter.Write(headerBytes);
                binaryWriter.Write(versionBytes);

                if (openedFastFile.IsSigned)
                {
                    // Xbox 360 signed files use IWffs100 streaming format
                    // Format:
                    //   0x00: IWff0100 (magic) - already written
                    //   0x08: Version (4 bytes) - already written
                    //   0x0C: IWffs100 (streaming header)
                    //   0x14-0x3FFF: Hash table area (preserved from original)
                    //   0x4000-0x400B: 12 bytes authentication data (preserved from original)
                    //   0x400C onwards: Single continuous zlib stream
                    //   No block structure, no end marker
                    binaryWriter.Write(StreamingHeaderBytes);

                    // Write hash table area and 12-byte prefix (from 0x14 to 0x400C)
                    // If we couldn't read from original, write zeros (file will likely not work on console)
                    if (hashTableAndPrefix != null)
                    {
                        binaryWriter.Write(hashTableAndPrefix);
                    }
                    else
                    {
                        binaryWriter.Write(new byte[0x400C - 0x14]);
                    }

                    // Read entire zone file
                    byte[] zoneData = binaryReader.ReadBytes((int)binaryReader.BaseStream.Length);

                    // Compress entire zone as single zlib stream
                    byte[] compressedData = CompressFullZlib(zoneData);
                    binaryWriter.Write(compressedData);

                    // No end marker for signed format - just the zlib stream
                }
                else
                {
                    // PS3/Xbox 360 unsigned: standard block format with 2-byte lengths
                    int chunkSize = 65536;
                    while (binaryReader.BaseStream.Position < binaryReader.BaseStream.Length)
                    {
                        byte[] chunk = binaryReader.ReadBytes(chunkSize);
                        byte[] compressedChunk = CompressFF(chunk);

                        int compressedLength = compressedChunk.Length;
                        byte[] lengthBytes = BitConverter.GetBytes(compressedLength);
                        Array.Reverse(lengthBytes);
                        binaryWriter.Write(lengthBytes, 2, 2);

                        binaryWriter.Write(compressedChunk);
                    }

                    // Write end marker (0x00 0x01)
                    binaryWriter.Write((byte)0x00);
                    binaryWriter.Write((byte)0x01);
                }
            }
        }
    }
}
