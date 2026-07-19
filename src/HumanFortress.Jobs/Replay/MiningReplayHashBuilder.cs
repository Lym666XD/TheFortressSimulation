using HumanFortress.Core.Determinism;
using HumanFortress.Jobs.Mining;
using HumanFortress.Simulation.Orders;
using SadRogue.Primitives;

namespace HumanFortress.Jobs.Replay;

internal static class MiningReplayHashBuilder
{
    internal static string Build(MiningJobReplaySnapshot snapshot)
    {
        return ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("jobs.mining.snapshot.v1");
            Append(hash, snapshot);
        });
    }

    internal static void Append(ReplayHashBuilder hash, MiningJobReplaySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(hash);

        AddActiveJobsHash(hash, snapshot.ActiveJobs);
        AddBacklogHash(hash, snapshot.BacklogEntries);
        AddDeferredHash(hash, snapshot.DeferredStairwells);
        AddReservedTilesHash(hash, snapshot.ReservedTiles);
        AddRecentCompletionsHash(hash, snapshot.RecentCompletions);
    }

    private static void AddActiveJobsHash(
        ReplayHashBuilder hash,
        IReadOnlyList<MiningActiveJobStateSnapshot> activeJobs)
    {
        hash.AddInt32(activeJobs.Count);
        foreach (var job in activeJobs.OrderBy(job => job.Order))
        {
            hash.AddInt32(job.Order);
            hash.AddGuid(job.WorkerId);
            AddPointHash(hash, job.Target);
            hash.AddInt32(job.Z);
            AddPointHash(hash, job.Adjacent);
            hash.AddInt32((int)job.Stage);
            hash.AddInt32(job.ProgressTicks);
            hash.AddInt32(job.RequiredTicks);
            hash.AddInt32(job.GeologyHandle);
            hash.AddInt32((int)job.TerrainKind);
            hash.AddInt32(job.Priority);
            hash.AddUInt64(job.AssignedTick);
            hash.AddInt32(job.ReplanFailCount);
            hash.AddInt32((int)job.Action);
            hash.AddInt32((int)job.Segment);
            hash.AddInt32(job.DesignationId);
            hash.AddByte(job.PathSearchAttempt);
        }
    }

    private static void AddBacklogHash(
        ReplayHashBuilder hash,
        IReadOnlyList<MiningBacklogEntrySnapshot> entries)
    {
        hash.AddInt32(entries.Count);
        foreach (var entry in entries.OrderBy(entry => entry.Order))
        {
            hash.AddInt32(entry.Order);
            AddPlannedDigHash(hash, entry.Dig);
            hash.AddUInt64(entry.EnqueuedTick);
        }
    }

    private static void AddDeferredHash(
        ReplayHashBuilder hash,
        IReadOnlyList<MiningDeferredStairwellSnapshot> entries)
    {
        hash.AddInt32(entries.Count);
        foreach (var entry in entries.OrderBy(entry => entry.Order))
        {
            hash.AddInt32(entry.Order);
            AddPlannedDigHash(hash, entry.Dig);
        }
    }

    private static void AddReservedTilesHash(
        ReplayHashBuilder hash,
        IReadOnlyList<MiningReservedTileSnapshot> tiles)
    {
        var ordered = tiles
            .OrderBy(tile => tile.Z)
            .ThenBy(tile => tile.Y)
            .ThenBy(tile => tile.X)
            .ToArray();

        hash.AddInt32(ordered.Length);
        foreach (var tile in ordered)
        {
            hash.AddInt32(tile.X);
            hash.AddInt32(tile.Y);
            hash.AddInt32(tile.Z);
        }
    }

    private static void AddRecentCompletionsHash(
        ReplayHashBuilder hash,
        IReadOnlyList<MiningRecentCompletionSnapshot> completions)
    {
        hash.AddInt32(completions.Count);
        foreach (var completion in completions.OrderBy(completion => completion.Order))
        {
            hash.AddInt32(completion.Order);
            AddPointHash(hash, completion.Cell);
            hash.AddInt32(completion.Z);
            hash.AddUInt64(completion.ExpireTick);
        }
    }

    private static void AddPlannedDigHash(ReplayHashBuilder hash, MiningSystem.PlannedDig dig)
    {
        AddPointHash(hash, dig.Cell);
        hash.AddInt32(dig.Z);
        hash.AddInt32(dig.GeologyHandle);
        hash.AddByte(dig.TerrainKind);
        hash.AddInt32(dig.Priority);
        hash.AddUInt64(dig.Seed);
        hash.AddInt32((int)dig.Action);
        hash.AddInt32((int)dig.Segment);
        hash.AddInt32(dig.DesignationId);
        hash.AddByte(dig.PathSearchAttempt);
    }

    private static void AddPointHash(ReplayHashBuilder hash, Point point)
    {
        hash.AddInt32(point.X);
        hash.AddInt32(point.Y);
    }
}
