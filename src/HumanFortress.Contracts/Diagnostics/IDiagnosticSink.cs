namespace HumanFortress.Contracts.Diagnostics;

/// <summary>
/// Receives diagnostic events without forcing callers to know where logs are written.
/// </summary>
public interface IDiagnosticSink
{
    void Write(DiagnosticEvent diagnosticEvent);
}
