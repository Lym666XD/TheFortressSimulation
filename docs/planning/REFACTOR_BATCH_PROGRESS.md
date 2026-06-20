# Refactor Batch Progress

This document tracks the current multi-step refactor batches so progress is visible without relying on chat history.

## Current Status Snapshot - 2026-06-19

- `HumanFortress.App/Jobs` no longer contains active source files.
- Profession contracts now compile from `HumanFortress.Contracts` under `HumanFortress.Contracts.Jobs`.
- Profession assignment state now lives in `HumanFortress.Jobs`; the old App-owned Jobs namespace has been removed from active source.
- Profession registry JSON loading now lives in `HumanFortress.Content.Definitions`.
- Content registry contracts now compile from `HumanFortress.Contracts.Content.Registry`.
- The structured runtime content registry implementation now compiles from `HumanFortress.Content.Registry`.
- `CoreDataRegistryLoader` now compiles from `HumanFortress.Content.Definitions`.
- Tick-facing transport/mining/construction/craft job wrappers now live in `HumanFortress.Runtime/Jobs`.
- Runtime dependency grouping (`FortressRuntimeDependencies`, catalogs, tunings, and workforce composition) now lives in `HumanFortress.Runtime`.
- Runtime concrete system composition (`SimulationRuntimeSystems`, `FortressRuntimeSystemsFactory`, planning groups, and job-system groups) now lives in `HumanFortress.Runtime`.
- Jobs owns the executor cores, diff emitters, callback loggers, profession/craft adapters, scheduler/workshop tuning types, worker-selection strategy, unified jobs orchestrator, sanitizer, mining drop resolver, and construction terrain-material resolver.
- Runtime host factory and generic startup orchestration now live in `HumanFortress.Runtime`; App injects logging and the optional auto-dig command delegate.
- Runtime command source files now live in `HumanFortress.Runtime/Commands` under the `HumanFortress.Runtime.Commands` namespace.
- Runtime composition types now use the `HumanFortress.Runtime` namespace, and tick-facing job wrappers now use `HumanFortress.Runtime.Jobs`.
- App still owns the SadConsole/platform host, logger callback binding, auto-dig command implementation, UI bootstrap, construction completion UI notification binding, and live UI/debug access surfaces.
- Remaining high-priority architecture work is reducing transitional internal bridges, cleaning remaining compatibility namespaces, and introducing UI/debug snapshot facades.

## Current No-Build Batch: Content Registry Namespace Cleanup

Status: build verified

### Completed

- Changed content registry contract DTOs, catalog interfaces/stores, tuning types, `IRuntimeGeologyCatalog`, `Footprint`, and the material `FixedPoint` helper from the old Core registry/content namespaces to `HumanFortress.Contracts.Content.Registry`.
- Changed the structured runtime `ContentRegistry` implementation and its helper registries from the old Core registry namespace to `HumanFortress.Content.Registry`.
- Changed `CoreDataRegistryLoader` from the old Core registry namespace to `HumanFortress.Content.Definitions`.
- Updated App, Runtime, Jobs, Simulation, WorldGen, Content, and regression-test call sites to use `HumanFortress.Contracts.Content.Registry` for contract types.
- Updated `ContentRegistry.Instance` call sites and structured registry aliases to use `HumanFortress.Content.Registry.ContentRegistry`.
- Added a direct `HumanFortress.Contracts` project reference to `HumanFortress.WorldGen` because WorldGen now directly consumes Contracts content types.
- Updated active architecture/rules/planning docs so current ownership no longer claims that the structured registry preserves the old Core registry namespace.

### Verification

- First fast build caught a namespace follow-up bug: `MaterialDefinition` had moved to `HumanFortress.Contracts.Content.Registry` while `FixedPoint` still lived under the old Core content namespace.
- Moved `FixedPoint` into `HumanFortress.Contracts.Content.Registry`.
- `HumanFortress.sln` fast build then passed with `0 Warning(s), 0 Error(s)` using .NET 8, `--no-restore`, single MSBuild worker, analyzers disabled, and `UseAppHost=false`.
- Source scans found no active source or test files using the old Core content registry namespace.
- `git diff --check` passed after the source namespace migration.

## Verified Batch: Runtime and Jobs Namespace Cleanup

Status: build verified

### Completed

- Changed Runtime composition source files that had already moved into `src/HumanFortress.Runtime` from the transitional `HumanFortress.App.Runtime` namespace to `HumanFortress.Runtime`.
- Changed tick-facing transport/mining/construction/craft wrapper systems in `src/HumanFortress.Runtime/Jobs` to the `HumanFortress.Runtime.Jobs` namespace.
- Changed Jobs-owned diff emitters, callback loggers, profession assignment/adapters, scheduler/workshop tunings, worker selection, unified jobs orchestration, sanitizer, mining drop resolver, and construction terrain-material resolver from the old App-owned namespace to `HumanFortress.Jobs`.
- Kept App UI/session glue under `HumanFortress.App.Runtime`; only non-UI Runtime composition source moved to the Runtime namespace.
- Split the profession namespace cleanup into its real module owners:
  - profession contracts now use `HumanFortress.Contracts.Jobs`
  - profession registry JSON loading now uses `HumanFortress.Content.Definitions`
  - profession assignment state remains in `HumanFortress.Jobs`
- Updated App, Runtime, and regression-test call sites to reference `HumanFortress.Runtime.Jobs`, `HumanFortress.Jobs`, and the Content/Contracts profession namespaces.
- Updated active architecture and simulation docs so they no longer describe the old App-owned Jobs namespace as live compatibility surface.

### Verification

- `HumanFortress.sln` fast build passed with `0 Warning(s), 0 Error(s)` using .NET 8, `--no-restore`, single MSBuild worker, analyzers disabled, and `UseAppHost=false`.
- Source scans found no active source files using the old App-owned Jobs namespace.
- Source scans found no moved Runtime source files still declaring `HumanFortress.App.Runtime`.
- `git diff --check` passed before the build.
- Content registry compatibility namespaces were handled in the follow-up content registry namespace cleanup batch.

## Current Batch: Runtime Generic Host and App Factory Cleanup

Status: completed

### Completed

- Added `HumanFortress.Runtime.SimulationRuntimeHost<TSystems>` as a Runtime-owned generic runtime host over `SimulationRuntimeHostCore`.
- Deleted the old App-owned `SimulationRuntimeHost` wrapper.
- Moved `SimulationRuntimeSystems` into `HumanFortress.Runtime`; it is now a Runtime-owned system collection and tick-registration surface.
- Added `FortressRuntimeLogging` as a small injected logging callback bundle so Runtime composition no longer calls App `Logger` directly.
- Initially added `FortressRuntimeHostFactory` in App as a temporary App composition bridge:
  - creates `SimulationRuntimeHost<SimulationRuntimeSystems>`
  - injects recipe/construction catalogs into `SimulationRuntimeContext`
  - registers the profession-weight callback without making Runtime depend on App professions
- Moved `FortressRuntimeHostFactory` into `HumanFortress.Runtime`; App now supplies `FortressRuntimeLogging` and the active content snapshot instead of letting the factory call App `Logger`.
- Added then moved `FortressRuntimeStartup` into `HumanFortress.Runtime`; initial-worker/profession setup is Runtime-owned, while optional auto-dig remains an App-provided command delegate.
- Updated `GameStateManager` so it no longer directly constructs the runtime host or owns initial-worker/auto-dig startup details. It now delegates host creation to `FortressRuntimeHostFactory` and startup hooks to `FortressRuntimeStartup`.
- Split concrete system assembly out of `SimulationRuntimeSystems` into `FortressRuntimeSystemsFactory`, leaving `SimulationRuntimeSystems` as a system collection plus tick-registration surface.
- Split `FortressRuntimeSystemsFactory` into explicit Runtime-owned runtime system groups:
  - `FortressRuntimeDependencies` for content catalogs, scheduler/workshop tunings, craft recipe adapter, and profession assignments
  - `FortressRuntimePlanningSystems` for mining/hauling/construction/craft planners and the shared transport request queue
  - `FortressRuntimeJobSystems` for mining/transport/construction/craft job executor shells
- Changed `FortressRuntimeHostFactory` to create one `FortressRuntimeDependencies` instance and pass the same construction/recipe catalogs into both `SimulationRuntimeContext` and `FortressRuntimeSystemsFactory`, removing duplicate content-registry reads from the runtime composition path.
- Split `FortressRuntimeDependencies` into smaller dependency groups:
  - `FortressRuntimeCatalogs` for construction/recipe catalogs and the craft recipe adapter
  - `FortressRuntimeTunings` for scheduler/workshop tunings
  - `FortressRuntimeWorkforce` for composing the Content-loaded profession registry with Jobs-owned profession assignment state
