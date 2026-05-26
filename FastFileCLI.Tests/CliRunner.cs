using System.Diagnostics;
using System.Text;

namespace FastFileCLI.Tests;

/// <summary>
/// Spawns the built ffcli.dll as a subprocess via `dotnet ffcli.dll ...args`,
/// captures stdout/stderr/exit code. The CLI dll is copied next to the test
/// dll because FastFileCLI is a ProjectReference.
/// </summary>
public static class CliRunner
{
    private static readonly string CliPath = ResolveCliPath();

    public record Result(int ExitCode, string Stdout, string Stderr);

    /// <summary>
    /// Runs the CLI with the given args. WorkingDirectory defaults to a unique
    /// temp directory so glob tests don't see each other's files.
    /// </summary>
    public static Result Run(params string[] args) => Run(args, workingDirectory: null);

    public static Result Run(string[] args, string? workingDirectory)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
        };

        psi.ArgumentList.Add(CliPath);
        foreach (var a in args) psi.ArgumentList.Add(a);

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        using var proc = new Process { StartInfo = psi };
        proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        proc.ErrorDataReceived  += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        if (!proc.WaitForExit(milliseconds: 30_000))
        {
            try { proc.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException($"ffcli {string.Join(' ', args)} timed out after 30s");
        }
        // Drain async readers
        proc.WaitForExit();

        return new Result(proc.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private static string ResolveCliPath()
    {
        // FastFileCLI's AssemblyName is "ffcli", so the built dll is ffcli.dll.
        string path = Path.Combine(AppContext.BaseDirectory, "ffcli.dll");
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"CLI dll not found at {path}. Make sure FastFileCLI is a ProjectReference of the test project.",
                path);
        return path;
    }
}

/// <summary>
/// RAII-style temp directory that auto-deletes on dispose. Use for isolating
/// glob tests and ad-hoc fixture file placement.
/// </summary>
public sealed class TempDir : IDisposable
{
    public string Path { get; }

    public TempDir()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "ffcli-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>Writes bytes to a file inside this temp dir, returns the full path.</summary>
    public string Write(string name, byte[] data)
    {
        string p = System.IO.Path.Combine(Path, name);
        File.WriteAllBytes(p, data);
        return p;
    }

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch { /* best effort */ }
    }
}
