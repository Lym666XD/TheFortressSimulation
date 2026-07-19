using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal sealed partial class ZonesUI
{
    private void DrawZoneOptions(
        ScreenSurface surface,
        int x,
        int y,
        ZoneSubmenu submenu,
        SimulationZoneCatalogData zoneCatalog)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;
        var options = ZoneOptionPresentation.GetOptions(zoneCatalog, submenu);
        int visibleCount = Math.Min(options.Count, 8);
        int height = Math.Max(6, visibleCount + 4);

        DrawBox(surface, x, y, 36, height, fg, bg);
        surface.Print(x + 1, y, $" {submenu.ToString().ToUpperInvariant()} ZONE ", highlight);
        for (int i = 0; i < visibleCount; i++)
            surface.Print(x + 2, y + 1 + i, $"[{options[i].Keybind}] {options[i].DisplayName}", fg);
        if (visibleCount == 0)
            surface.Print(x + 2, y + 1, "No zone definitions", Color.Gray);
        surface.Print(x + 2, y + height - 2, "[,] Remove Zone", fg);
    }
}
