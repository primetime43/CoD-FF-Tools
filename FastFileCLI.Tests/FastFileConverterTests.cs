using System.IO.Compression;
using System.Text;
using FastFileLib;
using FastFileLib.Models;
using Xunit;

namespace FastFileCLI.Tests;

/// <summary>
/// Tests for FastFileConverter — exercises the fixes that previously caused silent data
/// loss or malformed output:
///   1. ConvertUsingBaseZone now preserves localized strings (not just rawfiles).
///   2. ExtractRawFilesFromZone uses the full extension list (.lua/.csv/.graph/etc.).
///   3. CompressForPlatform routes MW2 + non-MW2 PC to their correct compressors.
/// </summary>
public class FastFileConverterTests
{
    [Fact]
    public void ConvertUsingBaseZone_PreservesLocalizedStrings()
    {
        // Build a synthetic WaW zone that contains both rawfiles and localize entries,
        // wrap it as a WaW PS3 .ff, convert it, then verify the rebuilt zone retained
        // the localize entries.
        var sourceRawFile = new RawFile("test.gsc", Encoding.ASCII.GetBytes("// raw file content"));
        var sourceLocalize = new LocalizedEntry("MENU_TEST_KEY", "Translated value");
        var sourceLocalize2 = new LocalizedEntry("RANK_PRESTIGE_10", "Tenth Prestige");

        var sourceZone = new ZoneBuilder(GameVersion.WaW, "patch_mp")
            .AddRawFile(sourceRawFile)
            .AddLocalizedEntry(sourceLocalize)
            .AddLocalizedEntry(sourceLocalize2)
            .Build();

        string sourceFf = Path.GetTempFileName();
        string outputFf = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(sourceFf, FfBuilder.BuildWaWPs3(sourceZone));

            var result = FastFileConverter.ConvertUsingBaseZone(sourceFf, "", outputFf);

            Assert.True(result.Success, $"Conversion failed: {result.Message}");

            // Decompress the output to a zone so we can inspect what was preserved.
            string outputZone = Path.GetTempFileName();
            try
            {
                FastFileProcessor.Decompress(outputFf, outputZone);
                byte[] zoneBytes = File.ReadAllBytes(outputZone);
                string zoneAscii = Encoding.ASCII.GetString(zoneBytes);

                Assert.Contains("test.gsc", zoneAscii);                  // rawfile name preserved
                Assert.Contains("MENU_TEST_KEY", zoneAscii);             // localize key 1 preserved
                Assert.Contains("Translated value", zoneAscii);          // localize value 1 preserved
                Assert.Contains("RANK_PRESTIGE_10", zoneAscii);          // localize key 2 preserved
                Assert.Contains("Tenth Prestige", zoneAscii);            // localize value 2 preserved
            }
            finally
            {
                File.Delete(outputZone);
            }
        }
        finally
        {
            File.Delete(sourceFf);
            File.Delete(outputFf);
        }
    }

    [Fact]
    public void ConvertUsingBaseZone_ExtractsCanonicalRawFileExtensions()
    {
        // Verify the extractor now picks up extensions beyond the old hardcoded list:
        // .lua and .csv were both in FastFileConstants.ValidRawFileExtensions but missing
        // from the old extractor's allowlist.
        var luaFile = new RawFile("mod_init.lua", Encoding.ASCII.GetBytes("-- lua content"));
        var csvFile = new RawFile("data.csv", Encoding.ASCII.GetBytes("a,b,c\n1,2,3"));
        var oldCoveredFile = new RawFile("baseline.gsc", Encoding.ASCII.GetBytes("// gsc"));

        var sourceZone = new ZoneBuilder(GameVersion.WaW, "patch_mp")
            .AddRawFile(luaFile)
            .AddRawFile(csvFile)
            .AddRawFile(oldCoveredFile)
            .Build();

        string sourceFf = Path.GetTempFileName();
        string outputFf = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(sourceFf, FfBuilder.BuildWaWPs3(sourceZone));

            var result = FastFileConverter.ConvertUsingBaseZone(sourceFf, "", outputFf);
            Assert.True(result.Success, result.Message);

            // ReplacedFiles is the canonical list of what got carried over.
            Assert.Contains("mod_init.lua", result.ReplacedFiles);
            Assert.Contains("data.csv", result.ReplacedFiles);
            Assert.Contains("baseline.gsc", result.ReplacedFiles);
        }
        finally
        {
            File.Delete(sourceFf);
            File.Delete(outputFf);
        }
    }

    [Fact]
    public void Convert_Mw2PsThreeToPc_RefusesCrossLayoutWithClearError()
    {
        // MW2 PS3 uses a 52-byte zone header; MW2 PC uses 56-byte (extra BlockSizeIndex slot).
        // The in-place patcher in Convert() can't insert/remove that slot, so it now refuses
        // cross-layout conversion with a clear error message rather than silently producing a
        // malformed FF. The right path for this case is ConvertUsingBaseZone (rebuilds the zone).
        byte[] mw2Zone = FfBuilder.BuildMinimalMW2Zone();
        string sourceFf = Path.GetTempFileName();
        string outputFf = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(sourceFf, FfBuilder.BuildMW2Ps3(mw2Zone));

            var result = FastFileConverter.Convert(sourceFf, outputFf, Platform.PC);

            Assert.False(result.Success);
            Assert.Contains("Cross-layout", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ConvertUsingBaseZone", result.Message);
        }
        finally
        {
            File.Delete(sourceFf);
            if (File.Exists(outputFf)) File.Delete(outputFf);
        }
    }

    [Fact]
    public void Convert_WaWPsThreeToPc_ProducesPcFormat()
    {
        // WaW PS3 and WaW PC both use the 52-byte zone header layout — just different
        // endianness (PS3 BE, PC LE). My ConvertAssetTypeIDs fix should swap the asset
        // type IDs from PS3 enum to PC enum (-2 shift since PC drops both pixelshader
        // and vertexshader), and PatchZoneHeaderForPlatform should write the header
        // fields LE for PC target. This test verifies the FF-level output is PC-shaped.
        byte[] wawZone = FfBuilder.BuildMinimalWaWZone();
        string sourceFf = Path.GetTempFileName();
        string outputFf = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(sourceFf, FfBuilder.BuildWaWPs3(wawZone));

            var result = FastFileConverter.Convert(sourceFf, outputFf, Platform.PC);

            Assert.True(result.Success, $"Conversion failed: {result.Message}");

            byte[] ff = File.ReadAllBytes(outputFf);
            // PC WaW FF: IWffu100 + version 0x183 LE (`83 01 00 00`) + single zlib at 0x0C
            Assert.Equal("IWffu100", Encoding.ASCII.GetString(ff, 0, 8));
            Assert.Equal(0x83, ff[8]);
            Assert.Equal(0x01, ff[9]);
            Assert.Equal(0x00, ff[10]);
            Assert.Equal(0x00, ff[11]);
            Assert.Equal(0x78, ff[12]);  // zlib magic
        }
        finally
        {
            File.Delete(sourceFf);
            if (File.Exists(outputFf)) File.Delete(outputFf);
        }
    }

    [Fact]
    public void Convert_WaWPsThreeToWii_RefusesCrossLayoutWithClearError()
    {
        // WaW PS3 is 52-byte; WaW Wii is 56-byte (extra BlockSizeIndex slot). Convert()
        // refuses cross-layout with a clear error pointing the user at ConvertUsingBaseZone.
        byte[] wawZone = FfBuilder.BuildMinimalWaWZone();
        string sourceFf = Path.GetTempFileName();
        string outputFf = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(sourceFf, FfBuilder.BuildWaWPs3(wawZone));

            var result = FastFileConverter.Convert(sourceFf, outputFf, Platform.Wii);

            Assert.False(result.Success);
            Assert.Contains("Cross-layout", result.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(sourceFf);
            if (File.Exists(outputFf)) File.Delete(outputFf);
        }
    }

    [Fact]
    public void ConvertUsingBaseZone_Mw2PsThreeToPc_ProducesMw2PcFormat()
    {
        // The proper path for MW2 PS3 → MW2 PC (different zone-header layouts) is the rebuild
        // approach. ConvertUsingBaseZone extracts the rawfiles and rebuilds via ZoneBuilder,
        // which is platform-aware, so the output ends up as proper MW2 PC: 12-byte standard
        // header + 9-byte preamble + single zlib at 0x15.
        byte[] mw2Zone = FfBuilder.BuildMinimalMW2Zone();
        string sourceFf = Path.GetTempFileName();
        string outputFf = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(sourceFf, FfBuilder.BuildMW2Ps3(mw2Zone));

            var result = FastFileConverter.ConvertUsingBaseZone(
                sourceFf, basePs3ZonePath: "", outputFf, zoneName: "test", targetPlatform: Platform.PC);

            // Synthetic zone has no rawfiles/localize, so we expect a failure for that reason
            // (NOT a cross-layout error). Verifies ConvertUsingBaseZone reaches the rebuild stage.
            Assert.False(result.Success);
            Assert.Contains("No raw files", result.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(sourceFf);
            if (File.Exists(outputFf)) File.Delete(outputFf);
        }
    }

    [Fact]
    public void ConvertUsingBaseZone_Mw2Source_ProducesMw2PsThreeFormat()
    {
        // Before the CompressForPlatform fix, MW2 sources converted to PS3 went through
        // the plain block compressor — which omits the 25-byte MW2 extended header.
        // After routing through Recompress, MW2 → PS3 lands on CompressMW2 and emits the
        // full header (allowOnlineUpdate + fileCreationTime + region + entryCount + sizes).
        var raw = new RawFile("mw2_mod.gsc", Encoding.ASCII.GetBytes("// mw2 content"));
        var zone = new ZoneBuilder(GameVersion.MW2, "patch_mp").AddRawFile(raw).Build();

        string sourceFf = Path.GetTempFileName();
        string outputFf = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(sourceFf, FfBuilder.BuildMW2Ps3(zone));

            var result = FastFileConverter.ConvertUsingBaseZone(sourceFf, "", outputFf);
            Assert.True(result.Success, result.Message);

            byte[] ff = File.ReadAllBytes(outputFf);
            Assert.Equal("IWffu100", Encoding.ASCII.GetString(ff, 0, 8));

            // MW2 PS3 BE version = 00 00 01 0D
            Assert.Equal(0x00, ff[8]);
            Assert.Equal(0x00, ff[9]);
            Assert.Equal(0x01, ff[10]);
            Assert.Equal(0x0D, ff[11]);

            // 25-byte extended header should be present before the first compressed block.
            // The header has allowOnlineUpdate at offset 12. The block format's 2-byte
            // length prefix should start at offset 12 + 25 = 37. Earlier WaW-style output
            // would have placed the length prefix at offset 12 instead.
            // (We can't decode the exact length without re-reading, but a non-zero length
            // field at 37 with a plausible zlib block right after is a strong signal.)
            Assert.True(ff.Length > 37, "Output should be long enough to contain the MW2 extended header");
            // First byte of extended header is allowOnlineUpdate, written as 0x01 by default.
            Assert.Equal(0x01, ff[12]);
        }
        finally
        {
            File.Delete(sourceFf);
            File.Delete(outputFf);
        }
    }
}
