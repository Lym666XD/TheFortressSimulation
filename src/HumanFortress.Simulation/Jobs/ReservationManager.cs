using HumanFortress.Contracts.Simulation.Save;

namespace HumanFortress.Simulation.Jobs;

/// <summary>
/// Owns resource reservations. Mutations require opaque generation tokens so a
/// stale job/finalizer cannot renew, release, consume, or transfer a successor's claim.
/// </summary>
internal sealed partial class ReservationManager
{
    internal readonly struct ItemToken
    {
        internal ItemToken(
            Guid resourceId,
            Guid holderId,
            string systemId,
            string jobId,
            ulong generation)
        {
            ResourceId = resourceId;
            HolderId = holderId;
            SystemId = systemId;
            JobId = jobId;
            Generation = generation;
        }

        internal Guid ResourceId { get; }
        internal Guid HolderId { get; }
        internal string SystemId { get; }
        internal string JobId { get; }
        internal ulong Generation { get; }
        internal bool IsValid => ResourceId != Guid.Empty
            && HolderId != Guid.Empty
            && !string.IsNullOrWhiteSpace(SystemId)
            && !string.IsNullOrWhiteSpace(JobId)
            && Generation != 0;

        internal bool Matches(ItemToken other) =>
            ResourceId == other.ResourceId
            && HolderId == other.HolderId
            && string.Equals(SystemId, other.SystemId, StringComparison.Ordinal)
            && string.Equals(JobId, other.JobId, StringComparison.Ordinal)
            && Generation == other.Generation;

    }

    internal readonly struct CreatureToken
    {
        internal CreatureToken(
            Guid resourceId,
            string holderSystem,
            string jobId,
            ulong generation)
        {
            ResourceId = resourceId;
            HolderSystem = holderSystem;
            JobId = jobId;
            Generation = generation;
        }

        internal Guid ResourceId { get; }
        internal string HolderSystem { get; }
        internal string JobId { get; }
        internal ulong Generation { get; }
        internal bool IsValid => ResourceId != Guid.Empty
            && !string.IsNullOrWhiteSpace(HolderSystem)
            && !string.IsNullOrWhiteSpace(JobId)
            && Generation != 0;

        internal bool Matches(CreatureToken other) =>
            ResourceId == other.ResourceId
            && string.Equals(HolderSystem, other.HolderSystem, StringComparison.Ordinal)
            && string.Equals(JobId, other.JobId, StringComparison.Ordinal)
            && Generation == other.Generation;

    }

    internal sealed class ItemReservation
    {
        internal required ItemToken Token { get; init; }
        internal ulong ExpireTick { get; set; }
        internal bool IsStagedTransfer { get; init; }
        internal Guid TransferSourceId { get; init; }
        internal ulong TransferSourceGeneration { get; init; }
    }

    internal sealed class CreatureReservation
    {
        internal required CreatureToken Token { get; init; }
        internal ulong ExpireTick { get; set; }
    }

    private readonly Dictionary<Guid, ItemReservation> _itemReservations = new();
    private readonly Dictionary<Guid, CreatureReservation> _creatureReservations = new();
    private readonly object _sync = new();
    private ulong _nextGeneration;

    internal bool TryAcquireItem(
        Guid itemId,
        Guid holderId,
        string systemId,
        string jobId,
        ulong currentTick,
        ulong expireTick,
        out ItemToken token)
    {
        token = default;
        if (!IsValidItemOwner(itemId, holderId, systemId, jobId)
            || expireTick < currentTick)
        {
            return false;
        }

        lock (_sync)
        {
            if (_itemReservations.TryGetValue(itemId, out var existing)
                && currentTick <= existing.ExpireTick)
            {
                return false;
            }

            token = new ItemToken(itemId, holderId, systemId, jobId, NextGenerationLocked());
            _itemReservations[itemId] = new ItemReservation
            {
                Token = token,
                ExpireTick = expireTick
            };
            return true;
        }
    }

    internal bool TryRenewItem(ItemToken token, ulong currentTick, ulong expireTick)
    {
        if (!token.IsValid || expireTick < currentTick)
            return false;

        lock (_sync)
        {
            if (!_itemReservations.TryGetValue(token.ResourceId, out var existing)
                || existing.IsStagedTransfer
                || currentTick > existing.ExpireTick
                || !existing.Token.Matches(token))
            {
                return false;
            }

            existing.ExpireTick = expireTick;
            return true;
        }
    }

    internal bool TryReleaseItem(ItemToken token) => TryRemoveItem(token, requireStaged: false);

    internal bool TryConsumeItem(ItemToken token) => TryRemoveItem(token, requireStaged: false);

