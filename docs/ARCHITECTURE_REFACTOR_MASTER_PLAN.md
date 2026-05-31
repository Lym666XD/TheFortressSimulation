# HumanFortress Architecture Refactor Master Plan

Date: 2026-05-31
Status: working refactor plan
Sources merged:
- `docs/architecture issue chaatgpt.md`
- `docs/architectureissue claude.txt`

This document merges both prior architecture reviews into one actionable refactor plan. It is intentionally strict: the target is a long-lived, deterministic, moddable simulation game, not a short prototype.

## Executive Verdict

The project already has several strong ideas:

- separated projects for Core, Simulation, Navigation, WorldGen, and App;
- a fixed tick scheduler with read/write phases;
- diff logs, command queue, event bus, deterministic RNG primitives;
- content registries and JSON-driven data;
- navigation caches and path services;
- early render snapshot direction;
- job planning and execution for mining, hauling, construction, and crafting.

The problem is that these ideas are not enforced by boundaries. Runtime composition, UI, world generation, content loading, job execution, command application, navigation rebuild, debug tooling, and direct world mutation are mixed across App classes.

The current codebase is best described as a working prototype with several professional architecture pieces present but bypassed. The refactor should first stop new architectural drift, then extract a headless deterministic runtime, then move gameplay systems out of App.

## Current Critical Findings

### P0: Runtime Ownership Is in the Wrong Place

`GameStateManager` currently owns or coordinates the active `World`, `TickScheduler`, `CommandQueue`, `EventBus`, RNG manager, diff logs, navigation manager, job planners, job executors, content loading, debug caches, initial worker spawning, auto-dig setup, and post-tick diff application.

That makes it a simulation runtime host, not a game-state manager.

Target:

```text
GameStateManager
  owns app-level transitions only

FortressRuntime / SimulationHost
  owns world, tick pipeline, commands, systems, RNG, diffs, navigation, snapshots
```

### P0: `FortressState` Is a God Screen

`FortressState` is currently responsible for UI surfaces, camera, cursor, map generation, filling the simulation world, snapshot building, navigation rebuild, UI callbacks, auto-dig debug orders, input handling, order submission, direct world reads, and rendering.

Target split:

```text
FortressScreen
  SadConsole lifecycle and surface ownership

FortressInputController
  device input -> UI action -> command

FortressUiCoordinator
  drawer/panel/tool state

FortressRenderer
  immutable snapshot -> visual output

FortressRuntime
  world creation, simulation, navigation, jobs, snapshot publishing
```

### P0: Command Execution Races the Tick Thread

The tick scheduler runs on a background simulation thread. `CommandQueue.ExecuteCommands` currently runs from the app update path on the render/main thread.

This violates the intended pipeline:

```text
input thread: enqueue only
simulation thread: ApplyCommands stage
```

Fix:

1. UI and App may only enqueue commands.
2. Commands execute inside the simulation tick, before planning/write stages.
3. Command execution must receive the real current tick.
4. Commands must not directly mutate world outside the authorized stage.

### P0: Direct World Mutation Bypasses the Architecture

Examples found in the reviews and current code:

- mining UI directly enqueues into `world.Orders`;
- craft jobs directly remove inputs and spawn outputs;
- construction jobs directly remove or move items in several paths;
- debug/UI paths read and mutate live runtime state;
- navigation queries can rebuild navigation data on demand.

Fix:

```text
Systems produce typed diffs or commands.
Commit/applicator owns authoritative writes.
Derived rebuilds happen after commit.
UI consumes snapshots only.
```

### P0: Determinism Is Aspirational, Not Guaranteed

Existing deterministic pieces are good but underused:

- `DeterministicRng`;
- `RngStreamManager`;
- `DiffLog`;
- stable tick scheduler;
- deterministic pathfinding pieces.

Current blockers:

- `SystemId.GetHashCode()` is used in a diff sort key;
- many authoritative IDs use `Guid.NewGuid()`;
- command IDs are random GUIDs and then used for ordering;
- world-gen UI uses `Environment.TickCount` and `new Random()`;
- `RngStreamManager` is constructed but not used by most systems;
- `SimulationContext.CurrentTick` returns `0`;
- diff applicators cover only part of the authoritative state;
- `EventBus` infrastructure exists but is effectively unused.

Fix:

