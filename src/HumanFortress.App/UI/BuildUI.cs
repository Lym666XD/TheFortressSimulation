using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

/// <summary>
/// Renders the Build quick menu with L2 submenus (no L3 for now).
/// </summary>
internal sealed partial class BuildUI
{
    public void DrawBuildRootPopup(ScreenSurface surface, int x, int y)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        DrawBox(surface, x, y, 30, 8, fg, bg);
        surface.Print(x + 1, y, " CONSTRUCTION ", highlight);
        surface.Print(x + 2, y + 1, "[Z] Structural", fg);
        surface.Print(x + 2, y + 2, "[X] Functional Structure", fg);
        surface.Print(x + 2, y + 3, "[C] Workshop", fg);
        surface.Print(x + 2, y + 4, "[V] Civil Furniture", fg);
        surface.Print(x + 2, y + 5, "[F] Utility Furniture", fg);
        surface.Print(x + 2, y + 6, "ESC: Cancel", Color.Gray);
    }

    // For now, L2 menus are displayed without L3 details
    // In the future, workshops will have L3 submenus
    public void DrawBuildWithSubmenu(ScreenSurface surface, int centerX, int centerY, BuildSubmenu activeSubmenu)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;
        var activeBg = new Color(40, 40, 0, 220);

        // L2 menu (centered)
        int l2Width = 30;
        int l2Height = 8;
        int l2X = centerX - l2Width / 2;
        int l2Y = centerY;

        DrawBox(surface, l2X, l2Y, l2Width, l2Height, fg, bg);
        surface.Print(l2X + 1, l2Y, " CONSTRUCTION ", highlight);

        DrawMenuOption(surface, l2X + 2, l2Y + 1, "[Z] Structural", activeSubmenu == BuildSubmenu.Structural, fg, activeBg);
        DrawMenuOption(surface, l2X + 2, l2Y + 2, "[X] Functional Structure", activeSubmenu == BuildSubmenu.FunctionalStructure, fg, activeBg);
        DrawMenuOption(surface, l2X + 2, l2Y + 3, "[C] Workshop", activeSubmenu == BuildSubmenu.Workshop, fg, activeBg);
        DrawMenuOption(surface, l2X + 2, l2Y + 4, "[V] Civil Furniture", activeSubmenu == BuildSubmenu.CivilFurniture, fg, activeBg);
        DrawMenuOption(surface, l2X + 2, l2Y + 5, "[F] Utility Furniture", activeSubmenu == BuildSubmenu.UtilityFurniture, fg, activeBg);
        surface.Print(l2X + 2, l2Y + 6, "ESC: Cancel", Color.Gray);

        // L3 menu for active submenu
        if (activeSubmenu == BuildSubmenu.Structural)
        {
            DrawStructuralL3(surface, l2X + l2Width + 2, l2Y);
        }
        else if (activeSubmenu == BuildSubmenu.Workshop)
        {
            DrawWorkshopMenu(surface, l2X + l2Width + 2, l2Y);
        }
    }
}
