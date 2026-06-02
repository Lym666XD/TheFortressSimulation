using HumanFortress.App.UI;
using SadConsole.Input;

namespace HumanFortress.App.Runtime;

internal static class FortressDebugMenuInput
{
    public static bool Handle(Keyboard keyboard, UiStore ui)
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
            return SelectCreature(ui, "core_race_dwarf");
        if (keyboard.IsKeyPressed(Keys.H))
            return SelectCreature(ui, "core_race_human");
        if (keyboard.IsKeyPressed(Keys.G))
            return SelectCreature(ui, "core_race_goblin");
        if (keyboard.IsKeyPressed(Keys.E))
            return SelectCreature(ui, "core_race_elf");
        if (keyboard.IsKeyPressed(Keys.O))
            return SelectCreature(ui, "core_race_orc");

        return false;
    }

    private static bool SelectCreature(UiStore ui, string creatureId)
    {
        ui.DebugSelectedCreature = creatureId;
        return true;
    }
}
