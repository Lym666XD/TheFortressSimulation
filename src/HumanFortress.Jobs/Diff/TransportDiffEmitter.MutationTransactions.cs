using HumanFortress.Core.Simulation;
using HumanFortress.Jobs.Transport;
using HumanFortress.Simulation.Items;

namespace HumanFortress.Jobs.Diff;

internal sealed partial class TransportDiffEmitter : ITransportCommitMutationParticipant
{
    object ITransportCommitMutationParticipant.CaptureMutationMemento()
    {
        return new MutationMemento(
            _diff?.CaptureMutationMemento(),
            _itemsDiff.CaptureMutationMemento());
    }

    void ITransportCommitMutationParticipant.RestoreMutationMemento(object memento)
    {
        if (memento is not MutationMemento typed)
            throw new ArgumentException("Transport diff mutation memento has the wrong type.", nameof(memento));

        if (_diff != null && typed.CoreOperations != null)
            _diff.RestoreMutationMemento(typed.CoreOperations);
        _itemsDiff.RestoreMutationMemento(typed.ItemOperations);
    }

    private sealed record MutationMemento(
        IReadOnlyList<DiffOp>? CoreOperations,
        ItemsDiffLog.MutationMemento ItemOperations);
}
