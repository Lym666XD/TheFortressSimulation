using HumanFortress.Simulation.Topology;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;
using WorldClass = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Placeables;

internal sealed partial class PlaceableManager
{
    internal static bool RemoveGhostAt(WorldClass world, Point position, int z, string? purpose, ulong tick)
    {
        return TryRemoveOwnedAt(
            world,
            position,
            z,
            tick,
            placeable => placeable.IsGhost
                && (string.IsNullOrEmpty(purpose)
                    || placeable.DefinitionId == $"core_construction_ghost:{purpose}"),
            out _);
    }

    internal static bool RemoveOwnedAt(WorldClass world, Point position, int z, ulong tick)
    {
        return TryRemoveOwnedAt(world, position, z, tick, static _ => true, out _);
    }

    internal static bool TryRemoveOwnedAt(
        WorldClass world,
        Point position,
        int z,
        ulong tick,
        Predicate<PlaceableInstance> predicate,
        out TopologyChangeDescription? committedChange)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(predicate);

        lock (world.TopologyLock)
        {
            committedChange = null;
            if (!TryGetOwnedAtAnchor(world, position, z, out var placeable, out var primaryCell)
                || placeable == null
                || !predicate(placeable)
                || !TryValidateRemovalFootprint(world, placeable, primaryCell, out var cells))
            {
                return false;
            }

            var transaction = new TopologyChangeTransaction(
                world,
                tick,
                TopologyChangeKind.PlaceableRemove,
                placeable.Guid,
                applyPreparedWrites: () =>
                {
                    foreach (var cell in cells)
                    {
                        cell.Chunk.RemoveDerivedFurniture(cell.LocalIndex, placeable.Guid, tick);
                        if (!cell.IsAnchor)
                        {
                            var data = cell.Chunk.GetPlaceableData()!;
                            if (!data.RemoveExternalRef(cell.LocalIndex))
                                return false;
                        }
                    }

                    return primaryCell.Chunk.GetPlaceableData()!
                        .RemovePlaceable(primaryCell.LocalIndex);
                });

            MarkFootprintCellsDirtyForChunk(transaction, cells);
            committedChange = transaction.Commit();
            return true;
        }
    }

    private static bool TryGetOwnedAtAnchor(
        WorldClass world,
        Point position,
        int z,
        out PlaceableInstance? placeable,
        out PlaceableFootprintCell primaryCell)
    {
        placeable = null;
        primaryCell = default;
        if (!world.IsValidPosition(position.X, position.Y, z))
            return false;

        var chunk = world.GetChunk(new ChunkKey(
            position.X / Chunk.SIZE_XY,
            position.Y / Chunk.SIZE_XY,
            z));
        if (chunk == null)
            return false;

        var localIndex = Chunk.LocalIndex(
            position.X % Chunk.SIZE_XY,
            position.Y % Chunk.SIZE_XY);
        if (chunk.GetPlaceableData()?.TryGetOwnedAt(localIndex, out var owned) != true)
            return false;

        placeable = owned;
        primaryCell = new PlaceableFootprintCell(
            position.X,
            position.Y,
            chunk,
            localIndex,
            IsAnchor: true);
        return true;
    }

    private static bool TryValidateRemovalFootprint(
        WorldClass world,
        PlaceableInstance placeable,
        PlaceableFootprintCell primaryCell,
        out PlaceableFootprintCell[] cells)
    {
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
                    cells = Array.Empty<PlaceableFootprintCell>();
                    return false;
                }

                var chunk = world.GetChunk(new ChunkKey(
                    worldX / Chunk.SIZE_XY,
                    worldY / Chunk.SIZE_XY,
                    placeable.Z));
                if (chunk == null)
                {
                    cells = Array.Empty<PlaceableFootprintCell>();
                    return false;
                }

                var localIndex = Chunk.LocalIndex(
                    worldX % Chunk.SIZE_XY,
                    worldY % Chunk.SIZE_XY);
                var isAnchor = dx == 0 && dy == 0;
                var data = chunk.GetPlaceableData();
                if (data == null
                    || (isAnchor
                        ? !data.TryGetOwnedAt(localIndex, out var owner)
                            || !ReferenceEquals(owner, placeable)
                        : !data.TryGetExternalRefAt(localIndex, out var ownerGuid)
                            || ownerGuid != placeable.Guid))
                {
                    cells = Array.Empty<PlaceableFootprintCell>();
                    return false;
                }

                prepared.Add(isAnchor
                    ? primaryCell
                    : new PlaceableFootprintCell(worldX, worldY, chunk, localIndex, false));
            }
        }

        cells = prepared.ToArray();
        return true;
    }
}
