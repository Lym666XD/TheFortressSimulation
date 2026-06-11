using System;

namespace HumanFortress.Core.Simulation;

public static class DiffTargetEncoding
{
    public const int ChunkSizeXY = 32;

    public static DiffTarget ForWorldCell(int worldX, int worldY, int z, int entityId = -1)
    {
        int chunkX = worldX / ChunkSizeXY;
        int chunkY = worldY / ChunkSizeXY;
        int localX = worldX % ChunkSizeXY;
        int localY = worldY % ChunkSizeXY;
        return ForChunkLocal(chunkX, chunkY, z, localX, localY, entityId);
    }

    public static DiffTarget ForChunkLocal(int chunkX, int chunkY, int z, int localX, int localY, int entityId = -1)
    {
        return new DiffTarget(EncodeChunkId(chunkX, chunkY, z), LocalIndex(localX, localY), entityId);
    }

    public static int EncodeChunkId(int chunkX, int chunkY, int z)
    {
        return ((z & 0x3FF) << 20) | ((chunkX & 0x3FF) << 10) | (chunkY & 0x3FF);
    }

    public static (int ChunkX, int ChunkY, int Z) DecodeChunkId(int chunkId)
    {
        int z = (chunkId >> 20) & 0x3FF;
        int chunkX = (chunkId >> 10) & 0x3FF;
        int chunkY = chunkId & 0x3FF;
        return (chunkX, chunkY, z);
    }

    public static int LocalIndex(int localX, int localY)
    {
        return localY * ChunkSizeXY + localX;
    }

    public static (int LocalX, int LocalY) DecodeLocalIndex(int localIndex)
    {
        int localX = localIndex % ChunkSizeXY;
        int localY = localIndex / ChunkSizeXY;
        return (localX, localY);
    }

    public static uint EntityId(Guid value)
    {
        var bytes = value.ToByteArray();
        return BitConverter.ToUInt32(bytes, 0);
    }

    public static int SignedEntityId(Guid value)
    {
        return unchecked((int)EntityId(value));
    }
}
