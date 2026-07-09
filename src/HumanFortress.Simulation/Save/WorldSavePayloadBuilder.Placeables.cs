using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Contracts.Simulation.Save;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Placeables;
using HumanFortress.Simulation.World;
using SimulationWorld = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Save;

internal static partial class WorldSavePayloadBuilder
{
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
            ToPayloadStringIntMap(construction.GetRequiredMaterialsSnapshot()),
            ToPayloadStringIntMap(construction.GetDeliveredMaterialsSnapshot()),
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

    private readonly record struct OwnedPlaceablePayloadSource(
        ChunkKey OwnerChunk,
        int LocalIndex,
        PlaceableInstance Placeable);
}
