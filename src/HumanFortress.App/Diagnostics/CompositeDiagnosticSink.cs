using HumanFortress.Core.Diagnostics;

namespace HumanFortress.App.Diagnostics;

internal sealed class CompositeDiagnosticSink : IDiagnosticSink, IDisposable
{
    private readonly IReadOnlyList<IDiagnosticSink> _sinks;

    public CompositeDiagnosticSink(params IDiagnosticSink[] sinks)
    {
        _sinks = sinks ?? Array.Empty<IDiagnosticSink>();
    }

    public void Write(DiagnosticEvent diagnosticEvent)
    {
        foreach (var sink in _sinks)
        {
            sink.Write(diagnosticEvent);
        }
    }

    public void Dispose()
    {
        foreach (var sink in _sinks)
        {
            if (sink is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
