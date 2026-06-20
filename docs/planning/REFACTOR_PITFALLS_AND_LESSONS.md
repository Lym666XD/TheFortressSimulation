# HumanFortress Refactor Pitfalls and Lessons

Date: 2026-06-06

This document records practical pitfalls found during the current architecture refactor. It is meant to keep future refactor work fast and predictable.

## Content Boundary Pitfalls

### Prefer one Content load result over split loaders

`HumanFortress.Content` now coordinates runtime content bootstrap plus data/core catalog loading. App/runtime composition should consume the Content-owned entry points instead of independently calling:

```text
ItemDefinitionCatalogLoader.Load(...)
CreatureDefinitionCatalogLoader.Load(...)
CoreDataRegistryLoader.Load(...)
RuntimeContentRegistryLoader.Load(...)
```

Current first-pass shape:

```text
HumanFortress.Content
  resolves published/source content paths
  resolves App registry files under content/registries
  loads legacy + structured runtime registries through FortressContentLoader / RuntimeContentRegistryLoader
  loads item/creature definitions
  loads construction/recipe core data
  returns immutable catalog snapshots and diagnostics

App/Runtime composition
  applies snapshots to world managers
  captures runtime catalog/tuning dependencies through FortressRuntimeContentSnapshotLoader
  exposes construction/recipe catalogs through the structured ContentRegistry while legacy compatibility remains
```

Do not introduce new App-local JSON traversal or a second content bootstrapper while this is being consolidated.

### Resolve App registry files through Content

App-side convenience registries still exist for UI/input/profession presentation, but they should not hard-code the published output layout. Use:

```csharp
FortressContentLoader.ResolveRegistryFile(baseDir, "some.registry.json")
```

instead of:

```csharp
Path.Combine(baseDir, "content", "registries", "some.registry.json")
```

This keeps published builds and source-checkout runs on the same path resolution rules. Current migrated call sites include input bindings, order display names, profession definitions, workshop category mapping, and scheduler/workshop tuning compatibility loaders.

### Keep data/core JSON traversal out of App

Construction/workshop and recipe loading now enters through the Content-owned core catalog loader:

```csharp
CoreContentCatalogLoader.Load(dataCorePath)
```

Do not reintroduce App-local parsing for:

```text
data/core/workshops/core_workshop_*.json
data/core/placeable/workshops.json
data/core/recipes/*.json
```

`SimulationWorldContentLoader` should not locate the runtime content directory itself. It now calls `FortressContentLoader`, and schema compatibility plus registry population belong behind the Content/structured registry boundary.

Important compatibility behavior preserved by the Content-owned core-data loader:

- new workshop files and legacy `placeable/workshops.json` are both loaded;
- duplicate construction ids are skipped and counted instead of failing startup;
- recipe files may be root arrays or `{ "recipes": [...] }` documents;
- legacy recipe aliases such as `workshop_id`, `workshop`, `duration_ticks`, and `primary_skill` still parse.

### Construction and recipe catalogs should be snapshots, not singletons

`ConstructionRegistry` and `RecipeRegistry` singleton compatibility classes have been deleted. `CoreDataRegistryLoader.Load(...)` parses core data into fresh immutable snapshots, and `ContentRegistry.ApplyCoreData(...)` swaps those snapshots into the runtime registry instance.

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

Do not add new `ConstructionRegistry.Instance.Get...` or `RecipeRegistry.Instance.Get...` reads in runtime systems. The normal read path is now:

```text
ContentRegistry.Instance.Constructions
ContentRegistry.Instance.Recipes
```

Those properties expose immutable snapshots through read-only interfaces. Keep it that way; do not recreate the old singleton classes for convenience.

Preferred direction:

```text
ContentRegistry
  owns load/validation/indexing
  swaps immutable construction/recipe catalog snapshots
  exposes read-only construction/recipe catalog interfaces

Runtime/App
  request definitions through catalog interfaces
  do not parse content files directly
```

Do not move Jobs-owned code back to `RecipeRegistry.Instance`. Craft already uses `ICraftRecipeCatalog`; future construction/craft/runtime seams should follow that pattern.

Runtime command targets should not keep "helpful" global fallbacks. `SimulationRuntimeContext` now requires explicit `IRecipeCatalog` and `IConstructionCatalog` dependencies for workshop queue commands. If a test or tool needs a context, pass `RecipeCatalogStore.Empty`, `ConstructionCatalogStore.Empty`, or a small in-memory catalog explicitly.

