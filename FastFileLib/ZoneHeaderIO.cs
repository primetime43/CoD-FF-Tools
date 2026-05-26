namespace FastFileLib;

/// <summary>
/// Reads and writes individual 4-byte fields of a zone-file header directly on disk,
/// in the platform's byte order (PC = little-endian; PS3 / Xbox 360 / Wii = big-endian).
/// Field offsets come from <see cref="FastFileConstants"/> and the endian conversion uses
/// the shared <see cref="FastFileConstants.ReadUInt32(byte[], int, bool)"/> primitives.
///
/// Shared by the editor's in-place resize path; available to any tool that needs to patch
/// a single header field without loading or rebuilding the whole zone.
/// </summary>
public static class ZoneHeaderIO
{
    /// <summary>Reads a 4-byte unsigned field at <paramref name="offset"/> from the zone file on disk.</summary>
    public static uint ReadHeaderUInt32(string path, int offset, bool isPC)
    {
        byte[] b = new byte[4];
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
        fs.Seek(offset, SeekOrigin.Begin);
        if (fs.Read(b, 0, 4) < 4)
            throw new EndOfStreamException($"Zone file too short to read header field at 0x{offset:X}.");
        return FastFileConstants.ReadUInt32(b, 0, littleEndian: isPC);
    }

    /// <summary>Writes a 4-byte unsigned field at <paramref name="offset"/> in the zone file on disk.</summary>
    public static void WriteHeaderUInt32(string path, int offset, uint value, bool isPC)
    {
        byte[] b = new byte[4];
        FastFileConstants.WriteUInt32(b, 0, value, littleEndian: isPC);
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Write);
        fs.Seek(offset, SeekOrigin.Begin);
        fs.Write(b, 0, 4);
    }

    /// <summary>Reads ZoneSize (offset 0x00) from the zone file header.</summary>
    public static uint ReadZoneSize(string path, bool isPC = false)
        => ReadHeaderUInt32(path, FastFileConstants.ZoneSizeOffset, isPC);

    /// <summary>
    /// Writes ZoneSize (offset 0x00). Only ZoneSize is touched — BlockSizeLarge (0x18) and the
    /// MemAlloc fields are fixed at zone-creation time and must NOT be modified here.
    /// </summary>
    public static void WriteZoneSize(string path, uint newSize, bool isPC = false)
        => WriteHeaderUInt32(path, FastFileConstants.ZoneSizeOffset, newSize, isPC);
}
