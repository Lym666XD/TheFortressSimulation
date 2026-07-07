using HumanFortress.App.UI;
using SadConsole.Input;

namespace HumanFortress.App.Input;

internal static partial class FortressOrdersKeyboardInput
{
    private static bool HandleWipSubmenu(Keyboard keyboard, UiStore ui, ulong uiTick, params (string Toast, Keys Key)[] actions)
    {
        foreach (var (toast, key) in actions)
        {
            if (keyboard.IsKeyPressed(key))
            {
                ui.AddToast(toast, uiTick + 120);
                return true;
            }
        }

        if (keyboard.IsKeyPressed(Keys.OemComma))
        {
            ui.CancelPlacement();
            return true;
        }

        return false;
    }
}
