using HumanFortress.Jobs.Transport;

namespace HumanFortress.Jobs.Profession;

internal sealed partial class TransportProfessionCompletionSink : ITransportCommitMutationParticipant
{
    object ITransportCommitMutationParticipant.CaptureMutationMemento() =>
        _professions.GetReplaySnapshot();

    void ITransportCommitMutationParticipant.RestoreMutationMemento(object memento)
    {
        if (memento is not ProfessionAssignmentsReplaySnapshot typed)
            throw new ArgumentException("Transport profession mutation memento has the wrong type.", nameof(memento));

        _professions.RestoreMutationMemento(typed);
    }
}
