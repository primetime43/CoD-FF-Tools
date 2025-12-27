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
        // Xbox 360 signed files have a 256-byte signature block after the header
        private const int SignatureBlockSize = 0x100;

        // Default to unsigned for decompression (actual header is read from file)
        protected override byte[] HeaderBytes => FastFileConstants.UnsignedHeaderBytes;
        protected override byte[] VersionBytes => CoD5Definition.VersionBytes;

        /// <summary>
        /// Recompresses the zone file back to FastFile format.
        /// Preserves the original file's signed/unsigned status from the header.
        /// Xbox 360 signed files have a signature block and use single zlib stream.
        /// Xbox 360 unsigned and PS3 files use standard block format.
        /// </summary>
        public override void Recompress(string ffFilePath, string zoneFilePath, FastFile openedFastFile)
        {
            // Determine header based on original file's signed status
            byte[] headerBytes = openedFastFile.IsSigned
                ? FastFileConstants.SignedHeaderBytes
                : FastFileConstants.UnsignedHeaderBytes;

            using (BinaryReader binaryReader = new BinaryReader(new FileStream(zoneFilePath, FileMode.Open, FileAccess.Read), Encoding.Default))
            using (BinaryWriter binaryWriter = new BinaryWriter(new FileStream(ffFilePath, FileMode.Create, FileAccess.Write), Encoding.Default))
            {
                // Write header (signed or unsigned based on original) and version
                binaryWriter.Write(headerBytes);
                binaryWriter.Write(VersionBytes);

                if (openedFastFile.IsSigned)
                {
                    // Xbox 360 signed files: write signature block (zeros) + single zlib stream
                    // The signature is not valid but works on modded consoles (RGH/JTAG)
                    binaryWriter.Write(new byte[SignatureBlockSize]);

                    // Read all zone data and compress as single zlib stream
                    byte[] zoneData = binaryReader.ReadBytes((int)binaryReader.BaseStream.Length);

                    using (var compressedStream = new MemoryStream())
                    {
                        using (var zlibStream = new ZLibStream(compressedStream, CompressionLevel.Optimal))
                        {
                            zlibStream.Write(zoneData, 0, zoneData.Length);
                        }
                        binaryWriter.Write(compressedStream.ToArray());
                    }
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
