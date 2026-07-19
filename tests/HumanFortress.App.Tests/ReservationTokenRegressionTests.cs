using HumanFortress.Contracts.Navigation;
using HumanFortress.Contracts.Simulation.Save;
using HumanFortress.Jobs.Transport;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Jobs;
using HumanFortress.Simulation.Replay;
using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;
using SimulationChunkKey = HumanFortress.Simulation.World.ChunkKey;

internal static class ReservationTokenRegressionTests
{
    internal static void RunAll()
    {
        TestTokenCompareAndRemoveMatrix();
        TestStaleTransportFinalizerCannotReleaseSuccessorJob();
        TestReservedPartialStackSplitTransfersAtomically();
        TestFailedReservedSplitLeavesSourceAndOwnershipUnchanged();
        TestReservationReplayHashCoversOwnershipAndGeneration();
        TestDeferredReservationRestoreFailsClosed();
    }

    private static void TestTokenCompareAndRemoveMatrix()
    {
        var reservations = new ReservationManager();
        var itemId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var holderId = Guid.Parse("20000000-0000-0000-0000-000000000002");
        var workerId = Guid.Parse("30000000-0000-0000-0000-000000000003");

        bool itemAcquired = reservations.TryAcquireItem(
            itemId, holderId, "Jobs.Transport", "haul:a", 10, 20, out var itemA);
        bool sameHolderNewJobBlocked = !reservations.TryAcquireItem(
            itemId, holderId, "Jobs.Transport", "haul:b", 11, 40, out _);
        bool itemRenewed = reservations.TryRenewItem(itemA, 12, 30);
        bool itemReacquired = reservations.TryAcquireItem(
            itemId, holderId, "Jobs.Transport", "haul:b", 31, 60, out var itemB);
        bool staleItemRenewRejected = !reservations.TryRenewItem(itemA, 31, 70);
        bool staleItemReleaseRejected = !reservations.TryReleaseItem(itemA);
        bool staleItemConsumeRejected = !reservations.TryConsumeItem(itemA);
        bool currentItemConsumed = reservations.TryConsumeItem(itemB);
        bool duplicateItemConsumeRejected = !reservations.TryConsumeItem(itemB);

        bool creatureAcquired = reservations.TryAcquireCreature(
            workerId, "Jobs.Mining", "mine:a", 10, 20, out var creatureA);
        bool sameSystemNewJobBlocked = !reservations.TryAcquireCreature(
            workerId, "Jobs.Mining", "mine:b", 11, 40, out _);
        bool creatureRenewed = reservations.TryRenewCreature(creatureA, 12, 30);
        bool creatureReacquired = reservations.TryAcquireCreature(
            workerId, "Jobs.Mining", "mine:b", 31, 60, out var creatureB);
        bool staleCreatureRenewRejected = !reservations.TryRenewCreature(creatureA, 31, 70);
        bool staleCreatureReleaseRejected = !reservations.TryReleaseCreature(creatureA);
        bool currentCreatureReleased = reservations.TryReleaseCreature(creatureB);
        bool duplicateCreatureReleaseRejected = !reservations.TryReleaseCreature(creatureB);

        RegressionAssert.True(
            itemAcquired
            && sameHolderNewJobBlocked
            && itemRenewed
            && itemReacquired
            && itemB.Generation > itemA.Generation
            && staleItemRenewRejected
            && staleItemReleaseRejected
            && staleItemConsumeRejected
            && currentItemConsumed
            && duplicateItemConsumeRejected
            && creatureAcquired
            && sameSystemNewJobBlocked
            && creatureRenewed
            && creatureReacquired
            && creatureB.Generation > creatureA.Generation
            && staleCreatureRenewRejected
            && staleCreatureReleaseRejected
            && currentCreatureReleased
            && duplicateCreatureReleaseRejected,
            "Reservation token acquire/renew/release/consume CAS matrix failed.");

        Console.WriteLine("[PASS] Reservation token compare-and-remove matrix");
    }

