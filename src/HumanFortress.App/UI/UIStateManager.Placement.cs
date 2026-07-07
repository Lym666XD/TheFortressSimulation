using HumanFortress.App;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal sealed partial class UIStateManager
{
    /// <summary>
    /// Start placement mode for zones/stockpiles/orders.
    /// </summary>
    public void StartPlacement(PlacementMode mode, int z)
    {
        Logger.Log($"[UIStateManager] StartPlacement: mode={mode} z={z}");
        _store.StartPlacement(mode, z);
    }

    /// <summary>
    /// Cancel placement and return to global context.
    /// </summary>
    public void CancelPlacement()
    {
        Logger.Log("[UIStateManager] CancelPlacement");
        _store.CancelPlacement();
    }

    /// <summary>
    /// Set hover tile for cursor preview.
    /// </summary>
    public void SetHoverTile(Point tile)
    {
        _store.SetHover(tile);
    }
}
