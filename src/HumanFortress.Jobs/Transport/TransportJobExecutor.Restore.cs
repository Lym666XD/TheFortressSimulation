using HumanFortress.Contracts.Navigation;
using HumanFortress.Core.Simulation;
using HumanFortress.Simulation.Jobs;

namespace HumanFortress.Jobs.Transport;

internal sealed partial class TransportJobExecutor
{
    internal TransportJobRestoreResult RestoreReplaySnapshot(
        TransportRequestQueueStateSnapshot queue,
        TransportJobReplaySnapshot executor)
    {
        var issues = ValidateReplaySnapshot(queue, executor);
        if (issues.Count > 0)
            return new TransportJobRestoreResult(false, issues.ToArray());

        _requests.RestoreStateSnapshot(queue);
        _backlog.RestoreStateSnapshot(executor.BacklogEntries);
        _active.Clear();
        ApplySchedulingHints(
            executor.IntakeCapHint,
            executor.MaxActiveCapHint,
            executor.ReserveSlotsHint);

        foreach (var snapshot in executor.ActiveJobs.OrderBy(static job => job.Order))
        {
            var job = new ActiveJob
            {
                CreatureId = snapshot.CreatureId,
                ItemId = snapshot.ItemId,
                Dest = snapshot.Destination,
                Stage = snapshot.Stage,
                Quantity = snapshot.Quantity,
                InvalidReplanCount = snapshot.InvalidReplanCount,
                Reason = snapshot.Reason
            };

            _active.Add(job);
            BeginRestoredMovement(job);
        }

        _inboxBuffer.Clear();
        LastIntakeCount = 0;
        return TransportJobRestoreResult.Successful;
    }

    private List<string> ValidateReplaySnapshot(
        TransportRequestQueueStateSnapshot queue,
        TransportJobReplaySnapshot executor)
    {
        var issues = new List<string>();
        if (queue.PendingRequests == null)
            issues.Add("Transport pending request list is missing.");
        if (executor.ActiveJobs == null)
            issues.Add("Transport active job list is missing.");
        if (executor.BacklogEntries == null)
            issues.Add("Transport backlog list is missing.");
        if (executor.ReserveSlotsHint < 0)
            issues.Add("Transport reserve-slot hint must not be negative.");
        if (executor.IntakeCapHint.HasValue && executor.IntakeCapHint.Value <= 0)
            issues.Add("Transport intake-cap hint must be positive when present.");
        if (executor.MaxActiveCapHint.HasValue && executor.MaxActiveCapHint.Value < 0)
            issues.Add("Transport max-active-cap hint must not be negative when present.");

        if (issues.Count > 0)
            return issues;

        var transportItems = new HashSet<Guid>();
        foreach (var request in queue.PendingRequests!)
        {
            ValidateTransportRequest(request, "pending request", issues);
            ValidateUniqueTransportItem(request.ItemGuid, "pending request", transportItems, issues);
        }

        foreach (var entry in executor.BacklogEntries!)
        {
            ValidateTransportRequest(entry.Request, "backlog request", issues);
            ValidateUniqueTransportItem(entry.Request.ItemGuid, "backlog request", transportItems, issues);
            if (entry.Order < 0)
                issues.Add("Transport backlog entry order must not be negative.");
        }

        var activeWorkers = new HashSet<Guid>();
        foreach (var job in executor.ActiveJobs!)
            ValidateActiveJob(job, activeWorkers, transportItems, issues);

        return issues;
    }

    private void ValidateTransportRequest(
        TransportRequest request,
        string label,
        ICollection<string> issues)
    {
        if (request.ItemGuid == Guid.Empty)
            issues.Add($"Transport {label} item id must not be empty.");
        if (request.Quantity < 0)
            issues.Add($"Transport {label} quantity must not be negative.");
        if (request.RequestorId == null)
            issues.Add($"Transport {label} requestor id is missing.");
        if (_world.Items.GetInstance(request.ItemGuid) == null)
            issues.Add($"Transport {label} references missing item {request.ItemGuid}.");
        if (!IsWorldCellPresent(request.From.X, request.From.Y, request.FromZ))
            issues.Add($"Transport {label} source cell is outside the restored world.");
        if (!IsWorldCellPresent(request.To.X, request.To.Y, request.ToZ))
            issues.Add($"Transport {label} destination cell is outside the restored world.");
    }