- Added a Content-owned `FortressRuntimeContentSnapshot` and loader so runtime composition captures construction/recipe catalogs plus scheduler/workshop tuning JSON through `HumanFortress.Content` instead of directly walking the structured registry from App.
- Added `ContentRegistry.GetTuningJson(...)` as the transitional structured-registry JSON export used by the Content snapshot loader.
- Changed `SchedulerTunings` and `WorkshopTunings` to load from JSON strings supplied by the runtime content snapshot, removing App runtime composition's direct dependency on `JObject` tuning reads.
- Changed `FortressRuntimeDependencies.Load(...)` to consume the Content-owned runtime snapshot; direct structured-registry access for runtime catalog/tuning capture is now behind `HumanFortress.Content.Loading`.
- Removed the Runtime `SimulationRuntimeContext` fallback to `ContentRegistry.Instance`; recipe and construction catalogs are now required constructor dependencies for workshop queue command targets.
- Exposed the active runtime session's recipe/construction catalogs through `SimulationRuntimeHost<TSystems>`, `GameStateManager`, and `FortressRuntimeAccess`.
- Changed workshop UI helpers, build keyboard workshop selection, workshop category mapping, workshop overlays, and workshop panel rendering to consume the active runtime construction/recipe catalogs instead of directly reading `ContentRegistry.Instance`.
- Added `IRuntimeGeologyCatalog` as a read-only geology catalog seam over the structured registry and included it in the runtime content snapshot.
- Exposed the active runtime geology catalog through `SimulationRuntimeHost<TSystems>`, `GameStateManager`, and `FortressRuntimeAccess`.
- Changed map terrain rendering, tile popups, and the simulation diff applicator to consume the injected runtime geology catalog instead of directly reading `ContentRegistry.Instance`.
- Changed `RenderSnapshotBuilder` to receive an explicit construction catalog, removing its direct construction-registry read from `HumanFortress.Simulation.Rendering`.
- Changed `ConstructionSystem` to receive explicit construction tuning plus an `IConstructionTerrainMaterialResolver`; App now provides the current Content-backed terrain-material resolver.
- Changed Jobs-owned construction execution to receive construction tuning from App composition instead of calling `ConstructionTuning.LoadFromContent()` internally.
- Moved mining channel air-geology lookup behind the mining drop resolver seam, so Jobs-owned mining result application no longer reads the global content registry.
- Changed `NavigationTuning` to parse injected JSON and removed `HumanFortress.Navigation`'s dependency on `HumanFortress.Core`.
- Changed runtime session creation to load content before creating the shared `NavigationManager`, then build navigation with the runtime snapshot's navigation tuning.
- Exposed `NavigationTuning` through the generic runtime host and runtime facade so Runtime job wrappers, navigation overlay, and debug path tooling use one active-session tuning source.
- Added `tuning.placeable` to the runtime content snapshot and injected `PlaceableTuning` into construction completion so completed placeables no longer implicitly use hard-coded defaults when content provides tuning.
- Removed unused scheduler/workshop direct file/registry tuning loaders; runtime composition now consumes scheduler/workshop tuning JSON only through the Content-owned snapshot.
- Removed unused `ConstructionTuning.LoadFromContent()` and replaced `PlaceableTuning.LoadFromContent()` with `LoadFromJson(...)`, preventing new Core-side global registry reads for tuning.
- Added smoke coverage for navigation and placeable tuning JSON parsing.
- Added mining tuning JSON to the Content-owned runtime snapshot and changed App `MiningDropResolver` to use injected `IRuntimeGeologyCatalog` plus that JSON instead of reading `ContentRegistry.Instance`.
- Changed App `ConstructionTerrainMaterialResolver` to use injected `IRuntimeGeologyCatalog`, removing the remaining construction planner adapter reads from `ContentRegistry.Instance`.
- Added mapgen/ore/cavern tuning JSON to the Content-owned runtime snapshot.
- Added `FortressGenerationContent` and injected geology plus mapgen/ore/cavern tuning into `FortressGenerator`, `FortressMap`, and `FortressChunk`, removing WorldGen production reads from `ContentRegistry.Instance`.
- Cached the active runtime content snapshot in `GameStateManager` during session content loading, then reused that same snapshot for navigation tuning, runtime dependencies, and fortress generation content.
- Changed `FortressSessionInitializer` to consume generation content through `FortressRuntimeAccess` instead of recapturing content from the global registry while generating the fortress map.
- Added zone definitions to `FortressRuntimeContentSnapshot`.
- Moved structured core-data application behind `FortressRuntimeContentSnapshotLoader.ApplyCoreData(...)`, so App no longer calls `ContentRegistry.Instance.ApplyCoreData(...)`.
- Changed `SimulationWorldContentLoader.LoadCoreContent(...)` to return the active runtime content snapshot and register zones from `snapshot.ZoneDefinitions` instead of reading `ContentRegistry.Instance.Zones.Values`.
- Simplified `ItemManager.SetDependencies(...)` to require only `World`, removing unused content-registry plumbing from App/test item-manager initialization.
- Changed `RuntimeContentRegistryLoader` to load only the structured runtime registry; the legacy `HumanFortress.Core.Content.ContentRegistry` is no longer part of normal bootstrap.
- Removed App startup and content smoke-test diagnostics wiring for the legacy registry.
- Updated content bootstrap smoke coverage to assert structured registry loading and runtime snapshot core-data application instead of legacy registry counts.
- Deleted the old `HumanFortress.Core.Content.ContentRegistry` source after splitting the still-used runtime geology JSON DTOs into `GeologyData.cs`; the structured registry is now the only runtime registry source model.
- Moved runtime geology and zone JSON DTOs to `HumanFortress.Contracts` while preserving their old `HumanFortress.Core.Content` namespace.
- Changed structured registry geology and zone loading to use explicit `System.Text.Json` mappings instead of Newtonsoft DTO attributes/`JToken.ToObject`.
- Added smoke coverage for `zones.json` snake_case field mapping (`display_name`, `ui_hints`, and `default_policies`).
- Moved construction and recipe definitions, read-only catalog interfaces, and immutable catalog stores to `HumanFortress.Contracts`; they now use `HumanFortress.Contracts.Content.Registry` after the namespace cleanup pass.
- Moved `CoreDataLoadResult`, `ConstructionContentLoadResult`, and `RecipeContentLoadResult` to `HumanFortress.Contracts`; they now use the content contract namespace after the namespace cleanup pass.
- Deleted the unused `ConstructionRegistry` and `RecipeRegistry` singleton compatibility classes after source scans found no external `*.Instance` consumers.
- Moved `CoreDataRegistryLoader` from Core to `HumanFortress.Content.Definitions`, leaving Core with `ContentRegistry.ApplyCoreData(...)` but no construction/recipe JSON parsing ownership.
- Removed the unused `ContentRegistry.LoadCoreData(...)` compatibility method; App/tests now enter core-data loading through `CoreContentCatalogLoader`.
- Moved `IRuntimeGeologyCatalog`, `ConstructionTuning`, and `PlaceableTuning` to `HumanFortress.Contracts` while preserving their old namespace.
- Changed construction/placeable tuning JSON parsing to use `System.Text.Json`, so these cross-module contract types no longer require Newtonsoft from Core.
- Added smoke coverage for `ConstructionTuning.LoadFromJson(...)`, matching the existing navigation/placeable tuning parser coverage.
- Moved `ContentVersion`, `ContentValidationResult`, and `ContentSnapshot` to `HumanFortress.Contracts` while preserving their old namespace.
- Moved `FixedPoint`, material definitions, terrain kind definitions, geology definitions, and biome template definitions to `HumanFortress.Contracts` while preserving their old namespaces.
- Moved terrain bit-layout DTOs and alias/migration DTOs to `HumanFortress.Contracts` while preserving their old namespace.
- Deleted unused `MaterialIdRegistry`, an obsolete hard-coded material/terrain display table superseded by content-driven geology/rendering.
- Deleted unused `MaterialSelectionService`; it was a Core-owned global material preference cache with no write call sites.
- Moved the structured runtime registry implementation (`ContentRegistry`, concrete material/terrain/geology/biome registries, alias resolver, material parser, and registry diagnostics bridge) from Core into `HumanFortress.Content/Registry` while preserving the historical namespace.
- Removed Core's `Newtonsoft.Json` package reference; Content now explicitly owns the remaining `JObject` tuning-store dependency.

### Verification

- Content fast build: passed with `0 Warning(s), 0 Error(s)`
- App fast build: passed with `0 Warning(s), 0 Error(s)`
- Test project fast build: passed with `0 Warning(s), 0 Error(s)`
- Full regression test entry: passed
- App `--init-only`: passed; startup loaded 83 materials, 17 geology entries, and 19 zone definitions
- `HumanFortress.sln` fast build: passed with `0 Warning(s), 0 Error(s)`
- App analyzer build: passed with the existing 41 historical analyzer warnings and `0 Error(s)`
- `git diff --check`: passed
- Latest 2026-06-13 sub-batch:
  - Core/Jobs/App/test fast builds passed with `0 Warning(s), 0 Error(s)`
  - Full regression test entry passed, including the new navigation/placeable tuning JSON smoke checks
- Latest 2026-06-14 sub-batch:
  - App fast build passed with `0 Warning(s), 0 Error(s)`
  - Test project fast build passed with `0 Warning(s), 0 Error(s)` after rerunning sequentially; an attempted parallel App/test build reproduced the documented `obj/ref/*.dll` file-lock issue
  - Full regression test entry passed
- Latest 2026-06-14 WorldGen/content-injection sub-batch:
  - WorldGen fast build passed with `0 Warning(s), 0 Error(s)`
  - App fast build passed with `0 Warning(s), 0 Error(s)`
  - Test project fast build passed with `0 Warning(s), 0 Error(s)`
  - Full regression test entry passed, including fortress generation determinism and FillWorld coverage
  - `HumanFortress.sln` fast build passed with `0 Warning(s), 0 Error(s)`
  - App `--init-only` passed; startup loaded 79 materials, 17 geology entries, and 19 zone definitions
  - App analyzer build passed with the existing 41 historical analyzer warnings and `0 Error(s)`
