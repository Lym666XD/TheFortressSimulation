using System.Diagnostics;
using HumanFortress.Contracts.Diagnostics;

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
    private readonly object _barrierLock = new();
    private readonly object _stateLock = new();
    private readonly IDiagnosticSink? _diagnostics;

    private ulong _currentTick;
    private bool _isRunning;
    private bool _isPaused;
    private float _speedMultiplier = 1.0f;
    private Thread? _tickThread;

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
    /// Target ticks per second (fixed at 50).
    /// </summary>
    public int TargetTPS => TARGET_TPS;

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
            if (_isRunning)
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
            if (_isRunning)
                throw new InvalidOperationException("Cannot clear systems while running");

            _systems.Clear();
            _systemFailures.Clear();
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
            _systemFailures.Clear();
            _currentTick = 0;
            _isPaused = false;
            _speedMultiplier = 1.0f;
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

            _isRunning = true;
            tickThread = new Thread(TickLoop)
            {
                Name = "SimulationTick",
                IsBackground = true  // Background thread will not prevent process exit
            };
            _tickThread = tickThread;
        }

        tickThread.Start();
    }

    /// <summary>
    /// Stop the simulation loop after the current tick completes.
    /// </summary>
    public void Stop()
    {
        Thread? threadToJoin;
        lock (_stateLock)
        {
            if (!_isRunning && _tickThread == null)
                return;

            _isRunning = false;
            threadToJoin = _tickThread;
        }

        if (threadToJoin != null && threadToJoin != Thread.CurrentThread)
        {
            threadToJoin.Join();
            lock (_stateLock)
            {
                if (ReferenceEquals(_tickThread, threadToJoin))
                {
                    _tickThread = null;
                }
            }
        }
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

        ExecuteTick();
    }

    private void TickLoop()
    {
        var frameTimer = Stopwatch.StartNew();
        var nextTickTime = frameTimer.ElapsedMilliseconds;

        try
        {
            while (GetIsRunning())
            {
                // If paused, sleep and skip tick execution
                if (GetIsPaused())
                {
                    Thread.Sleep(50); // Sleep 50ms when paused to reduce CPU usage
                    nextTickTime = frameTimer.ElapsedMilliseconds; // Reset timing when unpaused
                    continue;
                }

                var startTime = frameTimer.ElapsedMilliseconds;

                ExecuteTick();

                var elapsedMs = frameTimer.ElapsedMilliseconds - startTime;

                // Adjust tick interval based on speed multiplier.
                // Higher speed = shorter interval between ticks.
                int adjustedMsPerTick = Math.Max(1, (int)(MS_PER_TICK / GetSpeedMultiplier()));
                nextTickTime += adjustedMsPerTick;

                var sleepTime = (int)(nextTickTime - frameTimer.ElapsedMilliseconds);
                if (sleepTime > 0)
                {
                    Thread.Sleep(sleepTime);
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
            lock (_stateLock)
            {
                _isRunning = false;
                if (ReferenceEquals(_tickThread, Thread.CurrentThread))
                {
                    _tickThread = null;
                }
            }
        }
    }

    private void ExecuteTick()
    {
        var tick = CurrentTick;
        var systems = GetSystemSnapshot();

        PreTick?.Invoke(tick);

        // Phase 1: Read in deterministic registered-system order.
        var readFailures = ExecuteReadPhase(tick, systems);

        // Barrier
        lock (_barrierLock)
        {
            BarrierReached?.Invoke(tick);
        }

        // Phase 2: Write (serialized)
        ExecuteWritePhase(tick, systems, readFailures);

        PostTick?.Invoke(tick);

        AdvanceTick();
    }

    private HashSet<ITick> ExecuteReadPhase(ulong tick, IReadOnlyList<ITick> systems)
    {
        var failedSystems = new HashSet<ITick>();
        foreach (var system in systems)
        {
            if (IsSystemQuarantined(system))
                continue;

            try
            {
                system.ReadTick(tick);
            }
            catch (Exception ex)
            {
                failedSystems.Add(system);
                HandleSystemError(system, "Read", ex);
            }
        }

        return failedSystems;
    }

    private void ExecuteWritePhase(ulong tick, IReadOnlyList<ITick> systems, IReadOnlySet<ITick> readFailures)
    {
        // Write phase must be serialized
        foreach (var system in systems)
        {
            if (IsSystemQuarantined(system) || readFailures.Contains(system))
                continue;

            try
            {
                system.WriteTick(tick);
                ClearSystemFailure(system);
            }
            catch (Exception ex)
            {
                HandleSystemError(system, "Write", ex);
            }
        }
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
            if (state.ConsecutiveFailures >= MAX_CONSECUTIVE_SYSTEM_FAILURES)
                state.Quarantined = true;

            return state.Copy();
        }
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
