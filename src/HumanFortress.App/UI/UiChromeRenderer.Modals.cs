using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal static partial class UiChromeRenderer
{
    public static void DrawHelp(ScreenSurface overlay, UiStore ui)
    {
        if (!ui.HelpOpen) return;

        var surf = overlay.Surface;
        int width = Math.Min(50, surf.Width - 4);
        int height = Math.Min(14, surf.Height - 4);
        int x0 = (surf.Width - width) / 2;
        int y0 = (surf.Height - height) / 2;
        for (int yy = y0; yy < y0 + height; yy++)
            for (int x = x0; x < x0 + width; x++)
                surf.SetGlyph(x, yy, ' ', Color.White, new Color(10, 10, 10));

        string[] lines =
        {
            "Bindings (default):",
            "F1..F7: Open panels | Z/X/C: Quick menus",
            "F9: Cycle overlay",
            "ESC: Back/Close | Right-Click: Cancel",
            "WASD: Move camera  | Q/E: Change Z",
            "Mouse: Move cursor  | Ctrl+Wheel: Zoom",
            "",
            "See docs/CONTROLS.md for full reference.",
        };
        int y = y0 + 1;
        foreach (var line in lines)
        {
            surf.Print(x0 + 2, y++, line, Color.White);
        }
    }

    public static void DrawPause(ScreenSurface overlay, UiStore ui)
    {
        if (!ui.PauseOpen) return;

        var surf = overlay.Surface;
        const int width = 26;
        const int height = 5;
        int x0 = (surf.Width - width) / 2;
        int y0 = (surf.Height - height) / 2;
        for (int yy = y0; yy < y0 + height; yy++)
            for (int x = x0; x < x0 + width; x++)
                surf.SetGlyph(x, yy, ' ', Color.White, new Color(20, 20, 20));

        surf.Print(x0 + 2, y0 + 1, "== PAUSED ==", Color.Yellow);
        surf.Print(x0 + 2, y0 + 3, "ESC resume | M main menu", Color.White);
    }
}
