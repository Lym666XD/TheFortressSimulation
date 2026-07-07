using HumanFortress.App.Diagnostics;
using HumanFortress.Contracts.Content.Loading;
using HumanFortress.Runtime;

namespace HumanFortress.App.Startup;

internal static class StartupContentGate
{
    internal static bool TryLoadAndValidate(
        string baseDir,
        AppStartupOptions options,
        out FortressContentLoadReport contentLoad)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDir);

        contentLoad = FortressRuntimeContentLoader.LoadStartupContent(baseDir);
        FortressContentIssueLogger.LogIssues(contentLoad);
        return !options.StrictContent || TryEnforceStrictContent(contentLoad, options.ContentWarningsAsErrors);
    }

    internal static void LogResolvedPath(FortressContentLoadReport contentLoad)
    {
        ArgumentNullException.ThrowIfNull(contentLoad);

        if (contentLoad.ContentPath.ResolvedPath != null)
        {
            Logger.Log($"[STARTUP] Content loaded successfully from {contentLoad.ContentPath.ResolvedPath}");
            return;
        }

        Logger.Log("[STARTUP] WARNING: Content directory not found. Tried:");
        Logger.Log($"  - {contentLoad.ContentPath.PublishedPath}");
        Logger.Log($"  - {contentLoad.ContentPath.DevelopmentPath}");
    }

    private static bool TryEnforceStrictContent(FortressContentLoadReport contentLoad, bool contentWarningsAsErrors)
    {
        try
        {
            contentLoad.ThrowIfInvalid(contentWarningsAsErrors);
            return true;
        }
        catch (FortressContentLoadException ex)
        {
            Logger.Log("[STARTUP] STRICT CONTENT LOAD FAILED");
            foreach (var issue in ex.BlockingIssues)
            {
                Logger.Log($"[STARTUP] {issue}");
            }

            Environment.ExitCode = 1;
            Logger.Close();
            return false;
        }
    }
}