App UI helpers should also consume the active runtime session catalog facade instead of reaching for the global structured registry. Current migrated paths include:

- workshop panel keyboard default recipe lookup;
- workshop panel context resolution;
- map-click workshop detection;
- build-menu workshop category selection;
- workshop category mapping;
- workshop overlays and workshop panel title/footprint lookup.

Runtime geology display/application reads follow the same rule. Use the active `IRuntimeGeologyCatalog` from the runtime session facade for map rendering, tile popups, and terrain diff application. Do not add new direct geology lookups from `ContentRegistry.Instance` inside Runtime, Jobs, Simulation diff application, or App rendering helpers.

Construction planning/execution also uses explicit dependencies now:

```text
ConstructionSystem
  -> IConstructionTerrainMaterialResolver
  -> ConstructionTuning

ConstructionJobExecutor
  -> ConstructionTuning
  -> PlaceableTuning
```

The App adapter may bridge those calls to the transitional structured registry, but Simulation/JOBS-owned construction logic should not call `ConstructionTuning.LoadFromContent()` or `ContentRegistry.Instance` directly.

Runtime tuning objects should enter through the Content-owned runtime snapshot:

```text
FortressRuntimeContentSnapshot
  -> constructions / recipes
  -> runtime geology catalog
  -> zone definitions
  -> ConstructionTuning.LoadFromJson(...)
  -> mining tuning JSON for MiningDropResolver
  -> NavigationTuning.LoadFromJson(...)
  -> PlaceableTuning.LoadFromJson(...)
  -> SchedulerTunings.LoadFromJson(...)
  -> WorkshopTunings.LoadFromJson(...)
```

Do not add new `LoadFromContent(...)` or `LoadFromRegistry(...)` convenience paths for runtime tuning. Those helpers tend to recreate hidden global-content reads and make hot reload/content reload behavior inconsistent.

Structured core-data application should also stay behind the Content-owned snapshot loader:

```text
FortressRuntimeContentSnapshotLoader.ApplyCoreData(...)
  -> ContentRegistry.ApplyCoreData(...)
  -> CaptureLoaded()
  -> returns constructions / recipes / geology / zones / tuning JSON
```

`SimulationWorldContentLoader` may inject snapshots into the active `World`, but it should not call `ContentRegistry.Instance.ApplyCoreData(...)` or read `ContentRegistry.Instance.Zones` directly. Use the returned `FortressRuntimeContentSnapshot.ZoneDefinitions` when registering zone definitions with the simulation world.

App adapter seams should also consume the active runtime snapshot instead of the singleton registry:

```text
MiningDropResolver
  -> IRuntimeGeologyCatalog
  -> tuning.mining JSON

ConstructionTerrainMaterialResolver
  -> IRuntimeGeologyCatalog
```

Do not reintroduce direct `ContentRegistry.Instance` reads in these adapters just because they live in App. They are composition edges and must reflect the same active-session snapshot as Runtime/JOBS-owned systems.

WorldGen follows the same rule. Fortress generation should receive explicit content:

```text
FortressGenerationContent
  -> IRuntimeGeologyCatalog
  -> tuning.mapgen JSON
  -> tuning.ore JSON
  -> tuning.cavern JSON

FortressGenerator / FortressMap / FortressChunk
  -> consume FortressGenerationContent or its geology catalog
  -> never read ContentRegistry.Instance directly
```

`GameStateManager` caches the active `FortressRuntimeContentSnapshot` returned by session content loading. Reuse that snapshot for navigation tuning, runtime dependency composition, and fortress generation. Do not recapture the global registry from `FortressSessionInitializer` or from WorldGen just because the registry is already loaded.

Repeated core-data loads must replace construction/recipe snapshots, not append to mutable indexes. The current smoke tests verify construction count, recipe count, construction category queries, and workshop recipe queries stay stable after reload.

### Do not depend on full managers for static definitions

`ItemManager` and `CreatureManager` still expose definition lookup alongside runtime instances, but they no longer parse content files. Static definition storage is supplied as immutable catalog snapshots from the Content boundary.

When a system only needs definition metadata, use the read-only catalog seams:

