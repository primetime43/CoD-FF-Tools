using Call_of_Duty_FastFile_Editor.Models;
using FastFileLib;
using System.IO;
using System.Text;
using Ionic.Zlib;

namespace Call_of_Duty_FastFile_Editor.IO
{
    public abstract class FastFileHandlerBase : IFastFileHandler
    {
        protected abstract byte[] HeaderBytes { get; }
        protected abstract byte[] VersionBytes { get; }

        /// <summary>
        /// Reads a FastFile from <paramref name="inputFilePath"/>, decompresses its data sections,
        /// and writes the resulting "zone" content to <paramref name="outputFilePath"/>.
        /// </summary>
        /// <param name="inputFilePath">
        /// The full path to an existing FastFile (binary) to be decompressed.
        /// </param>
        /// <param name="outputFilePath">
        /// The full path where the decompressed zone file will be created (or overwritten).
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="inputFilePath"/> or <paramref name="outputFilePath"/> is <c>null</c> or empty.
        /// </exception>
        /// <exception cref="FileNotFoundException">
        /// Thrown if <paramref name="inputFilePath"/> does not exist.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown on any I/O error reading or writing the streams.
        /// </exception>
        /// <remarks>
        /// Uses FastFileLib's Decompress method which handles both unsigned (PS3) and signed (Xbox 360) files.
        /// Signed Xbox 360 files have a 0x4000 byte signature block after the header that is automatically skipped.
        /// </remarks>
        public virtual void Decompress(string inputFilePath, string outputFilePath)
        {
            // Use FastFileLib which handles both signed (Xbox 360) and unsigned (PS3) files
            FastFileProcessor.Decompress(inputFilePath, outputFilePath);
        }


        /// <summary>
        /// Takes an existing “zone” file, compresses it back into FastFile format,
        /// and writes the result to <paramref name="ffFilePath"/>, including header and version bytes.
        /// </summary>
        /// <param name="ffFilePath">
        /// The full path where the recomposed FastFile will be created (or overwritten).
        /// </param>
        /// <param name="zoneFilePath">
        /// The full path to the intermediate decompressed zone file to be recompressed.
        /// </param>
        /// <param name="openedFastFile">
        /// An object representing metadata about the FastFile (e.g. header/version bytes).
        /// Used here to supply <see cref="HeaderBytes"/> and <see cref="VersionBytes"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any argument is <c>null</c> or empty.
        /// </exception>
        /// <exception cref="FileNotFoundException">
        /// Thrown if <paramref name="zoneFilePath"/> does not exist.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown on any I/O error during read/write operations.
        /// </exception>
        /// <remarks>
        /// - Writes the header and version first, then splits the zone file into chunks (up to 64 KB each).
        /// - Each chunk is compressed via <see cref="CompressFF"/>, the compressed length is written
        ///   as a 2-byte big-endian value, followed by the compressed data bytes.
        /// - Finally writes a terminating 0x0001 marker to signal end-of-file.
        /// - Override in a subclass to alter chunk size, compression level, or header/version logic.
        /// </remarks>
        public virtual void Recompress(string ffFilePath, string zoneFilePath, FastFile openedFastFile)
        {
            using (BinaryReader binaryReader = new BinaryReader(new FileStream(zoneFilePath, FileMode.Open, FileAccess.Read), Encoding.Default))
            using (BinaryWriter binaryWriter = new BinaryWriter(new FileStream(ffFilePath, FileMode.Create, FileAccess.Write), Encoding.Default))
            {
                // Write header and version value
                binaryWriter.Write(HeaderBytes);
                binaryWriter.Write(VersionBytes);

                int chunkSize = 65536;
                while (binaryReader.BaseStream.Position < binaryReader.BaseStream.Length)
                {
                    byte[] chunk = binaryReader.ReadBytes(chunkSize);
                    byte[] compressedChunk = CompressFF(chunk);

                    int compressedLength = compressedChunk.Length;
                    byte[] lengthBytes = BitConverter.GetBytes(compressedLength);
                    Array.Reverse(lengthBytes); // Ensure correct byte order
                    binaryWriter.Write(lengthBytes, 2, 2); // Write only the last 2 bytes

                    binaryWriter.Write(compressedChunk);
                }

                // Write end marker (0x00 0x01) - required by the game
                binaryWriter.Write((byte)0x00);
                binaryWriter.Write((byte)0x01);
            }
        }

        /// <summary>
        /// Decompresses the given byte array using the shared FastFileLib.
        /// Automatically detects zlib vs raw deflate format.
        /// </summary>
        /// <param name="compressedData">
        /// A byte array containing data that was previously compressed with Deflate.
        /// Must not be <c>null</c>.
        /// </param>
        /// <returns>
        /// A new byte array containing the decompressed data.
        /// </returns>
        protected virtual byte[] DecompressFF(byte[] compressedData)
        {
            return FastFileProcessor.DecompressBlock(compressedData);
        }

        /// <summary>
        /// Compresses the given byte array using the shared FastFileLib.
        /// </summary>
        /// <param name="uncompressedData">
        /// A byte array of raw data to be compressed. Must not be <c>null</c>.
        /// </param>
        /// <returns>
        /// A new byte array containing the compressed data.
        /// </returns>
        protected virtual byte[] CompressFF(byte[] uncompressedData)
        {
            return FastFileProcessor.CompressBlock(uncompressedData);
        }
    }
}
