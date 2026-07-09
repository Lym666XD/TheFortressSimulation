using HumanFortress.Core.Simulation;
using HumanFortress.Jobs.Mining;
using HumanFortress.Runtime.Jobs;
using HumanFortress.Simulation.Diff;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Orders;
using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

internal static class MiningItemsDiffRegressionTests
{
    public static void RunAll()
    {
        Console.WriteLine("=== Mining/Items/Diff Regression Tests ===");

        TestMiningChannelTileReservationsReleaseFullFootprint();
        TestItemsDiffConsumption();
        TestItemsDiffSplitStack();
        TestMoveItemDiffRelocation();
        TestCarryDiffMergeRetainsMultipleItems();
        TestEntityScopedDiffMergeUsesWiderEntityKeys();
        TestEntityScopedDiffSortUsesWiderEntityKeys();

        Console.WriteLine("=== Mining/Items/Diff Regression Tests Completed ===\n");
    }

    private static void TestMiningChannelTileReservationsReleaseFullFootprint()
    {
        var tracker = new MiningTileReservationTracker();
        var target = new Point(4, 5);
        var dig = new MiningSystem.PlannedDig(
            target,
            Z: 3,
            GeologyHandle: 1,
            TerrainKind: (byte)TerrainKind.SolidWall,
            Priority: 0,
            Seed: 0UL,
            MiningAction.DigChannel,
            MiningSegment.None,
            DesignationId: 42);

        tracker.Reserve(dig);
        bool targetReserved = tracker.Contains(target, 3);
        bool belowReserved = tracker.Contains(target, 2);

        var job = new ActiveMiningJob
        {
            WorkerId = Guid.Parse("12345678-1234-1234-1234-123456789abc"),
            Target = target,
            Z = 3,
            Adjacent = target,
            Stage = MiningStage.ToAdj,
            TerrainKind = TerrainKind.SolidWall,
            Action = MiningAction.DigChannel,
            Segment = MiningSegment.None,
            DesignationId = 42
        };

        tracker.Release(job);
        RegressionAssert.True(targetReserved, "Mining channel did not reserve target tile.");
        RegressionAssert.True(belowReserved, "Mining channel did not reserve lower footprint tile.");
        RegressionAssert.True(tracker.Count == 0 && !tracker.Contains(target, 3) && !tracker.Contains(target, 2), "Mining channel release leaked target or lower footprint tile.");

        Console.WriteLine("[PASS] Mining channel reservation releases full footprint");
    }

    private static void TestItemsDiffConsumption()
    {
        var world = CreateWorldWithItems();
        var source = new Point(1, 1);
        SetOpen(world, source);

        var itemId = world.Items.SpawnItem("core_item_log_oak", source, 0, 5, 0);
        RegressionAssert.True(itemId.HasValue, "ItemsDiff consumption setup failed to spawn item.");
        var item = itemId.GetValueOrDefault();

        var log = new ItemsDiffLog();
        var chunk = new ChunkKey(0, 0, 0);
        int localIndex = Chunk.LocalIndex(source.X, source.Y);

        log.AddRemoveItem(item, chunk, localIndex, 3, 100, "test");
        ItemsDiffApplicator.ApplyAll(world, log.MergeAndSort(), 1);
        log.Clear();

        RegressionAssert.True(world.Items.GetInstance(item)?.StackCount == 2, "ItemsDiff partial consumption produced wrong stack count.");

        log.AddRemoveItem(item, chunk, localIndex, 2, 100, "test");
        ItemsDiffApplicator.ApplyAll(world, log.MergeAndSort(), 2);
        RegressionAssert.True(world.Items.GetInstance(item) == null, "ItemsDiff full removal did not remove item.");

        var secondItemId = world.Items.SpawnItem("core_item_log_oak", source, 0, 7, 3);
        RegressionAssert.True(secondItemId.HasValue, "ItemsDiff duplicate batch setup failed to spawn item.");
        var secondItem = secondItemId.GetValueOrDefault();

        log.Clear();
        log.AddRemoveItem(secondItem, chunk, localIndex, 3, 100, "test");
        log.AddRemoveItem(secondItem, chunk, localIndex, 3, 100, "test");
        ItemsDiffApplicator.ApplyRemovals(world, log.MergeAndSort());
        RegressionAssert.True(world.Items.GetInstance(secondItem)?.StackCount == 1, "ItemsDiff duplicate batch consumption produced wrong stack count.");

        Console.WriteLine("[PASS] ItemsDiff consumption");
    }

