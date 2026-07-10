using HumanFortress.Contracts.Diagnostics;

namespace HumanFortress.Content.Registry;

internal static class ContentRegistryDiagnostics
{
    internal static IDiagnosticSink? DiagnosticSink { get; set; }

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

    private static IDiagnosticSink Diagnostics => DiagnosticSink ?? DiagnosticHub.Sink;

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
}
