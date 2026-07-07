using HumanFortress.Core.Time;
using HumanFortress.Jobs.Configuration;
using HumanFortress.Jobs.Craft;
using HumanFortress.Jobs.Orchestration;
using HumanFortress.Jobs.Profession;
using HumanFortress.Jobs.Safety;
using HumanFortress.Runtime.Host;
using HumanFortress.Runtime.Jobs;
using HumanFortress.Simulation.Jobs;
using HumanFortress.Simulation.Orders;

namespace HumanFortress.Runtime.Composition;

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

    internal HaulingSystem HaulingPlanner { get; }
    internal ITransportRequestQueue TransportQueue { get; }
    internal TransportJobSystem TransportJobs { get; }
    internal MiningSystem MiningPlanner { get; }
    internal BuildableConstructionSystem BuildablePlanner { get; }
    internal ConstructionMaterialsPlanner ConstructionMaterialsPlanner { get; }
    internal MiningJobSystem MiningJobs { get; }
    internal ConstructionSystem ConstructionPlanner { get; }
    internal ConstructionJobSystem ConstructionJobs { get; }
    internal CraftPlanner CraftPlanner { get; }
    internal CraftJobSystem CraftJobs { get; }
    internal ProfessionAssignments ProfessionAssignments { get; }
    internal SchedulerTunings SchedulerTunings { get; }
    internal WorkshopTunings WorkshopTunings { get; }
    internal UnifiedJobsOrchestrator JobsOrchestrator { get; }
    internal SanitizeSystem Sanitizer { get; }

    void IRuntimeTickSystems.RegisterWith(TickScheduler scheduler)
    {
        scheduler.RegisterSystem(BuildablePlanner);
        scheduler.RegisterSystem(JobsOrchestrator);
        scheduler.RegisterSystem(Sanitizer);
    }
}
