namespace HumanFortress.Runtime.Commands;

internal interface IRuntimeProfessionCommandBindings
{
    void SetProfessionWeightHandler(Action<Guid, string, int>? setProfessionWeight);
}
