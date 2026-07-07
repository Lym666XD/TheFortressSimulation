using HumanFortress.App.Diagnostics;
using HumanFortress.Contracts.Content.Loading;

namespace HumanFortress.App.GameStates;

internal sealed class GameStateRuntimeConfiguration
{
    private GameStateRuntimeConfiguration(
        string baseDirectory,
        bool strictContent,
        bool contentWarningsAsErrors,
        Action<string> log,
        Func<string, Action<string>> createLogCallback,
        Action<FortressContentLoadReport> logContentIssues)
    {
        BaseDirectory = baseDirectory ?? throw new ArgumentNullException(nameof(baseDirectory));
        StrictContent = strictContent;
        ContentWarningsAsErrors = contentWarningsAsErrors;
        Log = log ?? throw new ArgumentNullException(nameof(log));
        CreateLogCallback = createLogCallback ?? throw new ArgumentNullException(nameof(createLogCallback));
        LogContentIssues = logContentIssues ?? throw new ArgumentNullException(nameof(logContentIssues));
    }

    internal string BaseDirectory { get; }

    internal bool StrictContent { get; }

    internal bool ContentWarningsAsErrors { get; }

    internal Action<string> Log { get; }

    internal Func<string, Action<string>> CreateLogCallback { get; }

    internal Action<FortressContentLoadReport> LogContentIssues { get; }

    internal static GameStateRuntimeConfiguration CreateDefault(
        bool strictContent,
        bool contentWarningsAsErrors)
    {
        return new GameStateRuntimeConfiguration(
            AppContext.BaseDirectory,
            strictContent,
            contentWarningsAsErrors,
            Logger.Log,
            category => Logger.CreateCallback(category),
            FortressContentIssueLogger.LogIssues);
    }
}