    private void ValidateActiveJob(
        TransportActiveJobStateSnapshot job,
        ISet<Guid> activeWorkers,
        ISet<Guid> transportItems,
        ICollection<string> issues)
    {
        if (job.Order < 0)
            issues.Add("Transport active job order must not be negative.");
        if (job.CreatureId == Guid.Empty)
            issues.Add("Transport active job creature id must not be empty.");
        if (job.ItemId == Guid.Empty)
            issues.Add("Transport active job item id must not be empty.");
        if (job.Quantity <= 0)
            issues.Add("Transport active job quantity must be positive.");
        if (job.InvalidReplanCount < 0)
            issues.Add("Transport active job invalid-replan count must not be negative.");
        if (!activeWorkers.Add(job.CreatureId))
            issues.Add($"Transport active job duplicates worker {job.CreatureId}.");
        ValidateUniqueTransportItem(job.ItemId, "active job", transportItems, issues);
        if (_world.Creatures.GetInstance(job.CreatureId) == null)
            issues.Add($"Transport active job references missing creature {job.CreatureId}.");
        if (_world.Items.GetInstance(job.ItemId) == null)
            issues.Add($"Transport active job references missing item {job.ItemId}.");
        if (!IsWorldCellPresent(job.Destination.X, job.Destination.Y, job.Destination.Z))
            issues.Add("Transport active job destination cell is outside the restored world.");

        if (!CanBeginRestoredMovement(job, out var pathIssue))
            issues.Add(pathIssue);
    }

    private bool CanBeginRestoredMovement(
        TransportActiveJobStateSnapshot job,
        out string issue)
    {
        issue = string.Empty;
        var creature = _world.Creatures.GetInstance(job.CreatureId);
        var item = _world.Items.GetInstance(job.ItemId);
        if (creature == null || item == null)
        {
            issue = "Transport active job cannot restore movement without existing creature and item.";
            return false;
        }

        var source = new Point3(creature.Position.X, creature.Position.Y, creature.Z);
        var goal = job.Stage == JobStage.ToItem
            ? new Point3(item.Position.X, item.Position.Y, item.Z)
            : job.Destination;
        if (source == goal)
            return true;

        var request = new PathRequest(
            source,
            goal,
            MoveMode.Walk,
            PathFlags.AllowDiagonal,
            SeedFrom(job.CreatureId, job.ItemId));
        IWorldNavigationView view = _navView;
        var path = _paths.Solve(in request, in view);
        if (path.Kind == PathResultKind.Found)
            return true;

        issue = $"Transport active job cannot restore movement path for worker {job.CreatureId}; path result was {path.Kind}.";
        return false;
    }

    private void BeginRestoredMovement(ActiveJob job)
    {
        var source = GetCreaturePos(job.CreatureId);
        var goal = job.Stage == JobStage.ToItem ? GetItemPos(job) : job.Dest;
        var request = new PathRequest(
            source,
            goal,
            MoveMode.Walk,
            PathFlags.AllowDiagonal,
            SeedFrom(job.CreatureId, job.ItemId));
        IWorldNavigationView view = _navView;
        var path = _paths.Solve(in request, in view);
        _move.BeginMovement(DiffTargetEncoding.EntityKey(job.CreatureId), request, path);
    }

    private bool IsWorldCellPresent(int x, int y, int z)
    {
        return _world.GetTile(x, y, z).HasValue;
    }

    private static void ValidateUniqueTransportItem(
        Guid itemId,
        string label,
        ISet<Guid> seenItems,
        ICollection<string> issues)
    {
        if (itemId == Guid.Empty)
            return;

        if (!seenItems.Add(itemId))
            issues.Add($"Transport {label} duplicates item {itemId}.");
    }
}
