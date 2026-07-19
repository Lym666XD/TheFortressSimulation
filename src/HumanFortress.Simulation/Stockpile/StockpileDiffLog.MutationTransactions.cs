namespace HumanFortress.Simulation.Stockpile;

internal sealed partial class StockpileDiffLog
{
    internal readonly record struct MutationMemento(
        IReadOnlyList<StockpileDiff> Operations,
        int LocalSequence);

    internal MutationMemento CaptureMutationMemento()
    {
        lock (_lock)
        {
            return new MutationMemento(_ops.ToArray(), _localSeq);
        }
    }

    internal void RestoreMutationMemento(MutationMemento memento)
    {
        ArgumentNullException.ThrowIfNull(memento.Operations);

        lock (_lock)
        {
            _ops.Clear();
            _ops.AddRange(memento.Operations);
            _localSeq = memento.LocalSequence;
        }
    }
}
