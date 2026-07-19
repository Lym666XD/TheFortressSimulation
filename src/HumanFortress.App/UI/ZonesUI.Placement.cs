using HumanFortress.App.Rendering;
using HumanFortress.Contracts.Runtime;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal sealed partial class ZonesUI
{
    /// <summary>
    /// Draw placement mode prompt.
    /// </summary>
    public void DrawPlacementMode(ScreenSurface surface, UiStore ui, Point mouseWorld)
    {
        if (ui.PlaceMode != PlacementMode.ZoneFirstCorner &&
            ui.PlaceMode != PlacementMode.ZoneSecondCorner &&
            ui.PlaceMode != PlacementMode.ZoneDelete)
            return;

        var surf = surface.Surface;
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;

        if (ui.PlaceMode == PlacementMode.ZoneFirstCorner)
        {
            int x = 2;
            int y = surf.Height - 5;
            DrawBox(surface, x, y, 50, 3, fg, bg);
            surface.Print(x + 2, y + 1, "Zone Placement: Select first corner", Color.Yellow);
        }
        else if (ui.PlaceMode == PlacementMode.ZoneSecondCorner)
        {
            int x = 2;
            int y = surf.Height - 5;
            DrawBox(surface, x, y, 50, 3, fg, bg);
            surface.Print(x + 2, y + 1, "Zone Placement: Select second corner", Color.Yellow);
        }
        else if (ui.PlaceMode == PlacementMode.ZoneDelete)
        {
            int x = 2;
            int y = surf.Height - 5;
            DrawBox(surface, x, y, 50, 3, fg, bg);
            surface.Print(x + 2, y + 1, "Click on a zone cell to delete it", Color.Red);
        }
    }

    /// <summary>
    /// Render placement preview on map (only when zone menu is open).
    /// </summary>
    public void RenderPlacementPreview(MapScreenSurface mapSurface, Point first, Point second, RuntimeViewportGeometry viewport, bool show)
    {
        if (!show) return;

        var rect = Rectangle.GetUnion(new Rectangle(first, 1, 1), new Rectangle(second, 1, 1));
        var gold = new Color(255, 230, 0);
        for (int wx = rect.X; wx < rect.X + rect.Width; wx++)
        {
            for (int wy = rect.Y; wy < rect.Y + rect.Height; wy++)
            {
                FortressViewportDrawing.SetWorldCellGlyph(
                    mapSurface.Surface,
                    viewport,
                    wx,
                    wy,
                    '.',
                    gold);
            }
        }
    }
}
