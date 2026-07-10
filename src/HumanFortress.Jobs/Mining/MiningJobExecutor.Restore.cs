using HumanFortress.Contracts.Navigation;
using HumanFortress.Core.Simulation;
using HumanFortress.Simulation.Orders;
using HumanFortress.Simulation.Tiles;
using SadRogue.Primitives;

namespace HumanFortress.Jobs.Mining;

internal sealed partial class MiningJobExecutor
{
    internal MiningJobRestoreResult RestoreReplaySnapshot(MiningJobReplaySnapshot snapshot)
    {
        var issues = ValidateReplaySnapshot(snapshot);
        if (issues.Count > 0)
            return new MiningJobRestoreResult(false, issues.ToArray());

        _backlog.RestoreStateSnapshot(snapshot.BacklogEntries);
        _deferredStairwells.RestoreStateSnapshot(snapshot.DeferredStairwells);
        _reservedTiles.RestoreStateSnapshot(snapshot.ReservedTiles);

        _active.Clear();
        foreach (var job in snapshot.ActiveJobs.OrderBy(static job => job.Order))
        {
            var active = new ActiveMiningJob
            {
                WorkerId = job.WorkerId,
                Target = job.Target,
                Z = job.Z,
                Adjacent = job.Adjacent,
                Stage = job.Stage,
                ProgressTicks = job.ProgressTicks,
                RequiredTicks = job.RequiredTicks,
                GeologyHandle = job.GeologyHandle,
                TerrainKind = job.TerrainKind,
                Priority = job.Priority,
                AssignedTick = job.AssignedTick,
                ReplanFailCount = job.ReplanFailCount,
                Action = job.Action,
                Segment = job.Segment,
                DesignationId = job.DesignationId
            };

            _active.Add(active);
            if (active.Stage == MiningStage.ToAdj)
                BeginRestoredMovement(active);
        }

        _recentCompleted.Clear();
        foreach (var completion in snapshot.RecentCompletions.OrderBy(static completion => completion.Order))
            _recentCompleted.Add((completion.Cell, completion.Z, completion.ExpireTick));

        _inboxBuffer.Clear();
        UpdateStats(0);
        return MiningJobRestoreResult.Successful;
    }

    private List<string> ValidateReplaySnapshot(MiningJobReplaySnapshot snapshot)
    {
        var issues = new List<string>();
        if (snapshot.ActiveJobs == null)
            issues.Add("Mining active job list is missing.");
        if (snapshot.BacklogEntries == null)
            issues.Add("Mining backlog list is missing.");
        if (snapshot.DeferredStairwells == null)
            issues.Add("Mining deferred stairwell list is missing.");
        if (snapshot.ReservedTiles == null)
            issues.Add("Mining reserved tile list is missing.");
        if (snapshot.RecentCompletions == null)
            issues.Add("Mining recent completion list is missing.");
        if (issues.Count > 0)
            return issues;

        var activeWorkers = new HashSet<Guid>();
        var activeTargets = new HashSet<(int X, int Y, int Z)>();
        var reservedTiles = new HashSet<(int X, int Y, int Z)>();

        foreach (var tile in snapshot.ReservedTiles!)
            ValidateReservedTile(tile, reservedTiles, issues);

        foreach (var job in snapshot.ActiveJobs!)
            ValidateActiveJob(job, activeWorkers, activeTargets, reservedTiles, issues);

        foreach (var entry in snapshot.BacklogEntries!)
            ValidateBacklogEntry(entry, issues);

        foreach (var entry in snapshot.DeferredStairwells!)
            ValidateDeferredStairwell(entry, issues);

        foreach (var completion in snapshot.RecentCompletions!)
            ValidateRecentCompletion(completion, issues);

        return issues;
    }

