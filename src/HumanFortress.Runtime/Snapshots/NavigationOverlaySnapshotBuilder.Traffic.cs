using HumanFortress.Contracts.Navigation;
using HumanFortress.Navigation;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class NavigationOverlaySnapshotBuilder
{
    private static IReadOnlyList<NavigationOverlayCellView> BuildTraffic(
        NavigationManager navigation,
        int z,
        Rectangle viewport)
    {
        var cells = new List<NavigationOverlayCellView>();
        ForEachViewportCell(viewport, (wx, wy) =>
        {
            if (!navigation.Source.TryGetTile(new Point3(wx, wy, z), out var tile))
                return;

            var level = (tile.MetaBits >> 4) & 0x3;
            if (level == 1)
                cells.Add(new NavigationOverlayCellView(wx, wy, '+', Green));
            else if (level == 2)
                cells.Add(new NavigationOverlayCellView(wx, wy, '-', Yellow));
            else if (level == 3)
                cells.Add(new NavigationOverlayCellView(wx, wy, 'R', Red));
        });

        return cells;
    }
}
