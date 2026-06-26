using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class WorkshopSnapshotBuilder
{
    internal static SimulationWorkshopDebugData Build(
        World? world,
        IConstructionCatalog? constructions)
    {
        if (world == null || constructions == null)
            return new SimulationWorkshopDebugData(Array.Empty<WorkshopSummaryView>(), 0, 0, 0);

        var workshops = new List<WorkshopSummaryView>();
        foreach (var chunk in world.GetAllChunks())
        {
            var placeables = chunk.GetPlaceableData();
            if (placeables == null)
                continue;

            foreach (var placeable in placeables.GetAllOwnedPlaceables())
            {
                if (TryCreateWorkshopView(world, constructions, placeable, out var workshop))
                    workshops.Add(workshop);
            }
        }

        var ordered = workshops
            .OrderBy(workshop => workshop.Z)
            .ThenBy(workshop => workshop.Y)
            .ThenBy(workshop => workshop.X)
            .ToList();

        return new SimulationWorkshopDebugData(
            ordered,
            ordered.Count(workshop => !workshop.IsSite),
            ordered.Count(workshop => workshop.IsSite),
            world.Orders.GetActiveBuildableSnapshot().Count);
    }

    internal static WorkshopSummaryView? FindById(
        World? world,
        IConstructionCatalog? constructions,
        Guid workshopGuid)
    {
        if (world == null || constructions == null)
            return null;

        foreach (var chunk in world.GetAllChunks())
        {
            var placeables = chunk.GetPlaceableData();
            if (placeables == null)
                continue;

            foreach (var placeable in placeables.GetAllOwnedPlaceables())
            {
                if (placeable.Guid != workshopGuid)
                    continue;

                return TryCreateWorkshopView(world, constructions, placeable, out var workshop)
                    ? workshop
                    : null;
            }
        }

        return null;
    }
}
