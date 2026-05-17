using System.IO.Compression;
using FastFileLib;
using Xunit;

namespace FastFileCLI.Tests;

/// <summary>
/// Round-trip tests for PC WaW FastFiles. These need real PC WaW samples and only
/// run when those samples exist at <see cref="SampleDir"/> on the machine. In CI or
/// on a machine without the samples, the tests are skipped.
///
/// The point of round-trip: decompress original FF → recompress via PC compile path
/// → decompress that → byte-compare against original zone bytes. If both zones match,
/// the FF format is structurally correct (zlib content may differ in compression level
/// but the decompressed payload should be identical).
/// </summary>
public class PcWaWRoundTripTests
{
    // Local path to PC WaW samples. Not committed to the repo. Tests are skipped
    // (via Skip on the [SkippableFact]) if the directory doesn't exist.
    private const string SampleDir = @"C:\Users\primetime43\Downloads\PC WAW files";

    public static IEnumerable<object[]> Samples()
    {
        if (!Directory.Exists(SampleDir)) yield break;
        foreach (var name in new[]
        {
            "default.ff",
            "mp_makin_day_load.ff",
            "patch.ff",
            "patch_mp (1).ff",
            // skip credits.ff/localized_mp_*.ff in CI - they're 7-10MB and slower
        })
        {
            string path = Path.Combine(SampleDir, name);
            if (File.Exists(path)) yield return new object[] { name, path };
        }
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void PcWaW_RoundTrip_PreservesZoneBytes(string sampleName, string samplePath)
    {
        // 1. Decompress original
        byte[] originalFf = File.ReadAllBytes(samplePath);
        byte[] originalZone = DecompressPcFf(originalFf);

        // 2. Verify the header looks like what we expect
        Assert.Equal((byte)'I', originalFf[0]);
        Assert.Equal((byte)'W', originalFf[1]);
        Assert.Equal(0x83, originalFf[8]);   // version LE byte 0
        Assert.Equal(0x01, originalFf[9]);   // version LE byte 1
        Assert.Equal(0x78, originalFf[12]);  // zlib CMF byte

        // 3. Recompile via our PC compile path
        var compiler = new Compiler(GameVersion.WaW, "PC");
        byte[] recompressedFf = compiler.Compile(originalZone);

        // 4. Verify our output also has the PC header layout
        Assert.Equal((byte)'I', recompressedFf[0]);
        Assert.Equal(0x83, recompressedFf[8]);
        Assert.Equal(0x01, recompressedFf[9]);
        Assert.Equal(0x78, recompressedFf[12]);  // zlib starts at byte 12

        // 5. Decompress our re-emitted FF
        byte[] roundTrippedZone = DecompressPcFf(recompressedFf);

        // 6. Zones must match byte-for-byte
        Assert.Equal(originalZone.Length, roundTrippedZone.Length);
        Assert.Equal(originalZone, roundTrippedZone);
    }

    [Fact]
    public void PcWaW_CompilerEmitsCorrectVersionBytes()
    {
        // Even without sample files, we can verify the compiler emits the right
        // PC version layout. Use a 1-byte zone to keep the output small.
        var compiler = new Compiler(GameVersion.WaW, "PC");
        byte[] ff = compiler.Compile(new byte[] { 0x00 });

        Assert.True(ff.Length >= 12);
        Assert.Equal("IWffu100", System.Text.Encoding.ASCII.GetString(ff, 0, 8));
        Assert.Equal(0x83, ff[8]);    // LE version byte 0
        Assert.Equal(0x01, ff[9]);
        Assert.Equal(0x00, ff[10]);
        Assert.Equal(0x00, ff[11]);
        Assert.Equal(0x78, ff[12]);   // zlib header CMF
    }

    [Fact]
    public void PcWaW_CompilerEmitsNoBlockEndMarker()
    {
        // PC WaW has no 00 01 end marker like the block format does.
        var compiler = new Compiler(GameVersion.WaW, "PC");
        byte[] ff = compiler.Compile(new byte[100]);

        // Last byte should be part of the zlib trailer (Adler-32 checksum), not 0x01.
        // A simple sanity check: the file should decompress cleanly without a trailing
        // 00 01 (which would be invalid trailing bytes for the zlib decompressor).
        byte[] zone = DecompressPcFf(ff);
        Assert.Equal(100, zone.Length);
    }

    /// <summary>
    /// Decompresses a PC-format FF: skip the 12-byte header, treat the rest as a
    /// single zlib stream.
    /// </summary>
    private static byte[] DecompressPcFf(byte[] ff)
    {
        using var input = new MemoryStream(ff, 12, ff.Length - 12);
        using var output = new MemoryStream();
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        zlib.CopyTo(output);
        return output.ToArray();
    }
}
