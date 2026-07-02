using HumanFortress.Contracts.Navigation;
using HumanFortress.Jobs;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Core.Simulation;
using HumanFortress.Jobs.Craft;
using HumanFortress.Jobs.Transport;
using HumanFortress.Runtime.Jobs;
using HumanFortress.Simulation.Diff;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Jobs;
using HumanFortress.Simulation.Orders;
using HumanFortress.Simulation.Placeables;
using HumanFortress.Simulation.Stockpile;
using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;
using WorldChunkKey = HumanFortress.Simulation.World.ChunkKey;
using WorldModel = HumanFortress.Simulation.World.World;

internal static class TransportConstructionCraftRegressionTests
{
    public static void RunAll()
    {
        Console.WriteLine("=== Transport/Construction/Craft Regression Tests ===");

        TestTransportJobFinalizerReleasesReservations();
        TestTransportRequestQueueDeduplicatesByItemAndKeepsShardIndex();
        TestTransportNoPathRollbackReleasesReservations();
        TestTransportDestinationValidationFailureReleasesReservations();
        TestTransportCanPickupFromStockpileForNonStockpileJobs();
        TestTransportMovedPickupTargetReplans();
        TestTransportThrottleBacklogsRemainingRequests();
        TestConstructionTerrainCompletionRemovesSite();
        TestCraftInputFailureKeepsQueueEntry();
        TestCraftConsumesInputFromWorkshopRing();

        Console.WriteLine("=== Transport/Construction/Craft Regression Tests Completed ===\n");
    }

    private static void TestTransportJobFinalizerReleasesReservations()
    {
        var reservations = new ReservationManager();
        var itemId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var workerId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

        bool itemReserved = reservations.TryReserveItem(itemId, workerId, currentTick: 10, expireTick: 100);
        bool creatureReserved = reservations.TryReserveCreature(workerId, "Jobs.Transport", currentTick: 10, expireTick: 100, jobId: "haul:test");

        var job = new ActiveJob
        {
            CreatureId = workerId,
            ItemId = itemId,
            Dest = new Point3(1, 1, 0),
            Stage = JobStage.ToItem,
            Quantity = 1,
            Reason = TransportReason.ToStockpile
        };
        var finished = new List<ActiveJob>();

        var finalizer = new TransportJobFinalizer(reservations);
        finalizer.Finish(job, finished);

        RegressionAssert.True(itemReserved, "Transport finalizer setup failed to reserve item.");
        RegressionAssert.True(creatureReserved, "Transport finalizer setup failed to reserve creature.");
        RegressionAssert.True(!reservations.IsItemReserved(itemId, currentTick: 11), "Transport finalizer did not release item reservation.");
        RegressionAssert.True(!reservations.IsCreatureReserved(workerId, currentTick: 11, out _, out _), "Transport finalizer did not release creature reservation.");
        RegressionAssert.True(finished.Count == 1 && ReferenceEquals(finished[0], job), "Transport finalizer did not track finished job.");

        Console.WriteLine("[PASS] Transport finalizer releases reservations");
    }

