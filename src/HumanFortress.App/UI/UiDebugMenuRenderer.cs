using HumanFortress.App.Diagnostics;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal static partial class UiDebugMenuRenderer
{
    public static void Draw(
        ScreenSurface mapSurface,
        UiStore ui,
        Point cursor,
        int currentZ,
        int zoomLevel,
        Point camera,
        int fortressSize,
        SimulationDebugMenuData debugMenu,
        DiagnosticSnapshot? diagnostics = null)
    {
        if (!ui.DebugOpen) return;
        var surf = mapSurface.Surface;
        int width = Math.Min((int)(surf.Width * 0.7), surf.Width - 4);
        int height = Math.Min((int)(surf.Height * 0.6), surf.Height - 4);
        int x0 = (surf.Width - width) / 2;
        int y0 = (surf.Height - height) / 2;
        var bg = new Color(15, 15, 15, 180);

        for (int yy = y0; yy < y0 + height; yy++)
            for (int x = x0; x < x0 + width; x++)
                surf.SetGlyph(x, yy, ' ', Color.White, bg);

        for (int x = x0; x < x0 + width; x++)
        {
            surf.SetGlyph(x, y0, '-');
            surf.SetGlyph(x, y0 + height - 1, '-');
        }
        for (int y = y0; y < y0 + height; y++)
        {
            surf.SetGlyph(x0, y, '|');
            surf.SetGlyph(x0 + width - 1, y, '|');
        }
        surf.SetGlyph(x0, y0, '+');
        surf.SetGlyph(x0 + width - 1, y0, '+');
        surf.SetGlyph(x0, y0 + height - 1, '+');
        surf.SetGlyph(x0 + width - 1, y0 + height - 1, '+');

        surf.Print(x0 + 2, y0, "DEBUG MENU", Color.Cyan);
        int tabX = x0 + 22;
        var tabColor0 = ui.DebugMenuTab == 0 ? Color.Yellow : Color.DarkGray;
        var tabColor1 = ui.DebugMenuTab == 1 ? Color.Yellow : Color.DarkGray;
        var tabColor2 = ui.DebugMenuTab == 2 ? Color.Yellow : Color.DarkGray;
        surf.Print(tabX, y0, "Status", tabColor0);
        surf.Print(tabX + 8, y0, "|", Color.Gray);
        surf.Print(tabX + 10, y0, "Creatures", tabColor1);
        surf.Print(tabX + 20, y0, "|", Color.Gray);
        surf.Print(tabX + 22, y0, "Items", tabColor2);

        if (ui.DebugMenuTab == 0)
        {
            DrawStatusTab(surf, x0, y0, width, cursor, currentZ, zoomLevel, camera, fortressSize, debugMenu, diagnostics);
        }
        else if (ui.DebugMenuTab == 1)
        {
            DrawCreaturesTab(surf, x0, y0, ui);
        }
        else
        {
            DrawItemsTab(surf, x0, y0, width, ui, debugMenu);
        }
    }

}