- Latest 2026-06-18 no-compile runtime-composition sub-batch:
  - Moved `SimulationRuntimeSystems`, `FortressRuntimeSystemsFactory`, `FortressRuntimePlanningSystems`, and `FortressRuntimeJobSystems` from App source into `HumanFortress.Runtime`.
  - Added `FortressRuntimeLogging` so Runtime composition receives App logging callbacks without calling App `Logger` directly.
  - Moved `FortressRuntimeHostFactory` and `FortressRuntimeStartup` from App source into `HumanFortress.Runtime`.
  - Changed `GameStateManager` to pass explicit runtime logging and the App-owned auto-dig command delegate into Runtime.
  - Moved all App command source files into `HumanFortress.Runtime/Commands`, changed their namespace to `HumanFortress.Runtime.Commands`, and removed the UI enum dependency from `CreateAdvancedMiningOrderCommand`; App now maps UI mining actions before constructing the command.
  - First fast build caught two migrated debug spawn commands still calling App `Logger`; those calls were removed so Runtime commands no longer depend on App logging.
  - `HumanFortress.sln` fast build passed with `0 Warning(s), 0 Error(s)` after the fix.
  - Tests and headless `--init-only` were not run in this sub-batch.
  - `git diff --check` passed
- Latest 2026-06-14 SimulationWorldContentLoader content-boundary sub-batch:
  - Content fast build passed with `0 Warning(s), 0 Error(s)`
  - Simulation fast build passed with `0 Warning(s), 0 Error(s)`
  - App fast build passed with `0 Warning(s), 0 Error(s)`
  - Test project fast build passed with `0 Warning(s), 0 Error(s)`
  - Full regression test entry passed
  - `HumanFortress.sln` fast build passed with `0 Warning(s), 0 Error(s)`
  - App `--init-only` passed with exit code 0; structured content log reports 83 materials, 17 runtime geology entries, 19 zone definitions, and 0 validation errors
  - App analyzer build with `--no-incremental` passed with the existing 41 historical analyzer warnings and `0 Error(s)`
- Latest 2026-06-14 structured-only bootstrap sub-batch:
  - Content fast build passed with `0 Warning(s), 0 Error(s)`
  - App fast build passed with `0 Warning(s), 0 Error(s)`
  - Test project fast build passed with `0 Warning(s), 0 Error(s)`
  - Full regression test entry passed
  - `HumanFortress.sln` fast build passed with `0 Warning(s), 0 Error(s)`
  - App `--init-only` passed with exit code 0; structured content log reports 83 materials, 17 runtime geology entries, 19 zone definitions, and 0 validation errors
  - App analyzer build with `--no-incremental` passed with the existing 41 historical analyzer warnings and `0 Error(s)`
  - `git diff --check` passed
- Latest 2026-06-14 geology/zone DTO contracts sub-batch:
  - Contracts fast build passed with `0 Warning(s), 0 Error(s)`
  - Core fast build passed with `0 Warning(s), 0 Error(s)`
  - Content fast build passed with `0 Warning(s), 0 Error(s)`
  - Simulation fast build passed with `0 Warning(s), 0 Error(s)`
  - App fast build passed with `0 Warning(s), 0 Error(s)`
  - Test project fast build passed with `0 Warning(s), 0 Error(s)`
  - Full regression test entry passed, including the new zone snake_case mapping assertions
  - `HumanFortress.sln` fast build passed with `0 Warning(s), 0 Error(s)`
  - App `--init-only` passed with exit code 0; structured content log reports 83 materials, 17 runtime geology entries, 19 zone definitions, and 0 validation errors
  - App analyzer build with `--no-incremental` passed with the existing 41 historical analyzer warnings and `0 Error(s)`
  - `git diff --check` passed
- Latest 2026-06-15 construction/recipe contracts sub-batch:
  - Moved `ConstructionDefinition`, `MaterialCost`, `WorkshopIo`, `WorkshopAttachment`, `IConstructionCatalog`, and `ConstructionCatalogStore` into Contracts
  - Moved `RecipeDefinition`, `RecipeIngredient`, `RecipeOutput`, `IRecipeCatalog`, and `RecipeCatalogStore` into Contracts
  - Moved core-data load result DTOs into Contracts
  - Deleted unused `ConstructionRegistry` and `RecipeRegistry` singleton compatibility classes
  - Contracts fast build passed with `0 Warning(s), 0 Error(s)`
  - Core fast build passed with `0 Warning(s), 0 Error(s)`
  - Content fast build passed with `0 Warning(s), 0 Error(s)`
  - App fast build passed with `0 Warning(s), 0 Error(s)`
  - Test project fast build passed with `0 Warning(s), 0 Error(s)`
  - Full regression test entry passed
- Latest 2026-06-15 core-data loader ownership sub-batch:
  - Moved `CoreDataRegistryLoader` into `HumanFortress.Content.Definitions`
  - Removed unused `ContentRegistry.LoadCoreData(...)`; only `ApplyCoreData(...)` remains on the structured registry
  - Source scan found no remaining production/test `LoadCoreData(...)` calls
  - Core fast build passed with `0 Warning(s), 0 Error(s)`
  - Content fast build passed with `0 Warning(s), 0 Error(s)`
  - App fast build passed with `0 Warning(s), 0 Error(s)`
  - Test project fast build passed with `0 Warning(s), 0 Error(s)`
  - Full regression test entry passed
  - `HumanFortress.sln` fast build passed with `0 Warning(s), 0 Error(s)`
  - App `--init-only` passed with exit code 0
  - App analyzer build passed with the existing 41 historical analyzer warnings and `0 Error(s)`
  - `git diff --check` passed
- Latest 2026-06-16 runtime content contracts sub-batch:
  - Moved `IRuntimeGeologyCatalog`, `ConstructionTuning`, and `PlaceableTuning` from Core to Contracts
  - Moved `ContentVersion`, `ContentValidationResult`, and `ContentSnapshot` from Core to Contracts
  - Moved `FixedPoint`, material, terrain kind, geology, and biome template definitions from Core to Contracts
  - Moved `TerrainBitLayout`, `BitFieldDefinition`, `AliasDefinition`, `ContentMigration`, and `MigrationRule` from Core to Contracts
  - Deleted unused `MaterialIdRegistry`
  - Deleted unused `MaterialSelectionService`
  - Replaced tuning parser Newtonsoft usage with `System.Text.Json`
  - Added `ConstructionTuning` JSON smoke coverage
  - Contracts/Core/Simulation/Jobs/App fast builds passed with `0 Warning(s), 0 Error(s)`
  - Test project fast build passed with `0 Warning(s), 0 Error(s)`
  - `HumanFortress.sln` fast build passed with `0 Warning(s), 0 Error(s)`
  - Test project run passed with `--no-build --no-restore`, including navigation/construction/placeable tuning JSON smoke checks and Phase A-D validation
- Latest 2026-06-16 structured registry implementation ownership sub-batch:
  - Moved structured registry implementation files from `src/HumanFortress.Core/Content/Registry` to `src/HumanFortress.Content/Registry`
  - Removed the Core `Newtonsoft.Json` package reference and added the explicit package reference to Content
  - Ran solution restore after the package move; this exposed a real transitive-dependency bug where WorldGen had been relying on Core's Newtonsoft package
  - Converted WorldGen mapgen/ore/cavern tuning parsing from `JObject`/`JArray` to `System.Text.Json.Nodes`, keeping Newtonsoft isolated to the Content registry compatibility layer
  - Source scan confirmed Core no longer contains structured registry implementation files or Newtonsoft usage
  - Source scan confirmed WorldGen no longer uses Newtonsoft
  - Core fast build passed with `0 Warning(s), 0 Error(s)`
  - Content fast build passed with `0 Warning(s), 0 Error(s)`
  - WorldGen fast build passed with `0 Warning(s), 0 Error(s)`
  - Solution restore passed with `--ignore-failed-sources`
  - `HumanFortress.sln` fast build passed with `0 Warning(s), 0 Error(s)`
  - Test project run passed with `--no-build --no-restore`, including the transport/construction/craft, mining/items/diff, core smoke, and Phase A-D suites
- Latest 2026-06-16 strict content diagnostics sub-batch:
  - Added `FortressContentLoadException`, `FortressContentLoadResult.ThrowIfInvalid(...)`, and `FortressContentLoader.LoadStrict(...)`
  - Added regression coverage for strict missing-content failures and warning promotion through `treatWarningsAsErrors`
  - Added App CLI flags `--strict-content` and `--content-warnings-as-errors`
  - Wired strict mode through `Program`, `GameStateManager`, and `SimulationWorldContentLoader` so startup registry loading and fortress session core catalog loading can fail fast
  - Documented the strict CI smoke command in `docs/operations/README-RUN.md`
  - Content fast build passed with `0 Warning(s), 0 Error(s)`
  - App fast build passed with `0 Warning(s), 0 Error(s)`
  - Strict `--init-only --strict-content --content-warnings-as-errors` run passed with exit code 0
  - `HumanFortress.sln` fast build passed with `0 Warning(s), 0 Error(s)`
  - Test project run passed with `--no-build --no-restore`
