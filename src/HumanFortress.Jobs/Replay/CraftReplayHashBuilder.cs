using HumanFortress.Core.Determinism;
using HumanFortress.Jobs.Craft;
using SadRogue.Primitives;

namespace HumanFortress.Jobs.Replay;

internal static class CraftReplayHashBuilder
{
    internal static string Build(CraftJobReplaySnapshot snapshot)
    {
        return ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("jobs.craft.snapshot.v1");
            Append(hash, snapshot);
        });
    }

    internal static void Append(ReplayHashBuilder hash, CraftJobReplaySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(hash);

        AddActiveJobsHash(hash, snapshot.ActiveJobs);
        AddBacklogHash(hash, snapshot.BacklogEntries);
    }

    private static void AddActiveJobsHash(
        ReplayHashBuilder hash,
        IReadOnlyList<CraftActiveJobStateSnapshot> activeJobs)
    {
        hash.AddInt32(activeJobs.Count);
        foreach (var job in activeJobs.OrderBy(job => job.Order))
        {
            hash.AddInt32(job.Order);
            hash.AddGuid(job.WorkerId);
            hash.AddGuid(job.WorkshopGuid);
            hash.AddGuid(job.QueueEntryId);
            hash.AddString(job.RecipeId);
            hash.AddInt32((int)job.Stage);
            hash.AddInt32(job.WorkTicksRemaining);
            AddPointHash(hash, job.Anchor);
            hash.AddInt32(job.Z);
        }
    }

    private static void AddBacklogHash(
        ReplayHashBuilder hash,
        IReadOnlyList<CraftBacklogEntrySnapshot> entries)
    {
        hash.AddInt32(entries.Count);
        foreach (var entry in entries.OrderBy(entry => entry.Order))
        {
            hash.AddInt32(entry.Order);
            AddPlannedCraftJobHash(hash, entry.Job);
        }
    }

    private static void AddPlannedCraftJobHash(ReplayHashBuilder hash, PlannedCraftJob job)
    {
        hash.AddGuid(job.WorkshopGuid);
        hash.AddGuid(job.QueueEntryId);
        hash.AddString(job.RecipeId);
        hash.AddInt32(job.DurationTicks);
        AddPointHash(hash, job.Anchor);
        hash.AddInt32(job.Z);
    }

    private static void AddPointHash(ReplayHashBuilder hash, Point point)
    {
        hash.AddInt32(point.X);
        hash.AddInt32(point.Y);
    }
}
