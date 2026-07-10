using HumanFortress.Contracts.Runtime.Save;
using HumanFortress.Jobs.Craft;
using HumanFortress.Jobs.Replay;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Save;

internal static class RuntimeSaveSnapshotDocumentCraftMapper
{
    internal static RuntimeSaveCraftJobsData ToDocumentData(CraftJobReplaySnapshot snapshot)
    {
        return new RuntimeSaveCraftJobsData(
            snapshot.ActiveJobs
                .OrderBy(static job => job.Order)
                .Select(ToDocumentActiveJob)
                .ToArray(),
            snapshot.BacklogEntries
                .OrderBy(static entry => entry.Order)
                .Select(ToDocumentBacklogEntry)
                .ToArray());
    }

    internal static CraftJobReplaySnapshot ToReplaySnapshot(RuntimeSaveCraftJobsData payload)
    {
        return new CraftJobReplaySnapshot(
            (payload.ActiveJobs ?? Array.Empty<RuntimeSaveCraftActiveJobData>())
                .OrderBy(static job => job.Order)
                .Select(ToActiveJobSnapshot)
                .ToArray(),
            (payload.BacklogEntries ?? Array.Empty<RuntimeSaveCraftBacklogEntryData>())
                .OrderBy(static entry => entry.Order)
                .Select(ToBacklogEntrySnapshot)
                .ToArray());
    }

    internal static string BuildReplayHash(RuntimeSaveCraftJobsData payload)
    {
        return CraftReplayHashBuilder.Build(ToReplaySnapshot(payload));
    }

    internal static int CountRecords(RuntimeSaveCraftJobsData payload)
    {
        return (payload.ActiveJobs?.Length ?? 0)
            + (payload.BacklogEntries?.Length ?? 0);
    }

    private static RuntimeSaveCraftActiveJobData ToDocumentActiveJob(
        CraftActiveJobStateSnapshot job)
    {
        return new RuntimeSaveCraftActiveJobData(
            job.Order,
            job.WorkerId,
            job.WorkshopGuid,
            job.QueueEntryId,
            job.RecipeId,
            (int)job.Stage,
            job.WorkTicksRemaining,
            job.Anchor.X,
            job.Anchor.Y,
            job.Z);
    }

    private static RuntimeSaveCraftBacklogEntryData ToDocumentBacklogEntry(
        CraftBacklogEntrySnapshot entry)
    {
        return new RuntimeSaveCraftBacklogEntryData(
            entry.Order,
            ToDocumentPlannedCraftJob(entry.Job));
    }

    private static RuntimeSavePlannedCraftJobData ToDocumentPlannedCraftJob(PlannedCraftJob job)
    {
        return new RuntimeSavePlannedCraftJobData(
            job.WorkshopGuid,
            job.QueueEntryId,
            job.RecipeId,
            job.DurationTicks,
            job.Anchor.X,
            job.Anchor.Y,
            job.Z);
    }

    private static CraftActiveJobStateSnapshot ToActiveJobSnapshot(
        RuntimeSaveCraftActiveJobData data)
    {
        ValidateGuid(data.WorkerId, "craft active job worker id");
        ValidateGuid(data.WorkshopGuid, "craft active job workshop id");
        ValidateGuid(data.QueueEntryId, "craft active job queue entry id");
        ValidateRecipeId(data.RecipeId, "craft active job recipe id");
        ValidateEnum<CraftJobStage>(data.Stage, "craft active job stage");
        if (data.WorkTicksRemaining < 0)
            throw new InvalidDataException("Craft active job work ticks remaining must not be negative.");

        return new CraftActiveJobStateSnapshot(
            data.Order,
            data.WorkerId,
            data.WorkshopGuid,
            data.QueueEntryId,
            data.RecipeId,
            (CraftJobStage)data.Stage,
            data.WorkTicksRemaining,
            new Point(data.AnchorX, data.AnchorY),
            data.Z);
    }

    private static CraftBacklogEntrySnapshot ToBacklogEntrySnapshot(
        RuntimeSaveCraftBacklogEntryData data)
    {
        return new CraftBacklogEntrySnapshot(
            data.Order,
            ToPlannedCraftJob(data.Job));
    }

    private static PlannedCraftJob ToPlannedCraftJob(RuntimeSavePlannedCraftJobData data)
    {
        ValidateGuid(data.WorkshopGuid, "craft planned job workshop id");
        ValidateGuid(data.QueueEntryId, "craft planned job queue entry id");
        ValidateRecipeId(data.RecipeId, "craft planned job recipe id");
        if (data.DurationTicks < 0)
            throw new InvalidDataException("Craft planned job duration ticks must not be negative.");

        return new PlannedCraftJob(
            data.WorkshopGuid,
            data.QueueEntryId,
            data.RecipeId,
            data.DurationTicks,
            new Point(data.AnchorX, data.AnchorY),
            data.Z);
    }

    private static void ValidateGuid(Guid value, string fieldName)
    {
        if (value == Guid.Empty)
            throw new InvalidDataException($"{fieldName} must not be empty.");
    }

    private static void ValidateRecipeId(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"{fieldName} must not be blank.");
    }

    private static void ValidateEnum<T>(int value, string fieldName)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(typeof(T), value))
            throw new InvalidDataException($"{fieldName} value {value} is not supported.");
    }
}
