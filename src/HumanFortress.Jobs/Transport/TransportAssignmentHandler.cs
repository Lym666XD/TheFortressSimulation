using HumanFortress.Contracts.Navigation;
using HumanFortress.Core.Simulation;
using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Jobs;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.Jobs.Transport;

internal sealed class TransportAssignmentHandler
{
    private readonly WorldModel _world;
    private readonly IPathService _paths;
    private readonly IWorldNavigationView _navView;
    private readonly IMovementExecutor _move;
    private readonly ITransportWorkerCandidateSource? _workerCandidates;
    private readonly ITransportJobLogger _logger;
    private readonly string _systemId;
    private readonly string _jobTag;
    private readonly int _creatureReserveTtlTicks;
    private readonly Func<Guid, Guid, uint> _seedFrom;

    internal TransportAssignmentHandler(
        WorldModel world,
        IPathService paths,
        IWorldNavigationView navView,
        IMovementExecutor move,
        ITransportWorkerCandidateSource? workerCandidates,
        ITransportJobLogger? logger,
        string systemId,
        string jobTag,
        int creatureReserveTtlTicks,
        Func<Guid, Guid, uint> seedFrom)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _navView = navView ?? throw new ArgumentNullException(nameof(navView));
        _move = move ?? throw new ArgumentNullException(nameof(move));
        _workerCandidates = workerCandidates;
        _logger = logger ?? NullTransportJobLogger.Instance;
        _systemId = systemId ?? throw new ArgumentNullException(nameof(systemId));
        _jobTag = jobTag ?? throw new ArgumentNullException(nameof(jobTag));
        _creatureReserveTtlTicks = creatureReserveTtlTicks;
        _seedFrom = seedFrom ?? throw new ArgumentNullException(nameof(seedFrom));
    }

    internal ActiveJob? TryAssign(TransportRequest request, IReadOnlyList<CreatureInstance> creatures, HashSet<Guid> busy, ulong tick)
    {
        var jobPoint = new Point3(request.From.X, request.From.Y, request.FromZ);
        var candidates = _workerCandidates?.SelectCandidates(_world, _jobTag, busy, _world.Reservations, tick, jobPoint)
            ?? creatures;

        foreach (var worker in candidates)
        {
            if (worker.HP <= 0) continue;
            if (busy.Contains(worker.Guid)) continue;
            if (!_world.Reservations.TryReserveCreature(worker.Guid, _systemId, tick, tick + (ulong)_creatureReserveTtlTicks, jobId: $"haul:{request.ItemGuid}"))
                continue;

            if (!_world.Reservations.TryReserveItem(request.ItemGuid, worker.Guid, tick, tick + (ulong)_creatureReserveTtlTicks))
            {
                _world.Reservations.ReleaseCreature(worker.Guid);
                continue;
            }

            var start = new Point3(worker.Position.X, worker.Position.Y, worker.Z);
            var toItem = new Point3(request.From.X, request.From.Y, request.FromZ);
            var pathRequest = new PathRequest(start, toItem, MoveMode.Walk, PathFlags.AllowDiagonal, _seedFrom(worker.Guid, request.ItemGuid));
            IWorldNavigationView view = _navView;
            var path = _paths.Solve(in pathRequest, in view);
            if (path.Kind != PathResultKind.Found)
            {
                _world.Reservations.ReleaseCreature(worker.Guid);
                _world.Reservations.ReleaseItem(request.ItemGuid);
                continue;
            }

            _move.BeginMovement(DiffTargetEncoding.EntityKey(worker.Guid), pathRequest, path);
            int quantity = request.Quantity > 0
                ? request.Quantity
                : (_world.Items.GetInstance(request.ItemGuid)?.StackCount ?? 1);

            _logger.Log($"[TRANS-JOBS][{tick}] Assigned worker={worker.Guid} item={request.ItemGuid} reason={request.Reason} -> ToItem ({request.From.X},{request.From.Y},{request.FromZ})");

            return new ActiveJob
            {
                CreatureId = worker.Guid,
                ItemId = request.ItemGuid,
                Dest = new Point3(request.To.X, request.To.Y, request.ToZ),
                Stage = JobStage.ToItem,
                Quantity = quantity,
                Reason = request.Reason
            };
        }

        return null;
    }
}
