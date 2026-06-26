using HumanFortress.App.UI.Commands;
using SadConsole;
using SadConsole.Input;

namespace HumanFortress.App.UI.Components;

internal sealed partial class InputHandlerComponent
{
    public void ProcessKeyboard(IScreenObject host, Keyboard keyboard, out bool handled)
    {
        handled = false;

        if (TryHandleDockShortcut(keyboard))
        {
            handled = true;
        }
        else if (TryHandleQuickShortcut(keyboard))
        {
            handled = true;
        }
        else if (_workAllocation.HandleKeyboard(keyboard))
        {
            handled = true;
        }
        else if (keyboard.IsKeyPressed(Keys.Escape))
        {
            new CancelCommand().Execute(_uiStateManager);
            handled = true;
        }
        else if (keyboard.IsKeyPressed(Keys.Back))
        {
            new NavigateBackCommand().Execute(_uiStateManager);
            handled = true;
        }
        else if (keyboard.IsKeyPressed(Keys.F9))
        {
            new ToggleHelpCommand().Execute(_uiStateManager);
            handled = true;
        }
        else if (keyboard.IsKeyPressed(Keys.F10))
        {
            new ToggleDebugCommand().Execute(_uiStateManager);
            handled = true;
        }
        else if (keyboard.IsKeyPressed(Keys.F11))
        {
            new TogglePauseCommand().Execute(_uiStateManager);
            handled = true;
        }
    }

    private bool TryHandleDockShortcut(Keyboard keyboard)
    {
        foreach (var slot in UiChromeSlots.DockSlots)
        {
            if (!keyboard.IsKeyPressed(slot.Key))
                continue;

            new ToggleDrawerCommand(slot.Drawer).Execute(_uiStateManager);
            return true;
        }

        return false;
    }

    private bool TryHandleQuickShortcut(Keyboard keyboard)
    {
        foreach (var slot in UiChromeSlots.QuickSlots)
        {
            if (!keyboard.IsKeyPressed(slot.Key))
                continue;

            new ToggleQuickMenuCommand(slot.Menu).Execute(_uiStateManager);
            return true;
        }

        return false;
    }
}
