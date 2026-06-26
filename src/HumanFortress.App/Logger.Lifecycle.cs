using HumanFortress.App.Diagnostics;
using HumanFortress.Core.Diagnostics;

namespace HumanFortress.App;

internal static partial class Logger
{
    internal static void Initialize(string logPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logPath);

        lock (_lockObject)
        {
            CloseLocked();
            _lastEvents = Array.Empty<DiagnosticEvent>();

            var fullLogPath = Path.GetFullPath(logPath);
            var baseDirectory = Path.GetDirectoryName(fullLogPath) ?? AppContext.BaseDirectory;
            var categoryLogDirectory = Path.Combine(baseDirectory, "logs");

            _ringBuffer = new InMemoryRingBufferDiagnosticSink(capacity: 2048);
            var routingSink = new CategoryRoutingDiagnosticSink(fullLogPath, categoryLogDirectory);
            var compositeSink = new CompositeDiagnosticSink(routingSink, _ringBuffer);
            _dispatcher = new AsyncDiagnosticDispatcher(compositeSink);
            _sink = _dispatcher;
            DiagnosticHub.Sink = _sink;
        }
    }

    internal static void Close()
    {
        lock (_lockObject)
        {
            CloseLocked();
        }
    }

    private static void CloseLocked()
    {
        _dispatcher?.Dispose();
        _lastEvents = _ringBuffer?.Snapshot() ?? _lastEvents;
        _dispatcher = null;
        _sink = NullDiagnosticSink.Instance;
        DiagnosticHub.Sink = NullDiagnosticSink.Instance;
        _ringBuffer = null;
    }
}
