using System.Diagnostics;
using System.Text.RegularExpressions;

namespace FastFileLib.Logging;

/// <summary>
/// TraceListener that captures System.Diagnostics.Trace.WriteLine /
/// Debug.WriteLine calls and forwards them to <see cref="LogService"/>.
///
/// Tries to extract a category and severity from messages of the form:
///   "[CategoryName] message text"
///   "[ParserName] ERROR: ..."
/// which matches the existing convention in the codebase. Messages without
/// a [Category] prefix get categorized as "Trace".
///
/// IMPORTANT: registers with <see cref="Trace.Listeners"/>. In Release builds,
/// Debug.WriteLine is compiled out, so only explicit Trace.WriteLine calls
/// flow through. In Debug builds, both are captured.
/// </summary>
public class TraceCapture : TraceListener
{
    // Matches "[Category] rest of message" - the convention used throughout the codebase
    // (e.g. "[FastFileProcessor] Block 1 decompression failed...").
    private static readonly Regex CategoryPrefix =
        new(@"^\s*\[(?<cat>[^\]]+)\]\s*(?<msg>.*)$", RegexOptions.Singleline);

    // To avoid recursion: LogService.MirrorToDebug calls Debug.WriteLine, which would
    // re-enter us if MirrorToDebug were on while TraceCapture is registered. We disable
    // MirrorToDebug while installed; if the caller wants Debug output too, they can
    // re-enable it (and accept the duplicate entries).
    private static bool _installed;
    private static TraceCapture? _instance;

    /// <summary>
    /// Registers a single TraceCapture instance with Trace.Listeners. Idempotent.
    /// Also disables <see cref="LogService.MirrorToDebug"/> to prevent infinite loops
    /// when entries log themselves back through Debug.WriteLine.
    /// </summary>
    public static void Install()
    {
        if (_installed) return;
        _instance = new TraceCapture { Name = nameof(TraceCapture) };
        Trace.Listeners.Add(_instance);
        LogService.MirrorToDebug = false;
        _installed = true;
    }

    /// <summary>Unregisters the listener. Mostly useful for tests.</summary>
    public static void Uninstall()
    {
        if (!_installed || _instance == null) return;
        Trace.Listeners.Remove(_instance);
        _instance.Dispose();
        _instance = null;
        _installed = false;
    }

    public override void Write(string? message)
    {
        // Trace.Write is rare - buffer-style writes are not the norm in this codebase.
        // We treat each Write as a complete entry.
        if (!string.IsNullOrEmpty(message)) Emit(message);
    }

    public override void WriteLine(string? message)
    {
        if (!string.IsNullOrEmpty(message)) Emit(message);
    }

    private static void Emit(string message)
    {
        var (category, body) = ParseCategory(message);
        var severity = InferSeverity(body);
        LogService.Add(severity, category, body);
    }

    private static (string category, string body) ParseCategory(string message)
    {
        var m = CategoryPrefix.Match(message);
        if (m.Success)
            return (m.Groups["cat"].Value.Trim(), m.Groups["msg"].Value.Trim());
        return ("Trace", message.Trim());
    }

    private static LogSeverity InferSeverity(string body)
    {
        // Crude but effective: many of the existing Debug.WriteLine messages contain
        // these markers. Refining per-site happens when those calls are converted to
        // LogService directly.
        if (body.Contains("ERROR", StringComparison.OrdinalIgnoreCase) ||
            body.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
            body.Contains("exception", StringComparison.OrdinalIgnoreCase))
            return LogSeverity.Error;
        if (body.Contains("WARN", StringComparison.OrdinalIgnoreCase))
            return LogSeverity.Warning;
        return LogSeverity.Debug;
    }
}
