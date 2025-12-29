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
        /// Xbox 360 signed files use IWffs100 streaming format.
        /// Xbox 360 unsigned and PS3 files use standard block format.
        /// </summary>
        public override void Recompress(string ffFilePath, string zoneFilePath, FastFile openedFastFile)
        {
            if (openedFastFile.IsSigned)
            {
                // Xbox 360 signed files use streaming format - use library method
                // Build version bytes from original file's version
                int originalVersion = openedFastFile.GameVersion;
                byte[] versionBytes = new byte[4];
                versionBytes[0] = (byte)((originalVersion >> 24) & 0xFF);
                versionBytes[1] = (byte)((originalVersion >> 16) & 0xFF);
                versionBytes[2] = (byte)((originalVersion >> 8) & 0xFF);
                versionBytes[3] = (byte)(originalVersion & 0xFF);

                FastFileProcessor.CompressXbox360Signed(zoneFilePath, ffFilePath, versionBytes, openedFastFile.FfFilePath);
            }
            else
            {
                // PS3/Xbox 360 unsigned: use base class standard block format
                // But we need to preserve the original version bytes
                byte[] headerBytes = FastFileConstants.UnsignedHeaderBytes;
                int originalVersion = openedFastFile.GameVersion;
                byte[] versionBytes = new byte[4];
                versionBytes[0] = (byte)((originalVersion >> 24) & 0xFF);
                versionBytes[1] = (byte)((originalVersion >> 16) & 0xFF);
                versionBytes[2] = (byte)((originalVersion >> 8) & 0xFF);
                versionBytes[3] = (byte)(originalVersion & 0xFF);

                using (BinaryReader binaryReader = new BinaryReader(new FileStream(zoneFilePath, FileMode.Open, FileAccess.Read), Encoding.Default))
                using (BinaryWriter binaryWriter = new BinaryWriter(new FileStream(ffFilePath, FileMode.Create, FileAccess.Write), Encoding.Default))
                {
                    binaryWriter.Write(headerBytes);
                    binaryWriter.Write(versionBytes);

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
