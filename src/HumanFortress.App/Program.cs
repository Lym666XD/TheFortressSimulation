using System;
using System.IO;
using HumanFortress.App.GameStates;
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

        // Load content registry BEFORE redirecting console output
        var contentPath = Path.Combine(Directory.GetCurrentDirectory(), "content");
        if (Directory.Exists(contentPath))
        {
            ContentRegistry.Instance.LoadContent(contentPath);
        }

        // Setup logging to file AFTER loading content
        var logFile = "fortress_debug.log";
        Logger.Initialize(logFile);

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
        Game.Instance.Dispose();
    }

    private static void OnGameStarted(object? sender, GameHost gameHost)
    {
        // Initialize game state manager with deterministic seed
        ulong masterSeed = 12345; // TODO: Make configurable
        _gameStateManager = new GameStateManager(masterSeed);

        // Register all states
        _gameStateManager.RegisterState(new MainMenuStateWrapper());
        _gameStateManager.RegisterState(new WorldGenStateWrapper());
        _gameStateManager.RegisterState(new WorldMapStateWrapper());
        _gameStateManager.RegisterState(new EmbarkPrepStateWrapper());
        _gameStateManager.RegisterState(new FortressPlayState());

        // Start at main menu - this will set the screen
        _gameStateManager.TransitionTo(GameStateType.MainMenu);
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

            // Register states
            System.Console.WriteLine("[TEST-CRASH] Registering states");
            gameStateManager.RegisterState(new MainMenuStateWrapper());
            gameStateManager.RegisterState(new WorldGenStateWrapper());
            gameStateManager.RegisterState(new WorldMapStateWrapper());
            gameStateManager.RegisterState(new EmbarkPrepStateWrapper());
            gameStateManager.RegisterState(new FortressPlayState());

            // Simulate the navigation path that causes crash
            System.Console.WriteLine("[TEST-CRASH] Setting up embark location");
            FortressState.EmbarkLocation = new Point(10, 10);
            FortressState.FortressSize = 2;

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
            WorldMapState.CurrentWorld = worldGen.Generate(worldParams);

            // Initialize the world before transitioning
            System.Console.WriteLine("[TEST-CRASH] Initializing world");
            gameStateManager.InitializeWorld(FortressState.FortressSize > 0 ? FortressState.FortressSize : 2, 50);

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

    /// <summary>
    /// Main menu state wrapper.
    /// </summary>
    private class MainMenuStateWrapper : GameState
    {
        private MainMenuState? _mainMenuState;

        public override GameStateType Type => GameStateType.MainMenu;

        public override void Enter()
        {
            Logger.Log("Entered Main Menu");
            _mainMenuState = new MainMenuState();
            _mainMenuState.IsFocused = true;
            GameHost.Instance.Screen = _mainMenuState;
            GameHost.Instance.Screen.IsFocused = true;
        }

        public override void Exit()
        {
            _mainMenuState = null;
        }
    }

    /// <summary>
    /// Fortress play state.
    /// </summary>
    private class FortressPlayState : GameState
    {
        private FortressState? _fortressState;

        public override GameStateType Type => GameStateType.FortressPlay;

        public override void Enter()
        {
            Logger.Log("Entered Fortress Play");

            // Initialize world with selected size
            int fortressSize = FortressState.FortressSize;

            // Validate fortress size (must be between 2 and 8)
            if (fortressSize < 2 || fortressSize > 8)
            {
                Logger.Log($"[FortressPlayState] Invalid fortress size {fortressSize}, defaulting to 2");
                fortressSize = 2;
            }

            // Get the GameStateManager instance and initialize world
            GameStateManager.Instance?.InitializeWorld(fortressSize, 50);

            // Create and display the fortress state
            _fortressState = new FortressState();

            // Only set focus and screen if GameHost is initialized
            if (GameHost.Instance != null)
            {
                _fortressState.IsFocused = true;
                GameHost.Instance.Screen = _fortressState;
                GameHost.Instance.Screen.IsFocused = true;
            }
            else
            {
                Logger.Log("[FortressPlayState] GameHost not initialized, deferring screen setup");
            }
        }

        public override void Exit()
        {
            _fortressState = null;
        }
    }

    /// <summary>
    /// Wrapper for WorldGenState to work with GameState system.
    /// </summary>
    private class WorldGenStateWrapper : GameState
    {
        private WorldGenState? _worldGenState;

        public override GameStateType Type => GameStateType.WorldGen;

        public override void Enter()
        {
            _worldGenState = new WorldGenState();
            _worldGenState.IsFocused = true;
            GameHost.Instance.Screen = _worldGenState;
            GameHost.Instance.Screen.IsFocused = true;
        }

        public override void Exit()
        {
            _worldGenState = null;
        }
    }

    /// <summary>
    /// Wrapper for WorldMapState to work with GameState system.
    /// </summary>
    private class WorldMapStateWrapper : GameState
    {
        private WorldMapState? _worldMapState;

        public override GameStateType Type => GameStateType.WorldMap;

        public override void Enter()
        {
            _worldMapState = new WorldMapState();
            _worldMapState.IsFocused = true;
            GameHost.Instance.Screen = _worldMapState;
            GameHost.Instance.Screen.IsFocused = true;
        }

        public override void Exit()
        {
            _worldMapState = null;
        }
    }

    /// <summary>
    /// Wrapper for EmbarkPrepState to work with GameState system.
    /// </summary>
    private class EmbarkPrepStateWrapper : GameState
    {
        private EmbarkPrepState? _embarkPrepState;

        public override GameStateType Type => GameStateType.EmbarkPrep;

        public override void Enter()
        {
            _embarkPrepState = new EmbarkPrepState();
            _embarkPrepState.IsFocused = true;
            GameHost.Instance.Screen = _embarkPrepState;
            GameHost.Instance.Screen.IsFocused = true;
        }

        public override void Exit()
        {
            _embarkPrepState = null;
        }
    }
}