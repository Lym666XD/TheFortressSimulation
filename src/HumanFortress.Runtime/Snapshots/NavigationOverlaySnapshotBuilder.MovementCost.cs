using HumanFortress.Navigation.Implementation;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class NavigationOverlaySnapshotBuilder
{
    private static IReadOnlyList<NavigationOverlayCellView> BuildMovementCost(
        NavigationManager navigation,
        NavigationTuning tuning,
        int z,
        Rectangle viewport)
    {
        var cells = new List<NavigationOverlayCellView>();
        ForEachViewportCell(viewport, (wx, wy) =>
        {
            if (!TryGetNavData(navigation, wx, wy, z, out var nav, out var index))
                return;

            var cost = nav.NavCost[index];
            if (cost == ushort.MaxValue)
            {
                cells.Add(new NavigationOverlayCellView(wx, wy, 'X', DarkRed));
                return;
            }

            double ratio = (double)cost / Math.Max(1, (int)tuning.BaseCost);
            int bin = (int)Math.Clamp(Math.Round(ratio * 10.0), 0, 35);
            char glyph = bin < 10 ? (char)('0' + bin) : (char)('A' + (bin - 10));
            string color = ratio <= 1.0
                ? Green
                : ratio <= 1.5
                    ? YellowGreen
                    : ratio <= 2.0
                        ? Yellow
                        : ratio <= 3.0
                            ? Orange
                            : Red;

            cells.Add(new NavigationOverlayCellView(wx, wy, glyph, color));
        });

        return cells;
    }
}