    private static void TestStaleTransportFinalizerCannotReleaseSuccessorJob()
    {
        var reservations = new ReservationManager();
        var itemId = Guid.Parse("40000000-0000-0000-0000-000000000004");
        var workerId = Guid.Parse("50000000-0000-0000-0000-000000000005");
        reservations.TryAcquireItem(
            itemId, workerId, "Jobs.Transport", "haul:old", 0, 10, out var oldItem);
        reservations.TryAcquireCreature(
            workerId, "Jobs.Transport", "haul:old", 0, 10, out var oldCreature);
        bool newItemAcquired = reservations.TryAcquireItem(
            itemId, workerId, "Jobs.Transport", "haul:new", 11, 50, out var newItem);
        bool newCreatureAcquired = reservations.TryAcquireCreature(
            workerId, "Jobs.Transport", "haul:new", 11, 50, out var newCreature);

        var oldJob = new ActiveJob
        {
            CreatureId = workerId,
            ItemId = itemId,
            Dest = new Point3(1, 1, 0),
            Stage = JobStage.ToItem,
            Quantity = 1,
            Reason = TransportReason.Misc,
            ItemReservation = oldItem,
            CreatureReservation = oldCreature
        };
        var finished = new List<ActiveJob>();
        new TransportJobFinalizer(reservations).Finish(oldJob, finished);

        RegressionAssert.True(
            newItemAcquired
            && newCreatureAcquired
            && newItem.Generation > oldItem.Generation
            && newCreature.Generation > oldCreature.Generation
            && reservations.IsItemReserved(itemId, 12)
            && reservations.IsCreatureReserved(workerId, 12, out var system, out var jobId)
            && system == "Jobs.Transport"
            && jobId == "haul:new"
            && finished.Count == 1,
            "A stale transport finalizer released the successor job's reservations.");

        Console.WriteLine("[PASS] Stale transport finalizer cannot release successor job");
    }

    private static void TestReservedPartialStackSplitTransfersAtomically()
    {
        var world = CreateItemWorld();
        var cell = new Point(3, 3);
        SetOpen(world, cell);
        var sourceId = world.Items.SpawnItem("core_item_log_oak", cell, 0, 5, 0)
            ?? throw new InvalidOperationException("Expected reserved split source item.");
        var holderId = Guid.Parse("60000000-0000-0000-0000-000000000006");
        var splitId = Guid.Parse("70000000-0000-0000-0000-000000000007");
        bool sourceAcquired = world.Reservations.TryAcquireItem(
            sourceId, holderId, "Jobs.Transport", "haul:split", 0, 100, out var sourceToken);
        bool transferStaged = world.Reservations.TryStageItemTransfer(
            sourceToken, splitId, 0, 100, out var stagedToken);

        var log = new ItemsDiffLog();
        log.AddSplitStack(
            sourceId,
            splitId,
            new SimulationChunkKey(0, 0, 0),
            Chunk.LocalIndex(cell.X, cell.Y),
            2,
            100,
            "Jobs.Transport",
            sourceToken,
            stagedToken);
        ItemsDiffApplicator.ApplyPreSimulation(world, log.MergeAndSort(), currentTick: 1);

        bool bothClaimsHeldBeforeCommit = world.Reservations.IsItemReserved(sourceId, 1)
            && world.Reservations.IsItemReserved(splitId, 1);
        bool transferCommitted = world.Reservations.TryCommitStagedItemTransfer(
            sourceToken, stagedToken, 1, 100);
        RegressionAssert.True(
            sourceAcquired
            && transferStaged
            && world.Items.GetInstance(sourceId)?.StackCount == 3
            && world.Items.GetInstance(splitId)?.StackCount == 2
            && bothClaimsHeldBeforeCommit
            && transferCommitted
            && !world.Reservations.IsItemReserved(sourceId, 1)
            && world.Reservations.IsItemReserved(splitId, 1)
            && !world.Reservations.TryReleaseItem(sourceToken)
            && world.Reservations.TryReleaseItem(stagedToken),
            "Reserved partial-stack split did not transfer ownership atomically.");

        Console.WriteLine("[PASS] Reserved partial stack split transfers atomically");
    }

