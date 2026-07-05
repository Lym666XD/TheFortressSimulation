using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Items;

internal sealed partial class ItemManager
{
    /// <summary>
    /// Spawn an item at the specified world position.
    /// Returns item GUID on success, null on failure.
    /// TODO: Use Diff-Log instead of direct Chunk write (per UPDATE_ORDER.md)
    /// TODO: Implement stack merging when spawning stackable items
    /// </summary>
    public Guid? SpawnItem(string itemId, Point worldPos, int z, int quantity = 1, ulong currentTick = 0)
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
                return null;
            }

            // Validate world is set
            if (_world == null)
            {
                Emit("[ItemManager] ERROR: World not set");
                return null;
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
                return null;
            }

            Emit("[ItemManager] Chunk found, getting tile...");

            // Validate tile supports spawning items.
            // Policy change: allow any walkable tile (floor/ramp/stairs) not just OpenWithFloor.
            var tile = chunk.GetTile(localX, localY);
            Emit($"[ItemManager] Tile kind: {tile.Kind}");

            if (!tile.IsWalkable)
            {
                Emit($"[ItemManager] ERROR: Tile at ({worldPos.X},{worldPos.Y},{z}) is not walkable (kind={tile.Kind})");
                return null;
            }

            // Check for existing stacks and merge if stackable (L5 layer)
            lock (_instanceLock)
            {
                // Find existing items at same position with same itemId
                var existingAtPos = _instances.Values
                    .Where(i => i.IsOnGround && i.Position.X == worldPos.X && i.Position.Y == worldPos.Y && i.Z == z && i.DefinitionId == itemId)
                    .OrderBy(i => i.Guid)
                    .ToList();

                if (existingAtPos.Count > 0)
                {
                    // Stack with first existing item
                    var existingItem = existingAtPos[0];
                    existingItem.StackCount += quantity;
                    string stackMsg = $"[ItemManager] SUCCESS: Stacked '{def.Name}' +{quantity} onto existing stack (guid={existingItem.Guid}, new qty={existingItem.StackCount}) at ({worldPos.X},{worldPos.Y},{z})";
                    Emit(stackMsg);
                    return existingItem.Guid;
                }

                // Extra diagnostics: if no stack match but there are other items here, log what's present
                var anyAtPos = _instances.Values
                    .Where(i => i.IsOnGround && i.Position.X == worldPos.X && i.Position.Y == worldPos.Y && i.Z == z)
                    .OrderBy(i => i.Guid)
                    .ToList();
                if (anyAtPos.Count > 0)
                {
                    var byId = anyAtPos
                        .GroupBy(i => i.DefinitionId)
                        .OrderBy(g => g.Key)
                        .Select(g => $"{g.Key}*{g.Sum(it => it.StackCount)}")
                        .ToList();
                    string diag = $"[ItemManager] STACK-CHECK: No stack match for id={itemId} at ({worldPos.X},{worldPos.Y},{z}); present={{{string.Join(", ", byId)}}}";
                    Emit(diag);
                }

                // Create new instance
                var guid = CreateNextInstanceGuidLocked();
                var instance = new ItemInstance(guid, itemId, worldPos, z, quantity, currentTick);
                instance.MaterialId = def.FixedMaterial;
                _instances[guid] = instance;
                IndexAdd(guid, worldPos, z);

                string spawnMsg = $"[ItemManager] SUCCESS: Spawned '{def.Name}' (id={itemId}, guid={guid}, qty={quantity}) at ({worldPos.X},{worldPos.Y},{z})";
                Emit(spawnMsg);
                return guid;
            }
        }
        catch (Exception ex)
        {
            Emit($"[ItemManager] EXCEPTION: Failed to spawn '{itemId}' at ({worldPos.X},{worldPos.Y},{z})");
            Emit($"[ItemManager] Exception: {ex.GetType().Name}: {ex.Message}");
            Emit($"[ItemManager] StackTrace: {ex.StackTrace}");
            return null;
        }
    }
}
