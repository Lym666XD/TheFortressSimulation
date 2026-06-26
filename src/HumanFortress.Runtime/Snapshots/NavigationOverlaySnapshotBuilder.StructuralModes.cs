using HumanFortress.Contracts.Navigation;
using HumanFortress.Navigation;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class NavigationOverlaySnapshotBuilder
{
    private static IReadOnlyList<NavigationOverlayCellView> BuildConnectivity(
        NavigationManager navigation,
        int z,
        Rectangle viewport)
    {
        var cells = new List<NavigationOverlayCellView>();
        ForEachViewportCell(viewport, (wx, wy) =>
        {
            if (!navigation.Source.IsValid(new Point3(wx, wy, z)))
                return;

            if (PositiveModulo(wx, ChunkNavData.ChunkSize) == 0
                || PositiveModulo(wy, ChunkNavData.ChunkSize) == 0)
            {
                cells.Add(new NavigationOverlayCellView(wx, wy, ':', DarkGray));
            }

            if (PositiveModulo(wx, ChunkNavData.ChunkSize) != 16
                || PositiveModulo(wy, ChunkNavData.ChunkSize) != 16)
            {
                return;
            }

            var key = new ChunkKey(wx / ChunkNavData.ChunkSize, wy / ChunkNavData.ChunkSize, z);
            var nav = navigation.GetNavData(key);
            if (nav == null)
                return;

            var version = nav.ConnectivityVersion.ToString();
            for (int i = 0; i < version.Length && wx + i < viewport.X + viewport.Width; i++)
            {
                if (navigation.Source.IsValid(new Point3(wx + i, wy, z)))
                    cells.Add(new NavigationOverlayCellView(wx + i, wy, version[i], Cyan));
            }
        });

        return cells;
    }

}
