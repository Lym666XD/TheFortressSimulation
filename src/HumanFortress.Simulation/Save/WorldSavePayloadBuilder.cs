using HumanFortress.Contracts.Simulation.Save;
using HumanFortress.Simulation.World;
using SimulationWorld = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Save;

internal static partial class WorldSavePayloadBuilder
{
    internal static WorldSavePayloadData Build(SimulationWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        var snapshot = WorldSaveSnapshotBuilder.Build(world);
        var chunks = world.GetAllChunks()
            .OrderBy(chunk => chunk.Key.Z)
            .ThenBy(chunk => chunk.Key.ChunkY)
            .ThenBy(chunk => chunk.Key.ChunkX)
            .Select(chunk => new WorldSaveChunkPayloadData(
                chunk.Key.ChunkX,
                chunk.Key.ChunkY,
                chunk.Key.Z,
                chunk.GetTilesCopy().Select(ToPayloadTile).ToArray()))
            .ToArray();

        return new WorldSavePayloadData(
            WorldSavePayloadFormat.CurrentVersion,
            snapshot.SizeInChunks,
            snapshot.SizeInTiles,
            snapshot.MaxZ,
            snapshot.ReplayHash,
            ToPayloadSectionHashes(snapshot.SectionHashes),
            ToPayloadCounts(snapshot.Counts),
            chunks,
            world.Items.GetAllInstances()
                .OrderBy(item => item.Guid)
                .Select(ToPayloadItem)
                .ToArray(),
            world.Creatures.GetAllInstances()
                .OrderBy(creature => creature.Guid)
                .Select(ToPayloadCreature)
                .ToArray(),
            world.Reservations.GetItemReservationsSnapshot()
                .OrderBy(reservation => reservation.itemId)
                .ThenBy(reservation => reservation.holderId)
                .Select(reservation => new WorldSaveItemReservationPayloadData(
                    reservation.itemId,
                    reservation.holderId,
                    reservation.expireTick))
                .ToArray(),
            world.Reservations.GetCreatureReservationsSnapshot()
                .OrderBy(reservation => reservation.workerId)
                .ThenBy(reservation => reservation.holderSystem, StringComparer.Ordinal)
                .ThenBy(reservation => reservation.jobId, StringComparer.Ordinal)
                .Select(reservation => new WorldSaveCreatureReservationPayloadData(
                    reservation.workerId,
                    reservation.holderSystem,
                    reservation.jobId,
                    reservation.expireTick))
                .ToArray(),
            world.Stockpiles.GetAllZones()
                .OrderBy(zone => zone.ZoneId)
                .Select(ToPayloadStockpileZone)
                .ToArray(),
            ToPayloadPlaceables(world),
            world.Orders.GetActiveMiningSnapshot()
                .OrderBy(order => order.Id)
                .ThenBy(order => order.ZMin)
                .ThenBy(order => order.ZMax)
                .ThenBy(order => order.Priority)
                .Select(ToPayloadMiningOrder)
                .ToArray(),
            world.Orders.GetActiveHaulsSnapshot()
                .OrderBy(order => order.Z)
                .ThenBy(order => order.Priority)
                .ThenBy(order => order.WorldRect.X)
                .ThenBy(order => order.WorldRect.Y)
                .Select(ToPayloadHaulOrder)
                .ToArray(),
            world.Orders.GetActiveConstructionSnapshot()
                .OrderBy(order => order.ZMin)
                .ThenBy(order => order.ZMax)
                .ThenBy(order => order.Priority)
                .ThenBy(order => order.WorldRect.X)
                .ThenBy(order => order.WorldRect.Y)
                .Select(ToPayloadConstructionOrder)
                .ToArray(),
            world.Orders.GetActiveBuildableSnapshot()
                .OrderBy(order => order.ConstructionId, StringComparer.Ordinal)
                .ThenBy(order => order.Anchor.X)
                .ThenBy(order => order.Anchor.Y)
                .ThenBy(order => order.Z)
                .ThenBy(order => order.Priority)
                .Select(ToPayloadBuildableOrder)
                .ToArray());
    }
}
