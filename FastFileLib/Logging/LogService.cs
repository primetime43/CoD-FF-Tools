using System.Diagnostics;

namespace FastFileLib.Logging;

/// <summary>
/// Centralized logging service. All FastFile library code and the editor write entries here;
/// the editor's Logs tab subscribes to <see cref="EntryAdded"/> to render them live.
///
/// Thread-safe. Entries are capped at <see cref="MaxEntries"/> with FIFO eviction so a long-running
/// session can't blow up memory.
/// </summary>
public static class LogService
{
    /// <summary>
    /// Maximum entries kept in memory. Oldest are evicted FIFO once exceeded.
    /// </summary>
    public const int MaxEntries = 10_000;

    private static readonly object _lock = new();
    private static readonly LinkedList<LogEntry> _entries = new();

    /// <summary>
    /// Fired (synchronously, on the calling thread) when a new entry is added.
    /// UI subscribers must marshal to the UI thread themselves.
    /// </summary>
    public static event EventHandler<LogEntry>? EntryAdded;

    /// <summary>Fired when Clear() is called.</summary>
    public static event EventHandler? Cleared;

    /// <summary>
    /// If true, every added entry also goes to System.Diagnostics.Debug.WriteLine
    /// (so it shows up in the VS Output window). Defaults to true.
    /// </summary>
    public static bool MirrorToDebug { get; set; } = true;

    public static void Debug  (string category, string message, Exception? ex = null) => Add(LogSeverity.Debug, category, message, ex);
    public static void Info   (string category, string message, Exception? ex = null) => Add(LogSeverity.Info, category, message, ex);
    public static void Warning(string category, string message, Exception? ex = null) => Add(LogSeverity.Warning, category, message, ex);
    public static void Error  (string category, string message, Exception? ex = null) => Add(LogSeverity.Error, category, message, ex);

    public static void Add(LogSeverity severity, string category, string message, Exception? exception = null)
    {
        var entry = new LogEntry(DateTime.Now, severity, category, message, exception);

        lock (_lock)
        {
            _entries.AddLast(entry);
            while (_entries.Count > MaxEntries)
                _entries.RemoveFirst();
        }

        if (MirrorToDebug)
            System.Diagnostics.Debug.WriteLine(entry.ToFormattedLine());

        EntryAdded?.Invoke(null, entry);
    }

    /// <summary>
    /// Returns a snapshot of all current entries (caller-owned copy; safe to enumerate).
    /// </summary>
    public static IReadOnlyList<LogEntry> GetAll()
    {
        lock (_lock)
        {
            return _entries.ToList();
        }
    }

    /// <summary>
    /// Returns the count of entries currently held in memory.
    /// </summary>
    public static int Count
    {
        get { lock (_lock) return _entries.Count; }
    }

    /// <summary>
    /// Removes all entries from memory and fires the Cleared event.
    /// </summary>
    public static void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
        Cleared?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// Writes all current entries to <paramref name="filePath"/> as a plain-text log,
    /// one entry per line. Returns the number of entries written.
    /// </summary>
    public static int Export(string filePath)
    {
        var snapshot = GetAll();
        using var sw = new StreamWriter(filePath, append: false);
        sw.WriteLine($"# FastFile editor log export");
        sw.WriteLine($"# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sw.WriteLine($"# Entries:   {snapshot.Count}");
        sw.WriteLine();
        foreach (var entry in snapshot)
        {
            sw.WriteLine(entry.ToFormattedLine());
            // Stack traces on their own indented lines for readability
            if (entry.Exception?.StackTrace != null)
            {
                foreach (var line in entry.Exception.StackTrace.Split('\n'))
                    sw.WriteLine("    " + line.TrimEnd('\r'));
            }
        }
        return snapshot.Count;
    }
}
