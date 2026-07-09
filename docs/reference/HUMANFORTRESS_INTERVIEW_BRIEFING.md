# HumanFortress - Interview Briefing

Updated: 2026-06-13
Status: reference briefing, not the architecture source of truth

Use [../architecture/GAME_ARCHITECTURE.md](../architecture/GAME_ARCHITECTURE.md) for the current architecture map. This briefing is tuned for explaining the project quickly and may omit low-level migration details.

## A. Project Snapshot

**What it does:** A Dwarf Fortress-inspired colony simulation where players manage a settlement of creatures — issuing orders to mine, haul resources, build constructions, and craft items, while the world simulates autonomously at 50 ticks per second.

**Users:** Hobbyist gamers interested in deep simulation / colony management (personal project, not shipped).

**Stack:** C# / .NET 8, SadConsole v10 (ASCII terminal renderer) + MonoGame, data-driven JSON content pipeline.

**Architecture:** Multi-project layered simulation — `Core` (tick scheduler, command queue, DiffLog, RNG), `Contracts` (cross-module DTOs/interfaces), `Content` (JSON loading facade and definition loaders), `Runtime` (command stage, tick pipeline, status DTO, Simulation-backed navigation adapter/factory, session factory/host core), `Simulation` (creatures, items, orders, stockpiles, world/chunk model), `Jobs` (transport, mining, construction, and craft executor cores, plus craft planning), `Navigation` (deterministic A*), `WorldGen` (multi-stage procedural generation), and `App` (UI, job composition shells/adapters, game state machine, SadConsole rendering). Fixed 50 TPS loop uses deterministic read and serialized write phases; chunk-partitioned read parallelism remains a future scheduler target.

---

## B. Directory Structure

```
src/
  HumanFortress.App/          # Entry point, UI, game states, job composition
    Commands/                  # Player commands (mining, hauling, zones, construction)
    GameStates/                # State machine (GameStateManager, FortressPlayState)
    Input/                     # Input bindings, orders registry
    Jobs/                      # UnifiedJobsOrchestrator, job composition shells, App adapters
    Runtime/                   # App runtime composition, startup hooks, UI runtime facades
    States/                    # SadConsole screens (MainMenu, WorldGen, WorldMap, EmbarkPrep, Fortress)
    UI/                        # Rendering surfaces, overlays, debug UI
  HumanFortress.Content/       # Content loading facade and static definition loaders
  HumanFortress.Core/          # Engine fundamentals
    Commands/                  # CommandQueue, ICommand
    Content/                   # ContentRegistry, MaterialIdRegistry, FixedPoint
    Events/                    # EventBus
    Random/                    # DeterministicRng, RngStreamManager
    Simulation/                # DiffLog, UpdateOrder
    Time/                      # TickScheduler, ITick
  HumanFortress.Contracts/     # Cross-module DTOs and interfaces
    Navigation/                # Path requests/results, navigation source/view contracts
  HumanFortress.Runtime/       # Emerging runtime boundary
    SimulationCommandStage.cs   # Authoritative pre-read command execution stage
    SimulationStatus.cs         # Runtime clock/control status DTO
    SimulationTickPipeline.cs   # Runtime pre/post tick barriers and diff application ordering
    SimulationRuntimeContext.cs # Runtime command context and command target aggregation
    SimulationRuntimeHost.cs    # Generic runtime host over App-supplied concrete systems
    SimulationRuntimeHostCore.cs # Scheduler/pipeline lifecycle core
    SimulationRuntimeSession.cs # Immutable generic session handle
    SimulationRuntimeSessionFactory.cs # Generic new-session factory
    *CommandTarget.cs           # Runtime command target seams/helpers
    SimulationNavigation*.cs    # Simulation-backed navigation source/factory
  HumanFortress.Jobs/          # Emerging job-system layer
    Transport/                 # Transport executor core, state/backlog/finalization/stats/intake/assignment/replan/pickup/delivery/debug slices
    Mining/                    # Mining executor core, state/backlog/finalization/stats/intake/assignment/active-runner/debug slices
    Construction/              # Construction executor core, material tracking, safety relocation, completion, diff/log/UI seams
    Craft/                     # Craft planner/executor core, workshop/material/input/output/finalization/recipe seam slices
  HumanFortress.Simulation/    # Game logic layer
    Creatures/                 # CreatureManager, definitions, instances
    Items/                     # ItemManager, ItemsDiffLog
    Orders/                    # MiningSystem, HaulingSystem, ConstructionSystem
    Stockpile/                 # StockpileManager, filters, item projection, diff applicator
    World/                     # Chunk, World, ChunkLifecycleManager
    Zones/                     # ZoneManager, ZoneCoordinator
  HumanFortress.Navigation/    # Pathfinding
    DeterministicAStar.cs      # A* with 3D movement (stairs, ramps)
    NavigationManager.cs       # Path caching, request routing
  HumanFortress.WorldGen/      # Procedural world generation
    Stages/                    # Elevation, Climate, Biome pipeline
content/registries/            # JSON tuning data (materials, creatures, terrain, workshops)
data/core/                     # Creature/item/recipe/workshop definitions
configs/                       # Map and game config files
```

