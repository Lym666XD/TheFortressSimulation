using System;
using System.Collections.Generic;
using System.Linq;
using HumanFortress.Core.Random;
using HumanFortress.Core.Simulation;
using HumanFortress.Contracts.Simulation.Items;
using HumanFortress.Simulation.Diagnostics;
using SadRogue.Primitives;
using HumanFortress.Simulation.World;
using HumanFortress.Simulation.Tiles;

namespace HumanFortress.Simulation.Items;

/// <summary>
/// Global manager for item definitions and runtime instances.
/// Thread-safe for concurrent reads; writes use locks.
/// Follows data-driven principles per ITEMS_SPEC.md and UPDATE_ORDER.md
/// </summary>
internal sealed class ItemManager : IItemDefinitionCatalog
{
    private const ulong ItemInstanceGuidScope = 0x4954454D53544143UL;

    // Static definition catalog (loaded at startup, swapped as an immutable snapshot on reload)
    private ItemDefinitionCatalogStore _definitionCatalog = ItemDefinitionCatalogStore.Empty;

    // Runtime instances (modified during gameplay)
    private readonly Dictionary<Guid, ItemInstance> _instances = new();
    private readonly object _instanceLock = new();
    private ulong _nextInstanceSequence;
    // Position index for fast per-tile queries
    private readonly Dictionary<(int X,int Y,int Z), List<Guid>> _posIndex = new();

    // Dependencies
    private HumanFortress.Simulation.World.World? _world;

    /// <summary>
    /// Optional logging callback (set by App layer to write to fortress_debug.log)
    /// </summary>
    public static Action<string>? LogCallback { get; set; }

    public int DefinitionCount => _definitionCatalog.DefinitionCount;
    public int InstanceCount
    {
        get
        {
            lock (_instanceLock)
            {
                return _instances.Count;
            }
        }
    }

    private static (int,int,int) KeyFor(Point pos, int z) => (pos.X, pos.Y, z);
    private void IndexAdd(Guid id, Point pos, int z)
    {
        var key = KeyFor(pos, z);
        if (!_posIndex.TryGetValue(key, out var list)) { list = new List<Guid>(); _posIndex[key] = list; }
        list.Add(id);
    }
    private void IndexRemove(Guid id, Point pos, int z)
    {
        var key = KeyFor(pos, z);
        if (_posIndex.TryGetValue(key, out var list))
        {
            for (int i = list.Count - 1; i >= 0; i--) if (list[i] == id) list.RemoveAt(i);
            if (list.Count == 0) _posIndex.Remove(key);
        }
    }

    /// <summary>
    /// Update item position and maintain position index.
    /// </summary>
    public void UpdateItemPosition(Guid id, Point oldPos, int oldZ, Point newPos, int newZ)
    {
        lock (_instanceLock)
        {
            if (_instances.TryGetValue(id, out var inst))
            {
                IndexRemove(id, oldPos, oldZ);
                inst.Position = newPos;
                inst.Z = newZ;
                IndexAdd(id, newPos, newZ);
            }
        }
    }

    /// <summary>
    /// Merge stacks at a given world position (post-move consolidation).
    /// Current policy: same DefinitionId at same (x,y,z) merge by increasing first instance's StackCount,
    /// deleting the redundant instances. Returns number of instances removed.
    /// </summary>
    public int MergeStacksAt(Point worldPos, int z)
    {
        lock (_instanceLock)
        {
            var key = KeyFor(worldPos, z);
            if (!_posIndex.TryGetValue(key, out var ids) || ids.Count <= 1)
                return 0;

            var byDef = new Dictionary<string, List<Guid>>();
            foreach (var gid in ids)
            {
                if (!_instances.TryGetValue(gid, out var inst)) continue;
                if (!inst.IsOnGround) continue;
                if (!byDef.TryGetValue(inst.DefinitionId, out var list))
                {
                    list = new List<Guid>();
                    byDef[inst.DefinitionId] = list;
                }
                list.Add(gid);
            }

            int removed = 0;
            foreach (var kv in byDef)
            {
                var list = kv.Value;
                if (list.Count <= 1) continue;
                var targetId = list[0];
                if (!_instances.TryGetValue(targetId, out var target)) continue;
                int sum = target.StackCount;
                for (int i = 1; i < list.Count; i++)
                {
                    var gid = list[i];
                    if (_instances.TryGetValue(gid, out var other))
                    {
                        sum += other.StackCount;
                        _instances.Remove(gid);
                        removed++;
                    }
                }
                target.StackCount = sum;
                _posIndex[key] = new List<Guid> { targetId };
                string msg = $"[ItemManager] MERGE: Consolidated {list.Count} stacks of '{target.DefinitionId}' at ({worldPos.X},{worldPos.Y},{z}) -> qty={target.StackCount}";
                Emit(msg);
            }
            return removed;
        }
    }

