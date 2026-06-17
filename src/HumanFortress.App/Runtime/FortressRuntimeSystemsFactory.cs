using HumanFortress.App.Jobs;
using HumanFortress.Core.Simulation;
using HumanFortress.Navigation;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.World;

namespace HumanFortress.App.Runtime;

internal static class FortressRuntimeSystemsFactory
{
    public static SimulationRuntimeSystems Create(
        World world,
        DiffLog diffLog,
        ItemsDiffLog itemsDiffLog,
        NavigationManager navigation,
        FortressRuntimeDependencies dependencies)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(diffLog);
        ArgumentNullException.ThrowIfNull(itemsDiffLog);
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(dependencies);

        var planners = FortressRuntimePlanningSystems.Create(world, dependencies);
        var jobs = FortressRuntimeJobSystems.Create(
            world,
            diffLog,
            itemsDiffLog,
            navigation,
            dependencies,
            planners);

        var sanitizer = new SanitizeSystem(world, diffLog, intervalTicks: 40, maxPerTick: 8, log: Logger.Log);

        var jobsOrchestrator = new UnifiedJobsOrchestrator(
            planners.Hauling,
            planners.ConstructionMaterials,
            planners.Mining,
            planners.Construction,
            planners.Craft,
            jobs.Transport,
            jobs.Mining,
            jobs.Construction,
            jobs.Craft,
            dependencies.SchedulerTunings,
            Logger.Log);

        return new SimulationRuntimeSystems(
            planners.Hauling,
            planners.TransportQueue,
            jobs.Transport,
            planners.Mining,
            planners.Buildable,
            planners.ConstructionMaterials,
            jobs.Mining,
            planners.Construction,
            jobs.Construction,
            planners.Craft,
            jobs.Craft,
            dependencies.ProfessionAssignments,
            dependencies.SchedulerTunings,
            dependencies.WorkshopTunings,
            jobsOrchestrator,
            sanitizer);
    }
}
