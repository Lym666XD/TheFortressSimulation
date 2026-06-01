using HumanFortress.Core.Commands;
using HumanFortress.Core.Events;
using HumanFortress.Core.Random;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Simulation.Diff;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Jobs;
using HumanFortress.Simulation.Placeables;
using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

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
        TestReservations();
        TestItemsDiffConsumption();
        TestItemsDiffSplitStack();
        TestMoveItemDiffRelocation();
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

        scheduler.Pause();
        scheduler.SetSpeed(4.0f);
        scheduler.ResetForNewSession();
        scheduler.ExecuteSingleTick();
        if (scheduler.CurrentTick == 1 && !scheduler.IsPaused && Math.Abs(scheduler.SpeedMultiplier - 1.0f) < 0.001f && testSystem.ReadCount == 1 && testSystem.WriteCount == 1)
        {
            Console.WriteLine("✓ TickScheduler: Reset clears session state and registered systems");
        }
        else
        {
            Console.WriteLine("✗ TickScheduler: Reset did not clear session state");
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

    private static void TestReservations()
    {
        Console.WriteLine("Testing reservations...");

        var reservations = new ReservationManager();
        var itemId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var holderA = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var holderB = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var workerId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        bool itemReserved = reservations.TryReserveItem(itemId, holderA, currentTick: 10, expireTick: 20);
        bool itemBlocked = !reservations.TryReserveItem(itemId, holderB, currentTick: 11, expireTick: 40);
        bool itemRefresh = reservations.TryReserveItem(itemId, holderA, currentTick: 12, expireTick: 50);
        bool itemExpiredAllowsNewHolder = reservations.TryReserveItem(itemId, holderB, currentTick: 51, expireTick: 70);

        bool creatureReserved = reservations.TryReserveCreature(workerId, "Jobs.Mining", currentTick: 10, expireTick: 20, jobId: "mine:a");
        bool creatureBlocked = !reservations.TryReserveCreature(workerId, "Jobs.Transport", currentTick: 11, expireTick: 40, jobId: "haul:a");
        bool creatureRefresh = reservations.TryReserveCreature(workerId, "Jobs.Mining", currentTick: 12, expireTick: 50, jobId: "mine:b");
        bool creatureExpiredAllowsNewHolder = reservations.TryReserveCreature(workerId, "Jobs.Transport", currentTick: 51, expireTick: 70, jobId: "haul:b");

        if (itemReserved && itemBlocked && itemRefresh && itemExpiredAllowsNewHolder
            && creatureReserved && creatureBlocked && creatureRefresh && creatureExpiredAllowsNewHolder)
        {
            Console.WriteLine("✓ Reservations: Active holders cannot be stolen before expiry");
        }
        else
        {
            Console.WriteLine("✗ Reservations: Holder exclusivity failed");
        }
    }

    private static void TestItemsDiffConsumption()
    {
        Console.WriteLine("Testing ItemsDiff consumption...");

        var world = new World(2, 2);
        world.Items.SetDependencies(world, HumanFortress.Core.Content.Registry.ContentRegistry.Instance);
        world.Items.LoadDefinitions(Path.Combine(AppContext.BaseDirectory, "data", "core"));
        world.SetTile(1, 1, 0, new TileBase(0, (ushort)TerrainKind.OpenWithFloor, 0, 0, 0, 0, 1), 0);

        var itemId = world.Items.SpawnItem("core_item_log_oak", new Point(1, 1), 0, 5, 0);
        if (!itemId.HasValue)
        {
            Console.WriteLine("✗ ItemsDiff: Test item spawn failed");
            return;
        }

        var log = new ItemsDiffLog();
        var chunk = new ChunkKey(0, 0, 0);
        int localIndex = Chunk.LocalIndex(1, 1);

        log.AddRemoveItem(itemId.Value, chunk, localIndex, 3, 100, "test");
        ItemsDiffApplicator.ApplyAll(world, log.MergeAndSort(), 1);
        log.Clear();

        var itemAfterPartial = world.Items.GetInstance(itemId.Value);
        bool partialOk = itemAfterPartial?.StackCount == 2;

        log.AddRemoveItem(itemId.Value, chunk, localIndex, 2, 100, "test");
        ItemsDiffApplicator.ApplyAll(world, log.MergeAndSort(), 2);

        bool removeOk = world.Items.GetInstance(itemId.Value) == null;

        var secondItemId = world.Items.SpawnItem("core_item_log_oak", new Point(1, 1), 0, 7, 3);
        bool duplicateBatchOk = false;
        if (secondItemId.HasValue)
        {
            log.Clear();
            log.AddRemoveItem(secondItemId.Value, chunk, localIndex, 3, 100, "test");
            log.AddRemoveItem(secondItemId.Value, chunk, localIndex, 3, 100, "test");
            ItemsDiffApplicator.ApplyRemovals(world, log.MergeAndSort());
            duplicateBatchOk = world.Items.GetInstance(secondItemId.Value)?.StackCount == 1;
        }

        if (partialOk && removeOk && duplicateBatchOk)
        {
            Console.WriteLine("✓ ItemsDiff: Partial consume, full removal, and duplicate batch consume applied");
        }
        else
        {
            Console.WriteLine("✗ ItemsDiff: Consumption diff failed");
        }
    }

    private static void TestItemsDiffSplitStack()
    {
        Console.WriteLine("Testing ItemsDiff split stack...");

        var world = new World(2, 2);
        world.Items.SetDependencies(world, HumanFortress.Core.Content.Registry.ContentRegistry.Instance);
        world.Items.LoadDefinitions(Path.Combine(AppContext.BaseDirectory, "data", "core"));

        var source = new Point(1, 1);
        world.SetTile(source.X, source.Y, 0, new TileBase(0, (ushort)TerrainKind.OpenWithFloor, 0, 0, 0, 0, 1), 0);
        var sourceId = world.Items.SpawnItem("core_item_log_oak", source, 0, 9, 0);
        if (!sourceId.HasValue)
        {
            Console.WriteLine("✗ ItemsDiff split: Test item spawn failed");
            return;
        }

        var newItemId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var chunk = new ChunkKey(0, 0, 0);
        int localIndex = Chunk.LocalIndex(source.X, source.Y);

        var itemsLog = new ItemsDiffLog();
        itemsLog.AddSplitStack(sourceId.Value, newItemId, chunk, localIndex, 4, 100, "test");

        var diffLog = new DiffLog();
        diffLog.AddOp(BuildMarkCarriedDiff(newItemId, source, 0, Guid.Empty));

        ItemsDiffApplicator.ApplyPreSimulation(world, itemsLog.MergeAndSort());
        SimulationDiffApplicator.ApplyAll(world, diffLog.MergeAndSort());

        var sourceItem = world.Items.GetInstance(sourceId.Value);
        var splitItem = world.Items.GetInstance(newItemId);
        bool sourceReduced = sourceItem?.StackCount == 5;
        bool splitCreatedAndCarried = splitItem?.StackCount == 4 && splitItem.IsCarried;

        if (sourceReduced && splitCreatedAndCarried)
        {
            Console.WriteLine("✓ ItemsDiff split: Split stack created before carry diff");
        }
        else
        {
            Console.WriteLine("✗ ItemsDiff split: Split or carry ordering failed");
        }
    }

    private static void TestMoveItemDiffRelocation()
    {
        Console.WriteLine("Testing MoveItem diff relocation...");

        var world = new World(2, 2);
        world.Items.SetDependencies(world, HumanFortress.Core.Content.Registry.ContentRegistry.Instance);
        world.Items.LoadDefinitions(Path.Combine(AppContext.BaseDirectory, "data", "core"));

        var sourceA = new Point(1, 1);
        var sourceB = new Point(1, 2);
        var dest = new Point(2, 1);
        world.SetTile(sourceA.X, sourceA.Y, 0, new TileBase(0, (ushort)TerrainKind.OpenWithFloor, 0, 0, 0, 0, 1), 0);
        world.SetTile(sourceB.X, sourceB.Y, 0, new TileBase(0, (ushort)TerrainKind.OpenWithFloor, 0, 0, 0, 0, 1), 0);
        world.SetTile(dest.X, dest.Y, 0, new TileBase(0, (ushort)TerrainKind.OpenWithFloor, 0, 0, 0, 0, 1), 0);

        var itemA = world.Items.SpawnItem("core_item_log_oak", sourceA, 0, 2, 0);
        var itemB = world.Items.SpawnItem("core_item_log_oak", sourceB, 0, 3, 0);
        var existing = world.Items.SpawnItem("core_item_log_oak", dest, 0, 5, 0);
        if (!itemA.HasValue || !itemB.HasValue || !existing.HasValue)
        {
            Console.WriteLine("✗ MoveItem diff: Test item spawn failed");
            return;
        }

        var log = new DiffLog();
        log.AddOp(BuildMoveItemDiff(itemA.Value, dest, 0));
        log.AddOp(BuildMoveItemDiff(itemB.Value, dest, 0));
        var merged = log.MergeAndSort();
        SimulationDiffApplicator.ApplyAll(world, merged);

        var destItems = world.Items.GetItemsAt(dest, 0)
            .Where(i => i.DefinitionId == "core_item_log_oak")
            .ToList();
        bool bothMovesRetained = merged.Count(op => op.Op == DiffOpType.MoveItem) == 2;
        bool destinationMerged = destItems.Count == 1 && destItems[0].StackCount == 10;
        bool sourcesEmpty = !world.Items.GetItemsAt(sourceA, 0).Any(i => i.DefinitionId == "core_item_log_oak")
            && !world.Items.GetItemsAt(sourceB, 0).Any(i => i.DefinitionId == "core_item_log_oak");

        if (bothMovesRetained && destinationMerged && sourcesEmpty)
        {
            Console.WriteLine("✓ MoveItem diff: Multiple same-destination moves retained and merged");
        }
        else
        {
            Console.WriteLine("✗ MoveItem diff: Relocation or merge failed");
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

        var clearedQueue = new CommandQueue();
        var staleFutureCommand = new TestCommand(100, "stale-future");
        clearedQueue.Enqueue(staleFutureCommand);
        clearedQueue.Clear();
        clearedQueue.ExecuteCommands(100, context);

        if (!staleFutureCommand.Executed && clearedQueue.GetExecutedCommands().Count == 0)
        {
            Console.WriteLine("✓ CommandQueue: Clear removes pending and executed commands");
        }
        else
        {
            Console.WriteLine("✗ CommandQueue: Clear left stale commands behind");
        }
    }

    private static DiffOp BuildMoveItemDiff(Guid itemId, Point dest, int z)
    {
        int chunkX = dest.X / Chunk.SIZE_XY;
        int chunkY = dest.Y / Chunk.SIZE_XY;
        int localX = dest.X % Chunk.SIZE_XY;
        int localY = dest.Y % Chunk.SIZE_XY;
        int localIndex = Chunk.LocalIndex(localX, localY);
        int chunkId = EncodeChunkId(new ChunkKey(chunkX, chunkY, z));
        var target = new DiffTarget(chunkId, localIndex, unchecked((int)ToEntity(itemId)));
        return new DiffOp(DiffOpType.MoveItem, target, "test", 100);
    }

    private static DiffOp BuildMarkCarriedDiff(Guid itemId, Point at, int z, Guid carrierId)
    {
        int chunkX = at.X / Chunk.SIZE_XY;
        int chunkY = at.Y / Chunk.SIZE_XY;
        int localX = at.X % Chunk.SIZE_XY;
        int localY = at.Y % Chunk.SIZE_XY;
        int localIndex = Chunk.LocalIndex(localX, localY);
        int chunkId = EncodeChunkId(new ChunkKey(chunkX, chunkY, z));
        var target = new DiffTarget(chunkId, localIndex, unchecked((int)ToEntity(itemId)));
        ulong args = ToEntity(carrierId);
        return new DiffOp(DiffOpType.MarkCarried, target, "test", 100, args);
    }

    private static int EncodeChunkId(ChunkKey ck)
    {
        return ((ck.Z & 0x3FF) << 20) | ((ck.ChunkX & 0x3FF) << 10) | (ck.ChunkY & 0x3FF);
    }

    private static uint ToEntity(Guid g)
    {
        var bytes = g.ToByteArray();
        return BitConverter.ToUInt32(bytes, 0);
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
