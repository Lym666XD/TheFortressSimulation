using HumanFortress.App;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Events;
using HumanFortress.Core.Random;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Simulation.World;
using HumanFortress.Navigation;
using Path = System.IO.Path;

namespace HumanFortress.App.GameStates;

/// <summary>
/// Manages game state transitions and owns core systems per GAME_ARCHITECTURE.md.
/// </summary>
public sealed class GameStateManager
{
    private static GameStateManager? _instance;
    public static GameStateManager Instance => _instance ?? throw new InvalidOperationException("GameStateManager not initialized");

    private readonly Dictionary<GameStateType, GameState> _states;
    private readonly TickScheduler _tickScheduler;
    private readonly CommandQueue _commandQueue;
    private readonly EventBus _eventBus;
    private readonly RngStreamManager _rngManager;
    private readonly DiffLog _diffLog;
    private readonly HumanFortress.Simulation.Items.ItemsDiffLog _itemsDiffLog;

    private GameState? _currentState;
    private World? _world;
    private SimulationContext? _simContext;
    private HumanFortress.Simulation.Orders.HaulingSystem? _haulingPlanner;
    private HumanFortress.App.Jobs.HaulJobSystem? _haulJobs;
    private HumanFortress.Simulation.Orders.MiningSystem? _miningPlanner;
    private HumanFortress.App.Jobs.MiningJobSystem? _miningJobs;
    private NavigationManager? _navManager;

    public GameStateManager(ulong masterSeed)
    {
        _instance = this;
        _states = new Dictionary<GameStateType, GameState>();
        _tickScheduler = new TickScheduler();
        _commandQueue = new CommandQueue();
        _eventBus = new EventBus();
        _rngManager = new RngStreamManager(masterSeed);
        _diffLog = new DiffLog();
        _itemsDiffLog = new HumanFortress.Simulation.Items.ItemsDiffLog();
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
    /// Simulation tick scheduler.
    /// </summary>
    public TickScheduler TickScheduler => _tickScheduler;

    public HumanFortress.Simulation.Orders.HaulingSystem? HaulingPlanner => _haulingPlanner;
    public HumanFortress.App.Jobs.HaulJobSystem? HaulJobs => _haulJobs;
    public HumanFortress.Simulation.Orders.MiningSystem? MiningPlanner => _miningPlanner;
    public HumanFortress.App.Jobs.MiningJobSystem? MiningJobs => _miningJobs;
    public NavigationManager? NavManager => _navManager;

    /// <summary>
    /// Enqueue a simulation command.
    /// </summary>
    public void EnqueueCommand(ICommand command)
    {
        _commandQueue.Enqueue(command);
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
                    _tickScheduler.Stop();
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
                StartSimulation();
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
        _world = new World(sizeInChunks, maxZ);
        _simContext = new SimulationContext(_diffLog, _world, _eventBus);
        // Initialize shared NavigationManager bound to this world
        _navManager = new NavigationManager(_world);

        // Load creature and item definitions
        // Try multiple possible paths for data files
        var baseDir = AppContext.BaseDirectory;
        string? dataPath = null;

        // Try path 1: published location (data/core in base directory)
        var path1 = Path.Combine(baseDir, "data", "core");
        if (Directory.Exists(path1))
        {
            dataPath = path1;
        }
        // Try path 2: development location (../../data/core relative to bin)
        else
        {
            var path2 = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "data", "core"));
            if (Directory.Exists(path2))
            {
                dataPath = path2;
            }
        }

