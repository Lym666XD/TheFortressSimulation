using HumanFortress.Contracts.Navigation;
using HumanFortress.Core.Determinism;
using HumanFortress.Jobs.Transport;
using HumanFortress.Simulation.Jobs;
using SadRogue.Primitives;

namespace HumanFortress.Jobs.Replay;

internal static class TransportReplayHashBuilder
{
    internal static string Build(
        TransportRequestQueueStateSnapshot queue,
        TransportJobReplaySnapshot executor)
    {
        return ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("jobs.transport.snapshot.v1");
            Append(hash, queue, executor);
        });
    }

    internal static void Append(
        ReplayHashBuilder hash,
        TransportRequestQueueStateSnapshot queue,
        TransportJobReplaySnapshot executor)
    {
        ArgumentNullException.ThrowIfNull(hash);

        AddRequestQueueHash(hash, queue);
        AddExecutorHash(hash, executor);
    }

    private static void AddRequestQueueHash(ReplayHashBuilder hash, TransportRequestQueueStateSnapshot queue)
    {
        var pending = queue.PendingRequests
            .OrderBy(request => request.CreatedTick)
            .ThenBy(request => request.Priority)
            .ThenBy(request => request.RequestorId, StringComparer.Ordinal)
            .ThenBy(request => request.ItemGuid)
            .ToArray();

        hash.AddInt32(pending.Length);
        foreach (var request in pending)
        {
            AddTransportRequestHash(hash, request);
        }
    }

    private static void AddExecutorHash(ReplayHashBuilder hash, TransportJobReplaySnapshot executor)
    {
        AddNullableInt32(hash, executor.IntakeCapHint);
        AddNullableInt32(hash, executor.MaxActiveCapHint);
        hash.AddInt32(executor.ReserveSlotsHint);

        hash.AddInt32(executor.ActiveJobs.Count);
        foreach (var job in executor.ActiveJobs.OrderBy(job => job.Order))
        {
            hash.AddInt32(job.Order);
            hash.AddGuid(job.CreatureId);
            hash.AddGuid(job.ItemId);
            AddPoint3Hash(hash, job.Destination);
            hash.AddInt32((int)job.Stage);
            hash.AddInt32(job.Quantity);
            hash.AddInt32(job.InvalidReplanCount);
            hash.AddInt32((int)job.Reason);
        }

        hash.AddInt32(executor.BacklogEntries.Count);
        foreach (var entry in executor.BacklogEntries.OrderBy(entry => entry.Order))
        {
            hash.AddInt32(entry.Order);
            AddTransportRequestHash(hash, entry.Request);
            hash.AddUInt64(entry.EnqueuedTick);
        }
    }

    private static void AddTransportRequestHash(ReplayHashBuilder hash, TransportRequest request)
    {
        hash.AddGuid(request.ItemGuid);
        AddPointHash(hash, request.From);
        hash.AddInt32(request.FromZ);
        AddPointHash(hash, request.To);
        hash.AddInt32(request.ToZ);
        hash.AddInt32(request.Quantity);
        hash.AddInt32((int)request.Reason);
        hash.AddInt32(request.Priority);
        hash.AddString(request.RequestorId);
        hash.AddUInt64(request.CreatedTick);
        hash.AddUInt32(request.Seed);
    }

    private static void AddPointHash(ReplayHashBuilder hash, Point point)
    {
        hash.AddInt32(point.X);
        hash.AddInt32(point.Y);
    }

    private static void AddPoint3Hash(ReplayHashBuilder hash, Point3 point)
    {
        hash.AddInt32(point.X);
        hash.AddInt32(point.Y);
        hash.AddInt32(point.Z);
    }

    private static void AddNullableInt32(ReplayHashBuilder hash, int? value)
    {
        hash.AddBoolean(value.HasValue);
        if (value.HasValue)
        {
            hash.AddInt32(value.Value);
        }
    }
}