    /// <summary>
    /// Split a stack into a new instance with takeCount units.
    /// Reduces the original stack by takeCount and spawns a new item at the same position/Z.
    /// Returns the new item's Guid, or null if split cannot be performed.
    /// </summary>
    public Guid? SplitStack(Guid sourceId, int takeCount)
    {
        if (takeCount <= 0) return null;
        lock (_instanceLock)
        {
            if (!_instances.TryGetValue(sourceId, out var inst)) return null;
            if (inst.StackCount <= takeCount) return null; // nothing to split if equal/full

            var newGuid = DeterministicGuidGenerator.GenerateFromSequence(ItemInstanceGuidScope, ++_nextInstanceSequence);
            return SplitStackLocked(sourceId, inst, takeCount, newGuid);
        }
    }

    public Guid? SplitStackWithGuid(Guid sourceId, int takeCount, Guid newGuid)
    {
        if (takeCount <= 0 || newGuid == Guid.Empty) return null;
        lock (_instanceLock)
        {
            if (_instances.ContainsKey(newGuid)) return null;
            if (!_instances.TryGetValue(sourceId, out var inst)) return null;
            if (inst.StackCount <= takeCount) return null;

            return SplitStackLocked(sourceId, inst, takeCount, newGuid);
        }
    }

    private Guid? SplitStackLocked(Guid sourceId, ItemInstance inst, int takeCount, Guid newGuid)
    {
        inst.StackCount -= takeCount;
        var clone = new ItemInstance(newGuid, inst.DefinitionId, inst.Position, inst.Z, takeCount, inst.SpawnedAtTick)
        {
            MaterialId = inst.MaterialId,
            OwnerFactionId = inst.OwnerFactionId,
            OwnerCreatureGuid = inst.OwnerCreatureGuid,
            UsePolicy = inst.UsePolicy,
            Forbidden = inst.Forbidden
        };
        _instances[newGuid] = clone;
        IndexAdd(newGuid, clone.Position, clone.Z);
        string msg = $"[ItemManager] SPLIT: {sourceId} -> new={newGuid} take={takeCount} remain={inst.StackCount} at ({clone.Position.X},{clone.Position.Y},{clone.Z})";
        Emit(msg);
        return newGuid;
    }

    /// <summary>
    /// Set dependencies (called after initialization)
    /// </summary>
    public void SetDependencies(HumanFortress.Simulation.World.World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        _world = world;
    }

