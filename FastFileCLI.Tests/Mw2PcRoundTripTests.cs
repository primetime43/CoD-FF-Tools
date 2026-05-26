using System.IO.Compression;
using System.Text;
using FastFileLib;
using Xunit;

namespace FastFileCLI.Tests;

/// <summary>
/// Round-trip tests for MW2 PC FastFiles (unsigned output).
///
/// MW2 PC layout written by <see cref="FastFileProcessor.CompressMW2PC"/>:
///   0x00..0x07  IWffu100 magic
///   0x08..0x0B  Version 0x114 (LE)
///   0x0C..0x14  9-byte preamble (allowOnlineUpdate + fileCreationTime)
///   0x15..      Single zlib stream of the entire zone
///
/// Signed input files are intentionally recompressed as unsigned (the only practical
/// option without IW's RSA-2048 private key).
/// </summary>
public class Mw2PcRoundTripTests
{
    // Local path to MW2 PC samples. Not committed to the repo. Tests that depend on
    // real samples skip themselves when the directory or specific file is missing.
    private const string SampleDir = @"C:\Users\primetime43\Downloads\MW2 PC files";

    [Fact]
    public void CompressMW2PC_EmitsCorrectHeaderLayout()
    {
        string inZone = Path.GetTempFileName();
        string outFf = Path.GetTempFileName();
        try
        {
            // 64KB of zeros — large enough to exercise the zlib stream without dragging tests.
            File.WriteAllBytes(inZone, new byte[0x10000]);

            FastFileProcessor.CompressMW2PC(inZone, outFf);
            byte[] ff = File.ReadAllBytes(outFf);

            // Header magic
            Assert.Equal("IWffu100", Encoding.ASCII.GetString(ff, 0, 8));

            // LE version 0x114
            Assert.Equal(0x14, ff[8]);
            Assert.Equal(0x01, ff[9]);
            Assert.Equal(0x00, ff[10]);
            Assert.Equal(0x00, ff[11]);

            // 9-byte preamble defaults: allowOnlineUpdate=1, fileCreationTime=0
            Assert.Equal(0x01, ff[12]);
            for (int i = 13; i < 21; i++)
                Assert.Equal(0x00, ff[i]);

            // zlib starts at 0x15
            Assert.Equal(0x78, ff[0x15]);
        }
        finally
        {
            File.Delete(inZone);
            File.Delete(outFf);
        }
    }

    [Fact]
    public void CompressMW2PC_RoundTripsZoneBytes()
    {
        string inZone = Path.GetTempFileName();
        string outFf = Path.GetTempFileName();
        try
        {
            byte[] originalZone = new byte[8192];
            for (int i = 0; i < originalZone.Length; i++)
                originalZone[i] = (byte)(i & 0xFF);

            File.WriteAllBytes(inZone, originalZone);
            FastFileProcessor.CompressMW2PC(inZone, outFf);

            byte[] ff = File.ReadAllBytes(outFf);
            byte[] roundTripped = DecompressMw2PcUnsignedFf(ff);

            Assert.Equal(originalZone.Length, roundTripped.Length);
            Assert.Equal(originalZone, roundTripped);
        }
        finally
        {
            File.Delete(inZone);
            File.Delete(outFf);
        }
    }

