using HumanFortress.Contracts.Navigation;
using HumanFortress.Contracts.Runtime.Save;
using HumanFortress.Jobs.Replay;
using HumanFortress.Jobs.Transport;
using HumanFortress.Simulation.Jobs;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Save;

internal static class RuntimeSaveSnapshotDocumentTransportMapper
{
    internal static RuntimeSaveTransportJobsData ToDocumentData(
        TransportRequestQueueStateSnapshot queue,
        TransportJobReplaySnapshot executor)
    {
        return new RuntimeSaveTransportJobsData(
            executor.IntakeCapHint,
            executor.MaxActiveCapHint,
            executor.ReserveSlotsHint,
            queue.PendingRequests
                .Select(ToDocumentRequest)
                .ToArray(),
            executor.ActiveJobs
                .OrderBy(static job => job.Order)
                .Select(ToDocumentActiveJob)
                .ToArray(),
            executor.BacklogEntries
                .OrderBy(static entry => entry.Order)
                .Select(ToDocumentBacklogEntry)
                .ToArray());
    }

    internal static TransportRequestQueueStateSnapshot ToQueueSnapshot(
        RuntimeSaveTransportJobsData payload)
    {
        return new TransportRequestQueueStateSnapshot(
            (payload.PendingRequests ?? Array.Empty<RuntimeSaveTransportRequestData>())
                .Select(ToTransportRequest)
                .OrderBy(static request => request.CreatedTick)
                .ThenBy(static request => request.Priority)
                .ThenBy(static request => request.RequestorId, StringComparer.Ordinal)
                .ThenBy(static request => request.ItemGuid)
                .ToArray());
    }

    internal static TransportJobReplaySnapshot ToReplaySnapshot(
        RuntimeSaveTransportJobsData payload)
    {
        return new TransportJobReplaySnapshot(
            payload.IntakeCapHint,
            payload.MaxActiveCapHint,
            payload.ReserveSlotsHint,
            (payload.ActiveJobs ?? Array.Empty<RuntimeSaveTransportActiveJobData>())
                .OrderBy(static job => job.Order)
                .Select(ToActiveJobSnapshot)
                .ToArray(),
            (payload.BacklogEntries ?? Array.Empty<RuntimeSaveTransportBacklogEntryData>())
                .OrderBy(static entry => entry.Order)
                .Select(ToBacklogEntrySnapshot)
                .ToArray());
    }

    internal static string BuildReplayHash(RuntimeSaveTransportJobsData payload)
    {
        return TransportReplayHashBuilder.Build(
            ToQueueSnapshot(payload),
            ToReplaySnapshot(payload));
    }

    internal static int CountRecords(RuntimeSaveTransportJobsData payload)
    {
        var schedulingHintCount = payload.IntakeCapHint.HasValue
            || payload.MaxActiveCapHint.HasValue
            || payload.ReserveSlotsHint != 0
            ? 1
            : 0;

        return (payload.PendingRequests?.Length ?? 0)
            + (payload.ActiveJobs?.Length ?? 0)
            + (payload.BacklogEntries?.Length ?? 0)
            + schedulingHintCount;
    }

    private static RuntimeSaveTransportRequestData ToDocumentRequest(TransportRequest request)
    {
        return new RuntimeSaveTransportRequestData(
            request.ItemGuid,
            request.From.X,
            request.From.Y,
            request.FromZ,
            request.To.X,
            request.To.Y,
            request.ToZ,
            request.Quantity,
            (int)request.Reason,
            request.Priority,
            request.RequestorId,
            request.CreatedTick,
            request.Seed);
    }

    private static RuntimeSaveTransportActiveJobData ToDocumentActiveJob(
        TransportActiveJobStateSnapshot job)
    {
        return new RuntimeSaveTransportActiveJobData(
            job.Order,
            job.CreatureId,
            job.ItemId,
            job.Destination.X,
            job.Destination.Y,
            job.Destination.Z,
            (int)job.Stage,
            job.Quantity,
            job.InvalidReplanCount,
            (int)job.Reason);
    }

    private static RuntimeSaveTransportBacklogEntryData ToDocumentBacklogEntry(
        TransportBacklogEntrySnapshot entry)
    {
        return new RuntimeSaveTransportBacklogEntryData(
            entry.Order,
            ToDocumentRequest(entry.Request),
            entry.EnqueuedTick);
    }

    private static TransportRequest ToTransportRequest(RuntimeSaveTransportRequestData data)
    {
        ValidateGuid(data.ItemGuid, "transport request item id");
        ValidateEnum<TransportReason>(data.Reason, "transport request reason");
        if (data.Quantity < 0)
            throw new InvalidDataException("Transport request quantity must not be negative.");
        if (data.RequestorId == null)
            throw new InvalidDataException("Transport request requestor id is missing.");

        return new TransportRequest(
            data.ItemGuid,
            new Point(data.FromX, data.FromY),
            data.FromZ,
            new Point(data.ToX, data.ToY),
            data.ToZ,
            data.Quantity,
            (TransportReason)data.Reason,
            data.Priority,
            data.RequestorId,
            data.CreatedTick,
            data.Seed);
    }

    private static TransportActiveJobStateSnapshot ToActiveJobSnapshot(
        RuntimeSaveTransportActiveJobData data)
    {
        ValidateGuid(data.CreatureId, "transport active job creature id");
        ValidateGuid(data.ItemId, "transport active job item id");
        ValidateEnum<JobStage>(data.Stage, "transport active job stage");
        ValidateEnum<TransportReason>(data.Reason, "transport active job reason");
        if (data.Quantity <= 0)
            throw new InvalidDataException("Transport active job quantity must be positive.");
        if (data.InvalidReplanCount < 0)
            throw new InvalidDataException("Transport active job invalid replan count must not be negative.");

        return new TransportActiveJobStateSnapshot(
            data.Order,
            data.CreatureId,
            data.ItemId,
            new Point3(data.DestinationX, data.DestinationY, data.DestinationZ),
            (JobStage)data.Stage,
            data.Quantity,
            data.InvalidReplanCount,
            (TransportReason)data.Reason);
    }

    private static TransportBacklogEntrySnapshot ToBacklogEntrySnapshot(
        RuntimeSaveTransportBacklogEntryData data)
    {
        return new TransportBacklogEntrySnapshot(
            data.Order,
            ToTransportRequest(data.Request),
            data.EnqueuedTick);
    }

    private static void ValidateGuid(Guid value, string fieldName)
    {
        if (value == Guid.Empty)
            throw new InvalidDataException($"{fieldName} must not be empty.");
    }

    private static void ValidateEnum<T>(int value, string fieldName)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(typeof(T), value))
            throw new InvalidDataException($"{fieldName} value {value} is not supported.");
    }
}
