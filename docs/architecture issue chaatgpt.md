# HumanFortress Architecture Review Report

**Repository reviewed:** `Lym666XD/TheFortressSimulation`  
**Branch reviewed:** `jobsystem`  
**Report type:** Code-level architecture review  
**Prepared for:** HumanFortress / TheFortressSimulation project  
**Date:** 2026-05-25  

> This report is based on a code-level review of the current project structure and implementation patterns.  
> It is intentionally strict and assumes the long-term goal is a large, maintainable simulation game rather than a short-lived prototype.

---

## 1. Executive Summary

The current project has several strong architectural ideas already present:

- separated projects for `Core`, `Simulation`, `Navigation`, `WorldGen`, and `App`;
- a fixed-tick simulation concept;
- a render snapshot direction;
- a content registry direction;
- navigation services and path caching;
- job orchestration;
- diff-log based world mutation;
- early UI state management;
- early deterministic/replay-oriented thinking.

However, the implementation boundaries do not yet consistently enforce those ideas.

The main risk is not that the project has a bad design. The main risk is that good architectural concepts are being bypassed by prototype-era shortcuts. The most serious examples are:

- `GameStateManager` is acting as a runtime host, content loader, system composition root, debug service, and scenario bootstrapper.
- `FortressState` is not only a UI screen; it also generates maps, fills the world, rebuilds navigation, wires job callbacks, injects debug orders, and directly reads live world state.
- Core gameplay job logic currently lives under `HumanFortress.App.Jobs`.
- Command execution appears to happen from the application/game-state update path rather than strictly inside the simulation tick pipeline.
- `DiffLog` currently uses `SystemId.GetHashCode()` in a deterministic sort key, which is unsafe for deterministic replay.
- Navigation queries can trigger on-demand navigation rebuilds, which violates the intended read/plan vs rebuild/commit separation.
- Content loading is spread across `ContentRegistry`, managers, and app/runtime classes.
- Entity identity uses GUIDs in authoritative paths, which is not suitable for deterministic replay.
- UI reads and draws from live world/runtime state rather than only consuming immutable snapshots.

If the goal is a long-term Dwarf Fortress / RimWorld / Factorio / Oxygen Not Included style simulation game, the project should now begin shifting from “working prototype structure” to “runtime-hosted, deterministic, snapshot-driven architecture”.

The top five recommended corrective actions are:

1. Extract `FortressRuntime` / `SimulationHost`.
2. Move `HumanFortress.App.Jobs` into a proper `HumanFortress.Jobs` or `HumanFortress.Simulation.Jobs` module.
3. Move command execution into the simulation tick pipeline.
4. Replace unstable diff ordering and GUID-derived entity IDs with deterministic identifiers and stable tie-breakers.
5. Refactor `FortressState` into a presentation-only screen that consumes snapshots and emits commands.

---

## 2. Current Project Structure Assessment

The current active solution appears to be organized around:

```text
HumanFortress.Core
HumanFortress.Simulation
HumanFortress.Navigation
HumanFortress.WorldGen
HumanFortress.App
```

This is a reasonable early-stage structure. The current dependency direction is broadly sensible:

```text
Core
  ↓
Simulation
  ↓
Navigation / WorldGen
  ↓
App
```

However, for a larger simulation game, this structure is too coarse. The following responsibilities are currently mixed:

- foundation primitives;
- contracts between modules;
- content loading and registry building;
- authoritative world data;
- simulation scheduling and commit pipeline;
- navigation;
- job scheduling and execution;
- runtime composition;
- UI;
- rendering;
- debug tooling;
- CLI/dev tooling.

The current structure can continue to support a prototype, but it will become difficult to scale once AI, combat, save/load, modding, economy, incidents, and richer UI are added.

---

## 3. Major Architectural Issues

### 3.1 `GameStateManager` Is Becoming a God Object

`GameStateManager` currently appears to own or coordinate too many unrelated responsibilities, including:

