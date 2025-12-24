using Call_of_Duty_FastFile_Editor.Constants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Call_of_Duty_FastFile_Editor.Services.IO
{
    public class ZoneFileIO
    {
        /// <summary>
        /// Reads the 4-byte zone file size from the header at the defined offset.
        /// </summary>
        /// <param name="path">Path to the zone file.</param>
        /// <param name="isPC">True for PC (little-endian), false for console (big-endian).</param>
        public static uint ReadZoneFileSize(string path, bool isPC = false)
        {
            var b = new byte[4];
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            fs.Seek(ZoneFileHeaderConstants.ZoneSizeOffset, SeekOrigin.Begin);
            fs.Read(b, 0, 4);
            return isPC
                ? System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(b)
                : System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(b);
        }

        /// <summary>
        /// Writes the updated zone file size to the header.
        /// Only updates ZoneSize (offset 0x00). BlockSizeLarge (offset 0x18) should NOT be modified
        /// as it represents XFILE_BLOCK_LARGE memory allocation which is set at zone creation time.
        /// </summary>
        /// <param name="path">Path to the zone file.</param>
        /// <param name="newSize">The new zone size value.</param>
        /// <param name="isPC">True for PC (little-endian), false for console (big-endian).</param>
        public static void WriteZoneFileSize(string path, uint newSize, bool isPC = false)
        {
            Span<byte> b = stackalloc byte[4];
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Write);

            // Write new ZoneSize at offset 0x00 only
            // BlockSizeLarge (0x18) should NOT be modified - it's for memory allocation
            if (isPC)
                System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(b, newSize);
            else
                System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(b, newSize);
            fs.Seek(ZoneFileHeaderConstants.ZoneSizeOffset, SeekOrigin.Begin);
            fs.Write(b);
        }

        /// <summary>
        /// Reads the BlockSizeLarge from the zone header.
        /// This represents the XFILE_BLOCK_LARGE allocation size.
        /// </summary>
        /// <param name="path">Path to the zone file.</param>
        /// <param name="isPC">True for PC (little-endian), false for console (big-endian).</param>
        public static uint ReadBlockSizeLarge(string path, bool isPC = false)
        {
            var b = new byte[4];
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            fs.Seek(ZoneFileHeaderConstants.EndOfFileDataPointer, SeekOrigin.Begin);
            fs.Read(b, 0, 4);
            return isPC
                ? System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(b)
                : System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(b);
        }
    }
}
