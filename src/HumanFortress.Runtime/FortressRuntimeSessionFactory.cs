using HumanFortress.Content.Loading;

namespace HumanFortress.Runtime;

public static class FortressRuntimeSessionFactory
{
    public static IFortressRuntimeSessionPorts Create(
        string baseDir,
        bool strictContent,
        bool contentWarningsAsErrors,
        Action<string>? log = null,
        Func<string, Action<string>>? createLogCallback = null,
        Action<FortressContentLoadResult>? logContentIssues = null)
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