```text
World
TickScheduler
CommandQueue
EventBus
RngStreamManager
DiffLog
ItemsDiffLog
NavigationManager
SimulationContext
Job planners
Job executors
Profession assignments
Scheduler tunings
Debug caches
Content loading
Recipe loading
Construction registry loading
Initial worker spawning
Auto-dig test setup
Post-tick diff application
Dirty chunk navigation rebuild
```

This makes it much more than a state manager.

#### Why this is a problem

A game state manager should manage large application-level states such as:

```text
MainMenu
WorldGeneration
Embark
FortressPlay
Pause
Settings
```

It should not own the full simulation runtime. If it does, the project becomes hard to test, hard to run headlessly, hard to benchmark, hard to replay, and hard to save/load cleanly.

#### Recommended direction

Create a dedicated runtime layer:

```text
HumanFortress.Runtime
  FortressRuntime
  SimulationHost
  SystemCompositionRoot
  ContentBootstrapper
  WorldBootstrapper
  ScenarioBootstrapper
  RuntimeDebugService
```

Then `GameStateManager` should only coordinate transitions between screens/states.

---

### 3.2 `FortressState` Mixes UI, Runtime, Map Generation, Debug, and World Mutation

`FortressState` currently appears to do far more than rendering and input handling. It is responsible for or directly involved in:

```text
Creating SadConsole surfaces
Managing camera and cursor
Generating the fortress map
Filling the simulation world with generated terrain
Creating RenderSnapshotBuilder
Wiring UI callbacks into job systems
Creating or using NavigationManager
Calling RebuildAll on navigation data
Injecting auto-dig orders
Creating stockpile/order/zone/build UI objects
Loading input bindings and orders registry
Drawing map, overlays, panels, highlights, debug UI
Reading live world state
```

#### Why this is critical

A UI screen must not be the place where the simulation world is generated, mutated, or wired. This makes the UI layer authoritative over gameplay state, which conflicts with the desired architecture:

```text
Input
  → Command
  → Simulation tick
  → Deterministic commit
  → Immutable snapshot
  → UI render
```

The current direction risks becoming:

```text
UI screen
  → direct world mutation
  → direct nav rebuild
  → direct debug command injection
  → direct runtime wiring
```

That is not scalable.

#### Recommended direction

Refactor toward:

```text
FortressPlayScreen
  Reads RenderSnapshot
  Reads DebugSnapshot
  Draws UI components
  Routes input to UiActions
  Maps UiActions to SimulationCommands
  Sends commands to FortressRuntime
```

All world creation, world filling, system wiring, and navigation rebuild should happen in Runtime/Simulation layers.

---

### 3.3 Core Gameplay Job Logic Lives in the App Layer

Core job systems such as mining, hauling, construction, crafting, profession assignment, and unified job orchestration appear to live under:

```text
HumanFortress.App.Jobs
```

This is a serious layering issue.

#### Why this is a problem

The `App` layer should own:

```text
Program startup
Window setup
SadConsole setup
Application state transitions
Input binding
UI screens
UI components
Presentation-specific debug panels
```

It should not own:

```text
Mining job execution
Hauling logic
Construction job execution
Crafting job execution
Worker selection
Profession allocation
Reservation rules
Movement execution
Job scheduling
```

If gameplay rules remain in `App`, then headless simulation, replay tests, benchmark runners, and server-like tools will all depend on the UI application project.

#### Recommended direction

Move gameplay job logic to:

```text
HumanFortress.Jobs
```

or, as an intermediate step:

```text
HumanFortress.Simulation.Jobs
```

The App layer may keep:

```text
JobsPanel
WorkDrawer UI
Job debug display components
Job-related input mappings
```

But not job rules or job execution.

---

### 3.4 Command Execution Should Be Inside the Simulation Tick Pipeline

The command queue is intended to support deterministic input handling. However, command execution appears to be triggered from the application/game-state update path while the tick scheduler runs on a background simulation thread.

#### Why this is critical

Even if the queue itself is locked, `command.Execute(context)` may mutate simulation state. If commands are executed from the UI/game-state update path instead of the simulation tick pipeline, then world mutation can interleave with simulation stages.

