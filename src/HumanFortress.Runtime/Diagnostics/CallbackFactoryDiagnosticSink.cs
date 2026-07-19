using System.Collections.Concurrent;
using HumanFortress.Contracts.Diagnostics;

namespace HumanFortress.Runtime.Diagnostics;

/// <summary>
/// Adapts the App callback factory into a session-owned diagnostic sink.
/// Callback lookup is cached per category and never mutates process-global state.
/// </summary>
internal sealed class CallbackFactoryDiagnosticSink : IDiagnosticSink
{
    private readonly Func<string, Action<string>> _callbackFactory;
    private readonly ConcurrentDictionary<string, Action<string>> _callbacks = new(StringComparer.Ordinal);

    internal CallbackFactoryDiagnosticSink(Func<string, Action<string>> callbackFactory)
    {
        _callbackFactory = callbackFactory ?? throw new ArgumentNullException(nameof(callbackFactory));
    }

    void IDiagnosticSink.Write(DiagnosticEvent diagnosticEvent)
    {
        ArgumentNullException.ThrowIfNull(diagnosticEvent);

        try
        {
            var callback = _callbacks.GetOrAdd(diagnosticEvent.Category, _callbackFactory);
            callback(diagnosticEvent.Message);
        }
        catch
        {
            // Diagnostics must never affect authoritative session behavior.
        }
    }
}
