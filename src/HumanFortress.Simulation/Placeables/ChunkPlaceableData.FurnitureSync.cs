using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Simulation.World;

namespace HumanFortress.Simulation.Placeables;

internal sealed partial class ChunkPlaceableData
{
    /// <summary>
    /// Synchronize placeable to FurnitureCell layer.
    /// Called after AddPlaceable to update L2 derived cache.
    /// PlaceableInstance (authoritative) -> FurnitureRef -> FurnitureCell (derived)
    /// </summary>
    public void SyncToFurnitureCell(Chunk chunk, PlaceableInstance placeable, ulong tick)
    {
        // Determine passability: Blocking placeables go in Blocker slot, others in Passables
        bool isBlocker = placeable.Kind switch
        {
            PlaceableKind.Installable => IsBlockingPassability(placeable),
            PlaceableKind.Construction => IsBlockingPassability(placeable),
            _ => false
        };

        // Get footprint dimensions
        var footprint = placeable.Footprint;
        var anchorX = placeable.Position.X % Chunk.SIZE_XY;
        var anchorY = placeable.Position.Y % Chunk.SIZE_XY;

        // Place FurnitureRef for each cell in footprint
        for (int dy = 0; dy < footprint.D; dy++)
        {
            for (int dx = 0; dx < footprint.W; dx++)
            {
                int cellX = anchorX + dx;
                int cellY = anchorY + dy;

                // Only place if cell is within chunk bounds
                if (cellX >= 0 && cellX < Chunk.SIZE_XY && cellY >= 0 && cellY < Chunk.SIZE_XY)
                {
                    var furnitureRef = new FurnitureRef(placeable.Guid);
                    chunk.PlaceFurniture(cellX, cellY, furnitureRef, isBlocker, tick);
                }
            }
        }
    }

    /// <summary>
    /// Check if placeable has blocking passability
    /// </summary>
    private static bool IsBlockingPassability(PlaceableInstance placeable)
    {
        if (placeable.IsGhost) return false;

        // Door passability depends on DoorState.IsOpen
        if (placeable.Passability == PassabilityMode.Doorway)
        {
            return !(placeable.DoorState?.IsOpen ?? false);
        }

        return placeable.Passability == PassabilityMode.Blocking;
    }

    /// <summary>
    /// Remove placeable from FurnitureCell layer.
    /// Called before RemovePlaceable to update L2 derived cache.
    /// </summary>
    public void UnsyncFromFurnitureCell(Chunk chunk, PlaceableInstance placeable, ulong tick)
    {
        var footprint = placeable.Footprint;
        var anchorX = placeable.Position.X % Chunk.SIZE_XY;
        var anchorY = placeable.Position.Y % Chunk.SIZE_XY;

        for (int dy = 0; dy < footprint.D; dy++)
        {
            for (int dx = 0; dx < footprint.W; dx++)
            {
                int cellX = anchorX + dx;
                int cellY = anchorY + dy;
                if (cellX >= 0 && cellX < Chunk.SIZE_XY && cellY >= 0 && cellY < Chunk.SIZE_XY)
                {
                    chunk.RemoveFurnitureAt(cellX, cellY, placeable.Guid, tick);
                }
            }
        }
    }
}
