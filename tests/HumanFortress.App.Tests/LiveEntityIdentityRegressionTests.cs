using HumanFortress.Contracts.Simulation.Save;
using HumanFortress.Core.Simulation;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Replay;
using HumanFortress.Simulation.Save;
using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

internal static class LiveEntityIdentityRegressionTests
{
    internal static void RunAll()
    {
        TestItemGuidAndEntityKeyCollisionsFailAtomically();
        TestItemIdentityAllocatorDoesNotRetreatAcrossRestore();
        TestCreatureGuidAndEntityKeyCollisionsFailAtomically();
        TestCreatureIdentityAllocatorDoesNotRetreatAcrossRestore();
        TestIdentityLedgerAffectsReplaySections();
    }

    private static void TestItemGuidAndEntityKeyCollisionsFailAtomically()
    {
        var world = CreateWorld(loadItems: true, loadCreatures: false);
        var cell = new Point(3, 3);
        SetOpen(world, cell);

        var sourceId = world.Items.SpawnItem("core_item_log_oak", cell, 0, quantity: 3, currentTick: 0)
            ?? throw new InvalidOperationException("Expected identity-test item to spawn.");
        var collidingId = CreateEntityKeyCollision(sourceId, 0x41);
        string initialHash = WorldReplayHashBuilder.Build(world);

        var duplicateResult = world.Items.SplitStackWithGuid(sourceId, 1, sourceId);
        var collisionResult = world.Items.SplitStackWithGuid(sourceId, 1, collidingId);
        RegressionAssert.True(
            !duplicateResult.Success
            && !collisionResult.Success
            && world.Items.InstanceCount == 1
            && world.Items.GetInstance(sourceId)?.StackCount == 3
            && world.Items.GetInstance(collidingId) == null
            && world.Items.GetInstanceByEntityKey(DiffTargetEncoding.EntityKey(sourceId))?.Guid == sourceId
            && world.Items.GetItemsAt(cell, 0, groundOnly: false).Single().Guid == sourceId
            && WorldReplayHashBuilder.Build(world) == initialHash,
            "Duplicate or colliding item split mutated the live entity or its indexes.");

        var restoreOwner = Guid.Parse("10203040-5060-7080-90a0-b0c0d0e0f001");
        var restoreCollision = CreateEntityKeyCollision(restoreOwner, 0x52);
        string beforeRestoreFailureHash = WorldReplayHashBuilder.Build(world);
        var duplicateRestoreIssues = world.Items.RestoreItemsSnapshot(new[]
        {
            CreateItemPayload(restoreOwner, cell),
            CreateItemPayload(restoreOwner, cell)
        });
        var collisionRestoreIssues = world.Items.RestoreItemsSnapshot(new[]
        {
            CreateItemPayload(restoreOwner, cell),
            CreateItemPayload(restoreCollision, cell)
        });
        RegressionAssert.True(
            duplicateRestoreIssues.Count > 0
            && collisionRestoreIssues.Count > 0
            && world.Items.InstanceCount == 1
            && world.Items.GetInstance(sourceId)?.StackCount == 3
            && world.Items.GetInstanceByEntityKey(DiffTargetEncoding.EntityKey(sourceId))?.Guid == sourceId
            && WorldReplayHashBuilder.Build(world) == beforeRestoreFailureHash,
            "Invalid item restore identities changed the pre-existing entity set or replay hash.");

        var worldPayload = WorldSavePayloadBuilder.Build(world);
        var collidingWorldPayload = worldPayload with
        {
            Counts = worldPayload.Counts with { ItemCount = 2 },
            Items = new[]
            {
                CreateItemPayload(restoreOwner, cell),
                CreateItemPayload(restoreCollision, cell)
            }
        };
        var worldRestore = WorldSavePayloadRestorer.RestoreSupportedSections(collidingWorldPayload);
        RegressionAssert.True(
            !worldRestore.Success
            && worldRestore.World == null
            && worldRestore.Issues.Any(issue => issue.Contains("collides", StringComparison.Ordinal)
                && issue.Contains("entity key", StringComparison.Ordinal)),
            "World payload restore did not reject an item projection collision during preflight.");

        var retiredId = Guid.Parse("21324354-6576-8798-a9ba-cbdcedfe1021");
        RegressionAssert.True(
            DiffTargetEncoding.EntityKey(retiredId) != DiffTargetEncoding.EntityKey(sourceId),
            "Retired item identity test setup produced an unintended collision.");
        var splitResult = world.Items.SplitStackWithGuid(sourceId, 1, retiredId);
        RegressionAssert.True(
            splitResult.Success && world.Items.RemoveInstance(retiredId),
            "Retired item identity test setup failed.");

        ulong retiredKey = DiffTargetEncoding.EntityKey(retiredId);
        var staleAlias = CreateEntityKeyCollision(retiredId, 0x63);
        string beforeStaleAliasHash = WorldReplayHashBuilder.Build(world);
        int sourceCountBeforeAlias = world.Items.GetInstance(sourceId)?.StackCount ?? -1;
        var retiredGuidReuseResult = world.Items.SplitStackWithGuid(sourceId, 1, retiredId);
        var staleAliasResult = world.Items.SplitStackWithGuid(sourceId, 1, staleAlias);
        RegressionAssert.True(
            !retiredGuidReuseResult.Success
            && !staleAliasResult.Success
            && world.Items.GetInstance(sourceId)?.StackCount == sourceCountBeforeAlias
            && world.Items.GetInstance(staleAlias) == null
            && world.Items.GetInstanceByEntityKey(retiredKey) == null
            && WorldReplayHashBuilder.Build(world) == beforeStaleAliasHash,
            "A retired item entity key rebound to a new GUID or changed replay state.");

        Console.WriteLine("[PASS] Item GUID and entity-key collisions fail atomically");
    }

