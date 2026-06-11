using HumanFortress.App.Commands;
using HumanFortress.App.Jobs;
using HumanFortress.App;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Content.Registry;
using HumanFortress.Core.Events;
using HumanFortress.Core.Diagnostics;
using HumanFortress.Core.Random;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Core.World;
using HumanFortress.Runtime;
using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Diff;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Jobs;
using HumanFortress.Simulation.Orders;
using HumanFortress.Simulation.Placeables;
using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.World;

internal static class CoreRuntimeSmokeTests
{
    private static readonly string[] ExpectedCommandOrder = { "first", "second" };

    public static void RunAll()
    {
        Console.WriteLine("=== Core Runtime Smoke Tests ===");

        TestTickScheduler();
        TestDeterministicRng();
        TestDeterministicRuntimeIds();
        TestDiffLog();
        TestWorldChunks();
        TestReservations();
        TestCommandQueue();
        TestSimulationCommandStage();
        TestSimulationRuntimeHostCore();
        TestSimulationRuntimeSessionFactory();
        TestAsyncDiagnosticLogger();
        TestContentLoadCoordinator();
        TestOrderCommandsUseRuntimeTarget();
        TestZoneCommandsUseRuntimeTarget();
        TestWorkshopQueueCommandUsesRuntimeTarget();
        TestStockpileCommandUsesRuntimeTarget();
        TestProfessionWeightCommand();
        TestSpawnItemCommandUsesItemDiff();
        TestSpawnCreatureCommandUsesCreatureDiff();
        TestEmbarkabilityDiagnostics();

        Console.WriteLine("=== Core Runtime Smoke Tests Completed ===\n");
    }

    private static void TestTickScheduler()
    {
        var scheduler = new TickScheduler();
        var testSystem = new TestTickSystem();
        scheduler.RegisterSystem(testSystem);

        scheduler.ExecuteSingleTick();

        RegressionAssert.True(
            testSystem.ReadCount == 1 && testSystem.WriteCount == 1,
            "TickScheduler did not execute read/write phases correctly.");

        var selfStoppingScheduler = new TickScheduler();
        using var preTickSeen = new ManualResetEventSlim(false);
        selfStoppingScheduler.PreTick += _ =>
        {
            preTickSeen.Set();
            selfStoppingScheduler.Stop();
        };

        selfStoppingScheduler.Start();
        bool stoppedCleanly = preTickSeen.Wait(1000)
            && SpinWait.SpinUntil(() => !selfStoppingScheduler.IsRunning, 1000);

        RegressionAssert.True(stoppedCleanly, "TickScheduler did not stop cleanly from tick thread.");

        scheduler.Pause();
        scheduler.SetSpeed(4.0f);
        scheduler.ResetForNewSession();
        scheduler.ExecuteSingleTick();

        RegressionAssert.True(
            scheduler.CurrentTick == 1
            && !scheduler.IsPaused
            && Math.Abs(scheduler.SpeedMultiplier - 1.0f) < 0.001f
            && testSystem.ReadCount == 1
            && testSystem.WriteCount == 1,
            "TickScheduler reset did not clear session state and registered systems.");

        Console.WriteLine("[PASS] TickScheduler");
    }

    private static void TestDeterministicRng()
    {
        var rng1 = new DeterministicRng(12345);
        var rng2 = new DeterministicRng(12345);

        for (int i = 0; i < 100; i++)
            RegressionAssert.True(rng1.Next() == rng2.Next(), "DeterministicRng diverged for identical seed.");

        var manager = new RngStreamManager(54321);
        var stream1 = manager.GetStream("test");
        var stream2 = manager.GetStream("test");

        RegressionAssert.True(ReferenceEquals(stream1, stream2), "RngStreamManager returned different streams for the same name.");

        Console.WriteLine("[PASS] Deterministic RNG");
    }

    private static void TestDeterministicRuntimeIds()
    {
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

        RegressionAssert.True(
            sequenceA1 == sequenceA2
            && sequenceA1 != sequenceB
            && derivedA1 == derivedA2
            && derivedA1 != derivedB
            && queueChecksum1 == queueChecksum2,
            "Deterministic runtime ID generation was not stable for repeated inputs.");

        Console.WriteLine("[PASS] Deterministic runtime IDs");
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
        var diffLog = new DiffLog();

        diffLog.AddOp(new DiffOp(DiffOpType.SetTerrain, new DiffTarget(0, 100), "test", 1));
        diffLog.AddOp(new DiffOp(DiffOpType.SetFluid, new DiffTarget(0, 100), "test", 2));

        var merged = diffLog.MergeAndSort();
        var entityId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var target = DiffTargetEncoding.ForWorldCell(35, 66, 7, DiffTargetEncoding.SignedEntityId(entityId));
        var decodedChunk = DiffTargetEncoding.DecodeChunkId(target.ChunkId);
        var decodedLocal = DiffTargetEncoding.DecodeLocalIndex(target.LocalIndex);
        bool worldCellTargetEncodes = WorldCellTargetEncoding.TryEncode(35, 66, 7, out var worldCellTarget);
        var encodedTarget = worldCellTarget.ToDiffTarget(DiffTargetEncoding.SignedEntityId(entityId));
        worldCellTargetEncodes = worldCellTargetEncodes
            && worldCellTarget.ChunkKey.Equals(new ChunkKey(1, 2, 7))
            && worldCellTarget.LocalIndex == Chunk.LocalIndex(3, 2)
            && encodedTarget.ChunkId == target.ChunkId
            && encodedTarget.LocalIndex == target.LocalIndex
            && encodedTarget.EntityId == target.EntityId;
        bool encodingRoundTrips = decodedChunk == (1, 2, 7)
            && decodedLocal == (3, 2)
            && target.EntityId == DiffTargetEncoding.SignedEntityId(entityId)
            && worldCellTargetEncodes;

        RegressionAssert.True(merged.Count == 2 && encodingRoundTrips, "DiffLog merge or target encoding round trip failed.");

        Console.WriteLine("[PASS] DiffLog");
    }

