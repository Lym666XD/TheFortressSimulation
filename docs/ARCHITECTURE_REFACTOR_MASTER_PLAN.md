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

## Current Refactor Progress - 2026-06-11

Estimated architecture-foundation progress: **95%**.

This percentage tracks the refactor from prototype structure toward a maintainable deterministic simulation architecture. It is not a gameplay feature-completion estimate.

### Completed Since This Plan Was Created

- Cleaned obsolete solution/code paths and kept the active solution buildable.
- Merged the two prior architecture reviews into this master plan.
- Split much of the `FortressState` god screen into focused runtime, interaction, placement, viewport, rendering, and session helper classes.
- Moved fortress map/session initialization responsibilities out of the screen-facing surface toward runtime/session helpers.
- Converted construction material consumption to item diffs instead of direct item deletion.
- Converted construction residual item relocation to `DiffOpType.MoveItem`.
- Split construction execution into focused collaborators for diff emission, material tracking, site safety/clearance, target mapping, and completion application.
- Split construction readiness/progress/diagnostic handling into `ConstructionSiteProgress` and completion sequencing into `ConstructionCompletionCoordinator`.
- Reduced `ConstructionJobSystem` to a small orchestration shell that scans construction sites and delegates progress/completion.
- Moved construction target mapping, requirement matching, footprint/ring scanning, material tracking, site progress, site safety, completion application/coordinator, and executor core into `HumanFortress.Jobs`.
- Added Jobs-owned construction seams for logging, diff emission, and workshop-completion notification, with App adapters over the existing logger, `ConstructionDiffEmitter`, and UI notification hook.
- Added a Jobs-owned `ConstructionJobExecutor` core that owns construction site scanning, readiness/progress, material consumption, safety relocation, completion, and stats.
- Reduced App `ConstructionJobSystem` to a composition shell that wires concrete diff emission, logging, and UI notification into the Jobs-owned construction executor.
- Fixed L0 terrain construction completion so the construction site is removed after emitting `SetTerrain`; this prevents completed walls/floors from remaining as stale material-requesting sites.
- Fixed construction completion iteration by snapshotting owned placeables before mutating the placeable collection.
- Added construction terrain-completion regression coverage for site removal, one-time material consumption, residual item relocation, and terrain application.
- Split `CraftJobSystem` into a small orchestration shell with focused collaborators for active craft job state, worker assignment/pathing, active movement/replan/work execution, craft item diff emission, workshop lookup/input area helpers, input material consumption, output emission, job finalization, and stat snapshots.
- Split `CraftPlanner` material handling into focused collaborators for input counting, material readiness checks, deterministic transport request emission, and transport request seeding.
- Fixed craft input-failure cleanup so a queued recipe is preserved as `AwaitingMaterials` when materials disappear before work starts instead of being incorrectly removed from the workshop queue.
- Added craft regression coverage for missing-input cleanup: queue entry preserved, worker cleared, active slot released, and no consumption diff emitted.
- Fixed craft workshop input-area inconsistency: planner, transport requests, and material consumption now consistently treat the workshop footprint plus adjacent ring as the available input area.
- Added craft regression coverage for consuming material from the workshop input ring.
- Moved craft executor state, stats, assignment, active runner, material consumer, output emitter, workshop locator, input counter, material readiness helper, transport request emitter, transport/path seeds, finalizer, and executor core into `HumanFortress.Jobs`.
- Added Jobs-owned craft seams for planned-job intake, item diff emission, and worker candidate selection, with App adapters over the existing planner, item diff emitter, and profession assignment system.
- Added a Jobs-owned `CraftJobExecutor` core that owns planned-job drain/backlog handling, worker assignment/pathing, active craft work, input consumption, output emission, finalization, and stat snapshots.
- Reduced App `CraftJobSystem` to a composition shell that wires navigation, item diff emission, and profession adapters into the Jobs-owned craft executor.
- Moved `CraftPlanner` into `HumanFortress.Jobs.Craft`, so craft planning and execution now share one Jobs-owned domain boundary.
- Added a Jobs-owned `ICraftRecipeCatalog` seam and App `CraftRecipeCatalogAdapter`, removing direct `RecipeRegistry.Instance` access from Jobs-owned craft planner/executor/consumer/output/assignment code.
- Added a first formal regression-test project at `tests/HumanFortress.App.Tests` and wired it into `HumanFortress.sln`.
- Added a test-project runner that calls the existing App `TestRunner` but converts any failure marker into a non-zero process exit code.
- Migrated the first regression batch out of App `TestRunner` into `tests/HumanFortress.App.Tests`: transport rollback/replan/backlog, construction completion cleanup, and craft input cleanup/input-ring consumption.
- Migrated the second regression batch out of App `TestRunner` into `tests/HumanFortress.App.Tests`: mining channel reservation cleanup, item consumption/split diffs, item move relocation/merge behavior, and carry diff merge behavior.
- Migrated the remaining App `TestRunner` phase/core smoke assertions into `tests/HumanFortress.App.Tests` as `CoreRuntimeSmokeTests` and removed the App `TestRunner` class.
- Migrated the legacy App `PhaseTests` validation harness into `tests/HumanFortress.App.Tests`; `./RunTests.sh` now runs transport/construction/craft, mining/items/diff, core runtime smoke checks, and Phase A-D validation in one formal test entry.
- Converted App `--validate` into a compatibility pointer that directs users to `./RunTests.sh`, matching the existing App `--test` behavior and keeping test logic out of the game executable.
- Fixed the old `HumanFortress.Core.Content.ContentRegistry` startup path so it loads `materials.authoring.json` when legacy `materials.json` is absent; startup content loading now reports 79 materials instead of 0.
- Added content-load log forwarding so the old registry summary and detailed validation errors are written to `fortress_debug.log` as well as the console.
- Added the first structured diagnostics layer: Core diagnostic events/sinks, App async dispatcher, main timeline log, category-routed log files, in-memory ring buffer, and compatibility bridging for existing `Logger.Log`/`LogCallback` paths.
- Added the missing ore material definitions referenced by `geology.json`, clearing the remaining startup geology cross-reference errors.
- Made construction loading de-duplicate new workshop definitions against legacy `placeable/workshops.json` entries, turning duplicate workshop ids into explicit skipped diagnostics instead of a registry load error.
- Made recipe loading tolerate legacy root-array recipe files without treating them as parse failures; current recipe loading reports `errors=0`.
- Added rule-level embarkability diagnostics to `WorldTile` and displayed failure reasons in the WorldMap side panel instead of only showing `NOT EMBARKABLE`.
- Added regression coverage for embarkability diagnostics.
- Added `HumanFortress.Runtime` as the first dedicated runtime assembly and wired it into the active solution.
- Moved `SimulationCommandStage`, `SimulationStatus`, `SimulationTickPipeline`, `SimulationRuntimeContext`, Runtime command target interfaces/helpers, the Simulation-backed navigation source/factory, `SimulationRuntimeHostCore`, the generic `SimulationRuntimeSession<THost>` handle, and `SimulationRuntimeSessionFactory<THost>` into `HumanFortress.Runtime`, with App now referencing the runtime assembly for command-stage execution, status snapshots, tick-pipeline barriers, command target dispatch, shared navigation creation, scheduler/pipeline lifecycle, and session creation.
- `GameStateManager` no longer directly creates or stores separate World/Navigation/RuntimeHost fields; it now owns one runtime session handle and delegates world reset, navigation creation, queue/diff cleanup, content-loading callback invocation, and host-wrapper callback invocation to the Runtime-owned generic session factory.
- Added an explicit `SimulationCommandStage` for the authoritative pre-read command execution boundary, with regression coverage proving queued commands execute inside the tick pipeline before systems enter `ReadTick` and receive the real runtime tick.
- Converted profession weight changes from direct `GameStateManager -> ProfessionAssignments` mutation into `SetProfessionWeightCommand`, executed through the tick command stage with regression coverage.
- Converted debug `SpawnItemCommand` from direct `world.Items.SpawnItem` mutation into an `ItemsDiffLog.AddItem` command target, so item creation is applied by the post-tick item diff applicator. Added regression coverage for command-stage item spawning.
- Converted debug `SpawnCreatureCommand` from direct `world.Creatures.SpawnCreature` mutation into a minimal spawn-only `CreaturesDiffLog` and post-tick `CreaturesDiffApplicator`, with regression coverage for command-stage creature spawning.
- Converted mining, advanced mining, haul, structural construction, and buildable construction order commands away from direct `context.World as World` access. They now enqueue through `IOrderCommandTarget` on `SimulationRuntimeContext`, with regression coverage for all five command paths.
- Converted zone create/update/delete commands away from direct concrete `World` access. They now route through `IZoneCommandTarget` on `SimulationRuntimeContext`, preserving `ZoneCoordinator` shard cleanup behavior and adding regression coverage for create, add cells, remove cells, and delete.
- Converted workshop queue update commands away from direct concrete `World`, registry, and placeable lookup access. They now route through `IWorkshopQueueCommandTarget` on `SimulationRuntimeContext`, with regression coverage for recipe add, entry move/remove, queue clear, worker-slot changes, and automation toggles.
- Converted stockpile creation commands away from direct concrete `World` access. They now route through `IStockpileCommandTarget`, with regression coverage for stockpile zone creation, chunk shard assignment, overlap rejection, and command-stage execution.
- Moved Runtime command target aggregation out of App. `SimulationRuntimeContext` now lives in Runtime, owns the command target interface surface, and delegates profession weight writes through an injected callback so Runtime does not depend on App `ProfessionAssignments`.
- Moved item spawn, creature spawn, order, zone, workshop queue, and stockpile command target helpers into Runtime. `WorkshopQueueCommandTarget` still reads the current Core content registries as a transitional step until the Content split provides a cleaner catalog seam.
- Added `docs/REFACTOR_BATCH_PROGRESS.md` as the ongoing batch progress log for multi-step refactor work.
- Added Navigation-owned source/snapshot contracts (`INavigationWorldSource`, `NavigationTile`, `NavigationChunkSnapshot`) and removed the direct `HumanFortress.Navigation -> HumanFortress.Simulation` project reference.
- Moved Simulation world/tile/chunk adaptation into Runtime through `SimulationNavigationSource`, so Navigation cache building no longer consumes `Simulation.World`, `Chunk`, `TileBase`, or `TerrainKind` directly.
- Removed Navigation query-time stale-cache rebuilds: `GetNavDataAt` is now read-only, while runtime/session composition and dirty-commit post-tick logic explicitly rebuild navigation data.
- Added `HumanFortress.Contracts` as the first dedicated contracts assembly and wired it into `HumanFortress.sln`.
- Moved navigation DTO/interface contracts into `HumanFortress.Contracts`: path requests/results, `Point3`, navigation chunk/source snapshots, movement/capability enums, `IWorldNavigationView`, and `IPathService`.
- Kept the moved navigation contracts in the `HumanFortress.Navigation` namespace as a transitional compatibility step, so the assembly boundary improved without forcing a broad namespace rewrite in the same pass.
- Added `HumanFortress.Jobs` as the first dedicated job-system assembly and wired it into `HumanFortress.sln`.
- Moved the first low-risk transport slice into `HumanFortress.Jobs`: active transport job state/stage, backlog buffering, and reservation finalization.
- Moved transport stats DTO/tracking/counters into `HumanFortress.Jobs`, replacing the `TransportJobSystem` nested stats DTO with `TransportJobStatsSnapshot`.
- Moved `TransportIntakeFilter` into `HumanFortress.Jobs`, so transport request readiness/de-dup filtering is no longer App-owned.
- Added Jobs-owned transport seams for logging and worker candidate selection, with App adapters over the existing logger and profession assignment system.
- Moved `TransportAssignmentHandler` into `HumanFortress.Jobs`; worker assignment/path seeding/reservation setup is now Jobs-owned while App still supplies profession selection through an adapter.
- Added a narrow Jobs-owned `ITransportMovementDiffEmitter` seam and moved `TransportReplanHandler` into `HumanFortress.Jobs`.
- Added Jobs-owned item/carry diff and job-completion seams, then moved `TransportPickupHandler` and `TransportDeliveryHandler` into `HumanFortress.Jobs`.
- Moved `TransportActiveJobRunner` into `HumanFortress.Jobs`, with movement and item/carry writes routed through narrow Jobs-owned diff interfaces.
- Added a Jobs-owned `TransportJobExecutor` core that owns transport request drain/backlog handling, assignment throttling, active write ticks, scheduling hints, and transport debug snapshots.
- Reduced App `TransportJobSystem` to a composition shell that wires navigation, diff emission, logging, and profession adapters into the Jobs-owned transport executor.
- Removed business reliance on obsolete item carry/reservation flags (`IsCarried`, `IsReserved`, `ReservedBy`) in favor of `IsOnGround`, `CarriedBy`, and `ReservationManager`.
- Added ground-item query helpers to `ItemManager` for safer system access.
- Made `SimulationDiffApplicator` carry/un-carry behavior use `CarriedBy` and item-position updates instead of legacy flags.
- Fixed diff merge behavior so same-cell `MoveItem`, `MarkCarried`, and `UnmarkCarried` operations for different item entities are retained.
- Strengthened `TransportJobSystem.SplitStack` with deterministic split IDs, collision checks, reservation checks, failure rollback, and regression coverage.
- Added `DiffTargetEncoding` to centralize chunk/local/entity target encoding.
- Added `WorldCellTargetEncoding` in Simulation as the bridge between world coordinates, `ChunkKey + localIndex`, and `DiffTarget`.
- Added `WorldCellTarget` overloads to `ItemsDiffLog`, so item diff producers can pass the shared target directly while the older `ChunkKey + localIndex` API remains compatible.
- Replaced duplicated App-side chunk/local encoding in mining, transport, construction, craft, sanitize, and item-spawn diff emitters with the shared Simulation helper.
- Added `WorldSafetyQueries` to centralize safe-cell search and construction-site checks.
- Added `TransportDestinationValidator` to move transport destination validation toward the Simulation layer.
- Added `TransportDiffEmitter` to centralize transport creature/item/carry/split diff emission.
- Split `TransportJobSystem.WriteTick` arrival handling into stage handlers and fixed an item reservation leak when a worker disappears before pickup.
- Added `TransportReplanHandler` and `TransportJobFinalizer` to isolate replan/unstuck behavior and reservation cleanup.
- Made `TransportJobSystem` accept an injectable `IPathService`, reducing hard coupling to concrete pathing and enabling deterministic transport regression tests.
- Added transport regression coverage for no-path-after-pickup rollback: carry state is unmarked and item/creature reservations are released.
- Added `TransportPickupHandler` and `TransportDeliveryHandler` to split pickup, split-stack, and destination completion behavior out of the main transport executor.
- Added transport regression coverage for destination validation failure: invalid destinations unmark carry state, release reservations, and avoid item relocation.
- Added transport regression coverage for moved pickup targets: workers replan to the updated item position before carrying.
- Added `TransportAssignmentHandler` and `TransportBacklogBuffer` to isolate worker assignment and backlog bookkeeping.
- Added `TransportActiveJobRunner` to isolate active job movement, replan, pickup, delivery, and missing-worker cleanup from the main transport executor.
- Added `TransportIntakeFilter` and `TransportStatsTracker` to isolate intake readiness filtering and scheduling stat snapshots.
- Fixed a transport throttle bug where requests drained from the queue could be dropped when `maxActiveJobs` was reached; covered with a backlog-preservation regression test.
- Split `MiningJobSystem` into focused collaborators for intake coordination, per-dig read processing, backlog retry, deferred stairwell retry, deterministic dig ordering, stat snapshots, debug snapshot building, active job DTO/state, diff emission, drop/tuning resolution, tile reservation tracking, adjacency lookup, stairwell gating, job assignment, active execution, result application, path seeding, and job finalization.
- Moved mining state/debug/stats/backlog/deferred-stairwell/tile-reservation/path-seed/ordering/adjacency/finalization/intake/stairwell/read-processing/assignment/result-application/active-runner slices into `HumanFortress.Jobs`.
- Added Jobs-owned mining seams for logging, worker candidate selection, work-cost/drop resolution, diff emission, and job-completion reporting, with App adapters over the existing logger, profession assignment system, drop resolver, and diff emitter.
- Added a Jobs-owned `MiningJobExecutor` core that owns mining request intake, deterministic dig ordering, assignment/read processing, active write ticks, stats, and debug snapshots.
- Reduced App `MiningJobSystem` to a composition shell that wires navigation, diff emission, drop resolution, logging, and profession adapters into the Jobs-owned mining executor.
- Fixed stale mining intake/stat snapshots when no planned digs are dequeued.
- Fixed mining backlog carryover-age tracking so it is counted per queued retry item instead of by shared designation id.
- Fixed a mining channel reservation leak where worker-missing and replan-timeout paths released the target tile but not the reserved lower channel footprint; covered with a regression test.
- Moved runtime geology, geology handle assignment, tuning, and zone loading into `HumanFortress.Core.Content.Registry.ContentRegistry`.
- Migrated worldgen, mining/construction terrain resolution, simulation diff application, and fortress map rendering/popups to read runtime geology and tuning through the structured registry.
- Moved construction/workshop and recipe JSON loading behind `ContentRegistry.LoadCoreData`, removing App-local parsing for `data/core/workshops`, legacy `data/core/placeable/workshops.json`, and `data/core/recipes`.
- Kept `ConstructionRegistry` and `RecipeRegistry` as transitional sub-registries under the structured registry boundary, with smoke coverage proving the unified core-data load populates known construction and recipe definitions.
- Added read-only `IConstructionCatalog` and `IRecipeCatalog` interfaces, exposed them from the structured `ContentRegistry`, and migrated runtime/gameplay construction/recipe reads away from direct `ConstructionRegistry.Instance` / `RecipeRegistry.Instance` access.
- Injected `IConstructionCatalog` into Jobs-owned construction and craft execution paths, so Jobs code no longer pulls construction definitions through a construction singleton.
- Replaced recipe-registry mutation in regression tests with in-memory test catalogs; direct construction/recipe singleton access is now contained inside the structured `ContentRegistry`.
- Verified the current code with `dotnet build` and headless tests on .NET 8.