    private static void TestItemIdentityAllocatorDoesNotRetreatAcrossRestore()
    {
        var world = CreateWorld(loadItems: true, loadCreatures: false);
        var firstCell = new Point(4, 4);
        var secondCell = new Point(5, 4);
        var thirdCell = new Point(6, 4);
        SetOpen(world, firstCell);
        SetOpen(world, secondCell);
        SetOpen(world, thirdCell);

        var firstId = world.Items.SpawnItem("core_item_log_oak", firstCell, 0)
            ?? throw new InvalidOperationException("Expected first allocator-test item to spawn.");
        var secondId = world.Items.SpawnItem("core_item_log_oak", secondCell, 0)
            ?? throw new InvalidOperationException("Expected second allocator-test item to spawn.");
        var restoreIssues = world.Items.RestoreItemsSnapshot(Array.Empty<WorldSaveItemPayloadData>());
        var thirdId = world.Items.SpawnItem("core_item_log_oak", thirdCell, 0)
            ?? throw new InvalidOperationException("Expected post-restore allocator-test item to spawn.");

        RegressionAssert.True(
            restoreIssues.Count == 0
            && thirdId != firstId
            && thirdId != secondId
            && world.Items.GetInstanceByEntityKey(DiffTargetEncoding.EntityKey(firstId)) == null
            && world.Items.GetInstanceByEntityKey(DiffTargetEncoding.EntityKey(secondId)) == null
            && world.Items.GetInstanceByEntityKey(DiffTargetEncoding.EntityKey(thirdId))?.Guid == thirdId,
            "Item identity sequence retreated or a stale key aliased after restore.");

        Console.WriteLine("[PASS] Item identity allocator does not retreat across restore");
    }

