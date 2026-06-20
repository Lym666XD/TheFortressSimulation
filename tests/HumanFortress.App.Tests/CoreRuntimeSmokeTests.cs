using HumanFortress.Runtime.Commands;
using HumanFortress.Runtime.Jobs;
using HumanFortress.Jobs;
using HumanFortress.App;
using HumanFortress.Content.Definitions;
using HumanFortress.Content.Loading;
using HumanFortress.Content.Registry;
using HumanFortress.Core.Commands;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Core.Events;
using HumanFortress.Core.Diagnostics;
using HumanFortress.Core.Random;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Core.World;
using HumanFortress.Jobs.Mining;
using HumanFortress.Jobs.Transport;
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
        TestRuntimeStartupHelpers();
        TestUnifiedJobsOrchestrator();
        TestMiningDropResolverJson();
        TestNavigationTuningJson();
        TestConstructionTuningJson();
        TestPlaceableTuningJson();
        TestAsyncDiagnosticLogger();
        TestContentBootstrap();
        TestContentLoadDiagnostics();
        TestDefinitionCatalogReloadsClearIndexes();
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

    private static SimulationRuntimeContext CreateRuntimeContext(
        DiffLog diffLog,
        ItemsDiffLog itemsDiffLog,
        CreaturesDiffLog creaturesDiffLog,
        World world,
        IEventBus? eventBus = null,
        IRecipeCatalog? recipes = null,
        IConstructionCatalog? constructions = null,
        Action<string>? log = null)
    {
        return new SimulationRuntimeContext(
            diffLog,
            itemsDiffLog,
            creaturesDiffLog,
            world,
            eventBus ?? new EventBus(),
            recipes ?? RecipeCatalogStore.Empty,
            constructions ?? ConstructionCatalogStore.Empty,
            log);
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
        var context = CreateRuntimeContext(diffLog, itemsDiffLog, creaturesDiffLog, world, eventBus);
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

    private static void TestRuntimeStartupHelpers()
    {
        var emptyWorld = new World(2, 3);
        RegressionAssert.True(
            !StartupDigTargetFinder.TryFindAnyDigTarget(emptyWorld, out _),
            "Startup dig target finder found a target in an empty world.");

        var digWorld = new World(2, 3);
        int cx = digWorld.SizeInTiles / 2;
        int cy = digWorld.SizeInTiles / 2;
        digWorld.SetTile(cx, cy, 1, new TileBase(0, (ushort)TerrainKind.SolidWall, 0, 0, 0, 0, 1), 0);

        RegressionAssert.True(
            StartupDigTargetFinder.TryFindNearestDigTarget(digWorld, out var target)
            && target.X == cx
            && target.Y == cy
            && target.Z == 1,
            "Startup dig target finder did not return the expected nearest dig target.");

        var workerWorld = new World(2, 3);
        DefinitionCatalogTestSupport.LoadCreatures(workerWorld);
        workerWorld.SetTile(cx, cy, 1, new TileBase(0, (ushort)TerrainKind.OpenWithFloor, 0, 0, 0, 0, 1), 0);

        int spawned = SimulationInitialWorkerSpawner.SpawnIfNeeded(workerWorld, desired: 1);
        int spawnedAgain = SimulationInitialWorkerSpawner.SpawnIfNeeded(workerWorld, desired: 5);

        RegressionAssert.True(
            spawned == 1
            && spawnedAgain == 0
            && workerWorld.Creatures.InstanceCount == 1,
            "Simulation initial worker spawner did not seed exactly once.");

        Console.WriteLine("[PASS] Runtime startup helpers");
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

    private static void TestNavigationTuningJson()
    {
        const string json = """
        {
          "allow_diagonals": true,
          "ramp_vertical_alignment_mode": "df",
          "ramp_requires_highside_support": false,
          "cost": {
            "base": 11,
            "orthogonal": 12,
            "diagonal": 17,
            "ramp_delta": 5,
            "stair_delta": 7
          },
          "diagonal_rules": {
            "corner_check": false
          },
          "fluids": {
            "shallow_threshold": 2,
            "deep_threshold": 5,
            "wade_cost": 8,
            "swim_cost": 21
          },
          "traffic": {
            "low": -3,
            "normal": 1,
            "high": 4,
            "restricted": 9
          },
          "doors": {
            "closed_blocks": false,
            "open_cost": 6
          },
          "budgets": {
            "max_nodes_per_search": 1200,
            "max_ms_per_tick_pathing": 4
          }
        }
        """;

        var tuning = HumanFortress.Navigation.NavigationTuning.LoadFromJson(json);
        var invalidNumericTuning = HumanFortress.Navigation.NavigationTuning.LoadFromJson("""
        {
          "cost": {
            "base": 70000
          },
          "fluids": {
            "shallow_threshold": 300
          }
        }
        """);

        RegressionAssert.True(
            tuning.AllowDiagonals
            && tuning.BaseCost == 11
            && tuning.OrthogonalCost == 12
            && tuning.DiagonalCost == 17
            && tuning.RampDelta == 5
            && tuning.StairDelta == 7
            && tuning.RampVerticalAlignmentMode == "df"
            && !tuning.RampRequiresHighsideSupport
            && !tuning.DiagonalCornerCheck
            && tuning.FluidShallowThreshold == 2
            && tuning.FluidDeepThreshold == 5
            && tuning.FluidWadeCost == 8
            && tuning.FluidSwimCost == 21
            && tuning.TrafficLow == -3
            && tuning.TrafficNormal == 1
            && tuning.TrafficHigh == 4
            && tuning.TrafficRestricted == 9
            && !tuning.DoorClosedBlocks
            && tuning.DoorOpenCost == 6
            && tuning.MaxNodesPerSearch == 1200
            && tuning.MaxMsPerTickPathing == 4
            && invalidNumericTuning.BaseCost == HumanFortress.Navigation.NavigationTuning.Default.BaseCost
            && invalidNumericTuning.FluidShallowThreshold == HumanFortress.Navigation.NavigationTuning.Default.FluidShallowThreshold,
            "NavigationTuning JSON parser did not apply supported fields or preserve defaults for invalid numeric ranges.");

        Console.WriteLine("[PASS] Navigation tuning JSON");
    }

    private static void TestPlaceableTuningJson()
    {
        const string json = """
        {
          "quality": {
            "beauty_per_tier": 2,
            "comfort_per_tier": 3,
            "min_tier": -2,
            "max_tier": 4
          },
          "durability": {
            "default_max_hp": 75,
            "hp_per_volume_ml": 0.002,
            "material_hp_multiplier": {
              "stone": 2.5,
              "default": 1.1
            },
            "condition_thresholds": {
              "good": 0.8,
              "poor": 0.1
            }
          },
          "installation": {
            "install_time_base_ticks": 120,
            "deconstruct_time_base_ticks": 90,
            "material_recovery_rate": 0.5,
            "preserve_item_on_uninstall": false
          },
          "construction": {
            "quality_tier_always_zero": false,
            "skill_xp_per_build_tick": 2
          },
          "doors": {
            "default_locked": true,
            "default_open": true,
            "open_cost_ticks": 7,
            "close_cost_ticks": 8,
            "closed_blocks_movement": false
          },
          "collision": {
            "check_full_footprint": false,
            "require_walkable_tiles": false,
            "allow_overlap_external_refs": true,
            "cross_chunk_validation": false
          },
          "workshops": {
            "workers_per_workshop_max": 12
          }
        }
        """;

        var tuning = PlaceableTuning.LoadFromJson(json);

        RegressionAssert.True(
            tuning.BeautyPerTier == 2
            && tuning.ComfortPerTier == 3
            && tuning.MinTier == -2
            && tuning.MaxTier == 4
            && tuning.DefaultMaxHP == 75
            && Math.Abs(tuning.HPPerVolumeML - 0.002f) < 0.0001f
            && Math.Abs(tuning.MaterialHPMultiplier["stone"] - 2.5f) < 0.0001f
            && Math.Abs(tuning.MaterialHPMultiplier["default"] - 1.1f) < 0.0001f
            && Math.Abs(tuning.ConditionThresholds["good"] - 0.8f) < 0.0001f
            && Math.Abs(tuning.ConditionThresholds["poor"] - 0.1f) < 0.0001f
            && tuning.InstallTimeBaseTicks == 120
            && tuning.DeconstructTimeBaseTicks == 90
            && Math.Abs(tuning.MaterialRecoveryRate - 0.5f) < 0.0001f
            && !tuning.PreserveItemOnUninstall
            && !tuning.ConstructionQualityAlwaysZero
            && tuning.SkillXPPerBuildTick == 2
            && tuning.DoorDefaultLocked
            && tuning.DoorDefaultOpen
            && tuning.DoorOpenCostTicks == 7
            && tuning.DoorCloseCostTicks == 8
            && !tuning.DoorClosedBlocksMovement
            && !tuning.CheckFullFootprint
            && !tuning.RequireWalkableTiles
            && tuning.AllowOverlapExternalRefs
            && !tuning.CrossChunkValidation
            && tuning.WorkersPerWorkshopMax == 12,
            "PlaceableTuning JSON parser did not apply supported fields.");

        Console.WriteLine("[PASS] Placeable tuning JSON");
    }

    private static void TestConstructionTuningJson()
    {
        const string json = """
        {
          "floor_plank_count": 2,
          "floor_block_count": 3,
          "wall_block_count": 4,
          "ramp_block_count": 5,
          "ramp_plank_count": 6,
          "stair_block_count": 7,
          "floor_requires_support": false,
          "floor_allow_neighbor_support": true,
          "build_rate_ticks": 11,
          "build_ticks_wall": 120,
          "build_ticks_floor": 130,
          "build_ticks_ramp": 140,
          "build_ticks_stairs": 150
        }
        """;

        var tuning = ConstructionTuning.LoadFromJson(json);

        RegressionAssert.True(
            tuning.FloorPlankCount == 2
            && tuning.FloorBlockCount == 3
            && tuning.WallBlockCount == 4
            && tuning.RampBlockCount == 5
            && tuning.RampPlankCount == 6
            && tuning.StairBlockCount == 7
            && !tuning.FloorRequiresSupport
            && tuning.FloorAllowNeighborSupport
            && tuning.BuildRateTicks == 11
            && tuning.BuildTicksWall == 120
            && tuning.BuildTicksFloor == 130
            && tuning.BuildTicksRamp == 140
            && tuning.BuildTicksStairs == 150,
            "ConstructionTuning JSON parser did not apply supported fields.");

        Console.WriteLine("[PASS] Construction tuning JSON");
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
            Logger.Warning("Content.Registry", "[Content.TestWarning] content issue smoke");
            Logger.Error("Core.CommandQueue", "[ERROR] command failed", new InvalidOperationException("diagnostic smoke"));
            Logger.Close();
            var snapshot = Logger.GetSnapshot();

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
                && contentText.Contains("content issue smoke", StringComparison.Ordinal)
                && simulationText.Contains("item diagnostics", StringComparison.Ordinal)
                && coreText.Contains("command failed", StringComparison.Ordinal)
                && snapshot.TotalCount >= 5
                && snapshot.ErrorOrHigherCount == 1
                && snapshot.WarningOrHigherCount == 2
                && snapshot.CategoryCounts.ContainsKey("Content.Registry")
                && snapshot.ContentIssues.Any(issue =>
                    issue.Code == "Content.TestWarning"
                    && issue.Message == "content issue smoke"
                    && issue.Level == DiagnosticLevel.Warning),
                "Async diagnostic logger did not flush, route category logs, or build diagnostic snapshots correctly.");
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

    private static void TestContentBootstrap()
    {
        var contentPath = FortressContentLoader.ResolveContentPath(AppContext.BaseDirectory);
        RegressionAssert.True(
            contentPath.ResolvedPath != null,
            $"Content directory not found for smoke test. Tried: {contentPath.PublishedPath}; {contentPath.DevelopmentPath}");

        var logRoot = Path.Combine(Path.GetTempPath(), "HumanFortressContentLoadTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(logRoot);
        var mainLog = Path.Combine(logRoot, "fortress_debug.log");

        try
        {
            Logger.Initialize(mainLog);

            var bootstrap = FortressContentLoader.Load(AppContext.BaseDirectory, forceReloadRegistries: true);
            RegressionAssert.True(
                bootstrap.IsValid(treatWarningsAsErrors: true),
                $"Content bootstrapper reported blocking issues:{Environment.NewLine}{bootstrap.FormatBlockingIssues(treatWarningsAsErrors: true)}");

            var result = bootstrap.Registries
                ?? throw new InvalidOperationException("Content bootstrapper did not load content registries.");

            var structured = ContentRegistry.Instance;
            var graniteWallHandle = structured.GetGeologyHandle("core_terrain_wall_rock_granite");
            var graniteWall = structured.GetGeologyByHandle(graniteWallHandle);
            var hasAirGeology = structured.TryGetGeologyHandleByMaterialAndKind(
                "air",
                TerrainKind.OpenNoFloor.ToString(),
                out var airHandle);
            var airGeology = structured.GetGeologyByHandle(airHandle);
            var navigationTuning = structured.GetTuning<Newtonsoft.Json.Linq.JObject>("tuning.navigation", "$");
            var lumberingZone = structured.GetZoneDefinition("lumbering");
            var bedroomZone = structured.GetZoneDefinition("bedroom");
            var loadedContent = bootstrap.CoreCatalogs
                ?? throw new InvalidOperationException("Content bootstrapper did not load core content catalogs.");

            var runtimeContent = FortressRuntimeContentSnapshotLoader.ApplyCoreData(loadedContent.CoreData);
            var coreDataResult = loadedContent.CoreData;
            var constructionCount = runtimeContent.Constructions.Count;
            var recipeCount = runtimeContent.Recipes.Count;
            var workshopCount = runtimeContent.Constructions.GetConstructionsByCategory("workshop").Count();
            var stoneworksRecipeCount = runtimeContent.Recipes.GetRecipesForWorkshop("core_construction_workshop_stoneworks").Count;
            var secondLoadedContent = CoreContentCatalogLoader.Load(bootstrap.CoreDataPath.ResolvedPath!);
            runtimeContent = FortressRuntimeContentSnapshotLoader.ApplyCoreData(secondLoadedContent.CoreData);
            var secondCoreDataResult = secondLoadedContent.CoreData;
            var stoneworks = runtimeContent.Constructions.GetConstruction("core_construction_workshop_stoneworks");
            var stoneBlocksRecipe = runtimeContent.Recipes.GetRecipe("core_recipe_stone_cut_blocks_c");

            RegressionAssert.True(
                result.StructuredLoaded
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
                && lumberingZone?.DisplayName == "Lumbering Zone"
                && lumberingZone.UiHints.Glyph == '\u2663'
                && lumberingZone.UiHints.Keybind == "Z"
                && lumberingZone.DefaultPolicies.AllowsActions.Contains("fell_tree")
                && bedroomZone != null
                && bedroomZone.DisplayName == "Bedroom"
                && coreDataResult.Constructions.LoadedCount > 0
                && coreDataResult.Constructions.ErrorCount == 0
                && coreDataResult.Recipes.LoadedCount > 0
                && coreDataResult.Recipes.ErrorCount == 0
                && loadedContent.Items.LoadedCount > 0
                && loadedContent.Items.ErrorCount == 0
                && loadedContent.Creatures.LoadedCount > 0
                && loadedContent.Creatures.ErrorCount == 0
                && secondCoreDataResult.Constructions.LoadedCount == constructionCount
                && secondCoreDataResult.Recipes.LoadedCount == recipeCount
                && runtimeContent.Constructions.Count == constructionCount
                && runtimeContent.Recipes.Count == recipeCount
                && runtimeContent.Constructions.GetConstructionsByCategory("workshop").Count() == workshopCount
                && runtimeContent.Recipes.GetRecipesForWorkshop("core_construction_workshop_stoneworks").Count == stoneworksRecipeCount
                && stoneworks != null
                && stoneBlocksRecipe != null,
                "Content bootstrapper did not load runtime content into the structured registry correctly.");
        }
        finally
        {
            Logger.Close();
            if (Directory.Exists(logRoot))
            {
                Directory.Delete(logRoot, recursive: true);
            }
        }

        Console.WriteLine("[PASS] Content bootstrap");
    }

    private static void TestContentLoadDiagnostics()
    {
        var missingBaseDir = Path.Combine(Path.GetTempPath(), "HumanFortressMissingContent", Guid.NewGuid().ToString("N"));
        var result = FortressContentLoader.Load(missingBaseDir);

        RegressionAssert.True(
            !result.IsValid()
            && result.HasErrors
            && result.GetBlockingIssues().Any(issue => issue.Code == "Content.PathMissing")
            && result.GetBlockingIssues().Any(issue => issue.Code == "Content.CoreDataPathMissing")
            && !string.IsNullOrWhiteSpace(result.FormatBlockingIssues()),
            "Content bootstrap diagnostics did not report missing content/core data paths.");

        FortressContentLoadException? strictException = null;
        try
        {
            FortressContentLoader.LoadStrict(missingBaseDir);
        }
        catch (FortressContentLoadException ex)
        {
            strictException = ex;
        }

        RegressionAssert.True(
            strictException != null
            && strictException.BlockingIssues.Any(issue => issue.Code == "Content.PathMissing")
            && strictException.BlockingIssues.Any(issue => issue.Code == "Content.CoreDataPathMissing"),
            "Strict content load did not throw the expected structured blocking issues.");

        var warningOnly = new FortressContentLoadResult(
            new ContentPathResolution("published-content", "development-content", "published-content"),
            new ContentPathResolution("published-core", "development-core", "published-core"),
            registries: null,
            coreCatalogs: null,
            registriesAlreadyLoaded: false,
            issues: new[]
            {
                new FortressContentIssue(
                    FortressContentIssueSeverity.Warning,
                    "Content.TestWarning",
                    "warning policy smoke")
            });

        FortressContentLoadException? warningStrictException = null;
        try
        {
            warningOnly.ThrowIfInvalid(treatWarningsAsErrors: true);
        }
        catch (FortressContentLoadException ex)
        {
            warningStrictException = ex;
        }

        RegressionAssert.True(
            warningOnly.IsValid()
            && !warningOnly.IsValid(treatWarningsAsErrors: true)
            && warningStrictException?.BlockingIssues.Count == 1
            && warningStrictException.BlockingIssues[0].Code == "Content.TestWarning",
            "Content warning policy did not promote warnings to strict blocking issues.");

        Console.WriteLine("[PASS] Content load diagnostics");
    }

    private static void TestDefinitionCatalogReloadsClearIndexes()
    {
        var dataPath = Path.Combine(AppContext.BaseDirectory, "data", "core");
        RegressionAssert.True(Directory.Exists(dataPath), $"Data directory not found for definition reload smoke test: {dataPath}");

        var world = new World(2, 10);

        DefinitionCatalogTestSupport.LoadItems(world, dataPath);
        int itemDefinitions = world.Items.DefinitionCount;
        int resources = world.Items.GetByKind("resource").Count();
        int logs = world.Items.GetByTag("log").Count();

        DefinitionCatalogTestSupport.LoadItems(world, dataPath);
        RegressionAssert.True(
            itemDefinitions > 0
            && resources > 0
            && logs > 0
            && world.Items.DefinitionCount == itemDefinitions
            && world.Items.GetByKind("resource").Count() == resources
            && world.Items.GetByTag("log").Count() == logs,
            "Item definition reload duplicated or leaked definition indexes.");

        DefinitionCatalogTestSupport.LoadCreatures(world, dataPath);
        int creatureDefinitions = world.Creatures.DefinitionCount;
        int humanoids = world.Creatures.GetByTag("humanoid").Count();

        DefinitionCatalogTestSupport.LoadCreatures(world, dataPath);
        RegressionAssert.True(
            creatureDefinitions > 0
            && humanoids > 0
            && world.Creatures.DefinitionCount == creatureDefinitions
            && world.Creatures.GetByTag("humanoid").Count() == humanoids,
            "Creature definition reload duplicated or leaked definition indexes.");

        Console.WriteLine("[PASS] Definition catalog reload indexes");
    }

    private static void TestProfessionWeightCommand()
    {
        var scheduler = new TickScheduler();
        var commandQueue = new CommandQueue();
        var diffLog = new DiffLog();
        var itemsDiffLog = new ItemsDiffLog();
        var creaturesDiffLog = new CreaturesDiffLog();
        var world = new World(2, 10);
        var context = CreateRuntimeContext(diffLog, itemsDiffLog, creaturesDiffLog, world);
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
        var context = CreateRuntimeContext(diffLog, itemsDiffLog, creaturesDiffLog, world);
        var pipeline = new SimulationTickPipeline(world, commandQueue, context, diffLog, itemsDiffLog, creaturesDiffLog, navigation: null);

        var rect = new SadRogue.Primitives.Rectangle(1, 1, 2, 2);
        var buildAnchor = new SadRogue.Primitives.Point(4, 4);
        var filter = new MaterialFilterSpec { CategoryKey = "test.floor" };

        pipeline.AttachTo(scheduler);
        commandQueue.Enqueue(new CreateMiningOrderCommand(tick: 0, rect, z: 2, priority: 11));
        commandQueue.Enqueue(new CreateAdvancedMiningOrderCommand(tick: 0, rect, zMin: 1, zMax: 3, action: HumanFortress.Simulation.Orders.MiningAction.Dig, priority: 12));
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
        var context = CreateRuntimeContext(diffLog, itemsDiffLog, creaturesDiffLog, world);
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
        var context = CreateRuntimeContext(
            diffLog,
            itemsDiffLog,
            creaturesDiffLog,
            world,
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
        var context = CreateRuntimeContext(diffLog, itemsDiffLog, creaturesDiffLog, world);
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
        DefinitionCatalogTestSupport.LoadItems(world);
        var context = CreateRuntimeContext(diffLog, itemsDiffLog, creaturesDiffLog, world);
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
        DefinitionCatalogTestSupport.LoadCreatures(world);
        var context = CreateRuntimeContext(diffLog, itemsDiffLog, creaturesDiffLog, world);
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

    private static void TestUnifiedJobsOrchestrator()
    {
        var trace = new List<string>();
        var haulPlanner = new OrchestratorPlannerProbe("haul-plan", trace);
        var constructionMaterialsPlanner = new OrchestratorPlannerProbe("construction-materials", trace);
        var miningPlanner = new OrchestratorPlannerProbe("mining-plan", trace);
        var constructionPlanner = new OrchestratorPlannerProbe("construction-plan", trace);
        var craftPlanner = new OrchestratorPlannerProbe("craft-plan", trace);

        var haulJobs = new OrchestratorTransportProbe(trace);
        var miningJobs = new OrchestratorMiningProbe(trace, backlogCount: 3);
        var constructionJobs = new OrchestratorConstructionProbe(trace);
        var craftJobs = new OrchestratorCraftProbe(trace);
        var tunings = new SchedulerTunings
        {
            HaulingLimits = new SchedulerTunings.HaulingLimitSettings
            {
                ReserveForMining = 2,
                ReserveBacklogThreshold = 1,
                BacklogIntakeCap = 7,
                BacklogIntakeThreshold = 2
            }
        };

        var orchestrator = new UnifiedJobsOrchestrator(
            haulPlanner,
            constructionMaterialsPlanner,
            miningPlanner,
            constructionPlanner,
            craftPlanner,
            haulJobs,
            miningJobs,
            constructionJobs,
            craftJobs,
            tunings);

        orchestrator.ReadTick(42);
        orchestrator.WriteTick(42);

        var expected = new[]
        {
            "mining-plan.read",
            "haul-plan.read",
            "construction-materials.read",
            "construction-plan.read",
            "craft-plan.read",
            "mining-plan.write",
            "haul-plan.write",
            "construction-materials.write",
            "construction-plan.write",
            "craft-plan.write",
            "haul-jobs.hints",
            "haul-jobs.read",
            "haul-jobs.write",
            "mining-jobs.read",
            "mining-jobs.write",
            "construction-jobs.read",
            "construction-jobs.write",
            "craft-jobs.read",
            "craft-jobs.write"
        };

        var stats = orchestrator.GetLastStats();
        RegressionAssert.True(
            trace.SequenceEqual(expected)
            && haulJobs.IntakeCap == 7
            && haulJobs.ReserveSlots == 2
            && stats.IntakeHaul == haulJobs.LastIntakeCount
            && stats.IntakeMining == miningJobs.LastIntakeCount
            && stats.IntakeConstruction == constructionJobs.LastIntakeCount
            && stats.IntakeCraft == craftJobs.LastIntakeCount,
            "UnifiedJobsOrchestrator order, scheduling hints, or intake stats changed.");

        Console.WriteLine("[PASS] Unified jobs orchestrator");
    }

    private static void TestMiningDropResolverJson()
    {
        const string tuningJson = """
        {
          "geology_ticks": {
            "default": { "wall": 17, "ramp": 5 }
          },
          "geology_drops": {
            "core_geology_granite": {
              "wall": [
                { "item_id": "core_item_boulder_granite", "min": 2, "max": 2 }
              ],
              "ramp": [
                { "item_id": "core_item_boulder_granite", "min": 1, "max": 1 }
              ]
            }
          }
        }
        """;

        var geology = new TestRuntimeGeologyCatalog();
        var resolver = new MiningDropResolver(geology, tuningJson);

        var wallDrops = resolver.ChooseDropsFor(geology.GraniteWallHandle, TerrainKind.SolidWall);
        var rampDrops = resolver.ChooseDropsFor(geology.GraniteWallHandle, TerrainKind.Ramp);
        var aliasDrops = resolver.ChooseDropsFor(geology.AliasGraniteWallHandle, TerrainKind.SolidWall);

        RegressionAssert.True(
            resolver.CalculateRequiredTicks(geology.GraniteWallHandle, TerrainKind.SolidWall) == 17
            && resolver.CalculateRequiredTicks(geology.GraniteWallHandle, TerrainKind.Ramp) == 5
            && resolver.ResolveAirGeologyHandle() == geology.AirHandle
            && wallDrops.Count == 1
            && wallDrops[0].itemId == "core_item_boulder_granite"
            && wallDrops[0].qty == 2
            && rampDrops.Count == 1
            && rampDrops[0].qty == 1
            && aliasDrops.Count == 1
            && aliasDrops[0].itemId == "core_item_boulder_granite",
            "MiningDropResolver JSON parsing or geology alias lookup changed.");

        Console.WriteLine("[PASS] Mining drop resolver JSON");
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

    private sealed class OrchestratorPlannerProbe : ITick
    {
        private readonly string _name;
        private readonly List<string> _trace;

        public OrchestratorPlannerProbe(string name, List<string> trace)
        {
            _name = name;
            _trace = trace;
        }

        public int Priority => 1;
        public string SystemId => _name;

        public void ReadTick(ulong tick)
        {
            _trace.Add($"{_name}.read");
        }

        public void WriteTick(ulong tick)
        {
            _trace.Add($"{_name}.write");
        }
    }

    private sealed class OrchestratorTransportProbe : IUnifiedTransportJobExecutor
    {
        private readonly List<string> _trace;

        public OrchestratorTransportProbe(List<string> trace)
        {
            _trace = trace;
        }

        public int Priority => 1;
        public string SystemId => "haul-jobs";
        public int LastIntakeCount { get; private set; } = 2;
        public int? IntakeCap { get; private set; }
        public int ReserveSlots { get; private set; }

        public void ApplySchedulingHints(int? intakeCap, int? maxActiveCap, int reserveSlots)
        {
            IntakeCap = intakeCap;
            ReserveSlots = reserveSlots;
            _trace.Add("haul-jobs.hints");
        }

        public TransportJobStatsSnapshot GetLastStatsSnapshot()
        {
            return new TransportJobStatsSnapshot(LastIntakeCount, Active: 1, Backlog: 0, CompletedDelta: 0, RequeuedDelta: 0, NoPathDelta: 0, CarryoverOld: 0);
        }

        public void ReadTick(ulong tick) => _trace.Add("haul-jobs.read");

        public void WriteTick(ulong tick) => _trace.Add("haul-jobs.write");
    }

    private sealed class OrchestratorMiningProbe : IUnifiedMiningJobExecutor
    {
        private readonly List<string> _trace;
        private readonly int _backlogCount;

        public OrchestratorMiningProbe(List<string> trace, int backlogCount)
        {
            _trace = trace;
            _backlogCount = backlogCount;
        }

        public int Priority => 1;
        public string SystemId => "mining-jobs";
        public int LastIntakeCount { get; private set; } = 3;

        public int GetBacklogCount() => _backlogCount;

        public MiningJobStatsSnapshot GetLastStatsSnapshot()
        {
            return new MiningJobStatsSnapshot(LastIntakeCount, Active: 1, Backlog: _backlogCount, Deferred: 0, ReservedTiles: 0, CarryoverOld: 0);
        }

        public void ReadTick(ulong tick) => _trace.Add("mining-jobs.read");

        public void WriteTick(ulong tick) => _trace.Add("mining-jobs.write");
    }

    private sealed class OrchestratorConstructionProbe : IUnifiedConstructionJobExecutor
    {
        private readonly List<string> _trace;

        public OrchestratorConstructionProbe(List<string> trace)
        {
            _trace = trace;
        }

        public int Priority => 1;
        public string SystemId => "construction-jobs";
        public int LastIntakeCount { get; private set; } = 4;

        public void ReadTick(ulong tick) => _trace.Add("construction-jobs.read");

        public void WriteTick(ulong tick) => _trace.Add("construction-jobs.write");
    }

    private sealed class OrchestratorCraftProbe : IUnifiedCraftJobExecutor
    {
        private readonly List<string> _trace;

        public OrchestratorCraftProbe(List<string> trace)
        {
            _trace = trace;
        }

        public int Priority => 1;
        public string SystemId => "craft-jobs";
        public int LastIntakeCount { get; private set; } = 5;

        public void ReadTick(ulong tick) => _trace.Add("craft-jobs.read");

        public void WriteTick(ulong tick) => _trace.Add("craft-jobs.write");
    }

    private sealed class TestRuntimeGeologyCatalog : IRuntimeGeologyCatalog
    {
        private readonly Dictionary<ushort, HumanFortress.Core.Content.GeologyData> _byHandle;
        private readonly Dictionary<string, ushort> _handles;

        public TestRuntimeGeologyCatalog()
        {
            var granite = new HumanFortress.Core.Content.GeologyData
            {
                Id = "core_geology_granite",
                Material = "granite"
            };
            var aliasGranite = new HumanFortress.Core.Content.GeologyData
            {
                Id = "core_terrain_wall_rock_granite",
                Material = "granite"
            };
            var air = new HumanFortress.Core.Content.GeologyData
            {
                Id = "core_geology_air",
                Material = "air"
            };

            _byHandle = new Dictionary<ushort, HumanFortress.Core.Content.GeologyData>
            {
                [GraniteWallHandle] = granite,
                [AliasGraniteWallHandle] = aliasGranite,
                [AirHandle] = air
            };
            _handles = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase)
            {
                ["core_geology_granite"] = GraniteWallHandle,
                ["core_terrain_wall_rock_granite"] = AliasGraniteWallHandle,
                ["core_geology_air"] = AirHandle,
                ["air|OpenNoFloor"] = AirHandle
            };
        }

        public ushort GraniteWallHandle => 11;
        public ushort AliasGraniteWallHandle => 12;
        public ushort AirHandle => 1;

        public HumanFortress.Core.Content.GeologyData? GetGeologyEntry(string id)
        {
            return _handles.TryGetValue(id, out var handle) ? GetGeologyByHandle(handle) : null;
        }

        public HumanFortress.Core.Content.GeologyData? GetGeologyByHandle(ushort handle)
        {
            return _byHandle.GetValueOrDefault(handle);
        }

        public ushort GetGeologyHandle(string id)
        {
            return _handles.GetValueOrDefault(id);
        }

        public bool TryGetGeologyHandleByMaterialAndKind(string materialId, string terrainKindName, out ushort handle)
        {
            return _handles.TryGetValue($"{materialId}|{terrainKindName}", out handle);
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