    private void ValidateActiveJob(
        MiningActiveJobStateSnapshot job,
        ISet<Guid> activeWorkers,
        ISet<(int X, int Y, int Z)> activeTargets,
        ISet<(int X, int Y, int Z)> reservedTiles,
        ICollection<string> issues)
    {
        if (job.Order < 0)
            issues.Add("Mining active job order must not be negative.");
        if (job.WorkerId == Guid.Empty)
            issues.Add("Mining active job worker id must not be empty.");
        if (!activeWorkers.Add(job.WorkerId))
            issues.Add($"Mining active job duplicates worker {job.WorkerId}.");
        if (!activeTargets.Add((job.Target.X, job.Target.Y, job.Z)))
            issues.Add($"Mining active job duplicates target ({job.Target.X},{job.Target.Y},{job.Z}).");
        if (job.Stage == MiningStage.Complete)
            issues.Add("Mining active job must not restore transient Complete stage.");
        if (!Enum.IsDefined(typeof(MiningStage), job.Stage))
            issues.Add($"Mining active job stage {job.Stage} is not supported.");
        if (job.ProgressTicks < 0)
            issues.Add("Mining active job progress ticks must not be negative.");
        if (job.RequiredTicks <= 0)
            issues.Add("Mining active job required ticks must be positive.");
        if (job.ProgressTicks > job.RequiredTicks)
            issues.Add("Mining active job progress ticks must not exceed required ticks.");
        if (job.ReplanFailCount < 0)
            issues.Add("Mining active job replan fail count must not be negative.");
        if (job.DesignationId <= 0)
            issues.Add("Mining active job designation id must be positive.");
        ValidateEnum<TerrainKind>((int)job.TerrainKind, "Mining active job terrain kind", issues);
        ValidateEnum<MiningAction>((int)job.Action, "Mining active job action", issues);
        ValidateEnum<MiningSegment>((int)job.Segment, "Mining active job segment", issues);
        ValidateMiningAction(job.Action, "active job", issues);
        ValidateSegment(job.Action, job.Segment, "active job", issues);
        if (_world.Creatures.GetInstance(job.WorkerId) == null)
            issues.Add($"Mining active job references missing creature {job.WorkerId}.");
        ValidateWorldCell(job.Target.X, job.Target.Y, job.Z, "Mining active job target", issues);
        ValidateWorldCell(job.Adjacent.X, job.Adjacent.Y, job.Z, "Mining active job adjacent", issues);
        if (!reservedTiles.Contains((job.Target.X, job.Target.Y, job.Z)))
            issues.Add($"Mining active job target ({job.Target.X},{job.Target.Y},{job.Z}) is not present in reserved tiles.");
        if (job.Action == MiningAction.DigChannel
            && job.Z > 0
            && !reservedTiles.Contains((job.Target.X, job.Target.Y, job.Z - 1)))
        {
            issues.Add($"Mining channel active job target ({job.Target.X},{job.Target.Y},{job.Z}) is missing lower reserved tile.");
        }

        if (job.Stage == MiningStage.ToAdj && !CanBeginRestoredMovement(job, out var pathIssue))
            issues.Add(pathIssue);
    }

    private void ValidateBacklogEntry(
        MiningBacklogEntrySnapshot entry,
        ICollection<string> issues)
    {
        if (entry.Order < 0)
            issues.Add("Mining backlog entry order must not be negative.");
        ValidatePlannedDig(entry.Dig, "backlog entry", issues);
    }

    private void ValidateDeferredStairwell(
        MiningDeferredStairwellSnapshot entry,
        ICollection<string> issues)
    {
        if (entry.Order < 0)
            issues.Add("Mining deferred stairwell order must not be negative.");
        ValidatePlannedDig(entry.Dig, "deferred stairwell", issues);
        if (entry.Dig.Action != MiningAction.DigStairwell)
            issues.Add("Mining deferred stairwell entry must contain a stairwell dig.");
    }

    private void ValidatePlannedDig(
        MiningSystem.PlannedDig dig,
        string label,
        ICollection<string> issues)
    {
        if (dig.DesignationId <= 0)
            issues.Add($"Mining {label} designation id must be positive.");
        ValidateEnum<TerrainKind>(dig.TerrainKind, $"Mining {label} terrain kind", issues);
        ValidateEnum<MiningAction>((int)dig.Action, $"Mining {label} action", issues);
        ValidateEnum<MiningSegment>((int)dig.Segment, $"Mining {label} segment", issues);
        ValidateMiningAction(dig.Action, label, issues);
        ValidateSegment(dig.Action, dig.Segment, label, issues);
        ValidateWorldCell(dig.Cell.X, dig.Cell.Y, dig.Z, $"Mining {label} target", issues);
    }

