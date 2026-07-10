using HumanFortress.Contracts.Navigation;
using HumanFortress.Core.Simulation;
using HumanFortress.Simulation.Placeables;

namespace HumanFortress.Jobs.Craft;

internal sealed partial class CraftJobExecutor
{
    internal CraftJobRestoreResult RestoreReplaySnapshot(CraftJobReplaySnapshot snapshot)
    {
        var issues = ValidateReplaySnapshot(snapshot);
        if (issues.Count > 0)
            return new CraftJobRestoreResult(false, issues.ToArray());

        _backlog.Clear();
        _active.Clear();

        foreach (var entry in snapshot.BacklogEntries.OrderBy(static entry => entry.Order))
            _backlog.Enqueue(entry.Job);

        foreach (var job in snapshot.ActiveJobs.OrderBy(static job => job.Order))
        {
            var active = new ActiveCraftJob
            {
                WorkerId = job.WorkerId,
                WorkshopGuid = job.WorkshopGuid,
                QueueEntryId = job.QueueEntryId,
                RecipeId = job.RecipeId,
                Stage = job.Stage,
                WorkTicksRemaining = job.WorkTicksRemaining,
                Anchor = job.Anchor,
                Z = job.Z
            };

            _active.Add(active);
            if (active.Stage == CraftJobStage.ToWorkshop)
                BeginRestoredMovement(active);
        }

        _inbox.Clear();
        LastIntakeCount = 0;
        _stats.RecordRead(0, _active.Count, _backlog.Count);
        return CraftJobRestoreResult.Successful;
    }

    private List<string> ValidateReplaySnapshot(CraftJobReplaySnapshot snapshot)
    {
        var issues = new List<string>();
        if (snapshot.ActiveJobs == null)
            issues.Add("Craft active job list is missing.");
        if (snapshot.BacklogEntries == null)
            issues.Add("Craft backlog list is missing.");
        if (issues.Count > 0)
            return issues;

        var queueEntryIds = new HashSet<Guid>();
        var activeWorkers = new HashSet<Guid>();
        var activeCountByWorkshop = new Dictionary<Guid, int>();

        foreach (var job in snapshot.ActiveJobs!)
            ValidateActiveJob(job, activeWorkers, queueEntryIds, activeCountByWorkshop, issues);

        foreach (var entry in snapshot.BacklogEntries!)
            ValidateBacklogEntry(entry, queueEntryIds, issues);

        ValidateWorkshopActiveCounts(activeCountByWorkshop, issues);
        return issues;
    }

    private void ValidateActiveJob(
        CraftActiveJobStateSnapshot job,
        ISet<Guid> activeWorkers,
        ISet<Guid> queueEntryIds,
        IDictionary<Guid, int> activeCountByWorkshop,
        ICollection<string> issues)
    {
        if (job.Order < 0)
            issues.Add("Craft active job order must not be negative.");
        if (job.WorkerId == Guid.Empty)
            issues.Add("Craft active job worker id must not be empty.");
        if (job.WorkshopGuid == Guid.Empty)
            issues.Add("Craft active job workshop id must not be empty.");
        if (job.QueueEntryId == Guid.Empty)
            issues.Add("Craft active job queue entry id must not be empty.");
        if (string.IsNullOrWhiteSpace(job.RecipeId))
            issues.Add("Craft active job recipe id must not be blank.");
        if (!Enum.IsDefined(typeof(CraftJobStage), job.Stage))
            issues.Add($"Craft active job stage {job.Stage} is not supported.");
        if (job.WorkTicksRemaining < 0)
            issues.Add("Craft active job work ticks remaining must not be negative.");
        if (!activeWorkers.Add(job.WorkerId))
            issues.Add($"Craft active job duplicates worker {job.WorkerId}.");
        ValidateUniqueQueueEntry(job.QueueEntryId, "active job", queueEntryIds, issues);
        ValidateRecipe(job.RecipeId, "active job", issues);
        if (_world.Creatures.GetInstance(job.WorkerId) == null)
            issues.Add($"Craft active job references missing creature {job.WorkerId}.");
        if (!IsWorldCellPresent(job.Anchor.X, job.Anchor.Y, job.Z))
            issues.Add("Craft active job anchor cell is outside the restored world.");

        if (!_workshops.TryFind(job.WorkshopGuid, out _, out var state) || state == null)
        {
            issues.Add($"Craft active job references missing workshop {job.WorkshopGuid}.");
        }
        else
        {
            ValidateActiveQueueEntry(job, state, issues);
            activeCountByWorkshop.TryGetValue(job.WorkshopGuid, out var currentCount);
            activeCountByWorkshop[job.WorkshopGuid] = currentCount + 1;
        }

        if (job.Stage == CraftJobStage.ToWorkshop && !CanBeginRestoredMovement(job, out var pathIssue))
            issues.Add(pathIssue);
    }

