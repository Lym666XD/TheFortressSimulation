using HumanFortress.App;
using HumanFortress.App.UI.Commands;
using SadConsole;
using SadConsole.Input;
using SadRogue.Primitives;

namespace HumanFortress.App.UI.Components;

internal sealed partial class InputHandlerComponent
{
    public void ProcessMouse(IScreenObject host, MouseScreenObjectState state, out bool handled)
    {
        handled = false;

        var localPos = state.SurfaceCellPosition;
        if (state.Mouse.LeftClicked)
        {
            if (_debugMenuInput.TryHandleClick(localPos))
            {
                handled = true;
                return;
            }

            handled = HandleLeftClick(localPos);
        }
        else if (state.Mouse.RightClicked)
        {
            handled = HandleRightClick(localPos);
        }
    }

    private bool HandleLeftClick(Point localPos)
    {
        var store = _uiStateManager.Store;

        if (_drawerMouseInput.HandleLeftClick(localPos))
        {
            return true;
        }

        if (_workAllocation.HandleClick(localPos))
        {
            store.SuppressNextTileClick = true;
            return true;
        }

        return false;
    }

    private bool HandleRightClick(Point localPos)
    {
        new NavigateBackCommand().Execute(_uiStateManager);
        Logger.Log("[InputHandler] Right-click -> NavigateBack (force cancel)");
        _uiStateManager.Store.SuppressNextTileClick = true;
        return true;
    }
}
