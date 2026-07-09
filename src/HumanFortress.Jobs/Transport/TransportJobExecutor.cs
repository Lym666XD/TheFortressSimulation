using HumanFortress.Contracts.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using HumanFortress.Simulation.Jobs;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.Jobs.Transport;

internal sealed class TransportJobExecutor
{
    internal const string SystemId = "Jobs.Transport";

    private const int CreatureReserveTtlTicks = 200;
    private const string JobTag = "hauling";

    private readonly WorldModel _world;
    private readonly ITransportRequestQueue _requests;
    private readonly IPathService _paths;
    private readonly ITransportJobLogger _logger;
    private readonly TransportIntakeFilter _intakeFilter;
    private readonly TransportAssignmentHandler _assignmentHandler;
    private readonly TransportActiveJobRunner _activeJobRunner;
    private readonly TransportStatsTracker _statsTracker = new();

    private readonly List<TransportRequest> _inboxBuffer = new();
    private readonly TransportBacklogBuffer _backlog = new();
    private readonly List<ActiveJob> _active = new();

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
        ITransportJobLogger? logger,
        int intakeBudget,
        int carryoverMaxTicks,
        int maxActiveJobs)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _requests = requestQueue ?? throw new ArgumentNullException(nameof(requestQueue));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        move = move ?? throw new ArgumentNullException(nameof(move));
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
            CreatureReserveTtlTicks,
            SeedFrom);
        var deliveryHandler = new TransportDeliveryHandler(
            destinationValidator,
            itemDiffEmitter,
            stockpileIndexEmitter,
            jobFinalizer,
            completionSink,
            _logger,
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
            SystemId,
            CreatureReserveTtlTicks);

        _configuredIntakePerTick = Math.Max(1, intakeBudget);
        _configuredMaxActive = Math.Max(0, maxActiveJobs);
        _carryoverMaxTicks = Math.Max(1, carryoverMaxTicks);
    }

    internal int LastIntakeCount { get; private set; }

    internal TransportJobStatsSnapshot GetLastStatsSnapshot() => _statsTracker.LastStats;

    internal int GetBacklogCount() => _backlog.Count;

    internal void ReadTick(ulong tick)
    {
        _paths.BeginTick();
        int intakeBudget = GetEffectiveIntakeBudget();
        _inboxBuffer.Clear();

        _backlog.DrainInto(intakeBudget, _inboxBuffer);

        if (_inboxBuffer.Count < intakeBudget)
        {
            _requests.Drain(intakeBudget - _inboxBuffer.Count, _inboxBuffer);
        }

        LastIntakeCount = _inboxBuffer.Count;

        if (_inboxBuffer.Count == 0)
        {
            if ((tick % 60UL) == 0UL)
            {
                _logger.Log($"[TRANS-JOBS][{tick}] No requests dequeued.");
            }

            _statsTracker.RecordRead(0, _active.Count, _backlog.Count, 0);
            return;
        }

        var reqs = _intakeFilter.FilterReadyRequests(_inboxBuffer, tick);

        var creatures = _world.Creatures.GetAllInstances().OrderBy(c => c.Guid).ToList();
        int allowedActive = GetAllowedActiveCount(creatures.Count);
        _logger.Log($"[TRANS-JOBS][{tick}] Intake={reqs.Count} Active={_active.Count} Backlog={_backlog.Count} Workers={creatures.Count} MaxActive={(allowedActive == int.MaxValue ? -1 : allowedActive)}");

        var busy = new HashSet<Guid>(_active.Select(a => a.CreatureId));
        bool throttleLogged = false;
        for (int i = 0; i < reqs.Count; i++)
        {
            var rq = reqs[i];
            if (allowedActive != int.MaxValue && _active.Count >= allowedActive)
            {
                if (!throttleLogged && HasActiveThrottle())
                {
                    int cappedReserve = Math.Min(creatures.Count, Math.Max(0, _hintReserveSlots));
                    _logger.Log($"[TRANS-JOBS][{tick}] Throttled assignments: active={_active.Count} limit={allowedActive} reserve={cappedReserve}");
                    throttleLogged = true;
                }

                _backlog.EnqueueRange(reqs, i, tick);
                break;
            }

            var assignedJob = _assignmentHandler.TryAssign(rq, creatures, busy, tick);
            if (assignedJob != null)
            {
                _active.Add(assignedJob);
                busy.Add(assignedJob.CreatureId);
                continue;
            }

            _backlog.TryEnqueue(rq, tick);
        }

        int carryoverOld = _backlog.CountOlderThan(tick, _carryoverMaxTicks * 2);
        _statsTracker.RecordRead(LastIntakeCount, _active.Count, _backlog.Count, carryoverOld);
    }

    internal void WriteTick(ulong tick)
    {
        if (_active.Count == 0)
        {
            return;
        }

        var finished = new List<ActiveJob>();
        _activeJobRunner.RunWriteTick(_active, tick, finished);
        if (finished.Count > 0)
        {
            foreach (var f in finished)
            {
                _active.Remove(f);
            }

            _statsTracker.RecordFinishedJobs();
        }
    }

    internal List<TransportActiveJobView> GetActiveJobsSnapshot()
    {
        var list = new List<TransportActiveJobView>(_active.Count);
        foreach (var j in _active)
        {
            var from = j.Stage == JobStage.ToItem ? GetItemPos(j) : GetCreaturePos(j.CreatureId);
            list.Add(new TransportActiveJobView(j.CreatureId, j.ItemId, from, j.Dest, j.Stage.ToString()));
        }

        return list;
    }

    internal TransportDebugSnapshot GetDebugSnapshot(int maxActive = 8, int maxRequests = 8, bool includeSeeds = false)
    {
        var stats = GetLastStatsSnapshot();
        var active = new List<TransportActiveJobDebugView>(Math.Min(maxActive, _active.Count));
        for (int i = 0; i < _active.Count && active.Count < maxActive; i++)
        {
            var j = _active[i];
            var from = j.Stage == JobStage.ToItem ? GetItemPos(j) : GetCreaturePos(j.CreatureId);
            uint seed = includeSeeds ? SeedFrom(j.CreatureId, j.ItemId) : 0u;
            active.Add(new TransportActiveJobDebugView(j.CreatureId, j.ItemId, from, j.Dest, j.Stage.ToString(), seed));
        }

        var pendingPeek = _requests.Peek(maxRequests).ToList();
        var shards = _requests.GetShardCountsSnapshot()
            .OrderBy(static kv => kv.Key)
            .Select(static kv => new TransportShardCountDebugView(kv.Key, kv.Value))
            .ToArray();
        int workers = _world.Creatures.GetAllInstances().Count();
        int allowedActive = GetAllowedActiveCount(workers);
        int reserved = Math.Min(workers, Math.Max(0, _hintReserveSlots));

        return new TransportDebugSnapshot(
            stats,
            active,
            pendingPeek,
            shards,
            BacklogCount: _backlog.Count,
            IntakeBudget: GetEffectiveIntakeBudget(),
            AllowedActive: allowedActive == int.MaxValue ? -1 : allowedActive,
            ReservedSlots: reserved,
            SeedsIncluded: includeSeeds);
    }

    internal TransportJobReplaySnapshot GetReplaySnapshot()
    {
        var active = new TransportActiveJobStateSnapshot[_active.Count];
        for (var i = 0; i < _active.Count; i++)
        {
            var job = _active[i];
            active[i] = new TransportActiveJobStateSnapshot(
                i,
                job.CreatureId,
                job.ItemId,
                job.Dest,
                job.Stage,
                job.Quantity,
                job.InvalidReplanCount,
                job.Reason);
        }

        return new TransportJobReplaySnapshot(
            _hintIntakeCap,
            _hintMaxActive,
            _hintReserveSlots,
            active,
            _backlog.GetStateSnapshot());
    }

    internal void ApplySchedulingHints(int? intakeCap, int? maxActiveCap, int reserveSlots)
    {
        _hintIntakeCap = intakeCap;
        _hintMaxActive = maxActiveCap;
        _hintReserveSlots = Math.Max(0, reserveSlots);
    }

    private int GetEffectiveIntakeBudget()
    {
        int budget = _configuredIntakePerTick;
        if (_hintIntakeCap.HasValue)
        {
            int cap = Math.Max(1, _hintIntakeCap.Value);
            budget = Math.Min(budget, cap);
        }

        return budget;
    }

    private int GetAllowedActiveCount(int totalWorkers)
    {
        int allowed = _configuredMaxActive > 0 ? _configuredMaxActive : int.MaxValue;
        if (_hintMaxActive.HasValue)
        {
            int cap = Math.Max(0, _hintMaxActive.Value);
            allowed = allowed == int.MaxValue ? cap : Math.Min(allowed, cap);
        }

        int reserve = Math.Min(totalWorkers, Math.Max(0, _hintReserveSlots));
        if (allowed == int.MaxValue)
        {
            if (reserve == 0)
            {
                return int.MaxValue;
            }

            int res = totalWorkers - reserve;
            return res < 0 ? 0 : res;
        }

        allowed = Math.Min(allowed, totalWorkers);
        allowed -= reserve;
        if (allowed < 0)
        {
            allowed = 0;
        }

        return allowed;
    }

    private bool HasActiveThrottle() => _configuredMaxActive > 0 || _hintReserveSlots > 0 || (_hintMaxActive.HasValue && _hintMaxActive.Value > 0);

    private Point3 GetCreaturePos(Guid creatureId)
    {
        var cr = _world.Creatures.GetInstance(creatureId);
        if (cr == null)
        {
            return new Point3(0, 0, 0);
        }

        return new Point3(cr.Position.X, cr.Position.Y, cr.Z);
    }

    private Point3 GetItemPos(ActiveJob job)
    {
        var it = _world.Items.GetInstance(job.ItemId);
        if (it == null)
        {
            return new Point3(0, 0, 0);
        }

        return new Point3(it.Position.X, it.Position.Y, it.Z);
    }

    private static uint SeedFrom(Guid a, Guid b)
    {
        unchecked
        {
            var ba = a.ToByteArray();
            var bb = b.ToByteArray();
            uint s = 2166136261;
            foreach (var t in ba)
            {
                s = (s ^ t) * 16777619;
            }

            foreach (var t in bb)
            {
                s = (s ^ t) * 16777619;
            }

            return s;
        }
    }
}