```csharp
IItemDefinitionCatalog
ICreatureDefinitionCatalog
```

Examples already migrated:

- construction material matching in `ConstructionMaterialTracker`;
- material source planning in `ConstructionMaterialsPlanner`;
- profession roster display-name lookup in `ProfessionAssignments`.

Do not add new gameplay systems that call `world.Items.GetDefinition(...)` or `world.Creatures.GetDefinition(...)` just because a `World` is nearby. Prefer explicit catalog injection. App UI/render/debug code may still read from managers for presentation until those surfaces get their own view-model/catalog cleanup.

### Rebuild definition indexes through fresh snapshots

Repeated content loads must be idempotent. `HumanFortress.Content.Definitions` loaders parse files into fresh immutable catalog snapshots, and managers replace their current snapshot through `SetDefinitionCatalog(...)`.

Do not append to existing indexes during reload. This creates hidden duplication bugs where:

```text
DefinitionCount remains stable
GetByKind(...) / GetByTag(...) grows after every reload
```

That kind of bug is easy to miss because normal startup loads once, while tests, hot reload, content validation tools, and future mod reload flows may load repeatedly.

Current direction:

```text
loader
  parses and validates JSON into a fresh result

catalog snapshot
  builds definition/id/tag/kind indexes from the fresh result

manager
  swaps to the fresh catalog snapshot
  does not parse files or incrementally append stale static definitions
```

Do not reintroduce file IO or JSON validation into `ItemManager` or `CreatureManager`. App/runtime composition should load content snapshots and inject them.

Stockpile filtering is still a partial TODO path. Do not wire it back to the legacy `HumanFortress.Core.Content.ContentRegistry`; the correct next shape is an explicit item definition catalog seam, matching construction material planning and profession roster naming.

### Move contracts by assembly before rewriting namespaces

Navigation contracts already use a transitional compatibility pattern: types compile from `HumanFortress.Contracts` while keeping their old namespace until a broader namespace cleanup is safe.

Static item/creature definitions now follow the same pattern. These types compile from `HumanFortress.Contracts`:

```text
ItemDefinition
CreatureDefinition
IItemDefinitionCatalog
ICreatureDefinitionCatalog
ItemDefinitionCatalogStore
CreatureDefinitionCatalogStore
PlaceableProfile
Footprint
PassabilityMode
EffectsBlock
```

Do not move them back into Simulation/Core just because their namespace still looks old. The namespace is intentionally transitional to avoid a repo-wide rewrite in the same pass.

Preferred direction:

```text
first pass
  move assembly ownership to Contracts
  preserve namespaces
  keep builds/tests stable

later cleanup
  rename namespaces once content/runtime ownership is stable
```

Avoid making `HumanFortress.Contracts` depend on `HumanFortress.Core` or `HumanFortress.Simulation`. If a shared DTO needs a Core type, the shared type should move down into Contracts with it.

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

### Do not parallel-build overlapping project graphs

Avoid running App build, Content build, and test-project build at the same time. Overlapping project graphs can touch the same `obj` files, including:

```text
src/HumanFortress.Content/obj/Debug/net8.0/HumanFortress.Content.dll
src/HumanFortress.Content/obj/Debug/net8.0/ref/HumanFortress.Content.dll
src/HumanFortress.App/obj/Debug/net8.0/apphost
```

On macOS this caused file-lock/copy/signing races:

- `HumanFortress.Content.dll` could not be opened for writing
- `ref/HumanFortress.Content.dll` could not be copied
- `apphost` not found during copy
- `apphost: is already signed`

Use sequential build/test commands.

When running a built DLL with `dotnet exec`, do not add the `dotnet run`-style argument separator:

```bash
# Correct
/opt/homebrew/opt/dotnet@8/bin/dotnet exec src/HumanFortress.App/bin/Debug/net8.0/HumanFortress.App.dll --init-only --strict-content --content-warnings-as-errors

# Wrong: passes a literal "--" to the app and may skip init-only parsing
/opt/homebrew/opt/dotnet@8/bin/dotnet exec src/HumanFortress.App/bin/Debug/net8.0/HumanFortress.App.dll -- --init-only --strict-content --content-warnings-as-errors
```

### Audit apparent hangs before assuming build is still running