This breaks the intended model:

```text
ApplyCommands stage
  → read/plan
  → deterministic merge
  → commit
  → snapshot
```

#### Recommended direction

The UI/app thread should only enqueue commands:

```text
UI thread:
  runtime.EnqueueCommand(command)
```

The simulation thread should execute them only at a deterministic stage:

```text
Simulation tick:
  ApplyCommands
    commandQueue.DrainForTick(currentTick)
    execute commands in stable order
```

No command should mutate the world directly from App/GameState update.

---

### 3.5 `SimulationContext.CurrentTick` Must Be Real

The current simulation context appears to expose `CurrentTick`, but it is not properly propagated and may return a placeholder value.

#### Why this matters

Many systems depend on correct tick identity:

```text
Command execution
Reservation TTL
Event timestamps
Replay logs
Job assignment times
Debug traces
Save metadata
Deterministic RNG stream selection
```

A placeholder tick value undermines deterministic simulation and makes debugging unreliable.

#### Recommended direction

`CurrentTick` should be supplied by the simulation scheduler and be available consistently through a tick-scoped context:

```text
SimulationTickContext
  Tick
  Stage
  WorldReadView
  WorldWriteContext
  CommandBuffer
  EventSink
  RngProvider
```

---

## 4. Determinism and Replay Risks

### 4.1 `SystemId.GetHashCode()` Must Not Be Used for Stable Ordering

The current `DiffLog` sort key includes `SystemId.GetHashCode()`.

This is unsafe for deterministic simulation. In .NET, string hash codes are not appropriate as stable cross-run, cross-platform simulation ordering keys. Even if they appear stable in one session, they should not be treated as a replay-stable ordering mechanism.

#### Recommended replacement

Use explicit deterministic identifiers:

```text
SystemOrderId
  Jobs.Mining = 100
  Jobs.Hauling = 110
  Jobs.Construction = 120
  Jobs.Crafting = 130
```

or a registered stable ordering table:

```text
SystemRegistry
  SystemId string
  StableSystemIndex int
  Stage
  Priority
```

Diff ordering should use:

```text
Tick
StageOrder
ChunkKey
LocalIndex
Layer
Priority
StableSystemIndex
LocalSequence
```

---

### 4.2 GUIDs Should Not Be Authoritative Simulation IDs

Creature and item instances currently appear to use `Guid.NewGuid()`. Some code derives an entity ID by converting bytes from a GUID to `uint`.

#### Why this is a problem

```text
Guid.NewGuid() is not deterministic.
Guid → uint can collide.
GUID allocation order can vary.
Replay and save/load identity becomes harder to reason about.
```

#### Recommended direction

Use deterministic simulation IDs:

```text
EntityId
  ulong Value

EntityIdAllocator
  seeded by world seed
  monotonic or deterministic per entity category
```

GUIDs can still exist as external/debug IDs, but the authoritative simulation path should use deterministic IDs.

---

### 4.3 Diff Payloads Are Too Packed and Weakly Typed

Current diff operations appear to use a generic `ulong Args` field for packed payload data.

#### Why this is a problem

It is compact, but it becomes hard to validate, extend, debug, and version.

#### Recommended direction

Short term:

```text
Keep packed args for hot paths only.
Add typed helper constructors and decoders.
```

Long term:

```text
DiffOp<TPayload>
SetTerrainPayload
MoveCreaturePayload
AddItemPayload
MoveItemPayload
```

At minimum, all packed payload formats should be centralized and tested.

---

### 4.4 Conflict Resolution Is Hardcoded

`DiffLog` currently hardcodes precedence for system IDs such as mining, hauling, and construction.

#### Why this is a problem

This does not scale to modding, AI, combat, fluids, zones, construction sites, doors, furniture, fire, collapse, and incidents.

#### Recommended direction

Use a registered merge table:

```text
Layer
Operation
Conflict group
Merge strategy
Priority
Stable system order
Partial accept policy
Rejection event type
```

---

## 5. Navigation and Movement Issues