### Current Known Runtime/Architecture Issues

- `TransportJobSystem` is now an App-owned composition shell around `HumanFortress.Jobs.Transport.TransportJobExecutor`. Remaining App transport responsibilities are navigation composition, concrete diff emission, logging adaptation, and profession adapters.
- `MiningJobSystem` is now an App-owned composition shell around `HumanFortress.Jobs.Mining.MiningJobExecutor`. Remaining App mining responsibilities are navigation composition, concrete diff emission, drop/content resolution, logging adaptation, and profession adapters.
- `ConstructionJobSystem` is now an App-owned composition shell around `HumanFortress.Jobs.Construction.ConstructionJobExecutor`. Remaining App construction responsibilities are concrete diff emission, logging adaptation, and UI workshop-completion notification.
- `CraftJobSystem` is now an App-owned composition shell around `HumanFortress.Jobs.Craft.CraftJobExecutor`. Remaining App craft responsibilities are navigation composition, concrete item diff emission, profession adapters, and recipe catalog adaptation.
- `CraftPlanner` now lives in `HumanFortress.Jobs.Craft`. Jobs-owned craft code no longer directly reads `RecipeRegistry.Instance`; App bridges existing recipe content through `ICraftRecipeCatalog`.
- Navigation no longer has a project dependency on Simulation, and navigation DTO/interface contracts now compile from `HumanFortress.Contracts`. Their namespace is still `HumanFortress.Navigation` as a transitional compatibility choice.
- `HumanFortress.Jobs` now owns transport, mining, construction, and craft executor cores plus their state/helper/stats/debug slices.
- `HumanFortress.Jobs` currently exposes internals to App/tests as a transitional bridge. This should shrink as full executors and their tests move into Jobs-owned boundaries.
- `HumanFortress.Runtime` now exists and owns the command-stage/status/tick-pipeline/command-target/navigation-adapter/session-handle/session-factory/host-core slice, but most app-facing composition still lives inside App. `GameStateManager` delegates session creation to Runtime's `SimulationRuntimeSessionFactory<SimulationRuntimeHost>` and keeps a single `SimulationRuntimeSession<SimulationRuntimeHost>`; the concrete host wrapper, content loading implementation, concrete job adapters, initial worker/auto-dig hooks, and UI/SadConsole lifetime are still App-owned.
- Command execution now has an explicit pre-read runtime stage. Profession weight changes are command-driven, debug item spawning emits `ItemsDiffLog` additions, debug creature spawning emits a minimal spawn-only `CreaturesDiffLog` operation, and App command implementations route through Runtime target interfaces instead of directly casting to `Simulation.World` or depending on App runtime internals. Remaining command-boundary work is reducing the broad transitional target interface surface and eventually replacing direct manager mutations with authoritative typed diffs where each subsystem has an applicator path.
- `StockpileDiff` exists but is not currently wired into the tick pipeline and still has applicator TODOs, so stockpile command migration should wait until stockpile diffs have an authoritative runtime stage.
- `ContentRegistry` is being collapsed toward `HumanFortress.Core.Content.Registry.ContentRegistry`. The structured registry now owns runtime geology loading, deterministic geology handles, tuning files, zone definitions, and construction/recipe core-data loading, and worldgen/mining/construction/rendering read paths have moved to it. The legacy `HumanFortress.Core.Content.ContentRegistry` still exists as a compatibility load target inside `ContentLoadCoordinator` until remaining content ownership is migrated.
- `ConstructionRegistry` and `RecipeRegistry` still exist as singleton-backed compatibility stores, but direct access is now contained inside the structured `ContentRegistry`. Normal startup loads them through the structured registry boundary, and runtime reads use `IConstructionCatalog` / `IRecipeCatalog` where practical. Their concrete mutation APIs should eventually become internal to `ContentRegistry` or be replaced by immutable catalog snapshots.
- Creature and item definitions still load through `CreatureManager` and `ItemManager`; this keeps App/runtime startup workable, but validation and registration should move behind the structured content boundary before deleting the legacy registry/coordinator path.
- Startup content diagnostics are improved and current legacy content loads without registry errors. The structured registry now loads successfully and reports remaining validation warnings explicitly.
- Logging now has a first-pass structured async diagnostics pipeline with category/level events, a main timeline log, category log files, and a ring-buffer sink for future UI debug panels. The secondary content registry, WorldGen progress/error paths, stockpile diff errors, and key Simulation orders/jobs fallbacks now route through diagnostic helpers. Remaining work is mostly command-line/test output, no-logger fallback helpers, and exposing diagnostics in UI.
- The world map embarkability UI now shows first-pass rule failures for the current tile. It still needs richer terrain/biome/geology diagnostics if embark rules become more complex.
- A formal regression-test project now exists and the former App `TestRunner` plus `PhaseTests` coverage has moved into `tests/HumanFortress.App.Tests`; App `--test` and `--validate` are compatibility pointers to `./RunTests.sh`.
- Historical analyzer warnings remain; they are not currently blocking runtime but should be cleaned during build-hygiene work.

