using HumanFortress.Navigation;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class NavigationOverlaySnapshotBuilder
{
    private static IReadOnlyList<NavigationOverlayCellView> BuildRampMask(
        NavigationManager navigation,
        int z,
        Rectangle viewport)
    {
        var cells = new List<NavigationOverlayCellView>();
        ForEachViewportCell(viewport, (wx, wy) =>
        {
            if (!TryGetNavData(navigation, wx, wy, z, out var nav, out var index))
                return;

            byte mask = nav.UpRampMask[index];
            if (mask == 0)
                return;

            char glyph = CountBits(mask) == 1
                ? FirstBit(mask) switch
                {
                    0 => '^',
                    1 => '/',
                    2 => '>',
                    3 => '\\',
                    4 => 'v',
                    5 => '/',
                    6 => '<',
                    7 => '\\',
                    _ => '+',
                }
                : '*';

            cells.Add(new NavigationOverlayCellView(wx, wy, glyph, Yellow));
        });

        return cells;
    }
}
