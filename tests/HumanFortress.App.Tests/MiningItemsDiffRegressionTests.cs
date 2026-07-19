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
        TestStackMergePreservesUnrelatedCellIndexEntries();
        TestNonStackableItemsNeverMerge();
        TestStackMergeRespectsDefinitionCapacity();
        TestStackCompatibilityMetadataPreventsMerge();
        TestCentralReservationRejectsRemoveAndSplit();
        TestExternallyOwnedStackIdentityPreventsMerge();
        TestStackMergeReportsIdentityTransfers();
        TestSplitStackPreservesMetadataAndIndexes();
        TestCarryDiffMergeRetainsMultipleItems();
        TestEntityScopedDiffMergeUsesWiderEntityKeys();
        TestEntityScopedDiffSortUsesWiderEntityKeys();
        LiveEntityIdentityRegressionTests.RunAll();

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
        DefinitionCatalogTestSupport.LoadCreatures(world);
        var carrierId = world.Creatures.SpawnCreature(
            "core_race_dwarf",
            source,
            z: 0,
            currentTick: 0);
        RegressionAssert.True(carrierId.HasValue, "ItemsDiff split setup failed to spawn a live carrier.");

        var sourceId = world.Items.SpawnItem("core_item_log_oak", source, 0, 9, 0);
        RegressionAssert.True(sourceId.HasValue, "ItemsDiff split setup failed to spawn source item.");
        var sourceItemId = sourceId.GetValueOrDefault();

        var newItemId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var chunk = new ChunkKey(0, 0, 0);
        int localIndex = Chunk.LocalIndex(source.X, source.Y);

        var itemsLog = new ItemsDiffLog();
        itemsLog.AddSplitStack(sourceItemId, newItemId, chunk, localIndex, 4, 100, "test");

        var diffLog = new DiffLog();
        diffLog.AddOp(BuildMarkCarriedDiff(newItemId, source, 0, carrierId.GetValueOrDefault()));

        ItemsDiffApplicator.ApplyPreSimulation(world, itemsLog.MergeAndSort());
        SimulationDiffApplicator.ApplyAll(world, diffLog.MergeAndSort());

        var sourceItem = world.Items.GetInstance(sourceItemId);
        var splitItem = world.Items.GetInstance(newItemId);
        RegressionAssert.True(sourceItem?.StackCount == 5, "ItemsDiff split did not reduce source stack correctly.");
        RegressionAssert.True(splitItem?.StackCount == 4 && splitItem.CarriedBy != null, "ItemsDiff split item was not created before carry diff.");

        var invalidLog = new ItemsDiffLog();
        var invalidNewItemId = Guid.Parse("99999999-2222-3333-4444-555555555555");
        invalidLog.AddSplitStack(sourceItemId, newItemId, chunk, localIndex, 1, 100, "test");
        var duplicateIdentityRejected = false;
        try
        {
            ItemsDiffApplicator.ApplyPreSimulation(world, invalidLog.MergeAndSort());
        }
        catch (InvalidOperationException)
        {
            duplicateIdentityRejected = true;
        }

        RegressionAssert.True(
            duplicateIdentityRejected
            && world.Items.GetInstance(sourceItemId)?.StackCount == 5
            && world.Items.GetInstance(invalidNewItemId) == null,
            "ItemsDiff duplicate-identity split did not fail closed without source mutation.");

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

    private static void TestStackMergePreservesUnrelatedCellIndexEntries()
    {
        var world = CreateWorldWithItems();
        var source = new Point(4, 4);
        var destination = new Point(5, 4);
        SetOpen(world, source);
        SetOpen(world, destination);

        var unrelatedId = world.Items.SpawnItem("core_item_boulder_granite", destination, 0, 1, 0);
        var existingStackId = world.Items.SpawnItem("core_item_log_oak", destination, 0, 5, 0);
        var movingStackId = world.Items.SpawnItem("core_item_log_oak", source, 0, 2, 0);
        RegressionAssert.True(
            unrelatedId.HasValue && existingStackId.HasValue && movingStackId.HasValue,
            "Multi-stack cell setup failed to spawn items.");

        SimulationDiffApplicator.ApplyAll(
            world,
            new[] { BuildMoveItemDiff(movingStackId.GetValueOrDefault(), destination, 0) });

        var destinationItems = world.Items.GetItemsAt(destination, 0).ToList();
        var unrelated = unrelatedId.GetValueOrDefault();
        RegressionAssert.True(
            world.Items.GetInstance(unrelated)?.Guid == unrelated
            && world.Items.GetInstanceByEntityKey(DiffTargetEncoding.EntityKey(unrelated))?.Guid == unrelated
            && destinationItems.Any(item => item.Guid == unrelated)
            && destinationItems.Where(item => item.DefinitionId == "core_item_log_oak").Sum(item => item.StackCount) == 7
            && destinationItems.Sum(item => item.StackCount) == 8,
            "Merging one definition removed an unrelated item from the destination position index.");

        Console.WriteLine("[PASS] Stack merge preserves unrelated cell index entries");
    }

    private static void TestNonStackableItemsNeverMerge()
    {
        var world = CreateWorldWithItems();
        var spawnCell = new Point(8, 4);
        var moveSource = new Point(8, 6);
        var moveDestination = new Point(9, 6);
        SetOpen(world, spawnCell);
        SetOpen(world, moveSource);
        SetOpen(world, moveDestination);

        var spawnedA = world.Items.SpawnItem("core_item_furniture_bed", spawnCell, 0, 1, 0);
        var spawnedB = world.Items.SpawnItem("core_item_furniture_bed", spawnCell, 0, 1, 1);
        var movingA = world.Items.SpawnItem("core_item_furniture_bed", moveSource, 0, 1, 2);
        var movingB = world.Items.SpawnItem("core_item_furniture_bed", moveDestination, 0, 1, 3);
        RegressionAssert.True(
            spawnedA.HasValue && spawnedB.HasValue && movingA.HasValue && movingB.HasValue,
            "Non-stackable item setup failed to spawn furniture.");

        SimulationDiffApplicator.ApplyAll(
            world,
            new[] { BuildMoveItemDiff(movingA.GetValueOrDefault(), moveDestination, 0) });

        var spawnedItems = world.Items.GetItemsAt(spawnCell, 0).ToList();
        var movedItems = world.Items.GetItemsAt(moveDestination, 0).ToList();
        RegressionAssert.True(
            spawnedA != spawnedB
            && spawnedItems.Count == 2
            && spawnedItems.All(item => item.StackCount == 1)
            && movedItems.Count == 2
            && movedItems.All(item => item.StackCount == 1)
            && world.Items.GetInstance(movingA.GetValueOrDefault()) != null
            && world.Items.GetInstance(movingB.GetValueOrDefault()) != null,
            "Items whose definition uses StackMode.None were merged during spawn or movement.");

        Console.WriteLine("[PASS] Non-stackable items never merge");
    }

    private static void TestStackMergeRespectsDefinitionCapacity()
    {
        var world = CreateWorldWithItems();
        var source = new Point(12, 4);
        var destination = new Point(13, 4);
        var bulkSpawnCell = new Point(14, 4);
        SetOpen(world, source);
        SetOpen(world, destination);
        SetOpen(world, bulkSpawnCell);

        var movingId = world.Items.SpawnItem("core_item_log_oak", source, 0, 7, 0);
        var existingId = world.Items.SpawnItem("core_item_log_oak", destination, 0, 8, 0);
        var bulkPrimaryId = world.Items.SpawnItem("core_item_log_oak", bulkSpawnCell, 0, 25, 0);
        RegressionAssert.True(
            movingId.HasValue && existingId.HasValue && bulkPrimaryId.HasValue,
            "Stack-capacity setup failed to spawn items.");

        SimulationDiffApplicator.ApplyAll(
            world,
            new[] { BuildMoveItemDiff(movingId.GetValueOrDefault(), destination, 0) });

        var destinationStacks = world.Items.GetItemsAt(destination, 0)
            .Where(item => item.DefinitionId == "core_item_log_oak")
            .ToList();
        var bulkSpawnStacks = world.Items.GetItemsAt(bulkSpawnCell, 0)
            .Where(item => item.DefinitionId == "core_item_log_oak")
            .ToList();
        RegressionAssert.True(
            destinationStacks.Count == 2
            && destinationStacks.Sum(item => item.StackCount) == 15
            && destinationStacks.All(item => item.StackCount is > 0 and <= 10)
            && world.Items.GetInstance(movingId.GetValueOrDefault()) != null
            && world.Items.GetInstance(existingId.GetValueOrDefault()) != null,
            "Stack merge exceeded the definition's max_per_stack or lost quantity/identity at capacity.");
        RegressionAssert.True(
            bulkSpawnStacks.Count == 3
            && bulkSpawnStacks.Sum(item => item.StackCount) == 25
            && bulkSpawnStacks.All(item => item.StackCount is > 0 and <= 10)
            && bulkSpawnStacks.Any(item => item.Guid == bulkPrimaryId.GetValueOrDefault()),
            "Bulk spawn did not split quantity into deterministic definition-sized stacks.");

        Console.WriteLine("[PASS] Stack merge respects definition capacity");
    }

    private static void TestStackCompatibilityMetadataPreventsMerge()
    {
        var cases = new (string Name, Action<ItemInstance> Mutate)[]
        {
            ("material", item => item.MaterialId = "core_mat_wood_pine"),
            ("quality", item => item.QualityTier = 2),
            ("ownership", item => item.OwnerFactionId = "test.faction.other"),
            ("access", item => item.UsePolicy = UsePolicy.Private),
            ("reservation", item => item.ReservationTokens.Add(new ReservationToken
            {
                JobGuid = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                ReservedCount = 1,
                ExpiresAtTick = 100,
                ReservationType = "test"
            })),
            ("condition", item =>
            {
                item.ConditionState = "Worn";
                item.DurabilityCurrent = 5;
                item.DurabilityMax = 10;
            }),
            ("artifact", item =>
            {
                item.Artifact = true;
                item.ArtifactName = "Test Artifact";
            }),
            ("provenance", item =>
            {
                item.CraftedBy = Guid.Parse("22222222-2222-2222-2222-222222222222");
                item.MakerFactionId = "test.faction.maker";
                item.StyleTag = "test.style";
            }),
            ("improvements", item => item.Improvements = new List<Improvement>
            {
                new()
                {
                    Type = "engraving",
                    MaterialId = "core_mat_stone_granite",
                    QualityTier = 1,
                    Description = "test"
                }
            }),
            ("perishable", item => item.Perishable = new PerishableState
            {
                CreatedAtTick = 1,
                FreshDurationTicks = 10,
                SpoilDurationTicks = 20,
                CurrentFreshness = 0.5f
            })
        };

        var world = CreateWorldWithItems();
        var failures = new List<string>();
        for (var i = 0; i < cases.Length; i++)
        {
            var source = new Point(16, 2 + i * 2);
            var destination = new Point(17, 2 + i * 2);
            SetOpen(world, source);
            SetOpen(world, destination);

            var movingId = world.Items.SpawnItem("core_item_log_oak", source, 0, 1, 0);
            var existingId = world.Items.SpawnItem("core_item_log_oak", destination, 0, 1, 0);
            if (!movingId.HasValue || !existingId.HasValue)
            {
                failures.Add($"{cases[i].Name}: setup failed");
                continue;
            }

            var movingItem = world.Items.GetInstance(movingId.Value);
            if (movingItem == null)
            {
                failures.Add($"{cases[i].Name}: moving item missing before mutation");
                continue;
            }

            cases[i].Mutate(movingItem);
            SimulationDiffApplicator.ApplyAll(
                world,
                new[] { BuildMoveItemDiff(movingId.Value, destination, 0) });

            var destinationItems = world.Items.GetItemsAt(destination, 0)
                .Where(item => item.DefinitionId == "core_item_log_oak")
                .ToList();
            if (destinationItems.Count != 2
                || destinationItems.Sum(item => item.StackCount) != 2
                || world.Items.GetInstance(movingId.Value) == null
                || world.Items.GetInstance(existingId.Value) == null)
            {
                failures.Add(cases[i].Name);
            }
        }

        RegressionAssert.True(
            failures.Count == 0,
            $"Stacks with incompatible metadata were merged: {string.Join(", ", failures)}");

        Console.WriteLine("[PASS] Stack compatibility metadata prevents merge");
    }

    private static void TestSplitStackPreservesMetadataAndIndexes()
    {
        var world = CreateWorldWithItems();
        var cell = new Point(24, 4);
        SetOpen(world, cell);

        var sourceId = world.Items.SpawnItem("core_item_log_oak", cell, 0, 5, 7);
        RegressionAssert.True(sourceId.HasValue, "Split metadata setup failed to spawn source stack.");
        var source = world.Items.GetInstance(sourceId.GetValueOrDefault())
            ?? throw new InvalidOperationException("Split metadata source stack was missing.");
        source.OwnerFactionId = "test.faction";
        source.OwnerCreatureGuid = Guid.Parse("33333333-3333-3333-3333-333333333333");
        source.UsePolicy = UsePolicy.Faction;
        source.Forbidden = true;
        source.QualityTier = 2;
        source.ConditionState = "Worn";
        source.DurabilityCurrent = 8;
        source.DurabilityMax = 10;
        source.CraftedBy = Guid.Parse("44444444-4444-4444-4444-444444444444");
        source.MakerFactionId = "test.maker";
        source.StyleTag = "test.style";
        source.Improvements = new List<Improvement>
        {
            new()
            {
                Type = "engraving",
                MaterialId = "core_mat_stone_granite",
                QualityTier = 1,
                CreatedBy = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                Description = "test engraving"
            }
        };
        source.Perishable = new PerishableState
        {
            CreatedAtTick = 2,
            FreshDurationTicks = 20,
            SpoilDurationTicks = 40,
            CurrentFreshness = 0.75f
        };

        var splitId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var splitLog = new ItemsDiffLog();
        splitLog.AddSplitStack(
            source.Guid,
            splitId,
            new ChunkKey(0, 0, 0),
            Chunk.LocalIndex(cell.X, cell.Y),
            2,
            100,
            "test");
        ItemsDiffApplicator.ApplyPreSimulation(world, splitLog.MergeAndSort());

        var split = world.Items.GetInstance(splitId);
        var indexedIds = world.Items.GetItemsAt(cell, 0, groundOnly: false)
            .Select(item => item.Guid)
            .ToHashSet();

        RegressionAssert.True(
            source.StackCount == 3
            && split is not null
            && split.StackCount == 2
            && split.MaterialId == source.MaterialId
            && split.OwnerFactionId == source.OwnerFactionId
            && split.OwnerCreatureGuid == source.OwnerCreatureGuid
            && split.UsePolicy == source.UsePolicy
            && split.Forbidden == source.Forbidden
            && split.QualityTier == source.QualityTier
            && split.ConditionState == source.ConditionState
            && split.DurabilityCurrent == source.DurabilityCurrent
            && split.DurabilityMax == source.DurabilityMax
            && split.CraftedBy == source.CraftedBy
            && split.MakerFactionId == source.MakerFactionId
            && split.StyleTag == source.StyleTag
            && split.SpawnedAtTick == source.SpawnedAtTick
            && split.Improvements is { Count: 1 }
            && source.Improvements is { Count: 1 }
            && !ReferenceEquals(split.Improvements, source.Improvements)
            && !ReferenceEquals(split.Improvements[0], source.Improvements[0])
            && split.Improvements[0].Type == source.Improvements[0].Type
            && split.Improvements[0].MaterialId == source.Improvements[0].MaterialId
            && split.Improvements[0].QualityTier == source.Improvements[0].QualityTier
            && split.Improvements[0].CreatedBy == source.Improvements[0].CreatedBy
            && split.Improvements[0].Description == source.Improvements[0].Description
            && split.Perishable != null
            && source.Perishable != null
            && !ReferenceEquals(split.Perishable, source.Perishable)
            && split.Perishable.CreatedAtTick == source.Perishable.CreatedAtTick
            && split.Perishable.FreshDurationTicks == source.Perishable.FreshDurationTicks
            && split.Perishable.SpoilDurationTicks == source.Perishable.SpoilDurationTicks
            && split.Perishable.CurrentFreshness == source.Perishable.CurrentFreshness
            && indexedIds.SetEquals(new[] { source.Guid, splitId })
            && world.Items.GetInstanceByEntityKey(DiffTargetEncoding.EntityKey(source.Guid))?.Guid == source.Guid
            && world.Items.GetInstanceByEntityKey(DiffTargetEncoding.EntityKey(splitId))?.Guid == splitId,
            "Split stack did not conserve quantity, metadata, independent mutable state, or indexes.");

        source.ReservationTokens.Add(new ReservationToken
        {
            JobGuid = Guid.Parse("77777777-7777-7777-7777-777777777777"),
            ReservedCount = 1,
            ExpiresAtTick = 100,
            ReservationType = "test"
        });
        int countBeforeRejectedSplit = source.StackCount;
        var rejectedSplitId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var rejectedSplitLog = new ItemsDiffLog();
        rejectedSplitLog.AddSplitStack(
            source.Guid,
            rejectedSplitId,
            new ChunkKey(0, 0, 0),
            Chunk.LocalIndex(cell.X, cell.Y),
            1,
            100,
            "test");
        var reservedSplitRejected = false;
        try
        {
            ItemsDiffApplicator.ApplyPreSimulation(world, rejectedSplitLog.MergeAndSort());
        }
        catch (InvalidOperationException)
        {
            reservedSplitRejected = true;
        }
        RegressionAssert.True(
            reservedSplitRejected
            && source.StackCount == countBeforeRejectedSplit
            && world.Items.GetInstance(rejectedSplitId) == null,
            "Reserved stack split was not rejected atomically.");

        Console.WriteLine("[PASS] Split stack preserves metadata and indexes");
    }

    private static void TestExternallyOwnedStackIdentityPreventsMerge()
    {
        var world = CreateWorldWithItems();
        var reservedSource = new Point(20, 24);
        var reservedDestination = new Point(21, 24);
        var stockpileSource = new Point(20, 26);
        var stockpileDestination = new Point(21, 26);
        SetOpen(world, reservedSource);
        SetOpen(world, reservedDestination);
        SetOpen(world, stockpileSource);
        SetOpen(world, stockpileDestination);

        var reservedTargetId = world.Items.SpawnItem("core_item_log_oak", reservedDestination, 0, 1, 0);
        var reservedMovingId = world.Items.SpawnItem("core_item_log_oak", reservedSource, 0, 1, 0);
        RegressionAssert.True(
            reservedTargetId.HasValue && reservedMovingId.HasValue,
            "Central-reservation merge setup failed.");
        var reservationHolder = Guid.Parse("99999999-9999-9999-9999-999999999999");
        RegressionAssert.True(
            world.Reservations.TryAcquireItem(
                reservedMovingId.GetValueOrDefault(),
                reservationHolder,
                "test.stack-merge",
                "merge-reservation",
                0,
                100,
                out _),
            "Central-reservation merge setup could not reserve the moving item.");

        SimulationDiffApplicator.ApplyAll(
            world,
            new[] { BuildMoveItemDiff(reservedMovingId.GetValueOrDefault(), reservedDestination, 0) });
        var reservedDestinationItems = world.Items.GetItemsAt(reservedDestination, 0).ToList();

        var chunkKey = new ChunkKey(0, 0, 0);
        var chunk = world.GetChunk(chunkKey)
            ?? throw new InvalidOperationException("Expected stockpile test chunk.");
        var stockpileData = chunk.GetStockpileData()
            ?? throw new InvalidOperationException("Expected stockpile test data.");
        int zoneId = world.Stockpiles.CreateZone("Stack Identity", chunkKey, 0);
        stockpileData.CreateOrUpdateShard(zoneId, chunkKey);
        stockpileData.AddCellsToZone(zoneId, new[]
        {
            Chunk.LocalIndex(stockpileDestination.X, stockpileDestination.Y)
        });

        var stockpileTargetId = world.Items.SpawnItem("core_item_log_oak", stockpileDestination, 0, 1, 0);
        var stockpileMovingId = world.Items.SpawnItem("core_item_log_oak", stockpileSource, 0, 1, 0);
        RegressionAssert.True(
            stockpileTargetId.HasValue && stockpileMovingId.HasValue,
            "Stockpile-identity merge setup failed.");
        SimulationDiffApplicator.ApplyAll(
            world,
            new[] { BuildMoveItemDiff(stockpileMovingId.GetValueOrDefault(), stockpileDestination, 0) });
        var stockpileDestinationItems = world.Items.GetItemsAt(stockpileDestination, 0).ToList();

        RegressionAssert.True(
            reservedDestinationItems.Count == 2
            && world.Items.GetInstance(reservedTargetId.GetValueOrDefault()) != null
            && world.Items.GetInstance(reservedMovingId.GetValueOrDefault()) != null
            && world.Reservations.IsItemReserved(reservedMovingId.GetValueOrDefault(), 0)
            && stockpileDestinationItems.Count == 2
            && world.Items.GetInstance(stockpileTargetId.GetValueOrDefault()) != null
            && world.Items.GetInstance(stockpileMovingId.GetValueOrDefault()) != null,
            "Merge consumed an item identity owned by reservations or stockpile indexing.");

        Console.WriteLine("[PASS] Externally owned stack identity prevents merge");
    }

    private static void TestCentralReservationRejectsRemoveAndSplit()
    {
        var world = CreateWorldWithItems();
        var cell = new Point(28, 28);
        SetOpen(world, cell);

        var sourceId = world.Items.SpawnItem("core_item_log_oak", cell, 0, 5, 0);
        RegressionAssert.True(sourceId.HasValue, "Central-reservation mutation setup failed.");
        var source = sourceId.GetValueOrDefault();
        var holder = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        RegressionAssert.True(
            world.Reservations.TryAcquireItem(
                source,
                holder,
                "test.stack-mutation",
                "mutation-reservation",
                currentTick: 10,
                expireTick: 20,
                out _),
            "Central-reservation mutation setup could not reserve source stack.");

        var splitId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
        var log = new ItemsDiffLog();
        var chunk = new ChunkKey(0, 0, 0);
        int localIndex = Chunk.LocalIndex(cell.X, cell.Y);
        log.AddRemoveItem(source, chunk, localIndex, 2, 100, "test");
        log.AddSplitStack(source, splitId, chunk, localIndex, 2, 100, "test");
        var ownershipViolationRejected = false;
        try
        {
            ItemsDiffApplicator.ApplyPreSimulation(world, log.MergeAndSort(), currentTick: 10);
        }
        catch (InvalidOperationException)
        {
            ownershipViolationRejected = true;
        }

        var indexedIds = world.Items.GetItemsAt(cell, 0, groundOnly: false)
            .Select(item => item.Guid)
            .ToArray();
        RegressionAssert.True(
            ownershipViolationRejected
            && world.Items.GetInstance(source)?.StackCount == 5
            && world.Items.GetInstance(splitId) == null
            && indexedIds.SequenceEqual(new[] { source })
            && world.Items.GetInstanceByEntityKey(DiffTargetEncoding.EntityKey(source))?.Guid == source
            && world.Reservations.IsItemReserved(source, currentTick: 10),
            "Live central reservation did not reject remove and split atomically.");

        var expiredRemoval = world.Items.RemoveQuantity(source, 1, currentTick: 21);
        RegressionAssert.True(
            expiredRemoval is { Status: ItemMutationStatus.Applied, AppliedQuantity: 1 }
            && world.Items.GetInstance(source)?.StackCount == 4
            && !world.Reservations.IsItemReserved(source, currentTick: 21),
            "Expired central reservation continued blocking owner mutation.");

        Console.WriteLine("[PASS] Central reservation rejects remove and split");
    }

    private static void TestStackMergeReportsIdentityTransfers()
    {
        var world = CreateWorldWithItems();
        var firstCell = new Point(26, 20);
        var secondCell = new Point(27, 20);
        SetOpen(world, firstCell);
        SetOpen(world, secondCell);

        var firstId = world.Items.SpawnItem("core_item_log_oak", firstCell, 0, 2, 0);
        var secondId = world.Items.SpawnItem("core_item_log_oak", secondCell, 0, 3, 0);
        RegressionAssert.True(firstId.HasValue && secondId.HasValue, "Merge-transfer setup failed.");
        world.Items.UpdateItemPosition(secondId.GetValueOrDefault(), firstCell, 0);

        var result = world.Items.MergeStacksAt(firstCell, 0);
        Guid targetId = firstId.GetValueOrDefault().CompareTo(secondId.GetValueOrDefault()) <= 0
            ? firstId.GetValueOrDefault()
            : secondId.GetValueOrDefault();
        Guid sourceId = targetId == firstId.GetValueOrDefault()
            ? secondId.GetValueOrDefault()
            : firstId.GetValueOrDefault();
        int expectedTransferQuantity = sourceId == firstId.GetValueOrDefault() ? 2 : 3;

        RegressionAssert.True(
            result is
            {
                Status: ItemMutationStatus.Applied,
                TransferredQuantity: var transferredQuantity,
                RemovedInstanceCount: 1,
                Transfers.Count: 1
            }
            && transferredQuantity == expectedTransferQuantity
            && result.Transfers[0] == new ItemStackTransfer(
                sourceId,
                targetId,
                expectedTransferQuantity,
                SourceRemoved: true),
            "Stack merge did not report the deterministic source-to-survivor identity transfer.");

        Console.WriteLine("[PASS] Stack merge reports identity transfers");
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
