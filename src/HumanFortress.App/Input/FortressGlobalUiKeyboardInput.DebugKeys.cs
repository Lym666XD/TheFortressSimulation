using SadConsole.Input;

namespace HumanFortress.App.Input;

internal static partial class FortressGlobalUiKeyboardInput
{
    private static bool IsDebugTogglePressed(Keyboard keyboard)
    {
        if (keyboard.IsKeyPressed(Keys.F12))
            return true;

        if (keyboard.KeysPressed.Count == 0)
            return false;

        foreach (var keyInfo in keyboard.KeysPressed)
        {
            var name = keyInfo.Key.ToString();
            if (name.Contains("OemTilde", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Oem3", StringComparison.OrdinalIgnoreCase)
                || name.Contains("OemGrave", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Oem8", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Oem7", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Backquote", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
