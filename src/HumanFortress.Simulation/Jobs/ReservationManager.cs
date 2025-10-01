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
}

