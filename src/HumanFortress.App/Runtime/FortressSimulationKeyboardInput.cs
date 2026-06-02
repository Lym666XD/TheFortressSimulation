using HumanFortress.App.UI;
using SadConsole.Input;

namespace HumanFortress.App.Runtime;

internal static class FortressSimulationKeyboardInput
{
    public static bool Handle(Keyboard keyboard, FortressRuntimeAccess runtime, UiStore ui, ulong uiTick)
    {
        ArgumentNullException.ThrowIfNull(keyboard);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(ui);

        if (keyboard.IsKeyPressed(Keys.Space))
        {
            var status = runtime.ToggleSimulationPause();
            string statusText = status.IsPaused ? "PAUSED" : $"Running at {status.SpeedMultiplier:F2}x";
            ui.AddToast(statusText, uiTick + 100);
            Logger.Log($"[SIM] Space -> Pause toggled: {status.IsPaused}");
            return true;
        }

        if (keyboard.IsKeyPressed(Keys.OemMinus) || keyboard.IsKeyPressed(Keys.Subtract))
        {
            var status = runtime.CycleSimulationSpeedDown();
            ui.AddToast($"Speed: {status.SpeedMultiplier:F2}x", uiTick + 100);
            Logger.Log($"[SIM] Speed decreased to {status.SpeedMultiplier:F2}x");
            return true;
        }

        if (keyboard.IsKeyPressed(Keys.OemPlus) || keyboard.IsKeyPressed(Keys.Add))
        {
            var status = runtime.CycleSimulationSpeedUp();
            ui.AddToast($"Speed: {status.SpeedMultiplier:F2}x", uiTick + 100);
            Logger.Log($"[SIM] Speed increased to {status.SpeedMultiplier:F2}x");
            return true;
        }

        return false;
    }
}
