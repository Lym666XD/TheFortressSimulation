using HumanFortress.Contracts.Diagnostics;

namespace HumanFortress.Content.Registry;

internal static class ContentRegistryDiagnostics
{
    private static readonly AsyncLocal<IDiagnosticSink?> ScopedSink = new();

    internal static IDisposable PushSink(IDiagnosticSink diagnosticSink)
    {
        ArgumentNullException.ThrowIfNull(diagnosticSink);

        var previous = ScopedSink.Value;
        ScopedSink.Value = diagnosticSink;
        return new DiagnosticScope(previous);
    }

    internal static void Emit(string message)
    {
        Emit(message, ResolveLevel(message));
    }

    internal static void Emit(string message, DiagnosticLevel level)
    {
        Diagnostics.Write(DiagnosticEvent.Create(level, "Content.Registry", message));
    }

    internal static void Emit(string message, Exception exception)
    {
        Diagnostics.Write(DiagnosticEvent.Create(DiagnosticLevel.Error, "Content.Registry", message, exception));
    }

    private static IDiagnosticSink Diagnostics => ScopedSink.Value ?? DiagnosticHub.Sink;

    private static DiagnosticLevel ResolveLevel(string message)
    {
        if (message.Contains("Fatal", StringComparison.OrdinalIgnoreCase))
        {
            return DiagnosticLevel.Fatal;
        }

        if (message.Contains("Error", StringComparison.OrdinalIgnoreCase))
        {
            return DiagnosticLevel.Error;
        }

        if (message.Contains("Warning", StringComparison.OrdinalIgnoreCase))
        {
            return DiagnosticLevel.Warning;
        }

        return DiagnosticLevel.Information;
    }

    private sealed class DiagnosticScope : IDisposable
    {
        private readonly IDiagnosticSink? _previous;
        private bool _disposed;

        internal DiagnosticScope(IDiagnosticSink? previous)
        {
            _previous = previous;
        }

        void IDisposable.Dispose()
        {
            if (_disposed)
                return;

            ScopedSink.Value = _previous;
            _disposed = true;
        }
    }
}