### 5.1 Navigation Queries Can Trigger Rebuilds

`NavigationManager.GetNavDataAt()` may detect stale navigation data and rebuild chunk navigation data on demand.

#### Why this is a problem

Pathfinding queries may happen during read/plan phases. If a read query triggers a rebuild, then the read phase is no longer read-only.

This violates the desired stage model:

```text
RebuildDerived stage:
  rebuild dirty derived data

Read/Plan stage:
  consume derived data only
```

#### Recommended direction

Navigation query should not rebuild.

Instead:

```text
GetNavDataAt()
  returns current immutable nav snapshot
  optionally reports stale/missing data

RebuildDerived stage
  rebuilds dirty chunks
  publishes new nav snapshots
```

---

### 5.2 PathService and MovementExecutor Should Be Runtime-Wide Services

Some job systems appear to create their own `PathService`, `WorldNavigationView`, and `MovementExecutor`.

#### Why this is a problem

This fragments core movement state:

```text
Different systems have different movement executors.
Path caches are not shared.
Path budgets are not centralized.
Movement conflicts are difficult to resolve.
A single worker could be managed by multiple movement owners.
```

#### Recommended direction

Create a centralized movement architecture:

```text
NavigationService
  path requests
  path cache
  path budget
  path stats

MovementSystem
  all active movement states
  movement intents
  reservation-aware step resolution

Job systems
  request movement
  do not own movement execution
```

A job system should say:

```text
Worker X needs to move to Y.
```

It should not own the full movement executor for Worker X.

---

### 5.3 Path Cache Invalidation Needs Stronger Versioning

The current cache key appears to include source and destination chunk connectivity versions. But paths may cross intermediate chunks.

A separate chunk index can invalidate cached paths, but correctness depends on every dirty chunk invalidating every relevant cache instance.

#### Recommended direction

Each cached path should store:

```text
Traversed chunks
Connectivity version per traversed chunk
Movement profile id
Dynamic obstacle policy
Request flags
```

On lookup, validate the entire corridor, not only source/destination.

---

## 6. World and Data Model Issues

### 6.1 `World` Is Both Data Container and Manager Aggregator

`World` currently owns chunks and also owns global managers such as creatures, items, orders, stockpiles, reservations, and zones.

This is acceptable for an early prototype, but it will get crowded.

#### Recommended direction

Split conceptually:

```text
WorldState
  chunks
  tile arrays
  entity stores
  indices

WorldServices / Managers
  query and mutation helpers

Simulation systems
  operate on WorldState through contexts
```

---

### 6.2 Direct World Mutation Is Too Easy

`World.SetTile()` is public and guarded mainly by a comment saying it should be called during write phase.

#### Why this is a problem

A comment is not an architectural boundary.

Any code can call SetTile if it has a World reference. Since many App/UI/runtime objects currently have direct World references, accidental direct mutation is likely.

#### Recommended direction

Use explicit write contexts:

```text
WorldReadView
WorldWriteContext
ChunkWriteToken
```

Only the simulation commit stage should receive a write context.

---

### 6.3 Locks Are Not a Substitute for Deterministic Ownership

`Chunk` uses locks for write operations. This helps prevent some memory races, but it does not guarantee deterministic simulation ordering.

For a deterministic simulation game, the goal is not simply “thread-safe writes”. The goal is:

```text
No arbitrary concurrent writes.
All writes happen through known stages.
Conflict resolution is deterministic.
```

#### Recommended direction

Prefer staged ownership:

```text
Read/Plan:
  many workers read immutable or stable state

Commit:
  deterministic writer applies resolved diffs
```

Use locks only around infrastructure where unavoidable, not as the primary simulation model.

---

### 6.4 Dirty Tile/Chunk Tracking Is Incomplete

Dirty tracking is essential for large worlds. Any system that changes terrain, furniture, fluid, fields, support, or occupancy should produce precise dirty information.

You need dirty tracking for:

```text
navigation
line of sight
support/collapse
lighting
fields
render snapshots
path cache invalidation
debug overlays
```