    private static void TestCreatureGuidAndEntityKeyCollisionsFailAtomically()
    {
        var world = CreateWorld(loadItems: false, loadCreatures: true);
        var cell = new Point(7, 7);
        SetOpen(world, cell);
        var originalId = world.Creatures.SpawnCreature(
            "core_race_dwarf",
            cell,
            0,
            factionId: "player",
            currentTick: 0)
            ?? throw new InvalidOperationException("Expected identity-test creature to spawn.");

        var restoreOwner = Guid.Parse("31425364-7586-97a8-b9ca-dbecfd0e1f20");
        var restoreCollision = CreateEntityKeyCollision(restoreOwner, 0x74);
        string initialHash = WorldReplayHashBuilder.Build(world);
        var duplicateIssues = world.Creatures.RestoreCreaturesSnapshot(new[]
        {
            CreateCreaturePayload(restoreOwner, cell),
            CreateCreaturePayload(restoreOwner, cell)
        });
        var collisionIssues = world.Creatures.RestoreCreaturesSnapshot(new[]
        {
            CreateCreaturePayload(restoreOwner, cell),
            CreateCreaturePayload(restoreCollision, cell)
        });
        RegressionAssert.True(
            duplicateIssues.Count > 0
            && collisionIssues.Count > 0
            && world.Creatures.InstanceCount == 1
            && world.Creatures.GetInstance(originalId) != null
            && world.Creatures.GetInstanceByEntityKey(DiffTargetEncoding.EntityKey(originalId))?.Guid == originalId
            && WorldReplayHashBuilder.Build(world) == initialHash,
            "Invalid creature restore identities changed the live entity set or replay hash.");

        var worldPayload = WorldSavePayloadBuilder.Build(world);
        var collidingWorldPayload = worldPayload with
        {
            Counts = worldPayload.Counts with { CreatureCount = 2 },
            Creatures = new[]
            {
                CreateCreaturePayload(restoreOwner, cell),
                CreateCreaturePayload(restoreCollision, cell)
            }
        };
        var worldRestore = WorldSavePayloadRestorer.RestoreSupportedSections(collidingWorldPayload);
        RegressionAssert.True(
            !worldRestore.Success
            && worldRestore.World == null
            && worldRestore.Issues.Any(issue => issue.Contains("collides", StringComparison.Ordinal)
                && issue.Contains("entity key", StringComparison.Ordinal)),
            "World payload restore did not reject a creature projection collision during preflight.");

        var replacementId = Guid.Parse("42536475-8697-a8b9-cadb-ecfd0e1f2031");
        var replacementIssues = world.Creatures.RestoreCreaturesSnapshot(new[]
        {
            CreateCreaturePayload(replacementId, new Point(8, 7))
        });
        RegressionAssert.True(replacementIssues.Count == 0, "Creature replacement restore setup failed.");

        ulong retiredKey = DiffTargetEncoding.EntityKey(originalId);
        var staleAlias = CreateEntityKeyCollision(originalId, 0x35);
        string beforeStaleAliasHash = WorldReplayHashBuilder.Build(world);
        var retiredGuidIssues = world.Creatures.RestoreCreaturesSnapshot(new[]
        {
            CreateCreaturePayload(originalId, cell)
        });
        var staleAliasIssues = world.Creatures.RestoreCreaturesSnapshot(new[]
        {
            CreateCreaturePayload(staleAlias, cell)
        });
        RegressionAssert.True(
            retiredGuidIssues.Count > 0
            && staleAliasIssues.Count > 0
            && world.Creatures.InstanceCount == 1
            && world.Creatures.GetInstance(replacementId) != null
            && world.Creatures.GetInstance(staleAlias) == null
            && world.Creatures.GetInstanceByEntityKey(retiredKey) == null
            && world.Creatures.GetInstanceByEntityKey(DiffTargetEncoding.EntityKey(replacementId))?.Guid == replacementId
            && WorldReplayHashBuilder.Build(world) == beforeStaleAliasHash,
            "A retired creature entity key rebound to a new GUID or changed replay state.");

        Console.WriteLine("[PASS] Creature GUID and entity-key collisions fail atomically");
    }

    private static void TestCreatureIdentityAllocatorDoesNotRetreatAcrossRestore()
    {
        var world = CreateWorld(loadItems: false, loadCreatures: true);
        var firstCell = new Point(9, 9);
        var secondCell = new Point(10, 9);
        var thirdCell = new Point(11, 9);
        SetOpen(world, firstCell);
        SetOpen(world, secondCell);
        SetOpen(world, thirdCell);

        var firstId = world.Creatures.SpawnCreature("core_race_dwarf", firstCell, 0, "player", 0)
            ?? throw new InvalidOperationException("Expected first allocator-test creature to spawn.");
        var secondId = world.Creatures.SpawnCreature("core_race_dwarf", secondCell, 0, "player", 0)
            ?? throw new InvalidOperationException("Expected second allocator-test creature to spawn.");
        var restoreIssues = world.Creatures.RestoreCreaturesSnapshot(Array.Empty<WorldSaveCreaturePayloadData>());
        var thirdId = world.Creatures.SpawnCreature("core_race_dwarf", thirdCell, 0, "player", 0)
            ?? throw new InvalidOperationException("Expected post-restore allocator-test creature to spawn.");

        RegressionAssert.True(
            restoreIssues.Count == 0
            && thirdId != firstId
            && thirdId != secondId
            && world.Creatures.GetInstanceByEntityKey(DiffTargetEncoding.EntityKey(firstId)) == null
            && world.Creatures.GetInstanceByEntityKey(DiffTargetEncoding.EntityKey(secondId)) == null
            && world.Creatures.GetInstanceByEntityKey(DiffTargetEncoding.EntityKey(thirdId))?.Guid == thirdId,
            "Creature identity sequence retreated or a stale key aliased after restore.");

        Console.WriteLine("[PASS] Creature identity allocator does not retreat across restore");
    }

