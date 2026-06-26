using SadConsole.Input;

namespace HumanFortress.App.States;

internal sealed partial class FortressState
{
    public override bool ProcessKeyboard(Keyboard keyboard)
    {
        return _inputController.ProcessKeyboard(keyboard);
    }

    public override bool ProcessMouse(MouseScreenObjectState state)
    {
        var result = _inputController.ProcessMouse(state);
        return result.ShouldCallBase ? base.ProcessMouse(state) : result.Handled;
    }
}
