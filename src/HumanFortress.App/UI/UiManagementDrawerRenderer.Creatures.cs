using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;
using System.Linq;

namespace HumanFortress.App.UI;

internal static partial class UiManagementDrawerRenderer
{
    private static void DrawCreaturesTab(ICellSurface surf, CreatureDrawerData creatures, UiStore ui, int startY, int maxHeight)
    {
        var rows = creatures.Rows;

        surf.Print(2, startY, $"=== All Creatures ({creatures.Total}) ===", Color.Yellow);
        surf.Print(2, startY + 1, "Click creature to view details | Filter: (Coming soon)", Color.Gray);

        if (rows.Count == 0)
        {
            surf.Print(2, startY + 3, "No creatures spawned yet.", Color.DarkGray);
            surf.Print(2, startY + 4, "Use F12 Debug menu to spawn creatures.", Color.DarkGray);
            return;
        }

        int y = startY + 3;
        int maxY = startY + maxHeight - 2;
        int displayed = 0;

        foreach (var creature in rows.Take(20))
        {
            if (y >= maxY) break;

            string status = creature.Alive ? "IDLE" : "DEAD";
            var statusColor = creature.Alive ? Color.Green : Color.Red;
            bool selected = ui.SelectedCreatureGuid == creature.CreatureId.ToString();
            var bgColor = selected ? new Color(50, 50, 0) : new Color(20, 20, 20);

            for (int x = 2; x < surf.Width - 2; x++)
                surf.SetGlyph(x, y, ' ', Color.White, bgColor);

            surf.Print(2, y, $"{creature.DisplayName,-12} @ ({creature.X,3},{creature.Y,3},{creature.Z,2})", Color.White);
            surf.Print(45, y, $"[{status}]", statusColor);

            y++;
            displayed++;
        }

        if (creatures.Total > displayed)
        {
            surf.Print(2, y, $"... and {creatures.Total - displayed} more (scroll coming soon)", Color.DarkGray);
        }

        surf.Print(2, startY + maxHeight - 1, $"Total: {creatures.Total}  Alive: {creatures.Alive}  Dead: {creatures.Dead}", Color.Cyan);
    }

    private static void DrawAnimalsTab(ICellSurface surf, int startY)
    {
        surf.Print(2, startY, "=== Animals ===", Color.Yellow);
        surf.Print(2, startY + 2, "Coming soon - Animal tracking will be available here.", Color.Gray);
        surf.Print(2, startY + 3, "Press ESC to return.", Color.DarkGray);
    }
}