- Latest 2026-06-16 diagnostics debug-surface sub-batch:
  - Added `DiagnosticSnapshot` and `DiagnosticIssueSummary` over the existing in-memory diagnostic ring buffer
  - Changed `Logger.Close()` to preserve the final in-memory diagnostic snapshot after flushing the async dispatcher
  - Exposed diagnostics snapshots through `GameStateManager` and `FortressRuntimeAccess`
  - Extended the Debug Status tab to show event/warning/error counts and the latest Content issue when the debug menu is open
  - Kept snapshot construction gated behind `ui.DebugOpen` so normal frame rendering does not rebuild diagnostic summaries every frame
  - Extended async diagnostic smoke coverage for snapshot counts, category counts, and parsed Content issue codes
  - Test project fast build passed with `0 Warning(s), 0 Error(s)`
  - Test project run passed with `--no-build --no-restore`
- Latest 2026-06-16 runtime/jobs boundary cleanup sub-batch:
  - Moved initial worker seeding into `HumanFortress.Runtime.SimulationInitialWorkerSpawner`, with App now passing only the logger callback
  - Added `HumanFortress.Runtime.StartupDigTargetFinder` and routed both App auto-dig bootstrap paths through it, removing duplicated dig-target search logic from App runtime helpers
  - Added smoke coverage for startup dig-target lookup and one-time initial-worker seeding
  - Moved `SchedulerTunings`, `WorkshopTunings`, and `WorkerSelectionStrategy` source ownership into `HumanFortress.Jobs/Configuration`; a later namespace cleanup moved these to `HumanFortress.Jobs`
  - Changed scheduler/workshop tuning parse failures to use injected log callbacks instead of depending on App `Logger`
  - Moved `SanitizeSystem` source ownership into `HumanFortress.Jobs/Safety`, with App composition passing the logger callback
  - Moved profession contracts into `HumanFortress.Contracts`, moved `ProfessionAssignments` source ownership into `HumanFortress.Jobs/Profession`, and moved `ProfessionRegistry` file loading into `HumanFortress.Content/Definitions`
  - Added Jobs-owned `IUnified*JobExecutor` orchestration interfaces and moved `UnifiedJobsOrchestrator` source ownership into `HumanFortress.Jobs/Orchestration`
  - Runtime-owned job-system wrappers now implement the small orchestration interfaces instead of being concrete dependencies of the orchestrator
  - Moved concrete transport/mining/construction/craft diff emitters into `HumanFortress.Jobs/Diff` while preserving transitional namespace compatibility
  - Moved transport/mining/craft profession and recipe adapters into `HumanFortress.Jobs/Profession`
  - Moved callback-backed transport/mining/construction job loggers into `HumanFortress.Jobs/Logging`
  - Moved construction terrain-material resolution into `HumanFortress.Jobs/Construction`
  - Moved mining drop/tuning resolution into `HumanFortress.Jobs/Mining` and converted it from Newtonsoft `JObject`/`JArray` parsing to `System.Text.Json.Nodes`
  - Moved tick-facing transport/mining/construction/craft job-system wrappers into `HumanFortress.Runtime/Jobs`; a later namespace cleanup moved these to `HumanFortress.Runtime.Jobs`
  - Collapsed the construction workshop-completion sink into the Runtime-owned construction wrapper as a callback bridge; App now only binds the UI callback during bootstrap
  - Added a transitional `HumanFortress.Runtime` internals bridge to `HumanFortress.Jobs` so Runtime composition can consume Jobs-owned internal diff emitters/adapters while namespace cleanup remains pending
  - `HumanFortress.App/Jobs` no longer contains active source files; later namespace cleanup removed the old App-owned Jobs namespace from active source
  - Added smoke coverage for unified job orchestration order, mining-backlog hauling hints, and intake-stat propagation
  - Added smoke coverage for mining tuning JSON parsing, geology alias drop lookup, air-handle lookup, and wall/ramp tick resolution
  - Runtime fast build passed with `0 Warning(s), 0 Error(s)`
  - Jobs fast build passed with `0 Warning(s), 0 Error(s)`
  - App fast build passed with `0 Warning(s), 0 Error(s)`
  - Test project fast build passed with `0 Warning(s), 0 Error(s)`
  - Test project run passed with `--no-build --no-restore`
- Latest 2026-06-14 legacy-registry-source deletion sub-batch:
  - Deleted `src/HumanFortress.Core/Content/ContentRegistry.cs`
  - Added `src/HumanFortress.Core/Content/GeologyData.cs` for the runtime geology DTOs still consumed by the structured registry
  - Source scan found no remaining production/test references to `HumanFortress.Core.Content.ContentRegistry`, `MaterialData`, or the old registry diagnostics fields
  - Core fast build passed with `0 Warning(s), 0 Error(s)`
  - Content fast build passed with `0 Warning(s), 0 Error(s)`
  - Simulation fast build passed with `0 Warning(s), 0 Error(s)`
  - WorldGen fast build passed with `0 Warning(s), 0 Error(s)`
  - App fast build passed with `0 Warning(s), 0 Error(s)`
  - Test project fast build passed with `0 Warning(s), 0 Error(s)`
  - Full regression test entry passed
  - `HumanFortress.sln` fast build passed with `0 Warning(s), 0 Error(s)`
  - App `--init-only` passed with exit code 0; structured content log reports 83 materials, 17 runtime geology entries, 19 zone definitions, and 0 validation errors

### Important Notes

- This removes the App-owned host wrapper but does not yet move concrete gameplay system composition fully out of App.
- The next runtime boundary target is the new runtime system group layer: move concrete session/system group composition out of App when Runtime has clean UI/log/content callback seams.
- Runtime context and runtime systems should continue receiving the same catalog snapshot references from `FortressRuntimeDependencies`; do not reintroduce independent `ContentRegistry.Instance` reads in host construction.
- Runtime composition should consume `FortressRuntimeContentSnapshot` through `FortressRuntimeDependencies.Load(...)`; keep structured-registry reads behind the Content snapshot loader rather than host/system factories.
- Runtime command targets and App UI helpers should use the active session catalogs exposed by `FortressRuntimeAccess`; do not add fallback reads to `ContentRegistry.Instance` for construction/recipe UI convenience paths.
- Jobs/Runtime/Simulation execution paths should receive catalog/tuning/geology dependencies explicitly. `IRuntimeGeologyCatalog`, `ConstructionTuning`, and `PlaceableTuning` now compile from Contracts; App/Content may bridge to the transitional structured registry, but Jobs/Runtime/Simulation should not reach for it directly.
- Navigation and placeable tunings now follow the same rule as construction/scheduler/workshop tunings: Content captures JSON once, App composition parses it, and runtime systems receive explicit objects.
- Mining drop/tick tuning now follows the same snapshot rule. Do not add new mining resolver reads from `ContentRegistry.Instance`.
- WorldGen now follows the same active-session snapshot rule: fortress generation receives explicit `FortressGenerationContent` with geology plus mapgen/ore/cavern tuning. Do not add new `ContentRegistry.Instance` reads to `FortressGenerator`, `FortressMap`, or `FortressChunk`.
- App world-content loading should call `FortressRuntimeContentSnapshotLoader.ApplyCoreData(...)` and consume the returned snapshot; do not add direct `ContentRegistry.Instance.ApplyCoreData(...)` or `ContentRegistry.Instance.Zones` reads back into `SimulationWorldContentLoader`.
- The remaining production direct structured-registry reads are now concentrated in Content bootstrap/snapshot capture/application and the Content-owned structured registry internals themselves.
- Normal startup now loads the structured runtime registry only. Do not re-add legacy registry loading to `RuntimeContentRegistryLoader`.
- The legacy `HumanFortress.Core.Content.ContentRegistry` source has been deleted. Do not reintroduce a second runtime registry source model or the old construction/recipe singleton registries.
- `HumanFortress.Navigation` must not regain a Core/Content reference for tuning convenience. Pass `NavigationTuning` into `NavigationManager`, `PathService`, overlay/debug helpers, or runtime factories.
- `GameStateManager` still owns UI-facing state transitions, simulation controls, and the jobs debug cache. That is acceptable until a runtime snapshot/debug facade exists.

## Previous Batch: Content Load Boundary Consolidation

Status: completed

### Completed

- Add a `HumanFortress.Content`-owned load coordinator/result that loads the currently split content catalogs in one place:
  - item definition catalog
  - creature definition catalog
  - construction catalog
  - recipe catalog
- Keep `Simulation` independent from `Content`; runtime managers should still receive snapshots.
- Keep `ContentRegistry` as the transitional structured runtime registry for geology/tuning/zones while core-data snapshot loading is folded behind the Content assembly.
- Update App startup and tests to consume the unified Content load result instead of calling item/creature loaders and Core-data loading separately.
- Preserve current compatibility behavior and diagnostics.
- Added `CoreContentCatalogLoader` and `CoreContentCatalogLoadResult` in `HumanFortress.Content.Definitions`.
- Moved `CoreDataRegistryLoader` into `HumanFortress.Content.Definitions` so Content coordinates construction/recipe core-data loading without App parsing JSON.
- Added `ContentRegistry.ApplyCoreData(...)` so App/runtime composition can apply construction/recipe snapshots loaded through the unified Content result.
- Changed `SimulationWorldContentLoader` to call `CoreContentCatalogLoader.Load(...)` once, then apply item, creature, construction, and recipe snapshots from that result.
- Changed test support and content smoke tests to use `CoreContentCatalogLoader`.
- Removed the old `ContentRegistry.LoadCoreData(...)` compatibility API after App/tests moved to the Content-owned loader.
- Added `FortressContentLoader` in `HumanFortress.Content.Loading` as the Content-owned runtime bootstrap entry:
  - resolves published vs source-checkout `content/`
  - resolves published vs source-checkout `data/core`
  - loads the legacy/structured runtime registries when needed
  - optionally loads the unified core catalog snapshots