    internal bool TryStageItemTransfer(
        ItemToken sourceToken,
        Guid destinationItemId,
        ulong currentTick,
        ulong expireTick,
        out ItemToken stagedToken)
    {
        stagedToken = default;
        if (!sourceToken.IsValid
            || destinationItemId == Guid.Empty
            || destinationItemId == sourceToken.ResourceId
            || expireTick < currentTick)
        {
            return false;
        }

        lock (_sync)
        {
            if (!_itemReservations.TryGetValue(sourceToken.ResourceId, out var source)
                || source.IsStagedTransfer
                || currentTick > source.ExpireTick
                || !source.Token.Matches(sourceToken))
            {
                return false;
            }

            if (_itemReservations.TryGetValue(destinationItemId, out var destination)
                && currentTick <= destination.ExpireTick)
            {
                return false;
            }

            stagedToken = new ItemToken(
                destinationItemId,
                sourceToken.HolderId,
                sourceToken.SystemId,
                sourceToken.JobId,
                NextGenerationLocked());
            _itemReservations[destinationItemId] = new ItemReservation
            {
                Token = stagedToken,
                ExpireTick = expireTick,
                IsStagedTransfer = true,
                TransferSourceId = sourceToken.ResourceId,
                TransferSourceGeneration = sourceToken.Generation
            };
            return true;
        }
    }

    internal bool ValidateStagedItemTransfer(
        ItemToken sourceToken,
        ItemToken stagedToken,
        ulong currentTick)
    {
        lock (_sync)
        {
            return MatchesStagedTransferLocked(sourceToken, stagedToken, currentTick);
        }
    }

    internal bool TryCommitStagedItemTransfer(
        ItemToken sourceToken,
        ItemToken stagedToken,
        ulong currentTick,
        ulong expireTick)
    {
        if (expireTick < currentTick)
            return false;

        lock (_sync)
        {
            if (!MatchesStagedTransferLocked(sourceToken, stagedToken, currentTick))
                return false;

            _itemReservations.Remove(sourceToken.ResourceId);
            _itemReservations[stagedToken.ResourceId] = new ItemReservation
            {
                Token = stagedToken,
                ExpireTick = expireTick
            };
            return true;
        }
    }

    internal bool TryCancelStagedItemTransfer(ItemToken stagedToken)
    {
        return TryRemoveItem(stagedToken, requireStaged: true);
    }

    internal bool TryAcquireCreature(
        Guid workerId,
        string systemId,
        string jobId,
        ulong currentTick,
        ulong expireTick,
        out CreatureToken token)
    {
        token = default;
        if (workerId == Guid.Empty
            || string.IsNullOrWhiteSpace(systemId)
            || string.IsNullOrWhiteSpace(jobId)
            || expireTick < currentTick)
        {
            return false;
        }

        lock (_sync)
        {
            if (_creatureReservations.TryGetValue(workerId, out var existing)
                && currentTick <= existing.ExpireTick)
            {
                return false;
            }

            token = new CreatureToken(workerId, systemId, jobId, NextGenerationLocked());
            _creatureReservations[workerId] = new CreatureReservation
            {
                Token = token,
                ExpireTick = expireTick
            };
            return true;
        }
    }

    internal bool TryRenewCreature(CreatureToken token, ulong currentTick, ulong expireTick)
    {
        if (!token.IsValid || expireTick < currentTick)
            return false;

        lock (_sync)
        {
            if (!_creatureReservations.TryGetValue(token.ResourceId, out var existing)
                || currentTick > existing.ExpireTick
                || !existing.Token.Matches(token))
            {
                return false;
            }

            existing.ExpireTick = expireTick;
            return true;
        }
    }

    internal bool TryReleaseCreature(CreatureToken token)
    {
        if (!token.IsValid)
            return false;

        lock (_sync)
        {
            if (!_creatureReservations.TryGetValue(token.ResourceId, out var existing)
                || !existing.Token.Matches(token))
            {
                return false;
            }

            _creatureReservations.Remove(token.ResourceId);
            return true;
        }
    }

    internal bool IsItemReserved(Guid itemId, ulong currentTick)
    {
        lock (_sync)
        {
            return _itemReservations.TryGetValue(itemId, out var reservation)
                && currentTick <= reservation.ExpireTick;
        }
    }

    internal bool IsCreatureReserved(
        Guid workerId,
        ulong currentTick,
        out string? holderSystem,
        out string? jobId)
    {
        lock (_sync)
        {
            if (_creatureReservations.TryGetValue(workerId, out var reservation)
                && currentTick <= reservation.ExpireTick)
            {
                holderSystem = reservation.Token.HolderSystem;
                jobId = reservation.Token.JobId;
                return true;
            }
        }

        holderSystem = null;
        jobId = null;
        return false;
    }

    internal IReadOnlyCollection<(
        Guid itemId,
        Guid holderId,
        string systemId,
        string jobId,
        ulong generation,
        ulong expireTick,
        bool stagedTransfer,
        Guid transferSourceId,
        ulong transferSourceGeneration)> GetItemReservationsSnapshot()
    {
        lock (_sync)
        {
            return _itemReservations
                .OrderBy(static entry => entry.Key)
                .Select(static entry => (
                    entry.Key,
                    entry.Value.Token.HolderId,
                    entry.Value.Token.SystemId,
                    entry.Value.Token.JobId,
                    entry.Value.Token.Generation,
                    entry.Value.ExpireTick,
                    entry.Value.IsStagedTransfer,
                    entry.Value.TransferSourceId,
                    entry.Value.TransferSourceGeneration))
                .ToArray();
        }
    }