    private static void TestTransportRequestQueueDeduplicatesByItemAndKeepsShardIndex()
    {
        var itemId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var source = new Point(1, 1);
        var dest = new Point(2, 1);
        var competingDest = new Point(33, 1);
        var queue = new TransportRequestQueue();

        bool firstAccepted = queue.Enqueue(new TransportRequest(
            itemId,
            source,
            FromZ: 0,
            dest,
            ToZ: 0,
            Quantity: 1,
            TransportReason.Misc,
            Priority: 0,
            RequestorId: "test",
            CreatedTick: 0,
            Seed: 0));
        bool mergedAccepted = queue.Enqueue(new TransportRequest(
            itemId,
            source,
            FromZ: 0,
            dest,
            ToZ: 0,
            Quantity: 2,
            TransportReason.Misc,
            Priority: 0,
            RequestorId: "test",
            CreatedTick: 1,
            Seed: 0));

        var shardCounts = queue.GetShardCountsSnapshot();
        RegressionAssert.True(firstAccepted, "Transport queue rejected the first request for an item.");
        RegressionAssert.True(!mergedAccepted, "Transport queue reported a merged duplicate as a new request.");
        RegressionAssert.True(queue.Count == 1, "Transport queue kept duplicate pending requests for one item.");
        RegressionAssert.True(shardCounts.Count == 1 && shardCounts.Values.FirstOrDefault() == 1, "Transport queue shard index did not match merged pending requests.");

        var drained = new List<TransportRequest>();
        int drainedCount = queue.Drain(10, drained);
        RegressionAssert.True(drainedCount == 1 && drained[0].Quantity == 3, "Transport queue did not drain the merged request quantity.");
        RegressionAssert.True(queue.Count == 0 && queue.GetShardCountsSnapshot().Count == 0, "Transport queue left stale shard data after draining a merged request.");

        bool secondRoundAccepted = queue.Enqueue(new TransportRequest(
            itemId,
            source,
            FromZ: 0,
            dest,
            ToZ: 0,
            Quantity: 1,
            TransportReason.Misc,
            Priority: 0,
            RequestorId: "test",
            CreatedTick: 2,
            Seed: 0));
        bool competingAccepted = queue.Enqueue(new TransportRequest(
            itemId,
            source,
            FromZ: 0,
            competingDest,
            ToZ: 0,
            Quantity: 1,
            TransportReason.Misc,
            Priority: 0,
            RequestorId: "test",
            CreatedTick: 3,
            Seed: 0));

        drained.Clear();
        queue.Drain(10, drained);
        RegressionAssert.True(secondRoundAccepted && !competingAccepted, "Transport queue did not reject competing destinations for one pending item.");
        RegressionAssert.True(drained.Count == 1 && drained[0].To == dest, "Transport queue did not preserve the earlier destination for a pending item.");

        Console.WriteLine("[PASS] Transport request queue item dedupe");
    }

    private static void TestTransportNoPathRollbackReleasesReservations()
    {
        var world = CreateWorldWithContent();
        var source = new Point(1, 1);
        var blockedDest = new Point(2, 1);
        SetOpen(world, source);
        world.SetTile(blockedDest.X, blockedDest.Y, 0, new TileBase(0, (ushort)TerrainKind.SolidWall, 0, 0, 0, 0, 1), 0);

        var workerId = world.Creatures.SpawnCreature("core_race_dwarf", source, 0, "player", 0);
        var itemId = world.Items.SpawnItem("core_item_log_oak", source, 0, 1, 0);
        RegressionAssert.True(workerId.HasValue && itemId.HasValue, "Transport no-path setup failed.");
        var worker = workerId.GetValueOrDefault();
        var item = itemId.GetValueOrDefault();

        var requests = new TransportRequestQueue();
        requests.Enqueue(new TransportRequest(
            item,
            source,
            FromZ: 0,
            blockedDest,
            ToZ: 0,
            Quantity: 1,
            TransportReason.ToStockpile,
            Priority: 0,
            RequestorId: "test",
            CreatedTick: 0,
            Seed: 0));

        var diffLog = new DiffLog();
        var itemsDiffLog = new ItemsDiffLog();
        var transport = new TransportJobSystem(
            world,
            requests,
            diffLog,
            itemsDiffLog: itemsDiffLog,
            intakeBudget: 1,
            maxActiveJobs: 1,
            pathService: new PickupFoundDestinationBlockedPathService());

        transport.ReadTick(0);
        transport.WriteTick(0);

        var merged = diffLog.MergeAndSort();
        RegressionAssert.True(merged.Count(op => op.Op == DiffOpType.MarkCarried) == 1, "Transport no-path rollback did not mark item carried before pickup.");
        RegressionAssert.True(merged.Count(op => op.Op == DiffOpType.UnmarkCarried) == 1, "Transport no-path rollback did not unmark carried state.");
        RegressionAssert.True(!world.Reservations.IsItemReserved(item, currentTick: 1), "Transport no-path rollback leaked item reservation.");
        RegressionAssert.True(!world.Reservations.IsCreatureReserved(worker, currentTick: 1, out _, out _), "Transport no-path rollback leaked creature reservation.");
        RegressionAssert.True(transport.GetActiveJobsSnapshot().Count == 0, "Transport no-path rollback left an active job.");

        Console.WriteLine("[PASS] Transport no-path rollback releases reservations");
    }