    private void ValidateReservedTile(
        MiningReservedTileSnapshot tile,
        ISet<(int X, int Y, int Z)> reservedTiles,
        ICollection<string> issues)
    {
        if (!reservedTiles.Add((tile.X, tile.Y, tile.Z)))
            issues.Add($"Mining reserved tile duplicates ({tile.X},{tile.Y},{tile.Z}).");
        ValidateWorldCell(tile.X, tile.Y, tile.Z, "Mining reserved tile", issues);
    }

    private void ValidateRecentCompletion(
        MiningRecentCompletionSnapshot completion,
        ICollection<string> issues)
    {
        if (completion.Order < 0)
            issues.Add("Mining recent completion order must not be negative.");
        ValidateWorldCell(completion.Cell.X, completion.Cell.Y, completion.Z, "Mining recent completion", issues);
    }

    private bool CanBeginRestoredMovement(
        MiningActiveJobStateSnapshot job,
        out string issue)
    {
        issue = string.Empty;
        var worker = _world.Creatures.GetInstance(job.WorkerId);
        if (worker == null)
        {
            issue = "Mining active job cannot restore movement without an existing worker.";
            return false;
        }

        var source = new Point3(worker.Position.X, worker.Position.Y, worker.Z);
        var destination = new Point3(job.Adjacent.X, job.Adjacent.Y, job.Z);
        if (source == destination)
            return true;

        var request = new PathRequest(
            source,
            destination,
            MoveMode.Walk,
            PathFlags.AllowDiagonal,
            MiningPathSeed.From(job.WorkerId, job.Target));
        var path = _paths.Solve(in request, in _navView);
        if (path.Kind == PathResultKind.Found)
            return true;

        issue = $"Mining active job cannot restore movement path for worker {job.WorkerId}; path result was {path.Kind}.";
        return false;
    }

    private void BeginRestoredMovement(ActiveMiningJob job)
    {
        var worker = _world.Creatures.GetInstance(job.WorkerId);
        if (worker == null)
            return;

        var request = new PathRequest(
            new Point3(worker.Position.X, worker.Position.Y, worker.Z),
            new Point3(job.Adjacent.X, job.Adjacent.Y, job.Z),
            MoveMode.Walk,
            PathFlags.AllowDiagonal,
            MiningPathSeed.From(job.WorkerId, job.Target));
        var path = _paths.Solve(in request, in _navView);
        _move.BeginMovement(DiffTargetEncoding.EntityKey(job.WorkerId), request, path);
    }

    private void ValidateWorldCell(
        int x,
        int y,
        int z,
        string label,
        ICollection<string> issues)
    {
        if (!_world.GetTile(x, y, z).HasValue)
            issues.Add($"{label} ({x},{y},{z}) is outside the restored world.");
    }

    private static void ValidateMiningAction(
        MiningAction action,
        string label,
        ICollection<string> issues)
    {
        if (action == MiningAction.RemoveDigging)
            issues.Add($"Mining {label} uses cancellation action instead of a dig action.");
    }

    private static void ValidateSegment(
        MiningAction action,
        MiningSegment segment,
        string label,
        ICollection<string> issues)
    {
        if (action == MiningAction.DigStairwell)
        {
            if (segment == MiningSegment.None)
                issues.Add($"Mining {label} stairwell dig must have a stairwell segment.");
            return;
        }

        if (segment != MiningSegment.None)
            issues.Add($"Mining {label} non-stairwell dig must not have a stairwell segment.");
    }

    private static void ValidateEnum<T>(
        int value,
        string label,
        ICollection<string> issues)
        where T : struct, Enum
    {
        if (!TryCreateEnumValue<T>(value, out var enumValue)
            || !Enum.IsDefined(typeof(T), enumValue))
        {
            issues.Add($"{label} value {value} is not supported.");
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
