using HumanFortress.Jobs;
using HumanFortress.Core.Simulation;
using HumanFortress.Navigation;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime;

internal static class FortressRuntimeSystemsFactory
{
    internal static SimulationRuntimeSystems Create(
        World world,
        DiffLog diffLog,
        ItemsDiffLog itemsDiffLog,
        NavigationManager navigation,
        FortressRuntimeDependencies dependencies,
        FortressRuntimeLogging? logging = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(diffLog);
        ArgumentNullException.ThrowIfNull(itemsDiffLog);
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(dependencies);

        logging ??= FortressRuntimeLogging.None;

        var planners = FortressRuntimePlanningSystems.Create(world, dependencies, logging);
        var jobs = FortressRuntimeJobSystems.Create(
            world,
            diffLog,
            itemsDiffLog,
            navigation,
            dependencies,
            planners,
            logging);

        var sanitizer = new SanitizeSystem(world, diffLog, intervalTicks: 40, maxPerTick: 8, log: logging.Log);

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
            logging.Log);

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
