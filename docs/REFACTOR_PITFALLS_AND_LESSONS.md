# HumanFortress Refactor Pitfalls and Lessons

Date: 2026-06-06

This document records practical pitfalls found during the current architecture refactor. It is meant to keep future refactor work fast and predictable.

## Content Boundary Pitfalls

### Keep data/core JSON traversal out of App

Construction/workshop and recipe loading now enters through:

```csharp
HumanFortress.Core.Content.Registry.ContentRegistry.Instance.LoadCoreData(dataCorePath)
```

Do not reintroduce App-local parsing for:

```text
data/core/workshops/core_workshop_*.json
data/core/placeable/workshops.json
data/core/recipes/*.json
```

`SimulationWorldContentLoader` may locate the runtime content directory and log load results, but schema compatibility and registry population belong behind the structured content registry boundary.

Important compatibility behavior preserved by the Core loader:

- new workshop files and legacy `placeable/workshops.json` are both loaded;
- duplicate construction ids are skipped and counted instead of failing startup;
- recipe files may be root arrays or `{ "recipes": [...] }` documents;
- legacy recipe aliases such as `workshop_id`, `workshop`, `duration_ticks`, and `primary_skill` still parse.

### ConstructionRegistry and RecipeRegistry are transitional

`ConstructionRegistry` and `RecipeRegistry` still exist as singleton stores. They are now loaded through `ContentRegistry.LoadCoreData`, so callers should treat them as compatibility sub-registries rather than independent content roots.

Runtime/gameplay reads should use the read-only catalog surface:

```csharp
ContentRegistry.Instance.Constructions.GetConstruction(id)
ContentRegistry.Instance.Recipes.GetRecipe(id)
```

For long-lived systems and Jobs-owned code, prefer constructor-injected interfaces:

```csharp
IConstructionCatalog
IRecipeCatalog
```

Do not add new `ConstructionRegistry.Instance.Get...` or `RecipeRegistry.Instance.Get...` reads in runtime systems. Direct singleton access is currently contained inside `ContentRegistry`; keep it that way so the concrete stores can be replaced by immutable content snapshots later.

Preferred direction:

```text
ContentRegistry
  owns load/validation/indexing
  exposes read-only construction/recipe catalogs

Runtime/App
  request definitions through catalog interfaces
  do not parse content files directly
```

Do not move Jobs-owned code back to `RecipeRegistry.Instance`. Craft already uses `ICraftRecipeCatalog`; future construction/craft/runtime seams should follow that pattern.

## Build and SDK Pitfalls

### Use the .NET 8 executable explicitly on macOS

On this machine, the stable build path is:

```bash
/opt/homebrew/opt/dotnet@8/bin/dotnet
```

Do not assume plain `dotnet` points to the right SDK/runtime. We saw a mismatch where the default CLI used newer .NET tooling while the app targeted `net8.0`, and the runtime failed until .NET 8 was correctly available.

Recommended build command:

```bash
/opt/homebrew/opt/dotnet@8/bin/dotnet build HumanFortress.sln --no-restore -m:1 -v:quiet -p:RunAnalyzers=false
```

### Do not parallel-build App and tests

Avoid running App build and test-project build at the same time. Both can touch:

```text
src/HumanFortress.App/obj/Debug/net8.0/apphost
```

On macOS this caused apphost copy/signing races:

- `apphost` not found during copy
- `apphost: is already signed`

Use sequential build/test commands.

### Avoid `dotnet run` for the test script

`dotnet run --project tests/...` can silently spend time in build/analyzer paths before producing output. This looked like a hang.

Current `RunTests.sh` avoids that by doing:

1. explicit build with analyzers disabled;
2. direct DLL execution.

Current test command:

```bash
./RunTests.sh
```

### Analyzer runs are separate hygiene work

During feature/refactor verification, use:

```bash
-p:RunAnalyzers=false
```

Historical analyzer warnings and errors still exist. They should be fixed in a dedicated build-hygiene pass, not mixed into gameplay/system refactors.

## Test Architecture Pitfalls

### App `--test` and `--validate` are compatibility pointers

The preferred test entry is now:

```bash
./RunTests.sh
```

`HumanFortress.App --test` and `HumanFortress.App --validate` no longer host tests. They print compatibility messages pointing to the formal test runner:

```text
tests/HumanFortress.App.Tests
```

The legacy App `PhaseTests` harness now lives in `tests/HumanFortress.App.Tests`, and `./RunTests.sh` runs it after the focused regression/smoke batches.

### `InternalsVisibleTo` is temporary

`HumanFortress.App.Tests` currently uses:

```csharp
InternalsVisibleTo("HumanFortress.App.Tests")
```

This is acceptable while job systems still live in App. It should disappear as systems move into `HumanFortress.Jobs`, `HumanFortress.Simulation`, or focused testable assemblies.

### Avoid duplicating migrated regression tests

When a regression moves from `src/HumanFortress.App/TestRunner.cs` into `tests/`, remove the old App copy. Otherwise we get duplicate runtime, duplicate logs, and unclear ownership.

First migrated batch:

- transport finalizer reservation cleanup;
- transport no-path rollback;
- transport invalid-destination rollback;
- transport moved pickup target replan;
- transport active-slot backlog preservation;
- construction terrain completion cleanup;
- craft missing-input queue preservation;
- craft workshop input-ring consumption.

Second migrated batch:

- mining channel reservation full-footprint cleanup;
- item consumption diffs;
- split-stack pre-simulation diffs;
- item move relocation and stack merge;
- carry/un-carry diff merge behavior.

Final migrated batches:

- tick scheduler smoke checks;
- deterministic RNG and runtime ID checks;
- diff target encoding smoke checks;
- world/chunk and reservation smoke checks;
- command queue ordering/clear behavior;
- runtime command stage execution before system `ReadTick`.
- Phase A-D validation coverage for platform, world generation, fortress bootstrap, and navigation.

## Runtime and Diagnostics Pitfalls

### Command execution belongs at the pre-read tick boundary

Do not call `CommandQueue.ExecuteCommands` from UI, game states, screen update code, or render-thread services. UI/App code should enqueue commands only.

Current runtime path:

```text
TickScheduler.PreTick
  -> Runtime-owned SimulationTickPipeline
  -> Runtime-owned SimulationCommandStage.Execute
  -> CommandQueue.ExecuteCommands
  -> system ReadTick
```

The regression coverage now proves a due command sees the real `SimulationRuntimeContext.CurrentTick` and is visible to systems during the same tick's `ReadTick`.

Profession allocation changes also go through this path now:

```text
UI work allocation input
  -> GameStateManager.EnqueueCurrentTickCommand
  -> SetProfessionWeightCommand
  -> IProfessionAssignmentCommandTarget
  -> ProfessionAssignments.SetWeight
```

`IProfessionAssignmentCommandTarget` is now a Runtime-owned seam. Runtime keeps `HumanFortress.Core.Commands.ISimulationContext` free of job-system details, while App still owns `ProfessionAssignments` and supplies a weight-write callback during host composition.

Debug item spawning also goes through this path now:

```text
SpawnItemCommand
  -> IItemSpawnCommandTarget
  -> ItemsDiffLog.Add(AddItem)
  -> ItemsDiffApplicator.ApplyAdditions
```

Debug creature spawning also goes through this path now:

```text
SpawnCreatureCommand
  -> ICreatureSpawnCommandTarget
  -> CreaturesDiffLog.AddSpawnCreature
  -> CreaturesDiffApplicator.ApplyAll
```

The creature diff path is intentionally narrow: it supports spawn-only command migration and should not be treated as a complete creature mutation system yet.

Core order commands also no longer cast `context.World` to the concrete `World` type:

```text
CreateMiningOrderCommand / CreateAdvancedMiningOrderCommand / CreateHaulOrderCommand
CreateConstructionOrderCommand / CreateBuildableConstructionOrderCommand
  -> IOrderCommandTarget
  -> OrdersManager.Enqueue...
```

This is a runtime seam, not a full order diff log. It keeps App command implementations from knowing the concrete world manager while preserving the existing order queue semantics.

Zone commands also no longer cast `context.World` to `World`:

```text
CreateZoneCommand / UpdateZoneCellsCommand / DeleteZoneCommand
  -> IZoneCommandTarget
  -> ZoneCoordinator
```

Use `ZoneCoordinator` rather than calling `ZoneManager` directly. Deleting a zone must remove chunk shards as well as the global zone instance; otherwise stale per-chunk zone data remains behind.

