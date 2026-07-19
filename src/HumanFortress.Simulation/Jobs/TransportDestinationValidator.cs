using System;
using HumanFortress.Simulation.Stockpile;
using HumanFortress.Simulation.World;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Jobs;

internal sealed class TransportDestinationValidator
{
    private readonly WorldModel _world;

    internal TransportDestinationValidator(WorldModel world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    internal bool IsItemInStockpile(Guid itemId)
    {
        var item = _world.Items.GetInstance(itemId);
        return item != null && IsStockpileDestinationValid(item.Position.X, item.Position.Y, item.Z);
    }

    internal bool ValidateDestination(int x, int y, int z, TransportReason reason)
    {
        switch (reason)
        {
            case TransportReason.ToStockpile:
            case TransportReason.ToWorkshopOutput:
            case TransportReason.FromTradeDepot:
                return IsStockpileDestinationValid(x, y, z);

            case TransportReason.ToConstructionSite:
            case TransportReason.ToInstallSite:
            case TransportReason.ToUpgradeSite:
                return IsConstructionSiteDestinationValid(x, y, z);

            case TransportReason.ToWorkshopInput:
                return IsWorkshopDestinationValid(x, y, z);

            case TransportReason.ToTradeDepot:
                return WorldSafetyQueries.IsStandableOrWalkable(_world, x, y, z);

            case TransportReason.ToArmory:
            case TransportReason.ToAmmoCache:
                return IsStockpileDestinationValid(x, y, z) || (_world.GetTile(x, y, z)?.IsStandable ?? false);

            case TransportReason.ToRefuel:
                return IsWorkshopDestinationValid(x, y, z);

            case TransportReason.Cleanup:
            case TransportReason.Misc:
                return WorldSafetyQueries.IsStandableOrWalkable(_world, x, y, z);

            default:
                return true;
        }
    }

    internal bool ValidateDestinationForItem(Guid itemId, int x, int y, int z, TransportReason reason)
    {
        if (!ValidateDestination(x, y, z, reason))
            return false;

        if (!WritesStockpileIndex(reason))
            return true;

        if (!TryGetStockpileZone(x, y, z, out var zone))
            return true;

        var item = _world.Items.GetInstance(itemId);
        if (item == null)
            return false;

        var definition = _world.Items.GetDefinition(item.DefinitionId);
        return zone.Filter.Accepts(StockpileItemProjection.FromItem(item, definition));
    }

    internal bool IsStockpileDestinationValid(int x, int y, int z)
    {
        return StockpileWorldQueries.TryGetStockpileCell(_world, x, y, z, out _);
    }

    internal bool IsConstructionSiteDestinationValid(int x, int y, int z)
    {
        foreach (var chunk in EnumerateNearbyChunks(x, y, z))
        {
            var placeables = chunk.GetPlaceableData();
            if (placeables == null) continue;

            foreach (var placeable in placeables.GetAllOwnedPlaceables())
            {
                if (placeable.ConstructionSite == null) continue;
                if (placeable.Z != z) continue;
                if (IsWithinFootprintOrAdjacent(x, y, placeable.Position.X, placeable.Position.Y, placeable.Footprint.W, placeable.Footprint.D))
                    return true;
            }
        }

        return false;
    }

    internal bool IsWorkshopDestinationValid(int x, int y, int z)
    {
        foreach (var chunk in EnumerateNearbyChunks(x, y, z))
        {
            var placeables = chunk.GetPlaceableData();
            if (placeables == null) continue;

            foreach (var placeable in placeables.GetAllOwnedPlaceables())
            {
                if (placeable.Z != z) continue;
                if (placeable.ConstructionSite != null) continue;
                if (IsWithinFootprintOrAdjacent(x, y, placeable.Position.X, placeable.Position.Y, placeable.Footprint.W, placeable.Footprint.D))
                    return true;
            }
        }

        return false;
    }

    private IEnumerable<Chunk> EnumerateNearbyChunks(int x, int y, int z)
    {
        if (x < 0 || y < 0 || z < 0) yield break;

        int chunkX = x / Chunk.SIZE_XY;
        int chunkY = y / Chunk.SIZE_XY;
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            var chunk = _world.GetChunk(new ChunkKey(chunkX + dx, chunkY + dy, z));
            if (chunk != null) yield return chunk;
        }
    }

    private bool TryGetChunkCell(int x, int y, int z, out Chunk chunk, out int localIndex)
    {
        chunk = null!;
        localIndex = 0;
        if (x < 0 || y < 0 || z < 0) return false;

        int chunkX = x / Chunk.SIZE_XY;
        int chunkY = y / Chunk.SIZE_XY;
        int localX = x % Chunk.SIZE_XY;
        int localY = y % Chunk.SIZE_XY;
        var found = _world.GetChunk(new ChunkKey(chunkX, chunkY, z));
        if (found == null) return false;

        chunk = found;
        localIndex = Chunk.LocalIndex(localX, localY);
        return true;
    }

    private bool TryGetStockpileZone(int x, int y, int z, out StockpileZone zone)
    {
        zone = null!;

        if (!StockpileWorldQueries.TryGetStockpileCell(_world, x, y, z, out var cell))
            return false;

        var found = _world.Stockpiles.GetZone(cell.ZoneId);
        if (found == null)
            return false;

        zone = found;
        return true;
    }

    internal static bool WritesStockpileIndex(TransportReason reason)
    {
        return reason is TransportReason.ToStockpile
            or TransportReason.ToWorkshopOutput
            or TransportReason.FromTradeDepot
            or TransportReason.ToArmory
            or TransportReason.ToAmmoCache;
    }

    internal static bool RequiresStockpileCell(TransportReason reason)
    {
        return reason is TransportReason.ToStockpile
            or TransportReason.ToWorkshopOutput
            or TransportReason.FromTradeDepot;
    }

    private static bool IsWithinFootprintOrAdjacent(int x, int y, int originX, int originY, int width, int depth)
    {
        int relativeX = x - originX;
        int relativeY = y - originY;
        return relativeX >= -1 && relativeX <= width && relativeY >= -1 && relativeY <= depth;
    }
}