    private static void TestWorldChunks()
    {
        var world = new World(4, 50);

        RegressionAssert.True(world.SizeInChunks == 4 && world.MaxZ == 50, "World was created with incorrect dimensions.");

        var chunkKey = new ChunkKey(1, 1, 10);
        var chunk = world.GetOrCreateChunk(chunkKey);

        RegressionAssert.True(chunk.Key.Equals(chunkKey), "World did not create/retrieve expected chunk.");

        world.UpdateLOD(64, 64, 10);
        RegressionAssert.True(world.GetActiveChunks().Any(), "World LOD update produced no active chunks.");

        Console.WriteLine("[PASS] World chunks");
    }

    private static void TestReservations()
    {
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

        RegressionAssert.True(
            itemReserved
            && itemBlocked
            && itemRefresh
            && itemExpiredAllowsNewHolder
            && creatureReserved
            && creatureBlocked
            && creatureRefresh
            && creatureExpiredAllowsNewHolder,
            "ReservationManager allowed active holders to be stolen before expiry.");

        Console.WriteLine("[PASS] Reservations");
    }

    private static void TestCommandQueue()
    {
        var queue = new CommandQueue();
        var testCommand = new TestCommand(0, "test");

        queue.Enqueue(testCommand);

        var diffLog = new DiffLog();
        var world = new World(2, 10);
        var eventBus = new EventBus();
        var context = new TestSimulationContext(diffLog, world, eventBus);

        queue.ExecuteCommands(0, context);
        RegressionAssert.True(testCommand.Executed, "CommandQueue did not execute due command.");

        var outOfOrderQueue = new CommandQueue();
        var futureCommand = new TestCommand(10, "future");
        var dueCommand = new TestCommand(0, "due");

        outOfOrderQueue.Enqueue(futureCommand);
        outOfOrderQueue.Enqueue(dueCommand);
        outOfOrderQueue.ExecuteCommands(0, context);

        RegressionAssert.True(
            !futureCommand.Executed && dueCommand.Executed,
            "CommandQueue allowed a future command to block a due command.");

        var orderLog = new List<string>();
        var deterministicQueue = new CommandQueue();
        deterministicQueue.Enqueue(new TestCommand(0, "first", orderLog, Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff")));
        deterministicQueue.Enqueue(new TestCommand(0, "second", orderLog, Guid.Empty));
        deterministicQueue.ExecuteCommands(0, context);

        RegressionAssert.True(orderLog.SequenceEqual(ExpectedCommandOrder), "CommandQueue did not preserve same-tick enqueue order.");

        var clearedQueue = new CommandQueue();
        var staleFutureCommand = new TestCommand(100, "stale-future");
        clearedQueue.Enqueue(staleFutureCommand);
        clearedQueue.Clear();
        clearedQueue.ExecuteCommands(100, context);

        RegressionAssert.True(
            !staleFutureCommand.Executed && clearedQueue.GetExecutedCommands().Count == 0,
            "CommandQueue clear left stale pending or executed commands behind.");

        Console.WriteLine("[PASS] CommandQueue");
    }

    private static void TestSimulationCommandStage()
    {
        var scheduler = new TickScheduler();
        var commandQueue = new CommandQueue();
        var diffLog = new DiffLog();
        var itemsDiffLog = new ItemsDiffLog();
        var creaturesDiffLog = new CreaturesDiffLog();
        var world = new World(2, 10);
        var eventBus = new EventBus();
        var context = new SimulationRuntimeContext(diffLog, itemsDiffLog, creaturesDiffLog, world, eventBus);
        var probe = new CommandStageProbe();
        var pipeline = new SimulationTickPipeline(world, commandQueue, context, diffLog, itemsDiffLog, creaturesDiffLog, navigation: null);
        var readSystem = new CommandStageReadSystem(probe);

        scheduler.RegisterSystem(readSystem);
        pipeline.AttachTo(scheduler);

        commandQueue.Enqueue(new ProbeCommand(tick: 0, probe));
        scheduler.ExecuteSingleTick();
        pipeline.DetachFrom(scheduler);

        RegressionAssert.True(
            probe.Executed
            && probe.ObservedTick == 0
            && readSystem.CommandWasVisibleDuringRead,
            "Simulation command stage did not execute queued commands before system ReadTick.");

        Console.WriteLine("[PASS] Simulation command stage");
    }

    private static void TestSimulationRuntimeHostCore()
    {
        var scheduler = new TickScheduler();
        var commandQueue = new CommandQueue();
        var diffLog = new DiffLog();
        var itemsDiffLog = new ItemsDiffLog();
        var creaturesDiffLog = new CreaturesDiffLog();
        var world = new World(2, 10);
        var context = new TestRuntimeCommandContext(diffLog, world, new EventBus());
        var probe = new CommandStageProbe();
        var readSystem = new CommandStageReadSystem(probe);
        var systems = new HostCoreTestSystems(readSystem);
        var host = new SimulationRuntimeHostCore(
            world,
            scheduler,
            commandQueue,
            context,
            diffLog,
            itemsDiffLog,
            creaturesDiffLog,
            navigation: null);

        bool registeredHookCalled = false;
        bool pipelineHookCalled = false;
        var configured = host.Configure(
            () => systems,
            registeredSystems =>
            {
                registeredHookCalled = ReferenceEquals(systems, registeredSystems);
                commandQueue.Enqueue(new ProbeCommand(tick: 0, probe));
            },
            attachedSystems => pipelineHookCalled = ReferenceEquals(systems, attachedSystems));

        scheduler.ExecuteSingleTick();
        bool firstTickCommandVisible = readSystem.CommandWasVisibleDuringRead;
        host.Stop();

        var detachedProbe = new CommandStageProbe();
        commandQueue.Enqueue(new ProbeCommand(tick: 1, detachedProbe));
        scheduler.ExecuteSingleTick();

        RegressionAssert.True(
            ReferenceEquals(configured, systems)
            && registeredHookCalled
            && pipelineHookCalled
            && systems.RegisterCount == 1
            && probe.Executed
            && probe.ObservedTick == 0
            && firstTickCommandVisible
            && !detachedProbe.Executed,
            "SimulationRuntimeHostCore did not own registration, command-stage attachment, or stop-time pipeline detachment.");

        Console.WriteLine("[PASS] Simulation runtime host core");
    }

    private static void TestSimulationRuntimeSessionFactory()
    {
        var scheduler = new TickScheduler();
        var staleSystem = new TestTickSystem();
        scheduler.RegisterSystem(staleSystem);
        scheduler.Pause();
        scheduler.SetSpeed(4.0f);

        var commandQueue = new CommandQueue();
        var staleCommand = new TestCommand(0, "stale");
        commandQueue.Enqueue(staleCommand);

        var diffLog = new DiffLog();
        diffLog.AddOp(new DiffOp(DiffOpType.SetTerrain, new DiffTarget(0, 1), "factory-test", 1));

        var itemsDiffLog = new ItemsDiffLog();
        itemsDiffLog.Add(
            ItemsDiffOp.AddItem,
            new HumanFortress.Simulation.World.ChunkKey(0, 0, 0),
            localIndex: 0,
            itemId: "core_item_log_oak",
            quantity: 1,
            priority: 1,
            systemId: "factory-test");

        World? contentWorld = null;
        World? hostWorld = null;
        HumanFortress.Navigation.NavigationManager? hostNavigation = null;
        var hostObject = new object();
        var factory = new SimulationRuntimeSessionFactory<object>(
            scheduler,
            commandQueue,
            diffLog,
            itemsDiffLog,
            world => contentWorld = world,
            (world, navigation) =>
            {
                hostWorld = world;
                hostNavigation = navigation;
                return hostObject;
            });

        var session = factory.CreateNew(sizeInChunks: 3, maxZ: 12);
        scheduler.ExecuteSingleTick();
        commandQueue.ExecuteCommands(0, new TestSimulationContext(diffLog, session.World, new EventBus()));

        RegressionAssert.True(
            session.World.SizeInChunks == 3
            && session.World.MaxZ == 12
            && ReferenceEquals(session.World, contentWorld)
            && ReferenceEquals(session.World, hostWorld)
            && ReferenceEquals(session.Navigation, hostNavigation)
            && ReferenceEquals(session.Host, hostObject)
            && scheduler.CurrentTick == 1
            && !scheduler.IsPaused
            && Math.Abs(scheduler.SpeedMultiplier - 1.0f) < 0.001f
            && staleSystem.ReadCount == 0
            && !staleCommand.Executed
            && commandQueue.GetExecutedCommands().Count == 0
            && diffLog.MergeAndSort().Count == 0
            && itemsDiffLog.MergeAndSort().Count == 0,
            "SimulationRuntimeSessionFactory did not reset session state or compose world/navigation/host correctly.");

        Console.WriteLine("[PASS] Simulation runtime session factory");
    }

    private static void TestAsyncDiagnosticLogger()
    {
        var logRoot = Path.Combine(Path.GetTempPath(), "HumanFortressDiagnosticsTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(logRoot);
        var mainLog = Path.Combine(logRoot, "fortress_debug.log");

        try
        {
            Logger.Initialize(mainLog);
            Logger.Info("Runtime.Test", "[STARTUP] diagnostic smoke");
            Logger.Log("[RECIPES] recipe diagnostics");
            Logger.Info("Simulation.Items", "[ItemManager] item diagnostics");
            Logger.Error("Core.CommandQueue", "[ERROR] command failed", new InvalidOperationException("diagnostic smoke"));
            Logger.Close();

            var logsDir = Path.Combine(logRoot, "logs");
            var mainText = File.ReadAllText(mainLog);
            var runtimeText = File.ReadAllText(Path.Combine(logsDir, "runtime.log"));
            var contentText = File.ReadAllText(Path.Combine(logsDir, "content.log"));
            var simulationText = File.ReadAllText(Path.Combine(logsDir, "simulation.log"));
            var coreText = File.ReadAllText(Path.Combine(logsDir, "core.log"));

            RegressionAssert.True(
                mainText.Contains("Runtime.Test", StringComparison.Ordinal)
                && mainText.Contains("seq=1", StringComparison.Ordinal)
                && runtimeText.Contains("diagnostic smoke", StringComparison.Ordinal)
                && contentText.Contains("recipe diagnostics", StringComparison.Ordinal)
                && simulationText.Contains("item diagnostics", StringComparison.Ordinal)
                && coreText.Contains("command failed", StringComparison.Ordinal),
                "Async diagnostic logger did not flush or route category logs correctly.");
        }
        finally
        {
            Logger.Close();
            if (Directory.Exists(logRoot))
            {
                Directory.Delete(logRoot, recursive: true);
            }
        }

        Console.WriteLine("[PASS] Async diagnostic logger");
    }

    private static void TestContentLoadCoordinator()
    {
        var contentPath = Path.Combine(AppContext.BaseDirectory, "content");
        RegressionAssert.True(Directory.Exists(contentPath), $"Content directory not found for smoke test: {contentPath}");

        var logRoot = Path.Combine(Path.GetTempPath(), "HumanFortressContentLoadTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(logRoot);
        var mainLog = Path.Combine(logRoot, "fortress_debug.log");

        try
        {
            Logger.Initialize(mainLog);
            HumanFortress.Core.Content.ContentRegistry.Diagnostics = Logger.Sink;

            var result = HumanFortress.Core.Content.ContentLoadCoordinator.Load(contentPath);
            var structured = ContentRegistry.Instance;
            var graniteWallHandle = structured.GetGeologyHandle("core_terrain_wall_rock_granite");
            var graniteWall = structured.GetGeologyByHandle(graniteWallHandle);
            var hasAirGeology = structured.TryGetGeologyHandleByMaterialAndKind(
                "air",
                TerrainKind.OpenNoFloor.ToString(),
                out var airHandle);
            var airGeology = structured.GetGeologyByHandle(airHandle);
            var navigationTuning = structured.GetTuning<Newtonsoft.Json.Linq.JObject>("tuning.navigation", "$");
            var bedroomZone = structured.GetZoneDefinition("bedroom");
            var dataPath = Path.Combine(AppContext.BaseDirectory, "data", "core");
            var coreDataResult = structured.LoadCoreData(dataPath);
            var stoneworks = structured.Constructions.GetConstruction("core_construction_workshop_stoneworks");
            var stoneBlocksRecipe = structured.Recipes.GetRecipe("core_recipe_stone_cut_blocks_c");

            RegressionAssert.True(
                result.LegacyLoaded
                && result.StructuredLoaded
                && result.LegacyMaterialCount >= 70
                && result.LegacyGeologyCount >= 10
                && result.LegacyZoneCount >= 1
                && result.LegacyErrorCount == 0
                && structured.IsLoaded
                && structured.Materials.ResolveStringId("core_mat_stone_granite").HasValue
                && structured.TerrainKinds.GetKind("solid_wall") != null
                && structured.GeologyEntries.Count >= 10
                && structured.Zones.Count >= 1
                && graniteWall != null
                && graniteWall.Material == "core_mat_stone_granite"
                && hasAirGeology
                && airGeology?.Id == "core_terrain_air"
                && navigationTuning != null
                && bedroomZone != null
                && coreDataResult.Constructions.LoadedCount > 0
                && coreDataResult.Constructions.ErrorCount == 0
                && coreDataResult.Recipes.LoadedCount > 0
                && coreDataResult.Recipes.ErrorCount == 0
                && stoneworks != null
                && stoneBlocksRecipe != null,
                "ContentLoadCoordinator did not load runtime content into the structured registry correctly.");
        }
        finally
        {
            Logger.Close();
            HumanFortress.Core.Content.ContentRegistry.Diagnostics = NullDiagnosticSink.Instance;
            if (Directory.Exists(logRoot))
            {
                Directory.Delete(logRoot, recursive: true);
            }
        }

        Console.WriteLine("[PASS] Content load coordinator");
    }

    private static void TestProfessionWeightCommand()
    {
        var scheduler = new TickScheduler();
        var commandQueue = new CommandQueue();
        var diffLog = new DiffLog();
        var itemsDiffLog = new ItemsDiffLog();
        var creaturesDiffLog = new CreaturesDiffLog();
        var world = new World(2, 10);
        var context = new SimulationRuntimeContext(diffLog, itemsDiffLog, creaturesDiffLog, world, new EventBus());
        var registry = ProfessionRegistry.Load(AppContext.BaseDirectory);
        var assignments = new ProfessionAssignments(registry);
        var professionId = registry.Definitions[0].Id;
        var workerId = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        var pipeline = new SimulationTickPipeline(world, commandQueue, context, diffLog, itemsDiffLog, creaturesDiffLog, navigation: null);

        context.SetProfessionWeightHandler(assignments.SetWeight);
        pipeline.AttachTo(scheduler);

        commandQueue.Enqueue(new SetProfessionWeightCommand(tick: 0, workerId, professionId, weight: 12));
        scheduler.ExecuteSingleTick();
        pipeline.DetachFrom(scheduler);

        RegressionAssert.True(
            assignments.GetWeight(workerId, professionId) == 9,
            "SetProfessionWeightCommand did not execute through the tick pipeline or clamp the requested weight.");

        Console.WriteLine("[PASS] Profession weight command");
    }

    private static void TestOrderCommandsUseRuntimeTarget()
    {
        var scheduler = new TickScheduler();
        var commandQueue = new CommandQueue();
        var diffLog = new DiffLog();
        var itemsDiffLog = new ItemsDiffLog();
        var creaturesDiffLog = new CreaturesDiffLog();
        var world = new World(2, 10);
        var context = new SimulationRuntimeContext(diffLog, itemsDiffLog, creaturesDiffLog, world, new EventBus());
        var pipeline = new SimulationTickPipeline(world, commandQueue, context, diffLog, itemsDiffLog, creaturesDiffLog, navigation: null);

        var rect = new SadRogue.Primitives.Rectangle(1, 1, 2, 2);
        var buildAnchor = new SadRogue.Primitives.Point(4, 4);
        var filter = new MaterialFilterSpec { CategoryKey = "test.floor" };

        pipeline.AttachTo(scheduler);
        commandQueue.Enqueue(new CreateMiningOrderCommand(tick: 0, rect, z: 2, priority: 11));
        commandQueue.Enqueue(new CreateAdvancedMiningOrderCommand(tick: 0, rect, zMin: 1, zMax: 3, action: HumanFortress.App.UI.MiningAction.Dig, priority: 12));
        commandQueue.Enqueue(new CreateHaulOrderCommand(tick: 0, rect, z: 2, priority: 13));
        commandQueue.Enqueue(new CreateConstructionOrderCommand(tick: 0, rect, zMin: 2, zMax: 2, shape: ConstructionShape.Floor, filter: filter, priority: 14));
        commandQueue.Enqueue(new CreateBuildableConstructionOrderCommand(tick: 0, "core_workshop_carpenter", buildAnchor, z: 2, priority: 15));
        scheduler.ExecuteSingleTick();
        pipeline.DetachFrom(scheduler);

        var miningAdds = new List<OrdersManager.MiningDesignation>();
        var hauls = new List<HaulDesignation>();
        var construction = new List<ConstructionDesignation>();
        var buildable = new List<BuildableConstructionDesignation>();

        int miningCount = world.Orders.DrainMiningAdds(miningAdds, maxCount: 8);
        int haulCount = world.Orders.DrainHaulDesignations(hauls, maxCount: 8);
        int constructionCount = world.Orders.DrainConstructionDesignations(construction, maxCount: 8);
        int buildableCount = world.Orders.DrainBuildableConstructions(buildable, maxCount: 8);

        RegressionAssert.True(
            miningCount == 2
            && miningAdds.Any(d => d.ZMin == 2 && d.ZMax == 2 && d.Priority == 11)
            && miningAdds.Any(d => d.ZMin == 1 && d.ZMax == 3 && d.Priority == 12)
            && haulCount == 1
            && hauls[0].Z == 2
            && hauls[0].Priority == 13
            && constructionCount == 1
            && construction[0].Shape == ConstructionShape.Floor
            && construction[0].Priority == 14
            && buildableCount == 1
            && buildable[0].ConstructionId == "core_workshop_carpenter"
            && buildable[0].Anchor == buildAnchor
            && buildable[0].Priority == 15,
            "Order commands did not enqueue through the runtime order command target.");

        Console.WriteLine("[PASS] Order commands runtime target");
    }

    private static void TestZoneCommandsUseRuntimeTarget()
    {
        var scheduler = new TickScheduler();
        var commandQueue = new CommandQueue();
        var diffLog = new DiffLog();
        var itemsDiffLog = new ItemsDiffLog();
        var creaturesDiffLog = new CreaturesDiffLog();
        var world = new World(2, 10);
        var context = new SimulationRuntimeContext(diffLog, itemsDiffLog, creaturesDiffLog, world, new EventBus());
        var pipeline = new SimulationTickPipeline(world, commandQueue, context, diffLog, itemsDiffLog, creaturesDiffLog, navigation: null);

        var initialRect = new SadRogue.Primitives.Rectangle(1, 1, 2, 2);
        var extraRect = new SadRogue.Primitives.Rectangle(4, 4, 1, 1);
        var removeRect = new SadRogue.Primitives.Rectangle(1, 1, 1, 1);

        pipeline.AttachTo(scheduler);
        commandQueue.Enqueue(new CreateZoneCommand(tick: 0, defId: "core_zone_stockpile", name: "Runtime Zone", worldRect: initialRect, z: 0));
        scheduler.ExecuteSingleTick();

        var zone = world.Zones.Manager.GetAllZones().SingleOrDefault();
        RegressionAssert.True(
            zone != null
            && zone.Name == "Runtime Zone"
            && zone.TotalCells == 4
            && world.Zones.GetZoneAtPosition(1, 1, 0) == zone.ZoneId,
            "CreateZoneCommand did not create a zone through the runtime zone command target.");

        int zoneId = zone!.ZoneId;
        commandQueue.Enqueue(new UpdateZoneCellsCommand(0, zoneId, extraRect, 0, true));
        scheduler.ExecuteSingleTick();

        RegressionAssert.True(
            world.Zones.GetZoneAtPosition(4, 4, 0) == zoneId,
            "UpdateZoneCellsCommand did not add cells through the runtime zone command target.");

        commandQueue.Enqueue(new UpdateZoneCellsCommand(0, zoneId, removeRect, 0, false));
        scheduler.ExecuteSingleTick();

        RegressionAssert.True(
            world.Zones.GetZoneAtPosition(1, 1, 0) == 0
            && world.Zones.GetZoneAtPosition(2, 2, 0) == zoneId,
            "UpdateZoneCellsCommand did not remove cells through the runtime zone command target.");

        commandQueue.Enqueue(new DeleteZoneCommand(tick: 0, zoneId));
        scheduler.ExecuteSingleTick();
        pipeline.DetachFrom(scheduler);

        RegressionAssert.True(
            world.Zones.Manager.GetZone(zoneId) == null
            && world.Zones.GetZoneAtPosition(2, 2, 0) == 0
            && world.Zones.GetZoneAtPosition(4, 4, 0) == 0,
            "DeleteZoneCommand did not delete the zone and chunk shards through the runtime zone command target.");

        Console.WriteLine("[PASS] Zone commands runtime target");
    }

    private static void TestWorkshopQueueCommandUsesRuntimeTarget()
    {
        var scheduler = new TickScheduler();
        var commandQueue = new CommandQueue();
        var diffLog = new DiffLog();
        var itemsDiffLog = new ItemsDiffLog();
        var creaturesDiffLog = new CreaturesDiffLog();
        var world = new World(2, 10);
        var recipeCatalog = new TestRecipeCatalog(new[]
        {
            new RecipeDefinition
            {
                Id = "test_recipe_a",
                Name = "Test Recipe A",
                Workshops = new[] { "test_workshop" },
                Outputs = new[] { new RecipeOutput { DefId = "test_output_a", Count = 1 } }
            },
            new RecipeDefinition
            {
                Id = "test_recipe_b",
                Name = "Test Recipe B",
                Workshops = new[] { "test_workshop" },
                Outputs = new[] { new RecipeOutput { DefId = "test_output_b", Count = 1 } }
            }
        });
        var context = new SimulationRuntimeContext(
            diffLog,
            itemsDiffLog,
            creaturesDiffLog,
            world,
            new EventBus(),
            recipes: recipeCatalog,
            constructions: ContentRegistry.Instance.Constructions);
        var pipeline = new SimulationTickPipeline(world, commandQueue, context, diffLog, itemsDiffLog, creaturesDiffLog, navigation: null);
        var workshopPosition = new SadRogue.Primitives.Point(5, 5);
        var workshopGuid = Guid.Parse("aaaaaaaa-4444-4444-4444-aaaaaaaaaaaa");
        var workshop = new PlaceableInstance(
            workshopGuid,
            PlaceableKind.Construction,
            "test_workshop",
            workshopPosition,
            z: 0,
            footprint: new Footprint(1, 1, 1))
        {
            Workshop = new WorkshopState()
        };
        workshop.Workshop.ConfigureWorkers(defaultAllowed: 1, maxWorkers: 4);

        world.SetTile(workshopPosition.X, workshopPosition.Y, 0, new TileBase(0, (ushort)TerrainKind.OpenWithFloor, 0, 0, 0, 0, 1), 0);
        PlaceableManager.PlacePlaceable(world, workshop, tick: 0);

        pipeline.AttachTo(scheduler);
        commandQueue.Enqueue(new UpdateWorkshopQueueCommand(0, workshopGuid, WorkshopQueueOperation.AddRecipe, recipeId: "test_recipe_a"));
        commandQueue.Enqueue(new UpdateWorkshopQueueCommand(0, workshopGuid, WorkshopQueueOperation.AddRecipe, recipeId: "test_recipe_b"));
        scheduler.ExecuteSingleTick();

        RegressionAssert.True(
            workshop.Workshop.Queue.Count == 2
            && workshop.Workshop.Queue[0].RecipeId == "test_recipe_a"
            && workshop.Workshop.Queue[1].RecipeId == "test_recipe_b",
            "UpdateWorkshopQueueCommand did not add recipes through the runtime workshop queue target.");

        var secondEntryId = workshop.Workshop.Queue[1].EntryId;
        commandQueue.Enqueue(new UpdateWorkshopQueueCommand(0, workshopGuid, WorkshopQueueOperation.MoveEntry, entryId: secondEntryId, moveOffset: -1));
        commandQueue.Enqueue(new UpdateWorkshopQueueCommand(0, workshopGuid, WorkshopQueueOperation.SetWorkerSlots, intValue: 3));
        commandQueue.Enqueue(new UpdateWorkshopQueueCommand(0, workshopGuid, WorkshopQueueOperation.ToggleAutoSupply, boolValue: false));
        commandQueue.Enqueue(new UpdateWorkshopQueueCommand(0, workshopGuid, WorkshopQueueOperation.ToggleAutoStockpile, boolValue: false));
        scheduler.ExecuteSingleTick();

        RegressionAssert.True(
            workshop.Workshop.Queue[0].RecipeId == "test_recipe_b"
            && workshop.Workshop.AllowedWorkers == 3
            && !workshop.Workshop.AutoRequestMaterials
            && !workshop.Workshop.AutoStockpileOutputs,
            "UpdateWorkshopQueueCommand did not move queue entries or update workshop settings through the runtime target.");

        commandQueue.Enqueue(new UpdateWorkshopQueueCommand(0, workshopGuid, WorkshopQueueOperation.RemoveEntry, entryId: secondEntryId));
        scheduler.ExecuteSingleTick();

        RegressionAssert.True(
            workshop.Workshop.Queue.Count == 1
            && workshop.Workshop.Queue[0].RecipeId == "test_recipe_a",
            "UpdateWorkshopQueueCommand did not remove queue entries through the runtime target.");

        commandQueue.Enqueue(new UpdateWorkshopQueueCommand(0, workshopGuid, WorkshopQueueOperation.ClearQueue));
        scheduler.ExecuteSingleTick();
        pipeline.DetachFrom(scheduler);

        RegressionAssert.True(
            workshop.Workshop.Queue.Count == 0,
            "UpdateWorkshopQueueCommand did not clear the queue through the runtime target.");

        Console.WriteLine("[PASS] Workshop queue command runtime target");
    }

    private static void TestStockpileCommandUsesRuntimeTarget()
    {
        var scheduler = new TickScheduler();
        var commandQueue = new CommandQueue();
        var diffLog = new DiffLog();
        var itemsDiffLog = new ItemsDiffLog();
        var creaturesDiffLog = new CreaturesDiffLog();
        var world = new World(2, 10);
        var context = new SimulationRuntimeContext(diffLog, itemsDiffLog, creaturesDiffLog, world, new EventBus());
        var pipeline = new SimulationTickPipeline(world, commandQueue, context, diffLog, itemsDiffLog, creaturesDiffLog, navigation: null);
        var rect = new SadRogue.Primitives.Rectangle(1, 1, 2, 2);

        for (int x = rect.X; x < rect.X + rect.Width; x++)
        {
            for (int y = rect.Y; y < rect.Y + rect.Height; y++)
            {
                world.SetTile(x, y, 0, new TileBase(0, (ushort)TerrainKind.OpenWithFloor, 0, 0, 0, 0, 1), 0);
            }
        }

        pipeline.AttachTo(scheduler);
        commandQueue.Enqueue(new CreateStockpileCommand(tick: 0, rect, z: 0, presetId: "wood"));
        scheduler.ExecuteSingleTick();

        var zone = world.Stockpiles.GetAllZones().SingleOrDefault();
        var chunk = world.GetChunk(new ChunkKey(0, 0, 0));
        var stockpileData = chunk?.GetStockpileData();
        int firstCell = Chunk.LocalIndex(1, 1);
        var shard = zone == null ? null : stockpileData?.GetShard(zone.ZoneId);

        RegressionAssert.True(
            zone != null
            && zone.Name == "Wood Stockpile 1"
            && zone.MemberChunks.Contains(new ChunkKey(0, 0, 0))
            && stockpileData != null
            && stockpileData.GetZoneAtCell(firstCell) == zone.ZoneId
            && shard != null
            && shard.Capacity == 4,
            "CreateStockpileCommand did not create stockpile zone shards through the runtime stockpile command target.");

        commandQueue.Enqueue(new CreateStockpileCommand(tick: 0, rect, z: 0, presetId: "wood"));
        scheduler.ExecuteSingleTick();
        pipeline.DetachFrom(scheduler);

        RegressionAssert.True(
            world.Stockpiles.GetAllZones().Count() == 1,
            "CreateStockpileCommand created a duplicate stockpile for a fully overlapping rectangle.");

        Console.WriteLine("[PASS] Stockpile command runtime target");
    }

    private static void TestSpawnItemCommandUsesItemDiff()
    {
        var scheduler = new TickScheduler();
        var commandQueue = new CommandQueue();
        var diffLog = new DiffLog();
        var itemsDiffLog = new ItemsDiffLog();
        var creaturesDiffLog = new CreaturesDiffLog();
        var world = new World(2, 10);
        world.Items.SetDependencies(world, HumanFortress.Core.Content.Registry.ContentRegistry.Instance);
        world.Items.LoadDefinitions(Path.Combine(AppContext.BaseDirectory, "data", "core"));
        var context = new SimulationRuntimeContext(diffLog, itemsDiffLog, creaturesDiffLog, world, new EventBus());
        var pipeline = new SimulationTickPipeline(world, commandQueue, context, diffLog, itemsDiffLog, creaturesDiffLog, navigation: null);
        var target = new SadRogue.Primitives.Point(2, 2);
        world.SetTile(target.X, target.Y, 0, new TileBase(0, (ushort)TerrainKind.OpenWithFloor, 0, 0, 0, 0, 1), 0);

        pipeline.AttachTo(scheduler);
        commandQueue.Enqueue(new SpawnItemCommand(tick: 0, "core_item_log_oak", target, z: 0, quantity: 3));
        scheduler.ExecuteSingleTick();
        pipeline.DetachFrom(scheduler);

        var spawned = world.Items.GetItemsAt(target, 0).FirstOrDefault(i => i.DefinitionId == "core_item_log_oak");
        RegressionAssert.True(
            spawned != null && spawned.StackCount == 3,
            "SpawnItemCommand did not emit an item diff that was applied after the tick.");

        Console.WriteLine("[PASS] Spawn item command item diff");
    }

    private static void TestSpawnCreatureCommandUsesCreatureDiff()
    {
        var scheduler = new TickScheduler();
        var commandQueue = new CommandQueue();
        var diffLog = new DiffLog();
        var itemsDiffLog = new ItemsDiffLog();
        var creaturesDiffLog = new CreaturesDiffLog();
        var world = new World(2, 10);
        world.Creatures.LoadDefinitions(Path.Combine(AppContext.BaseDirectory, "data", "core"));
        var context = new SimulationRuntimeContext(diffLog, itemsDiffLog, creaturesDiffLog, world, new EventBus());
        var pipeline = new SimulationTickPipeline(world, commandQueue, context, diffLog, itemsDiffLog, creaturesDiffLog, navigation: null);
        var target = new SadRogue.Primitives.Point(3, 3);
        world.SetTile(target.X, target.Y, 0, new TileBase(0, (ushort)TerrainKind.OpenWithFloor, 0, 0, 0, 0, 1), 0);

        pipeline.AttachTo(scheduler);
        commandQueue.Enqueue(new SpawnCreatureCommand(tick: 0, "core_race_dwarf", target, z: 0, factionId: "player"));
        scheduler.ExecuteSingleTick();
        pipeline.DetachFrom(scheduler);

        var spawned = world.Creatures.GetAllInstances().FirstOrDefault(c => c.DefinitionId == "core_race_dwarf");
        RegressionAssert.True(
            spawned != null && spawned.Position == target && spawned.Z == 0 && spawned.FactionId == "player",
            "SpawnCreatureCommand did not emit a creature diff that was applied after the tick.");

        Console.WriteLine("[PASS] Spawn creature command creature diff");
    }

    private static void TestEmbarkabilityDiagnostics()
    {
        var valid = new WorldTile { Elevation = 0.5f, RiverClass = 0 };
        var low = new WorldTile { Elevation = 0.2f, RiverClass = 0 };
        var high = new WorldTile { Elevation = 0.9f, RiverClass = 0 };
        var river = new WorldTile { Elevation = 0.5f, RiverClass = 3 };

        RegressionAssert.True(valid.IsEmbarkable && valid.GetEmbarkabilityFailures().Count == 0, "Valid embark tile reported failures.");
        RegressionAssert.True(!low.IsEmbarkable && low.GetEmbarkabilityFailures().Any(reason => reason.Contains("Elevation", StringComparison.Ordinal)), "Low embark tile did not explain elevation failure.");
        RegressionAssert.True(!high.IsEmbarkable && high.GetEmbarkabilityFailures().Any(reason => reason.Contains("Elevation", StringComparison.Ordinal)), "High embark tile did not explain elevation failure.");
        RegressionAssert.True(!river.IsEmbarkable && river.GetEmbarkabilityFailures().Any(reason => reason.Contains("River class", StringComparison.Ordinal)), "River embark tile did not explain river failure.");

        Console.WriteLine("[PASS] Embarkability diagnostics");
    }

    private sealed class TestTickSystem : ITick
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

    private sealed class TestCommand : ICommand
    {
        private readonly List<string>? _executionLog;

        public TestCommand(ulong tick, string type, List<string>? executionLog = null, Guid? commandId = null)
        {
            Tick = tick;
            CommandId = commandId ?? Guid.NewGuid();
            CommandType = type;
            _executionLog = executionLog;
        }

        public ulong Tick { get; }
        public Guid CommandId { get; }
        public string CommandType { get; }
        public bool Executed { get; private set; }

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

    private sealed class CommandStageProbe
    {
        public bool Executed { get; set; }
        public ulong ObservedTick { get; set; } = ulong.MaxValue;
    }

    private sealed class ProbeCommand : ICommand
    {
        private readonly CommandStageProbe _probe;

        public ProbeCommand(ulong tick, CommandStageProbe probe)
        {
            Tick = tick;
            _probe = probe;
        }

        public ulong Tick { get; }
        public Guid CommandId { get; } = Guid.Parse("99999999-9999-9999-9999-999999999999");
        public string CommandType => "probe";

        public void Execute(ISimulationContext context)
        {
            _probe.Executed = true;
            _probe.ObservedTick = context.CurrentTick;
        }

        public byte[] Serialize()
        {
            return Array.Empty<byte>();
        }
    }

    private sealed class CommandStageReadSystem : ITick
    {
        private readonly CommandStageProbe _probe;

        public CommandStageReadSystem(CommandStageProbe probe)
        {
            _probe = probe;
        }

        public bool CommandWasVisibleDuringRead { get; private set; }
        public int Priority => 1;
        public string SystemId => "CommandStageReadSystem";

        public void ReadTick(ulong tick)
        {
            CommandWasVisibleDuringRead = _probe.Executed && _probe.ObservedTick == tick;
        }

        public void WriteTick(ulong tick)
        {
        }
    }

    private sealed class HostCoreTestSystems : IRuntimeTickSystems
    {
        private readonly ITick _system;

        public HostCoreTestSystems(ITick system)
        {
            _system = system;
        }

        public int RegisterCount { get; private set; }

        public void RegisterWith(TickScheduler scheduler)
        {
            RegisterCount++;
            scheduler.RegisterSystem(_system);
        }
    }

    private sealed class TestRecipeCatalog : IRecipeCatalog
    {
        private readonly Dictionary<string, RecipeDefinition> _recipes;
        private readonly Dictionary<string, List<RecipeDefinition>> _byWorkshop = new(StringComparer.OrdinalIgnoreCase);

        public TestRecipeCatalog(IEnumerable<RecipeDefinition> recipes)
        {
            _recipes = recipes.ToDictionary(recipe => recipe.Id, StringComparer.OrdinalIgnoreCase);
            foreach (var recipe in _recipes.Values)
            {
                foreach (var workshop in recipe.Workshops)
                {
                    if (!_byWorkshop.TryGetValue(workshop, out var list))
                    {
                        list = new List<RecipeDefinition>();
                        _byWorkshop[workshop] = list;
                    }

                    list.Add(recipe);
                }
            }
        }

        public int Count => _recipes.Count;

        public RecipeDefinition? GetRecipe(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? null : _recipes.GetValueOrDefault(id);
        }

        public IReadOnlyList<RecipeDefinition> GetRecipesForWorkshop(string workshopId)
        {
            return string.IsNullOrWhiteSpace(workshopId) || !_byWorkshop.TryGetValue(workshopId, out var recipes)
                ? Array.Empty<RecipeDefinition>()
                : recipes;
        }

        public IEnumerable<RecipeDefinition> GetAllRecipes()
        {
            return _recipes.Values;
        }
    }

    private sealed class TestRuntimeCommandContext : IRuntimeCommandContext
    {
        public TestRuntimeCommandContext(DiffLog diffLog, World world, IEventBus eventBus)
        {
            DiffLog = diffLog;
            World = world;
            EventBus = eventBus;
        }

        public DiffLog DiffLog { get; }
        public ulong CurrentTick { get; private set; }
        public IWorldReader World { get; }
        public IEventBus EventBus { get; }

        public void SetCurrentTick(ulong tick)
        {
            CurrentTick = tick;
        }
    }

    private sealed class TestSimulationContext : ISimulationContext
    {
        public TestSimulationContext(DiffLog diffLog, World world, IEventBus eventBus)
        {
            DiffLog = diffLog;
            World = world;
            EventBus = eventBus;
        }

        public DiffLog DiffLog { get; }
        public ulong CurrentTick => 0;
        public IWorldReader World { get; }
        public IEventBus EventBus { get; }
    }
}
