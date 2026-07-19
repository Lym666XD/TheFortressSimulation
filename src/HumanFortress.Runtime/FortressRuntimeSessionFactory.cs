using HumanFortress.Contracts.Content.Loading;

namespace HumanFortress.Runtime;

public static class FortressRuntimeSessionFactory
{
    public static IFortressRuntimeAppSessionPorts Create(
        string baseDir,
        bool strictContent,
        bool contentWarningsAsErrors,
        Action<string>? log = null,
        Func<string, Action<string>>? createLogCallback = null,
        Action<FortressContentLoadReport>? logContentIssues = null)
    {
        return CreateCore(
            baseDir,
            strictContent,
            contentWarningsAsErrors,
            log,
            createLogCallback,
            logContentIssues);
    }

    internal static IFortressRuntimeSessionPorts CreateFull(
        string baseDir,
        bool strictContent,
        bool contentWarningsAsErrors,
        Action<string>? log = null,
        Func<string, Action<string>>? createLogCallback = null,
        Action<FortressContentLoadReport>? logContentIssues = null)
    {
        return CreateCore(
            baseDir,
            strictContent,
            contentWarningsAsErrors,
            log,
            createLogCallback,
            logContentIssues);
    }

    internal static IFortressRuntimeHeadlessScenarioSessionPorts CreateHeadlessScenario(
        string baseDir,
        ulong rngSeed,
        int transportPlanningWorkerCount = 1,
        bool strictContent = true,
        bool contentWarningsAsErrors = true,
        Action<string>? log = null,
        Func<string, Action<string>>? createLogCallback = null,
        Action<FortressContentLoadReport>? logContentIssues = null)
    {
        if (transportPlanningWorkerCount < 1)
            throw new ArgumentOutOfRangeException(nameof(transportPlanningWorkerCount));

        return new FortressRuntimeSessionCore(
            new FortressRuntimeSessionCoreOptions(
                baseDir,
                strictContent,
                contentWarningsAsErrors,
                log,
                createLogCallback,
                logContentIssues,
                rngSeed,
                transportPlanningWorkerCount));
    }

    private static FortressRuntimeSessionCore CreateCore(
        string baseDir,
        bool strictContent,
        bool contentWarningsAsErrors,
        Action<string>? log,
        Func<string, Action<string>>? createLogCallback,
        Action<FortressContentLoadReport>? logContentIssues)
    {
        return new FortressRuntimeSessionCore(
            baseDir,
            strictContent,
            contentWarningsAsErrors,
            log,
            createLogCallback,
            logContentIssues);
    }
}
