using HumanFortress.App.UI;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole.Input;

namespace HumanFortress.App.Input;

internal static class FortressDebugMenuInput
{
    public static bool Handle(Keyboard keyboard, UiStore ui, SimulationDebugMenuData debugMenu)
    {
        ArgumentNullException.ThrowIfNull(keyboard);
        ArgumentNullException.ThrowIfNull(ui);

        if (!ui.DebugOpen)
            return false;

        if (keyboard.IsKeyPressed(Keys.Tab))
        {
            ui.DebugMenuTab = (ui.DebugMenuTab + 1) % 3;
            return true;
        }

        if (keyboard.IsKeyPressed(Keys.D0))
        {
            ui.DebugMenuTab = 0;
            return true;
        }

        if (keyboard.IsKeyPressed(Keys.D1))
        {
            if (ui.DebugMenuTab != 2)
                ui.DebugMenuTab = 1;
            return true;
        }

        if (keyboard.IsKeyPressed(Keys.D2))
        {
            if (ui.DebugMenuTab != 2)
                ui.DebugMenuTab = 2;
            return true;
        }

        if ((keyboard.IsKeyPressed(Keys.D3) || keyboard.IsKeyPressed(Keys.D4) || keyboard.IsKeyPressed(Keys.D5)) && ui.DebugMenuTab == 2)
            return true;

        if (ui.DebugMenuTab != 1)
            return false;

        if (keyboard.IsKeyPressed(Keys.D))
            return DebugSelectionPolicy.SelectCreatureByIndex(ui, debugMenu, 0);
        if (keyboard.IsKeyPressed(Keys.H))
            return DebugSelectionPolicy.SelectCreatureByIndex(ui, debugMenu, 1);
        if (keyboard.IsKeyPressed(Keys.G))
            return DebugSelectionPolicy.SelectCreatureByIndex(ui, debugMenu, 2);
        if (keyboard.IsKeyPressed(Keys.E))
            return DebugSelectionPolicy.SelectCreatureByIndex(ui, debugMenu, 3);
        if (keyboard.IsKeyPressed(Keys.O))
            return DebugSelectionPolicy.SelectCreatureByIndex(ui, debugMenu, 4);

        return false;
    }
}
