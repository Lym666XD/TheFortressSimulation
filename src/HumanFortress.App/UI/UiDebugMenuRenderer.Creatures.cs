using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal static partial class UiDebugMenuRenderer
{
    private static void DrawCreaturesTab(
        ICellSurface surf,
        int x0,
        int y0,
        UiStore ui,
        SimulationDebugMenuData debugMenu)
    {
        surf.Print(x0 + 2, y0 + 2, "Spawn Creature:", Color.Yellow);
        int cx = x0 + 2;
        int cy = y0 + 3;
        var creatures = debugMenu.Creatures ?? Array.Empty<DebugCreatureView>();
        foreach (var creature in creatures.Take(5))
        {
            bool selected = string.Equals(ui.DebugSelectedCreature, creature.Id, StringComparison.Ordinal);
            WritePill(
                surf,
                ref cx,
                cy,
                creature.DisplayName,
                selected ? Color.Black : Color.White,
                selected ? Color.Yellow : new Color(40, 40, 40));
        }

        var selectedName = creatures
            .FirstOrDefault(creature => string.Equals(creature.Id, ui.DebugSelectedCreature, StringComparison.Ordinal))
            .DisplayName;
        surf.Print(x0 + 2, y0 + 9, $"Selected: {selectedName ?? ui.DebugSelectedCreature}", Color.Cyan);
        surf.Print(x0 + 2, y0 + 11, "Click map to spawn at mouse position", Color.Green);
    }

}
