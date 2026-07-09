using HumanFortress.Contracts.Navigation;
using HumanFortress.Core.Simulation;
using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Placeables;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.Jobs.Craft;

internal sealed class CraftAssignmentHandler
{
    private readonly WorldModel _world;
    private readonly CraftWorkshopLocator _workshops;
    private readonly IPathService _paths;
    private readonly IWorldNavigationView _navView;
    private readonly IMovementExecutor _move;
    private readonly ICraftRecipeCatalog _recipes;
    private readonly ICraftWorkerCandidateSource? _workerCandidates;
    private readonly string _systemId;
    private readonly int _creatureReserveTtlTicks;

    internal CraftAssignmentHandler(
        WorldModel world,
        CraftWorkshopLocator workshops,
        IPathService paths,
        IWorldNavigationView navView,
        IMovementExecutor move,
        ICraftRecipeCatalog recipes,
        ICraftWorkerCandidateSource? workerCandidates,
        string systemId,
        int creatureReserveTtlTicks)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _workshops = workshops ?? throw new ArgumentNullException(nameof(workshops));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _navView = navView ?? throw new ArgumentNullException(nameof(navView));
        _move = move ?? throw new ArgumentNullException(nameof(move));
        _recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
        _workerCandidates = workerCandidates;
        _systemId = systemId ?? throw new ArgumentNullException(nameof(systemId));
        _creatureReserveTtlTicks = creatureReserveTtlTicks;
    }

    internal CraftAssignmentResult TryAssign(
        PlannedCraftJob job,
        IReadOnlyList<CreatureInstance> workers,
        HashSet<Guid> busy,
        ulong tick,
        out ActiveCraftJob? activeJob)
    {
        activeJob = null;

        if (!_workshops.TryFind(job.WorkshopGuid, out _, out var state))
        {
            return CraftAssignmentResult.Invalid;
        }

        if (state == null || state.Queue.Count == 0)
        {
            return CraftAssignmentResult.Invalid;
        }

        var entry = state.GetEntry(job.QueueEntryId);
        if (entry == null)
        {
            return CraftAssignmentResult.Invalid;
        }

        entry.IsScheduled = false;

        var recipe = _recipes.GetRecipe(job.RecipeId);
        if (recipe == null)
        {
            return CraftAssignmentResult.Invalid;
        }

        var jobPoint = new Point3(job.Anchor.X, job.Anchor.Y, job.Z);
        var candidates = _workerCandidates?.SelectCandidates(_world, recipe.JobTag, busy, _world.Reservations, tick, jobPoint)
            ?? workers;

        foreach (var worker in candidates)
        {
            if (busy.Contains(worker.Guid))
            {
                continue;
            }

            if (!_world.Reservations.TryReserveCreature(worker.Guid, _systemId, tick, tick + (ulong)_creatureReserveTtlTicks, jobId: $"craft:{job.RecipeId}"))
            {
                continue;
            }

            var start = new Point3(worker.Position.X, worker.Position.Y, worker.Z);
            var dest = new Point3(job.Anchor.X, job.Anchor.Y, job.Z);
            var request = new PathRequest(start, dest, MoveMode.Walk, PathFlags.None, CraftPathSeed.From(worker.Guid, job.WorkshopGuid));
            var path = _paths.Solve(in request, in _navView);
            if (path.Kind != PathResultKind.Found)
            {
                _world.Reservations.ReleaseCreature(worker.Guid);
                continue;
            }

            _move.BeginMovement(DiffTargetEncoding.EntityKey(worker.Guid), request, path);

            activeJob = new ActiveCraftJob
            {
                WorkerId = worker.Guid,
                WorkshopGuid = job.WorkshopGuid,
                QueueEntryId = job.QueueEntryId,
                RecipeId = job.RecipeId,
                Stage = CraftJobStage.ToWorkshop,
                WorkTicksRemaining = job.DurationTicks,
                Anchor = job.Anchor,
                Z = job.Z
            };

            busy.Add(worker.Guid);
            state.RegisterJobStart();
            entry.Status = CraftQueueStatus.InProgress;
            entry.ActiveWorkerId = worker.Guid;
            return CraftAssignmentResult.Assigned;
        }

        return CraftAssignmentResult.Backlog;
    }
}
