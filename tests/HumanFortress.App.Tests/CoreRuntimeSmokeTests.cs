using System.Collections.Immutable;
using HumanFortress.Runtime.Commands;
using HumanFortress.Runtime.Jobs;
using HumanFortress.Jobs;
using HumanFortress.App;
using HumanFortress.Content.Definitions;
using HumanFortress.Content.Loading;
using HumanFortress.Core.Commands;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Contracts.Runtime;
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
        TestDiffLog();
        TestTypedCommandDiffOrderingPolicy();
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
            zoneDetailId: null,
            tick: 0);

        RegressionAssert.True(
            frame.StockpilePresets.Options.Any(option => option.Id == "weapons" && option.Name == "Weapon Storage")
            && frame.StockpilePresets.Options.FirstOrDefault().Id == "all",
            "Runtime stockpile preset menu did not use content-backed preset definitions.");

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

        public TestCommand(
            ulong tick,
            string type,
            List<string>? executionLog = null,
            Guid? commandId = null,
            bool throwOnExecute = false)
        {
            Tick = tick;
            CommandId = commandId ?? Guid.NewGuid();
            CommandType = type;
            _executionLog = executionLog;
            _throwOnExecute = throwOnExecute;
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
            return Array.Empty<byte>();
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
