using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal static partial class UiDebugMenuRenderer
{
    private static void DrawCreaturesTab(ICellSurface surf, int x0, int y0, UiStore ui)
    {
        surf.Print(x0 + 2, y0 + 2, "Spawn Creature:", Color.Yellow);
        int cx = x0 + 2;
        int cy = y0 + 3;
        WritePill(surf, ref cx, cy, "Dwarf", ui.DebugSelectedCreature.Contains("dwarf") ? Color.Black : Color.White, ui.DebugSelectedCreature.Contains("dwarf") ? Color.Yellow : new Color(40, 40, 40));
        WritePill(surf, ref cx, cy, "Human", ui.DebugSelectedCreature.Contains("human") ? Color.Black : Color.White, ui.DebugSelectedCreature.Contains("human") ? Color.Yellow : new Color(40, 40, 40));
        WritePill(surf, ref cx, cy, "Goblin", ui.DebugSelectedCreature.Contains("goblin") ? Color.Black : Color.White, ui.DebugSelectedCreature.Contains("goblin") ? Color.Yellow : new Color(40, 40, 40));
        WritePill(surf, ref cx, cy, "Elf", ui.DebugSelectedCreature.Contains("elf") ? Color.Black : Color.White, ui.DebugSelectedCreature.Contains("elf") ? Color.Yellow : new Color(40, 40, 40));
        WritePill(surf, ref cx, cy, "Orc", ui.DebugSelectedCreature.Contains("orc") ? Color.Black : Color.White, ui.DebugSelectedCreature.Contains("orc") ? Color.Yellow : new Color(40, 40, 40));

        surf.Print(x0 + 2, y0 + 9, $"Selected: {GetCreatureName(ui.DebugSelectedCreature)}", Color.Cyan);
        surf.Print(x0 + 2, y0 + 11, "Click map to spawn at mouse position", Color.Green);
    }

    private static string GetCreatureName(string id)
    {
        return id switch
        {
            "core_race_dwarf" => "Dwarf",
            "core_race_human" => "Human",
            "core_race_goblin" => "Goblin",
            "core_race_elf" => "Elf",
            "core_race_orc" => "Orc",
            _ => "Unknown"
        };
    }
}