    private static void TestItemsDiffSplitStack()
    {
        var world = CreateWorldWithItems();
        var source = new Point(1, 1);
        SetOpen(world, source);

        var sourceId = world.Items.SpawnItem("core_item_log_oak", source, 0, 9, 0);
        RegressionAssert.True(sourceId.HasValue, "ItemsDiff split setup failed to spawn source item.");
        var sourceItemId = sourceId.GetValueOrDefault();

        var newItemId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var chunk = new ChunkKey(0, 0, 0);
        int localIndex = Chunk.LocalIndex(source.X, source.Y);

        var itemsLog = new ItemsDiffLog();
        itemsLog.AddSplitStack(sourceItemId, newItemId, chunk, localIndex, 4, 100, "test");

        var diffLog = new DiffLog();
        diffLog.AddOp(BuildMarkCarriedDiff(newItemId, source, 0, Guid.Empty));

        ItemsDiffApplicator.ApplyPreSimulation(world, itemsLog.MergeAndSort());
        SimulationDiffApplicator.ApplyAll(world, diffLog.MergeAndSort());

        var sourceItem = world.Items.GetInstance(sourceItemId);
        var splitItem = world.Items.GetInstance(newItemId);
        RegressionAssert.True(sourceItem?.StackCount == 5, "ItemsDiff split did not reduce source stack correctly.");
        RegressionAssert.True(splitItem?.StackCount == 4 && splitItem.CarriedBy != null, "ItemsDiff split item was not created before carry diff.");

        var invalidLog = new ItemsDiffLog();
        var invalidNewItemId = Guid.Parse("99999999-2222-3333-4444-555555555555");
        invalidLog.AddSplitStack(sourceItemId, invalidNewItemId, chunk, localIndex, 5, 100, "test");
        invalidLog.AddSplitStack(sourceItemId, newItemId, chunk, localIndex, 1, 100, "test");
        ItemsDiffApplicator.ApplyPreSimulation(world, invalidLog.MergeAndSort());

        RegressionAssert.True(world.Items.GetInstance(sourceItemId)?.StackCount == 5, "ItemsDiff failed split mutated source stack.");
        RegressionAssert.True(world.Items.GetInstance(invalidNewItemId) == null, "ItemsDiff failed split created invalid item.");

        Console.WriteLine("[PASS] ItemsDiff split stack");
    }

    private static void TestMoveItemDiffRelocation()
    {
        var world = CreateWorldWithItems();
        var sourceA = new Point(1, 1);
        var sourceB = new Point(1, 2);
        var dest = new Point(2, 1);
        SetOpen(world, sourceA);
        SetOpen(world, sourceB);
        SetOpen(world, dest);

        var itemA = world.Items.SpawnItem("core_item_log_oak", sourceA, 0, 2, 0);
        var itemB = world.Items.SpawnItem("core_item_log_oak", sourceB, 0, 3, 0);
        var existing = world.Items.SpawnItem("core_item_log_oak", dest, 0, 5, 0);
        RegressionAssert.True(itemA.HasValue && itemB.HasValue && existing.HasValue, "MoveItem diff setup failed.");

        var log = new DiffLog();
        log.AddOp(BuildMoveItemDiff(itemA.GetValueOrDefault(), dest, 0));
        log.AddOp(BuildMoveItemDiff(itemB.GetValueOrDefault(), dest, 0));
        var merged = log.MergeAndSort();
        SimulationDiffApplicator.ApplyAll(world, merged);

        var destItems = world.Items.GetItemsAt(dest, 0)
            .Where(i => i.DefinitionId == "core_item_log_oak")
            .ToList();

        RegressionAssert.True(merged.Count(op => op.Op == DiffOpType.MoveItem) == 2, "MoveItem diff merge collapsed same-destination item moves.");
        RegressionAssert.True(destItems.Count == 1 && destItems[0].StackCount == 10, "MoveItem diff relocation did not merge destination stacks.");
        RegressionAssert.True(!world.Items.GetItemsAt(sourceA, 0).Any(i => i.DefinitionId == "core_item_log_oak"), "MoveItem diff left item at source A.");
        RegressionAssert.True(!world.Items.GetItemsAt(sourceB, 0).Any(i => i.DefinitionId == "core_item_log_oak"), "MoveItem diff left item at source B.");

        Console.WriteLine("[PASS] MoveItem diff relocation");
    }

