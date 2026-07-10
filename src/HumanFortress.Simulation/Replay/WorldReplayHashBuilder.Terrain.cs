using HumanFortress.Core.Determinism;
using SimulationWorld = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Replay;

internal static partial class WorldReplayHashBuilder
{
    private static string BuildTerrainHash(SimulationWorld world)
    {
        return ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("world.terrain.snapshot.v1");
            hash.AddInt32(world.SizeInChunks);
            hash.AddInt32(world.MaxZ);
            AddTerrainHash(hash, world);
        });
    }

    private static void AddTerrainHash(ReplayHashBuilder hash, SimulationWorld world)
    {
        var chunks = world.GetAllChunks()
            .OrderBy(chunk => chunk.Key.Z)
            .ThenBy(chunk => chunk.Key.ChunkY)
            .ThenBy(chunk => chunk.Key.ChunkX)
            .ToArray();

        hash.AddInt32(chunks.Length);
        foreach (var chunk in chunks)
        {
            hash.AddInt32(chunk.Key.ChunkX);
            hash.AddInt32(chunk.Key.ChunkY);
            hash.AddInt32(chunk.Key.Z);

            var tiles = chunk.GetTilesCopy();
            hash.AddInt32(tiles.Length);
            for (var i = 0; i < tiles.Length; i++)
            {
                var tile = tiles[i];
                hash.AddInt32(i);
                hash.AddInt32(tile.GeoMatId);
                hash.AddInt32(tile.TerrainBits);
                hash.AddByte(tile.SurfaceBits);
                hash.AddByte(tile.FluidKind);
                hash.AddByte(tile.FluidDepth);
                hash.AddByte(tile.MetaBits);
                hash.AddInt32(tile.TrafficCost);
            }
        }
    }
}
