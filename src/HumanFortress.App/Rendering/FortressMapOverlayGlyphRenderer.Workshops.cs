using HumanFortress.App.UI;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

internal static partial class FortressMapOverlayGlyphRenderer
{
    public static void DrawWorkshopsOverlay(
        MapScreenSurface mapSurface,
        SimulationWorkshopDebugData workshops,
        int currentZ,
        Rectangle viewport)
    {
        var border = new Color(255, 230, 0);
        var fill = new Color(255, 230, 0, 90);
        var siteBorder = new Color(255, 140, 0);
        var siteFill = new Color(255, 140, 0, 60);
        var surf = mapSurface.Surface;
        foreach (var workshop in workshops.Workshops)
        {
            if (workshop.Z != currentZ)
                continue;

            var borderColor = workshop.IsSite ? siteBorder : border;
            var fillColor = workshop.IsSite ? siteFill : fill;
            for (int dy = 0; dy < workshop.FootprintD; dy++)
            {
                for (int dx = 0; dx < workshop.FootprintW; dx++)
                {
                    int sx = workshop.X + dx - viewport.X;
                    int sy = workshop.Y + dy - viewport.Y;
                    if (sx >= 0 && sx < mapSurface.Width && sy >= 0 && sy < mapSurface.Height)
                    {
                        mapSurface.SetGlyph(sx, sy, '.', fillColor, Color.Transparent);
                    }
                }
            }

            for (int dx = 0; dx < workshop.FootprintW; dx++)
            {
                int sx = workshop.X + dx - viewport.X;
                int sy1 = workshop.Y - viewport.Y;
                int sy2 = workshop.Y + workshop.FootprintD - 1 - viewport.Y;
                if (sx >= 0 && sx < mapSurface.Width)
                {
                    if (sy1 >= 0 && sy1 < mapSurface.Height) mapSurface.SetGlyph(sx, sy1, '-', borderColor, Color.Transparent);
                    if (sy2 >= 0 && sy2 < mapSurface.Height) mapSurface.SetGlyph(sx, sy2, '-', borderColor, Color.Transparent);
                }
            }

            for (int dy = 0; dy < workshop.FootprintD; dy++)
            {
                int sy = workshop.Y + dy - viewport.Y;
                int sx1 = workshop.X - viewport.X;
                int sx2 = workshop.X + workshop.FootprintW - 1 - viewport.X;
                if (sy >= 0 && sy < mapSurface.Height)
                {
                    if (sx1 >= 0 && sx1 < mapSurface.Width) mapSurface.SetGlyph(sx1, sy, '|', borderColor, Color.Transparent);
                    if (sx2 >= 0 && sx2 < mapSurface.Width) mapSurface.SetGlyph(sx2, sy, '|', borderColor, Color.Transparent);
                }
            }

            if (workshop.IsSite && !string.IsNullOrWhiteSpace(workshop.SiteMaterialProgressText))
            {
                string text = workshop.SiteMaterialProgressText;
                int tx = workshop.X - viewport.X;
                int ty = workshop.Y - viewport.Y - 1;
                if (ty < 0)
                    ty = workshop.Y - viewport.Y;

                if (tx >= 0 && ty >= 0 && tx + text.Length < surf.Width && ty < surf.Height)
                {
                    surf.Print(tx, ty, text, Color.White);
                }
            }
        }
    }

}
