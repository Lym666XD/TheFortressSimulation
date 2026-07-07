using HumanFortress.App.UI;
using HumanFortress.App.UI.Selection;
using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

internal sealed class FortressViewState
{
    public MapScreenSurface? MapSurface { get; private set; }
    public UiOverlaySurface? UiSurface { get; private set; }
    public ISelectionTool? SelectionTool { get; private set; }

    public bool HasMapSurface => MapSurface != null;

    public void Apply(MapScreenSurface mapSurface, UiOverlaySurface uiSurface, ISelectionTool selectionTool)
    {
        MapSurface = mapSurface ?? throw new ArgumentNullException(nameof(mapSurface));
        UiSurface = uiSurface ?? throw new ArgumentNullException(nameof(uiSurface));
        SelectionTool = selectionTool ?? throw new ArgumentNullException(nameof(selectionTool));
    }

    public int MapWidthOr(int fallback)
    {
        return MapSurface?.Surface.Width ?? fallback;
    }

    public int MapHeightOr(int fallback)
    {
        return MapSurface?.Surface.Height ?? fallback;
    }

    public int UiWidthOr(int fallback)
    {
        return UiSurface?.Surface.Width ?? fallback;
    }

    public int UiHeightOr(int fallback)
    {
        return UiSurface?.Surface.Height ?? fallback;
    }

    public Point MapPositionOr(Point fallback)
    {
        return MapSurface?.Position ?? fallback;
    }

    public bool TryToMapLocal(Point surfaceLocal, out Point mapLocal)
    {
        mapLocal = default;
        var mapSurface = MapSurface;
        if (mapSurface == null || UiSurface == null)
            return false;

        mapLocal = new Point(surfaceLocal.X - mapSurface.Position.X, surfaceLocal.Y - mapSurface.Position.Y);
        return mapLocal.X >= 0
            && mapLocal.Y >= 0
            && mapLocal.X < mapSurface.Surface.Width
            && mapLocal.Y < mapSurface.Surface.Height;
    }
}