- Added `RuntimeContentRegistryLoader` in `HumanFortress.Content.Loading` and removed the old Core-owned `ContentLoadCoordinator`, so registry bootstrap orchestration now belongs to Content.
- Added `FortressContentIssue` diagnostics with severity/code/message, plus `FortressContentLoadResult.IsValid(...)`, `GetBlockingIssues(...)`, and `FormatBlockingIssues(...)`.
- Added App-side content issue logging through `FortressContentIssueLogger`.
- Added `ResolveRegistryFile(...)` so App-side UI/input/profession convenience registries no longer hard-code `baseDir/content/registries`.
- Removed unused old-registry parameters from the incomplete stockpile hauling/filter TODO path; future stockpile filtering should depend on item definition catalog seams, not the legacy content registry.
- Changed `Program` and `SimulationWorldContentLoader` to enter content loading through `FortressContentLoader` instead of owning their own path discovery and coordinator calls.
- Changed scheduler/workshop tunings to load from the already-loaded structured registry during runtime composition instead of reading tuning JSON directly from App.
- Switched input bindings, orders display registry, profession registry, workshop category mapping, and legacy tuning compatibility loaders to use the Content-owned registry-file resolver.
- Confirmed App now has no direct `Path.Combine(baseDir, "content", "registries", ...)` call sites; that knowledge is centralized in `HumanFortress.Content`.

### Verification

- Content build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- App build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Test project build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Test project run: passed, including content-load smoke, definition reload, transport/construction/craft, mining/items/diff, core runtime smoke checks, and Phase A-D validation
- Solution build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- `--init-only`: passed and still reports `79 materials`
- App analyzer build: passed with existing 41 analyzer warnings and `0 Error(s)`
- `git diff --check`: passed
- Path scan: no App/test call sites directly combine `baseDir/content/registries`; runtime registry bootstrap enters through `FortressContentLoader` / `RuntimeContentRegistryLoader`.

### Audit Context

- `docs/archive/plans/HUMANFORTRESS_MAIN_BRANCH_ARCHITECTURE_AUDIT_FOR_CODEX.md` was read on 2026-06-12.
- Its `HumanFortress.Content` build concern is no longer a current build blocker; the solution builds successfully.
- Its larger Content concern is now substantially reduced: item, creature, construction, recipe, runtime registry bootstrap, structured registry implementation, and App registry-file path resolution now enter through `HumanFortress.Content`.
- Remaining Content work is strict content-mode diagnostics, richer debug surfaces, cleanup of the few remaining non-registry content DTO compatibility namespaces, and future compiled-pack support.
- The agreed next priority after the remaining Content hygiene is moving concrete runtime composition out of App.

### Important Notes

- At that point, the legacy `HumanFortress.Core.Content.ContentRegistry` source still existed, but `RuntimeContentRegistryLoader` loaded only the structured runtime registry. A later sub-batch deleted the old source after splitting `GeologyData` into its own file.
- The structured `HumanFortress.Content.Registry.ContentRegistry` remains the runtime registry for geology handles, tuning, zones, construction catalogs, and recipe catalogs; its implementation now compiles from `HumanFortress.Content.Registry`.
- `FortressContentLoader` is a Content-owned facade over the structured registry and catalog snapshot loaders; do not add another App-side bootstrapper.
- Core no longer owns a legacy/structured registry coordinator, construction/recipe singleton registries, the construction/recipe core-data JSON loader, or the structured registry implementation. The remaining cleanup is policy/diagnostics and compatibility naming, not a second runtime registry source model.
- Remaining direct references to `HumanFortress.Core.Content.ContentRegistry` are now historical documentation/source-compatibility references, not normal bootstrap requirements.
- Do not run overlapping .NET project builds in parallel. A parallel Content/App/test build reproduced file-lock failures on `HumanFortress.Content/obj/Debug/net8.0/*.dll`.

## Previous Batch: Construction and Recipe Catalog Snapshots

Status: completed

### Completed

- Added immutable construction and recipe catalog snapshots:
  - `ConstructionCatalogStore`
  - `RecipeCatalogStore`
- Changed the core-data loading path so it parses construction/workshop and recipe JSON into fresh catalog snapshots instead of mutating `ConstructionRegistry.Instance` and `RecipeRegistry.Instance`.
- Changed `ContentRegistry` to own current construction/recipe snapshots as instance fields exposed through `IConstructionCatalog` and `IRecipeCatalog`.
- `ContentRegistry.ApplyCoreData(...)` now swaps the current construction/recipe snapshots from the Content-owned load result, matching the item/creature snapshot pattern.
- `ContentRegistry.LoadContent(...)` / `LoadContentAsync(...)` now resets construction/recipe snapshots to empty as part of runtime content clearing.
- Changed App craft composition so `CraftRecipeCatalogAdapter` receives an explicit `IRecipeCatalog` instead of reading `ContentRegistry.Instance.Recipes` internally.
- Added regression coverage proving repeated core-data loads keep construction/recipe counts and workshop/category queries stable instead of accumulating duplicate indexes.

### Verification

- Core build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- App build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Test project build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Test project run: passed, including content-load idempotence, transport/construction/craft, mining/items/diff, core runtime smoke checks, and Phase A-D validation
- Solution build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- `--init-only`: passed and still reports `79 materials`
- App analyzer build: passed with existing 41 analyzer warnings and `0 Error(s)`
- `git diff --check`: passed

### Important Notes

- `ConstructionRegistry` and `RecipeRegistry` classes still exist as compatibility types, but `ContentRegistry` no longer uses their singleton instances for normal startup loading.
- Construction/recipe definitions still live in Core for now. The next content boundary pass can move their DTOs/catalog stores into Contracts or Content once namespace churn is acceptable.
- Some UI/App convenience code still reads `ContentRegistry.Instance.Constructions` / `Recipes` directly. That is acceptable because those properties now expose snapshots through read-only interfaces.
- The remaining content unification work is folding item/creature catalog loading and construction/recipe core-data loading into one coherent `HumanFortress.Content` load result, then deleting legacy registry compatibility.

## Previous Batch: Content-Owned Item and Creature Definition Loading

Status: completed

### Completed

- Added `HumanFortress.Content` as a real project and included it in `HumanFortress.sln`.
- Moved static item and creature JSON loading/parsing/validation into `HumanFortress.Content.Definitions`:
  - `ItemDefinitionCatalogLoader`
  - `CreatureDefinitionCatalogLoader`
- The Content loaders now produce immutable catalog snapshots:
  - `ItemDefinitionCatalogStore`
  - `CreatureDefinitionCatalogStore`
- Removed the old Simulation-owned definition loader files:
  - `Simulation/Items/ItemDefinitionLoader.cs`
  - `Simulation/Creatures/CreatureDefinitionLoader.cs`
- Removed `ItemManager.LoadDefinitions(...)` and `CreatureManager.LoadDefinitions(...)`; managers now consume prebuilt snapshots through `SetDefinitionCatalog(...)`.
- Kept Simulation independent from the Content project. The dependency direction is now `App/tests -> Content -> Contracts`, while Simulation only consumes contract/store types.
- Changed App startup composition so `SimulationWorldContentLoader` loads item/creature catalogs through `HumanFortress.Content`, logs loader diagnostics, and injects the snapshots into the active world managers.
- Added test-side `DefinitionCatalogTestSupport` so regression tests explicitly load catalog snapshots and inject them without relying on manager file IO.

### Verification

- Content build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Simulation build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- App build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Test project build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Test project run: passed, including transport/construction/craft, mining/items/diff, core runtime smoke checks, and Phase A-D validation
- Solution build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- `--init-only`: passed and still reports `79 materials`
- App analyzer build: passed with existing 41 analyzer warnings and `0 Error(s)`
- `git diff --check`: passed

### Important Notes

- This batch deliberately avoided `Simulation -> Content`; file parsing now belongs to Content, runtime managers consume snapshots.
- The item/creature DTO namespaces are still transitional, but assembly ownership is now Contracts and loader ownership is now Content.
- Content loaders still preserve current compatibility behavior: root-array item files, `{ "items": [...] }` envelopes, furniture/placeable profile parsing, generic resource-name enrichment, and current validation messages.
- The following batch applied the same snapshot pattern to construction and recipe data. The next content unification step is folding all catalog loading into one coherent Content load result.

## Previous Batch: Static Definition Contracts Migration

Status: completed

### Completed

- Moved shared static definition DTOs into `HumanFortress.Contracts`:
  - `ItemDefinition`
  - `CreatureDefinition`
  - item feature blocks such as `StackBlock`, `EquipBlock`, `WeaponBlock`, `ContainerBlock`, and `UseBlock`
  - shared placeable DTOs `PlaceableProfile`, `Footprint`, `PassabilityMode`, and `EffectsBlock`
