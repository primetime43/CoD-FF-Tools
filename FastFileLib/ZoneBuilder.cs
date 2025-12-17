using System.Text;
using System.Text.RegularExpressions;
using FastFileLib.Models;

namespace FastFileLib;

/// <summary>
/// Builds a zone file from raw files and localized entries.
/// The zone structure is: [Header] + [AssetTable] + [RawFiles] + [Localized] + [Footer] + [Padding]
/// </summary>
public class ZoneBuilder
{
    private readonly GameVersion _gameVersion;
    private readonly List<RawFile> _rawFiles;
    private readonly List<LocalizedEntry> _localizedEntries;
    private readonly string _zoneName;

    // Size tracking for header calculations
    private int _assetTableSize;
    private int _rawFilesSize;
    private int _localizedSize;
    private int _footerSize;

    public ZoneBuilder(GameVersion gameVersion, string zoneName = "custom_patch_mp")
    {
        _gameVersion = gameVersion;
        _rawFiles = new List<RawFile>();
        _localizedEntries = new List<LocalizedEntry>();
        _zoneName = zoneName;
    }

    /// <summary>
    /// Adds a raw file to be included in the zone.
    /// </summary>
    public ZoneBuilder AddRawFile(RawFile rawFile)
    {
        _rawFiles.Add(rawFile);
        return this;
    }

    /// <summary>
    /// Adds multiple raw files to be included in the zone.
    /// </summary>
    public ZoneBuilder AddRawFiles(IEnumerable<RawFile> rawFiles)
    {
        _rawFiles.AddRange(rawFiles);
        return this;
    }

    /// <summary>
    /// Adds a localized string entry.
    /// </summary>
    public ZoneBuilder AddLocalizedEntry(LocalizedEntry entry)
    {
        _localizedEntries.Add(entry);
        return this;
    }

    /// <summary>
    /// Adds multiple localized string entries.
    /// </summary>
    public ZoneBuilder AddLocalizedEntries(IEnumerable<LocalizedEntry> entries)
    {
        _localizedEntries.AddRange(entries);
        return this;
    }

    /// <summary>
    /// Parses a .str file content and adds the localized entries.
    /// Format expected:
    /// REFERENCE    reference_name
    /// LANG_ENGLISH "translated text"
    /// </summary>
    public ZoneBuilder AddLocalizedFromStr(string strContent)
    {
        var references = Regex.Matches(strContent + "\r\n", @"(?<=REFERENCE)(\s+)(.*?)(?=\r\n)");
        var languages = Regex.Matches(strContent + "\r\n", @"(?<=LANG_ENGLISH)(\s+)(.*?)(?=\r\n)");

        for (int i = 0; i < references.Count && i < languages.Count; i++)
        {
            var reference = references[i].Groups[2].Value.Trim();
            var value = languages[i].Groups[2].Value.Trim().Trim('"');

            _localizedEntries.Add(new LocalizedEntry(reference, value));
        }

        return this;
    }

    /// <summary>
    /// Builds the complete zone file.
    /// </summary>
    public byte[] Build()
    {
        // Build sections in order (footer first since we need sizes for header)
        var rawFilesSection = BuildRawFilesSection();
        var localizedSection = BuildLocalizedSection();
        var assetTableSection = BuildAssetTableSection();
        var footerSection = BuildFooterSection();
        var headerSection = BuildHeaderSection();

        // Combine all sections
        var zone = new List<byte>();
        zone.AddRange(headerSection);
        zone.AddRange(assetTableSection);
        zone.AddRange(rawFilesSection);
        zone.AddRange(localizedSection);
        zone.AddRange(footerSection);

        // Pad to 64KB boundary
        int padding = (zone.Count / FastFileConstants.BlockSize + 1) * FastFileConstants.BlockSize - zone.Count;
        zone.AddRange(new byte[padding]);

        return zone.ToArray();
    }

    /// <summary>
    /// Builds the zone header (52 bytes for CoD4/WaW).
    /// Structure from Zone.md:
    /// 0x00: ZoneSize, 0x04: ExternalSize, 0x08: BlockSizeTemp, 0x0C: BlockSizePhysical,
    /// 0x10: BlockSizeRuntime, 0x14: BlockSizeVirtual, 0x18: BlockSizeLarge, 0x1C: BlockSizeCallback,
    /// 0x20: BlockSizeVertex, 0x24: ScriptStringCount, 0x28: ScriptStringsPtr, 0x2C: AssetCount, 0x30: AssetsPtr
    /// </summary>
    private byte[] BuildHeaderSection()
    {
        var header = new byte[52];

        // Calculate ZoneSize (total size excluding header, but we'll calculate based on content)
        int zoneSize = _assetTableSize + _rawFilesSize + _localizedSize + _footerSize;

        // Asset count
        int assetCount = _assetTableSize / 8;

        // Memory allocation blocks
        var blockSizeTemp = FastFileConstants.GetMemAlloc1(_gameVersion);
        var blockSizeVertex = FastFileConstants.GetMemAlloc2(_gameVersion);

        // 0x00: ZoneSize
        WriteBigEndian(header, 0x00, zoneSize);

        // 0x04: ExternalSize (0)
        WriteBigEndian(header, 0x04, 0);

        // 0x08: BlockSizeTemp
        blockSizeTemp.CopyTo(header, 0x08);

        // 0x0C: BlockSizePhysical (0)
        WriteBigEndian(header, 0x0C, 0);

        // 0x10: BlockSizeRuntime (0)
        WriteBigEndian(header, 0x10, 0);

        // 0x14: BlockSizeVirtual (0 for rawfile-only zones)
        WriteBigEndian(header, 0x14, 0);

        // 0x18: BlockSizeLarge (set to raw files size + some buffer)
        WriteBigEndian(header, 0x18, _rawFilesSize + _localizedSize);

        // 0x1C: BlockSizeCallback (0)
        WriteBigEndian(header, 0x1C, 0);

        // 0x20: BlockSizeVertex
        blockSizeVertex.CopyTo(header, 0x20);

        // 0x24: ScriptStringCount (0 - no script strings in rawfile-only zones)
        WriteBigEndian(header, 0x24, 0);

        // 0x28: ScriptStringsPtr (FF FF FF FF)
        header[0x28] = 0xFF; header[0x29] = 0xFF; header[0x2A] = 0xFF; header[0x2B] = 0xFF;

        // 0x2C: AssetCount
        WriteBigEndian(header, 0x2C, assetCount);

        // 0x30: AssetsPtr (FF FF FF FF)
        header[0x30] = 0xFF; header[0x31] = 0xFF; header[0x32] = 0xFF; header[0x33] = 0xFF;

        return header;
    }

