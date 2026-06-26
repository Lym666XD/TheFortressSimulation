using HumanFortress.App.UI;
using SadConsole.Input;

namespace HumanFortress.App.Input;

internal static partial class FortressGlobalUiKeyboardInput
{
    public static bool HandleDrawerTabs(Keyboard keyboard, UiStore ui)
    {
        ArgumentNullException.ThrowIfNull(keyboard);
        ArgumentNullException.ThrowIfNull(ui);

        if (ui.Context != UiContext.Drawer || !keyboard.IsKeyPressed(Keys.Tab))
            return false;

        if (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift))
            ui.TabPrev();
        else
            ui.TabNext();

        return true;
    }
}
