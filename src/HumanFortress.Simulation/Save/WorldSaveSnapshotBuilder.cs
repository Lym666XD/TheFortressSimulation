using HumanFortress.Simulation.Replay;
using SimulationWorld = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Save;

internal static class WorldSaveSnapshotBuilder
{
    internal static WorldSaveSnapshot Build(SimulationWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        var chunks = world.GetAllChunks().ToArray();
        var counts = new WorldSaveCounts(
            ChunkCount: chunks.Length,
            TileCount: chunks.Sum(chunk => chunk.GetTilesCopy().Length),
            ItemCount: world.Items.GetAllInstances().Count(),
            CreatureCount: world.Creatures.GetAllInstances().Count(),
            ItemReservationCount: world.Reservations.GetItemReservationsSnapshot().Count,
            CreatureReservationCount: world.Reservations.GetCreatureReservationsSnapshot().Count,
            StockpileZoneCount: world.Stockpiles.GetAllZones().Count(),
            OwnedPlaceableCount: chunks.Sum(chunk => chunk.GetPlaceableData()?.OwnedCount ?? 0),
            MiningOrderCount: world.Orders.GetActiveMiningSnapshot().Count,
            HaulOrderCount: world.Orders.GetActiveHaulsSnapshot().Count,
            ConstructionOrderCount: world.Orders.GetActiveConstructionSnapshot().Count,
            BuildableOrderCount: world.Orders.GetActiveBuildableSnapshot().Count);

        return new WorldSaveSnapshot(
            SchemaVersion: WorldSaveSnapshotSchema.CurrentVersion,
            SizeInChunks: world.SizeInChunks,
            SizeInTiles: world.SizeInTiles,
            MaxZ: world.MaxZ,
            ReplayHash: WorldReplayHashBuilder.Build(world),
            SectionHashes: WorldReplayHashBuilder.BuildSectionHashes(world),
            Counts: counts);
    }
}