    [Fact]
    public void CompressMW2PC_PreservesPreambleFromOriginal()
    {
        string original = Path.GetTempFileName();
        string inZone = Path.GetTempFileName();
        string outFf = Path.GetTempFileName();
        try
        {
            // Hand-craft a fake "original" FF whose preamble has distinctive marker bytes.
            // Header doesn't need to be valid for ReadMW2PCPreamble — only the 9 bytes at 0x0C matter.
            var fakeOriginal = new byte[0x15];
            for (int i = 0; i < 12; i++) fakeOriginal[i] = 0xAA;        // header (ignored)
            fakeOriginal[0x0C] = 0x55;                                   // allowOnlineUpdate marker
            for (int i = 0x0D; i < 0x15; i++) fakeOriginal[i] = 0xCD;   // fileCreationTime marker
            File.WriteAllBytes(original, fakeOriginal);

            File.WriteAllBytes(inZone, new byte[128]);
            FastFileProcessor.CompressMW2PC(inZone, outFf, original);

            byte[] ff = File.ReadAllBytes(outFf);
            Assert.Equal(0x55, ff[0x0C]);
            for (int i = 0x0D; i < 0x15; i++)
                Assert.Equal(0xCD, ff[i]);
        }
        finally
        {
            File.Delete(original);
            File.Delete(inZone);
            File.Delete(outFf);
        }
    }

    [Fact]
    public void Recompress_DispatchesToMw2PcPath()
    {
        // Verifies the top-level Recompress() entry point (used by the editor) routes
        // MW2 + PC through CompressMW2PC instead of throwing NotSupportedException.
        string inZone = Path.GetTempFileName();
        string outFf = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(inZone, new byte[256]);
            FastFileProcessor.Recompress(inZone, outFf, GameVersion.MW2, "PC", signed: false);

            byte[] ff = File.ReadAllBytes(outFf);
            Assert.Equal("IWffu100", Encoding.ASCII.GetString(ff, 0, 8));
            Assert.Equal(0x14, ff[8]);    // 0x114 LE version
            Assert.Equal(0x78, ff[0x15]); // zlib header
        }
        finally
        {
            File.Delete(inZone);
            File.Delete(outFf);
        }
    }

    public static IEnumerable<object[]> Samples()
    {
        if (!Directory.Exists(SampleDir))
        {
            yield return new object[] { "(no samples available)", "" };
            yield break;
        }
        bool any = false;
        foreach (var name in new[] { "common.ff", "code_post_gfx.ff", "localized_common.ff" })
        {
            string path = Path.Combine(SampleDir, name);
            if (File.Exists(path))
            {
                any = true;
                yield return new object[] { name, path };
            }
        }
        if (!any) yield return new object[] { "(no samples available)", "" };
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Mw2Pc_RoundTrip_PreservesZoneBytes(string sampleName, string samplePath)
    {
        if (string.IsNullOrEmpty(samplePath))
            return; // No samples on this machine — treat as skipped.

        string originalZone = Path.GetTempFileName();
        string outFf = Path.GetTempFileName();
        try
        {
            // 1. Decompress original FF -> zone
            FastFileProcessor.Decompress(samplePath, originalZone);
            byte[] zoneBytes = File.ReadAllBytes(originalZone);
            Assert.True(zoneBytes.Length > 0, $"Decompression produced empty zone for {sampleName}");

            // 2. Recompress via our MW2 PC path, preserving the original preamble
            FastFileProcessor.CompressMW2PC(originalZone, outFf, samplePath);

            // 3. Decompress the round-tripped FF
            string roundTrippedZone = Path.GetTempFileName();
            try
            {
                FastFileProcessor.Decompress(outFf, roundTrippedZone);
                byte[] rt = File.ReadAllBytes(roundTrippedZone);
                Assert.Equal(zoneBytes.Length, rt.Length);
                Assert.Equal(zoneBytes, rt);
            }
            finally
            {
                File.Delete(roundTrippedZone);
            }
        }
        finally
        {
            File.Delete(originalZone);
            File.Delete(outFf);
        }
    }

    /// <summary>Decompresses an unsigned MW2 PC FF: skip 0x15 bytes, treat the rest as zlib.</summary>
    private static byte[] DecompressMw2PcUnsignedFf(byte[] ff)
    {
        const int zlibStart = 0x15;
        using var input = new MemoryStream(ff, zlibStart, ff.Length - zlibStart);
        using var output = new MemoryStream();
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        zlib.CopyTo(output);
        return output.ToArray();
    }
}