Workshop queue commands also no longer cast `context.World` to `World`:

```text
UpdateWorkshopQueueCommand
  -> IWorkshopQueueCommandTarget
  -> SimulationRuntimeContext workshop resolver
  -> WorkshopState
```

Keep recipe lookup and placeable lookup out of the command implementation. The command should dispatch the requested operation only; runtime owns finding the workshop instance, initializing `WorkshopState`, and applying worker-slot/automation/queue mutations.

Stockpile creation commands also no longer cast `context.World` to `World`:

```text
CreateStockpileCommand
  -> IStockpileCommandTarget
  -> StockpileCommandTarget
  -> StockpileManager + ChunkStockpileData
```

This is intentionally a runtime seam rather than `StockpileDiff`. The current stockpile diff applicator is not attached to the active tick pipeline and still has unresolved item/job TODOs, so using it for command migration would make stockpile creation silently disappear or become partially applied.

The richer workshop and stockpile target behavior now lives in Runtime-owned helper classes rather than directly in `SimulationRuntimeContext`. Item spawning, creature spawning, order enqueueing, and zone mutation also now live behind Runtime-owned target helpers.

`SimulationRuntimeContext` itself is Runtime-owned now. It remains a broad transitional adapter that implements several command target interfaces; the next cleanup is reducing that interface surface or grouping targets by command family once the command model is clearer.

Profession assignment remains a special case: Runtime only stores an injected weight-write callback, while App still owns `ProfessionAssignments`.

Do not use `StockpileDiff` as a migration target yet. Its applicator is not attached to the active tick pipeline and still contains TODO paths for job creation and item placement/removal.

### Do not let GameStateManager recreate the runtime graph by hand

`GameStateManager` previously stored separate `World`, `NavigationManager`, and `SimulationRuntimeHost` fields and assembled them directly inside `InitializeWorld`. That made it both a state machine and an implicit composition root.

Current first pass:

```text
GameStateManager
  -> Runtime-owned SimulationRuntimeSessionFactory<SimulationRuntimeHost>.CreateNew(...)
  -> App content-loading callback
  -> App host-wrapper callback
  -> Runtime-owned SimulationRuntimeSession<SimulationRuntimeHost>(World, Navigation, Host)
  -> App-owned SimulationRuntimeHost wrapper
  -> Runtime-owned SimulationRuntimeHostCore
```

Keep future runtime construction behind this seam. UI/state code should not directly reset schedulers, clear command queues, create navigation managers, or new up runtime hosts. Content loading and concrete host construction are still App callbacks because they depend on current content registries, job adapters, UI hooks, and SadConsole-facing lifetime.

`SimulationRuntimeHostCore` owns scheduler restart, tick-system registration, pipeline attachment, and stop-time pipeline detachment. Keep App-specific system creation, initial worker spawning, auto-dig seeding, and UI/debug hooks in the App wrapper until those dependencies have their own Runtime/Content seams.

The debug cache remains in `GameStateManager` for now because it is UI-facing state, not simulation session state. Do not move it into the runtime host until there is a structured diagnostics surface.

## Navigation Boundary Pitfalls

### Contracts assembly owns shared navigation contracts

`HumanFortress.Contracts` now contains the stable navigation DTO/interface surface:

```text
IPathService
IWorldNavigationView
INavigationWorldSource
NavigationChunkSnapshot
NavigationTile
PathRequest / Path / PathNode / Point3 / ChunkKey
MoveMode / PathFlags / NavCapability
```

The namespace intentionally remains `HumanFortress.Navigation` for now. Treat this as a transitional compatibility decision: the assembly boundary matters more than forcing a large namespace churn before job systems are moved out of App.

Any project that implements test doubles or directly consumes these contracts should reference `HumanFortress.Contracts` explicitly. Do not rely on transitive references through App.

### Keep Simulation types out of Navigation

`HumanFortress.Navigation` no longer references `HumanFortress.Simulation`. Do not pass `World`, `Chunk`, `TileBase`, or `TerrainKind` into Navigation internals.

Use the Contracts-owned source/snapshot contracts instead:

```text
INavigationWorldSource
NavigationChunkSnapshot
NavigationTile
NavigationTileKind
```

The current Simulation adapter lives in `HumanFortress.Runtime` as `SimulationNavigationSource`. Keep it there unless a more explicit world-navigation adapter package is introduced; do not move Simulation type knowledge back into Navigation.

