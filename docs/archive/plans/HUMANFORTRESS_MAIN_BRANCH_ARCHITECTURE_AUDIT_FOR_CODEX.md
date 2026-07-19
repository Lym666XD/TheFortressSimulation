# HumanFortress Main Branch Architecture Audit for Codex

**Repository:** `Lym666XD/TheFortressSimulation`  
**Branch reviewed:** `main`  
**Audit focus:** Real source code, not design documents  
**Purpose:** Architecture context and refactor guidance for Codex / Claude Code  
**Date:** 2026-06-12  

**Current status note (2026-07-11):** This is a source audit snapshot, not the current architecture overview. It has been reconciled into [STAGED_REFACTOR_TARGET.md](../../planning/STAGED_REFACTOR_TARGET.md). Some findings were resolved after the audit text was written; for current runtime/content boundaries, read [GAME_ARCHITECTURE.md](../../architecture/GAME_ARCHITECTURE.md) and [CONTENT_SYSTEM.md](../../content/CONTENT_SYSTEM.md).

> This document summarizes a strict code-level architecture audit of the current `main` branch.  
> It is intended to help future coding agents understand the project’s current state, the intended direction, and the risks of making broad changes without respecting architectural boundaries.

---

## 0. How Codex Should Use This Document

### Hard rules for coding agents

Before making changes:

1. Read the actual source files. Do not rely only on this document.
2. Do not perform broad rewrites without a scoped task.
3. Prefer small, buildable, testable phases.
4. Preserve existing behavior unless explicitly asked to change it.
5. Do not move files across projects without checking project references.
6. Do not add new gameplay logic to `HumanFortress.App`.
7. Do not make UI read or mutate authoritative simulation state directly.
8. Do not create a second command/mutation path outside the simulation tick pipeline.
9. Do not introduce nondeterministic ordering, `new Random()`, unstable hash-based ordering, or thread-order-dependent behavior.
10. When uncertain, produce an analysis report first instead of editing code.

### Recommended agent workflow

Use this sequence:

```text
Round 1: Inspect and report.
Round 2: Propose scoped migration plan.
Round 3: Implement one small phase.
Round 4: Build and test.
Round 5: Report remaining risks.
```

Avoid prompts like:

```text
Refactor the architecture.
```

Prefer prompts like:

```text
Move only SimulationRuntimeHost and SimulationRuntimeSystems from App.Runtime to Runtime. Preserve behavior. Do not touch UI layout or job logic.
```

---

## 1. Executive Summary

The `main` branch has improved significantly compared with the earlier prototype architecture.

Major improvements observed:

```text
1. The old dual-solution risk appears resolved; only HumanFortress.sln is visible.
2. New projects exist: Contracts, Content, Jobs, Runtime, and App.Tests.
3. CommandQueue execution has moved into the simulation tick pipeline through SimulationTickPipeline.PreTick.
4. Navigation no longer directly references Simulation; it reads through INavigationWorldSource.
5. FortressState has been reduced from a large god-class into a smaller coordinator.
6. Mining and Transport core execution logic has partially moved into HumanFortress.Jobs.
7. Creature and item runtime GUIDs now use deterministic sequence generation.
8. DiffLog no longer uses string.GetHashCode as a deterministic sort key.
```

However, the branch is still in a **half-refactored transitional architecture**.

Main risks:

```text
1. HumanFortress.Content appears to have source files that reference Core/Simulation types while its csproj only references Contracts.
2. HumanFortress.Runtime exists, but real simulation host composition still lives in HumanFortress.App.Runtime.
3. HumanFortress.Jobs exists, but App/Jobs still contains tick-system wrappers, tunings, orchestrator, and gameplay-facing system shells.
4. UI is more componentized, but it still reads live World and concrete job systems.
5. Content loading still has legacy and structured registries coexisting.
6. PathService and MovementExecutor are still created per job system rather than owned by a runtime-wide movement/navigation service.
7. Diff priority semantics are not fully unified between diff systems.
8. Tests exist, but the test project is a custom executable rather than a standard xUnit/NUnit/MSTest setup.
```

Overall assessment:

```text
Direction: correct
Progress: significant
Maturity: mid-refactor transitional state
Biggest risk: half-migrated architecture becoming permanent
Best next move: finish Runtime / Jobs / Content / UI snapshot boundary migration before adding major gameplay systems
```

---

## 2. Current Project Structure

The active solution currently contains:

