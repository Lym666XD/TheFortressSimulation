using HumanFortress.Contracts.Navigation;
using HumanFortress.Core.Simulation;
using HumanFortress.Jobs.Transport.Planning;
using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Jobs;
using HumanFortress.Simulation.Stockpile;
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

    internal ActiveJob? TryAssign(
        TransportRequest request,
        IReadOnlyList<CreatureInstance> creatures,
        HashSet<Guid> busy,
        ulong tick,
        out bool increasePathSearchAttempt)
    {
        return TryAssignCore(
            request,
            creatures,
            busy,
            tick,
            requiredWorkerId: null,
            out increasePathSearchAttempt);
    }

    internal IReadOnlyList<Guid> CaptureRankedHaulingWorkerIds(
        IReadOnlyList<CreatureInstance> creatures,
        ulong tick,
        Point3 referencePoint)
    {
        ArgumentNullException.ThrowIfNull(creatures);
        var candidates = _workerCandidates?.SelectCandidates(
                _world,
                _jobTag,
                new HashSet<Guid>(),
                _world.Reservations,
                tick,
                referencePoint)
            ?? creatures.OrderBy(static worker => worker.Guid);
        return candidates
            .Where(static worker => worker.HP > 0)
            .Select(static worker => worker.Guid)
            .Distinct()
            .ToArray();
    }

    internal ActiveJob? TryAssignPlanned(
        TransportRequest request,
        CreatureInstance worker,
        HashSet<Guid> busy,
        ulong tick,
        out bool increasePathSearchAttempt)
    {
        ArgumentNullException.ThrowIfNull(worker);
        return TryAssignCore(
            request,
            new[] { worker },
            busy,
            tick,
            worker.Guid,
            out increasePathSearchAttempt);
    }

    internal bool MatchesPlannedAssignment(
        TransportIntent intent,
        in TransportRequest request,
        CreatureInstance worker)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(worker);
        if (intent.Kind != TransportIntentKind.AssignRequest
            || request.ItemGuid != intent.ItemId
            || worker.Guid != intent.CreatureId
            || new Point3(request.From.X, request.From.Y, request.FromZ) != intent.SourcePosition
            || new Point3(request.To.X, request.To.Y, request.ToZ) != intent.Destination
            || request.Quantity != intent.Quantity
            || request.Reason != intent.Reason
            || request.PathSearchAttempt != intent.PathSearchAttempt)
        {
            return false;
        }

        var item = _world.Items.GetInstance(intent.ItemId);
        var expectedItem = intent.ExpectedItem;
        if (item == null
            || DiffTargetEncoding.EntityKey(item.Guid) != expectedItem.EntityKey
            || new Point3(item.Position.X, item.Position.Y, item.Z) != expectedItem.Position
            || item.StackCount != expectedItem.StackCount
            || item.IsOnGround != expectedItem.IsOnGround
            || (item.CarriedBy ?? Guid.Empty) != expectedItem.CarrierId)
        {
            return false;
        }

        var expectedCreature = intent.ExpectedCreature;
        if (DiffTargetEncoding.EntityKey(worker.Guid) != expectedCreature.EntityKey
            || new Point3(worker.Position.X, worker.Position.Y, worker.Z) != expectedCreature.Position
            || worker.HP != expectedCreature.HitPoints)
        {
            return false;
        }

        if (!MatchesStockpileExpectation(intent.ExpectedStockpile)
            || !MatchesNavigationExpectation(intent.ExpectedNavigation)
            || !MatchesMovementExpectation(worker.Guid, intent.ExpectedMovement))
        {
            return false;
        }

        return true;
    }

    private ActiveJob? TryAssignCore(
        TransportRequest request,
        IReadOnlyList<CreatureInstance> creatures,
        HashSet<Guid> busy,
        ulong tick,
        Guid? requiredWorkerId,
        out bool increasePathSearchAttempt)
    {
        increasePathSearchAttempt = false;
        var jobPoint = new Point3(request.From.X, request.From.Y, request.FromZ);
        var candidates = _workerCandidates?.SelectCandidates(_world, _jobTag, busy, _world.Reservations, tick, jobPoint)
            ?? creatures;

        foreach (var worker in candidates)
        {
            if (requiredWorkerId.HasValue && worker.Guid != requiredWorkerId.Value) continue;
            if (worker.HP <= 0) continue;
            if (busy.Contains(worker.Guid)) continue;
            string jobId = $"haul:{request.ItemGuid}";
            if (!_world.Reservations.TryAcquireCreature(
                    worker.Guid,
                    _systemId,
                    jobId,
                    tick,
                    tick + (ulong)_creatureReserveTtlTicks,
                    out var creatureReservation))
                continue;

            if (!_world.Reservations.TryAcquireItem(
                    request.ItemGuid,
                    worker.Guid,
                    _systemId,
                    jobId,
                    tick,
                    tick + (ulong)_creatureReserveTtlTicks,
                    out var itemReservation))
            {
                _world.Reservations.TryReleaseCreature(creatureReservation);
                continue;
            }

            var start = new Point3(worker.Position.X, worker.Position.Y, worker.Z);
            var pathRequest = CreatePathRequest(
                in request,
                start,
                _seedFrom(worker.Guid, request.ItemGuid));
            IWorldNavigationView view = _navView;
            var path = _paths.Solve(in pathRequest, in view);
            if (path.Kind != PathResultKind.Found)
            {
                if (path.Kind == PathResultKind.Partial)
                    increasePathSearchAttempt = true;
                _world.Reservations.TryReleaseCreature(creatureReservation);
                _world.Reservations.TryReleaseItem(itemReservation);
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
                Reason = request.Reason,
                PathSearchAttempt = pathRequest.EffectiveSearchAttempt,
                CreatureReservation = creatureReservation,
                ItemReservation = itemReservation
            };
        }

        return null;
    }

    internal static PathRequest CreatePathRequest(
        in TransportRequest request,
        Point3 source,
        uint seed)
    {
        return new PathRequest(
            source,
            new Point3(request.From.X, request.From.Y, request.FromZ),
            MoveMode.Walk,
            PathFlags.AllowDiagonal,
            seed,
            request.PathSearchAttempt);
    }

    private bool MatchesStockpileExpectation(TransportStockpileExpectation expected)
    {
        if (expected.Kind == TransportStockpileExpectationKind.None)
            return true;
        if (expected.Kind == TransportStockpileExpectationKind.Absent)
        {
            return !StockpileWorldQueries.TryGetStockpileCell(
                _world,
                expected.Position.X,
                expected.Position.Y,
                expected.Position.Z,
                out _);
        }

        if (!StockpileWorldQueries.TryGetStockpileCell(
                _world,
                expected.Position.X,
                expected.Position.Y,
                expected.Position.Z,
                out var location)
            || location.ZoneId != expected.ZoneId
            || _world.Stockpiles.GetZone(location.ZoneId) is not { } zone
            || zone.Generation != expected.Generation)
        {
            return false;
        }

        var occupying = _world.Items
            .GetGroundItemsAt(
                new SadRogue.Primitives.Point(expected.Position.X, expected.Position.Y),
                expected.Position.Z)
            .Select(static item => item.Guid)
            .FirstOrDefault();
        return occupying == expected.OccupyingItemId;
    }

    private bool MatchesNavigationExpectation(TransportNavigationExpectation expected)
    {
        return unchecked((ulong)_navView.GetConnectivityVersion(expected.StartChunk))
                == expected.StartConnectivityVersion
            && unchecked((ulong)_navView.GetConnectivityVersion(expected.GoalChunk))
                == expected.GoalConnectivityVersion;
    }

    private bool MatchesMovementExpectation(
        Guid creatureId,
        TransportMovementExpectation expected)
    {
        var cursor = _move.GetCursorSnapshot(DiffTargetEncoding.EntityKey(creatureId));
        if (!expected.IsRequired)
            return !cursor.HasValue;
        if (!cursor.HasValue)
            return false;

        var value = cursor.Value;
        return value.Revision == expected.CursorRevision
            && value.Position == expected.Position
            && value.Request.Destination == expected.Destination
            && value.Path.Hash == expected.PathHash
            && value.CurrentStep == expected.CurrentStep
            && value.Path.Steps.Length == expected.StepCount;
    }
}
