using HumanFortress.Core.Diagnostics;

namespace HumanFortress.Simulation.Diagnostics;

internal static class SimulationDiagnostics
{
    internal static void Information(
        Action<string>? callback,
        string category,
        string message,
        ulong? tick = null)
    {
        Emit(callback, DiagnosticLevel.Information, category, message, exception: null, tick);
    }

    internal static void Error(
        Action<string>? callback,
        string category,
        string message,
        Exception? exception = null,
        ulong? tick = null)
    {
        Emit(callback, DiagnosticLevel.Error, category, message, exception, tick);
    }

    private static void Emit(
        Action<string>? callback,
        DiagnosticLevel level,
        string category,
        string message,
        Exception? exception,
        ulong? tick)
    {
        if (callback != null)
        {
            callback(message);
            return;
        }

        if (DiagnosticHub.IsConfigured)
        {
            DiagnosticHub.Sink.Write(DiagnosticEvent.Create(level, category, message, exception, tick));
            return;
        }

        Console.WriteLine(message);
    }
}
