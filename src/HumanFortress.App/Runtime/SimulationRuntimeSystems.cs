using HumanFortress.App.Jobs;
using HumanFortress.Core.Time;
using HumanFortress.Jobs.Craft;
using HumanFortress.Runtime;
using HumanFortress.Simulation.Jobs;
using HumanFortress.Simulation.Orders;

namespace HumanFortress.App.Runtime;

/// <summary>
/// Exposes the simulation systems that participate in the runtime tick loop.
/// </summary>
internal sealed class SimulationRuntimeSystems : IRuntimeTickSystems
{
    internal SimulationRuntimeSystems(
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

    public void RegisterWith(TickScheduler scheduler)
    {
        scheduler.RegisterSystem(BuildablePlanner);
        scheduler.RegisterSystem(JobsOrchestrator);
        scheduler.RegisterSystem(Sanitizer);
    }
}
