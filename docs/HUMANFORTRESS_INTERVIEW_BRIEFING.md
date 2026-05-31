# HumanFortress - Interview Briefing

## A. Project Snapshot

**What it does:** A Dwarf Fortress-inspired colony simulation where players manage a settlement of creatures — issuing orders to mine, haul resources, build constructions, and craft items, while the world simulates autonomously at 50 ticks per second.

**Users:** Hobbyist gamers interested in deep simulation / colony management (personal project, not shipped).

**Stack:** C# / .NET 8, SadConsole v10 (ASCII terminal renderer) + MonoGame, data-driven JSON content pipeline.

**Architecture:** Multi-project layered simulation — `Core` (tick scheduler, command queue, DiffLog, RNG), `Simulation` (creatures, items, orders, stockpiles, world/chunk model), `Navigation` (deterministic A*), `WorldGen` (multi-stage procedural generation), and `App` (UI, job systems, game state machine, SadConsole rendering). Fixed 50 TPS loop uses read-parallel / write-serialized phases for determinism.

---

## B. Directory Structure

```
src/
  HumanFortress.App/          # Entry point, UI, game states, job systems
    Commands/                  # Player commands (mining, hauling, zones, construction)
    GameStates/                # State machine (GameStateManager, FortressPlayState)
    Input/                     # Input bindings, orders registry
    Jobs/                      # UnifiedJobsOrchestrator, Mining/Transport/Construction/CraftJobSystem
    States/                    # SadConsole screens (MainMenu, WorldGen, WorldMap, EmbarkPrep, Fortress)
    UI/                        # Rendering surfaces, overlays, debug UI
  HumanFortress.Core/          # Engine fundamentals
    Commands/                  # CommandQueue, ICommand
    Content/                   # ContentRegistry, MaterialIdRegistry, FixedPoint
    Events/                    # EventBus
    Random/                    # DeterministicRng, RngStreamManager
    Simulation/                # DiffLog, UpdateOrder
    Time/                      # TickScheduler, ITick
  HumanFortress.Simulation/    # Game logic layer
    Creatures/                 # CreatureManager, definitions, instances
    Items/                     # ItemManager, ItemsDiffLog
    Orders/                    # MiningSystem, HaulingSystem, ConstructionSystem
    Stockpile/                 # StockpileManager, filters, hauling broker
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
- Each tick has two phases: **Read** (systems run in parallel via `Parallel.ForEach`) then **Write** (serialized, one system at a time).
- A barrier between phases guarantees no system writes while others read.
- Speed multiplier (0.25x–8x) adjusts tick interval; falling 5+ ticks behind resets timing to prevent spiral.
- Systems register with a `Priority` and are sorted, so update order is deterministic.

```csharp
private void ExecuteTick()
{
    var tick = _currentTick;
    PreTick?.Invoke(tick);

    // Phase 1: Read (parallel allowed)
    ExecuteReadPhase(tick);

    // Barrier
    lock (_barrierLock) { BarrierReached?.Invoke(tick); }

    // Phase 2: Write (serialized)
    ExecuteWritePhase(tick);

    PostTick?.Invoke(tick);
    _currentTick++;
}

private void ExecuteReadPhase(ulong tick)
{
    Parallel.ForEach(_systems, system =>
    {
        try { system.ReadTick(tick); }
        catch (Exception ex) { HandleSystemError(system, "Read", ex); }
    });
}