    private static void TestTransportDestinationValidationFailureReleasesReservations()
    {
        var world = CreateWorldWithContent();
        var source = new Point(1, 1);
        var invalidStockpileDest = new Point(2, 1);
        SetOpen(world, source);
        SetOpen(world, invalidStockpileDest);

        var workerId = world.Creatures.SpawnCreature("core_race_dwarf", source, 0, "player", 0);
        var itemId = world.Items.SpawnItem("core_item_log_oak", source, 0, 1, 0);
        RegressionAssert.True(workerId.HasValue && itemId.HasValue, "Transport destination-validation setup failed.");
        var worker = workerId.GetValueOrDefault();
        var item = itemId.GetValueOrDefault();

        var requests = new TransportRequestQueue();
        requests.Enqueue(new TransportRequest(
            item,
            source,
            FromZ: 0,
            invalidStockpileDest,
            ToZ: 0,
            Quantity: 1,
            TransportReason.ToStockpile,
            Priority: 0,
            RequestorId: "test",
            CreatedTick: 0,
            Seed: 0));

        var diffLog = new DiffLog();
        var itemsDiffLog = new ItemsDiffLog();
        var transport = new TransportJobSystem(
            world,
            requests,
            diffLog,
            itemsDiffLog: itemsDiffLog,
            intakeBudget: 1,
            maxActiveJobs: 1,
            pathService: new AllPathsFoundPathService());

        transport.ReadTick(0);
        for (ulong tick = 0; tick <= 4; tick++)
            transport.WriteTick(tick);

        var merged = diffLog.MergeAndSort();
        RegressionAssert.True(merged.Count(op => op.Op == DiffOpType.MarkCarried) == 1, "Destination validation rollback did not mark carried state.");
        RegressionAssert.True(merged.Count(op => op.Op == DiffOpType.UnmarkCarried) == 1, "Destination validation rollback did not unmark carried state.");
        RegressionAssert.True(merged.All(op => op.Op != DiffOpType.MoveItem), "Destination validation rollback moved item to invalid destination.");
        RegressionAssert.True(!world.Reservations.IsItemReserved(item, currentTick: 5), "Destination validation rollback leaked item reservation.");
        RegressionAssert.True(!world.Reservations.IsCreatureReserved(worker, currentTick: 5, out _, out _), "Destination validation rollback leaked creature reservation.");
        RegressionAssert.True(transport.GetActiveJobsSnapshot().Count == 0, "Destination validation rollback left active job.");

        Console.WriteLine("[PASS] Transport destination validation rollback");
    }