- Moved read-only item/creature definition catalog interfaces into `HumanFortress.Contracts`:
  - `IItemDefinitionCatalog`
  - `ICreatureDefinitionCatalog`
- Moved immutable definition catalog snapshot stores into `HumanFortress.Contracts`:
  - `ItemDefinitionCatalogStore`
  - `CreatureDefinitionCatalogStore`
- Added explicit project references:
  - `HumanFortress.Core -> HumanFortress.Contracts`
  - `HumanFortress.Simulation -> HumanFortress.Contracts`
- Preserved the existing namespaces as a transitional compatibility step, matching the earlier Navigation contracts migration pattern. This avoids a broad namespace rewrite while still moving assembly ownership to Contracts.
- Left `ItemDefinitionLoader` and `CreatureDefinitionLoader` in Simulation for that batch only; they were moved into `HumanFortress.Content` in the following batch.
- Left the local internal `StockpileFilter.ItemDefinition` placeholder untouched because it is a separate stockpile filtering stub, not the runtime item definition contract.

### Verification

- Contracts build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Core build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Simulation build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- App build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Test project build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Test DLL run: passed
- Solution build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- `--init-only`: passed
- App analyzer build: passed with existing 41 analyzer warnings and `0 Error(s)`

### Important Notes

- This is an assembly-boundary migration, not a namespace cleanup. The types now compile from Contracts but still use the old namespaces for compatibility.
- The next content step after this batch moved item/creature definition loading out of Simulation and behind the Content assembly.
- A later cleanup pass should rename these contracts into a true content/contracts namespace after runtime ownership is stable.

## Previous Batch: Immutable Item and Creature Definition Catalog Stores

Status: completed

### Completed

- Added internal immutable snapshot stores:
  - `ItemDefinitionCatalogStore`
  - `CreatureDefinitionCatalogStore`
- Changed `ItemManager` to replace its static definition catalog snapshot on `LoadDefinitions` instead of owning mutable definition/kind/tag dictionaries directly.
- Changed `CreatureManager` to replace its static definition catalog snapshot on `LoadDefinitions` instead of owning mutable definition/tag dictionaries directly.
- Kept `ItemManager` and `CreatureManager` implementing the existing read-only catalog interfaces, so external callers do not need a broad migration in this pass.
- Preserved duplicate-definition compatibility: loader `LoadedCount` still reflects loaded valid entries, while final `DefinitionCount` reflects the last-definition-wins catalog snapshot.
- Stabilized the legacy Phase D concurrent pathfinder test by giving that test a wider pathing time budget. The old test accidentally treated normal 3ms tick-budget deferral as pathfinding failure under slower thread scheduling.

### Verification

- Simulation build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- App build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Test project build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Test DLL run: passed, including definition reload and Phase A-D validation
- Solution build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- `--init-only`: passed
- App analyzer build: passed with existing 41 analyzer warnings and `0 Error(s)`

### Important Notes

- Static item/creature data is now a snapshot inside the managers, but the content system still does not own those snapshots.
- `ItemDefinitionCatalogStore` and `CreatureDefinitionCatalogStore` are internal Simulation types because `ItemDefinition` and `CreatureDefinition` still live in Simulation.
- The next content boundary step is making these snapshots produced by the structured content registry/load coordinator, then passing them into the runtime managers.

## Previous Batch: Item and Creature Definition Loader Extraction

Status: completed

### Completed

- Extracted static item JSON loading/parsing/validation/normalization from `ItemManager` into `ItemDefinitionLoader`.
- Extracted static creature JSON loading/validation from `CreatureManager` into `CreatureDefinitionLoader`.
- Kept the existing public manager API compatible, so runtime callers can still use `LoadDefinitions`, `GetDefinition`, kind/tag queries, and runtime item/creature instance APIs without a broad migration in the same pass.
- Preserved current compatibility behavior for item content:
  - root-array item files still load;
  - `{ "items": [...] }` item envelopes still load;
  - legacy furniture/placeable profile parsing still works;
  - generic material names such as boulder/block/plank/log are still enriched from material ids;
  - construction/material validation still receives structured content registry context.
- Changed manager reload behavior to clear and rebuild definition/tag/kind indexes from the loaded definition set, instead of letting repeated loads accumulate duplicate index entries.
- Added regression coverage proving repeated item and creature definition loads keep definition counts and kind/tag query counts stable.

### Verification

- Simulation build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- App build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Test project build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Test DLL run: passed, including `Definition catalog reload indexes`
- Solution build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- `--init-only`: passed
- App analyzer build: passed with existing analyzer warnings and `0 Error(s)`

### Important Notes

- This is a loader extraction, not the final content-ownership move.
- `ItemDefinition` and `CreatureDefinition` still live in `HumanFortress.Simulation`, so the loaders currently stay in Simulation too.
- Startup still invokes item/creature definition loading through `world.Items.LoadDefinitions(...)` and `world.Creatures.LoadDefinitions(...)` for compatibility.
- The next content step should introduce immutable item/creature definition catalog snapshots, then let managers consume those snapshots instead of owning the static definition dictionaries directly.

## Previous Batch: Item and Creature Definition Catalog Seams

Status: completed

### Completed

- Added Simulation-level read-only definition catalog interfaces:
  - `IItemDefinitionCatalog`
  - `ICreatureDefinitionCatalog`
- Made `ItemManager` implement `IItemDefinitionCatalog`, preserving its existing runtime instance ownership.
- Made `CreatureManager` implement `ICreatureDefinitionCatalog`, preserving its existing runtime instance ownership.
- Migrated construction material matching in `HumanFortress.Jobs.Construction.ConstructionMaterialTracker` to use `IItemDefinitionCatalog` for item definition lookup.
- Migrated `ConstructionMaterialsPlanner` to receive `IItemDefinitionCatalog` explicitly instead of reading definitions through the full item manager API.
- Migrated App runtime composition to pass `world.Items` as the item definition catalog for construction material planning.
- Migrated `ProfessionAssignments` roster naming to use an injected `ICreatureDefinitionCatalog`, with App runtime composition passing `world.Creatures`.
- Confirmed the remaining direct item/creature definition reads are primarily UI/render/debug presentation code and can be handled separately.

### Verification

- Simulation build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- App build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Test project build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Test DLL run: passed
- Solution build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- `--init-only`: passed
- App analyzer build: passed with existing analyzer warnings and `0 Error(s)`
- `git diff --check`: passed

### Important Notes

- Item and creature JSON loading still lives in `ItemManager.LoadDefinitions` and `CreatureManager.LoadDefinitions`. This batch only separates the read-only definition surface from the full runtime managers.
- Moving item/creature definitions into a structured content-owned immutable catalog is a larger step because the definition types currently live in `HumanFortress.Simulation`.
- Future work should split static definition loading/validation from runtime instance management, then move the definition types/catalogs toward the content boundary.
- Avoid mixing App/test build commands in parallel. During this batch, parallel App/test builds reproduced the known macOS `apphost` signing/copy race; sequential rebuilds passed immediately.

## Previous Batch: Content Catalog Boundary Hardening

Status: completed

### Completed

- Added read-only catalog interfaces in Core:
  - `IConstructionCatalog`
  - `IRecipeCatalog`
- Changed `HumanFortress.Content.Registry.ContentRegistry` to expose construction and recipe content through read-only catalog interfaces instead of concrete mutable registry types.
- Kept `ConstructionRegistry` and `RecipeRegistry` as internal compatibility stores for that batch. A later batch replaced the normal `ContentRegistry.LoadCoreData` path with immutable construction/recipe snapshots.
- Migrated runtime/gameplay read paths from direct singleton access to catalog access:
  - Runtime workshop queue commands
  - buildable construction planning
  - construction completion application
  - craft workshop lookup/planning/execution
  - craft recipe adapter
  - App workshop UI, map click, overlay, and workshop category helpers
  - content smoke assertions
- Injected `IConstructionCatalog` into Jobs-owned construction/craft executors and locators, so Jobs no longer directly grabs construction definitions from a global construction singleton.
- At that point, App runtime composition resolved `ContentRegistry.Instance.Constructions` once and passed the catalog to buildable construction, construction jobs, craft planner, and craft jobs. A later batch moved this capture behind `FortressRuntimeContentSnapshot`.
- Replaced remaining recipe-registry test fixture writes with small in-memory test catalogs. At that time the old singleton compatibility classes were still isolated behind the structured registry; later batches deleted those classes entirely.

### Verification

- Core build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- App build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Test project build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Test DLL run: passed
- Solution build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- `--init-only`: passed
- App analyzer build: passed with existing analyzer warnings and `0 Error(s)`
- `git diff --check`: passed

### Important Notes

- This is a boundary hardening pass, not the final deletion of the concrete registry classes.
- The concrete construction/recipe registry singletons are still present, but direct references are now contained inside `ContentRegistry`.
- `SimulationRuntimeContext` later removed its fallback to `ContentRegistry.Instance`; runtime contexts now require explicit recipe/construction catalogs.
- Later batches moved runtime snapshot capture/application behind `HumanFortress.Content`; remaining production content-global reads are now concentrated in Content bootstrap/snapshot internals.

## Previous Batch: Core Data Registry Loading Unification

Status: completed

### Completed

- Moved construction/workshop and recipe JSON loading behind `HumanFortress.Content.Registry.ContentRegistry.LoadCoreData`.
- Added a Core-owned `CoreDataRegistryLoader` that parses:
  - `data/core/workshops/core_workshop_*.json`
  - legacy `data/core/placeable/workshops.json`
  - `data/core/recipes/*.json`
