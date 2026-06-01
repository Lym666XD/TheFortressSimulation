using HumanFortress.App.Jobs;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Navigation;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Jobs;
using HumanFortress.Simulation.Orders;
using HumanFortress.Simulation.World;

namespace HumanFortress.App.Runtime;

/// <summary>
/// Builds and exposes the simulation systems that participate in the runtime tick loop.
/// </summary>
internal sealed class SimulationRuntimeSystems
{
    private SimulationRuntimeSystems(
        HaulingSystem haulingPlanner,
        ITransportRequestQueue transportQueue,
        TransportJobSystem transportJobs,
        MiningSystem miningPlanner,
        BuildableConstructionSystem buildablePlanner,
        ConstructionMaterialsPlanner constructionMaterialsPlanner,
        MiningJobSystem miningJobs,
        ConstructionSystem constructionPlanner,
        ConstructionJobSystem constructionJobs,
        CraftPlanner craftPlanner,
        CraftJobSystem craftJobs,
        ProfessionAssignments professionAssignments,
        SchedulerTunings schedulerTunings,
        WorkshopTunings workshopTunings,
        UnifiedJobsOrchestrator jobsOrchestrator,
        SanitizeSystem sanitizer)
    {
        HaulingPlanner = haulingPlanner;
        TransportQueue = transportQueue;
        TransportJobs = transportJobs;
        MiningPlanner = miningPlanner;
        BuildablePlanner = buildablePlanner;
        ConstructionMaterialsPlanner = constructionMaterialsPlanner;
        MiningJobs = miningJobs;
        ConstructionPlanner = constructionPlanner;
        ConstructionJobs = constructionJobs;
        CraftPlanner = craftPlanner;
        CraftJobs = craftJobs;
        ProfessionAssignments = professionAssignments;
        SchedulerTunings = schedulerTunings;
        WorkshopTunings = workshopTunings;
        JobsOrchestrator = jobsOrchestrator;
        Sanitizer = sanitizer;
    }

    public HaulingSystem HaulingPlanner { get; }
    public ITransportRequestQueue TransportQueue { get; }
    public TransportJobSystem TransportJobs { get; }
    public MiningSystem MiningPlanner { get; }
    public BuildableConstructionSystem BuildablePlanner { get; }
    public ConstructionMaterialsPlanner ConstructionMaterialsPlanner { get; }
    public MiningJobSystem MiningJobs { get; }
    public ConstructionSystem ConstructionPlanner { get; }
    public ConstructionJobSystem ConstructionJobs { get; }
    public CraftPlanner CraftPlanner { get; }
    public CraftJobSystem CraftJobs { get; }
    public ProfessionAssignments ProfessionAssignments { get; }
    public SchedulerTunings SchedulerTunings { get; }
    public WorkshopTunings WorkshopTunings { get; }
    public UnifiedJobsOrchestrator JobsOrchestrator { get; }
    public SanitizeSystem Sanitizer { get; }

    public static SimulationRuntimeSystems Create(
        World world,
        DiffLog diffLog,
        ItemsDiffLog itemsDiffLog,
        NavigationManager navigation,
        string baseDir)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(diffLog);
        ArgumentNullException.ThrowIfNull(itemsDiffLog);
        ArgumentNullException.ThrowIfNull(navigation);

        var miningPlanner = new MiningSystem(world, world.Orders);
        var transportQueue = new TransportRequestQueue();
        var haulingPlanner = new HaulingSystem(world, world.Orders, transportIntake: transportQueue);
        var constructionMaterialsPlanner = new ConstructionMaterialsPlanner(world, transportQueue);
        ConstructionMaterialsPlanner.LogCallback = msg => Logger.Log(msg);
        var constructionPlanner = new ConstructionSystem(world, world.Orders);
        var buildablePlanner = new BuildableConstructionSystem(world, world.Orders);

        var schedulerTunings = SchedulerTunings.LoadFromContent(baseDir);
        var workshopTunings = WorkshopTunings.LoadFromContent(baseDir);
        var professionRegistry = ProfessionRegistry.Load(baseDir);
        var professionAssignments = new ProfessionAssignments(professionRegistry);

        var miningJobs = new MiningJobSystem(
            world,
            miningPlanner,
            diffLog,
            itemsDiffLog,
            navigation,
            intakeBudget: schedulerTunings.Mining.PlanPerTick,
            carryoverMaxTicks: schedulerTunings.BackpressureMaxCarryoverTicks,
            professions: professionAssignments,
            workerStrategy: schedulerTunings.WorkerSelection);

        var transportJobs = new TransportJobSystem(
            world,
            transportQueue,
            diffLog,
            navigation,
            itemsDiffLog: itemsDiffLog,
            intakeBudget: schedulerTunings.Hauling.PlanPerTick,
            carryoverMaxTicks: schedulerTunings.BackpressureMaxCarryoverTicks,
            maxActiveJobs: schedulerTunings.HaulingLimits.MaxActive,
            professions: professionAssignments,
            workerStrategy: schedulerTunings.WorkerSelection);

        var constructionJobs = new ConstructionJobSystem(
            world,
            constructionPlanner,
            diffLog,
            itemsDiffLog,
            maxPerTick: schedulerTunings.Construction.PlanPerTick);

        var craftPlanner = new CraftPlanner(world, transportQueue, workshopTunings);
        var craftJobs = new CraftJobSystem(
            world,
            craftPlanner,
            itemsDiffLog,
            navigation,
            professionAssignments,
            schedulerTunings.WorkerSelection);

        var sanitizer = new SanitizeSystem(world, diffLog, intervalTicks: 40, maxPerTick: 8);

        var jobsOrchestrator = new UnifiedJobsOrchestrator(
            haulingPlanner,
            constructionMaterialsPlanner,
            miningPlanner,
            constructionPlanner,
            craftPlanner,
            transportJobs,
            miningJobs,
            constructionJobs,
            craftJobs,
            schedulerTunings);

        return new SimulationRuntimeSystems(
            haulingPlanner,
            transportQueue,
            transportJobs,
            miningPlanner,
            buildablePlanner,
            constructionMaterialsPlanner,
            miningJobs,
            constructionPlanner,
            constructionJobs,
            craftPlanner,
            craftJobs,
            professionAssignments,
            schedulerTunings,
            workshopTunings,
            jobsOrchestrator,
            sanitizer);
    }

    public void RegisterWith(TickScheduler scheduler)
    {
        scheduler.RegisterSystem(BuildablePlanner);
        scheduler.RegisterSystem(JobsOrchestrator);
        scheduler.RegisterSystem(Sanitizer);
    }
}
