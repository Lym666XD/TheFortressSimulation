using SadRogue.Primitives;

namespace HumanFortress.Simulation.Orders;

internal sealed partial class MiningSystem
{
    private bool IsCanceled(int x, int y, int z)
    {
        if (_cancels.Count == 0)
            return false;

        var point = new Point(x, y);
        foreach (var cancellation in _cancels)
        {
            if (z < cancellation.ZMin || z > cancellation.ZMax)
                continue;
            if (cancellation.Rect.Contains(point))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Query if a tile is covered by any active cancellation region.
    /// Read-safe; consumed by job executors to drop or abort work reliably.
    /// </summary>
    internal bool IsTileCanceled(int x, int y, int z)
    {
        return IsCanceled(x, y, z);
    }
}
