using HumanFortress.Contracts.Diagnostics;

namespace HumanFortress.Simulation.Diagnostics;

internal static class SimulationDiagnostics
{
    internal static void Information(
        IDiagnosticSink sink,
        string category,
        string message,
        ulong? tick = null)
    {
        Emit(sink, DiagnosticLevel.Information, category, message, exception: null, tick);
    }

    internal static void Error(
        IDiagnosticSink sink,
        string category,
        string message,
        Exception? exception = null,
        ulong? tick = null)
    {
        Emit(sink, DiagnosticLevel.Error, category, message, exception, tick);
    }

    private static void Emit(
        IDiagnosticSink sink,
        DiagnosticLevel level,
        string category,
        string message,
        Exception? exception,
        ulong? tick)
    {
        ArgumentNullException.ThrowIfNull(sink);

        try
        {
            sink.Write(DiagnosticEvent.Create(level, category, message, exception, tick));
        }
        catch
        {
            // Diagnostics must never change authoritative mutation outcomes.
        }
    }
}
