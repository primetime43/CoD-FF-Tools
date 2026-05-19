using System.Text;
using FastFileLib;
using FastFileLib.Models;
using Xunit;

namespace FastFileCLI.Tests;

/// <summary>
/// `ffcli compress` used to default to WaW + PS3 unless --game / --platform were
/// passed explicitly. Running it on a WaW PC zone without flags produced a
/// PS3-formatted FF (BE version + 64KB block format) whose first bytes were
/// `IWffu100 + 00 00 01 83 + ...`. The PC engine then read those LE and got
/// 0x83010000 = -2097086464, failing the version check (expecting 387). These
/// tests pin auto-detection so the default-PS3 regression can't come back.
/// </summary>
public class CompressAutoDetectTests
{
    [Fact]
    public void Compress_WaWPcZone_NoFlags_AutoDetectsPC()
    {
        using var dir = new TempDir();
        var zonePath = WritePcZone(dir, GameVersion.WaW);
        var outPath = Path.Combine(dir.Path, "out.ff");

        var r = CliRunner.Run("compress", zonePath, outPath);
        Assert.Equal(0, r.ExitCode);
        Assert.Contains("Platform: PC", r.Stdout);

        // PC WaW FF header: "IWffu100" + version 0x183 stored little-endian.
        var ff = File.ReadAllBytes(outPath);
        Assert.Equal("IWffu100", Encoding.ASCII.GetString(ff, 0, 8));
        Assert.Equal(0x83, ff[8]);  // <- the critical byte; was 0x00 before the fix
        Assert.Equal(0x01, ff[9]);
        Assert.Equal(0x00, ff[10]);
        Assert.Equal(0x00, ff[11]);
        // Zlib stream starts at 0x0C — PC uses single zlib, not 64KB blocks.
        Assert.Equal(0x78, ff[12]);
    }

    [Fact]
    public void Compress_ConsoleZone_NoFlags_AutoDetectsPS3()
    {
        // A console (PS3) zone should still default correctly when no flags are passed.
        using var dir = new TempDir();
        var zonePath = WriteConsoleZone(dir, GameVersion.WaW);
        var outPath = Path.Combine(dir.Path, "out.ff");

        var r = CliRunner.Run("compress", zonePath, outPath);
        Assert.Equal(0, r.ExitCode);
        Assert.Contains("Platform: PS3", r.Stdout);

        // PS3 WaW FF header: "IWffu100" + version 0x183 stored big-endian.
        var ff = File.ReadAllBytes(outPath);
        Assert.Equal("IWffu100", Encoding.ASCII.GetString(ff, 0, 8));
        Assert.Equal(0x00, ff[8]);
        Assert.Equal(0x00, ff[9]);
        Assert.Equal(0x01, ff[10]);
        Assert.Equal(0x83, ff[11]);
    }

    [Fact]
    public void Compress_ExplicitPlatformFlag_OverridesAutoDetect()
    {
        // User-supplied --platform must still win in case auto-detect picks wrong.
        using var dir = new TempDir();
        var zonePath = WritePcZone(dir, GameVersion.WaW);
        var outPath = Path.Combine(dir.Path, "out.ff");

        var r = CliRunner.Run("compress", zonePath, outPath, "--platform", "ps3");
        Assert.Equal(0, r.ExitCode);
        Assert.Contains("Platform: PS3", r.Stdout);

        var ff = File.ReadAllBytes(outPath);
        Assert.Equal(0x00, ff[8]);
        Assert.Equal(0x83, ff[11]);  // BE version
    }

    private static string WritePcZone(TempDir dir, GameVersion gv) =>
        dir.Write("pc.zone", new ZoneBuilder(gv, "auto_test", "PC")
            .AddRawFile(new RawFile("a.gsc", Encoding.ASCII.GetBytes("x")))
            .Build());

    private static string WriteConsoleZone(TempDir dir, GameVersion gv) =>
        dir.Write("ps3.zone", new ZoneBuilder(gv, "auto_test", "PS3")
            .AddRawFile(new RawFile("a.gsc", Encoding.ASCII.GetBytes("x")))
            .Build());
}
