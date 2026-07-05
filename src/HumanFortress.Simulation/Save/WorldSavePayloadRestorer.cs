using HumanFortress.Contracts.Simulation.Save;
using HumanFortress.Simulation.World;
using SimulationWorld = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Save;

internal static partial class WorldSavePayloadRestorer
{
    internal static WorldSavePayloadRestoreResult RestoreTerrainOnly(WorldSavePayloadData payload)
    {
        return Restore(payload, restoreSupportedState: false);
    }

    internal static WorldSavePayloadRestoreResult RestoreTerrainAndItems(WorldSavePayloadData payload)
    {
        return RestoreSupportedSections(payload);
    }

    internal static WorldSavePayloadRestoreResult RestoreSupportedSections(WorldSavePayloadData payload)
    {
        return Restore(payload, restoreSupportedState: true);
    }

    private static WorldSavePayloadRestoreResult Restore(
        WorldSavePayloadData payload,
        bool restoreSupportedState)
    {
        var issues = new List<string>();
        ValidatePayload(payload, issues);
        ValidateSupportedSections(payload, restoreSupportedState, issues);

        if (issues.Count > 0)
            return Failed(payload, issues);

        var world = new SimulationWorld(payload.SizeInChunks, payload.MaxZ);
        foreach (var chunkPayload in payload.Chunks)
        {
            var chunk = world.GetOrCreateChunk(new ChunkKey(chunkPayload.ChunkX, chunkPayload.ChunkY, chunkPayload.Z));
            for (var i = 0; i < chunkPayload.Tiles.Length; i++)
            {
                var (x, y) = Chunk.IndexToLocal(i);
                chunk.SetTile(x, y, ToTileBase(chunkPayload.Tiles[i]), tick: 0);
            }
        }

        if (restoreSupportedState)
        {
            world.Items.SetDependencies(world);
            var itemIssues = world.Items.RestoreItemsSnapshot(payload.Items);
            issues.AddRange(itemIssues);
            var creatureIssues = world.Creatures.RestoreCreaturesSnapshot(payload.Creatures);
            issues.AddRange(creatureIssues);
            var reservationIssues = world.Reservations.RestoreSnapshot(
                payload.ItemReservations,
                payload.CreatureReservations);
            issues.AddRange(reservationIssues);
            var stockpileIssues = world.Stockpiles.RestoreZonesSnapshot(payload.StockpileZones);
            issues.AddRange(stockpileIssues);
            var placeableIssues = RestorePlaceablesSnapshot(world, payload.Placeables);
            issues.AddRange(placeableIssues);
            var orderIssues = world.Orders.RestoreActiveSnapshot(
                payload.MiningOrders,
                payload.HaulOrders,
                payload.ConstructionOrders,
                payload.BuildableOrders);
            issues.AddRange(orderIssues);

            if (issues.Count > 0)
                return FailedAfterPartialRestore(payload, world, issues);
        }

        var restoredSnapshot = WorldSaveSnapshotBuilder.Build(world);
        if (!string.Equals(restoredSnapshot.ReplayHash, payload.ReplayHash, StringComparison.Ordinal))
        {
            issues.Add("Restored world hash does not match saved world hash.");
            return new WorldSavePayloadRestoreResult(
                success: false,
                world: null,
                savedWorldHash: payload.ReplayHash ?? string.Empty,
                restoredWorldHash: restoredSnapshot.ReplayHash,
                restoredChunkCount: restoredSnapshot.Counts.ChunkCount,
                restoredTileCount: restoredSnapshot.Counts.TileCount,
                issues);
        }

        return new WorldSavePayloadRestoreResult(
            success: true,
            world,
            payload.ReplayHash,
            restoredSnapshot.ReplayHash,
            restoredSnapshot.Counts.ChunkCount,
            restoredSnapshot.Counts.TileCount,
            Array.Empty<string>());
    }
}