### Updated Near-Term Order

1. Continue carving Runtime out of App by moving host/runtime responsibilities only after content loading, job adapter composition, and UI-facing lifetime dependencies have cleaner seams.
2. Audit remaining command implementations that still directly mutate world managers and migrate them to typed diffs where the target subsystem has an applicator path.
3. Continue ContentRegistry unification by replacing remaining concrete construction/recipe registry mutation access with immutable catalog snapshots or test fixtures, then moving item definition validation and creature definition validation behind the structured registry boundary before deleting the legacy registry and coordinator compatibility path.
4. Continue diagnostics migration by replacing remaining direct `Console.WriteLine` paths and exposing the in-memory diagnostics ring buffer through a UI/debug surface.
5. Move remaining App-owned job adapters/tunings only when their dependencies have clear Runtime/Content homes.
6. Split the monolithic test project into focused project-level test assemblies when the module boundaries are stable enough.

## Current Critical Findings

### P0: Runtime Ownership Is in the Wrong Place

`GameStateManager` no longer directly creates the active `World`, navigation manager, and runtime host wrapper. World/session reset and navigation creation now sit behind Runtime's `SimulationRuntimeSessionFactory<SimulationRuntimeHost>`, with `GameStateManager` holding one `SimulationRuntimeSession<SimulationRuntimeHost>`.

