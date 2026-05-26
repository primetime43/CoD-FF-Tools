using Xunit;

namespace FastFileCLI.Tests;

/// <summary>
/// Verifies the "drop a .ff onto ffcli.exe" workflow. Windows passes the dropped file
/// path as the first arg, so we test by invoking the CLI with just a path (no command).
/// </summary>
public class DragDropTests
{
    [Fact]
    public void DroppedFile_RunsReportAutomatically()
    {
        using var dir = new TempDir();
        string ff = dir.Write("patch_mp.ff", FfBuilder.BuildWaWPs3(FfBuilder.BuildMinimalWaWZone()));

        var r = CliRunner.Run(ff);
        Assert.Equal(0, r.ExitCode);
        Assert.Contains("Detected 1 dropped file(s)", r.Stdout);
        Assert.Contains("=== FastFile Report ===", r.Stdout);
        Assert.Contains("Game:     WaW", r.Stdout);
    }

    [Fact]
    public void DroppedZoneFile_AlsoTriggersReport()
    {
        using var dir = new TempDir();
        string zone = dir.Write("patch_mp.zone", FfBuilder.BuildMinimalWaWZone());

        var r = CliRunner.Run(zone);
        Assert.Equal(0, r.ExitCode);
        Assert.Contains("=== FastFile Report ===", r.Stdout);
    }

    [Fact]
    public void MultipleDroppedFiles_ReportsEach()
    {
        using var dir = new TempDir();
        string a = dir.Write("a.ff", FfBuilder.BuildWaWPs3(FfBuilder.BuildMinimalWaWZone()));
        string b = dir.Write("b.ff", FfBuilder.BuildCoD4Ps3(FfBuilder.BuildMinimalWaWZone()));

        var r = CliRunner.Run(a, b);
        Assert.Equal(0, r.ExitCode);
        Assert.Contains("Detected 2 dropped file(s)", r.Stdout);

        // Two separate report headers
        int reportCount = CountOccurrences(r.Stdout, "=== FastFile Report ===");
        Assert.Equal(2, reportCount);

        // Both games detected
        Assert.Contains("WaW", r.Stdout);
        Assert.Contains("CoD4", r.Stdout);
    }

    [Fact]
    public void NonFFArg_StillDispatchesAsCommand()
    {
        // A plain 'info' arg with no value should still dispatch as the info command
        // (and fail with usage), NOT be treated as a dropped file.
        var r = CliRunner.Run("info");
        Assert.Equal(1, r.ExitCode);
        Assert.Contains("Usage: ffcli info", r.Stderr);
        Assert.DoesNotContain("Detected", r.Stdout);
    }

    [Fact]
    public void ArgThatLooksLikePathButIsntFF_TreatedAsCommand()
    {
        // A non-existent path with no FF extension should NOT be drag-drop mode -
        // it falls through to the dispatcher and gets 'unknown command'.
        var r = CliRunner.Run("not-a-real-thing.txt");
        Assert.Equal(1, r.ExitCode);
        Assert.Contains("Unknown command", r.Stderr);
    }

    [Fact]
    public void NonExistentFFPath_FallsThroughToCommandDispatch()
    {
        // 'foo.ff' that doesn't exist - LooksLikeDroppedFile returns false because
        // the file doesn't exist, so we don't enter drag-drop mode. It then falls
        // through to dispatcher as an unknown command.
        var r = CliRunner.Run("foo.ff");
        Assert.Equal(1, r.ExitCode);
        Assert.Contains("Unknown command", r.Stderr);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0)
        { count++; i += needle.Length; }
        return count;
    }
}
