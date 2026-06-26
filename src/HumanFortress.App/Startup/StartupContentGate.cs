using HumanFortress.App.Diagnostics;
using HumanFortress.Content.Loading;

namespace HumanFortress.App.Startup;

internal static class StartupContentGate
{
    public static bool TryLoadAndValidate(
        string baseDir,
        AppStartupOptions options,
        out FortressContentLoadResult contentLoad)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDir);

        contentLoad = FortressContentLoader.Load(baseDir, includeCoreCatalogs: false);
        FortressContentIssueLogger.LogIssues(contentLoad);
        return !options.StrictContent || TryEnforceStrictContent(contentLoad, options.ContentWarningsAsErrors);
    }

    public static void LogResolvedPath(FortressContentLoadResult contentLoad)
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

    private static bool TryEnforceStrictContent(FortressContentLoadResult contentLoad, bool contentWarningsAsErrors)
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
