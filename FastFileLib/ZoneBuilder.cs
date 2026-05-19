using System.Text;
using System.Text.RegularExpressions;
using FastFileLib.GameDefinitions;
using FastFileLib.Models;

namespace FastFileLib;

/// <summary>
/// Builds a zone file from raw files and localized entries.
/// The zone structure is: [Header] + [AssetTable] + [RawFiles] + [Localized] + [Footer] + [Padding]
///
/// Header layout depends on game + platform. We support:
///   - 48 bytes (MW2 Xbox 360): no BlockSizeVertex slot, asset table @ 0x30
///   - 52 bytes (default — PS3, CoD4/WaW Xbox 360, CoD4/WaW PC): asset table @ 0x34
///   - 56 bytes (Wii WaW, MW2 PC): adds BlockSizeIndex slot, asset table @ 0x38
/// PC zones store all 32-bit fields little-endian; everything else is big-endian.
/// Asset type IDs also shift per platform (Xbox 360 drops vertexshader, PC drops
/// pixelshader+vertexshader, MW2 PC adds vertexdecl).
///
/// Rawfile entry format also varies: CoD4/WaW use a 12-byte uncompressed header,
/// MW2 uses 16/20-byte headers with zlib-compressed payloads (size fields LE on PC,
/// BE on console).
/// </summary>
public class ZoneBuilder
{
    private readonly GameVersion _gameVersion;
    private readonly List<RawFile> _rawFiles;
    private readonly List<LocalizedEntry> _localizedEntries;
    private readonly string _zoneName;
    private readonly bool _isPC;
    private readonly bool _isXbox360;
    private readonly bool _isWii;

    // Size tracking for header calculations
    private int _assetTableSize;
    private int _rawFilesSize;
    private int _localizedSize;
    private int _footerSize;

