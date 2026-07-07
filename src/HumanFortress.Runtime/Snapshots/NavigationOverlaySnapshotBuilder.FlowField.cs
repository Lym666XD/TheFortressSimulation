using HumanFortress.Contracts.Navigation;
using HumanFortress.Navigation.Implementation;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class NavigationOverlaySnapshotBuilder
{
    private static IReadOnlyList<NavigationOverlayCellView> BuildFlowField(
        NavigationManager navigation,
        int z,
        Rectangle viewport,
        Point? selectedTarget)
    {
        if (selectedTarget == null)
            return Array.Empty<NavigationOverlayCellView>();

        var target = selectedTarget.Value;
        var cells = new List<NavigationOverlayCellView>();
        ForEachViewportCell(viewport, (wx, wy) =>
        {
            if (!TryGetNavData(navigation, wx, wy, z, out var nav, out var index))
                return;

            if ((nav.NavMask[index] & (byte)NavCapability.Walk) == 0)
                return;

            int dx = target.X - wx;
            int dy = target.Y - wy;
            int distance = Math.Abs(dx) + Math.Abs(dy);
            string color = distance < 10 ? Green : distance < 20 ? Yellow : distance < 30 ? Orange : Red;
            cells.Add(new NavigationOverlayCellView(wx, wy, GetFlowArrow(dx, dy), color));
        });

        return cells;
    }
}
