namespace HumanFortress.Core.Diagnostics;

/// <summary>
/// Diagnostic sink that intentionally drops all events.
/// </summary>
public sealed class NullDiagnosticSink : IDiagnosticSink
{
    public static NullDiagnosticSink Instance { get; } = new();

    private NullDiagnosticSink()
    {
    }

    public void Write(DiagnosticEvent diagnosticEvent)
    {
    }
}
