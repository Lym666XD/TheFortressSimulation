using HumanFortress.Core.Simulation;

namespace HumanFortress.Simulation.Identity;

internal enum EntityIdentityClaimFailure
{
    None = 0,
    EmptyGuid,
    DuplicateGuid,
    RetiredGuid,
    EntityKeyCollision
}

internal readonly record struct LiveEntityIdentityBinding(ulong EntityKey, Guid OwnerGuid);

internal readonly record struct LiveEntityIdentityAuthoritySnapshot(
    ulong NextAllocationSequence,
    IReadOnlyList<LiveEntityIdentityBinding> HistoricalBindings,
    IReadOnlyList<Guid> RetiredGuids);

internal readonly record struct EntityIdentityClaimResult(
    EntityIdentityClaimFailure Failure,
    Guid CandidateGuid,
    ulong EntityKey,
    Guid ConflictingGuid)
{
    internal static EntityIdentityClaimResult Accepted(Guid candidateGuid)
    {
        return new EntityIdentityClaimResult(
            EntityIdentityClaimFailure.None,
            candidateGuid,
            DiffTargetEncoding.EntityKey(candidateGuid),
            Guid.Empty);
    }

    internal bool Success => Failure == EntityIdentityClaimFailure.None;

    internal string Describe(string entityKind)
    {
        return Failure switch
        {
            EntityIdentityClaimFailure.EmptyGuid => $"The {entityKind} GUID is empty.",
            EntityIdentityClaimFailure.DuplicateGuid =>
                $"The {entityKind} GUID {CandidateGuid} is already live.",
            EntityIdentityClaimFailure.RetiredGuid =>
                $"The retired {entityKind} GUID {CandidateGuid} cannot be reused in this identity generation.",
            EntityIdentityClaimFailure.EntityKeyCollision =>
                $"The {entityKind} GUID {CandidateGuid} collides with {ConflictingGuid} at entity key 0x{EntityKey:X16}.",
            _ => string.Empty
        };
    }
}

/// <summary>
/// Owns the collision-safe live mapping from compact entity keys to full GUIDs.
/// Historical key ownership and retired GUIDs are retained for the lifetime of
/// the manager so a stale compact handle can never resolve to a different entity.
/// </summary>
internal sealed class LiveEntityIdentityIndex
{
    private readonly Dictionary<ulong, Guid> _liveOwnersByKey = new();
    private readonly HashSet<Guid> _liveGuids = new();
    private readonly Dictionary<ulong, Guid> _historicalOwnersByKey = new();
    private readonly HashSet<Guid> _retiredGuids = new();

    internal EntityIdentityClaimResult ValidateNew(Guid candidateGuid)
    {
        if (candidateGuid == Guid.Empty)
        {
            return new EntityIdentityClaimResult(
                EntityIdentityClaimFailure.EmptyGuid,
                candidateGuid,
                0,
                Guid.Empty);
        }

        ulong entityKey = DiffTargetEncoding.EntityKey(candidateGuid);
        if (_liveGuids.Contains(candidateGuid))
        {
            return new EntityIdentityClaimResult(
                EntityIdentityClaimFailure.DuplicateGuid,
                candidateGuid,
                entityKey,
                candidateGuid);
        }

        if (_retiredGuids.Contains(candidateGuid))
        {
            return new EntityIdentityClaimResult(
                EntityIdentityClaimFailure.RetiredGuid,
                candidateGuid,
                entityKey,
                candidateGuid);
        }

        if (_historicalOwnersByKey.TryGetValue(entityKey, out var historicalOwner)
            && historicalOwner != candidateGuid)
        {
            return new EntityIdentityClaimResult(
                EntityIdentityClaimFailure.EntityKeyCollision,
                candidateGuid,
                entityKey,
                historicalOwner);
        }

        return EntityIdentityClaimResult.Accepted(candidateGuid);
    }

    internal EntityIdentityClaimResult TryAdd(Guid candidateGuid)
    {
        var validation = ValidateNew(candidateGuid);
        if (!validation.Success)
            return validation;

        ulong entityKey = validation.EntityKey;
        _liveOwnersByKey.Add(entityKey, candidateGuid);
        _liveGuids.Add(candidateGuid);
        _historicalOwnersByKey.TryAdd(entityKey, candidateGuid);
        return validation;
    }

