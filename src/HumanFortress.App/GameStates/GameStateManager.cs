using HumanFortress.App;
using HumanFortress.App.Commands;
using HumanFortress.App.Diagnostics;
using HumanFortress.App.Runtime;
using HumanFortress.Content.Loading;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Content.Registry;
using HumanFortress.Core.Events;
using HumanFortress.Core.Random;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Jobs.Craft;
using HumanFortress.Jobs.Mining;
using HumanFortress.Simulation.World;
using HumanFortress.Navigation;
using HumanFortress.Jobs.Transport;
using HumanFortress.Runtime;
using HumanFortress.WorldGen;

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
    private readonly bool _strictContent;
    private readonly bool _contentWarningsAsErrors;
    private readonly SimulationRuntimeSessionFactory<SimulationRuntimeHost<SimulationRuntimeSystems>> _runtimeSessionFactory;

    private GameState? _currentState;
    private SimulationRuntimeSession<SimulationRuntimeHost<SimulationRuntimeSystems>>? _runtimeSession;
    private JobsDebugData? _jobsDebugCache;
    private ulong _jobsDebugCacheTick = 0;
    private FortressRuntimeContentSnapshot? _runtimeContentSnapshot;
    private FortressGenerationContent? _generationContent;
    private const ulong JobsDebugRefreshTicks = 10;

    public GameStateManager(
        ulong masterSeed,
        bool enqueueAutoDig = false,
        bool strictContent = false,
        bool contentWarningsAsErrors = false)
    {
        _states = new Dictionary<GameStateType, GameState>();
        _tickScheduler = new TickScheduler();
        _commandQueue = new CommandQueue();
        _eventBus = new EventBus();
        _rngManager = new RngStreamManager(masterSeed);
        _diffLog = new DiffLog();
        _itemsDiffLog = new HumanFortress.Simulation.Items.ItemsDiffLog();
        _enqueueAutoDig = enqueueAutoDig;
        _strictContent = strictContent;
        _contentWarningsAsErrors = contentWarningsAsErrors;
        var baseDir = AppContext.BaseDirectory;
        _runtimeSessionFactory = new SimulationRuntimeSessionFactory<SimulationRuntimeHost<SimulationRuntimeSystems>>(
            _tickScheduler,
            _commandQueue,
            _diffLog,
            _itemsDiffLog,
            world =>
            {
                _runtimeContentSnapshot = null;
                _generationContent = null;
                _runtimeContentSnapshot = SimulationWorldContentLoader.LoadCoreContent(
                    world,
                    baseDir,
                    _strictContent,
                    _contentWarningsAsErrors);
                _generationContent = CreateFortressGenerationContent(_runtimeContentSnapshot);
            },
            (world, navigation) => FortressRuntimeHostFactory.Create(
                world,
                _tickScheduler,
                _commandQueue,
                _eventBus,
                _diffLog,
                _itemsDiffLog,
                navigation,
                baseDir,
                _runtimeContentSnapshot),
            () => NavigationTuning.LoadFromJson(_runtimeContentSnapshot?.NavigationTuningJson));
    }

    /// <summary>
    /// Current game state.
    /// </summary>
    public GameState? CurrentState => _currentState;

    /// <summary>
    /// Active world (when in fortress play).
    /// </summary>
    public World? World => _runtimeSession?.World;

    /// <summary>
    /// Current simulation clock and controls exposed without leaking the scheduler.
    /// </summary>
    public ulong CurrentTick => _tickScheduler.CurrentTick;
    public SimulationStatus SimulationStatus => new(_tickScheduler.CurrentTick, _tickScheduler.IsPaused, _tickScheduler.SpeedMultiplier);

    public HumanFortress.Simulation.Orders.HaulingSystem? HaulingPlanner => RuntimeHost?.Systems?.HaulingPlanner;
    public HumanFortress.Simulation.Jobs.ITransportRequestQueue? TransportQueue => RuntimeHost?.Systems?.TransportQueue;
    public HumanFortress.App.Jobs.TransportJobSystem? TransportJobs => RuntimeHost?.Systems?.TransportJobs;
    public HumanFortress.Simulation.Orders.MiningSystem? MiningPlanner => RuntimeHost?.Systems?.MiningPlanner;
    public HumanFortress.App.Jobs.MiningJobSystem? MiningJobs => RuntimeHost?.Systems?.MiningJobs;
    public HumanFortress.Simulation.Orders.ConstructionSystem? ConstructionPlanner => RuntimeHost?.Systems?.ConstructionPlanner;
    public HumanFortress.App.Jobs.ConstructionJobSystem? ConstructionJobs => RuntimeHost?.Systems?.ConstructionJobs;
    public HumanFortress.Jobs.Craft.CraftPlanner? CraftPlanner => RuntimeHost?.Systems?.CraftPlanner;
    public HumanFortress.App.Jobs.CraftJobSystem? CraftJobs => RuntimeHost?.Systems?.CraftJobs;
    public HumanFortress.App.Jobs.ProfessionAssignments? ProfessionAssignments => RuntimeHost?.Systems?.ProfessionAssignments;
    public NavigationManager? NavManager => _runtimeSession?.Navigation;
    public HumanFortress.App.Jobs.UnifiedJobsOrchestrator? JobsOrchestrator => RuntimeHost?.Systems?.JobsOrchestrator;
    public HumanFortress.App.Jobs.SchedulerTunings? SchedulerTunings => RuntimeHost?.Systems?.SchedulerTunings;
    public HumanFortress.App.Jobs.WorkshopTunings? WorkshopTunings => RuntimeHost?.Systems?.WorkshopTunings;
    public NavigationTuning? NavigationTuning => RuntimeHost?.NavigationTuning;
    public IRecipeCatalog? Recipes => RuntimeHost?.Recipes;
    public IConstructionCatalog? Constructions => RuntimeHost?.Constructions;
    public IRuntimeGeologyCatalog? Geology => RuntimeHost?.Geology;
    public FortressGenerationContent? GenerationContent => _generationContent;

    public DiagnosticSnapshot GetDiagnosticSnapshot()
    {
        return Logger.GetSnapshot();
    }

    /// <summary>
    /// Cached debug data for Jobs/Work drawer. Gated by SchedulerTunings.DebugPanel.
    /// Refreshes every JobsDebugRefreshTicks unless force=true.
    /// </summary>
    public JobsDebugData? GetJobsDebugData(ulong tick, bool force = false)
    {
        var systems = RuntimeHost?.Systems;
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
        var professions = RuntimeHost?.Systems?.ProfessionAssignments;
        if (professions == null) return Array.Empty<HumanFortress.App.Jobs.ProfessionAssignments.ProfessionRosterEntry>();
        return professions.GetRosterSnapshot(World);
    }

    public void SetProfessionWeight(Guid workerId, string professionId, int weight)
    {
        EnqueueCurrentTickCommand(tick => new SetProfessionWeightCommand(tick, workerId, professionId, weight));
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
                    RuntimeHost?.Stop();
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
                FortressRuntimeStartup.Start(runtime, _enqueueAutoDig, _commandQueue, _tickScheduler);
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
        RuntimeHost?.Stop();

        _jobsDebugCache = null;
        _jobsDebugCacheTick = 0;

        _runtimeSession = _runtimeSessionFactory.CreateNew(sizeInChunks, maxZ);
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
        TransportDebugSnapshot? Transport,
        MiningDebugSnapshot? Mining,
        CraftJobStatsSnapshot? Craft,
        HumanFortress.App.Jobs.SchedulerTunings? Tunings);

    /// <summary>
    /// Shutdown and cleanup all systems before application exit.
    /// </summary>
    public void Shutdown()
    {
        Logger.Log("[GameStateManager] Shutdown requested");

        // Stop simulation if running
        if (RuntimeHost?.IsRunning == true)
        {
            Logger.Log("[GameStateManager] Stopping tick scheduler");
            RuntimeHost.Stop();
        }

        // Exit current state
        if (_currentState != null)
        {
            Logger.Log($"[GameStateManager] Exiting current state: {_currentState.Type}");
            _currentState.Exit();
        }

        Logger.Log("[GameStateManager] Shutdown complete");
    }

    private SimulationRuntimeHost<SimulationRuntimeSystems> RequireRuntimeHost()
    {
        if (_runtimeSession == null)
            throw new InvalidOperationException("World not initialized");
        return _runtimeSession.Host;
    }

    private SimulationRuntimeHost<SimulationRuntimeSystems>? RuntimeHost => _runtimeSession?.Host;

    private static FortressGenerationContent CreateFortressGenerationContent(FortressRuntimeContentSnapshot content)
    {
        return new FortressGenerationContent(
            content.Geology,
            content.MapgenTuningJson,
            content.OreTuningJson,
            content.CavernTuningJson);
    }
}