---

## C. Key Files

### 1. `src/HumanFortress.Core/Time/TickScheduler.cs`
**Why:** The heart of the engine — every system flows through this scheduler.

- Runs a fixed 50 TPS simulation loop on a dedicated background thread.
- Each tick has two phases: **Read** (systems run in deterministic registered-system order) then **Write** (serialized, one system at a time).
- A barrier between phases guarantees no system writes while others read.
- Speed multiplier (0.25x–8x) adjusts tick interval; falling 5+ ticks behind resets timing to prevent spiral.
- Systems register with a `Priority` and are sorted, so update order is deterministic.

```csharp
private void ExecuteTick()
{
    var tick = CurrentTick;
    var systems = GetSystemSnapshot();

    PreTick?.Invoke(tick);

    // Phase 1: Read in deterministic registered-system order.
    ExecuteReadPhase(tick, systems);

    // Barrier
    lock (_barrierLock) { BarrierReached?.Invoke(tick); }

    // Phase 2: Write (serialized)
    ExecuteWritePhase(tick, systems);

    PostTick?.Invoke(tick);
    AdvanceTick();
}

private void ExecuteReadPhase(ulong tick, IReadOnlyList<ITick> systems)
{
    foreach (var system in systems)
    {
        try { system.ReadTick(tick); }
        catch (Exception ex) { HandleSystemError(system, "Read", ex); }
    }
}

private void ExecuteWritePhase(ulong tick, IReadOnlyList<ITick> systems)
{
    foreach (var system in systems)
    {
        try { system.WriteTick(tick); }
        catch (Exception ex) { HandleSystemError(system, "Write", ex); }
    }
}
```

### 2. `src/HumanFortress.Navigation/DeterministicAStar.cs`
**Why:** Non-trivial algorithm work — 3D pathfinding with vertical movement and determinism guarantees.

- Standard A* with binary heap, extended for 3D (stairs up/down, ramp ascent/descent with bitmask directions).
- Enforces node expansion limit and per-tick time budget — returns partial paths if either is exceeded.
- Movement modes (Walk, Crawl, Swim, Fly) checked via `NavCapability` flags.
- Corner-cutting prevention for diagonal movement.
- Uses fixed-point cost scaling (10x) for finer granularity without floats.
- Path hash computed for determinism verification.

```csharp
while (_openSet.Count > 0)
{
    if (_nodesExpanded >= _tuning.MaxNodesPerSearch)
        return BuildPartialPath(request, world);
    if (_timer.ElapsedMilliseconds > _tuning.MaxMsPerTickPathing)
        return BuildPartialPath(request, world);

    var current = _openSet.Pop();
    var currentNode = _nodeMap[current.Key];

    if (currentNode.Position == request.Destination)
        return BuildCompletePath(request, current.Key, world);

    _closedSet.Add(current.Key);
    _nodesExpanded++;
    ExpandNeighbors(currentNode, request, world);
}
```

