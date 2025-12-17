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
    /// Builds the zone header (48 bytes for CoD4/WaW, different for MW2).
    /// </summary>
    private byte[] BuildHeaderSection()
    {
        var header = new List<byte>();

        // Calculate total sizes
        int totalDataSize = _assetTableSize + _rawFilesSize + _localizedSize + _footerSize + 16;
        int totalZoneSize = 52 + _assetTableSize + _rawFilesSize + _localizedSize + _footerSize;

        // Asset count (number of asset table entries / 8)
        byte[] assetCount = GetBigEndianBytes(_assetTableSize / 8);

        // Total data size (big-endian)
        byte[] dataSizeBytes = GetBigEndianBytes(totalDataSize);

        // Total zone size (big-endian)
        byte[] zoneSizeBytes = GetBigEndianBytes(totalZoneSize);

        // Memory allocation blocks
        var memAlloc1 = FastFileConstants.GetMemAlloc1(_gameVersion);
        var memAlloc2 = FastFileConstants.GetMemAlloc2(_gameVersion);

        // Build header structure
        // Bytes 0-3: Total data size
        header.AddRange(dataSizeBytes);

        // Bytes 4-23: Memory allocation block 1 (20 bytes, memAlloc1 at offset 4)
        byte[] allocBlock1 = new byte[20];
        memAlloc1.CopyTo(allocBlock1, 4);
        header.AddRange(allocBlock1);

        // Bytes 24-27: Total zone size
        header.AddRange(zoneSizeBytes);

        // Bytes 28-43: Memory allocation block 2 (16 bytes, memAlloc2 at offset 4)
        byte[] allocBlock2 = new byte[16];
        memAlloc2.CopyTo(allocBlock2, 4);
        header.AddRange(allocBlock2);

        // Bytes 44-47: Asset count
        header.AddRange(assetCount);

        // Bytes 48-51: 0xFFFFFFFF marker
        header.AddRange(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });

        return header.ToArray();
    }

    /// <summary>
    /// Builds the asset table section.
    /// Each asset entry is 8 bytes: FF FF FF FF 00 00 00 [type] (ptr first, then type)
    /// </summary>
    private byte[] BuildAssetTableSection()
    {
        var table = new List<byte>();

        byte rawFileType = FastFileConstants.GetRawFileAssetType(_gameVersion);
        byte localizeType = FastFileConstants.GetLocalizeAssetType(_gameVersion);

        // Entry for each raw file - format: [ptr][type] = FF FF FF FF 00 00 00 22
        foreach (var _ in _rawFiles)
        {
            byte[] entry = { 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, rawFileType };
            table.AddRange(entry);
        }

        // Entry for each localized string
        foreach (var _ in _localizedEntries)
        {
            byte[] entry = { 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, localizeType };
            table.AddRange(entry);
        }

        // Final rawfile entry (required by format)
        byte[] finalEntry = { 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, rawFileType };
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
