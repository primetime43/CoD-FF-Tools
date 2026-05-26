using System.Text;
using FastFileLib;
using FastFileLib.Models;
using Xunit;

namespace FastFileCLI.Tests;

/// <summary>
/// CLI patch command tests. Builds zones via ZoneBuilder (the lib's canonical
/// writer), runs `ffcli patch` against them, then re-scans with RawFileScanner
/// to confirm the patched payload + size fields round-trip correctly. Covers
/// CoD4/WaW console BE, WaW PC LE, and MW2 PS3 zlib-compressed entries.
/// </summary>
public class PatchCommandTests
{
    [Fact]
    public void Patch_WaWPs3_SameSize_OverwritesPayload()
    {
        using var dir = new TempDir();
        var zonePath = WriteZone(dir, GameVersion.WaW, "PS3", ("a.gsc", "AAA"), ("target.gsc", "ORIG"));
        var contentPath = dir.Write("new.txt", Encoding.ASCII.GetBytes("NEW!"));

        var r = CliRunner.Run("patch", zonePath, "target.gsc", contentPath);

        Assert.Equal(0, r.ExitCode);
        var found = RawFileScanner.FindRawFiles(File.ReadAllBytes(zonePath), GameVersion.WaW, isPC: false);
        var target = found.Single(f => f.Name == "target.gsc");
        Assert.Equal("NEW!", Encoding.ASCII.GetString(target.Data));
        // The neighbor must still be parseable — proves the tail shift didn't corrupt it.
        Assert.Equal("AAA", Encoding.ASCII.GetString(found.Single(f => f.Name == "a.gsc").Data));
    }

    [Fact]
    public void Patch_WaWPs3_GrowsZone_AndShiftsTail()
    {
        using var dir = new TempDir();
        var zonePath = WriteZone(dir, GameVersion.WaW, "PS3", ("small.gsc", "hi"), ("after.gsc", "TAIL"));
        long oldZoneSize = new FileInfo(zonePath).Length;

        var grown = new string('X', 200);
        var contentPath = dir.Write("grown.txt", Encoding.ASCII.GetBytes(grown));

        var r = CliRunner.Run("patch", zonePath, "small.gsc", contentPath);

        Assert.Equal(0, r.ExitCode);
        var found = RawFileScanner.FindRawFiles(File.ReadAllBytes(zonePath), GameVersion.WaW, isPC: false);

        var target = found.Single(f => f.Name == "small.gsc");
        Assert.Equal(grown, Encoding.ASCII.GetString(target.Data));
        // Following entry was shifted by +198 bytes; scanner must still find it correctly.
        Assert.Equal("TAIL", Encoding.ASCII.GetString(found.Single(f => f.Name == "after.gsc").Data));
        Assert.True(new FileInfo(zonePath).Length > oldZoneSize, "zone should have grown");
    }

    [Fact]
    public void Patch_WaWPc_WritesSizeFieldLittleEndian()
    {
        // Regression for the bug that motivated this rewrite: the legacy patch
        // command read/wrote BE, corrupting WaW PC zones whose size fields are LE.
        using var dir = new TempDir();
        var zonePath = WriteZone(dir, GameVersion.WaW, "PC", ("target.gsc", "PCPCPC"));
        var contentPath = dir.Write("new.txt", Encoding.ASCII.GetBytes("LE!"));

        var r = CliRunner.Run("patch", zonePath, "target.gsc", contentPath);

        Assert.Equal(0, r.ExitCode);
        var found = RawFileScanner.FindRawFiles(File.ReadAllBytes(zonePath), GameVersion.WaW, isPC: true);
        Assert.Equal("LE!", Encoding.ASCII.GetString(found.Single().Data));
    }

    [Fact]
    public void Patch_Mw2Ps3_ReCompressesZlibPayload()
    {
        // MW2 stores rawfile payloads zlib-compressed. Patching must compress the new
        // content and update BOTH compressedLen and uncompressedLen at the right offsets.
        using var dir = new TempDir();
        var zonePath = WriteZone(dir, GameVersion.MW2, "PS3",
            ("first.gsc", new string('A', 500)),
            ("target.gsc", "original payload"));

        string newText = string.Concat(Enumerable.Repeat("repeated text - compresses well\n", 50));
        var contentPath = dir.Write("new.txt", Encoding.ASCII.GetBytes(newText));

        var r = CliRunner.Run("patch", zonePath, "target.gsc", contentPath);

        Assert.Equal(0, r.ExitCode);
        var found = RawFileScanner.FindRawFiles(File.ReadAllBytes(zonePath), GameVersion.MW2, isPC: false);

        var target = found.Single(f => f.Name == "target.gsc");
        Assert.True(target.WasCompressed);
        Assert.Equal(newText, Encoding.ASCII.GetString(target.Data));
        Assert.Equal(newText.Length, target.DataSize);
        Assert.True(target.CompressedSize > 0 && target.CompressedSize < newText.Length,
            $"expected compressed size < uncompressed ({target.CompressedSize} vs {newText.Length})");
    }

    [Fact]
    public void Patch_MissingTarget_PrintsErrorAndExitsNonzero()
    {
        using var dir = new TempDir();
        var zonePath = WriteZone(dir, GameVersion.WaW, "PS3", ("real.gsc", "x"));
        var contentPath = dir.Write("new.txt", Encoding.ASCII.GetBytes("ignored"));

        var r = CliRunner.Run("patch", zonePath, "nonexistent.gsc", contentPath);

        Assert.Equal(1, r.ExitCode);
        Assert.Contains("not found", r.Stderr, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("real.gsc", r.Stderr);  // suggested available files
    }

    /// <summary>
    /// Builds a synthetic zone via <see cref="ZoneBuilder"/> and writes it to a temp
    /// file, returning the path. Each entry is given its own ASCII payload so the
    /// tests can identify them after re-scanning.
    /// </summary>
    private static string WriteZone(TempDir dir, GameVersion gv, string platform, params (string name, string payload)[] entries)
    {
        var builder = new ZoneBuilder(gv, "patch_test", platform);
        foreach (var (name, payload) in entries)
            builder.AddRawFile(new RawFile(name, Encoding.ASCII.GetBytes(payload)));
        return dir.Write("test.zone", builder.Build());
    }
}
