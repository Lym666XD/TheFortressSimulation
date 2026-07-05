using HumanFortress.Contracts.Simulation.Save;
using HumanFortress.Simulation.Replay;
using HumanFortress.Simulation.Tiles;

namespace HumanFortress.Simulation.Save;

internal static partial class WorldSavePayloadBuilder
{
    private static WorldSaveSectionHashesData ToPayloadSectionHashes(WorldReplaySectionHashes hashes)
    {
        return new WorldSaveSectionHashesData(
            hashes.TerrainHash,
            hashes.ItemsHash,
            hashes.CreaturesHash,
            hashes.ReservationsHash,
            hashes.StockpileZonesHash,
            hashes.PlaceablesHash,
            hashes.OrdersHash);
    }

    private static WorldSaveCountsData ToPayloadCounts(WorldSaveCounts counts)
    {
        return new WorldSaveCountsData(
            counts.ChunkCount,
            counts.TileCount,
            counts.ItemCount,
            counts.CreatureCount,
            counts.ItemReservationCount,
            counts.CreatureReservationCount,
            counts.StockpileZoneCount,
            counts.OwnedPlaceableCount,
            counts.MiningOrderCount,
            counts.HaulOrderCount,
            counts.ConstructionOrderCount,
            counts.BuildableOrderCount);
    }

    private static WorldSaveTilePayloadData ToPayloadTile(TileBase tile)
    {
        return new WorldSaveTilePayloadData(
            tile.GeoMatId,
            tile.TerrainBits,
            tile.SurfaceBits,
            tile.FluidKind,
            tile.FluidDepth,
            tile.MetaBits,
            tile.TrafficCost);
    }
}
