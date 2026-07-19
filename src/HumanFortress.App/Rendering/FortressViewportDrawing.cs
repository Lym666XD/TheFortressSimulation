using HumanFortress.Contracts.Runtime;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

internal static class FortressViewportDrawing
{
    internal static bool TryGetLocalPosition(
        RuntimeViewportGeometry viewport,
        int worldX,
        int worldY,
        out Point localPosition)
    {
        if (!RuntimeViewportGeometryMath.TryWorldToLocal(
                viewport,
                new RuntimePoint(worldX, worldY),
                out var local))
        {
            localPosition = default;
            return false;
        }

        localPosition = new Point(local.X, local.Y);
        return true;
    }

    internal static void SetWorldCellGlyph(
        ICellSurface surface,
        RuntimeViewportGeometry viewport,
        int worldX,
        int worldY,
        int glyph,
        Color foreground,
        Color? background = null,
        bool fillZoomCell = false)
    {
        if (!RuntimeViewportGeometryMath.TryGetWorldCellLocalRect(
                viewport,
                new RuntimePoint(worldX, worldY),
                out var rect))
        {
            return;
        }

        int width = fillZoomCell ? rect.Width : Math.Min(1, rect.Width);
        int height = fillZoomCell ? rect.Height : Math.Min(1, rect.Height);
        for (int y = rect.Y; y < rect.Y + height; y++)
        {
            for (int x = rect.X; x < rect.X + width; x++)
            {
                surface.SetGlyph(
                    x,
                    y,
                    glyph,
                    foreground,
                    background ?? Color.Transparent);
            }
        }
    }
}
