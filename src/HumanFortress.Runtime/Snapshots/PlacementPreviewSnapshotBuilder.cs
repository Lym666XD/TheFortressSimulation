using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class PlacementPreviewSnapshotBuilder
{
    internal static SimulationPlacementPreviewData Build(
        World? world,
        Point first,
        Point second,
        int z,
        SimulationPlacementPreviewMode mode)
    {
        var rect = CreateRectangle(first, second);
        if (world == null)
            return CreateEmpty(rect);

        var cells = new List<PlacementPreviewCellView>();
        for (int wy = rect.Y; wy < rect.Y + rect.Height; wy++)
        {
            for (int wx = rect.X; wx < rect.X + rect.Width; wx++)
            {
                if (!IsEligible(world, wx, wy, z, mode))
                    continue;

                cells.Add(new PlacementPreviewCellView(wx, wy));
            }
        }

        return new SimulationPlacementPreviewData(
            rect.X,
            rect.Y,
            rect.Width,
            rect.Height,
            cells.Count,
            rect.Width * rect.Height,
            cells);
    }

}