It still owns or coordinates the app state transitions, `TickScheduler`, `CommandQueue`, `EventBus`, RNG manager, diff logs, runtime session handle, debug caches, and simulation controls.

That is better than the original god-object shape, but it still makes `GameStateManager` partly responsible for runtime lifetime instead of pure app-state transitions.

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

### P0: Command Execution Boundary

The tick scheduler runs on a background simulation thread. Command execution now happens through `SimulationCommandStage`, attached to the tick pipeline's pre-read boundary.

Current first-pass pipeline:

```text
input thread: enqueue only
simulation thread: PreTick -> SimulationCommandStage -> system ReadTick -> barrier -> system WriteTick -> PostTick diff commit
```

Remaining fix:

1. Keep UI and App enqueue-only for all simulation-affecting actions.
2. Replace direct world mutation inside command implementations with typed diffs where the target domain has a diff/applicator path.
3. Add replay coverage once command serialization and deterministic command ids are mature enough.

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

There is now an initial formal regression-test project, `tests/HumanFortress.App.Tests`. The former App `TestRunner` and `PhaseTests` coverage has moved into it, including transport/construction/craft, mining/items/diff, core runtime smoke checks, and Phase A-D validation. App `--test` and `--validate` are compatibility pointers to `./RunTests.sh`.

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
- direct `Console.WriteLine` logging still exists in command-line compatibility messages, tests, startup summaries, and no-logger fallback helpers.

