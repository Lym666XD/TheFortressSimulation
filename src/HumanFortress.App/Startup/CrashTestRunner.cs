using HumanFortress.App.GameStates;
using HumanFortress.App.Session;
using HumanFortress.App.WorldGeneration;
using HumanFortress.Contracts.WorldGen;
using SadRogue.Primitives;

namespace HumanFortress.App.Startup;

internal static class CrashTestRunner
{
    public static void Run()
    {
        using var logWriter = new StreamWriter("fortress_crash_test.log", false)
        {
            AutoFlush = true
        };
        Console.SetOut(logWriter);
        Console.SetError(logWriter);

        GameStateManager? gameStateManager = null;
        try
        {
            Console.WriteLine("[TEST-CRASH] Starting crash test");

            const ulong masterSeed = 12345;
            gameStateManager = new GameStateManager();
            var navigator = new AppStateNavigator(gameStateManager);
            var session = new FortressSessionContext(autoDig: false);

            Console.WriteLine("[TEST-CRASH] Registering states");
            AppStateRegistration.RegisterAll(gameStateManager, navigator, session);

            Console.WriteLine("[TEST-CRASH] Setting up embark location");
            session.ConfigureEmbark(new Point(10, 10), 2);

            Console.WriteLine("[TEST-CRASH] Generating test world");
            var worldGeneration = new WorldGenerationAccess();
            var worldSettings = new WorldGenerationSettings
            {
                Seed = (uint)masterSeed,
                Width = 64,
                Height = 64,
                Name = "TestWorld",
                Difficulty = WorldGenerationDifficulty.Normal
            };
            session.SetGeneratedWorld(worldGeneration.Generate(worldSettings));
            session.ConfigureEmbark(new Point(10, 10), 2);

            Console.WriteLine("[TEST-CRASH] Attempting to transition to FortressPlay");
            gameStateManager.TransitionTo(GameStateType.FortressPlay);

            Console.WriteLine("[TEST-CRASH] Test completed without crash!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TEST-CRASH] CAUGHT EXCEPTION: {ex.Message}");
            Console.WriteLine($"[TEST-CRASH] Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"[TEST-CRASH] Inner exception: {ex.InnerException.Message}");
                Console.WriteLine($"[TEST-CRASH] Inner stack: {ex.InnerException.StackTrace}");
            }
        }
        finally
        {
            gameStateManager?.Shutdown();
        }
    }
}