### Query-time rebuild must stay removed

`NavigationManager.GetNavDataAt` is read-only. Do not reintroduce stale-cache rebuilds from path queries.

The intended ownership is:

```text
simulation commit -> collect dirty chunks -> rebuild navigation -> path queries read cache only
```

For isolated job-system tests or temporary private navigation managers, rebuild explicitly at composition time instead of rebuilding from `GetNavDataAt`.

### Content loading diagnostics must fail loudly and specifically

We saw startup output like:

```text
[ContentRegistry] Loaded: 0 materials, 17 geology entries, 19 zone definitions
[ContentRegistry] 18 errors during loading
```

Root causes found and fixed in the first pass:

- old `HumanFortress.Core.Content.ContentRegistry` only looked for legacy `materials.json`, while the repo now ships `materials.authoring.json`;
- summarized errors hid the actual missing references;
- content loading happened before file logging was initialized;
- `geology.json` referenced four ore material ids that did not exist;
- construction loaded both new workshop files and legacy `placeable/workshops.json`, causing duplicate ids;
- recipe loading treated legacy root-array files as parse errors.

Current first-pass behavior:

- content loading is logged to both console and the async diagnostic pipeline;
- App creates a full timeline log at `fortress_debug.log` plus category logs under `logs/`;
- old registry material loading reports 79 materials instead of 0;
- `ContentLoadCoordinator` now loads both legacy and structured registries from App startup;
- structured registry loading now supports top-level array material files and resets validation state before reload;
- geology cross-reference errors are clear and currently clean;
- construction duplicates are explicitly skipped and counted;
- recipe loading reports `errors=0`.

Remaining architecture risk: there are still two `ContentRegistry` models in the codebase, but the structured registry is now the intended owner for runtime geology handles, tuning files, and zones. The next content pass should move construction definitions, recipes, item definition validation, and creature definition validation behind that same boundary, then remove the legacy registry compatibility load.

### Diagnostics should be async, categorized, and non-authoritative

The first structured diagnostics pass added:

```text
module code
  -> IDiagnosticSink / Logger compatibility facade
  -> async dispatcher
  -> fortress_debug.log
  -> logs/content.log, runtime.log, simulation.log, jobs.log, navigation.log, ui.log, core.log
```

Do not let simulation systems write files directly. Emit a diagnostic event or use a temporary compatibility callback. The dispatcher owns file IO and sequence assignment.

`HumanFortress.Simulation` now has a small `SimulationDiagnostics` helper for transitional systems that still expose static `LogCallback` bridges. Use that helper instead of adding new `Console.WriteLine` calls inside Simulation code.

Do not use diagnostics for authoritative replay. Logs are for observability and debugging; deterministic replay still belongs to command/event/save streams.

`DiagnosticHub` is a transitional bridge for Core systems that are not yet constructed with dependencies. New runtime-owned services should prefer an injected `IDiagnosticSink`.

`Contracts` should stay log-free.

### Embarkability UI needs rule-level diagnostics

The world map showed every tile as `NOT EMBARKABLE`. The UI did not expose which rule failed.

First pass implemented:

- `WorldTile.GetEmbarkabilityFailures()` exposes the current rule failures;
- WorldMap side panel shows the first failure reasons under `NOT EMBARKABLE`;
- regression coverage verifies low elevation, high elevation, and river-class failures.

Before changing world-gen or embark rules further, keep diagnostics showing:

- selected tile coordinates;
- terrain/biome/geology summary;
- each embarkability rule;
- pass/fail reason.

### Console logging is too noisy for tests

Current tests print a lot of `ItemManager` and creature spawn logs. That is acceptable short-term, but it slows reading and hides failures.

Long-term fix:

- structured logger;
- categories and levels;
- test mode default level;
- capture logs only on failure.

## Refactor Process Pitfalls

### Move job systems in slices, not whole executors

`HumanFortress.Jobs` now exists, but the first transport extraction deliberately moved only low-risk state/helper types:

