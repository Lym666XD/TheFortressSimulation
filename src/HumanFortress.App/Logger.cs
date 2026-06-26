using HumanFortress.App.Diagnostics;
using HumanFortress.Core.Diagnostics;

namespace HumanFortress.App;

/// <summary>
/// App-facing diagnostics facade. Existing Logger.Log callers are bridged into the async diagnostic pipeline.
/// </summary>
internal static partial class Logger
{
    private static readonly object _lockObject = new();
    private static AsyncDiagnosticDispatcher? _dispatcher;
    private static IDiagnosticSink _sink = NullDiagnosticSink.Instance;
    private static InMemoryRingBufferDiagnosticSink? _ringBuffer;
    private static IReadOnlyList<DiagnosticEvent> _lastEvents = Array.Empty<DiagnosticEvent>();

    internal static IDiagnosticSink Sink => _sink;

    internal static IReadOnlyList<DiagnosticEvent> RecentEvents => _ringBuffer?.Snapshot() ?? _lastEvents;

    internal static DiagnosticSnapshot GetSnapshot()
    {
        return DiagnosticSnapshot.FromEvents(RecentEvents);
    }

    internal static void Log(string message)
    {
        Write(DiagnosticLevel.Information, LegacyLogCategoryResolver.Resolve(message), message);
    }

    private static void Write(
        DiagnosticLevel level,
        string category,
        string message,
        Exception? exception = null,
        ulong? tick = null)
    {
        _sink.Write(DiagnosticEvent.Create(level, category, message, exception, tick));
    }

}
