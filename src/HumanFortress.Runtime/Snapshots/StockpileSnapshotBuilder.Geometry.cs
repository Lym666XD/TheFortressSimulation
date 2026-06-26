using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class StockpileSnapshotBuilder
{
    private static bool IntersectsViewport(ChunkKey chunkKey, Rectangle viewport)
    {
        int chunkX = chunkKey.ChunkX * Chunk.SIZE_XY;
        int chunkY = chunkKey.ChunkY * Chunk.SIZE_XY;
        return chunkX < viewport.X + viewport.Width
            && chunkX + Chunk.SIZE_XY > viewport.X
            && chunkY < viewport.Y + viewport.Height
            && chunkY + Chunk.SIZE_XY > viewport.Y;
    }

    private static bool Contains(Rectangle viewport, int worldX, int worldY)
    {
        return worldX >= viewport.X
            && worldX < viewport.X + viewport.Width
            && worldY >= viewport.Y
            && worldY < viewport.Y + viewport.Height;
    }
}
