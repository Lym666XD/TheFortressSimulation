using HumanFortress.Contracts.Content.Registry;

namespace HumanFortress.Runtime.Snapshots;

internal static class BuildCatalogSnapshotBuilder
{
    internal static SimulationBuildCatalogData Build(IConstructionCatalog? constructions)
    {
        if (constructions == null)
            return new SimulationBuildCatalogData(Array.Empty<BuildableConstructionView>());

        var workshops = constructions.GetConstructionsByCategory("workshop").ToList();
        if (workshops.Count == 0)
            workshops = constructions.GetConstructionsByCategory("workshops").ToList();

        if (workshops.Count == 0)
        {
            workshops = constructions.GetAllConstructions()
                .Where(WorkshopSnapshotRules.IsWorkshopDefinition)
                .ToList();
        }

        return new SimulationBuildCatalogData(
            workshops
                .Select(WorkshopSnapshotRules.ToBuildableConstructionView)
                .ToList());
    }
}