```text
HumanFortress.Core
HumanFortress.Contracts
HumanFortress.Content
HumanFortress.Simulation
HumanFortress.Navigation
HumanFortress.Jobs
HumanFortress.WorldGen
HumanFortress.Runtime
HumanFortress.App
HumanFortress.App.Tests
```

This is close to the desired long-term direction, but not all project boundaries are yet clean.

### Current high-level dependency intent

Desired direction:

```text
Core / Foundation
  ↓
Contracts
  ↓
Content / World / Simulation / Navigation / Jobs
  ↓
Runtime
  ↓
UI / Rendering / App
```

Current reality:

```text
Runtime pipeline exists in HumanFortress.Runtime.
But concrete session composition still exists in HumanFortress.App.Runtime.

Jobs executors exist in HumanFortress.Jobs.
But tick-system wrappers and orchestration still exist in HumanFortress.App.Jobs.

Contracts exist.
But some contract types physically live in Contracts while using HumanFortress.Navigation namespace.

Content project exists.
But its source files appear to reference Simulation/Core types without matching project references.
```

---

## 3. Positive Findings

### 3.1 Command execution is now inside the simulation tick pipeline

This is a major improvement.

Previously, commands were at risk of executing from the App/UI frame update path. In the current main branch, command execution is attached through:

```text
SimulationTickPipeline.AttachTo(TickScheduler)
  PreTick += ExecutePreTick

SimulationCommandStage.Execute(tick)
  context.SetCurrentTick(tick)
  commandQueue.ExecuteCommands(tick, context)
```

This matches the intended model:

```text
UI thread:
  enqueue command only

Simulation thread:
  PreTick / ApplyCommands
  Read phase
  Write phase
  PostTick / diff application
```

This is one of the most important architectural fixes already made.

---

### 3.2 Navigation dependency inversion is mostly achieved

`HumanFortress.Navigation` no longer directly references `HumanFortress.Simulation`.

Navigation now depends on a contract-style source:

```text
INavigationWorldSource
NavigationTile
NavigationChunkSnapshot
```

A runtime adapter bridges Simulation World to Navigation:

```text
SimulationNavigationSource : INavigationWorldSource
```

This is the correct direction:

```text
Navigation should not know Simulation.World internals.
Runtime adapts Simulation.World into navigation snapshots.
```

---

### 3.3 FortressState has been greatly reduced

`FortressState` is now a smaller coordinator rather than a giant monolithic screen.

It delegates to many helper/controller classes, such as:

```text
FortressSessionLoader
FortressViewBootstrapper
FortressKeyboardInputRouter
FortressMouseInputRouter
FortressFrameRenderer
FortressPlacementRouter
FortressMapInteractionController
FortressNavigationDebugController
```

This is a strong improvement over a single massive UI class.

---

### 3.4 Jobs logic is partially moved into HumanFortress.Jobs

Mining and Transport have moved substantial core logic into executor classes:

```text
HumanFortress.Jobs.Mining.MiningJobExecutor
HumanFortress.Jobs.Transport.TransportJobExecutor
```

App-side job systems now act more like wrapper/composition shells.

This is good, but incomplete. The wrappers should also eventually move out of App.

---

### 3.5 Deterministic runtime ID generation has improved

`CreatureManager` and `ItemManager` now use deterministic sequence-based GUID generation rather than `Guid.NewGuid()` for runtime instances.

This is a significant improvement for replay and deterministic simulation.

---

### 3.6 DiffLog no longer uses string.GetHashCode

The previous unstable hash concern has been reduced. The current DiffLog uses a custom stable hash and also falls back to ordinal string comparison.

This is much better than depending on .NET string hash behavior.

---

## 4. Critical Findings

### C1. `HumanFortress.Content` project references appear inconsistent with its source code

Observed structure:

```text
HumanFortress.Content.csproj
  references only HumanFortress.Contracts
```

But source files in `HumanFortress.Content/Definitions` reference:

```text
HumanFortress.Simulation.Creatures
HumanFortress.Simulation.Items
HumanFortress.Core.Content.Registry
```

This is both a possible build issue and an architectural issue.

#### Why it matters

`Content` should be a lower-level content definition and loading layer. It should not depend on `Simulation` runtime types.

If `Content` depends on `Simulation`, the intended dependency direction is inverted:

```text
Bad:
  Content -> Simulation

Better:
  Content -> definitions/contracts only
  Simulation -> consumes compiled content snapshots
```