    private static void TestTransportCanPickupFromStockpileForNonStockpileJobs()
    {
        var world = CreateWorldWithContent();
        var source = new Point(1, 1);
        var dest = new Point(2, 1);
        SetOpen(world, source);
        SetOpen(world, dest);

        var workerId = world.Creatures.SpawnCreature("core_race_dwarf", source, 0, "player", 0);
        var itemId = world.Items.SpawnItem("core_item_log_oak", source, 0, 1, 0);
        RegressionAssert.True(workerId.HasValue && itemId.HasValue, "Transport stockpile pickup setup failed.");
        var item = itemId.GetValueOrDefault();

        var chunkKey = new WorldChunkKey(0, 0, 0);
        var chunk = world.GetChunk(chunkKey)
            ?? throw new InvalidOperationException("Expected transport stockpile test chunk.");
        chunk.EnsureStockpileData();
        var stockpileData = chunk.GetStockpileData()
            ?? throw new InvalidOperationException("Expected transport stockpile test data.");
        int zoneId = world.Stockpiles.CreateZone("Input Stockpile", chunkKey, 0);
        int sourceCell = Chunk.LocalIndex(source.X, source.Y);
        stockpileData.CreateOrUpdateShard(zoneId, chunkKey);
        stockpileData.AddCellsToZone(zoneId, new[] { sourceCell });
        stockpileData.OnItemPlaced(
            DiffTargetEncoding.SignedEntityId(item),
            sourceCell,
            zoneId,
            new List<string> { "wood" });
        world.Stockpiles.GetZone(zoneId)?.UpdateMemberChunks(new[] { chunkKey });

        var requests = new TransportRequestQueue();
        requests.Enqueue(new TransportRequest(
            item,
            source,
            FromZ: 0,
            dest,
            ToZ: 0,
            Quantity: 1,
            TransportReason.Misc,
            Priority: 0,
            RequestorId: "test",
            CreatedTick: 0,
            Seed: 0));

        var diffLog = new DiffLog();
        var stockpileDiffLog = new StockpileDiffLog();
        var transport = new TransportJobSystem(
            world,
            requests,
            diffLog,
            itemsDiffLog: new ItemsDiffLog(),
            stockpileDiffLog: stockpileDiffLog,
            intakeBudget: 1,
            maxActiveJobs: 1,
            pathService: new AllPathsFoundPathService());

        transport.ReadTick(0);
        for (ulong tick = 0; tick <= 4; tick++)
            transport.WriteTick(tick);

        var shard = stockpileData.GetShard(zoneId)
            ?? throw new InvalidOperationException("Expected transport stockpile test shard.");
        StockpileDiffApplicator.ApplyAll(world, stockpileDiffLog.MergeAndSort());
        var merged = diffLog.MergeAndSort();

        RegressionAssert.True(merged.Any(op => op.Op == DiffOpType.MarkCarried), "Transport refused to pick up a stockpiled item for a non-stockpile job.");
        RegressionAssert.True(!stockpileData.GetItemsInZone(zoneId).Any(), "Transport stockpile pickup did not remove the item from zone index.");
        RegressionAssert.True(!stockpileData.GetItemsByTag("wood").Any(), "Transport stockpile pickup did not remove the item from tag index.");
        RegressionAssert.True(shard.UsedSlots == 0, "Transport stockpile pickup did not free the stockpile slot.");

        Console.WriteLine("[PASS] Transport can pick up stockpiled items for non-stockpile jobs");
    }

    private static void TestTransportMovedPickupTargetReplans()
    {
        var world = CreateWorldWithContent();
        var source = new Point(1, 1);
        var movedPickup = new Point(2, 1);
        var dest = new Point(3, 1);
        SetOpen(world, source);
        SetOpen(world, movedPickup);
        SetOpen(world, dest);

        var workerId = world.Creatures.SpawnCreature("core_race_dwarf", source, 0, "player", 0);
        var itemId = world.Items.SpawnItem("core_item_log_oak", source, 0, 1, 0);
        RegressionAssert.True(workerId.HasValue && itemId.HasValue, "Moved pickup target setup failed.");
        var worker = workerId.GetValueOrDefault();
        var item = itemId.GetValueOrDefault();

        var requests = new TransportRequestQueue();
        requests.Enqueue(new TransportRequest(
            item,
            source,
            FromZ: 0,
            dest,
            ToZ: 0,
            Quantity: 1,
            TransportReason.Misc,
            Priority: 0,
            RequestorId: "test",
            CreatedTick: 0,
            Seed: 0));

        var diffLog = new DiffLog();
        var itemsDiffLog = new ItemsDiffLog();
        var transport = new TransportJobSystem(
            world,
            requests,
            diffLog,
            itemsDiffLog: itemsDiffLog,
            intakeBudget: 1,
            maxActiveJobs: 1,
            pathService: new AllPathsFoundPathService());

        transport.ReadTick(0);
        world.Items.UpdateItemPosition(item, source, 0, movedPickup, 0);

        transport.WriteTick(0);
        var firstPass = diffLog.MergeAndSort();
        bool noEarlyCarry = firstPass.All(op => op.Op != DiffOpType.MarkCarried)
            && transport.GetActiveJobsSnapshot().Count == 1
            && transport.GetActiveJobsSnapshot()[0].Stage == JobStage.ToItem.ToString();
        RegressionAssert.True(noEarlyCarry, "Moved pickup target was carried before replanning.");

        for (ulong tick = 1; tick <= 4; tick++)
            transport.WriteTick(tick);

        var afterRepath = diffLog.MergeAndSort();
        var active = transport.GetActiveJobsSnapshot();
        RegressionAssert.True(afterRepath.Count(op => op.Op == DiffOpType.MarkCarried) == 1, "Moved pickup target was not carried after replanning.");
        RegressionAssert.True(active.Count == 1 && active[0].Stage == JobStage.ToDest.ToString(), "Moved pickup target did not advance to destination stage.");
        RegressionAssert.True(world.Reservations.IsItemReserved(item, currentTick: 5), "Moved pickup target lost item reservation.");
        RegressionAssert.True(world.Reservations.IsCreatureReserved(worker, currentTick: 5, out _, out _), "Moved pickup target lost creature reservation.");

        Console.WriteLine("[PASS] Transport moved pickup target replans");
    }