    private static void WriteBigEndian(byte[] data, int offset, int value)
    {
        data[offset] = (byte)(value >> 24);
        data[offset + 1] = (byte)(value >> 16);
        data[offset + 2] = (byte)(value >> 8);
        data[offset + 3] = (byte)(value & 0xFF);
    }

    /// <summary>
    /// Builds the asset table section.
    /// Each asset entry is 8 bytes: [4-byte type: 00 00 00 XX] [4-byte ptr: FF FF FF FF]
    /// Per Zone.md: "Each record is 8 bytes (big-endian on PS3)"
    /// </summary>
    private byte[] BuildAssetTableSection()
    {
        var table = new List<byte>();

        byte rawFileType = FastFileConstants.GetRawFileAssetType(_gameVersion);
        byte localizeType = FastFileConstants.GetLocalizeAssetType(_gameVersion);

        // Entry for each raw file - format: [type][ptr] = 00 00 00 22 FF FF FF FF
        foreach (var _ in _rawFiles)
        {
            byte[] entry = { 0x00, 0x00, 0x00, rawFileType, 0xFF, 0xFF, 0xFF, 0xFF };
            table.AddRange(entry);
        }

        // Entry for each localized string
        foreach (var _ in _localizedEntries)
        {
            byte[] entry = { 0x00, 0x00, 0x00, localizeType, 0xFF, 0xFF, 0xFF, 0xFF };
            table.AddRange(entry);
        }

        // Final rawfile entry (required by format)
        byte[] finalEntry = { 0x00, 0x00, 0x00, rawFileType, 0xFF, 0xFF, 0xFF, 0xFF };
        table.AddRange(finalEntry);

        _assetTableSize = table.Count;
        return table.ToArray();
    }

    /// <summary>
    /// Builds the raw files section.
    /// Each raw file: FF FF FF FF + [size] + FF FF FF FF + [name\0] + [data] + [\0]
    /// </summary>
    private byte[] BuildRawFilesSection()
    {
        var section = new List<byte>();

        foreach (var rawFile in _rawFiles)
        {
            // Marker: FF FF FF FF
            section.AddRange(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });

            // Uncompressed size (big-endian)
            section.AddRange(GetBigEndianBytes(rawFile.Data.Length));

            // Pointer placeholder: FF FF FF FF
            section.AddRange(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });

            // Filename (null-terminated)
            section.AddRange(Encoding.ASCII.GetBytes(rawFile.Name));
            section.Add(0x00);

            // Raw data
            section.AddRange(rawFile.Data);

            // Null terminator
            section.Add(0x00);
        }

        _rawFilesSize = section.Count;
        return section.ToArray();
    }

    /// <summary>
    /// Builds the localized strings section.
    /// Each entry: FF FF FF FF FF FF FF FF + [value\0] + [reference\0]
    /// </summary>
    private byte[] BuildLocalizedSection()
    {
        var section = new List<byte>();

        foreach (var entry in _localizedEntries)
        {
            // Marker: FF FF FF FF FF FF FF FF
            section.AddRange(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF });

            // Localized value (null-terminated)
            section.AddRange(Encoding.Default.GetBytes(entry.Value));
            section.Add(0x00);

            // Reference key (null-terminated)
            section.AddRange(Encoding.Default.GetBytes(entry.Reference));
            section.Add(0x00);
        }

        _localizedSize = section.Count;
        return section.ToArray();
    }

    /// <summary>
    /// Builds the footer section.
    /// Contains terminator markers and zone name.
    /// </summary>
    private byte[] BuildFooterSection()
    {
        var footer = new List<byte>();

        if (_gameVersion == GameVersion.MW2)
        {
            // MW2 footer: 16 bytes
            footer.AddRange(new byte[]
            {
                0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF
            });
        }
        else
        {
            // CoD4/WaW footer: 12 bytes
            footer.AddRange(new byte[]
            {
                0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00,
                0xFF, 0xFF, 0xFF, 0xFF
            });
        }

        // Zone name (null-terminated with extra null)
        footer.AddRange(Encoding.ASCII.GetBytes(_zoneName));
        footer.AddRange(new byte[] { 0x00, 0x00 });

        _footerSize = footer.Count;
        return footer.ToArray();
    }

    /// <summary>
    /// Converts an int to big-endian bytes.
    /// </summary>
    private static byte[] GetBigEndianBytes(int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return bytes;
    }
}