Fix:

1. Keep one active solution.
2. Move publish settings into a publish profile.
3. Turn determinism-related warnings into errors.
4. Align package versions.
5. Replace obsolete item reservation/carry fields.
6. Continue structured logging migration with categories, levels, and UI-facing debug surfaces.

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

Status: mostly complete.

Completed:

1. Keep one valid solution file.
2. Delete unreachable old play-state implementation.
3. Add this master refactor plan.

Still pending:

4. Freeze new gameplay features until command/diff/runtime boundary work begins.
5. Add CI build for the active solution.

### Phase 1: Extract Runtime Inside Existing Projects

Status: in progress.

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

Progress:

- `FortressState` has been split into focused helpers for session loading, view state, interaction routing, placement, rendering, and viewport math.
- More runtime/session responsibilities now live outside the screen class.
- A true independent runtime composition root is still pending.

### Phase 2: Move Commands Into Tick Pipeline

Status: first pass complete.

1. [Done first pass] UI/runtime paths enqueue `ICommand` instances through `CommandQueue`.
2. [Done] Tick scheduler has a pre-read command stage through `SimulationCommandStage`.
3. [Done] `SimulationRuntimeContext.CurrentTick` is set from the real scheduler tick before command execution.
4. [Done first pass] Mining placement uses `CreateAdvancedMiningOrderCommand`.
5. [Done] Profession weight changes become commands.
6. [Pending] Commands that still directly mutate authoritative world state move to typed diffs where the domain supports them.

