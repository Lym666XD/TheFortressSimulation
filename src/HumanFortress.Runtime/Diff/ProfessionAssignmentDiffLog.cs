namespace HumanFortress.Runtime.Diff;

internal sealed class ProfessionAssignmentDiffLog
{
    private readonly List<ProfessionAssignmentDiff> _ops = new();
    private readonly object _lock = new();
    private int _localSeq;
    private Action<Guid, string, int>? _setProfessionWeight;

    internal void SetProfessionWeightHandler(Action<Guid, string, int>? setProfessionWeight)
    {
        lock (_lock)
        {
            _setProfessionWeight = setProfessionWeight;
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

    internal void ApplyAll()
    {
        List<ProfessionAssignmentDiff> diffs;
        Action<Guid, string, int>? handler;
        lock (_lock)
        {
            diffs = _ops.OrderBy(static diff => diff.LocalSeq).ToList();
            handler = _setProfessionWeight;
            _ops.Clear();
            _localSeq = 0;
        }

        if (handler == null)
            return;

        foreach (var diff in diffs)
        {
            handler(diff.WorkerId, diff.ProfessionId, diff.Weight);
        }
    }

    internal void Clear()
    {
        lock (_lock)
        {
            _ops.Clear();
            _localSeq = 0;
        }
    }

    private readonly record struct ProfessionAssignmentDiff(
        Guid WorkerId,
        string ProfessionId,
        int Weight,
        string SystemId,
        int LocalSeq);
}
