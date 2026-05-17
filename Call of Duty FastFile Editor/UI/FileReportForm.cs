using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Call_of_Duty_FastFile_Editor.Models;
using FastFileLib;
using FastFileLib.GameDefinitions;

namespace Call_of_Duty_FastFile_Editor.UI
{
    /// <summary>
    /// A structured report viewer that walks the FastFile + Zone byte layout and
    /// presents each section (header, asset pool, raw files, footer, etc.) with
    /// parsed fields, address ranges, and a hex/ASCII dump - similar to the
    /// PS2 MET File Editor "report" view requested in issue #60.
    /// </summary>
    public partial class FileReportForm : Form
    {
        private readonly FastFile _fastFile;
        private readonly byte[] _ffBytes;          // raw FastFile bytes from disk (may be null if loaded from .zone)
        private readonly byte[] _zoneBytes;        // decompressed zone bytes
        private readonly List<RawFileNode> _rawFiles;
        private readonly List<LocalizedEntry> _localized;
        private readonly bool _isMW2;
        private readonly bool _isXbox360;
        private readonly bool _isPC;

        private Section _selectedSection;

        public FileReportForm(FastFile fastFile,
                              List<RawFileNode> rawFiles,
                              List<LocalizedEntry> localizedEntries)
        {
            InitializeComponent();

            _fastFile = fastFile ?? throw new ArgumentNullException(nameof(fastFile));
            _rawFiles = rawFiles ?? new List<RawFileNode>();
            _localized = localizedEntries ?? new List<LocalizedEntry>();

            _isMW2 = fastFile.IsMW2File;
            _isXbox360 = fastFile.IsXbox360;
            _isPC = fastFile.IsPC;

            // Load raw FF bytes if the source file exists on disk
            try
            {
                if (!_fastFile.IsFromZoneFile && File.Exists(_fastFile.FfFilePath))
                    _ffBytes = File.ReadAllBytes(_fastFile.FfFilePath);
            }
            catch
            {
                _ffBytes = null;
            }

            _zoneBytes = _fastFile.OpenedFastFileZone?.Data ?? Array.Empty<byte>();

            PopulateTopBar();
            BuildSectionsTree();
        }

        private void PopulateTopBar()
        {
            fileNameLabel.Text = $"File: {_fastFile.FastFileName}";

            string ffSize = _ffBytes != null
                ? $"FF size: 0x{_ffBytes.Length:X} ({_ffBytes.Length:N0} bytes)"
                : "FF size: (loaded from .zone)";
            ffSizeLabel.Text = ffSize;

            zoneSizeLabel.Text = _zoneBytes.Length > 0
                ? $"Zone size: 0x{_zoneBytes.Length:X} ({_zoneBytes.Length:N0} bytes)"
                : "Zone size: -";
        }

        // -----------------------------------------------------------------
        //  Tree building
        // -----------------------------------------------------------------

        private void BuildSectionsTree()
        {
            sectionsTree.BeginUpdate();
            sectionsTree.Nodes.Clear();

            // ----- FastFile-level sections -----
            if (_ffBytes != null && _ffBytes.Length >= 12)
            {
                var ffRoot = new TreeNode("FastFile (compressed)")
                {
                    Tag = BuildFastFileSummarySection()
                };
                ffRoot.Nodes.Add(NewSectionNode("FastFile Header", BuildFastFileHeaderSection()));

                if (_isMW2)
                {
                    var mw2Ext = BuildMW2ExtendedHeaderSection();
                    if (mw2Ext != null)
                        ffRoot.Nodes.Add(NewSectionNode("MW2 Extended Header", mw2Ext));
                }

                var streamHdr = BuildXbox360StreamingHeaderSection();
                if (streamHdr != null)
                    ffRoot.Nodes.Add(NewSectionNode("Xbox 360 Streaming Header + Hash Table", streamHdr));

                ffRoot.Nodes.Add(NewSectionNode("Compressed Data", BuildCompressedDataSection()));
                ffRoot.Expand();
                sectionsTree.Nodes.Add(ffRoot);
            }

            // ----- Zone-level sections -----
            if (_zoneBytes.Length > 0)
            {
                var zoneRoot = new TreeNode("Zone (decompressed)")
                {
                    Tag = BuildZoneSummarySection()
                };
                zoneRoot.Nodes.Add(NewSectionNode("Zone Header", BuildZoneHeaderSection()));
                zoneRoot.Nodes.Add(NewSectionNode("Asset Pool", BuildAssetPoolSection()));

                if (_rawFiles.Count > 0)
                {
                    var rawRoot = new TreeNode($"Raw Files ({_rawFiles.Count})")
                    {
                        Tag = BuildRawFilesSummarySection()
                    };
                    foreach (var rf in _rawFiles.OrderBy(r => r.StartOfFileHeader))
                    {
                        string display = string.IsNullOrEmpty(rf.FileName) ? "(unnamed)" : rf.FileName;
                        rawRoot.Nodes.Add(NewSectionNode(display, BuildRawFileSection(rf)));
                    }
                    zoneRoot.Nodes.Add(rawRoot);
                }

                if (_localized.Count > 0)
                {
                    var locRoot = new TreeNode($"Localized Strings ({_localized.Count})")
                    {
                        Tag = BuildLocalizedSummarySection()
                    };
                    // Cap drilldown to a reasonable number to keep tree fast
                    foreach (var entry in _localized.Take(500))
                    {
                        string display = string.IsNullOrEmpty(entry.Key) ? "(unnamed)" : entry.Key;
                        locRoot.Nodes.Add(NewSectionNode(display, BuildLocalizedEntrySection(entry)));
                    }
                    if (_localized.Count > 500)
                    {
                        locRoot.Nodes.Add(new TreeNode($"... {_localized.Count - 500} more not shown"));
                    }
                    zoneRoot.Nodes.Add(locRoot);
                }

                var footer = BuildFooterSection();
                if (footer != null)
                    zoneRoot.Nodes.Add(NewSectionNode("Footer", footer));

                zoneRoot.Expand();
                sectionsTree.Nodes.Add(zoneRoot);
            }

            sectionsTree.EndUpdate();

            // Auto-select first node
            if (sectionsTree.Nodes.Count > 0)
                sectionsTree.SelectedNode = sectionsTree.Nodes[0];
        }

