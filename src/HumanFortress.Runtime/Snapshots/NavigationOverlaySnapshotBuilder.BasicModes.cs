using HumanFortress.Contracts.Navigation;
using HumanFortress.Navigation.Implementation;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class NavigationOverlaySnapshotBuilder
{
    private static IReadOnlyList<NavigationOverlayCellView> BuildWalkability(
        NavigationManager navigation,
        int z,
        Rectangle viewport)
    {
        var cells = new List<NavigationOverlayCellView>();
        ForEachViewportCell(viewport, (wx, wy) =>
        {
            if (!TryGetNavData(navigation, wx, wy, z, out var nav, out var index))
                return;

            var caps = (NavCapability)nav.NavMask[index];
            if ((caps & NavCapability.Walk) != 0)
                cells.Add(new NavigationOverlayCellView(wx, wy, '.', Green));
            else if ((caps & NavCapability.Swim) != 0)
                cells.Add(new NavigationOverlayCellView(wx, wy, '~', Blue));
            else if ((caps & NavCapability.Fly) != 0)
                cells.Add(new NavigationOverlayCellView(wx, wy, 'o', Gray));
            else
                cells.Add(new NavigationOverlayCellView(wx, wy, 'X', DarkRed));
        });

        return cells;
    }
}
