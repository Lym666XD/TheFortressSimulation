using HumanFortress.App;
using HumanFortress.App.Runtime;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Events;
using HumanFortress.Core.Random;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Simulation.World;
using HumanFortress.Navigation;

namespace HumanFortress.App.GameStates;

/// <summary>
/// Manages game state transitions and owns core systems per GAME_ARCHITECTURE.md.
/// </summary>
public sealed class GameStateManager
{
    private readonly Dictionary<GameStateType, GameState> _states;
    private readonly TickScheduler _tickScheduler;
    private readonly CommandQueue _commandQueue;
    private readonly EventBus _eventBus;
    private readonly RngStreamManager _rngManager;
    private readonly DiffLog _diffLog;
    private readonly HumanFortress.Simulation.Items.ItemsDiffLog _itemsDiffLog;
    private readonly bool _enqueueAutoDig;

    private GameState? _currentState;
    private World? _world;
    private NavigationManager? _navManager;
    private SimulationRuntimeHost? _runtimeHost;
    private JobsDebugData? _jobsDebugCache;
    private ulong _jobsDebugCacheTick = 0;
    private const ulong JobsDebugRefreshTicks = 10;

    public GameStateManager(ulong masterSeed, bool enqueueAutoDig = false)
    {
        _states = new Dictionary<GameStateType, GameState>();
        _tickScheduler = new TickScheduler();
        _commandQueue = new CommandQueue();
        _eventBus = new EventBus();
        _rngManager = new RngStreamManager(masterSeed);
        _diffLog = new DiffLog();
        _itemsDiffLog = new HumanFortress.Simulation.Items.ItemsDiffLog();
        _enqueueAutoDig = enqueueAutoDig;
    }

    /// <summary>
    /// Current game state.
    /// </summary>
    public GameState? CurrentState => _currentState;

    /// <summary>
    /// Active world (when in fortress play).
    /// </summary>
    public World? World => _world;

    /// <summary>
    /// Current simulation clock and controls exposed without leaking the scheduler.
    /// </summary>
    public ulong CurrentTick => _tickScheduler.CurrentTick;
    public SimulationStatus SimulationStatus => new(_tickScheduler.CurrentTick, _tickScheduler.IsPaused, _tickScheduler.SpeedMultiplier);

    public HumanFortress.Simulation.Orders.HaulingSystem? HaulingPlanner => _runtimeHost?.Systems?.HaulingPlanner;
    public HumanFortress.Simulation.Jobs.ITransportRequestQueue? TransportQueue => _runtimeHost?.Systems?.TransportQueue;
    public HumanFortress.App.Jobs.TransportJobSystem? TransportJobs => _runtimeHost?.Systems?.TransportJobs;
    public HumanFortress.Simulation.Orders.MiningSystem? MiningPlanner => _runtimeHost?.Systems?.MiningPlanner;
    public HumanFortress.App.Jobs.MiningJobSystem? MiningJobs => _runtimeHost?.Systems?.MiningJobs;
    public HumanFortress.Simulation.Orders.ConstructionSystem? ConstructionPlanner => _runtimeHost?.Systems?.ConstructionPlanner;
    public HumanFortress.App.Jobs.ConstructionJobSystem? ConstructionJobs => _runtimeHost?.Systems?.ConstructionJobs;
    public HumanFortress.App.Jobs.CraftPlanner? CraftPlanner => _runtimeHost?.Systems?.CraftPlanner;
    public HumanFortress.App.Jobs.CraftJobSystem? CraftJobs => _runtimeHost?.Systems?.CraftJobs;
    public HumanFortress.App.Jobs.ProfessionAssignments? ProfessionAssignments => _runtimeHost?.Systems?.ProfessionAssignments;
    public NavigationManager? NavManager => _runtimeHost?.Navigation ?? _navManager;
    public HumanFortress.App.Jobs.UnifiedJobsOrchestrator? JobsOrchestrator => _runtimeHost?.Systems?.JobsOrchestrator;
    public HumanFortress.App.Jobs.SchedulerTunings? SchedulerTunings => _runtimeHost?.Systems?.SchedulerTunings;
    public HumanFortress.App.Jobs.WorkshopTunings? WorkshopTunings => _runtimeHost?.Systems?.WorkshopTunings;

