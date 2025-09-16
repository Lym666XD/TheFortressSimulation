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
            var startTime = _frameTimer.ElapsedMilliseconds;

            ExecuteTick();

            var elapsedMs = _frameTimer.ElapsedMilliseconds - startTime;
            nextTickTime += MS_PER_TICK;

            var sleepTime = (int)(nextTickTime - _frameTimer.ElapsedMilliseconds);
            if (sleepTime > 0)
            {
                Thread.Sleep(sleepTime);
            }
            else if (sleepTime < -MS_PER_TICK * 5)
            {
                // If we're more than 5 ticks behind, reset timing
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