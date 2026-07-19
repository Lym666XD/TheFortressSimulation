using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.World;
using SimulationWorld = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Topology;

internal sealed partial class TopologyChangeTransaction
{
    internal static TopologyChangeDescription ApplyTerrain(
        SimulationWorld world,
        ChunkKey chunkKey,
        int localX,
        int localY,
        TileBase newTile,
        ulong tick)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (localX < 0 || localX >= Chunk.SIZE_XY || localY < 0 || localY >= Chunk.SIZE_XY)
            throw new ArgumentOutOfRangeException(nameof(localX));

        lock (world.TopologyLock)
        {
            var chunk = world.GetChunk(chunkKey)
                ?? throw new InvalidOperationException($"Terrain topology chunk {chunkKey} is not loaded.");
            var localIndex = Chunk.LocalIndex(localX, localY);
            var oldTile = chunk.GetTile(localX, localY);
            var topologyChanged = oldTile.Kind != newTile.Kind
                || oldTile.IsWalkable != newTile.IsWalkable
                || oldTile.IsStandable != newTile.IsStandable
                || oldTile.IsFlyable != newTile.IsFlyable;

            if (!topologyChanged)
            {
                chunk.SetTile(localX, localY, newTile, tick);
                return new TopologyChangeDescription(
                    TopologyChangeKind.Terrain,
                    subjectId: null,
                    Array.Empty<TopologyAffectedChunk>());
            }

            var transaction = new TopologyChangeTransaction(
                world,
                tick,
                TopologyChangeKind.Terrain,
                subjectId: null,
                applyPreparedWrites: () =>
                {
                    chunk.ReplaceTileForTopologyTransaction(localIndex, newTile, tick);
                    return true;
                });

            var worldX = chunkKey.ChunkX * Chunk.SIZE_XY + localX;
            var worldY = chunkKey.ChunkY * Chunk.SIZE_XY + localY;
            transaction.TrackChangedCellAndDependencies(worldX, worldY, chunkKey.Z);
            return transaction.Commit();
        }
    }
}