    private void ValidateBacklogEntry(
        CraftBacklogEntrySnapshot entry,
        ISet<Guid> queueEntryIds,
        ICollection<string> issues)
    {
        if (entry.Order < 0)
            issues.Add("Craft backlog entry order must not be negative.");

        var job = entry.Job;
        if (job.WorkshopGuid == Guid.Empty)
            issues.Add("Craft backlog job workshop id must not be empty.");
        if (job.QueueEntryId == Guid.Empty)
            issues.Add("Craft backlog job queue entry id must not be empty.");
        if (string.IsNullOrWhiteSpace(job.RecipeId))
            issues.Add("Craft backlog job recipe id must not be blank.");
        if (job.DurationTicks < 0)
            issues.Add("Craft backlog job duration ticks must not be negative.");
        ValidateUniqueQueueEntry(job.QueueEntryId, "backlog job", queueEntryIds, issues);
        ValidateRecipe(job.RecipeId, "backlog job", issues);
        if (!IsWorldCellPresent(job.Anchor.X, job.Anchor.Y, job.Z))
            issues.Add("Craft backlog job anchor cell is outside the restored world.");

        if (!_workshops.TryFind(job.WorkshopGuid, out _, out var state) || state == null)
        {
            issues.Add($"Craft backlog job references missing workshop {job.WorkshopGuid}.");
            return;
        }

        var queueEntry = state.GetEntry(job.QueueEntryId);
        if (queueEntry == null)
        {
            issues.Add($"Craft backlog job references missing queue entry {job.QueueEntryId}.");
            return;
        }

        if (!string.Equals(queueEntry.RecipeId, job.RecipeId, StringComparison.Ordinal))
            issues.Add($"Craft backlog job queue entry recipe '{queueEntry.RecipeId}' does not match payload recipe '{job.RecipeId}'.");
        if (queueEntry.Status == CraftQueueStatus.InProgress)
            issues.Add($"Craft backlog job queue entry {job.QueueEntryId} is already in progress.");
        if (queueEntry.ActiveWorkerId.HasValue)
            issues.Add($"Craft backlog job queue entry {job.QueueEntryId} already has an active worker.");
    }

    private void ValidateActiveQueueEntry(
        CraftActiveJobStateSnapshot job,
        WorkshopState state,
        ICollection<string> issues)
    {
        var entry = state.GetEntry(job.QueueEntryId);
        if (entry == null)
        {
            issues.Add($"Craft active job references missing queue entry {job.QueueEntryId}.");
            return;
        }

        if (!string.Equals(entry.RecipeId, job.RecipeId, StringComparison.Ordinal))
            issues.Add($"Craft active job queue entry recipe '{entry.RecipeId}' does not match payload recipe '{job.RecipeId}'.");
        if (entry.Status != CraftQueueStatus.InProgress)
            issues.Add($"Craft active job queue entry {job.QueueEntryId} is not marked in progress.");
        if (entry.ActiveWorkerId != job.WorkerId)
            issues.Add($"Craft active job queue entry {job.QueueEntryId} active worker does not match payload worker {job.WorkerId}.");
    }

    private void ValidateWorkshopActiveCounts(
        IReadOnlyDictionary<Guid, int> activeCountByWorkshop,
        ICollection<string> issues)
    {
        foreach (var (placeable, _) in _workshops.EnumerateWorkshops())
        {
            var state = placeable.Workshop;
            if (state == null)
                continue;

            activeCountByWorkshop.TryGetValue(placeable.Guid, out var expected);
            if (state.ActiveJobs != expected)
            {
                issues.Add(
                    $"Craft workshop {placeable.Guid} active job count {state.ActiveJobs} does not match payload count {expected}.");
            }
        }
    }

    private void ValidateRecipe(
        string recipeId,
        string label,
        ICollection<string> issues)
    {
        if (string.IsNullOrWhiteSpace(recipeId))
            return;

        if (_recipes.GetRecipe(recipeId) == null)
            issues.Add($"Craft {label} references unknown recipe '{recipeId}'.");
    }

    private bool CanBeginRestoredMovement(
        CraftActiveJobStateSnapshot job,
        out string issue)
    {
        issue = string.Empty;
        var worker = _world.Creatures.GetInstance(job.WorkerId);
        if (worker == null)
        {
            issue = "Craft active job cannot restore movement without an existing worker.";
            return false;
        }

        var source = new Point3(worker.Position.X, worker.Position.Y, worker.Z);
        var destination = new Point3(job.Anchor.X, job.Anchor.Y, job.Z);
        if (source == destination)
            return true;

        var request = new PathRequest(
            source,
            destination,
            MoveMode.Walk,
            PathFlags.None,
            CraftPathSeed.From(job.WorkerId, job.WorkshopGuid));
        var path = _paths.Solve(in request, in _navView);
        if (path.Kind == PathResultKind.Found)
            return true;

        issue = $"Craft active job cannot restore movement path for worker {job.WorkerId}; path result was {path.Kind}.";
        return false;
    }

    private void BeginRestoredMovement(ActiveCraftJob job)
    {
        var worker = _world.Creatures.GetInstance(job.WorkerId);
        if (worker == null)
            return;

        var request = new PathRequest(
            new Point3(worker.Position.X, worker.Position.Y, worker.Z),
            new Point3(job.Anchor.X, job.Anchor.Y, job.Z),
            MoveMode.Walk,
            PathFlags.None,
            CraftPathSeed.From(job.WorkerId, job.WorkshopGuid));
        var path = _paths.Solve(in request, in _navView);
        _move.BeginMovement(DiffTargetEncoding.EntityKey(job.WorkerId), request, path);
    }

    private bool IsWorldCellPresent(int x, int y, int z)
    {
        return _world.GetTile(x, y, z).HasValue;
    }

    private static void ValidateUniqueQueueEntry(
        Guid queueEntryId,
        string label,
        ISet<Guid> seenQueueEntries,
        ICollection<string> issues)
    {
        if (queueEntryId == Guid.Empty)
            return;

        if (!seenQueueEntries.Add(queueEntryId))
            issues.Add($"Craft {label} duplicates queue entry {queueEntryId}.");
    }
}
