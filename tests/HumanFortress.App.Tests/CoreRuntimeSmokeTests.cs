using System.Collections.Immutable;
using HumanFortress.Runtime.Commands;
using HumanFortress.Runtime.Jobs;
using HumanFortress.Jobs;
using HumanFortress.App;
using HumanFortress.Content.Definitions;
using HumanFortress.Content.Loading;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Determinism;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Save;
using HumanFortress.Contracts.Simulation.Save;
using HumanFortress.Core.Events;
using HumanFortress.Core.Diagnostics;
using HumanFortress.Core.Random;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Core.World;
using HumanFortress.Contracts.Runtime.Snapshots;
using HumanFortress.Jobs.Craft;
using HumanFortress.Jobs.Mining;
using HumanFortress.Jobs.Replay;
using HumanFortress.Jobs.Transport;
using HumanFortress.Runtime;
using HumanFortress.Runtime.Replay;
using HumanFortress.Runtime.Save;
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
        TestDiffLog();
        TestTypedCommandDiffOrderingPolicy();
        TestStockpileMessageDrainSortKeyIsStable();
        TestMiningRectanglesIncludeSingleCellMaxExtent();
        TestStockpileFilterUsesItemProjection();
        TestStockpileDataIndexUpdatesAreIdempotent();
        TestTransportStockpileIndexEmitterUsesStockpileDiffs();
        TestHaulingPlannerReservesStockpileCapacity();
        TestHaulingPlannerDoesNotReserveDuplicatePendingTransport();
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

        RegressionAssert.True(
            ReferenceEquals(stream1, stream2)
            && snapshot.Select(state => state.StreamName).SequenceEqual(new[] { "alpha", "beta", "test" })
            && snapshotHash == reverseSnapshotHash
            && snapshotHash != changedSnapshotHash
            && snapshotHash == restoredSnapshotHash,
            "RngStreamManager did not expose stable canonical stream state snapshots.");

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
        var restore = WorldSavePayloadRestorer.RestoreSupportedSections(payload);
        var restoredHash = restore.World == null
            ? string.Empty
            : WorldReplayHashBuilder.Build(restore.World);
        var restoredItem = restore.World?.Items.GetInstance(itemId);
        var restoredCreature = restore.World?.Creatures.GetInstance(creatureId);
        var restoredItemReservations = restore.World?.Reservations.GetItemReservationsSnapshot() ?? Array.Empty<(Guid itemId, Guid holderId, ulong expireTick)>();
        var restoredCreatureReservations = restore.World?.Reservations.GetCreatureReservationsSnapshot() ?? Array.Empty<(Guid workerId, string holderSystem, string? jobId, ulong expireTick)>();
        var restoredStockpile = restore.World?.Stockpiles.GetZone(stockpileId);
        var placeableWorld = CreateReplayHashWorld();
        var placeablePayload = WorldSavePayloadBuilder.Build(placeableWorld);
        var placeableSectionRestore = WorldSavePayloadRestorer.RestoreSupportedSections(placeablePayload);
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
            && payload.Items.Length == 1
            && payload.Counts.ItemCount == 1
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
            && restoredItem.Improvements?.Count == 1
            && restoredItem.Perishable?.CreatedAtTick == 6
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
            "World save payload did not restore supported world sections and placeable/workshop authority by hash.");

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
            rngDocumentServices.RngStreams.GetStateSnapshot(),
            Array.Empty<CommandReplayRecord>(),
            Array.Empty<CommandReplayRecord>()).ToDocumentData();
        var rngDocumentRoundTrip = RuntimeSaveSnapshotDocumentCodec.Deserialize(
            RuntimeSaveSnapshotDocumentCodec.Serialize(rngDocument));
        var mappedRngStreams = RuntimeSaveSnapshotDocumentRngMapper.ToRngStreamStateSnapshots(rngDocumentRoundTrip);
        var rngRestoreServices = new RuntimeSessionServices();
        _ = rngRestoreServices.RngStreams.GetStream("stale.before.restore").Next();
        var rngRestore = RuntimeSaveSnapshotRngRestorer.Restore(rngRestoreServices, rngDocumentRoundTrip);

        RegressionAssert.True(
            rngDocumentRoundTrip.RngStreams.Length == 1
            && mappedRngStreams.Count == 1
            && mappedRngStreams[0].StreamName == "save.rng"
            && RngReplayHashBuilder.Build(mappedRngStreams) == rngDocumentCheckpoint.RngHash
            && rngRestore.Success
            && rngRestore.RestoredStreamCount == 1
            && rngRestoreServices.RngStreams.GetStateSnapshot().Count == 1
            && RngReplayHashBuilder.Build(rngRestoreServices.RngStreams) == rngDocumentCheckpoint.RngHash,
            "Runtime save snapshot document did not preserve and restore RNG stream payload rows.");

        var runtime = FortressRuntimeSessionFactory.Create(
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
            && noWorldManifest.Checkpoint.AggregateHash == noWorldData.AggregateHash
            && manifest.FormatVersion == RuntimeSaveFormat.CurrentVersion
            && manifest.Content.HasContent
            && !string.IsNullOrWhiteSpace(manifest.Content.ContentHash)
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
            && sections["commands.pending"].RecordCount == worldData.PendingCommandLogRecordCount,
            "Runtime save manifest did not bind checkpoint sections and content signature correctly.");

        RegressionAssert.True(
            saveSnapshot.Manifest.Checkpoint.AggregateHash == manifest.Checkpoint.AggregateHash
            && saveSnapshot.WorldPayload.HasValue
            && saveSnapshot.WorldPayload.Value.ReplayHash == worldData.WorldHash
            && saveSnapshot.RngStreams.Length == worldData.RngStreamCount
            && RngReplayHashBuilder.Build(RuntimeSaveSnapshotDocumentRngMapper.ToRngStreamStateSnapshots(saveSnapshot)) == worldData.RngHash
            && saveSnapshot.ExecutedCommandRecords.Length == worldData.CommandLogRecordCount
            && saveSnapshot.PendingCommandRecords.Length == worldData.PendingCommandLogRecordCount
            && saveSnapshot.Manifest.Checkpoint.CommandLogHash == worldData.CommandLogHash
            && saveSnapshot.Manifest.Checkpoint.PendingCommandLogHash == worldData.PendingCommandLogHash,
            "Runtime save snapshot document did not bind the manifest to the command replay journal snapshot.");

        var pendingRuntime = FortressRuntimeSessionFactory.Create(
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
        var directoryValidationResult = pendingRuntime.ValidateSaveSnapshotDirectory(documentStoreDirectory);

        RegressionAssert.True(
            storedDocument.Manifest.Checkpoint.AggregateHash == pendingDocumentRoundTrip.Manifest.Checkpoint.AggregateHash
            && replacedDocument.Manifest.Checkpoint.AggregateHash == pendingDocumentRoundTrip.Manifest.Checkpoint.AggregateHash
            && storedDocument.PendingCommandRecords.Length == 1
            && replacedDocument.PendingCommandRecords.Length == 1
            && directoryValidationResult.Success,
            "Runtime save snapshot document store did not atomically write/read the save document.");

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

        var restoreRuntime = FortressRuntimeSessionFactory.Create(
            AppContext.BaseDirectory,
            strictContent: false,
            contentWarningsAsErrors: false);
        restoreRuntime.InitializeWorld(sizeInChunks: 2, maxZ: 2);
        var restoreResult = restoreRuntime.RestorePendingCommandsFromSaveSnapshotDocument(pendingDocumentRoundTrip);
        var directoryRestoreRuntime = FortressRuntimeSessionFactory.Create(
            AppContext.BaseDirectory,
            strictContent: false,
            contentWarningsAsErrors: false);
        directoryRestoreRuntime.InitializeWorld(sizeInChunks: 2, maxZ: 2);
        var directoryRestoreResult = directoryRestoreRuntime.RestorePendingCommandsFromSaveSnapshotDirectory(documentStoreDirectory);
        var directoryWorldRestoreRuntime = FortressRuntimeSessionFactory.Create(
            AppContext.BaseDirectory,
            strictContent: false,
            contentWarningsAsErrors: false);
        var directoryWorldRestoreResult = directoryWorldRestoreRuntime.RestoreWorldFromSaveSnapshotDirectory(documentStoreDirectory);
        var directoryFullRestoreRuntime = FortressRuntimeSessionFactory.Create(
            AppContext.BaseDirectory,
            strictContent: false,
            contentWarningsAsErrors: false);
        var directoryFullRestoreResult = directoryFullRestoreRuntime.RestoreFullFromSaveSnapshotDirectory(documentStoreDirectory);
        var directoryFullRestoredDocument = directoryFullRestoreRuntime.CreateSaveSnapshotDocumentData();
        Directory.Delete(documentStoreDirectory, recursive: true);
        var missingDirectoryValidationResult = pendingRuntime.ValidateSaveSnapshotDirectory(documentStoreDirectory);
        var missingDirectoryWorldRestoreResult = pendingRuntime.RestoreWorldFromSaveSnapshotDirectory(documentStoreDirectory);
        var missingDirectoryFullRestoreResult = pendingRuntime.RestoreFullFromSaveSnapshotDirectory(documentStoreDirectory);
        var documentWorldRestoreRuntime = FortressRuntimeSessionFactory.Create(
            AppContext.BaseDirectory,
            strictContent: false,
            contentWarningsAsErrors: false);
        var documentWorldRestoreResult = documentWorldRestoreRuntime.RestoreWorldFromSaveSnapshotDocument(pendingDocumentRoundTrip);
        var restoredWorldDocument = documentWorldRestoreRuntime.CreateSaveSnapshotDocumentData();
        var documentFullRestoreRuntime = FortressRuntimeSessionFactory.Create(
            AppContext.BaseDirectory,
            strictContent: false,
            contentWarningsAsErrors: false);
        var documentFullRestoreResult = documentFullRestoreRuntime.RestoreFullFromSaveSnapshotDocument(pendingDocumentRoundTrip);
        var documentFullRestoredDocument = documentFullRestoreRuntime.CreateSaveSnapshotDocumentData();
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
            && directoryFullRestoreResult.Success
            && directoryFullRestoreResult.Validation.Success
            && directoryFullRestoreResult.SavedWorldHash == pendingDocumentRoundTrip.WorldPayload?.ReplayHash
            && directoryFullRestoreResult.RestoredWorldHash == pendingDocumentRoundTrip.WorldPayload?.ReplayHash
            && directoryFullRestoreResult.RestoredRngStreamCount == pendingDocumentRoundTrip.RngStreams.Length
            && directoryFullRestoreResult.PendingRecordCount == 1
            && directoryFullRestoreResult.RestoredCommandCount == 1
            && directoryFullRestoredDocument.Manifest.Checkpoint.WorldHash == pendingDocumentRoundTrip.WorldPayload?.ReplayHash
            && directoryFullRestoredDocument.PendingCommandRecords.Length == 1
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
            && documentFullRestoreResult.RestoredWorldHash == pendingDocumentRoundTrip.WorldPayload?.ReplayHash
            && documentFullRestoreResult.RestoredRngStreamCount == pendingDocumentRoundTrip.RngStreams.Length
            && documentFullRestoreResult.RestoredCommandCount == 1
            && documentFullRestoredDocument.Manifest.Checkpoint.WorldHash == pendingDocumentRoundTrip.WorldPayload?.ReplayHash
            && documentFullRestoredDocument.Manifest.Checkpoint.RngHash == pendingDocumentRoundTrip.Manifest.Checkpoint.RngHash
            && documentFullRestoredDocument.PendingCommandRecords.Length == 1
            && restoredRuntimeDocument.PendingCommandRecords.Length == 2
            && restoredSequences.SequenceEqual(new long?[] { 1, 2 }),
            "Runtime save snapshot document validation/restore did not restore pending commands or advance command identity.");

        var rejectedInvalidDocument = false;
        try
        {
            _ = RuntimeSaveSnapshotDocumentCodec.Serialize(new RuntimeSaveSnapshotDocumentData(
                pendingDocument.Manifest,
                pendingDocument.WorldPayload,
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
        const int itemHandle = 42;
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
        int itemHandle = DiffTargetEncoding.SignedEntityId(itemId);

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
        HumanFortress.Navigation.NavigationManager? hostNavigation = null;
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

        Console.WriteLine("[PASS] Runtime stockpile preset menu uses content catalog");
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
            return _recipes.Values;
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
}
