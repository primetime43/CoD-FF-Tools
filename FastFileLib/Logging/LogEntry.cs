namespace FastFileLib.Logging;

/// <summary>
/// A single log entry captured by LogService.
/// </summary>
public class LogEntry
{
    public DateTime Timestamp { get; }
    public LogSeverity Severity { get; }
    public string Category { get; }
    public string Message { get; }
    public Exception? Exception { get; }

    public LogEntry(DateTime timestamp, LogSeverity severity, string category, string message, Exception? exception)
    {
        Timestamp = timestamp;
        Severity = severity;
        Category = category ?? "";
        Message = message ?? "";
        Exception = exception;
    }

    /// <summary>
    /// Formats the entry as a single line suitable for export or display:
    ///   [HH:mm:ss.fff] [SEVERITY] [Category] message  (Exception: ...)
    /// </summary>
    public string ToFormattedLine()
    {
        string baseLine = $"[{Timestamp:HH:mm:ss.fff}] [{Severity}] [{Category}] {Message}";
        if (Exception != null)
            baseLine += $"  (Exception: {Exception.GetType().Name}: {Exception.Message})";
        return baseLine;
    }
}
