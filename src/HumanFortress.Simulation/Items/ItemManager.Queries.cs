using HumanFortress.Core.Simulation;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Items;

internal sealed partial class ItemManager
{
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
            for (var y = worldRect.Y; y <= worldRect.MaxExtentY; y++)
            {
                for (var x = worldRect.X; x <= worldRect.MaxExtentX; x++)
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

    private static uint ToEntityId(Guid guid)
    {
        return DiffTargetEncoding.EntityId(guid);
    }
}
