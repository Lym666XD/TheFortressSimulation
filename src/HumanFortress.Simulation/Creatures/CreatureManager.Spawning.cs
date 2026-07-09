using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Creatures;

internal sealed partial class CreatureManager
{
    /// <summary>
    /// Spawn a creature at the specified world position.
    /// Returns creature GUID on success, null on failure.
    /// TODO: Use Diff-Log instead of direct Chunk write (per UPDATE_ORDER.md)
    /// </summary>
    public Guid? SpawnCreature(string creatureId, Point worldPos, int z, string factionId = "neutral", ulong currentTick = 0)
    {
        try
        {
            Emit($"[CreatureManager] SpawnCreature called: id={creatureId}, pos=({worldPos.X},{worldPos.Y},{z})");
            Emit($"[CreatureManager] Definitions loaded: {_definitionCatalog.DefinitionCount}");

            var def = _definitionCatalog.GetDefinition(creatureId);
            if (def == null)
            {
                Emit($"[CreatureManager] ERROR: Unknown creature '{creatureId}'");
                Emit($"[CreatureManager] Available creatures: {string.Join(", ", _definitionCatalog.GetAllDefinitions().Select(definition => definition.Id).Take(5))}");
                return null;
            }

            if (_world == null)
            {
                Emit("[CreatureManager] ERROR: World not set");
                return null;
            }

            int chunkX = worldPos.X / 32;
            int chunkY = worldPos.Y / 32;
            int localX = worldPos.X % 32;
            int localY = worldPos.Y % 32;

            Emit($"[CreatureManager] Chunk coords: ({chunkX},{chunkY},{z}), Local: ({localX},{localY})");

            var chunkKey = new ChunkKey(chunkX, chunkY, z);
            var chunk = _world.GetChunk(chunkKey);

            if (chunk == null)
            {
                Emit($"[CreatureManager] ERROR: Chunk not found at ({chunkX},{chunkY},{z})");
                return null;
            }

            Emit("[CreatureManager] Chunk found, getting tile...");

            var tile = chunk.GetTile(localX, localY);
            Emit($"[CreatureManager] Tile kind: {tile.Kind}");

            if (!tile.IsWalkable)
            {
                Emit($"[CreatureManager] ERROR: Tile at ({worldPos.X},{worldPos.Y},{z}) is not walkable (kind={tile.Kind})");
                return null;
            }

            var maxHP = 100; // TODO: Calculate from creature stats
            Guid guid;

            lock (_instanceLock)
            {
                guid = CreateNextInstanceGuidLocked();
                var instance = new CreatureInstance(guid, creatureId, factionId, worldPos, z, maxHP, currentTick);
                _instances[guid] = instance;
                EntityKeyIndexAdd(guid);
            }

            // TODO: Write to Chunk L6 layer via Diff-Log (currently just tracking in manager)
            Emit($"[CreatureManager] SUCCESS: Spawned '{def.Name}' (id={creatureId}, guid={guid}) at ({worldPos.X},{worldPos.Y},{z})");

            return guid;
        }
        catch (Exception ex)
        {
            Emit($"[CreatureManager] EXCEPTION: Failed to spawn '{creatureId}' at ({worldPos.X},{worldPos.Y},{z})");
            Emit($"[CreatureManager] Exception: {ex.GetType().Name}: {ex.Message}");
            Emit($"[CreatureManager] StackTrace: {ex.StackTrace}");
            return null;
        }
    }
}
