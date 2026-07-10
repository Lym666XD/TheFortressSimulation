using HumanFortress.Contracts.Runtime.Save;
using HumanFortress.Jobs.Mining;
using HumanFortress.Jobs.Replay;
using HumanFortress.Simulation.Orders;
using HumanFortress.Simulation.Tiles;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Save;

internal static class RuntimeSaveSnapshotDocumentMiningMapper
{
    internal static RuntimeSaveMiningJobsData ToDocumentData(MiningJobReplaySnapshot snapshot)
    {
        return new RuntimeSaveMiningJobsData(
            snapshot.ActiveJobs
                .OrderBy(static job => job.Order)
                .Select(ToDocumentActiveJob)
                .ToArray(),
            snapshot.BacklogEntries
                .OrderBy(static entry => entry.Order)
                .Select(ToDocumentBacklogEntry)
                .ToArray(),
            snapshot.DeferredStairwells
                .OrderBy(static entry => entry.Order)
                .Select(ToDocumentDeferredStairwell)
                .ToArray(),
            snapshot.ReservedTiles
                .OrderBy(static tile => tile.Z)
                .ThenBy(static tile => tile.Y)
                .ThenBy(static tile => tile.X)
                .Select(ToDocumentReservedTile)
                .ToArray(),
            snapshot.RecentCompletions
                .OrderBy(static completion => completion.Order)
                .Select(ToDocumentRecentCompletion)
                .ToArray());
    }

    internal static MiningJobReplaySnapshot ToReplaySnapshot(RuntimeSaveMiningJobsData payload)
    {
        return new MiningJobReplaySnapshot(
            (payload.ActiveJobs ?? Array.Empty<RuntimeSaveMiningActiveJobData>())
                .OrderBy(static job => job.Order)
                .Select(ToActiveJobSnapshot)
                .ToArray(),
            (payload.BacklogEntries ?? Array.Empty<RuntimeSaveMiningBacklogEntryData>())
                .OrderBy(static entry => entry.Order)
                .Select(ToBacklogEntrySnapshot)
                .ToArray(),
            (payload.DeferredStairwells ?? Array.Empty<RuntimeSaveMiningDeferredStairwellData>())
                .OrderBy(static entry => entry.Order)
                .Select(ToDeferredStairwellSnapshot)
                .ToArray(),
            (payload.ReservedTiles ?? Array.Empty<RuntimeSaveMiningReservedTileData>())
                .OrderBy(static tile => tile.Z)
                .ThenBy(static tile => tile.Y)
                .ThenBy(static tile => tile.X)
                .Select(ToReservedTileSnapshot)
                .ToArray(),
            (payload.RecentCompletions ?? Array.Empty<RuntimeSaveMiningRecentCompletionData>())
                .OrderBy(static completion => completion.Order)
                .Select(ToRecentCompletionSnapshot)
                .ToArray());
    }

    internal static string BuildReplayHash(RuntimeSaveMiningJobsData payload)
    {
        return MiningReplayHashBuilder.Build(ToReplaySnapshot(payload));
    }

    internal static int CountRecords(RuntimeSaveMiningJobsData payload)
    {
        return (payload.ActiveJobs?.Length ?? 0)
            + (payload.BacklogEntries?.Length ?? 0)
            + (payload.DeferredStairwells?.Length ?? 0)
            + (payload.ReservedTiles?.Length ?? 0)
            + (payload.RecentCompletions?.Length ?? 0);
    }

    private static RuntimeSaveMiningActiveJobData ToDocumentActiveJob(
        MiningActiveJobStateSnapshot job)
    {
        return new RuntimeSaveMiningActiveJobData(
            job.Order,
            job.WorkerId,
            job.Target.X,
            job.Target.Y,
            job.Z,
            job.Adjacent.X,
            job.Adjacent.Y,
            (int)job.Stage,
            job.ProgressTicks,
            job.RequiredTicks,
            job.GeologyHandle,
            (int)job.TerrainKind,
            job.Priority,
            job.AssignedTick,
            job.ReplanFailCount,
            (int)job.Action,
            (int)job.Segment,
            job.DesignationId);
    }

    private static RuntimeSaveMiningBacklogEntryData ToDocumentBacklogEntry(
        MiningBacklogEntrySnapshot entry)
    {
        return new RuntimeSaveMiningBacklogEntryData(
            entry.Order,
            ToDocumentPlannedDig(entry.Dig),
            entry.EnqueuedTick);
    }

    private static RuntimeSaveMiningDeferredStairwellData ToDocumentDeferredStairwell(
        MiningDeferredStairwellSnapshot entry)
    {
        return new RuntimeSaveMiningDeferredStairwellData(
            entry.Order,
            ToDocumentPlannedDig(entry.Dig));
    }

    private static RuntimeSaveMiningReservedTileData ToDocumentReservedTile(
        MiningReservedTileSnapshot tile)
    {
        return new RuntimeSaveMiningReservedTileData(tile.X, tile.Y, tile.Z);
    }

    private static RuntimeSaveMiningRecentCompletionData ToDocumentRecentCompletion(
        MiningRecentCompletionSnapshot completion)
    {
        return new RuntimeSaveMiningRecentCompletionData(
            completion.Order,
            completion.Cell.X,
            completion.Cell.Y,
            completion.Z,
            completion.ExpireTick);
    }

    private static RuntimeSavePlannedMiningDigData ToDocumentPlannedDig(
        MiningSystem.PlannedDig dig)
    {
        return new RuntimeSavePlannedMiningDigData(
            dig.Cell.X,
            dig.Cell.Y,
            dig.Z,
            dig.GeologyHandle,
            dig.TerrainKind,
            dig.Priority,
            dig.Seed,
            (int)dig.Action,
            (int)dig.Segment,
            dig.DesignationId);
    }