- Preserved the existing compatibility behavior while moving ownership out of App:
  - workshop definitions from new and legacy files are both loaded
  - duplicate construction ids are skipped and counted instead of failing the load
  - recipe root arrays and `{ "recipes": [...] }` documents are both supported
  - recipe aliases such as `workshops`, `workshop_id`, `workshop`, `work_time.duration_ticks`, `duration_ticks`, `skill.primary`, and `primary_skill` still parse
- Exposed `ContentRegistry.Constructions` and `ContentRegistry.Recipes` as transitional sub-registry accessors over the existing singleton registries.
- Simplified `SimulationWorldContentLoader`: App still locates the active `data/core` path and, at that time, still loaded creature/item managers directly, but no longer contained construction or recipe JSON parsing logic.
- Expanded content smoke coverage to prove `ContentRegistry.LoadCoreData` loads constructions and recipes without errors and populates known construction/recipe ids.

### Verification

- Core build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- App build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- App build with analyzers: passed with existing analyzer warnings and `0 Error(s)`
- Test project build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Test DLL run: passed, including the new construction/recipe content smoke assertions
- `--init-only`: passed
- `git diff --check`: passed before doc updates

### Important Notes

- This removes App-level file parsing for construction and recipe content, but it does not yet delete `ConstructionRegistry` or `RecipeRegistry`.
- `ConstructionRegistry` and `RecipeRegistry` are now better treated as compatibility sub-registries under the structured registry boundary. The next content pass should either fold their storage into `ContentRegistry` or hide them behind read-only catalog interfaces.
- At that point, creature and item definition loading still lived in their managers. This was completed later by moving their loaders into `HumanFortress.Content.Definitions` and injecting catalog snapshots.
- At that point, the legacy `HumanFortress.Core.Content.ContentRegistry` still existed for compatibility while material/geology migration was completed. Later batches deleted that legacy registry source.

## Previous Batch: Runtime Content Registry Unification

Status: completed

### Completed

- Promoted `HumanFortress.Content.Registry.ContentRegistry` toward the single authoritative content registry by adding runtime content capabilities that previously only existed in the legacy registry:
  - runtime `geology.json` loading
  - deterministic geology handle assignment
  - `GetGeologyHandle`
  - `GetGeologyByHandle`
  - `TryGetGeologyHandleByMaterialAndKind`
  - `tuning.*.json` loading and `GetTuning<T>`
  - `zones.json` loading and `GetZoneDefinition`
- Kept `content/registries/geology.json` as the current canonical runtime terrain prototype source because the active game still depends on `core_terrain_*` and `core_mat_*` ids.
- Added material alias indexing for geology lookups, so runtime calls such as `air + OpenNoFloor` can resolve to `core_terrain_air`.
- Migrated low-risk tuning and zone call sites to the structured registry:
  - `NavigationTuning`
  - `ConstructionTuning`
  - `PlaceableTuning`
  - active session zone registration
- Migrated core runtime geology readers to the structured registry:
  - `FortressGenerator`
  - `FortressMap`
  - `SimulationDiffApplicator`
  - `ConstructionSystem`
  - `MiningDropResolver`
  - `MiningResultApplier`
  - fortress map/tile renderers
- Expanded content smoke coverage to prove the structured registry can resolve runtime geology handles, tuning, and zones.

### Verification

- Core build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- App build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- App build with analyzers: passed with existing analyzer warnings and `0 Error(s)`
- Test project build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Test DLL run: passed, including FortressMap-to-World conversion through the structured registry
- `--init-only`: passed
- `git diff --check`: passed

### Important Notes

- At that point, the legacy `HumanFortress.Core.Content.ContentRegistry` still existed and was loaded by the old `ContentLoadCoordinator` for compatibility. Later batches deleted that legacy source and retired the coordinator path.
- At that point, the console summary from the legacy registry still appeared during startup. Later batches moved normal startup to the structured registry behind `FortressContentLoader`.
- At that point, `ConstructionRegistry`, `RecipeRegistry`, item definitions, and creature definitions still had separate loading paths. Later batches moved item/creature loading into `HumanFortress.Content.Definitions` and changed construction/recipe loading to produce immutable snapshots.
- `geology_prototypes.json` remains present but should not override runtime `geology.json` until its ids and material references are aligned with the active `core_terrain_*` content model.

## Previous Batch: Content Registry Bootstrap Unification

Status: completed

### Completed

- Added the first shared loading entry point while the legacy and structured content registries still coexist. This was later superseded by `HumanFortress.Content.Loading.RuntimeContentRegistryLoader`.
- App startup now loads both:
  - legacy `HumanFortress.Core.Content.ContentRegistry`
  - structured `HumanFortress.Content.Registry.ContentRegistry`
- `SimulationWorldContentLoader` now has a headless/session safety check that loads the registries if a caller bypasses `Program`.
- Fixed structured registry reload hygiene by resetting `ValidationResult`, `ContentHash`, and `IsLoaded` before each load.
- Fixed structured material loading for top-level array files such as `materials.authoring.json`.
- Fixed content diagnostic severity so validation summaries with warnings are not misclassified as errors.
- Fixed material category inference so materials tagged `ore` satisfy terrain-kind `ore` category validation.
- Added smoke coverage proving the coordinator loads both registry models and resolves structured material/terrain lookups.

### Verification

- App build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- App build with analyzers: passed with existing warnings and `0 Error(s)`
- `--init-only`: passed; content summary prints once and structured registry reports `0 warnings, 0 errors`
- Test project build: passed with `0 Warning(s), 0 Error(s)`
- Test DLL run: passed, including the new content load coordinator smoke test
- `HumanFortress.sln` build: passed with `0 Warning(s), 0 Error(s)`
- `git diff --check`: passed

### Important Notes

- This is bootstrap unification, not final content architecture. The two registry classes still exist.
- The structured registry treats the missing `content/templates/biomes` directory as optional because current WorldGen still uses enum/tuning based biomes, not biome templates.
- Recipes, construction definitions, item definitions, and creature definitions still have separate loading paths.
- The next content step should decide whether geology handles and tuning move into the structured registry or whether a new read-only content catalog facade should sit above both.

## Previous Batch: Structured Diagnostics First Pass

Status: completed

### Completed

- Added `HumanFortress.Core.Diagnostics` primitives:
  - `DiagnosticLevel`
  - `DiagnosticEvent`
  - `IDiagnosticSink`
  - `NullDiagnosticSink`
  - `DiagnosticSinkExtensions`
  - transitional `DiagnosticHub`
- Reworked App `Logger` into an async diagnostics facade while keeping the existing `Logger.Log(string)` compatibility API.
- Added an async background dispatcher so simulation/UI/runtime threads enqueue diagnostic events without writing files directly.
- Added a main timeline log plus category-routed logs:
  - `fortress_debug.log`
  - `logs/app.log`
  - `logs/content.log`
  - `logs/core.log`
  - `logs/jobs.log`
  - `logs/navigation.log`
  - `logs/runtime.log`
  - `logs/simulation.log`
  - `logs/ui.log`
- Added an in-memory ring-buffer sink for a later UI/debug diagnostics panel.
- Added a Simulation-local `SimulationDiagnostics` helper so Simulation systems can use `Core.Diagnostics` without depending on App.
- Routed startup `ContentRegistry` diagnostics through `IDiagnosticSink` while preserving its console summary for command-line visibility.
- Routed the secondary `HumanFortress.Content.Registry.ContentRegistry` and its material/terrain/geology/biome/alias helper registries through a shared content diagnostics helper with console fallback when App logging is not initialized.
- Bridged existing static lower-level callbacks into categorized diagnostics:
  - `NavigationManager`
  - `CreatureManager`
  - `ItemManager`
  - `SimulationDiffApplicator`
  - `OrdersManager`
  - `MiningSystem`
  - `ConstructionMaterialsPlanner`
- Replaced direct App UI/runtime initialization `Console.WriteLine` calls in the fortress setup/render-snapshot path with `Logger` calls.
- Routed Core `CommandQueue`, `TickScheduler`, and `EventBus` error paths through `DiagnosticHub`, so they can land in `core.log` once App initializes logging.
- Routed WorldGen fortress fill/conversion progress and generator-stage errors through `DiagnosticHub`, with console fallback only when App logging is not initialized.
- Routed Simulation diff, item diff, creature diff, stockpile diff, mining, hauling, orders, and construction-material planner diagnostics through the same diagnostics bridge.
- Converted `OrdersManager.LogCallback` and `MiningSystem.LogCallback` from visible static fields to properties while preserving current App wiring.
- Added smoke coverage proving async diagnostics flush and route to category files.

### Verification

- App build: passed with `0 Warning(s), 0 Error(s)`
- `./RunTests.sh`: passed with `0 Warning(s), 0 Error(s)`
- `--init-only`: passed and generated `fortress_debug.log` plus category logs under `logs/`
- `HumanFortress.sln` build: passed with `0 Warning(s), 0 Error(s)`
- `git diff --check`: passed

### Important Notes

