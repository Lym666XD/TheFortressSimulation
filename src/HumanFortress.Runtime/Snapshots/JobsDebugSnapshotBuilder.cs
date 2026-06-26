using HumanFortress.Runtime;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class JobsDebugSnapshotBuilder
{
    internal static SimulationJobsDebugData? Build(
        SimulationRuntimeHost<SimulationRuntimeSystems>? runtimeHost,
        ulong tick)
    {
        var systems = runtimeHost?.Systems;
        if (systems == null)
            return null;

        var transportStats = systems.TransportJobs.GetLastStatsSnapshot();
        var miningStats = systems.MiningJobs.GetLastStatsSnapshot();
        var craftStats = systems.CraftJobs.GetLastStatsSnapshot();
        var scheduler = systems.JobsOrchestrator.GetLastStats();
        var tunings = systems.SchedulerTunings;

        var miningActive = systems.MiningJobs.GetActiveJobsSnapshot();

        return new SimulationJobsDebugData(
            Tick: tick,
            Transport: MapTransportStats(transportStats),
            Mining: MapMiningStats(miningStats),
            Craft: MapCraftStats(craftStats),
            Construction: new ConstructionJobStatusView(
                systems.ConstructionJobs.LastProcessedSites,
                systems.ConstructionJobs.LastIntakeCount,
                tunings.Construction.PlanPerTick),
            Scheduler: new JobsSchedulerStatsView(
                scheduler.Tick,
                scheduler.PlanMsTotal,
                scheduler.ApplyMsTotal,
                scheduler.IntakeHaul,
                scheduler.IntakeMining,
                scheduler.IntakeConstruction,
                scheduler.IntakeCraft),
            TransportDebug: BuildTransportDebug(systems),
            ActiveJobs: BuildActiveJobs(systems, miningActive),
            ActiveMiningTargets: miningActive
                .Select(job => new JobPoint3(job.Target.X, job.Target.Y, job.Z))
                .ToList(),
            RecentMiningCompletions: systems.MiningJobs.GetRecentCompletions(tick)
                .Select(completion => new JobPoint3(completion.Cell.X, completion.Cell.Y, completion.Z))
                .ToList());
    }
}