#### Immediate action

Run:

```bash
dotnet build HumanFortress.sln
```

Specifically verify whether `HumanFortress.Content` builds.

#### Long-term fix

Introduce content-owned definition DTOs, for example:

```text
HumanFortress.Content.Definitions.CreatureDefinitionData
HumanFortress.Content.Definitions.ItemDefinitionData
```

Then have Simulation or World convert those definitions into runtime catalogs or instances.

Do not simply add a `Simulation` reference to `HumanFortress.Content` as the long-term solution unless the team explicitly accepts that dependency inversion.

---

### C2. Runtime composition is still App-owned

`HumanFortress.Runtime` contains useful core runtime pieces:

```text
SimulationRuntimeHostCore
SimulationTickPipeline
SimulationCommandStage
SimulationRuntimeContext
SimulationRuntimeSessionFactory
```

But concrete simulation composition still lives in:

```text
HumanFortress.App.Runtime.SimulationRuntimeHost
HumanFortress.App.Runtime.SimulationRuntimeSystems
```

These App.Runtime classes own or create:

```text
World
TickScheduler
CommandQueue
DiffLog
ItemsDiffLog
CreaturesDiffLog
NavigationManager
SimulationRuntimeContext
Mining / Transport / Construction / Craft systems
ProfessionAssignments
SchedulerTunings
WorkshopTunings
UnifiedJobsOrchestrator
SanitizeSystem
```

#### Why it matters

The App layer should own:

```text
Program startup
SadConsole host
Screens
Input
Presentation
```

It should not own the simulation session host.

If concrete runtime composition remains in App, then future headless simulation, benchmark runner, replay tool, save migration test, and determinism CI may all drag in App/SadConsole dependencies.

#### Recommended fix

Move concrete runtime composition into `HumanFortress.Runtime`:

```text
HumanFortress.Runtime
  SimulationRuntimeHost
  SimulationRuntimeSystems
  RuntimeSystemCompositionRoot
  FortressSessionInitializer
```

Keep App responsible only for:

```text
Creating the window
Creating screens
Passing user commands to Runtime
Reading snapshots/debug snapshots from Runtime
```

---

### C3. UI still reads live World and concrete job systems

`FortressRuntimeAccess` exposes:

```text
World
NavigationManager
TransportJobSystem
MiningJobSystem
ConstructionJobSystem
CraftJobSystem
ProfessionAssignments
UnifiedJobsOrchestrator
SchedulerTunings
```

Rendering code passes live `World` into map and UI renderers.

`FortressUiOverlayRenderer` directly uses live `world`, concrete job systems, UI services, and content registry.

#### Why it matters

The intended architecture is:

```text
UI reads immutable snapshots.
UI emits commands.
UI does not read or mutate live simulation state.
```

Current UI still has access to authoritative live state.

This blocks safe multithreading and makes replay/debug boundaries weaker.

#### Recommended fix

Redesign `FortressRuntimeAccess` to expose only:

```text
SimulationStatus
ICommandSink
IRenderSnapshotProvider
IDebugSnapshotProvider
IEventStreamSnapshotProvider
```

Do not expose:

```text
World
NavigationManager
Concrete job systems
Concrete simulation systems
```

Introduce debug snapshots:

```text
JobsDebugSnapshot
NavigationDebugSnapshot
TileInspectionSnapshot
EntityInspectionSnapshot
StockpileDebugSnapshot
WorkshopDebugSnapshot
```

Then update UI components to consume snapshots instead of live World.

---

## 5. High Severity Findings

### H1. App/Jobs wrappers are still gameplay-adjacent and participate in tick scheduling

Although mining and transport execution logic moved into `HumanFortress.Jobs`, App still contains job system wrappers:

```text
HumanFortress.App.Jobs.MiningJobSystem
HumanFortress.App.Jobs.TransportJobSystem
HumanFortress.App.Jobs.ConstructionJobSystem
HumanFortress.App.Jobs.CraftJobSystem
HumanFortress.App.Jobs.UnifiedJobsOrchestrator
HumanFortress.App.Jobs.SanitizeSystem
HumanFortress.App.Jobs.SchedulerTunings
HumanFortress.App.Jobs.WorkshopTunings
HumanFortress.App.Jobs.ProfessionAssignments
```

These are not presentation concerns.

#### Why it matters

