using HumanFortress.Core.Simulation;
using HumanFortress.Navigation.Implementation;
using HumanFortress.Runtime.Navigation;
using HumanFortress.Runtime.Jobs;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Stockpile;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Composition;

internal sealed partial class FortressRuntimeJobSystems
{
    private static MiningJobSystem CreateMining(
        World world,
        DiffLog diffLog,
        ItemsDiffLog itemsDiffLog,
        NavigationManager navigation,
        RuntimeNavigationServices navigationServices,
        FortressRuntimeDependencies dependencies,
        FortressRuntimePlanningSystems planners,
        FortressRuntimeLogging logging)
    {
        var schedulerTunings = dependencies.SchedulerTunings;

        return new MiningJobSystem(
            world,
            planners.Mining,
            diffLog,
            itemsDiffLog,
            navigation,
            intakeBudget: schedulerTunings.Mining.PlanPerTick,
            carryoverMaxTicks: schedulerTunings.BackpressureMaxCarryoverTicks,
            professions: dependencies.ProfessionAssignments,
            workerStrategy: schedulerTunings.WorkerSelection,
            navigationTuning: dependencies.NavigationTuning,
            navigationServices: navigationServices,
            miningTuningJson: dependencies.MiningTuningJson,
            geology: dependencies.Geology,
            log: logging.Log);
    }

    private static TransportJobSystem CreateTransport(
        World world,
        DiffLog diffLog,
        ItemsDiffLog itemsDiffLog,
        StockpileDiffLog stockpileDiffLog,
        NavigationManager navigation,
        RuntimeNavigationServices navigationServices,
        FortressRuntimeDependencies dependencies,
        FortressRuntimePlanningSystems planners,
        FortressRuntimeLogging logging)
    {
        var schedulerTunings = dependencies.SchedulerTunings;

        return new TransportJobSystem(
            world,
            planners.TransportQueue,
            diffLog,
            navigation,
            itemsDiffLog: itemsDiffLog,
            stockpileDiffLog: stockpileDiffLog,
            intakeBudget: schedulerTunings.Hauling.PlanPerTick,
            carryoverMaxTicks: schedulerTunings.BackpressureMaxCarryoverTicks,
            maxActiveJobs: schedulerTunings.HaulingLimits.MaxActive,
            professions: dependencies.ProfessionAssignments,
            workerStrategy: schedulerTunings.WorkerSelection,
            navigationTuning: dependencies.NavigationTuning,
            navigationServices: navigationServices,
            log: logging.Log);
    }
}
