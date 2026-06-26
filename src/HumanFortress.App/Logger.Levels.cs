using HumanFortress.Core.Diagnostics;

namespace HumanFortress.App;

internal static partial class Logger
{
    internal static void Trace(string category, string message, ulong? tick = null)
    {
        Write(DiagnosticLevel.Trace, category, message, tick: tick);
    }

    internal static void Debug(string category, string message, ulong? tick = null)
    {
        Write(DiagnosticLevel.Debug, category, message, tick: tick);
    }

    internal static void Info(string category, string message, ulong? tick = null)
    {
        Write(DiagnosticLevel.Information, category, message, tick: tick);
    }

    internal static void Warning(string category, string message, ulong? tick = null)
    {
        Write(DiagnosticLevel.Warning, category, message, tick: tick);
    }

    internal static void Error(string category, string message, Exception? exception = null, ulong? tick = null)
    {
        Write(DiagnosticLevel.Error, category, message, exception, tick);
    }

    internal static void Fatal(string category, string message, Exception? exception = null, ulong? tick = null)
    {
        Write(DiagnosticLevel.Fatal, category, message, exception, tick);
    }

    internal static Action<string> CreateCallback(string category, DiagnosticLevel level = DiagnosticLevel.Information)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        return message => Write(level, category, message);
    }
}
