using System.Collections.Immutable;
using System.Text.Json.Nodes;
using HumanFortress.App.Rendering;
using HumanFortress.Runtime.Commands;
using HumanFortress.Runtime.Jobs;
using HumanFortress.App;
using HumanFortress.Content.Definitions;
using HumanFortress.Content.Loading;
using HumanFortress.Contracts.Content.Loading;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Determinism;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Save;
using HumanFortress.Contracts.Simulation.Save;
using HumanFortress.Core.Events;
using HumanFortress.Contracts.Diagnostics;
using HumanFortress.Core.Random;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Core.World;
using HumanFortress.Contracts.Runtime.Snapshots;
using HumanFortress.Jobs.Configuration;
using HumanFortress.Jobs.Craft;
using HumanFortress.Jobs.Diff;
using HumanFortress.Jobs.Mining;
using HumanFortress.Jobs.Orchestration;
using HumanFortress.Jobs.Profession;
using HumanFortress.Jobs.Replay;
using HumanFortress.Jobs.Transport;
using HumanFortress.Runtime;
using HumanFortress.Runtime.Composition;
using HumanFortress.Runtime.Host;
using HumanFortress.Runtime.Replay;
using HumanFortress.Runtime.Save;
using HumanFortress.Runtime.Content;
using HumanFortress.Runtime.Diff;
using HumanFortress.Runtime.Session;
using HumanFortress.Runtime.Snapshots;
using HumanFortress.Runtime.Startup;
using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Diff;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Jobs;
using HumanFortress.Simulation.Orders;
using HumanFortress.Simulation.Placeables;
using HumanFortress.Simulation.Replay;
using HumanFortress.Simulation.Save;
using HumanFortress.Simulation.Stockpile;
using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.World;
using HumanFortress.Simulation.Zones;

internal static class CoreRuntimeSmokeTests
{
    private static readonly string[] ExpectedCommandOrder = { "first", "second" };

    public static void RunAll()
    {
        Console.WriteLine("=== Core Runtime Smoke Tests ===");

        TestTickScheduler();
        TestDeterministicRng();
        TestDeterministicRuntimeIds();
        TestRuntimeCommandIdsAreStable();
        TestRuntimeCommandReplayFactory();
        TestRuntimeCommandReplayRestorer();
        TestReplayHashBuilder();
        TestCommandReplayJournalHashBuilder();
        TestWorldReplayHashBuilder();
        TestWorldSaveSnapshotBuilder();
        TestWorldSavePayloadRestorer();
        TestMiningReplayHashBuilder();
        TestCraftReplayHashBuilder();
        TestRuntimeReplayCheckpointHash();
        TestFullRuntimeSimulationLoopDeterminism();
        TestDiffLog();
        TestTypedCommandDiffOrderingPolicy();
        TestStockpileDiffOrderingUsesEntityKeys();
        TestStockpileMessageDrainSortKeyIsStable();
        TestMiningRectanglesIncludeSingleCellMaxExtent();
        TestMiningZRangeMapper();
        TestStockpileFilterUsesItemProjection();
        TestStockpileDataIndexUpdatesAreIdempotent();
        TestStockpileDataUsesEntityKeysForItemIndexes();
        TestZoneAndStockpileReadSnapshotsUseStableOrdering();
        TestItemManagerEntityKeyLookupIndexStaysInSync();
        TestCreatureManagerEntityKeyLookupIndexStaysInSync();
        TestTransportStockpileIndexEmitterUsesStockpileDiffs();
        TestHaulingPlannerReservesStockpileCapacity();
        TestHaulingPlannerDoesNotReserveDuplicatePendingTransport();
        TestWorldChunks();
        TestWorldPlaceableAndConstructionSnapshotsUseStableOrdering();
        TestCrossChunkPlaceableReferencesResolveAndRemove();
        TestConstructionRequirementsSupportDefinitionIds();
        TestReservations();
        TestCommandQueue();
        TestEventBus();
        TestSimulationCommandStage();
        TestSimulationRuntimeHostCore();
        TestSimulationRuntimeSessionFactory();
        TestRuntimeStartupHelpers();
        TestUnifiedJobsOrchestrator();
        TestMiningDropResolverJson();
        TestNavigationTuningJson();
        TestConstructionTuningJson();
        TestPlaceableTuningJson();
        TestSchedulerTuningJson();
        TestAsyncDiagnosticLogger();
        TestContentBootstrap();
        TestContentLoadDiagnostics();
        TestDefinitionCatalogReloadsClearIndexes();
        TestOrderCommandsUseRuntimeTarget();
        TestOrdersManagerActiveSnapshotsUseStableOrdering();
        TestZoneCommandsUseRuntimeTarget();
        TestWorkshopQueueCommandUsesRuntimeTarget();
        TestStockpileCommandUsesRuntimeTarget();
        TestRuntimeFramePublisherPresenterDiffBase();
        TestAppMapViewportPresenterConsumesRuntimeDeltas();
        TestAppUiOverlayPresenterConsumesRuntimeSectionDeltas();
        TestRuntimeStockpilePresetMenuUsesContentCatalog();
        TestProfessionWeightCommand();
        TestSpawnItemCommandUsesItemDiff();
        TestSpawnCreatureCommandUsesCreatureDiff();
        TestEmbarkabilityDiagnostics();

        Console.WriteLine("=== Core Runtime Smoke Tests Completed ===\n");
    }

    private static SimulationCommandExecutionContext CreateRuntimeContext(
        DiffLog diffLog,
        RuntimeMutationDiffLogs mutationDiffs,
        World world,
        IEventBus? eventBus = null,
        IRecipeCatalog? recipes = null,
        FortressRuntimeStockpilePresetCatalog? stockpilePresets = null,
        Action<string>? log = null)
    {
        var simulationContext = new SimulationRuntimeContext(
            diffLog,
            world,
            eventBus ?? new EventBus());

        return new SimulationCommandExecutionContext(
            simulationContext,
            simulationContext,
            world,
            mutationDiffs,
            recipes ?? RecipeCatalogStore.Empty,
            stockpilePresets,
            log: log);
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

        var duplicateScheduler = new TickScheduler();
        duplicateScheduler.RegisterSystem(new TestTickSystem());
        var duplicateRejected = false;
        try
        {
            duplicateScheduler.RegisterSystem(new TestTickSystem());
        }
        catch (InvalidOperationException)
        {
            duplicateRejected = true;
        }

        RegressionAssert.True(
            duplicateRejected,
            "TickScheduler should reject duplicate SystemId values so deterministic ordering and failure state remain unambiguous.");

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

        var faultScheduler = new TickScheduler();
        var failingReadSystem = new FailingReadTickSystem();
        var healthySystem = new TestTickSystem();
        faultScheduler.RegisterSystem(failingReadSystem);
        faultScheduler.RegisterSystem(healthySystem);
        faultScheduler.ExecuteSingleTick();

        RegressionAssert.True(
            failingReadSystem.ReadCount == 1
            && failingReadSystem.WriteCount == 0
            && healthySystem.ReadCount == 1
            && healthySystem.WriteCount == 1,
            "TickScheduler should skip a system write after that system fails during read.");

        faultScheduler.ExecuteSingleTick();
        faultScheduler.ExecuteSingleTick();
        faultScheduler.ExecuteSingleTick();

        RegressionAssert.True(
            failingReadSystem.ReadCount == 3
            && failingReadSystem.WriteCount == 0
            && healthySystem.ReadCount == 4
            && healthySystem.WriteCount == 4,
            "TickScheduler should quarantine repeatedly failing systems while continuing healthy systems.");

        Console.WriteLine("[PASS] TickScheduler");
    }