    private static void TestIdentityLedgerAffectsReplaySections()
    {
        var withHistory = CreateWorld(loadItems: true, loadCreatures: true);
        var withoutHistory = CreateWorld(loadItems: true, loadCreatures: true);
        var cell = new Point(12, 12);
        SetOpen(withHistory, cell);
        SetOpen(withoutHistory, cell);

        var retiredItemId = withHistory.Items.SpawnItem("core_item_log_oak", cell, 0)
            ?? throw new InvalidOperationException("Expected retired replay-ledger item to spawn.");
        RegressionAssert.True(
            withHistory.Items.RemoveInstance(retiredItemId),
            "Replay-ledger item retirement setup failed.");
        var liveItemId = withHistory.Items.SpawnItem("core_item_log_oak", cell, 0)
            ?? throw new InvalidOperationException("Expected live replay-ledger item to spawn.");
        var liveItemPayload = WorldSavePayloadBuilder.Build(withHistory).Items
            .Single(item => item.Guid == liveItemId);
        var itemRestoreIssues = withoutHistory.Items.RestoreItemsSnapshot(new[]
        {
            liveItemPayload
        });

        withHistory.Creatures.SpawnCreature("core_race_dwarf", cell, 0, "player", 0);
        var liveCreatureId = Guid.Parse("53647586-97a8-b9ca-dbec-fd0e1f203142");
        var creaturePayload = CreateCreaturePayload(liveCreatureId, cell);
        var historyCreatureIssues = withHistory.Creatures.RestoreCreaturesSnapshot(new[] { creaturePayload });
        var noHistoryCreatureIssues = withoutHistory.Creatures.RestoreCreaturesSnapshot(new[] { creaturePayload });

        var withHistoryFirst = WorldReplayHashBuilder.BuildSectionHashes(withHistory);
        var withHistorySecond = WorldReplayHashBuilder.BuildSectionHashes(withHistory);
        var withoutHistoryHashes = WorldReplayHashBuilder.BuildSectionHashes(withoutHistory);
        RegressionAssert.True(
            itemRestoreIssues.Count == 0
            && historyCreatureIssues.Count == 0
            && noHistoryCreatureIssues.Count == 0
            && withHistory.Items.GetAllInstances().Select(item => item.Guid)
                .SequenceEqual(withoutHistory.Items.GetAllInstances().Select(item => item.Guid))
            && withHistory.Creatures.GetAllInstances().Select(creature => creature.Guid)
                .SequenceEqual(withoutHistory.Creatures.GetAllInstances().Select(creature => creature.Guid))
            && withHistoryFirst == withHistorySecond
            && withHistoryFirst.ItemsHash != withoutHistoryHashes.ItemsHash
            && withHistoryFirst.CreaturesHash != withoutHistoryHashes.CreaturesHash,
            "Identity allocator/tombstone history was unstable or absent from item/creature replay sections.");

        Console.WriteLine("[PASS] Identity ledger affects replay sections deterministically");
    }

    private static World CreateWorld(bool loadItems, bool loadCreatures)
    {
        var world = new World(2, 1);
        if (loadItems)
            DefinitionCatalogTestSupport.LoadItems(world);
        if (loadCreatures)
            DefinitionCatalogTestSupport.LoadCreatures(world);
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

    private static Guid CreateEntityKeyCollision(Guid owner, byte discriminator)
    {
        var bytes = owner.ToByteArray();
        bytes[15] ^= discriminator == 0 ? (byte)0x01 : discriminator;
        var collision = new Guid(bytes);
        RegressionAssert.True(
            collision != owner
            && DiffTargetEncoding.EntityKey(collision) == DiffTargetEncoding.EntityKey(owner),
            "Entity-key collision test setup failed.");
        return collision;
    }

    private static WorldSaveItemPayloadData CreateItemPayload(Guid id, Point cell)
    {
        return new WorldSaveItemPayloadData(
            id,
            "core_item_log_oak",
            null,
            1,
            new WorldSavePointData(cell.X, cell.Y),
            0,
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

    private static WorldSaveCreaturePayloadData CreateCreaturePayload(Guid id, Point cell)
    {
        return new WorldSaveCreaturePayloadData(
            id,
            "core_race_dwarf",
            "player",
            new WorldSavePointData(cell.X, cell.Y),
            Z: 0,
            HP: 100,
            MaxHP: 100,
            SpawnedAtTick: 0);
    }
}