    public ZoneBuilder(GameVersion gameVersion, string zoneName = "custom_patch_mp", string platform = "PS3")
    {
        _gameVersion = gameVersion;
        _rawFiles = new List<RawFile>();
        _localizedEntries = new List<LocalizedEntry>();
        _zoneName = zoneName;

        string p = (platform ?? "PS3").Trim().ToUpperInvariant();
        _isPC = p == "PC";
        _isXbox360 = p == "XBOX360" || p == "XBOX 360";
        _isWii = p == "WII";
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
    /// Builds the zone header at the size dictated by game + platform. See class docs
    /// for the three supported layouts.
    /// </summary>
    private byte[] BuildHeaderSection()
    {
        int headerSize = FastFileConstants.GetZoneHeaderSize(_gameVersion, _isXbox360, _isPC, _isWii);
        var header = new byte[headerSize];

        // ZoneSize = everything after the header (asset table + sections + footer, excluding padding).
        int zoneSize = _assetTableSize + _rawFilesSize + _localizedSize + _footerSize;
        int assetCount = _assetTableSize / 8;

        uint blockSizeTemp = GetBlockSizeTempValue();
        uint blockSizeVertex = GetBlockSizeVertexValue();

        // XFile fields up through BlockSizeCallback are at the same offsets in every layout.
        WriteUInt32(header, 0x00, (uint)zoneSize);                   // ZoneSize
        WriteUInt32(header, 0x04, 0);                                // ExternalSize
        WriteUInt32(header, 0x08, blockSizeTemp);                    // BlockSizeTemp
        WriteUInt32(header, 0x0C, 0);                                // BlockSizePhysical
        WriteUInt32(header, 0x10, 0);                                // BlockSizeRuntime
        WriteUInt32(header, 0x14, 0);                                // BlockSizeVirtual
        WriteUInt32(header, 0x18, (uint)(_rawFilesSize + _localizedSize)); // BlockSizeLarge
        WriteUInt32(header, 0x1C, 0);                                // BlockSizeCallback

        // BlockSizeVertex slot exists on every layout EXCEPT MW2 Xbox 360 (48-byte).
        bool isMw2Xbox360 = _gameVersion == GameVersion.MW2 && _isXbox360;
        if (!isMw2Xbox360)
        {
            WriteUInt32(header, 0x20, blockSizeVertex);
        }

        // 56-byte layouts (Wii WaW, MW2 PC) carry an extra BlockSizeIndex slot at 0x24.
        if (headerSize == FastFileConstants.ZoneHeaderSize_Wii)
        {
            WriteUInt32(header, 0x24, 0);                            // BlockSizeIndex
        }

        // XAssetList offsets float based on layout.
        int scriptStringCountOffset = FastFileConstants.GetScriptStringCountOffset(_gameVersion, _isXbox360, _isPC, _isWii);
        int scriptStringsPtrOffset  = scriptStringCountOffset + 4;
        int assetCountOffset        = scriptStringsPtrOffset + 4;
        int assetsPtrOffset         = assetCountOffset + 4;

        WriteUInt32(header, scriptStringCountOffset, 0);
        WriteUInt32(header, scriptStringsPtrOffset, 0xFFFFFFFF);
        WriteUInt32(header, assetCountOffset, (uint)assetCount);
        WriteUInt32(header, assetsPtrOffset, 0xFFFFFFFF);

        return header;
    }

    /// <summary>
    /// BlockSizeTemp (MemAlloc1). Best-effort defaults — for PC/Wii these are
    /// per-zone allocations rather than fixed magic constants in retail files;
    /// when patching from a real source the patcher preserves the original.
    /// </summary>
    private uint GetBlockSizeTempValue() => _gameVersion switch
    {
        GameVersion.CoD4 => CoD4Definition.MemAlloc1Value,
        GameVersion.WaW when _isXbox360 => CoD5Definition.Xbox360MemAlloc1Value,
        GameVersion.WaW => CoD5Definition.MemAlloc1Value,
        GameVersion.MW2 => MW2Definition.MemAlloc1Value,
        _ => 0u,
    };

    /// <summary>
    /// BlockSizeVertex (MemAlloc2). See <see cref="GetBlockSizeTempValue"/>.
    /// Real MW2 PC zones (patch_mp, mp_rust_load, etc.) consistently use 0 here —
    /// vertex memory isn't pre-allocated by the PC engine the way it is on PS3.
    /// Using PS3's 0x1000 default on PC would over-reserve vertex memory the zone
    /// doesn't need.
    /// </summary>
    private uint GetBlockSizeVertexValue() => _gameVersion switch
    {
        GameVersion.CoD4 => CoD4Definition.MemAlloc2Value,
        GameVersion.WaW when _isXbox360 => CoD5Definition.Xbox360MemAlloc2Value,
        GameVersion.WaW => CoD5Definition.MemAlloc2Value,
        GameVersion.MW2 when _isPC => 0u,
        GameVersion.MW2 => MW2Definition.MemAlloc2Value,
        _ => 0u,
    };

    /// <summary>
    /// Writes a 32-bit unsigned int at the right endianness for the target platform.
    /// PC zones are little-endian; PS3 / Xbox 360 / Wii are big-endian.
    /// </summary>
    private void WriteUInt32(byte[] data, int offset, uint value)
    {
        if (_isPC)
        {
            data[offset]     = (byte)(value & 0xFF);
            data[offset + 1] = (byte)((value >> 8) & 0xFF);
            data[offset + 2] = (byte)((value >> 16) & 0xFF);
            data[offset + 3] = (byte)((value >> 24) & 0xFF);
        }
        else
        {
            data[offset]     = (byte)((value >> 24) & 0xFF);
            data[offset + 1] = (byte)((value >> 16) & 0xFF);
            data[offset + 2] = (byte)((value >> 8) & 0xFF);
            data[offset + 3] = (byte)(value & 0xFF);
        }
    }

    /// <summary>
    /// Builds the asset table section. Each entry is 8 bytes: [type word][ptr placeholder].
    /// The type word is a 32-bit int whose low byte holds the per-platform asset type ID, so
    /// it is BE on consoles/Wii (type byte at offset 3) and LE on PC (type byte at offset 0).
    /// Asset type IDs themselves shift between platforms — handled by FastFileConstants.
    /// </summary>
    private byte[] BuildAssetTableSection()
    {
        byte rawFileType = FastFileConstants.GetRawFileAssetType(_gameVersion, _isXbox360, _isPC, _isWii);
        byte localizeType = FastFileConstants.GetLocalizeAssetType(_gameVersion, _isXbox360, _isPC, _isWii);

        int totalEntries = _rawFiles.Count + _localizedEntries.Count + 1; // +1 trailing rawfile
        var table = new byte[totalEntries * 8];
        int offset = 0;

        foreach (var _ in _rawFiles)
        {
            WriteAssetTableEntry(table, offset, rawFileType);
            offset += 8;
        }
        foreach (var _ in _localizedEntries)
        {
            WriteAssetTableEntry(table, offset, localizeType);
            offset += 8;
        }
        // Trailing final rawfile entry — required by the format; without it the engine hangs.
        WriteAssetTableEntry(table, offset, rawFileType);

        _assetTableSize = table.Length;
        return table;
    }

    private void WriteAssetTableEntry(byte[] table, int offset, byte typeId)
    {
        WriteUInt32(table, offset, typeId);          // type word @ +0
        WriteUInt32(table, offset + 4, 0xFFFFFFFF);  // ptr placeholder @ +4
    }

    /// <summary>
    /// Builds the raw files section. Layout differs by game:
    ///   CoD4/WaW (all platforms): [FFFFFFFF][size BE][FFFFFFFF][name\0][data][\0]
    ///   MW2 PS3/Xbox 360 first:   [FFFFFFFF][FFFFFFFF][compressedLen BE][len BE][FFFFFFFF][name\0][zlib data]
    ///   MW2 PS3/Xbox 360 rest:    [FFFFFFFF][compressedLen BE][len BE][FFFFFFFF][name\0][zlib data]
    ///   MW2 PC (all entries):     [FFFFFFFF][compressedLen LE][len LE][FFFFFFFF][name\0][zlib data]
    /// MW2 entries are packed tightly with no trailing null between them.
    /// </summary>
    private byte[] BuildRawFilesSection()
    {
        var section = new List<byte>();

        if (_gameVersion == GameVersion.MW2)
            BuildMw2RawFilesSection(section);
        else
            BuildStandardRawFilesSection(section);

        _rawFilesSize = section.Count;
        return section.ToArray();
    }

    private static readonly byte[] FfMarker = { 0xFF, 0xFF, 0xFF, 0xFF };

    private void BuildStandardRawFilesSection(List<byte> section)
    {
        foreach (var rawFile in _rawFiles)
        {
            section.AddRange(FfMarker);
            section.AddRange(GetBigEndianBytes(rawFile.Data.Length));  // BE on all CoD4/WaW platforms
            section.AddRange(FfMarker);
            section.AddRange(Encoding.ASCII.GetBytes(rawFile.Name));
            section.Add(0x00);
            section.AddRange(rawFile.Data);
            section.Add(0x00);
        }
    }

    private void BuildMw2RawFilesSection(List<byte> section)
    {
        bool isFirst = true;
        foreach (var rawFile in _rawFiles)
        {
            byte[] compressed = CompressionHelper.CompressZlib(rawFile.Data);

            // First file on PS3/Xbox 360 carries an extra leading FF marker (20-byte header).
            // MW2 PC does not appear to use this variant — all entries are 16-byte.
            if (isFirst && !_isPC)
                section.AddRange(FfMarker);

            section.AddRange(FfMarker);
            AppendUInt32(section, (uint)compressed.Length);   // compressedLen
            AppendUInt32(section, (uint)rawFile.Data.Length); // uncompressedLen
            section.AddRange(FfMarker);
            section.AddRange(Encoding.ASCII.GetBytes(rawFile.Name));
            section.Add(0x00);
            section.AddRange(compressed);
            // No trailing null — MW2 entries are packed tightly.

            isFirst = false;
        }
    }

    /// <summary>
    /// Appends a 32-bit unsigned int at the platform's endianness. Same rule as
    /// <see cref="WriteUInt32"/> but for List&lt;byte&gt; targets.
    /// </summary>
    private void AppendUInt32(List<byte> dst, uint value)
    {
        if (_isPC)
        {
            dst.Add((byte)(value & 0xFF));
            dst.Add((byte)((value >> 8) & 0xFF));
            dst.Add((byte)((value >> 16) & 0xFF));
            dst.Add((byte)((value >> 24) & 0xFF));
        }
        else
        {
            dst.Add((byte)((value >> 24) & 0xFF));
            dst.Add((byte)((value >> 16) & 0xFF));
            dst.Add((byte)((value >> 8) & 0xFF));
            dst.Add((byte)(value & 0xFF));
        }
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
