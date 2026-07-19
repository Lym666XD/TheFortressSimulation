using System;
using HumanFortress.App.UI;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

internal static partial class FortressMapOverlayGlyphRenderer
{
    public static void DrawOrderHighlights(
        ScreenSurface mapSurface,
        UiStore ui,
        RuntimeViewportGeometry viewport,
        int currentZ,
        ulong tick,
        Func<Point, Point, SimulationPlacementPreviewMode, SimulationPlacementPreviewData> placementPreviewProvider)
    {
        var surf = mapSurface.Surface;
        var highlights = ui.GetHighlights();
        if (highlights.Count == 0) return;
        bool flash = ((tick / 10) % 2) == 0;
        foreach (var h in highlights)
        {
            if (currentZ < h.ZMin || currentZ > h.ZMax) continue;
            bool isMining = h.Kind.StartsWith("mining", StringComparison.OrdinalIgnoreCase);
            if (!RuntimeViewportGeometryMath.TryGetWorldCellLocalRect(
                    viewport,
                    new RuntimePoint(h.Rect.X, h.Rect.Y),
                    out var firstCell))
                continue;
            int x0 = firstCell.X;
            int y0 = firstCell.Y;
            int x1 = x0 + (h.Rect.Width * viewport.ZoomLevel) - 1;
            int y1 = y0 + (h.Rect.Height * viewport.ZoomLevel) - 1;
            if (!isMining)
            {
                DrawOrderHighlightBorder(surf, x0, y0, x1, y1, flash);
            }

            if (isMining)
            {
                DrawMiningHighlight(surf, viewport, currentZ, h, x0, y0, placementPreviewProvider);
            }

            if (h.Kind.StartsWith("construction", StringComparison.OrdinalIgnoreCase))
            {
                DrawConstructionHighlight(surf, viewport, h, placementPreviewProvider);
            }
        }
    }
}