### 3. `src/HumanFortress.Core/Simulation/DiffLog.cs`
**Why:** Shows the determinism strategy — all world mutations go through a log that sorts and merges conflicts.

- All write operations produce `DiffOp` structs with a type, target (chunk + tile index), system ID, and priority.
- `MergeAndSort()` applies a deterministic sort key, then resolves conflicts: entity-scoped operations merge per entity, and tile operations merge by target/op with lower numeric priority winning; system precedence is Mining > Transport/Haul > Construction > default.
- Thread-safe via locks; cleared each tick after applying.

```csharp
public readonly struct DiffOp
{
    public readonly DiffOpType Op;
    public readonly DiffTarget Target;
    public readonly string SystemId;
    public readonly int Priority;
    // ...
    public readonly ulong SortKey =>
        ((ulong)(uint)Target.ChunkId << 32) |
        ((ulong)(ushort)Target.LocalIndex << 16) |
        ((ulong)(byte)Priority << 8) |
        StableSystemHash8(SystemId);
}
```

### 4. `src/HumanFortress.App/Jobs/UnifiedJobsOrchestrator.cs`
**Why:** Shows how multiple game systems coordinate — the job pipeline tying planning to execution.

- Single orchestrator implementing `ITick` — replaces per-system scheduling with one deterministic sequence.
- **ReadTick:** runs planners in fixed order (Mining, Hauling, Construction, Craft) and logs per-subsystem timings.
- **WriteTick:** flushes planner outputs, then runs executor Read+Write back-to-back for each job type.
- Hauling gets scheduling hints (reserve workers for mining if backlog is high, cap intake when overloaded).
- Captures detailed stats per tick for debug UI.

```csharp
public void WriteTick(ulong tick)
{
    // Flush planners
    _miningPlanner.WriteTick(tick);
    _haulPlanner.WriteTick(tick);
    // ...

    // Hauling scheduling hints based on mining backlog
    int miningBacklog = _miningJobs.GetBacklogCount();
    if (hLimits.ReserveForMining > 0 && miningBacklog >= hLimits.ReserveBacklogThreshold)
        reserve = hLimits.ReserveForMining;

    // Run executors: Read then Write for each job type
    _haulJobs.ReadTick(tick);
    _haulJobs.WriteTick(tick);
    // ... mining, construction, craft follow
}
```

### 5. `src/HumanFortress.App/GameStates/GameStateManager.cs`
**Why:** The app state manager and current top-level runtime lifetime owner.

- Owns app state transitions and shared scheduler/control primitives: TickScheduler, CommandQueue, EventBus, RngStreamManager, DiffLog.
- Holds one `SimulationRuntimeSession<SimulationRuntimeHost<SimulationRuntimeSystems>>` for active fortress play instead of separate World/Navigation/RuntimeHost fields.
- Delegates world reset, navigation creation, queue/diff cleanup, content-loading callback invocation, host creation, and generic startup orchestration to Runtime-owned factories/helpers.
- Starts/stops the runtime host when entering/leaving FortressPlay.

### 6. `src/HumanFortress.Runtime/SimulationRuntimeSessionFactory.cs`
**Why:** The first generic Runtime-owned new-session factory seam.

- Creates the authoritative `World` for a new fortress session.
- Resets scheduler/session queues and diff logs for a clean run.
- Creates the `NavigationManager` bound to that world.
- Calls App-supplied callbacks for core creature/item/zone/workshop/recipe content loading and Runtime-owned `SimulationRuntimeHost<SimulationRuntimeSystems>` creation. The generic host delegates scheduler/pipeline lifecycle to Runtime-owned `SimulationRuntimeHostCore`; App supplies logging/content inputs rather than owning the host factory source.