    /// <summary>
    /// Cached debug data for Jobs/Work drawer. Gated by SchedulerTunings.DebugPanel.
    /// Refreshes every JobsDebugRefreshTicks unless force=true.
    /// </summary>
    public JobsDebugData? GetJobsDebugData(ulong tick, bool force = false)
    {
        var systems = _runtimeHost?.Systems;
        var tunings = systems?.SchedulerTunings;
        if (systems == null || tunings == null || !tunings.DebugPanel) return null;
        if (!force && _jobsDebugCache.HasValue && (tick - _jobsDebugCacheTick) < JobsDebugRefreshTicks)
            return _jobsDebugCache;

        var transport = systems.TransportJobs.GetDebugSnapshot(
            maxActive: 8,
            maxRequests: 8,
            includeSeeds: tunings.DebugPanel);
        var mining = systems.MiningJobs.GetDebugSnapshot(
            maxActive: 8,
            includeSeeds: tunings.DebugPanel);

        var craft = systems.CraftJobs.GetLastStatsSnapshot();

        _jobsDebugCache = new JobsDebugData(
            Tick: tick,
            Transport: transport,
            Mining: mining,
            Craft: craft,
            Tunings: tunings);
        _jobsDebugCacheTick = tick;
        return _jobsDebugCache;
    }

    public IReadOnlyList<HumanFortress.App.Jobs.ProfessionAssignments.ProfessionRosterEntry> GetProfessionRosterSnapshot()
    {
        var professions = _runtimeHost?.Systems?.ProfessionAssignments;
        if (professions == null) return Array.Empty<HumanFortress.App.Jobs.ProfessionAssignments.ProfessionRosterEntry>();
        return professions.GetRosterSnapshot(_world);
    }

    public void SetProfessionWeight(Guid workerId, string professionId, int weight)
    {
        _runtimeHost?.Systems?.ProfessionAssignments.SetWeight(workerId, professionId, weight);
    }

    /// <summary>
    /// Enqueue a simulation command.
    /// </summary>
    public void EnqueueCommand(ICommand command)
    {
        _commandQueue.Enqueue(command);
    }

    public void EnqueueCurrentTickCommand(Func<ulong, ICommand> commandFactory)
    {
        ArgumentNullException.ThrowIfNull(commandFactory);
        EnqueueCommand(commandFactory(CurrentTick));
    }

    public SimulationStatus ToggleSimulationPause()
    {
        _tickScheduler.TogglePause();
        return SimulationStatus;
    }

    public SimulationStatus CycleSimulationSpeedDown()
    {
        _tickScheduler.CycleSpeedDown();
        return SimulationStatus;
    }

    public SimulationStatus CycleSimulationSpeedUp()
    {
        _tickScheduler.CycleSpeedUp();
        return SimulationStatus;
    }

    /// <summary>
    /// Register a state.
    /// </summary>
    public void RegisterState(GameState state)
    {
        _states[state.Type] = state;
    }

    /// <summary>
    /// Transition to a new state.
    /// </summary>
    public void TransitionTo(GameStateType newStateType)
    {
        ChangeState(newStateType);
    }

