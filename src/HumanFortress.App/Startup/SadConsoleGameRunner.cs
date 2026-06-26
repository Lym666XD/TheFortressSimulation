using SadConsole;
using SadConsole.Configuration;

namespace HumanFortress.App.Startup;

internal static class SadConsoleGameRunner
{
    public static void Run(AppStartupOptions options)
    {
        Settings.WindowTitle = "HumanFortress";
        var gameApp = new SadConsoleGameApp(options);

        var gameStartup = new Builder()
            .SetScreenSize(120, 40)
            .OnStart(gameApp.OnStarted)
            .ConfigureFonts(true)
            .IsStartingScreenFocused(true);

        Game.Create(gameStartup);
        Game.Instance.Run();

        gameApp.Shutdown();

        Game.Instance.Dispose();

        Logger.Log("[SHUTDOWN] Game shutting down normally");
        Logger.Close();
    }
}
