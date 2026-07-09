using System.Collections.Generic;
using System.Linq;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Placeables;

internal sealed partial class PlaceableManager
{
    /// <summary>
    /// Get all chunks affected by placeable footprint.
    /// Used for cross-chunk collision detection and placement.
    /// </summary>
    internal static IEnumerable<ChunkKey> GetAffectedChunks(Point position, int z, Footprint footprint)
    {
        var chunks = new HashSet<ChunkKey>();

        for (int dy = 0; dy < footprint.D; dy++)
        {
            for (int dx = 0; dx < footprint.W; dx++)
            {
                int worldX = position.X + dx;
                int worldY = position.Y + dy;

                int chunkX = worldX / Chunk.SIZE_XY;
                int chunkY = worldY / Chunk.SIZE_XY;

                chunks.Add(new ChunkKey(chunkX, chunkY, z));
            }
        }

        return chunks
            .OrderBy(static chunk => chunk.Z)
            .ThenBy(static chunk => chunk.ChunkY)
            .ThenBy(static chunk => chunk.ChunkX)
            .ToArray();
    }
}