### Phase 3: Make Diff Commit Real

Status: in progress.

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

Progress:

- Construction material consumption now uses item diffs.
- Construction item relocation now uses `MoveItem`.
- Construction diff emission is isolated behind `ConstructionDiffEmitter`.
- Construction material counting/consumption is isolated behind `ConstructionMaterialTracker`.
- Construction site safety/clearance is isolated behind `ConstructionSiteSafety`.
- Construction readiness, progress advancement, and diagnostic logging are isolated behind `ConstructionSiteProgress`.
- Construction target mapping and final L0/L2 completion are isolated behind `ConstructionTargetMapper` and `ConstructionCompletionApplier`.
- Construction completion sequencing is isolated behind `ConstructionCompletionCoordinator`.
- Construction L0 completion now has regression coverage for removing completed construction sites and avoiding repeated material requests.
- Item consumption, stack splitting, item relocation, and carry/un-carry state now have focused regression coverage outside App `TestRunner`.
- Craft item add/remove diff emission is isolated behind `CraftDiffEmitter`.
- Craft workshop lookup and footprint checks are isolated behind `CraftWorkshopLocator`.
- Craft input counting/readiness is isolated behind `CraftInputCounter` and `CraftMaterialReadinessChecker`.
- Craft material transport requests are isolated behind `CraftTransportRequestEmitter`.
- Craft input consumption is isolated behind `CraftMaterialConsumer`.
- Craft output emission is isolated behind `CraftOutputEmitter`.
- Craft job queue/active-slot cleanup is isolated behind `CraftJobFinalizer`, with regression coverage for missing inputs preserving the queue entry.
- Craft worker assignment/pathing is isolated behind `CraftAssignmentHandler`.
- Craft active movement/replan/work execution is isolated behind `CraftActiveJobRunner`.
- Craft stat snapshots are isolated behind `CraftStatsTracker`.
- Craft workshop input-area consistency is covered by a regression for consuming material from the adjacent input ring.
- Transport item movement, carry state, and split-stack emission are centralized through `TransportDiffEmitter`.
- `DiffTargetEncoding` centralizes target encoding for diff producers/consumers.
- Diff merge behavior now preserves item-entity-specific move/carry operations.
- Transport no-path-after-pickup rollback now has regression coverage for carry-state cleanup and reservation release.
- Transport destination-validation failure now has regression coverage for carry-state cleanup, reservation release, and no item relocation.
- Transport moved-pickup behavior now has regression coverage for replanning before carry pickup.
- Transport throttle/backlog behavior now has regression coverage so drained requests are not dropped when active slots are full.
- `WorldCellTargetEncoding` now centralizes Simulation world-cell target encoding for App diff emitters that still need `ChunkKey + localIndex`.
- `ItemsDiffLog` now accepts `WorldCellTarget` directly for add/remove/split operations.
- Mining diff emission is isolated behind `MiningDiffEmitter`.
- Mining drop/tuning lookup and caching is isolated behind `MiningDropResolver`.
- Mining terrain-result branching is isolated behind `MiningResultApplier`.
- Mining active execution is isolated behind `MiningActiveJobRunner`.
- Mining worker/path assignment is isolated behind `MiningAssignmentHandler`.
- Mining stairwell skip/defer/connectivity rules are isolated behind `MiningStairwellGate`.
- Mining intake coordination, per-dig read processing, backlog/deferred retry bookkeeping, stat snapshots, debug snapshot building, adjacency lookup, path seeding, and job finalization are isolated from the main executor.
- Mining channel tile reservation cleanup has regression coverage for the target-plus-below footprint.
- Construction material tracking, site safety, progress/diagnostics, diff emission, target mapping, completion sequencing, and completion application have been isolated from the main executor.
- Craft diff emission, workshop lookup, input counting/readiness, material transport requests, material consumption, output emission, worker assignment, active execution, stat snapshots, and finalization have been isolated from the main executor/planner.

