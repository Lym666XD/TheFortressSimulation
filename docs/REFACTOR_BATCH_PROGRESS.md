# Refactor Batch Progress

This document tracks the current multi-step refactor batches so progress is visible without relying on chat history.

## Current Batch: Construction and Recipe Catalog Snapshots

Status: completed

### Completed

- Added immutable construction and recipe catalog snapshots:
  - `ConstructionCatalogStore`
  - `RecipeCatalogStore`
- Changed `CoreDataRegistryLoader` so it parses construction/workshop and recipe JSON into fresh catalog snapshots instead of mutating `ConstructionRegistry.Instance` and `RecipeRegistry.Instance`.
- Changed `ContentRegistry` to own current construction/recipe snapshots as instance fields exposed through `IConstructionCatalog` and `IRecipeCatalog`.
- `ContentRegistry.LoadCoreData(...)` now swaps the current construction/recipe snapshots from the load result, matching the item/creature snapshot pattern.
- `ContentRegistry.LoadContent(...)` / `LoadContentAsync(...)` now resets construction/recipe snapshots to empty as part of runtime content clearing.
- Changed App craft composition so `CraftRecipeCatalogAdapter` receives an explicit `IRecipeCatalog` instead of reading `ContentRegistry.Instance.Recipes` internally.
- Added regression coverage proving repeated `LoadCoreData(...)` calls keep construction/recipe counts and workshop/category queries stable instead of accumulating duplicate indexes.

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
- Changed `HumanFortress.Core.Content.Registry.ContentRegistry` to expose construction and recipe content through read-only catalog interfaces instead of concrete mutable registry types.
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
- App runtime composition now resolves `ContentRegistry.Instance.Constructions` once and passes the catalog to buildable construction, construction jobs, craft planner, and craft jobs.
- Replaced remaining recipe-registry test fixture writes with small in-memory test catalogs, so `ConstructionRegistry.Instance` / `RecipeRegistry.Instance` are now only referenced inside the structured `ContentRegistry`.

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
- `SimulationRuntimeContext` keeps a fallback to `ContentRegistry.Instance` for compatibility, but the App host and command-target tests now supply recipe/construction catalogs explicitly where needed.
- Remaining content-global reads now mostly concern geology/tuning/item dependencies or App/UI convenience paths. Those should be handled in later content/runtime cleanup passes.

## Previous Batch: Core Data Registry Loading Unification

Status: completed

### Completed

- Moved construction/workshop and recipe JSON loading behind `HumanFortress.Core.Content.Registry.ContentRegistry.LoadCoreData`.
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
- The legacy `HumanFortress.Core.Content.ContentRegistry` still exists for compatibility while material/geology migration is completed.

## Previous Batch: Runtime Content Registry Unification

Status: completed

### Completed

- Promoted `HumanFortress.Core.Content.Registry.ContentRegistry` toward the single authoritative content registry by adding runtime content capabilities that previously only existed in the legacy registry:
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

- The legacy `HumanFortress.Core.Content.ContentRegistry` still exists and is still loaded by `ContentLoadCoordinator` for compatibility. It is no longer the desired runtime read target for geology/tuning/zones.
- The console summary from the legacy registry still appears during startup. Remove it when the legacy registry is deleted or replaced by a pure migration shim.
- At that point, `ConstructionRegistry`, `RecipeRegistry`, item definitions, and creature definitions still had separate loading paths. Later batches moved item/creature loading into `HumanFortress.Content.Definitions` and changed construction/recipe loading to produce immutable snapshots.
- `geology_prototypes.json` remains present but should not override runtime `geology.json` until its ids and material references are aligned with the active `core_terrain_*` content model.

## Previous Batch: Content Registry Bootstrap Unification

Status: completed

### Completed

- Added `ContentLoadCoordinator` as the first shared loading entry point while the legacy and structured content registries still coexist.
- App startup now loads both:
  - legacy `HumanFortress.Core.Content.ContentRegistry`
  - structured `HumanFortress.Core.Content.Registry.ContentRegistry`
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
- Routed the secondary `HumanFortress.Core.Content.Registry.ContentRegistry` and its material/terrain/geology/biome/alias helper registries through a shared content diagnostics helper with console fallback when App logging is not initialized.
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
- Updated App job shells, session creation, runtime host wiring, fortress initialization, and smoke tests to consume the Runtime-owned pipeline/navigation/session factory.
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
