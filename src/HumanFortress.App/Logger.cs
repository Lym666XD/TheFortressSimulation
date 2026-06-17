using HumanFortress.App.Diagnostics;
using HumanFortress.Core.Diagnostics;

namespace HumanFortress.App;

/// <summary>
/// App-facing diagnostics facade. Existing Logger.Log callers are bridged into the async diagnostic pipeline.
/// </summary>
public static class Logger
{
    private static readonly object _lockObject = new();
    private static AsyncDiagnosticDispatcher? _dispatcher;
    private static IDiagnosticSink _sink = NullDiagnosticSink.Instance;
    private static InMemoryRingBufferDiagnosticSink? _ringBuffer;
    private static IReadOnlyList<DiagnosticEvent> _lastEvents = Array.Empty<DiagnosticEvent>();

    public static IDiagnosticSink Sink => _sink;

    public static IReadOnlyList<DiagnosticEvent> RecentEvents => _ringBuffer?.Snapshot() ?? _lastEvents;

    public static DiagnosticSnapshot GetSnapshot()
    {
        return DiagnosticSnapshot.FromEvents(RecentEvents);
    }

    public static void Initialize(string logPath)
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

    public static void Log(string message)
    {
        Write(DiagnosticLevel.Information, LegacyLogCategoryResolver.Resolve(message), message);
    }

    public static void Trace(string category, string message, ulong? tick = null)
    {
        Write(DiagnosticLevel.Trace, category, message, tick: tick);
    }

    public static void Debug(string category, string message, ulong? tick = null)
    {
        Write(DiagnosticLevel.Debug, category, message, tick: tick);
    }

    public static void Info(string category, string message, ulong? tick = null)
    {
        Write(DiagnosticLevel.Information, category, message, tick: tick);
    }

    public static void Warning(string category, string message, ulong? tick = null)
    {
        Write(DiagnosticLevel.Warning, category, message, tick: tick);
    }

    public static void Error(string category, string message, Exception? exception = null, ulong? tick = null)
    {
        Write(DiagnosticLevel.Error, category, message, exception, tick);
    }

    public static void Fatal(string category, string message, Exception? exception = null, ulong? tick = null)
    {
        Write(DiagnosticLevel.Fatal, category, message, exception, tick);
    }

    public static Action<string> CreateCallback(string category, DiagnosticLevel level = DiagnosticLevel.Information)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        return message => Write(level, category, message);
    }

    public static void Close()
    {
        lock (_lockObject)
        {
            CloseLocked();
        }
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

internal static class LegacyLogCategoryResolver
{
    public static string Resolve(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "App.Legacy";
        }

        var prefix = ExtractBracketPrefix(message);
        if (!string.IsNullOrWhiteSpace(prefix))
        {
            return PrefixToCategory(prefix);
        }

        if (message.Contains("ContentRegistry", StringComparison.OrdinalIgnoreCase))
        {
            return "Content.Registry";
        }

        if (message.Contains("ItemManager", StringComparison.OrdinalIgnoreCase))
        {
            return "Simulation.Items";
        }

        if (message.Contains("CreatureManager", StringComparison.OrdinalIgnoreCase))
        {
            return "Simulation.Creatures";
        }

        return "App.Legacy";
    }

    private static string? ExtractBracketPrefix(string message)
    {
        if (message.Length < 3 || message[0] != '[')
        {
            return null;
        }

        var end = message.IndexOf(']', StringComparison.Ordinal);
        if (end <= 1)
        {
            return null;
        }

        return message.Substring(1, end - 1);
    }

    private static string PrefixToCategory(string prefix)
    {
        if (prefix.StartsWith("Content", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("RECIPES", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("CONSTR.REG", StringComparison.OrdinalIgnoreCase))
        {
            return "Content.Registry";
        }

        if (prefix.StartsWith("STARTUP", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("SHUTDOWN", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("NATIVE", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("HEADLESS", StringComparison.OrdinalIgnoreCase))
        {
            return "Runtime.App";
        }

        if (prefix.StartsWith("NAV", StringComparison.OrdinalIgnoreCase))
        {
            return "Navigation.Manager";
        }

        if (prefix.StartsWith("DIFF", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("EJECT", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("ItemManager", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("CreatureManager", StringComparison.OrdinalIgnoreCase))
        {
            return "Simulation";
        }

        if (prefix.StartsWith("MINING", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("HAUL", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("BUILD", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("CM-", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("ORDERS", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("STOCKPILE", StringComparison.OrdinalIgnoreCase))
        {
            return "Jobs";
        }

        if (prefix.StartsWith("UI", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("MOUSE", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("KEY", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("RIGHT-CLICK", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("RenderMap", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("FortressState", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("GenerateFortressMap", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("BuildSnapshot", StringComparison.OrdinalIgnoreCase))
        {
            return "UI";
        }

        return "App.Legacy";
    }
}
