using HumanFortress.Jobs.Transport;
using HumanFortress.Simulation.Stockpile;

namespace HumanFortress.Jobs.Diff;

internal sealed partial class TransportStockpileIndexEmitter : ITransportCommitMutationParticipant
{
    object ITransportCommitMutationParticipant.CaptureMutationMemento() =>
        _stockpileDiffs.CaptureMutationMemento();

    void ITransportCommitMutationParticipant.RestoreMutationMemento(object memento)
    {
        if (memento is not StockpileDiffLog.MutationMemento typed)
            throw new ArgumentException("Transport stockpile mutation memento has the wrong type.", nameof(memento));

        _stockpileDiffs.RestoreMutationMemento(typed);
    }
}
