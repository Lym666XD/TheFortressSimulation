using System;
using System.IO;
using HumanFortress.App.Startup;

namespace HumanFortress.App;

/// <summary>
/// Main entry point for the game.
/// </summary>
public static class Program
{
    public static void Main(string[] args)
    {
        var startupOptions = AppStartupOptions.Parse(args);

        if (startupOptions.Command == AppStartupCommand.LegacyTestShim)
        {
            System.Console.WriteLine("The App --test entry point has moved to tests/HumanFortress.App.Tests.");
            System.Console.WriteLine("Run ./RunTests.sh from the repository root.");
            return;
        }

        if (startupOptions.Command == AppStartupCommand.LegacyValidateShim)
        {
            System.Console.WriteLine("The App --validate entry point has moved to tests/HumanFortress.App.Tests.");
            System.Console.WriteLine("Run ./RunTests.sh from the repository root.");
            return;
        }

        if (startupOptions.Command == AppStartupCommand.TestCrash)
        {
            CrashTestRunner.Run();
            return;
        }

        // Normalize working directory to executable base; helps native DLL discovery
        var baseDir = AppContext.BaseDirectory;
        Directory.SetCurrentDirectory(baseDir);

        // Setup logging before startup diagnostics and content loading.
        var logFile = "fortress_debug.log";
        Logger.Initialize(logFile);

        // Preload critical native libraries (SDL2/OpenAL) to avoid DllNotFound issues
        NativeLibraryPreloader.TryPreload(Path.Combine(baseDir, "SDL2.dll"));
        NativeLibraryPreloader.TryPreload(Path.Combine(baseDir, "soft_oal.dll"));

        // Load content registries from either published output or source checkout.
        if (!StartupContentGate.TryLoadAndValidate(baseDir, startupOptions, out var contentLoad))
        {
            return;
        }

        // Initialize logging callbacks for lower-level components (Simulation/Navigation layers).
        FortressRuntimeLoggingBridge.BindStaticCallbacks(category => Logger.CreateCallback(category));

        // Don't redirect console output - SadConsole needs it for rendering
        // System.Console.SetOut(logWriter);
        // System.Console.SetError(logWriter);

        Logger.Log($"[STARTUP] HumanFortress starting at {DateTime.Now}");
        Logger.Log($"[STARTUP] Log file: {System.IO.Path.GetFullPath(logFile)}");
        StartupContentGate.LogResolvedPath(contentLoad);

        UnhandledExceptionLogger.Bind();

        // Headless init-only mode: initialize world (loads items/creatures/zones) then exit
        if (startupOptions.Command == AppStartupCommand.InitOnly)
        {
            HeadlessInitRunner.Run(startupOptions);
            return;
        }

        SadConsoleGameRunner.Run(startupOptions);
    }
}