### Phase 4: Invert Navigation

Status: in progress.

1. Move navigation contracts into Contracts.
2. [Done first pass] Remove direct `World` field from NavigationManager.
3. [Done first pass] World/Runtime provides navigation source snapshots.
4. [Done] Remove query-time rebuild.
5. [Done first pass] Rebuild navigation after session/world initialization and dirty commit.

Progress:

- `HumanFortress.Navigation` no longer references `HumanFortress.Simulation`.
- `NavigationManager` now consumes `INavigationWorldSource` instead of `Simulation.World.World`.
- `ChunkNavData` rebuilds from `NavigationTile[]` snapshots instead of `TileBase[]`.
- `WorldNavigationView` is source-backed and stays inside Navigation without knowing Simulation terrain types.
- Runtime provides `SimulationNavigationSource` as the current adapter over authoritative `World`; App still owns session-level timing for when the shared navigation manager is created and rebuilt.
- `NavigationManager.GetNavDataAt` is now read-only; explicit `RebuildAll`/`RebuildChunkNavData` calls own cache mutation.
- Private job-system navigation managers explicitly rebuild once at composition time; shared runtime navigation is rebuilt after fortress map generation and after dirty world commits.

### Phase 5: Move Jobs Out of App

Status: executor-core move is mostly complete; App composition shells and adapters remain.

