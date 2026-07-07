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
