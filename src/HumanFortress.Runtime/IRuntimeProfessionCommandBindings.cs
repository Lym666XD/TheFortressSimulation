namespace HumanFortress.Runtime;

internal interface IRuntimeProfessionCommandBindings
{
    void SetProfessionWeightHandler(Action<Guid, string, int>? setProfessionWeight);
}