    private static void TestTransportThrottleBacklogsRemainingRequests()
    {
        var world = CreateWorldWithContent();
        var sourceA = new Point(1, 1);
        var sourceB = new Point(1, 2);
        var destA = new Point(2, 1);
        var destB = new Point(2, 2);
        SetOpen(world, sourceA);
        SetOpen(world, sourceB);
        SetOpen(world, destA);
        SetOpen(world, destB);

        var workerId = world.Creatures.SpawnCreature("core_race_dwarf", sourceA, 0, "player", 0);
        var itemA = world.Items.SpawnItem("core_item_log_oak", sourceA, 0, 1, 0);
        var itemB = world.Items.SpawnItem("core_item_log_oak", sourceB, 0, 1, 0);
        RegressionAssert.True(workerId.HasValue && itemA.HasValue && itemB.HasValue, "Transport throttle backlog setup failed.");
        var firstItem = itemA.GetValueOrDefault();
        var secondItem = itemB.GetValueOrDefault();

        var requests = new TransportRequestQueue();
        requests.Enqueue(new TransportRequest(firstItem, sourceA, 0, destA, 0, 1, TransportReason.Misc, 0, "test", 0, 0));
        requests.Enqueue(new TransportRequest(secondItem, sourceB, 0, destB, 0, 1, TransportReason.Misc, 0, "test", 0, 0));

        var transport = new TransportJobSystem(
            world,
            requests,
            new DiffLog(),
            itemsDiffLog: new ItemsDiffLog(),
            intakeBudget: 2,
            maxActiveJobs: 1,
            pathService: new AllPathsFoundPathService());

        transport.ReadTick(0);
        RegressionAssert.True(transport.GetActiveJobsSnapshot().Count == 1, "Transport throttle did not assign first job.");
        RegressionAssert.True(transport.GetBacklogCount() == 1, "Transport throttle did not backlog remaining request.");
        RegressionAssert.True(requests.Count == 0, "Transport throttle did not drain queue into active/backlog ownership.");

        for (ulong tick = 0; tick <= 5; tick++)
            transport.WriteTick(tick);
        RegressionAssert.True(transport.GetActiveJobsSnapshot().Count == 0, "Transport throttle first job did not complete.");

        transport.ReadTick(6);
        RegressionAssert.True(transport.GetActiveJobsSnapshot().Count == 1, "Transport throttle did not recover backlogged request.");
        RegressionAssert.True(transport.GetBacklogCount() == 0, "Transport throttle backlog was not consumed.");

        Console.WriteLine("[PASS] Transport throttle preserves backlog");
    }

