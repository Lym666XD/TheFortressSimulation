namespace HumanFortress.App.States;

internal sealed partial class FortressState
{
    public override void Update(TimeSpan delta)
    {
        base.Update(delta);
        _updateLoop.Update();
    }
}
