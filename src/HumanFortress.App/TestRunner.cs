using HumanFortress.Core.Commands;
using HumanFortress.Core.Events;
using HumanFortress.Core.Random;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Simulation.Placeables;
using HumanFortress.Simulation.World;

namespace HumanFortress.App;

/// <summary>
/// Test runner to verify Phase A implementation.
/// </summary>
public static class TestRunner
{
    private static readonly string[] ExpectedCommandOrder = { "first", "second" };

    public static void RunTests()
    {
        Console.WriteLine("=== Phase A Implementation Tests ===\n");

        TestTickScheduler();
        TestDeterministicRng();
        TestDeterministicRuntimeIds();
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

        var selfStoppingScheduler = new TickScheduler();
        var preTickSeen = new ManualResetEventSlim(false);
        selfStoppingScheduler.PreTick += _ =>
        {
            preTickSeen.Set();
            selfStoppingScheduler.Stop();
        };

        selfStoppingScheduler.Start();
        var stoppedCleanly = preTickSeen.Wait(1000)
            && SpinWait.SpinUntil(() => !selfStoppingScheduler.IsRunning, 1000);

        if (stoppedCleanly)
        {
            Console.WriteLine("✓ TickScheduler: Stop from tick thread completed without deadlock");
        }
        else
        {
            Console.WriteLine("✗ TickScheduler: Stop from tick thread did not complete");
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

    private static void TestDeterministicRuntimeIds()
    {
        Console.WriteLine("Testing deterministic runtime IDs...");

        const ulong testScope = 0x5445535452554E49UL;
        var sequenceA1 = DeterministicGuidGenerator.GenerateFromSequence(testScope, 1);
        var sequenceA2 = DeterministicGuidGenerator.GenerateFromSequence(testScope, 1);
        var sequenceB = DeterministicGuidGenerator.GenerateFromSequence(testScope, 2);

        var sourceGuid = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var derivedA1 = DeterministicGuidGenerator.GenerateFromGuid(testScope, sourceGuid, 42);
        var derivedA2 = DeterministicGuidGenerator.GenerateFromGuid(testScope, sourceGuid, 42);
        var derivedB = DeterministicGuidGenerator.GenerateFromGuid(testScope, sourceGuid, 43);

        var queueChecksum1 = BuildWorkshopQueueIdChecksum();
        var queueChecksum2 = BuildWorkshopQueueIdChecksum();

        if (sequenceA1 == sequenceA2
            && sequenceA1 != sequenceB
            && derivedA1 == derivedA2
            && derivedA1 != derivedB
            && queueChecksum1 == queueChecksum2)
        {
            Console.WriteLine("✓ Runtime IDs: Stable across repeated deterministic inputs");
        }
        else
        {
            Console.WriteLine("✗ Runtime IDs: Deterministic generation failed");
        }
    }

    private static string BuildWorkshopQueueIdChecksum()
    {
        var workshopGuid = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var state = new WorkshopState();
        state.AddEntry("core_recipe_a", "Recipe A", workshopGuid, 100);
        state.AddEntry("core_recipe_b", "Recipe B", workshopGuid, 100);
        return string.Join("|", state.Queue.Select(e => e.EntryId.ToString("N")));
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

        var outOfOrderQueue = new CommandQueue();
        var futureCommand = new TestCommand(10, "future");
        var dueCommand = new TestCommand(0, "due");

        outOfOrderQueue.Enqueue(futureCommand);
        outOfOrderQueue.Enqueue(dueCommand);
        outOfOrderQueue.ExecuteCommands(0, context);

        if (!futureCommand.Executed && dueCommand.Executed)
        {
            Console.WriteLine("✓ CommandQueue: Future command does not block due command");
        }
        else
        {
            Console.WriteLine("✗ CommandQueue: Future command blocked due command");
        }

        var orderLog = new List<string>();
        var deterministicQueue = new CommandQueue();
        deterministicQueue.Enqueue(new TestCommand(0, "first", orderLog, Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff")));
        deterministicQueue.Enqueue(new TestCommand(0, "second", orderLog, Guid.Empty));
        deterministicQueue.ExecuteCommands(0, context);

        if (orderLog.SequenceEqual(ExpectedCommandOrder))
        {
            Console.WriteLine("✓ CommandQueue: Same-tick commands execute in enqueue order");
        }
        else
        {
            Console.WriteLine($"✗ CommandQueue: Same-tick order was {string.Join(", ", orderLog)}");
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
        private readonly List<string>? _executionLog;

        public TestCommand(ulong tick, string type, List<string>? executionLog = null, Guid? commandId = null)
        {
            Tick = tick;
            CommandId = commandId ?? Guid.NewGuid();
            CommandType = type;
            _executionLog = executionLog;
        }

        public void Execute(ISimulationContext context)
        {
            Executed = true;
            _executionLog?.Add(CommandType);
        }

        public byte[] Serialize()
        {
            return Array.Empty<byte>();
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
