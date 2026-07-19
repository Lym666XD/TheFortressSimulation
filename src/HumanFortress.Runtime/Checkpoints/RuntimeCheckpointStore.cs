namespace HumanFortress.Runtime.Checkpoints;

internal enum RuntimeCheckpointPublicationMode
{
    Unavailable,
    Full,
    Delta,
}

internal readonly record struct RuntimeCheckpointPublicationPlan(
    RuntimeCheckpointPublicationMode Mode,
    string RequestHash,
    RuntimeCommittedCheckpoint? Checkpoint,
    RuntimeCommittedCheckpoint? BaseCheckpoint)
{
    internal bool RequiresFullSnapshot => Mode == RuntimeCheckpointPublicationMode.Full;
}

internal sealed class RuntimeCheckpointStore
{
    internal const int RetainedBaseCount = 2;
    internal const int MaximumRetainedCheckpointCount = RetainedBaseCount + 1;

    private RuntimeCheckpointGenerationLease? _activeGeneration;
    private StoreState _state = StoreState.Empty;

    internal int RetainedCount => Volatile.Read(ref _state).Checkpoints.Length;

    internal void ActivateGeneration(RuntimeCheckpointGenerationLease generation)
    {
        ArgumentNullException.ThrowIfNull(generation);
        if (!generation.IsValid)
            throw new InvalidOperationException("Cannot activate an invalid checkpoint generation.");

        var previous = Interlocked.Exchange(ref _activeGeneration, generation);
        if (previous != null && !ReferenceEquals(previous, generation))
            previous.Invalidate();

        Volatile.Write(ref _state, new StoreState(
            generation.Generation,
            Array.Empty<RuntimeCommittedCheckpoint>()));
    }

    internal bool DeactivateGeneration(RuntimeCheckpointGenerationLease generation)
    {
        ArgumentNullException.ThrowIfNull(generation);
        if (!ReferenceEquals(
                Interlocked.CompareExchange(ref _activeGeneration, null, generation),
                generation))
        {
            return false;
        }

        generation.Invalidate();
        while (true)
        {
            var observed = Volatile.Read(ref _state);
            if (observed.Generation != generation.Generation)
                return true;

            if (ReferenceEquals(
                    Interlocked.CompareExchange(ref _state, StoreState.Empty, observed),
                    observed))
            {
                return true;
            }
        }
    }

    internal bool TryPublish(
        RuntimeCheckpointGenerationLease generation,
        RuntimeCommittedCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(generation);
        ArgumentNullException.ThrowIfNull(checkpoint);
        if (!IsCurrentGeneration(generation)
            || checkpoint.Identity.SessionGeneration != generation.Generation)
        {
            return false;
        }

        while (true)
        {
            var observed = Volatile.Read(ref _state);
            if (observed.Generation != generation.Generation || !IsCurrentGeneration(generation))
                return false;

            if (observed.Checkpoints.Length > 0
                && checkpoint.Identity.RuntimeTick <= observed.Checkpoints[0].Identity.RuntimeTick)
            {
                return false;
            }

            var retainedCount = Math.Min(
                observed.Checkpoints.Length,
                RetainedBaseCount);
            var checkpoints = new RuntimeCommittedCheckpoint[retainedCount + 1];
            checkpoints[0] = checkpoint;
            if (retainedCount > 0)
                Array.Copy(observed.Checkpoints, 0, checkpoints, 1, retainedCount);

            var updated = new StoreState(generation.Generation, checkpoints);
            if (!ReferenceEquals(
                    Interlocked.CompareExchange(ref _state, updated, observed),
                    observed))
            {
                continue;
            }

            return IsCurrentGeneration(generation);
        }
    }

    internal bool TryGetLatest(out RuntimeCommittedCheckpoint? checkpoint)
    {
        var observed = Volatile.Read(ref _state);
        if (observed.Checkpoints.Length == 0)
        {
            checkpoint = null;
            return false;
        }

        checkpoint = observed.Checkpoints[0];
        return true;
    }

    internal RuntimeCheckpointPublicationPlan ResolvePublication(
        string requestHash,
        string? previousRequestHash,
        string? requestedBaseAggregateHash)
    {
        if (string.IsNullOrWhiteSpace(requestHash))
            throw new ArgumentException("Checkpoint request hash must not be blank.", nameof(requestHash));

        var observed = Volatile.Read(ref _state);
        if (observed.Checkpoints.Length == 0)
        {
            return new RuntimeCheckpointPublicationPlan(
                RuntimeCheckpointPublicationMode.Unavailable,
                requestHash,
                null,
                null);
        }

        var latest = observed.Checkpoints[0];
        if (!string.Equals(requestHash, previousRequestHash, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(requestedBaseAggregateHash))
        {
            return Full(requestHash, latest);
        }

        RuntimeCommittedCheckpoint? baseCheckpoint = null;
        foreach (var candidate in observed.Checkpoints)
        {
            if (string.Equals(
                    candidate.Identity.AggregateHash,
                    requestedBaseAggregateHash,
                    StringComparison.Ordinal))
            {
                baseCheckpoint = candidate;
                break;
            }
        }

        return baseCheckpoint == null
            ? Full(requestHash, latest)
            : new RuntimeCheckpointPublicationPlan(
                RuntimeCheckpointPublicationMode.Delta,
                requestHash,
                latest,
                baseCheckpoint);
    }

    private bool IsCurrentGeneration(RuntimeCheckpointGenerationLease generation)
    {
        return generation.IsValid
            && ReferenceEquals(Volatile.Read(ref _activeGeneration), generation);
    }

    private static RuntimeCheckpointPublicationPlan Full(
        string requestHash,
        RuntimeCommittedCheckpoint checkpoint)
    {
        return new RuntimeCheckpointPublicationPlan(
            RuntimeCheckpointPublicationMode.Full,
            requestHash,
            checkpoint,
            null);
    }

    private sealed record StoreState(
        ulong Generation,
        RuntimeCommittedCheckpoint[] Checkpoints)
    {
        internal static StoreState Empty { get; } = new(
            0,
            Array.Empty<RuntimeCommittedCheckpoint>());
    }
}
