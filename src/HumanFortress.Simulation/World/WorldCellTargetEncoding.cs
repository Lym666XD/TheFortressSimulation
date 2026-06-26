using HumanFortress.Core.Simulation;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.World;

internal readonly struct WorldCellTarget
{
    public WorldCellTarget(ChunkKey chunkKey, int localIndex)
    {
        ChunkKey = chunkKey;
        LocalIndex = localIndex;
    }

    public ChunkKey ChunkKey { get; }

    public int LocalIndex { get; }

    public DiffTarget ToDiffTarget(int entityId = -1)
    {
        return new DiffTarget(
            DiffTargetEncoding.EncodeChunkId(ChunkKey.ChunkX, ChunkKey.ChunkY, ChunkKey.Z),
            LocalIndex,
            entityId);
    }
}

internal static class WorldCellTargetEncoding
{
    public static bool TryEncode(Point cell, int z, out WorldCellTarget target)
    {
        return TryEncode(cell.X, cell.Y, z, out target);
    }

    public static bool TryEncode(int worldX, int worldY, int z, out WorldCellTarget target)
    {
        target = default;
        if (worldX < 0 || worldY < 0 || z < 0) return false;

        int chunkX = worldX / Chunk.SIZE_XY;
        int chunkY = worldY / Chunk.SIZE_XY;
        int localX = worldX % Chunk.SIZE_XY;
        int localY = worldY % Chunk.SIZE_XY;
        target = new WorldCellTarget(new ChunkKey(chunkX, chunkY, z), Chunk.LocalIndex(localX, localY));
        return true;
    }
}
