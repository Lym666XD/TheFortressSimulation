using SadRogue.Primitives;

namespace HumanFortress.Simulation.Items;

internal sealed partial class ItemManager
{
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
            foreach (var gid in ids.OrderBy(static id => id))
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

            var removed = 0;
            foreach (var kv in byDef.OrderBy(static entry => entry.Key, StringComparer.Ordinal))
            {
                var list = kv.Value;
                if (list.Count <= 1) continue;
                list.Sort();
                var targetId = list[0];
                if (!_instances.TryGetValue(targetId, out var target)) continue;
                var sum = target.StackCount;
                for (var i = 1; i < list.Count; i++)
                {
                    var gid = list[i];
                    if (_instances.TryGetValue(gid, out var other))
                    {
                        sum += other.StackCount;
                        EntityKeyIndexRemove(gid);
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

            var newGuid = CreateNextInstanceGuidLocked();
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
        EntityKeyIndexAdd(newGuid);
        IndexAdd(newGuid, clone.Position, clone.Z);
        string msg = $"[ItemManager] SPLIT: {sourceId} -> new={newGuid} take={takeCount} remain={inst.StackCount} at ({clone.Position.X},{clone.Position.Y},{clone.Z})";
        Emit(msg);
        return newGuid;
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
            EntityKeyIndexRemove(guid);
            _instances.Remove(guid);
            string msg = $"[ItemManager] REMOVE: Removed item guid={guid} id={inst.DefinitionId} at ({inst.Position.X},{inst.Position.Y},{inst.Z})";
            Emit(msg);
            return true;
        }
    }
}