        if (dataPath != null)
        {
            Logger.Log($"[GameStateManager] Loading creature and item definitions from {dataPath}");
            _world.Creatures.LoadDefinitions(dataPath);

            // Set ContentRegistry dependency for material validation
            _world.Items.SetDependencies(_world, HumanFortress.Core.Content.Registry.ContentRegistry.Instance);
            _world.Items.LoadDefinitions(dataPath);

            // Register zone definitions into ZoneManager
            var contentRegistry = HumanFortress.Core.Content.ContentRegistry.Instance;
            foreach (var zoneData in contentRegistry.Zones.Values)
            {
                _world.Zones.Manager.RegisterDefinition(zoneData);
            }

            Logger.Log($"[GameStateManager] Loaded {_world.Creatures.DefinitionCount} creatures, {_world.Items.DefinitionCount} items, {_world.Zones.Manager.GetAllDefinitions().Count()} zone definitions");
        }
        else
        {
            Logger.Log($"[GameStateManager] WARNING: Data directory not found. Tried:");
            Logger.Log($"  - {path1}");
            Logger.Log($"  - {Path.Combine(baseDir, "..", "..", "..", "..", "..", "data", "core")}");
        }
    }

    /// <summary>
    /// Update current state.
    /// </summary>
    public void Update(double deltaTime)
    {
        _currentState?.Update(deltaTime);

        // Process commands if simulation is running
        if (_tickScheduler.IsRunning && _simContext != null)
        {
            _commandQueue.ExecuteCommands(_tickScheduler.CurrentTick, _simContext);
        }
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

    private void StartSimulation()
    {
        if (_world == null)
            throw new InvalidOperationException("World not initialized");

        // Register systems with tick scheduler
        // Mining planner produces planned digs from mining designations
        _miningPlanner = new HumanFortress.Simulation.Orders.MiningSystem(_world, _world.Orders);
        _tickScheduler.RegisterSystem(_miningPlanner);

        // Mining job executor moves to adjacency and digs
        _miningJobs = new HumanFortress.App.Jobs.MiningJobSystem(_world, _miningPlanner, _diffLog, _itemsDiffLog, _navManager);
        _tickScheduler.RegisterSystem(_miningJobs);

        // Hauling planner produces planned moves from haul designations
        _haulingPlanner = new HumanFortress.Simulation.Orders.HaulingSystem(_world, _world.Orders);
        _tickScheduler.RegisterSystem(_haulingPlanner);

        // Haul job executor assigns creatures and moves items along paths
        _haulJobs = new HumanFortress.App.Jobs.HaulJobSystem(_world, _haulingPlanner, _diffLog, _navManager);
        _tickScheduler.RegisterSystem(_haulJobs);

        // Apply diffs after write phase (minimal: currently only used for auditing; runtime updates happen inline)
        _tickScheduler.PostTick += OnPostTickApplyDiffs;

        _tickScheduler.Start();
    }

    /// <summary>
    /// Simulation context implementation.
    /// </summary>
    private sealed class SimulationContext : ISimulationContext
    {
        private readonly DiffLog _diffLog;
        private readonly World _world;
        private readonly EventBus _eventBus;

        public SimulationContext(DiffLog diffLog, World world, EventBus eventBus)
        {
            _diffLog = diffLog;
            _world = world;
            _eventBus = eventBus;
        }

        public DiffLog DiffLog => _diffLog;
        public ulong CurrentTick => 0; // Not currently propagated by scheduler
        public IWorldReader World => _world;
        public IEventBus EventBus => _eventBus;
    }

    /// <summary>
    /// Shutdown and cleanup all systems before application exit.
    /// </summary>
    public void Shutdown()
    {
        Logger.Log("[GameStateManager] Shutdown requested");

        // Stop simulation if running
        if (_tickScheduler.IsRunning)
        {
            Logger.Log("[GameStateManager] Stopping tick scheduler");
            _tickScheduler.Stop();
        }

        // Exit current state
        if (_currentState != null)
        {
            Logger.Log($"[GameStateManager] Exiting current state: {_currentState.Type}");
            _currentState.Exit();
        }

        Logger.Log("[GameStateManager] Shutdown complete");
    }

    private void OnPostTickApplyDiffs(ulong tick)
    {
        // Merge and clear for next tick. In this phase, we could apply supported ops.
        var merged = _diffLog.MergeAndSort();
        HumanFortress.Simulation.Diff.SimulationDiffApplicator.ApplyAll(_world!, merged);
        _diffLog.Clear();

        // Apply Items diffs
        var items = _itemsDiffLog.MergeAndSort();
        HumanFortress.Simulation.Items.ItemsDiffApplicator.ApplyAll(_world!, items, tick);
        _itemsDiffLog.Clear();

        // Rebuild navigation for dirty chunks (after terrain changes)
        var dirtyChunks = _world!.GetAndClearDirtyChunks();
        if (dirtyChunks.Count > 0)
        {
            foreach (var ck in dirtyChunks)
            {
                var chunk = _world.GetChunk(ck);
                if (chunk != null)
                {
                    _navManager?.RebuildChunkNavData(chunk);
                }
            }
        }
    }
}
