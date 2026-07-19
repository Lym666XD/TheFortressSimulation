using HumanFortress.App.UI;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;
using UiMiningAction = HumanFortress.App.UI.MiningAction;

namespace HumanFortress.App.Rendering;

internal static partial class FortressMapOverlayGlyphRenderer
{
    private static void DrawMiningHighlight(
        ICellSurface surf,
        RuntimeViewportGeometry viewport,
        int currentZ,
        OrderHighlight highlight,
        int screenX,
        int screenY,
        Func<Point, Point, SimulationPlacementPreviewMode, SimulationPlacementPreviewData> placementPreviewProvider)
    {
        var action = ExtractHighlightSuffix(highlight.Kind);
        var previewMode = ToHighlightMiningPreviewMode(action, currentZ, highlight.ZMin);
        if (previewMode.HasValue)
            DrawHighlightDots(surf, viewport, placementPreviewProvider(
                new Point(highlight.Rect.X, highlight.Rect.Y),
                new Point(highlight.Rect.X + highlight.Rect.Width - 1, highlight.Rect.Y + highlight.Rect.Height - 1),
                previewMode.Value),
                '\u00B7');

        if (action == nameof(UiMiningAction.DigStairwell))
        {
            DrawStairwellLabel(surf, currentZ, highlight, screenX, screenY);
        }
    }

    private static void DrawConstructionHighlight(
        ICellSurface surf,
        RuntimeViewportGeometry viewport,
        OrderHighlight highlight,
        Func<Point, Point, SimulationPlacementPreviewMode, SimulationPlacementPreviewData> placementPreviewProvider)
    {
        var shape = ExtractHighlightSuffix(highlight.Kind);
        var previewMode = ToHighlightConstructionPreviewMode(shape);
        if (previewMode.HasValue)
            DrawHighlightDots(surf, viewport, placementPreviewProvider(
                new Point(highlight.Rect.X, highlight.Rect.Y),
                new Point(highlight.Rect.X + highlight.Rect.Width - 1, highlight.Rect.Y + highlight.Rect.Height - 1),
                previewMode.Value),
                '.');
    }

    private static void DrawStairwellLabel(ICellSurface surf, int currentZ, OrderHighlight highlight, int screenX, int screenY)
    {
        string? label = currentZ == highlight.ZMin
            ? "Top"
            : currentZ == highlight.ZMax
                ? "Bottom"
                : null;
        if (label == null)
            return;

        int sx = screenX + 1, sy = screenY + 1;
        if (sx >= 0 && sx < surf.Width && sy >= 0 && sy < surf.Height)
            surf.Print(sx, sy, label, Color.Cyan);
    }

    private static void DrawHighlightDots(ICellSurface surf, RuntimeViewportGeometry viewport, SimulationPlacementPreviewData preview, char glyph)
    {
        var dotFg = new Color(255, 230, 0);
        foreach (var cell in preview.Cells)
        {
            FortressViewportDrawing.SetWorldCellGlyph(surf, viewport, cell.X, cell.Y, glyph, dotFg);
        }
    }

    private static string ExtractHighlightSuffix(string kind)
    {
        int idx = kind.IndexOf(':');
        return idx >= 0 && idx + 1 < kind.Length ? kind.Substring(idx + 1) : string.Empty;
    }
}
