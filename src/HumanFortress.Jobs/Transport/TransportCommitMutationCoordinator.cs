namespace HumanFortress.Jobs.Transport;

internal interface ITransportCommitMutationParticipant
{
    object CaptureMutationMemento();

    void RestoreMutationMemento(object memento);
}

internal interface ITransportCommitMutationScope : IDisposable
{
    void Commit();
}

/// <summary>
/// Groups append-only diff owners and completion sinks into one rollback boundary.
/// Participants are restored in reverse capture order when the scope is not committed.
/// </summary>
internal sealed class TransportCommitMutationCoordinator
{
    private readonly ITransportCommitMutationParticipant[] _participants;

    internal static TransportCommitMutationCoordinator Empty { get; } = new();

    internal TransportCommitMutationCoordinator(
        params ITransportCommitMutationParticipant[] participants)
    {
        ArgumentNullException.ThrowIfNull(participants);
        if (participants.Any(static participant => participant == null))
            throw new ArgumentException("Transport commit mutation participants cannot contain null.", nameof(participants));

        var unique = new HashSet<ITransportCommitMutationParticipant>(
            ReferenceEqualityComparer.Instance);
        _participants = participants.Where(unique.Add).ToArray();
    }

    internal ITransportCommitMutationScope BeginMutationScope()
    {
        var entries = new MutationEntry[_participants.Length];
        for (int index = 0; index < _participants.Length; index++)
        {
            var participant = _participants[index];
            entries[index] = new MutationEntry(
                participant,
                participant.CaptureMutationMemento());
        }

        return new MutationScope(entries);
    }

    private readonly record struct MutationEntry(
        ITransportCommitMutationParticipant Participant,
        object Memento);

    private sealed class MutationScope : ITransportCommitMutationScope
    {
        private MutationEntry[]? _entries;
        private bool _committed;

        internal MutationScope(MutationEntry[] entries)
        {
            _entries = entries;
        }

        void ITransportCommitMutationScope.Commit()
        {
            ObjectDisposedException.ThrowIf(_entries == null, this);
            _committed = true;
        }

        void IDisposable.Dispose()
        {
            var entries = Interlocked.Exchange(ref _entries, null);
            if (entries == null || _committed)
                return;

            List<Exception>? failures = null;
            for (int index = entries.Length - 1; index >= 0; index--)
            {
                try
                {
                    var entry = entries[index];
                    entry.Participant.RestoreMutationMemento(entry.Memento);
                }
                catch (Exception exception)
                {
                    (failures ??= new List<Exception>()).Add(exception);
                }
            }

            if (failures != null)
                throw new AggregateException("Transport commit side-effect rollback failed.", failures);
        }
    }
}
