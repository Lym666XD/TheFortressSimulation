using HumanFortress.App.UI;
using HumanFortress.Contracts.Runtime;
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
        RuntimeViewportGeometry viewport)
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
                    FortressViewportDrawing.SetWorldCellGlyph(
                        surf,
                        viewport,
                        workshop.X + dx,
                        workshop.Y + dy,
                        '.',
                        fillColor);
                }
            }

            for (int dx = 0; dx < workshop.FootprintW; dx++)
            {
                FortressViewportDrawing.SetWorldCellGlyph(surf, viewport, workshop.X + dx, workshop.Y, '-', borderColor);
                FortressViewportDrawing.SetWorldCellGlyph(surf, viewport, workshop.X + dx, workshop.Y + workshop.FootprintD - 1, '-', borderColor);
            }

            for (int dy = 0; dy < workshop.FootprintD; dy++)
            {
                FortressViewportDrawing.SetWorldCellGlyph(surf, viewport, workshop.X, workshop.Y + dy, '|', borderColor);
                FortressViewportDrawing.SetWorldCellGlyph(surf, viewport, workshop.X + workshop.FootprintW - 1, workshop.Y + dy, '|', borderColor);
            }

            if (workshop.IsSite && !string.IsNullOrWhiteSpace(workshop.SiteMaterialProgressText))
            {
                string text = workshop.SiteMaterialProgressText;
                if (!FortressViewportDrawing.TryGetLocalPosition(viewport, workshop.X, workshop.Y, out var local))
                    continue;
                int tx = local.X;
                int ty = local.Y - 1;
                if (ty < 0)
                    ty = local.Y;

                if (tx >= 0 && ty >= 0 && tx + text.Length < surf.Width && ty < surf.Height)
                {
                    surf.Print(tx, ty, text, Color.White);
                }
            }
        }
    }

}
