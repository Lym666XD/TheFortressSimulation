using HumanFortress.Simulation.World;
using HumanFortress.Simulation.Stockpile;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Items;

internal sealed partial class ItemManager
{
    private const int MaxCreatedStacksPerSpawn = 4096;

    /// <summary>
    /// Convenience entry for callers that only need the primary affected stack.
    /// Use SpawnItems when every affected or created stack must be observed.
    /// </summary>
    internal Guid? SpawnItem(string itemId, Point worldPos, int z, int quantity = 1, ulong currentTick = 0)
    {
        return SpawnItems(itemId, worldPos, z, quantity, currentTick).PrimaryItemId;
    }

    /// <summary>
    /// Atomically distribute quantity across compatible capacity and new stacks.
    /// </summary>
    internal ItemSpawnResult SpawnItems(
        string itemId,
        Point worldPos,
        int z,
        int quantity = 1,
        ulong currentTick = 0)
    {
        try
        {
            Emit($"[ItemManager] SpawnItem called: id={itemId}, pos=({worldPos.X},{worldPos.Y},{z}), qty={quantity}");
            Emit($"[ItemManager] Definitions loaded: {_definitionCatalog.DefinitionCount}");

            // Validate definition exists
            var def = _definitionCatalog.GetDefinition(itemId);
            if (def == null)
            {
                Emit($"[ItemManager] ERROR: Unknown item '{itemId}'");
                Emit($"[ItemManager] Available items: {string.Join(", ", _definitionCatalog.GetAllDefinitions().Select(definition => definition.Id).Take(5))}");
                return ItemSpawnResult.Rejected(quantity, $"Unknown item definition '{itemId}'.");
            }

            if (quantity <= 0)
            {
                Emit($"[ItemManager] ERROR: Spawn quantity must be positive, received {quantity}");
                return ItemSpawnResult.Rejected(quantity, "Spawn quantity must be positive.");
            }

            // Validate world is set
            if (_world == null)
            {
                Emit("[ItemManager] ERROR: World not set");
                return ItemSpawnResult.Rejected(quantity, "World dependency is not set.");
            }

            // Calculate chunk and local position
            var chunkX = worldPos.X / 32;
            var chunkY = worldPos.Y / 32;
            var localX = worldPos.X % 32;
            var localY = worldPos.Y % 32;

            Emit($"[ItemManager] Chunk coords: ({chunkX},{chunkY},{z}), Local: ({localX},{localY})");

            var chunkKey = new ChunkKey(chunkX, chunkY, z);
            var chunk = _world.GetChunk(chunkKey);

            if (chunk == null)
            {
                Emit($"[ItemManager] ERROR: Chunk not found at ({chunkX},{chunkY},{z})");
                return ItemSpawnResult.Rejected(quantity, "Target chunk does not exist.");
            }

            Emit("[ItemManager] Chunk found, getting tile...");

            // Validate tile supports spawning items.
            // Policy change: allow any walkable tile (floor/ramp/stairs) not just OpenWithFloor.
            var tile = chunk.GetTile(localX, localY);
            Emit($"[ItemManager] Tile kind: {tile.Kind}");

            if (!tile.IsWalkable)
            {
                Emit($"[ItemManager] ERROR: Tile at ({worldPos.X},{worldPos.Y},{z}) is not walkable (kind={tile.Kind})");
                return ItemSpawnResult.Rejected(quantity, "Target tile is not walkable.");
            }

            lock (_instanceLock)
            {
                var template = new ItemInstance(Guid.Empty, itemId, worldPos, z, quantity, currentTick)
                {
                    MaterialId = def.FixedMaterial
                };
                bool stackable = ItemStackPolicy.TryGetCapacity(def, out int capacity);
                bool requiresEmpty = def.Stack?.RequiresEmpty == true;
                bool cellIdentityOwnedByStockpile = StockpileWorldQueries.TryGetStockpileCell(
                    _world,
                    worldPos.X,
                    worldPos.Y,
                    z,
                    out _);
                var groundAtPos = GetGroundItemsAtLocked(worldPos, z);
                var mergeTargets = new List<ItemInstance>();
                if (stackable && !cellIdentityOwnedByStockpile)
                {
                    foreach (var existingItem in groundAtPos.OrderBy(static item => item.Guid))
                    {
                        if (existingItem.StackCount <= 0
                            || existingItem.StackCount >= capacity
                            || _world.Reservations.IsItemReserved(existingItem.Guid, currentTick)
                            || !ItemStackPolicy.AreCompatible(
                                existingItem,
                                template,
                                def,
                                !requiresEmpty || IsContainerEmptyLocked(existingItem.Guid),
                                secondContainerIsEmpty: true))
                        {
                            continue;
                        }

                        mergeTargets.Add(existingItem);
                    }
                }

                long existingCapacity = mergeTargets.Sum(item => (long)capacity - item.StackCount);
                long quantityRequiringNewStacks = Math.Max(0L, (long)quantity - existingCapacity);
                long requiredNewStacks = stackable
                    ? (quantityRequiringNewStacks + capacity - 1L) / capacity
                    : quantityRequiringNewStacks;
                if (requiredNewStacks > MaxCreatedStacksPerSpawn)
                {
                    return ItemSpawnResult.Rejected(
                        quantity,
                        $"Spawn would create {requiredNewStacks} stacks; limit is {MaxCreatedStacksPerSpawn}.");
                }

                int remaining = quantity;
                var transfers = new List<ItemSpawnTransfer>(mergeTargets.Count + (int)requiredNewStacks);
                foreach (var existingItem in mergeTargets)
                {
                    if (remaining == 0)
                        break;

                    int transfer = Math.Min(remaining, capacity - existingItem.StackCount);
                    existingItem.StackCount += transfer;
                    remaining -= transfer;
                    transfers.Add(new ItemSpawnTransfer(existingItem.Guid, transfer, Created: false));
                }

                while (remaining > 0)
                {
                    int stackQuantity = stackable ? Math.Min(remaining, capacity) : 1;
                    var guid = CreateNextInstanceGuidLocked();
                    var instance = new ItemInstance(guid, itemId, worldPos, z, stackQuantity, currentTick)
                    {
                        MaterialId = def.FixedMaterial
                    };
                    EntityKeyIndexAdd(guid);
                    _instances.Add(guid, instance);
                    IndexAdd(guid, worldPos, z);
                    transfers.Add(new ItemSpawnTransfer(guid, stackQuantity, Created: true));
                    remaining -= stackQuantity;
                }

                string spawnMsg = $"[ItemManager] SUCCESS: Spawned '{def.Name}' (id={itemId}, primary={transfers[0].ItemId}, qty={quantity}, stacks={transfers.Count}) at ({worldPos.X},{worldPos.Y},{z})";
                Emit(spawnMsg);
                return new ItemSpawnResult(
                    ItemMutationStatus.Applied,
                    quantity,
                    quantity,
                    transfers.ToArray(),
                    string.Empty);
            }
        }
        catch (Exception ex)
        {
            Emit($"[ItemManager] EXCEPTION: Failed to spawn '{itemId}' at ({worldPos.X},{worldPos.Y},{z})");
            Emit($"[ItemManager] Exception: {ex.GetType().Name}: {ex.Message}");
            Emit($"[ItemManager] StackTrace: {ex.StackTrace}");
            return ItemSpawnResult.Rejected(quantity, $"Spawn failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private List<ItemInstance> GetGroundItemsAtLocked(Point worldPos, int z)
    {
        var key = KeyFor(worldPos, z);
        if (!_posIndex.TryGetValue(key, out var ids) || ids.Count == 0)
            return new List<ItemInstance>();

        var items = new List<ItemInstance>(ids.Count);
        foreach (var guid in ids)
        {
            if (_instances.TryGetValue(guid, out var instance) && instance.IsOnGround)
                items.Add(instance);
        }

        return items;
    }
}