    private static void TestCarryDiffMergeRetainsMultipleItems()
    {
        var log = new DiffLog();
        var tile = new Point(4, 4);
        var itemA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        var itemB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
        var carrier = Guid.Parse("cccccccc-0000-0000-0000-000000000003");

        log.AddOp(BuildMarkCarriedDiff(itemA, tile, 0, carrier));
        log.AddOp(BuildMarkCarriedDiff(itemB, tile, 0, carrier));
        log.AddOp(BuildUnmarkCarriedDiff(itemA, tile, 0));
        log.AddOp(BuildUnmarkCarriedDiff(itemB, tile, 0));

        var merged = log.MergeAndSort();
        RegressionAssert.True(merged.Count(op => op.Op == DiffOpType.MarkCarried) == 2, "Carry diff merge collapsed multiple MarkCarried ops.");
        RegressionAssert.True(merged.Count(op => op.Op == DiffOpType.UnmarkCarried) == 2, "Carry diff merge collapsed multiple UnmarkCarried ops.");

        Console.WriteLine("[PASS] Carry diff merge retains multiple items");
    }

    private static void TestEntityScopedDiffMergeUsesWiderEntityKeys()
    {
        var log = new DiffLog();
        var tile = new Point(4, 4);
        var itemA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        var itemB = Guid.Parse("aaaaaaaa-0001-0000-0000-000000000002");

        RegressionAssert.True(
            DiffTargetEncoding.EntityId(itemA) == DiffTargetEncoding.EntityId(itemB)
            && DiffTargetEncoding.EntityKey(itemA) != DiffTargetEncoding.EntityKey(itemB),
            "Entity key collision regression setup failed.");

        log.AddOp(BuildMoveItemDiff(itemA, tile, 0));
        log.AddOp(BuildMoveItemDiff(itemB, tile, 0));

        var merged = log.MergeAndSort();
        RegressionAssert.True(
            merged.Count(op => op.Op == DiffOpType.MoveItem) == 2,
            "Entity-scoped diff merge collapsed distinct items that share the legacy 32-bit entity id.");

        Console.WriteLine("[PASS] Entity-scoped diff merge uses wider entity keys");
    }

    private static void TestEntityScopedDiffSortUsesWiderEntityKeys()
    {
        var log = new DiffLog();
        var tile = new Point(4, 4);
        var lowerLegacyIdHigherEntityKey = Guid.Parse("00000001-ffff-ffff-0000-000000000000");
        var higherLegacyIdLowerEntityKey = Guid.Parse("ffffffff-0000-0000-0000-000000000000");

        RegressionAssert.True(
            DiffTargetEncoding.EntityId(lowerLegacyIdHigherEntityKey) < DiffTargetEncoding.EntityId(higherLegacyIdLowerEntityKey)
            && DiffTargetEncoding.EntityKey(lowerLegacyIdHigherEntityKey) > DiffTargetEncoding.EntityKey(higherLegacyIdLowerEntityKey),
            "Entity-key sort regression setup failed.");

        log.AddOp(BuildMoveItemDiff(lowerLegacyIdHigherEntityKey, tile, 0));
        log.AddOp(BuildMoveItemDiff(higherLegacyIdLowerEntityKey, tile, 0));

        var moveKeys = log.MergeAndSort()
            .Where(op => op.Op == DiffOpType.MoveItem)
            .Select(op => op.Target.EntityKey)
            .ToArray();

        RegressionAssert.True(
            moveKeys.SequenceEqual(moveKeys.OrderBy(static key => key)),
            "Entity-scoped diff sort used legacy 32-bit entity ids before wider entity keys.");

        Console.WriteLine("[PASS] Entity-scoped diff sort uses wider entity keys");
    }

    private static World CreateWorldWithItems()
    {
        var world = new World(2, 2);
        DefinitionCatalogTestSupport.LoadItems(world);
        return world;
    }

    private static void SetOpen(World world, Point cell)
    {
        world.SetTile(cell.X, cell.Y, 0, new TileBase(0, (ushort)TerrainKind.OpenWithFloor, 0, 0, 0, 0, 1), 0);
    }

    private static DiffOp BuildMoveItemDiff(Guid itemId, Point dest, int z)
    {
        var target = DiffTargetEncoding.ForWorldCell(
            dest.X,
            dest.Y,
            z,
            itemId);
        return new DiffOp(DiffOpType.MoveItem, target, "test", 100);
    }

    private static DiffOp BuildMarkCarriedDiff(Guid itemId, Point at, int z, Guid carrierId)
    {
        var target = DiffTargetEncoding.ForWorldCell(
            at.X,
            at.Y,
            z,
            itemId);
        ulong args = DiffTargetEncoding.EntityKey(carrierId);
        return new DiffOp(DiffOpType.MarkCarried, target, "test", 100, args);
    }

    private static DiffOp BuildUnmarkCarriedDiff(Guid itemId, Point at, int z)
    {
        var target = DiffTargetEncoding.ForWorldCell(
            at.X,
            at.Y,
            z,
            itemId);
        return new DiffOp(DiffOpType.UnmarkCarried, target, "test", 100);
    }
}
