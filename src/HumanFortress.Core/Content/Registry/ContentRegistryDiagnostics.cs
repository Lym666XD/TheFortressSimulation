using HumanFortress.Core.Diagnostics;

namespace HumanFortress.Core.Content.Registry;

internal static class ContentRegistryDiagnostics
{
    public static void Emit(string message)
    {
        Emit(message, ResolveLevel(message));
    }

    public static void Emit(string message, DiagnosticLevel level)
    {
        DiagnosticHub.Sink.Write(DiagnosticEvent.Create(level, "Content.Registry", message));

        if (!DiagnosticHub.IsConfigured)
        {
            Console.WriteLine(message);
        }
    }

    public static void Emit(string message, Exception exception)
    {
        DiagnosticHub.Sink.Write(DiagnosticEvent.Create(DiagnosticLevel.Error, "Content.Registry", message, exception));

        if (!DiagnosticHub.IsConfigured)
        {
            Console.WriteLine(message);
        }
    }

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