When the Codex turn appears to be waiting for many minutes, first determine whether a backend command is actually still running. We have seen apparent long waits where no build/game process existed; only VS Code's Roslyn language server was alive. That means the delay was a session/front-end wait or an interrupted turn, not a `.NET` compile.

Use a bracketed `pgrep` pattern so the search command does not match itself:

```bash
pgrep -fl "[d]otnet|[H]umanFortress|[M]SBuild|[V]BCSCompiler"
```

Interpretation:

- only `Microsoft.CodeAnalysis.LanguageServer` / Roslyn: normal editor background process, not a stuck game/build;
- `dotnet build`, `MSBuild`, or `VBCSCompiler` lasting longer than expected: investigate build output, then stop/retry sequentially if needed;
- `dotnet exec ... HumanFortress.App.dll` without `--init-only`: likely a normal game loop, not a test/init command.

In the managed sandbox, broad `ps` may fail with `operation not permitted`; prefer `pgrep -fl` for this check.

Operational rule: if a build/run command has no output for about 30 seconds, check the process list and report the state instead of waiting indefinitely.

Important agent limitation: Codex does not have an independent wall-clock timer that wakes it up while a tool call is still pending. The "30 seconds" rule only works if commands are launched with short wait windows and the agent regains control at the tool boundary. Do not rely on the agent to notice elapsed time while a long-running tool call is hung at the session/front-end layer.

Preferred mitigation:

- split verification into short, sequential commands instead of one long command chain;
- prefer build/test commands that exit on their own;
- avoid interactive or normal game-loop commands unless explicitly testing the UI;
- if a command returns no output in the first wait window, immediately run the `pgrep` audit before continuing;
- when doing broad mechanical refactors, batch several source edits first, then run one bounded verification pass instead of compiling after every tiny edit.

For very large refactors, it is acceptable to ask the human to run the full local compile manually while the agent continues reading/designing. The agent should still run lightweight checks it can finish reliably, such as `rg` scans and `git diff --check`.

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

### Do not let timing budgets make deterministic tests flaky

The legacy Phase D concurrent pathfinder test originally used the production `NavigationTuning.Default` budget:

```text
MaxMsPerTickPathing = 3
```

`PathService.Solve` is allowed to return `Path.Invalid` and queue work for a later tick when that per-tick budget is exceeded. Under slower thread scheduling, the concurrent test could report:

```text
Only 8/10 paths were found
```

That was not proof of an unreachable path; it was normal budget deferral being treated as a failure. Tests that assert pathfinding correctness should use a test-specific tuning budget large enough to avoid scheduler noise. Separate tests should cover production budget/queue behavior explicitly.

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

`IProfessionAssignmentCommandTarget` is now a Runtime-owned seam. Runtime keeps `HumanFortress.Core.Commands.ISimulationContext` free of job-system details, while App composition supplies a weight-write callback from the Jobs-owned `ProfessionAssignments` instance during host composition.

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

Profession assignment remains a special case: Runtime only stores an injected weight-write callback, while App composition still wires the Jobs-owned `ProfessionAssignments` instance into the active session systems.

Do not use `StockpileDiff` as a migration target yet. Its applicator is not attached to the active tick pipeline and still contains TODO paths for job creation and item placement/removal.

### Do not let GameStateManager recreate the runtime graph by hand

`GameStateManager` previously stored separate `World`, `NavigationManager`, and `SimulationRuntimeHost` fields and assembled them directly inside `InitializeWorld`. That made it both a state machine and an implicit composition root.

Current first pass:

```text
GameStateManager
  -> Runtime-owned SimulationRuntimeSessionFactory<SimulationRuntimeHost<SimulationRuntimeSystems>>.CreateNew(...)
  -> App content-loading callback
  -> Runtime-owned FortressRuntimeHostFactory through an App callback that supplies logging/content inputs
  -> Runtime-owned SimulationRuntimeSession<SimulationRuntimeHost<SimulationRuntimeSystems>>(World, Navigation, Host)
  -> Runtime-owned generic SimulationRuntimeHost<TSystems>
  -> Runtime-owned SimulationRuntimeHostCore
```

Keep future runtime construction behind this seam. UI/state code should not directly reset schedulers, clear command queues, create navigation managers, or new up runtime hosts. Content loading and concrete system construction are still App callbacks because they depend on current content registries, job adapters, logger callbacks, UI hooks, and SadConsole-facing lifetime.

