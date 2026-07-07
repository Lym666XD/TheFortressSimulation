using System;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal sealed partial class ZonesUI
{
    private void DrawMenuOption(ScreenSurface surface, int x, int y, string text, bool active, Color fg, Color activeBg)
    {
        if (active)
        {
            for (int i = 0; i < 22; i++)
                surface.SetGlyph(x + i, y, ' ', fg, activeBg);
            surface.Print(x, y, text, Color.Yellow);
        }
        else
        {
            surface.Print(x, y, text, fg);
        }
    }

    private void DrawBox(ScreenSurface surface, int x, int y, int width, int height, Color fg, Color bg)
    {
        for (int i = 0; i < width; i++)
            for (int j = 0; j < height; j++)
                surface.SetGlyph(x + i, y + j, ' ', fg, bg);

        for (int i = 1; i < width - 1; i++)
        {
            surface.SetGlyph(x + i, y, '-', fg, bg);
            surface.SetGlyph(x + i, y + height - 1, '-', fg, bg);
        }
        for (int j = 1; j < height - 1; j++)
        {
            surface.SetGlyph(x, y + j, '|', fg, bg);
            surface.SetGlyph(x + width - 1, y + j, '|', fg, bg);
        }
        surface.SetGlyph(x, y, '+', fg, bg);
        surface.SetGlyph(x + width - 1, y, '+', fg, bg);
        surface.SetGlyph(x, y + height - 1, '+', fg, bg);
        surface.SetGlyph(x + width - 1, y + height - 1, '+', fg, bg);
    }

    private Color ParseColor(string hexColor)
    {
        try
        {
            if (hexColor.StartsWith("#"))
            {
                hexColor = hexColor.Substring(1);
            }

            if (hexColor.Length == 6)
            {
                int r = Convert.ToInt32(hexColor.Substring(0, 2), 16);
                int g = Convert.ToInt32(hexColor.Substring(2, 2), 16);
                int b = Convert.ToInt32(hexColor.Substring(4, 2), 16);
                return new Color(r, g, b);
            }
        }
        catch
        {
            // Fallback to white.
        }

        return Color.White;
    }
}
