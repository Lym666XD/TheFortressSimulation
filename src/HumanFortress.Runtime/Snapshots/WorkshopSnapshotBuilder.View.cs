using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Simulation.Placeables;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class WorkshopSnapshotBuilder
{
    private static bool TryCreateWorkshopView(
        World world,
        IConstructionCatalog constructions,
        PlaceableInstance placeable,
        out WorkshopSummaryView workshop)
    {
        workshop = default;

        string definitionId = placeable.ConstructionSite?.TargetId ?? placeable.DefinitionId;
        var definition = constructions.GetConstruction(definitionId);
        if (definition == null || !WorkshopSnapshotRules.IsWorkshopDefinition(definition))
            return false;

        var state = placeable.Workshop;
        IReadOnlyList<WorkshopQueueEntryView> queue = state == null
            ? Array.Empty<WorkshopQueueEntryView>()
            : state.Queue.Select(MapQueueEntry).ToList();
        var tags = definition.PlaceableProfile.Tags?.ToArray() ?? Array.Empty<string>();
        var materialProgress = placeable.ConstructionSite == null
            ? null
            : FormatMaterialProgress(world, placeable);

        workshop = new WorkshopSummaryView(
            placeable.Guid,
            definition.Id,
            definition.Name,
            placeable.Position.X,
            placeable.Position.Y,
            placeable.Z,
            placeable.Footprint.W,
            placeable.Footprint.D,
            definition.PlaceableProfile.Passability.ToString(),
            tags,
            definition.AttachmentSlots?.Length ?? 0,
            materialProgress,
            placeable.ConstructionSite != null,
            state?.ActiveJobs ?? 0,
            state?.AllowedWorkers ?? 0,
            state?.MaxWorkers ?? 0,
            state?.AutoRequestMaterials ?? false,
            state?.AutoStockpileOutputs ?? false,
            state?.Queue.Count ?? 0,
            queue.Any(entry => entry.IsBlocked),
            queue);
        return true;
    }

    private static WorkshopQueueEntryView MapQueueEntry(CraftQueueEntry entry)
    {
        return entry.Status switch
        {
            CraftQueueStatus.InProgress => new WorkshopQueueEntryView(
                entry.EntryId,
                entry.DisplayName,
                ">",
                entry.ActiveWorkerId.HasValue ? $"Working ({entry.ActiveWorkerId.Value.ToString("N")[..6]})" : "Working",
                false),
            CraftQueueStatus.AwaitingMaterials => new WorkshopQueueEntryView(
                entry.EntryId,
                entry.DisplayName,
                "!",
                entry.BlockingReason ?? "Waiting for inputs",
                true),
            CraftQueueStatus.Scheduled => new WorkshopQueueEntryView(
                entry.EntryId,
                entry.DisplayName,
                "*",
                "Assigned",
                false),
            _ => new WorkshopQueueEntryView(
                entry.EntryId,
                entry.DisplayName,
                "-",
                "Ready",
                false)
        };
    }
}
