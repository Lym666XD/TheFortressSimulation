using HumanFortress.App.Rendering;
using HumanFortress.App.UI;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Simulation.World;
using HumanFortress.WorldGen;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

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

    public void RenderPopup(UiOverlaySurface? uiSurface, FortressMap? fortressMap, World? world, IRuntimeGeologyCatalog? geology)
    {
        if (!IsOpen || uiSurface == null)
            return;

        FortressTilePopupRenderer.Render(uiSurface, fortressMap, world, WorldPosition, Z, geology);
    }

}
