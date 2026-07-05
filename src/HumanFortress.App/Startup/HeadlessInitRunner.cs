using HumanFortress.App.GameStates;

namespace HumanFortress.App.Startup;

internal static class HeadlessInitRunner
{
    internal static void Run(AppStartupOptions options)
    {
        try
        {
            var runtime = new GameStateRuntimeCoordinator(
                GameStateRuntimeConfiguration.CreateDefault(
                    options.StrictContent,
                    options.ContentWarningsAsErrors));
            runtime.InitializeWorld(sizeInChunks: 2, maxZ: 50);
            runtime.StopIfRunning();
            Logger.Log("[HEADLESS] Init-only completed");
        }
        catch (Exception ex)
        {
            Logger.Log($"[HEADLESS] ERROR: {ex.Message}");
            Environment.ExitCode = 1;
        }
        finally
        {
            Logger.Close();
        }
    }
}
