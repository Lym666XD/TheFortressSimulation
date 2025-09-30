using System.Diagnostics;

namespace HumanFortress.Core.Time;

/// <summary>
/// Fixed-step tick scheduler implementing the authoritative UPDATE_ORDER.
/// Runs at 50 TPS (20ms per tick) with read-parallel/write-serialized execution.
/// </summary>
public sealed class TickScheduler
{
    private const int TARGET_TPS = 50;
    private const int MS_PER_TICK = 1000 / TARGET_TPS; // 20ms

    private readonly List<ITick> _systems = new();
    private readonly object _barrierLock = new();
    private readonly Stopwatch _frameTimer = new();

    private ulong _currentTick;
    private bool _isRunning;
    private bool _isPaused;
    private float _speedMultiplier = 1.0f;
    private Thread? _tickThread;

    public event Action<ulong>? PreTick;
    public event Action<ulong>? PostTick;
    public event Action<ulong>? BarrierReached;

    /// <summary>
    /// Current simulation tick number.
    /// </summary>
    public ulong CurrentTick => _currentTick;

    /// <summary>
    /// Whether the simulation is currently running.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Target ticks per second (fixed at 50).
    /// </summary>
    public int TargetTPS => TARGET_TPS;

    /// <summary>
    /// Whether the simulation is currently paused.
    /// </summary>
    public bool IsPaused => _isPaused;

    /// <summary>
    /// Current speed multiplier (0.25x - 8x).
    /// </summary>
    public float SpeedMultiplier => _speedMultiplier;

    /// <summary>
    /// Pause the simulation. Does not affect command queue.
    /// </summary>
    public void Pause()
    {
        _isPaused = true;
    }

    /// <summary>
    /// Resume the simulation from pause.
    /// </summary>
    public void Resume()
    {
        _isPaused = false;
    }

    /// <summary>
    /// Toggle pause state.
    /// </summary>
    public void TogglePause()
    {
        _isPaused = !_isPaused;
    }

    /// <summary>
    /// Set simulation speed multiplier.
    /// Clamped to [0.25, 8.0] range.
    /// </summary>
    public void SetSpeed(float multiplier)
    {
        _speedMultiplier = Math.Clamp(multiplier, 0.25f, 8.0f);
    }

    /// <summary>
    /// Cycle through predefined speed levels: 0.25x, 0.5x, 1x, 2x, 4x, 8x.
    /// </summary>
    public void CycleSpeedUp()
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

    /// <summary>
    /// Cycle through predefined speed levels (reverse).
    /// </summary>
    public void CycleSpeedDown()
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

    /// <summary>
    /// Register a system to participate in the tick loop.
    /// </summary>
    public void RegisterSystem(ITick system)
    {
        if (_isRunning)
            throw new InvalidOperationException("Cannot register systems while running");

        _systems.Add(system);
        _systems.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }

    /// <summary>
    /// Start the fixed-step simulation loop.
    /// </summary>
    public void Start()
    {
        if (_isRunning)
            return;

        _isRunning = true;
        _tickThread = new Thread(TickLoop)
        {
            Name = "SimulationTick",
            IsBackground = false
        };
        _tickThread.Start();
    }

    /// <summary>
    /// Stop the simulation loop after the current tick completes.
    /// </summary>
    public void Stop()
    {
        _isRunning = false;
        _tickThread?.Join();
        _tickThread = null;
    }

    /// <summary>
    /// Execute a single tick synchronously (for testing/replay).
    /// </summary>
    public void ExecuteSingleTick()
    {
        ExecuteTick();
    }

    private void TickLoop()
    {
        _frameTimer.Start();
        var nextTickTime = _frameTimer.ElapsedMilliseconds;

        while (_isRunning)
        {
            // If paused, sleep and skip tick execution
            if (_isPaused)
            {
                Thread.Sleep(50); // Sleep 50ms when paused to reduce CPU usage
                nextTickTime = _frameTimer.ElapsedMilliseconds; // Reset timing when unpaused
                continue;
            }

            var startTime = _frameTimer.ElapsedMilliseconds;

            ExecuteTick();

            var elapsedMs = _frameTimer.ElapsedMilliseconds - startTime;

            // Adjust tick interval based on speed multiplier
            // Higher speed = shorter interval between ticks
            int adjustedMsPerTick = (int)(MS_PER_TICK / _speedMultiplier);
            nextTickTime += adjustedMsPerTick;

            var sleepTime = (int)(nextTickTime - _frameTimer.ElapsedMilliseconds);
            if (sleepTime > 0)
            {
                Thread.Sleep(sleepTime);
            }
            else if (sleepTime < -adjustedMsPerTick * 5)
            {
                // If we're more than 5 ticks behind, reset timing to prevent spiral
                nextTickTime = _frameTimer.ElapsedMilliseconds;
            }
        }
    }

    private void ExecuteTick()
    {
        var tick = _currentTick;

        PreTick?.Invoke(tick);

        // Phase 1: Read (parallel allowed)
        ExecuteReadPhase(tick);

        // Barrier
        lock (_barrierLock)
        {
            BarrierReached?.Invoke(tick);
        }

        // Phase 2: Write (serialized)
        ExecuteWritePhase(tick);

        PostTick?.Invoke(tick);

        _currentTick++;
    }

    private void ExecuteReadPhase(ulong tick)
    {
        // Systems can run in parallel during read phase
        Parallel.ForEach(_systems, system =>
        {
            try
            {
                system.ReadTick(tick);
            }
            catch (Exception ex)
            {
                HandleSystemError(system, "Read", ex);
            }
        });
    }

    private void ExecuteWritePhase(ulong tick)
    {
        // Write phase must be serialized
        foreach (var system in _systems)
        {
            try
            {
                system.WriteTick(tick);
            }
            catch (Exception ex)
            {
                HandleSystemError(system, "Write", ex);
            }
        }
    }

    private void HandleSystemError(ITick system, string phase, Exception ex)
    {
        // Per ERROR_HANDLING_POLICY.md: catch, quarantine, log, continue
        Console.WriteLine($"[ERROR] System {system.SystemId} failed in {phase} phase: {ex.Message}");
        // TODO: Implement quarantine logic
    }
}