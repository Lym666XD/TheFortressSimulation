using System;
using System.IO;
using HumanFortress.App.GameStates;
using HumanFortress.App.Runtime;
using HumanFortress.App.States;
using HumanFortress.Core.Content;
using SadConsole;
using SadConsole.Configuration;
using SadRogue.Primitives;

namespace HumanFortress.App;

/// <summary>
/// Main entry point for the game.
/// </summary>
public static class Program
{
    private static GameStateManager? _gameStateManager;
    private static bool _autoDigRequested;

    public static void Main(string[] args)
    {
        // Run tests if requested
        if (args.Length > 0 && args[0] == "--test")
        {
            TestRunner.RunTests();
            return;
        }

        // Run phase validation tests
        if (args.Length > 0 && args[0] == "--validate")
        {
            PhaseTests.RunAllPhaseTests();
            return;
        }

        // Auto-crash test mode
        if (args.Length > 0 && args[0] == "--test-crash")
        {
            TestCrash();
            return;
        }

        // Optional self-test: auto enqueue a dig order after world init
        if (args.Any(a => string.Equals(a, "--auto-dig", StringComparison.OrdinalIgnoreCase)))
        {
            _autoDigRequested = true;
        }

        // Normalize working directory to executable base; helps native DLL discovery
        var baseDir = AppContext.BaseDirectory;
        Directory.SetCurrentDirectory(baseDir);

        // Preload critical native libraries (SDL2/OpenAL) to avoid DllNotFound issues
        TryPreloadNative(Path.Combine(baseDir, "SDL2.dll"));
        TryPreloadNative(Path.Combine(baseDir, "soft_oal.dll"));

        // Load content registry from publish folder
        var contentPath = Path.Combine(baseDir, "content");
        if (Directory.Exists(contentPath))
        {
            ContentRegistry.Instance.LoadContent(contentPath);
        }

        // Setup logging to file AFTER loading content
        var logFile = "fortress_debug.log";
        Logger.Initialize(logFile);

        // Initialize logging callbacks for lower-level components (Simulation/Navigation layers)
        HumanFortress.Navigation.NavigationManager.LogCallback = Logger.Log;
        HumanFortress.Simulation.Items.ItemManager.LogCallback = Logger.Log;
        HumanFortress.Simulation.Diff.SimulationDiffApplicator.LogCallback = Logger.Log;
        HumanFortress.Simulation.Orders.OrdersManager.LogCallback = Logger.Log;
        HumanFortress.Simulation.Orders.MiningSystem.LogCallback = Logger.Log;
        HumanFortress.Simulation.Jobs.ConstructionMaterialsPlanner.LogCallback = Logger.Log;

        // Don't redirect console output - SadConsole needs it for rendering
        // System.Console.SetOut(logWriter);
        // System.Console.SetError(logWriter);

        Logger.Log($"[STARTUP] HumanFortress starting at {DateTime.Now}");
        Logger.Log($"[STARTUP] Log file: {System.IO.Path.GetFullPath(logFile)}");

        if (Directory.Exists(contentPath))
        {
            Logger.Log($"[STARTUP] Content loaded successfully from {contentPath}");
        }
        else
        {
            Logger.Log($"[STARTUP] WARNING: Content directory not found at {contentPath}");
        }

        // Add unhandled exception handler for debugging
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            Logger.Log("[FATAL ERROR] Unhandled exception:");
            Logger.Log($"Message: {ex?.Message}");
            Logger.Log($"Stack trace: {ex?.StackTrace}");
            Logger.Log($"Inner exception: {ex?.InnerException?.Message}");
            Logger.Close();
        };

        // Headless init-only mode: initialize world (loads items/creatures/zones) then exit
        if (args.Length > 0 && args[0] == "--init-only")
        {
            try
            {
                var gsm = new GameStateManager(12345);
                // Use small world size just to trigger definitions loading
                gsm.InitializeWorld(sizeInChunks: 2, maxZ: 50);
                Logger.Log("[HEADLESS] Init-only completed");
            }
            catch (Exception ex)
            {
                Logger.Log($"[HEADLESS] ERROR: {ex.Message}");
            }
            finally
            {
                Logger.Close();
            }
            return;
        }

        // Initialize SadConsole per UI_AND_INPUT_MODEL.md
        Settings.WindowTitle = "HumanFortress";

        var gameStartup = new Builder()
            .SetScreenSize(120, 40)
            .OnStart(OnGameStarted)
            .ConfigureFonts(true)
            .IsStartingScreenFocused(true)
            .AddFrameUpdateEvent(OnFrameUpdate);