1. Replace unstable hash usage with explicit stable string hash or numeric system order.
2. Replace authoritative GUIDs with deterministic `EntityId`, `ItemId`, `PlaceableId`, `CommandSeq`.
3. Use named RNG streams for all sim branches.
4. Move commands into the tick pipeline.
5. Add a headless deterministic replay test.

### P0: Job Systems Live in App

Mining, hauling, construction, crafting, scheduler tunings, and profession assignment live under `HumanFortress.App.Jobs`. These systems are core simulation logic and should run headlessly.

Target:

```text
HumanFortress.Jobs
  job scheduler, job state, job executors, profession assignment

HumanFortress.App
  UI host only
```

The move requires first fixing Navigation dependencies so Jobs can depend on pathfinding without pulling SadConsole/MonoGame.

### P0: Navigation Depends on Simulation World Directly

`NavigationManager` directly references `HumanFortress.Simulation.World.World` even though `IWorldNavigationView` already exists.

Target:

```text
Navigation
  depends on Foundation/Contracts only
  consumes IWorldNavigationView

World or Runtime
  implements IWorldNavigationView adapter
```

Also remove on-demand rebuild from query paths. Rebuild must be scheduled in `RebuildDerived`, not triggered by path queries.

### P1: Content Registry Is Split

There are two `ContentRegistry` classes:

- `HumanFortress.Core.Content.ContentRegistry`
- `HumanFortress.Core.Content.Registry.ContentRegistry`

They use different loading styles and different responsibilities. Some consumers use the older loaded registry, while others receive the newer singleton that is not consistently loaded in production.

Fix:

1. Pick one content model.
2. Move it into `HumanFortress.Content`.
3. Content loading happens once in `ContentBootstrapper`.
4. Runtime receives immutable registry snapshots.
5. Managers do not discover/load content themselves.

### P1: Rendering Snapshot Is Not Yet a Real Boundary

The project has `RenderSnapshot`, but UI rendering still reads live world state in many panels. `RenderSnapshotBuilder` also contains backend-like visual mapping and lives under Simulation.

Target:

```text
World/Snapshot
  semantic data: terrain kind, materials, items, actors, jobs, designations

Rendering
  visual mapping: glyphs, colors, palette, draw order

UI
  panel state + presentation models from snapshots
```

### P1: Tests Are Not Structured

There is no formal test project. Tests are embedded in App as `PhaseTests` and `TestRunner`, which pulls testing into a WinExe and makes headless CI awkward.

Fix:

```text
tests/HumanFortress.Foundation.Tests
tests/HumanFortress.Content.Tests
tests/HumanFortress.Simulation.Tests
tests/HumanFortress.Navigation.Tests
tests/HumanFortress.Runtime.Tests
```

Minimum required tests:

- diff merge ordering;
- command queue ordering;
- deterministic RNG stream restore;
- pathfinding deterministic hash;
- content registry validation;
- fixed seed simulation hash;
- save/load round trip hash.

### P1: Save/Load Is Designed but Not Implemented

`SAVE_FORMAT.md` is strong and should remain the target. Implementation should start with a minimal vertical slice:

1. manifest with engine/content hash;
2. world/chunk terrain snapshot;
3. items, placeables, creatures;
4. job queues and reservations;
5. RNG stream states;
6. reload and hash equality at the same tick.

Do not persist caches: navigation cache, path cache, spatial indexes, render snapshots, stockpile cached lists.

### P2: Build Hygiene and Project Hygiene

Issues:

- duplicate/stale solution files;
- inconsistent warnings-as-errors policy;
- global warning suppressions hide determinism risks;
- App is always `win-x64` self-contained;
- package version split for `TheSadRogue.Primitives`;
- obsolete item fields are still used;
- hot-path `Console.WriteLine` logging is common.

Fix:

1. Keep one active solution.
2. Move publish settings into a publish profile.
3. Turn determinism-related warnings into errors.
4. Align package versions.
5. Replace obsolete item reservation/carry fields.
6. Add structured logging with categories and levels.

## Recommended Target Architecture

The proposed architecture is directionally strong:

```text
HumanFortress.Foundation
  deterministic primitives, IDs, RNG, small utilities

HumanFortress.Contracts
  interfaces and DTOs across modules

HumanFortress.Content
  content loading, schema validation, registries

HumanFortress.World
  authoritative world/chunks/entities/managers

HumanFortress.Simulation
  tick pipeline, diff, event, stage graph, command execution

HumanFortress.Navigation
  pathfinding, movement profile, region graph, flow fields

HumanFortress.Jobs
  job scheduling, mining/hauling/construction/crafting

HumanFortress.AI
  needs, utility decision, memory, schedules

HumanFortress.Save
  persistence, migration, replay

HumanFortress.Runtime
  composition root, simulation host, headless session

HumanFortress.UI
  MVU state, components, panels, input mapping

HumanFortress.Rendering
  renderer backend abstraction

HumanFortress.App
  Program, SadConsole host, app states
```

The main caution is granularity. This is a good end-state, but creating all assemblies before boundaries are clean will add friction. Extract in dependency order and only split a project once the ownership boundary is enforced.

### Recommended Dependency Direction

```text
Foundation
  -> no project dependencies

Contracts
  -> Foundation

Content
  -> Foundation, Contracts

World
  -> Foundation, Contracts, Content

Simulation
  -> Foundation, Contracts, World

Navigation
  -> Foundation, Contracts
  -> no direct World dependency

Jobs
  -> Foundation, Contracts, World, Simulation, Navigation

AI
  -> Foundation, Contracts, World, Simulation, Jobs

Save
  -> Foundation, Contracts, Content, World, Simulation, Jobs
  -> should serialize DTO/snapshots, not runtime services

Runtime
  -> Content, World, Simulation, Navigation, Jobs, AI, Save

Rendering
  -> Foundation, Contracts
  -> consumes render/presentation snapshots

UI
  -> Foundation, Contracts, Rendering
  -> emits commands, reads presentation snapshots

App
  -> Runtime, UI, Rendering
  -> owns SadConsole/MonoGame host
```

Rules:

- Foundation never references game modules.
- Contracts must stay small; it must not become a dumping ground.
- Navigation does not know `World`.
- UI does not know authoritative `World`.
- App does not contain simulation rules.
- Runtime is the only composition root.

## Module Notes

### Foundation

Move deterministic primitives here:

- stable hash;
- deterministic IDs;
- deterministic RNG;
- fixed-point values;
- small value objects;
- tick/time primitives.

Current candidates:

- `Core/Random/*`
- `Core/Content/FixedPoint.cs`
- stable ID types to be added.

### Contracts

Use only for stable cross-module interfaces and DTOs:

- `IWorldNavigationView`;
- command DTO contracts;
- snapshot DTOs;
- event DTOs;
- content ID structs;
- save DTO contracts.

Avoid putting large domain behavior here.

### Content

Own all content loading:

- schema validation;
- alias resolution;
- registries;
- packset signatures;
- content hash;
- generated runtime handles.

Content should produce immutable registry snapshots consumed by Runtime and World.

### World

Own authoritative state:

- chunks;
- tile layers;
- items;
- creatures;
- placeables;
- zones;
- stockpiles;
- reservations.

World should expose controlled read views and write methods that are only reachable through simulation commit contexts.

### Simulation

Own the deterministic tick:

- stage graph;
- command execution stage;
- diff collection and merge;
- event stream;
- derived rebuild scheduling;
- single-thread and multi-thread modes;
- deterministic diagnostics.

Simulation should not know SadConsole, rendering, or UI.

### Navigation

Own algorithms:

- A*;
- path cache;
- movement executor;
- region graph;
- flow fields later.

It consumes navigation views and outputs paths/movement updates. It does not rebuild world state on query.

### Jobs

Own gameplay labor:

- mining;
- hauling;
- construction;
- crafting;
- profession assignment;
- job queues;
- reservation policies.

Jobs may use Navigation, but writes must go through Simulation diffs/commands.

### AI

Add after Jobs/Runtime boundaries are stable:

- needs;
- schedules;
- utility scoring;
- memory;
- behavior plans.

Do not add AI before command/diff determinism is fixed.

### Save

Own persistence and replay:

- atomic save;
- migrations;
- content hash compatibility;
- command replay;
- canonical hash;
- save/load round-trip tests.

### Runtime

The key extraction target:

- `FortressRuntime`;
- `SimulationHost`;
- `ContentBootstrapper`;
- `WorldBootstrapper`;
- `SystemCompositionRoot`;
- `HeadlessSession`;
- `RuntimeDebugService`.

