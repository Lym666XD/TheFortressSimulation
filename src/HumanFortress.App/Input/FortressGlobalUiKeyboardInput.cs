using HumanFortress.App.UI;
using SadConsole.Input;

namespace HumanFortress.App.Input;

internal static partial class FortressGlobalUiKeyboardInput
{
    public static bool HandleHelpAndDebug(Keyboard keyboard, UiStore ui)
    {
        ArgumentNullException.ThrowIfNull(keyboard);
        ArgumentNullException.ThrowIfNull(ui);

        var changed = false;

        if (keyboard.IsKeyPressed(Keys.H))
        {
            ui.ToggleHelp();
            changed = true;
        }

        if (IsDebugTogglePressed(keyboard))
        {
            ui.ToggleDebug();
            Logger.Log($"[DEBUG] Toggle debug menu -> {ui.DebugOpen}");
            changed = true;
        }

        return changed;
    }

}
