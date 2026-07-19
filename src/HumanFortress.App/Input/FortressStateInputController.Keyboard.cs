using HumanFortress.App.UI;
using SadConsole.Input;

namespace HumanFortress.App.Input;

internal sealed partial class FortressStateInputController
{
    public bool ApplyMouseWheelInput(int scrollWheelValueChange, Keyboard? keyboard)
    {
        var wheel = FortressMouseWheelInput.Handle(
            scrollWheelValueChange,
            keyboard,
            _ui,
            _view.SelectionTool,
            _viewport.ZoomLevel,
            _viewport.CurrentZ,
            _viewport.WorldBounds);

        if (!wheel.Changed)
            return false;

        _viewport.ApplyWheel(wheel.ZoomLevel, wheel.CurrentZ);
        ClampCameraToWorld();
        return true;
    }

    public bool ProcessKeyboard(Keyboard keyboard)
    {
        var result = FortressKeyboardInputRouter.Process(_inputContexts.CreateKeyboard(), keyboard);
        _viewport.ApplyKeyboard(result.CameraPosition, result.CurrentZ);

        if (result.ShouldRedraw)
            RedrawAfterInput();

        return result.Handled;
    }
}
