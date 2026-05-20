using Xunit;

namespace FastFileCLI.Tests;

public class DecompressAndListTests
{
    [Fact]
    public void Decompress_WritesZoneFile_AndPrintsSummary()
    {
        // Note: Decompressor.FixZoneHeaderSizes rewrites bytes 0-3 and 24-27 of the
        // decompressed zone to match actual file length, so we can't assert byte-for-byte
        // equality with the source. We just verify the file was written and CLI reported
        // the operation, plus check the bytes around the rewritten regions are preserved.
        using var dir = new TempDir();
        byte[] zone = FfBuilder.BuildMinimalWaWZone();
        string ff = dir.Write("test.ff", FfBuilder.BuildWaWPs3(zone));
        string outZone = Path.Combine(dir.Path, "out.zone");

        var r = CliRunner.Run("decompress", ff, outZone);
        Assert.Equal(0, r.ExitCode);
        Assert.True(File.Exists(outZone), "decompressed zone file should exist");

        byte[] roundTripped = File.ReadAllBytes(outZone);
        Assert.Equal(zone.Length, roundTripped.Length);

        // BlockSizeTemp at offset 0x08 is NOT rewritten by FixZoneHeaderSizes, so it
        // should survive the round-trip unchanged (this is the WaW PS3 MemAlloc value).
        Assert.Equal(0x10B0, (roundTripped[0x0A] << 8) | roundTripped[0x0B]);

        Assert.Contains("Decompressing:", r.Stdout);
        Assert.Contains("Game: WaW", r.Stdout);
    }

    [Fact]
    public void Decompress_NoOutputPath_DefaultsToZoneExtension()
    {
        using var dir = new TempDir();
        string ff = dir.Write("my.ff", FfBuilder.BuildWaWPs3(FfBuilder.BuildMinimalWaWZone()));

        var r = CliRunner.Run("decompress", ff);
        Assert.Equal(0, r.ExitCode);

        string expectedZone = Path.ChangeExtension(ff, ".zone");
        Assert.True(File.Exists(expectedZone));
    }

    [Fact]
    public void Decompress_MissingFile_ExitsOne()
    {
        var r = CliRunner.Run("decompress", "nope.ff");
        Assert.Equal(1, r.ExitCode);
    }

    [Fact]
    public void List_ZoneWithNoRawFiles_StillExitsZero()
    {
        using var dir = new TempDir();
        // Minimal zone has no raw-file-like content (no .gsc/.cfg/etc names)
        string zonePath = dir.Write("test.zone", FfBuilder.BuildMinimalWaWZone());

        var r = CliRunner.Run("list", zonePath);
        Assert.Equal(0, r.ExitCode);
        Assert.Contains("Found 0 raw file(s)", r.Stdout);
    }

    [Fact]
    public void List_MissingFile_ExitsOne()
    {
        var r = CliRunner.Run("list", "missing.zone");
        Assert.Equal(1, r.ExitCode);
    }

    [Fact]
    public void Decompress_Mw2Xbox360Signed_RoundTripsZoneBytes()
    {
        // Regression: signed MW2 Xbox 360 uses the IW4 "authed chunks" format with the full
        // 25-byte DB_Header, so IWffs100 sits at 0x25 (vs MW2 PC's 0x15). The decompressor
        // previously had no path for this — it fell through to the block decoder and failed
        // ("unsupported compression method"). Verify a synthetic signed MW2 Xbox 360 FF now
        // decompresses back to the exact zone bytes.
        byte[] zone = new byte[1500];
        for (int i = 0; i < zone.Length; i++) zone[i] = (byte)((i * 7 + 3) & 0xFF);

        using var dir = new TempDir();
        string ff = dir.Write("mw2xbox.ff", FfBuilder.BuildMW2Xbox360Signed(zone));

        // Sanity: detected as signed MW2 console.
        var info = FastFileLib.FastFileInfo.FromFile(ff);
        Assert.Equal("MW2", info.GameName);
        Assert.True(info.IsSigned);

        string outZone = Path.Combine(dir.Path, "out.zone");
        int blocks = FastFileLib.FastFileProcessor.Decompress(ff, outZone);
        Assert.True(blocks > 0);

        byte[] rt = File.ReadAllBytes(outZone);
        Assert.Equal(zone.Length, rt.Length);
        Assert.Equal(zone, rt);
    }
}
