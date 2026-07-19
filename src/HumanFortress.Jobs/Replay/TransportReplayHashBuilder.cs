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
            hash.AddString("jobs.transport.snapshot.v2");
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
            hash.AddByte(job.PathSearchAttempt);
            AddMovementCursorHash(hash, job.MovementCursor);
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
        hash.AddByte(request.PathSearchAttempt);
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

    private static void AddMovementCursorHash(
        ReplayHashBuilder hash,
        MovementCursorData? cursor)
    {
        hash.AddBoolean(cursor.HasValue);
        if (!cursor.HasValue)
            return;

        var value = cursor.Value;
        hash.AddUInt64(value.EntityKey);
        hash.AddUInt64(value.Revision);
        AddPoint3Hash(hash, value.Request.Source);
        AddPoint3Hash(hash, value.Request.Destination);
        hash.AddInt32((int)value.Request.Mode);
        hash.AddInt32((int)value.Request.Flags);
        hash.AddUInt32(value.Request.Seed);
        hash.AddByte(value.Request.EffectiveSearchAttempt);
        hash.AddInt32((int)value.Path.Kind);
        hash.AddInt32(value.Path.Length);
        hash.AddUInt32(value.Path.TotalCost);
        hash.AddUInt32(value.Path.Hash);
        hash.AddInt32(value.Path.Steps.Length);
        foreach (var step in value.Path.Steps.Span)
        {
            AddPoint3Hash(hash, step.Position);
            hash.AddInt32(step.Cost);
        }

        hash.AddInt32(value.CurrentStep);
        AddPoint3Hash(hash, value.Position);
        hash.AddInt32(value.StuckTicks);
        hash.AddInt32(value.LastProgress);
        hash.AddInt32(value.LastConnectivityVersion);
        hash.AddInt32(value.StepWait);
    }
}
