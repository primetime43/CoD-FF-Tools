using Call_of_Duty_FastFile_Editor.GameDefinitions;
using Call_of_Duty_FastFile_Editor.Models;
using FastFileLib;
using FastFileLib.GameDefinitions;
using System.Text;

namespace Call_of_Duty_FastFile_Editor.IO
{
    public class CoD5FastFileHandler : FastFileHandlerBase
    {
        // Default to unsigned for decompression (actual header is read from file)
        protected override byte[] HeaderBytes => FastFileConstants.UnsignedHeaderBytes;
        protected override byte[] VersionBytes => CoD5Definition.VersionBytes;

        /// <summary>
        /// Recompresses the zone file back to FastFile format.
        /// Preserves the original file's signed/unsigned status from the header.
        /// Xbox 360 MP files use signed header (IWff0100), SP and PS3 files use unsigned (IWffu100).
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
