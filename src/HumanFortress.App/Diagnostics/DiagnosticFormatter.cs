using System.Globalization;
using HumanFortress.Core.Diagnostics;

namespace HumanFortress.App.Diagnostics;

internal static class DiagnosticFormatter
{
    public static string Format(DiagnosticEvent diagnosticEvent)
    {
        ArgumentNullException.ThrowIfNull(diagnosticEvent);

        var tick = diagnosticEvent.Tick.HasValue
            ? string.Create(CultureInfo.InvariantCulture, $" tick={diagnosticEvent.Tick.Value}")
            : string.Empty;

        var line = string.Create(
            CultureInfo.InvariantCulture,
            $"{diagnosticEvent.TimestampUtc:O} seq={diagnosticEvent.Sequence} level={diagnosticEvent.Level} category={diagnosticEvent.Category} thread={diagnosticEvent.ManagedThreadId}{tick} {diagnosticEvent.Message}");

        if (string.IsNullOrWhiteSpace(diagnosticEvent.Exception))
        {
            return line;
        }

        return line + Environment.NewLine + diagnosticEvent.Exception;
    }
}