### 7. `src/HumanFortress.Runtime/SimulationCommandStage.cs`
**Why:** The simulation command boundary.

- Runs queued `ICommand` instances from the tick pipeline's pre-read boundary.
- Sets the runtime command context's current tick before command execution.
- Keeps UI/App input paths enqueue-only: commands are applied by the simulation thread before systems read.
- Profession allocation changes now use `SetProfessionWeightCommand` and `ProfessionAssignmentDiffLog` instead of directly mutating `ProfessionAssignments` from UI/game-state code or command execution.
- Debug item spawning now emits `ItemsDiffLog.AddItem` through `IItemSpawnCommandTarget`, then the post-tick item applicator creates the item.
- Debug creature spawning now emits a spawn-only `CreaturesDiffLog` operation through `ICreatureSpawnCommandTarget`, then the post-tick creature applicator creates the creature.
- Mining, haul, structural construction, and buildable construction order commands now enqueue through `IOrderCommandTarget` into `OrderDiffLog`, so command implementations no longer cast `context.World` to the concrete Simulation world and order manager mutation happens post-tick.
- Runtime command-target behavior now lives in focused helpers for item spawning, creature spawning, order diffs, workshop queue/settings diffs, zone mutation diffs, and stockpile create/delete commands. `SimulationRuntimeContext` is Runtime-owned and remains the transitional adapter that implements the interfaces and delegates to those helpers.
- Profession assignment is bridged through an injected Runtime callback, so Runtime does not reference App `ProfessionAssignments`.
- Zone create/update/delete commands now route through `IZoneCommandTarget` into `ZoneDiffLog`, preserving `ZoneCoordinator` cell-shard behavior while moving authoritative mutation to the post-tick zone applicator.
- Workshop queue update commands now route through `IWorkshopQueueCommandTarget` into `WorkshopDiffLog`, keeping recipe validation in Runtime while moving placeable lookup, worker-slot initialization, and queue/settings mutation to the post-tick workshop applicator.
- Stockpile create/delete commands now route through `IStockpileCommandTarget`, with authoritative global-zone/shard mutation applied by the post-tick `StockpileDiffApplicator`.

---

## D. Things I Actually Did Here

1. **Designed the deterministic read / write-serialized tick loop** — `TickScheduler.cs` runs all systems' ReadTick in stable registered-system order, then WriteTick sequentially, enforcing determinism with a barrier. Chunk-partitioned read parallelism is a future scheduler target.

2. **Implemented deterministic A* pathfinding with 3D vertical movement** — `DeterministicAStar.cs` handles stairs, ramps (with directional bitmasks), diagonal corner-cutting prevention, and partial path fallback under time/node budgets.

3. **Built the DiffLog conflict resolution system** — `DiffLog.cs` collects mutation ops, sorts by a stable composite key, and resolves conflicts with explicit precedence rules (Mining > Transport/Haul > Construction > default).

4. **Created the UnifiedJobsOrchestrator** — coordinates four job subsystems (mining, hauling, construction, crafting) in a single deterministic pipeline with adaptive scheduling hints (reserves workers for high-priority backlogs).

5. **Implemented a data-driven content loading path** — `FortressContentLoader` loads/validates JSON-backed creature, item, recipe, construction, tuning, geology, and zone data; Runtime's `SimulationWorldContentLoader` applies those snapshots to the active world managers and runtime composition.

6. **Built a multi-stage procedural world generator** — `WorldGen/Stages/` pipeline (Elevation -> Climate -> Biome) produces the overworld map; `FortressGenerator` carves the detailed local map with geology and ore placement.

---

## E. Three Likely Interview Questions

1. **"How do you keep the simulation deterministic when systems run in parallel?"**
   Hint: `TickScheduler` runs parallel `ReadTick`, a barrier, then serialized `WriteTick`; mutations should flow through diff logs or runtime command targets. RNG uses named streams (`RngStreamManager`) so each system gets reproducible sequences.