Even if these classes are “composition shells”, they are part of the simulation tick pipeline. Therefore they should not live in the App project.

#### Recommended fix

Move them to:

```text
HumanFortress.Jobs
```

or, if some are runtime composition only:

```text
HumanFortress.Runtime
```

App should retain only:

```text
Job panels
Work drawer
Debug overlays
Input mappings
```

---

### H2. PathService and MovementExecutor are still owned per job system

Mining and Transport each create their own path and movement infrastructure:

```text
MiningJobSystem
  new PathService(...)
  new WorldNavigationView(...)
  MiningJobExecutor
    new MovementExecutor(paths)

TransportJobSystem
  new PathService(...)
  new WorldNavigationView(...)
  TransportJobExecutor
    new MovementExecutor(paths)
```

#### Why it matters

This fragments movement ownership:

```text
Separate path budgets
Separate path caches
Separate movement state
Separate movement statistics
Weak multi-agent conflict handling
No single authority for movement reservations
```

This becomes a serious issue once multiple systems move the same entities.

#### Recommended fix

Create runtime-wide services:

```text
NavigationService
  owns PathService
  owns path budget
  owns path cache
  owns path stats

MovementSystem
  owns active movement states
  receives MoveIntent
  resolves occupancy/reservation conflicts
  emits movement diffs
```

Jobs should request movement; they should not own movement execution.

---

### H3. Diff priority semantics are inconsistent

Core `DiffLog` appears to treat lower numeric priority as stronger:

```text
candidate.Priority < incumbent.Priority
```

But `ItemsDiff.GetSortKey()` inverts priority using:

```text
255 - Priority
```

which implies higher priority may come first.

#### Why it matters

If priority semantics differ across diff systems, same-tick conflicts will be very hard to reason about.

This is dangerous for:

```text
items
jobs
movement
construction
combat
fire
fluid
collapse
modded systems
```

#### Recommended fix

Define a single project-wide rule:

```text
Option A: larger priority value wins
Option B: smaller priority value wins
```

Then update:

```text
DiffLog
ItemsDiff
CreaturesDiff
Reservations
Command queue tie-breakers
Job scheduling
Merge tables
```

Use tests to lock this behavior.

---

### H4. System precedence is still hardcoded in DiffLog

Current core diff precedence contains hardcoded system strings:

```text
Jobs.Mining
Jobs.Haul / Jobs.Transport
Jobs.Construction
else
```

#### Why it matters

This will not scale to:

```text
Combat
Fluids
Fire
Collapse
Doors
Machines
AI panic movement
Incidents
Modded systems
```

#### Recommended fix

Introduce a deterministic system order registry:

```text
SystemOrderRegistry
  SystemId
  StageId
  StableOrder
  Layer
  ConflictGroup
```

This can be static at first, data-driven later.

---

### H5. TickScheduler is still a coarse `Parallel.ForEach` scheduler

The scheduler runs all `ReadTick` methods in parallel:

```text
Parallel.ForEach(systems, system => system.ReadTick(tick))
```

This assumes all `ReadTick` methods are side-effect free, but the code does not enforce it.

#### Why it matters

Future systems may mutate internal shared buffers, caches, queues, or manager state in ReadTick. This can cause race conditions or nondeterminism.

#### Recommended long-term direction

Introduce:

```text
SimulationStageGraph
  StageId
  Systems
  ReadSets
  WriteSets
  Dependencies
  ParallelMode
  PartitioningMode
```

The current scheduler is acceptable as a v1 scheduler but should not be considered the final concurrency model.

---

### H6. World mutation remains too accessible

`World.SetTile` is public and only guarded by a comment:

```text
Must be called only during Write phase.
```

`Chunk.SetTile` also depends on convention plus a lock.

#### Why it matters

Any code with a `World` reference can mutate authoritative state if it calls public methods directly.

This weakens the command/diff/commit model.

#### Recommended fix

Introduce write contexts:

```text
WorldReadView
WorldWriteContext
ChunkWriteToken
```

Only the simulation commit/applicator stage should receive write access.

---

### H7. Dirty tile tracking is still incomplete

`Chunk.MarkTileDirty` still effectively bumps connectivity version and contains a TODO for a real dirty tile system.

#### Why it matters

Large simulation games need precise dirty tracking for:

```text
navigation
render snapshots
line of sight
support/collapse
fields
fluid
temperature
debug overlays
path cache invalidation
```

#### Recommended fix

Add explicit dirty structures:

```text
DirtyTileSet
DirtyChunkSet
DirtyZSliceSet
DirtyNavRegionSet
DirtyRenderRegionSet
```

---

## 6. Medium Severity Findings

### M1. Tests exist, but they are custom executable tests rather than a standard test framework

`HumanFortress.App.Tests` is an executable project.

Its `Program.cs` manually calls test groups and returns failure based on output text.

This is better than having tests inside the App executable, but it is still not a mature test setup.

#### Recommended fix

Move to one of:

```text
xUnit
NUnit
MSTest
```

Then use:

```bash
dotnet test
```

Add separate projects:

```text
HumanFortress.Core.Tests
HumanFortress.Runtime.Tests
HumanFortress.Simulation.Tests
HumanFortress.Navigation.Tests
HumanFortress.Jobs.Tests
HumanFortress.Content.Tests
HumanFortress.Determinism.Tests
```

---

### M2. `RunTests.sh` is machine-specific

The script uses:

```text
/opt/homebrew/opt/dotnet@8/bin/dotnet
```

This is not portable.

#### Recommended fix

Use:

```bash
dotnet test
```

or a repo-local test script that locates `dotnet` through PATH.

---

### M3. Contracts live in one assembly but use another namespace

Navigation contracts physically live under `HumanFortress.Contracts`, but use:

```csharp
namespace HumanFortress.Navigation;
```

This may be intentional for compatibility, but it is confusing.

#### Recommended fix

Either document this explicitly or rename namespaces to:

```csharp
HumanFortress.Contracts.Navigation
```

---

### M4. RngStreamManager exists but is not yet a true gameplay dependency

The infrastructure exists, but most systems do not appear to consume named streams.

#### Recommended fix

Make RNG access explicit in simulation contexts:

```text
SimulationTickContext.Rng
IRngStreamProvider
```

Also remove global suppression of `CA5394` over time.

---

### M5. EventBus appears unused in gameplay flow

EventBus exists, but no clear production `Publish` usage was observed during this audit.

#### Recommended fix

Either:

```text
Use it for post-commit event streams
```

or:

```text
Do not treat it as an active architecture pillar yet.
```

If used, ensure deterministic event ordering.

---

## 7. Content System Findings

### 7.1 Two content registries still coexist

There is a legacy registry:

```text
HumanFortress.Core.Content.ContentRegistry
```

and a structured registry:

```text
HumanFortress.Core.Content.Registry.ContentRegistry
```

`ContentLoadCoordinator` explicitly coordinates both.

This is an acceptable transitional strategy, but it must not become permanent.

### 7.2 Continue-on-error mode is risky

Structured registry load failures can be logged while the game continues with the legacy registry.

This is useful during migration, but dangerous for production or CI.

#### Recommended fix

Add a strict content mode:

```text
StrictContentMode = true
structured registry failure = fail build / fail boot
```

### 7.3 Long-term goal

Replace the two-registry system with one authoritative content snapshot:

```text
ContentRegistrySnapshot
  Materials
  Geology
  Zones
  Items
  Creatures
  Recipes
  Constructions
  Tunings
  Tags
  Runtime IDs
  Content hash
```

---

## 8. UI / Rendering Findings

### 8.1 UI is much more modular than before

`FortressState` now delegates to many controller/renderer classes. This is good.

### 8.2 But UI still directly consumes live state

Rendering and overlays still receive live `World` and runtime job systems.

Examples of current live dependencies:

```text
FortressRuntimeAccess.World
FortressRuntimeAccess.MiningJobs
FortressRuntimeAccess.TransportJobs
FortressUiOverlayRenderer.Render(... world ...)
UiRenderer.DrawDrawer(... world ...)
DrawOrderHighlights(... world ...)
DrawWorkshopsOverlay(... world ...)
DebugPageOverlayRenderer.PostDrawItemsPage(... world ...)
```

#### Recommended long-term UI model

```text
UI input:
  keyboard/mouse
  -> UiAction
  -> CommandFactory
  -> Runtime.EnqueueCommand

UI rendering:
  RenderSnapshot
  DebugSnapshotBundle
  UiModel
  -> Components
  -> SadConsole backend
```

No UI component should need live `World`.

---

## 9. World / Entity / Manager Findings

### 9.1 World is still a service locator

`World` exposes managers:

```text
Creatures
Items
Orders
Stockpiles
Reservations
Zones
```