`SimulationRuntimeHostCore` owns scheduler restart, tick-system registration, pipeline attachment, and stop-time pipeline detachment. `SimulationRuntimeHost<TSystems>` owns the generic lifecycle shell. `SimulationRuntimeSystems`, `FortressRuntimeDependencies`, `FortressRuntimePlanningSystems`, `FortressRuntimeJobSystems`, `FortressRuntimeSystemsFactory`, `FortressRuntimeHostFactory`, and `FortressRuntimeStartup` now compile from Runtime. `FortressRuntimeDependencies` is split into `FortressRuntimeCatalogs`, `FortressRuntimeTunings`, and `FortressRuntimeWorkforce`. App still supplies logger callbacks and optional command delegates, such as auto-dig, because those remain App-specific.

Do not collapse those groups back into one long factory method. They are migration handles: dependencies point toward Content/Runtime, planners point toward Simulation/Jobs boundaries, and job-system shells point toward Jobs/App adapter cleanup.

`FortressRuntimeHostFactory` should create `FortressRuntimeDependencies` once and use that same instance for both `SimulationRuntimeContext` catalog injection and concrete system creation. If host construction reads content separately from systems creation, command targets and gameplay systems can accidentally observe different catalog snapshots after a future hot-reload/content-reload pass. It should receive logging as callbacks; do not reintroduce direct App `Logger` calls into Runtime source.

For runtime composition, structured registry reads should stay behind the Content-owned `FortressRuntimeContentSnapshotLoader`. `FortressRuntimeDependencies.Load(...)` should consume that snapshot, then split it into catalog/tuning/geology/workforce groups. Do not add new direct `ContentRegistry.Instance` / `JObject` tuning reads to host factory, systems factory, job-system group creation, runtime command targets, App rendering helpers, App workshop/build UI helper code, or Navigation.

Scheduler/workshop tuning loaders no longer keep direct file/registry compatibility paths. Runtime composition should use:

```text
FortressRuntimeContentSnapshot
  -> ConstructionTuning.LoadFromJson(...)
  -> NavigationTuning.LoadFromJson(...)
  -> PlaceableTuning.LoadFromJson(...)
  -> SchedulerTunings.LoadFromJson(...)
  -> WorkshopTunings.LoadFromJson(...)
```

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

`HumanFortress.Navigation` no longer references `HumanFortress.Simulation` or `HumanFortress.Core`. Do not pass `World`, `Chunk`, `TileBase`, `TerrainKind`, content registries, or Core tuning loaders into Navigation internals.

Use the Contracts-owned source/snapshot contracts instead:

```text
INavigationWorldSource
NavigationChunkSnapshot
NavigationTile
NavigationTileKind
```

The current Simulation adapter lives in `HumanFortress.Runtime` as `SimulationNavigationSource`. Keep it there unless a more explicit world-navigation adapter package is introduced; do not move Simulation type knowledge back into Navigation.

Navigation tuning follows the same dependency rule:

```text
Content snapshot
  -> NavigationTuning.LoadFromJson(...)
  -> SimulationNavigationFactory.Create(world, rebuildAll, tuning)
  -> NavigationManager(...)
  -> PathService(...)
```

Do not reintroduce `NavigationTuning.LoadFromContent()` or a Core/Content project reference from `HumanFortress.Navigation`.

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
- the old registry material-loading bug was removed with the old registry source; the structured registry currently reports 83 materials in startup logs;
- `RuntimeContentRegistryLoader` now loads only the structured runtime registry behind `FortressContentLoader`;
- structured registry loading now supports top-level array material files and resets validation state before reload;
- geology cross-reference errors are clear and currently clean;
- construction duplicates are explicitly skipped and counted;
- recipe loading reports `errors=0`.

Remaining architecture risk: there is now one normal runtime registry source model, the structured registry, and production direct reads are concentrated in Content bootstrap/snapshot capture/application. The old legacy registry source has been deleted, and the structured registry implementation now compiles from `HumanFortress.Content.Registry`. The remaining risk is policy and compatibility: strict content-load failure rules, richer diagnostics/debug surfaces, and cleanup of the few remaining non-registry content DTO compatibility namespaces should be handled without adding new singleton reads.

Runtime geology and zone JSON DTOs have moved to `HumanFortress.Contracts` while keeping their old namespace. The zone loader now uses explicit `System.Text.Json` property mappings, which prevents `zones.json` snake_case fields such as `display_name`, `ui_hints`, and `default_policies` from silently deserializing to defaults.

