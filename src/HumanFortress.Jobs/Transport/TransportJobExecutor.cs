using HumanFortress.Contracts.Navigation;
using System;
using System.Collections.Generic;
using HumanFortress.Simulation.Jobs;
using HumanFortress.Jobs.Transport.Planning;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.Jobs.Transport;

internal sealed partial class TransportJobExecutor
{
    internal const string SystemId = "Jobs.Transport";

    private const int CreatureReserveTtlTicks = 200;
    private const string JobTag = "hauling";

    private readonly WorldModel _world;
    private readonly ITransportRequestQueue _requests;
    private readonly IPathService _paths;
    private readonly IWorldNavigationView _navView;
    private readonly IMovementExecutor _move;
    private readonly ITransportJobLogger _logger;
    private readonly TransportIntakeFilter _intakeFilter;
    private readonly TransportAssignmentHandler _assignmentHandler;
    private readonly TransportActiveJobRunner _activeJobRunner;
    private readonly TransportCommitMutationCoordinator _commitMutations;
    private readonly TransportStatsTracker _statsTracker = new();

    private readonly List<TransportRequest> _inboxBuffer = new();
    private readonly TransportBacklogBuffer _backlog = new();
    private readonly List<ActiveJob> _active = new();
    private PreparedTransportTick? _preparedTick;

    private readonly int _configuredIntakePerTick;
    private readonly int _configuredMaxActive;
    private readonly int _carryoverMaxTicks;
    private int? _hintIntakeCap;
    private int? _hintMaxActive;
    private int _hintReserveSlots;

    internal TransportJobExecutor(
        WorldModel world,
        ITransportRequestQueue requestQueue,
        IPathService paths,
        IWorldNavigationView navView,
        IMovementExecutor move,
        ITransportMovementDiffEmitter movementDiffEmitter,
        ITransportItemDiffEmitter itemDiffEmitter,
        ITransportStockpileIndexEmitter? stockpileIndexEmitter,
        ITransportWorkerCandidateSource? workerCandidates,
        ITransportJobCompletionSink? completionSink,
        TransportCommitMutationCoordinator commitMutations,
        ITransportJobLogger? logger,
        int intakeBudget,
        int carryoverMaxTicks,
        int maxActiveJobs)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _requests = requestQueue ?? throw new ArgumentNullException(nameof(requestQueue));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _navView = navView ?? throw new ArgumentNullException(nameof(navView));
        move = move ?? throw new ArgumentNullException(nameof(move));
        _move = move;
        _commitMutations = commitMutations ?? throw new ArgumentNullException(nameof(commitMutations));
        _logger = logger ?? NullTransportJobLogger.Instance;
        _intakeFilter = new TransportIntakeFilter(world);
        stockpileIndexEmitter ??= NullTransportStockpileIndexEmitter.Instance;

        var destinationValidator = new TransportDestinationValidator(world);
        var jobFinalizer = new TransportJobFinalizer(world.Reservations);
        var replanHandler = new TransportReplanHandler(world, paths, navView, move, movementDiffEmitter, _logger, SeedFrom);
        _assignmentHandler = new TransportAssignmentHandler(
            world,
            paths,
            navView,
            move,
            workerCandidates,
            _logger,
            SystemId,
            JobTag,
            CreatureReserveTtlTicks,
            SeedFrom);
        var pickupHandler = new TransportPickupHandler(
            world,
            destinationValidator,
            paths,
            navView,
            move,
            itemDiffEmitter,
            stockpileIndexEmitter,
            jobFinalizer,
            _logger,
            _statsTracker,
            CreatureReserveTtlTicks,
            SeedFrom);
        var deliveryHandler = new TransportDeliveryHandler(
            destinationValidator,
            itemDiffEmitter,
            stockpileIndexEmitter,
            jobFinalizer,
            completionSink,
            _logger,
            _statsTracker,
            JobTag);
        _activeJobRunner = new TransportActiveJobRunner(
            world,
            navView,
            move,
            movementDiffEmitter,
            itemDiffEmitter,
            stockpileIndexEmitter,
            replanHandler,
            jobFinalizer,
            pickupHandler,
            deliveryHandler,
            CreatureReserveTtlTicks);

        _configuredIntakePerTick = Math.Max(1, intakeBudget);
        _configuredMaxActive = Math.Max(0, maxActiveJobs);
        _carryoverMaxTicks = Math.Max(1, carryoverMaxTicks);
    }

    internal int LastIntakeCount { get; private set; }

    internal TransportJobStatsSnapshot GetLastStatsSnapshot() => _statsTracker.LastStats;

    internal int GetBacklogCount() => _backlog.Count;
}
