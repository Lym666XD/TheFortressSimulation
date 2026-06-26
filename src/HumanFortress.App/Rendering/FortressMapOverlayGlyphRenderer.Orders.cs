using System;
using HumanFortress.App.UI;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

internal static partial class FortressMapOverlayGlyphRenderer
{
    public static void DrawOrderHighlights(
        ScreenSurface mapSurface,
        UiStore ui,
        Point camera,
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
            int x0 = h.Rect.X - camera.X;
            int y0 = h.Rect.Y - camera.Y;
            int x1 = x0 + h.Rect.Width - 1;
            int y1 = y0 + h.Rect.Height - 1;
            if (!isMining)
            {
                DrawOrderHighlightBorder(surf, x0, y0, x1, y1, flash);
            }

            if (isMining)
            {
                DrawMiningHighlight(surf, camera, currentZ, h, x0, y0, placementPreviewProvider);
            }

            if (h.Kind.StartsWith("construction", StringComparison.OrdinalIgnoreCase))
            {
                DrawConstructionHighlight(surf, camera, h, placementPreviewProvider);
            }
        }
    }
}
