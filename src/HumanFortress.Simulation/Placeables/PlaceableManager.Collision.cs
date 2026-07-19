using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;
using WorldClass = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Placeables;

internal sealed partial class PlaceableManager
{
    /// <summary>
    /// Check if placeable can be placed at position without collision.
    /// Validates full footprint across all chunks.
    /// </summary>
    internal static CollisionResult CheckCollision(
        WorldClass world,
        Point position,
        int z,
        Footprint footprint)
    {
        var result = new CollisionResult { CanPlace = true };

        if (footprint.W <= 0 || footprint.D <= 0 || footprint.H <= 0)
        {
            result.CanPlace = false;
            result.FailureReason = $"Invalid footprint {footprint}";
            return result;
        }

        // Check each cell in footprint
        for (int dy = 0; dy < footprint.D; dy++)
        {
            for (int dx = 0; dx < footprint.W; dx++)
            {
                int worldX = position.X + dx;
                int worldY = position.Y + dy;

                if (!world.IsValidPosition(worldX, worldY, z))
                {
                    result.CanPlace = false;
                    result.FailureReason = $"Cell ({worldX}, {worldY}, {z}) is outside the world";
                    result.BlockedCells.Add(new Point(worldX, worldY));
                    return result;
                }

                // Get chunk for this cell
                int chunkX = worldX / Chunk.SIZE_XY;
                int chunkY = worldY / Chunk.SIZE_XY;
                var chunk = world.GetChunk(new ChunkKey(chunkX, chunkY, z));
                if (chunk == null)
                {
                    result.CanPlace = false;
                    result.FailureReason = $"Chunk not loaded at ({worldX}, {worldY}, {z})";
                    return result;
                }

                // Convert to local coordinates
                int localX = worldX % Chunk.SIZE_XY;
                int localY = worldY % Chunk.SIZE_XY;
                int localIndex = Chunk.LocalIndex(localX, localY);

                // Check if cell already has placeable
                var placeableData = chunk.GetPlaceableData();
                if (placeableData?.HasPlaceableAt(localIndex) == true
                    || chunk.HasFurnitureAt(localIndex))
                {
                    result.CanPlace = false;
                    result.FailureReason = $"Cell ({worldX}, {worldY}) already occupied";
                    result.BlockedCells.Add(new Point(worldX, worldY));
                }

                // Check if tile is walkable (basic placement rule)
                var tile = chunk.GetTile(localX, localY);
                if (!tile.IsWalkable)
                {
                    result.CanPlace = false;
                    result.FailureReason = $"Cell ({worldX}, {worldY}) is not walkable";
                    result.BlockedCells.Add(new Point(worldX, worldY));
                }
            }
        }

        return result;
    }
}