```text
ActiveJob
JobStage
TransportBacklogBuffer
TransportJobFinalizer
TransportJobStatsSnapshot
TransportStatsTracker
JobStats
TransportIntakeFilter
ITransportJobLogger
ITransportWorkerCandidateSource
TransportAssignmentHandler
ITransportMovementDiffEmitter
TransportReplanHandler
ITransportItemDiffEmitter
ITransportJobCompletionSink
TransportPickupHandler
TransportDeliveryHandler
TransportActiveJobRunner
TransportActiveJobView
TransportActiveJobDebugView
TransportDebugSnapshot
TransportJobExecutor
```

This is intentional. Moving the full transport executor in one pass would drag pathing, world access, diffs, professions, logging, and debug snapshots across the boundary at once.

Use this order for future transport movement:

```text
state/contracts -> stats snapshots -> intake/filtering -> assignment/replan -> pickup/delivery -> active runner -> debug DTOs -> executor core -> App composition shell
```

The stats snapshot is now a top-level `TransportJobStatsSnapshot` in Jobs. Avoid reintroducing nested DTOs on `TransportJobSystem`; nested public DTOs make later assembly movement harder.

Transport active/debug snapshot DTOs now live in Jobs (`TransportActiveJobView`, `TransportActiveJobDebugView`, `TransportDebugSnapshot`). Keep public debug contracts near the executor that owns the data; do not put them back as nested App types.

`TransportIntakeFilter` now owns request readiness/de-dup filtering in Jobs. Keep it focused on domain state checks (`item exists`, `item on ground`, `not reserved`) and do not add UI logging or executor side effects to it.

`TransportAssignmentHandler` now lives in Jobs. App-specific profession weighting is behind `ITransportWorkerCandidateSource`, and App logging is behind `ITransportJobLogger`. Keep those seams narrow; do not pass `ProfessionAssignments`, `WorkerSelectionStrategy`, or `Logger` back into Jobs-owned handlers.

`TransportReplanHandler` now lives in Jobs. It only depends on `ITransportMovementDiffEmitter.MoveCreature` instead of the full App `TransportDiffEmitter`. Preserve that narrow dependency: replan should not learn how to split stacks, mark carry state, or move items.

`TransportPickupHandler` and `TransportDeliveryHandler` now live in Jobs. They depend on `ITransportItemDiffEmitter` for item/carry/split diffs and `ITransportJobCompletionSink` for profession progress. Keep destination validation in Simulation and keep App-specific profession objects behind the completion sink.

`TransportActiveJobRunner` now lives in Jobs. It should remain a coordinator over movement update, replan, pickup, delivery, and missing-worker cleanup. It depends on separate movement and item/carry diff interfaces; do not collapse those back into the App `TransportDiffEmitter` concrete type.

`TransportJobExecutor` now owns the transport tick core in Jobs: request drain/backlog, assignment throttle, active write tick, scheduling hints, and debug snapshots. App `TransportJobSystem` should stay a composition shell that wires navigation, diff emission, logging, and profession adapters.

### Mining extraction now follows the same shell/core pattern

The mining executor has the same ownership rule as transport: Jobs owns the tick core, App owns concrete adapters.

Jobs-owned mining slices now include:

```text
ActiveMiningJob
MiningStage
MiningBacklogBuffer
MiningDeferredStairwellBuffer
MiningTileReservationTracker
MiningPathSeed
MiningJobStatsSnapshot
MiningDebugSnapshot
MiningDebugSnapshotBuilder
MiningDigOrdering
MiningAdjacencyFinder
MiningIntakeCoordinator
MiningStairwellGate
MiningReadJobProcessor
MiningAssignmentHandler
MiningResultApplier
MiningActiveJobRunner
MiningJobExecutor
```

Keep App dependencies behind the narrow mining seams:

```text
IMiningJobLogger
IMiningWorkerCandidateSource
IMiningWorkCostResolver
IMiningDropResolver
IMiningDiffEmitter
IMiningJobCompletionSink
```

Do not pass `ProfessionAssignments`, `WorkerSelectionStrategy`, `Logger`, `MiningDiffEmitter`, or `MiningDropResolver` directly into Jobs-owned mining code. App `MiningJobSystem` should remain only a composition shell.

### Construction extraction uses adapter-only App ownership

Construction is simpler than transport/mining because it does not own pathing or worker assignment, but it still has important App-owned concrete concerns: diff emission, logging, and UI workshop-completion notification.

Jobs-owned construction slices now include:

