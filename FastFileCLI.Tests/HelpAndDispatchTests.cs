using Xunit;

namespace FastFileCLI.Tests;

public class HelpAndDispatchTests
{
    [Fact]
    public void NoArgs_PrintsUsageAndExitsZero()
    {
        var r = CliRunner.Run();
        Assert.Equal(0, r.ExitCode);
        Assert.Contains("FastFile CLI", r.Stdout);
        Assert.Contains("Usage: ffcli <command>", r.Stdout);
        // Should mention the new report command
        Assert.Contains("report", r.Stdout);
        // The deleted interactive menu must NOT appear
        Assert.DoesNotContain("Interactive Mode", r.Stdout);
        Assert.DoesNotContain("Select an option", r.Stdout);
    }

    [Fact]
    public void Help_PrintsUsage()
    {
        var r = CliRunner.Run("help");
        Assert.Equal(0, r.ExitCode);
        Assert.Contains("Usage: ffcli", r.Stdout);
    }

    [Theory]
    [InlineData("info")]
    [InlineData("report")]
    [InlineData("decompress")]
    [InlineData("compress")]
    [InlineData("list")]
    [InlineData("extract")]
    [InlineData("patch")]
    public void Help_ForKnownCommand_DescribesIt(string command)
    {
        var r = CliRunner.Run("help", command);
        Assert.Equal(0, r.ExitCode);
        Assert.Contains("Usage:", r.Stdout);
    }

    [Fact]
    public void Help_ReportMentionsBugReportUseCase()
    {
        var r = CliRunner.Run("help", "report");
        Assert.Equal(0, r.ExitCode);
        Assert.Contains("bug report", r.Stdout, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnknownCommand_ExitsOneAndErrorsToStderr()
    {
        var r = CliRunner.Run("blorp");
        Assert.Equal(1, r.ExitCode);
        Assert.Contains("Unknown command", r.Stderr);
    }

    [Fact]
    public void HelpForUnknownCommand_ExitsOne()
    {
        var r = CliRunner.Run("help", "blorp");
        Assert.Equal(1, r.ExitCode);
    }
}