    internal EntityIdentityClaimResult TryReplace(IReadOnlyCollection<Guid> candidateGuids)
    {
        ArgumentNullException.ThrowIfNull(candidateGuids);

        var incomingGuids = new HashSet<Guid>();
        var incomingOwnersByKey = new Dictionary<ulong, Guid>();
        foreach (var candidateGuid in candidateGuids)
        {
            if (candidateGuid == Guid.Empty)
            {
                return new EntityIdentityClaimResult(
                    EntityIdentityClaimFailure.EmptyGuid,
                    candidateGuid,
                    0,
                    Guid.Empty);
            }

            ulong entityKey = DiffTargetEncoding.EntityKey(candidateGuid);
            if (!incomingGuids.Add(candidateGuid))
            {
                return new EntityIdentityClaimResult(
                    EntityIdentityClaimFailure.DuplicateGuid,
                    candidateGuid,
                    entityKey,
                    candidateGuid);
            }

            if (_retiredGuids.Contains(candidateGuid))
            {
                return new EntityIdentityClaimResult(
                    EntityIdentityClaimFailure.RetiredGuid,
                    candidateGuid,
                    entityKey,
                    candidateGuid);
            }

            if (incomingOwnersByKey.TryGetValue(entityKey, out var incomingOwner)
                && incomingOwner != candidateGuid)
            {
                return new EntityIdentityClaimResult(
                    EntityIdentityClaimFailure.EntityKeyCollision,
                    candidateGuid,
                    entityKey,
                    incomingOwner);
            }

            if (_historicalOwnersByKey.TryGetValue(entityKey, out var historicalOwner)
                && historicalOwner != candidateGuid)
            {
                return new EntityIdentityClaimResult(
                    EntityIdentityClaimFailure.EntityKeyCollision,
                    candidateGuid,
                    entityKey,
                    historicalOwner);
            }

            incomingOwnersByKey.Add(entityKey, candidateGuid);
        }

        foreach (var liveGuid in _liveGuids)
        {
            if (!incomingGuids.Contains(liveGuid))
                _retiredGuids.Add(liveGuid);
        }

        _liveOwnersByKey.Clear();
        _liveGuids.Clear();
        foreach (var entry in incomingOwnersByKey.OrderBy(static entry => entry.Key))
        {
            _liveOwnersByKey.Add(entry.Key, entry.Value);
            _liveGuids.Add(entry.Value);
            _historicalOwnersByKey.TryAdd(entry.Key, entry.Value);
        }

        return EntityIdentityClaimResult.Accepted(Guid.Empty);
    }

    internal bool Remove(Guid guid)
    {
        ulong entityKey = DiffTargetEncoding.EntityKey(guid);
        if (!_liveOwnersByKey.TryGetValue(entityKey, out var liveOwner)
            || liveOwner != guid
            || !_liveGuids.Remove(guid))
        {
            return false;
        }

        _liveOwnersByKey.Remove(entityKey);
        _retiredGuids.Add(guid);
        return true;
    }

    internal bool TryResolve(ulong entityKey, out Guid guid)
    {
        return _liveOwnersByKey.TryGetValue(entityKey, out guid);
    }

    internal LiveEntityIdentityAuthoritySnapshot GetAuthoritySnapshot(ulong nextAllocationSequence)
    {
        return new LiveEntityIdentityAuthoritySnapshot(
            nextAllocationSequence,
            _historicalOwnersByKey
                .OrderBy(static entry => entry.Key)
                .ThenBy(static entry => entry.Value)
                .Select(static entry => new LiveEntityIdentityBinding(entry.Key, entry.Value))
                .ToArray(),
            _retiredGuids.OrderBy(static guid => guid).ToArray());
    }

    internal void RestoreAuthoritySnapshot(
        LiveEntityIdentityAuthoritySnapshot snapshot,
        IEnumerable<Guid> liveGuids)
    {
        ArgumentNullException.ThrowIfNull(liveGuids);

        _liveOwnersByKey.Clear();
        _liveGuids.Clear();
        _historicalOwnersByKey.Clear();
        _retiredGuids.Clear();

        foreach (var binding in snapshot.HistoricalBindings
            .OrderBy(static binding => binding.EntityKey)
            .ThenBy(static binding => binding.OwnerGuid))
        {
            _historicalOwnersByKey.Add(binding.EntityKey, binding.OwnerGuid);
        }

        foreach (var guid in snapshot.RetiredGuids.OrderBy(static guid => guid))
            _retiredGuids.Add(guid);

        foreach (var guid in liveGuids.OrderBy(static guid => guid))
        {
            ulong entityKey = DiffTargetEncoding.EntityKey(guid);
            if (!_historicalOwnersByKey.TryGetValue(entityKey, out var historicalOwner)
                || historicalOwner != guid)
            {
                throw new InvalidOperationException(
                    $"Identity memento does not own live GUID {guid} at key 0x{entityKey:X16}.");
            }

            _liveOwnersByKey.Add(entityKey, guid);
            _liveGuids.Add(guid);
            _retiredGuids.Remove(guid);
        }
    }
}
