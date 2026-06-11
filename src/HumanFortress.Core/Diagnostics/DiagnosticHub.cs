namespace HumanFortress.Core.Diagnostics;

/// <summary>
/// Transitional process-wide diagnostic bridge for core services not yet created through dependency injection.
/// </summary>
public static class DiagnosticHub
{
    private static IDiagnosticSink _sink = NullDiagnosticSink.Instance;

    public static IDiagnosticSink Sink
    {
        get => _sink;
        set => _sink = value ?? NullDiagnosticSink.Instance;
    }

    public static bool IsConfigured => !ReferenceEquals(_sink, NullDiagnosticSink.Instance);

    public static void Error(string category, string message, Exception? exception = null, ulong? tick = null)
    {
        _sink.Error(category, message, exception, tick);
    }
}