private void ExecuteWritePhase(ulong tick)
{
    foreach (var system in _systems)
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
- `MergeAndSort()` applies a deterministic sort key, then resolves conflicts: for MoveCreature ops, one op per entity per tick; for tile ops, highest-priority system wins (Mining > Hauling > Construction).
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
        ((ulong)Target.ChunkId << 32) |
        ((ulong)Target.LocalIndex << 16) |
        ((ulong)(uint)Priority << 8) |
        ((ulong)(uint)SystemId.GetHashCode());
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
**Why:** The composition root — shows how all systems are wired together.

- Singleton that owns TickScheduler, CommandQueue, EventBus, RngStreamManager, DiffLog.
- Creates and wires World, NavigationManager, all job planners/executors, and the UnifiedJobsOrchestrator.
- Manages game state transitions (MainMenu -> WorldGen -> WorldMap -> EmbarkPrep -> FortressPlay).
- Registers all ITick systems with the scheduler in correct priority order.

---

## D. Things I Actually Did Here

1. **Designed the read-parallel / write-serialized tick loop** — `TickScheduler.cs` runs all systems' ReadTick in parallel, then WriteTick sequentially, enforcing determinism with a barrier.

2. **Implemented deterministic A* pathfinding with 3D vertical movement** — `DeterministicAStar.cs` handles stairs, ramps (with directional bitmasks), diagonal corner-cutting prevention, and partial path fallback under time/node budgets.

3. **Built the DiffLog conflict resolution system** — `DiffLog.cs` collects all mutation ops, sorts by a stable composite key, and resolves conflicts with system-precedence rules (Mining > Hauling > Construction).

4. **Created the UnifiedJobsOrchestrator** — coordinates four job subsystems (mining, hauling, construction, crafting) in a single deterministic pipeline with adaptive scheduling hints (reserves workers for high-priority backlogs).

5. **Implemented a data-driven content pipeline** — creature/item/material definitions loaded from JSON at startup (`CreatureManager.LoadDefinitions`), validated, and tag-indexed for runtime queries.

6. **Built a multi-stage procedural world generator** — `WorldGen/Stages/` pipeline (Elevation -> Climate -> Biome) produces the overworld map; `FortressGenerator` carves the detailed local map with geology and ore placement.

---

## E. Three Likely Interview Questions

1. **"How do you keep the simulation deterministic when systems run in parallel?"**
   Hint: TickScheduler.cs lines 204–225 — parallel reads can't mutate state; all mutations go through DiffLog which sorts by a stable composite key before applying. RNG uses named streams (`RngStreamManager`) so each system gets reproducible sequences.

2. **"Walk me through what happens when a player issues a mining order."**
   Hint: Player command -> `CreateMiningOrderCommand` enqueued in `CommandQueue` -> `MiningSystem.ReadTick` picks it up and creates designations -> `MiningJobSystem` assigns idle creatures, pathfinds via `DeterministicAStar`, stages dig progress over ticks -> on completion, emits `SetTerrain` DiffOp and drops items.

3. **"Your A* handles stairs and ramps — how does vertical movement work?"**
   Hint: `DeterministicAStar.CheckVerticalNeighbors()` (line 214) — stairs add neighbors one Z-level up/down with a cost delta. Ramps use a directional bitmask (`TryGetUpRampMask`) so a ramp at tile X can connect to specific adjacent tiles one Z-level higher; descent checks the reverse direction.

---

## F. Honest Self-Check

**Potential weak spots an interviewer might notice:**

- **Hardcoded master seed** (`Program.cs:172`): `ulong masterSeed = 12345; // TODO: Make configurable`. Shows the game isn't configurable yet.
- **~30 TODO markers** scattered through the codebase — stockpile integration, diff-log migration for spawning, HP calculation, quarantine logic. This is normal for an in-progress personal project, but don't claim these systems are "complete."
- **CreatureManager.SpawnCreature bypasses DiffLog** — it directly writes instead of going through the mutation pipeline. A TODO acknowledges this. Don't claim the DiffLog is used everywhere.
- **StockpileDiffApplicator** has many stub methods (TODO comments for actual item placement/removal). Don't steer the interviewer toward stockpiles.
- **No automated test suite** beyond `PhaseTests.cs` and `TestRunner.cs` (manual validation). Don't claim TDD.
- **Verbose debug logging** (Console.WriteLine in CreatureManager.SpawnCreature) — production code wouldn't log at this granularity. Minor, but shows it's a dev build.

**Things that are solid and safe to highlight:**
- The tick scheduler / read-write phase separation is clean and well-structured.
- The A* implementation is genuinely non-trivial (3D, partial paths, deterministic).
- The DiffLog merge strategy is a real design decision with clear conflict resolution rules.
- The job orchestrator shows systems-level thinking about scheduling and resource contention.
- The multi-project separation (Core/Simulation/Navigation/WorldGen/App) is a mature architectural choice for a personal project.