    /// <summary>
    /// Replace the static item definition catalog with an already-loaded immutable snapshot.
    /// </summary>
    public void SetDefinitionCatalog(ItemDefinitionCatalogStore catalog)
    {
        _definitionCatalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

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
            int chunkX = worldPos.X / 32;
            int chunkY = worldPos.Y / 32;
            int localX = worldPos.X % 32;
            int localY = worldPos.Y % 32;

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
                var guid = DeterministicGuidGenerator.GenerateFromSequence(ItemInstanceGuidScope, ++_nextInstanceSequence);
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

    /// <summary>
    /// Get all item definitions
    /// </summary>
    public IEnumerable<ItemDefinition> GetAllDefinitions()
    {
        return _definitionCatalog.GetAllDefinitions();
    }

    /// <summary>
    /// Get item definitions by kind (resource/weapon/armor/tool/container/consumable)
    /// </summary>
    public IEnumerable<ItemDefinition> GetByKind(string kind)
    {
        return _definitionCatalog.GetByKind(kind);
    }

    /// <summary>
    /// Get available kinds (for UI category display)
    /// </summary>
    public IEnumerable<string> GetAvailableKinds()
    {
        return _definitionCatalog.GetAvailableKinds();
    }

    /// <summary>
    /// Get item definitions by tag
    /// </summary>
    public IEnumerable<ItemDefinition> GetByTag(string tag)
    {
        return _definitionCatalog.GetByTag(tag);
    }

    /// <summary>
    /// Get definition by ID
    /// </summary>
    public ItemDefinition? GetDefinition(string id)
    {
        return _definitionCatalog.GetDefinition(id);
    }

    /// <summary>
    /// Get instance by GUID
    /// </summary>
    public ItemInstance? GetInstance(Guid guid)
    {
        lock (_instanceLock)
        {
            return _instances.GetValueOrDefault(guid);
        }
    }

    /// <summary>
    /// Find an item by the compact entity id used in DiffTarget.
    /// </summary>
    public ItemInstance? GetInstanceByEntityId(uint entityId)
    {
        lock (_instanceLock)
        {
            foreach (var inst in _instances.Values)
            {
                if (ToEntityId(inst.Guid) == entityId)
                    return inst;
            }

            return null;
        }
    }

    /// <summary>
    /// Get all instances (creates a snapshot for thread safety)
    /// </summary>
    public IEnumerable<ItemInstance> GetAllInstances()
    {
        lock (_instanceLock)
        {
            return _instances.Values.ToList();
        }
    }

    /// <summary>
    /// Get all item instances that are physically on the ground.
    /// </summary>
    public IEnumerable<ItemInstance> GetGroundInstances()
    {
        lock (_instanceLock)
        {
            return _instances.Values
                .Where(inst => inst.IsOnGround)
                .ToList();
        }
    }

    /// <summary>
    /// Get all ground item instances on a Z layer.
    /// </summary>
    public IEnumerable<ItemInstance> GetGroundInstancesAtZ(int z)
    {
        lock (_instanceLock)
        {
            return _instances.Values
                .Where(inst => inst.IsOnGround && inst.Z == z)
                .ToList();
        }
    }

    /// <summary>
    /// Get snapshot of items at a given tile (on ground by default).
    /// </summary>
    public IEnumerable<ItemInstance> GetItemsAt(Point worldPos, int z, bool groundOnly = true)
    {
        lock (_instanceLock)
        {
            var key = KeyFor(worldPos, z);
            if (!_posIndex.TryGetValue(key, out var ids) || ids.Count == 0)
                return Enumerable.Empty<ItemInstance>();
            var list = new List<ItemInstance>(ids.Count);
            foreach (var gid in ids)
            {
                if (_instances.TryGetValue(gid, out var inst))
                {
                    if (!groundOnly || inst.IsOnGround)
                        list.Add(inst);
                }
            }
            return list;
        }
    }

    /// <summary>
    /// Get snapshot of ground items at a given tile.
    /// </summary>
    public IEnumerable<ItemInstance> GetGroundItemsAt(Point worldPos, int z)
    {
        return GetItemsAt(worldPos, z, groundOnly: true);
    }

    /// <summary>
    /// Get snapshot of ground items inside a world rectangle on one Z layer.
    /// </summary>
    public IEnumerable<ItemInstance> GetGroundItemsIn(Rectangle worldRect, int z)
    {
        lock (_instanceLock)
        {
            var list = new List<ItemInstance>();
            for (int y = worldRect.Y; y <= worldRect.MaxExtentY; y++)
            {
                for (int x = worldRect.X; x <= worldRect.MaxExtentX; x++)
                {
                    var key = KeyFor(new Point(x, y), z);
                    if (!_posIndex.TryGetValue(key, out var ids) || ids.Count == 0)
                        continue;

                    foreach (var gid in ids)
                    {
                        if (_instances.TryGetValue(gid, out var inst) && inst.IsOnGround)
                            list.Add(inst);
                    }
                }
            }
            return list;
        }
    }

    /// <summary>
    /// Remove an item instance by GUID, updating position index accordingly.
    /// Returns true if removed.
    /// </summary>
    public bool RemoveInstance(Guid guid)
    {
        lock (_instanceLock)
        {
            if (!_instances.TryGetValue(guid, out var inst)) return false;
            IndexRemove(guid, inst.Position, inst.Z);
            _instances.Remove(guid);
            string msg = $"[ItemManager] REMOVE: Removed item guid={guid} id={inst.DefinitionId} at ({inst.Position.X},{inst.Position.Y},{inst.Z})";
            Emit(msg);
            return true;
        }
    }

    private static void Emit(string message)
    {
        SimulationDiagnostics.Information(LogCallback, "Simulation.Items", message);
    }

    private static uint ToEntityId(Guid guid)
    {
        return DiffTargetEncoding.EntityId(guid);
    }
}