    /// <summary>
    /// Convenience method for changing states (used by UI states).
    /// </summary>
    public void ChangeState(GameStateType newStateType)
    {
        try
        {
            Logger.Log($"[GameStateManager] ChangeState from {_currentState?.Type} to {newStateType}");
            Logger.Log($"[GameStateManager] States registered: {string.Join(", ", _states.Keys)}");

            // Per GAME_STATE_FLOW.md: transitions happen at end-of-tick barrier
            if (_currentState != null)
            {
                Logger.Log($"[GameStateManager] Calling Exit on {_currentState.Type}");
                _currentState.Exit();

                // Stop simulation when leaving FortressPlay
                if (_currentState.Type == GameStateType.FortressPlay)
                {
                    Logger.Log("[GameStateManager] Stopping simulation");
                    _runtimeHost?.Stop();
                }
            }

            if (!_states.TryGetValue(newStateType, out var newState))
            {
                Logger.Log($"[GameStateManager] ERROR: State {newStateType} not found in registered states");
                throw new InvalidOperationException($"State {newStateType} not registered");
            }

            _currentState = newState;
            Logger.Log($"[GameStateManager] Calling Enter on {newStateType}");
            _currentState.Enter();
            Logger.Log($"[GameStateManager] Enter completed for {newStateType}");

            // Start simulation when entering FortressPlay
            if (newStateType == GameStateType.FortressPlay)
            {
                Logger.Log("[GameStateManager] Starting simulation for FortressPlay");
                var runtime = RequireRuntimeHost();
                _jobsDebugCache = null;
                _jobsDebugCacheTick = 0;
                runtime.Start(enqueueAutoDig: _enqueueAutoDig);
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"[GameStateManager] FATAL ERROR in ChangeState: {ex.Message}");
            Logger.Log($"[GameStateManager] Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Logger.Log($"[GameStateManager] Inner exception: {ex.InnerException.Message}");
                Logger.Log($"[GameStateManager] Inner stack: {ex.InnerException.StackTrace}");
            }
            throw;
        }
    }

    /// <summary>
    /// Initialize world for fortress play.
    /// </summary>
    public void InitializeWorld(int sizeInChunks, int maxZ)
    {
        _runtimeHost?.Stop();

        _tickScheduler.ResetForNewSession();
        _commandQueue.Clear();
        _diffLog.Clear();
        _itemsDiffLog.Clear();
        _jobsDebugCache = null;
        _jobsDebugCacheTick = 0;

        _world = new World(sizeInChunks, maxZ);
        // Initialize shared NavigationManager bound to this world
        _navManager = new NavigationManager(_world);
        SimulationWorldContentLoader.LoadCoreContent(_world, AppContext.BaseDirectory);
        _runtimeHost = new SimulationRuntimeHost(
            _world,
            _tickScheduler,
            _commandQueue,
            _eventBus,
            _diffLog,
            _itemsDiffLog,
            _navManager,
            AppContext.BaseDirectory);
    }

    /// <summary>
    /// Update current state.
    /// </summary>
    public void Update(double deltaTime)
    {
        _currentState?.Update(deltaTime);
    }

    /// <summary>
    /// Render current state.
    /// </summary>
    public void Render()
    {
        _currentState?.Render();
    }

    /// <summary>
    /// Handle input for current state.
    /// </summary>
    public void HandleInput()
    {
        _currentState?.HandleInput();
    }

    public readonly record struct JobsDebugData(
        ulong Tick,
        HumanFortress.App.Jobs.TransportJobSystem.TransportDebugSnapshot? Transport,
        HumanFortress.App.Jobs.MiningJobSystem.MiningDebugSnapshot? Mining,
        HumanFortress.App.Jobs.CraftJobStatsSnapshot? Craft,
        HumanFortress.App.Jobs.SchedulerTunings? Tunings);

    /// <summary>
    /// Shutdown and cleanup all systems before application exit.
    /// </summary>
    public void Shutdown()
    {
        Logger.Log("[GameStateManager] Shutdown requested");

        // Stop simulation if running
        if (_runtimeHost?.IsRunning == true)
        {
            Logger.Log("[GameStateManager] Stopping tick scheduler");
            _runtimeHost.Stop();
        }

        // Exit current state
        if (_currentState != null)
        {
            Logger.Log($"[GameStateManager] Exiting current state: {_currentState.Type}");
            _currentState.Exit();
        }

        Logger.Log("[GameStateManager] Shutdown complete");
    }

    private SimulationRuntimeHost RequireRuntimeHost()
    {
        if (_runtimeHost != null)
            return _runtimeHost;
        if (_world == null)
            throw new InvalidOperationException("World not initialized");
        if (_navManager == null)
            _navManager = new NavigationManager(_world);

        _runtimeHost = new SimulationRuntimeHost(
            _world,
            _tickScheduler,
            _commandQueue,
            _eventBus,
            _diffLog,
            _itemsDiffLog,
            _navManager,
            AppContext.BaseDirectory);
        return _runtimeHost;
    }
}