2. **"Walk me through what happens when a player issues a mining order."**
   Hint: Player command -> `CreateMiningOrderCommand` enqueued in `CommandQueue` -> Runtime command stage executes it before read systems -> `MiningSystem.ReadTick` observes the order and creates designations -> `MiningJobSystem` assigns idle creatures, pathfinds via `DeterministicAStar`, stages dig progress over ticks -> on completion, emits `SetTerrain` DiffOp and drops items.

3. **"Your A* handles stairs and ramps — how does vertical movement work?"**
   Hint: `DeterministicAStar.CheckVerticalNeighbors()` adds stair neighbors one Z-level up/down. Ramps use `TryGetUpRampMask`, so a ramp can connect only through allowed directional mask bits; descent checks the reverse direction.

---

## F. Honest Self-Check

**Potential weak spots an interviewer might notice:**

- **Hardcoded master seed** (`Program.OnGameStarted`): `ulong masterSeed = 12345; // TODO: Make configurable`. `--init-only` and the crash-test helper also use fixed seeds.
- **About 33 TODO/FIXME markers** remain in code — stockpile integration, item lookup, HP calculation, quarantine logic, dirty tile sets, render-snapshot seed capture, and some UI settings placeholders. This is normal for an in-progress personal project, but don't claim these systems are "complete."
- **Creature spawning is only partially diff-backed** — `SpawnCreatureCommand` now uses a spawn-only creature diff, but `CreatureManager.SpawnCreature` remains the low-level applicator write and this is not yet a full creature mutation pipeline.
- **Order commands use a runtime seam, not an order diff log** — mining/haul/construction commands no longer touch concrete `World` directly, but they still enqueue into `OrdersManager` rather than emitting a formal order diff.
- **SimulationRuntimeContext still exposes transitional target interfaces** — concrete behavior now lives in Runtime helper classes, but the next architecture step is reducing the context's broad interface surface.
- **Stockpile is mid-refactor, not finished gameplay** — create/delete, preset filters, item projection, item-index updates, and planner reservations now use typed diffs through Runtime/Jobs/Simulation seams. Remaining work is richer long-horizon reservation and stockpile maintenance behavior, not App-owned mutation.
- **Test suite is early**: focused regression/smoke coverage and former Phase A-D validation now live in `tests/HumanFortress.App.Tests`, but it is still a lightweight runner rather than mature module-level CI. Don't claim full TDD yet.
- **Content compatibility naming is mostly outside Content now**: startup uses `FortressContentLoader` and `RuntimeContentRegistryLoader` to load the structured registry only. The structured registry implementation compiles from `HumanFortress.Content.Registry`; registry contracts compile from `HumanFortress.Contracts.Content.Registry`; runtime geology/zone DTOs compile from `HumanFortress.Contracts.Content`. Navigation contracts still use a transitional namespace even though they compile from `HumanFortress.Contracts`.
- **Diagnostics migration is partial**: the App logger is now async and category-routed, while content registries, WorldGen, stockpile diff errors, and key Simulation orders/jobs paths use diagnostic bridges. Test/CLI output and no-logger fallbacks still print to console. Don't claim logging is fully production-grade yet.
- **UI is not fully presentation-pure yet**: active map/drawer/debug/placement/workshop paths now read Runtime-owned snapshot DTOs instead of live `World`, concrete job systems, or the legacy App `RenderSnapshotBuilder` bridge. The remaining caution is that App still owns sizeable UI state/input orchestration and calls runtime facades for read/query and command enqueue operations.

**Things that are solid and safe to highlight:**
- The tick scheduler / read-write phase separation is clean and well-structured.
- The A* implementation is genuinely non-trivial (3D, partial paths, deterministic).
- The DiffLog merge strategy is a real design decision with clear conflict resolution rules.
- The job orchestrator shows systems-level thinking about scheduling and resource contention.
- The multi-project separation (Core/Contracts/Simulation/Jobs/Navigation/WorldGen/App) is a mature architectural choice for a personal project.
