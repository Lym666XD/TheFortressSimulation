using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Core.Simulation;
using HumanFortress.Runtime.Diff;
using HumanFortress.Runtime.Host;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Stockpile;
using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

internal static class TickCommitTransactionRegressionTests
{
    internal static void RunAll()
    {
        TestEveryTypedFamilyFailsDuringPreparationBeforeAuthorityWrites();
        TestEveryCommitStageFailureRestoresAuthorityAndRetainsLogs();
        TestFullIntegerPriorityOrderingIsLowerFirst();
    }

    private static void TestEveryTypedFamilyFailsDuringPreparationBeforeAuthorityWrites()
    {
        AssertPreparationFault(logs =>
            logs.Items.Add(
                ItemsDiffOp.AddItem,
                new ChunkKey(0, 0, 0),
                0,
                "item.test",
                quantity: 0,
                priority: 1,
                systemId: "test.items"));

        AssertPreparationFault((_, core) => core.AddOp(new DiffOp(
            DiffOpType.SetFluid,
            DiffTargetEncoding.ForWorldCell(0, 0, 0),
            "test.core",
            priority: 1)));

        AssertPreparationFault(logs =>
            logs.Creatures.AddSpawnCreature(
                "creature.test",
                new Point(-1, 0),
                0,
                "faction.test",
                priority: 1,
                systemId: "test.creatures"));

        AssertPreparationFault(logs =>
            logs.Orders.AddHaul(
                new Rectangle(0, 0, 0, 0),
                0,
                priority: 1,
                createdTick: 1,
                systemId: "test.orders"));

        AssertPreparationFault(logs =>
            logs.Workshops.AddRecipe(
                Guid.Empty,
                "recipe.test",
                currentTick: 1,
                priority: 1,
                systemId: "test.workshops"));

        AssertPreparationFault(logs =>
            logs.Zones.AddCreateZone(
                "zone.test",
                "Test",
                new Rectangle(0, 0, 0, 0),
                0,
                createdTick: 1,
                priority: 1,
                systemId: "test.zones"));

        AssertPreparationFault(logs =>
            logs.Stockpiles.AddPlaceItem(
                itemHandle: 1,
                chunk: new ChunkKey(0, 0, 0),
                cellIndex: Chunk.CELLS_PER_LAYER,
                zoneId: 1,
                quantity: 1,
                priority: 1,
                systemId: "test.stockpiles"));

        AssertPreparationFault(logs =>
            logs.Professions.AddSetWeight(
                Guid.Empty,
                "profession.test",
                weight: 5,
                systemId: "test.professions"));
    }

    private static void TestEveryCommitStageFailureRestoresAuthorityAndRetainsLogs()
    {
        var stages = new[]
        {
            TickMutationCommitStage.ItemsPreSimulation,
            TickMutationCommitStage.CoreSimulation,
            TickMutationCommitStage.Creatures,
            TickMutationCommitStage.ItemsAdditions,
            TickMutationCommitStage.Orders,
            TickMutationCommitStage.Workshops,
            TickMutationCommitStage.Zones,
            TickMutationCommitStage.Stockpiles,
            TickMutationCommitStage.Professions
        };

        foreach (var injectedStage in stages)
        {
            var (world, logs, core) = CreateAuthority();
            var before = world.CaptureMutationMemento();
            core.AddOp(new DiffOp(
                DiffOpType.SetTerrain,
                DiffTargetEncoding.ForWorldCell(1, 1, 0),
                "test.terrain",
                priority: 1,
                args: (ulong)TerrainKind.SolidWall));

            var transaction = CreateTransaction(
                world,
                logs,
                core,
                stage =>
                {
                    if (stage == injectedStage)
                        throw new InvalidOperationException($"injected {stage} failure");
                });

            var fault = CaptureFault(
                () => transaction.Commit(42),
                $"{injectedStage} injection did not surface a structured tick fault.");
            var after = world.CaptureMutationMemento();

            AssertAuthorityEquivalent(before, after, injectedStage);
            RegressionAssert.True(
                fault.Tick == 42
                && fault.Stage == injectedStage
                && fault.RollbackSucceeded
                && core.MergeAndSort().Count == 1,
                $"{injectedStage} failure did not retain logs or report successful rollback.");
        }
    }

    private static void TestFullIntegerPriorityOrderingIsLowerFirst()
    {
        var items = new ItemsDiffLog();
        foreach (var priority in new[] { 1600, 1000, 1400 })
        {
            items.Add(
                ItemsDiffOp.AddItem,
                new ChunkKey(0, 0, 0),
                0,
                $"item.{priority}",
                quantity: 1,
                priority: priority,
                systemId: "test.items.priority");
        }

        var stockpiles = new StockpileDiffLog();
        foreach (var priority in new[] { 1600, 1000, 1400 })
        {
            stockpiles.AddReleaseSlot(
                new ChunkKey(0, 0, 0),
                zoneId: 1,
                priority: priority,
                systemId: "test.stockpile.priority");
        }

        RegressionAssert.True(
            items.MergeAndSort().Select(static diff => diff.Priority).SequenceEqual(new[] { 1000, 1400, 1600 })
            && stockpiles.MergeAndSort().Select(static diff => diff.Priority).SequenceEqual(new[] { 1000, 1400, 1600 }),
            "Typed diff ordering truncated full integer priorities or used higher-first authority.");
    }

    private static void AssertPreparationFault(Action<RuntimeMutationDiffLogs> arrange)
    {
        AssertPreparationFault((logs, _) => arrange(logs));
    }

