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
    private HumanFortress.Simulation.Jobs.ITransportRequestQueue? _transportQueue;
    private HumanFortress.App.Jobs.TransportJobSystem? _transportJobs;
    private HumanFortress.Simulation.Orders.MiningSystem? _miningPlanner;
    private HumanFortress.Simulation.Orders.BuildableConstructionSystem? _buildablePlanner;
    private HumanFortress.Simulation.Jobs.ConstructionMaterialsPlanner? _cmPlanner;
    private HumanFortress.App.Jobs.MiningJobSystem? _miningJobs;
    private HumanFortress.Simulation.Orders.ConstructionSystem? _constructionPlanner;
    private HumanFortress.App.Jobs.ConstructionJobSystem? _constructionJobs;
    private NavigationManager? _navManager;
    private HumanFortress.App.Jobs.UnifiedJobsOrchestrator? _jobsOrchestrator;
    private HumanFortress.App.Jobs.SchedulerTunings? _schedulerTunings;

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
    public HumanFortress.Simulation.Jobs.ITransportRequestQueue? TransportQueue => _transportQueue;
    public HumanFortress.App.Jobs.TransportJobSystem? TransportJobs => _transportJobs;
    public HumanFortress.Simulation.Orders.MiningSystem? MiningPlanner => _miningPlanner;
    public HumanFortress.App.Jobs.MiningJobSystem? MiningJobs => _miningJobs;
    public HumanFortress.Simulation.Orders.ConstructionSystem? ConstructionPlanner => _constructionPlanner;
    public HumanFortress.App.Jobs.ConstructionJobSystem? ConstructionJobs => _constructionJobs;
    public NavigationManager? NavManager => _navManager;
    public HumanFortress.App.Jobs.UnifiedJobsOrchestrator? JobsOrchestrator => _jobsOrchestrator;

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

            // Load buildable constructions (workshops, etc.) into ConstructionRegistry (App-layer pass per CONTENT_REGISTRY_OVERVIEW.md)
            try
            {
                LoadBuildableConstructions(Path.Combine(dataPath, "workshops"));
            }
            catch (Exception ex)
            {
                Logger.Log($"[CONSTR.REG] ERROR: failed loading constructions: {ex.Message}");
            }
        }
        else
        {
            Logger.Log($"[GameStateManager] WARNING: Data directory not found. Tried:");
            Logger.Log($"  - {path1}");
            Logger.Log($"  - {Path.Combine(baseDir, "..", "..", "..", "..", "..", "data", "core")}");
        }
    }

    /// <summary>
    /// App-layer loader for buildable constructions (workshops etc.).
    /// Reads data/core/workshops/core_workshop_*.json files and publishes into ConstructionRegistry.
    /// </summary>
    private static void LoadBuildableConstructions(string workshopsDir)
    {
        if (!Directory.Exists(workshopsDir))
        {
            Logger.Log($"[CONSTR.REG] workshops dir not found: {workshopsDir}");
            return;
        }

        var files = new List<string>();
        // Load all core_workshop_*.json files
        foreach (var f in Directory.GetFiles(workshopsDir, "core_workshop_*.json", SearchOption.TopDirectoryOnly))
        {
            files.Add(f);
        }

        // Fallback: also check for legacy workshops.json in parent placeable directory (for backward compatibility)
        var legacyPath = Path.Combine(Path.GetDirectoryName(workshopsDir) ?? "", "placeable", "workshops.json");
        if (File.Exists(legacyPath))
        {
            files.Add(legacyPath);
            Logger.Log($"[CONSTR.REG] loading legacy workshops.json from placeable dir");
        }

        var defs = new List<HumanFortress.Core.Content.Registry.ConstructionDefinition>();
        int errors = 0;
        foreach (var file in files)
        {
            try
            {
                foreach (var d in ParseConstructionsFile(file))
                {
                    // Only accept entries that have a placeable_profile (L2) for this iteration
                    if (d.PlaceableProfile != null && d.PlaceableProfile.Footprint.W > 0 && d.PlaceableProfile.Footprint.D > 0)
                    {
                        defs.Add(d);
                    }
                }
            }
            catch (Exception ex)
            {
                errors++;
                Logger.Log($"[CONSTR.REG] error parsing {Path.GetFileName(file)}: {ex.Message}");
            }
        }

        // Publish to registry
        var reg = HumanFortress.Core.Content.Registry.ConstructionRegistry.Instance;
        reg.Clear();
        try
        {
            reg.LoadConstructions(defs);
        }
        catch (Exception ex)
        {
            errors++;
            Logger.Log($"[CONSTR.REG] load error: {ex.Message}");
        }

        var cats = string.Join(',', reg.GetAllCategories());
        Logger.Log($"[CONSTR.REG] loaded={reg.Count} categories=[{cats}] errors={errors}");
    }

    private static IEnumerable<HumanFortress.Core.Content.Registry.ConstructionDefinition> ParseConstructionsFile(string file)
    {
        using var fs = File.OpenRead(file);
        using var doc = System.Text.Json.JsonDocument.Parse(fs);
        var root = doc.RootElement;

        // Support both "constructions" and "workshops" arrays
        System.Text.Json.JsonElement arr;
        bool isWorkshopFile = false;
        if (root.TryGetProperty("workshops", out arr) && arr.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            isWorkshopFile = true;
        }
        else if (root.TryGetProperty("constructions", out arr) && arr.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            isWorkshopFile = false;
        }
        else
        {
            yield break;
        }

        // Parse attachments array if present (for workshop files)
        HumanFortress.Core.Content.Registry.WorkshopAttachment[]? attachments = null;
        if (isWorkshopFile && root.TryGetProperty("attachments", out var attachArr) && attachArr.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            attachments = ParseAttachments(attachArr);
        }

        foreach (var elem in arr.EnumerateArray())
        {
            // We accept two shapes:
            // A) L2: has "placeable_profile": {...}
            // B) Legacy: top-level footprint/passability (ignored here unless placeable_profile present)
            var hasProfile = elem.TryGetProperty("placeable_profile", out var profileElem) && profileElem.ValueKind == System.Text.Json.JsonValueKind.Object;
            if (!hasProfile)
            {
                // Skip legacy L0 definitions in this pass
                continue;
            }

            var def = new HumanFortress.Core.Content.Registry.ConstructionDefinition();
            def.Id = elem.GetProperty("id").GetString() ?? string.Empty;
            def.Name = elem.TryGetProperty("name", out var nameE) ? (nameE.GetString() ?? def.Id) : def.Id;
            def.Category = elem.TryGetProperty("category", out var catE) ? (catE.GetString() ?? "") : "";
            def.BuildTimeTicks = elem.TryGetProperty("build_time_ticks", out var btE) ? btE.GetInt32() : 1000;

            // Materials: prefer material_costs; fallback to materials_required
            var mats = new List<HumanFortress.Core.Content.Registry.MaterialCost>();
            if (elem.TryGetProperty("material_costs", out var mcE) && mcE.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var m in mcE.EnumerateArray())
                {
                    var mc = new HumanFortress.Core.Content.Registry.MaterialCost
                    {
                        Tag = m.TryGetProperty("tag", out var tagE) ? tagE.GetString() : null,
                        DefId = m.TryGetProperty("def_id", out var didE) ? didE.GetString() : (m.TryGetProperty("defId", out var did2E) ? did2E.GetString() : null),
                        Count = m.TryGetProperty("count", out var cntE) ? cntE.GetInt32() : 1
                    };
                    mats.Add(mc);
                }
            }
            else if (elem.TryGetProperty("materials_required", out var mrE) && mrE.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var m in mrE.EnumerateArray())
                {
                    var mc = new HumanFortress.Core.Content.Registry.MaterialCost
                    {
                        Tag = m.TryGetProperty("tag", out var tagE) ? tagE.GetString() : null,
                        DefId = m.TryGetProperty("def_id", out var didE) ? didE.GetString() : (m.TryGetProperty("defId", out var did2E) ? did2E.GetString() : null),
                        Count = m.TryGetProperty("count", out var cntE) ? cntE.GetInt32() : 1
                    };
                    mats.Add(mc);
                }
            }
            // Use materials as defined in JSON (pure data-driven)
            def.MaterialCosts = mats.ToArray();

            // Placeable profile
            var pp = new HumanFortress.Core.Content.Registry.PlaceableProfile();
            var fpE = profileElem.GetProperty("footprint");
            var fp = new HumanFortress.Core.Content.Registry.Footprint(
                w: fpE.GetProperty("w").GetInt32(),
                d: fpE.GetProperty("d").GetInt32(),
                h: fpE.TryGetProperty("h", out var hE) ? hE.GetInt32() : 1);
            pp.Footprint = fp;
            // passability: string -> enum
            if (profileElem.TryGetProperty("passability", out var passE))
            {
                var s = (passE.GetString() ?? "nonblocking").Trim().ToLowerInvariant();
                pp.Passability = s switch
                {
                    "blocking" => HumanFortress.Core.Content.Registry.PassabilityMode.Blocking,
                    "doorway" => HumanFortress.Core.Content.Registry.PassabilityMode.Doorway,
                    _ => HumanFortress.Core.Content.Registry.PassabilityMode.Nonblocking
                };
            }
            pp.RequiresFloor = profileElem.TryGetProperty("requires_floor", out var rfE) && rfE.GetBoolean();
            pp.ClearanceH = profileElem.TryGetProperty("clearance_h", out var clE) ? clE.GetInt32() : 0;
            pp.BlocksLight = profileElem.TryGetProperty("blocks_light", out var blE) && blE.GetBoolean();

            if (profileElem.TryGetProperty("tags", out var tagsE) && tagsE.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                pp.Tags = tagsE.EnumerateArray().Select(t => t.GetString() ?? string.Empty).Where(t => !string.IsNullOrWhiteSpace(t)).ToArray();
            }

            var eff = new HumanFortress.Core.Content.Registry.EffectsBlock();
            if (profileElem.TryGetProperty("effects", out var effE) && effE.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                eff.Beauty = effE.TryGetProperty("beauty", out var bE) ? bE.GetInt32() : 0;
                eff.Comfort = effE.TryGetProperty("comfort", out var cE) ? cE.GetInt32() : 0;
                eff.LightLumen = effE.TryGetProperty("light_lumen", out var lE) ? lE.GetInt32() : 0;
                eff.HeatW = effE.TryGetProperty("heat_w", out var h2E) ? h2E.GetInt32() : 0;
            }
            pp.Effects = eff;
            def.PlaceableProfile = pp;

            // Parse workshop-specific fields (optional)
            if (elem.TryGetProperty("io", out var ioE) && ioE.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                def.Io = new HumanFortress.Core.Content.Registry.WorkshopIo
                {
                    InputSlots = ioE.TryGetProperty("input_slots", out var inE) ? inE.GetInt32() : 4,
                    OutputSlots = ioE.TryGetProperty("output_slots", out var outE) ? outE.GetInt32() : 4,
                    BufferSlots = ioE.TryGetProperty("buffer_slots", out var bufE) ? bufE.GetInt32() : 2
                };
            }

            if (elem.TryGetProperty("attachment_slots", out var slotsE) && slotsE.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                def.AttachmentSlots = slotsE.EnumerateArray()
                    .Select(s => s.GetString() ?? "")
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToArray();
            }

            if (elem.TryGetProperty("power_baseline_w", out var pwrE))
            {
                def.PowerBaselineW = pwrE.GetInt32();
            }

            if (elem.TryGetProperty("era_min", out var eMinE))
            {
                def.EraMin = eMinE.GetString();
            }

            if (elem.TryGetProperty("era_max", out var eMaxE))
            {
                def.EraMax = eMaxE.GetString();
            }

            // Attach the attachments array from file level (for workshop files)
            if (attachments != null && attachments.Length > 0)
            {
                def.Attachments = attachments;
            }

            // Validate now to surface file-specific errors
            def.Validate();
            yield return def;
        }
    }

    private static HumanFortress.Core.Content.Registry.WorkshopAttachment[] ParseAttachments(System.Text.Json.JsonElement attachArr)
    {
        var list = new List<HumanFortress.Core.Content.Registry.WorkshopAttachment>();
        foreach (var elem in attachArr.EnumerateArray())
        {
            var att = new HumanFortress.Core.Content.Registry.WorkshopAttachment
            {
                Id = elem.TryGetProperty("id", out var idE) ? (idE.GetString() ?? "") : "",
                Name = elem.TryGetProperty("name", out var nameE) ? (nameE.GetString() ?? "") : "",
                Slot = elem.TryGetProperty("slot", out var slotE) ? (slotE.GetString() ?? "") : "",
                Era = elem.TryGetProperty("era", out var eraE) ? eraE.GetString() : null,
                EraMin = elem.TryGetProperty("era_min", out var eMinE) ? eMinE.GetString() : null,
                EraMax = elem.TryGetProperty("era_max", out var eMaxE) ? eMaxE.GetString() : null,
                UpgradeTo = elem.TryGetProperty("upgrade_to", out var upgE) ? upgE.GetString() : null,
                PowerW = elem.TryGetProperty("power_w", out var pwrE) ? pwrE.GetInt32() : 0
            };

            if (elem.TryGetProperty("tags", out var tagsE) && tagsE.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                att.Tags = tagsE.EnumerateArray()
                    .Select(t => t.GetString() ?? "")
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToArray();
            }

            list.Add(att);
        }
        return list.ToArray();
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

        // Instantiate planners (not registered directly)
        _miningPlanner = new HumanFortress.Simulation.Orders.MiningSystem(_world, _world.Orders);
        _transportQueue = new HumanFortress.Simulation.Jobs.TransportRequestQueue();
        _haulingPlanner = new HumanFortress.Simulation.Orders.HaulingSystem(_world, _world.Orders, transportIntake: _transportQueue);
        _cmPlanner = new HumanFortress.Simulation.Jobs.ConstructionMaterialsPlanner(_world, _transportQueue);
        _constructionPlanner = new HumanFortress.Simulation.Orders.ConstructionSystem(_world, _world.Orders);
        _buildablePlanner = new HumanFortress.Simulation.Orders.BuildableConstructionSystem(_world, _world.Orders);

        // Load scheduler tunings (fallback to defaults when missing)
        var baseDir = AppContext.BaseDirectory;
        _schedulerTunings = HumanFortress.App.Jobs.SchedulerTunings.LoadFromContent(baseDir);

        // Executors (honor per-tick intake budgets & backpressure window)
        _miningJobs = new HumanFortress.App.Jobs.MiningJobSystem(
            _world, _miningPlanner, _diffLog, _itemsDiffLog, _navManager,
            intakeBudget: _schedulerTunings.Mining.PlanPerTick,
            carryoverMaxTicks: _schedulerTunings.BackpressureMaxCarryoverTicks);
        _transportJobs = new HumanFortress.App.Jobs.TransportJobSystem(
            _world, _transportQueue!, _diffLog, _navManager,
            intakeBudget: _schedulerTunings.Hauling.PlanPerTick,
            carryoverMaxTicks: _schedulerTunings.BackpressureMaxCarryoverTicks);
        _constructionJobs = new HumanFortress.App.Jobs.ConstructionJobSystem(
            _world, _constructionPlanner, _diffLog,
            maxPerTick: _schedulerTunings.Construction.PlanPerTick);

        // Register sanitizer (low-frequency safety net)
        var sanitizer = new HumanFortress.App.Jobs.SanitizeSystem(_world, intervalTicks: 40, maxPerTick: 8);

        // Register unified jobs orchestrator (v1 single-threaded orchestration)
        _jobsOrchestrator = new HumanFortress.App.Jobs.UnifiedJobsOrchestrator(
            _haulingPlanner,
            _cmPlanner,
            _miningPlanner,
            _constructionPlanner,
            _transportJobs,
            _miningJobs,
            _constructionJobs,
            _schedulerTunings
        );
        // Buildable planner is independent (read-only, places sites). Run before orchestrator writes.
        _tickScheduler.RegisterSystem(_buildablePlanner);
        _tickScheduler.RegisterSystem(_jobsOrchestrator);
        _tickScheduler.RegisterSystem(sanitizer);

        // Apply diffs after write phase (minimal: currently only used for auditing; runtime updates happen inline)
        _tickScheduler.PostTick += OnPostTickApplyDiffs;

        // Ensure we have initial workers to execute jobs (spawn dwarves on nearby standable tiles)
        TrySpawnInitialWorkers();

        // Optional self-test: enqueue a mining order automatically for reproducible logs
        if (Program.AutoDig)
        {
            try
            {
                SelfTestAutoDig();
            }
            catch (Exception ex)
            {
                Logger.Log($"[AUTO-DIG] ERROR: {ex.Message}");
            }
        }

        _tickScheduler.Start();
    }

    /// <summary>
    /// Self-test helper: find a SolidWall near world center at a mid Z and enqueue a 1x1 Dig order.
    /// Logs in the same format as UI direct enqueue for comparison.
    /// </summary>
    private void SelfTestAutoDig()
    {
        if (_world == null) return;
        int tiles = _world.SizeInTiles;
        int cx = tiles / 2;
        int cy = tiles / 2;

        int zMid = Math.Max(0, Math.Min(_world.MaxZ - 1, _world.MaxZ / 2));
        int zMin = 0;
        int zMax = Math.Max(0, _world.MaxZ - 1);

        int? foundX = null, foundY = null, foundZ = null;
        // First, search an expanding ring around center across all Z
        for (int z = zMin; z <= zMax && foundX == null; z++)
        {
            for (int r = 0; r <= Math.Max(cx, cy) && foundX == null; r++)
            {
                for (int dx = -r; dx <= r && foundX == null; dx++)
                {
                    int dy1 = r - Math.Abs(dx);
                    foreach (int dy in new int[] { -dy1, dy1 })
                    {
                        int x = cx + dx;
                        int y = cy + dy;
                        if (x < 0 || y < 0 || x >= tiles || y >= tiles) continue;
                        var t = _world.GetTile(x, y, z);
                        if (t == null) continue;
                        // Prefer SolidWall (Dig action accepts SolidWall and Ramp)
                        if (t.Value.Kind == HumanFortress.Simulation.Tiles.TerrainKind.SolidWall)
                        {
                            foundX = x; foundY = y; foundZ = z;
                            break;
                        }
                        // Fallback: allow Ramp as a dig target as well
                        if (t.Value.Kind == HumanFortress.Simulation.Tiles.TerrainKind.Ramp)
                        {
                            foundX = x; foundY = y; foundZ = z;
                            break;
                        }
                    }
                }
            }
        }

        // If not found near center, fall back to exhaustive scan
        if (foundX == null)
        {
            for (int z = zMin; z <= zMax && foundX == null; z++)
            {
                for (int y = 0; y < tiles && foundX == null; y++)
                {
                    for (int x = 0; x < tiles && foundX == null; x++)
                    {
                        var t = _world.GetTile(x, y, z);
                        if (t == null) continue;
                        var k = t.Value.Kind;
                        if (k == HumanFortress.Simulation.Tiles.TerrainKind.SolidWall || k == HumanFortress.Simulation.Tiles.TerrainKind.Ramp)
                        {
                            foundX = x; foundY = y; foundZ = z;
                            break;
                        }
                    }
                }
            }
        }

        if (foundX == null)
        {
            Logger.Log("[AUTO-DIG] No SolidWall or Ramp found anywhere; skip.");
            return;
        }

        var rect = new SadRogue.Primitives.Rectangle(foundX.Value, foundY.Value, 1, 1);
        int z0 = foundZ!.Value;
        // Emit the same UI log format for consistency
        Logger.Log($"[DEBUG] Creating mining order (direct enqueue) zMin={z0} zMax={z0} rect=({rect.X},{rect.Y},{rect.Width}x{rect.Height})");
        _world.Orders.EnqueueMiningAdvanced(
            rect,
            z0,
            z0,
            HumanFortress.Simulation.Orders.MiningAction.Dig,
            priority: 50,
            createdTick: _tickScheduler.CurrentTick
        );
        Logger.Log($"[AUTO-DIG] Enqueued test Dig at ({rect.X},{rect.Y},{z0})");
    }

    /// <summary>
    /// Spawn a minimal set of initial workers if none exist, so mining/hauling can proceed.
    /// Looks for OpenWithFloor tiles near world center across a small radius and surface-ish Z range.
    /// </summary>
    private void TrySpawnInitialWorkers(int desired = 5)
    {
        if (_world == null) return;
        if (_world.Creatures.InstanceCount > 0) return;

        try
        {
            int tiles = _world.SizeInTiles;
            int cx = tiles / 2;
            int cy = tiles / 2;

            // Heuristic Z window around mid-depth (surface-ish)
            int zMid = Math.Max(0, Math.Min(_world.MaxZ - 1, _world.MaxZ / 2));
            int zMin = Math.Max(0, zMid - 5);
            int zMax = Math.Min(_world.MaxZ - 1, zMid + 5);

            int spawned = 0;
            int radiusMax = Math.Max(4, tiles / 8);
            for (int r = 0; r <= radiusMax && spawned < desired; r++)
            {
                for (int z = zMin; z <= zMax && spawned < desired; z++)
                {
                    // Scan a diamond ring at radius r around (cx,cy)
                    for (int dx = -r; dx <= r && spawned < desired; dx++)
                    {
                        int dy1 = r - Math.Abs(dx);
                        foreach (int dy in new int[] { -dy1, dy1 })
                        {
                            int wx = cx + dx;
                            int wy = cy + dy;
                            if (wx < 0 || wy < 0 || wx >= tiles || wy >= tiles) continue;
                            var t = _world.GetTile(wx, wy, z);
                            if (t == null) continue;
                            // Prefer standable floors; fallback to any walkable (ramp/slope/stairs) if floors are scarce
                            if (!(t.Value.IsStandable || t.Value.IsWalkable)) continue;

                            var guid = _world.Creatures.SpawnCreature("core_race_dwarf", new SadRogue.Primitives.Point(wx, wy), z, "player", 0);
                            if (guid.HasValue)
                            {
                                spawned++;
                                if (spawned >= desired) break;
                            }
                        }
                    }
                }
            }

            // Fallback: exhaustive scan for any standable tile across the map
            if (spawned < desired)
            {
                for (int z = 0; z < _world.MaxZ && spawned < desired; z++)
                {
                    for (int wy = 0; wy < tiles && spawned < desired; wy++)
                    {
                        for (int wx = 0; wx < tiles && spawned < desired; wx++)
                        {
                            var t = _world.GetTile(wx, wy, z);
                            if (t == null || !(t.Value.IsStandable || t.Value.IsWalkable)) continue;
                            var guid = _world.Creatures.SpawnCreature("core_race_dwarf", new SadRogue.Primitives.Point(wx, wy), z, "player", 0);
                            if (guid.HasValue) spawned++;
                        }
                    }
                }
            }

            Logger.Log($"[SIM] Initial workers spawned: {spawned}");
        }
        catch (Exception ex)
        {
            Logger.Log($"[SIM] Spawn initial workers failed: {ex.Message}");
        }
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