        private static TreeNode NewSectionNode(string text, Section section)
        {
            return new TreeNode(text) { Tag = section };
        }

        private void SectionsTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node?.Tag is Section section)
            {
                _selectedSection = section;
                RenderReport(section);
                RenderSummary(section);
                statusLabel.Text = $"{section.Title}  -  0x{section.StartOffset:X} .. 0x{section.EndOffset:X}  ({section.Length} bytes)";
            }
            else
            {
                reportRichTextBox.Clear();
                summaryTextBox.Clear();
                statusLabel.Text = "Ready";
            }
        }

        // -----------------------------------------------------------------
        //  Section builders
        // -----------------------------------------------------------------

        private Section BuildFastFileSummarySection()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"FastFile: {_fastFile.FastFileName}");
            sb.AppendLine($"Full Path: {_fastFile.FfFilePath}");
            sb.AppendLine();
            sb.AppendLine($"Game: {(_fastFile.IsCod4File ? "CoD4" : _fastFile.IsCod5File ? "WaW" : _fastFile.IsMW2File ? "MW2" : "Unknown")}");
            sb.AppendLine($"Platform: {_fastFile.Platform}");
            sb.AppendLine($"Magic: {_fastFile.FastFileMagic}");
            sb.AppendLine($"Version: 0x{_fastFile.GameVersion:X} ({_fastFile.GameVersion})");
            sb.AppendLine($"Signed: {(_fastFile.IsSigned ? "Yes" : "No")}");
            sb.AppendLine($"Total file size: {FastFileInfo.FormatFileSize(_fastFile.FileLength)}");

            return new Section
            {
                Title = "FastFile Summary",
                Data = _ffBytes,
                StartOffset = 0,
                Length = _ffBytes?.Length ?? 0,
                DumpStart = 0,
                DumpLength = 0,   // no hex dump for summary
                Fields = sb.ToString(),
                Summary = sb.ToString()
            };
        }

        private Section BuildFastFileHeaderSection()
        {
            var fields = new StringBuilder();
            string magic = Encoding.ASCII.GetString(_ffBytes, 0, 8);
            fields.AppendLine($"Magic            : '{magic}'  (offset 0x00, 8 bytes)");
            fields.AppendLine($"Version (raw)    : {ToHex(_ffBytes, 8, 4)}  (offset 0x08, 4 bytes)");
            fields.AppendLine($"Version (BE u32) : 0x{ReadU32BE(_ffBytes, 8):X8}");
            fields.AppendLine($"Version (LE u32) : 0x{ReadU32LE(_ffBytes, 8):X8}");
            fields.AppendLine();
            fields.AppendLine($"Detected game    : {(_fastFile.IsCod4File ? "CoD4" : _fastFile.IsCod5File ? "WaW" : _fastFile.IsMW2File ? "MW2" : "Unknown")}");
            fields.AppendLine($"Detected platform: {_fastFile.Platform}");
            fields.AppendLine($"Signed           : {(_fastFile.IsSigned ? "Yes (IWff0100)" : "No (IWffu100)")}");

            var summary = new StringBuilder();
            summary.AppendLine($"Header starts at address: 0 (0x00000000)");
            summary.AppendLine($"Header ends at address: 11 (0x0000000B)");
            summary.AppendLine($"Length of the header: 12 (0x0C)");
            summary.AppendLine($"Magic: {magic}");
            summary.AppendLine($"Version: 0x{_fastFile.GameVersion:X}");

            return new Section
            {
                Title = "FastFile Header",
                Data = _ffBytes,
                StartOffset = 0,
                Length = 12,
                DumpStart = 0,
                DumpLength = 12,
                Fields = fields.ToString(),
                Summary = summary.ToString()
            };
        }

        private Section BuildMW2ExtendedHeaderSection()
        {
            if (_ffBytes == null || _ffBytes.Length < 12 + 25)
                return null;

            // Structure: allowOnlineUpdate(1) + fileCreationTime(8) + region(4) + entryCount(4) + entries(entryCount*0x14) + fileSizes(8)
            int pos = 12;
            byte allowOnlineUpdate = _ffBytes[pos];
            byte[] creationTime = new byte[8];
            Array.Copy(_ffBytes, pos + 1, creationTime, 0, 8);
            uint region = ReadU32BE(_ffBytes, pos + 9);
            uint entryCount = ReadU32BE(_ffBytes, pos + 13);

            int entriesSize = 0;
            if (entryCount < 10000)  // sanity check
                entriesSize = (int)entryCount * 0x14;

            int fileSizesOffset = pos + 17 + entriesSize;
            int extHeaderLen = 17 + entriesSize + 8;

            if (fileSizesOffset + 8 > _ffBytes.Length)
                return null;

            uint fileSize = ReadU32BE(_ffBytes, fileSizesOffset);
            uint maxFileSize = ReadU32BE(_ffBytes, fileSizesOffset + 4);

            var fields = new StringBuilder();
            fields.AppendLine($"allowOnlineUpdate : 0x{allowOnlineUpdate:X2}  (offset 0x{pos:X})");
            fields.AppendLine($"fileCreationTime  : {ToHex(creationTime, 0, 8)}  (offset 0x{pos + 1:X})");
            fields.AppendLine($"region            : 0x{region:X8} ({DecodeRegion(region)})  (offset 0x{pos + 9:X})");
            fields.AppendLine($"entryCount        : {entryCount}  (offset 0x{pos + 13:X})");
            if (entryCount > 0)
                fields.AppendLine($"entries           : {entriesSize} bytes  (offset 0x{pos + 17:X})");
            fields.AppendLine($"fileSize          : 0x{fileSize:X8} ({fileSize:N0} bytes)  (offset 0x{fileSizesOffset:X})");
            fields.AppendLine($"maxFileSize       : 0x{maxFileSize:X8} ({maxFileSize:N0} bytes)  (offset 0x{fileSizesOffset + 4:X})");

            var summary = new StringBuilder();
            summary.AppendLine($"Extended header starts at: 0x{pos:X}");
            summary.AppendLine($"Extended header ends at  : 0x{pos + extHeaderLen - 1:X}");
            summary.AppendLine($"Length                   : {extHeaderLen} (0x{extHeaderLen:X})");
            summary.AppendLine($"Entry count              : {entryCount}");
            summary.AppendLine($"Stored fileSize          : 0x{fileSize:X} bytes");
            summary.AppendLine($"Actual FF size on disk   : 0x{_ffBytes.Length:X} bytes");
            if (fileSize != _ffBytes.Length && fileSize != 0)
                summary.AppendLine("WARNING: stored fileSize does not match actual FF size on disk.");

            return new Section
            {
                Title = "MW2 Extended Header",
                Data = _ffBytes,
                StartOffset = pos,
                Length = extHeaderLen,
                DumpStart = pos,
                DumpLength = Math.Min(extHeaderLen, 64),
                Fields = fields.ToString(),
                Summary = summary.ToString()
            };
        }

        private Section BuildXbox360StreamingHeaderSection()
        {
            if (_ffBytes == null || _ffBytes.Length < FastFileConstants.Xbox360SignedZlibStart)
                return null;

            // Check IWffs100 magic at 0x0C
            string innerMagic = Encoding.ASCII.GetString(_ffBytes, 0x0C, 8);
            if (innerMagic != FastFileConstants.StreamingHeader)
                return null;

            int hashStart = FastFileConstants.Xbox360SignedHashTableStart;
            int hashEnd = FastFileConstants.Xbox360SignedHashTableEnd;
            int authEnd = FastFileConstants.Xbox360SignedAuthDataEnd;
            int streamStart = FastFileConstants.Xbox360SignedZlibStart;
            int totalLen = streamStart - 0x0C;

            var fields = new StringBuilder();
            fields.AppendLine($"Streaming magic   : '{innerMagic}'  (offset 0x0C, 8 bytes)");
            fields.AppendLine($"Hash table        : 0x{hashStart:X} .. 0x{hashEnd:X}  ({hashEnd - hashStart} bytes, SHA-1 chunk hashes)");
            fields.AppendLine($"Auth data         : 0x{hashEnd:X} .. 0x{authEnd:X}  ({authEnd - hashEnd} bytes)");
            fields.AppendLine($"Compressed stream : starts at 0x{streamStart:X}");

            var summary = new StringBuilder();
            summary.AppendLine($"Xbox 360 signed streaming layout detected.");
            summary.AppendLine($"Inner streaming magic at: 0x0C");
            summary.AppendLine($"Hash + auth blob       : 0x14 .. 0x400C ({authEnd - hashStart} bytes)");
            summary.AppendLine($"Compressed stream start: 0x{streamStart:X}");

            return new Section
            {
                Title = "Xbox 360 Streaming Header + Hash Table",
                Data = _ffBytes,
                StartOffset = 0x0C,
                Length = totalLen,
                DumpStart = 0x0C,
                DumpLength = 32,  // show streaming magic + first few hash bytes
                Fields = fields.ToString(),
                Summary = summary.ToString()
            };
        }

        private Section BuildCompressedDataSection()
        {
            int start;
            if (_isMW2 && _ffBytes.Length > 12 + 25)
                start = 12 + 25;  // approximate (varies with entryCount)
            else if (_isXbox360 && _ffBytes.Length > FastFileConstants.Xbox360SignedZlibStart
                     && Encoding.ASCII.GetString(_ffBytes, 0x0C, 8) == FastFileConstants.StreamingHeader)
                start = FastFileConstants.Xbox360SignedZlibStart;
            else
                start = 12;

            int len = Math.Max(0, _ffBytes.Length - start);

            var fields = new StringBuilder();
            fields.AppendLine($"Compressed stream starts at: 0x{start:X}");
            fields.AppendLine($"Compressed stream length    : {len:N0} bytes (0x{len:X})");
            if (_zoneBytes.Length > 0 && len > 0)
            {
                double ratio = (double)len / _zoneBytes.Length;
                fields.AppendLine($"Decompressed (zone) size    : {_zoneBytes.Length:N0} bytes (0x{_zoneBytes.Length:X})");
                fields.AppendLine($"Compression ratio           : {ratio * 100:0.0}%");
            }
            // Check end marker for block-format files
            if (_ffBytes.Length >= 2)
            {
                byte b0 = _ffBytes[_ffBytes.Length - 2];
                byte b1 = _ffBytes[_ffBytes.Length - 1];
                fields.AppendLine($"Last two bytes              : 0x{b0:X2} 0x{b1:X2} {(b0 == 0x00 && b1 == 0x01 ? "(block-format end marker)" : "")}");
            }

            return new Section
            {
                Title = "Compressed Data",
                Data = _ffBytes,
                StartOffset = start,
                Length = len,
                DumpStart = start,
                DumpLength = Math.Min(len, 48),
                Fields = fields.ToString(),
                Summary = fields.ToString()
            };
        }

        private Section BuildZoneSummarySection()
        {
            var zone = _fastFile.OpenedFastFileZone;
            var sb = new StringBuilder();
            sb.AppendLine($"Zone size      : {_zoneBytes.Length:N0} bytes (0x{_zoneBytes.Length:X})");
            sb.AppendLine($"Asset count    : {zone.AssetCount}");
            sb.AppendLine($"Raw files      : {_rawFiles.Count}");
            sb.AppendLine($"Localized      : {_localized.Count}");
            sb.AppendLine();
            sb.AppendLine($"BlockSizeTemp  : 0x{zone.BlockSizeTemp:X8}");
            sb.AppendLine($"BlockSizeVertex: 0x{zone.BlockSizeVertex:X8}");
            sb.AppendLine($"BlockSizeLarge : 0x{zone.BlockSizeLarge:X8}");

            return new Section
            {
                Title = "Zone Summary",
                Data = _zoneBytes,
                StartOffset = 0,
                Length = _zoneBytes.Length,
                DumpStart = 0,
                DumpLength = 0,
                Fields = sb.ToString(),
                Summary = sb.ToString()
            };
        }

        private Section BuildZoneHeaderSection()
        {
            var zone = _fastFile.OpenedFastFileZone;
            int hdrSize = FastFileConstants.GetZoneHeaderSize(
                _fastFile.IsCod4File ? GameVersion.CoD4 :
                _fastFile.IsCod5File ? GameVersion.WaW :
                _fastFile.IsMW2File ? GameVersion.MW2 : GameVersion.Unknown,
                _isXbox360, _isPC);

            var fields = new StringBuilder();
            fields.AppendLine("XFile (memory block allocation):");
            fields.AppendLine($"  0x00  ZoneSize         : 0x{zone.ZoneSize:X8} ({zone.ZoneSize:N0})");
            fields.AppendLine($"  0x04  ExternalSize     : 0x{zone.ExternalSize:X8}");
            fields.AppendLine($"  0x08  BlockSizeTemp    : 0x{zone.BlockSizeTemp:X8}");
            fields.AppendLine($"  0x0C  BlockSizePhysical: 0x{zone.BlockSizePhysical:X8}");
            fields.AppendLine($"  0x10  BlockSizeRuntime : 0x{zone.BlockSizeRuntime:X8}");
            fields.AppendLine($"  0x14  BlockSizeVirtual : 0x{zone.BlockSizeVirtual:X8}");
            fields.AppendLine($"  0x18  BlockSizeLarge   : 0x{zone.BlockSizeLarge:X8}");
            fields.AppendLine($"  0x1C  BlockSizeCallback: 0x{zone.BlockSizeCallback:X8}");
            if (hdrSize >= 0x34)
                fields.AppendLine($"  0x20  BlockSizeVertex  : 0x{zone.BlockSizeVertex:X8}");
            fields.AppendLine();
            fields.AppendLine("XAssetList:");
            int slOffset = FastFileConstants.GetScriptStringCountOffset(
                _fastFile.IsCod4File ? GameVersion.CoD4 :
                _fastFile.IsCod5File ? GameVersion.WaW :
                _fastFile.IsMW2File ? GameVersion.MW2 : GameVersion.Unknown,
                _isXbox360, _isPC);
            fields.AppendLine($"  0x{slOffset:X2}  ScriptStringCount: {zone.ScriptStringCount}");
            fields.AppendLine($"  0x{slOffset + 4:X2}  ScriptStringsPtr : 0x{zone.ScriptStringsPtr:X8}");
            fields.AppendLine($"  0x{slOffset + 8:X2}  AssetCount       : {zone.AssetCount}");
            fields.AppendLine($"  0x{slOffset + 12:X2}  AssetsPtr        : 0x{zone.AssetsPtr:X8}");

            var summary = new StringBuilder();
            summary.AppendLine($"Zone header size: {hdrSize} bytes (0x{hdrSize:X})");
            summary.AppendLine($"Asset count: {zone.AssetCount}");
            summary.AppendLine($"Script string count: {zone.ScriptStringCount}");

            // MemAlloc validation
            uint expectedMemAlloc1 = _fastFile.IsCod4File ? CoD4Definition.MemAlloc1Value
                                   : _fastFile.IsCod5File ? (_isXbox360 ? CoD5Definition.Xbox360MemAlloc1Value : CoD5Definition.MemAlloc1Value)
                                   : _fastFile.IsMW2File ? MW2Definition.MemAlloc1Value
                                   : 0;
            if (expectedMemAlloc1 != 0 && zone.BlockSizeTemp != expectedMemAlloc1)
            {
                summary.AppendLine();
                summary.AppendLine($"WARNING: BlockSizeTemp 0x{zone.BlockSizeTemp:X} != expected 0x{expectedMemAlloc1:X}");
            }

            return new Section
            {
                Title = "Zone Header",
                Data = _zoneBytes,
                StartOffset = 0,
                Length = hdrSize,
                DumpStart = 0,
                DumpLength = hdrSize,
                Fields = fields.ToString(),
                Summary = summary.ToString()
            };
        }

        private Section BuildAssetPoolSection()
        {
            var zone = _fastFile.OpenedFastFileZone;
            int start = zone.AssetPoolStartOffset;
            int end = zone.AssetPoolEndOffset;
            int len = Math.Max(0, end - start);

            var fields = new StringBuilder();
            fields.AppendLine($"Asset pool starts: 0x{start:X}");
            fields.AppendLine($"Asset pool ends  : 0x{end:X}");
            fields.AppendLine($"Asset count      : {zone.AssetCount}  ({len / 8} entries x 8 bytes)");
            fields.AppendLine();

            // Asset entries are 8 bytes. Two layouts exist in the wild:
            //   [type][ptr]  = 00 00 00 XX FF FF FF FF  (CoD4/WaW, MW2 retail)  -> type at +3
            //   [ptr][type]  = FF FF FF FF 00 00 00 XX  (MW2 PS3 mod files)     -> type at +7
            // Pick the layout by looking at the first entry: whichever side holds 0xFF FF FF FF
            // is the pointer half, so the other side holds the type byte.
            int safeLen = Math.Min(len, _zoneBytes.Length - start);
            int typeOffsetInEntry = 3; // default
            if (safeLen >= 8)
            {
                bool leftIsPtr = _zoneBytes[start] == 0xFF && _zoneBytes[start + 1] == 0xFF
                              && _zoneBytes[start + 2] == 0xFF && _zoneBytes[start + 3] == 0xFF;
                bool rightIsPtr = _zoneBytes[start + 4] == 0xFF && _zoneBytes[start + 5] == 0xFF
                               && _zoneBytes[start + 6] == 0xFF && _zoneBytes[start + 7] == 0xFF;
                if (leftIsPtr && !rightIsPtr)
                    typeOffsetInEntry = 7;
                else if (!leftIsPtr && rightIsPtr)
                    typeOffsetInEntry = 3;
            }

            var typeCounts = new Dictionary<byte, int>();
            for (int i = 0; i + 8 <= safeLen; i += 8)
            {
                byte t = _zoneBytes[start + i + typeOffsetInEntry];
                if (!typeCounts.ContainsKey(t)) typeCounts[t] = 0;
                typeCounts[t]++;
            }
            fields.AppendLine($"Entry layout: {(typeOffsetInEntry == 3 ? "[type][ptr]" : "[ptr][type]")}  (type byte at +{typeOffsetInEntry})");
            fields.AppendLine();
            fields.AppendLine("Type ID counts:");
            foreach (var kv in typeCounts.OrderByDescending(kv => kv.Value))
            {
                string name = ResolveAssetTypeName(kv.Key);
                fields.AppendLine($"  0x{kv.Key:X2}  {name,-18}  {kv.Value}");
            }

            var summary = new StringBuilder();
            summary.AppendLine($"Asset pool range : 0x{start:X} .. 0x{end:X}");
            summary.AppendLine($"Total entries    : {len / 8}");
            summary.AppendLine($"Total bytes      : {len:N0} (0x{len:X})");

            return new Section
            {
                Title = "Asset Pool",
                Data = _zoneBytes,
                StartOffset = start,
                Length = len,
                DumpStart = start,
                DumpLength = Math.Min(len, 16 * 8),  // up to 16 entries
                Fields = fields.ToString(),
                Summary = summary.ToString()
            };
        }

        private Section BuildRawFilesSummarySection()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Total raw files: {_rawFiles.Count}");
            sb.AppendLine();
            long totalBytes = _rawFiles.Sum(r => (long)r.MaxSize);
            sb.AppendLine($"Combined size  : {totalBytes:N0} bytes (0x{totalBytes:X})");
            int firstOffset = _rawFiles.Count > 0 ? _rawFiles.Min(r => r.StartOfFileHeader) : 0;
            int lastEnd = _rawFiles.Count > 0 ? _rawFiles.Max(r => r.RawFileEndPosition) : 0;
            sb.AppendLine($"Spans          : 0x{firstOffset:X} .. 0x{lastEnd:X}");
            sb.AppendLine();
            sb.AppendLine("Largest files:");
            foreach (var rf in _rawFiles.OrderByDescending(r => r.MaxSize).Take(10))
                sb.AppendLine($"  {rf.MaxSize,10:N0}  {rf.FileName}");

            return new Section
            {
                Title = "Raw Files Summary",
                Data = _zoneBytes,
                StartOffset = firstOffset,
                Length = Math.Max(0, lastEnd - firstOffset),
                DumpStart = firstOffset,
                DumpLength = 0,
                Fields = sb.ToString(),
                Summary = sb.ToString()
            };
        }

        private Section BuildRawFileSection(RawFileNode rf)
        {
            var fields = new StringBuilder();
            fields.AppendLine($"File             : {rf.FileName}");
            fields.AppendLine();
            fields.AppendLine("FILE ENTRY HEADER");
            fields.AppendLine($"  Location       : 0x{rf.StartOfFileHeader:X} - 0x{rf.EndOfFileHeader:X}");
            fields.AppendLine($"  Length         : {rf.EndOfFileHeader - rf.StartOfFileHeader + 1} bytes (0x{rf.EndOfFileHeader - rf.StartOfFileHeader + 1:X})");
            fields.AppendLine($"  Header size    : {rf.HeaderSize} bytes");
            fields.AppendLine();
            fields.AppendLine("FILE DATA");
            fields.AppendLine($"  Location       : 0x{rf.CodeStartPosition:X} - 0x{rf.CodeEndPosition:X}");
            fields.AppendLine($"  Size           : {rf.MaxSize} bytes (0x{rf.MaxSize:X})");
            if (rf.IsCompressed)
                fields.AppendLine($"  Compressed size: {rf.CompressedSize} bytes (zlib)");

            var summary = new StringBuilder();
            summary.AppendLine($"Header starts at address: {rf.StartOfFileHeader} (0x{rf.StartOfFileHeader:X})");
            summary.AppendLine($"Header ends at address: {rf.EndOfFileHeader} (0x{rf.EndOfFileHeader:X})");
            summary.AppendLine($"Length of the header: {rf.EndOfFileHeader - rf.StartOfFileHeader + 1} (0x{rf.EndOfFileHeader - rf.StartOfFileHeader + 1:X})");
            summary.AppendLine($"Length of the name: {rf.FileName?.Length ?? 0} (0x{rf.FileName?.Length ?? 0:X})");
            summary.AppendLine($"Path: {rf.FileName}");
            summary.AppendLine($"Offset: {rf.CodeStartPosition} (0x{rf.CodeStartPosition:X})");
            summary.AppendLine($"Size: {rf.MaxSize} (0x{rf.MaxSize:X})");
            summary.AppendLine($"Data spans from 0x{rf.CodeStartPosition:X} to 0x{rf.CodeEndPosition:X}");

            // Show header bytes + first slice of data
            int dumpStart = rf.StartOfFileHeader;
            int dumpLen = Math.Min(rf.RawFileEndPosition - rf.StartOfFileHeader, 128);

            return new Section
            {
                Title = $"Raw File: {rf.FileName}",
                Data = _zoneBytes,
                StartOffset = rf.StartOfFileHeader,
                Length = rf.RawFileEndPosition - rf.StartOfFileHeader,
                DumpStart = dumpStart,
                DumpLength = dumpLen,
                Fields = fields.ToString(),
                Summary = summary.ToString()
            };
        }

        private Section BuildLocalizedSummarySection()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Total localized entries: {_localized.Count}");
            int firstOffset = _localized.Count > 0 ? _localized.Min(l => l.StartOfFileHeader) : 0;
            int lastEnd = _localized.Count > 0 ? _localized.Max(l => l.EndOfFileHeader) : 0;
            sb.AppendLine($"Spans: 0x{firstOffset:X} .. 0x{lastEnd:X}");
            return new Section
            {
                Title = "Localized Strings Summary",
                Data = _zoneBytes,
                StartOffset = firstOffset,
                Length = Math.Max(0, lastEnd - firstOffset),
                DumpStart = firstOffset,
                DumpLength = 0,
                Fields = sb.ToString(),
                Summary = sb.ToString()
            };
        }

        private Section BuildLocalizedEntrySection(LocalizedEntry entry)
        {
            var fields = new StringBuilder();
            fields.AppendLine($"Key            : {entry.Key}");
            fields.AppendLine($"Localized text : {entry.LocalizedText}");
            fields.AppendLine();
            fields.AppendLine($"Header location: 0x{entry.StartOfFileHeader:X} - 0x{entry.EndOfFileHeader:X}");
            fields.AppendLine($"Data location  : 0x{entry.StartOfFileData:X} - 0x{entry.EndOfFileData:X}");

            var summary = new StringBuilder();
            summary.AppendLine($"Key: {entry.Key}");
            summary.AppendLine($"Length of value: {entry.TextBytes.Length} (0x{entry.TextBytes.Length:X})");
            summary.AppendLine($"Length of key  : {entry.KeyBytes.Length} (0x{entry.KeyBytes.Length:X})");
            summary.AppendLine($"Entry starts at: 0x{entry.StartOfFileHeader:X}");
            summary.AppendLine($"Entry ends at  : 0x{entry.EndOfFileHeader:X}");

            int dumpStart = entry.StartOfFileHeader;
            int dumpLen = Math.Min(entry.EndOfFileHeader - entry.StartOfFileHeader + 1, 128);

            return new Section
            {
                Title = $"Localized: {entry.Key}",
                Data = _zoneBytes,
                StartOffset = entry.StartOfFileHeader,
                Length = entry.EndOfFileHeader - entry.StartOfFileHeader + 1,
                DumpStart = dumpStart,
                DumpLength = dumpLen,
                Fields = fields.ToString(),
                Summary = summary.ToString()
            };
        }

        private Section BuildFooterSection()
        {
            // The footer is after the last raw file/localize entry. We look from the end
            // of the largest known asset position to the zone padding.
            int lastAssetEnd = 0;
            if (_rawFiles.Count > 0)
                lastAssetEnd = Math.Max(lastAssetEnd, _rawFiles.Max(r => r.RawFileEndPosition));
            if (_localized.Count > 0)
                lastAssetEnd = Math.Max(lastAssetEnd, _localized.Max(l => l.EndOfFileHeader + 1));

            if (lastAssetEnd <= 0 || lastAssetEnd >= _zoneBytes.Length)
                return null;

            // Trim trailing zero padding to find the footer's actual end
            int searchEnd = _zoneBytes.Length;
            while (searchEnd > lastAssetEnd && _zoneBytes[searchEnd - 1] == 0)
                searchEnd--;

            int footerLen = Math.Max(0, searchEnd - lastAssetEnd);
            if (footerLen <= 0)
                return null;

            var fields = new StringBuilder();
            fields.AppendLine($"Footer location: 0x{lastAssetEnd:X} - 0x{searchEnd:X}");
            fields.AppendLine($"Footer length  : {footerLen} bytes (0x{footerLen:X})");
            fields.AppendLine($"Padding after  : {_zoneBytes.Length - searchEnd} bytes (zero-fill to 64KB boundary)");

            var summary = new StringBuilder();
            summary.AppendLine($"Footer starts at: 0x{lastAssetEnd:X}");
            summary.AppendLine($"Footer ends at  : 0x{searchEnd:X}");
            summary.AppendLine($"Zone total size : 0x{_zoneBytes.Length:X}");

            return new Section
            {
                Title = "Footer",
                Data = _zoneBytes,
                StartOffset = lastAssetEnd,
                Length = footerLen,
                DumpStart = lastAssetEnd,
                DumpLength = Math.Min(footerLen, 64),
                Fields = fields.ToString(),
                Summary = summary.ToString()
            };
        }

        // -----------------------------------------------------------------
        //  Rendering
        // -----------------------------------------------------------------

        private void RenderReport(Section section)
        {
            reportRichTextBox.SuspendLayout();
            reportRichTextBox.Clear();

            // Title banner
            AppendCentered(section.Title.ToUpperInvariant());
            AppendLine($"  Location: 0x{section.StartOffset:X} - 0x{section.EndOffset:X}");
            AppendLine($"  Size: {section.Length:N0} bytes (0x{section.Length:X})");
            AppendLine();

            // Parsed fields
            if (!string.IsNullOrWhiteSpace(section.Fields))
            {
                AppendLine("FIELDS");
                AppendLine(new string('-', 60));
                AppendText(section.Fields);
                AppendLine();
            }

            // Hex/ASCII dump
            if (section.DumpLength > 0 && section.Data != null)
            {
                AppendLine("HEX / ASCII DUMP");
                AppendLine(new string('-', 60));
                AppendText(BuildHexDump(section.Data, section.DumpStart, section.DumpLength));

                int total = Math.Min(section.Length, section.Data.Length - section.StartOffset);
                if (section.DumpLength < total)
                    AppendLine($"\n  ... showing {section.DumpLength} of {total} bytes");
            }

            reportRichTextBox.SelectionStart = 0;
            reportRichTextBox.ScrollToCaret();
            reportRichTextBox.ResumeLayout();
        }

        private void RenderSummary(Section section)
        {
            summaryTextBox.Text = section.Summary ?? string.Empty;
        }

        private void AppendCentered(string text)
        {
            const int width = 70;
            int pad = Math.Max(0, (width - text.Length) / 2);
            reportRichTextBox.AppendText(new string(' ', pad) + text + Environment.NewLine);
            reportRichTextBox.AppendText(new string('=', width) + Environment.NewLine + Environment.NewLine);
        }

        private void AppendLine(string text = "")
        {
            reportRichTextBox.AppendText(text + Environment.NewLine);
        }

        private void AppendText(string text)
        {
            reportRichTextBox.AppendText(text);
        }

        // -----------------------------------------------------------------
        //  Helpers
        // -----------------------------------------------------------------

        private static string BuildHexDump(byte[] data, int start, int length)
        {
            if (data == null || start >= data.Length)
                return string.Empty;

            length = Math.Min(length, data.Length - start);
            var sb = new StringBuilder();

            // Header
            sb.AppendLine("  Offset(h)   00 01 02 03 04 05 06 07  08 09 0A 0B 0C 0D 0E 0F   ASCII");
            sb.AppendLine();

            for (int i = 0; i < length; i += 16)
            {
                int rowOffset = start + i;
                int rowLen = Math.Min(16, length - i);

                sb.Append($"  {rowOffset:X8}    ");

                // Hex bytes
                for (int j = 0; j < 16; j++)
                {
                    if (j == 8) sb.Append(' ');
                    if (j < rowLen)
                        sb.Append($"{data[rowOffset + j]:X2} ");
                    else
                        sb.Append("   ");
                }

                sb.Append("  ");

                // ASCII
                for (int j = 0; j < rowLen; j++)
                {
                    byte b = data[rowOffset + j];
                    sb.Append(b >= 0x20 && b < 0x7F ? (char)b : '.');
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static string ToHex(byte[] data, int offset, int length)
        {
            if (data == null || offset + length > data.Length) return "(out of range)";
            var sb = new StringBuilder(length * 3);
            for (int i = 0; i < length; i++)
                sb.Append(data[offset + i].ToString("X2") + (i < length - 1 ? " " : ""));
            return sb.ToString();
        }

        private static uint ReadU32BE(byte[] data, int offset)
        {
            return (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
        }

        private static uint ReadU32LE(byte[] data, int offset)
        {
            return (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
        }

        private static string DecodeRegion(uint region)
        {
            return region switch
            {
                0x01 => "English",
                0x02 => "French",
                0x03 => "German",
                0x04 => "Italian",
                0x05 => "Spanish",
                0x06 => "British",
                0x07 => "Russian",
                0x08 => "Polish",
                0x09 => "Korean",
                0x0A => "Taiwanese",
                0x0B => "Japanese",
                0x0C => "Chinese",
                0x0D => "Thai",
                0x0E => "Leet",
                0x0F => "Czech",
                _ => "Unknown"
            };
        }

        private string ResolveAssetTypeName(byte typeId)
        {
            // Resolve based on detected game + platform
            if (_fastFile.IsCod4File)
            {
                if (_isPC && Enum.IsDefined(typeof(CoD4AssetTypePC), (int)typeId))
                    return ((CoD4AssetTypePC)typeId).ToString();
                if (_isXbox360 && Enum.IsDefined(typeof(CoD4AssetTypeXbox360), (int)typeId))
                    return ((CoD4AssetTypeXbox360)typeId).ToString();
                if (Enum.IsDefined(typeof(CoD4AssetTypePS3), (int)typeId))
                    return ((CoD4AssetTypePS3)typeId).ToString();
            }
            else if (_fastFile.IsCod5File)
            {
                if (_isPC && Enum.IsDefined(typeof(CoD5AssetTypePC), (int)typeId))
                    return ((CoD5AssetTypePC)typeId).ToString();
                if (_isXbox360 && Enum.IsDefined(typeof(CoD5AssetTypeXbox360), (int)typeId))
                    return ((CoD5AssetTypeXbox360)typeId).ToString();
                if (Enum.IsDefined(typeof(CoD5AssetTypePS3), (int)typeId))
                    return ((CoD5AssetTypePS3)typeId).ToString();
            }
            else if (_fastFile.IsMW2File)
            {
                if (_isPC && Enum.IsDefined(typeof(MW2AssetTypePC), (int)typeId))
                    return ((MW2AssetTypePC)typeId).ToString();
                if (_isXbox360 && Enum.IsDefined(typeof(MW2AssetTypeXbox360), (int)typeId))
                    return ((MW2AssetTypeXbox360)typeId).ToString();
                if (Enum.IsDefined(typeof(MW2AssetTypePS3), (int)typeId))
                    return ((MW2AssetTypePS3)typeId).ToString();
            }
            return "(unknown)";
        }

        /// <summary>
        /// Represents a logical section of the file for the report view.
        /// </summary>
        private class Section
        {
            public string Title { get; set; }
            public byte[] Data { get; set; }
            public int StartOffset { get; set; }
            public int Length { get; set; }
            public int DumpStart { get; set; }
            public int DumpLength { get; set; }
            public string Fields { get; set; }
            public string Summary { get; set; }

            public int EndOffset => Length > 0 ? StartOffset + Length - 1 : StartOffset;
        }
    }
}
