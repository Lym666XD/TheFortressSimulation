using HumanFortress.Contracts.Runtime.Checkpoints;

namespace HumanFortress.Runtime.Checkpoints;

internal sealed class RuntimeCheckpointCoordinator
{
    private readonly RuntimeCheckpointStore _store;
    private RuntimeCheckpointGenerationLease? _activeGeneration;
    private long _nextGeneration;

    internal RuntimeCheckpointCoordinator(RuntimeCheckpointStore? store = null)
    {
        _store = store ?? new RuntimeCheckpointStore();
    }

    internal RuntimeCheckpointStore Store => _store;

    internal RuntimeCheckpointGenerationLease BeginGeneration()
    {
        long next = Interlocked.Increment(ref _nextGeneration);
        if (next <= 0)
            throw new InvalidOperationException("Checkpoint generation counter exhausted.");

        var generation = new RuntimeCheckpointGenerationLease((ulong)next);
        _store.ActivateGeneration(generation);
        Interlocked.Exchange(ref _activeGeneration, generation);
        return generation;
    }

    internal bool InvalidateGeneration(RuntimeCheckpointGenerationLease generation)
    {
        ArgumentNullException.ThrowIfNull(generation);
        if (!ReferenceEquals(
                Interlocked.CompareExchange(ref _activeGeneration, null, generation),
                generation))
        {
            return false;
        }

        generation.Invalidate();
        _store.DeactivateGeneration(generation);
        return true;
    }

    internal bool TryPublish(
        RuntimeCheckpointGenerationLease generation,
        ulong runtimeTick,
        RuntimeContentIdentityData content,
        IEnumerable<RuntimeCheckpointSectionInput> sections,
        out RuntimeCommittedCheckpoint? checkpoint)
    {
        ArgumentNullException.ThrowIfNull(generation);
        ArgumentNullException.ThrowIfNull(sections);
        if (!generation.IsValid
            || !ReferenceEquals(Volatile.Read(ref _activeGeneration), generation))
        {
            checkpoint = null;
            return false;
        }

        var candidate = new RuntimeCommittedCheckpoint(
            generation,
            runtimeTick,
            content,
            sections);
        if (!_store.TryPublish(generation, candidate))
        {
            checkpoint = null;
            return false;
        }

        checkpoint = candidate;
        return true;
    }

    internal RuntimeCheckpointPublicationPlan ResolvePublication(
        string requestHash,
        string? previousRequestHash,
        string? requestedBaseAggregateHash)
    {
        return _store.ResolvePublication(
            requestHash,
            previousRequestHash,
            requestedBaseAggregateHash);
    }
}
