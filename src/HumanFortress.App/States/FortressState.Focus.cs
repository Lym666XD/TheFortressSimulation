namespace HumanFortress.App.States;

internal sealed partial class FortressState
{
    public override void OnFocused()
    {
        base.OnFocused();
        Logger.Log("[FOCUS] FortressState focused");
    }

    public override void OnFocusLost()
    {
        base.OnFocusLost();
        Logger.Log("[FOCUS] FortressState lost focus -> reclaim");
        IsFocused = true;
    }

    private void EnsureFocused()
    {
        if (!IsFocused)
            IsFocused = true;
    }
}
