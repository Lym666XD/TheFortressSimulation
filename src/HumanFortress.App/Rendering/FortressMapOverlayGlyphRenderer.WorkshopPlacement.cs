using HumanFortress.Contracts.Runtime;
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
        RuntimeViewportGeometry viewport)
    {
        var gold = new Color(255, 230, 0);
        for (int dy = 0; dy < footprintD; dy++)
        {
            for (int dx = 0; dx < footprintW; dx++)
            {
                int x = anchor.X + dx;
                int y = anchor.Y + dy;
                FortressViewportDrawing.SetWorldCellGlyph(mapSurface.Surface, viewport, x, y, '.', gold);
            }
        }

        for (int dx = 0; dx < footprintW; dx++)
        {
            FortressViewportDrawing.SetWorldCellGlyph(mapSurface.Surface, viewport, anchor.X + dx, anchor.Y, '-', gold);
            FortressViewportDrawing.SetWorldCellGlyph(mapSurface.Surface, viewport, anchor.X + dx, anchor.Y + footprintD - 1, '-', gold);
        }

        for (int dy = 0; dy < footprintD; dy++)
        {
            FortressViewportDrawing.SetWorldCellGlyph(mapSurface.Surface, viewport, anchor.X, anchor.Y + dy, '|', gold);
            FortressViewportDrawing.SetWorldCellGlyph(mapSurface.Surface, viewport, anchor.X + footprintW - 1, anchor.Y + dy, '|', gold);
        }
    }
}