    private static MiningActiveJobStateSnapshot ToActiveJobSnapshot(
        RuntimeSaveMiningActiveJobData data)
    {
        ValidateGuid(data.WorkerId, "mining active job worker id");
        ValidateNonNegative(data.Order, "Mining active job order");
        ValidateNonNegative(data.ProgressTicks, "Mining active job progress ticks");
        if (data.RequiredTicks <= 0)
            throw new InvalidDataException("Mining active job required ticks must be positive.");
        ValidateNonNegative(data.ReplanFailCount, "Mining active job replan fail count");
        ValidatePositive(data.DesignationId, "Mining active job designation id");
        ValidateUShort(data.GeologyHandle, "Mining active job geology handle");
        ValidateEnum<MiningStage>(data.Stage, "mining active job stage");
        ValidateEnum<TerrainKind>(data.TerrainKind, "mining active job terrain kind");
        ValidateEnum<MiningAction>(data.Action, "mining active job action");
        ValidateEnum<MiningSegment>(data.Segment, "mining active job segment");

        return new MiningActiveJobStateSnapshot(
            data.Order,
            data.WorkerId,
            new Point(data.TargetX, data.TargetY),
            data.Z,
            new Point(data.AdjacentX, data.AdjacentY),
            (MiningStage)data.Stage,
            data.ProgressTicks,
            data.RequiredTicks,
            (ushort)data.GeologyHandle,
            (TerrainKind)data.TerrainKind,
            data.Priority,
            data.AssignedTick,
            data.ReplanFailCount,
            (MiningAction)data.Action,
            (MiningSegment)data.Segment,
            data.DesignationId);
    }

    private static MiningBacklogEntrySnapshot ToBacklogEntrySnapshot(
        RuntimeSaveMiningBacklogEntryData data)
    {
        ValidateNonNegative(data.Order, "Mining backlog entry order");
        return new MiningBacklogEntrySnapshot(
            data.Order,
            ToPlannedDig(data.Dig, "mining backlog dig"),
            data.EnqueuedTick);
    }

    private static MiningDeferredStairwellSnapshot ToDeferredStairwellSnapshot(
        RuntimeSaveMiningDeferredStairwellData data)
    {
        ValidateNonNegative(data.Order, "Mining deferred stairwell order");
        return new MiningDeferredStairwellSnapshot(
            data.Order,
            ToPlannedDig(data.Dig, "mining deferred stairwell dig"));
    }

    private static MiningReservedTileSnapshot ToReservedTileSnapshot(
        RuntimeSaveMiningReservedTileData data)
    {
        return new MiningReservedTileSnapshot(data.X, data.Y, data.Z);
    }

    private static MiningRecentCompletionSnapshot ToRecentCompletionSnapshot(
        RuntimeSaveMiningRecentCompletionData data)
    {
        ValidateNonNegative(data.Order, "Mining recent completion order");
        return new MiningRecentCompletionSnapshot(
            data.Order,
            new Point(data.X, data.Y),
            data.Z,
            data.ExpireTick);
    }

    private static MiningSystem.PlannedDig ToPlannedDig(
        RuntimeSavePlannedMiningDigData data,
        string fieldName)
    {
        ValidateUShort(data.GeologyHandle, $"{fieldName} geology handle");
        ValidateEnum<TerrainKind>(data.TerrainKind, $"{fieldName} terrain kind");
        ValidateEnum<MiningAction>(data.Action, $"{fieldName} action");
        ValidateEnum<MiningSegment>(data.Segment, $"{fieldName} segment");
        ValidatePositive(data.DesignationId, $"{fieldName} designation id");

        return new MiningSystem.PlannedDig(
            new Point(data.X, data.Y),
            data.Z,
            (ushort)data.GeologyHandle,
            (byte)data.TerrainKind,
            data.Priority,
            data.Seed,
            (MiningAction)data.Action,
            (MiningSegment)data.Segment,
            data.DesignationId);
    }

    private static void ValidateGuid(Guid value, string fieldName)
    {
        if (value == Guid.Empty)
            throw new InvalidDataException($"{fieldName} must not be empty.");
    }

    private static void ValidateNonNegative(int value, string fieldName)
    {
        if (value < 0)
            throw new InvalidDataException($"{fieldName} must not be negative.");
    }

    private static void ValidatePositive(int value, string fieldName)
    {
        if (value <= 0)
            throw new InvalidDataException($"{fieldName} must be positive.");
    }

    private static void ValidateUShort(int value, string fieldName)
    {
        if (value < ushort.MinValue || value > ushort.MaxValue)
            throw new InvalidDataException($"{fieldName} value {value} is outside ushort range.");
    }

    private static void ValidateEnum<T>(int value, string fieldName)
        where T : struct, Enum
    {
        if (!TryCreateEnumValue<T>(value, out var enumValue)
            || !Enum.IsDefined(typeof(T), enumValue))
        {
            throw new InvalidDataException($"{fieldName} value {value} is not supported.");
        }
    }

    private static bool TryCreateEnumValue<T>(
        int value,
        out object enumValue)
        where T : struct, Enum
    {
        try
        {
            var underlying = Enum.GetUnderlyingType(typeof(T));
            enumValue = Convert.ChangeType(value, underlying);
            return true;
        }
        catch (Exception ex) when (ex is InvalidCastException or OverflowException)
        {
            enumValue = default(T);
            return false;
        }
    }
}