    private static void TestConstructionTerrainCompletionRemovesSite()
    {
        var world = new WorldModel(2, 2);
        DefinitionCatalogTestSupport.LoadItems(world);

        var target = new Point(2, 2);
        var safe = new Point(3, 2);
        SetOpen(world, target);
        SetOpen(world, safe);

        var required = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["stone_block"] = 1
        };
        var site = PlaceableFactory.CreateConstructionSite(
            target,
            z: 0,
            tickSeed: 0,
            targetId: "l0:Wall",
            fp: new Footprint(1, 1, 1),
            materialsRequired: required,
            totalBuildTicks: 1);
        PlaceableManager.PlacePlaceable(world, site, tick: 0);

        var itemId = world.Items.SpawnItem("core_item_block_granite", target, 0, quantity: 2, currentTick: 0);
        RegressionAssert.True(itemId.HasValue, "Construction terrain completion setup failed to spawn item.");

        var diffLog = new DiffLog();
        var itemsDiffLog = new ItemsDiffLog();
        var construction = new ConstructionJobSystem(
            world,
            new ConstructionSystem(
                world,
                new OrdersManager(),
                EmptyConstructionTerrainMaterialResolver.Instance,
                ConstructionTuning.Default),
            diffLog,
            itemsDiffLog,
            ConstructionCatalogStore.Empty,
            ConstructionTuning.Default,
            PlaceableTuning.Default,
            maxPerTick: 1);

        construction.WriteTick(0);

        var chunk = world.GetChunk(new WorldChunkKey(0, 0, 0));
        bool siteRemoved = chunk?.GetPlaceableData()?.TryGetOwnedAt(Chunk.LocalIndex(target.X, target.Y), out _) != true;
        var itemDiffs = itemsDiffLog.MergeAndSort();
        var terrainDiffs = diffLog.MergeAndSort();

        RegressionAssert.True(siteRemoved, "Construction completion did not remove site.");
        RegressionAssert.True(terrainDiffs.Any(op => op.Op == DiffOpType.SetTerrain), "Construction completion did not emit SetTerrain.");
        RegressionAssert.True(itemDiffs.Count(d => d.Op == ItemsDiffOp.RemoveItem) == 1, "Construction completion did not emit exactly one material removal.");

        ItemsDiffApplicator.ApplyPreSimulation(world, itemDiffs);
        SimulationDiffApplicator.ApplyAll(world, terrainDiffs);

        var remainingItem = world.Items.GetInstance(itemId.GetValueOrDefault());
        var tile = world.GetTile(target.X, target.Y, 0);
        RegressionAssert.True(remainingItem?.StackCount == 1, "Construction completion consumed incorrect material quantity.");
        RegressionAssert.True(remainingItem?.Position != target, "Construction completion did not move residual material off anchor.");
        RegressionAssert.True(tile?.Kind == TerrainKind.SolidWall, "Construction completion did not apply terrain change.");