This is convenient but weakens boundaries.

### 9.2 Managers return live object references

`CreatureManager.GetAllInstances()` returns a list copy, but the objects themselves are likely mutable live instances.

This is not a true immutable snapshot.

### 9.3 Entity IDs are still GUID-based internally

Deterministic GUID generation is an improvement, but diff encoding still truncates GUIDs to 32-bit entity IDs.

#### Recommended long-term entity identity

```csharp
public readonly record struct EntityId(ulong Value);
```

Use GUIDs only as external/debug IDs if needed.

---

## 10. Navigation / Movement Findings

### 10.1 Navigation module boundary is improved

Navigation depends on contracts and no longer directly on Simulation.

### 10.2 Movement ownership is still fragmented

MovementExecutor is still created inside job executors.

This prevents a single deterministic movement/reservation layer from managing conflicts.

#### Recommended future model

```text
Job systems:
  produce MoveIntent / WorkIntent

MovementSystem:
  owns all movement state
  performs reservation-aware step resolution
  emits MoveCreature diffs

ReservationSystem:
  resolves tile/entity conflicts deterministically
```

---

## 11. Recommended Target Architecture

Long-term structure:

```text
src/
  HumanFortress.Foundation/        # or keep Core
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

tests/
  HumanFortress.Core.Tests/
  HumanFortress.Content.Tests/
  HumanFortress.Runtime.Tests/
  HumanFortress.Simulation.Tests/
  HumanFortress.Navigation.Tests/
  HumanFortress.Jobs.Tests/
  HumanFortress.Determinism.Tests/
  HumanFortress.Integration.Tests/
```

Practical next structure:

```text
HumanFortress.Core
HumanFortress.Contracts
HumanFortress.Content
HumanFortress.Simulation
HumanFortress.Navigation
HumanFortress.Jobs
HumanFortress.WorldGen
HumanFortress.Runtime
HumanFortress.UI
HumanFortress.App
HumanFortress.*.Tests
```

AI, Combat, Economy, Incidents, Save, Rendering can be introduced later.

---

## 12. Recommended Dependency Direction

Desired dependency direction:

```text
Core/Foundation
  ↓
Contracts
  ↓
Content / World / Simulation / Navigation / Jobs
  ↓
Runtime
  ↓
UI / Rendering
  ↓
App
```

Key rules:

```text
App depends inward.
Inner layers never depend on App.

Runtime wires modules.
Modules should not randomly create each other.

UI reads snapshots and emits commands.
UI must not mutate live World.

Jobs own job rules.
App only displays job UI.

Navigation owns pathfinding.
Jobs request paths, but Navigation does not know job semantics.

Simulation owns tick, command execution, diff merge, and commit.
World owns authoritative state.
```

---

## 13. Migration Roadmap

### Phase 0: Build and project-reference verification

First action:

```bash
dotnet build HumanFortress.sln
```

Specifically verify:

```text
HumanFortress.Content builds correctly.
```

Resolve the Content project reference / dependency direction issue before large refactors.

---

### Phase 1: Move concrete runtime host out of App

Move from:

```text
HumanFortress.App.Runtime.SimulationRuntimeHost
HumanFortress.App.Runtime.SimulationRuntimeSystems
```

to:

```text
HumanFortress.Runtime
```

Preserve behavior.

Do not touch UI layout.

Do not change job logic.

---

### Phase 2: Move remaining App/Jobs wrappers out of App

Move:

```text
MiningJobSystem
TransportJobSystem
ConstructionJobSystem
CraftJobSystem
UnifiedJobsOrchestrator
SchedulerTunings
WorkshopTunings
ProfessionAssignments
SanitizeSystem
```

to either:

```text
HumanFortress.Jobs
```

or:

```text
HumanFortress.Runtime
```

depending on whether the class is gameplay logic or composition.

---

### Phase 3: Build runtime-wide NavigationService and MovementSystem

Introduce:

```text
NavigationService
  owns PathService
  owns path budget
  owns path cache

MovementSystem
  owns movement state
  resolves movement intents
  emits movement diffs
```

Then remove per-job MovementExecutor ownership.

---

### Phase 4: Replace live UI state access with snapshots

Refactor `FortressRuntimeAccess` to expose:

```text
SimulationStatus
Command sink
Render snapshot provider
Debug snapshot provider
Event snapshot provider
```

Remove exposure of:

```text
World
NavigationManager
Concrete job systems
```