    internal IReadOnlyCollection<(
        Guid workerId,
        string holderSystem,
        string jobId,
        ulong generation,
        ulong expireTick)> GetCreatureReservationsSnapshot()
    {
        lock (_sync)
        {
            return _creatureReservations
                .OrderBy(static entry => entry.Key)
                .Select(static entry => (
                    entry.Key,
                    entry.Value.Token.HolderSystem,
                    entry.Value.Token.JobId,
                    entry.Value.Token.Generation,
                    entry.Value.ExpireTick))
                .ToArray();
        }
    }

    internal ulong GetGenerationHighWatermark()
    {
        lock (_sync)
        {
            return _nextGeneration;
        }
    }

    internal IReadOnlyList<string> RestoreSnapshot(
        IReadOnlyList<WorldSaveItemReservationPayloadData>? itemReservations,
        IReadOnlyList<WorldSaveCreatureReservationPayloadData>? creatureReservations)
    {
        var issues = ValidateRestorePayload(itemReservations, creatureReservations);
        if (issues.Count > 0)
            return issues;

        if (itemReservations!.Count != 0 || creatureReservations!.Count != 0)
        {
            return new[]
            {
                "Non-empty reservation restore is unsupported because the deferred payload does not carry ownership generations."
            };
        }

        lock (_sync)
        {
            _itemReservations.Clear();
            _creatureReservations.Clear();
        }

        return Array.Empty<string>();
    }

    private bool TryRemoveItem(ItemToken token, bool requireStaged)
    {
        if (!token.IsValid)
            return false;

        lock (_sync)
        {
            if (!_itemReservations.TryGetValue(token.ResourceId, out var existing)
                || !existing.Token.Matches(token)
                || existing.IsStagedTransfer != requireStaged)
            {
                return false;
            }

            _itemReservations.Remove(token.ResourceId);
            return true;
        }
    }

    private bool MatchesStagedTransferLocked(
        ItemToken sourceToken,
        ItemToken stagedToken,
        ulong currentTick)
    {
        return sourceToken.IsValid
            && stagedToken.IsValid
            && _itemReservations.TryGetValue(sourceToken.ResourceId, out var source)
            && !source.IsStagedTransfer
            && source.Token.Matches(sourceToken)
            && currentTick <= source.ExpireTick
            && _itemReservations.TryGetValue(stagedToken.ResourceId, out var staged)
            && staged.IsStagedTransfer
            && staged.Token.Matches(stagedToken)
            && currentTick <= staged.ExpireTick
            && staged.TransferSourceId == sourceToken.ResourceId
            && staged.TransferSourceGeneration == sourceToken.Generation;
    }

    private ulong NextGenerationLocked()
    {
        if (_nextGeneration == ulong.MaxValue)
            throw new InvalidOperationException("Reservation generation space is exhausted.");
        return ++_nextGeneration;
    }

    private static bool IsValidItemOwner(
        Guid itemId,
        Guid holderId,
        string systemId,
        string jobId)
    {
        return itemId != Guid.Empty
            && holderId != Guid.Empty
            && !string.IsNullOrWhiteSpace(systemId)
            && !string.IsNullOrWhiteSpace(jobId);
    }

    private static List<string> ValidateRestorePayload(
        IReadOnlyList<WorldSaveItemReservationPayloadData>? itemReservations,
        IReadOnlyList<WorldSaveCreatureReservationPayloadData>? creatureReservations)
    {
        var issues = new List<string>();
        if (itemReservations == null)
            issues.Add("World item reservation payload is missing.");
        if (creatureReservations == null)
            issues.Add("World creature reservation payload is missing.");
        if (issues.Count > 0)
            return issues;

        var itemIds = new HashSet<Guid>();
        for (var i = 0; i < itemReservations!.Count; i++)
        {
            var reservation = itemReservations[i];
            if (reservation.ItemId == Guid.Empty)
                issues.Add($"World item reservation payload[{i}] has an empty item id.");
            else if (!itemIds.Add(reservation.ItemId))
                issues.Add($"World item reservation payload[{i}] duplicates item id {reservation.ItemId}.");
            if (reservation.HolderId == Guid.Empty)
                issues.Add($"World item reservation payload[{i}] has an empty holder id.");
        }

        var workerIds = new HashSet<Guid>();
        for (var i = 0; i < creatureReservations!.Count; i++)
        {
            var reservation = creatureReservations[i];
            if (reservation.WorkerId == Guid.Empty)
                issues.Add($"World creature reservation payload[{i}] has an empty worker id.");
            else if (!workerIds.Add(reservation.WorkerId))
                issues.Add($"World creature reservation payload[{i}] duplicates worker id {reservation.WorkerId}.");
            if (string.IsNullOrWhiteSpace(reservation.HolderSystem))
                issues.Add($"World creature reservation payload[{i}] has a blank holder system.");
        }

        return issues;
    }
}