        Console.WriteLine("[PASS] Construction terrain completion removes site");
    }

    private static void TestCraftInputFailureKeepsQueueEntry()
    {
        var world = new WorldModel(2, 2);
        var workshopPos = new Point(2, 2);
        SetOpen(world, workshopPos);
        const string recipeId = "test_recipe_missing_input";
        var recipes = CreateRecipeCatalog(recipeId, "Missing Input Test");

        var workshop = CreateWorkshop("aaaaaaaa-1111-1111-1111-aaaaaaaaaaaa", workshopPos);
        PlaceableManager.PlacePlaceable(world, workshop, tick: 0);

        var entry = workshop.Workshop!.AddEntry(recipeId, "Missing Input Test", workshop.Guid, currentTick: 0);
        workshop.Workshop.RegisterJobStart();
        entry.Status = CraftQueueStatus.InProgress;
        entry.ActiveWorkerId = Guid.Parse("bbbbbbbb-2222-2222-2222-bbbbbbbbbbbb");

        var locator = new CraftWorkshopLocator(world, ConstructionCatalogStore.Empty);
        var itemsDiffLog = new ItemsDiffLog();
        var consumer = new CraftMaterialConsumer(
            world,
            locator,
            recipes,
            new CraftDiffEmitter(itemsDiffLog, priority: 100, systemId: "test"));
        var finalizer = new CraftJobFinalizer(world, locator);

        var job = CreateCraftJob(entry, workshop, recipeId, workshopPos);
        bool consumed = consumer.TryConsumeInputs(job);
        finalizer.Finish(job, CraftJobFinishReason.InputsUnavailable);

        RegressionAssert.True(!consumed, "Craft input failure unexpectedly consumed inputs.");
        RegressionAssert.True(workshop.Workshop.GetEntry(entry.EntryId) != null, "Craft input failure removed queue entry.");
        RegressionAssert.True(entry.Status == CraftQueueStatus.AwaitingMaterials, "Craft input failure did not mark entry awaiting materials.");
        RegressionAssert.True(entry.BlockingReason?.Contains("core_item_log_oak", StringComparison.Ordinal) == true, "Craft input failure did not report missing input.");
        RegressionAssert.True(entry.ActiveWorkerId == null && !entry.IsScheduled, "Craft input failure did not clear worker/scheduled state.");
        RegressionAssert.True(workshop.Workshop.ActiveJobs == 0, "Craft input failure did not release active workshop slot.");
        RegressionAssert.True(itemsDiffLog.MergeAndSort().Count == 0, "Craft input failure emitted a consumption diff.");

        Console.WriteLine("[PASS] Craft input failure preserves queue entry");
    }

    private static void TestCraftConsumesInputFromWorkshopRing()
    {
        var world = new WorldModel(2, 2);
        DefinitionCatalogTestSupport.LoadItems(world);

        var workshopPos = new Point(2, 2);
        var ringPos = new Point(3, 2);
        SetOpen(world, workshopPos);
        SetOpen(world, ringPos);
        const string recipeId = "test_recipe_ring_input";
        var recipes = CreateRecipeCatalog(recipeId, "Ring Input Test");

        var workshop = CreateWorkshop("aaaaaaaa-3333-3333-3333-aaaaaaaaaaaa", workshopPos);
        PlaceableManager.PlacePlaceable(world, workshop, tick: 0);
        var entry = workshop.Workshop!.AddEntry(recipeId, "Ring Input Test", workshop.Guid, currentTick: 0);
        entry.Status = CraftQueueStatus.InProgress;
        entry.ActiveWorkerId = Guid.Parse("bbbbbbbb-3333-3333-3333-bbbbbbbbbbbb");

        var itemId = world.Items.SpawnItem("core_item_log_oak", ringPos, 0, quantity: 1, currentTick: 0);
        RegressionAssert.True(itemId.HasValue, "Craft ring input setup failed to spawn item.");

        var itemsDiffLog = new ItemsDiffLog();
        var consumer = new CraftMaterialConsumer(
            world,
            new CraftWorkshopLocator(world, ConstructionCatalogStore.Empty),
            recipes,
            new CraftDiffEmitter(itemsDiffLog, priority: 100, systemId: "test"));
        var job = CreateCraftJob(entry, workshop, recipeId, workshopPos);

        bool consumed = consumer.TryConsumeInputs(job);
        var diffs = itemsDiffLog.MergeAndSort();

        RegressionAssert.True(consumed, "Craft ring input was not consumed.");
        RegressionAssert.True(diffs.Count == 1, "Craft ring input emitted incorrect diff count.");
        RegressionAssert.True(diffs[0].Op == ItemsDiffOp.RemoveItem, "Craft ring input emitted incorrect diff operation.");
        RegressionAssert.True(diffs[0].ItemGuid == itemId.GetValueOrDefault(), "Craft ring input emitted removal for wrong item.");
        RegressionAssert.True(diffs[0].LocalIndex == Chunk.LocalIndex(ringPos.X, ringPos.Y), "Craft ring input emitted removal at wrong local cell.");
        RegressionAssert.True(diffs[0].Quantity == 1, "Craft ring input emitted wrong removal quantity.");

        Console.WriteLine("[PASS] Craft consumes input from workshop ring");
    }

    private static WorldModel CreateWorldWithContent()
    {
        var world = new WorldModel(2, 2);
        DefinitionCatalogTestSupport.LoadCreatures(world);
        DefinitionCatalogTestSupport.LoadItems(world);
        return world;
    }

    private static void SetOpen(WorldModel world, Point cell)
    {
        world.SetTile(cell.X, cell.Y, 0, new TileBase(0, (ushort)TerrainKind.OpenWithFloor, 0, 0, 0, 0, 1), 0);
    }

    private static ICraftRecipeCatalog CreateRecipeCatalog(string recipeId, string name)
    {
        return new TestCraftRecipeCatalog(
            new RecipeDefinition
            {
                Id = recipeId,
                Name = name,
                Inputs = new[] { new RecipeIngredient { DefId = "core_item_log_oak", Count = 1 } },
                Outputs = new[] { new RecipeOutput { DefId = "core_item_plank_oak", Count = 1 } },
                DurationTicks = 1,
                PrimarySkill = "craft"
            });
    }

    private static PlaceableInstance CreateWorkshop(string id, Point position)
    {
        return new PlaceableInstance(
            Guid.Parse(id),
            PlaceableKind.Construction,
            "test_workshop",
            position,
            z: 0,
            footprint: new Footprint(1, 1, 1))
        {
            Workshop = new WorkshopState()
        };
    }

    private static ActiveCraftJob CreateCraftJob(CraftQueueEntry entry, PlaceableInstance workshop, string recipeId, Point anchor)
    {
        return new ActiveCraftJob
        {
            WorkerId = entry.ActiveWorkerId!.Value,
            WorkshopGuid = workshop.Guid,
            QueueEntryId = entry.EntryId,
            RecipeId = recipeId,
            Stage = CraftJobStage.ToWorkshop,
            WorkTicksRemaining = 1,
            Anchor = anchor,
            Z = 0
        };
    }

    private sealed class TestCraftRecipeCatalog : ICraftRecipeCatalog
    {
        private readonly RecipeDefinition _recipe;

        public TestCraftRecipeCatalog(RecipeDefinition recipe)
        {
            _recipe = recipe;
        }

        public RecipeDefinition? GetRecipe(string recipeId)
        {
            return string.Equals(_recipe.Id, recipeId, StringComparison.OrdinalIgnoreCase)
                ? _recipe
                : null;
        }
    }

    private sealed class PickupFoundDestinationBlockedPathService : IPathService
    {
        public HumanFortress.Contracts.Navigation.Path Solve(in PathRequest request, in IWorldNavigationView world)
        {
            if (request.Source == request.Destination)
            {
                var steps = new[] { new PathNode(request.Source, 1) };
                return new HumanFortress.Contracts.Navigation.Path(PathResultKind.Found, steps.Length, 0, 0, steps);
            }

            return HumanFortress.Contracts.Navigation.Path.Failed;
        }

        public void BeginTick()
        {
        }

        public void ProcessQueuedRequests(IWorldNavigationView world)
        {
        }

        public void InvalidateChunk(HumanFortress.Contracts.Navigation.ChunkKey chunk)
        {
        }
    }

    private sealed class AllPathsFoundPathService : IPathService
    {
        public HumanFortress.Contracts.Navigation.Path Solve(in PathRequest request, in IWorldNavigationView world)
        {
            var end = request.Source == request.Destination ? request.Source : request.Destination;
            var steps = new[] { new PathNode(end, 1) };
            return new HumanFortress.Contracts.Navigation.Path(PathResultKind.Found, steps.Length, 0, 0, steps);
        }

        public void BeginTick()
        {
        }

        public void ProcessQueuedRequests(IWorldNavigationView world)
        {
        }

        public void InvalidateChunk(HumanFortress.Contracts.Navigation.ChunkKey chunk)
        {
        }
    }
}
