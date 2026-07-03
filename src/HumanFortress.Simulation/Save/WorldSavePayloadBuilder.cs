using HumanFortress.Contracts.Simulation.Save;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Orders;
using HumanFortress.Simulation.Placeables;
using HumanFortress.Simulation.Replay;
using HumanFortress.Simulation.Stockpile;
using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.World;
using SimulationWorld = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Save;

internal static class WorldSavePayloadBuilder
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

    private static WorldSaveItemPayloadData ToPayloadItem(ItemInstance item)
    {
        return new WorldSaveItemPayloadData(
            item.Guid,
            item.DefinitionId,
            item.MaterialId,
            item.StackCount,
            ToPayloadPoint(item.Position),
            item.Z,
            item.ContainedBy,
            item.CarriedBy,
            item.EquippedBy,
            ToPayloadPlacement(item.InstalledAt),
            item.OwnerFactionId,
            item.OwnerCreatureGuid,
            (int)item.UsePolicy,
            item.Forbidden,
            ToPayloadReservationTokens(item.ReservationTokens),
            item.QualityTier,
            item.Artifact,
            item.ArtifactName,
            item.ConditionState,
            item.DurabilityCurrent,
            item.DurabilityMax,
            item.CraftedBy,
            item.MakerFactionId,
            item.StyleTag,
            ToPayloadImprovements(item.Improvements),
            ToPayloadPerishable(item.Perishable),
            item.SpawnedAtTick);
    }

    private static WorldSaveCreaturePayloadData ToPayloadCreature(CreatureInstance creature)
    {
        return new WorldSaveCreaturePayloadData(
            creature.Guid,
            creature.DefinitionId,
            creature.FactionId,
            ToPayloadPoint(creature.Position),
            creature.Z,
            creature.HP,
            creature.MaxHP,
            creature.SpawnedAtTick);
    }

    private static WorldSaveStockpileZonePayloadData ToPayloadStockpileZone(StockpileZone zone)
    {
        return new WorldSaveStockpileZonePayloadData(
            zone.ZoneId,
            zone.Name,
            ToPayloadChunkKey(zone.HomeChunk),
            new WorldSaveStockpileFilterPayloadData(
                (int)zone.Filter.Mode,
                ToSortedArray(zone.Filter.Tags),
                ToSortedArray(zone.Filter.ItemIds),
                ToSortedArray(zone.Filter.Materials)),
            zone.Priority,
            zone.TargetStacks,
            zone.HysteresisLow,
            zone.HysteresisHigh,
            zone.Generation,
            zone.CreatedTick,
            zone.MemberChunks
                .OrderBy(chunk => chunk.Z)
                .ThenBy(chunk => chunk.ChunkY)
                .ThenBy(chunk => chunk.ChunkX)
                .Select(ToPayloadChunkKey)
                .ToArray());
    }

    private static WorldSaveMiningOrderPayloadData ToPayloadMiningOrder(OrdersManager.MiningDesignation order)
    {
        return new WorldSaveMiningOrderPayloadData(
            order.Id,
            ToPayloadRectangle(order.Rect),
            order.ZMin,
            order.ZMax,
            (int)order.Action,
            order.Priority,
            order.CreatedTick);
    }

    private static WorldSaveHaulOrderPayloadData ToPayloadHaulOrder(HaulDesignation order)
    {
        return new WorldSaveHaulOrderPayloadData(
            ToPayloadRectangle(order.WorldRect),
            order.Z,
            order.Priority,
            order.CreatedTick);
    }

    private static WorldSaveConstructionOrderPayloadData ToPayloadConstructionOrder(
        ConstructionDesignation order)
    {
        return new WorldSaveConstructionOrderPayloadData(
            ToPayloadRectangle(order.WorldRect),
            order.ZMin,
            order.ZMax,
            (int)order.Shape,
            ToPayloadMaterialFilter(order.Filter),
            order.Priority,
            order.CreatedTick);
    }

    private static WorldSaveBuildableOrderPayloadData ToPayloadBuildableOrder(
        BuildableConstructionDesignation order)
    {
        return new WorldSaveBuildableOrderPayloadData(
            order.ConstructionId,
            ToPayloadPoint(order.Anchor),
            order.Z,
            order.Priority,
            order.CreatedTick);
    }

    private static WorldSavePlaceablePayloadData[] ToPayloadPlaceables(SimulationWorld world)
    {
        return world.GetAllChunks()
            .SelectMany(chunk =>
            {
                var data = chunk.GetPlaceableData();
                if (data == null)
                    return Array.Empty<OwnedPlaceablePayloadSource>();

                return data.GetOwnedPlaceableSnapshot()
                    .Select(entry => new OwnedPlaceablePayloadSource(
                        chunk.Key,
                        entry.LocalIndex,
                        entry.Placeable))
                    .ToArray();
            })
            .OrderBy(source => source.Placeable.Guid)
            .ThenBy(source => source.OwnerChunk.Z)
            .ThenBy(source => source.OwnerChunk.ChunkY)
            .ThenBy(source => source.OwnerChunk.ChunkX)
            .ThenBy(source => source.LocalIndex)
            .Select(source => ToPayloadPlaceable(source.OwnerChunk, source.LocalIndex, source.Placeable))
            .ToArray();
    }

    private static WorldSavePlaceablePayloadData ToPayloadPlaceable(
        ChunkKey ownerChunk,
        int ownerLocalIndex,
        PlaceableInstance placeable)
    {
        return new WorldSavePlaceablePayloadData(
            ToPayloadChunkKey(ownerChunk),
            ownerLocalIndex,
            placeable.Guid,
            (int)placeable.Kind,
            placeable.DefinitionId,
            ToPayloadPoint(placeable.Position),
            placeable.Z,
            ToPayloadFootprint(placeable.Footprint),
            placeable.SourceItemGuid,
            placeable.SourceItemMaterial,
            placeable.SourceItemQuality,
            ToPayloadImprovements(placeable.SourceItemDecorations),
            placeable.SourceItemMaker,
            ToPayloadEffects(placeable.Effects),
            (int)placeable.Passability,
            placeable.IsGhost,
            ToPayloadConstructionSite(placeable.ConstructionSite),
            ToPayloadWorkshop(placeable.Workshop),
            ToPayloadDoorState(placeable.DoorState),
            placeable.OwnerFactionId,
            placeable.OwnerCreatureGuid,
            placeable.Forbidden,
            placeable.HitPoints,
            placeable.MaxHitPoints);
    }

    private static WorldSaveFootprintData ToPayloadFootprint(Footprint footprint)
    {
        return new WorldSaveFootprintData(footprint.W, footprint.D, footprint.H);
    }

    private static WorldSaveEffectsData ToPayloadEffects(EffectsBlock effects)
    {
        return new WorldSaveEffectsData(
            effects.Beauty,
            effects.Comfort,
            effects.LightLumen,
            effects.HeatW);
    }

    private static WorldSaveConstructionSitePayloadData? ToPayloadConstructionSite(
        ConstructionSiteState? construction)
    {
        if (construction == null)
            return null;

        return new WorldSaveConstructionSitePayloadData(
            construction.TargetId,
            ToPayloadStringIntMap(construction.MaterialsRequired),
            ToPayloadStringIntMap(construction.MaterialsDelivered),
            construction.BuildProgressTicks,
            construction.TotalBuildTicks);
    }

    private static WorldSaveWorkshopPayloadData? ToPayloadWorkshop(WorkshopState? workshop)
    {
        if (workshop == null)
            return null;

        return new WorldSaveWorkshopPayloadData(
            workshop.AutoRequestMaterials,
            workshop.AutoStockpileOutputs,
            workshop.AllowedWorkers,
            workshop.MaxWorkers,
            workshop.ActiveJobs,
            workshop.NextEntrySequence,
            workshop.Queue.Select(ToPayloadWorkshopQueueEntry).ToArray());
    }

    private static WorldSaveWorkshopQueueEntryPayloadData ToPayloadWorkshopQueueEntry(CraftQueueEntry entry)
    {
        return new WorldSaveWorkshopQueueEntryPayloadData(
            entry.EntryId,
            entry.RecipeId,
            entry.DisplayName,
            (int)entry.Status,
            entry.HasPendingRequests,
            entry.LastRequestTick,
            entry.ActiveWorkerId,
            entry.IsScheduled,
            entry.BlockingReason);
    }

    private static WorldSaveDoorStatePayloadData? ToPayloadDoorState(DoorState? door)
    {
        if (door == null)
            return null;

        return new WorldSaveDoorStatePayloadData(door.IsOpen, door.IsLocked);
    }

    private static WorldSavePointData ToPayloadPoint(SadRogue.Primitives.Point point)
    {
        return new WorldSavePointData(point.X, point.Y);
    }

    private static WorldSaveRectangleData ToPayloadRectangle(SadRogue.Primitives.Rectangle rectangle)
    {
        return new WorldSaveRectangleData(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
    }

    private static WorldSaveChunkKeyData ToPayloadChunkKey(ChunkKey key)
    {
        return new WorldSaveChunkKeyData(key.ChunkX, key.ChunkY, key.Z);
    }

    private static WorldSaveMaterialFilterPayloadData ToPayloadMaterialFilter(MaterialFilterSpec filter)
    {
        return new WorldSaveMaterialFilterPayloadData(
            filter.PreferredMaterialId,
            filter.CategoryKey,
            ToSortedArray(filter.Tags));
    }

    private static WorldSavePlacementData? ToPayloadPlacement(PlacementData? placement)
    {
        if (placement == null)
            return null;

        return new WorldSavePlacementData(
            ToPayloadPoint(placement.AnchorWorld),
            placement.Z,
            placement.Rotation,
            placement.StateId);
    }

    private static WorldSaveItemReservationTokenData[] ToPayloadReservationTokens(
        IEnumerable<ReservationToken> reservations)
    {
        return reservations
            .OrderBy(token => token.JobGuid)
            .ThenBy(token => token.ClaimantCreatureGuid)
            .ThenBy(token => token.ReservationType, StringComparer.Ordinal)
            .Select(token => new WorldSaveItemReservationTokenData(
                token.JobGuid,
                token.ClaimantCreatureGuid,
                token.ReservedCount,
                token.ExpiresAtTick,
                token.ReservationType))
            .ToArray();
    }

    private static WorldSaveItemImprovementData[]? ToPayloadImprovements(IReadOnlyList<Improvement>? improvements)
    {
        if (improvements == null)
            return null;

        return improvements
            .OrderBy(improvement => improvement.Type, StringComparer.Ordinal)
            .ThenBy(improvement => improvement.MaterialId, StringComparer.Ordinal)
            .ThenBy(improvement => improvement.QualityTier)
            .ThenBy(improvement => improvement.CreatedBy)
            .ThenBy(improvement => improvement.Description, StringComparer.Ordinal)
            .Select(improvement => new WorldSaveItemImprovementData(
                improvement.Type,
                improvement.MaterialId,
                improvement.QualityTier,
                improvement.CreatedBy,
                improvement.Description))
            .ToArray();
    }

    private static WorldSaveItemPerishableData? ToPayloadPerishable(PerishableState? perishable)
    {
        if (perishable == null)
            return null;

        return new WorldSaveItemPerishableData(
            perishable.CreatedAtTick,
            perishable.FreshDurationTicks,
            perishable.SpoilDurationTicks,
            perishable.CurrentFreshness);
    }

    private static WorldSaveStringIntData[] ToPayloadStringIntMap(IEnumerable<KeyValuePair<string, int>> values)
    {
        return values
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => new WorldSaveStringIntData(entry.Key, entry.Value))
            .ToArray();
    }

    private static string[] ToSortedArray(IEnumerable<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private readonly record struct OwnedPlaceablePayloadSource(
        ChunkKey OwnerChunk,
        int LocalIndex,
        PlaceableInstance Placeable);
}
