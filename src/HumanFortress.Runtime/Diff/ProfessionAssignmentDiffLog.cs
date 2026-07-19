using HumanFortress.Jobs.Profession;

namespace HumanFortress.Runtime.Diff;

internal sealed class ProfessionAssignmentDiffLog
{
    private readonly List<ProfessionAssignmentDiff> _ops = new();
    private readonly object _lock = new();
    private int _localSeq;
    private Action<Guid, string, int>? _setProfessionWeight;
    private ProfessionAssignments? _transactionOwner;

    internal void SetProfessionWeightHandler(Action<Guid, string, int>? setProfessionWeight)
    {
        lock (_lock)
        {
            _setProfessionWeight = setProfessionWeight;
            _transactionOwner = setProfessionWeight?.Target as ProfessionAssignments;
        }
    }

    internal void AddSetWeight(Guid workerId, string professionId, int weight, string systemId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(professionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(systemId);

        lock (_lock)
        {
            _ops.Add(new ProfessionAssignmentDiff(
                workerId,
                professionId,
                weight,
                systemId,
                _localSeq++));
        }
    }

    internal PreparedProfessionAssignmentDiffs Prepare()
    {
        List<ProfessionAssignmentDiff> diffs;
        Action<Guid, string, int>? handler;
        ProfessionAssignments? transactionOwner;
        lock (_lock)
        {
            diffs = _ops.OrderBy(static diff => diff.LocalSeq).ToList();
            handler = _setProfessionWeight;
            transactionOwner = _transactionOwner;
        }

        if (diffs.Count > 0 && handler == null)
            throw new InvalidOperationException("Profession mutation handler is not configured.");
        if (diffs.Count > 0 && transactionOwner == null)
        {
            throw new NotSupportedException(
                "Profession mutations require a ProfessionAssignments transaction owner; " +
                "the configured delegate cannot provide rollback state.");
        }

        foreach (var diff in diffs)
        {
            if (diff.WorkerId == Guid.Empty)
                throw new InvalidOperationException("Profession mutation has an empty worker id.");
            if (string.IsNullOrWhiteSpace(diff.ProfessionId))
                throw new InvalidOperationException("Profession mutation has a blank profession id.");
            if (string.IsNullOrWhiteSpace(diff.SystemId))
                throw new InvalidOperationException("Profession mutation has a blank system id.");
        }

        return new PreparedProfessionAssignmentDiffs(
            diffs,
            handler,
            transactionOwner,
            transactionOwner?.GetReplaySnapshot()
                ?? ProfessionAssignmentsReplaySnapshot.Empty);
    }

    internal void Apply(PreparedProfessionAssignmentDiffs prepared)
    {
        if (prepared.Handler == null)
            return;

        foreach (var diff in prepared.Diffs)
        {
            prepared.Handler(diff.WorkerId, diff.ProfessionId, diff.Weight);
        }
    }

    internal static void Rollback(PreparedProfessionAssignmentDiffs prepared)
    {
        prepared.TransactionOwner?.RestoreMutationMemento(prepared.Memento);
    }

    internal void Clear()
    {
        lock (_lock)
        {
            _ops.Clear();
            _localSeq = 0;
        }
    }

    internal readonly record struct PreparedProfessionAssignmentDiffs(
        IReadOnlyList<ProfessionAssignmentDiff> Diffs,
        Action<Guid, string, int>? Handler,
        ProfessionAssignments? TransactionOwner,
        ProfessionAssignmentsReplaySnapshot Memento);

    internal readonly record struct ProfessionAssignmentDiff(
        Guid WorkerId,
        string ProfessionId,
        int Weight,
        string SystemId,
        int LocalSeq);
}
