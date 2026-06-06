// =============================================================================
// IW4 (MW2 PS3) zone reader — ported from Jacob Schroeder's FastFile
//   https://github.com/jacob-schroeder/FastFile
// Ports: FastFile.Logic/Extensions/BinarySpanExtensions.cs (big-endian span reads).
// ReadVec4 dropped (the ported readers don't need it); rest is faithful.
// =============================================================================

using System.Buffers.Binary;
using System.Text;

namespace FastFileLib.Iw4;

internal static class BinarySpanExtensions
{
    public static bool ReadBool(this ReadOnlySpan<byte> span, ref int offset)
    {
        byte value = span[offset];
        offset++;
        return value switch
        {
            0 => false,
            1 => true,
            _ => throw new InvalidDataException($"Invalid boolean value {value}"),
        };
    }

    public static byte ReadByte(this ReadOnlySpan<byte> span, ref int offset)
    {
        byte value = span[offset];
        offset++;
        return value;
    }

    public static float ReadFloat(this ReadOnlySpan<byte> span, ref int offset)
    {
        float value = BinaryPrimitives.ReadSingleBigEndian(span.Slice(offset, 4));
        offset += 4;
        return value;
    }

    public static int ReadInt32(this ReadOnlySpan<byte> span, ref int offset)
    {
        int value = BinaryPrimitives.ReadInt32BigEndian(span.Slice(offset, 4));
        offset += 4;
        return value;
    }

    public static ushort ReadUInt16(this ReadOnlySpan<byte> span, ref int offset)
    {
        ushort value = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(offset, 2));
        offset += 2;
        return value;
    }

    public static uint ReadUInt32(this ReadOnlySpan<byte> span, ref int offset)
    {
        uint value = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(offset, 4));
        offset += 4;
        return value;
    }

    public static ulong ReadUInt64(this ReadOnlySpan<byte> span, ref int offset)
    {
        ulong value = BinaryPrimitives.ReadUInt64BigEndian(span.Slice(offset, 8));
        offset += 8;
        return value;
    }

    public static string ReadCStringAt(this ReadOnlySpan<byte> span, ref int offset)
    {
        ReadOnlySpan<byte> slice = span.Slice(offset);
        int end = slice.IndexOf((byte)0);
        if (end < 0)
            throw new InvalidDataException("Missing null terminator.");

        var data = slice.Slice(0, end);
        offset += end + 1;
        return Encoding.Latin1.GetString(data);
    }

    public static string ReadString(this ReadOnlySpan<byte> span, ref int offset, int length)
    {
        string result = Encoding.Latin1.GetString(span.Slice(offset, length));
        offset += length;
        return result;
    }

    public static byte[] Read(this ReadOnlySpan<byte> span, ref int offset, int length)
    {
        byte[] result = span.Slice(offset, length).ToArray();
        offset += length;
        return result;
    }

    public static Vec4 ReadVec4(this ReadOnlySpan<byte> span, ref int offset)
    {
        return new Vec4
        {
            A = span.ReadFloat(ref offset),
            R = span.ReadFloat(ref offset),
            G = span.ReadFloat(ref offset),
            B = span.ReadFloat(ref offset),
        };
    }
}
