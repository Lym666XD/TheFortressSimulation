using HumanFortress.Core.Diagnostics;

namespace HumanFortress.App.Diagnostics;

internal sealed record DiagnosticIssueSummary(
    DiagnosticLevel Level,
    string Category,
    string Code,
    string Message,
    long Sequence,
    DateTimeOffset TimestampUtc)
{
    internal static DiagnosticIssueSummary FromEvent(DiagnosticEvent diagnosticEvent)
    {
        ArgumentNullException.ThrowIfNull(diagnosticEvent);

        var code = diagnosticEvent.Category;
        var message = diagnosticEvent.Message;

        if (message.Length > 2 && message[0] == '[')
        {
            var end = message.IndexOf(']', StringComparison.Ordinal);
            if (end > 1)
            {
                code = message.Substring(1, end - 1);
                message = message[(end + 1)..].TrimStart();
            }
        }

        return new DiagnosticIssueSummary(
            diagnosticEvent.Level,
            diagnosticEvent.Category,
            code,
            message,
            diagnosticEvent.Sequence,
            diagnosticEvent.TimestampUtc);
    }
}
