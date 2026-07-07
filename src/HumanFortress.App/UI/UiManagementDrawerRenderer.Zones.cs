using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal static partial class UiManagementDrawerRenderer
{
    private static void DrawZonesTab(ICellSurface surf, ZoneDrawerData zones, int startY, int maxHeight)
    {
        surf.Print(2, startY, "All Zones:", Color.Yellow);

        var rows = zones.Zones;
        if (rows.Count == 0)
        {
            surf.Print(4, startY + 2, "No zones created yet", Color.Gray);
            surf.Print(4, startY + 3, "Press X to open zone menu and create zones", Color.DarkGray);
            return;
        }

        int line = startY + 2;
        int maxLines = startY + maxHeight - 2;

        surf.Print(4, line++, $"{"ID",-6} {"Name",-25} {"Type",-20} {"Cells",8}", Color.Gray);

        foreach (var zone in rows)
        {
            if (line >= maxLines) break;

            string name = zone.Name.Length > 24 ? zone.Name.Substring(0, 21) + "..." : zone.Name;
            string type = zone.DisplayName.Length > 19 ? zone.DisplayName.Substring(0, 16) + "..." : zone.DisplayName;

            surf.Print(4, line++, $"{zone.ZoneId,-6} {name,-25} {type,-20} {zone.TotalCells,8}", Color.White);
        }

        if (rows.Count > (maxLines - startY - 2))
        {
            surf.Print(4, maxLines, $"... and {rows.Count - (maxLines - startY - 2)} more", Color.DarkGray);
        }
    }
}