App state transitions create, pause, resume, and dispose Runtime. They do not compose systems themselves.

### UI

Use MVU-style ownership:

- immutable view model in;
- UI model updated by actions;
- commands out;
- no authoritative world mutation.

### Rendering

Own render backend abstraction:

- palette/glyph/tile visual mapping;
- SadConsole adapter can remain in App initially;
- semantic snapshot in, draw commands out.

### App

Small host:

- `Program`;
- SadConsole/MonoGame bootstrap;
- top-level states;
- native host lifecycle;
- app config.

No gameplay rules.

## Migration Plan

### Phase 0: Stop Architectural Bleeding

Do now:

1. Keep one valid solution file.
2. Delete unreachable old play-state implementation.
3. Add this master refactor plan.
4. Freeze new gameplay features until command/diff/runtime boundary work begins.
5. Add CI build for the active solution.

### Phase 1: Extract Runtime Inside Existing Projects

Before creating many new assemblies, create a runtime folder/class in the current structure:

```text
HumanFortress.App/GameStates
  GameStateManager becomes app state only

HumanFortress.App/Runtime or new HumanFortress.Runtime
  FortressRuntime
  SimulationHost
  RuntimeServices
```

Move world creation, system composition, tick start/stop, post-tick diff application, navigation dirty rebuild, worker bootstrap, and debug service out of `GameStateManager`.

### Phase 2: Move Commands Into Tick Pipeline

1. UI only calls `runtime.EnqueueCommand`.
2. Tick scheduler gets an `ApplyCommands` stage.
3. `SimulationContext.CurrentTick` becomes real.
4. Mining UI uses `CreateAdvancedMiningOrderCommand`.
5. Profession weight changes become commands.

### Phase 3: Make Diff Commit Real

Choose and enforce:

```text
Preferred: systems emit typed diffs -> deterministic merge -> applicators mutate world
```

Add typed diffs for:

- terrain;
- placeables;
- items;
- creatures;
- reservations;
- workshop queues;
- zones/stockpiles.

### Phase 4: Invert Navigation

1. Move navigation contracts into Contracts.
2. Remove direct `World` field from NavigationManager.
3. World/Runtime provides `IWorldNavigationView`.
4. Remove query-time rebuild.
5. Rebuild navigation only after dirty commit.

### Phase 5: Move Jobs Out of App

After Navigation is inverted:

1. Move mining/hauling/construction/crafting executors to `HumanFortress.Jobs`.
2. Move profession assignment and scheduler tunings with them.
3. Keep App-only debug panels in UI/App.
4. Prove jobs can run in a headless test.

### Phase 6: Split Content

1. Pick the modern registry model or explicitly retire it.
2. Move registry code to `HumanFortress.Content`.
3. Load once in Runtime.
4. Inject immutable registry snapshots into systems.
5. Remove global singleton reliance where practical.

### Phase 7: Snapshot-Driven UI and Rendering

1. Runtime publishes render/presentation snapshots after commit.
2. UI panels read snapshots, not `World`.
3. Rendering owns palette/glyph visual mapping.
4. Debug tools consume explicit debug snapshots.

### Phase 8: Save/Load and Determinism CI

1. Add headless runner.
2. Add canonical snapshot hash.
3. Add fixed seed scenario.
4. Add save/load round-trip.
5. Add command replay.

## Definition of Done for the Refactor Foundation

The foundation refactor is not done until all of these are true:

- App can launch the game using `FortressRuntime`.
- A headless session can run without SadConsole/MonoGame.
- UI cannot mutate authoritative world state directly.
- Commands execute only in the simulation tick.
- Navigation no longer references Simulation `World` directly.
- Jobs no longer live in App.
- A fixed-seed headless run produces a stable hash.
- Active solution builds in CI.
- Formal tests exist outside App.
- Save/load can round-trip a minimal fortress state.

## Practical First Pull Requests

Recommended PR order:

1. Clean solution and obsolete play-state code.
2. Add `FortressRuntime` shell and move world/tick ownership.
3. Move command execution into tick.
4. Replace direct mining order enqueue with command.
5. Fix `CurrentTick`.
6. Replace unstable diff sort key.
7. Add first headless deterministic test.
8. Invert Navigation dependency.
9. Move Jobs out of App.
10. Start Content split.
