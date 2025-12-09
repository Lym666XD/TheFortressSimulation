using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace HumanFortress.Simulation.Jobs;

/// <summary>
/// Central reservation manager for items/destinations.
/// v1.1.1 minimal: item-level reservations with TTL and holder.
/// Read-safe; writes happen in Write phase only.
/// </summary>
public sealed class ReservationManager
{
    private sealed class ItemReservation
    {
        public Guid ItemId { get; init; }
        public Guid HolderId { get; set; }
        public ulong ExpireTick { get; set; }
    }

    private readonly ConcurrentDictionary<Guid, ItemReservation> _itemRes = new();

    // Creature (worker) reservations to ensure cross-system exclusivity
    private sealed class CreatureReservation
    {
        public Guid WorkerId { get; init; }
        public string HolderSystem { get; set; } = string.Empty;
        public string? JobId { get; set; }
        public ulong ExpireTick { get; set; }
    }

    private readonly ConcurrentDictionary<Guid, CreatureReservation> _creatureRes = new();

    /// <summary>
    /// Try reserve an item for a holder until expireTick.
    /// </summary>
    public bool TryReserveItem(Guid itemId, Guid holderId, ulong expireTick)
    {
        return _itemRes.AddOrUpdate(itemId,
            addValueFactory: id => new ItemReservation { ItemId = id, HolderId = holderId, ExpireTick = expireTick },
            updateValueFactory: (id, existing) =>
            {
                // If expired or same holder, refresh; otherwise fail by keeping existing
                if (existing.ExpireTick <= expireTick || existing.HolderId == holderId)
                {
                    existing.HolderId = holderId;
                    existing.ExpireTick = expireTick;
                }
                return existing;
            })
            .HolderId == holderId; // true if our holder holds after update
    }

    /// <summary>
    /// Release an item's reservation.
    /// </summary>
    public void ReleaseItem(Guid itemId)
    {
        _itemRes.TryRemove(itemId, out _);
    }

    /// <summary>
    /// Check if item is reserved at tick.
    /// </summary>
    public bool IsItemReserved(Guid itemId, ulong currentTick)
    {
        if (_itemRes.TryGetValue(itemId, out var res))
        {
            if (currentTick <= res.ExpireTick) return true;
            // Expired → cleanup
            _itemRes.TryRemove(itemId, out _);
        }
        return false;
    }

    /// <summary>
    /// Snapshot for UI/debugging.
    /// </summary>
    public IReadOnlyCollection<(Guid itemId, Guid holderId, ulong expireTick)> GetItemReservationsSnapshot()
    {
        var list = new List<(Guid, Guid, ulong)>(_itemRes.Count);
        foreach (var kv in _itemRes)
        {
            list.Add((kv.Key, kv.Value.HolderId, kv.Value.ExpireTick));
        }
        return list;
    }

    // ===== Creature reservation API =====

    public bool TryReserveCreature(Guid workerId, string systemId, ulong expireTick, string? jobId = null)
    {
        var res = _creatureRes.AddOrUpdate(workerId,
            addValueFactory: id => new CreatureReservation { WorkerId = id, HolderSystem = systemId, JobId = jobId, ExpireTick = expireTick },
            updateValueFactory: (id, existing) =>
            {
                // If expired or same holder, refresh; otherwise keep existing holder
                if (existing.ExpireTick <= expireTick || existing.HolderSystem == systemId)
                {
                    existing.HolderSystem = systemId;
                    existing.JobId = jobId ?? existing.JobId;
                    existing.ExpireTick = expireTick;
                }
                return existing;
            });
        return res.HolderSystem == systemId;
    }

    public void ReleaseCreature(Guid workerId)
    {
        _creatureRes.TryRemove(workerId, out _);
    }

    public bool IsCreatureReserved(Guid workerId, ulong currentTick, out string? holderSystem, out string? jobId)
    {
        holderSystem = null; jobId = null;
        if (_creatureRes.TryGetValue(workerId, out var res))
        {
            if (currentTick <= res.ExpireTick)
            {
                holderSystem = res.HolderSystem;
                jobId = res.JobId;
                return true;
            }
            _creatureRes.TryRemove(workerId, out _);
        }
        return false;
    }

    public IReadOnlyCollection<(Guid workerId, string holderSystem, string? jobId, ulong expireTick)> GetCreatureReservationsSnapshot()
    {
        var list = new List<(Guid, string, string?, ulong)>(_creatureRes.Count);
        foreach (var kv in _creatureRes)
        {
            list.Add((kv.Key, kv.Value.HolderSystem, kv.Value.JobId, kv.Value.ExpireTick));
        }
        return list;
    }
}

