using HumanFortress.Simulation.Topology;
using HumanFortress.Simulation.World;
using WorldClass = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Placeables;

internal sealed partial class PlaceableManager
{
    /// <summary>
    /// Place an authoritative owner, every secondary reference, and every
    /// derived furniture cell as one validate-before-apply topology commit.
    /// </summary>
    internal static void PlacePlaceable(
        WorldClass world,
        PlaceableInstance placeable,
        ulong tick)
    {
        if (!TryPlacePlaceable(world, placeable, tick, out var failureReason, out _))
            throw new InvalidOperationException(failureReason);
    }

    internal static bool TryPlacePlaceable(
        WorldClass world,
        PlaceableInstance placeable,
        ulong tick,
        out string failureReason,
        out TopologyChangeDescription? committedChange)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(placeable);

        lock (world.TopologyLock)
        {
            committedChange = null;
            if (!TryValidatePlacement(world, placeable, out var cells, out failureReason))
                return false;

            var primary = cells.Single(static cell => cell.IsAnchor);
            var chunks = cells
                .Select(static cell => cell.Chunk)
                .DistinctBy(static chunk => chunk.Key)
                .OrderBy(static chunk => chunk.Key.Z)
                .ThenBy(static chunk => chunk.Key.ChunkY)
                .ThenBy(static chunk => chunk.Key.ChunkX)
                .ToArray();
            var isBlocker = IsBlockingPassability(placeable);

            var transaction = new TopologyChangeTransaction(
                world,
                tick,
                TopologyChangeKind.PlaceableCreate,
                placeable.Guid,
                applyPreparedWrites: () =>
                {
                    foreach (var chunk in chunks)
                        chunk.EnsurePlaceableData();

                    var primaryData = primary.Chunk.GetPlaceableData()!;
                    if (!primaryData.TryAddPlaceable(primary.LocalIndex, placeable))
                        return false;

                    foreach (var cell in cells)
                    {
                        var data = cell.Chunk.GetPlaceableData()!;
                        if (!cell.IsAnchor
                            && !data.TryAddExternalRef(cell.LocalIndex, placeable.Guid))
                        {
                            return false;
                        }

                        if (!cell.Chunk.TryPlaceDerivedFurniture(
                                cell.LocalIndex,
                                new FurnitureRef(placeable.Guid),
                                isBlocker,
                                tick))
                        {
                            return false;
                        }
                    }

                    return true;
                });

            MarkFootprintCellsDirtyForChunk(transaction, cells);
            committedChange = transaction.Commit();
            failureReason = string.Empty;
            return true;
        }
    }

    private static bool TryValidatePlacement(
        WorldClass world,
        PlaceableInstance placeable,
        out PlaceableFootprintCell[] cells,
        out string failureReason)
    {
        cells = Array.Empty<PlaceableFootprintCell>();
        if (placeable.Footprint.W <= 0
            || placeable.Footprint.D <= 0
            || placeable.Footprint.H <= 0
            || placeable.Footprint.W > world.SizeInTiles
            || placeable.Footprint.D > world.SizeInTiles)
        {
            failureReason = $"Invalid footprint {placeable.Footprint}.";
            return false;
        }

        if (ContainsAuthoritativePlaceableGuid(world, placeable.Guid))
        {
            failureReason = $"Placeable GUID {placeable.Guid} is already owned or referenced.";
            return false;
        }

        var prepared = new List<PlaceableFootprintCell>(
            placeable.Footprint.W * placeable.Footprint.D);
        for (var dy = 0; dy < placeable.Footprint.D; dy++)
        {
            for (var dx = 0; dx < placeable.Footprint.W; dx++)
            {
                var worldX = placeable.Position.X + dx;
                var worldY = placeable.Position.Y + dy;
                if (!world.IsValidPosition(worldX, worldY, placeable.Z))
                {
                    failureReason = $"Footprint cell ({worldX}, {worldY}, {placeable.Z}) is outside the world.";
                    return false;
                }

                var chunkKey = new ChunkKey(
                    worldX / Chunk.SIZE_XY,
                    worldY / Chunk.SIZE_XY,
                    placeable.Z);
                var chunk = world.GetChunk(chunkKey);
                if (chunk == null)
                {
                    failureReason = $"Chunk not loaded at ({worldX}, {worldY}, {placeable.Z}).";
                    return false;
                }

                var localIndex = Chunk.LocalIndex(worldX % Chunk.SIZE_XY, worldY % Chunk.SIZE_XY);
                if (chunk.GetPlaceableData()?.HasPlaceableAt(localIndex) == true
                    || chunk.HasFurnitureAt(localIndex))
                {
                    failureReason = $"Footprint cell ({worldX}, {worldY}, {placeable.Z}) is already occupied.";
                    return false;
                }

                if (!chunk.GetTile(worldX % Chunk.SIZE_XY, worldY % Chunk.SIZE_XY).IsWalkable)
                {
                    failureReason = $"Footprint cell ({worldX}, {worldY}, {placeable.Z}) is not walkable.";
                    return false;
                }

                prepared.Add(new PlaceableFootprintCell(
                    worldX,
                    worldY,
                    chunk,
                    localIndex,
                    dx == 0 && dy == 0));
            }
        }

        cells = prepared.ToArray();
        failureReason = string.Empty;
        return true;
    }

    private static bool ContainsAuthoritativePlaceableGuid(WorldClass world, Guid guid)
    {
        foreach (var chunk in world.GetAllChunks())
        {
            var data = chunk.GetPlaceableData();
            if (data == null)
                continue;

            if (data.GetOwnedPlaceableSnapshot().Any(entry => entry.Placeable.Guid == guid)
                || data.GetExternalReferenceSnapshot().Any(entry => entry.PlaceableGuid == guid))
            {
                return true;
            }
        }

        return false;
    }

    private static void MarkFootprintCellsDirtyForChunk(
        TopologyChangeTransaction transaction,
        IReadOnlyList<PlaceableFootprintCell> cells)
    {
        foreach (var cell in cells)
            transaction.TrackChangedCellAndDependencies(cell.WorldX, cell.WorldY, cell.Chunk.Key.Z);
    }

    private static bool IsBlockingPassability(PlaceableInstance placeable)
    {
        if (placeable.IsGhost)
            return false;
        if (placeable.Passability == HumanFortress.Contracts.Content.Registry.PassabilityMode.Doorway)
            return !(placeable.DoorState?.IsOpen ?? false);
        return placeable.Passability == HumanFortress.Contracts.Content.Registry.PassabilityMode.Blocking;
    }

    private readonly record struct PlaceableFootprintCell(
        int WorldX,
        int WorldY,
        Chunk Chunk,
        int LocalIndex,
        bool IsAnchor);
}
