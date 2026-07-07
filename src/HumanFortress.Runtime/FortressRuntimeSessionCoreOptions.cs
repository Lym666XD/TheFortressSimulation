using HumanFortress.Contracts.Content.Loading;

namespace HumanFortress.Runtime;

internal sealed class FortressRuntimeSessionCoreOptions
{
    internal FortressRuntimeSessionCoreOptions(
        string baseDir,
        bool strictContent,
        bool contentWarningsAsErrors,
        Action<string>? log = null,
        Func<string, Action<string>>? createLogCallback = null,
        Action<FortressContentLoadReport>? logContentIssues = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDir);

        BaseDir = baseDir;
        StrictContent = strictContent;
        ContentWarningsAsErrors = contentWarningsAsErrors;
        Log = log;
        CreateLogCallback = createLogCallback;
        LogContentIssues = logContentIssues;
    }

    internal string BaseDir { get; }

    internal bool StrictContent { get; }

    internal bool ContentWarningsAsErrors { get; }

    internal Action<string>? Log { get; }

    internal Func<string, Action<string>>? CreateLogCallback { get; }

    internal Action<FortressContentLoadReport>? LogContentIssues { get; }
}
