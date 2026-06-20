# HumanFortress Architecture Overview

Updated: 2026-06-19
Status: current overview plus target boundaries

This document describes the current codebase shape. Older architecture docs described a complete deterministic, data-driven fortress simulator. That is still the target, but the implementation is currently in a transitional refactor.

## Goals

- Deterministic simulation at fixed 50 TPS.
- UI enqueues commands; simulation applies them inside the tick pipeline.
- Runtime state mutates through command targets and diff applicators, not from UI event handlers.
- Content is JSON-driven and loaded through a single Content-owned bootstrap facade.
- App remains the SadConsole host, but simulation composition is moving toward Runtime.

## Current Solution Projects

Current solution projects:

- `HumanFortress.Contracts` - shared DTOs and contract interfaces.
- `HumanFortress.Core` - foundational commands, events, random, time, and diagnostics.
- `HumanFortress.Content` - content loading facade, runtime registry implementation, static definition loaders, and runtime content snapshot capture.
- `HumanFortress.Simulation` - world, tiles, orders, items, creatures, stockpiles, zones, diff applicators.
- `HumanFortress.Navigation` - pathfinding and navigation caches; no direct dependency on Simulation.
- `HumanFortress.Jobs` - transport, mining, construction, and craft executor cores plus domain helpers.
- `HumanFortress.Runtime` - command stage, runtime command implementations/targets, tick pipeline, navigation adapter, session factory/core, generic runtime host, and concrete fortress runtime composition.
- `HumanFortress.WorldGen` - world generation.
- `HumanFortress.App` - SadConsole app, game states, UI, logger binding, and App-specific delegates.
- `HumanFortress.App.Tests` - lightweight regression/smoke test executable.

Current broad dependency direction:

```text
Contracts
  <- Core
  <- Simulation / Navigation / Jobs / Content / Runtime
  <- App / Tests
```

There are still transitional dependencies and a few compatibility namespaces, but Runtime/Jobs and Content registry ownership now use their module namespaces in active source.

## Startup And Content Loading

Current startup path:

```text
Program
  -> FortressContentLoader.Load(baseDir, includeCoreCatalogs: false)
  -> Logger / diagnostics setup
  -> SadConsole Builder
  -> GameStateManager
  -> AppStateRegistration
```

Fortress session content path:

```text
GameStateManager.InitializeWorld
  -> SimulationRuntimeSessionFactory.CreateNew
  -> World + Navigation creation
  -> SimulationWorldContentLoader.LoadCoreContent
  -> FortressContentLoader.Load(baseDir)
  -> world.Items.SetDefinitionCatalog(...)
  -> world.Creatures.SetDefinitionCatalog(...)
  -> FortressRuntimeContentSnapshotLoader.ApplyCoreData(...)
  -> snapshot.ZoneDefinitions registered into world zones
  -> active runtime content snapshot reused by runtime composition
```

`HumanFortress.Content.Loading.FortressContentLoader` is the current content boundary. It resolves published/source paths for:

- `content/`
- `data/core/`
- individual files under `content/registries/`

It also returns `FortressContentIssue` entries for missing directories, empty catalogs, registry errors, and structured-registry warnings. App logs these through `FortressContentIssueLogger`.

## Runtime And Tick Pipeline

Current runtime ownership:

```text
GameStateManager
  owns app state transitions, scheduler handle, command queue, event bus, diff logs,
  and one SimulationRuntimeSession<SimulationRuntimeHost<SimulationRuntimeSystems>>.

HumanFortress.Runtime
  owns SimulationRuntimeSessionFactory, SimulationRuntimeHost<TSystems>,
  SimulationRuntimeHostCore, SimulationTickPipeline, SimulationCommandStage,
  command target helpers, SimulationRuntimeSystems, concrete runtime system
  factories, dependency groups, planning groups, job-system groups,
  FortressRuntimeHostFactory, and FortressRuntimeStartup.

HumanFortress.App.Runtime
  owns App logger callback binding, the optional auto-dig command delegate,
  UI/session bootstrap glue, and SadConsole lifetime integration.
```

Current tick shape:

```text
PreTick
  SimulationCommandStage executes queued commands for the current tick.

Read phase
  TickScheduler calls ITick.ReadTick in parallel.

Barrier
  TickScheduler emits BarrierReached.

Write phase
  TickScheduler calls ITick.WriteTick serially.

PostTick
  SimulationTickPipeline applies ItemsDiffLog pre-simulation operations,
  DiffLog terrain/entity operations,
  CreaturesDiffLog operations,
  item additions,
  and navigation rebuilds for dirty chunks.
```

This is not yet the full nine-stage `UPDATE_ORDER.md` model. The important current invariant is:

- commands execute before read systems;
- planning is read phase;
- authoritative mutation happens in write/post-tick applicators;
- navigation rebuild happens after terrain/entity diffs.

## Jobs Boundary

Current job composition:

```text
FortressRuntimeSystemsFactory
  builds the SimulationRuntimeSystems collection from:
    HaulingSystem
    TransportRequestQueue
    MiningSystem
    ConstructionSystem
    BuildableConstructionSystem
    ConstructionMaterialsPlanner
    CraftPlanner
    TransportJobSystem
    MiningJobSystem
    ConstructionJobSystem
    CraftJobSystem
    UnifiedJobsOrchestrator
    SanitizeSystem
```

