using HumanFortress.Jobs.Mining;
using HumanFortress.Runtime;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class JobsDebugSnapshotBuilder
{
    private static IReadOnlyList<ActiveJobView> BuildActiveJobs(
        SimulationRuntimeSystems systems,
        IReadOnlyList<MiningActiveJobView> miningActive)
    {
        var transportActive = systems.TransportJobs.GetActiveJobsSnapshot();
        var craftActive = systems.CraftJobs.GetActiveJobsSnapshot();

        var activeJobs = new List<ActiveJobView>(
            transportActive.Count + miningActive.Count + craftActive.Count);

        foreach (var job in transportActive)
        {
            activeJobs.Add(new ActiveJobView(
                "Haul",
                job.CreatureId,
                job.Stage,
                $"{job.Dest.X},{job.Dest.Y},{job.Dest.Z}"));
        }

        foreach (var job in miningActive)
        {
            activeJobs.Add(new ActiveJobView(
                "Mine",
                job.WorkerId,
                job.Stage,
                $"{job.Target.X},{job.Target.Y},{job.Z}"));
        }

        foreach (var job in craftActive)
        {
            activeJobs.Add(new ActiveJobView(
                "Craft",
                job.WorkerId,
                job.Stage,
                job.RecipeId));
        }

        return activeJobs;
    }
}