    private static void TestFailedReservedSplitLeavesSourceAndOwnershipUnchanged()
    {
        var world = CreateItemWorld();
        var sourceCell = new Point(4, 4);
        var existingCell = new Point(5, 4);
        SetOpen(world, sourceCell);
        SetOpen(world, existingCell);
        var sourceId = world.Items.SpawnItem("core_item_log_oak", sourceCell, 0, 5, 0)
            ?? throw new InvalidOperationException("Expected failed split source item.");
        var existingId = world.Items.SpawnItem("core_item_log_oak", existingCell, 0, 1, 0)
            ?? throw new InvalidOperationException("Expected failed split collision item.");
        var holderId = Guid.Parse("80000000-0000-0000-0000-000000000008");
        world.Reservations.TryAcquireItem(
            sourceId, holderId, "Jobs.Transport", "haul:failed-split", 0, 100, out var sourceToken);
        bool transferStaged = world.Reservations.TryStageItemTransfer(
            sourceToken, existingId, 0, 100, out var stagedToken);
        string beforeHash = WorldReplayHashBuilder.Build(world);

        var log = new ItemsDiffLog();
        log.AddSplitStack(
            sourceId,
            existingId,
            new SimulationChunkKey(0, 0, 0),
            Chunk.LocalIndex(sourceCell.X, sourceCell.Y),
            2,
            100,
            "Jobs.Transport",
            sourceToken,
            stagedToken);
        bool splitRejected = false;
        try
        {
            ItemsDiffApplicator.ApplyPreSimulation(world, log.MergeAndSort(), currentTick: 1);
        }
        catch (InvalidOperationException)
        {
            splitRejected = true;
        }

        RegressionAssert.True(
            splitRejected
            && transferStaged
            && world.Items.GetInstance(sourceId)?.StackCount == 5
            && world.Items.GetInstance(existingId)?.StackCount == 1
            && world.Reservations.IsItemReserved(sourceId, 1)
            && world.Reservations.IsItemReserved(existingId, 1)
            && WorldReplayHashBuilder.Build(world) == beforeHash
            && world.Reservations.TryCancelStagedItemTransfer(stagedToken)
            && world.Reservations.IsItemReserved(sourceId, 1)
            && !world.Reservations.IsItemReserved(existingId, 1),
            "Failed reserved split changed item identity, quantity, or source ownership.");

        Console.WriteLine("[PASS] Failed reserved split leaves source and ownership unchanged");
    }

    private static void TestReservationReplayHashCoversOwnershipAndGeneration()
    {
        var sameA = new World(2, 1);
        var sameB = new World(2, 1);
        var laterGeneration = new World(2, 1);
        var differentJob = new World(2, 1);
        var itemId = Guid.Parse("90000000-0000-0000-0000-000000000009");
        var holderId = Guid.Parse("a0000000-0000-0000-0000-00000000000a");
        var workerId = Guid.Parse("b0000000-0000-0000-0000-00000000000b");

        AcquireReplayReservations(sameA, itemId, holderId, workerId, "job:a");
        AcquireReplayReservations(sameB, itemId, holderId, workerId, "job:a");
        laterGeneration.Reservations.TryAcquireItem(
            Guid.Parse("c0000000-0000-0000-0000-00000000000c"),
            holderId,
            "Jobs.Test",
            "throwaway",
            0,
            0,
            out var throwaway);
        laterGeneration.Reservations.TryReleaseItem(throwaway);
        AcquireReplayReservations(laterGeneration, itemId, holderId, workerId, "job:a");
        AcquireReplayReservations(differentJob, itemId, holderId, workerId, "job:b");

        string hashA = WorldReplayHashBuilder.BuildSectionHashes(sameA).ReservationsHash;
        string hashASecond = WorldReplayHashBuilder.BuildSectionHashes(sameA).ReservationsHash;
        string hashB = WorldReplayHashBuilder.BuildSectionHashes(sameB).ReservationsHash;
        string generationHash = WorldReplayHashBuilder.BuildSectionHashes(laterGeneration).ReservationsHash;
        string ownerHash = WorldReplayHashBuilder.BuildSectionHashes(differentJob).ReservationsHash;
        RegressionAssert.True(
            hashA == hashASecond
            && hashA == hashB
            && hashA != generationHash
            && hashA != ownerHash,
            "Reservation replay hash omitted generation or token ownership, or was not stable.");

        Console.WriteLine("[PASS] Reservation replay hash covers ownership and generation");
    }

