namespace HumanFortress.Core.Simulation;

public sealed partial class DiffLog
{
    /// <summary>
    /// Captures the pending operations so a higher-level serialized transaction
    /// can restore the log if its authority commit fails.
    /// </summary>
    public IReadOnlyList<DiffOp> CaptureMutationMemento()
    {
        lock (_lock)
        {
            return _operations.ToArray();
        }
    }

    /// <summary>
    /// Restores a memento previously returned by <see cref="CaptureMutationMemento"/>.
    /// </summary>
    public void RestoreMutationMemento(IReadOnlyList<DiffOp> memento)
    {
        ArgumentNullException.ThrowIfNull(memento);

        lock (_lock)
        {
            _operations.Clear();
            _operations.AddRange(memento);
        }
    }
}
