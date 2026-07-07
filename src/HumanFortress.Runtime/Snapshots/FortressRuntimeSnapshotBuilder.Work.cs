using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Runtime.Composition;
using HumanFortress.Runtime.Host;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class FortressRuntimeSnapshotBuilder
{
    internal static SimulationJobsDebugData? BuildJobsSnapshot(
        SimulationRuntimeHost<SimulationRuntimeSystems>? runtimeHost,
        ulong tick)
    {
        return JobsDebugSnapshotBuilder.Build(runtimeHost, tick);
    }

    internal static SimulationWorkDrawerData BuildWorkDrawerSnapshot(
        SimulationRuntimeHost<SimulationRuntimeSystems>? runtimeHost,
        World? world,
        IConstructionCatalog? constructions,
        ulong tick)
    {
        return new SimulationWorkDrawerData(
            world != null,
            BuildJobsSnapshot(runtimeHost, tick),
            BuildWorkforceSnapshot(runtimeHost, world),
            BuildOrdersSnapshot(world),
            BuildWorkshopSnapshot(world, constructions));
    }

    internal static WorkforceDebugData BuildWorkforceSnapshot(
        SimulationRuntimeHost<SimulationRuntimeSystems>? runtimeHost,
        World? world)
    {
        return WorkforceSnapshotBuilder.Build(runtimeHost, world);
    }

    internal static SimulationOrdersDebugData BuildOrdersSnapshot(World? world)
    {
        return OrdersSnapshotBuilder.Build(world);
    }

    internal static SimulationWorkshopDebugData BuildWorkshopSnapshot(
        World? world,
        IConstructionCatalog? constructions)
    {
        return WorkshopSnapshotBuilder.Build(world, constructions);
    }

    internal static WorkshopSummaryView? FindWorkshopSnapshot(
        World? world,
        IConstructionCatalog? constructions,
        Guid workshopGuid)
    {
        return WorkshopSnapshotBuilder.FindById(world, constructions, workshopGuid);
    }

    internal static string? GetDefaultRecipeForWorkshop(IRecipeCatalog? recipes, string? workshopId)
    {
        if (recipes == null || string.IsNullOrWhiteSpace(workshopId))
            return null;

        var workshopRecipes = recipes.GetRecipesForWorkshop(workshopId);
        return workshopRecipes.Count == 0
            ? null
            : workshopRecipes[0].Id;
    }
}