```text
ConstructionRequirementMatcher
ConstructionTargetMapper
ConstructionFootprintCells
ConstructionMaterialTracker
ConstructionSiteProgress
ConstructionSiteSafety
ConstructionCompletionApplier
ConstructionCompletionCoordinator
ConstructionJobExecutor
```

Keep App dependencies behind the narrow construction seams:

```text
IConstructionJobLogger
IConstructionDiffEmitter
IConstructionWorkshopCompletionSink
```

Do not pass `Logger`, `ConstructionDiffEmitter`, or the static `ConstructionJobSystem.UiNotifyWorkshopComplete` hook directly into Jobs-owned construction code. App `ConstructionJobSystem` should remain only a composition shell.

The current `InternalsVisibleTo("HumanFortress.App")` bridge in `HumanFortress.Jobs` is transitional. Do not let it become the permanent architecture.

### Craft extraction needs three explicit seams

Craft looks small from the outside, but it crosses planning, content lookup, material logistics, worker assignment, movement, item diffs, and workshop queue state. Moving it safely required separating those concerns instead of dragging App types into Jobs.

Jobs-owned craft slices now include:

```text
PlannedCraftJob
ActiveCraftJob
CraftJobStatsSnapshot
ActiveCraftJobView
CraftWorkshopLocator
CraftInputCounter
CraftMaterialReadinessChecker
CraftTransportRequestEmitter
CraftPlanner
CraftMaterialConsumer
CraftOutputEmitter
CraftAssignmentHandler
CraftActiveJobRunner
CraftJobFinalizer
CraftJobExecutor
```

Keep App dependencies behind the narrow craft seams:

```text
ICraftJobPlanner
ICraftRecipeCatalog
ICraftDiffEmitter
ICraftWorkerCandidateSource
```

Do not pass `RecipeRegistry`, `CraftDiffEmitter`, `ProfessionAssignments`, or `WorkerSelectionStrategy` deeper into Jobs-owned craft code. App `CraftJobSystem` should remain only a composition shell.

`CraftPlanner` now lives in Jobs. The important lesson is that Planner could move only after recipe lookup was hidden behind `ICraftRecipeCatalog`; otherwise Jobs-owned craft code would keep reaching into the global `RecipeRegistry.Instance` singleton.

### Diff-based systems need regression tests before movement

Any direct world mutation replaced by diffs must have a small regression first or immediately after.

Useful examples already added:

- construction material consumption emits item remove diff;
- construction residual relocation emits `MoveItem`;
- transport split-stack rollback is non-mutating on failure;
- craft input failure preserves queue entry.

### Do not rederive chunk/local targets in App emitters

App diff emitters should not hand-roll:

```text
chunkX = worldX / Chunk.SIZE_XY
localX = worldX % Chunk.SIZE_XY
localIndex = Chunk.LocalIndex(localX, localY)
```

Use `WorldCellTargetEncoding` instead, then pass the resulting `WorldCellTarget` directly to `ItemsDiffLog` where possible. The older `ChunkKey + localIndex` item-diff overloads remain for compatibility, while general `DiffLog` still uses `DiffTarget`.

### Planner and executor must share domain rules

Craft exposed a real bug:

- planner treated workshop footprint plus adjacent ring as available input area;
- consumer only consumed from the footprint.

Result: jobs could be planned as ready but fail at consumption time.

Rule: if planner and executor both reason about the same gameplay concept, extract that rule into shared helper code.

### Do not move projects before dependencies are inverted

Transport, mining, construction, and craft executor cores now live in `HumanFortress.Jobs`, and craft planning has also moved there. App still owns the composition shells, concrete adapters, and runtime glue that depend on UI, content singletons, or runtime wiring.

Moving them too early would drag App/runtime/navigation dependencies into the wrong assemblies. Invert Navigation and stabilize contracts first.

### Keep build verification short and explicit

Fast checks:

```bash
/opt/homebrew/opt/dotnet@8/bin/dotnet build src/HumanFortress.App/HumanFortress.App.csproj --no-restore --no-dependencies -m:1 -v:quiet -p:RunAnalyzers=false
./RunTests.sh
```

Full check:

```bash
/opt/homebrew/opt/dotnet@8/bin/dotnet build HumanFortress.sln --no-restore -m:1 -v:quiet -p:RunAnalyzers=false
git diff --check
```

If a command produces no output for too long, investigate instead of waiting indefinitely.
