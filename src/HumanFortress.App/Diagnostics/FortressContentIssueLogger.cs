using HumanFortress.Contracts.Content.Loading;

namespace HumanFortress.App.Diagnostics;

internal static class FortressContentIssueLogger
{
    public static void LogIssues(FortressContentLoadReport result)
    {
        ArgumentNullException.ThrowIfNull(result);

        foreach (var issue in result.Issues)
        {
            var message = $"[{issue.Code}] {issue.Message}";
            if (issue.Severity == FortressContentIssueSeverity.Error)
            {
                Logger.Error("Content.Registry", message);
            }
            else
            {
                Logger.Warning("Content.Registry", message);
            }
        }
    }
}
