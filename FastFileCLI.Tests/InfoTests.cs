using System.Text.Json;
using Xunit;

namespace FastFileCLI.Tests;

public class InfoTests
{
    [Fact]
    public void Info_OnMissingFile_ExitsOne()
    {
        var r = CliRunner.Run("info", "does-not-exist.ff");
        Assert.Equal(1, r.ExitCode);
        // Either no-files-matched or file-not-found is acceptable.
        Assert.True(
            r.Stderr.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
            r.Stderr.Contains("No files matched", StringComparison.OrdinalIgnoreCase),
            $"Expected file-not-found message, got stderr: {r.Stderr}");
    }

    [Fact]
    public void Info_WaWPs3_PrintsExpectedFields()
    {
        using var dir = new TempDir();
        string ff = dir.Write("test.ff", FfBuilder.BuildWaWPs3(FfBuilder.BuildMinimalWaWZone()));

        var r = CliRunner.Run("info", ff);
        Assert.Equal(0, r.ExitCode);
        Assert.Contains("Game:      WaW", r.Stdout);
        Assert.Contains("Magic:     IWffu100", r.Stdout);
        Assert.Contains("Signed:    No", r.Stdout);
        Assert.Contains("0x00000183", r.Stdout);  // WaW version
    }

    [Fact]
    public void Info_CoD4Ps3_DetectsGameAndPlatform()
    {
        using var dir = new TempDir();
        string ff = dir.Write("test.ff", FfBuilder.BuildCoD4Ps3(FfBuilder.BuildMinimalWaWZone()));

        var r = CliRunner.Run("info", ff);
        Assert.Equal(0, r.ExitCode);
        Assert.Contains("Game:      CoD4", r.Stdout);
        Assert.Contains("0x00000001", r.Stdout);
    }

    [Fact]
    public void Info_CoD4Pc_DetectsLittleEndian()
    {
        using var dir = new TempDir();
        string ff = dir.Write("test.ff", FfBuilder.BuildCoD4Pc(FfBuilder.BuildMinimalWaWZone()));

        var r = CliRunner.Run("info", ff);
        Assert.Equal(0, r.ExitCode);
        Assert.Contains("Game:      CoD4", r.Stdout);
        Assert.Contains("Platform:  PC", r.Stdout);
    }

    [Fact]
    public void Info_Xbox360Signed_FlagsSigned()
    {
        using var dir = new TempDir();
        string ff = dir.Write("test.ff", FfBuilder.BuildWaWXbox360Signed(FfBuilder.BuildMinimalWaWZone()));

        var r = CliRunner.Run("info", ff);
        Assert.Equal(0, r.ExitCode);
        Assert.Contains("Signed:    Yes", r.Stdout);
        Assert.Contains("Platform:  Xbox 360", r.Stdout);
    }

    [Fact]
    public void Info_Json_SingleFile_EmitsObject()
    {
        using var dir = new TempDir();
        string ff = dir.Write("test.ff", FfBuilder.BuildWaWPs3(FfBuilder.BuildMinimalWaWZone()));

        var r = CliRunner.Run("info", ff, "--json");
        Assert.Equal(0, r.ExitCode);

        using var doc = JsonDocument.Parse(r.Stdout);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        Assert.Equal("WaW", doc.RootElement.GetProperty("game").GetString());
        Assert.Equal("PS3", doc.RootElement.GetProperty("platform").GetString());
        Assert.Equal("IWffu100", doc.RootElement.GetProperty("magic").GetString());
        Assert.False(doc.RootElement.GetProperty("signed").GetBoolean());
    }

    [Fact]
    public void Info_Json_MultipleFiles_EmitsArray()
    {
        using var dir = new TempDir();
        dir.Write("a.ff", FfBuilder.BuildWaWPs3(FfBuilder.BuildMinimalWaWZone()));
        dir.Write("b.ff", FfBuilder.BuildCoD4Ps3(FfBuilder.BuildMinimalWaWZone()));

        var r = CliRunner.Run(new[] { "info", "*.ff", "--json" }, workingDirectory: dir.Path);
        Assert.Equal(0, r.ExitCode);

        using var doc = JsonDocument.Parse(r.Stdout);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(2, doc.RootElement.GetArrayLength());

        var games = new List<string>();
        foreach (var el in doc.RootElement.EnumerateArray())
            games.Add(el.GetProperty("game").GetString()!);
        Assert.Contains("WaW", games);
        Assert.Contains("CoD4", games);
    }
}
