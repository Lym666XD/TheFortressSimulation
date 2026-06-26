using HumanFortress.Core.Simulation;
using HumanFortress.Navigation;
using HumanFortress.Runtime.Jobs;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime;

internal sealed partial class FortressRuntimeJobSystems
{
    private static MiningJobSystem CreateMining(
        World world,
        DiffLog diffLog,
        ItemsDiffLog itemsDiffLog,
        NavigationManager navigation,
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
            miningTuningJson: dependencies.MiningTuningJson,
            geology: dependencies.Geology,
            log: logging.Log);
    }

    private static TransportJobSystem CreateTransport(
        World world,
        DiffLog diffLog,
        ItemsDiffLog itemsDiffLog,
        NavigationManager navigation,
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
            intakeBudget: schedulerTunings.Hauling.PlanPerTick,
            carryoverMaxTicks: schedulerTunings.BackpressureMaxCarryoverTicks,
            maxActiveJobs: schedulerTunings.HaulingLimits.MaxActive,
            professions: dependencies.ProfessionAssignments,
            workerStrategy: schedulerTunings.WorkerSelection,
            navigationTuning: dependencies.NavigationTuning,
            log: logging.Log);
    }
}
