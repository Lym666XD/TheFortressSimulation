namespace HumanFortress.Runtime;

internal sealed class ProfessionAssignmentCommandTarget : IProfessionAssignmentCommandTarget
{
    private Action<Guid, string, int>? _setProfessionWeight;

    internal void SetHandler(Action<Guid, string, int>? setProfessionWeight)
    {
        _setProfessionWeight = setProfessionWeight;
    }

    void IProfessionAssignmentCommandTarget.SetProfessionWeight(Guid workerId, string professionId, int weight)
    {
        _setProfessionWeight?.Invoke(workerId, professionId, weight);
    }
}