    private static void TestDeferredReservationRestoreFailsClosed()
    {
        var reservations = new ReservationManager();
        var itemId = Guid.Parse("d0000000-0000-0000-0000-00000000000d");
        var holderId = Guid.Parse("e0000000-0000-0000-0000-00000000000e");
        reservations.TryAcquireItem(
            itemId, holderId, "Jobs.Test", "live", 0, 100, out var liveToken);
        var before = reservations.GetItemReservationsSnapshot().Single();

        var issues = reservations.RestoreSnapshot(
            new[] { new WorldSaveItemReservationPayloadData(itemId, holderId, 100) },
            Array.Empty<WorldSaveCreatureReservationPayloadData>());
        var after = reservations.GetItemReservationsSnapshot().Single();
        var empty = new ReservationManager();
        var emptyIssues = empty.RestoreSnapshot(
            Array.Empty<WorldSaveItemReservationPayloadData>(),
            Array.Empty<WorldSaveCreatureReservationPayloadData>());
        bool liveReleased = reservations.TryReleaseItem(liveToken);
        var emptyExistingIssues = reservations.RestoreSnapshot(
            Array.Empty<WorldSaveItemReservationPayloadData>(),
            Array.Empty<WorldSaveCreatureReservationPayloadData>());
        bool postRestoreAcquired = reservations.TryAcquireItem(
            Guid.Parse("f0000000-0000-0000-0000-00000000000f"),
            holderId,
            "Jobs.Test",
            "after-empty-restore",
            1,
            100,
            out var postRestoreToken);

        RegressionAssert.True(
            issues.Any(issue => issue.Contains("unsupported", StringComparison.OrdinalIgnoreCase))
            && before.Equals(after)
            && liveReleased
            && emptyExistingIssues.Count == 0
            && postRestoreAcquired
            && postRestoreToken.Generation > liveToken.Generation
            && emptyIssues.Count == 0
            && empty.GetItemReservationsSnapshot().Count == 0
            && empty.GetCreatureReservationsSnapshot().Count == 0,
            "Deferred reservation restore forged token ownership or rejected the empty compatibility scene.");

        Console.WriteLine("[PASS] Deferred reservation restore fails closed");
    }

    private static World CreateItemWorld()
    {
        var world = new World(2, 1);
        DefinitionCatalogTestSupport.LoadItems(world);
        return world;
    }

    private static void SetOpen(World world, Point cell)
    {
        world.SetTile(
            cell.X,
            cell.Y,
            0,
            new TileBase(0, (ushort)TerrainKind.OpenWithFloor, 0, 0, 0, 0, 1),
            0);
    }

    private static void AcquireReplayReservations(
        World world,
        Guid itemId,
        Guid holderId,
        Guid workerId,
        string jobId)
    {
        RegressionAssert.True(
            world.Reservations.TryAcquireItem(
                itemId, holderId, "Jobs.Test", jobId, 0, 100, out _)
            && world.Reservations.TryAcquireCreature(
                workerId, "Jobs.Test", jobId, 0, 100, out _),
            "Replay reservation setup failed.");
    }
}
