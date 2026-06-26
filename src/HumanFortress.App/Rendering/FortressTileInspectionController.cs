using HumanFortress.App.UI;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

internal sealed class FortressTileInspectionController
{
    public bool IsOpen { get; private set; }
    public Point WorldPosition { get; private set; } = new(0, 0);
    public int Z { get; private set; }

    public void Open(Point worldPosition, int z)
    {
        WorldPosition = worldPosition;
        Z = z;
        IsOpen = true;
    }

    public void Hide()
    {
        IsOpen = false;
    }

    public void RenderPopup(UiOverlaySurface? uiSurface, SimulationTileInspectionData tile)
    {
        if (!IsOpen || uiSurface == null)
            return;

        FortressTilePopupRenderer.Render(uiSurface, tile);
    }
}