---

### Phase 5: Finish content registry migration

Unify legacy and structured registries.

Add strict mode:

```text
CI/Release: structured content load failure is fatal
Development: optional fallback allowed temporarily
```

---

### Phase 6: Standardize tests

Replace custom executable-style tests with standard:

```text
xUnit / NUnit / MSTest
dotnet test
```

Split test projects by module.

Add determinism harness:

```text
fixed seed
fixed command log
run N ticks
hash world state
compare expected hash
```

---

## 14. First 5 Concrete Tasks for Codex

If Codex is asked to implement improvements, start with these small tasks.

### Task 1: Verify and report Content project build status

Do not change code first.

Inspect:

```text
HumanFortress.Content.csproj
HumanFortress.Content/Definitions/*
```

Report whether the project compiles and whether its dependencies are architecturally valid.

---

### Task 2: Move SimulationRuntimeHost and SimulationRuntimeSystems out of App.Runtime

Scope:

```text
Move concrete runtime composition into HumanFortress.Runtime.
Preserve behavior.
Do not change UI.
Do not change job internals.
```

Expected risk:

```text
Project references may need updating.
App should no longer own simulation composition.
```

---

### Task 3: Move App/Jobs wrappers to Jobs or Runtime

Scope:

```text
Move job tick-system wrappers and tunings out of App.
Keep App job UI/debug classes in App.
```

---

### Task 4: Add DebugSnapshot facade to FortressRuntimeAccess

Scope:

```text
Introduce snapshot-style access for job debug data.
Do not yet remove all live World reads.
```

This is a transition step toward snapshot-only UI.

---

### Task 5: Normalize diff priority semantics

Scope:

```text
Document and test whether lower or higher priority wins.
Update DiffLog and ItemsDiff to match.
Add regression tests.
```

---

## 15. Areas Codex Should Not Touch Yet

Avoid broad changes to these areas until foundations are stable:

```text
Combat
AI
Incidents
Save/load
Full ECS conversion
GPU acceleration
Full UI rewrite
Renderer replacement
Hierarchical pathfinding implementation
Flow fields
Large actor movement
```

These are important, but they should wait until Runtime / Jobs / Content / UI boundaries are cleaner.

---

## 16. Open Design Questions

The following need human decisions before implementation.

### Q1. Should `Content` be allowed to depend on `Simulation`?

Recommendation:

```text
No. Content should own definitions, not runtime simulation types.
```

### Q2. Should priority use higher-is-stronger or lower-is-stronger?

Recommendation:

```text
Pick one globally.
```

### Q3. Should Runtime expose live World to UI during transition?

Recommendation:

```text
Temporary only.
Plan removal through snapshots.
```

### Q4. Should job tick-system wrappers live in Jobs or Runtime?

Recommendation:

```text
Gameplay rule wrappers live in Jobs.
Pure composition lives in Runtime.
```

### Q5. Should MovementExecutor remain job-local?

Recommendation:

```text
No. Move toward a central MovementSystem.
```

### Q6. Should dual content registries remain?

Recommendation:

```text
Only temporarily. Add strict CI mode and a migration plan.
```

---

## 17. Final Assessment

The `main` branch is on a good path.

It has already fixed some of the most dangerous earlier architecture problems:

```text
Command execution moved into simulation tick.
Navigation dependency inversion improved.
FortressState shrank significantly.
Jobs logic partially moved out of App.
Runtime pipeline exists.
Tests exist outside the main App executable.
```

But it is not yet a mature layered architecture.

The current architecture is best described as:

```text
A promising mid-refactor state with correct direction, but incomplete boundaries.
```

The biggest danger is allowing the half-migrated architecture to become permanent.

The next work should focus on completing the boundary migrations:

```text
1. Validate and fix Content project dependencies.
2. Move concrete runtime composition out of App.
3. Move remaining App/Jobs wrappers out of App.
4. Replace UI live-world access with snapshots.
5. Centralize movement/path services.
6. Standardize tests and determinism harness.
```

Only after these foundations are stable should the project add large new systems such as AI, combat, save/load, incidents, or advanced multithreading.

---

## 18. One-Sentence Summary

HumanFortress main branch has made real architectural progress, but it is still in a half-refactored state: Runtime, Jobs, Content, Contracts, and UI boundaries exist, yet App still owns too much composition, UI still reads live world state, Content dependencies look suspicious, and movement/path ownership remains fragmented.