- This is a first-pass logging architecture, not a total cleanup of every direct console write in the repository.
- `Contracts` should remain log-free. It defines DTOs/interfaces and should not emit diagnostics.
- `Core.Diagnostics.DiagnosticHub` is transitional. Prefer constructor-injected `IDiagnosticSink` for new long-lived systems once composition boundaries are stable.
- Existing test runs still print manager diagnostics when no App logger is initialized; that fallback is intentional for headless/local test visibility.
- Remaining direct console output is mostly command-line compatibility messages, diagnostic fallbacks when no App logger is initialized, startup content summary, and test output.

## Previous Batch: Runtime Assembly Expansion

Status: completed

### Completed

- Added the first real `HumanFortress.Runtime` assembly and wired it into `HumanFortress.sln`.
- Added a Runtime-owned `IRuntimeCommandContext` seam over `ISimulationContext` for command-stage tick ownership.
- Moved `SimulationCommandStage` out of App and into `HumanFortress.Runtime`.
- Moved `SimulationStatus` out of App and into `HumanFortress.Runtime` as a public runtime clock/control snapshot DTO.
- Moved `SimulationTickPipeline` out of App and into `HumanFortress.Runtime`, so pre-tick command execution, post-tick diff application, creature diff application, item diff application, and dirty-chunk navigation rebuilds now live in the runtime assembly.
- Moved the Simulation-to-Navigation adapter into Runtime:
  - `SimulationNavigationSource`
  - `SimulationNavigationFactory`
- Added Runtime-owned `IRuntimeTickSystems` and `SimulationRuntimeHostCore`, moving scheduler restart, system registration, tick-pipeline attachment, and stop-time pipeline detachment out of App.
- Moved the immutable session handle into Runtime as `SimulationRuntimeSession<THost>`, keeping the App-specific host wrapper type outside Runtime.
- Moved the new-session factory into Runtime as `SimulationRuntimeSessionFactory<THost>`. App now supplies content-loading and host-wrapper callbacks instead of owning the world/navigation/session reset logic directly.
- Moved all command target interfaces into Runtime:
  - profession weight assignment
  - item spawning
  - creature spawning
  - order enqueueing
  - zone mutation
  - workshop queue mutation
  - stockpile creation
- Moved `SimulationRuntimeContext` into Runtime. It now owns the command target aggregation and delegates profession weight writes through an injected callback instead of depending on App `ProfessionAssignments`.
- Moved concrete command target helpers into Runtime:
  - `ItemSpawnCommandTarget`
  - `CreatureSpawnCommandTarget`
  - `OrderCommandTarget`
  - `ZoneCommandTarget`
  - `WorkshopQueueCommandTarget`
  - `StockpileCommandTarget`
- Removed `HumanFortress.App.Commands` dependency on `HumanFortress.App.Runtime`; player/debug commands now depend on Runtime command target seams.
- Updated Runtime job wrappers, session creation, runtime host wiring, fortress initialization, and smoke tests to consume the Runtime-owned pipeline/navigation/session factory.
- Updated App to reference Runtime while keeping UI, SadConsole hosting, concrete job adapters, content loading implementation, and concrete host-wrapper composition callbacks in App for now.

### Verification

- App build: passed after adding the missing Runtime -> Contracts and Runtime -> SadRogue references.
- `./RunTests.sh`: passed with `0 Warning(s), 0 Error(s)`
- `--init-only`: passed; startup loaded 79 materials, 17 geology entries, 19 zone definitions, 322 item definitions, and 5 creature definitions
- `HumanFortress.sln` build: passed with `0 Warning(s), 0 Error(s)`
- `git diff --check`: passed

### Important Notes

- Runtime now references Contracts, Core, Navigation, and Simulation. It still does not reference App, UI, SadConsole, or Jobs.
- Runtime now has a direct `TheSadRogue.Primitives` package reference because Runtime command target contracts expose `Point` and `Rectangle`.
- The App `SimulationRuntimeHost` is now a thin wrapper over `SimulationRuntimeHostCore`, and `SimulationRuntimeSessionFactory<THost>` now lives in Runtime. Content loading implementation, concrete job adapter composition, initial worker spawning, auto-dig seeding, input/UI wiring, and SadConsole state ownership still live in App.
- Navigation composition is split: Runtime owns the Simulation-backed navigation source/factory, while App still decides when a session creates/rebuilds the shared navigation manager.
- App `ProfessionAssignments` remains App-owned. Runtime receives only a profession weight callback, which keeps Runtime free of Jobs/App references.
- `WorkshopQueueCommandTarget` still uses the current Core content registries directly as a transitional step until the Content split gives Runtime a cleaner recipe/construction catalog seam.

## Previous Batch: Diff Target Encoding Cleanup

Status: completed

### Completed

- Added `WorldCellTarget` and `WorldCellTargetEncoding` in `HumanFortress.Simulation.World` as the shared Simulation-level bridge between world coordinates, `ChunkKey + localIndex`, and `DiffTargetEncoding`.
- Replaced duplicated App-side chunk/local target encoding in:
  - `MiningDiffEmitter`
  - `TransportDiffEmitter`
  - `ConstructionDiffEmitter`
  - `CraftDiffEmitter`
  - `SanitizeSystem`
  - `ItemSpawnCommandTarget`
- Added `WorldCellTarget` overloads to `ItemsDiffLog` for add, remove, and split-stack operations.
- Updated App item diff emitters to pass `WorldCellTarget` directly instead of unpacking `ChunkKey + localIndex`.
- Added smoke coverage proving `WorldCellTargetEncoding` produces the same chunk/local/entity target as `DiffTargetEncoding`.

### Verification

- App build: passed with `0 Warning(s), 0 Error(s)`
- `./RunTests.sh`: passed with `0 Warning(s), 0 Error(s)`
- `--init-only`: passed; startup loaded 79 materials, 17 geology entries, 19 zone definitions, 322 item definitions, and 5 creature definitions
- `HumanFortress.sln` build: passed with `0 Warning(s), 0 Error(s)`
- `git diff --check`: passed

### Important Notes

- This is a target-encoding cleanup, not a full item diff representation migration.
- `ItemsDiffLog` now accepts `WorldCellTarget` as the preferred bridge, but the older `ChunkKey + localIndex` overloads remain for compatibility.
- General `DiffLog` operations still use `DiffTarget`.
- The new helper removes repeated arithmetic and gives the later full diff-target unification a single Simulation-owned migration point.

## Older Batch: Runtime Target Helper Split

Status: completed

### Completed

- Initially split the remaining concrete command-target behavior out of `SimulationRuntimeContext` into focused helpers:
  - profession weight command-target behavior
  - `ItemSpawnCommandTarget`
  - `CreatureSpawnCommandTarget`
  - `OrderCommandTarget`
  - `ZoneCommandTarget`
- Kept `SimulationRuntimeContext` as the transitional compatibility point implementing existing command target interfaces, but reduced it to session state plus delegation.
- Preserved the existing command boundary and command interfaces so UI/input and regression tests do not need broad call-site churn.
- Removed direct concrete `World` access from all files in `src/HumanFortress.App/Commands`.
- Added runtime command target seams:
  - `IOrderCommandTarget`
  - `IZoneCommandTarget`
  - `IWorkshopQueueCommandTarget`
  - `IStockpileCommandTarget`
  - `IItemSpawnCommandTarget`
  - `ICreatureSpawnCommandTarget`
  - `IProfessionAssignmentCommandTarget`
- Migrated command families to runtime targets:
  - mining, advanced mining, hauling, structural construction, and buildable construction orders
  - zone create/update/delete
  - workshop queue add/move/remove/clear and automation/worker settings
  - stockpile creation
  - debug item spawning
  - debug creature spawning
  - profession weight changes
- Added `WorkshopQueueCommandTarget` and `StockpileCommandTarget` helper classes so `SimulationRuntimeContext` delegates richer command behavior instead of becoming a large implementation dump.
- Added regression coverage for order, zone, workshop queue, stockpile, item spawn, creature spawn, profession weight, and command-stage execution paths.

### Verification

- App fast build: passed
- `./RunTests.sh`: passed with `0 Warning(s), 0 Error(s)`
- `--init-only`: passed; startup loaded 79 materials, 17 geology entries, 19 zone definitions, 322 item definitions, and 5 creature definitions
- `HumanFortress.sln` build: passed with `0 Warning(s), 0 Error(s)`
- `git diff --check`: passed

### Important Notes

- Stockpile creation now uses a runtime seam, not `StockpileDiff`.
- `StockpileDiffApplicator` remains unsuitable as an authoritative migration target until it is connected to the tick pipeline and TODO item/job paths are resolved.
- `SimulationRuntimeContext` still implements several target interfaces as a transitional compatibility point. The concrete behavior now lives in runtime target helpers, but the next structural step is moving those helpers toward a true Runtime assembly.
- Spawn/item/job emitters still mix `ChunkKey + localIndex` and `DiffTargetEncoding.DiffTarget` target shapes. The follow-up cleanup introduced `WorldCellTargetEncoding` as a first shared migration point.

### Next Candidate Batch

1. Move the next pure runtime slice into `HumanFortress.Runtime` only after its App/UI dependencies are isolated.
2. Continue diff target unification by deciding whether `ItemsDiffLog` should eventually migrate from `WorldCellTarget` to `DiffTarget` directly.
3. Reduce the transitional interface list on `SimulationRuntimeContext` once command target aggregation has a cleaner shape.
4. Separately design the authoritative stockpile diff stage before migrating stockpile job/item placement behavior.