#### Recommended direction

Add real dirty sets:

```text
DirtyTileSet
DirtyChunkSet
DirtyZSliceSet
DirtyNavRegionSet
DirtyRenderRegionSet
```

---

## 7. Content and Registry Issues

### 7.1 Content Loading Is Spread Across Too Many Places

Content is currently loaded or parsed in multiple places:

```text
ContentRegistry
CreatureManager
ItemManager
GameStateManager
Other app/runtime setup paths
```

#### Why this is a problem

A modded simulation game needs one coherent content pipeline.

Fragmented content loading makes it hard to implement:

```text
content pack load order
schema validation
stable runtime IDs
save compatibility
mod conflict resolution
hot reload
content tests
registry signatures
```

#### Recommended direction

Create:

```text
HumanFortress.Content
  ContentLoader
  ContentPack
  RegistryBuilder
  SchemaValidator
  RegistrySnapshot
  IdMapBuilder
  TuningRegistry
```

Managers should receive a compiled registry snapshot. They should not read JSON files themselves.

---

### 7.2 `ContentRegistry.Instance` Is a Global Singleton

Global singletons make testing and modding harder.

#### Problems

```text
Cannot easily test multiple content packs in one process.
Cannot easily run multiple worlds with different registries.
Cannot easily isolate tests.
Harder to support save-specific registry signatures.
```

#### Recommended direction

Replace:

```text
ContentRegistry.Instance
```

with:

```text
ContentRegistrySnapshot registry
```

passed through runtime services or simulation context.

---

## 8. UI and Rendering Issues

### 8.1 UI Is Partially Componentized but Still Centralized

The project has `UiStore` and UI helper classes, which is good. But `FortressState` still appears to orchestrate too much drawing and live world access.

`UiStore` itself is a large mutable state bag containing many unrelated UI concepts:

```text
drawers
quick menus
placement modes
selected creature/item
workshop panel state
stockpile/zone placement
debug menu
toasts
construction selection
buildable selection
```

#### Recommended direction

Use a stronger MVU-style architecture:

```text
UiModel
UiAction
UiReducer
UiSelectors
InputRouter
CommandMapper
Components
```

Components should receive:

```text
UiModel
RenderSnapshot
DebugSnapshot
```

They should not receive live `World`.

---

### 8.2 UI Must Not Read Live World State

The current UI appears to draw overlays and panels using live world references.

#### Why this is a problem

This breaks concurrent safety and makes snapshots less meaningful.

#### Recommended direction

The runtime should publish:

```text
RenderSnapshot
DebugSnapshotBundle
EventStreamSnapshot
```

The UI should consume these only.

If an inspector needs tile details, it should ask for a snapshot/debug DTO, not a mutable live tile reference.

---

### 8.3 Rendering Snapshot Contains Backend-Like Visual Mapping

`RenderSnapshotBuilder` appears to contain tile visual mapping and palette/glyph logic.

#### Why this is a problem

Simulation can produce renderable semantic snapshots, but glyph/palette mapping belongs to rendering.

#### Recommended split

```text
Simulation.Snapshots
  semantic tile/entity/placeable state

Rendering
  maps semantic state to glyphs, colors, palettes, animations

Rendering.SadConsole
  SadConsole-specific backend
```

---

## 9. Logging and Debug Tooling Issues

### 9.1 Hot-Path Text Logging Is Too Heavy

Many systems log frequently during tick operations.

#### Risks

```text
performance overhead
non-deterministic log interleaving
large noisy logs
harder to see important events
debug code mixed with gameplay code
```

#### Recommended direction

Use structured debug data:

```text
JobDebugSnapshot
NavigationDebugSnapshot
DiffMergeDebugSnapshot
PerformanceCounters
ReplayHashSnapshot
```

Text logs should be rate-limited and category-based.

---

### 9.2 Debug Tools Should Be First-Class

For a simulation game, debugging tools are not optional. They are architecture.

Recommended debug panels:

```text
Tile inspector
Entity inspector
Job board inspector
Reservation inspector
Path inspector
Nav mesh/region overlay
Diff merge inspector
Performance panel
Replay/desync panel
Content registry inspector
```

---

## 10. Testing and CI Issues

### 10.1 Formal Test Projects Are Missing

The project currently appears to rely heavily on CLI smoke modes such as validation, crash testing, and init-only flows.

These are useful, but they are not enough.

#### Recommended tests

```text
HumanFortress.Foundation.Tests
HumanFortress.Content.Tests
HumanFortress.World.Tests
HumanFortress.Simulation.Tests
HumanFortress.Navigation.Tests
HumanFortress.Jobs.Tests
HumanFortress.Integration.Tests
HumanFortress.Determinism.Tests
HumanFortress.Benchmarks
```

#### Priority test categories

```text
Diff merge determinism
Command queue ordering
Pathfinding correctness
Path cache invalidation
Dirty chunk rebuild
Mining job scenario
Hauling reservation conflict
Save/load roundtrip
Content validation
World hash replay
Scheduler jitter fuzz
```

---

### 10.2 CLI Tools Should Become Separate Headless Tools

Current CLI modes are useful but should evolve into tools:

```text
HumanFortress.HeadlessRunner
HumanFortress.ReplayTool
HumanFortress.BenchmarkRunner
HumanFortress.ContentCompiler
```

This prevents the UI app from becoming the only way to validate the simulation.

---

## 11. Build and Platform Configuration Issues

### 11.1 Analyzer and Warning Policy Is Inconsistent

Some projects are strict, while others allow warnings or suppress many analyzer warnings.

For a large project, inconsistent build rules cause architectural erosion.

#### Recommended direction

```text
Foundation / Contracts:
  strictest warnings

Simulation / Navigation / Jobs / World:
  warnings as errors in CI

App / UI:
  can be somewhat more flexible but still checked

Generated/prototype code:
  explicitly isolated
```

---

### 11.2 App Build Should Not Always Be Win-x64 Self-Contained

Platform-specific publish settings should usually live in publish profiles, not ordinary project defaults.

Recommended:

```text
Debug build:
  portable

Publish profiles:
  win-x64 self-contained
  linux-x64 optional
  osx optional
```

---

## 12. Recommended Target Architecture

A mature target architecture could be:

```text
src/
  HumanFortress.Foundation/
  HumanFortress.Contracts/
  HumanFortress.Content/
  HumanFortress.World/
  HumanFortress.Simulation/
  HumanFortress.Navigation/
  HumanFortress.Jobs/
  HumanFortress.AI/
  HumanFortress.Economy/
  HumanFortress.Combat/
  HumanFortress.Incidents/
  HumanFortress.WorldGen/
  HumanFortress.Save/
  HumanFortress.Runtime/
  HumanFortress.Rendering/
  HumanFortress.Rendering.SadConsole/
  HumanFortress.UI/
  HumanFortress.App/

tools/
  HumanFortress.ContentCompiler/
  HumanFortress.ReplayTool/
  HumanFortress.BenchmarkRunner/
  HumanFortress.HeadlessRunner/

tests/
  HumanFortress.Foundation.Tests/
  HumanFortress.Content.Tests/
  HumanFortress.World.Tests/
  HumanFortress.Simulation.Tests/
  HumanFortress.Navigation.Tests/
  HumanFortress.Jobs.Tests/
  HumanFortress.Integration.Tests/
  HumanFortress.Determinism.Tests/
  HumanFortress.Benchmarks/
```

For a more practical first migration:

```text
HumanFortress.Foundation or keep HumanFortress.Core
HumanFortress.Contracts
HumanFortress.Content
HumanFortress.World
HumanFortress.Simulation
HumanFortress.Navigation
HumanFortress.Jobs
HumanFortress.WorldGen
HumanFortress.Save
HumanFortress.Runtime
HumanFortress.UI
HumanFortress.App
```

AI, combat, economy, incidents, and rendering can begin as namespaces before becoming separate projects.

---

## 13. Recommended Dependency Direction

