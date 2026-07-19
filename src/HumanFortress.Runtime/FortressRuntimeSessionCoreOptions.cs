using HumanFortress.Contracts.Content.Loading;
using HumanFortress.Runtime.Session;

namespace HumanFortress.Runtime;

internal sealed class FortressRuntimeSessionCoreOptions
{
    internal FortressRuntimeSessionCoreOptions(
        string baseDir,
        bool strictContent,
        bool contentWarningsAsErrors,
        Action<string>? log = null,
        Func<string, Action<string>>? createLogCallback = null,
        Action<FortressContentLoadReport>? logContentIssues = null,
        ulong rngSeed = RuntimeSessionServices.DefaultRngSeed,
        int transportPlanningWorkerCount = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDir);
        if (transportPlanningWorkerCount < 1)
            throw new ArgumentOutOfRangeException(nameof(transportPlanningWorkerCount));

        BaseDir = baseDir;
        StrictContent = strictContent;
        ContentWarningsAsErrors = contentWarningsAsErrors;
        Log = log;
        CreateLogCallback = createLogCallback;
        LogContentIssues = logContentIssues;
        RngSeed = rngSeed;
        TransportPlanningWorkerCount = transportPlanningWorkerCount;
    }

    internal string BaseDir { get; }

    internal bool StrictContent { get; }

    internal bool ContentWarningsAsErrors { get; }

    internal Action<string>? Log { get; }

    internal Func<string, Action<string>>? CreateLogCallback { get; }

    internal Action<FortressContentLoadReport>? LogContentIssues { get; }

    internal ulong RngSeed { get; }

    internal int TransportPlanningWorkerCount { get; }
}
