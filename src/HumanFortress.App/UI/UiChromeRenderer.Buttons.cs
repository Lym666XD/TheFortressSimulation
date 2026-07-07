using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal static partial class UiChromeRenderer
{
    public static void DrawDock(ScreenSurface overlay, UiStore ui)
    {
        var surf = overlay.Surface;
        var buttons = ButtonLayoutCalculator.CalculateDockButtons(surf.Width, surf.Height);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (!UiChromeSlots.TryGetDockSlot(i, out var slot))
                continue;

            var button = buttons[i];
            DrawSquareButton(surf, button.Bounds.X, button.Bounds.Y, button.Label, ui.OpenDrawer == slot.Drawer, button.Bounds.Width);
        }
    }

    public static void DrawQuickIcons(ScreenSurface overlay, UiStore ui)
    {
        var surf = overlay.Surface;
        var buttons = ButtonLayoutCalculator.CalculateQuickButtons(surf.Width, surf.Height);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (!UiChromeSlots.TryGetQuickSlot(i, out var slot))
                continue;

            var button = buttons[i];
            DrawSquareButton(surf, button.Bounds.X, button.Bounds.Y, button.Label, ui.QuickMenu == slot.Menu, button.Bounds.Width);
        }
    }

    private static void DrawSquareButton(ICellSurface surf, int x, int y, string text, bool active, int width)
    {
        var fg = active ? Color.Black : Color.White;
        var bg = active ? Color.Yellow : new Color(40, 40, 40);
        for (int i = 0; i < width; i++)
        {
            surf.SetGlyph(x + i, y, ' ', Color.White, bg);
        }

        int labelX = x + Math.Max(0, (width - text.Length) / 2);
        surf.Print(labelX, y, text, fg);
    }
}
