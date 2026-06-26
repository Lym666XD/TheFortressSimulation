using SadConsole;
using SadConsole.Input;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal sealed partial class FortressStateInputController
{
    public FortressMouseInputResult ProcessMouse(MouseScreenObjectState state)
    {
        return FortressMouseInputRouter.Process(_inputContexts.CreateMouse(), state);
    }

    public void OnOverlayLeftClicked(Point local)
    {
        if (_view.UiSurface == null)
            return;

        FortressOverlayClickController.HandleLeftClick(_inputContexts.CreateOverlayClick(), local);
    }

    public void OnOverlayRightClicked(Point local)
    {
        if (_view.UiSurface == null)
            return;

        FortressOverlayClickController.HandleRightClick(_inputContexts.CreateOverlayClick(), local);
    }

    public void OnMapMouseMoved(Point local)
    {
        ApplyMouseHover(local, updateSelection: true, logMapEvent: true);
    }

    public void OnOverlayMouseMoved(Point local)
    {
        var hover = FortressMouseHoverController.ApplyOverlayHover(_inputContexts.CreateMouseHover(), local);
        _viewport.ApplyHover(hover.LastMousePosition, hover.CursorPosition);
    }

    public void OnMapLeftClicked(Point local)
    {
        FortressMapInteractionController.HandleLeftClick(_inputContexts.CreateMapInteraction(), local);
    }

    public bool ApplyMouseHover(Point mapLocal, bool updateSelection, bool logMapEvent)
    {
        var hover = FortressMouseHoverController.Apply(
            _inputContexts.CreateMouseHover(),
            mapLocal,
            updateSelection,
            logMapEvent);

        _viewport.ApplyHover(hover.LastMousePosition, hover.CursorPosition);
        return hover.Changed;
    }
}
