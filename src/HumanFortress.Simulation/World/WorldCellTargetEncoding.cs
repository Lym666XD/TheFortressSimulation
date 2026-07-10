using HumanFortress.Core.Simulation;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.World;

internal readonly struct WorldCellTarget
{
    internal WorldCellTarget(ChunkKey chunkKey, int localIndex)
    {
        ChunkKey = chunkKey;
        LocalIndex = localIndex;
    }

    internal ChunkKey ChunkKey { get; }

    internal int LocalIndex { get; }

    internal DiffTarget ToDiffTarget(int entityId = -1)
    {
        return DiffTargetEncoding.ForEncodedTarget(
            DiffTargetEncoding.EncodeChunkId(ChunkKey.ChunkX, ChunkKey.ChunkY, ChunkKey.Z),
            LocalIndex,
            entityId);
    }

    internal DiffTarget ToDiffTarget(Guid entityGuid)
    {
        return DiffTargetEncoding.ForEncodedTarget(
            DiffTargetEncoding.EncodeChunkId(ChunkKey.ChunkX, ChunkKey.ChunkY, ChunkKey.Z),
            LocalIndex,
            entityGuid);
    }
}

internal static class WorldCellTargetEncoding
{
    internal static bool TryEncode(Point cell, int z, out WorldCellTarget target)
    {
        return TryEncode(cell.X, cell.Y, z, out target);
    }

    internal static bool TryEncode(int worldX, int worldY, int z, out WorldCellTarget target)
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
