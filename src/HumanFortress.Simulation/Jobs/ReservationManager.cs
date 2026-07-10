using System;
using System.Collections.Generic;
using System.Linq;
using HumanFortress.Contracts.Simulation.Save;

namespace HumanFortress.Simulation.Jobs;

/// <summary>
/// Central reservation manager for items/destinations.
/// v1.1.1 minimal: item-level reservations with TTL and holder.
/// Reservations are simulation-owned write-phase state.
/// </summary>
internal sealed class ReservationManager
{
    private sealed class ItemReservation
    {
        internal Guid ItemId { get; init; }
        internal Guid HolderId { get; set; }
        internal ulong ExpireTick { get; set; }
    }

    private readonly Dictionary<Guid, ItemReservation> _itemRes = new();

    // Creature (worker) reservations to ensure cross-system exclusivity
    private sealed class CreatureReservation
    {
        internal Guid WorkerId { get; init; }
        internal string HolderSystem { get; set; } = string.Empty;
        internal string? JobId { get; set; }
        internal ulong ExpireTick { get; set; }
    }

    private readonly Dictionary<Guid, CreatureReservation> _creatureRes = new();

    /// <summary>
    /// Try reserve an item for a holder until expireTick.
    /// </summary>
    internal bool TryReserveItem(Guid itemId, Guid holderId, ulong currentTick, ulong expireTick)
    {
        if (_itemRes.TryGetValue(itemId, out var existing)
            && currentTick <= existing.ExpireTick
            && existing.HolderId != holderId)
        {
            return false;
        }

        _itemRes[itemId] = new ItemReservation
        {
            ItemId = itemId,
            HolderId = holderId,
            ExpireTick = expireTick
        };
        return true;
    }

    /// <summary>
    /// Release an item's reservation.
    /// </summary>
    internal void ReleaseItem(Guid itemId)
    {
        _itemRes.Remove(itemId);
    }

    /// <summary>
    /// Check if item is reserved at tick.
    /// </summary>
    internal bool IsItemReserved(Guid itemId, ulong currentTick)
    {
        if (_itemRes.TryGetValue(itemId, out var res))
        {
            if (currentTick <= res.ExpireTick) return true;
        }
        return false;
    }

    /// <summary>
    /// Snapshot for UI/debugging.
    /// </summary>
    internal IReadOnlyCollection<(Guid itemId, Guid holderId, ulong expireTick)> GetItemReservationsSnapshot()
    {
        var list = new List<(Guid, Guid, ulong)>(_itemRes.Count);
        foreach (var kv in _itemRes.OrderBy(static kv => kv.Key))
        {
            list.Add((kv.Key, kv.Value.HolderId, kv.Value.ExpireTick));
        }
        return list;
    }

    // ===== Creature reservation API =====

    internal bool TryReserveCreature(Guid workerId, string systemId, ulong currentTick, ulong expireTick, string? jobId = null)
    {
        if (_creatureRes.TryGetValue(workerId, out var existing)
            && currentTick <= existing.ExpireTick
            && existing.HolderSystem != systemId)
        {
            return false;
        }

        _creatureRes[workerId] = new CreatureReservation
        {
            WorkerId = workerId,
            HolderSystem = systemId,
            JobId = jobId ?? existing?.JobId,
            ExpireTick = expireTick
        };
        return true;
    }

    internal void ReleaseCreature(Guid workerId)
    {
        _creatureRes.Remove(workerId);
    }

    internal bool IsCreatureReserved(Guid workerId, ulong currentTick, out string? holderSystem, out string? jobId)
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
        }
        return false;
    }

    internal IReadOnlyCollection<(Guid workerId, string holderSystem, string? jobId, ulong expireTick)> GetCreatureReservationsSnapshot()
    {
        var list = new List<(Guid, string, string?, ulong)>(_creatureRes.Count);
        foreach (var kv in _creatureRes.OrderBy(static kv => kv.Key))
        {
            list.Add((kv.Key, kv.Value.HolderSystem, kv.Value.JobId, kv.Value.ExpireTick));
        }
        return list;
    }

    internal IReadOnlyList<string> RestoreSnapshot(
        IReadOnlyList<WorldSaveItemReservationPayloadData>? itemReservations,
        IReadOnlyList<WorldSaveCreatureReservationPayloadData>? creatureReservations)
    {
        var issues = new List<string>();
        if (itemReservations == null)
        {
            issues.Add("World item reservation payload is missing.");
        }

        if (creatureReservations == null)
        {
            issues.Add("World creature reservation payload is missing.");
        }

        if (issues.Count > 0)
            return issues;

        ValidateItemReservations(itemReservations!, issues);
        ValidateCreatureReservations(creatureReservations!, issues);
        if (issues.Count > 0)
            return issues;

        _itemRes.Clear();
        foreach (var reservation in itemReservations!)
        {
            _itemRes[reservation.ItemId] = new ItemReservation
            {
                ItemId = reservation.ItemId,
                HolderId = reservation.HolderId,
                ExpireTick = reservation.ExpireTick
            };
        }

        _creatureRes.Clear();
        foreach (var reservation in creatureReservations!)
        {
            _creatureRes[reservation.WorkerId] = new CreatureReservation
            {
                WorkerId = reservation.WorkerId,
                HolderSystem = reservation.HolderSystem,
                JobId = reservation.JobId,
                ExpireTick = reservation.ExpireTick
            };
        }

        return Array.Empty<string>();
    }

    private static void ValidateItemReservations(
        IReadOnlyList<WorldSaveItemReservationPayloadData> reservations,
        ICollection<string> issues)
    {
        var seen = new HashSet<Guid>();
        for (var i = 0; i < reservations.Count; i++)
        {
            var reservation = reservations[i];
            if (reservation.ItemId == Guid.Empty)
            {
                issues.Add($"World item reservation payload[{i}] has an empty item id.");
            }
            else if (!seen.Add(reservation.ItemId))
            {
                issues.Add($"World item reservation payload[{i}] duplicates item id {reservation.ItemId}.");
            }

            if (reservation.HolderId == Guid.Empty)
            {
                issues.Add($"World item reservation payload[{i}] has an empty holder id.");
            }
        }
    }

    private static void ValidateCreatureReservations(
        IReadOnlyList<WorldSaveCreatureReservationPayloadData> reservations,
        ICollection<string> issues)
    {
        var seen = new HashSet<Guid>();
        for (var i = 0; i < reservations.Count; i++)
        {
            var reservation = reservations[i];
            if (reservation.WorkerId == Guid.Empty)
            {
                issues.Add($"World creature reservation payload[{i}] has an empty worker id.");
            }
            else if (!seen.Add(reservation.WorkerId))
            {
                issues.Add($"World creature reservation payload[{i}] duplicates worker id {reservation.WorkerId}.");
            }

            if (string.IsNullOrWhiteSpace(reservation.HolderSystem))
            {
                issues.Add($"World creature reservation payload[{i}] has a blank holder system.");
            }
        }
    }
}
