namespace HumanFortress.Simulation.Creatures;

internal sealed partial class CreaturesDiffLog
{
    internal readonly record struct MutationMemento(
        IReadOnlyList<CreaturesDiff> Operations,
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
