using HumanFortress.Contracts.Navigation;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Core.Simulation;
using HumanFortress.Jobs.Construction;
using HumanFortress.Jobs.Configuration;
using HumanFortress.Jobs.Craft;
using HumanFortress.Jobs.Diff;
using HumanFortress.Jobs.Profession;
using HumanFortress.Jobs.Replay;
using HumanFortress.Jobs.Transport;
using HumanFortress.Navigation.Implementation;
using HumanFortress.Runtime.Jobs;
using HumanFortress.Simulation.Diff;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Jobs;
using HumanFortress.Simulation.Orders;
using HumanFortress.Simulation.Placeables;
using HumanFortress.Simulation.Replay;
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
        TestTransportPartialPickupWaitsForCommittedSplit();
        TestTransportAssignmentRetryIgnoresAgeAndPersistsInBacklog();
        TestTransportRetryablePathResultPreservesActiveJob();
        TestTransportThrottleBacklogsRemainingRequests();
        TestTransportReplaySnapshotHashCoversPendingActiveBacklog();
        TestTransportPlannedCommitRollbackRestoresAuthority();
        TestTransportPlannedAssignmentUsesEligiblePathFallback();
        TestTransportPlannedAssignmentPreservesClosestWorkerStrategy();
        TestTransportOptionalStockpileAbsenceUsesCommitCas();
        TestTransportPlannedPendingSplitContinuesWithoutCursor();
        TestTransportPlanCommitDefersStaleNavigationRevision();
        TestTransportFinalizationFaultRollsBackEveryOwner();
        TransportCommitMutationScopeRegressionTests.RunAll();
        TestConstructionTerrainCompletionRemovesSite();
        TestConstructionMaterialSelectionSkipsCentralReservations();
        TestCraftInputFailureKeepsQueueEntry();
        TestCraftConsumesInputFromWorkshopRing();
        TestCraftMaterialSelectionSkipsCentralReservations();
        ReservationTokenRegressionTests.RunAll();
        TransportIntentPipelineRegressionTests.RunAll();

        Console.WriteLine("=== Transport/Construction/Craft Regression Tests Completed ===\n");
    }

    private static void TestTransportPlannedCommitRollbackRestoresAuthority()
    {
        var world = CreateWorldWithContent();
        var source = new Point(1, 1);
        var destination = new Point(2, 1);
        SetOpen(world, source);
        SetOpen(world, destination);

        var workerId = world.Creatures.SpawnCreature(
            "core_race_dwarf",
            source,
            0,
            "player",
            0) ?? throw new InvalidOperationException("Transport rollback worker setup failed.");
        var itemId = world.Items.SpawnItem(
            "core_item_log_oak",
            source,
            0,
            1,
            0) ?? throw new InvalidOperationException("Transport rollback item setup failed.");
        var request = new TransportRequest(
            itemId,
            source,
            0,
            destination,
            0,
            1,
            TransportReason.Misc,
            Priority: 1000,
            RequestorId: "transaction-test",
            CreatedTick: 0,
            Seed: 7);
        var queue = new TransportRequestQueue();
        queue.Enqueue(in request);
        var transport = new TransportJobSystem(
            world,
            queue,
            new DiffLog(),
            itemsDiffLog: new ItemsDiffLog(),
            intakeBudget: 1,
            maxActiveJobs: 1,
            pathService: new AllPathsFoundPathService());

        transport.ReadPlan(0, workerCount: 2);
        transport.SetCommitProbe(stage =>
        {
            if (stage == TransportCommitStage.Assignments)
                throw new InvalidOperationException("injected transport assignment fault");
        });

        bool faulted = false;
        try
        {
            transport.PrepareSequentialCompatibility(0);
            transport.ApplySequentialCompatibility(0);
        }
        catch (InvalidOperationException exception)
            when (exception.Message.Contains("injected transport", StringComparison.Ordinal))
        {
            faulted = true;
        }

        RegressionAssert.True(
            faulted
            && queue.Count == 1
            && queue.GetStateSnapshot().PendingRequests.Single().Equals(request)
            && transport.GetActiveJobsSnapshot().Count == 0
            && transport.GetMovementCursorSnapshot(workerId) == null
            && !world.Reservations.IsItemReserved(itemId, 0)
            && !world.Reservations.IsCreatureReserved(workerId, 0, out _, out _),
            "Transport Plan/Commit failure left queue, reservation, movement, or active-job authority mutated.");

        Console.WriteLine("[PASS] Transport planned commit rollback restores authority");
    }

    private static void TestTransportFinalizationFaultRollsBackEveryOwner()
    {
        var world = CreateWorldWithContent();
        var source = new Point(3, 3);
        var destination = new Point(4, 3);
        SetOpen(world, source);
        SetOpen(world, destination);

        var workerId = world.Creatures.SpawnCreature(
            "core_race_dwarf",
            source,
            0,
            "player",
            0) ?? throw new InvalidOperationException("Transport finalization rollback worker setup failed.");
        var itemId = world.Items.SpawnItem(
            "core_item_log_oak",
            source,
            0,
            1,
            0) ?? throw new InvalidOperationException("Transport finalization rollback item setup failed.");

        var chunkKey = new WorldChunkKey(0, 0, 0);
        var chunk = world.GetChunk(chunkKey)
            ?? throw new InvalidOperationException("Transport finalization rollback chunk setup failed.");
        chunk.EnsureStockpileData();
        var stockpileData = chunk.GetStockpileData()
            ?? throw new InvalidOperationException("Transport finalization rollback stockpile setup failed.");
        int zoneId = world.Stockpiles.CreateZone("Rollback Stockpile", chunkKey, 0);
        stockpileData.CreateOrUpdateShard(zoneId, chunkKey);
        stockpileData.AddCellsToZone(zoneId, new[] { Chunk.LocalIndex(destination.X, destination.Y) });
        world.Stockpiles.GetZone(zoneId)?.UpdateMemberChunks(new[] { chunkKey });

        var request = new TransportRequest(
            itemId,
            source,
            0,
            destination,
            0,
            1,
            TransportReason.ToStockpile,
            Priority: 1000,
            RequestorId: "finalization-transaction-test",
            CreatedTick: 0,
            Seed: 11);
        var queue = new TransportRequestQueue();
        queue.Enqueue(in request);
        var coreDiffs = new DiffLog();
        var itemDiffs = new ItemsDiffLog();
        var stockpileDiffs = new StockpileDiffLog();
        var professions = new ProfessionAssignments(
            new TransportCommitMutationScopeRegressionTests.TestProfessionRegistry());
        var transport = new TransportJobSystem(
            world,
            queue,
            coreDiffs,
            itemsDiffLog: itemDiffs,
            stockpileDiffLog: stockpileDiffs,
            intakeBudget: 1,
            maxActiveJobs: 1,
            professions: professions,
            pathService: new AllPathsFoundPathService(),
            navigationTuning: new NavigationTuning { MovementStepDelayTicks = 0 });

        transport.ReadPlan(0, workerCount: 2);
        transport.ApplySequentialCompatibility(0);

        bool faulted = false;
        for (ulong tick = 1; tick < 10; tick++)
        {
            var activeBeforePlan = transport.GetActiveJobsSnapshot().Single();
            var cursorBeforePlan = transport.GetMovementCursorSnapshot(workerId)
                ?? throw new InvalidOperationException("Transport finalization rollback cursor disappeared.");
            bool willFinalize = activeBeforePlan.Stage == JobStage.ToDest.ToString()
                && cursorBeforePlan.Position == new Point3(destination.X, destination.Y, 0);

            transport.ReadPlan(tick, workerCount: 2);
            if (!willFinalize)
            {
                transport.ApplySequentialCompatibility(tick);
                ApplyTransportDiffs(world, coreDiffs, itemDiffs, stockpileDiffs, tick);
                continue;
            }

            string worldBefore = WorldReplayHashBuilder.Build(world);
            string professionBefore = ProfessionReplayHashBuilder.Build(professions.GetReplaySnapshot());
            var statsBefore = transport.GetLastStatsSnapshot();
            var activeBefore = transport.GetActiveJobsSnapshot().Single();
            var cursorBefore = transport.GetMovementCursorSnapshot(workerId)
                ?? throw new InvalidOperationException("Transport finalization rollback cursor CAS setup failed.");
            transport.SetCommitProbe(stage =>
            {
                if (stage == TransportCommitStage.Finalization)
                    throw new InvalidOperationException("injected transport finalization fault");
            });

            try
            {
                transport.ApplySequentialCompatibility(tick);
            }
            catch (InvalidOperationException exception)
                when (exception.Message.Contains("injected transport finalization", StringComparison.Ordinal))
            {
                faulted = true;
            }

            var cursorAfter = transport.GetMovementCursorSnapshot(workerId);
            RegressionAssert.True(
                faulted
                && WorldReplayHashBuilder.Build(world) == worldBefore
                && ProfessionReplayHashBuilder.Build(professions.GetReplaySnapshot()) == professionBefore
                && transport.GetLastStatsSnapshot().Equals(statsBefore)
                && transport.GetActiveJobsSnapshot().Single().Equals(activeBefore)
                && cursorAfter.HasValue
                && cursorAfter.Value.Revision == cursorBefore.Revision
                && cursorAfter.Value.CurrentStep == cursorBefore.CurrentStep
                && cursorAfter.Value.Position == cursorBefore.Position
                && cursorAfter.Value.Path.Hash == cursorBefore.Path.Hash
                && coreDiffs.MergeAndSort().Count == 0
                && itemDiffs.MergeAndSort().Count == 0
                && stockpileDiffs.MergeAndSort().Count == 0
                && world.Reservations.IsItemReserved(itemId, tick)
                && world.Reservations.IsCreatureReserved(workerId, tick, out _, out _),
                "Transport finalization fault retained diff, profession, stats, active, reservation, or movement authority.");
            break;
        }

        RegressionAssert.True(faulted, "Transport finalization rollback scenario never reached delivery commit.");
        Console.WriteLine("[PASS] Transport finalization rollback restores every owner");
    }

    private static void TestTransportPlannedAssignmentUsesEligiblePathFallback()
    {
        var world = CreateWorldWithContent();
        var workerCells = new[] { new Point(2, 2), new Point(2, 3), new Point(2, 4) };
        var itemCell = new Point(5, 3);
        var destination = new Point(6, 3);
        foreach (var cell in workerCells.Append(itemCell).Append(destination))
            SetOpen(world, cell);

        var workerIds = workerCells
            .Select(cell => world.Creatures.SpawnCreature(
                "core_race_dwarf",
                cell,
                0,
                "player",
                0) ?? throw new InvalidOperationException("Transport fallback worker setup failed."))
            .OrderBy(static id => id)
            .ToArray();
        var workerById = world.Creatures.GetAllInstances()
            .ToDictionary(static worker => worker.Guid);
        var itemId = world.Items.SpawnItem(
            "core_item_log_oak",
            itemCell,
            0,
            1,
            0) ?? throw new InvalidOperationException("Transport fallback item setup failed.");

        var professions = new ProfessionAssignments(
            new TransportCommitMutationScopeRegressionTests.TestProfessionRegistry());
        professions.SetWeight(workerIds[0], "hauler", 0);
        var rejectedWorkerPosition = workerById[workerIds[1]];
        var pathService = new RejectSourcePathService(new Point3(
            rejectedWorkerPosition.Position.X,
            rejectedWorkerPosition.Position.Y,
            rejectedWorkerPosition.Z));
        var queue = new TransportRequestQueue();
        var request = new TransportRequest(
            itemId,
            itemCell,
            0,
            destination,
            0,
            1,
            TransportReason.Misc,
            Priority: 1000,
            RequestorId: "eligible-fallback-test",
            CreatedTick: 0,
            Seed: 13);
        queue.Enqueue(in request);
        var transport = new TransportJobSystem(
            world,
            queue,
            new DiffLog(),
            itemsDiffLog: new ItemsDiffLog(),
            intakeBudget: 1,
            maxActiveJobs: 1,
            professions: professions,
            pathService: pathService);

        transport.ReadPlan(0, workerCount: 2);
        transport.ApplySequentialCompatibility(0);

        var active = transport.GetActiveJobsSnapshot().Single();
        RegressionAssert.True(
            active.CreatureId == workerIds[2]
            && queue.Count == 0
            && transport.GetMovementCursorSnapshot(workerIds[0]) == null
            && transport.GetMovementCursorSnapshot(workerIds[1]) == null
            && transport.GetMovementCursorSnapshot(workerIds[2]).HasValue
            && !world.Reservations.IsCreatureReserved(workerIds[0], 0, out _, out _)
            && !world.Reservations.IsCreatureReserved(workerIds[1], 0, out _, out _)
            && world.Reservations.IsCreatureReserved(workerIds[2], 0, out _, out _),
            "Transport planned assignment ignored profession eligibility or failed to use a conflict-safe path fallback.");

        Console.WriteLine("[PASS] Transport planned assignment uses eligible path fallback");
    }

    private static void TestTransportPlannedAssignmentPreservesClosestWorkerStrategy()
    {
        var world = CreateWorldWithContent();
        var workerCells = new[] { new Point(2, 2), new Point(12, 12) };
        foreach (var cell in workerCells)
            SetOpen(world, cell);

        var workerIds = workerCells
            .Select(cell => world.Creatures.SpawnCreature(
                "core_race_dwarf",
                cell,
                0,
                "player",
                0) ?? throw new InvalidOperationException("Transport closest-worker setup failed."))
            .OrderBy(static id => id)
            .ToArray();
        var expectedWorker = world.Creatures.GetInstance(workerIds[1])
            ?? throw new InvalidOperationException("Transport closest-worker lookup failed.");
        var itemCell = expectedWorker.Position;
        var destination = new Point(itemCell.X + 1, itemCell.Y);
        SetOpen(world, itemCell);
        SetOpen(world, destination);

        var itemId = world.Items.SpawnItem(
            "core_item_log_oak",
            itemCell,
            0,
            1,
            0) ?? throw new InvalidOperationException("Transport closest-worker item setup failed.");
        var queue = new TransportRequestQueue();
        var request = new TransportRequest(
            itemId,
            itemCell,
            0,
            destination,
            0,
            1,
            TransportReason.Misc,
            Priority: 1000,
            RequestorId: "closest-worker-strategy-test",
            CreatedTick: 0,
            Seed: 19);
        queue.Enqueue(in request);
        var transport = new TransportJobSystem(
            world,
            queue,
            new DiffLog(),
            itemsDiffLog: new ItemsDiffLog(),
            intakeBudget: 1,
            maxActiveJobs: 1,
            professions: new ProfessionAssignments(
                new TransportCommitMutationScopeRegressionTests.TestProfessionRegistry()),
            workerStrategy: WorkerSelectionStrategy.Closest,
            pathService: new AllPathsFoundPathService());

        transport.ReadPlan(0, workerCount: 4);
        transport.ApplySequentialCompatibility(0);

        RegressionAssert.True(
            transport.GetActiveJobsSnapshot().Single().CreatureId == workerIds[1]
            && transport.GetMovementCursorSnapshot(workerIds[0]) == null
            && transport.GetMovementCursorSnapshot(workerIds[1]).HasValue,
            "Transport planned assignment replaced Closest worker ranking with canonical GUID order.");

        Console.WriteLine("[PASS] Transport planned assignment preserves Closest worker strategy");
    }

    private static void TestTransportOptionalStockpileAbsenceUsesCommitCas()
    {
        var world = CreateWorldWithContent();
        var source = new Point(3, 3);
        var destination = new Point(4, 3);
        SetOpen(world, source);
        SetOpen(world, destination);
        var workerId = world.Creatures.SpawnCreature(
            "core_race_dwarf",
            source,
            0,
            "player",
            0) ?? throw new InvalidOperationException("Transport absent-stockpile worker setup failed.");
        var itemId = world.Items.SpawnItem(
            "core_item_log_oak",
            source,
            0,
            1,
            0) ?? throw new InvalidOperationException("Transport absent-stockpile item setup failed.");
        var request = new TransportRequest(
            itemId,
            source,
            0,
            destination,
            0,
            1,
            TransportReason.ToArmory,
            Priority: 1000,
            RequestorId: "optional-stockpile-cas-test",
            CreatedTick: 0,
            Seed: 23);
        var queue = new TransportRequestQueue();
        queue.Enqueue(in request);
        var stockpileDiffs = new StockpileDiffLog();
        var transport = new TransportJobSystem(
            world,
            queue,
            new DiffLog(),
            itemsDiffLog: new ItemsDiffLog(),
            stockpileDiffLog: stockpileDiffs,
            intakeBudget: 1,
            maxActiveJobs: 1,
            pathService: new AllPathsFoundPathService());

        transport.ReadPlan(0, workerCount: 2);

        var chunkKey = new WorldChunkKey(0, 0, 0);
        var chunk = world.GetChunk(chunkKey)
            ?? throw new InvalidOperationException("Transport absent-stockpile chunk setup failed.");
        chunk.EnsureStockpileData();
        var stockpileData = chunk.GetStockpileData()
            ?? throw new InvalidOperationException("Transport absent-stockpile data setup failed.");
        int zoneId = world.Stockpiles.CreateZone("Late Armory", chunkKey, 0);
        stockpileData.CreateOrUpdateShard(zoneId, chunkKey);
        stockpileData.AddCellsToZone(zoneId, new[] { Chunk.LocalIndex(destination.X, destination.Y) });
        world.Stockpiles.GetZone(zoneId)?.UpdateMemberChunks(new[] { chunkKey });

        transport.ApplySequentialCompatibility(0);

        RegressionAssert.True(
            queue.Count == 1
            && transport.GetActiveJobsSnapshot().Count == 0
            && transport.GetMovementCursorSnapshot(workerId) == null
            && !world.Reservations.IsItemReserved(itemId, 0)
            && !world.Reservations.IsCreatureReserved(workerId, 0, out _, out _)
            && stockpileDiffs.MergeAndSort().Count == 0,
            "Transport committed an optional stockpile index write after its absent-cell expectation changed.");

        Console.WriteLine("[PASS] Transport optional stockpile absence uses Commit CAS");
    }

    private static void TestTransportPlannedPendingSplitContinuesWithoutCursor()
    {
        var world = CreateWorldWithContent();
        var source = new Point(6, 6);
        var destination = new Point(7, 6);
        SetOpen(world, source);
        SetOpen(world, destination);
        var workerId = world.Creatures.SpawnCreature(
            "core_race_dwarf",
            source,
            0,
            "player",
            0) ?? throw new InvalidOperationException("Planned pending split worker setup failed.");
        var sourceItemId = world.Items.SpawnItem(
            "core_item_log_oak",
            source,
            0,
            5,
            0) ?? throw new InvalidOperationException("Planned pending split item setup failed.");
        var queue = new TransportRequestQueue();
        var request = new TransportRequest(
            sourceItemId,
            source,
            0,
            destination,
            0,
            2,
            TransportReason.Misc,
            Priority: 1000,
            RequestorId: "planned-pending-split-test",
            CreatedTick: 0,
            Seed: 17);
        queue.Enqueue(in request);
        var coreDiffs = new DiffLog();
        var itemDiffs = new ItemsDiffLog();
        var stockpileDiffs = new StockpileDiffLog();
        var transport = new TransportJobSystem(
            world,
            queue,
            coreDiffs,
            itemsDiffLog: itemDiffs,
            stockpileDiffLog: stockpileDiffs,
            intakeBudget: 1,
            maxActiveJobs: 1,
            pathService: new AllPathsFoundPathService(),
            navigationTuning: new NavigationTuning { MovementStepDelayTicks = 0 });

        transport.ReadPlan(0, workerCount: 2);
        transport.ApplySequentialCompatibility(0);
        var assignmentCursor = transport.GetMovementCursorSnapshot(workerId);
        RegressionAssert.True(
            assignmentCursor.HasValue
            && assignmentCursor.Value.Revision == 1
            && assignmentCursor.Value.CurrentStep == 0
            && coreDiffs.MergeAndSort().Count == 0,
            "A newly assigned planned transport job advanced before its next active intent.");

        transport.ReadPlan(1, workerCount: 2);
        transport.ApplySequentialCompatibility(1);
        var split = itemDiffs.MergeAndSort()
            .Single(static diff => diff.Op == ItemsDiffOp.SplitStack);
        RegressionAssert.True(
            transport.GetMovementCursorSnapshot(workerId) == null
            && transport.GetActiveJobsSnapshot().Single().ItemId == sourceItemId,
            "Planned partial pickup did not wait cursor-free for the committed split.");
        ApplyTransportDiffs(world, coreDiffs, itemDiffs, stockpileDiffs, tick: 1);

        transport.ReadPlan(2, workerCount: 2);
        transport.ApplySequentialCompatibility(2);
        var active = transport.GetActiveJobsSnapshot().Single();
        RegressionAssert.True(
            active.ItemId == split.NewItemGuid
            && active.Stage == JobStage.ToDest.ToString()
            && transport.GetMovementCursorSnapshot(workerId).HasValue
            && world.Items.GetInstance(sourceItemId)?.StackCount == 3
            && world.Items.GetInstance(split.NewItemGuid)?.StackCount == 2
            && !world.Reservations.IsItemReserved(sourceItemId, 2)
            && world.Reservations.IsItemReserved(split.NewItemGuid, 2),
            "Pending-split active decision did not transfer identity and rebuild destination movement.");

        Console.WriteLine("[PASS] Transport planned pending split continues without cursor");
    }

    private static void TestTransportPlanCommitDefersStaleNavigationRevision()
    {
        var world = CreateWorldWithContent();
        var source = new Point(8, 8);
        var destination = new Point(9, 8);
        SetOpen(world, source);
        SetOpen(world, destination);
        var workerId = world.Creatures.SpawnCreature(
            "core_race_dwarf",
            source,
            0,
            "player",
            0) ?? throw new InvalidOperationException("Transport stale navigation worker setup failed.");
        var itemId = world.Items.SpawnItem(
            "core_item_log_oak",
            source,
            0,
            1,
            0) ?? throw new InvalidOperationException("Transport stale navigation item setup failed.");
        var queue = new TransportRequestQueue();
        var request = new TransportRequest(
            itemId,
            source,
            0,
            destination,
            0,
            1,
            TransportReason.Misc,
            Priority: 1000,
            RequestorId: "stale-navigation-test",
            CreatedTick: 0,
            Seed: 19);
        queue.Enqueue(in request);
        var coreDiffs = new DiffLog();
        var transport = new TransportJobSystem(
            world,
            queue,
            coreDiffs,
            itemsDiffLog: new ItemsDiffLog(),
            intakeBudget: 1,
            maxActiveJobs: 1,
            pathService: new AllPathsFoundPathService(),
            navigationTuning: new NavigationTuning { MovementStepDelayTicks = 0 });

        transport.ReadPlan(0, workerCount: 2);
        transport.ApplySequentialCompatibility(0);
        transport.ReadPlan(1, workerCount: 2);
        SetOpen(world, new Point(10, 10));
        transport.NavigationManager.RebuildAll();

        var cursorBefore = transport.GetMovementCursorSnapshot(workerId)
            ?? throw new InvalidOperationException("Transport stale navigation cursor setup failed.");
        var activeBefore = transport.GetActiveJobsSnapshot().Single();
        string worldBefore = WorldReplayHashBuilder.Build(world);
        transport.ApplySequentialCompatibility(1);
        var cursorAfter = transport.GetMovementCursorSnapshot(workerId);

        RegressionAssert.True(
            WorldReplayHashBuilder.Build(world) == worldBefore
            && transport.GetActiveJobsSnapshot().Single().Equals(activeBefore)
            && cursorAfter.HasValue
            && cursorAfter.Value.Revision == cursorBefore.Revision
            && cursorAfter.Value.CurrentStep == cursorBefore.CurrentStep
            && cursorAfter.Value.Position == cursorBefore.Position
            && coreDiffs.MergeAndSort().Count == 0,
            "Transport committed a movement proposal after its navigation revision CAS became stale.");

        transport.ReadPlan(2, workerCount: 2);
        transport.ApplySequentialCompatibility(2);
        RegressionAssert.True(
            transport.GetActiveJobsSnapshot().Single().Stage == JobStage.ToDest.ToString()
            && coreDiffs.MergeAndSort().Any(static diff => diff.Op == DiffOpType.MarkCarried),
            "Transport did not replan and continue after deferring a stale navigation commit.");

        Console.WriteLine("[PASS] Transport Plan/Commit defers stale navigation revision");
    }

    private static void ApplyTransportDiffs(
        WorldModel world,
        DiffLog coreDiffs,
        ItemsDiffLog itemDiffs,
        StockpileDiffLog stockpileDiffs,
        ulong tick)
    {
        var items = itemDiffs.MergeAndSort();
        ItemsDiffApplicator.ApplyPreSimulation(world, items, tick);
        SimulationDiffApplicator.ApplyAll(world, coreDiffs.MergeAndSort(), geology: null, tick);
        ItemsDiffApplicator.ApplyAdditions(world, items, tick);
        StockpileDiffApplicator.ApplyAll(world, stockpileDiffs.MergeAndSort());
        coreDiffs.Clear();
        itemDiffs.Clear();
        stockpileDiffs.Clear();
    }

    private static void TestTransportJobFinalizerReleasesReservations()
    {
        var reservations = new ReservationManager();
        var itemId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var workerId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

        bool itemReserved = reservations.TryAcquireItem(
            itemId,
            workerId,
            "Jobs.Transport",
            "haul:test",
            currentTick: 10,
            expireTick: 100,
            out var itemToken);
        bool creatureReserved = reservations.TryAcquireCreature(
            workerId,
            "Jobs.Transport",
            "haul:test",
            currentTick: 10,
            expireTick: 100,
            out var creatureToken);

        var job = new ActiveJob
        {
            CreatureId = workerId,
            ItemId = itemId,
            Dest = new Point3(1, 1, 0),
            Stage = JobStage.ToItem,
            Quantity = 1,
            Reason = TransportReason.ToStockpile,
            ItemReservation = itemToken,
            CreatureReservation = creatureToken
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

        queue.Enqueue(new TransportRequest(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0003"),
            source,
            FromZ: 0,
            new Point(65, 1),
            ToZ: 0,
            Quantity: 1,
            TransportReason.Misc,
            Priority: 0,
            RequestorId: "test",
            CreatedTick: 4,
            Seed: 0));
        queue.Enqueue(new TransportRequest(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0001"),
            source,
            FromZ: 0,
            new Point(1, 1),
            ToZ: 0,
            Quantity: 1,
            TransportReason.Misc,
            Priority: 0,
            RequestorId: "test",
            CreatedTick: 5,
            Seed: 0));
        queue.Enqueue(new TransportRequest(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0002"),
            source,
            FromZ: 0,
            new Point(33, 1),
            ToZ: 0,
            Quantity: 1,
            TransportReason.Misc,
            Priority: 0,
            RequestorId: "test",
            CreatedTick: 6,
            Seed: 0));
        int[] activeShardIds = queue.GetActiveShardIds();
        int[] shardCountIds = queue.GetShardCountsSnapshot().Keys.ToArray();
        RegressionAssert.True(
            activeShardIds.SequenceEqual(activeShardIds.OrderBy(static shardId => shardId))
            && shardCountIds.SequenceEqual(shardCountIds.OrderBy(static shardId => shardId)),
            "Transport queue shard snapshots did not return stable shard-id order.");

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

        transport.PrepareSequentialCompatibility(0);
        transport.ApplySequentialCompatibility(0);

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

        transport.PrepareSequentialCompatibility(0);
        for (ulong tick = 0; tick <= 4; tick++)
            transport.ApplySequentialCompatibility(tick);

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
            DiffTargetEncoding.EntityKey(item),
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

        transport.PrepareSequentialCompatibility(0);
        for (ulong tick = 0; tick <= 4; tick++)
            transport.ApplySequentialCompatibility(tick);

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

        transport.PrepareSequentialCompatibility(0);
        world.Items.UpdateItemPosition(item, movedPickup, 0);

        transport.ApplySequentialCompatibility(0);
        var firstPass = diffLog.MergeAndSort();
        bool noEarlyCarry = firstPass.All(op => op.Op != DiffOpType.MarkCarried)
            && transport.GetActiveJobsSnapshot().Count == 1
            && transport.GetActiveJobsSnapshot()[0].Stage == JobStage.ToItem.ToString();
        RegressionAssert.True(noEarlyCarry, "Moved pickup target was carried before replanning.");

        for (ulong tick = 1; tick <= 4; tick++)
            transport.ApplySequentialCompatibility(tick);

        var afterRepath = diffLog.MergeAndSort();
        var active = transport.GetActiveJobsSnapshot();
        RegressionAssert.True(afterRepath.Count(op => op.Op == DiffOpType.MarkCarried) == 1, "Moved pickup target was not carried after replanning.");
        RegressionAssert.True(active.Count == 1 && active[0].Stage == JobStage.ToDest.ToString(), "Moved pickup target did not advance to destination stage.");
        RegressionAssert.True(world.Reservations.IsItemReserved(item, currentTick: 5), "Moved pickup target lost item reservation.");
        RegressionAssert.True(world.Reservations.IsCreatureReserved(worker, currentTick: 5, out _, out _), "Moved pickup target lost creature reservation.");

        Console.WriteLine("[PASS] Transport moved pickup target replans");
    }

    private static void TestTransportPartialPickupWaitsForCommittedSplit()
    {
        var world = new WorldModel(2, 1);
        DefinitionCatalogTestSupport.LoadCreatures(world);
        DefinitionCatalogTestSupport.LoadItems(world);
        var source = new Point(6, 6);
        var destination = new Point(7, 6);
        SetOpen(world, source);
        SetOpen(world, destination);
        var workerId = world.Creatures.SpawnCreature("core_race_dwarf", source, 0, "player", 0)
            ?? throw new InvalidOperationException("Expected partial-pickup worker.");
        var sourceItemId = world.Items.SpawnItem("core_item_log_oak", source, 0, 5, 0)
            ?? throw new InvalidOperationException("Expected partial-pickup source stack.");
        var requests = new TransportRequestQueue();
        requests.Enqueue(new TransportRequest(
            sourceItemId,
            source,
            0,
            destination,
            0,
            Quantity: 2,
            TransportReason.Misc,
            Priority: 0,
            RequestorId: "partial-split-test",
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
        transport.PrepareSequentialCompatibility(0);

        ItemsDiff splitDiff = default;
        ulong splitTick = 0;
        for (ulong tick = 0; tick < 4; tick++)
        {
            transport.ApplySequentialCompatibility(tick);
            var splits = itemsDiffLog.MergeAndSort()
                .Where(diff => diff.Op == ItemsDiffOp.SplitStack)
                .ToArray();
            if (splits.Length == 0)
                continue;

            splitDiff = splits.Single();
            splitTick = tick;
            break;
        }

        var stagedView = transport.GetActiveJobsSnapshot().Single();
        RegressionAssert.True(
            splitDiff.NewItemGuid != Guid.Empty
            && stagedView.ItemId == sourceItemId
            && stagedView.Stage == JobStage.ToItem.ToString()
            && world.Items.GetInstance(splitDiff.NewItemGuid) == null
            && world.Reservations.IsItemReserved(sourceItemId, splitTick)
            && world.Reservations.IsItemReserved(splitDiff.NewItemGuid, splitTick),
            "Transport changed job item identity before the split committed.");

        ItemsDiffApplicator.ApplyPreSimulation(world, new[] { splitDiff }, splitTick);
        transport.ApplySequentialCompatibility(splitTick + 1);
        var committedView = transport.GetActiveJobsSnapshot().Single();
        RegressionAssert.True(
            committedView.ItemId == splitDiff.NewItemGuid
            && committedView.Stage == JobStage.ToDest.ToString()
            && world.Items.GetInstance(sourceItemId)?.StackCount == 3
            && world.Items.GetInstance(splitDiff.NewItemGuid)?.StackCount == 2
            && !world.Reservations.IsItemReserved(sourceItemId, splitTick + 1)
            && world.Reservations.IsItemReserved(splitDiff.NewItemGuid, splitTick + 1)
            && world.Reservations.IsCreatureReserved(workerId, splitTick + 1, out _, out _),
            "Transport did not switch identity only after split and reservation-transfer commit.");

        Console.WriteLine("[PASS] Transport partial pickup waits for committed split");
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

        transport.PrepareSequentialCompatibility(0);
        RegressionAssert.True(transport.GetActiveJobsSnapshot().Count == 1, "Transport throttle did not assign first job.");
        RegressionAssert.True(transport.GetBacklogCount() == 1, "Transport throttle did not backlog remaining request.");
        RegressionAssert.True(requests.Count == 0, "Transport throttle did not drain queue into active/backlog ownership.");

        for (ulong tick = 0; tick <= 5; tick++)
            transport.ApplySequentialCompatibility(tick);
        RegressionAssert.True(transport.GetActiveJobsSnapshot().Count == 0, "Transport throttle first job did not complete.");

        transport.PrepareSequentialCompatibility(6);
        RegressionAssert.True(transport.GetActiveJobsSnapshot().Count == 1, "Transport throttle did not recover backlogged request.");
        RegressionAssert.True(transport.GetBacklogCount() == 0, "Transport throttle backlog was not consumed.");

        Console.WriteLine("[PASS] Transport throttle preserves backlog");
    }

    private static void TestTransportRetryablePathResultPreservesActiveJob()
    {
        var world = CreateWorldWithContent();
        var source = new Point(6, 6);
        var destination = new Point(9, 6);
        SetOpen(world, source);
        SetOpen(world, destination);

        var workerId = world.Creatures.SpawnCreature("core_race_dwarf", source, 0, "player", 0);
        var itemId = world.Items.SpawnItem("core_item_log_oak", source, 0, 1, 0);
        RegressionAssert.True(workerId.HasValue && itemId.HasValue, "Retryable path setup failed.");

        var requests = new TransportRequestQueue();
        requests.Enqueue(new TransportRequest(
            itemId.GetValueOrDefault(),
            source,
            0,
            destination,
            0,
            1,
            TransportReason.Misc,
            0,
            "test",
            0,
            0));
        var diffs = new DiffLog();
        var pathService = new PartialThenFoundPathService();
        var transport = new TransportJobSystem(
            world,
            requests,
            diffs,
            itemsDiffLog: new ItemsDiffLog(),
            intakeBudget: 1,
            maxActiveJobs: 1,
            pathService: pathService);

        transport.PrepareSequentialCompatibility(0);
        transport.ApplySequentialCompatibility(0);
        var afterPartial = diffs.MergeAndSort();
        var activeAfterPartial = transport.GetActiveJobsSnapshot();
        var statsAfterPartial = transport.GetLastStatsSnapshot();

        RegressionAssert.True(
            afterPartial.Count(op => op.Op == DiffOpType.MarkCarried) == 1
            && afterPartial.All(op => op.Op != DiffOpType.UnmarkCarried)
            && activeAfterPartial.Count == 1
            && activeAfterPartial[0].Stage == JobStage.ToDest.ToString()
            && statsAfterPartial.NoPathDelta == 0
            && world.Reservations.IsItemReserved(itemId.GetValueOrDefault(), 1)
            && world.Reservations.IsCreatureReserved(workerId.GetValueOrDefault(), 1, out _, out _),
            "Retryable destination path was treated as a permanent transport failure.");

        transport.ApplySequentialCompatibility(1);
        RegressionAssert.True(
            pathService.SearchAttempts.Contains(1),
            "Transport replan did not advance the explicit deterministic path search attempt.");

        Console.WriteLine("[PASS] Transport retryable path result preserves active job");
    }

    private static void TestTransportAssignmentRetryIgnoresAgeAndPersistsInBacklog()
    {
        var world = CreateWorldWithContent();
        var source = new Point(3, 3);
        var destination = new Point(7, 3);
        SetOpen(world, source);
        SetOpen(world, destination);

        var workerId = world.Creatures.SpawnCreature("core_race_dwarf", source, 0, "player", 0);
        var itemId = world.Items.SpawnItem("core_item_log_oak", source, 0, 1, 0);
        RegressionAssert.True(workerId.HasValue && itemId.HasValue, "Transport assignment retry setup failed.");

        var requests = new TransportRequestQueue();
        requests.Enqueue(new TransportRequest(
            itemId.GetValueOrDefault(),
            source,
            0,
            destination,
            0,
            1,
            TransportReason.Misc,
            0,
            "aged-request",
            CreatedTick: 0,
            Seed: 0));
        var pathService = new AssignmentPartialThenFoundPathService();
        var transport = new TransportJobSystem(
            world,
            requests,
            new DiffLog(),
            itemsDiffLog: new ItemsDiffLog(),
            intakeBudget: 1,
            maxActiveJobs: 1,
            pathService: pathService);

        transport.PrepareSequentialCompatibility(1000);
        var retrySnapshot = transport.GetReplaySnapshot();
        RegressionAssert.True(
            pathService.SearchAttempts.SequenceEqual(new byte[] { 0 })
            && retrySnapshot.ActiveJobs.Count == 0
            && retrySnapshot.BacklogEntries.Count == 1
            && retrySnapshot.BacklogEntries[0].Request.PathSearchAttempt == 1,
            "An aged transport request skipped attempt zero or lost its Partial retry tier in backlog.");

        transport.PrepareSequentialCompatibility(1001);
        RegressionAssert.True(
            pathService.SearchAttempts.SequenceEqual(new byte[] { 0, 1 })
            && transport.GetActiveJobsSnapshot().Count == 1
            && transport.GetBacklogCount() == 0,
            "Transport backlog retry did not use the tier produced by the previous Partial result.");

        Console.WriteLine("[PASS] Transport assignment retry ignores age and persists in backlog");
    }

    private static void TestTransportReplaySnapshotHashCoversPendingActiveBacklog()
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
        RegressionAssert.True(workerId.HasValue && itemA.HasValue && itemB.HasValue, "Transport replay snapshot setup failed.");
        var itemAId = itemA.GetValueOrDefault();
        var itemBId = itemB.GetValueOrDefault();

        var requests = new TransportRequestQueue();
        requests.Enqueue(new TransportRequest(itemAId, sourceA, 0, destA, 0, 1, TransportReason.Misc, 0, "test-a", 0, 101));
        requests.Enqueue(new TransportRequest(itemBId, sourceB, 0, destB, 0, 1, TransportReason.ToWorkshopInput, 1, "test-b", 0, 202));

        var transport = new TransportJobSystem(
            world,
            requests,
            new DiffLog(),
            itemsDiffLog: new ItemsDiffLog(),
            intakeBudget: 2,
            maxActiveJobs: 1,
            pathService: new AllPathsFoundPathService());

        var initialHash = TransportReplayHashBuilder.Build(requests.GetStateSnapshot(), transport.GetReplaySnapshot());
        var initialHashSecondRead = TransportReplayHashBuilder.Build(requests.GetStateSnapshot(), transport.GetReplaySnapshot());

        transport.ApplySchedulingHints(intakeCap: 2, maxActiveCap: 1, reserveSlots: 0);
        transport.PrepareSequentialCompatibility(0);
        var activeBacklogSnapshot = transport.GetReplaySnapshot();
        var activeBacklogHash = TransportReplayHashBuilder.Build(requests.GetStateSnapshot(), activeBacklogSnapshot);
        var activeBacklogHashSecondRead = TransportReplayHashBuilder.Build(requests.GetStateSnapshot(), transport.GetReplaySnapshot());

        transport.ApplySchedulingHints(intakeCap: 1, maxActiveCap: 1, reserveSlots: 1);
        var hintChangedHash = TransportReplayHashBuilder.Build(requests.GetStateSnapshot(), transport.GetReplaySnapshot());

        RegressionAssert.True(
            initialHash == initialHashSecondRead
            && activeBacklogHash == activeBacklogHashSecondRead
            && initialHash != activeBacklogHash
            && activeBacklogHash != hintChangedHash
            && requests.GetStateSnapshot().PendingRequests.Count == 0
            && activeBacklogSnapshot.ActiveJobs.Count == 1
            && activeBacklogSnapshot.BacklogEntries.Count == 1,
            "Transport replay snapshot hash did not cover pending, active, backlog, or scheduling hint state.");

        Console.WriteLine("[PASS] Transport replay snapshot hash");
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

        construction.ApplySequentialCompatibility(0);

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

    private static void TestConstructionMaterialSelectionSkipsCentralReservations()
    {
        var world = new WorldModel(2, 2);
        DefinitionCatalogTestSupport.LoadItems(world);

        var target = new Point(6, 6);
        var availableCell = new Point(7, 6);
        SetOpen(world, target);
        SetOpen(world, availableCell);

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

        var reservedId = world.Items.SpawnItem(
            "core_item_block_granite",
            target,
            0,
            quantity: 1,
            currentTick: 0);
        var availableId = world.Items.SpawnItem(
            "core_item_block_granite",
            availableCell,
            0,
            quantity: 1,
            currentTick: 0);
        RegressionAssert.True(
            reservedId.HasValue && availableId.HasValue,
            "Construction reservation-selection setup failed to spawn materials.");

        var holder = Guid.Parse("cccccccc-dddd-eeee-ffff-000000000001");
        RegressionAssert.True(
            world.Reservations.TryAcquireItem(
                reservedId.GetValueOrDefault(),
                holder,
                "test.construction",
                "construction-material",
                currentTick: 10,
                expireTick: 20,
                out _),
            "Construction reservation-selection setup could not reserve material.");

        var itemsDiffLog = new ItemsDiffLog();
        var tracker = new ConstructionMaterialTracker(
            world,
            new ConstructionFootprintCells(world),
            world.Items,
            new ConstructionDiffEmitter(
                diff: null,
                itemsDiff: itemsDiffLog,
                systemId: "test",
                priority: 100),
            logger: null);
        var delivered = tracker.CountDelivered(site, currentTick: 10);
        bool consumed = tracker.TryConsume(
            site,
            new Dictionary<string, int>(required, StringComparer.OrdinalIgnoreCase),
            currentTick: 10);
        var diffs = itemsDiffLog.MergeAndSort();

        RegressionAssert.True(
            delivered.GetValueOrDefault("stone_block") == 1
            && consumed
            && diffs.Count == 1
            && diffs[0].ItemGuid == availableId.GetValueOrDefault(),
            "Construction material selection counted or consumed a centrally reserved item.");

        ItemsDiffApplicator.ApplyPreSimulation(world, diffs, currentTick: 10);
        RegressionAssert.True(
            world.Items.GetInstance(reservedId.GetValueOrDefault())?.StackCount == 1
            && world.Items.GetInstance(availableId.GetValueOrDefault()) == null
            && world.Reservations.IsItemReserved(reservedId.GetValueOrDefault(), currentTick: 10),
            "Construction consumption damaged the reserved item or its reservation.");

        Console.WriteLine("[PASS] Construction material selection skips central reservations");
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

        var entry = workshop.Workshop!.AddEntry(recipeId, workshop.Guid, currentTick: 0);
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
        bool consumed = consumer.TryConsumeInputs(job, currentTick: 0);
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
        var entry = workshop.Workshop!.AddEntry(recipeId, workshop.Guid, currentTick: 0);
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

        bool consumed = consumer.TryConsumeInputs(job, currentTick: 0);
        var diffs = itemsDiffLog.MergeAndSort();

        RegressionAssert.True(consumed, "Craft ring input was not consumed.");
        RegressionAssert.True(diffs.Count == 1, "Craft ring input emitted incorrect diff count.");
        RegressionAssert.True(diffs[0].Op == ItemsDiffOp.RemoveItem, "Craft ring input emitted incorrect diff operation.");
        RegressionAssert.True(diffs[0].ItemGuid == itemId.GetValueOrDefault(), "Craft ring input emitted removal for wrong item.");
        RegressionAssert.True(diffs[0].LocalIndex == Chunk.LocalIndex(ringPos.X, ringPos.Y), "Craft ring input emitted removal at wrong local cell.");
        RegressionAssert.True(diffs[0].Quantity == 1, "Craft ring input emitted wrong removal quantity.");

        Console.WriteLine("[PASS] Craft consumes input from workshop ring");
    }

    private static void TestCraftMaterialSelectionSkipsCentralReservations()
    {
        var world = new WorldModel(2, 2);
        DefinitionCatalogTestSupport.LoadItems(world);

        var workshopPos = new Point(10, 10);
        var reservedCell = new Point(11, 10);
        var availableCell = new Point(10, 11);
        SetOpen(world, workshopPos);
        SetOpen(world, reservedCell);
        SetOpen(world, availableCell);
        const string recipeId = "test_recipe_reserved_input";
        var recipes = CreateRecipeCatalog(recipeId, "Reserved Input Test");

        var workshop = CreateWorkshop("aaaaaaaa-4444-4444-4444-aaaaaaaaaaaa", workshopPos);
        PlaceableManager.PlacePlaceable(world, workshop, tick: 0);
        var entry = workshop.Workshop!.AddEntry(recipeId, workshop.Guid, currentTick: 0);
        entry.Status = CraftQueueStatus.InProgress;
        entry.ActiveWorkerId = Guid.Parse("bbbbbbbb-4444-4444-4444-bbbbbbbbbbbb");

        var reservedId = world.Items.SpawnItem("core_item_log_oak", reservedCell, 0, 1, 0);
        var availableId = world.Items.SpawnItem("core_item_log_oak", availableCell, 0, 1, 0);
        RegressionAssert.True(
            reservedId.HasValue && availableId.HasValue,
            "Craft reservation-selection setup failed to spawn inputs.");
        var holder = Guid.Parse("dddddddd-eeee-ffff-0000-000000000001");
        RegressionAssert.True(
            world.Reservations.TryAcquireItem(
                reservedId.GetValueOrDefault(),
                holder,
                "test.craft",
                "craft-material",
                currentTick: 10,
                expireTick: 20,
                out _),
            "Craft reservation-selection setup could not reserve input.");

        var itemsDiffLog = new ItemsDiffLog();
        var consumer = new CraftMaterialConsumer(
            world,
            new CraftWorkshopLocator(world, ConstructionCatalogStore.Empty),
            recipes,
            new CraftDiffEmitter(itemsDiffLog, priority: 100, systemId: "test"));
        var job = CreateCraftJob(entry, workshop, recipeId, workshopPos);

        bool consumed = consumer.TryConsumeInputs(job, currentTick: 10);
        var diffs = itemsDiffLog.MergeAndSort();
        RegressionAssert.True(
            consumed
            && diffs.Count == 1
            && diffs[0].ItemGuid == availableId.GetValueOrDefault(),
            "Craft material selection consumed a centrally reserved input.");

        ItemsDiffApplicator.ApplyPreSimulation(world, diffs, currentTick: 10);
        RegressionAssert.True(
            world.Items.GetInstance(reservedId.GetValueOrDefault())?.StackCount == 1
            && world.Items.GetInstance(availableId.GetValueOrDefault()) == null
            && world.Reservations.IsItemReserved(reservedId.GetValueOrDefault(), currentTick: 10),
            "Craft consumption damaged the reserved input or its reservation.");

        Console.WriteLine("[PASS] Craft material selection skips central reservations");
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

        public void InvalidateChunk(HumanFortress.Contracts.Navigation.ChunkKey chunk)
        {
        }
    }

    private sealed class RejectSourcePathService : IPathService
    {
        private readonly Point3 _rejectedSource;

        internal RejectSourcePathService(Point3 rejectedSource)
        {
            _rejectedSource = rejectedSource;
        }

        public HumanFortress.Contracts.Navigation.Path Solve(
            in PathRequest request,
            in IWorldNavigationView world)
        {
            if (request.Source == _rejectedSource)
                return HumanFortress.Contracts.Navigation.Path.Failed;

            var steps = new[] { new PathNode(request.Destination, 1) };
            return new HumanFortress.Contracts.Navigation.Path(
                PathResultKind.Found,
                steps.Length,
                0,
                0,
                steps);
        }

        public void BeginTick()
        {
        }

        public void InvalidateChunk(HumanFortress.Contracts.Navigation.ChunkKey chunk)
        {
        }
    }

    private sealed class PartialThenFoundPathService : IPathService
    {
        private int _solveCount;

        internal List<byte> SearchAttempts { get; } = new();

        public HumanFortress.Contracts.Navigation.Path Solve(in PathRequest request, in IWorldNavigationView world)
        {
            _solveCount++;
            SearchAttempts.Add(request.SearchAttempt);
            if (_solveCount == 2)
            {
                var partialSteps = new[] { new PathNode(request.Source, 1) };
                return new HumanFortress.Contracts.Navigation.Path(
                    PathResultKind.Partial,
                    partialSteps.Length,
                    0,
                    0,
                    partialSteps);
            }

            var steps = new[] { new PathNode(request.Destination, 1) };
            return new HumanFortress.Contracts.Navigation.Path(
                PathResultKind.Found,
                steps.Length,
                0,
                0,
                steps);
        }

        public void BeginTick()
        {
        }

        public void InvalidateChunk(HumanFortress.Contracts.Navigation.ChunkKey chunk)
        {
        }
    }

    private sealed class AssignmentPartialThenFoundPathService : IPathService
    {
        internal List<byte> SearchAttempts { get; } = new();

        public HumanFortress.Contracts.Navigation.Path Solve(
            in PathRequest request,
            in IWorldNavigationView world)
        {
            SearchAttempts.Add(request.SearchAttempt);
            if (SearchAttempts.Count == 1)
            {
                var partialSteps = new[] { new PathNode(request.Source, 1) };
                return new HumanFortress.Contracts.Navigation.Path(
                    PathResultKind.Partial,
                    partialSteps.Length,
                    0,
                    0,
                    partialSteps);
            }

            var steps = new[] { new PathNode(request.Destination, 1) };
            return new HumanFortress.Contracts.Navigation.Path(
                PathResultKind.Found,
                steps.Length,
                0,
                0,
                steps);
        }

        public void BeginTick()
        {
        }

        public void InvalidateChunk(HumanFortress.Contracts.Navigation.ChunkKey chunk)
        {
        }
    }
}