`HumanFortress.Jobs` now owns most executor cores for:

- transport;
- mining;
- construction;
- crafting.

`HumanFortress.Jobs` also owns scheduler/workshop tuning types, worker-selection strategy, profession assignment/selection state, concrete job diff emitters, profession/craft adapters, callback job loggers, mining drop/tuning resolution, construction terrain-material resolution, `UnifiedJobsOrchestrator`, and the low-frequency `SanitizeSystem` safety net. Profession contract DTOs/interfaces compile from `HumanFortress.Contracts.Jobs`, and profession registry JSON loading compiles from `HumanFortress.Content.Definitions`.

`HumanFortress.Runtime` now owns the tick-facing transport/mining/construction/craft job-system wrappers in `HumanFortress.Runtime.Jobs`, plus the concrete fortress runtime system collection/factory/grouping layer. `HumanFortress.Content` owns profession registry file loading from `professions.json`.

App still owns UI bootstrap and App-specific delegates: it passes logging callbacks, supplies the optional auto-dig command implementation, and binds construction workshop-completion notifications into the UI.

The App runtime composition layer is now split into smaller migration points:

- Runtime-owned `FortressRuntimeHostFactory` creates the generic Runtime host for a fortress world.
- Runtime-owned `FortressRuntimeDependencies` groups construction/recipe catalogs, scheduler/workshop tunings, and workforce dependencies.
- Runtime-owned `FortressRuntimePlanningSystems` builds planners and the shared `TransportRequestQueue`.
- Runtime-owned `FortressRuntimeJobSystems` builds Runtime-owned job-system wrappers over Jobs-owned executor cores.
- Runtime-owned `FortressRuntimeSystemsFactory` assembles the concrete runtime system collection.
- Runtime-owned `FortressRuntimeStartup` handles initial-worker/profession setup and invokes an optional App-provided auto-dig delegate.

The composition center has moved to Runtime. The remaining App coupling is mostly logging callback injection, optional App command delegates, UI completion binding, live debug/UI access, and compatibility namespaces.

Target direction:

```text
App
  UI and platform host only

Runtime
  simulation session lifecycle and generic host

Jobs
  job planning/execution domain logic and module-owned tests

App
  supplies concrete systems/adapters until those composition seams move down
```

## UI Boundary

Current UI implementation is SadConsole-first and has a real `UiStore`, input services, drawers, quick menus, overlays, and placement tools.

Important current limitation:

- UI still reads live `World` and concrete job/runtime systems in multiple renderer/panel paths.

Target direction remains:

```text
Simulation -> immutable/debug snapshots -> UI
UI -> commands -> Runtime command stage -> Simulation
```

Until snapshot facades are added, documents that say "UI reads snapshots only" should be read as target architecture, not current fact.

## Content Boundary

Current runtime content is not a compiled `.cpack` pipeline.

Current sources:

- `content/registries/*.json`
- `content/schemas/*.schema.json`
- `data/core/items/*.json`
- `data/core/creatures/*.json`
- `data/core/workshops/*.json`
- `data/core/recipes/*.json`
- `data/core/placeable/*.json`

Current loaders:

- `FortressContentLoader`
- `RuntimeContentRegistryLoader`
- `HumanFortress.Content.Registry.ContentRegistry`
- `CoreContentCatalogLoader`
- `ItemDefinitionCatalogLoader`
- `CreatureDefinitionCatalogLoader`
- `CoreDataRegistryLoader` (Content-owned)

The old `.cpack` content build pipeline remains a future design archived at `docs/archive/legacy/CONTENT_BUILD_PIPELINE_FUTURE.md`.

## Current Known Gaps

- Generic runtime host/lifecycle and concrete fortress system construction now live in `HumanFortress.Runtime`.
- Runtime-owned job wrappers now exist around Jobs-owned executor cores; App still owns UI/bootstrap binding, logger callback injection, and App-specific delegates.
- UI still reads live runtime state and concrete systems.
- The old legacy content registry source has been deleted; normal bootstrap loads the structured runtime registry only.
- `ConstructionRegistry` and `RecipeRegistry` singleton compatibility classes have been deleted; construction/recipe/material/terrain/geology/biome definitions, terrain bit-layout DTOs, alias/migration DTOs, catalog interfaces, immutable catalog stores, runtime geology catalog interface, construction/placeable tuning types, fixed-point material primitives, and content version/snapshot/validation result types now compile from `HumanFortress.Contracts.Content.Registry`.
- `CoreDataRegistryLoader` now compiles from `HumanFortress.Content.Definitions`, and the structured `ContentRegistry` implementation compiles from `HumanFortress.Content.Registry`; the remaining content gaps are strict fail-fast policy, richer diagnostics/debug surfaces, and the future compiled pack pipeline.
- Full save/load, storyteller, combat, full snapshot rendering, and compiled content packs are not complete current systems.

## High-Signal References

- `../planning/ARCHITECTURE_REFACTOR_MASTER_PLAN.md`
- `../planning/REFACTOR_BATCH_PROGRESS.md`
- `../planning/REFACTOR_PITFALLS_AND_LESSONS.md`
- `../content/CONTENT_SYSTEM.md`
- `../simulation/WORK_AND_JOBS_SYSTEM.md`
- `../simulation/TRANSPORT_SYSTEM.md`
- `UPDATE_ORDER.md`
