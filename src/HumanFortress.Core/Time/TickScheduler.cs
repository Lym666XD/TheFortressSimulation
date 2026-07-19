using System.Diagnostics;
using HumanFortress.Contracts.Diagnostics;
using HumanFortress.Contracts.Time;

namespace HumanFortress.Core.Time;

/// <summary>
/// Fixed-step tick scheduler implementing the authoritative UPDATE_ORDER.
/// Runs at 50 TPS (20ms per tick) with deterministic read and serialized write phases.
/// </summary>
public sealed class TickScheduler
{
    private const int TARGET_TPS = 50;
    private const int MS_PER_TICK = 1000 / TARGET_TPS; // 20ms
    private const int MAX_CONSECUTIVE_SYSTEM_FAILURES = 3;

    private readonly List<ITick> _systems = new();
    private readonly Dictionary<string, SystemFailureState> _systemFailures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _systemFailureTotals = new(StringComparer.Ordinal);
    private readonly object _barrierLock = new();
    private readonly object _stopLock = new();
    private readonly object _stateLock = new();
    private readonly IDiagnosticSink? _diagnostics;

    private ulong _currentTick;
    private bool _isRunning;
    private bool _isPaused;
    private bool _stopRequested;
    private float _speedMultiplier = 1.0f;
    private Thread? _tickThread;
    private long _systemFailureCountTotal;
    private TickSchedulerExecutionPosition _executionPosition = new(
        0,
        TickSchedulerExecutionPhase.Stopped);

    public event Action<ulong>? PreTick;
    public event Action<ulong>? PostTick;
    public event Action<ulong>? BarrierReached;