    private static void TestDeterministicRng()
    {
        var rng1 = new DeterministicRng(12345);
        var rng2 = new DeterministicRng(12345);
        var adjacentSeedDiffers = new DeterministicRng(12345).Next() != new DeterministicRng(12346).Next();

        for (int i = 0; i < 100; i++)
            RegressionAssert.True(rng1.Next() == rng2.Next(), "DeterministicRng diverged for identical seed.");

        var manager = new RngStreamManager(54321);
        var stream1 = manager.GetStream("test");
        var stream2 = manager.GetStream("test");
        var beta = manager.GetStream("beta");
        var alpha = manager.GetStream("alpha");
        _ = beta.Next();
        _ = alpha.Next();

        var snapshot = manager.GetStateSnapshot();
        var snapshotHash = RngReplayHashBuilder.Build(snapshot);
        var reverseSnapshotHash = RngReplayHashBuilder.Build(snapshot.Reverse());
        _ = alpha.Next();
        var changedSnapshotHash = RngReplayHashBuilder.Build(manager);
        var restoredManager = new RngStreamManager(54321);
        restoredManager.RestoreStates(snapshot);
        var restoredSnapshotHash = RngReplayHashBuilder.Build(restoredManager);
        var emptyStreamRejected = false;
        try
        {
            manager.GetStream("");
        }
        catch (ArgumentException)
        {
            emptyStreamRejected = true;
        }

        RegressionAssert.True(
            adjacentSeedDiffers
            && ReferenceEquals(stream1, stream2)
            && snapshot.Select(state => state.StreamName).SequenceEqual(new[] { "alpha", "beta", "test" })
            && snapshotHash == reverseSnapshotHash
            && snapshotHash != changedSnapshotHash
            && snapshotHash == restoredSnapshotHash,
            "RngStreamManager did not expose stable canonical stream state snapshots.");

        RegressionAssert.True(
            emptyStreamRejected,
            "RngStreamManager should reject empty stream names.");

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

        var positionA1 = DeterministicGuidGenerator.GenerateFromPosition(testScope, 10, 20, 0, 0x504F534755494431UL);
        var positionA2 = DeterministicGuidGenerator.GenerateFromPosition(testScope, 10, 20, 0, 0x504F534755494431UL);
        var positionB = DeterministicGuidGenerator.GenerateFromPosition(testScope, 10, 20, 0, 0x504F534755494432UL);

        var queueChecksum1 = BuildWorkshopQueueIdChecksum();
        var queueChecksum2 = BuildWorkshopQueueIdChecksum();

        RegressionAssert.True(
            sequenceA1 == sequenceA2
            && sequenceA1 != sequenceB
            && derivedA1 == derivedA2
            && derivedA1 != derivedB
            && positionA1 == positionA2
            && positionA1 != positionB
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

    private static void TestRuntimeCommandIdsAreStable()
    {
        var rect = new SadRogue.Primitives.Rectangle(1, 2, 3, 4);
        ICommand haulA = new CreateHaulOrderCommand(42, rect, z: 0, priority: 10);
        ICommand haulB = new CreateHaulOrderCommand(42, rect, z: 0, priority: 10);
        ICommand haulDifferentTick = new CreateHaulOrderCommand(43, rect, z: 0, priority: 10);
        ICommand constructionStoneTags = new CreateConstructionOrderCommand(
            42,
            rect,
            zMin: 0,
            zMax: 0,
            ConstructionShape.Wall,
            new MaterialFilterSpec
            {
                CategoryKey = "test.wall",
                Tags = new[] { "stone_block", "construction" }
            },
            priority: 10);
        ICommand constructionStoneTagsReordered = new CreateConstructionOrderCommand(
            42,
            rect,
            zMin: 0,
            zMax: 0,
            ConstructionShape.Wall,
            new MaterialFilterSpec
            {
                CategoryKey = "test.wall",
                Tags = new[] { "construction", "stone_block" }
            },
            priority: 10);
        ICommand constructionWoodTags = new CreateConstructionOrderCommand(
            42,
            rect,
            zMin: 0,
            zMax: 0,
            ConstructionShape.Wall,
            new MaterialFilterSpec
            {
                CategoryKey = "test.wall",
                Tags = new[] { "construction", "wood_log" }
            },
            priority: 10);

        var workshopId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        ICommand addRecipeA = RuntimeWorkshopCommandFactory.AddRecipe(workshopId, "core_recipe_plank")(42);
        ICommand addRecipeB = RuntimeWorkshopCommandFactory.AddRecipe(workshopId, "core_recipe_plank")(42);
        ICommand setSlots = RuntimeWorkshopCommandFactory.SetWorkerSlots(workshopId, 2)(42);
        ICommand wrappedA1 = new RuntimeIdentifiedCommand(haulA, sequence: 1);
        ICommand wrappedA2 = new RuntimeIdentifiedCommand(haulB, sequence: 1);
        ICommand wrappedB = new RuntimeIdentifiedCommand(haulB, sequence: 2);

        RegressionAssert.True(
            haulA.CommandId == haulB.CommandId
            && haulA.CommandId != haulDifferentTick.CommandId
            && constructionStoneTags.CommandId == constructionStoneTagsReordered.CommandId
            && constructionStoneTags.CommandId != constructionWoodTags.CommandId
            && addRecipeA.CommandId == addRecipeB.CommandId
            && addRecipeA.CommandId != setSlots.CommandId
            && wrappedA1.CommandId == wrappedA2.CommandId
            && wrappedA1.CommandId != wrappedB.CommandId
            && addRecipeA.Serialize().Length > 0,
            "Runtime command ids should be stable for replay inputs and workshop commands should serialize their payload.");

        Console.WriteLine("[PASS] Runtime command ids");
    }

    private static void TestRuntimeCommandReplayFactory()
    {
        var factory = new RuntimeCommandReplayFactory();
        var rect = new SadRogue.Primitives.Rectangle(1, 2, 3, 4);
        var point = new SadRogue.Primitives.Point(5, 6);
        var workerId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var workshopId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
        var entryId = Guid.Parse("cccccccc-dddd-eeee-ffff-aaaaaaaaaaaa");

        ICommand[] commands =
        {
            new CreateMiningOrderCommand(10, rect, z: 1, priority: 11),
            new CreateAdvancedMiningOrderCommand(11, rect, zMin: 3, zMax: 1, MiningAction.DigRamp, priority: 12),
            new CreateHaulOrderCommand(12, rect, z: 2, priority: 13),
            new CreateConstructionOrderCommand(
                13,
                rect,
                zMin: 4,
                zMax: 2,
                ConstructionShape.Wall,
                new MaterialFilterSpec
                {
                    PreferredMaterialId = "core_mat_stone_granite",
                    CategoryKey = "l0.wall",
                    Tags = new[] { "stone", "block", "construction" }
                },
                priority: 14),
            new CreateBuildableConstructionOrderCommand(14, "core_placeable_workbench", point, z: 3, priority: 15),
            new CreateZoneCommand(15, "core_zone_stockpile", "Replay Zone", rect, z: 4),
            new UpdateZoneCellsCommand(16, zoneId: 123, rect, z: 5, isAdding: true),
            new DeleteZoneCommand(17, zoneId: 123),
            new CreateStockpileCommand(18, rect, z: 6, presetId: "wood"),
            new DeleteStockpileCommand(19, zoneId: 456),
            new SetProfessionWeightCommand(20, workerId, "miner", weight: 8),
            new UpdateWorkshopQueueCommand(
                21,
                workshopId,
                WorkshopQueueOperation.MoveEntry,
                recipeId: "core_recipe_plank",
                entryId: entryId,
                intValue: 2,
                moveOffset: -1,
                boolValue: false),
            new SpawnItemCommand(22, "core_item_log_oak", point, z: 7, quantity: 9),
            new SpawnCreatureCommand(23, "core_race_dwarf", point, z: 8, factionId: "player")
        };

        foreach (var command in commands)
        {
            AssertRuntimeReplayRoundTrip(factory, command);
        }

        ICommand identified = new RuntimeIdentifiedCommand(
            new CreateHaulOrderCommand(24, rect, z: 9, priority: 16),
            sequence: 42);
        var identifiedRecord = CommandReplayRecord.FromCommand(identified);
        RegressionAssert.True(
            identifiedRecord.CommandIdentitySequence == 42,
            "CommandReplayRecord did not preserve Runtime command identity sequence.");
        AssertRuntimeReplayRoundTrip(factory, identified);

        var unknown = new CommandReplayRecord(1, Guid.Empty, "unknown.command", Array.Empty<byte>());
        var rejectedUnknown = !factory.TryCreateCommand(unknown, out _, out var unknownError)
            && unknownError?.Contains("Unknown replay command type", StringComparison.Ordinal) == true;

        var validRecord = CommandReplayRecord.FromCommand(new CreateHaulOrderCommand(25, rect, z: 10, priority: 17));
        var payload = validRecord.ToPayloadArray();
        var truncated = new CommandReplayRecord(
            validRecord.Tick,
            validRecord.CommandId,
            validRecord.CommandType,
            payload.Take(payload.Length - 1).ToArray());
        var rejectedTruncated = !factory.TryCreateCommand(truncated, out _, out var truncatedError)
            && truncatedError?.Contains("decode failed", StringComparison.Ordinal) == true;

        var unsupportedVersionPayload = validRecord.ToPayloadArray();
        BitConverter.GetBytes(999).CopyTo(unsupportedVersionPayload, 0);
        var unsupportedVersion = new CommandReplayRecord(
            validRecord.Tick,
            validRecord.CommandId,
            validRecord.CommandType,
            unsupportedVersionPayload);
        var rejectedUnsupportedVersion = !factory.TryCreateCommand(unsupportedVersion, out _, out var unsupportedVersionError)
            && unsupportedVersionError?.Contains("Unsupported payload version", StringComparison.Ordinal) == true;

        var wrongId = new CommandReplayRecord(
            validRecord.Tick,
            Guid.Parse("dddddddd-eeee-ffff-aaaa-bbbbbbbbbbbb"),
            validRecord.CommandType,
            validRecord.Payload);
        var rejectedWrongId = !factory.TryCreateCommand(wrongId, out _, out var wrongIdError)
            && wrongIdError?.Contains("id mismatch", StringComparison.Ordinal) == true;

        RegressionAssert.True(
            rejectedUnknown && rejectedTruncated && rejectedUnsupportedVersion && rejectedWrongId,
            "RuntimeCommandReplayFactory did not reject invalid replay records predictably.");

        Console.WriteLine("[PASS] Runtime command replay factory");
    }

    private static void AssertRuntimeReplayRoundTrip(RuntimeCommandReplayFactory factory, ICommand original)
    {
        var record = CommandReplayRecord.FromCommand(original);
        var decoded = factory.TryCreateCommand(record, out var replayCommand, out var errorMessage);

        RegressionAssert.True(
            decoded
            && replayCommand != null
            && replayCommand.Tick == original.Tick
            && replayCommand.CommandId == original.CommandId
            && replayCommand.CommandType == original.CommandType
            && replayCommand.Serialize().SequenceEqual(original.Serialize()),
            $"RuntimeCommandReplayFactory failed replay round trip for {original.CommandType}: {errorMessage}");
    }

    private static void TestRuntimeCommandReplayRestorer()
    {
        var baselineHash = ExecuteOrderCommandsAndHash(BuildReplayOrderCommands(wrapWithRuntimeIdentity: false));
        var replayServices = new RuntimeSessionServices();
        var replayRecords = BuildReplayOrderCommands(wrapWithRuntimeIdentity: true)
            .Select(CommandReplayRecord.FromCommand)
            .ToArray();
        var replayHash = ExecuteReplayRecordsAndHash(replayServices, replayRecords, out var restoreResult);
        var nextIdentitySequence = replayServices.NextCommandIdentitySequence();

        RegressionAssert.True(
            restoreResult.Success
            && restoreResult.RecordCount == replayRecords.Length
            && restoreResult.RestoredCommandCount == replayRecords.Length
            && restoreResult.MaxCommandIdentitySequence == replayRecords.Length
            && nextIdentitySequence == replayRecords.Length + 1
            && replayHash == baselineHash,
            "Runtime command replay restore did not produce the same authoritative order hash or advance identity sequence.");

        var atomicServices = new RuntimeSessionServices();
        var atomicRect = new SadRogue.Primitives.Rectangle(9, 9, 1, 1);
        var preservedCommand = new CreateHaulOrderCommand(0, atomicRect, z: 0, priority: 99);
        atomicServices.CommandQueue.Enqueue(preservedCommand);
        var failedResult = new RuntimeCommandReplayRestorer().RestorePending(
            atomicServices,
            new[] { new CommandReplayRecord(0, Guid.Empty, "unknown.command", Array.Empty<byte>()) });
        var atomicWorld = ExecuteExistingOrderQueue(atomicServices);
        var preservedHauls = new List<HaulDesignation>();
        var preservedHaulCount = atomicWorld.Orders.DrainHaulDesignations(preservedHauls, maxCount: 4);

        RegressionAssert.True(
            !failedResult.Success
            && failedResult.Issues.Count == 1
            && preservedHaulCount == 1
            && preservedHauls[0].Priority == 99,
            "Runtime command replay restore failure was not atomic for the existing pending command queue.");

        Console.WriteLine("[PASS] Runtime command replay restorer");
    }

    private static void TestReplayHashBuilder()
    {
        var first = ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("snapshot.test");
            hash.AddInt32(42);
            hash.AddUInt64(123);
            hash.AddBoolean(true);
            hash.AddGuid(Guid.Parse("11111111-2222-3333-4444-555555555555"));
            hash.AddNullableString(null);
            hash.AddBytes(new byte[] { 1, 2, 3 });
        });
        var second = ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("snapshot.test");
            hash.AddInt32(42);
            hash.AddUInt64(123);
            hash.AddBoolean(true);
            hash.AddGuid(Guid.Parse("11111111-2222-3333-4444-555555555555"));
            hash.AddNullableString(null);
            hash.AddBytes(new byte[] { 1, 2, 3 });
        });
        var different = ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("snapshot.test");
            hash.AddInt32(43);
        });

        var finalizedGuarded = false;
        using (var builder = new ReplayHashBuilder())
        {
            builder.AddString("done");
            _ = builder.FinishHex();
            try
            {
                builder.AddInt32(1);
            }
            catch (InvalidOperationException)
            {
                finalizedGuarded = true;
            }
        }

        RegressionAssert.True(
            first == second
            && first != different
            && finalizedGuarded,
            "ReplayHashBuilder did not provide stable canonical primitive hashing.");

        Console.WriteLine("[PASS] Replay hash builder");
    }

    private static void TestCommandReplayJournalHashBuilder()
    {
        var rect = new SadRogue.Primitives.Rectangle(1, 2, 3, 4);
        var recordA = CommandReplayRecord.FromCommand(new RuntimeIdentifiedCommand(
            new CreateHaulOrderCommand(10, rect, z: 1, priority: 5),
            sequence: 1));
        var recordB = CommandReplayRecord.FromCommand(new RuntimeIdentifiedCommand(
            new CreateMiningOrderCommand(11, rect, z: 1, priority: 6),
            sequence: 2));
        var changedRecord = CommandReplayRecord.FromCommand(new RuntimeIdentifiedCommand(
            new CreateHaulOrderCommand(10, rect, z: 1, priority: 7),
            sequence: 1));

        var hash = CommandReplayJournalHashBuilder.Build(new[] { recordA, recordB });
        var sameHash = CommandReplayJournalHashBuilder.Build(new[] { recordA, recordB });
        var reversedHash = CommandReplayJournalHashBuilder.Build(new[] { recordB, recordA });
        var changedHash = CommandReplayJournalHashBuilder.Build(new[] { changedRecord, recordB });
        var emptyHash = CommandReplayJournalHashBuilder.Build(Array.Empty<CommandReplayRecord>());

        RegressionAssert.True(
            hash == sameHash
            && hash != reversedHash
            && hash != changedHash
            && hash != emptyHash,
            "CommandReplayJournalHashBuilder did not provide stable command replay journal hashes.");

        Console.WriteLine("[PASS] Command replay journal hash builder");
    }

    private static void TestWorldReplayHashBuilder()
    {
        var worldA = CreateReplayHashWorld();
        var worldB = CreateReplayHashWorld();
        var worldC = CreateReplayHashWorld();
        var workshopQueueCountBefore = GetReplayHashWorkshop(worldA).Workshop!.Queue.Count;
        var hashA = WorldReplayHashBuilder.Build(worldA);
        var hashB = WorldReplayHashBuilder.Build(worldB);
        var hashASecondRead = WorldReplayHashBuilder.Build(worldA);
        var workshopQueueCountAfter = GetReplayHashWorkshop(worldA).Workshop!.Queue.Count;

        GetReplayHashWorkshop(worldC).Workshop!.Queue[0].Status = CraftQueueStatus.InProgress;
        var changedPlaceableHash = WorldReplayHashBuilder.Build(worldC);
        worldB.SetTile(1, 1, 0, new TileBase(0, (ushort)TerrainKind.SolidWall, 0, 0, 0, 0, 1), 9);
        var changedHash = WorldReplayHashBuilder.Build(worldB);

        RegressionAssert.True(
            hashA == hashB
            && hashA == hashASecondRead
            && workshopQueueCountBefore == 1
            && workshopQueueCountAfter == workshopQueueCountBefore
            && hashA != changedPlaceableHash
            && hashA != changedHash,
            "WorldReplayHashBuilder did not produce stable authoritative hashes for equivalent world state.");

        Console.WriteLine("[PASS] World replay hash builder");
    }

    private static void TestWorldSaveSnapshotBuilder()
    {
        var world = CreateReplayHashWorld();
        var snapshot = WorldSaveSnapshotBuilder.Build(world);
        var secondSnapshot = WorldSaveSnapshotBuilder.Build(world);
        var originalTerrainHash = snapshot.SectionHashes.TerrainHash;

        world.SetTile(1, 1, 0, new TileBase(0, (ushort)TerrainKind.SolidWall, 0, 0, 0, 0, 1), 9);
        var changedSnapshot = WorldSaveSnapshotBuilder.Build(world);

        RegressionAssert.True(
            snapshot.SchemaVersion == WorldSaveSnapshotSchema.CurrentVersion
            && snapshot.ReplayHash == WorldReplayHashBuilder.Build(CreateReplayHashWorld())
            && snapshot.ReplayHash == secondSnapshot.ReplayHash
            && snapshot.SectionHashes == secondSnapshot.SectionHashes
            && snapshot.ReplayHash != changedSnapshot.ReplayHash
            && originalTerrainHash != changedSnapshot.SectionHashes.TerrainHash
            && snapshot.SectionHashes.ItemsHash == changedSnapshot.SectionHashes.ItemsHash
            && snapshot.Counts.ChunkCount > 0
            && snapshot.Counts.TileCount > 0
            && snapshot.Counts.ItemCount == 1
            && snapshot.Counts.CreatureCount == 1
            && snapshot.Counts.ItemReservationCount == 1
            && snapshot.Counts.CreatureReservationCount == 1
            && snapshot.Counts.StockpileZoneCount == 1
            && snapshot.Counts.OwnedPlaceableCount == 2
            && snapshot.Counts.HaulOrderCount == 1,
            "WorldSaveSnapshotBuilder did not provide a stable Simulation-owned world save summary.");

        Console.WriteLine("[PASS] World save snapshot builder");
    }

    private static void TestWorldSavePayloadRestorer()
    {
        var terrainWorld = new World(2, 2);
        terrainWorld.SetTile(1, 1, 0, new TileBase(7, (ushort)TerrainKind.OpenWithFloor, 3, 0, 0, 1, 2), 4);
        terrainWorld.SetTile(2, 1, 0, new TileBase(7, (ushort)TerrainKind.OpenWithFloor, 3, 0, 0, 1, 2), 4);
        terrainWorld.SetTile(3, 1, 0, new TileBase(7, (ushort)TerrainKind.OpenWithFloor, 3, 0, 0, 1, 2), 4);
        terrainWorld.SetTile(1, 2, 0, new TileBase(7, (ushort)TerrainKind.OpenWithFloor, 3, 0, 0, 1, 2), 4);
        terrainWorld.SetTile(33, 2, 1, new TileBase(8, (ushort)TerrainKind.SolidWall, 0, 1, 2, 0, 9), 5);

        var terrainOnlyPayload = WorldSavePayloadBuilder.Build(terrainWorld);
        var terrainOnlyHash = WorldReplayHashBuilder.Build(terrainWorld);
        var terrainOnlyRestore = WorldSavePayloadRestorer.RestoreTerrainOnly(terrainOnlyPayload);
        DefinitionCatalogTestSupport.LoadItems(terrainWorld);
        var itemId = terrainWorld.Items.SpawnItem(
            "core_item_log_oak",
            new SadRogue.Primitives.Point(1, 1),
            z: 0,
            quantity: 3,
            currentTick: 6)
            ?? throw new InvalidOperationException("Test item failed to spawn.");
        var item = terrainWorld.Items.GetInstance(itemId)
            ?? throw new InvalidOperationException("Test item could not be read.");
        item.OwnerFactionId = "player";
        item.UsePolicy = UsePolicy.Faction;
        item.Forbidden = true;
        item.QualityTier = 2;
        item.Artifact = true;
        item.ArtifactName = "The Regression Log";
        item.ConditionState = "Good";
        item.DurabilityCurrent = 7;
        item.DurabilityMax = 9;
        item.MakerFactionId = "player";
        item.StyleTag = "test.style";
        item.Improvements = new List<Improvement>
        {
            new()
            {
                Type = "engraving",
                MaterialId = "core_mat_wood_oak",
                QualityTier = 1,
                Description = "save payload smoke"
            }
        };
        item.Perishable = new PerishableState
        {
            CreatedAtTick = 6,
            FreshDurationTicks = 100,
            SpoilDurationTicks = 200,
            CurrentFreshness = 0.75f
        };

        DefinitionCatalogTestSupport.LoadCreatures(terrainWorld);
        var creatureId = terrainWorld.Creatures.SpawnCreature(
            "core_race_dwarf",
            new SadRogue.Primitives.Point(2, 1),
            z: 0,
            factionId: "player",
            currentTick: 7)
            ?? throw new InvalidOperationException("Test creature failed to spawn.");
        var creature = terrainWorld.Creatures.GetInstance(creatureId)
            ?? throw new InvalidOperationException("Test creature could not be read.");
        creature.HP = 77;
        creature.MaxHP = 120;
        var itemLocalReservationJob = Guid.Parse("22222222-3333-4444-5555-666666666666");
        item.ReservationTokens.Add(new ReservationToken
        {
            JobGuid = itemLocalReservationJob,
            ClaimantCreatureGuid = creatureId,
            ReservedCount = 2,
            ExpiresAtTick = 82,
            ReservationType = "haul"
        });

        var carriedCell = new SadRogue.Primitives.Point(2, 1);
        var carriedItemId = terrainWorld.Items.SpawnItem(
            "core_item_log_oak",
            carriedCell,
            z: 0,
            quantity: 1,
            currentTick: 8)
            ?? throw new InvalidOperationException("Test carried item failed to spawn.");
        var carriedItem = terrainWorld.Items.GetInstance(carriedItemId)
            ?? throw new InvalidOperationException("Test carried item could not be read.");
        carriedItem.CarriedBy = creatureId;
        carriedItem.OwnerCreatureGuid = creatureId;

        var equippedCell = new SadRogue.Primitives.Point(2, 1);
        var equippedItemId = terrainWorld.Items.SpawnItem(
            "core_item_log_oak",
            equippedCell,
            z: 0,
            quantity: 1,
            currentTick: 9)
            ?? throw new InvalidOperationException("Test equipped item failed to spawn.");
        var equippedItem = terrainWorld.Items.GetInstance(equippedItemId)
            ?? throw new InvalidOperationException("Test equipped item could not be read.");
        equippedItem.EquippedBy = creatureId;
        equippedItem.OwnerCreatureGuid = creatureId;

        var containedCell = new SadRogue.Primitives.Point(1, 2);
        var containedItemId = terrainWorld.Items.SpawnItem(
            "core_item_log_oak",
            containedCell,
            z: 0,
            quantity: 1,
            currentTick: 10)
            ?? throw new InvalidOperationException("Test contained item failed to spawn.");
        var containedItem = terrainWorld.Items.GetInstance(containedItemId)
            ?? throw new InvalidOperationException("Test contained item could not be read.");
        containedItem.ContainedBy = itemId;

        var installedCell = new SadRogue.Primitives.Point(3, 1);
        var installedItemId = terrainWorld.Items.SpawnItem(
            "core_item_log_oak",
            installedCell,
            z: 0,
            quantity: 1,
            currentTick: 11)
            ?? throw new InvalidOperationException("Test installed item failed to spawn.");
        var installedItem = terrainWorld.Items.GetInstance(installedItemId)
            ?? throw new InvalidOperationException("Test installed item could not be read.");
        installedItem.InstalledAt = new PlacementData
        {
            AnchorWorld = installedCell,
            Z = 0,
            Rotation = 1,
            StateId = "test-installed"
        };

        var itemReservationHolder = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var creatureReservationJob = "job-restore-smoke";
        terrainWorld.Reservations.TryReserveItem(
            itemId,
            itemReservationHolder,
            currentTick: 8,
            expireTick: 80);
        terrainWorld.Reservations.TryReserveCreature(
            creatureId,
            "save.restore.test",
            currentTick: 8,
            expireTick: 81,
            jobId: creatureReservationJob);

        var stockpileId = terrainWorld.Stockpiles.CreateZone(
            "Restore Stockpile",
            new ChunkKey(0, 0, 0),
            currentTick: 9);
        terrainWorld.Stockpiles.UpdateZone(stockpileId, zone =>
        {
            zone.Filter = new StockpileFilter
            {
                Mode = FilterMode.Whitelist,
                Tags = ImmutableHashSet.Create(StringComparer.Ordinal, "wood"),
                ItemIds = ImmutableHashSet.Create(StringComparer.Ordinal, "core_item_log_oak"),
                Materials = ImmutableHashSet.Create(StringComparer.Ordinal, "core_mat_wood_oak")
            };
            zone.Priority = 3;
            zone.TargetStacks = 12;
            zone.HysteresisLow = 4;
            zone.HysteresisHigh = 10;
            zone.UpdateMemberChunks(new[] { new ChunkKey(0, 0, 0) });
        });

        terrainWorld.Orders.EnqueueMiningAdvanced(
            new SadRogue.Primitives.Rectangle(3, 3, 1, 1),
            zMin: 0,
            zMax: 0,
            MiningAction.Dig,
            priority: 11,
            createdTick: 10);
        terrainWorld.Orders.EnqueueHaul(
            new SadRogue.Primitives.Rectangle(4, 4, 1, 1),
            z: 0,
            priority: 12,
            createdTick: 11);
        terrainWorld.Orders.EnqueueConstruction(
            new SadRogue.Primitives.Rectangle(5, 5, 1, 1),
            zMin: 0,
            zMax: 0,
            ConstructionShape.Wall,
            new MaterialFilterSpec
            {
                PreferredMaterialId = "core_mat_stone_granite",
                CategoryKey = "test.wall",
                Tags = new[] { "stone", "construction" }
            },
            priority: 13,
            createdTick: 12);
        terrainWorld.Orders.EnqueueBuildableConstruction(
            "core_placeable_workbench",
            new SadRogue.Primitives.Point(6, 6),
            z: 0,
            priority: 14,
            createdTick: 13);

        var payload = WorldSavePayloadBuilder.Build(terrainWorld);
        var duplicateItemPayload = payload with
        {
            Counts = payload.Counts with { ItemCount = payload.Counts.ItemCount + 1 },
            Items = payload.Items.Concat(new[] { payload.Items[0] }).ToArray()
        };
        var duplicateItemRestore = WorldSavePayloadRestorer.RestoreSupportedSections(duplicateItemPayload);
        var duplicateCreaturePayload = payload with
        {
            Counts = payload.Counts with { CreatureCount = payload.Counts.CreatureCount + 1 },
            Creatures = payload.Creatures.Concat(new[] { payload.Creatures[0] }).ToArray()
        };
        var duplicateCreatureRestore = WorldSavePayloadRestorer.RestoreSupportedSections(duplicateCreaturePayload);
        var duplicateStockpilePayload = payload with
        {
            Counts = payload.Counts with { StockpileZoneCount = payload.Counts.StockpileZoneCount + 1 },
            StockpileZones = payload.StockpileZones.Concat(new[] { payload.StockpileZones[0] }).ToArray()
        };
        var duplicateStockpileRestore = WorldSavePayloadRestorer.RestoreSupportedSections(duplicateStockpilePayload);
        var duplicateMemberChunkStockpiles = payload.StockpileZones.ToArray();
        duplicateMemberChunkStockpiles[0] = duplicateMemberChunkStockpiles[0] with
        {
            MemberChunks = duplicateMemberChunkStockpiles[0].MemberChunks
                .Concat(new[] { duplicateMemberChunkStockpiles[0].MemberChunks[0] })
                .ToArray()
        };
        var duplicateMemberChunkPayload = payload with
        {
            StockpileZones = duplicateMemberChunkStockpiles
        };
        var duplicateMemberChunkRestore = WorldSavePayloadRestorer.RestoreSupportedSections(duplicateMemberChunkPayload);
        var duplicateMiningOrderPayload = payload with
        {
            Counts = payload.Counts with { MiningOrderCount = payload.Counts.MiningOrderCount + 1 },
            MiningOrders = payload.MiningOrders.Concat(new[] { payload.MiningOrders[0] }).ToArray()
        };
        var duplicateMiningOrderRestore = WorldSavePayloadRestorer.RestoreSupportedSections(duplicateMiningOrderPayload);
        var missingCarrierId = Guid.Parse("99999999-1111-2222-3333-444444444444");
        var missingCarrierPayload = payload with
        {
            Items = payload.Items
                .Select(itemPayload => itemPayload.Guid == carriedItemId
                    ? itemPayload with { CarriedBy = missingCarrierId }
                    : itemPayload)
                .ToArray()
        };
        var missingCarrierRestore = WorldSavePayloadRestorer.RestoreSupportedSections(missingCarrierPayload);
        var missingEquippedCreatureId = Guid.Parse("99999999-5555-6666-7777-888888888888");
        var missingEquippedCreaturePayload = payload with
        {
            Items = payload.Items
                .Select(itemPayload => itemPayload.Guid == equippedItemId
                    ? itemPayload with { EquippedBy = missingEquippedCreatureId }
                    : itemPayload)
                .ToArray()
        };
        var missingEquippedCreatureRestore = WorldSavePayloadRestorer.RestoreSupportedSections(missingEquippedCreaturePayload);
        var missingContainerId = Guid.Parse("99999999-9999-8888-7777-666666666666");
        var missingContainerPayload = payload with
        {
            Items = payload.Items
                .Select(itemPayload => itemPayload.Guid == containedItemId
                    ? itemPayload with { ContainedBy = missingContainerId }
                    : itemPayload)
                .ToArray()
        };
        var missingContainerRestore = WorldSavePayloadRestorer.RestoreSupportedSections(missingContainerPayload);
        var containedCyclePayload = payload with
        {
            Items = payload.Items
                .Select(itemPayload => itemPayload.Guid == itemId
                    ? itemPayload with { ContainedBy = containedItemId }
                    : itemPayload)
                .ToArray()
        };
        var containedCycleRestore = WorldSavePayloadRestorer.RestoreSupportedSections(containedCyclePayload);
        var invalidInstalledAnchorPayload = payload with
        {
            Items = payload.Items
                .Select(itemPayload => itemPayload.Guid == installedItemId
                    ? itemPayload with
                    {
                        InstalledAt = new WorldSavePlacementData(
                            new WorldSavePointData(payload.SizeInTiles + 1, 1),
                            0,
                            1,
                            "invalid-anchor")
                    }
                    : itemPayload)
                .ToArray()
        };
        var invalidInstalledAnchorRestore = WorldSavePayloadRestorer.RestoreSupportedSections(invalidInstalledAnchorPayload);
        var invalidInstalledRotationPayload = payload with
        {
            Items = payload.Items
                .Select(itemPayload => itemPayload.Guid == installedItemId
                    ? itemPayload with
                    {
                        InstalledAt = new WorldSavePlacementData(
                            new WorldSavePointData(installedCell.X, installedCell.Y),
                            0,
                            9,
                            "invalid-rotation")
                    }
                    : itemPayload)
                .ToArray()
        };
        var invalidInstalledRotationRestore = WorldSavePayloadRestorer.RestoreSupportedSections(invalidInstalledRotationPayload);
        var missingTokenClaimantId = Guid.Parse("99999999-aaaa-bbbb-cccc-dddddddddddd");
        var missingTokenClaimantPayload = payload with
        {
            Items = payload.Items
                .Select(itemPayload => itemPayload.Guid == itemId
                    ? itemPayload with
                    {
                        ReservationTokens = itemPayload.ReservationTokens
                            .Select(token => token with { ClaimantCreatureGuid = missingTokenClaimantId })
                            .ToArray()
                    }
                    : itemPayload)
                .ToArray()
        };
        var missingTokenClaimantRestore = WorldSavePayloadRestorer.RestoreSupportedSections(missingTokenClaimantPayload);
        var overReservedTokenPayload = payload with
        {
            Items = payload.Items
                .Select(itemPayload => itemPayload.Guid == itemId
                    ? itemPayload with
                    {
                        ReservationTokens = itemPayload.ReservationTokens
                            .Select(token => token with { ReservedCount = itemPayload.StackCount + 1 })
                            .ToArray()
                    }
                    : itemPayload)
                .ToArray()
        };
        var overReservedTokenRestore = WorldSavePayloadRestorer.RestoreSupportedSections(overReservedTokenPayload);
        var duplicateTokenPayload = payload with
        {
            Items = payload.Items
                .Select(itemPayload => itemPayload.Guid == itemId
                    ? itemPayload with
                    {
                        ReservationTokens = itemPayload.ReservationTokens
                            .Concat(itemPayload.ReservationTokens)
                            .ToArray()
                    }
                    : itemPayload)
                .ToArray()
        };
        var duplicateTokenRestore = WorldSavePayloadRestorer.RestoreSupportedSections(duplicateTokenPayload);
        var restore = WorldSavePayloadRestorer.RestoreSupportedSections(payload);
        var restoredHash = restore.World == null
            ? string.Empty
            : WorldReplayHashBuilder.Build(restore.World);
        var restoredItem = restore.World?.Items.GetInstance(itemId);
        var restoredCarriedItem = restore.World?.Items.GetInstance(carriedItemId);
        var restoredEquippedItem = restore.World?.Items.GetInstance(equippedItemId);
        var restoredContainedItem = restore.World?.Items.GetInstance(containedItemId);
        var restoredInstalledItem = restore.World?.Items.GetInstance(installedItemId);
        var restoredGroundItemsAtCarriedCell = restore.World?.Items.GetGroundItemsAt(carriedCell, 0).ToArray()
            ?? Array.Empty<ItemInstance>();
        var restoredAllItemsAtCarriedCell = restore.World?.Items.GetItemsAt(carriedCell, 0, groundOnly: false).ToArray()
            ?? Array.Empty<ItemInstance>();
        var restoredGroundItemsAtContainedCell = restore.World?.Items.GetGroundItemsAt(containedCell, 0).ToArray()
            ?? Array.Empty<ItemInstance>();
        var restoredAllItemsAtContainedCell = restore.World?.Items.GetItemsAt(containedCell, 0, groundOnly: false).ToArray()
            ?? Array.Empty<ItemInstance>();
        var restoredGroundItemsAtInstalledCell = restore.World?.Items.GetGroundItemsAt(installedCell, 0).ToArray()
            ?? Array.Empty<ItemInstance>();
        var restoredAllItemsAtInstalledCell = restore.World?.Items.GetItemsAt(installedCell, 0, groundOnly: false).ToArray()
            ?? Array.Empty<ItemInstance>();
        var restoredCreature = restore.World?.Creatures.GetInstance(creatureId);
        var restoredItemReservations = restore.World?.Reservations.GetItemReservationsSnapshot() ?? Array.Empty<(Guid itemId, Guid holderId, ulong expireTick)>();
        var restoredCreatureReservations = restore.World?.Reservations.GetCreatureReservationsSnapshot() ?? Array.Empty<(Guid workerId, string holderSystem, string? jobId, ulong expireTick)>();
        var restoredStockpile = restore.World?.Stockpiles.GetZone(stockpileId);
        var placeableWorld = CreateReplayHashWorld();
        var placeablePayload = WorldSavePayloadBuilder.Build(placeableWorld);
        var placeableSectionRestore = WorldSavePayloadRestorer.RestoreSupportedSections(placeablePayload);
        var duplicatePlaceablePayload = placeablePayload with
        {
            Counts = placeablePayload.Counts with { OwnedPlaceableCount = placeablePayload.Counts.OwnedPlaceableCount + 1 },
            Placeables = placeablePayload.Placeables.Concat(new[] { placeablePayload.Placeables[0] }).ToArray()
        };
        var duplicatePlaceableRestore = WorldSavePayloadRestorer.RestoreSupportedSections(duplicatePlaceablePayload);
        var restoredWorkshop = placeableSectionRestore.World == null
            ? null
            : GetReplayHashWorkshop(placeableSectionRestore.World);
        var restoredWorkshopEntry = restoredWorkshop?.Workshop?.Queue.SingleOrDefault();

        RegressionAssert.True(
            placeableSectionRestore.Success,
            "World save payload placeable/workshop restore failed: "
            + string.Join("; ", placeableSectionRestore.Issues));
        RegressionAssert.True(
            placeableSectionRestore.RestoredWorldHash == placeablePayload.ReplayHash,
            $"World save payload placeable/workshop restore hash mismatch: saved={placeablePayload.ReplayHash} restored={placeableSectionRestore.RestoredWorldHash}.");

        var worldSavePayloadFailureHints = new List<string>();
        void AddWorldSavePayloadHint(bool condition, string label)
        {
            if (!condition)
                worldSavePayloadFailureHints.Add(label);
        }

        AddWorldSavePayloadHint(payload.Placeables.Length == 0, $"payload.Placeables.Length={payload.Placeables.Length}");
        AddWorldSavePayloadHint(restore.Success, $"restore.Success={restore.Success}: {string.Join("; ", restore.Issues)}");
        AddWorldSavePayloadHint(restore.RestoredWorldHash == payload.ReplayHash, $"restore hash saved={payload.ReplayHash} restored={restore.RestoredWorldHash}");
        AddWorldSavePayloadHint(duplicatePlaceableRestore.Success == false, $"duplicatePlaceableRestore.Success={duplicatePlaceableRestore.Success}: {string.Join("; ", duplicatePlaceableRestore.Issues)}");
        AddWorldSavePayloadHint(placeablePayload.Placeables.Length == 2, $"placeablePayload.Placeables.Length={placeablePayload.Placeables.Length}");
        AddWorldSavePayloadHint(placeablePayload.Counts.OwnedPlaceableCount == 2, $"placeablePayload.Counts.OwnedPlaceableCount={placeablePayload.Counts.OwnedPlaceableCount}");
        AddWorldSavePayloadHint(restoredWorkshop?.Workshop != null, "restoredWorkshop.Workshop is null");
        AddWorldSavePayloadHint(restoredWorkshop?.Workshop?.AllowedWorkers == 2, $"restoredWorkshop.AllowedWorkers={restoredWorkshop?.Workshop?.AllowedWorkers}");
        AddWorldSavePayloadHint(restoredWorkshop?.Workshop?.MaxWorkers == 3, $"restoredWorkshop.MaxWorkers={restoredWorkshop?.Workshop?.MaxWorkers}");
        AddWorldSavePayloadHint(restoredWorkshop?.Workshop?.NextEntrySequence == 1, $"restoredWorkshop.NextEntrySequence={restoredWorkshop?.Workshop?.NextEntrySequence}");
        AddWorldSavePayloadHint(restoredWorkshopEntry != null, "restoredWorkshopEntry is null");
        AddWorldSavePayloadHint(restoredWorkshopEntry?.Status == CraftQueueStatus.AwaitingMaterials, $"restoredWorkshopEntry.Status={restoredWorkshopEntry?.Status}");
        AddWorldSavePayloadHint(restoredWorkshopEntry?.HasPendingRequests == true, $"restoredWorkshopEntry.HasPendingRequests={restoredWorkshopEntry?.HasPendingRequests}");
        AddWorldSavePayloadHint(restoredWorkshopEntry?.LastRequestTick == 12, $"restoredWorkshopEntry.LastRequestTick={restoredWorkshopEntry?.LastRequestTick}");
        AddWorldSavePayloadHint(restoredWorkshopEntry?.ActiveWorkerId == Guid.Parse("bbbbbbbb-5555-5555-5555-bbbbbbbbbbbb"), $"restoredWorkshopEntry.ActiveWorkerId={restoredWorkshopEntry?.ActiveWorkerId}");
        AddWorldSavePayloadHint(restoredWorkshopEntry?.IsScheduled == true, $"restoredWorkshopEntry.IsScheduled={restoredWorkshopEntry?.IsScheduled}");
        AddWorldSavePayloadHint(restoredWorkshopEntry?.BlockingReason == "waiting_for_material", $"restoredWorkshopEntry.BlockingReason={restoredWorkshopEntry?.BlockingReason}");

        RegressionAssert.True(
            terrainOnlyPayload.SchemaVersion == WorldSavePayloadFormat.CurrentVersion
            && terrainOnlyPayload.Chunks.Length == 2
            && terrainOnlyPayload.Items.Length == 0
            && terrainOnlyPayload.Placeables.Length == 0
            && terrainOnlyPayload.Counts.ChunkCount == 2
            && terrainOnlyPayload.Counts.TileCount == 2 * Chunk.CELLS_PER_LAYER
            && terrainOnlyPayload.ReplayHash == terrainOnlyHash
            && terrainOnlyRestore.Success
            && terrainOnlyRestore.RestoredWorldHash == terrainOnlyPayload.ReplayHash
            && payload.SchemaVersion == WorldSavePayloadFormat.CurrentVersion
            && payload.Chunks.Length == 2
            && payload.Items.Length == 5
            && payload.Counts.ItemCount == 5
            && payload.Items.Any(itemPayload => itemPayload.Guid == carriedItemId && itemPayload.CarriedBy == creatureId)
            && payload.Items.Any(itemPayload => itemPayload.Guid == equippedItemId && itemPayload.EquippedBy == creatureId)
            && payload.Items.Any(itemPayload => itemPayload.Guid == containedItemId && itemPayload.ContainedBy == itemId)
            && payload.Items.Any(itemPayload => itemPayload.Guid == installedItemId && itemPayload.InstalledAt.HasValue)
            && payload.Items.Any(itemPayload => itemPayload.Guid == itemId
                && itemPayload.ReservationTokens.Length == 1
                && itemPayload.ReservationTokens[0].ClaimantCreatureGuid == creatureId)
            && payload.Creatures.Length == 1
            && payload.Counts.CreatureCount == 1
            && payload.ItemReservations.Length == 1
            && payload.CreatureReservations.Length == 1
            && payload.StockpileZones.Length == 1
            && payload.Placeables.Length == 0
            && payload.MiningOrders.Length == 1
            && payload.HaulOrders.Length == 1
            && payload.ConstructionOrders.Length == 1
            && payload.BuildableOrders.Length == 1
            && payload.Counts.ChunkCount == 2
            && payload.Counts.TileCount == 2 * Chunk.CELLS_PER_LAYER
            && payload.ReplayHash == WorldReplayHashBuilder.Build(terrainWorld)
            && !duplicateItemRestore.Success
            && duplicateItemRestore.World == null
            && duplicateItemRestore.Issues.Any(issue => issue.Contains("duplicates guid", StringComparison.Ordinal))
            && !duplicateCreatureRestore.Success
            && duplicateCreatureRestore.World == null
            && duplicateCreatureRestore.Issues.Any(issue => issue.Contains("duplicates guid", StringComparison.Ordinal))
            && !duplicateStockpileRestore.Success
            && duplicateStockpileRestore.World == null
            && duplicateStockpileRestore.Issues.Any(issue => issue.Contains("duplicates zone id", StringComparison.Ordinal))
            && !duplicateMemberChunkRestore.Success
            && duplicateMemberChunkRestore.World == null
            && duplicateMemberChunkRestore.Issues.Any(issue => issue.Contains("member chunk", StringComparison.Ordinal) && issue.Contains("duplicates chunk", StringComparison.Ordinal))
            && !duplicateMiningOrderRestore.Success
            && duplicateMiningOrderRestore.World == null
            && duplicateMiningOrderRestore.Issues.Any(issue => issue.Contains("duplicates mining id", StringComparison.Ordinal))
            && !missingCarrierRestore.Success
            && missingCarrierRestore.World == null
            && missingCarrierRestore.Issues.Any(issue => issue.Contains("references missing carrier creature", StringComparison.Ordinal))
            && !missingEquippedCreatureRestore.Success
            && missingEquippedCreatureRestore.World == null
            && missingEquippedCreatureRestore.Issues.Any(issue => issue.Contains("references missing equipped creature", StringComparison.Ordinal))
            && !missingContainerRestore.Success
            && missingContainerRestore.World == null
            && missingContainerRestore.Issues.Any(issue => issue.Contains("references missing containing item", StringComparison.Ordinal))
            && !containedCycleRestore.Success
            && containedCycleRestore.World == null
            && containedCycleRestore.Issues.Any(issue => issue.Contains("contained-item cycle", StringComparison.Ordinal))
            && !invalidInstalledAnchorRestore.Success
            && invalidInstalledAnchorRestore.World == null
            && invalidInstalledAnchorRestore.Issues.Any(issue => issue.Contains("installed anchor", StringComparison.Ordinal) && issue.Contains("outside world bounds", StringComparison.Ordinal))
            && !invalidInstalledRotationRestore.Success
            && invalidInstalledRotationRestore.World == null
            && invalidInstalledRotationRestore.Issues.Any(issue => issue.Contains("installed rotation", StringComparison.Ordinal))
            && !missingTokenClaimantRestore.Success
            && missingTokenClaimantRestore.World == null
            && missingTokenClaimantRestore.Issues.Any(issue => issue.Contains("references missing claimant creature", StringComparison.Ordinal))
            && !overReservedTokenRestore.Success
            && overReservedTokenRestore.World == null
            && overReservedTokenRestore.Issues.Any(issue => issue.Contains("reservation tokens reserve", StringComparison.Ordinal))
            && !duplicateTokenRestore.Success
            && duplicateTokenRestore.World == null
            && duplicateTokenRestore.Issues.Any(issue => issue.Contains("duplicates reservation token identity", StringComparison.Ordinal))
            && restore.Success
            && restore.SavedWorldHash == payload.ReplayHash
            && restore.RestoredWorldHash == payload.ReplayHash
            && restoredHash == payload.ReplayHash
            && restore.RestoredChunkCount == payload.Counts.ChunkCount
            && restore.RestoredTileCount == payload.Counts.TileCount
            && restoredItem != null
            && restoredItem.Guid == itemId
            && restoredItem.StackCount == item.StackCount
            && restoredItem.OwnerFactionId == item.OwnerFactionId
            && restoredItem.UsePolicy == item.UsePolicy
            && restoredItem.ReservationTokens.Count == 1
            && restoredItem.ReservationTokens[0].JobGuid == itemLocalReservationJob
            && restoredItem.ReservationTokens[0].ClaimantCreatureGuid == creatureId
            && restoredItem.ReservationTokens[0].ReservedCount == 2
            && restoredItem.ReservationTokens[0].ExpiresAtTick == 82
            && restoredItem.ReservationTokens[0].ReservationType == "haul"
            && restoredItem.Improvements?.Count == 1
            && restoredItem.Perishable?.CreatedAtTick == 6
            && restoredCarriedItem != null
            && restoredCarriedItem.Guid == carriedItemId
            && restoredCarriedItem.CarriedBy == creatureId
            && restoredCarriedItem.OwnerCreatureGuid == creatureId
            && !restoredGroundItemsAtCarriedCell.Any(itemAtCell => itemAtCell.Guid == carriedItemId)
            && restoredAllItemsAtCarriedCell.Any(itemAtCell => itemAtCell.Guid == carriedItemId)
            && restoredEquippedItem != null
            && restoredEquippedItem.Guid == equippedItemId
            && restoredEquippedItem.EquippedBy == creatureId
            && restoredEquippedItem.OwnerCreatureGuid == creatureId
            && !restoredGroundItemsAtCarriedCell.Any(itemAtCell => itemAtCell.Guid == equippedItemId)
            && restoredAllItemsAtCarriedCell.Any(itemAtCell => itemAtCell.Guid == equippedItemId)
            && restoredContainedItem != null
            && restoredContainedItem.Guid == containedItemId
            && restoredContainedItem.ContainedBy == itemId
            && !restoredGroundItemsAtContainedCell.Any(itemAtCell => itemAtCell.Guid == containedItemId)
            && restoredAllItemsAtContainedCell.Any(itemAtCell => itemAtCell.Guid == containedItemId)
            && restoredInstalledItem != null
            && restoredInstalledItem.Guid == installedItemId
            && restoredInstalledItem.InstalledAt != null
            && restoredInstalledItem.InstalledAt.AnchorWorld == installedCell
            && restoredInstalledItem.InstalledAt.Z == 0
            && restoredInstalledItem.InstalledAt.Rotation == 1
            && restoredInstalledItem.InstalledAt.StateId == "test-installed"
            && !restoredGroundItemsAtInstalledCell.Any(itemAtCell => itemAtCell.Guid == installedItemId)
            && restoredAllItemsAtInstalledCell.Any(itemAtCell => itemAtCell.Guid == installedItemId)
            && restoredCreature != null
            && restoredCreature.Guid == creatureId
            && restoredCreature.HP == 77
            && restoredCreature.MaxHP == 120
            && restoredItemReservations.Any(reservation => reservation.itemId == itemId && reservation.holderId == itemReservationHolder && reservation.expireTick == 80)
            && restoredCreatureReservations.Any(reservation => reservation.workerId == creatureId && reservation.holderSystem == "save.restore.test" && reservation.jobId == creatureReservationJob && reservation.expireTick == 81)
            && restoredStockpile != null
            && restoredStockpile.Filter.ItemIds.Contains("core_item_log_oak")
            && restore.World!.Orders.GetActiveMiningSnapshot().Count == 1
            && restore.World.Orders.GetActiveHaulsSnapshot().Count == 1
            && restore.World.Orders.GetActiveConstructionSnapshot().Count == 1
            && restore.World.Orders.GetActiveBuildableSnapshot().Count == 1
            && !duplicatePlaceableRestore.Success
            && duplicatePlaceableRestore.World == null
            && duplicatePlaceableRestore.Issues.Any(issue => issue.Contains("duplicates placeable", StringComparison.Ordinal))
            && placeablePayload.Placeables.Length == 2
            && placeablePayload.Counts.OwnedPlaceableCount == 2
            && restoredWorkshop?.Workshop != null
            && restoredWorkshop.Workshop.AllowedWorkers == 2
            && restoredWorkshop.Workshop.MaxWorkers == 3
            && restoredWorkshop.Workshop.NextEntrySequence == 1
            && restoredWorkshopEntry != null
            && restoredWorkshopEntry.Status == CraftQueueStatus.AwaitingMaterials
            && restoredWorkshopEntry.HasPendingRequests
            && restoredWorkshopEntry.LastRequestTick == 12
            && restoredWorkshopEntry.ActiveWorkerId == Guid.Parse("bbbbbbbb-5555-5555-5555-bbbbbbbbbbbb")
            && restoredWorkshopEntry.IsScheduled
            && restoredWorkshopEntry.BlockingReason == "waiting_for_material",
            "World save payload did not restore supported world sections and placeable/workshop authority by hash. Hints: "
            + string.Join(" | ", worldSavePayloadFailureHints));

        Console.WriteLine("[PASS] World save payload restorer");
    }

    private static ICommand[] BuildReplayOrderCommands(bool wrapWithRuntimeIdentity)
    {
        var rect = new SadRogue.Primitives.Rectangle(1, 1, 2, 2);
        var buildAnchor = new SadRogue.Primitives.Point(4, 4);
        var filter = new MaterialFilterSpec
        {
            CategoryKey = "test.floor",
            Tags = new[] { "construction", "stone" }
        };
        ICommand[] commands =
        {
            new CreateMiningOrderCommand(tick: 0, rect, z: 2, priority: 11),
            new CreateAdvancedMiningOrderCommand(tick: 0, rect, zMin: 1, zMax: 3, action: MiningAction.Dig, priority: 12),
            new CreateHaulOrderCommand(tick: 0, rect, z: 2, priority: 13),
            new CreateConstructionOrderCommand(tick: 0, rect, zMin: 2, zMax: 2, shape: ConstructionShape.Floor, filter: filter, priority: 14),
            new CreateBuildableConstructionOrderCommand(tick: 0, "core_workshop_carpenter", buildAnchor, z: 2, priority: 15)
        };

        if (!wrapWithRuntimeIdentity)
            return commands;

        return commands
            .Select((command, index) => (ICommand)new RuntimeIdentifiedCommand(command, sequence: index + 1))
            .ToArray();
    }

    private static string ExecuteOrderCommandsAndHash(IReadOnlyList<ICommand> commands)
    {
        var services = new RuntimeSessionServices();
        foreach (var command in commands)
        {
            services.CommandQueue.Enqueue(command);
        }

        return ExecuteExistingOrderQueueAndHash(services);
    }

    private static string ExecuteReplayRecordsAndHash(
        RuntimeSessionServices services,
        IReadOnlyList<CommandReplayRecord> records,
        out RuntimeCommandReplayRestoreResult restoreResult)
    {
        restoreResult = new RuntimeCommandReplayRestorer().RestorePending(services, records);
        return ExecuteExistingOrderQueueAndHash(services);
    }

    private static string ExecuteExistingOrderQueueAndHash(RuntimeSessionServices services)
    {
        var world = ExecuteExistingOrderQueue(services);
        return BuildWorldReplayHash(world);
    }

    private static World ExecuteExistingOrderQueue(RuntimeSessionServices services)
    {
        var world = new World(2, 10);
        var context = CreateRuntimeContext(
            services.DiffLog,
            services.MutationDiffs,
            world,
            services.EventBus);
        var pipeline = new SimulationTickPipeline(
            world,
            services.CommandQueue,
            context,
            context,
            services.DiffLog,
            services.MutationDiffs,
            navigation: null);

        pipeline.AttachTo(services.TickScheduler);
        services.TickScheduler.ExecuteSingleTick();
        pipeline.DetachFrom(services.TickScheduler);

        return world;
    }

    private static string BuildWorldReplayHash(World world)
    {
        return WorldReplayHashBuilder.Build(world);
    }

    private static void TestFullRuntimeSimulationLoopDeterminism()
    {
        const int tickCount = 500;
        const int sampleInterval = 50;

        var first = CreateManualRuntimeDeterminismRun();
        var second = CreateManualRuntimeDeterminismRun();
        try
        {
            RuntimeDeterminismSample[] firstSamples = RunManualRuntimeDeterminismLoop(
                first,
                tickCount,
                sampleInterval);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            RuntimeDeterminismSample[] secondSamples = RunManualRuntimeDeterminismLoop(
                second,
                tickCount,
                sampleInterval);

            RegressionAssert.True(
                firstSamples.Length == tickCount / sampleInterval
                && secondSamples.Length == firstSamples.Length
                && firstSamples[^1].Tick == (ulong)tickCount
                && firstSamples.SequenceEqual(secondSamples)
                && firstSamples.All(static sample =>
                    !string.IsNullOrWhiteSpace(sample.WorldHash)
                    && !string.IsNullOrWhiteSpace(sample.CheckpointHash)
                    && !string.IsNullOrWhiteSpace(sample.RngHash)
                    && !string.IsNullOrWhiteSpace(sample.CommandLogHash)
                    && !string.IsNullOrWhiteSpace(sample.PendingCommandLogHash)
                    && sample.CommandLogRecordCount == 1
                    && sample.PendingCommandLogRecordCount == 0
                    && !string.IsNullOrWhiteSpace(sample.TransportHash)
                    && !string.IsNullOrWhiteSpace(sample.MiningHash)
                    && !string.IsNullOrWhiteSpace(sample.CraftHash)),
                "Full Runtime simulation loop did not produce identical world/checkpoint hashes across equivalent sessions. "
                + $"first={FormatRuntimeDeterminismSample(firstSamples)} second={FormatRuntimeDeterminismSample(secondSamples)}");
        }
        finally
        {
            first.Session.Host.Stop();
            second.Session.Host.Stop();
        }

        Console.WriteLine("[PASS] Full Runtime simulation loop determinism");
    }

    private static RuntimeDeterminismSample[] RunManualRuntimeDeterminismLoop(
        RuntimeDeterminismRun run,
        int tickCount,
        int sampleInterval)
    {
        var samples = new List<RuntimeDeterminismSample>();
        for (var i = 0; i < tickCount; i++)
        {
            run.Services.TickScheduler.ExecuteSingleTick();
            if ((i + 1) % sampleInterval == 0)
            {
                samples.Add(CaptureRuntimeDeterminismSample(run));
            }
        }

        return samples.ToArray();
    }

    private static RuntimeDeterminismSample CaptureRuntimeDeterminismSample(RuntimeDeterminismRun run)
    {
        var fortressSession = new FortressRuntimeSession(run.Session);
        var checkpoint = RuntimeReplayCheckpointHashBuilder.BuildData(run.Services, fortressSession);
        var worldHash = WorldReplayHashBuilder.Build(run.Session.World);

        RegressionAssert.True(
            checkpoint.WorldHash == worldHash
            && checkpoint.Metadata.RuntimeTick == run.Services.TickScheduler.CurrentTick,
            "Runtime checkpoint did not match the authoritative world hash or scheduler tick during determinism sampling.");

        return new RuntimeDeterminismSample(
            checkpoint.Metadata.RuntimeTick,
            worldHash,
            checkpoint.AggregateHash,
            checkpoint.RngHash,
            checkpoint.RngStreamCount,
            checkpoint.CommandLogHash,
            checkpoint.CommandLogRecordCount,
            checkpoint.PendingCommandLogHash,
            checkpoint.PendingCommandLogRecordCount,
            checkpoint.TransportHash ?? string.Empty,
            checkpoint.MiningHash ?? string.Empty,
            checkpoint.CraftHash ?? string.Empty);
    }

    private static RuntimeDeterminismRun CreateManualRuntimeDeterminismRun()
    {
        var services = new RuntimeSessionServices();
        FortressRuntimeContentSnapshot? content = null;
        var factory = new SimulationRuntimeSessionFactory<SimulationRuntimeHost<SimulationRuntimeSystems>>(
            services,
            world =>
            {
                content = SimulationWorldContentLoader.LoadCoreContent(
                    world,
                    AppContext.BaseDirectory,
                    strictContent: false,
                    treatWarningsAsErrors: false);
                FillDeterminismSmokeTerrain(world);
            },
            (world, navigation) =>
            {
                return FortressRuntimeHostFactory.Create(
                    world,
                    services,
                    navigation,
                    AppContext.BaseDirectory,
                    content,
                    FortressRuntimeLogging.None);
            });

        var session = factory.CreateNew(sizeInChunks: 2, maxZ: 3);
        session.Navigation.RebuildAll();
        session.Host.AttachForManualTicks(systems =>
        {
            var spawned = SimulationInitialWorkerSpawner.SpawnIfNeeded(session.World, desired: 2);
            systems.ProfessionAssignments.Initialize(session.World.Creatures.GetAllInstances());
            RuntimeAutoDigSeeder.EnqueueIfPossible(
                session.World,
                services.CommandQueue,
                services.TickScheduler.CurrentTick);

            RegressionAssert.True(
                spawned == 2
                && session.World.Creatures.InstanceCount == 2
                && services.CommandQueue.GetPendingCommandRecords().Count == 1,
                "Full Runtime determinism setup did not seed workers and one startup auto-dig command.");
        });

        return new RuntimeDeterminismRun(services, session);
    }

    private static void FillDeterminismSmokeTerrain(World world)
    {
        var z = Math.Min(1, world.MaxZ - 1);
        var floor = new TileBase(
            geoMatId: 0,
            terrainBits: (ushort)TerrainKind.OpenWithFloor,
            surfaceBits: 0,
            fluidKind: 0,
            fluidDepth: 0,
            metaBits: 0,
            trafficCost: 1);
        var wall = new TileBase(
            geoMatId: 0,
            terrainBits: (ushort)TerrainKind.SolidWall,
            surfaceBits: 0,
            fluidKind: 0,
            fluidDepth: 0,
            metaBits: 0,
            trafficCost: 1);

        for (var layer = 0; layer < world.MaxZ; layer++)
        {
            for (var y = 0; y < world.SizeInTiles; y++)
            {
                for (var x = 0; x < world.SizeInTiles; x++)
                {
                    world.SetTile(x, y, layer, floor, tick: 0);
                }
            }
        }

        var targetX = Math.Min(world.SizeInTiles - 2, world.SizeInTiles / 2 + 4);
        var targetY = world.SizeInTiles / 2;
        world.SetTile(targetX, targetY, z, wall, tick: 0);
    }

    private static string FormatRuntimeDeterminismSample(IReadOnlyList<RuntimeDeterminismSample> samples)
    {
        return string.Join(
            " | ",
            samples.Select(static sample =>
                $"{sample.Tick}:{sample.WorldHash}/{sample.CheckpointHash}/rng={sample.RngHash}:{sample.RngStreamCount}/cmd={sample.CommandLogHash}:{sample.CommandLogRecordCount}/pending={sample.PendingCommandLogHash}:{sample.PendingCommandLogRecordCount}/jobs={sample.TransportHash},{sample.MiningHash},{sample.CraftHash}"));
    }

    private sealed record RuntimeDeterminismRun(
        RuntimeSessionServices Services,
        SimulationRuntimeSession<SimulationRuntimeHost<SimulationRuntimeSystems>> Session);

    private readonly record struct RuntimeDeterminismSample(
        ulong Tick,
        string WorldHash,
        string CheckpointHash,
        string RngHash,
        int RngStreamCount,
        string CommandLogHash,
        int CommandLogRecordCount,
        string PendingCommandLogHash,
        int PendingCommandLogRecordCount,
        string TransportHash,
        string MiningHash,
        string CraftHash);

    private static World CreateReplayHashWorld()
    {
        var world = new World(2, 2);
        DefinitionCatalogTestSupport.LoadItems(world);
        DefinitionCatalogTestSupport.LoadCreatures(world);

        var itemCell = new SadRogue.Primitives.Point(1, 1);
        var creatureCell = new SadRogue.Primitives.Point(2, 2);
        world.SetTile(itemCell.X, itemCell.Y, 0, new TileBase(0, (ushort)TerrainKind.OpenWithFloor, 0, 0, 0, 0, 1), 0);
        world.SetTile(creatureCell.X, creatureCell.Y, 0, new TileBase(0, (ushort)TerrainKind.OpenWithFloor, 0, 0, 0, 0, 1), 0);

        var itemId = world.Items.SpawnItem("core_item_log_oak", itemCell, 0, quantity: 3, currentTick: 5)
            ?? throw new InvalidOperationException("Test item failed to spawn.");
        var creatureId = world.Creatures.SpawnCreature("core_race_dwarf", creatureCell, 0, "player", currentTick: 6)
            ?? throw new InvalidOperationException("Test creature failed to spawn.");

        world.Reservations.TryReserveItem(
            itemId,
            Guid.Parse("11111111-2222-3333-4444-555555555555"),
            currentTick: 7,
            expireTick: 20);
        world.Reservations.TryReserveCreature(
            creatureId,
            "test.system",
            currentTick: 7,
            expireTick: 21,
            jobId: "job-1");

        world.Orders.EnqueueHaul(new SadRogue.Primitives.Rectangle(1, 1, 2, 2), z: 0, priority: 13, createdTick: 8);

        var stockpileId = world.Stockpiles.CreateZone("Replay Stockpile", new ChunkKey(0, 0, 0), currentTick: 9);
        world.Stockpiles.UpdateZone(stockpileId, zone =>
        {
            zone.Filter = new StockpileFilter
            {
                Mode = FilterMode.Whitelist,
                Tags = ImmutableHashSet.Create(StringComparer.Ordinal, "wood"),
                ItemIds = ImmutableHashSet.Create(StringComparer.Ordinal, "core_item_log_oak"),
                Materials = ImmutableHashSet.Create(StringComparer.Ordinal, "core_mat_wood_oak")
            };
            zone.Priority = 2;
            zone.TargetStacks = 16;
            zone.HysteresisLow = 4;
            zone.HysteresisHigh = 12;
            zone.UpdateMemberChunks(new[] { new ChunkKey(0, 0, 0) });
        });

        AddReplayHashPlaceables(world);

        return world;
    }

    private static void AddReplayHashPlaceables(World world)
    {
        var constructionPosition = new SadRogue.Primitives.Point(4, 4);
        var workshopPosition = new SadRogue.Primitives.Point(8, 8);
        world.SetTile(constructionPosition.X, constructionPosition.Y, 0, new TileBase(0, (ushort)TerrainKind.OpenWithFloor, 0, 0, 0, 0, 1), 10);
        world.SetTile(workshopPosition.X, workshopPosition.Y, 0, new TileBase(0, (ushort)TerrainKind.OpenWithFloor, 0, 0, 0, 0, 1), 10);

        var constructionSite = PlaceableFactory.CreateConstructionSite(
            constructionPosition,
            z: 0,
            tickSeed: 10,
            targetId: "test_construction_wall",
            fp: new Footprint(2, 1, 1),
            materialsRequired: new Dictionary<string, int>
            {
                ["stone"] = 2,
                ["wood"] = 1
            },
            totalBuildTicks: 30);
        constructionSite.ConstructionSite!.MaterialsDelivered["stone"] = 1;
        constructionSite.ConstructionSite.BuildProgressTicks = 7;
        PlaceableManager.PlacePlaceable(world, constructionSite, tick: 10);

        var workshop = new PlaceableInstance(
            Guid.Parse("aaaaaaaa-5555-5555-5555-aaaaaaaaaaaa"),
            PlaceableKind.Construction,
            "test_workshop",
            workshopPosition,
            z: 0,
            footprint: new Footprint(1, 1, 1))
        {
            HitPoints = 10,
            MaxHitPoints = 12,
            Workshop = new WorkshopState()
        };
        workshop.Workshop.ConfigureWorkers(defaultAllowed: 2, maxWorkers: 3);
        var queueEntry = workshop.Workshop.AddEntry("test_recipe_plank", "Replay Plank", workshop.Guid, currentTick: 11);
        queueEntry.Status = CraftQueueStatus.AwaitingMaterials;
        queueEntry.HasPendingRequests = true;
        queueEntry.LastRequestTick = 12;
        queueEntry.ActiveWorkerId = Guid.Parse("bbbbbbbb-5555-5555-5555-bbbbbbbbbbbb");
        queueEntry.IsScheduled = true;
        queueEntry.BlockingReason = "waiting_for_material";
        PlaceableManager.PlacePlaceable(world, workshop, tick: 11);
    }

    private static PlaceableInstance GetReplayHashWorkshop(World world)
    {
        return world.GetAllChunks()
            .SelectMany(chunk => chunk.GetPlaceableData()?.GetAllOwnedPlaceables() ?? Enumerable.Empty<PlaceableInstance>())
            .Single(placeable => placeable.Workshop != null);
    }

    private static void TestMiningReplayHashBuilder()
    {
        var snapshot = CreateMiningReplaySnapshot(progressTicks: 3);
        var sameSnapshot = CreateMiningReplaySnapshot(progressTicks: 3);
        var changedSnapshot = CreateMiningReplaySnapshot(progressTicks: 4);

        var hash = MiningReplayHashBuilder.Build(snapshot);
        var sameHash = MiningReplayHashBuilder.Build(sameSnapshot);
        var secondReadHash = MiningReplayHashBuilder.Build(snapshot);
        var changedHash = MiningReplayHashBuilder.Build(changedSnapshot);

        RegressionAssert.True(
            hash == sameHash
            && hash == secondReadHash
            && hash != changedHash,
            "MiningReplayHashBuilder did not produce stable hashes for authoritative mining job state.");

        Console.WriteLine("[PASS] Mining replay hash builder");
    }

    private static MiningJobReplaySnapshot CreateMiningReplaySnapshot(int progressTicks)
    {
        var active = new[]
        {
            new MiningActiveJobStateSnapshot(
                Order: 0,
                WorkerId: Guid.Parse("aaaaaaaa-7777-7777-7777-aaaaaaaaaaaa"),
                Target: new SadRogue.Primitives.Point(5, 6),
                Z: 1,
                Adjacent: new SadRogue.Primitives.Point(5, 5),
                Stage: MiningStage.Digging,
                ProgressTicks: progressTicks,
                RequiredTicks: 12,
                GeologyHandle: 33,
                TerrainKind: TerrainKind.SolidWall,
                Priority: 4,
                AssignedTick: 44,
                ReplanFailCount: 2,
                Action: MiningAction.DigChannel,
                Segment: MiningSegment.Middle,
                DesignationId: 77)
        };

        var backlog = new[]
        {
            new MiningBacklogEntrySnapshot(
                Order: 0,
                Dig: new MiningSystem.PlannedDig(
                    new SadRogue.Primitives.Point(7, 8),
                    Z: 1,
                    GeologyHandle: 34,
                    TerrainKind: (byte)TerrainKind.SolidWall,
                    Priority: 5,
                    Seed: 1234,
                    Action: MiningAction.Dig,
                    Segment: MiningSegment.None,
                    DesignationId: 78),
                EnqueuedTick: 55)
        };

        var deferred = new[]
        {
            new MiningDeferredStairwellSnapshot(
                Order: 0,
                Dig: new MiningSystem.PlannedDig(
                    new SadRogue.Primitives.Point(9, 10),
                    Z: 2,
                    GeologyHandle: 35,
                    TerrainKind: (byte)TerrainKind.SolidWall,
                    Priority: 6,
                    Seed: 5678,
                    Action: MiningAction.DigStairwell,
                    Segment: MiningSegment.Top,
                    DesignationId: 79))
        };

        var reserved = new[]
        {
            new MiningReservedTileSnapshot(5, 6, 1),
            new MiningReservedTileSnapshot(5, 6, 0)
        };

        var completions = new[]
        {
            new MiningRecentCompletionSnapshot(
                Order: 0,
                Cell: new SadRogue.Primitives.Point(3, 4),
                Z: 1,
                ExpireTick: 99)
        };

        return new MiningJobReplaySnapshot(active, backlog, deferred, reserved, completions);
    }

    private static void TestCraftReplayHashBuilder()
    {
        var snapshot = CreateCraftReplaySnapshot(workTicksRemaining: 9);
        var sameSnapshot = CreateCraftReplaySnapshot(workTicksRemaining: 9);
        var changedSnapshot = CreateCraftReplaySnapshot(workTicksRemaining: 8);

        var hash = CraftReplayHashBuilder.Build(snapshot);
        var sameHash = CraftReplayHashBuilder.Build(sameSnapshot);
        var secondReadHash = CraftReplayHashBuilder.Build(snapshot);
        var changedHash = CraftReplayHashBuilder.Build(changedSnapshot);

        RegressionAssert.True(
            hash == sameHash
            && hash == secondReadHash
            && hash != changedHash,
            "CraftReplayHashBuilder did not produce stable hashes for authoritative craft job state.");

        Console.WriteLine("[PASS] Craft replay hash builder");
    }

    private static CraftJobReplaySnapshot CreateCraftReplaySnapshot(int workTicksRemaining)
    {
        var workshopId = Guid.Parse("aaaaaaaa-8888-8888-8888-aaaaaaaaaaaa");
        var queueEntryId = Guid.Parse("bbbbbbbb-8888-8888-8888-bbbbbbbbbbbb");

        var active = new[]
        {
            new CraftActiveJobStateSnapshot(
                Order: 0,
                WorkerId: Guid.Parse("cccccccc-8888-8888-8888-cccccccccccc"),
                WorkshopGuid: workshopId,
                QueueEntryId: queueEntryId,
                RecipeId: "test_recipe_plank",
                Stage: CraftJobStage.Working,
                WorkTicksRemaining: workTicksRemaining,
                Anchor: new SadRogue.Primitives.Point(11, 12),
                Z: 1)
        };

        var backlog = new[]
        {
            new CraftBacklogEntrySnapshot(
                Order: 0,
                Job: new PlannedCraftJob(
                    WorkshopGuid: workshopId,
                    QueueEntryId: Guid.Parse("dddddddd-8888-8888-8888-dddddddddddd"),
                    RecipeId: "test_recipe_block",
                    DurationTicks: 21,
                    Anchor: new SadRogue.Primitives.Point(13, 14),
                    Z: 2))
        };

        return new CraftJobReplaySnapshot(active, backlog);
    }

    private static void TestRuntimeReplayCheckpointHash()
    {
        var services = new RuntimeSessionServices();
        var emptyServicesData = RuntimeReplayCheckpointHashBuilder.BuildData(services, session: null);
        var emptyServicesHash = emptyServicesData.AggregateHash;
        var sameEmptyServicesHash = RuntimeReplayCheckpointHashBuilder.BuildData(services, session: null).AggregateHash;
        _ = services.RngStreams.GetStream("checkpoint.test").Next();
        var changedRngData = RuntimeReplayCheckpointHashBuilder.BuildData(services, session: null);
        services.ResetForNewSession();
        var resetServicesData = RuntimeReplayCheckpointHashBuilder.BuildData(services, session: null);
        var commandLogServices = new RuntimeSessionServices();
        var replayOrderCommands = BuildReplayOrderCommands(wrapWithRuntimeIdentity: true);
        foreach (var command in replayOrderCommands)
        {
            commandLogServices.CommandQueue.Enqueue(command);
        }

        _ = ExecuteExistingOrderQueue(commandLogServices);
        var commandLogData = RuntimeReplayCheckpointHashBuilder.BuildData(commandLogServices, session: null);
        var pendingCommandServices = new RuntimeSessionServices();
        pendingCommandServices.CommandQueue.Enqueue(new CreateHaulOrderCommand(
            tick: 99,
            new SadRogue.Primitives.Rectangle(2, 2, 1, 1),
            z: 0,
            priority: 3));
        var pendingCommandData = RuntimeReplayCheckpointHashBuilder.BuildData(pendingCommandServices, session: null);

        RegressionAssert.True(
            emptyServicesHash == sameEmptyServicesHash
            && emptyServicesHash != changedRngData.AggregateHash
            && emptyServicesData.RngHash != changedRngData.RngHash
            && emptyServicesData.RngStreamCount == 0
            && changedRngData.RngStreamCount == 1
            && emptyServicesHash == resetServicesData.AggregateHash
            && emptyServicesData.RngHash == resetServicesData.RngHash
            && resetServicesData.RngStreamCount == 0
            && emptyServicesData.CommandLogRecordCount == 0
            && emptyServicesData.PendingCommandLogRecordCount == 0
            && commandLogData.CommandLogRecordCount == replayOrderCommands.Length
            && commandLogData.CommandLogHash != emptyServicesData.CommandLogHash
            && commandLogData.AggregateHash != emptyServicesData.AggregateHash
            && pendingCommandData.PendingCommandLogRecordCount == 1
            && pendingCommandData.PendingCommandLogHash != emptyServicesData.PendingCommandLogHash
            && pendingCommandData.AggregateHash != emptyServicesData.AggregateHash
            && emptyServicesData.Metadata.SchemaVersion == SimulationSnapshotSchema.CurrentVersion,
            "Runtime replay checkpoint hash did not include session-owned RNG stream state and command replay journal state.");

        var rngDocumentServices = new RuntimeSessionServices();
        _ = rngDocumentServices.RngStreams.GetStream("save.rng").Next();
        var rngDocumentCheckpoint = RuntimeReplayCheckpointHashBuilder.BuildData(rngDocumentServices, session: null);
        var rngDocumentManifest = RuntimeSaveManifestBuilder.Build(
            rngDocumentCheckpoint,
            content: null,
            worldSnapshot: null);
        var rngDocument = new RuntimeSaveSnapshotData(
            rngDocumentManifest,
            worldPayload: null,
            miningJobs: null,
            transportJobs: null,
            craftJobs: null,
            rngDocumentServices.RngStreams.GetStateSnapshot(),
            Array.Empty<CommandReplayRecord>(),
            Array.Empty<CommandReplayRecord>()).ToDocumentData();
        var rngDocumentRoundTrip = RuntimeSaveSnapshotDocumentCodec.Deserialize(
            RuntimeSaveSnapshotDocumentCodec.Serialize(rngDocument));
        var mappedRngStreams = RuntimeSaveSnapshotDocumentRngMapper.ToRngStreamStateSnapshots(rngDocumentRoundTrip);
        var rngRestoreServices = new RuntimeSessionServices();
        _ = rngRestoreServices.RngStreams.GetStream("stale.before.restore").Next();
        var rngRestore = RuntimeSaveSnapshotRngRestorer.Restore(rngRestoreServices, rngDocumentRoundTrip);
        var rejectedMismatchedCommandSnapshot = false;
        try
        {
            _ = new RuntimeSaveSnapshotData(
                rngDocumentManifest,
                worldPayload: null,
                miningJobs: null,
                transportJobs: null,
                craftJobs: null,
                rngDocumentServices.RngStreams.GetStateSnapshot(),
                Array.Empty<CommandReplayRecord>(),
                new[]
                {
                    new CommandReplayRecord(0, Guid.Empty, "test.pending.mismatch", Array.Empty<byte>())
                });
        }
        catch (InvalidOperationException)
        {
            rejectedMismatchedCommandSnapshot = true;
        }

        RegressionAssert.True(
            rngDocumentRoundTrip.RngStreams.Length == 1
            && mappedRngStreams.Count == 1
            && mappedRngStreams[0].StreamName == "save.rng"
            && RngReplayHashBuilder.Build(mappedRngStreams) == rngDocumentCheckpoint.RngHash
            && rngRestore.Success
            && rngRestore.RestoredStreamCount == 1
            && rngRestoreServices.RngStreams.GetStateSnapshot().Count == 1
            && RngReplayHashBuilder.Build(rngRestoreServices.RngStreams) == rngDocumentCheckpoint.RngHash
            && rejectedMismatchedCommandSnapshot,
            "Runtime save snapshot document did not preserve and restore RNG stream payload rows.");

        var runtime = FortressRuntimeSessionFactory.CreateFull(
            AppContext.BaseDirectory,
            strictContent: false,
            contentWarningsAsErrors: false);
        var noWorldData = runtime.GetReplayCheckpointData();
        var noWorldManifest = runtime.GetSaveManifestData();
        runtime.InitializeWorld(sizeInChunks: 2, maxZ: 2);
        var worldData = runtime.GetReplayCheckpointData();
        var secondWorldData = runtime.GetReplayCheckpointData();
        var manifest = runtime.GetSaveManifestData();
        var saveSnapshot = runtime.CreateSaveSnapshotDocumentData();
        var sections = manifest.Sections.ToDictionary(section => section.Name, StringComparer.Ordinal);

        RegressionAssert.True(
            worldData.AggregateHash == runtime.GetReplayCheckpointHash()
            && worldData.AggregateHash == secondWorldData.AggregateHash
            && worldData.AggregateHash != noWorldData.AggregateHash
            && worldData.WorldHash != null
            && noWorldData.WorldHash == null
            && worldData.RngHash == secondWorldData.RngHash
            && worldData.Metadata.SchemaVersion == SimulationSnapshotSchema.CurrentVersion
            && worldData.Metadata.RuntimeTick == runtime.SimulationStatus.CurrentTick,
            "Runtime replay checkpoint hash did not expose a stable Runtime-authored session checkpoint.");

        RegressionAssert.True(
            noWorldManifest.FormatVersion == RuntimeSaveFormat.CurrentVersion
            && !noWorldManifest.Content.HasContent
            && !noWorldManifest.ContentCatalog.HasCatalog
            && noWorldManifest.Checkpoint.AggregateHash == noWorldData.AggregateHash
            && manifest.FormatVersion == RuntimeSaveFormat.CurrentVersion
            && manifest.Content.HasContent
            && !string.IsNullOrWhiteSpace(manifest.Content.ContentHash)
            && manifest.ContentCatalog.HasCatalog
            && manifest.ContentCatalog.MaterialNames.Length == manifest.Content.MaterialCount
            && manifest.ContentCatalog.TerrainKindNames.Length == manifest.Content.TerrainKindCount
            && manifest.ContentCatalog.ConstructionIds.Length == manifest.Content.ConstructionCount
            && manifest.ContentCatalog.RecipeIds.Length == manifest.Content.RecipeCount
            && manifest.ContentCatalog.GeologyIds.Length == manifest.Content.GeologyCount
            && manifest.ContentCatalog.ZoneIds.Length == manifest.Content.ZoneCount
            && manifest.Content.MaterialCount > 0
            && manifest.Content.TerrainKindCount > 0
            && manifest.Content.ConstructionCount > 0
            && manifest.Content.RecipeCount > 0
            && manifest.Content.GeologyCount > 0
            && manifest.Content.ZoneCount > 0
            && manifest.Checkpoint.AggregateHash == worldData.AggregateHash
            && sections["world"].Present
            && sections["world"].Hash == worldData.WorldHash
            && sections["world"].RequiredForFortressMode
            && sections["world"].RecordCount.GetValueOrDefault(-1) >= 0
            && sections["world.terrain"].Present
            && sections["world.terrain"].RecordCount.GetValueOrDefault(-1) >= 0
            && sections["world.items"].Present
            && sections["world.items"].RecordCount.GetValueOrDefault(-1) >= 0
            && sections["world.creatures"].Present
            && sections["world.creatures"].RecordCount.GetValueOrDefault(-1) >= 0
            && sections["world.reservations"].Present
            && sections["world.reservations"].RecordCount.GetValueOrDefault(-1) >= 0
            && sections["world.stockpiles"].Present
            && sections["world.stockpiles"].RecordCount.GetValueOrDefault(-1) >= 0
            && sections["world.placeables"].Present
            && sections["world.placeables"].RecordCount.GetValueOrDefault(-1) >= 0
            && sections["world.orders"].Present
            && sections["world.orders"].RecordCount.GetValueOrDefault(-1) >= 0
            && sections["rng"].Present
            && sections["rng"].Hash == worldData.RngHash
            && sections["rng"].RecordCount == worldData.RngStreamCount
            && sections["commands.executed"].Present
            && sections["commands.executed"].Hash == worldData.CommandLogHash
            && sections["commands.executed"].RecordCount == worldData.CommandLogRecordCount
            && sections["commands.pending"].Present
            && sections["commands.pending"].Hash == worldData.PendingCommandLogHash
            && sections["commands.pending"].RecordCount == worldData.PendingCommandLogRecordCount
            && sections["jobs.transport"].Present
            && sections["jobs.transport"].Hash == worldData.TransportHash
            && sections["jobs.transport"].RecordCount == worldData.TransportRecordCount
            && sections["jobs.mining"].Present
            && sections["jobs.mining"].Hash == worldData.MiningHash
            && sections["jobs.mining"].RecordCount == worldData.MiningRecordCount
            && sections["jobs.craft"].Present
            && sections["jobs.craft"].Hash == worldData.CraftHash
            && sections["jobs.craft"].RecordCount == worldData.CraftRecordCount,
            "Runtime save manifest did not bind checkpoint sections and content signature correctly.");

        RegressionAssert.True(
            saveSnapshot.Manifest.Checkpoint.AggregateHash == manifest.Checkpoint.AggregateHash
            && saveSnapshot.WorldPayload.HasValue
            && saveSnapshot.WorldPayload.Value.ReplayHash == worldData.WorldHash
            && saveSnapshot.MiningJobs.HasValue
            && RuntimeSaveSnapshotDocumentMiningMapper.CountRecords(saveSnapshot.MiningJobs.Value) == worldData.MiningRecordCount
            && RuntimeSaveSnapshotDocumentMiningMapper.BuildReplayHash(saveSnapshot.MiningJobs.Value) == worldData.MiningHash
            && saveSnapshot.TransportJobs.HasValue
            && RuntimeSaveSnapshotDocumentTransportMapper.CountRecords(saveSnapshot.TransportJobs.Value) == worldData.TransportRecordCount
            && RuntimeSaveSnapshotDocumentTransportMapper.BuildReplayHash(saveSnapshot.TransportJobs.Value) == worldData.TransportHash
            && saveSnapshot.CraftJobs.HasValue
            && RuntimeSaveSnapshotDocumentCraftMapper.CountRecords(saveSnapshot.CraftJobs.Value) == worldData.CraftRecordCount
            && RuntimeSaveSnapshotDocumentCraftMapper.BuildReplayHash(saveSnapshot.CraftJobs.Value) == worldData.CraftHash
            && saveSnapshot.RngStreams.Length == worldData.RngStreamCount
            && RngReplayHashBuilder.Build(RuntimeSaveSnapshotDocumentRngMapper.ToRngStreamStateSnapshots(saveSnapshot)) == worldData.RngHash
            && saveSnapshot.ExecutedCommandRecords.Length == worldData.CommandLogRecordCount
            && saveSnapshot.PendingCommandRecords.Length == worldData.PendingCommandLogRecordCount
            && saveSnapshot.Manifest.Checkpoint.CommandLogHash == worldData.CommandLogHash
            && saveSnapshot.Manifest.Checkpoint.PendingCommandLogHash == worldData.PendingCommandLogHash,
            "Runtime save snapshot document did not bind the manifest to the command replay journal snapshot.");

        var pendingRuntime = FortressRuntimeSessionFactory.CreateFull(
            AppContext.BaseDirectory,
            strictContent: false,
            contentWarningsAsErrors: false);
        pendingRuntime.InitializeWorld(sizeInChunks: 2, maxZ: 2);
        pendingRuntime.QueueHaulOrder(new RuntimeRect(1, 1, 1, 1), z: 0, priority: 5);
        var pendingDocument = pendingRuntime.CreateSaveSnapshotDocumentData();
        var pendingDocumentJson = RuntimeSaveSnapshotDocumentCodec.Serialize(pendingDocument);
        var pendingDocumentRoundTrip = RuntimeSaveSnapshotDocumentCodec.Deserialize(pendingDocumentJson);
        var pendingCommand = pendingDocumentRoundTrip.PendingCommandRecords.Single();

        RegressionAssert.True(
            pendingDocumentRoundTrip.Manifest.Checkpoint.AggregateHash == pendingDocument.Manifest.Checkpoint.AggregateHash
            && pendingDocumentRoundTrip.WorldPayload.HasValue
            && pendingDocumentRoundTrip.WorldPayload.Value.ReplayHash == pendingDocument.Manifest.Checkpoint.WorldHash
            && pendingDocumentRoundTrip.RngStreams.Length == pendingDocument.Manifest.Checkpoint.RngStreamCount
            && RngReplayHashBuilder.Build(RuntimeSaveSnapshotDocumentRngMapper.ToRngStreamStateSnapshots(pendingDocumentRoundTrip)) == pendingDocument.Manifest.Checkpoint.RngHash
            && pendingDocumentRoundTrip.PendingCommandRecords.Length == 1
            && pendingDocumentRoundTrip.ExecutedCommandRecords.Length == 0
            && pendingCommand.CommandType == "orders.haul.rect"
            && pendingCommand.PayloadLength > 0
            && !string.IsNullOrWhiteSpace(pendingCommand.PayloadBase64)
            && Convert.FromBase64String(pendingCommand.PayloadBase64).Length == pendingCommand.PayloadLength,
            "Runtime save snapshot document JSON round trip did not preserve pending command replay data.");

        var mappedPendingRecords = RuntimeSaveSnapshotDocumentCommandMapper.ToPendingCommandReplayRecords(pendingDocumentRoundTrip);
        var mappedExecutedRecords = RuntimeSaveSnapshotDocumentCommandMapper.ToExecutedCommandReplayRecords(pendingDocumentRoundTrip);
        var mappedPendingRecord = mappedPendingRecords.Single();

        RegressionAssert.True(
            mappedExecutedRecords.Count == 0
            && mappedPendingRecords.Count == 1
            && mappedPendingRecord.CommandType == pendingCommand.CommandType
            && mappedPendingRecord.CommandId == pendingCommand.CommandId
            && mappedPendingRecord.PayloadLength == pendingCommand.PayloadLength
            && CommandReplayJournalHashBuilder.Build(mappedPendingRecords) == pendingDocumentRoundTrip.Manifest.Checkpoint.PendingCommandLogHash,
            "Runtime save snapshot document mapper did not rebuild pending command replay records for Runtime-owned replay restore.");

        var documentStoreDirectory = Path.Combine(Path.GetTempPath(), "humanfortress-save-document-" + Guid.NewGuid().ToString("N"));
        pendingRuntime.WriteSaveSnapshotDocument(documentStoreDirectory);
        var storedDocument = pendingRuntime.ReadSaveSnapshotDocument(documentStoreDirectory);
        pendingRuntime.WriteSaveSnapshotDocument(documentStoreDirectory);
        var replacedDocument = pendingRuntime.ReadSaveSnapshotDocument(documentStoreDirectory);
        var directoryInspectionResult = pendingRuntime.InspectSaveSnapshotDirectory(documentStoreDirectory);
        var directoryValidationResult = pendingRuntime.ValidateSaveSnapshotDirectory(documentStoreDirectory);
        var compatibleMigrationResult = pendingRuntime.MigrateSaveSnapshotDirectory(
            documentStoreDirectory,
            documentStoreDirectory + "-migrated");
        var slotManifestPath = Path.Combine(documentStoreDirectory, RuntimeSaveSlotFormat.ManifestFileName);
        var slotManifestJson = File.ReadAllText(slotManifestPath);
        var slotManifest = RuntimeSaveSlotManifestCodec.Deserialize(slotManifestJson);
        var slotManifestValidationResult = RuntimeSaveSlotManifestVerifier.Validate(slotManifest, pendingDocumentRoundTrip);
        var compatibleSlotResult = RuntimeSaveSlotCompatibilityPolicy.Evaluate(slotManifest);
        var olderSlotCompatibilityResult = RuntimeSaveSlotCompatibilityPolicy.Evaluate(slotManifest with
        {
            SlotFormatVersion = RuntimeSaveSlotFormat.CurrentVersion - 1
        });
        var olderSnapshotCompatibilityResult = RuntimeSaveSlotCompatibilityPolicy.Evaluate(slotManifest with
        {
            RuntimeSnapshotFormatVersion = RuntimeSaveFormat.CurrentVersion - 1
        });
        var futureSlotCompatibilityResult = RuntimeSaveSlotCompatibilityPolicy.Evaluate(slotManifest with
        {
            SlotFormatVersion = RuntimeSaveSlotFormat.CurrentVersion + 1
        });
        var wrongKindCompatibilityResult = RuntimeSaveSlotCompatibilityPolicy.Evaluate(slotManifest with
        {
            SlotKind = "legacy-slot"
        });
        var tamperedSlotManifestJson = RuntimeSaveSlotManifestCodec.Serialize(slotManifest with
        {
            CheckpointAggregateHash = "tampered"
        });
        File.WriteAllText(slotManifestPath, tamperedSlotManifestJson);
        var tamperedSlotManifestValidationResult = pendingRuntime.ValidateSaveSnapshotDirectory(documentStoreDirectory);
        var tamperedSlotInspectionResult = pendingRuntime.InspectSaveSnapshotDirectory(documentStoreDirectory);
        var oldSlotManifestJson = RuntimeSaveSlotManifestCodec.Serialize(slotManifest with
        {
            SlotFormatVersion = RuntimeSaveSlotFormat.CurrentVersion - 1
        });
        File.WriteAllText(slotManifestPath, oldSlotManifestJson);
        var oldSlotManifestValidationResult = pendingRuntime.ValidateSaveSnapshotDirectory(documentStoreDirectory);
        var oldSlotInspectionResult = pendingRuntime.InspectSaveSnapshotDirectory(documentStoreDirectory);
        var oldSlotMigrationResult = pendingRuntime.MigrateSaveSnapshotDirectory(
            documentStoreDirectory,
            documentStoreDirectory + "-old-migrated");
        var oldSlotMigratedInspectionResult = pendingRuntime.InspectSaveSnapshotDirectory(
            documentStoreDirectory + "-old-migrated");
        File.WriteAllText(slotManifestPath, slotManifestJson);
        var snapshotDocumentPath = Path.Combine(documentStoreDirectory, RuntimeSaveSlotFormat.SnapshotDocumentFileName);
        var snapshotDocumentJson = File.ReadAllText(snapshotDocumentPath);
        File.Delete(snapshotDocumentPath);
        var missingDocumentInspectionResult = pendingRuntime.InspectSaveSnapshotDirectory(documentStoreDirectory);
        File.WriteAllText(snapshotDocumentPath, snapshotDocumentJson);
        var contentMismatchDocument = pendingDocumentRoundTrip with
        {
            Manifest = pendingDocumentRoundTrip.Manifest with
            {
                Content = pendingDocumentRoundTrip.Manifest.Content with
                {
                    ContentHash = pendingDocumentRoundTrip.Manifest.Content.ContentHash + "-mismatch"
                }
            }
        };
        File.WriteAllText(
            snapshotDocumentPath,
            RuntimeSaveSnapshotDocumentCodec.Serialize(contentMismatchDocument));
        var contentMismatchInspectionResult = pendingRuntime.InspectSaveSnapshotDirectory(documentStoreDirectory);
        File.WriteAllText(snapshotDocumentPath, snapshotDocumentJson);
        var legacySnapshotOnlyDirectory = documentStoreDirectory + "-legacy-snapshot-only";
        var legacySnapshotOnlyMigratedDirectory = legacySnapshotOnlyDirectory + "-migrated";
        Directory.CreateDirectory(legacySnapshotOnlyDirectory);
        File.WriteAllText(
            Path.Combine(legacySnapshotOnlyDirectory, RuntimeSaveSlotFormat.SnapshotDocumentFileName),
            snapshotDocumentJson);
        var legacySnapshotOnlyInspectionResult = pendingRuntime.InspectSaveSnapshotDirectory(legacySnapshotOnlyDirectory);
        var legacySnapshotOnlyMigrationResult = pendingRuntime.MigrateSaveSnapshotDirectory(
            legacySnapshotOnlyDirectory,
            legacySnapshotOnlyMigratedDirectory);
        var legacySnapshotOnlyMigratedInspectionResult = pendingRuntime.InspectSaveSnapshotDirectory(
            legacySnapshotOnlyMigratedDirectory);
        var runtimeV4Directory = documentStoreDirectory + "-runtime-v4";
        var runtimeV4MigratedDirectory = runtimeV4Directory + "-migrated";
        var runtimeV4Document = pendingDocumentRoundTrip with
        {
            MiningJobs = null,
            Manifest = pendingDocumentRoundTrip.Manifest with
            {
                FormatVersion = 4
            }
        };
        Directory.CreateDirectory(runtimeV4Directory);
        File.WriteAllText(
            Path.Combine(runtimeV4Directory, RuntimeSaveSlotFormat.SnapshotDocumentFileName),
            RuntimeSaveSnapshotDocumentCodec.Serialize(runtimeV4Document));
        File.WriteAllText(
            Path.Combine(runtimeV4Directory, RuntimeSaveSlotFormat.ManifestFileName),
            RuntimeSaveSlotManifestCodec.Serialize(RuntimeSaveSlotManifestBuilder.Build(runtimeV4Document)));
        var runtimeV4InspectionResult = pendingRuntime.InspectSaveSnapshotDirectory(runtimeV4Directory);
        var runtimeV4MigrationResult = pendingRuntime.MigrateSaveSnapshotDirectory(
            runtimeV4Directory,
            runtimeV4MigratedDirectory);
        var runtimeV4MigratedInspectionResult = pendingRuntime.InspectSaveSnapshotDirectory(
            runtimeV4MigratedDirectory);
        var runtimeV4MigratedDocument = pendingRuntime.ReadSaveSnapshotDocument(runtimeV4MigratedDirectory);
        var runtimeV4NonEmptyMiningDirectory = documentStoreDirectory + "-runtime-v4-non-empty-mining";
        var runtimeV4NonEmptyMiningDocument = runtimeV4Document with
        {
            Manifest = runtimeV4Document.Manifest with
            {
                Checkpoint = runtimeV4Document.Manifest.Checkpoint with
                {
                    MiningRecordCount = 1
                },
                Sections = runtimeV4Document.Manifest.Sections
                    .Select(section => section.Name == "jobs.mining"
                        ? section with { RecordCount = 1 }
                        : section)
                    .ToArray()
            }
        };
        Directory.CreateDirectory(runtimeV4NonEmptyMiningDirectory);
        File.WriteAllText(
            Path.Combine(runtimeV4NonEmptyMiningDirectory, RuntimeSaveSlotFormat.SnapshotDocumentFileName),
            RuntimeSaveSnapshotDocumentCodec.Serialize(runtimeV4NonEmptyMiningDocument));
        File.WriteAllText(
            Path.Combine(runtimeV4NonEmptyMiningDirectory, RuntimeSaveSlotFormat.ManifestFileName),
            RuntimeSaveSlotManifestCodec.Serialize(RuntimeSaveSlotManifestBuilder.Build(runtimeV4NonEmptyMiningDocument)));
        var runtimeV4NonEmptyMiningInspectionResult = pendingRuntime.InspectSaveSnapshotDirectory(runtimeV4NonEmptyMiningDirectory);
        var runtimeV4NonEmptyMiningMigrationResult = pendingRuntime.MigrateSaveSnapshotDirectory(
            runtimeV4NonEmptyMiningDirectory,
            runtimeV4NonEmptyMiningDirectory + "-migrated");
        var runtimeV5NoCatalogDirectory = documentStoreDirectory + "-runtime-v5-no-catalog";
        var runtimeV5NoCatalogMigratedDirectory = runtimeV5NoCatalogDirectory + "-migrated";
        var runtimeV5NoCatalogDocument = pendingDocumentRoundTrip with
        {
            Manifest = pendingDocumentRoundTrip.Manifest with
            {
                FormatVersion = 5,
                ContentCatalog = RuntimeSaveContentCatalogSummaryData.Unavailable
            }
        };
        var runtimeV5MissingCatalogJsonNode = JsonNode.Parse(RuntimeSaveSnapshotDocumentCodec.Serialize(runtimeV5NoCatalogDocument))!.AsObject();
        runtimeV5MissingCatalogJsonNode["manifest"]!.AsObject().Remove("contentCatalog");
        var runtimeV5MissingCatalogJson = runtimeV5MissingCatalogJsonNode.ToJsonString();
        var runtimeV5MissingCatalogRoundTrip = RuntimeSaveSnapshotDocumentCodec.Deserialize(runtimeV5MissingCatalogJson);
        Directory.CreateDirectory(runtimeV5NoCatalogDirectory);
        File.WriteAllText(
            Path.Combine(runtimeV5NoCatalogDirectory, RuntimeSaveSlotFormat.SnapshotDocumentFileName),
            runtimeV5MissingCatalogJson);
        File.WriteAllText(
            Path.Combine(runtimeV5NoCatalogDirectory, RuntimeSaveSlotFormat.ManifestFileName),
            RuntimeSaveSlotManifestCodec.Serialize(RuntimeSaveSlotManifestBuilder.Build(runtimeV5MissingCatalogRoundTrip)));
        var runtimeV5NoCatalogInspectionResult = pendingRuntime.InspectSaveSnapshotDirectory(runtimeV5NoCatalogDirectory);
        var runtimeV5NoCatalogMigrationResult = pendingRuntime.MigrateSaveSnapshotDirectory(
            runtimeV5NoCatalogDirectory,
            runtimeV5NoCatalogMigratedDirectory);
        var runtimeV5NoCatalogMigratedInspectionResult = pendingRuntime.InspectSaveSnapshotDirectory(
            runtimeV5NoCatalogMigratedDirectory);
        var runtimeV5NoCatalogMigratedDocument = pendingRuntime.ReadSaveSnapshotDocument(runtimeV5NoCatalogMigratedDirectory);
        var runtimeV5OldSlotDirectory = documentStoreDirectory + "-runtime-v5-old-slot";
        var runtimeV5OldSlotMigratedDirectory = runtimeV5OldSlotDirectory + "-migrated";
        Directory.CreateDirectory(runtimeV5OldSlotDirectory);
        File.WriteAllText(
            Path.Combine(runtimeV5OldSlotDirectory, RuntimeSaveSlotFormat.SnapshotDocumentFileName),
            runtimeV5MissingCatalogJson);
        File.WriteAllText(
            Path.Combine(runtimeV5OldSlotDirectory, RuntimeSaveSlotFormat.ManifestFileName),
            RuntimeSaveSlotManifestCodec.Serialize(
                RuntimeSaveSlotManifestBuilder.Build(runtimeV5MissingCatalogRoundTrip) with
                {
                    SlotFormatVersion = RuntimeSaveSlotFormat.CurrentVersion - 1
                }));
        var runtimeV5OldSlotInspectionResult = pendingRuntime.InspectSaveSnapshotDirectory(runtimeV5OldSlotDirectory);
        var runtimeV5OldSlotMigrationResult = pendingRuntime.MigrateSaveSnapshotDirectory(
            runtimeV5OldSlotDirectory,
            runtimeV5OldSlotMigratedDirectory);
        var runtimeV5OldSlotMigratedInspectionResult = pendingRuntime.InspectSaveSnapshotDirectory(runtimeV5OldSlotMigratedDirectory);
        var compatibleCurrentCatalog = directoryInspectionResult.ContentCompatibility.CurrentCatalog;
        var compatibleSavedCatalog = directoryInspectionResult.ContentCompatibility.SavedCatalog;
        var mismatchCurrentCatalog = contentMismatchInspectionResult.ContentCompatibility.CurrentCatalog;
        var mismatchSavedCatalog = contentMismatchInspectionResult.ContentCompatibility.SavedCatalog;
        var compatibleSavedCatalogMatchesSignature =
            compatibleSavedCatalog.HasCatalog
            && compatibleSavedCatalog.MaterialNames.Length == directoryInspectionResult.ContentCompatibility.SavedContent.MaterialCount
            && compatibleSavedCatalog.TerrainKindNames.Length == directoryInspectionResult.ContentCompatibility.SavedContent.TerrainKindCount
            && compatibleSavedCatalog.ConstructionIds.Length == directoryInspectionResult.ContentCompatibility.SavedContent.ConstructionCount
            && compatibleSavedCatalog.RecipeIds.Length == directoryInspectionResult.ContentCompatibility.SavedContent.RecipeCount
            && compatibleSavedCatalog.GeologyIds.Length == directoryInspectionResult.ContentCompatibility.SavedContent.GeologyCount
            && compatibleSavedCatalog.ZoneIds.Length == directoryInspectionResult.ContentCompatibility.SavedContent.ZoneCount;
        var compatibleCurrentCatalogMatchesSignature =
            compatibleCurrentCatalog.HasCatalog
            && compatibleCurrentCatalog.MaterialNames.Length == directoryInspectionResult.ContentCompatibility.CurrentContent.MaterialCount
            && compatibleCurrentCatalog.TerrainKindNames.Length == directoryInspectionResult.ContentCompatibility.CurrentContent.TerrainKindCount
            && compatibleCurrentCatalog.ConstructionIds.Length == directoryInspectionResult.ContentCompatibility.CurrentContent.ConstructionCount
            && compatibleCurrentCatalog.RecipeIds.Length == directoryInspectionResult.ContentCompatibility.CurrentContent.RecipeCount
            && compatibleCurrentCatalog.GeologyIds.Length == directoryInspectionResult.ContentCompatibility.CurrentContent.GeologyCount
            && compatibleCurrentCatalog.ZoneIds.Length == directoryInspectionResult.ContentCompatibility.CurrentContent.ZoneCount;
        var compatibleCurrentCatalogUsesStableOrder =
            compatibleCurrentCatalog.MaterialNames.SequenceEqual(compatibleCurrentCatalog.MaterialNames.OrderBy(static value => value, StringComparer.Ordinal))
            && compatibleCurrentCatalog.TerrainKindNames.SequenceEqual(compatibleCurrentCatalog.TerrainKindNames.OrderBy(static value => value, StringComparer.Ordinal))
            && compatibleCurrentCatalog.ConstructionIds.SequenceEqual(compatibleCurrentCatalog.ConstructionIds.OrderBy(static value => value, StringComparer.Ordinal))
            && compatibleCurrentCatalog.RecipeIds.SequenceEqual(compatibleCurrentCatalog.RecipeIds.OrderBy(static value => value, StringComparer.Ordinal))
            && compatibleCurrentCatalog.GeologyIds.SequenceEqual(compatibleCurrentCatalog.GeologyIds.OrderBy(static value => value, StringComparer.Ordinal))
            && compatibleCurrentCatalog.ZoneIds.SequenceEqual(compatibleCurrentCatalog.ZoneIds.OrderBy(static value => value, StringComparer.Ordinal));
        var mismatchCurrentCatalogMatchesSignature =
            mismatchCurrentCatalog.HasCatalog
            && mismatchCurrentCatalog.MaterialNames.Length == contentMismatchInspectionResult.ContentCompatibility.CurrentContent.MaterialCount
            && mismatchCurrentCatalog.TerrainKindNames.Length == contentMismatchInspectionResult.ContentCompatibility.CurrentContent.TerrainKindCount
            && mismatchCurrentCatalog.ConstructionIds.Length == contentMismatchInspectionResult.ContentCompatibility.CurrentContent.ConstructionCount
            && mismatchCurrentCatalog.RecipeIds.Length == contentMismatchInspectionResult.ContentCompatibility.CurrentContent.RecipeCount
            && mismatchCurrentCatalog.GeologyIds.Length == contentMismatchInspectionResult.ContentCompatibility.CurrentContent.GeologyCount
            && mismatchCurrentCatalog.ZoneIds.Length == contentMismatchInspectionResult.ContentCompatibility.CurrentContent.ZoneCount;
        var mismatchSavedCatalogMatchesSignature =
            mismatchSavedCatalog.HasCatalog
            && mismatchSavedCatalog.MaterialNames.Length == contentMismatchInspectionResult.ContentCompatibility.SavedContent.MaterialCount
            && mismatchSavedCatalog.TerrainKindNames.Length == contentMismatchInspectionResult.ContentCompatibility.SavedContent.TerrainKindCount
            && mismatchSavedCatalog.ConstructionIds.Length == contentMismatchInspectionResult.ContentCompatibility.SavedContent.ConstructionCount
            && mismatchSavedCatalog.RecipeIds.Length == contentMismatchInspectionResult.ContentCompatibility.SavedContent.RecipeCount
            && mismatchSavedCatalog.GeologyIds.Length == contentMismatchInspectionResult.ContentCompatibility.SavedContent.GeologyCount
            && mismatchSavedCatalog.ZoneIds.Length == contentMismatchInspectionResult.ContentCompatibility.SavedContent.ZoneCount;
        var savedCatalogWithMissingMaterial = compatibleCurrentCatalog with
        {
            MaterialNames = compatibleCurrentCatalog.MaterialNames
                .Concat(new[] { "zz_saved_only_material" })
                .OrderBy(static value => value, StringComparer.Ordinal)
                .ToArray()
        };
        var catalogShapeCompatibility = RuntimeSaveSlotContentCompatibilityPolicy.Evaluate(
            directoryInspectionResult.ContentCompatibility.CurrentContent with
            {
                MaterialCount = directoryInspectionResult.ContentCompatibility.CurrentContent.MaterialCount + 1
            },
            savedCatalogWithMissingMaterial,
            directoryInspectionResult.ContentCompatibility.CurrentContent,
            compatibleCurrentCatalog);
        var catalogShapeDifference = catalogShapeCompatibility.DifferenceDetails.SingleOrDefault(
            difference => difference.Kind == RuntimeSaveContentCompatibilityDifferenceKind.MaterialCount);
        var runtimeV4RequiredTransforms = new[] { "runtime_snapshot:4->5", "runtime_snapshot:5->6" };
        var runtimeV5RequiredTransforms = new[] { "runtime_snapshot:5->6" };
        var runtimeV5OldSlotRequiredTransforms = new[] { "runtime_snapshot:5->6", "slot:0->1" };

        RegressionAssert.True(
            storedDocument.Manifest.Checkpoint.AggregateHash == pendingDocumentRoundTrip.Manifest.Checkpoint.AggregateHash
            && replacedDocument.Manifest.Checkpoint.AggregateHash == pendingDocumentRoundTrip.Manifest.Checkpoint.AggregateHash
            && storedDocument.PendingCommandRecords.Length == 1
            && replacedDocument.PendingCommandRecords.Length == 1
            && directoryInspectionResult.Success
            && directoryInspectionResult.Validation.Success
            && directoryInspectionResult.Manifest.HasValue
            && directoryInspectionResult.Manifest.Value.CheckpointAggregateHash == pendingDocumentRoundTrip.Manifest.Checkpoint.AggregateHash
            && directoryInspectionResult.Compatibility.Status == RuntimeSaveSlotCompatibilityStatus.Compatible
            && directoryInspectionResult.Compatibility.CanRead
            && directoryInspectionResult.ContentCompatibility.Status == RuntimeSaveSlotContentCompatibilityStatus.Compatible
            && directoryInspectionResult.ContentCompatibility.CanBindContent
            && !directoryInspectionResult.ContentCompatibility.RequiresMissingContentPolicy
            && directoryInspectionResult.ContentCompatibility.BlockingIssues.Length == 0
            && compatibleSavedCatalogMatchesSignature
            && compatibleCurrentCatalogMatchesSignature
            && compatibleCurrentCatalogUsesStableOrder
            && !directoryInspectionResult.MigrationPlan.RequiresMigration
            && !directoryInspectionResult.MigrationPlan.CanMigrate
            && directoryInspectionResult.MigrationPlan.BlockingIssues.Length == 0
            && directoryInspectionResult.RestorePlan.CanRestorePendingCommands
            && directoryInspectionResult.RestorePlan.CanRestoreWorld
            && directoryInspectionResult.RestorePlan.CanRestoreFull
            && directoryInspectionResult.RestorePlan.BlockingIssues.Length == 0
            && directoryValidationResult.Success
            && compatibleMigrationResult.Success
            && !compatibleMigrationResult.MigrationApplied
            && compatibleMigrationResult.Inspection.Success
            && compatibleMigrationResult.Inspection.Compatibility.Status == RuntimeSaveSlotCompatibilityStatus.Compatible
            && compatibleMigrationResult.AppliedTransforms.Length == 0
            && compatibleMigrationResult.MigrationIssues.Length == 0
            && File.Exists(slotManifestPath)
            && slotManifest.SlotFormatVersion == RuntimeSaveSlotFormat.CurrentVersion
            && slotManifest.SlotKind == RuntimeSaveSlotFormat.SlotKind
            && slotManifest.RuntimeSnapshotDocumentFileName == RuntimeSaveSlotFormat.SnapshotDocumentFileName
            && slotManifest.RuntimeSnapshotFormatVersion == pendingDocumentRoundTrip.Manifest.FormatVersion
            && slotManifest.CheckpointAggregateHash == pendingDocumentRoundTrip.Manifest.Checkpoint.AggregateHash
            && slotManifest.ManifestSectionCount == pendingDocumentRoundTrip.Manifest.Sections.Count
            && slotManifest.RngStreamCount == pendingDocumentRoundTrip.RngStreams.Length
            && slotManifest.ExecutedCommandRecordCount == pendingDocumentRoundTrip.ExecutedCommandRecords.Length
            && slotManifest.PendingCommandRecordCount == pendingDocumentRoundTrip.PendingCommandRecords.Length
            && slotManifestValidationResult.Success
            && !tamperedSlotManifestValidationResult.Success
            && tamperedSlotManifestValidationResult.Issues.Any(issue => issue.Section == "slot.manifest")
            && compatibleSlotResult.Status == RuntimeSaveSlotCompatibilityStatus.Compatible
            && compatibleSlotResult.CanRead
            && !compatibleSlotResult.RequiresMigration
            && olderSlotCompatibilityResult.Status == RuntimeSaveSlotCompatibilityStatus.MigrationRequired
            && !olderSlotCompatibilityResult.CanRead
            && olderSlotCompatibilityResult.RequiresMigration
            && olderSnapshotCompatibilityResult.Status == RuntimeSaveSlotCompatibilityStatus.MigrationRequired
            && !olderSnapshotCompatibilityResult.CanRead
            && olderSnapshotCompatibilityResult.RequiresMigration
            && futureSlotCompatibilityResult.Status == RuntimeSaveSlotCompatibilityStatus.UnsupportedFutureSlotFormat
            && !futureSlotCompatibilityResult.CanRead
            && !futureSlotCompatibilityResult.RequiresMigration
            && wrongKindCompatibilityResult.Status == RuntimeSaveSlotCompatibilityStatus.UnsupportedSlotKind
            && !wrongKindCompatibilityResult.CanRead
            && !oldSlotManifestValidationResult.Success
            && oldSlotManifestValidationResult.Issues.Any(issue => issue.Section == "slot.compatibility")
            && !tamperedSlotInspectionResult.Success
            && tamperedSlotInspectionResult.Manifest.HasValue
            && tamperedSlotInspectionResult.Compatibility.CanRead
            && !tamperedSlotInspectionResult.RestorePlan.CanRestoreFull
            && tamperedSlotInspectionResult.RestorePlan.BlockingIssues.Any(issue => issue.Section == "slot.manifest")
            && !oldSlotInspectionResult.Success
            && oldSlotInspectionResult.Manifest.HasValue
            && oldSlotInspectionResult.Compatibility.Status == RuntimeSaveSlotCompatibilityStatus.MigrationRequired
            && oldSlotInspectionResult.Compatibility.RequiresMigration
            && oldSlotInspectionResult.MigrationPlan.RequiresMigration
            && oldSlotInspectionResult.MigrationPlan.CanMigrate
            && oldSlotInspectionResult.MigrationPlan.RequiredTransforms.Contains($"slot:{RuntimeSaveSlotFormat.CurrentVersion - 1}->{RuntimeSaveSlotFormat.CurrentVersion}")
            && oldSlotInspectionResult.MigrationPlan.BlockingIssues.Length == 0
            && oldSlotMigrationResult.Success
            && oldSlotMigrationResult.MigrationApplied
            && oldSlotMigrationResult.Inspection.MigrationPlan.RequiresMigration
            && oldSlotMigrationResult.AppliedTransforms.SequenceEqual(new[] { $"slot:{RuntimeSaveSlotFormat.CurrentVersion - 1}->{RuntimeSaveSlotFormat.CurrentVersion}" })
            && oldSlotMigrationResult.MigrationIssues.Length == 0
            && oldSlotMigratedInspectionResult.Success
            && oldSlotMigratedInspectionResult.Compatibility.Status == RuntimeSaveSlotCompatibilityStatus.Compatible
            && oldSlotMigratedInspectionResult.RestorePlan.CanRestoreFull
            && !oldSlotInspectionResult.RestorePlan.CanRestoreFull
            && oldSlotInspectionResult.Validation.Issues.Any(issue => issue.Section == "slot.compatibility")
            && !missingDocumentInspectionResult.Success
            && missingDocumentInspectionResult.Manifest.HasValue
            && missingDocumentInspectionResult.Compatibility.CanRead
            && !missingDocumentInspectionResult.RestorePlan.CanRestorePendingCommands
            && !missingDocumentInspectionResult.RestorePlan.CanRestoreWorld
            && !missingDocumentInspectionResult.RestorePlan.CanRestoreFull
            && missingDocumentInspectionResult.RestorePlan.BlockingIssues.Any(issue => issue.Section == "snapshot.document")
            && missingDocumentInspectionResult.Validation.Issues.Any(issue => issue.Section == "snapshot.document")
            && contentMismatchInspectionResult.Success
            && contentMismatchInspectionResult.Validation.Success
            && contentMismatchInspectionResult.ContentCompatibility.Status == RuntimeSaveSlotContentCompatibilityStatus.ContentHashMismatch
            && !contentMismatchInspectionResult.ContentCompatibility.CanBindContent
            && contentMismatchInspectionResult.ContentCompatibility.RequiresMissingContentPolicy
            && contentMismatchInspectionResult.ContentCompatibility.Differences.Any(difference => difference.Contains("content hash", StringComparison.Ordinal))
            && contentMismatchInspectionResult.ContentCompatibility.DifferenceDetails.Any(difference =>
                difference.Kind == RuntimeSaveContentCompatibilityDifferenceKind.ContentHash
                && difference.Field == "content hash"
                && !difference.HasSavedCatalogKeys
                && !difference.HasCurrentCatalogKeys)
            && mismatchSavedCatalogMatchesSignature
            && mismatchCurrentCatalogMatchesSignature
            && catalogShapeCompatibility.Status == RuntimeSaveSlotContentCompatibilityStatus.CatalogShapeMismatch
            && !catalogShapeCompatibility.CanBindContent
            && catalogShapeCompatibility.DifferenceDetails.Length == 1
            && catalogShapeDifference.Kind == RuntimeSaveContentCompatibilityDifferenceKind.MaterialCount
            && catalogShapeDifference.Field == "material count"
            && catalogShapeDifference.HasCurrentCatalogKeys
            && catalogShapeDifference.HasSavedCatalogKeys
            && catalogShapeDifference.MissingCurrentKeys.SequenceEqual(new[] { "zz_saved_only_material" })
            && catalogShapeDifference.AdditionalCurrentKeys.Length == 0
            && catalogShapeCompatibility.Differences.SequenceEqual(catalogShapeCompatibility.DifferenceDetails.Select(static difference => difference.Message))
            && !contentMismatchInspectionResult.RestorePlan.CanRestorePendingCommands
            && !contentMismatchInspectionResult.RestorePlan.CanRestoreWorld
            && !contentMismatchInspectionResult.RestorePlan.CanRestoreFull
            && contentMismatchInspectionResult.RestorePlan.BlockingIssues.Any(issue => issue.Section == "slot.content")
            && !legacySnapshotOnlyInspectionResult.Success
            && !legacySnapshotOnlyInspectionResult.Manifest.HasValue
            && legacySnapshotOnlyInspectionResult.Compatibility.Status == RuntimeSaveSlotCompatibilityStatus.MigrationRequired
            && legacySnapshotOnlyInspectionResult.Compatibility.RequiresMigration
            && legacySnapshotOnlyInspectionResult.MigrationPlan.CanMigrate
            && legacySnapshotOnlyInspectionResult.MigrationPlan.RequiredTransforms.SequenceEqual(new[] { "slot:0->1" })
            && legacySnapshotOnlyInspectionResult.MigrationPlan.BlockingIssues.Length == 0
            && !legacySnapshotOnlyInspectionResult.RestorePlan.CanRestoreFull
            && legacySnapshotOnlyInspectionResult.Validation.Issues.Any(issue => issue.Section == "slot.manifest")
            && legacySnapshotOnlyMigrationResult.Success
            && legacySnapshotOnlyMigrationResult.MigrationApplied
            && legacySnapshotOnlyMigrationResult.AppliedTransforms.SequenceEqual(new[] { "slot:0->1" })
            && legacySnapshotOnlyMigrationResult.MigrationIssues.Length == 0
            && legacySnapshotOnlyMigratedInspectionResult.Success
            && legacySnapshotOnlyMigratedInspectionResult.Compatibility.Status == RuntimeSaveSlotCompatibilityStatus.Compatible
            && legacySnapshotOnlyMigratedInspectionResult.RestorePlan.CanRestoreFull
            && !runtimeV4InspectionResult.Success
            && runtimeV4InspectionResult.Compatibility.Status == RuntimeSaveSlotCompatibilityStatus.MigrationRequired
            && runtimeV4InspectionResult.Compatibility.RequiresMigration
            && runtimeV4InspectionResult.MigrationPlan.CanMigrate
            && runtimeV4InspectionResult.MigrationPlan.RequiredTransforms.SequenceEqual(runtimeV4RequiredTransforms)
            && runtimeV4InspectionResult.MigrationPlan.BlockingIssues.Length == 0
            && runtimeV4InspectionResult.Validation.Issues.Any(issue => issue.Section == "manifest")
            && runtimeV4MigrationResult.Success
            && runtimeV4MigrationResult.MigrationApplied
            && runtimeV4MigrationResult.AppliedTransforms.SequenceEqual(runtimeV4RequiredTransforms)
            && runtimeV4MigrationResult.MigrationIssues.Length == 0
            && runtimeV4MigratedInspectionResult.Success
            && runtimeV4MigratedInspectionResult.Compatibility.Status == RuntimeSaveSlotCompatibilityStatus.Compatible
            && runtimeV4MigratedInspectionResult.RestorePlan.CanRestoreFull
            && runtimeV4MigratedDocument.Manifest.FormatVersion == RuntimeSaveFormat.CurrentVersion
            && !runtimeV4MigratedDocument.MiningJobs.HasValue
            && !runtimeV4NonEmptyMiningInspectionResult.MigrationPlan.CanMigrate
            && runtimeV4NonEmptyMiningInspectionResult.MigrationPlan.BlockingIssues.Any(issue => issue.Section == "jobs.mining")
            && !runtimeV4NonEmptyMiningMigrationResult.Success
            && runtimeV4NonEmptyMiningMigrationResult.MigrationIssues.Any(issue => issue.Section == "jobs.mining"),
            "Runtime save snapshot document store did not write/read/inspect a strict compatible save-slot manifest with the save document.");

        RegressionAssert.True(
            !runtimeV5NoCatalogInspectionResult.Success
            && runtimeV5NoCatalogInspectionResult.Compatibility.Status == RuntimeSaveSlotCompatibilityStatus.MigrationRequired
            && runtimeV5NoCatalogInspectionResult.Compatibility.RequiresMigration
            && runtimeV5NoCatalogInspectionResult.MigrationPlan.CanMigrate
            && runtimeV5NoCatalogInspectionResult.MigrationPlan.RequiredTransforms.SequenceEqual(runtimeV5RequiredTransforms)
            && !runtimeV5MissingCatalogRoundTrip.Manifest.ContentCatalog.HasCatalog
            && runtimeV5NoCatalogInspectionResult.ContentCompatibility.Status == RuntimeSaveSlotContentCompatibilityStatus.Compatible
            && !runtimeV5NoCatalogInspectionResult.ContentCompatibility.SavedCatalog.HasCatalog
            && runtimeV5NoCatalogInspectionResult.ContentCompatibility.CurrentCatalog.HasCatalog
            && runtimeV5NoCatalogMigrationResult.Success
            && runtimeV5NoCatalogMigrationResult.MigrationApplied
            && runtimeV5NoCatalogMigrationResult.AppliedTransforms.SequenceEqual(runtimeV5RequiredTransforms)
            && runtimeV5NoCatalogMigrationResult.MigrationIssues.Length == 0
            && runtimeV5NoCatalogMigratedInspectionResult.Success
            && runtimeV5NoCatalogMigratedInspectionResult.Compatibility.Status == RuntimeSaveSlotCompatibilityStatus.Compatible
            && runtimeV5NoCatalogMigratedInspectionResult.RestorePlan.CanRestoreFull
            && runtimeV5NoCatalogMigratedDocument.Manifest.FormatVersion == RuntimeSaveFormat.CurrentVersion
            && !runtimeV5NoCatalogMigratedDocument.Manifest.ContentCatalog.HasCatalog,
            "Runtime save v5-to-v6 migration did not preserve historical missing saved content catalogs while keeping restore policy Runtime-owned.");

        RegressionAssert.True(
            !runtimeV5OldSlotInspectionResult.Success
            && runtimeV5OldSlotInspectionResult.Compatibility.Status == RuntimeSaveSlotCompatibilityStatus.MigrationRequired
            && runtimeV5OldSlotInspectionResult.Compatibility.RequiresMigration
            && runtimeV5OldSlotInspectionResult.MigrationPlan.CanMigrate
            && runtimeV5OldSlotInspectionResult.MigrationPlan.RequiredTransforms.SequenceEqual(runtimeV5OldSlotRequiredTransforms)
            && runtimeV5OldSlotMigrationResult.Success
            && runtimeV5OldSlotMigrationResult.MigrationApplied
            && runtimeV5OldSlotMigrationResult.AppliedTransforms.SequenceEqual(runtimeV5OldSlotRequiredTransforms)
            && runtimeV5OldSlotMigrationResult.MigrationIssues.Length == 0
            && runtimeV5OldSlotMigratedInspectionResult.Success
            && runtimeV5OldSlotMigratedInspectionResult.Compatibility.Status == RuntimeSaveSlotCompatibilityStatus.Compatible
            && runtimeV5OldSlotMigratedInspectionResult.Manifest.HasValue
            && runtimeV5OldSlotMigratedInspectionResult.Manifest.Value.SlotFormatVersion == RuntimeSaveSlotFormat.CurrentVersion
            && runtimeV5OldSlotMigratedInspectionResult.Manifest.Value.RuntimeSnapshotFormatVersion == RuntimeSaveFormat.CurrentVersion,
            "Runtime migration did not apply combined runtime snapshot and slot manifest transforms in the Runtime-owned execution order.");

        var validDocumentResult = pendingRuntime.ValidateSaveSnapshotDocument(pendingDocumentRoundTrip);
        var tamperedDocument = pendingDocumentRoundTrip with
        {
            Manifest = pendingDocumentRoundTrip.Manifest with
            {
                Checkpoint = pendingDocumentRoundTrip.Manifest.Checkpoint with
                {
                    PendingCommandLogHash = "tampered"
                }
            }
        };
        var tamperedDocumentResult = pendingRuntime.ValidateSaveSnapshotDocument(tamperedDocument);
        var tamperedRngDocument = pendingDocumentRoundTrip with
        {
            RngStreams = new[]
            {
                new RuntimeSaveRngStreamRecordData("tampered.rng", 1, 2, 3, 4)
            }
        };
        var tamperedRngDocumentResult = pendingRuntime.ValidateSaveSnapshotDocument(tamperedRngDocument);
        var duplicateManifestSectionDocument = pendingDocumentRoundTrip with
        {
            Manifest = pendingDocumentRoundTrip.Manifest with
            {
                Sections = pendingDocumentRoundTrip.Manifest.Sections
                    .Concat(new[] { pendingDocumentRoundTrip.Manifest.Sections[0] })
                    .ToArray()
            }
        };
        var duplicateManifestSectionResult = pendingRuntime.ValidateSaveSnapshotDocument(duplicateManifestSectionDocument);
        var unknownManifestSectionDocument = pendingDocumentRoundTrip with
        {
            Manifest = pendingDocumentRoundTrip.Manifest with
            {
                Sections = pendingDocumentRoundTrip.Manifest.Sections
                    .Concat(new[]
                    {
                        new RuntimeSaveManifestSectionData(
                            "external.unrecognized",
                            Present: true,
                            Hash: "unsupported",
                            RequiredForFortressMode: false,
                            RecordCount: 0)
                    })
                    .ToArray()
            }
        };
        var unknownManifestSectionResult = pendingRuntime.ValidateSaveSnapshotDocument(unknownManifestSectionDocument);
        var negativeManifestSectionCountDocument = pendingDocumentRoundTrip with
        {
            Manifest = pendingDocumentRoundTrip.Manifest with
            {
                Sections = pendingDocumentRoundTrip.Manifest.Sections
                    .Select(section => section.Name == "rng"
                        ? section with { RecordCount = -1 }
                        : section)
                    .ToArray()
            }
        };
        var negativeManifestSectionCountResult = pendingRuntime.ValidateSaveSnapshotDocument(negativeManifestSectionCountDocument);
        var absentManifestSectionWithMetadataDocument = pendingDocumentRoundTrip with
        {
            Manifest = pendingDocumentRoundTrip.Manifest with
            {
                Sections = pendingDocumentRoundTrip.Manifest.Sections
                    .Select(section => section.Name == "jobs.mining"
                        ? section with { Present = false, Hash = "stale", RecordCount = 0 }
                        : section)
                    .ToArray()
            }
        };
        var absentManifestSectionWithMetadataResult = pendingRuntime.ValidateSaveSnapshotDocument(absentManifestSectionWithMetadataDocument);
        var missingKnownManifestSectionDocument = pendingDocumentRoundTrip with
        {
            Manifest = pendingDocumentRoundTrip.Manifest with
            {
                Sections = pendingDocumentRoundTrip.Manifest.Sections
                    .Where(section => section.Name != "jobs.craft")
                    .ToArray()
            }
        };
        var missingKnownManifestSectionResult = pendingRuntime.ValidateSaveSnapshotDocument(missingKnownManifestSectionDocument);
        var nonEmptyJobStateDocument = pendingDocumentRoundTrip with
        {
            MiningJobs = null,
            Manifest = pendingDocumentRoundTrip.Manifest with
            {
                Sections = pendingDocumentRoundTrip.Manifest.Sections
                    .Select(section => section.Name == "jobs.mining"
                        ? section with { RecordCount = 1 }
                        : section)
                    .ToArray()
            }
        };

        var restoreRuntime = FortressRuntimeSessionFactory.CreateFull(
            AppContext.BaseDirectory,
            strictContent: false,
            contentWarningsAsErrors: false);
        restoreRuntime.InitializeWorld(sizeInChunks: 2, maxZ: 2);
        var restoreResult = restoreRuntime.RestorePendingCommandsFromSaveSnapshotDocument(pendingDocumentRoundTrip);
        var directoryRestoreRuntime = FortressRuntimeSessionFactory.CreateFull(
            AppContext.BaseDirectory,
            strictContent: false,
            contentWarningsAsErrors: false);
        directoryRestoreRuntime.InitializeWorld(sizeInChunks: 2, maxZ: 2);
        var directoryRestoreResult = directoryRestoreRuntime.RestorePendingCommandsFromSaveSnapshotDirectory(documentStoreDirectory);
        var directoryWorldRestoreRuntime = FortressRuntimeSessionFactory.CreateFull(
            AppContext.BaseDirectory,
            strictContent: false,
            contentWarningsAsErrors: false);
        var directoryWorldRestoreResult = directoryWorldRestoreRuntime.RestoreWorldFromSaveSnapshotDirectory(documentStoreDirectory);
        var directoryFullRestoreRuntime = FortressRuntimeSessionFactory.CreateFull(
            AppContext.BaseDirectory,
            strictContent: false,
            contentWarningsAsErrors: false);
        var directoryFullRestoreResult = directoryFullRestoreRuntime.RestoreFullFromSaveSnapshotDirectory(documentStoreDirectory);
        Directory.Delete(documentStoreDirectory, recursive: true);
        var missingDirectoryValidationResult = pendingRuntime.ValidateSaveSnapshotDirectory(documentStoreDirectory);
        var missingDirectoryWorldRestoreResult = pendingRuntime.RestoreWorldFromSaveSnapshotDirectory(documentStoreDirectory);
        var missingDirectoryFullRestoreResult = pendingRuntime.RestoreFullFromSaveSnapshotDirectory(documentStoreDirectory);
        var documentWorldRestoreRuntime = FortressRuntimeSessionFactory.CreateFull(
            AppContext.BaseDirectory,
            strictContent: false,
            contentWarningsAsErrors: false);
        var documentWorldRestoreResult = documentWorldRestoreRuntime.RestoreWorldFromSaveSnapshotDocument(pendingDocumentRoundTrip);
        var restoredWorldDocument = documentWorldRestoreRuntime.CreateSaveSnapshotDocumentData();
        var documentFullRestoreRuntime = FortressRuntimeSessionFactory.CreateFull(
            AppContext.BaseDirectory,
            strictContent: false,
            contentWarningsAsErrors: false);
        var documentFullRestoreResult = documentFullRestoreRuntime.RestoreFullFromSaveSnapshotDocument(pendingDocumentRoundTrip);
        var nonEmptyJobStateFullRestoreRuntime = FortressRuntimeSessionFactory.CreateFull(
            AppContext.BaseDirectory,
            strictContent: false,
            contentWarningsAsErrors: false);
        var nonEmptyJobStateFullRestoreResult = nonEmptyJobStateFullRestoreRuntime.RestoreFullFromSaveSnapshotDocument(nonEmptyJobStateDocument);
        var contentMismatchPendingRestoreRuntime = FortressRuntimeSessionFactory.CreateFull(
            AppContext.BaseDirectory,
            strictContent: false,
            contentWarningsAsErrors: false);
        contentMismatchPendingRestoreRuntime.InitializeWorld(sizeInChunks: 2, maxZ: 2);
        var contentMismatchPendingRestoreResult = contentMismatchPendingRestoreRuntime.RestorePendingCommandsFromSaveSnapshotDocument(contentMismatchDocument);
        var contentMismatchWorldRestoreRuntime = FortressRuntimeSessionFactory.CreateFull(
            AppContext.BaseDirectory,
            strictContent: false,
            contentWarningsAsErrors: false);
        var contentMismatchWorldRestoreResult = contentMismatchWorldRestoreRuntime.RestoreWorldFromSaveSnapshotDocument(contentMismatchDocument);
        var contentMismatchFullRestoreRuntime = FortressRuntimeSessionFactory.CreateFull(
            AppContext.BaseDirectory,
            strictContent: false,
            contentWarningsAsErrors: false);
        var contentMismatchFullRestoreResult = contentMismatchFullRestoreRuntime.RestoreFullFromSaveSnapshotDocument(contentMismatchDocument);
        var invalidTransportJobs = new RuntimeSaveTransportJobsData(
            IntakeCapHint: null,
            MaxActiveCapHint: null,
            ReserveSlotsHint: 0,
            PendingRequests: Array.Empty<RuntimeSaveTransportRequestData>(),
            ActiveJobs: new[]
            {
                new RuntimeSaveTransportActiveJobData(
                    Order: 0,
                    CreatureId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    ItemId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    DestinationX: 0,
                    DestinationY: 0,
                    DestinationZ: 0,
                    Stage: (int)JobStage.ToItem,
                    Quantity: 1,
                    InvalidReplanCount: 0,
                    Reason: (int)TransportReason.ToStockpile)
            },
            BacklogEntries: Array.Empty<RuntimeSaveTransportBacklogEntryData>());
        var invalidTransportHash = RuntimeSaveSnapshotDocumentTransportMapper.BuildReplayHash(invalidTransportJobs);
        var invalidTransportCount = RuntimeSaveSnapshotDocumentTransportMapper.CountRecords(invalidTransportJobs);
        var invalidTransportDocument = pendingDocumentRoundTrip with
        {
            TransportJobs = invalidTransportJobs,
            Manifest = pendingDocumentRoundTrip.Manifest with
            {
                Checkpoint = pendingDocumentRoundTrip.Manifest.Checkpoint with
                {
                    TransportHash = invalidTransportHash,
                    TransportRecordCount = invalidTransportCount
                },
                Sections = pendingDocumentRoundTrip.Manifest.Sections
                    .Select(section => section.Name == RuntimeSaveManifestSections.JobsTransport
                        ? section with
                        {
                            Present = true,
                            Hash = invalidTransportHash,
                            RecordCount = invalidTransportCount
                        }
                        : section)
                    .ToArray()
            }
        };
        var invalidTransportDocumentValidation = pendingRuntime.ValidateSaveSnapshotDocument(invalidTransportDocument);
        var invalidTransportFullRestoreRuntime = FortressRuntimeSessionFactory.CreateFull(
            AppContext.BaseDirectory,
            strictContent: false,
            contentWarningsAsErrors: false);
        invalidTransportFullRestoreRuntime.InitializeWorld(sizeInChunks: 2, maxZ: 2);
        invalidTransportFullRestoreRuntime.QueueHaulOrder(new RuntimeRect(4, 4, 1, 1), z: 0, priority: 7);
        var invalidTransportRestoreBeforeHash = invalidTransportFullRestoreRuntime.GetReplayCheckpointHash();
        var invalidTransportFullRestoreResult = invalidTransportFullRestoreRuntime.RestoreFullFromSaveSnapshotDocument(invalidTransportDocument);
        var invalidTransportRestoreAfterHash = invalidTransportFullRestoreRuntime.GetReplayCheckpointHash();
        var invalidTransportRestoreAfterDocument = invalidTransportFullRestoreRuntime.CreateSaveSnapshotDocumentData();
        restoreRuntime.QueueHaulOrder(new RuntimeRect(2, 2, 1, 1), z: 0, priority: 6);
        var restoredRuntimeDocument = restoreRuntime.CreateSaveSnapshotDocumentData();
        var restoredSequences = restoredRuntimeDocument.PendingCommandRecords
            .Select(record => record.CommandIdentitySequence)
            .ToArray();

        RegressionAssert.True(
            validDocumentResult.Success
            && !tamperedDocumentResult.Success
            && tamperedDocumentResult.Issues.Any(issue => issue.Section == "commands.pending")
            && !tamperedRngDocumentResult.Success
            && tamperedRngDocumentResult.Issues.Any(issue => issue.Section == "rng")
            && !duplicateManifestSectionResult.Success
            && duplicateManifestSectionResult.Issues.Any(issue => issue.Section == "manifest.sections")
            && !unknownManifestSectionResult.Success
            && unknownManifestSectionResult.Issues.Any(issue => issue.Section == "manifest.sections")
            && !negativeManifestSectionCountResult.Success
            && negativeManifestSectionCountResult.Issues.Any(issue => issue.Section == "manifest.sections")
            && !absentManifestSectionWithMetadataResult.Success
            && absentManifestSectionWithMetadataResult.Issues.Count(issue => issue.Section == "manifest.sections") >= 2
            && !missingKnownManifestSectionResult.Success
            && missingKnownManifestSectionResult.Issues.Any(issue => issue.Section == "manifest.sections")
            && restoreResult.Success
            && restoreResult.Validation.Success
            && restoreResult.PendingRecordCount == 1
            && restoreResult.RestoredCommandCount == 1
            && restoreResult.MaxCommandIdentitySequence == 1
            && directoryRestoreResult.Success
            && directoryRestoreResult.Validation.Success
            && directoryRestoreResult.RestoredCommandCount == 1
            && directoryWorldRestoreResult.Success
            && directoryWorldRestoreResult.Validation.Success
            && directoryWorldRestoreResult.SavedWorldHash == pendingDocumentRoundTrip.WorldPayload?.ReplayHash
            && directoryWorldRestoreResult.RestoredWorldHash == pendingDocumentRoundTrip.WorldPayload?.ReplayHash
            && directoryFullRestoreResult.Validation.Success
            && directoryFullRestoreResult.Success
            && directoryFullRestoreResult.RestoredWorldHash == pendingDocumentRoundTrip.WorldPayload?.ReplayHash
            && directoryFullRestoreResult.RestoredRngStreamCount == pendingDocumentRoundTrip.RngStreams.Length
            && directoryFullRestoreResult.RestoredCommandCount == 1
            && !missingDirectoryValidationResult.Success
            && missingDirectoryValidationResult.Issues.Any(issue => issue.Section == "snapshot.document")
            && !missingDirectoryWorldRestoreResult.Success
            && missingDirectoryWorldRestoreResult.Validation.Issues.Any(issue => issue.Section == "snapshot.document")
            && !missingDirectoryFullRestoreResult.Success
            && missingDirectoryFullRestoreResult.Validation.Issues.Any(issue => issue.Section == "snapshot.document")
            && documentWorldRestoreResult.Success
            && documentWorldRestoreResult.RestoredWorldHash == pendingDocumentRoundTrip.WorldPayload?.ReplayHash
            && restoredWorldDocument.Manifest.Checkpoint.WorldHash == pendingDocumentRoundTrip.WorldPayload?.ReplayHash
            && documentFullRestoreResult.Success
            && documentFullRestoreResult.Validation.Success
            && documentFullRestoreResult.RestoredWorldHash == pendingDocumentRoundTrip.WorldPayload?.ReplayHash
            && documentFullRestoreResult.RestoredRngStreamCount == pendingDocumentRoundTrip.RngStreams.Length
            && documentFullRestoreResult.RestoredCommandCount == 1
            && !nonEmptyJobStateFullRestoreResult.Success
            && !nonEmptyJobStateFullRestoreResult.Validation.Success
            && nonEmptyJobStateFullRestoreResult.Validation.Issues.Any(issue =>
                issue.Section == "jobs.mining"
                && issue.Message.Contains("no mining job payload", StringComparison.Ordinal))
            && !contentMismatchPendingRestoreResult.Success
            && contentMismatchPendingRestoreResult.Validation.Success
            && contentMismatchPendingRestoreResult.PendingRecordCount == 1
            && contentMismatchPendingRestoreResult.RestoreIssues.Any(issue => issue.Section == "slot.content")
            && !contentMismatchWorldRestoreResult.Success
            && contentMismatchWorldRestoreResult.Validation.Success
            && contentMismatchWorldRestoreResult.RestoreIssues.Any(issue => issue.Section == "slot.content")
            && !contentMismatchFullRestoreResult.Success
            && contentMismatchFullRestoreResult.Validation.Success
            && contentMismatchFullRestoreResult.RestoreIssues.Any(issue => issue.Section == "slot.content")
            && invalidTransportDocumentValidation.Success
            && !invalidTransportFullRestoreResult.Success
            && invalidTransportFullRestoreResult.Validation.Success
            && invalidTransportFullRestoreResult.RestoreIssues.Any(issue =>
                issue.Section == RuntimeSaveManifestSections.JobsTransport
                && issue.Message.Contains("missing creature", StringComparison.Ordinal))
            && invalidTransportRestoreAfterHash == invalidTransportRestoreBeforeHash
            && invalidTransportRestoreAfterDocument.PendingCommandRecords.Length == 1
            && restoredRuntimeDocument.PendingCommandRecords.Length == 2
            && restoredSequences.SequenceEqual(new long?[] { 1, 2 }),
            "Runtime save snapshot document validation/restore did not restore pending commands or advance command identity.");

        var rejectedInvalidDocument = false;
        try
        {
            _ = RuntimeSaveSnapshotDocumentCodec.Serialize(new RuntimeSaveSnapshotDocumentData(
                pendingDocument.Manifest,
                pendingDocument.WorldPayload,
                pendingDocument.MiningJobs,
                pendingDocument.TransportJobs,
                pendingDocument.CraftJobs,
                pendingDocument.RngStreams,
                Array.Empty<RuntimeSaveCommandRecordData>(),
                new[]
                {
                    pendingCommand with { PayloadBase64 = "not base64" }
                }));
        }
        catch (InvalidDataException)
        {
            rejectedInvalidDocument = true;
        }

        RegressionAssert.True(
            rejectedInvalidDocument,
            "Runtime save snapshot document codec did not reject malformed command payloads.");

        Console.WriteLine("[PASS] Runtime replay checkpoint hash");
    }

    private static void TestDiffLog()
    {
        var diffLog = new DiffLog();

        diffLog.AddOp(new DiffOp(DiffOpType.SetTerrain, new DiffTarget(0, 100), "test", 1));
        diffLog.AddOp(new DiffOp(DiffOpType.SetFluid, new DiffTarget(0, 100), "test", 2));

        var merged = diffLog.MergeAndSort();
        var entityId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var target = DiffTargetEncoding.ForWorldCell(
            35,
            66,
            7,
            entityId);
        var decodedChunk = DiffTargetEncoding.DecodeChunkId(target.ChunkId);
        var decodedLocal = DiffTargetEncoding.DecodeLocalIndex(target.LocalIndex);
        bool worldCellTargetEncodes = WorldCellTargetEncoding.TryEncode(35, 66, 7, out var worldCellTarget);
        var encodedTarget = worldCellTarget.ToDiffTarget(entityId);
        worldCellTargetEncodes = worldCellTargetEncodes
            && worldCellTarget.ChunkKey.Equals(new ChunkKey(1, 2, 7))
            && worldCellTarget.LocalIndex == Chunk.LocalIndex(3, 2)
            && encodedTarget.ChunkId == target.ChunkId
            && encodedTarget.LocalIndex == target.LocalIndex
            && encodedTarget.EntityId == target.EntityId
            && encodedTarget.EntityKey == target.EntityKey
            && encodedTarget.HasEntityKey;
        bool encodingRoundTrips = decodedChunk == (1, 2, 7)
            && decodedLocal == (3, 2)
            && target.EntityId == DiffTargetEncoding.SignedEntityId(entityId)
            && target.EntityKey == DiffTargetEncoding.EntityKey(entityId)
            && target.HasEntityKey
            && worldCellTargetEncodes;

        var conflictLog = new DiffLog();
        var conflictTarget = new DiffTarget(4, 7);
        conflictLog.AddOp(new DiffOp(
            DiffOpType.SetTerrain,
            conflictTarget,
            "System.Later",
            priority: 10,
            args: 1,
            systemOrder: 200));
        conflictLog.AddOp(new DiffOp(
            DiffOpType.SetTerrain,
            conflictTarget,
            "System.Earlier",
            priority: 10,
            args: 2,
            systemOrder: 100));
        var conflictMerged = conflictLog.MergeAndSort();
        bool systemOrderWins = conflictMerged.Count == 1
            && conflictMerged[0].SystemOrder == 100
            && conflictMerged[0].Args == 2;

        var localSeqLog = new DiffLog();
        localSeqLog.AddOp(new DiffOp(
            DiffOpType.SetTerrain,
            conflictTarget,
            "System.Local",
            priority: 10,
            args: 1,
            systemOrder: 100,
            localSeq: 1));
        localSeqLog.AddOp(new DiffOp(
            DiffOpType.SetTerrain,
            conflictTarget,
            "System.Local",
            priority: 10,
            args: 2,
            systemOrder: 100,
            localSeq: 2));
        var localSeqMerged = localSeqLog.MergeAndSort();
        bool localSeqWins = localSeqMerged.Count == 1
            && localSeqMerged[0].LocalSeq == 2
            && localSeqMerged[0].Args == 2;

        RegressionAssert.True(
            merged.Count == 2 && encodingRoundTrips && systemOrderWins && localSeqWins,
            "DiffLog merge, explicit system order, local sequence, or target encoding round trip failed.");

        Console.WriteLine("[PASS] DiffLog");
    }

    private static void TestTypedCommandDiffOrderingPolicy()
    {
        var rect = new SadRogue.Primitives.Rectangle(1, 1, 2, 2);

        var orderDiffLog = new OrderDiffLog();
        orderDiffLog.AddHaul(rect, z: 0, priority: 1, createdTick: 10, systemId: "test");
        orderDiffLog.AddMining(rect, z: 0, priority: 99, createdTick: 10, systemId: "test");
        var orderDiffs = orderDiffLog.MergeAndSort();

        var zoneDiffLog = new ZoneDiffLog();
        zoneDiffLog.AddDeleteZone(zoneId: 42, priority: 1, systemId: "test");
        zoneDiffLog.AddCreateZone("meeting", "Meeting", rect, z: 0, createdTick: 10, priority: 99, systemId: "test");
        var zoneDiffs = zoneDiffLog.MergeAndSort();

        var workshopDiffLog = new WorkshopDiffLog();
        var workshopId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-000000000001");
        workshopDiffLog.SetWorkerSlots(workshopId, workerSlots: 1, priority: 1, systemId: "test");
        workshopDiffLog.AddRecipe(workshopId, "recipe.high", "High Priority Recipe", currentTick: 10, priority: 99, systemId: "test");
        var workshopDiffs = workshopDiffLog.MergeAndSort();

        var itemsDiffLog = new ItemsDiffLog();
        var targetChunk = new ChunkKey(0, 0, 0);
        itemsDiffLog.Add(ItemsDiffOp.AddItem, targetChunk, localIndex: 7, itemId: "item.low", quantity: 1, priority: 1, systemId: "test");
        itemsDiffLog.Add(ItemsDiffOp.AddItem, targetChunk, localIndex: 7, itemId: "item.high", quantity: 1, priority: 99, systemId: "test");
        var itemDiffs = itemsDiffLog.MergeAndSort();

        var stockpileDiffLog = new StockpileDiffLog();
        var stockpileCells = new Dictionary<ChunkKey, IReadOnlyList<int>>
        {
            [targetChunk] = new[] { 7 }
        };
        stockpileDiffLog.AddCreateZone("Low", targetChunk, stockpileCells, createdTick: 10, priority: 1, systemId: "test");
        stockpileDiffLog.AddCreateZone("High", targetChunk, stockpileCells, createdTick: 10, priority: 99, systemId: "test");
        var stockpileDiffs = stockpileDiffLog.MergeAndSort();
        var firstStockpileCreate = stockpileDiffs[0].Data as StockpileCreateZoneData;

        RegressionAssert.True(
            orderDiffs[0].Op == OrderDiffOp.Haul
            && orderDiffs[1].Op == OrderDiffOp.Mining
            && zoneDiffs[0].Op == ZoneDiffOp.DeleteZone
            && zoneDiffs[1].Op == ZoneDiffOp.CreateZone
            && workshopDiffs[0].Op == WorkshopDiffOp.SetWorkerSlots
            && workshopDiffs[1].Op == WorkshopDiffOp.AddRecipe
            && itemDiffs[0].ItemId == "item.high"
            && firstStockpileCreate?.Name == "High",
            "Typed command diff ordering policy regressed.");

        Console.WriteLine("[PASS] Typed command diff ordering policy");
    }

    private static void TestStockpileMessageDrainSortKeyIsStable()
    {
        var sourceA = new ChunkKey(1, 2, 3);
        var sourceACopy = new ChunkKey(1, 2, 3);
        var sourceB = new ChunkKey(2, 2, 3);
        var dest = new ChunkKey(4, 5, 3);

        var first = StockpileMessage.HaulJobAssigned(
            jobId: 10,
            zoneId: 20,
            itemHandle: 30,
            quantity: 1,
            sourceChunk: sourceA,
            destChunk: dest,
            cellIndex: 40,
            localSeq: 1);
        var repeat = StockpileMessage.HaulJobAssigned(
            jobId: 10,
            zoneId: 20,
            itemHandle: 30,
            quantity: 1,
            sourceChunk: sourceACopy,
            destChunk: dest,
            cellIndex: 40,
            localSeq: 1);
        var differentSource = StockpileMessage.HaulJobAssigned(
            jobId: 10,
            zoneId: 20,
            itemHandle: 30,
            quantity: 1,
            sourceChunk: sourceB,
            destChunk: dest,
            cellIndex: 40,
            localSeq: 1);
        var laterLocalSequence = StockpileMessage.HaulJobAssigned(
            jobId: 10,
            zoneId: 20,
            itemHandle: 30,
            quantity: 1,
            sourceChunk: sourceA,
            destChunk: dest,
            cellIndex: 40,
            localSeq: 2);

        RegressionAssert.True(
            first.GetDrainSortKey(tick: 123) == repeat.GetDrainSortKey(tick: 123)
            && first.GetDrainSortKey(tick: 123) != differentSource.GetDrainSortKey(tick: 123)
            && first.GetDrainSortKey(tick: 123) < laterLocalSequence.GetDrainSortKey(tick: 123),
            "Stockpile mailbox drain sort key is not stable by tick/source chunk/local sequence.");

        Console.WriteLine("[PASS] Stockpile mailbox drain sort key");
    }

    private static void TestStockpileDiffOrderingUsesEntityKeys()
    {
        var chunk = new ChunkKey(0, 0, 0);
        var highKey = new StockpileDiff
        {
            Op = StockpileDiffOp.PlaceItem,
            TargetChunk = chunk,
            ZoneId = 1,
            CellIndex = 2,
            ItemHandle = 200,
            Priority = 100,
            SystemId = "z.system",
            LocalSeq = 0
        };
        var lowKey = highKey with
        {
            ItemHandle = 100,
            SystemId = "z.system",
            LocalSeq = 1
        };
        var earlierSystem = highKey with
        {
            ItemHandle = 200,
            SystemId = "a.system",
            LocalSeq = 2
        };
        var diffs = new List<StockpileDiff> { highKey, lowKey, earlierSystem };

        diffs.Sort(StockpileDiff.CompareDeterministic);

        RegressionAssert.True(
            diffs[0].ItemHandle == 100
            && diffs[1].SystemId == "a.system"
            && diffs[2].LocalSeq == 0,
            "Stockpile diff ordering did not use item entity key and system id before local sequence.");

        Console.WriteLine("[PASS] Stockpile diff ordering uses entity keys");
    }

    private static void TestMiningRectanglesIncludeSingleCellMaxExtent()
    {
        var world = new World(2, 2);
        var cell = new SadRogue.Primitives.Point(1, 1);
        var rect = new SadRogue.Primitives.Rectangle(cell.X, cell.Y, 1, 1);
        world.SetTile(cell.X, cell.Y, 0, new TileBase(0, (ushort)TerrainKind.SolidWall, 0, 0, 0, 0, 1), 0);

        int eligible = MiningOrderRules.CountEligible(world, rect, zMin: 0, zMax: 0, MiningAction.Dig);
        world.Orders.EnqueueMining(rect, z: 0, priority: 50, createdTick: 0);

        var mining = new MiningSystem(world, world.Orders, maxPerTick: 8);
        mining.ReadTick(0);
        mining.WriteTick(0);

        var planned = new List<MiningSystem.PlannedDig>();
        int plannedCount = mining.DequeuePlannedDigs(max: 8, planned);

        RegressionAssert.True(
            eligible == 1
            && plannedCount == 1
            && planned[0].Cell == cell,
            "Mining one-cell rectangles did not include SadRogue inclusive MaxExtent bounds.");

        Console.WriteLine("[PASS] Mining rectangle inclusive bounds");
    }

    private static void TestMiningZRangeMapper()
    {
        var stairwell = MiningZRangeMapper.ToSimulationRange(25, 32, MiningAction.DigStairwell);
        var ordinaryDig = MiningZRangeMapper.ToSimulationRange(25, 32, MiningAction.Dig);
        var singleLayerStairwell = MiningZRangeMapper.ToSimulationRange(25, 25, MiningAction.DigStairwell);

        RegressionAssert.True(
            stairwell.WasInverted
            && stairwell.LayerCount == 7
            && stairwell.ZMin == 18
            && stairwell.ZMax == 25
            && !ordinaryDig.WasInverted
            && ordinaryDig.ZMin == 25
            && ordinaryDig.ZMax == 32
            && !singleLayerStairwell.WasInverted
            && singleLayerStairwell.ZMin == 25
            && singleLayerStairwell.ZMax == 25,
            "Mining stairwell UI Z ranges must map to internal downward ranges while ordinary mining preserves Z bounds.");

        Console.WriteLine("[PASS] Mining Z range mapper");
    }

    private static void TestStockpileFilterUsesItemProjection()
    {
        var woodStack = new HumanFortress.Simulation.Stockpile.ItemStackRef(
            1,
            "core_item_log",
            new[] { "wood", "raw" },
            "core_mat_wood_oak");

        var stoneStack = new HumanFortress.Simulation.Stockpile.ItemStackRef(
            2,
            "core_item_stone",
            new[] { "stone", "raw" },
            "core_mat_stone_granite");

        var defaultFilter = new StockpileFilter();
        var woodTagFilter = new StockpileFilter
        {
            Tags = ImmutableHashSet.Create(StringComparer.Ordinal, "wood")
        };
        var logIdFilter = new StockpileFilter
        {
            ItemIds = ImmutableHashSet.Create(StringComparer.Ordinal, "core_item_log")
        };
        var oakMaterialFilter = new StockpileFilter
        {
            Materials = ImmutableHashSet.Create(StringComparer.Ordinal, "core_mat_wood_oak")
        };
        var woodBlacklist = new StockpileFilter
        {
            Mode = FilterMode.Blacklist,
            Tags = ImmutableHashSet.Create(StringComparer.Ordinal, "wood")
        };
        var presetDefinitions = StockpilePresetLoader.LoadJson(
            "[{\"id\":\"wood\",\"name\":\"Wood Storage\",\"mode\":\"Whitelist\",\"tags\":[\"wood\"],\"priority\":2}]",
            "test stockpile presets");
        var presetCatalog = FortressRuntimeStockpilePresetCatalog.FromDefinitions(
            presetDefinitions,
            "test stockpile presets");
        var contentPresetFilter = presetCatalog.Resolve("wood").CreateFilter();

        RegressionAssert.True(defaultFilter.Accepts(woodStack), "Default stockpile filter did not accept a valid item handle.");
        RegressionAssert.True(woodTagFilter.Accepts(woodStack), "Stockpile tag filter did not match projected item tags.");
        RegressionAssert.True(logIdFilter.Accepts(woodStack), "Stockpile item id filter did not match projected item definition id.");
        RegressionAssert.True(oakMaterialFilter.Accepts(woodStack), "Stockpile material filter did not match projected material id.");
        RegressionAssert.True(!woodBlacklist.Accepts(woodStack), "Stockpile blacklist did not reject matching projected item tags.");
        RegressionAssert.True(woodBlacklist.Accepts(stoneStack), "Stockpile blacklist rejected a non-matching projected item.");
        RegressionAssert.True(
            presetCatalog.Resolve("wood").Priority == 2
            && contentPresetFilter.Accepts(woodStack)
            && !contentPresetFilter.Accepts(stoneStack),
            "Content-loaded stockpile preset did not map to the Runtime stockpile filter catalog.");

        Console.WriteLine("[PASS] Stockpile filter item projection");
    }

    private static void TestStockpileDataIndexUpdatesAreIdempotent()
    {
        var data = new ChunkStockpileData();
        var chunkKey = new ChunkKey(0, 0, 0);
        const int zoneId = 7;
        const ulong itemHandle = 42;
        const int cellIndex = 3;
        var tags = new List<string> { "wood", "raw" };

        data.CreateOrUpdateShard(zoneId, chunkKey);
        data.AddCellsToZone(zoneId, new[] { cellIndex });

        data.OnItemRemoved(itemHandle, cellIndex, zoneId, tags);
        data.OnItemPlaced(itemHandle, cellIndex, zoneId, tags);
        data.OnItemPlaced(itemHandle, cellIndex, zoneId, tags);

        var shard = data.GetShard(zoneId)
            ?? throw new InvalidOperationException("Stockpile shard should exist after CreateOrUpdateShard.");
        RegressionAssert.True(
            data.GetItemsInZone(zoneId).Count() == 1
            && data.GetItemsByTag("wood").Count() == 1
            && shard.UsedSlots == 1,
            "Stockpile item indexes counted duplicate placements.");

        data.OnItemRemoved(itemHandle, cellIndex, zoneId, tags);
        data.OnItemRemoved(itemHandle, cellIndex, zoneId, tags);

        RegressionAssert.True(
            !data.GetItemsInZone(zoneId).Any()
            && !data.GetItemsByTag("wood").Any()
            && shard.UsedSlots == 0,
            "Stockpile item indexes did not handle repeated removals safely.");

        Console.WriteLine("[PASS] Stockpile item index idempotence");
    }

    private static void TestStockpileDataUsesEntityKeysForItemIndexes()
    {
        var data = new ChunkStockpileData();
        var chunkKey = new ChunkKey(0, 0, 0);
        const int zoneId = 7;
        const int cellIndex = 3;
        var tags = new List<string> { "wood" };
        var itemA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        var itemB = Guid.Parse("aaaaaaaa-0001-0000-0000-000000000002");
        ulong itemAKey = DiffTargetEncoding.EntityKey(itemA);
        ulong itemBKey = DiffTargetEncoding.EntityKey(itemB);

        RegressionAssert.True(
            DiffTargetEncoding.SignedEntityId(itemA) == DiffTargetEncoding.SignedEntityId(itemB)
            && itemAKey != itemBKey,
            "Stockpile entity-key collision test setup no longer produces a legacy 32-bit id collision.");

        data.CreateOrUpdateShard(zoneId, chunkKey);
        data.AddCellsToZone(zoneId, new[] { cellIndex });
        data.OnItemPlaced(itemAKey, cellIndex, zoneId, tags);
        data.OnItemPlaced(itemBKey, cellIndex, zoneId, tags);

        var indexedItems = data.GetItemsInZone(zoneId).OrderBy(static handle => handle).ToArray();
        RegressionAssert.True(
            indexedItems.SequenceEqual(new[] { itemAKey, itemBKey }.OrderBy(static handle => handle))
            && data.GetItemsByTag("wood").Count() == 2,
            "Stockpile item index collapsed two distinct item GUIDs that share the same legacy 32-bit entity id.");

        Console.WriteLine("[PASS] Stockpile item indexes use wider entity keys");
    }

    private static void TestZoneAndStockpileReadSnapshotsUseStableOrdering()
    {
        var chunkKey = new ChunkKey(0, 0, 0);

        var zoneManager = new ZoneManager();
        zoneManager.RegisterDefinition(new ZoneDefinitionData
        {
            Id = "zone_b",
            Category = "work",
            DisplayName = "B"
        });
        zoneManager.RegisterDefinition(new ZoneDefinitionData
        {
            Id = "zone_a",
            Category = "work",
            DisplayName = "A"
        });
        string[] definitionIds = zoneManager.GetAllDefinitions()
            .Select(static definition => definition.Id)
            .ToArray();

        var zoneData = new ChunkZoneData();
        zoneData.CreateOrUpdateShard(9, chunkKey);
        zoneData.CreateOrUpdateShard(3, chunkKey);
        int[] zoneShardIds = zoneData.GetAllShards()
            .Select(static shard => shard.ZoneId)
            .ToArray();
        int runtimeZoneId = zoneManager.CreateZone("zone_a", "Ordered Zone", chunkKey, currentTick: 0);
        var runtimeZone = zoneManager.GetZone(runtimeZoneId)
            ?? throw new InvalidOperationException("Expected ordered zone to exist.");
        runtimeZone.UpdateMemberChunks(new[]
        {
            new ChunkKey(1, 0, 1),
            new ChunkKey(1, 0, 0),
            new ChunkKey(0, 0, 0)
        });
        ChunkKey[] runtimeZoneMemberChunks = runtimeZone.GetMemberChunksSnapshot().ToArray();

        var stockpileManager = new StockpileManager();
        var filter = new WorldSaveStockpileFilterPayloadData(
            (int)FilterMode.Whitelist,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>());
        var restoreIssues = stockpileManager.RestoreZonesSnapshot(new[]
        {
            new WorldSaveStockpileZonePayloadData(
                2,
                "Second",
                new WorldSaveChunkKeyData(0, 0, 0),
                filter,
                1,
                100,
                70,
                90,
                1,
                0,
                new[]
                {
                    new WorldSaveChunkKeyData(1, 0, 1),
                    new WorldSaveChunkKeyData(1, 0, 0),
                    new WorldSaveChunkKeyData(0, 0, 0)
                }),
            new WorldSaveStockpileZonePayloadData(
                1,
                "First",
                new WorldSaveChunkKeyData(0, 0, 0),
                filter,
                1,
                100,
                70,
                90,
                1,
                0,
                Array.Empty<WorldSaveChunkKeyData>())
        });
        int[] stockpileZoneIds = stockpileManager.GetAllZones()
            .Select(static zone => zone.ZoneId)
            .ToArray();
        ChunkKey[] stockpileMemberChunks = stockpileManager.GetZone(2)?.GetMemberChunksSnapshot().ToArray()
            ?? throw new InvalidOperationException("Expected restored stockpile zone to exist.");

        var stockpileData = new ChunkStockpileData();
        stockpileData.CreateOrUpdateShard(5, chunkKey);
        stockpileData.CreateOrUpdateShard(2, chunkKey);
        int[] stockpileShardIds = stockpileData.GetAllShards()
            .Select(static shard => shard.ZoneId)
            .ToArray();

        var tags = new List<string> { "wood" };
        stockpileData.OnItemPlaced(10UL, cellIndex: 1, zoneId: 5, tags: tags);
        stockpileData.OnItemPlaced(4UL, cellIndex: 2, zoneId: 5, tags: tags);
        stockpileData.OnItemPlaced(7UL, cellIndex: 3, zoneId: 0, tags: tags);
        stockpileData.OnItemPlaced(1UL, cellIndex: 4, zoneId: 0, tags: tags);
        ulong[] zoneItemHandles = stockpileData.GetItemsInZone(5).ToArray();
        ulong[] tagItemHandles = stockpileData.GetItemsByTag("wood").ToArray();
        ulong[] looseItemHandles = stockpileData.GetLooseItems().ToArray();

        RegressionAssert.True(
            definitionIds.SequenceEqual(new[] { "zone_a", "zone_b" })
            && zoneShardIds.SequenceEqual(new[] { 3, 9 })
            && runtimeZoneMemberChunks.SequenceEqual(new[]
            {
                new ChunkKey(0, 0, 0),
                new ChunkKey(1, 0, 0),
                new ChunkKey(1, 0, 1)
            })
            && restoreIssues.Count == 0
            && stockpileZoneIds.SequenceEqual(new[] { 1, 2 })
            && stockpileMemberChunks.SequenceEqual(new[]
            {
                new ChunkKey(0, 0, 0),
                new ChunkKey(1, 0, 0),
                new ChunkKey(1, 0, 1)
            })
            && stockpileShardIds.SequenceEqual(new[] { 2, 5 })
            && zoneItemHandles.SequenceEqual(new[] { 4UL, 10UL })
            && tagItemHandles.SequenceEqual(new[] { 1UL, 4UL, 7UL, 10UL })
            && looseItemHandles.SequenceEqual(new[] { 1UL, 7UL }),
            "Zone and stockpile read snapshots did not return stable owner-defined order.");

        Console.WriteLine("[PASS] Zone and stockpile read snapshots use stable ordering");
    }

    private static void TestItemManagerEntityKeyLookupIndexStaysInSync()
    {
        var world = new World(2, 1);
        DefinitionCatalogTestSupport.LoadItems(world);
        var cell = new SadRogue.Primitives.Point(2, 2);
        world.SetTile(cell.X, cell.Y, 0, new TileBase(0, (ushort)TerrainKind.OpenWithFloor, 0, 0, 0, 0, 1), 0);

        var sourceId = world.Items.SpawnItem("core_item_log_oak", cell, 0, quantity: 3, currentTick: 0)
            ?? throw new InvalidOperationException("Expected source item to spawn.");
        ulong sourceKey = DiffTargetEncoding.EntityKey(sourceId);

        RegressionAssert.True(
            world.Items.GetInstanceByEntityKey(sourceKey)?.Guid == sourceId,
            "ItemManager entity-key index did not resolve a spawned item.");

        var splitId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        RegressionAssert.True(
            world.Items.SplitStackWithGuid(sourceId, takeCount: 1, newGuid: splitId) == splitId,
            "ItemManager split-stack setup failed.");
        ulong splitKey = DiffTargetEncoding.EntityKey(splitId);
        var mergeTarget = sourceId.CompareTo(splitId) <= 0 ? sourceId : splitId;
        var mergedAway = mergeTarget == sourceId ? splitId : sourceId;
        ulong mergeTargetKey = DiffTargetEncoding.EntityKey(mergeTarget);
        ulong mergedAwayKey = DiffTargetEncoding.EntityKey(mergedAway);

        RegressionAssert.True(
            world.Items.GetInstanceByEntityKey(splitKey)?.Guid == splitId,
            "ItemManager entity-key index did not register a split stack.");

        RegressionAssert.True(
            world.Items.MergeStacksAt(cell, 0) == 1
            && world.Items.GetInstanceByEntityKey(mergedAwayKey) == null
            && world.Items.GetInstanceByEntityKey(mergeTargetKey)?.Guid == mergeTarget
            && world.Items.GetInstanceByEntityKey(mergeTargetKey)?.StackCount == 3,
            "ItemManager merge-stack target or entity-key index was not deterministic.");

        RegressionAssert.True(
            world.Items.RemoveInstance(mergeTarget)
            && world.Items.GetInstanceByEntityKey(mergeTargetKey) == null,
            "ItemManager entity-key index did not unregister a removed item.");

        var itemA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        var itemB = Guid.Parse("aaaaaaaa-0001-0000-0000-000000000002");
        var restoreIssues = world.Items.RestoreItemsSnapshot(new[]
        {
            CreateSaveItemPayload(itemB, cell, 0),
            CreateSaveItemPayload(itemA, cell, 0)
        });

        RegressionAssert.True(
            restoreIssues.Count == 0
            && DiffTargetEncoding.EntityId(itemA) == DiffTargetEncoding.EntityId(itemB)
            && world.Items.GetInstanceByEntityId(DiffTargetEncoding.EntityId(itemA))?.Guid == itemA,
            "ItemManager legacy entity-id fallback did not resolve collisions deterministically.");

        Console.WriteLine("[PASS] ItemManager entity-key lookup index stays in sync");
    }

    private static WorldSaveItemPayloadData CreateSaveItemPayload(Guid id, SadRogue.Primitives.Point cell, int z)
    {
        return new WorldSaveItemPayloadData(
            id,
            "core_item_log_oak",
            null,
            1,
            new WorldSavePointData(cell.X, cell.Y),
            z,
            null,
            null,
            null,
            null,
            null,
            null,
            (int)UsePolicy.Public,
            false,
            Array.Empty<WorldSaveItemReservationTokenData>(),
            0,
            false,
            null,
            "normal",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            0);
    }

    private static void TestCreatureManagerEntityKeyLookupIndexStaysInSync()
    {
        var world = new World(2, 1);
        DefinitionCatalogTestSupport.LoadCreatures(world);
        var cell = new SadRogue.Primitives.Point(2, 2);
        world.SetTile(cell.X, cell.Y, 0, new TileBase(0, (ushort)TerrainKind.OpenWithFloor, 0, 0, 0, 0, 1), 0);

        var spawnedId = world.Creatures.SpawnCreature("core_race_dwarf", cell, 0, factionId: "player", currentTick: 0)
            ?? throw new InvalidOperationException("Expected test creature to spawn.");
        ulong spawnedKey = DiffTargetEncoding.EntityKey(spawnedId);

        RegressionAssert.True(
            world.Creatures.GetInstanceByEntityKey(spawnedKey)?.Guid == spawnedId,
            "CreatureManager entity-key index did not resolve a spawned creature.");

        var restoredId = Guid.Parse("bbbbbbbb-2222-3333-4444-555555555555");
        var restoreIssues = world.Creatures.RestoreCreaturesSnapshot(new[]
        {
            new WorldSaveCreaturePayloadData(
                restoredId,
                "core_race_dwarf",
                "player",
                new WorldSavePointData(3, 3),
                Z: 0,
                HP: 75,
                MaxHP: 100,
                SpawnedAtTick: 5)
        });
        ulong restoredKey = DiffTargetEncoding.EntityKey(restoredId);

        RegressionAssert.True(
            restoreIssues.Count == 0
            && world.Creatures.GetInstanceByEntityKey(spawnedKey) == null
            && world.Creatures.GetInstanceByEntityKey(restoredKey)?.Guid == restoredId,
            "CreatureManager entity-key index did not rebuild correctly after restore.");

        var creatureA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        var creatureB = Guid.Parse("aaaaaaaa-0001-0000-0000-000000000002");
        restoreIssues = world.Creatures.RestoreCreaturesSnapshot(new[]
        {
            new WorldSaveCreaturePayloadData(
                creatureB,
                "core_race_dwarf",
                "player",
                new WorldSavePointData(3, 3),
                Z: 0,
                HP: 100,
                MaxHP: 100,
                SpawnedAtTick: 0),
            new WorldSaveCreaturePayloadData(
                creatureA,
                "core_race_dwarf",
                "player",
                new WorldSavePointData(4, 4),
                Z: 0,
                HP: 100,
                MaxHP: 100,
                SpawnedAtTick: 0)
        });

        RegressionAssert.True(
            restoreIssues.Count == 0
            && DiffTargetEncoding.EntityId(creatureA) == DiffTargetEncoding.EntityId(creatureB)
            && world.Creatures.GetInstanceByEntityId(DiffTargetEncoding.EntityId(creatureA))?.Guid == creatureA,
            "CreatureManager legacy entity-id fallback did not resolve collisions deterministically.");

        Console.WriteLine("[PASS] CreatureManager entity-key lookup index stays in sync");
    }

    private static void TestTransportStockpileIndexEmitterUsesStockpileDiffs()
    {
        var world = new World(2, 2);
        DefinitionCatalogTestSupport.LoadItems(world);

        var source = new SadRogue.Primitives.Point(2, 2);
        var destination = new SadRogue.Primitives.Point(5, 5);
        world.SetTile(source.X, source.Y, 0, new TileBase(0, (ushort)TerrainKind.OpenWithFloor, 0, 0, 0, 0, 1), 0);
        world.SetTile(destination.X, destination.Y, 0, new TileBase(0, (ushort)TerrainKind.OpenWithFloor, 0, 0, 0, 0, 1), 0);

        var itemId = world.Items.SpawnItem("core_item_log_oak", source, 0, quantity: 1, currentTick: 0)
            ?? throw new InvalidOperationException("Expected test item to spawn.");
        ulong itemHandle = DiffTargetEncoding.EntityKey(itemId);

        var chunkKey = new ChunkKey(0, 0, 0);
        var chunk = world.GetChunk(chunkKey)
            ?? throw new InvalidOperationException("Expected test chunk to exist.");
        chunk.EnsureStockpileData();
        var stockpileData = chunk.GetStockpileData()
            ?? throw new InvalidOperationException("Expected stockpile data to exist.");

        int zoneId = world.Stockpiles.CreateZone("Test Stockpile", chunkKey, 0);
        int cellIndex = Chunk.LocalIndex(destination.X, destination.Y);
        stockpileData.CreateOrUpdateShard(zoneId, chunkKey);
        stockpileData.AddCellsToZone(zoneId, new[] { cellIndex });
        var zone = world.Stockpiles.GetZone(zoneId)
            ?? throw new InvalidOperationException("Expected stockpile zone to exist.");
        zone.UpdateMemberChunks(new[] { chunkKey });

        var validator = new TransportDestinationValidator(world);
        zone.Filter = new StockpileFilter
        {
            ItemIds = ImmutableHashSet.Create(StringComparer.Ordinal, "core_item_boulder_granite")
        };
        RegressionAssert.True(
            validator.ValidateDestination(destination.X, destination.Y, 0, TransportReason.ToStockpile)
            && !validator.ValidateDestinationForItem(itemId, destination.X, destination.Y, 0, TransportReason.ToStockpile),
            "Transport stockpile destination validation did not re-check the zone filter against the item projection.");
        zone.Filter = new StockpileFilter();

        var stockpileDiffLog = new StockpileDiffLog();
        var emitter = new TransportStockpileIndexEmitter(
            world,
            stockpileDiffLog,
            priority: 100,
            systemId: "test.transport");

        world.Items.UpdateItemPosition(itemId, source, 0, destination, 0);
        emitter.RecordDelivery(itemId, new HumanFortress.Contracts.Navigation.Point3(destination.X, destination.Y, 0), TransportReason.ToStockpile);
        StockpileDiffApplicator.ApplyAll(world, stockpileDiffLog.MergeAndSort());
        stockpileDiffLog.Clear();

        var shard = stockpileData.GetShard(zoneId)
            ?? throw new InvalidOperationException("Expected stockpile shard to exist.");
        RegressionAssert.True(
            stockpileData.GetItemsInZone(zoneId).SingleOrDefault() == itemHandle
            && shard.UsedSlots == 1,
            "Transport stockpile delivery did not enqueue a post-tick stockpile place-item diff.");
        RegressionAssert.True(
            !StockpileWorldQueries.TryFindDestination(world, world.Items.GetInstance(itemId)!, new[] { zone }, out _, out _),
            "Stockpile destination selection did not respect shard capacity after transport indexed delivery.");

        emitter.RecordPickup(itemId, new HumanFortress.Contracts.Navigation.Point3(destination.X, destination.Y, 0));
        StockpileDiffApplicator.ApplyAll(world, stockpileDiffLog.MergeAndSort());
        stockpileDiffLog.Clear();

        RegressionAssert.True(
            !stockpileData.GetItemsInZone(zoneId).Any()
            && shard.UsedSlots == 0,
            "Transport stockpile pickup did not enqueue a post-tick stockpile remove-item diff.");

        RegressionAssert.True(stockpileData.TryReserveSlot(zoneId), "Stockpile test setup failed to reserve a slot.");
        emitter.ReleaseDestinationReservation(new HumanFortress.Contracts.Navigation.Point3(destination.X, destination.Y, 0), TransportReason.ToStockpile);
        StockpileDiffApplicator.ApplyAll(world, stockpileDiffLog.MergeAndSort());

        RegressionAssert.True(
            shard.ReservedSlots == 0,
            "Transport stockpile cancellation did not enqueue a post-tick stockpile release-slot diff.");

        emitter.RecordDelivery(itemId, new HumanFortress.Contracts.Navigation.Point3(destination.X, destination.Y, 0), TransportReason.ToStockpile);
        StockpileDiffApplicator.ApplyAll(world, stockpileDiffLog.MergeAndSort());
        stockpileDiffLog.Clear();

        var constructionItemsDiffs = new ItemsDiffLog();
        var constructionDiffEmitter = new ConstructionDiffEmitter(
            null,
            constructionItemsDiffs,
            "test.construction",
            100,
            world,
            stockpileDiffLog);
        constructionDiffEmitter.RemoveItem(itemId, destination, 0, quantity: 1);
        ItemsDiffApplicator.ApplyPreSimulation(world, constructionItemsDiffs.MergeAndSort());
        StockpileDiffApplicator.ApplyAll(world, stockpileDiffLog.MergeAndSort());
        stockpileDiffLog.Clear();

        RegressionAssert.True(
            constructionItemsDiffs.MergeAndSort().Any(diff => diff.Op == ItemsDiffOp.RemoveItem)
            && !stockpileData.GetItemsInZone(zoneId).Any()
            && !stockpileData.GetItemsByTag("wood").Any()
            && shard.UsedSlots == 0,
            "Construction item consumption did not enqueue a stockpile remove-item diff for a fully consumed stockpile stack.");

        itemId = world.Items.SpawnItem("core_item_log_oak", destination, 0, quantity: 1, currentTick: 0)
            ?? throw new InvalidOperationException("Expected replacement craft test item to spawn.");
        emitter.RecordDelivery(itemId, new HumanFortress.Contracts.Navigation.Point3(destination.X, destination.Y, 0), TransportReason.ToStockpile);
        StockpileDiffApplicator.ApplyAll(world, stockpileDiffLog.MergeAndSort());
        stockpileDiffLog.Clear();

        var craftItemsDiffs = new ItemsDiffLog();
        var craftDiffEmitter = new CraftDiffEmitter(
            craftItemsDiffs,
            100,
            "test.craft",
            world,
            stockpileDiffLog);
        craftDiffEmitter.RemoveItem(itemId, destination, 0, quantity: 1);
        ItemsDiffApplicator.ApplyPreSimulation(world, craftItemsDiffs.MergeAndSort());
        StockpileDiffApplicator.ApplyAll(world, stockpileDiffLog.MergeAndSort());

        RegressionAssert.True(
            craftItemsDiffs.MergeAndSort().Any(diff => diff.Op == ItemsDiffOp.RemoveItem)
            && !stockpileData.GetItemsInZone(zoneId).Any()
            && !stockpileData.GetItemsByTag("wood").Any()
            && shard.UsedSlots == 0,
            "Craft item consumption did not enqueue a stockpile remove-item diff for a fully consumed stockpile stack.");

        Console.WriteLine("[PASS] Transport stockpile index diffs");
    }

    private static void TestHaulingPlannerReservesStockpileCapacity()
    {
        var world = new World(2, 2);
        DefinitionCatalogTestSupport.LoadItems(world);

        var sourceA = new SadRogue.Primitives.Point(1, 1);
        var sourceB = new SadRogue.Primitives.Point(2, 1);
        var destination = new SadRogue.Primitives.Point(5, 5);
        world.SetTile(sourceA.X, sourceA.Y, 0, new TileBase(0, (ushort)TerrainKind.OpenWithFloor, 0, 0, 0, 0, 1), 0);
        world.SetTile(sourceB.X, sourceB.Y, 0, new TileBase(0, (ushort)TerrainKind.OpenWithFloor, 0, 0, 0, 0, 1), 0);
        world.SetTile(destination.X, destination.Y, 0, new TileBase(0, (ushort)TerrainKind.OpenWithFloor, 0, 0, 0, 0, 1), 0);

        var itemA = world.Items.SpawnItem("core_item_log_oak", sourceA, 0, quantity: 1, currentTick: 0);
        var itemB = world.Items.SpawnItem("core_item_log_oak", sourceB, 0, quantity: 1, currentTick: 0);
        RegressionAssert.True(itemA.HasValue && itemB.HasValue, "Hauling planner reserve setup failed to spawn items.");

        var chunkKey = new ChunkKey(0, 0, 0);
        var chunk = world.GetChunk(chunkKey)
            ?? throw new InvalidOperationException("Expected test chunk to exist.");
        chunk.EnsureStockpileData();
        var stockpileData = chunk.GetStockpileData()
            ?? throw new InvalidOperationException("Expected stockpile data to exist.");

        int zoneId = world.Stockpiles.CreateZone("Single Cell", chunkKey, 0);
        int cellIndex = Chunk.LocalIndex(destination.X, destination.Y);
        stockpileData.CreateOrUpdateShard(zoneId, chunkKey);
        stockpileData.AddCellsToZone(zoneId, new[] { cellIndex });
        world.Stockpiles.GetZone(zoneId)?.UpdateMemberChunks(new[] { chunkKey });

        var transportQueue = new TransportRequestQueue();
        var stockpileDiffLog = new StockpileDiffLog();
        var hauling = new HaulingSystem(
            world,
            world.Orders,
            transportIntake: transportQueue,
            stockpileDiffLog: stockpileDiffLog);

        world.Orders.EnqueueHaul(new SadRogue.Primitives.Rectangle(1, 1, 2, 1), z: 0, priority: 50, createdTick: 0);
        hauling.ReadTick(0);
        hauling.WriteTick(0);

        var reserveDiffs = stockpileDiffLog.MergeAndSort()
            .Where(diff => diff.Op == StockpileDiffOp.ReserveSlot)
            .ToList();
        StockpileDiffApplicator.ApplyAll(world, stockpileDiffLog.MergeAndSort());

        var shard = stockpileData.GetShard(zoneId)
            ?? throw new InvalidOperationException("Expected stockpile shard to exist.");
        RegressionAssert.True(
            transportQueue.Count == 1
            && reserveDiffs.Count == 1
            && shard.ReservedSlots == 1,
            $"Hauling planner did not cap same-tick stockpile requests by available shard capacity. queue={transportQueue.Count} reserveDiffs={reserveDiffs.Count} reservedSlots={shard.ReservedSlots}");

        Console.WriteLine("[PASS] Hauling planner stockpile reservations");
    }

    private static void TestHaulingPlannerDoesNotReserveDuplicatePendingTransport()
    {
        var world = new World(2, 2);
        DefinitionCatalogTestSupport.LoadItems(world);

        var source = new SadRogue.Primitives.Point(1, 1);
        var destination = new SadRogue.Primitives.Point(5, 5);
        world.SetTile(source.X, source.Y, 0, new TileBase(0, (ushort)TerrainKind.OpenWithFloor, 0, 0, 0, 0, 1), 0);
        world.SetTile(destination.X, destination.Y, 0, new TileBase(0, (ushort)TerrainKind.OpenWithFloor, 0, 0, 0, 0, 1), 0);

        var itemId = world.Items.SpawnItem("core_item_log_oak", source, 0, quantity: 1, currentTick: 0);
        RegressionAssert.True(itemId.HasValue, "Hauling duplicate reservation setup failed to spawn item.");

        var chunkKey = new ChunkKey(0, 0, 0);
        var chunk = world.GetChunk(chunkKey)
            ?? throw new InvalidOperationException("Expected test chunk to exist.");
        chunk.EnsureStockpileData();
        var stockpileData = chunk.GetStockpileData()
            ?? throw new InvalidOperationException("Expected stockpile data to exist.");

        int zoneId = world.Stockpiles.CreateZone("Duplicate Guard", chunkKey, 0);
        int cellIndex = Chunk.LocalIndex(destination.X, destination.Y);
        stockpileData.CreateOrUpdateShard(zoneId, chunkKey);
        stockpileData.AddCellsToZone(zoneId, new[] { cellIndex });
        world.Stockpiles.GetZone(zoneId)?.UpdateMemberChunks(new[] { chunkKey });

        var transportQueue = new TransportRequestQueue();
        var stockpileDiffLog = new StockpileDiffLog();
        var hauling = new HaulingSystem(
            world,
            world.Orders,
            transportIntake: transportQueue,
            stockpileDiffLog: stockpileDiffLog);

        world.Orders.EnqueueHaul(new SadRogue.Primitives.Rectangle(1, 1, 1, 1), z: 0, priority: 50, createdTick: 0);
        hauling.ReadTick(0);
        hauling.WriteTick(0);

        world.Orders.EnqueueHaul(new SadRogue.Primitives.Rectangle(1, 1, 1, 1), z: 0, priority: 50, createdTick: 1);
        hauling.ReadTick(1);
        hauling.WriteTick(1);

        var reserveDiffs = stockpileDiffLog.MergeAndSort()
            .Where(diff => diff.Op == StockpileDiffOp.ReserveSlot)
            .ToList();
        RegressionAssert.True(
            transportQueue.Count == 1 && reserveDiffs.Count == 1,
            "Hauling planner reserved stockpile capacity for a duplicate pending transport request.");

        Console.WriteLine("[PASS] Hauling planner duplicate pending transport guard");
    }

    private static void TestWorldChunks()
    {
        var world = new World(4, 50);

        RegressionAssert.True(world.SizeInChunks == 4 && world.MaxZ == 50, "World was created with incorrect dimensions.");

        var largeWorld = new World(World.MaxSizeInChunks, 3);
        var tooLargeRejected = false;
        try
        {
            _ = new World(World.MaxSizeInChunks + 1, 3);
        }
        catch (ArgumentException)
        {
            tooLargeRejected = true;
        }

        RegressionAssert.True(
            largeWorld.SizeInTiles == 512 && tooLargeRejected,
            "World size guard should allow 512x512 lazy chunk worlds while rejecting unsupported larger sizes.");

        var chunkKey = new ChunkKey(1, 1, 10);
        var chunk = world.GetOrCreateChunk(chunkKey);

        RegressionAssert.True(chunk.Key.Equals(chunkKey), "World did not create/retrieve expected chunk.");

        chunk.MarkTileDirty(9, tick: 1);
        chunk.MarkTileDirty(3, tick: 2);
        chunk.MarkTileDirty(9, tick: 3);
        var dirtyTiles = chunk.DrainDirtyTileIndices();
        var dirtyTilesAfterDrain = chunk.DrainDirtyTileIndices();

        RegressionAssert.True(
            dirtyTiles.SequenceEqual(new[] { 3, 9 })
            && dirtyTilesAfterDrain.Length == 0,
            "Chunk dirty tile set did not deduplicate, sort, and drain local indexes deterministically.");

        world.UpdateLOD(64, 64, 10);
        RegressionAssert.True(world.GetActiveChunks().Any(), "World LOD update produced no active chunks.");

        Console.WriteLine("[PASS] World chunks");
    }

    private static void TestWorldPlaceableAndConstructionSnapshotsUseStableOrdering()
    {
        var world = new World(4, 3);
        var chunkKeys = new[]
        {
            new ChunkKey(1, 0, 1),
            new ChunkKey(0, 1, 0),
            new ChunkKey(1, 0, 0),
            new ChunkKey(0, 0, 0)
        };

        foreach (var key in chunkKeys)
        {
            world.GetOrCreateChunk(key);
            world.MarkChunkDirty(key);
        }

        var orderedChunks = world.GetAllChunks()
            .Select(static chunk => chunk.Key)
            .ToArray();
        var orderedDirtyChunks = world.GetAndClearDirtyChunks().ToArray();

        var placeableData = new ChunkPlaceableData();
        var laterPlaceable = new PlaceableInstance(
            Guid.Parse("bbbbbbbb-1111-1111-1111-bbbbbbbbbbbb"),
            PlaceableKind.Construction,
            "test_later",
            new SadRogue.Primitives.Point(2, 0),
            z: 0,
            footprint: new Footprint(1, 1, 1));
        var earlierPlaceable = new PlaceableInstance(
            Guid.Parse("aaaaaaaa-1111-1111-1111-aaaaaaaaaaaa"),
            PlaceableKind.Construction,
            "test_earlier",
            new SadRogue.Primitives.Point(1, 0),
            z: 0,
            footprint: new Footprint(1, 1, 1));
        placeableData.AddPlaceable(localIndex: 8, laterPlaceable);
        placeableData.AddPlaceable(localIndex: 1, earlierPlaceable);

        int[] localIndexes = placeableData.GetOwnedPlaceableSnapshot()
            .Select(static entry => entry.LocalIndex)
            .ToArray();
        Guid[] ownedPlaceableIds = placeableData.GetAllOwnedPlaceables()
            .Select(static placeable => placeable.Guid)
            .ToArray();

        var construction = new ConstructionSiteState
        {
            MaterialsRequired = new Dictionary<string, int>
            {
                ["wood"] = 1,
                ["block"] = 2,
                ["stone"] = 3
            },
            MaterialsDelivered = new Dictionary<string, int>
            {
                ["stone"] = 1,
                ["block"] = 1
            }
        };

        string[] requiredIds = construction.GetRequiredMaterialIdsSnapshot().ToArray();
        string[] requiredRows = construction.GetRequiredMaterialsSnapshot()
            .Select(static entry => entry.Key)
            .ToArray();
        string[] deliveredRows = construction.GetDeliveredMaterialsSnapshot()
            .Select(static entry => entry.Key)
            .ToArray();

        var expectedChunkOrder = new[]
        {
            new ChunkKey(0, 0, 0),
            new ChunkKey(1, 0, 0),
            new ChunkKey(0, 1, 0),
            new ChunkKey(1, 0, 1)
        };

        RegressionAssert.True(
            orderedChunks.SequenceEqual(expectedChunkOrder)
            && orderedDirtyChunks.SequenceEqual(expectedChunkOrder)
            && localIndexes.SequenceEqual(new[] { 1, 8 })
            && ownedPlaceableIds.SequenceEqual(new[] { earlierPlaceable.Guid, laterPlaceable.Guid })
            && requiredIds.SequenceEqual(new[] { "block", "stone", "wood" })
            && requiredRows.SequenceEqual(new[] { "block", "stone", "wood" })
            && deliveredRows.SequenceEqual(new[] { "block", "stone" }),
            "World, placeable, or construction owner snapshots did not return stable ordering.");

        Console.WriteLine("[PASS] World/placeable/construction snapshots use stable ordering");
    }

    private static void TestCrossChunkPlaceableReferencesResolveAndRemove()
    {
        var world = new World(2, 1);
        var anchor = new SadRogue.Primitives.Point(31, 4);
        var secondaryCell = new SadRogue.Primitives.Point(32, 4);
        var floor = new TileBase(0, (ushort)TerrainKind.OpenWithFloor, 0, 0, 0, 0, 1);
        world.SetTile(anchor.X, anchor.Y, 0, floor, 0);
        world.SetTile(secondaryCell.X, secondaryCell.Y, 0, floor, 0);

        var placeable = new PlaceableInstance(
            Guid.Parse("aaaaaaaa-7777-7777-7777-aaaaaaaaaaaa"),
            PlaceableKind.Construction,
            "cross_chunk_test",
            anchor,
            z: 0,
            footprint: new Footprint(2, 1, 1));

        PlaceableManager.PlacePlaceable(world, placeable, tick: 1);

        var secondaryChunk = world.GetChunk(new ChunkKey(1, 0, 0));
        var secondaryData = secondaryChunk?.GetPlaceableData();
        var secondaryLocalIndex = Chunk.LocalIndex(0, 4);
        var primaryResolved = PlaceableManager.TryGetPlaceableAt(world, anchor, 0, out var primaryPlaceable);
        var secondaryResolved = PlaceableManager.TryGetPlaceableAt(world, secondaryCell, 0, out var secondaryPlaceable);
        var externalRefStored = secondaryData?.TryGetExternalRefAt(secondaryLocalIndex, out var externalGuid) == true
            && externalGuid == placeable.Guid;

        var removed = PlaceableManager.RemoveOwnedAt(world, anchor, 0, tick: 2);
        var secondaryReleased = secondaryData?.HasPlaceableAt(secondaryLocalIndex) == false;
        var collisionAfterRemoval = PlaceableManager.CheckCollision(world, anchor, 0, new Footprint(2, 1, 1));
        var secondaryResolvedAfterRemoval = PlaceableManager.TryGetPlaceableAt(world, secondaryCell, 0, out _);

        RegressionAssert.True(
            primaryResolved
            && ReferenceEquals(primaryPlaceable, placeable)
            && secondaryResolved
            && ReferenceEquals(secondaryPlaceable, placeable)
            && externalRefStored
            && removed
            && secondaryReleased
            && collisionAfterRemoval.CanPlace
            && !secondaryResolvedAfterRemoval,
            "Cross-chunk placeable references must resolve through Simulation and be removed with the owner.");

        Console.WriteLine("[PASS] Cross-chunk placeable references resolve and remove cleanly");
    }

    private static void TestConstructionRequirementsSupportDefinitionIds()
    {
        var itemDefinition = new HumanFortress.Contracts.Simulation.Items.ItemDefinition
        {
            Id = "core_item_block_stone",
            Tags = new[] { "stone", "block" }
        };
        var matchingRequirement = ConstructionMaterialRequirement.ForDefinition("core_item_block_stone");
        var otherRequirement = ConstructionMaterialRequirement.ForDefinition("core_item_log_wood");
        var tagRequirement = ConstructionMaterialRequirement.ForTag("block");

        var construction = new ConstructionDefinition
        {
            Id = "core_construction_test_def_material",
            Name = "Definition Material Test",
            BuildTimeTicks = 10,
            PlaceableProfile = new PlaceableProfile { Footprint = new Footprint(1, 1, 1) },
            MaterialCosts = new[]
            {
                new MaterialCost { DefId = "core_item_block_stone", Count = 2 },
                new MaterialCost { Tag = "block", Count = 1 }
            }
        };
        var requirements = BuildableConstructionSystem.BuildMaterialRequirements(construction);

        RegressionAssert.True(
            matchingRequirement == "def:core_item_block_stone"
            && ConstructionMaterialRequirement.MatchesItem(itemDefinition, matchingRequirement)
            && !ConstructionMaterialRequirement.MatchesItem(itemDefinition, otherRequirement)
            && ConstructionMaterialRequirement.MatchesItem(itemDefinition, tagRequirement)
            && requirements.TryGetValue("def:core_item_block_stone", out var requiredDefCount)
            && requiredDefCount == 2
            && requirements.TryGetValue("block", out var requiredTagCount)
            && requiredTagCount == 1,
            "Construction material requirements must preserve def_id requirements and match them by item definition id.");

        Console.WriteLine("[PASS] Construction requirements support definition ids");
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

        var replayRecordQueue = new CommandQueue();
        var replayCommandId = Guid.Parse("12121212-3434-5656-7878-909090909090");
        var replayCommand = new TestCommand(
            5,
            "replay.record",
            commandId: replayCommandId,
            payload: new byte[] { 1, 2, 3 });
        replayRecordQueue.Enqueue(replayCommand);
        replayRecordQueue.ExecuteCommands(5, context);
        var replayRecord = replayRecordQueue.GetExecutedCommandRecords().SingleOrDefault();
        var replayPayload = replayRecord?.ToPayloadArray() ?? Array.Empty<byte>();
        replayPayload[0] = 9;

        RegressionAssert.True(
            replayRecord != null
            && replayRecord.Tick == 5
            && replayRecord.CommandId == replayCommandId
            && replayRecord.CommandType == "replay.record"
            && replayRecord.PayloadLength == 3
            && replayRecord.ToPayloadArray().SequenceEqual(new byte[] { 1, 2, 3 }),
            "CommandQueue replay records did not preserve immutable tick/id/type/payload data.");

        replayRecordQueue.ClearExecutedCommands();
        RegressionAssert.True(
            replayRecordQueue.GetExecutedCommandRecords().Count == 0,
            "CommandQueue did not clear executed replay records.");

        var pendingRecordQueue = new CommandQueue();
        var pendingLater = new TestCommand(10, "pending.later", payload: new byte[] { 10 });
        var pendingFirst = new TestCommand(5, "pending.first", payload: new byte[] { 5 });
        var pendingSecond = new TestCommand(5, "pending.second", payload: new byte[] { 6 });
        pendingRecordQueue.Enqueue(pendingLater);
        pendingRecordQueue.Enqueue(pendingFirst);
        pendingRecordQueue.Enqueue(pendingSecond);
        var pendingRecords = pendingRecordQueue.GetPendingCommandRecords();
        var pendingReplaySnapshot = pendingRecordQueue.GetReplaySnapshot();

        RegressionAssert.True(
            pendingRecords.Select(record => record.CommandType).SequenceEqual(new[] { "pending.first", "pending.second", "pending.later" })
            && pendingReplaySnapshot.PendingRecords.Select(record => record.CommandType).SequenceEqual(pendingRecords.Select(record => record.CommandType))
            && pendingReplaySnapshot.ExecutedRecords.Count == 0,
            "CommandQueue pending replay records were not exposed in deterministic future execution order.");

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

        var restoredQueue = new CommandQueue();
        var restoredFuture = new TestCommand(3, "restored-future");
        var restoredDue = new TestCommand(1, "restored-due");
        restoredQueue.RestoreCommands(new ICommand[] { restoredFuture, restoredDue });

        RegressionAssert.True(
            restoredQueue.GetExecutedCommands().Count == 0,
            "CommandQueue restore marked pending replay commands as already executed.");

        restoredQueue.ExecuteCommands(1, context);
        RegressionAssert.True(
            restoredDue.Executed
            && !restoredFuture.Executed
            && restoredQueue.GetExecutedCommands().SequenceEqual(new ICommand[] { restoredDue }),
            "CommandQueue restore did not replay only due commands into executed history.");

        restoredQueue.ExecuteCommands(3, context);
        RegressionAssert.True(
            restoredFuture.Executed
            && restoredQueue.GetExecutedCommands().SequenceEqual(new ICommand[] { restoredDue, restoredFuture }),
            "CommandQueue restore did not preserve pending future commands for later replay.");

        var atomicRestoreQueue = new CommandQueue();
        var preservedCommand = new TestCommand(0, "preserved");
        bool rejectedInvalidRestore = false;
        atomicRestoreQueue.Enqueue(preservedCommand);
        try
        {
            atomicRestoreQueue.RestoreCommands(new[] { (ICommand)null! });
        }
        catch (ArgumentNullException)
        {
            rejectedInvalidRestore = true;
        }

        atomicRestoreQueue.ExecuteCommands(0, context);
        RegressionAssert.True(
            rejectedInvalidRestore && preservedCommand.Executed,
            "CommandQueue invalid restore cleared existing pending commands before validation.");

        var diagnostics = new RecordingDiagnosticSink();
        var previousSink = DiagnosticHub.Sink;
        var failingQueue = new CommandQueue();
        var failingCommandId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        try
        {
            DiagnosticHub.Sink = diagnostics;
            failingQueue.Enqueue(new TestCommand(0, "failing", commandId: failingCommandId, throwOnExecute: true));
            failingQueue.ExecuteCommands(0, context);
        }
        finally
        {
            DiagnosticHub.Sink = previousSink;
        }

        var failureDiagnostic = diagnostics.Events.SingleOrDefault(e => e.Category == "Core.CommandQueue");
        RegressionAssert.True(
            failureDiagnostic != null
            && failureDiagnostic.Message.Contains(failingCommandId.ToString(), StringComparison.Ordinal)
            && failureDiagnostic.Message.Contains("seq=1", StringComparison.Ordinal),
            "CommandQueue failure diagnostics did not include the deterministic command id and queue sequence.");

        Console.WriteLine("[PASS] CommandQueue");
    }

    private static void TestEventBus()
    {
        var bus = new EventBus();
        var order = new List<string>();
        Action<TestGameEvent> first = _ => order.Add("first");
        Action<TestGameEvent> second = _ => order.Add("second");
        Action<TestGameEvent> failing = _ => throw new InvalidOperationException("event smoke");
        Action<TestGameEvent> afterFailure = _ => order.Add("after");

        bus.Subscribe(first);
        bus.Subscribe(second);
        bus.Publish(new TestGameEvent(1, "ordered"));

        bus.Unsubscribe(first);
        bus.Publish(new TestGameEvent(2, "after-unsubscribe"));

        var diagnostics = new RecordingDiagnosticSink();
        var previousSink = DiagnosticHub.Sink;
        try
        {
            DiagnosticHub.Sink = diagnostics;
            bus.Subscribe(failing);
            bus.Subscribe(afterFailure);
            bus.Publish(new TestGameEvent(3, "failure"));
        }
        finally
        {
            DiagnosticHub.Sink = previousSink;
        }

        RegressionAssert.True(
            order.SequenceEqual(new[] { "first", "second", "second", "second", "after" })
            && diagnostics.Events.Any(e => e.Category == "Core.EventBus" && e.Message.Contains("event smoke", StringComparison.Ordinal)),
            "EventBus did not preserve registration order, unsubscribe handlers, or continue after a handler failure.");

        Console.WriteLine("[PASS] EventBus");
    }

    private static void TestSimulationCommandStage()
    {
        var scheduler = new TickScheduler();
        var commandQueue = new CommandQueue();
        var diffLog = new DiffLog();
        var itemsDiffLog = new ItemsDiffLog();
        var creaturesDiffLog = new CreaturesDiffLog();
        var mutationDiffs = new RuntimeMutationDiffLogs(itemsDiffLog, creaturesDiffLog);
        var world = new World(2, 10);
        var eventBus = new EventBus();
        var context = CreateRuntimeContext(diffLog, mutationDiffs, world, eventBus);
        var probe = new CommandStageProbe();
        var pipeline = new SimulationTickPipeline(world, commandQueue, context, context, diffLog, mutationDiffs, navigation: null);
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
        var mutationDiffs = new RuntimeMutationDiffLogs(itemsDiffLog, creaturesDiffLog);
        var world = new World(2, 10);
        var context = new TestRuntimeCommandContext(diffLog, world, new EventBus(), mutationDiffs);
        var probe = new CommandStageProbe();
        var readSystem = new CommandStageReadSystem(probe);
        var systems = new HostCoreTestSystems(readSystem);
        var host = new SimulationRuntimeHostCore(
            world,
            scheduler,
            commandQueue,
            context,
            context,
            diffLog,
            mutationDiffs,
            ConstructionCatalogStore.Empty,
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
        var services = new RuntimeSessionServices(
            scheduler,
            commandQueue,
            new EventBus(),
            diffLog,
            itemsDiffLog);
        services.MutationDiffs.Orders.AddHaul(
            new SadRogue.Primitives.Rectangle(0, 0, 1, 1),
            z: 0,
            priority: 1,
            createdTick: 0,
            systemId: "factory-test");
        services.MutationDiffs.Stockpiles.AddDeleteZone(
            zoneId: 1,
            priority: 1,
            systemId: "factory-test");

        World? contentWorld = null;
        World? hostWorld = null;
        HumanFortress.Navigation.Implementation.NavigationManager? hostNavigation = null;
        var hostObject = new object();
        var factory = new SimulationRuntimeSessionFactory<object>(
            services,
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
            && itemsDiffLog.MergeAndSort().Count == 0
            && services.MutationDiffs.Orders.MergeAndSort().Count == 0
            && services.MutationDiffs.Stockpiles.MergeAndSort().Count == 0,
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
          "movement": {
            "step_delay_ticks": 3
          },
          "budgets": {
            "max_nodes_per_search": 1200,
            "max_paths_per_tick": 4
          }
        }
        """;

        var tuning = HumanFortress.Navigation.Implementation.NavigationTuning.LoadFromJson(json);
        var invalidNumericTuning = HumanFortress.Navigation.Implementation.NavigationTuning.LoadFromJson("""
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
            && tuning.MovementStepDelayTicks == 3
            && tuning.MaxNodesPerSearch == 1200
            && tuning.MaxPathsPerTick == 4
            && invalidNumericTuning.BaseCost == HumanFortress.Navigation.Implementation.NavigationTuning.Default.BaseCost
            && invalidNumericTuning.FluidShallowThreshold == HumanFortress.Navigation.Implementation.NavigationTuning.Default.FluidShallowThreshold,
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

    private static void TestSchedulerTuningJson()
    {
        const string json = """
        {
          "threads": 0,
          "queue_policy": "single",
          "budgets": {
            "hauling": { "plan_per_tick": 12 },
            "mining": { "plan_per_tick": 0 },
            "construction": { "plan_per_tick": 34 }
          },
          "backpressure": { "max_carryover_ticks": 4 },
          "logging": { "level": "debug", "per_job_stats": false, "debug_panel": true },
          "worker_selection": { "strategy": "highest_skill" },
          "hauling_limits": {
            "max_active": 9,
            "reserve_for_mining": 3,
            "reserve_backlog_threshold": 2,
            "backlog_intake_cap": 5,
            "backlog_intake_threshold": 1
          }
        }
        """;

        var tuning = SchedulerTunings.LoadFromJson(json, "scheduler smoke");

        RegressionAssert.True(
            tuning.Threads == 1
            && tuning.QueuePolicy == "single"
            && tuning.Hauling.PlanPerTick == 12
            && tuning.Mining.PlanPerTick == 1
            && tuning.Construction.PlanPerTick == 34
            && tuning.BackpressureMaxCarryoverTicks == 4
            && !tuning.PerJobStatsLogging
            && tuning.LogLevel == "debug"
            && tuning.DebugPanel
            && tuning.WorkerSelection == WorkerSelectionStrategy.HighestSkill
            && tuning.HaulingLimits.MaxActive == 9
            && tuning.HaulingLimits.ReserveForMining == 3
            && tuning.HaulingLimits.ReserveBacklogThreshold == 2
            && tuning.HaulingLimits.BacklogIntakeCap == 5
            && tuning.HaulingLimits.BacklogIntakeThreshold == 1,
            "Scheduler tuning JSON parser did not apply deterministic plan budgets and limits.");

        Console.WriteLine("[PASS] Scheduler tuning JSON");
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

            RegressionAssert.True(
                bootstrap.StructuredRegistriesLoaded,
                "Content bootstrapper did not load content registries.");

            var structured = FortressRuntimeContentSnapshotLoader.CaptureLoaded();
            var graniteWallHandle = structured.Geology.GetGeologyHandle("core_terrain_wall_rock_granite");
            var graniteWall = structured.Geology.GetGeologyByHandle(graniteWallHandle);
            var hasAirGeology = structured.Geology.TryGetGeologyHandleByMaterialAndKind(
                "air",
                TerrainKind.OpenNoFloor.ToString(),
                out var airHandle);
            var airGeology = structured.Geology.GetGeologyByHandle(airHandle);
            var navigationTuning = structured.NavigationTuningJson;
            structured.ZonesById.TryGetValue("lumbering", out var lumberingZone);
            structured.ZonesById.TryGetValue("bedroom", out var bedroomZone);
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
                bootstrap.StructuredRegistriesLoaded
                && structured.Materials.ResolveStringId("core_mat_stone_granite").HasValue
                && structured.TerrainKinds.GetKind("solid_wall") != null
                && structured.GeologyEntries.Count >= 10
                && structured.ZonesById.Count >= 1
                && graniteWall != null
                && graniteWall.Material == "core_mat_stone_granite"
                && hasAirGeology
                && airGeology?.Id == "core_terrain_air"
                && !string.IsNullOrWhiteSpace(navigationTuning)
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
        var professionDiffLog = new ProfessionAssignmentDiffLog();
        var mutationDiffs = new RuntimeMutationDiffLogs(itemsDiffLog, creaturesDiffLog, professions: professionDiffLog);
        var world = new World(2, 10);
        var context = CreateRuntimeContext(
            diffLog,
            mutationDiffs,
            world,
            recipes: RecipeCatalogStore.Empty);
        var registry = ProfessionRegistryLoader.Load(AppContext.BaseDirectory);
        var assignments = new ProfessionAssignments(registry);
        var professionId = registry.Definitions[0].Id;
        var workerId = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        var pipeline = new SimulationTickPipeline(
            world,
            commandQueue,
            context,
            context,
            diffLog,
            mutationDiffs,
            navigation: null);

        context.ProfessionCommandBindings.SetProfessionWeightHandler(assignments.SetWeight);
        pipeline.AttachTo(scheduler);

        commandQueue.Enqueue(new SetProfessionWeightCommand(tick: 0, workerId, professionId, weight: 12));
        RegressionAssert.True(
            assignments.GetWeight(workerId, professionId) == 5,
            "SetProfessionWeightCommand mutated profession assignments before the post-tick profession diff applicator.");
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
        var orderDiffLog = new OrderDiffLog();
        var stockpileDiffLog = new StockpileDiffLog();
        var mutationDiffs = new RuntimeMutationDiffLogs(
            itemsDiffLog,
            creaturesDiffLog,
            orders: orderDiffLog,
            stockpiles: stockpileDiffLog);
        var world = new World(2, 10);
        var context = CreateRuntimeContext(
            diffLog,
            mutationDiffs,
            world);
        var pipeline = new SimulationTickPipeline(
            world,
            commandQueue,
            context,
            context,
            diffLog,
            mutationDiffs,
            navigation: null);

        var rect = new SadRogue.Primitives.Rectangle(1, 1, 2, 2);
        var buildAnchor = new SadRogue.Primitives.Point(4, 4);
        var filter = new MaterialFilterSpec { CategoryKey = "test.floor" };

        pipeline.AttachTo(scheduler);
        commandQueue.Enqueue(new CreateMiningOrderCommand(tick: 0, rect, z: 2, priority: 11));
        commandQueue.Enqueue(new CreateAdvancedMiningOrderCommand(tick: 0, rect, zMin: 1, zMax: 3, action: HumanFortress.Simulation.Orders.MiningAction.Dig, priority: 12));
        commandQueue.Enqueue(new CreateHaulOrderCommand(tick: 0, rect, z: 2, priority: 13));
        commandQueue.Enqueue(new CreateConstructionOrderCommand(tick: 0, rect, zMin: 2, zMax: 2, shape: ConstructionShape.Floor, filter: filter, priority: 14));
        commandQueue.Enqueue(new CreateBuildableConstructionOrderCommand(tick: 0, "core_workshop_carpenter", buildAnchor, z: 2, priority: 15));
        RegressionAssert.True(
            !world.Orders.GetActiveMiningSnapshot().Any()
            && !world.Orders.GetActiveHaulsSnapshot().Any()
            && !world.Orders.GetActiveConstructionSnapshot().Any()
            && !world.Orders.GetActiveBuildableSnapshot().Any(),
            "Order commands mutated order manager state before the post-tick order diff applicator.");
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

    private static void TestOrdersManagerActiveSnapshotsUseStableOrdering()
    {
        var world = new World(2, 4);
        var laterRect = new SadRogue.Primitives.Rectangle(8, 8, 2, 2);
        var earlierRect = new SadRogue.Primitives.Rectangle(1, 1, 1, 1);
        var buildAnchorB = new SadRogue.Primitives.Point(4, 4);
        var buildAnchorA = new SadRogue.Primitives.Point(2, 2);

        world.Orders.EnqueueHaul(laterRect, z: 2, priority: 20, createdTick: 20);
        world.Orders.EnqueueHaul(earlierRect, z: 0, priority: 10, createdTick: 10);
        world.Orders.EnqueueMiningAdvanced(laterRect, zMin: 2, zMax: 2, MiningAction.Dig, priority: 20, createdTick: 20);
        world.Orders.EnqueueMiningAdvanced(earlierRect, zMin: 0, zMax: 0, MiningAction.Dig, priority: 10, createdTick: 10);
        world.Orders.EnqueueConstruction(
            laterRect,
            zMin: 2,
            zMax: 2,
            ConstructionShape.Wall,
            new MaterialFilterSpec { CategoryKey = "test.wall.b" },
            priority: 20,
            createdTick: 20);
        world.Orders.EnqueueConstruction(
            earlierRect,
            zMin: 0,
            zMax: 0,
            ConstructionShape.Floor,
            new MaterialFilterSpec { CategoryKey = "test.floor.a" },
            priority: 10,
            createdTick: 10);
        world.Orders.EnqueueBuildableConstruction("workshop.b", buildAnchorB, z: 0, priority: 20, createdTick: 20);
        world.Orders.EnqueueBuildableConstruction("workshop.a", buildAnchorA, z: 0, priority: 10, createdTick: 10);

        var hauls = world.Orders.GetActiveHaulsSnapshot();
        var mining = world.Orders.GetActiveMiningSnapshot();
        var construction = world.Orders.GetActiveConstructionSnapshot();
        var buildable = world.Orders.GetActiveBuildableSnapshot();

        RegressionAssert.True(
            hauls.Count == 2
            && hauls[0].Z == 0
            && hauls[0].WorldRect == earlierRect
            && mining.Count == 2
            && mining[0].Id == 1
            && mining[1].Id == 2
            && construction.Count == 2
            && construction[0].ZMin == 0
            && construction[0].WorldRect == earlierRect
            && buildable.Count == 2
            && buildable[0].ConstructionId == "workshop.a"
            && buildable[0].Anchor == buildAnchorA,
            "OrdersManager active snapshots should expose deterministic owner ordering.");

        Console.WriteLine("[PASS] OrdersManager active snapshots use stable ordering");
    }

    private static void TestZoneCommandsUseRuntimeTarget()
    {
        var scheduler = new TickScheduler();
        var commandQueue = new CommandQueue();
        var diffLog = new DiffLog();
        var itemsDiffLog = new ItemsDiffLog();
        var creaturesDiffLog = new CreaturesDiffLog();
        var zoneDiffLog = new ZoneDiffLog();
        var stockpileDiffLog = new StockpileDiffLog();
        var mutationDiffs = new RuntimeMutationDiffLogs(
            itemsDiffLog,
            creaturesDiffLog,
            zones: zoneDiffLog,
            stockpiles: stockpileDiffLog);
        var world = new World(2, 10);
        var context = CreateRuntimeContext(
            diffLog,
            mutationDiffs,
            world);
        var pipeline = new SimulationTickPipeline(
            world,
            commandQueue,
            context,
            context,
            diffLog,
            mutationDiffs,
            navigation: null);

        var initialRect = new SadRogue.Primitives.Rectangle(1, 1, 2, 2);
        var extraRect = new SadRogue.Primitives.Rectangle(4, 4, 1, 1);
        var removeRect = new SadRogue.Primitives.Rectangle(1, 1, 1, 1);

        pipeline.AttachTo(scheduler);
        commandQueue.Enqueue(new CreateZoneCommand(tick: 0, defId: "core_zone_stockpile", name: "Runtime Zone", worldRect: initialRect, z: 0));
        RegressionAssert.True(
            !world.Zones.Manager.GetAllZones().Any(),
            "CreateZoneCommand mutated zone state before the post-tick zone diff applicator.");
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
        RegressionAssert.True(
            world.Zones.GetZoneAtPosition(4, 4, 0) == 0,
            "UpdateZoneCellsCommand added cells before the post-tick zone diff applicator.");
        scheduler.ExecuteSingleTick();

        RegressionAssert.True(
            world.Zones.GetZoneAtPosition(4, 4, 0) == zoneId,
            "UpdateZoneCellsCommand did not add cells through the runtime zone command target.");

        commandQueue.Enqueue(new UpdateZoneCellsCommand(0, zoneId, removeRect, 0, false));
        RegressionAssert.True(
            world.Zones.GetZoneAtPosition(1, 1, 0) == zoneId,
            "UpdateZoneCellsCommand removed cells before the post-tick zone diff applicator.");
        scheduler.ExecuteSingleTick();

        RegressionAssert.True(
            world.Zones.GetZoneAtPosition(1, 1, 0) == 0
            && world.Zones.GetZoneAtPosition(2, 2, 0) == zoneId,
            "UpdateZoneCellsCommand did not remove cells through the runtime zone command target.");

        commandQueue.Enqueue(new DeleteZoneCommand(tick: 0, zoneId));
        RegressionAssert.True(
            world.Zones.Manager.GetZone(zoneId) != null,
            "DeleteZoneCommand deleted the zone before the post-tick zone diff applicator.");
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
        var workshopDiffLog = new WorkshopDiffLog();
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
        var constructionCatalog = FortressRuntimeContentSnapshotLoader.CaptureLoaded().Constructions;
        var mutationDiffs = new RuntimeMutationDiffLogs(
            itemsDiffLog,
            creaturesDiffLog,
            workshops: workshopDiffLog);
        var context = CreateRuntimeContext(
            diffLog,
            mutationDiffs,
            world,
            recipes: recipeCatalog);
        var pipeline = new SimulationTickPipeline(
            world,
            commandQueue,
            context,
            context,
            diffLog,
            mutationDiffs,
            constructionCatalog,
            navigation: null);
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
        RegressionAssert.True(
            workshop.Workshop.Queue.Count == 0,
            "UpdateWorkshopQueueCommand added recipes before the post-tick workshop diff applicator.");
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
        RegressionAssert.True(
            workshop.Workshop.Queue[0].RecipeId == "test_recipe_a"
            && workshop.Workshop.AllowedWorkers == 1
            && workshop.Workshop.AutoRequestMaterials
            && workshop.Workshop.AutoStockpileOutputs,
            "UpdateWorkshopQueueCommand moved entries or changed workshop settings before the post-tick workshop diff applicator.");
        scheduler.ExecuteSingleTick();

        RegressionAssert.True(
            workshop.Workshop.Queue[0].RecipeId == "test_recipe_b"
            && workshop.Workshop.AllowedWorkers == 3
            && !workshop.Workshop.AutoRequestMaterials
            && !workshop.Workshop.AutoStockpileOutputs,
            "UpdateWorkshopQueueCommand did not move queue entries or update workshop settings through the runtime target.");

        commandQueue.Enqueue(new UpdateWorkshopQueueCommand(0, workshopGuid, WorkshopQueueOperation.RemoveEntry, entryId: secondEntryId));
        RegressionAssert.True(
            workshop.Workshop.Queue.Count == 2,
            "UpdateWorkshopQueueCommand removed a queue entry before the post-tick workshop diff applicator.");
        scheduler.ExecuteSingleTick();

        RegressionAssert.True(
            workshop.Workshop.Queue.Count == 1
            && workshop.Workshop.Queue[0].RecipeId == "test_recipe_a",
            "UpdateWorkshopQueueCommand did not remove queue entries through the runtime target.");

        commandQueue.Enqueue(new UpdateWorkshopQueueCommand(0, workshopGuid, WorkshopQueueOperation.ClearQueue));
        RegressionAssert.True(
            workshop.Workshop.Queue.Count == 1,
            "UpdateWorkshopQueueCommand cleared the queue before the post-tick workshop diff applicator.");
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
        var stockpileDiffLog = new StockpileDiffLog();
        var mutationDiffs = new RuntimeMutationDiffLogs(
            itemsDiffLog,
            creaturesDiffLog,
            stockpiles: stockpileDiffLog);
        var world = new World(2, 10);
        var stockpilePresets = FortressRuntimeStockpilePresetCatalog.FromDefinitions(
            new[]
            {
                new StockpilePresetDefinition
                {
                    Id = "wood",
                    Name = "Wood Storage",
                    Mode = "Whitelist",
                    Tags = new[] { "wood", "log", "plank" },
                    Priority = 2
                }
            },
            "test stockpile presets",
            log: null);
        var context = CreateRuntimeContext(
            diffLog,
            mutationDiffs,
            world,
            stockpilePresets: stockpilePresets);
        var pipeline = new SimulationTickPipeline(
            world,
            commandQueue,
            context,
            context,
            diffLog,
            mutationDiffs,
            navigation: null);
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
        RegressionAssert.True(
            !world.Stockpiles.GetAllZones().Any(),
            "CreateStockpileCommand mutated stockpile state before the post-tick diff applicator.");
        scheduler.ExecuteSingleTick();

        var zone = world.Stockpiles.GetAllZones().SingleOrDefault();
        var chunk = world.GetChunk(new ChunkKey(0, 0, 0));
        var stockpileData = chunk?.GetStockpileData();
        int firstCell = Chunk.LocalIndex(1, 1);
        var shard = zone == null ? null : stockpileData?.GetShard(zone.ZoneId);

        RegressionAssert.True(
            zone != null
            && zone.Name == "Wood Stockpile 1"
            && zone.Priority == 2
            && zone.Filter.Tags.SetEquals(new[] { "wood", "log", "plank" })
            && zone.MemberChunks.Contains(new ChunkKey(0, 0, 0))
            && stockpileData != null
            && stockpileData.GetZoneAtCell(firstCell) == zone.ZoneId
            && shard != null
            && shard.Capacity == 4,
            "CreateStockpileCommand did not create stockpile zone shards through the runtime stockpile command target.");

        var createdZone = zone ?? throw new InvalidOperationException("Stockpile zone should exist after create command applies.");
        var createdStockpileData = stockpileData ?? throw new InvalidOperationException("Stockpile chunk data should exist after create command applies.");

        commandQueue.Enqueue(new CreateStockpileCommand(tick: 0, rect, z: 0, presetId: "wood"));
        scheduler.ExecuteSingleTick();

        RegressionAssert.True(
            world.Stockpiles.GetAllZones().Count() == 1,
            "CreateStockpileCommand created a duplicate stockpile for a fully overlapping rectangle.");

        int zoneId = createdZone.ZoneId;
        commandQueue.Enqueue(new DeleteStockpileCommand(tick: 0, zoneId));
        RegressionAssert.True(
            world.Stockpiles.GetZone(zoneId) != null
            && createdStockpileData.GetZoneAtCell(firstCell) == zoneId
            && createdStockpileData.GetShard(zoneId) != null,
            "DeleteStockpileCommand mutated stockpile state before the post-tick diff applicator.");
        scheduler.ExecuteSingleTick();
        pipeline.DetachFrom(scheduler);

        RegressionAssert.True(
            !world.Stockpiles.GetAllZones().Any()
            && createdStockpileData.GetZoneAtCell(firstCell) == 0
            && createdStockpileData.GetShard(zoneId) == null,
            "DeleteStockpileCommand did not delete stockpile zone shards through the post-tick diff applicator.");

        var overlapScheduler = new TickScheduler();
        var overlapCommandQueue = new CommandQueue();
        var overlapDiffLog = new DiffLog();
        var overlapItemsDiffLog = new ItemsDiffLog();
        var overlapCreaturesDiffLog = new CreaturesDiffLog();
        var overlapStockpileDiffLog = new StockpileDiffLog();
        var overlapMutationDiffs = new RuntimeMutationDiffLogs(
            overlapItemsDiffLog,
            overlapCreaturesDiffLog,
            stockpiles: overlapStockpileDiffLog);
        var overlapWorld = new World(2, 10);
        var overlapContext = CreateRuntimeContext(
            overlapDiffLog,
            overlapMutationDiffs,
            overlapWorld);
        var overlapPipeline = new SimulationTickPipeline(
            overlapWorld,
            overlapCommandQueue,
            overlapContext,
            overlapContext,
            overlapDiffLog,
            overlapMutationDiffs,
            navigation: null);
        var firstRect = new SadRogue.Primitives.Rectangle(1, 1, 2, 2);
        var secondRect = new SadRogue.Primitives.Rectangle(2, 2, 2, 2);
        for (int x = 1; x <= 3; x++)
        {
            for (int y = 1; y <= 3; y++)
            {
                overlapWorld.SetTile(x, y, 0, new TileBase(0, (ushort)TerrainKind.OpenWithFloor, 0, 0, 0, 0, 1), 0);
            }
        }

        overlapPipeline.AttachTo(overlapScheduler);
        overlapCommandQueue.Enqueue(new CreateStockpileCommand(tick: 0, firstRect, z: 0, presetId: "wood"));
        overlapCommandQueue.Enqueue(new CreateStockpileCommand(tick: 0, secondRect, z: 0, presetId: "stone"));
        overlapScheduler.ExecuteSingleTick();
        overlapPipeline.DetachFrom(overlapScheduler);

        var overlapZones = overlapWorld.Stockpiles.GetAllZones()
            .OrderBy(static zone => zone.ZoneId)
            .ToList();
        var overlapChunk = overlapWorld.GetChunk(new ChunkKey(0, 0, 0));
        var overlapStockpileData = overlapChunk?.GetStockpileData();
        int overlappedCell = Chunk.LocalIndex(2, 2);
        var firstShard = overlapZones.Count > 0
            ? overlapStockpileData?.GetShard(overlapZones[0].ZoneId)
            : null;
        var secondShard = overlapZones.Count > 1
            ? overlapStockpileData?.GetShard(overlapZones[1].ZoneId)
            : null;

        RegressionAssert.True(
            overlapZones.Count == 2
            && overlapStockpileData != null
            && overlapZones.Count > 1
            && overlapStockpileData.GetZoneAtCell(overlappedCell) == overlapZones[0].ZoneId
            && firstShard?.Capacity == 4
            && secondShard?.Capacity == 3,
            "StockpileDiffApplicator did not resolve same-tick overlapping stockpile creates deterministically.");

        var terrainScheduler = new TickScheduler();
        var terrainCommandQueue = new CommandQueue();
        var terrainDiffLog = new DiffLog();
        var terrainItemsDiffLog = new ItemsDiffLog();
        var terrainCreaturesDiffLog = new CreaturesDiffLog();
        var terrainMutationDiffs = new RuntimeMutationDiffLogs(
            terrainItemsDiffLog,
            terrainCreaturesDiffLog,
            stockpiles: new StockpileDiffLog());
        var terrainWorld = new World(2, 10);
        var terrainContext = CreateRuntimeContext(
            terrainDiffLog,
            terrainMutationDiffs,
            terrainWorld);
        var terrainPipeline = new SimulationTickPipeline(
            terrainWorld,
            terrainCommandQueue,
            terrainContext,
            terrainContext,
            terrainDiffLog,
            terrainMutationDiffs,
            navigation: null);
        var terrainRect = new SadRogue.Primitives.Rectangle(6, 6, 1, 1);
        terrainWorld.SetTile(6, 6, 0, new TileBase(0, (ushort)TerrainKind.OpenWithFloor, 0, 0, 0, 0, 1), 0);

        RegressionAssert.True(
            WorldCellTargetEncoding.TryEncode(6, 6, 0, out var terrainTarget),
            "Failed to encode stockpile terrain regression target.");

        terrainPipeline.AttachTo(terrainScheduler);
        terrainCommandQueue.Enqueue(new CreateStockpileCommand(tick: 0, terrainRect, z: 0, presetId: "wood"));
        terrainDiffLog.AddOp(new DiffOp(
            DiffOpType.SetTerrain,
            terrainTarget.ToDiffTarget(),
            "test.stockpile-terrain",
            priority: 0,
            args: (ulong)TerrainKind.SolidWall));
        terrainScheduler.ExecuteSingleTick();
        terrainPipeline.DetachFrom(terrainScheduler);

        RegressionAssert.True(
            !terrainWorld.Stockpiles.GetAllZones().Any()
            && terrainWorld.GetTile(6, 6, 0)?.Kind == TerrainKind.SolidWall,
            "StockpileDiffApplicator accepted a cell that became ineligible after same-tick terrain diffs.");

        Console.WriteLine("[PASS] Stockpile command runtime target");
    }

    private static void TestRuntimeStockpilePresetMenuUsesContentCatalog()
    {
        var runtime = FortressRuntimeSessionFactory.Create(
            AppContext.BaseDirectory,
            strictContent: false,
            contentWarningsAsErrors: false);
        runtime.InitializeWorld(sizeInChunks: 2, maxZ: 2);

        var frame = runtime.GetUiOverlayFrameData(
            currentZ: 0,
            viewport: new RuntimeRect(0, 0, 10, 10),
            showZoneOverlay: false,
            includeManagementDrawer: false,
            includeWorkDrawer: false,
            includeDebugMenu: false,
            stockpileDetailZoneId: null,
            zoneDetailId: null);

        RegressionAssert.True(
            frame.StockpilePresets.Options.Any(option => option.Id == "weapons" && option.Name == "Weapon Storage")
            && frame.StockpilePresets.Options.FirstOrDefault().Id == "all",
            "Runtime stockpile preset menu did not use content-backed preset definitions.");

        RegressionAssert.True(
            frame.Metadata.SchemaVersion == SimulationSnapshotSchema.CurrentVersion
            && frame.Metadata.RuntimeTick == runtime.SimulationStatus.CurrentTick,
            "Runtime overlay snapshot metadata was not authored by the Runtime session.");

        var sameFrame = runtime.GetUiOverlayFrameData(
            currentZ: 0,
            viewport: new RuntimeRect(0, 0, 10, 10),
            showZoneOverlay: false,
            includeManagementDrawer: false,
            includeWorkDrawer: false,
            includeDebugMenu: false,
            stockpileDetailZoneId: null,
            zoneDetailId: null);
        var differentFrameRequest = runtime.GetUiOverlayFrameData(
            currentZ: 0,
            viewport: new RuntimeRect(1, 0, 10, 10),
            showZoneOverlay: false,
            includeManagementDrawer: false,
            includeWorkDrawer: false,
            includeDebugMenu: false,
            stockpileDetailZoneId: null,
            zoneDetailId: null);

        RegressionAssert.True(
            frame.Publication.SchemaVersion == SimulationSnapshotPublicationSchema.CurrentVersion
            && frame.Publication.Surface == SimulationSnapshotPublicationSurface.UiOverlayFrame
            && frame.Publication.RequestHashAlgorithm == ReplayHashBuilder.Algorithm
            && !string.IsNullOrWhiteSpace(frame.Publication.RequestHash)
            && frame.Publication.RequestHash == sameFrame.Publication.RequestHash
            && frame.Publication.RequestHash != differentFrameRequest.Publication.RequestHash,
            "Runtime overlay snapshot publication metadata was not stable and request-keyed.");

        RegressionAssert.True(
            frame.PresenterFrame.SchemaVersion == SimulationSnapshotPresenterFrameSchema.CurrentVersion
            && frame.PresenterFrame.TransferMode == SimulationSnapshotTransferMode.FullSnapshot
            && frame.PresenterFrame.PayloadHashAlgorithm == SimulationSnapshotPayloadHashAlgorithm.JsonSha256V1
            && frame.PresenterFrame.PublicationSequence > 0
            && !string.IsNullOrWhiteSpace(frame.PresenterFrame.PayloadHash)
            && frame.PresenterFrame.PayloadHash == sameFrame.PresenterFrame.PayloadHash
            && frame.PresenterFrame.PublicationSequence == sameFrame.PresenterFrame.PublicationSequence
            && !frame.PresenterFrame.CanDiffFromPrevious
            && frame.PresenterFrame.DeltaBasePayloadHash is null
            && differentFrameRequest.PresenterFrame.PublicationSequence > frame.PresenterFrame.PublicationSequence
            && differentFrameRequest.PresenterFrame.PayloadHash != frame.PresenterFrame.PayloadHash
            && !differentFrameRequest.PresenterFrame.CanDiffFromPrevious
            && differentFrameRequest.PresenterFrame.DeltaBasePayloadHash is null,
            "Runtime overlay presenter-frame metadata did not expose a stable full-snapshot payload identity.");

        RegressionAssert.True(
            frame.Delta.SchemaVersion == SimulationUiOverlayFrameDeltaSchema.CurrentVersion
            && frame.Delta.IsAvailable
            && frame.Delta.PayloadHashAlgorithm == SimulationSnapshotPayloadHashAlgorithm.JsonSha256V1
            && !string.IsNullOrWhiteSpace(frame.Delta.PayloadHash)
            && !frame.Delta.CanApplyToBase
            && frame.Delta.BasePayloadHash is null
            && frame.Delta.SectionHashes.Count == 11
            && frame.Delta.ChangedSections.Count == frame.Delta.SectionHashes.Count
            && frame.Delta.SectionHashes.Any(section => section.Section == SimulationUiOverlayFrameSection.StockpilePresets)
            && sameFrame.Delta.PayloadHash == frame.Delta.PayloadHash
            && sameFrame.Delta.ChangedSections.Count == frame.Delta.ChangedSections.Count
            && differentFrameRequest.Delta.IsAvailable
            && !differentFrameRequest.Delta.CanApplyToBase
            && differentFrameRequest.Delta.BasePayloadHash is null
            && differentFrameRequest.Delta.SectionHashes.Count == frame.Delta.SectionHashes.Count
            && differentFrameRequest.Delta.ChangedSections.Count == differentFrameRequest.Delta.SectionHashes.Count,
            "Runtime overlay frame did not expose section-level delta metadata.");

        var renderFrame = runtime.GetFrameRenderData(
            includeMapViewport: true,
            fortressSize: 2 * 32,
            cameraPosition: new RuntimePoint(0, 0),
            cursorPosition: new RuntimePoint(0, 0),
            currentZ: 0,
            zoomLevel: 1,
            viewWidth: 10,
            viewHeight: 10,
            cursorGlyph: 'X',
            navigationMode: SimulationNavigationOverlayMode.None,
            selectedNavigationTarget: null,
            tileInspectionWorldPosition: new RuntimePoint(0, 0),
            tileInspectionZ: 0);

        RegressionAssert.True(
            renderFrame.Metadata.SchemaVersion == SimulationSnapshotSchema.CurrentVersion
            && renderFrame.Metadata.RuntimeTick == runtime.SimulationStatus.CurrentTick,
            "Runtime frame-render snapshot metadata was not authored by the Runtime session.");

        var sameRenderFrame = runtime.GetFrameRenderData(
            includeMapViewport: true,
            fortressSize: 2 * 32,
            cameraPosition: new RuntimePoint(0, 0),
            cursorPosition: new RuntimePoint(0, 0),
            currentZ: 0,
            zoomLevel: 1,
            viewWidth: 10,
            viewHeight: 10,
            cursorGlyph: 'X',
            navigationMode: SimulationNavigationOverlayMode.None,
            selectedNavigationTarget: null,
            tileInspectionWorldPosition: new RuntimePoint(0, 0),
            tileInspectionZ: 0);
        var differentRenderRequest = runtime.GetFrameRenderData(
            includeMapViewport: true,
            fortressSize: 2 * 32,
            cameraPosition: new RuntimePoint(1, 0),
            cursorPosition: new RuntimePoint(0, 0),
            currentZ: 0,
            zoomLevel: 1,
            viewWidth: 10,
            viewHeight: 10,
            cursorGlyph: 'X',
            navigationMode: SimulationNavigationOverlayMode.None,
            selectedNavigationTarget: null,
            tileInspectionWorldPosition: new RuntimePoint(0, 0),
            tileInspectionZ: 0);

        RegressionAssert.True(
            renderFrame.Publication.SchemaVersion == SimulationSnapshotPublicationSchema.CurrentVersion
            && renderFrame.Publication.Surface == SimulationSnapshotPublicationSurface.FrameRender
            && renderFrame.Publication.RequestHashAlgorithm == ReplayHashBuilder.Algorithm
            && !string.IsNullOrWhiteSpace(renderFrame.Publication.RequestHash)
            && renderFrame.Publication.RequestHash == sameRenderFrame.Publication.RequestHash
            && renderFrame.Publication.RequestHash != differentRenderRequest.Publication.RequestHash,
            "Runtime frame-render snapshot publication metadata was not stable and request-keyed.");

        RegressionAssert.True(
            renderFrame.PresenterFrame.SchemaVersion == SimulationSnapshotPresenterFrameSchema.CurrentVersion
            && renderFrame.PresenterFrame.TransferMode == SimulationSnapshotTransferMode.FullSnapshot
            && renderFrame.PresenterFrame.PayloadHashAlgorithm == SimulationSnapshotPayloadHashAlgorithm.JsonSha256V1
            && renderFrame.PresenterFrame.PublicationSequence > differentFrameRequest.PresenterFrame.PublicationSequence
            && !string.IsNullOrWhiteSpace(renderFrame.PresenterFrame.PayloadHash)
            && renderFrame.PresenterFrame.PayloadHash == sameRenderFrame.PresenterFrame.PayloadHash
            && renderFrame.PresenterFrame.PublicationSequence == sameRenderFrame.PresenterFrame.PublicationSequence
            && !renderFrame.PresenterFrame.CanDiffFromPrevious
            && renderFrame.PresenterFrame.DeltaBasePayloadHash is null
            && differentRenderRequest.PresenterFrame.PublicationSequence > renderFrame.PresenterFrame.PublicationSequence
            && differentRenderRequest.PresenterFrame.PayloadHash != renderFrame.PresenterFrame.PayloadHash
            && !differentRenderRequest.PresenterFrame.CanDiffFromPrevious
            && differentRenderRequest.PresenterFrame.DeltaBasePayloadHash is null,
            "Runtime frame-render presenter-frame metadata did not expose a stable full-snapshot payload identity.");

        RegressionAssert.True(
            renderFrame.MapViewport.Delta.SchemaVersion == SimulationMapViewportDeltaSchema.CurrentVersion
            && renderFrame.MapViewport.Delta.IsAvailable
            && renderFrame.MapViewport.Delta.PayloadHashAlgorithm == SimulationSnapshotPayloadHashAlgorithm.CanonicalSha256V1
            && !string.IsNullOrWhiteSpace(renderFrame.MapViewport.Delta.PayloadHash)
            && !renderFrame.MapViewport.Delta.CanApplyToBase
            && renderFrame.MapViewport.Delta.BasePayloadHash is null
            && renderFrame.MapViewport.Delta.ChangedCells.Count == renderFrame.MapViewport.Width * renderFrame.MapViewport.Height
            && renderFrame.MapViewport.Delta.ChangedRows.Count == renderFrame.MapViewport.Height
            && renderFrame.MapViewport.Delta.ChangedRows.All(row =>
                row.PayloadHashAlgorithm == SimulationSnapshotPayloadHashAlgorithm.CanonicalSha256V1
                && !string.IsNullOrWhiteSpace(row.PayloadHash)
                && row.Cells.Count == renderFrame.MapViewport.Width)
            && renderFrame.MapViewport.Delta.ChangedRegions.Count == ExpectedMapViewportRegionCount(
                renderFrame.MapViewport.Width,
                renderFrame.MapViewport.Height)
            && renderFrame.MapViewport.Delta.ChangedRegions.All(region =>
                region.PayloadHashAlgorithm == SimulationSnapshotPayloadHashAlgorithm.CanonicalSha256V1
                && !string.IsNullOrWhiteSpace(region.PayloadHash)
                && region.Width > 0
                && region.Height > 0
                && region.Cells.Count <= region.Width * region.Height)
            && sameRenderFrame.MapViewport.Delta.PayloadHash == renderFrame.MapViewport.Delta.PayloadHash
            && sameRenderFrame.MapViewport.Delta.ChangedCells.Count == renderFrame.MapViewport.Delta.ChangedCells.Count
            && sameRenderFrame.MapViewport.Delta.ChangedRows.Count == renderFrame.MapViewport.Delta.ChangedRows.Count
            && sameRenderFrame.MapViewport.Delta.ChangedRegions.Count == renderFrame.MapViewport.Delta.ChangedRegions.Count
            && differentRenderRequest.MapViewport.Delta.IsAvailable
            && !differentRenderRequest.MapViewport.Delta.CanApplyToBase
            && differentRenderRequest.MapViewport.Delta.BasePayloadHash is null
            && differentRenderRequest.MapViewport.Delta.PayloadHash != renderFrame.MapViewport.Delta.PayloadHash,
            "Runtime frame-render map viewport did not expose full-snapshot changed-cell/row/region delta metadata.");

        Console.WriteLine("[PASS] Runtime stockpile preset menu uses content catalog");
    }

    private static void TestRuntimeFramePublisherPresenterDiffBase()
    {
        var publisher = new RuntimeFrameSnapshotPublisher();
        var request = new RuntimeUiOverlayFrameRequest(
            CurrentZ: 0,
            Viewport: new RuntimeRect(0, 0, 10, 10),
            ShowZoneOverlay: false,
            IncludeManagementDrawer: false,
            IncludeWorkDrawer: false,
            IncludeDebugMenu: false,
            StockpileDetailZoneId: null,
            ZoneDetailId: null);

        var first = publisher.PublishUiOverlayFrame(
            session: null,
            runtimeTick: 10,
            allowCache: false,
            request);
        var second = publisher.PublishUiOverlayFrame(
            session: null,
            runtimeTick: 11,
            allowCache: false,
            request);
        var changedRequest = publisher.PublishUiOverlayFrame(
            session: null,
            runtimeTick: 12,
            allowCache: false,
            request with { CurrentZ = 1 });
        publisher.Invalidate();
        var afterInvalidate = publisher.PublishUiOverlayFrame(
            session: null,
            runtimeTick: 13,
            allowCache: false,
            request);

        RegressionAssert.True(
            first.PresenterFrame.TransferMode == SimulationSnapshotTransferMode.FullSnapshot
            && !first.PresenterFrame.CanDiffFromPrevious
            && first.PresenterFrame.DeltaBasePayloadHash is null
            && second.PresenterFrame.CanDiffFromPrevious
            && second.PresenterFrame.DeltaBasePayloadHash == first.PresenterFrame.PayloadHash
            && second.PresenterFrame.PublicationSequence > first.PresenterFrame.PublicationSequence
            && changedRequest.PresenterFrame.PublicationSequence > second.PresenterFrame.PublicationSequence
            && !changedRequest.PresenterFrame.CanDiffFromPrevious
            && changedRequest.PresenterFrame.DeltaBasePayloadHash is null
            && afterInvalidate.PresenterFrame.PublicationSequence > changedRequest.PresenterFrame.PublicationSequence
            && !afterInvalidate.PresenterFrame.CanDiffFromPrevious
            && afterInvalidate.PresenterFrame.DeltaBasePayloadHash is null,
            "Runtime frame publisher did not expose stable presenter-frame diff base metadata.");

        RegressionAssert.True(
            first.Delta.IsAvailable
            && !first.Delta.CanApplyToBase
            && first.Delta.BasePayloadHash is null
            && first.Delta.ChangedSections.Count == first.Delta.SectionHashes.Count
            && second.Delta.CanApplyToBase
            && second.Delta.BasePayloadHash == first.Delta.PayloadHash
            && second.Delta.PayloadHash == first.Delta.PayloadHash
            && second.Delta.ChangedSections.Count == 0
            && !changedRequest.Delta.CanApplyToBase
            && changedRequest.Delta.BasePayloadHash is null
            && changedRequest.Delta.ChangedSections.Count == changedRequest.Delta.SectionHashes.Count
            && !afterInvalidate.Delta.CanApplyToBase
            && afterInvalidate.Delta.BasePayloadHash is null
            && afterInvalidate.Delta.ChangedSections.Count == afterInvalidate.Delta.SectionHashes.Count,
            "Runtime frame publisher did not expose UI overlay section deltas.");

        var renderRequest = new RuntimeFrameRenderRequest(
            IncludeMapViewport: true,
            FortressSize: 2,
            CameraPosition: new RuntimePoint(0, 0),
            CursorPosition: new RuntimePoint(0, 0),
            CurrentZ: 0,
            ZoomLevel: 1,
            ViewWidth: 4,
            ViewHeight: 3,
            CursorGlyph: 'X',
            NavigationMode: SimulationNavigationOverlayMode.None,
            SelectedNavigationTarget: null,
            TileInspectionWorldPosition: new RuntimePoint(0, 0),
            TileInspectionZ: 0);
        var firstRender = publisher.PublishFrameRender(
            session: null,
            runtimeTick: 20,
            allowCache: false,
            renderRequest);
        var secondRender = publisher.PublishFrameRender(
            session: null,
            runtimeTick: 21,
            allowCache: false,
            renderRequest);
        var changedRenderRequest = publisher.PublishFrameRender(
            session: null,
            runtimeTick: 22,
            allowCache: false,
            renderRequest with { CameraPosition = new RuntimePoint(1, 0) });

        RegressionAssert.True(
            firstRender.MapViewport.Delta.IsAvailable
            && !firstRender.MapViewport.Delta.CanApplyToBase
            && firstRender.MapViewport.Delta.BasePayloadHash is null
            && firstRender.MapViewport.Delta.ChangedCells.Count == firstRender.MapViewport.Width * firstRender.MapViewport.Height
            && firstRender.MapViewport.Delta.ChangedRows.Count == firstRender.MapViewport.Height
            && firstRender.MapViewport.Delta.ChangedRegions.Count == ExpectedMapViewportRegionCount(
                firstRender.MapViewport.Width,
                firstRender.MapViewport.Height)
            && secondRender.MapViewport.Delta.CanApplyToBase
            && secondRender.MapViewport.Delta.BasePayloadHash == firstRender.MapViewport.Delta.PayloadHash
            && secondRender.MapViewport.Delta.PayloadHash == firstRender.MapViewport.Delta.PayloadHash
            && secondRender.MapViewport.Delta.ChangedCells.Count == 0
            && secondRender.MapViewport.Delta.ChangedRows.Count == 0
            && secondRender.MapViewport.Delta.ChangedRegions.Count == 0
            && !changedRenderRequest.MapViewport.Delta.CanApplyToBase
            && changedRenderRequest.MapViewport.Delta.BasePayloadHash is null
            && changedRenderRequest.MapViewport.Delta.ChangedCells.Count == changedRenderRequest.MapViewport.Width * changedRenderRequest.MapViewport.Height
            && changedRenderRequest.MapViewport.Delta.ChangedRows.Count == changedRenderRequest.MapViewport.Height
            && changedRenderRequest.MapViewport.Delta.ChangedRegions.Count == ExpectedMapViewportRegionCount(
                changedRenderRequest.MapViewport.Width,
                changedRenderRequest.MapViewport.Height),
            "Runtime frame publisher did not expose changed-cell/row/region map viewport deltas.");

        Console.WriteLine("[PASS] Runtime frame publisher exposes presenter-frame diff base metadata");
    }

    private static int ExpectedMapViewportRegionCount(int width, int height)
    {
        int regionSize = SimulationMapViewportDeltaSchema.RegionSize;
        return (((width - 1) / regionSize) + 1)
            * (((height - 1) / regionSize) + 1);
    }

    private static void TestAppMapViewportPresenterConsumesRuntimeDeltas()
    {
        var cache = new FortressMapViewportPresenterCache();
        var firstRows = new[]
        {
            new MapViewportRowDeltaView(
                ScreenY: 0,
                PayloadHash: "row-0-a",
                PayloadHashAlgorithm: SimulationSnapshotPayloadHashAlgorithm.CanonicalSha256V1,
                Cells: new[] { ViewCell(0, 0, 'a'), ViewCell(1, 0, 'b') }),
            new MapViewportRowDeltaView(
                ScreenY: 1,
                PayloadHash: "row-1-a",
                PayloadHashAlgorithm: SimulationSnapshotPayloadHashAlgorithm.CanonicalSha256V1,
                Cells: new[] { ViewCell(0, 1, 'c'), ViewCell(1, 1, 'd') })
        };
        var first = new SimulationMapViewportData(
            IsAvailable: true,
            HasWorld: true,
            Width: 2,
            Height: 2,
            CameraX: 0,
            CameraY: 0,
            CurrentZ: 0,
            Cells: Array.Empty<MapViewportCellView>(),
            Delta: SimulationMapViewportDeltaData.FullSnapshot(
                "map-a",
                Array.Empty<MapViewportCellView>(),
                firstRows,
                Array.Empty<MapViewportRegionDeltaView>()));

        var firstPresented = cache.Present(first);
        var firstGlyphs = firstPresented.Cells.ToDictionary(static cell => (cell.ScreenX, cell.ScreenY), static cell => cell.Glyph);

        var changedRow = new MapViewportRowDeltaView(
            ScreenY: 1,
            PayloadHash: "row-1-b",
            PayloadHashAlgorithm: SimulationSnapshotPayloadHashAlgorithm.CanonicalSha256V1,
            Cells: new[] { ViewCell(0, 1, 'e'), ViewCell(1, 1, 'f') });
        var second = first with
        {
            Cells = Array.Empty<MapViewportCellView>(),
            Delta = SimulationMapViewportDeltaData.Delta(
                "map-b",
                "map-a",
                Array.Empty<MapViewportCellView>(),
                new[] { changedRow },
                Array.Empty<MapViewportRegionDeltaView>())
        };
        var secondPresented = cache.Present(second);
        var secondGlyphs = secondPresented.Cells.ToDictionary(static cell => (cell.ScreenX, cell.ScreenY), static cell => cell.Glyph);

        var third = first with
        {
            Cells = Array.Empty<MapViewportCellView>(),
            Delta = SimulationMapViewportDeltaData.Delta(
                "map-c",
                "map-b",
                new[] { ViewCell(1, 0, 'z') },
                Array.Empty<MapViewportRowDeltaView>(),
                Array.Empty<MapViewportRegionDeltaView>())
        };
        var thirdPresented = cache.Present(third);
        var thirdGlyphs = thirdPresented.Cells.ToDictionary(static cell => (cell.ScreenX, cell.ScreenY), static cell => cell.Glyph);

        RegressionAssert.True(
            firstPresented.Cells.Count == 4
            && firstGlyphs[(0, 0)] == 'a'
            && firstGlyphs[(1, 1)] == 'd'
            && secondPresented.Cells.Count == 4
            && secondGlyphs[(0, 0)] == 'a'
            && secondGlyphs[(1, 0)] == 'b'
            && secondGlyphs[(0, 1)] == 'e'
            && secondGlyphs[(1, 1)] == 'f'
            && thirdPresented.Cells.Count == 4
            && thirdGlyphs[(0, 0)] == 'a'
            && thirdGlyphs[(1, 0)] == 'z'
            && thirdGlyphs[(0, 1)] == 'e'
            && thirdGlyphs[(1, 1)] == 'f',
            "App map viewport presenter did not compose Runtime row/cell deltas into a full viewport.");

        Console.WriteLine("[PASS] App map viewport presenter consumes Runtime deltas");
    }

    private static MapViewportCellView ViewCell(int x, int y, char glyph)
    {
        return new MapViewportCellView(
            x,
            y,
            glyph,
            new SnapshotColor(200, 200, 200));
    }

    private static void TestAppUiOverlayPresenterConsumesRuntimeSectionDeltas()
    {
        var cache = new FortressUiOverlayPresenterCache();
        var first = new SimulationUiOverlayFrameData(
            BuildCatalog: BuildCatalog("core_workshop_kitchen"),
            Jobs: null,
            Workshops: new SimulationWorkshopDebugData(Array.Empty<WorkshopSummaryView>(), 0, 0, 0),
            StockpilePresets: PresetMenu("all", "All"),
            StockpileOverlay: new SimulationStockpileOverlayData(new[] { new StockpileOverlayCellView(4, 5) }),
            StockpileDetail: null,
            ZoneOverlay: SimulationZoneOverlayData.Empty,
            ZoneDetail: null,
            ManagementDrawer: SimulationManagementDrawerData.Empty,
            WorkDrawer: null,
            DebugMenu: null,
            Delta: SimulationUiOverlayFrameDeltaData.FullSnapshot(
                "overlay-a",
                OverlaySectionHashes("a"),
                OverlaySectionNames()));
        var firstPresented = cache.Present(first);

        var second = first with
        {
            BuildCatalog = new SimulationBuildCatalogData(Array.Empty<BuildableConstructionView>()),
            StockpilePresets = PresetMenu("wood", "Wood"),
            StockpileOverlay = SimulationStockpileOverlayData.Empty,
            Delta = SimulationUiOverlayFrameDeltaData.Delta(
                "overlay-b",
                "overlay-a",
                OverlaySectionHashes("b"),
                new[] { SimulationUiOverlayFrameSection.StockpilePresets })
        };
        var secondPresented = cache.Present(second);

        RegressionAssert.True(
            firstPresented.BuildCatalog.Workshops.Count == 1
            && firstPresented.StockpilePresets.Options[0].Id == "all"
            && firstPresented.StockpileOverlay.Cells.Count == 1
            && secondPresented.BuildCatalog.Workshops.Count == 1
            && secondPresented.BuildCatalog.Workshops[0].Id == "core_workshop_kitchen"
            && secondPresented.StockpilePresets.Options.Count == 1
            && secondPresented.StockpilePresets.Options[0].Id == "wood"
            && secondPresented.StockpileOverlay.Cells.Count == 1
            && secondPresented.StockpileOverlay.Cells[0].X == 4
            && secondPresented.ManagementDrawer.HasValue
            && secondPresented.ManagementDrawer.Value.HasWorld == false,
            "App UI overlay presenter did not compose Runtime section deltas into a full overlay frame.");

        Console.WriteLine("[PASS] App UI overlay presenter consumes Runtime section deltas");
    }

    private static SimulationBuildCatalogData BuildCatalog(string workshopId)
    {
        return new SimulationBuildCatalogData(new[]
        {
            new BuildableConstructionView(
                workshopId,
                "Kitchen",
                "workshops",
                3,
                3,
                1,
                "walkable",
                Array.Empty<string>())
        });
    }

    private static SimulationStockpilePresetMenuData PresetMenu(
        string id,
        string name)
    {
        return new SimulationStockpilePresetMenuData(new[]
        {
            new StockpilePresetMenuOptionView(id, name, 1)
        });
    }

    private static SimulationUiOverlaySectionHashData[] OverlaySectionHashes(string suffix)
    {
        return OverlaySectionNames()
            .Select(section => new SimulationUiOverlaySectionHashData(section, $"{section}:{suffix}"))
            .ToArray();
    }

    private static string[] OverlaySectionNames()
    {
        return new[]
        {
            SimulationUiOverlayFrameSection.BuildCatalog,
            SimulationUiOverlayFrameSection.Jobs,
            SimulationUiOverlayFrameSection.Workshops,
            SimulationUiOverlayFrameSection.StockpilePresets,
            SimulationUiOverlayFrameSection.StockpileOverlay,
            SimulationUiOverlayFrameSection.StockpileDetail,
            SimulationUiOverlayFrameSection.ZoneOverlay,
            SimulationUiOverlayFrameSection.ZoneDetail,
            SimulationUiOverlayFrameSection.ManagementDrawer,
            SimulationUiOverlayFrameSection.WorkDrawer,
            SimulationUiOverlayFrameSection.DebugMenu
        };
    }

    private static void TestSpawnItemCommandUsesItemDiff()
    {
        var scheduler = new TickScheduler();
        var commandQueue = new CommandQueue();
        var diffLog = new DiffLog();
        var itemsDiffLog = new ItemsDiffLog();
        var creaturesDiffLog = new CreaturesDiffLog();
        var mutationDiffs = new RuntimeMutationDiffLogs(itemsDiffLog, creaturesDiffLog);
        var world = new World(2, 10);
        DefinitionCatalogTestSupport.LoadItems(world);
        var context = CreateRuntimeContext(diffLog, mutationDiffs, world);
        var pipeline = new SimulationTickPipeline(world, commandQueue, context, context, diffLog, mutationDiffs, navigation: null);
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
        var mutationDiffs = new RuntimeMutationDiffLogs(itemsDiffLog, creaturesDiffLog);
        var world = new World(2, 10);
        DefinitionCatalogTestSupport.LoadCreatures(world);
        var context = CreateRuntimeContext(diffLog, mutationDiffs, world);
        var pipeline = new SimulationTickPipeline(world, commandQueue, context, context, diffLog, mutationDiffs, navigation: null);
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
        var lowGrassland = new WorldTile { Elevation = 0.29f, RiverClass = 0 };
        var low = new WorldTile { Elevation = 0.2f, RiverClass = 0 };
        var high = new WorldTile { Elevation = 0.9f, RiverClass = 0 };
        var river = new WorldTile { Elevation = 0.5f, RiverClass = 3 };

        RegressionAssert.True(valid.IsEmbarkable && valid.GetEmbarkabilityFailures().Count == 0, "Valid embark tile reported failures.");
        RegressionAssert.True(lowGrassland.IsEmbarkable && lowGrassland.GetEmbarkabilityFailures().Count == 0, "Low grassland embark tile reported failures.");
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
            && stats.IntakeCraft == craftJobs.LastIntakeCount
            && stats.PlanStageCount == 5
            && stats.ApplyStageCount == 4,
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
                { "item_id": "core_item_boulder_granite", "min": 2, "max": 2 },
                { "item_id": "core_item_trace_granite", "min": 1, "max": 3 }
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
        var wallDropsAgain = resolver.ChooseDropsFor(geology.GraniteWallHandle, TerrainKind.SolidWall);
        var rampDrops = resolver.ChooseDropsFor(geology.GraniteWallHandle, TerrainKind.Ramp);
        var aliasDrops = resolver.ChooseDropsFor(geology.AliasGraniteWallHandle, TerrainKind.SolidWall);

        RegressionAssert.True(
            resolver.CalculateRequiredTicks(geology.GraniteWallHandle, TerrainKind.SolidWall) == 17
            && resolver.CalculateRequiredTicks(geology.GraniteWallHandle, TerrainKind.Ramp) == 5
            && resolver.ResolveAirGeologyHandle() == geology.AirHandle
            && wallDrops.Count == 2
            && wallDrops[0].itemId == "core_item_boulder_granite"
            && wallDrops[0].qty == 2
            && wallDrops[1].itemId == "core_item_trace_granite"
            && wallDrops.SequenceEqual(wallDropsAgain)
            && rampDrops.Count == 1
            && rampDrops[0].qty == 1
            && aliasDrops.Count == 2
            && aliasDrops[0].itemId == "core_item_boulder_granite"
            && aliasDrops[1].itemId == "core_item_trace_granite",
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

    private sealed class FailingReadTickSystem : ITick
    {
        public int ReadCount { get; private set; }
        public int WriteCount { get; private set; }
        public int Priority => 50;
        public string SystemId => "FailingReadSystem";

        public void ReadTick(ulong tick)
        {
            ReadCount++;
            throw new InvalidOperationException("intentional read failure");
        }

        public void WriteTick(ulong tick)
        {
            WriteCount++;
        }
    }

    private sealed class TestCommand : ICommand
    {
        private readonly List<string>? _executionLog;
        private readonly bool _throwOnExecute;
        private readonly byte[] _payload;

        public TestCommand(
            ulong tick,
            string type,
            List<string>? executionLog = null,
            Guid? commandId = null,
            bool throwOnExecute = false,
            byte[]? payload = null)
        {
            Tick = tick;
            CommandId = commandId ?? Guid.NewGuid();
            CommandType = type;
            _executionLog = executionLog;
            _throwOnExecute = throwOnExecute;
            _payload = payload?.ToArray() ?? Array.Empty<byte>();
        }

        public ulong Tick { get; }
        public Guid CommandId { get; }
        public string CommandType { get; }
        public bool Executed { get; private set; }

        public void Execute(ISimulationContext context)
        {
            if (_throwOnExecute)
            {
                throw new InvalidOperationException("intentional command failure");
            }

            Executed = true;
            _executionLog?.Add(CommandType);
        }

        public byte[] Serialize()
        {
            return _payload.ToArray();
        }
    }

    private sealed class RecordingDiagnosticSink : IDiagnosticSink
    {
        private readonly List<DiagnosticEvent> _events = new();

        public IReadOnlyList<DiagnosticEvent> Events => _events;

        public void Write(DiagnosticEvent diagnosticEvent)
        {
            _events.Add(diagnosticEvent);
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
        private readonly Dictionary<ushort, HumanFortress.Contracts.Content.Registry.GeologyData> _byHandle;
        private readonly Dictionary<string, ushort> _handles;

        public TestRuntimeGeologyCatalog()
        {
            var granite = new HumanFortress.Contracts.Content.Registry.GeologyData
            {
                Id = "core_geology_granite",
                Material = "granite"
            };
            var aliasGranite = new HumanFortress.Contracts.Content.Registry.GeologyData
            {
                Id = "core_terrain_wall_rock_granite",
                Material = "granite"
            };
            var air = new HumanFortress.Contracts.Content.Registry.GeologyData
            {
                Id = "core_geology_air",
                Material = "air"
            };

            _byHandle = new Dictionary<ushort, HumanFortress.Contracts.Content.Registry.GeologyData>
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

        public HumanFortress.Contracts.Content.Registry.GeologyData? GetGeologyEntry(string id)
        {
            return _handles.TryGetValue(id, out var handle) ? GetGeologyByHandle(handle) : null;
        }

        public HumanFortress.Contracts.Content.Registry.GeologyData? GetGeologyByHandle(ushort handle)
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
            return _recipes.Values.OrderBy(recipe => recipe.Id, StringComparer.Ordinal);
        }
    }

    private sealed class TestRuntimeCommandContext :
        IRuntimeCommandClockContext,
        ISimulationContext,
        IRuntimeProfessionCommandTargetContext,
        IRuntimeItemSpawnCommandTargetContext,
        IRuntimeCreatureSpawnCommandTargetContext,
        IRuntimeOrderCommandTargetContext,
        IRuntimeZoneCommandTargetContext,
        IRuntimeWorkshopCommandTargetContext,
        IRuntimeStockpileCommandTargetContext
    {
        private readonly SimulationRuntimeCommandTargets _commandTargets;

        public TestRuntimeCommandContext(
            DiffLog diffLog,
            World world,
            IEventBus eventBus,
            RuntimeMutationDiffLogs mutationDiffs)
        {
            DiffLog = diffLog;
            World = world;
            EventBus = eventBus;
            _commandTargets = new SimulationRuntimeCommandTargets(
                world,
                mutationDiffs,
                RecipeCatalogStore.Empty);
        }

        public DiffLog DiffLog { get; }
        public ulong CurrentTick { get; private set; }
        public IWorldReader World { get; }
        public IEventBus EventBus { get; }
        public IProfessionAssignmentCommandTarget Professions => _commandTargets.Professions;
        public IItemSpawnCommandTarget Items => _commandTargets.Items;
        public ICreatureSpawnCommandTarget Creatures => _commandTargets.Creatures;
        public IOrderCommandTarget Orders => _commandTargets.Orders;
        public IZoneCommandTarget Zones => _commandTargets.Zones;
        public IWorkshopQueueCommandTarget Workshops => _commandTargets.Workshops;
        public IStockpileCommandTarget Stockpiles => _commandTargets.Stockpiles;

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

    private readonly record struct TestGameEvent(ulong Tick, string EventType) : IGameEvent;
}