Item and creature definition loading has now moved into `HumanFortress.Content.Definitions`, and Simulation managers consume snapshots instead of reading files. Construction/recipe loading now also produces immutable snapshots owned by the structured registry and exposed through `FortressRuntimeContentSnapshot`. Construction/recipe definitions and catalog interfaces/stores compile from Contracts. The remaining registry-unification risk is strict-mode diagnostics and compatibility naming, not Core-owned registry implementation or App-local loading orchestration.

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

`TransportAssignmentHandler` now lives in Jobs. Profession weighting is behind `ITransportWorkerCandidateSource`, and logging is behind `ITransportJobLogger`. Keep those seams narrow; do not pass App globals or UI-facing services back into Jobs-owned handlers.

`TransportReplanHandler` now lives in Jobs. It only depends on `ITransportMovementDiffEmitter.MoveCreature` instead of the full App `TransportDiffEmitter`. Preserve that narrow dependency: replan should not learn how to split stacks, mark carry state, or move items.

`TransportPickupHandler` and `TransportDeliveryHandler` now live in Jobs. They depend on `ITransportItemDiffEmitter` for item/carry/split diffs and `ITransportJobCompletionSink` for profession progress. Keep destination validation in Simulation and keep App-specific profession objects behind the completion sink.

`TransportActiveJobRunner` now lives in Jobs. It should remain a coordinator over movement update, replan, pickup, delivery, and missing-worker cleanup. It depends on separate movement and item/carry diff interfaces; do not collapse those back into a monolithic concrete emitter.

`TransportJobExecutor` now owns the transport tick core in Jobs: request drain/backlog, assignment throttle, active write tick, scheduling hints, and debug snapshots. The Runtime-owned `TransportJobSystem` should stay a composition shell over narrow Jobs interfaces.

### Mining extraction now follows the same shell/core pattern

The mining executor has the same ownership rule as transport: Jobs owns the tick core and concrete job adapters/emitters; Runtime owns the tick-facing wrapper; App only supplies composition-time logger/content/UI callbacks.

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

Do not make Jobs-owned mining code depend directly on App globals or App-owned concrete services. The tick-facing `MiningJobSystem` wrapper should remain only a composition shell, and executor dependencies should continue crossing through the narrow mining interfaces above. Source-owned Jobs/Runtime helpers may still have the old namespace until the compatibility cleanup pass, but they should not regain App assembly ownership.

### Construction extraction uses callback-only App ownership

Construction is simpler than transport/mining because it does not own pathing or worker assignment. Diff emission and logging bridges are now Jobs-owned, while App ownership is limited to binding the UI workshop-completion callback during bootstrap.

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

Do not pass `Logger`, concrete UI services, or the static `ConstructionJobSystem.UiNotifyWorkshopComplete` hook directly into Jobs-owned construction code. The Runtime-owned `ConstructionJobSystem` should remain only a composition shell over narrow Jobs interfaces and callback injection.

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

Do not pass `RecipeRegistry` or App globals deeper into Jobs-owned craft code. `CraftJobSystem` is now a Runtime-owned composition shell, while `CraftDiffEmitter`, `ProfessionAssignments`, and `WorkerSelectionStrategy` are Jobs-owned compatibility-namespace types.

`CraftPlanner` now lives in Jobs. The important lesson is that Planner could move only after recipe lookup was hidden behind `ICraftRecipeCatalog`; otherwise Jobs-owned craft code would keep reaching into the global `RecipeRegistry.Instance` singleton.

### Diff-based systems need regression tests before movement

Any direct world mutation replaced by diffs must have a small regression first or immediately after.

Useful examples already added:

- construction material consumption emits item remove diff;
- construction residual relocation emits `MoveItem`;
- transport split-stack rollback is non-mutating on failure;
- craft input failure preserves queue entry.

### Do not rederive chunk/local targets in job emitters

Job diff emitters should not hand-roll:

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

Transport, mining, construction, and craft executor cores now live in `HumanFortress.Jobs`, and craft planning has also moved there. Runtime now owns the tick-facing job wrappers. App still owns concrete session/runtime glue that depends on UI lifetime, logger callbacks, and bootstrap wiring.

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
