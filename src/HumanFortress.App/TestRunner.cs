using HumanFortress.Core.Commands;
using HumanFortress.Core.Events;
using HumanFortress.Core.Random;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Simulation.World;

namespace HumanFortress.App;

/// <summary>
/// Test runner to verify Phase A implementation.
/// </summary>
public static class TestRunner
{
    public static void RunTests()
    {
        Console.WriteLine("=== Phase A Implementation Tests ===\n");

        TestTickScheduler();
        TestDeterministicRng();
        TestDiffLog();
        TestWorldChunks();
        TestCommandQueue();

        Console.WriteLine("\n=== All Tests Completed ===");
    }

    private static void TestTickScheduler()
    {
        Console.WriteLine("Testing TickScheduler...");

        var scheduler = new TickScheduler();
        var testSystem = new TestTickSystem();
        scheduler.RegisterSystem(testSystem);

        // Run a single tick
        scheduler.ExecuteSingleTick();

        if (testSystem.ReadCount == 1 && testSystem.WriteCount == 1)
        {
            Console.WriteLine("✓ TickScheduler: Read/Write phases executed correctly");
        }
        else
        {
            Console.WriteLine("✗ TickScheduler: Phase execution failed");
        }
    }

    private static void TestDeterministicRng()
    {
        Console.WriteLine("Testing Deterministic RNG...");

        var rng1 = new DeterministicRng(12345);
        var rng2 = new DeterministicRng(12345);

        bool deterministic = true;
        for (int i = 0; i < 100; i++)
        {
            if (rng1.Next() != rng2.Next())
            {
                deterministic = false;
                break;
            }
        }

        if (deterministic)
        {
            Console.WriteLine("✓ RNG: Deterministic with same seed");
        }
        else
        {
            Console.WriteLine("✗ RNG: Not deterministic");
        }

        // Test stream manager
        var manager = new RngStreamManager(54321);
        var stream1 = manager.GetStream("test");
        var stream2 = manager.GetStream("test");

        if (stream1 == stream2)
        {
            Console.WriteLine("✓ RNG Streams: Same stream returned for same name");
        }
        else
        {
            Console.WriteLine("✗ RNG Streams: Different streams for same name");
        }
    }

    private static void TestDiffLog()
    {
        Console.WriteLine("Testing DiffLog...");

        var diffLog = new DiffLog();

        // Add operations
        diffLog.AddOp(new DiffOp(
            DiffOpType.SetTerrain,
            new DiffTarget(0, 100),
            "test",
            1
        ));

        diffLog.AddOp(new DiffOp(
            DiffOpType.SetFluid,
            new DiffTarget(0, 100),
            "test",
            2
        ));

        var merged = diffLog.MergeAndSort();

        if (merged.Count == 2)
        {
            Console.WriteLine("✓ DiffLog: Operations added and merged");
        }
        else
        {
            Console.WriteLine("✗ DiffLog: Merge failed");
        }
    }

    private static void TestWorldChunks()
    {
        Console.WriteLine("Testing World and Chunks...");

        var world = new World(4, 50); // 4x4 chunks, 50 Z levels

        if (world.SizeInChunks == 4 && world.MaxZ == 50)
        {
            Console.WriteLine("✓ World: Created with correct dimensions");
        }
        else
        {
            Console.WriteLine("✗ World: Incorrect dimensions");
        }

        var chunkKey = new ChunkKey(1, 1, 10);
        var chunk = world.GetOrCreateChunk(chunkKey);

        if (chunk != null && chunk.Key.Equals(chunkKey))
        {
            Console.WriteLine("✓ Chunks: Created and retrieved correctly");
        }
        else
        {
            Console.WriteLine("✗ Chunks: Creation/retrieval failed");
        }

        // Test LOD
        world.UpdateLOD(64, 64, 10); // Center of world
        var activeChunks = world.GetActiveChunks().Count();

        if (activeChunks > 0)
        {
            Console.WriteLine($"✓ LOD: {activeChunks} active chunks after LOD update");
        }
        else
        {
            Console.WriteLine("✗ LOD: No active chunks");
        }
    }

    private static void TestCommandQueue()
    {
        Console.WriteLine("Testing CommandQueue...");

        var queue = new CommandQueue();
        var testCommand = new TestCommand(0, "test");

        queue.Enqueue(testCommand);

        // Create mock context
        var diffLog = new DiffLog();
        var world = new World(2, 10);
        var eventBus = new EventBus();
        var context = new TestSimulationContext(diffLog, world, eventBus);

        queue.ExecuteCommands(0, context);

        if (testCommand.Executed)
        {
            Console.WriteLine("✓ CommandQueue: Command executed");
        }
        else
        {
            Console.WriteLine("✗ CommandQueue: Command not executed");
        }
    }

    // Test helpers
    private class TestTickSystem : ITick
    {
        public int ReadCount { get; private set; }
        public int WriteCount { get; private set; }
        public int Priority => 100;
        public string SystemId => "TestSystem";

        public void ReadTick(ulong tick)
        {
            ReadCount++;
        }

        public void WriteTick(ulong tick)
        {
            WriteCount++;
        }
    }

    private class TestCommand : ICommand
    {
        public ulong Tick { get; }
        public Guid CommandId { get; }
        public string CommandType { get; }
        public bool Executed { get; private set; }

        public TestCommand(ulong tick, string type)
        {
            Tick = tick;
            CommandId = Guid.NewGuid();
            CommandType = type;
        }

        public void Execute(ISimulationContext context)
        {
            Executed = true;
        }

        public byte[] Serialize()
        {
            return new byte[0];
        }
    }

    private class TestSimulationContext : ISimulationContext
    {
        public DiffLog DiffLog { get; }
        public ulong CurrentTick => 0;
        public IWorldReader World { get; }
        public IEventBus EventBus { get; }

        public TestSimulationContext(DiffLog diffLog, World world, IEventBus eventBus)
        {
            DiffLog = diffLog;
            World = world;
            EventBus = eventBus;
        }
    }
}