        Game.Create(gameStartup);
        Game.Instance.Run();

        // Shutdown game systems before disposing
        Logger.Log("[SHUTDOWN] Game window closed, shutting down systems");
        _gameStateManager?.Shutdown();

        Game.Instance.Dispose();

        // Clean up resources
        Logger.Log("[SHUTDOWN] Game shutting down normally");
        Logger.Close();
    }

    // Attempt to load a native library explicitly to stabilize P/Invoke resolution
    private static void TryPreloadNative(string fullPath)
    {
        try
        {
            if (File.Exists(fullPath))
            {
                System.Runtime.InteropServices.NativeLibrary.Load(fullPath);
                Logger.Log($"[NATIVE] Preloaded {fullPath}");
            }
            else
            {
                Logger.Log($"[NATIVE] Missing native library: {fullPath}");
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"[NATIVE] Failed to load {fullPath}: {ex.Message}");
        }
    }

    private static void OnGameStarted(object? sender, GameHost gameHost)
    {
        // Initialize game state manager with deterministic seed
        ulong masterSeed = 12345; // TODO: Make configurable
        var session = new FortressSessionContext(_autoDigRequested);
        _gameStateManager = new GameStateManager(masterSeed, enqueueAutoDig: _autoDigRequested);
        var navigator = new AppStateNavigator(_gameStateManager);

        AppStateRegistration.RegisterAll(_gameStateManager, navigator, session);

        // In auto-dig mode, jump straight to FortressPlay to run simulation and enqueue test dig
        if (_autoDigRequested)
        {
            // Set a safe default fortress size for automated run
            session.ConfigureEmbark(new Point(10, 10), 2);
            _gameStateManager.TransitionTo(GameStateType.FortressPlay);
        }
        else
        {
            // Start at main menu - this will set the screen
            _gameStateManager.TransitionTo(GameStateType.MainMenu);
        }
    }

    private static void OnFrameUpdate(object? sender, GameHost gameHost)
    {
        var deltaTime = gameHost.UpdateFrameDelta.TotalSeconds;
        _gameStateManager?.Update(deltaTime);
        // SadConsole handles input automatically for the focused screen
    }

    private static void TestCrash()
    {
        // Setup logging
        var logFile = "fortress_crash_test.log";
        var logWriter = new System.IO.StreamWriter(logFile, false);
        logWriter.AutoFlush = true;
        System.Console.SetOut(logWriter);
        System.Console.SetError(logWriter);

        try
        {

            System.Console.WriteLine("[TEST-CRASH] Starting crash test");

            // Initialize game state manager
            ulong masterSeed = 12345;
            var gameStateManager = new GameStateManager(masterSeed);
            var navigator = new AppStateNavigator(gameStateManager);
            var session = new FortressSessionContext(autoDig: false);

            System.Console.WriteLine("[TEST-CRASH] Registering states");
            AppStateRegistration.RegisterAll(gameStateManager, navigator, session);

            // Simulate the navigation path that causes crash
            System.Console.WriteLine("[TEST-CRASH] Setting up embark location");
            session.ConfigureEmbark(new Point(10, 10), 2);

            // Generate a test world
            System.Console.WriteLine("[TEST-CRASH] Generating test world");
            var worldGen = new HumanFortress.WorldGen.WorldGenerator();
            var worldParams = new HumanFortress.Core.World.WorldParams
            {
                Seed = (uint)masterSeed,
                Width = 64,
                Height = 64,
                Name = "TestWorld",
                Difficulty = HumanFortress.Core.World.DifficultyPreset.Normal
            };
            session.SetGeneratedWorld(worldGen.Generate(worldParams));
            session.ConfigureEmbark(new Point(10, 10), 2);

            // Initialize the world before transitioning
            System.Console.WriteLine("[TEST-CRASH] Initializing world");
            gameStateManager.InitializeWorld(session.FortressSize, 50);

            // Try to transition directly to FortressPlay
            System.Console.WriteLine("[TEST-CRASH] Attempting to transition to FortressPlay");
            gameStateManager.ChangeState(GameStateType.FortressPlay);

            System.Console.WriteLine("[TEST-CRASH] Test completed without crash!");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[TEST-CRASH] CAUGHT EXCEPTION: {ex.Message}");
            System.Console.WriteLine($"[TEST-CRASH] Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                System.Console.WriteLine($"[TEST-CRASH] Inner exception: {ex.InnerException.Message}");
                System.Console.WriteLine($"[TEST-CRASH] Inner stack: {ex.InnerException.StackTrace}");
            }
        }
        finally
        {
            logWriter?.Flush();
            logWriter?.Close();
        }
    }

}
