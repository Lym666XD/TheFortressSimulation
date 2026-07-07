using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

internal static partial class FortressMapOverlayGlyphRenderer
{
    private static void DrawOrderHighlightBorder(ICellSurface surf, int x0, int y0, int x1, int y1, bool flash)
    {
        var fg = flash ? Color.Yellow : Color.Orange;
        for (int x = x0; x <= x1; x++)
        {
            if (x >= 0 && x < surf.Width)
            {
                if (y0 >= 0 && y0 < surf.Height) { surf.SetGlyph(x, y0, '-', fg, Color.Transparent); }
                if (y1 >= 0 && y1 < surf.Height) { surf.SetGlyph(x, y1, '-', fg, Color.Transparent); }
            }
        }

        for (int y = y0; y <= y1; y++)
        {
            if (y >= 0 && y < surf.Height)
            {
                if (x0 >= 0 && x0 < surf.Width) { surf.SetGlyph(x0, y, '|', fg, Color.Transparent); }
                if (x1 >= 0 && x1 < surf.Width) { surf.SetGlyph(x1, y, '|', fg, Color.Transparent); }
            }
        }

        if (x0 >= 0 && x0 < surf.Width && y0 >= 0 && y0 < surf.Height) { surf.SetGlyph(x0, y0, '+', fg, Color.Transparent); }
        if (x1 >= 0 && x1 < surf.Width && y0 >= 0 && y0 < surf.Height) { surf.SetGlyph(x1, y0, '+', fg, Color.Transparent); }
        if (x0 >= 0 && x0 < surf.Width && y1 >= 0 && y1 < surf.Height) { surf.SetGlyph(x0, y1, '+', fg, Color.Transparent); }
        if (x1 >= 0 && x1 < surf.Width && y1 >= 0 && y1 < surf.Height) { surf.SetGlyph(x1, y1, '+', fg, Color.Transparent); }
    }
}
