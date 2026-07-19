using HumanFortress.App.Rendering;
using HumanFortress.App.UI;
using SadConsole.Input;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal sealed class FortressNavigationDebugController
{
    private Point? _pathStart;
    private int _pathStartZ;

    public bool HandleKeyboard(
        Keyboard keyboard,
        NavigationOverlay? navigationOverlay,
        Point cursorPosition,
        int currentZ,
        UiStore ui,
        ulong uiTick)
    {
        ArgumentNullException.ThrowIfNull(keyboard);
        ArgumentNullException.ThrowIfNull(ui);

        if (keyboard.IsKeyPressed(Keys.F9))
        {
            CycleNavigationOverlay(navigationOverlay, cursorPosition, ui, uiTick);
            return true;
        }

        if (!keyboard.IsKeyPressed(Keys.F10))
            return false;

        bool ctrl = keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl);
        if (ctrl)
        {
            navigationOverlay?.ClearPath();
            _pathStart = null;
            ui.AddToast("Path cleared", uiTick + 120);
            return true;
        }

        HandlePathTool(navigationOverlay, cursorPosition, currentZ, ui, uiTick);
        return true;
    }

    private static void CycleNavigationOverlay(NavigationOverlay? navigationOverlay, Point cursorPosition, UiStore ui, ulong uiTick)
    {
        if (navigationOverlay == null)
        {
            ui.AddToast("Overlay: unavailable", uiTick + 150);
            return;
        }

        navigationOverlay.CycleMode();
        if (navigationOverlay.CurrentMode == NavigationOverlay.OverlayMode.FlowField)
            navigationOverlay.SetTarget(cursorPosition);

        ui.AddToast($"Overlay: {navigationOverlay.CurrentMode}", uiTick + 150);
    }

    private void HandlePathTool(
        NavigationOverlay? navigationOverlay,
        Point cursorPosition,
        int currentZ,
        UiStore ui,
        ulong uiTick)
    {
        if (navigationOverlay == null)
            return;

        if (_pathStart == null)
        {
            _pathStart = cursorPosition;
            _pathStartZ = currentZ;
            ui.AddToast($"Start set at ({cursorPosition.X},{cursorPosition.Y},{currentZ})", uiTick + 150);
            navigationOverlay.CurrentMode = NavigationOverlay.OverlayMode.PathDisplay;
            return;
        }

        navigationOverlay.CurrentMode = NavigationOverlay.OverlayMode.PathDisplay;
        navigationOverlay.RequestPath(
            _pathStart.Value,
            _pathStartZ,
            cursorPosition,
            currentZ);
        ui.AddToast("Path requested", uiTick + 120);
    }
}
