using Call_of_Duty_FastFile_Editor.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Call_of_Duty_FastFile_Editor.Services
{
    public static class Utilities
    {
        public static int ReadInt32BigEndianAtOffset(int offset, ZoneFile loadedZone, bool isBigEndian = true)
        {
            if (offset + 4 > loadedZone.Data.Length)
                throw new EndOfStreamException($"Cannot read Int32 at offset 0x{offset:X}, exceeds file length.");

            byte[] bytes = new byte[4];
            Array.Copy(loadedZone.Data, offset, bytes, 0, 4);

            if (isBigEndian)
                Array.Reverse(bytes);

            return BitConverter.ToInt32(bytes, 0);
        }

        /// <summary>
        /// Helper method to read a UInt32 at a specific offset. Can specify endianness.
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="isBigEndian"></param>
        /// <param name="loadedZone"></param>
        /// <returns></returns>
        /// <exception cref="EndOfStreamException"></exception>
        public static uint ReadUInt32AtOffset(int offset, ZoneFile loadedZone, bool isBigEndian = true)
        {
            if (offset + 4 > loadedZone.Data.Length)
                throw new EndOfStreamException($"Cannot read UInt32 at offset 0x{offset:X}, exceeds file length.");

            byte[] bytes = new byte[4];
            Array.Copy(loadedZone.Data, offset, bytes, 0, 4);

            if (isBigEndian)
                Array.Reverse(bytes);

            return BitConverter.ToUInt32(bytes, 0);
        }


        /// <summary>
        /// Helper method to read a null-terminated string at a specific offset
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="loadedZone"></param>
        /// <returns></returns>
        /// <exception cref="EndOfStreamException"></exception>
        public static string ReadStringAtOffset(int offset, ZoneFile loadedZone)
        {
            if (offset >= loadedZone.Data.Length)
                throw new EndOfStreamException($"Cannot read string at offset 0x{offset:X}, exceeds file length.");

            int end = offset;
            while (end < loadedZone.Data.Length && loadedZone.Data[end] != 0x00)
            {
                end++;
            }

            if (end == loadedZone.Data.Length)
                throw new EndOfStreamException($"String starting at offset 0x{offset:X} is not null-terminated.");

            return Encoding.UTF8.GetString(loadedZone.Data, offset, end - offset);
        }

        /// <summary>
        /// Helper method to get bytes at a specific offset and length
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        /// <param name="loadedZone"></param>
        /// <returns></returns>
        /// <exception cref="EndOfStreamException"></exception>
        public static byte[] GetBytesAtOffset(int offset, ZoneFile loadedZone, int length = 4) // default 4 bytes aka a word
        {
            if (offset + length > loadedZone.Data.Length)
                throw new EndOfStreamException($"Cannot read {length} bytes at offset 0x{offset:X}, exceeds file length.");

            byte[] bytes = new byte[length];
            Array.Copy(loadedZone.Data, offset, bytes, 0, length);
            return bytes;
        }

        /// <summary>
        /// Converts a uint value to a big endian hexadecimal string.
        /// </summary>
        /// <param name="value">The uint value to convert.</param>
        /// <returns>A string representing the big endian hexadecimal value.</returns>
        public static string ConvertToBigEndianHex(uint value)
        {
            byte[] bytes = BitConverter.GetBytes(value);

            // Check system endianness and reverse if necessary to get big endian
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            // Convert byte array to hexadecimal string without dashes
            return BitConverter.ToString(bytes).Replace("-", "");
        }

        /// <summary>
        /// Converts a int value to a big endian hexadecimal string.
        /// </summary>
        /// <param name="value">The uint value to convert.</param>
        /// <returns>A string representing the big endian hexadecimal value.</returns>
        public static string ConvertToBigEndianHex(int value)
        {
            byte[] bytes = BitConverter.GetBytes(value);

            // Check system endianness and reverse if necessary to get big endian
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            // Convert byte array to hexadecimal string without dashes
            return BitConverter.ToString(bytes).Replace("-", "");
        }

        /// <summary>
        /// Reads a 4-byte unsigned integer from the given byte array at the specified offset in big-endian order.
        /// </summary>
        public static uint ReadUInt32BigEndian(byte[] data, int offset)
        {
            byte[] bytes = new byte[4];
            Array.Copy(data, offset, bytes, 0, 4);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            return BitConverter.ToUInt32(bytes, 0);
        }

        /// <summary>
        /// Reads a 4-byte unsigned integer from the given byte array at the specified offset in little-endian order.
        /// </summary>
        public static uint ReadUInt32LittleEndian(byte[] data, int offset)
        {
            if (offset + 4 > data.Length)
                return 0;
            return (uint)(data[offset] | (data[offset + 1] << 8) |
                          (data[offset + 2] << 16) | (data[offset + 3] << 24));
        }

        /// <summary>
        /// Reads a null-terminated UTF8 string from the given byte array starting at the specified offset.
        /// </summary>
        public static string ReadNullTerminatedString(byte[] data, int offset)
        {
            List<byte> byteList = new List<byte>();
            while (offset < data.Length)
            {
                byte b = data[offset++];
                if (b == 0)
                    break;
                byteList.Add(b);
            }
            return Encoding.UTF8.GetString(byteList.ToArray());
        }

        /// <summary>
        /// Helper method to read a string until a null terminator starting at a specific offset
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        /*private static string ReadNullTerminatedString(BinaryReader reader)
        {
            List<byte> bytes = new List<byte>();
            try
            {
                byte b;
                while ((b = reader.ReadByte()) != 0)
                {
                    bytes.Add(b);
                }
            }
            catch (EndOfStreamException)
            {
                MessageBox.Show("Unexpected end of stream while reading a null-terminated string.", "Deserialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }
            return Encoding.UTF8.GetString(bytes.ToArray());
        }*/
    }
}
