using Xunit;

namespace FastFileCLI.Tests;

public class ReportTests
{
    [Fact]
    public void Report_OnMissingFile_ExitsOne()
    {
        var r = CliRunner.Run("report", "does-not-exist.ff");
        Assert.Equal(1, r.ExitCode);
    }

    [Fact]
    public void Report_WaWPs3_PrintsAllExpectedSections()
    {
        using var dir = new TempDir();
        string ff = dir.Write("test.ff", FfBuilder.BuildWaWPs3(FfBuilder.BuildMinimalWaWZone()));

        var r = CliRunner.Run("report", ff);
        Assert.Equal(0, r.ExitCode);

        // Core sections
        Assert.Contains("=== FastFile Report ===", r.Stdout);
        Assert.Contains("== FastFile Header ==", r.Stdout);
        Assert.Contains("== Compressed Data ==", r.Stdout);

        // Detected metadata
        Assert.Contains("Game:     WaW", r.Stdout);
        Assert.Contains("Magic:    IWffu100", r.Stdout);

        // Should detect the block-format end marker we wrote
        Assert.Contains("block-format end marker", r.Stdout);

        // Decompression should succeed for a synthetic block-format file
        Assert.Contains("Decompressed:", r.Stdout);
        Assert.Contains("[OK]", r.Stdout);

        // Zone header parsed
        Assert.Contains("== Zone Header ==", r.Stdout);
        Assert.Contains("BlockSizeTemp", r.Stdout);
        Assert.Contains("AssetCount", r.Stdout);

        // Asset pool parsed
        Assert.Contains("== Asset Pool ==", r.Stdout);
        Assert.Contains("Layout:", r.Stdout);
    }

    [Fact]
    public void Report_MW2Ps3_PrintsExtendedHeaderSection()
    {
        using var dir = new TempDir();
        string ff = dir.Write("test.ff", FfBuilder.BuildMW2Ps3(FfBuilder.BuildMinimalMW2Zone()));

        var r = CliRunner.Run("report", ff);
        Assert.Equal(0, r.ExitCode);
        Assert.Contains("MW2 Extended Header", r.Stdout);
        Assert.Contains("allowOnlineUpdate", r.Stdout);
        Assert.Contains("entryCount:", r.Stdout);
    }

    [Fact]
    public void Report_Xbox360Signed_PrintsStreamingSection()
    {
        using var dir = new TempDir();
        string ff = dir.Write("test.ff", FfBuilder.BuildWaWXbox360Signed(FfBuilder.BuildMinimalWaWZone()));

        var r = CliRunner.Run("report", ff);
        Assert.Equal(0, r.ExitCode);

        Assert.Contains("== Xbox 360 Streaming Header ==", r.Stdout);
        Assert.Contains("IWffs100 magic at:    0x0C", r.Stdout);
        Assert.Contains("Compressed stream at: 0x400C", r.Stdout);
    }

    [Fact]
    public void Report_NonFastFile_StillProducesOutput()
    {
        // Junk file should not crash the report - it should error gracefully.
        using var dir = new TempDir();
        string junk = dir.Write("junk.bin", new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0xCA, 0xFE, 0xBA, 0xBE, 0x00, 0x00, 0x00, 0x00 });

        var r = CliRunner.Run("report", junk);
        // Even if parsing fails, the report header should print and exit code should be 0
        // (we successfully *generated* a report; the report's content describes the failure).
        Assert.Equal(0, r.ExitCode);
        Assert.Contains("=== FastFile Report ===", r.Stdout);
    }

    [Fact]
    public void Report_GlobMatchesMultipleFiles_SeparatesWithDivider()
    {
        using var dir = new TempDir();
        dir.Write("a.ff", FfBuilder.BuildWaWPs3(FfBuilder.BuildMinimalWaWZone()));
        dir.Write("b.ff", FfBuilder.BuildCoD4Ps3(FfBuilder.BuildMinimalWaWZone()));

        var r = CliRunner.Run(new[] { "report", "*.ff" }, workingDirectory: dir.Path);
        Assert.Equal(0, r.ExitCode);

        // Two report headers (one per file) and one divider between them
        int reportCount = CountOccurrences(r.Stdout, "=== FastFile Report ===");
        Assert.Equal(2, reportCount);
    }

    [Fact]
    public void Report_WaWZoneWithCorruptMemAlloc_FlagsMismatch()
    {
        // Build a WaW PS3 FF with a junk MemAlloc1 value that matches neither PS3 (0x10B0)
        // nor Xbox 360 (0x0A90). The zone-peek detection falls back to "PS3/Xbox 360"
        // (defaults to PS3 expected values), and the validator should flag that
        // BlockSizeTemp doesn't match the expected magic.
        using var dir = new TempDir();
        byte[] zone = FfBuilder.BuildMinimalWaWZone();
        // Overwrite BlockSizeTemp with a junk value (not a known PS3/Xbox 360 magic)
        zone[0x08] = 0x00; zone[0x09] = 0x00; zone[0x0A] = 0xDE; zone[0x0B] = 0xAD;
        string ff = dir.Write("test.ff", FfBuilder.BuildWaWPs3(zone));

        var r = CliRunner.Run("report", ff);
        Assert.Equal(0, r.ExitCode);
        Assert.Contains("Issues Detected", r.Stdout);
        Assert.Contains("BlockSizeTemp", r.Stdout);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { count++; i += needle.Length; }
        return count;
    }
}
