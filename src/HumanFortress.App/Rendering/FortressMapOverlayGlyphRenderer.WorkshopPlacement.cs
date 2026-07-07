using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

internal static partial class FortressMapOverlayGlyphRenderer
{
    public static void DrawWorkshopPlacementPreview(
        ScreenSurface mapSurface,
        Point anchor,
        int footprintW,
        int footprintD,
        Rectangle viewport)
    {
        var gold = new Color(255, 230, 0);
        for (int dy = 0; dy < footprintD; dy++)
        {
            for (int dx = 0; dx < footprintW; dx++)
            {
                int x = anchor.X + dx;
                int y = anchor.Y + dy;
                int sx = x - viewport.X;
                int sy = y - viewport.Y;
                if (sx >= 0 && sy >= 0 && sx < mapSurface.Width && sy < mapSurface.Height)
                {
                    mapSurface.SetGlyph(sx, sy, '.', gold, Color.Transparent);
                }
            }
        }

        for (int dx = 0; dx < footprintW; dx++)
        {
            int sx = anchor.X + dx - viewport.X;
            int sy1 = anchor.Y - viewport.Y;
            int sy2 = anchor.Y + footprintD - 1 - viewport.Y;
            if (sx >= 0 && sx < mapSurface.Width)
            {
                if (sy1 >= 0 && sy1 < mapSurface.Height) mapSurface.SetGlyph(sx, sy1, '-', gold, Color.Transparent);
                if (sy2 >= 0 && sy2 < mapSurface.Height) mapSurface.SetGlyph(sx, sy2, '-', gold, Color.Transparent);
            }
        }

        for (int dy = 0; dy < footprintD; dy++)
        {
            int sy = anchor.Y + dy - viewport.Y;
            int sx1 = anchor.X - viewport.X;
            int sx2 = anchor.X + footprintW - 1 - viewport.X;
            if (sy >= 0 && sy < mapSurface.Height)
            {
                if (sx1 >= 0 && sx1 < mapSurface.Width) mapSurface.SetGlyph(sx1, sy, '|', gold, Color.Transparent);
                if (sx2 >= 0 && sx2 < mapSurface.Width) mapSurface.SetGlyph(sx2, sy, '|', gold, Color.Transparent);
            }
        }
    }
}
