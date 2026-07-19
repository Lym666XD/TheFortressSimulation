using System.Text.Json;
using HumanFortress.Contracts.Runtime.Save;

namespace HumanFortress.Runtime.Save;

internal static class RuntimeSaveSnapshotDocumentCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    internal static string Serialize(RuntimeSaveSnapshotDocumentData document)
    {
        Validate(document);
        return JsonSerializer.Serialize(document, JsonOptions);
    }

    internal static RuntimeSaveSnapshotDocumentData Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Save snapshot document JSON must not be blank.", nameof(json));

        var document = JsonSerializer.Deserialize<RuntimeSaveSnapshotDocumentData>(json, JsonOptions);
        Validate(document);
        return document;
    }

    private static void Validate(RuntimeSaveSnapshotDocumentData document)
    {
        if (document.ExecutedCommandRecords == null)
            throw new InvalidDataException("Save snapshot document is missing executed command records.");
        if (document.PendingCommandRecords == null)
            throw new InvalidDataException("Save snapshot document is missing pending command records.");
        if (document.RngStreams == null)
            throw new InvalidDataException("Save snapshot document is missing RNG stream records.");

        ValidateRngStreams(document.RngStreams);
        ValidateMiningJobs(document.MiningJobs);
        ValidateTransportJobs(document.TransportJobs);
        ValidateCraftJobs(document.CraftJobs);
        ValidateCommandRecords(document.ExecutedCommandRecords, "executed");
        ValidateCommandRecords(document.PendingCommandRecords, "pending");
    }

    private static void ValidateMiningJobs(RuntimeSaveMiningJobsData? payload)
    {
        if (!payload.HasValue)
            return;

        var value = payload.Value;
        if (value.ActiveJobs == null)
            throw new InvalidDataException("Save snapshot mining job payload is missing active jobs.");
        if (value.BacklogEntries == null)
            throw new InvalidDataException("Save snapshot mining job payload is missing backlog entries.");
        if (value.DeferredStairwells == null)
            throw new InvalidDataException("Save snapshot mining job payload is missing deferred stairwells.");
        if (value.ReservedTiles == null)
            throw new InvalidDataException("Save snapshot mining job payload is missing reserved tiles.");
        if (value.RecentCompletions == null)
            throw new InvalidDataException("Save snapshot mining job payload is missing recent completions.");

        for (var i = 0; i < value.ActiveJobs.Length; i++)
            ValidateMiningActiveJob(value.ActiveJobs[i], i);
        for (var i = 0; i < value.BacklogEntries.Length; i++)
            ValidateMiningBacklogEntry(value.BacklogEntries[i], i);
        for (var i = 0; i < value.DeferredStairwells.Length; i++)
            ValidateMiningDeferredStairwell(value.DeferredStairwells[i], i);
        for (var i = 0; i < value.RecentCompletions.Length; i++)
            ValidateMiningRecentCompletion(value.RecentCompletions[i], i);
    }

    private static void ValidateMiningActiveJob(
        RuntimeSaveMiningActiveJobData job,
        int index)
    {
        if (job.Order < 0)
            throw new InvalidDataException($"Save snapshot mining active job {index} has a negative order.");
        if (job.WorkerId == Guid.Empty)
            throw new InvalidDataException($"Save snapshot mining active job {index} has an empty worker id.");
        if (job.ProgressTicks < 0)
            throw new InvalidDataException($"Save snapshot mining active job {index} has negative progress ticks.");
        if (job.RequiredTicks <= 0)
            throw new InvalidDataException($"Save snapshot mining active job {index} has non-positive required ticks.");
        if (job.ProgressTicks > job.RequiredTicks)
            throw new InvalidDataException($"Save snapshot mining active job {index} progress exceeds required ticks.");
        if (job.ReplanFailCount < 0)
            throw new InvalidDataException($"Save snapshot mining active job {index} has a negative replan fail count.");
        if (job.GeologyHandle < ushort.MinValue || job.GeologyHandle > ushort.MaxValue)
            throw new InvalidDataException($"Save snapshot mining active job {index} geology handle is out of range.");
        if (job.DesignationId <= 0)
            throw new InvalidDataException($"Save snapshot mining active job {index} has a non-positive designation id.");
    }

    private static void ValidateMiningBacklogEntry(
        RuntimeSaveMiningBacklogEntryData entry,
        int index)
    {
        if (entry.Order < 0)
            throw new InvalidDataException($"Save snapshot mining backlog entry {index} has a negative order.");
        ValidateMiningPlannedDig(entry.Dig, $"backlog entry {index}");
    }

    private static void ValidateMiningDeferredStairwell(
        RuntimeSaveMiningDeferredStairwellData entry,
        int index)
    {
        if (entry.Order < 0)
            throw new InvalidDataException($"Save snapshot mining deferred stairwell {index} has a negative order.");
        ValidateMiningPlannedDig(entry.Dig, $"deferred stairwell {index}");
    }

    private static void ValidateMiningPlannedDig(
        RuntimeSavePlannedMiningDigData dig,
        string label)
    {
        if (dig.GeologyHandle < ushort.MinValue || dig.GeologyHandle > ushort.MaxValue)
            throw new InvalidDataException($"Save snapshot mining {label} geology handle is out of range.");
        if (dig.DesignationId <= 0)
            throw new InvalidDataException($"Save snapshot mining {label} has a non-positive designation id.");
    }

    private static void ValidateMiningRecentCompletion(
        RuntimeSaveMiningRecentCompletionData completion,
        int index)
    {
        if (completion.Order < 0)
            throw new InvalidDataException($"Save snapshot mining recent completion {index} has a negative order.");
    }

    private static void ValidateTransportJobs(RuntimeSaveTransportJobsData? payload)
    {
        if (!payload.HasValue)
            return;

        var value = payload.Value;
        if (value.ReserveSlotsHint < 0)
            throw new InvalidDataException("Save snapshot transport job payload has a negative reserve-slot hint.");
        if (value.IntakeCapHint.HasValue && value.IntakeCapHint.Value <= 0)
            throw new InvalidDataException("Save snapshot transport job payload has an invalid intake cap hint.");
        if (value.MaxActiveCapHint.HasValue && value.MaxActiveCapHint.Value < 0)
            throw new InvalidDataException("Save snapshot transport job payload has an invalid max-active cap hint.");
        if (value.PendingRequests == null)
            throw new InvalidDataException("Save snapshot transport job payload is missing pending requests.");
        if (value.ActiveJobs == null)
            throw new InvalidDataException("Save snapshot transport job payload is missing active jobs.");
        if (value.BacklogEntries == null)
            throw new InvalidDataException("Save snapshot transport job payload is missing backlog entries.");

        for (var i = 0; i < value.PendingRequests.Length; i++)
            ValidateTransportRequest(value.PendingRequests[i], $"pending request {i}");
        for (var i = 0; i < value.ActiveJobs.Length; i++)
            ValidateTransportActiveJob(value.ActiveJobs[i], i);
        for (var i = 0; i < value.BacklogEntries.Length; i++)
            ValidateTransportBacklogEntry(value.BacklogEntries[i], i);
    }

    private static void ValidateTransportRequest(
        RuntimeSaveTransportRequestData request,
        string label)
    {
        if (request.ItemGuid == Guid.Empty)
            throw new InvalidDataException($"Save snapshot transport {label} has an empty item id.");
        if (request.Quantity < 0)
            throw new InvalidDataException($"Save snapshot transport {label} has a negative quantity.");
        if (request.RequestorId == null)
            throw new InvalidDataException($"Save snapshot transport {label} is missing requestor id.");
    }

    private static void ValidateTransportActiveJob(
        RuntimeSaveTransportActiveJobData job,
        int index)
    {
        if (job.CreatureId == Guid.Empty)
            throw new InvalidDataException($"Save snapshot transport active job {index} has an empty creature id.");
        if (job.ItemId == Guid.Empty)
            throw new InvalidDataException($"Save snapshot transport active job {index} has an empty item id.");
        if (job.Quantity <= 0)
            throw new InvalidDataException($"Save snapshot transport active job {index} has a non-positive quantity.");
        if (job.InvalidReplanCount < 0)
            throw new InvalidDataException($"Save snapshot transport active job {index} has a negative invalid-replan count.");
    }

    private static void ValidateTransportBacklogEntry(
        RuntimeSaveTransportBacklogEntryData entry,
        int index)
    {
        ValidateTransportRequest(entry.Request, $"backlog entry {index}");
    }

    private static void ValidateCraftJobs(RuntimeSaveCraftJobsData? payload)
    {
        if (!payload.HasValue)
            return;

        var value = payload.Value;
        if (value.ActiveJobs == null)
            throw new InvalidDataException("Save snapshot craft job payload is missing active jobs.");
        if (value.BacklogEntries == null)
            throw new InvalidDataException("Save snapshot craft job payload is missing backlog entries.");

        for (var i = 0; i < value.ActiveJobs.Length; i++)
            ValidateCraftActiveJob(value.ActiveJobs[i], i);
        for (var i = 0; i < value.BacklogEntries.Length; i++)
            ValidateCraftBacklogEntry(value.BacklogEntries[i], i);
    }

    private static void ValidateCraftActiveJob(
        RuntimeSaveCraftActiveJobData job,
        int index)
    {
        if (job.WorkerId == Guid.Empty)
            throw new InvalidDataException($"Save snapshot craft active job {index} has an empty worker id.");
        if (job.WorkshopGuid == Guid.Empty)
            throw new InvalidDataException($"Save snapshot craft active job {index} has an empty workshop id.");
        if (job.QueueEntryId == Guid.Empty)
            throw new InvalidDataException($"Save snapshot craft active job {index} has an empty queue entry id.");
        if (string.IsNullOrWhiteSpace(job.RecipeId))
            throw new InvalidDataException($"Save snapshot craft active job {index} has a blank recipe id.");
        if (job.WorkTicksRemaining < 0)
            throw new InvalidDataException($"Save snapshot craft active job {index} has negative work ticks remaining.");
    }

    private static void ValidateCraftBacklogEntry(
        RuntimeSaveCraftBacklogEntryData entry,
        int index)
    {
        if (entry.Job.WorkshopGuid == Guid.Empty)
            throw new InvalidDataException($"Save snapshot craft backlog entry {index} has an empty workshop id.");
        if (entry.Job.QueueEntryId == Guid.Empty)
            throw new InvalidDataException($"Save snapshot craft backlog entry {index} has an empty queue entry id.");
        if (string.IsNullOrWhiteSpace(entry.Job.RecipeId))
            throw new InvalidDataException($"Save snapshot craft backlog entry {index} has a blank recipe id.");
        if (entry.Job.DurationTicks < 0)
            throw new InvalidDataException($"Save snapshot craft backlog entry {index} has negative duration ticks.");
    }

    private static void ValidateRngStreams(IEnumerable<RuntimeSaveRngStreamRecordData> records)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var record in records)
        {
            if (string.IsNullOrWhiteSpace(record.StreamName))
                throw new InvalidDataException($"Save snapshot RNG stream {index} has a blank stream name.");
            if (!seen.Add(record.StreamName))
                throw new InvalidDataException($"Save snapshot RNG stream {index} duplicates stream '{record.StreamName}'.");
            index++;
        }
    }

    private static void ValidateCommandRecords(
        IEnumerable<RuntimeSaveCommandRecordData> records,
        string sectionName)
    {
        var index = 0;
        foreach (var record in records)
        {
            ValidateCommandRecord(record, sectionName, index);
            index++;
        }
    }

    private static void ValidateCommandRecord(
        RuntimeSaveCommandRecordData record,
        string sectionName,
        int index)
    {
        if (string.IsNullOrWhiteSpace(record.CommandType))
            throw new InvalidDataException($"Save snapshot {sectionName} command {index} has a blank command type.");
        if (record.PayloadLength < 0)
            throw new InvalidDataException($"Save snapshot {sectionName} command {index} has a negative payload length.");
        if (record.CommandIdentitySequence.HasValue && record.CommandIdentitySequence.Value <= 0)
            throw new InvalidDataException($"Save snapshot {sectionName} command {index} has an invalid command identity sequence.");
        if (record.PayloadBase64 == null)
            throw new InvalidDataException($"Save snapshot {sectionName} command {index} is missing payload bytes.");

        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(record.PayloadBase64);
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException($"Save snapshot {sectionName} command {index} payload is not valid base64.", ex);
        }

        if (payload.Length != record.PayloadLength)
            throw new InvalidDataException($"Save snapshot {sectionName} command {index} payload length does not match payload bytes.");
    }
}
