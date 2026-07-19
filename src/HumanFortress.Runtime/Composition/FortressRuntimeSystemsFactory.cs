using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Jobs.Orchestration;
using HumanFortress.Jobs.Safety;
using HumanFortress.Navigation.Implementation;
using HumanFortress.Runtime.Navigation;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Stockpile;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Composition;

internal static class FortressRuntimeSystemsFactory
{
    internal static SimulationRuntimeSystems Create(
        World world,
        DiffLog diffLog,
        ItemsDiffLog itemsDiffLog,
        StockpileDiffLog stockpileDiffLog,
        NavigationManager navigation,
        RuntimePathServiceRegistry pathServices,
        FortressRuntimeDependencies dependencies,
        FortressRuntimeLogging? logging = null,
        int transportPlanningWorkerCount = 1)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(diffLog);
        ArgumentNullException.ThrowIfNull(itemsDiffLog);
        ArgumentNullException.ThrowIfNull(stockpileDiffLog);
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(pathServices);
        ArgumentNullException.ThrowIfNull(dependencies);
        if (transportPlanningWorkerCount < 1)
            throw new ArgumentOutOfRangeException(nameof(transportPlanningWorkerCount));

        logging ??= FortressRuntimeLogging.None;

        var planners = FortressRuntimePlanningSystems.Create(
            world,
            stockpileDiffLog,
            dependencies,
            logging);
        var jobs = FortressRuntimeJobSystems.Create(
            world,
            diffLog,
            itemsDiffLog,
            stockpileDiffLog,
            navigation,
            pathServices,
            dependencies,
            planners,
            logging,
            transportPlanningWorkerCount);

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
            logging.Log,
            readPlanStages: new IReadPlanStage[] { jobs.Transport });

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
