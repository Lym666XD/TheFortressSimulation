using HumanFortress.Core.Simulation;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Items;

internal sealed partial class ItemManager
{
    /// <summary>
    /// Get instance by GUID
    /// </summary>
    internal ItemInstance? GetInstance(Guid guid)
    {
        lock (_instanceLock)
        {
            return _instances.GetValueOrDefault(guid);
        }
    }

    /// <summary>
    /// Find an item by the compact entity id used in DiffTarget.
    /// </summary>
    internal ItemInstance? GetInstanceByEntityId(uint entityId)
    {
        lock (_instanceLock)
        {
            return _legacyEntityIdIndex.TryGetValue(entityId, out var ids)
                && ids.Count > 0
                && _instances.TryGetValue(ids[0], out var instance)
                ? instance
                : null;
        }
    }

    /// <summary>
    /// Find an item by the wider stable entity key used by entity-scoped DiffTarget operations.
    /// </summary>
    internal ItemInstance? GetInstanceByEntityKey(ulong entityKey)
    {
        lock (_instanceLock)
        {
            return _identityIndex.TryResolve(entityKey, out var guid)
                ? _instances.GetValueOrDefault(guid)
                : null;
        }
    }

    /// <summary>
    /// Get all instances (creates a snapshot for thread safety)
    /// </summary>
    internal IEnumerable<ItemInstance> GetAllInstances()
    {
        lock (_instanceLock)
        {
            return _instances
                .OrderBy(static entry => entry.Key)
                .Select(static entry => entry.Value)
                .ToList();
        }
    }

    /// <summary>
    /// Get all item instances that are physically on the ground.
    /// </summary>
    internal IEnumerable<ItemInstance> GetGroundInstances()
    {
        lock (_instanceLock)
        {
            return OrderItemsSpatially(_instances
                .Select(static entry => entry.Value)
                .Where(inst => inst.IsOnGround)
            ).ToList();
        }
    }

    /// <summary>
    /// Get all ground item instances on a Z layer.
    /// </summary>
    internal IEnumerable<ItemInstance> GetGroundInstancesAtZ(int z)
    {
        lock (_instanceLock)
        {
            return OrderItemsSpatially(_instances
                .Select(static entry => entry.Value)
                .Where(inst => inst.IsOnGround && inst.Z == z)
            ).ToList();
        }
    }

    /// <summary>
    /// Get snapshot of items at a given tile (on ground by default).
    /// </summary>
    internal IEnumerable<ItemInstance> GetItemsAt(Point worldPos, int z, bool groundOnly = true)
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

            return list
                .OrderBy(static item => item.Guid)
                .ToList();
        }
    }

    /// <summary>
    /// Get snapshot of ground items at a given tile.
    /// </summary>
    internal IEnumerable<ItemInstance> GetGroundItemsAt(Point worldPos, int z)
    {
        return GetItemsAt(worldPos, z, groundOnly: true);
    }

    /// <summary>
    /// Get snapshot of ground items inside a world rectangle on one Z layer.
    /// </summary>
    internal IEnumerable<ItemInstance> GetGroundItemsIn(Rectangle worldRect, int z)
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

            return OrderItemsSpatially(list).ToList();
        }
    }

    private static IOrderedEnumerable<ItemInstance> OrderItemsSpatially(IEnumerable<ItemInstance> items)
    {
        return items
            .OrderBy(static item => item.Z)
            .ThenBy(static item => item.Position.Y)
            .ThenBy(static item => item.Position.X)
            .ThenBy(static item => item.Guid);
    }

    private static uint ToEntityId(Guid guid)
    {
        return DiffTargetEncoding.EntityId(guid);
    }

    private static ulong ToEntityKey(Guid guid)
    {
        return DiffTargetEncoding.EntityKey(guid);
    }
}
