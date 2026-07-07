using HumanFortress.Contracts.Navigation;
using HumanFortress.Navigation.Implementation;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class NavigationOverlaySnapshotBuilder
{
    private static bool TryGetNavData(
        NavigationManager navigation,
        int worldX,
        int worldY,
        int z,
        out ChunkNavData nav,
        out int index)
    {
        nav = null!;
        index = 0;
        if (!navigation.Source.IsValid(new Point3(worldX, worldY, z)))
            return false;

        int chunkX = worldX / ChunkNavData.ChunkSize;
        int chunkY = worldY / ChunkNavData.ChunkSize;
        nav = navigation.GetNavData(new ChunkKey(chunkX, chunkY, z))!;
        if (nav == null)
            return false;

        int localX = PositiveModulo(worldX, ChunkNavData.ChunkSize);
        int localY = PositiveModulo(worldY, ChunkNavData.ChunkSize);
        index = localY * ChunkNavData.ChunkSize + localX;
        return true;
    }

    private static void ForEachViewportCell(Rectangle viewport, Action<int, int> visit)
    {
        for (int y = 0; y < viewport.Height; y++)
        {
            for (int x = 0; x < viewport.Width; x++)
            {
                visit(viewport.X + x, viewport.Y + y);
            }
        }
    }

    private static int PositiveModulo(int value, int divisor)
    {
        return ((value % divisor) + divisor) % divisor;
    }

    private static char GetFlowArrow(int dx, int dy)
    {
        if (Math.Abs(dx) > Math.Abs(dy))
            return dx > 0 ? '>' : '<';

        if (dy != 0)
            return dy > 0 ? 'v' : '^';

        return '.';
    }

    private static int CountBits(byte value)
    {
        int count = 0;
        while (value != 0)
        {
            value &= (byte)(value - 1);
            count++;
        }

        return count;
    }

    private static byte FirstBit(byte value)
    {
        for (byte i = 0; i < 8; i++)
        {
            if ((value & (1 << i)) != 0)
                return i;
        }

        return 0;
    }
}