```text
Foundation
  ↓
Contracts
  ↓
Content
  ↓
World
  ↓
Simulation
  ↓
Jobs / AI / Combat / Economy / Incidents / Navigation
  ↓
Runtime
  ↓
UI / Rendering
  ↓
App
```

More precisely:

```text
App depends on Runtime, UI, Rendering.SadConsole.
Runtime depends on all gameplay modules.
UI depends on Contracts and Rendering abstractions, not live World.
Rendering depends on snapshots/contracts, not Simulation internals.
Navigation depends on Contracts, not full Simulation internals.
Jobs depends on Simulation/World/Navigation contracts, not App.
Simulation owns tick, command execution, diff merge, and deterministic commit.
World owns authoritative data structures.
Content owns registries and validation.
Foundation owns deterministic primitives.
```

---

## 14. Suggested Migration Plan

### Phase 1: Stop Architectural Bleeding

Rules:

```text
No new gameplay logic in App.
No new direct world mutation in UI states.
No new static callbacks from simulation/jobs to UI.
No new content parsing in GameStateManager.
No new per-job-system movement executor.
```

### Phase 2: Extract Runtime

Create:

```text
FortressRuntime
SimulationHost
SystemCompositionRoot
ContentBootstrapper
WorldBootstrapper
ScenarioBootstrapper
RuntimeDebugService
```

Move out of `GameStateManager`:

```text
World ownership
Scheduler ownership
System creation
Content loading
Initial spawn logic
Post-tick diff application
Dirty navigation rebuild
Debug caches
```

### Phase 3: Move Jobs Out of App

Move:

```text
UnifiedJobsOrchestrator
MiningJobSystem
TransportJobSystem
ConstructionJobSystem
CraftJobSystem
CraftPlanner
ProfessionAssignments
SchedulerTunings
WorkshopTunings
SanitizeSystem
```

into:

```text
HumanFortress.Jobs
```

### Phase 4: Fix Determinism Foundations

Fix:

```text
SystemId.GetHashCode in DiffLog
Guid.NewGuid as authoritative entity ID
SimulationContext.CurrentTick
Command execution location
Path rebuild in read/query phase
```

### Phase 5: Introduce Contracts

Extract:

```text
IWorldNavigationView
ICommandSink
IRenderSnapshotProvider
IDebugSnapshotProvider
IEventStream
IContentRegistryView
```

### Phase 6: Refactor UI

Split `FortressState` into:

```text
FortressPlayScreen
WorldViewportComponent
TopBarComponent
DrawerHostComponent
InspectorPanelComponent
DebugPanelHostComponent
QuickMenuComponent
OverlayLayerComponent
InputRouter
CommandMapper
```

### Phase 7: Add Tests and Tools

Add:

```text
Navigation.Tests
Simulation.Tests
Jobs.Tests
Content.Tests
Determinism.Tests
HeadlessRunner
ReplayTool
BenchmarkRunner
```

---

## 15. Final Assessment

HumanFortress has a promising foundation. The existing code shows awareness of serious simulation-game architecture concerns: deterministic ticks, snapshots, navigation caches, diff logs, content-driven definitions, and job orchestration.

However, the current implementation is still too prototype-oriented for a large long-term game. The architecture is at risk because boundaries are not enforced:

```text
UI can mutate or wire world/runtime.
App owns gameplay rules.
Runtime concerns live inside GameStateManager.
Jobs own movement/path services.
Navigation can rebuild during queries.
Content loading is fragmented.
Diff ordering is not truly deterministic.
Entity identity is not replay-safe.
```

The highest-value architectural move is not a new algorithm. It is to establish a real runtime boundary.

The first strategic target should be:

```text
App becomes thin.
Runtime owns session orchestration.
Simulation owns tick and commit.
Jobs own gameplay work rules.
Navigation owns path services.
UI consumes snapshots and emits commands.
```

If these boundaries are established now, the project can grow toward AI, combat, save/load, modding, ECS-style storage, multithreading, and richer UI without collapsing into an unmaintainable prototype.