    public TickScheduler(IDiagnosticSink? diagnostics = null)
    {
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Current simulation tick number.
    /// </summary>
    public ulong CurrentTick
    {
        get
        {
            lock (_stateLock)
            {
                return _currentTick;
            }
        }
    }

    /// <summary>
    /// Whether the simulation is currently running.
    /// </summary>
    public bool IsRunning
    {
        get
        {
            lock (_stateLock)
            {
                return _isRunning;
            }
        }
    }

    /// <summary>
    /// Whether a scheduler thread still exists, including one that outlived a bounded stop request.
    /// </summary>
    public bool HasActiveThread
    {
        get
        {
            lock (_stateLock)
            {
                return _tickThread != null;
            }
        }
    }

    /// <summary>
    /// Target ticks per second (fixed at 50).
    /// </summary>
    public int TargetTPS => TARGET_TPS;

    /// <summary>
    /// Maximum time the compatibility <see cref="Stop"/> call waits for the tick thread.
    /// </summary>
    public static TimeSpan DefaultStopTimeout { get; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Last scheduler phase and system observed by the tick thread.
    /// </summary>
    public TickSchedulerExecutionPosition ExecutionPosition
    {
        get
        {
            lock (_stateLock)
            {
                return _executionPosition;
            }
        }
    }

    /// <summary>
    /// Capture failure history for the currently configured scheduler pipeline.
    /// A successful retry clears current consecutive state but not historical counts.
    /// </summary>
    public TickSchedulerHealthSnapshot CaptureHealthSnapshot()
    {
        lock (_stateLock)
        {
            var systems = _systemFailureTotals
                .OrderBy(static entry => entry.Key, StringComparer.Ordinal)
                .Select(entry =>
                {
                    _systemFailures.TryGetValue(entry.Key, out var current);
                    return new TickSchedulerSystemFailureSnapshot(
                        entry.Key,
                        entry.Value,
                        current?.ConsecutiveFailures ?? 0,
                        current?.Quarantined ?? false);
                })
                .ToArray();

            return new TickSchedulerHealthSnapshot(_systemFailureCountTotal, systems);
        }
    }

    /// <summary>
    /// Whether the simulation is currently paused.
    /// </summary>
    public bool IsPaused
    {
        get
        {
            lock (_stateLock)
            {
                return _isPaused;
            }
        }
    }

    /// <summary>
    /// Current speed multiplier (0.25x - 8x).
    /// </summary>
    public float SpeedMultiplier
    {
        get
        {
            lock (_stateLock)
            {
                return _speedMultiplier;
            }
        }
    }

    /// <summary>
    /// Pause the simulation. Does not affect command queue.
    /// </summary>
    public void Pause()
    {
        lock (_stateLock)
        {
            _isPaused = true;
        }
    }

    /// <summary>
    /// Resume the simulation from pause.
    /// </summary>
    public void Resume()
    {
        lock (_stateLock)
        {
            _isPaused = false;
        }
    }

    /// <summary>
    /// Toggle pause state.
    /// </summary>
    public void TogglePause()
    {
        lock (_stateLock)
        {
            _isPaused = !_isPaused;
        }
    }

    /// <summary>
    /// Set simulation speed multiplier.
    /// Clamped to [0.25, 8.0] range.
    /// </summary>
    public void SetSpeed(float multiplier)
    {
        lock (_stateLock)
        {
            _speedMultiplier = Math.Clamp(multiplier, 0.25f, 8.0f);
        }
    }

    /// <summary>
    /// Cycle through predefined speed levels: 0.25x, 0.5x, 1x, 2x, 4x, 8x.
    /// </summary>
    public void CycleSpeedUp()
    {
        lock (_stateLock)
        {
            _speedMultiplier = _speedMultiplier switch
            {
                < 0.5f => 0.5f,
                < 1.0f => 1.0f,
                < 2.0f => 2.0f,
                < 4.0f => 4.0f,
                < 8.0f => 8.0f,
                _ => 8.0f
            };
        }
    }

    /// <summary>
    /// Cycle through predefined speed levels (reverse).
    /// </summary>
    public void CycleSpeedDown()
    {
        lock (_stateLock)
        {
            _speedMultiplier = _speedMultiplier switch
            {
                > 4.0f => 4.0f,
                > 2.0f => 2.0f,
                > 1.0f => 1.0f,
                > 0.5f => 0.5f,
                > 0.25f => 0.25f,
                _ => 0.25f
            };
        }
    }

    /// <summary>
    /// Register a system to participate in the tick loop.
    /// </summary>
    public void RegisterSystem(ITick system)
    {
        ArgumentNullException.ThrowIfNull(system);
        var systemId = system.SystemId;
        if (string.IsNullOrWhiteSpace(systemId))
            throw new ArgumentException("Tick systems must provide a non-empty SystemId.", nameof(system));

        lock (_stateLock)
        {
            if (_isRunning || _tickThread != null)
                throw new InvalidOperationException("Cannot register systems while running");

            if (_systems.Any(existing => string.Equals(existing.SystemId, systemId, StringComparison.Ordinal)))
                throw new InvalidOperationException($"Tick system id '{systemId}' is already registered.");

            _systems.Add(system);
            _systemFailures.Remove(systemId);
            _systems.Sort(CompareSystems);
        }
    }

    /// <summary>
    /// Remove all registered systems before rebuilding a simulation session.
    /// </summary>
    public void ClearSystems()
    {
        lock (_stateLock)
        {
            if (_isRunning || _tickThread != null)
                throw new InvalidOperationException("Cannot clear systems while running");

            _systems.Clear();
            ResetSystemHealthNoLock();
        }
    }

    /// <summary>
    /// Reset scheduler state before starting a fresh simulation session.
    /// </summary>
    public void ResetForNewSession()
    {
        lock (_stateLock)
        {
            if (_isRunning || _tickThread != null)
                throw new InvalidOperationException("Cannot reset scheduler while running");

            _systems.Clear();
            ResetSystemHealthNoLock();
            _currentTick = 0;
            _isPaused = false;
            _speedMultiplier = 1.0f;
            _executionPosition = new TickSchedulerExecutionPosition(
                0,
                TickSchedulerExecutionPhase.Stopped);
            ResetStopRequest();
        }
    }

    /// <summary>
    /// Start the fixed-step simulation loop.
    /// </summary>
    public void Start()
    {
        Thread tickThread;
        lock (_stateLock)
        {
            if (_isRunning || _tickThread != null)
                return;

            ResetStopRequest();
            _isRunning = true;
            _executionPosition = new TickSchedulerExecutionPosition(
                _currentTick,
                TickSchedulerExecutionPhase.Starting);
            tickThread = new Thread(TickLoop)
            {
                Name = "SimulationTick",
                IsBackground = true  // Background thread will not prevent process exit
            };
            _tickThread = tickThread;
            try
            {
                tickThread.Start();
            }
            catch
            {
                _isRunning = false;
                _tickThread = null;
                _executionPosition = new TickSchedulerExecutionPosition(
                    _currentTick,
                    TickSchedulerExecutionPhase.Stopped);
                SignalStopRequest();
                throw;
            }
        }
    }

    /// <summary>
    /// Request simulation shutdown and wait for the bounded default stop budget.
    /// </summary>
    public void Stop()
    {
        _ = TryStop(DefaultStopTimeout);
    }

    /// <summary>
    /// Requests scheduler shutdown and waits up to <paramref name="timeout"/>
    /// for the tick thread to terminate.
    /// </summary>
    public TickSchedulerStopResult TryStop(TimeSpan timeout)
    {
        if (timeout < TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                timeout,
                $"Stop timeout must be between zero and {int.MaxValue} milliseconds.");
        }

        Thread? threadToJoin;
        lock (_stateLock)
        {
            if (!_isRunning && _tickThread == null)
            {
                return new TickSchedulerStopResult(
                    TickSchedulerStopStatus.AlreadyStopped,
                    _executionPosition);
            }

            _isRunning = false;
            threadToJoin = _tickThread;
        }

        SignalStopRequest();

        if (threadToJoin == Thread.CurrentThread)
        {
            return new TickSchedulerStopResult(
                TickSchedulerStopStatus.SelfStopRequested,
                ExecutionPosition);
        }

        if (threadToJoin == null)
        {
            return new TickSchedulerStopResult(
                TickSchedulerStopStatus.Stopped,
                ExecutionPosition);
        }

        if (!threadToJoin.Join(timeout))
        {
            return new TickSchedulerStopResult(
                TickSchedulerStopStatus.TimedOut,
                ExecutionPosition);
        }

        return new TickSchedulerStopResult(
            TickSchedulerStopStatus.Stopped,
            ExecutionPosition);
    }

    /// <summary>
    /// Execute a single tick synchronously (for testing/replay).
    /// </summary>
    public void ExecuteSingleTick()
    {
        lock (_stateLock)
        {
            if (_isRunning || _tickThread != null)
                throw new InvalidOperationException("Cannot execute a single tick while the scheduler is running");
        }

        try
        {
            _ = ExecuteTick(honorStopRequest: false);
        }
        finally
        {
            SetExecutionPosition(CurrentTick, TickSchedulerExecutionPhase.Stopped);
        }
    }

    private void TickLoop()
    {
        var frameTimer = Stopwatch.StartNew();
        var nextTickTime = frameTimer.ElapsedMilliseconds;

        try
        {
            while (!ShouldStop(honorStopRequest: true))
            {
                // If paused, sleep and skip tick execution
                if (GetIsPaused())
                {
                    SetExecutionPosition(CurrentTick, TickSchedulerExecutionPhase.Paused);
                    if (WaitForStop(millisecondsTimeout: 50))
                        break;
                    nextTickTime = frameTimer.ElapsedMilliseconds; // Reset timing when unpaused
                    continue;
                }

                var startTime = frameTimer.ElapsedMilliseconds;

                if (!ExecuteTick(honorStopRequest: true))
                    break;

                var elapsedMs = frameTimer.ElapsedMilliseconds - startTime;

                // Adjust tick interval based on speed multiplier.
                // Higher speed = shorter interval between ticks.
                int adjustedMsPerTick = Math.Max(1, (int)(MS_PER_TICK / GetSpeedMultiplier()));
                nextTickTime += adjustedMsPerTick;

                var sleepTime = (int)(nextTickTime - frameTimer.ElapsedMilliseconds);
                if (sleepTime > 0)
                {
                    SetExecutionPosition(CurrentTick, TickSchedulerExecutionPhase.Sleeping);
                    if (WaitForStop(sleepTime))
                        break;
                }
                else if (sleepTime < -adjustedMsPerTick * 5)
                {
                    // If we're more than 5 ticks behind, reset timing to prevent spiral
                    nextTickTime = frameTimer.ElapsedMilliseconds;
                }
            }
        }
        finally
        {
            SignalStopRequest();
            lock (_stateLock)
            {
                _isRunning = false;
                _executionPosition = new TickSchedulerExecutionPosition(
                    _currentTick,
                    TickSchedulerExecutionPhase.Stopped);
                if (ReferenceEquals(_tickThread, Thread.CurrentThread))
                {
                    _tickThread = null;
                }
            }
        }
    }

    private bool ExecuteTick(bool honorStopRequest)
    {
        var tick = CurrentTick;
        var systems = GetSystemSnapshot();

        if (ShouldStop(honorStopRequest))
            return false;

        SetExecutionPosition(tick, TickSchedulerExecutionPhase.PreTick);
        PreTick?.Invoke(tick);
        if (ShouldStop(honorStopRequest))
            return false;

        // Phase 1: Read in deterministic registered-system order.
        var readFailures = new HashSet<ITick>();
        if (!ExecuteReadPhase(tick, systems, readFailures, honorStopRequest))
            return false;

        // Barrier
        SetExecutionPosition(tick, TickSchedulerExecutionPhase.Barrier);
        lock (_barrierLock)
        {
            BarrierReached?.Invoke(tick);
        }
        if (ShouldStop(honorStopRequest))
            return false;

        // Phase 2: Write (serialized)
        if (!ExecuteWritePhase(tick, systems, readFailures, honorStopRequest))
            return false;

        SetExecutionPosition(tick, TickSchedulerExecutionPhase.PostTick);
        PostTick?.Invoke(tick);

        SetExecutionPosition(tick, TickSchedulerExecutionPhase.AdvancingTick);
        AdvanceTick();
        return true;
    }

    private bool ExecuteReadPhase(
        ulong tick,
        IReadOnlyList<ITick> systems,
        HashSet<ITick> failedSystems,
        bool honorStopRequest)
    {
        foreach (var system in systems)
        {
            if (ShouldStop(honorStopRequest))
                return false;
            if (IsSystemQuarantined(system))
                continue;

            SetExecutionPosition(tick, TickSchedulerExecutionPhase.Read, system.SystemId);
            try
            {
                system.ReadTick(tick);
            }
            catch (Exception ex)
            {
                failedSystems.Add(system);
                HandleSystemError(system, "Read", ex);
            }

            if (ShouldStop(honorStopRequest))
                return false;
        }

        return true;
    }

    private bool ExecuteWritePhase(
        ulong tick,
        IReadOnlyList<ITick> systems,
        IReadOnlySet<ITick> readFailures,
        bool honorStopRequest)
    {
        // Write phase must be serialized
        foreach (var system in systems)
        {
            if (ShouldStop(honorStopRequest))
                return false;
            if (IsSystemQuarantined(system) || readFailures.Contains(system))
                continue;

            SetExecutionPosition(tick, TickSchedulerExecutionPhase.Write, system.SystemId);
            try
            {
                system.WriteTick(tick);
                ClearSystemFailure(system);
            }
            catch (Exception ex)
            {
                HandleSystemError(system, "Write", ex);
            }

            if (ShouldStop(honorStopRequest))
                return false;
        }

        return true;
    }

    private ITick[] GetSystemSnapshot()
    {
        lock (_stateLock)
        {
            return _systems.ToArray();
        }
    }

    private bool GetIsRunning()
    {
        lock (_stateLock)
        {
            return _isRunning;
        }
    }

    private bool ShouldStop(bool honorStopRequest)
    {
        return honorStopRequest && (IsStopRequested() || !GetIsRunning());
    }

    private bool IsStopRequested()
    {
        lock (_stopLock)
        {
            return _stopRequested;
        }
    }

    private void ResetStopRequest()
    {
        lock (_stopLock)
        {
            _stopRequested = false;
        }
    }

    private void SignalStopRequest()
    {
        lock (_stopLock)
        {
            _stopRequested = true;
            Monitor.PulseAll(_stopLock);
        }
    }

    private bool WaitForStop(int millisecondsTimeout)
    {
        lock (_stopLock)
        {
            if (_stopRequested)
                return true;

            _ = Monitor.Wait(_stopLock, millisecondsTimeout);
            return _stopRequested;
        }
    }

    private void SetExecutionPosition(
        ulong tick,
        TickSchedulerExecutionPhase phase,
        string? systemId = null)
    {
        lock (_stateLock)
        {
            _executionPosition = new TickSchedulerExecutionPosition(tick, phase, systemId);
        }
    }

    private bool GetIsPaused()
    {
        lock (_stateLock)
        {
            return _isPaused;
        }
    }

    private float GetSpeedMultiplier()
    {
        lock (_stateLock)
        {
            return _speedMultiplier;
        }
    }

    private void AdvanceTick()
    {
        lock (_stateLock)
        {
            _currentTick++;
        }
    }

    private bool IsSystemQuarantined(ITick system)
    {
        lock (_stateLock)
        {
            return _systemFailures.TryGetValue(system.SystemId, out var state)
                && state.Quarantined;
        }
    }

    private void ClearSystemFailure(ITick system)
    {
        lock (_stateLock)
        {
            _systemFailures.Remove(system.SystemId);
        }
    }

    private void HandleSystemError(ITick system, string phase, Exception ex)
    {
        // Per ERROR_HANDLING_POLICY.md: catch, quarantine, log, continue
        var state = RecordSystemFailure(system);
        Diagnostics.Error(
            "Core.TickScheduler",
            state.Quarantined
                ? $"[ERROR] System {system.SystemId} failed in {phase} phase and was quarantined after {state.ConsecutiveFailures} consecutive failures: {ex.Message}"
                : $"[ERROR] System {system.SystemId} failed in {phase} phase: {ex.Message}",
            ex,
            CurrentTick);
    }

    private IDiagnosticSink Diagnostics => _diagnostics ?? DiagnosticHub.Sink;

    private SystemFailureState RecordSystemFailure(ITick system)
    {
        lock (_stateLock)
        {
            if (!_systemFailures.TryGetValue(system.SystemId, out var state))
            {
                state = new SystemFailureState();
                _systemFailures[system.SystemId] = state;
            }

            state.ConsecutiveFailures++;
            _systemFailureCountTotal++;
            _systemFailureTotals[system.SystemId] =
                _systemFailureTotals.TryGetValue(system.SystemId, out var total)
                    ? total + 1
                    : 1;
            if (state.ConsecutiveFailures >= MAX_CONSECUTIVE_SYSTEM_FAILURES)
                state.Quarantined = true;

            return state.Copy();
        }
    }

    private void ResetSystemHealthNoLock()
    {
        _systemFailures.Clear();
        _systemFailureTotals.Clear();
        _systemFailureCountTotal = 0;
    }

    private static int CompareSystems(ITick a, ITick b)
    {
        var priority = a.Priority.CompareTo(b.Priority);
        return priority != 0
            ? priority
            : string.CompareOrdinal(a.SystemId, b.SystemId);
    }

    private sealed class SystemFailureState
    {
        public int ConsecutiveFailures { get; set; }
        public bool Quarantined { get; set; }

        public SystemFailureState Copy()
        {
            return new SystemFailureState
            {
                ConsecutiveFailures = ConsecutiveFailures,
                Quarantined = Quarantined
            };
        }
    }
}
