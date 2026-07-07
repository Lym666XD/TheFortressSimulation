using HumanFortress.Contracts.Diagnostics;

namespace HumanFortress.App.Diagnostics;

internal sealed class DiagnosticSnapshot
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

    internal IReadOnlyList<DiagnosticEvent> Events { get; }
    internal IReadOnlyDictionary<DiagnosticLevel, int> LevelCounts { get; }
    internal IReadOnlyDictionary<string, int> CategoryCounts { get; }
    internal IReadOnlyList<DiagnosticIssueSummary> ContentIssues { get; }
    internal int TotalCount => Events.Count;
    internal int WarningOrHigherCount => CountAtLeast(DiagnosticLevel.Warning);
    internal int ErrorOrHigherCount => CountAtLeast(DiagnosticLevel.Error);

    internal static DiagnosticSnapshot FromEvents(IEnumerable<DiagnosticEvent> events)
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