    private static void AssertPreparationFault(Action<RuntimeMutationDiffLogs, DiffLog> arrange)
    {
        var (world, logs, core) = CreateAuthority();
        var before = world.CaptureMutationMemento();
        arrange(logs, core);
        var fault = CaptureFault(
            () => CreateTransaction(world, logs, core).Commit(7),
            "Invalid typed mutation did not fail closed during preparation.");
        var after = world.CaptureMutationMemento();

        RegressionAssert.True(
            fault.Stage == TickMutationCommitStage.Preparation
            && fault.RollbackSucceeded
            && before.Chunks.Count == after.Chunks.Count
            && before.Items.Instances.Count == after.Items.Instances.Count
            && before.Creatures.Instances.Count == after.Creatures.Instances.Count,
            "Typed mutation preflight changed authority before commit.");
    }

    private static TickMutationCommitTransaction CreateTransaction(
        World world,
        RuntimeMutationDiffLogs logs,
        DiffLog core,
        Action<TickMutationCommitStage>? afterStage = null)
    {
        return new TickMutationCommitTransaction(
            world,
            core,
            logs,
            ConstructionCatalogStore.Empty,
            geology: null,
            afterStage: afterStage);
    }

    private static (World World, RuntimeMutationDiffLogs Logs, DiffLog Core) CreateAuthority()
    {
        var world = new World(World.MinSizeInChunks, maxZ: 1);
        var chunk = world.GetOrCreateChunk(new ChunkKey(0, 0, 0));
        for (var y = 0; y < Chunk.SIZE_XY; y++)
        for (var x = 0; x < Chunk.SIZE_XY; x++)
        {
            chunk.SetTile(
                x,
                y,
                new TileBase(0, (ushort)TerrainKind.OpenWithFloor, 0, 0, 0, 0, 1),
                tick: 0);
        }
        chunk.DrainDirtyTileIndices();
        return (world, new RuntimeMutationDiffLogs(), new DiffLog());
    }

    private static TickMutationCommitFaultException CaptureFault(Action action, string message)
    {
        try
        {
            action();
        }
        catch (TickMutationCommitFaultException fault)
        {
            return fault;
        }

        throw new InvalidOperationException(message);
    }

    private static void AssertAuthorityEquivalent(
        World.MutationMemento before,
        World.MutationMemento after,
        TickMutationCommitStage stage)
    {
        bool chunksEqual = before.Chunks.Count == after.Chunks.Count;
        if (chunksEqual)
        {
            for (var i = 0; i < before.Chunks.Count; i++)
            {
                var left = before.Chunks[i];
                var right = after.Chunks[i];
                chunksEqual &= left.Key.Equals(right.Key)
                    && left.State.Tiles.SequenceEqual(right.State.Tiles)
                    && left.State.LodLevel == right.State.LodLevel
                    && left.State.LastModifiedTick == right.State.LastModifiedTick
                    && left.State.ConnectivityVersion == right.State.ConnectivityVersion
                    && left.State.DirtyTiles.SequenceEqual(right.State.DirtyTiles)
                    && left.State.Workshops.Count == right.State.Workshops.Count
                    && left.State.Zones?.DirtyGeneration == right.State.Zones?.DirtyGeneration
                    && left.State.Stockpiles?.DirtyGeneration == right.State.Stockpiles?.DirtyGeneration;
            }
        }

        RegressionAssert.True(
            chunksEqual
            && before.DirtyChunks.SequenceEqual(after.DirtyChunks)
            && before.Items.Instances.Count == after.Items.Instances.Count
            && before.Items.Identity.NextAllocationSequence == after.Items.Identity.NextAllocationSequence
            && before.Items.Identity.HistoricalBindings.SequenceEqual(after.Items.Identity.HistoricalBindings)
            && before.Items.Identity.RetiredGuids.SequenceEqual(after.Items.Identity.RetiredGuids)
            && before.Creatures.Instances.Count == after.Creatures.Instances.Count
            && before.Creatures.Identity.NextAllocationSequence == after.Creatures.Identity.NextAllocationSequence
            && before.Creatures.Identity.HistoricalBindings.SequenceEqual(after.Creatures.Identity.HistoricalBindings)
            && before.Creatures.Identity.RetiredGuids.SequenceEqual(after.Creatures.Identity.RetiredGuids)
            && before.Orders.NextMiningId == after.Orders.NextMiningId
            && before.Orders.HaulQueue.Count == after.Orders.HaulQueue.Count
            && before.Orders.RecentHauls.Count == after.Orders.RecentHauls.Count
            && before.Orders.ActiveHauls.Count == after.Orders.ActiveHauls.Count
            && before.Orders.RecentMining.Count == after.Orders.RecentMining.Count
            && before.Orders.ActiveMining.Count == after.Orders.ActiveMining.Count
            && before.Orders.MiningAdd.Count == after.Orders.MiningAdd.Count
            && before.Orders.MiningCancel.Count == after.Orders.MiningCancel.Count
            && before.Orders.ConstructionQueue.Count == after.Orders.ConstructionQueue.Count
            && before.Orders.RecentConstruction.Count == after.Orders.RecentConstruction.Count
            && before.Orders.ActiveConstruction.Count == after.Orders.ActiveConstruction.Count
            && before.Orders.BuildableQueue.Count == after.Orders.BuildableQueue.Count
            && before.Orders.RecentBuildable.Count == after.Orders.RecentBuildable.Count
            && before.Orders.ActiveBuildable.Count == after.Orders.ActiveBuildable.Count
            && before.Zones.Zones.Count == after.Zones.Zones.Count
            && before.Zones.NextZoneId == after.Zones.NextZoneId
            && before.Stockpiles.Zones.Count == after.Stockpiles.Zones.Count
            && before.Stockpiles.NextZoneId == after.Stockpiles.NextZoneId,
            $"{stage} failure left authoritative mutation state behind.");
    }
}
