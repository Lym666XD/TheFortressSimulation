namespace HumanFortress.Contracts.Diagnostics;

/// <summary>
/// Convenience helpers for writing typed diagnostic events.
/// </summary>
public static class DiagnosticSinkExtensions
{
    public static void Trace(this IDiagnosticSink sink, string category, string message, ulong? tick = null)
    {
        Write(sink, DiagnosticLevel.Trace, category, message, exception: null, tick);
    }

    public static void Debug(this IDiagnosticSink sink, string category, string message, ulong? tick = null)
    {
        Write(sink, DiagnosticLevel.Debug, category, message, exception: null, tick);
    }

    public static void Information(this IDiagnosticSink sink, string category, string message, ulong? tick = null)
    {
        Write(sink, DiagnosticLevel.Information, category, message, exception: null, tick);
    }

    public static void Warning(this IDiagnosticSink sink, string category, string message, ulong? tick = null)
    {
        Write(sink, DiagnosticLevel.Warning, category, message, exception: null, tick);
    }

    public static void Error(this IDiagnosticSink sink, string category, string message, Exception? exception = null, ulong? tick = null)
    {
        Write(sink, DiagnosticLevel.Error, category, message, exception, tick);
    }

    public static void Fatal(this IDiagnosticSink sink, string category, string message, Exception? exception = null, ulong? tick = null)
    {
        Write(sink, DiagnosticLevel.Fatal, category, message, exception, tick);
    }

    private static void Write(
        IDiagnosticSink sink,
        DiagnosticLevel level,
        string category,
        string message,
        Exception? exception,
        ulong? tick)
    {
        ArgumentNullException.ThrowIfNull(sink);
        sink.Write(DiagnosticEvent.Create(level, category, message, exception, tick));
    }
}
