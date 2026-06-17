using HumanFortress.Core.Diagnostics;

namespace HumanFortress.App.Diagnostics;

public sealed class DiagnosticSnapshot
{
    private DiagnosticSnapshot(
        IReadOnlyList<DiagnosticEvent> events,
        IReadOnlyDictionary<DiagnosticLevel, int> levelCounts,
        IReadOnlyDictionary<string, int> categoryCounts,
        IReadOnlyList<DiagnosticIssueSummary> contentIssues)
    {
        Events = events;
        LevelCounts = levelCounts;
        CategoryCounts = categoryCounts;
        ContentIssues = contentIssues;
    }

    public IReadOnlyList<DiagnosticEvent> Events { get; }
    public IReadOnlyDictionary<DiagnosticLevel, int> LevelCounts { get; }
    public IReadOnlyDictionary<string, int> CategoryCounts { get; }
    public IReadOnlyList<DiagnosticIssueSummary> ContentIssues { get; }
    public int TotalCount => Events.Count;
    public int WarningOrHigherCount => CountAtLeast(DiagnosticLevel.Warning);
    public int ErrorOrHigherCount => CountAtLeast(DiagnosticLevel.Error);

    public static DiagnosticSnapshot FromEvents(IEnumerable<DiagnosticEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var orderedEvents = events
            .OrderBy(diagnosticEvent => diagnosticEvent.Sequence)
            .ToArray();

        var levelCounts = orderedEvents
            .GroupBy(diagnosticEvent => diagnosticEvent.Level)
            .ToDictionary(group => group.Key, group => group.Count());

        var categoryCounts = orderedEvents
            .GroupBy(diagnosticEvent => diagnosticEvent.Category, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var contentIssues = orderedEvents
            .Where(IsContentIssue)
            .Select(DiagnosticIssueSummary.FromEvent)
            .ToArray();

        return new DiagnosticSnapshot(orderedEvents, levelCounts, categoryCounts, contentIssues);
    }

    private int CountAtLeast(DiagnosticLevel minimumLevel)
    {
        return Events.Count(diagnosticEvent => diagnosticEvent.Level >= minimumLevel);
    }

    private static bool IsContentIssue(DiagnosticEvent diagnosticEvent)
    {
        return diagnosticEvent.Level >= DiagnosticLevel.Warning
            && diagnosticEvent.Category.StartsWith("Content.", StringComparison.Ordinal);
    }
}

public sealed record DiagnosticIssueSummary(
    DiagnosticLevel Level,
    string Category,
    string Code,
    string Message,
    long Sequence,
    DateTimeOffset TimestampUtc)
{
    public static DiagnosticIssueSummary FromEvent(DiagnosticEvent diagnosticEvent)
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