After Navigation is inverted:

1. Move mining/hauling/construction/crafting executors to `HumanFortress.Jobs`.
2. Move profession assignment and scheduler tunings with them.
3. Keep App-only debug panels in UI/App.
4. Prove jobs can run in a headless test.

Preparation progress:

- Transport destination validation has moved toward Simulation ownership.
- World safe-cell queries have moved into Simulation.
- Transport diff emission has been isolated from transport scheduling logic.
- Transport replan/unstuck behavior and job finalization have been isolated from the main executor.
- Transport pickup and delivery behavior have been isolated from the main executor.
- Transport assignment and backlog bookkeeping have been isolated from the main executor.
- Transport active job iteration has been isolated from the main executor.
- Transport intake filtering and stat snapshots have been isolated from the main executor.
- Transport pathing is now injectable through `IPathService`, preparing the executor for runtime-owned path services.
- Mining intake coordination, per-dig read processing, backlog, deferred stairwell retry, deterministic dig ordering, stat snapshots, debug snapshot building, diff emission, drop resolution, tile reservation tracking, adjacency lookup, stairwell gating, assignment, active execution, result application, path seeding, and finalization have been isolated from the main executor.
- Construction material tracking, site safety/clearance, progress/diagnostics, diff emission, target mapping, completion sequencing, and completion application have been isolated from the main executor.
- Craft diff emission, workshop lookup, input counting/readiness, material transport requests, material consumption, output emission, worker assignment, active execution, stat snapshots, and finalization have been isolated from the main executor/planner.
- Transport, mining, construction, and craft executor cores now live in `HumanFortress.Jobs`; craft planning also lives in Jobs. App still owns composition shells, concrete adapters, tunings that depend on App/runtime concerns, and UI/debug surfaces.

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

1. [Done] Clean solution and obsolete play-state code.
2. [In progress] Add `FortressRuntime` shell and move world/tick ownership.
3. [Done first pass] Move command execution into tick.
4. [Done first pass] Replace direct mining order enqueue with command.
5. [Done first pass] Fix command execution `CurrentTick`.
6. [Partially done] Replace unstable diff/target ordering hazards; continue determinism audit.
7. [Mostly done] Add headless regression checks; former App `TestRunner` and `PhaseTests` coverage has moved into `tests/HumanFortress.App.Tests`.
8. [Mostly done] Invert Navigation dependency; direct Navigation -> Simulation reference and query-time rebuilds have been removed. Contracts relocation remains.
9. [Mostly done] Move job executor cores out of App; remaining work is planner/runtime/content seams and cleanup of transitional App adapters.
10. [Pending] Start Content split.

Immediate next PR-sized work:

1. Start ContentRegistry unification and continue structured logging/debug diagnostics cleanup.
2. Carve a real Runtime composition boundary out of `GameStateManager` and App runtime helpers.
3. Split the current monolithic App test project into focused module-level test projects when dependencies make that practical.
