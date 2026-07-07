namespace HumanFortress.App.Runtime;

internal sealed partial class FortressRuntimeAccess
{
    internal void SetProfessionWeight(Guid workerId, string professionId, int weight)
    {
        _professionCommands.SetProfessionWeight(workerId, professionId, weight);
    }

    void IFortressRuntimeUiInputAccess.SetProfessionWeight(Guid workerId, string professionId, int weight) =>
        SetProfessionWeight(workerId, professionId, weight);
}
