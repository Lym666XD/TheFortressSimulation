# HumanFortress Architecture Overview

Updated: 2026-07-05
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

- `HumanFortress.Contracts` - shared DTOs and contract interfaces, including diagnostics event/sink contracts under `HumanFortress.Contracts.Diagnostics`, content-loading report/path/issue DTOs under `HumanFortress.Contracts.Content.Loading`, navigation contracts under `HumanFortress.Contracts.Navigation`, runtime request/status/geometry/notification DTOs under `HumanFortress.Contracts.Runtime`, runtime/UI snapshot DTOs under `HumanFortress.Contracts.Runtime.Snapshots`, Runtime save document DTOs under `HumanFortress.Contracts.Runtime.Save`, generated-world DTO/settings contracts under `HumanFortress.Contracts.WorldGen`, static item/creature definition contracts under `HumanFortress.Contracts.Simulation.Items` and `HumanFortress.Contracts.Simulation.Creatures`, world save payload DTOs under `HumanFortress.Contracts.Simulation.Save`, and content registry contracts/DTOs under `HumanFortress.Contracts.Content.Registry`.
- `HumanFortress.Core` - foundational commands, events, deterministic random, replay hash primitives, time, and world primitives.
- `HumanFortress.Content` - content loading facade, focused internal runtime registry implementation partials, internal/friend static definition loaders split by catalog family, internal/friend profession registry loader, and internal/friend runtime content snapshot capture.
- `HumanFortress.Simulation` - internal/friend simulation implementation for world, tiles, orders, items, creatures, placeables, stockpiles, zones, save payload mapping, and diff applicators. Authoritative item/creature/order/placeable managers and entities are split by responsibility inside their Simulation submodules. Stable cross-module contracts live in `HumanFortress.Contracts`; Runtime/Jobs/WorldGen/tests use friend access while App does not reference Simulation directly.
- `HumanFortress.Navigation` - internal concrete pathfinding and navigation cache implementation with no ordinary public implementation surface; public navigation contracts live in `HumanFortress.Contracts.Navigation`.
- `HumanFortress.Jobs` - internal transport, mining, construction, and craft executor cores plus domain helpers consumed by Runtime; implementation access is through internal/friend surfaces and Jobs-owned contracts rather than public helper classes. Active Jobs implementation sources use focused directory namespaces such as `HumanFortress.Jobs.Configuration`, `.Diff`, `.Logging`, `.Orchestration`, `.Profession`, `.Safety`, `.Mining`, `.Construction`, `.Craft`, `.Transport`, and `.Replay` instead of the root Jobs namespace.
- `HumanFortress.Runtime` - public runtime session/world-generation factories and session port interfaces plus internal tick pipeline, session core, generic runtime host, named internal active-session handles, concrete fortress runtime composition, and focused internal modules for command execution/targets, active-session content bootstrap, Simulation-backed navigation adapters, startup/autodig helpers, job wrappers, snapshots, save, and replay.
- `HumanFortress.WorldGen` - internal/friend concrete world-generation service/data/factory, world generator, fortress generator/map, and stage implementations under `HumanFortress.WorldGen.Implementation`; concrete fortress generation is split by generation phase inside focused implementation partials. Stable generated-world DTO/settings/service contracts live in `HumanFortress.Contracts.WorldGen`, and ordinary external creation enters through Runtime.
- `HumanFortress.App` - startup/SadConsole app host, game states, session flow, input, rendering, UI, logger binding, and App-specific delegates. It no longer directly references Core, Content, Jobs, Simulation, Navigation, or WorldGen projects.
- `HumanFortress.App.Tests` - lightweight regression/smoke test executable.

Current broad dependency direction:

```text
Contracts
  <- Core / Content / Navigation
  <- Simulation
  <- Jobs / WorldGen
  <- Runtime
  <- App / Tests
```

The formal smoke runner now locks more than this project-reference graph. It
also checks the production source-import direction matrix, Core/Runtime/App
public-surface allowlists, App.Runtime/Runtime import allowlists,
implementation project public-surface rules, and the exact `InternalsVisibleTo`
friend-assembly graph. This keeps module ownership executable: Contracts cannot
import implementation namespaces or take package/project dependencies, App can
only import App/Contracts/Runtime, and Runtime remains the only ordinary
production composition boundary that can see
Content/Core/Jobs/Navigation/Simulation/WorldGen implementation projects.
There are still transitional dependencies, but Runtime/Jobs, Content registry ownership, navigation contracts, and item/creature definition contracts now use their module namespaces in active source.

## Startup And Content Loading

Current startup path:

```text
Program
  -> AppStartupOptions / StartupContentGate
  -> Logger / diagnostics setup
  -> SadConsoleGameRunner
  -> SadConsoleGameApp
  -> GameStateManager / AppStateRegistration
```

Fortress session content path:

```text
FortressPlayGameState
  -> IFortressPlayRuntimeHost.InitializeWorld
  -> SimulationRuntimeSessionFactory.CreateNew
  -> World + Navigation creation
  -> Runtime.SimulationWorldContentLoader.LoadCoreContent
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

It also returns `FortressContentIssue` entries for missing directories, empty catalogs, registry errors, and structured-registry warnings. App logs these through the App.Diagnostics `FortressContentIssueLogger`.

Content concrete registry helpers, including the structured `ContentRegistry` and `FortressContentLoader`, are internal implementation details. `ContentRegistry` is split into focused partials for load orchestration/query/snapshot compatibility, material/terrain parsing, biome/geology parsing plus deterministic geology indexing, and tuning/zones/alias/validation/hash behavior. Core data definition loading is also split inside `HumanFortress.Content.Definitions`: `CoreDataRegistryLoader` keeps core-data directory orchestration separate from construction/workshop parsing, recipe parsing, and shared JSON helper methods. Static definition loading follows the same pattern: `ItemDefinitionCatalogLoader` keeps file traversal/options separate from legacy item/furniture parsing and validation/name enrichment, while `CreatureDefinitionCatalogLoader` keeps file traversal/options separate from creature stat validation. Runtime enters through the Content-owned loader as a friend assembly, then exposes only Contracts-owned content load reports/file-resolution DTOs to App through `FortressRuntimeContentLoader`. Runtime and tests use friend-only Content surfaces such as `CoreContentCatalogLoader`, `FortressRuntimeContentSnapshotLoader`, and `ProfessionRegistryLoader`; ordinary App/UI code should not depend on those catalog loader internals, concrete registry classes, or the Content project.

## Runtime And Tick Pipeline

Current runtime ownership:

```text
GameStateManager
  owns app state transitions and delegates scheduler/command/event/diff/runtime
  session ownership to App.Runtime session glue.

HumanFortress.Runtime
  owns public FortressRuntimeSessionFactory/session ports, internal
  FortressRuntimeSessionCore facade partials, semantic command queue request
  entrypoints, and focused internal Runtime modules for host/session/tick,
  composition, command, content, navigation, startup, geometry,
  world-generation, snapshot, save, replay, and job wrapper concerns.
  Runtime read-model and command-target helpers are split by snapshot family,
  lookup/eligibility, lifecycle role, and internal module namespace so Runtime
  does not become a new facade god object.
  Runtime-only composition helpers live in `HumanFortress.Runtime.Composition`;
  host/tick-pipeline helpers live in `HumanFortress.Runtime.Host`; session
  handles/services live in `HumanFortress.Runtime.Session`; mutation log
  bundles live in `HumanFortress.Runtime.Diff`; active-session content
  bootstrap adapters and stockpile preset mapping live in
  `HumanFortress.Runtime.Content`; Runtime geometry adapters live in
  `HumanFortress.Runtime.Geometry`; fortress-generation runner glue lives in
  `HumanFortress.Runtime.WorldGeneration`; Simulation-backed navigation
  adapters live in `HumanFortress.Runtime.Navigation`; startup/autodig helpers
  live in `HumanFortress.Runtime.Startup`; command execution and command target
  roles live in `HumanFortress.Runtime.Commands`; snapshot/save/replay/job
  wrappers keep their existing focused Runtime submodules. These helpers are
  internal implementation details. Concrete commands, command targets, Runtime
  job wrappers, command contexts, navigation-source adapters, and small catalog
  adapters use explicit interface implementations where possible so concrete
  helper types do not present a misleading public API. Runtime commands cast to
  narrow target-context roles for order, zone, stockpile, workshop, spawn, and
  profession operations instead of receiving an all-target command aggregate;
  the command stage, tick pipeline, and host core only depend on
  `ISimulationContext` plus the separate clock role.
  The simulation clock/read context is separate from the command execution
  context: `SimulationRuntimeContext` owns `ISimulationContext` plus tick
  updates, while `SimulationCommandExecutionContext` composes that read context
  with command target roles for the command stage.
  Command-side mutation logs are grouped in the Runtime-owned
  `RuntimeMutationDiffLogs` bundle, owned by `RuntimeSessionServices` for the
  active session, so command targets and post-tick applicators drain the same
  authoritative log instances and session reset clears all typed command
  mutation logs together. Runtime session enqueue also wraps commands with a
  deterministic session command identity sequence, so replay-facing Runtime
  command ids avoid random GUID generation while `CommandQueue` remains the
  authority for execution order. Save/replay persistence should read
  `CommandReplayRecord` values from `CommandQueue.GetExecutedCommandRecords()`
  rather than retaining live command instances. Runtime command replay decoding
  is owned by Runtime through a versioned payload factory; concrete decoders
  are split by command family in focused replay factory partials, while Core
  only owns the record/factory contracts. The current Runtime save document
  also carries a Contracts-owned Simulation world payload and primitive RNG
  stream rows. Simulation owns the world payload builder/restorer, split by
  authoritative section under `HumanFortress.Simulation.Save`: builder partials
  cover metadata/terrain, entities, stockpiles, placeables, orders, and shared
  conversions, while restorer partials cover payload validation, placeable
  validation/restore, conversion/failure helpers, and the main restore flow.
  Runtime validates the manifest/document and restores terrain, ground item
  instances, creature instances, global reservations, stockpile zones, active
  order designations, owned placeables/workshop state, RNG streams, and pending
  command records through a Runtime-owned full restore entrypoint. Unsupported
  item location modes and item-local reservation tokens fail closed with
  structured restore issues. The staged persistence plan is documented in
  `SAVE_REPLAY_ARCHITECTURE.md`; `SAVE_FORMAT.md` remains the longer-term
  on-disk format target.
  Public Runtime surface is intentionally centered on `FortressRuntimeSessionFactory`,
  `FortressRuntimeWorldGenerationFactory`, the App-facing
  `IFortressRuntimeAppSessionPorts` aggregate, role-specific non-save session
  port interfaces, logging bootstrap, and Runtime request/result DTOs. Public
  session ports use Contracts runtime primitives
  rather than SadConsole/SadRogue geometry, while Runtime maps those DTOs to
  current internal world geometry where needed. `FortressRuntimeSessionCore`
  and Runtime session options are internal construction/session helpers, and
  public snapshot DTOs live in `HumanFortress.Contracts.Runtime.Snapshots`.
  Full save/replay session ports are internal/friend-only; ordinary App runtime
  sessions do not expose save/load/replay checkpoint methods.
  Concrete WorldGen imports in Runtime are limited to the world-generation
  service factory facade and fortress-generation runner; other Runtime layers
  consume Contracts-owned generation requests/results or already-filled
  Simulation world state.

HumanFortress.App.Runtime
  owns runtime facade adapters over Runtime session port APIs. App supplies
  logger callback factories, UI completion handlers, and bootstrap request DTOs,
  while mapping App/SadRogue presentation geometry into Contracts runtime
  primitives before crossing the Runtime port boundary. Runtime owns the
  lower-layer callback target list, active runtime session core, WorldGen-backed
  fortress-map generation/fill, and concrete command construction. The App
  runtime facade is split by caller role, with mixed placement/debug-spawn and
  workshop-panel read/write surfaces separated into query and command role
  interfaces before they are wrapped by App.Input ports.

HumanFortress.App.Session
  owns fortress session context, load results, loaded-session presentation
  state, generated-world session queries over App world-generation ports, and
  session bootstrap orchestration.

HumanFortress.App.GameStates
  owns app state registration/navigation and delegates runtime lifetime through
  GameStateRuntimeCoordinator, GameStateRuntimeLifecycle, narrow fortress-play
  runtime host interfaces, App-owned world-generation service provider glue,
  and App-owned SadConsole screen presentation.

HumanFortress.App.Startup
  owns CLI startup option parsing, native preload, startup content gate,
  Runtime static logging callback binding, unhandled exception logging,
  headless init, crash-test runner, and SadConsole lifetime runner.

HumanFortress.App.Input / Rendering / UI
  own device input routing, SadConsole view/layout/rendering helpers, and
  transient UI/service state. UI bootstrap uses App-owned interaction data
  sources and snapshots before crossing into Runtime facade methods. Input and
  Rendering expose their own runtime port wrappers, so ordinary controllers and
  renderers depend on App module ports rather than raw App.Runtime facade
  interfaces. Input and UI renderers are split by event channel or presentation
  surface, while Runtime-built contract snapshot DTOs provide simulation facts.
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
  OrderDiffLog operations,
  WorkshopDiffLog operations,
  ZoneDiffLog operations,
  StockpileDiffLog operations,
  ProfessionAssignmentDiffLog operations,
  and navigation rebuilds for dirty chunks.
```

This is not yet the full nine-stage `UPDATE_ORDER.md` model. The important current invariant is:

- commands execute before read systems;
- planning is read phase;
- authoritative mutation happens in write/post-tick applicators;
- profession weight commands now queue `ProfessionAssignmentDiffLog` entries and are applied post-tick through the bound profession assignment handler;
- order commands now queue `OrderDiffLog` entries and are applied by the post-tick order applicator rather than by direct Runtime command-target mutation;
- workshop queue/settings commands now queue `WorkshopDiffLog` entries and are applied by the post-tick workshop applicator rather than by direct Runtime command-target mutation;
- zone create/update/delete commands now queue `ZoneDiffLog` entries and are applied by the post-tick zone applicator rather than by direct Runtime command-target mutation;
- stockpile creation and deletion now queue `StockpileDiffLog` entries and are applied by the post-tick stockpile applicator rather than by direct Runtime command-target mutation;
- stockpile create diffs carry preset-derived filter/priority data loaded through Content contract definitions and mapped by Runtime, so preset rules are applied by the post-tick stockpile applicator;
- stockpile delete diffs carry only the zone id; the stockpile applicator reads current authoritative member chunks at apply time before deleting shards and the global zone;
- stockpile filter matching uses Simulation item projections for definition id/tags/materials; stockpile cell/destination lookup is centralized in Simulation `StockpileWorldQueries`; the hauling planner queues stockpile reserve-slot diffs beside transport requests while tracking same-tick planned reservations; Jobs/Transport queues stockpile item-index place/remove/release diffs for transport pickup/delivery/cancel paths and for construction/craft full-stack item consumption; remaining stockpile maintenance/broker work belongs with Jobs/Transport rather than App or Content;
- command-target construction and tick-pipeline application share `RuntimeMutationDiffLogs` instead of passing every typed log separately through host/context/pipeline constructors;
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

`HumanFortress.Jobs` also owns scheduler/workshop tuning types, worker-selection strategy, profession assignment/selection state, concrete job diff emitters, profession/craft adapters, callback job loggers, mining drop/tuning resolution, construction terrain-material resolution, `UnifiedJobsOrchestrator`, and the low-frequency `SanitizeSystem` safety net. These Jobs implementation types are internal; Runtime and tests reach them through transitional friend access. Profession contract DTOs/interfaces compile from `HumanFortress.Contracts.Jobs`, and profession registry JSON loading enters through Content's internal/friend `ProfessionRegistryLoader` while the concrete registry implementation stays internal.

Jobs-owned executors consume navigation through `HumanFortress.Contracts.Navigation` (`IPathService`, `IWorldNavigationView`, and `IMovementExecutor`). Runtime job-system wrappers create the internal concrete `HumanFortress.Navigation.Implementation` services (`PathService`, `WorldNavigationView`, and `MovementExecutor`) and inject the contract interfaces, so Jobs does not reference the concrete Navigation project.

`HumanFortress.Runtime` now owns the tick-facing transport/mining/construction/craft job-system wrappers in `HumanFortress.Runtime.Jobs`, active world content application, optional startup auto-dig command seeding, construction workshop-completion notification bridging, plus the concrete fortress runtime system collection/factory/grouping layer. `HumanFortress.Content` owns profession registry file loading from `professions.json` and returns the `IProfessionRegistry` contract to Runtime.

App still owns UI bootstrap and App-specific handlers: it passes logging callbacks, supplies the auto-dig setting, and binds construction workshop-completion notifications into the UI through Runtime's notification bridge.

The App runtime composition layer is now split into smaller migration points:

- Runtime-owned `FortressRuntimeHostFactory` creates the generic Runtime host for a fortress world.
- Runtime-owned `FortressRuntimeDependencies` groups construction/recipe catalogs, scheduler/workshop tunings, and workforce dependencies.
- Runtime-owned `FortressRuntimePlanningSystems` builds planners and the shared `TransportRequestQueue`.
- Runtime-owned `FortressRuntimeJobSystems` builds Runtime-owned job-system wrappers over Jobs-owned executor cores. The planning and job-system composition groups now live in separate Runtime files so constructor policy does not regrow as one mixed composition object.
- Runtime-owned `FortressRuntimeSystemsFactory` assembles the concrete runtime system collection.
- Runtime-owned `FortressRuntimeStartup` handles initial-worker/profession setup and invokes Runtime-owned optional auto-dig seeding when enabled by App settings.

The composition center has moved to Runtime. The remaining App coupling is mostly logging callback injection, UI completion binding, session bootstrap orchestration, live debug/UI access facades, and compatibility namespaces.

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

- UI still has transitional runtime/session/bootstrap glue, especially around session initialization and App-owned UI callback binding.
- Main map terrain/entity rendering, frame render data, overlay frame data, Work/jobs/profession panels, Work drawer labor/order summaries, Work drawer workshop lists/status panels, F1/F2/F4 management drawer lists, zone/stockpile overlay/detail popups, stockpile preset menu options, stockpile/zone hit-testing, navigation debug overlay draw modes, F10 path-debug queries, tile click logging, haul/mining/construction placement previews, construction order highlight dots, debug spawn readiness/count logging, workshop panel keyboard editing, detailed workshop panel rendering, workshop overlay/material-progress rendering, workshop map-click hit-testing, build quick-menu workshop browsing, buildable placement preview, Debug menu status/items, tile inspection popups, and mining job highlights now consume Runtime-built snapshot DTO contracts from `HumanFortress.Contracts.Runtime.Snapshots` through App facade methods instead of reading concrete Runtime job wrappers, `ProfessionAssignments`, construction/recipe catalogs, live order/creature/item/zone/stockpile lists, item definitions, tile/geology data, visible zone/stockpile chunks/shards, live navigation chunks/caches/path objects, mutable `WorkshopState`, live workshop placeables, `FortressMap`, or live terrain/entity managers directly through `FortressRuntimeAccess`/former `UiRenderer`/map-click/rendering helpers. Overlay/input contexts carry explicit UI/navigation/map-availability dependencies rather than the full loaded-session snapshot, and loaded-session state/load results no longer carry live `World` or `FortressMap` objects.
- The SadConsole presentation layer is being split within App instead of pushed down into Runtime: overlay orchestration lives in `HumanFortress.App.Rendering`, map overlay glyph drawing is split by overlay type in `FortressMapOverlayGlyphRenderer` partials, placement preview rendering lives in `FortressPlacementOverlayRenderer`, chrome/topbar/dock drawing in `UiChromeRenderer`, management drawer drawing in `UiManagementDrawerRenderer` partial tab renderers, Debug menu drawing in `UiDebugMenuRenderer` partial tab renderers, quick-menu drawing in `UiQuickMenuRenderer` plus focused `OrdersUI`/`ZonesUI`/`StockpileUI` partials, Work drawer panels in `UiWorkDrawerRenderer` partial tab renderers, and workshop modal drawing in `UiWorkshopPanelRenderer`. These classes are allowed to know about `ScreenSurface`, `UiStore`, and input presentation state, but simulation facts should continue to enter as Contracts snapshot DTOs.
- App input/presentation helpers are being split by event channel and UI surface inside App: SadConsole component input, screen chrome hit testing, root/submenu quick-menu hit testing, Build/Zone menu input/rendering, Debug menu clicks, Work allocation input, placement overlay/controller behavior, navigation overlay drawing, UI state navigation/drawers/quick menus, chrome buttons/modals/toasts, button layout, main/embark/worldgen menu rendering, UI command objects, and legacy log classification are separate App partials/helpers rather than Runtime/Jobs/Simulation code.
- Runtime snapshot construction is split by read-model family rather than concentrated behind a single god builder: navigation basic/structural overlay modes/path cells, map viewport terrain/entity glyph policy, workshop summaries/material progress, management drawer data, stockpile overlay/detail/hit tests, jobs debug data, frame/overlay aggregates, and session-level Work/map/debug query entrypoints live in focused snapshot builder partials.
- Generated-world world-map/embark presentation reads through `HumanFortress.App.Session` query methods over `HumanFortress.Contracts.WorldGen` DTOs such as `WorldMapTileView` and `WorldTileSnapshot` rather than letting App screens read `WorldGenResult.Tiles`, raw `WorldTile`, concrete `GeneratedWorldData`, or `BiomeType`. App no longer references the WorldGen assembly directly; world-generation UI receives the contract `IWorldGenerationService` from Runtime's `FortressRuntimeWorldGenerationFactory`, and Runtime owns fortress-map generation/fill.
- App runtime access is split by caller role. `IFortressRuntimeReadAccess` is the render/read-model facade; keyboard, UI-input, placement, map-inspection, debug-spawn, workshop-panel, navigation-debug, simulation-control, session-bootstrap, and semantic command-request paths use smaller interfaces instead of a full play facade. `GameStateRuntimeCoordinator` creates the active Runtime session through `FortressRuntimeSessionFactory`, keeps only `IFortressRuntimeSessionPorts`, binds the concrete `FortressRuntimeAccess` adapter to module-owned port groups, and hands `FortressState` a `FortressStateRuntimePorts` bundle made from `App.Rendering`, `App.Input`, and `App.Session` port packages. Runtime still owns save/load ports internally, but fortress-play App UI no longer exposes a save/load facade until a real save UI boundary exists. App active source should not reference Core, Simulation, Jobs, Navigation, WorldGen, `HumanFortress.Runtime.Commands`, `HumanFortress.Runtime.Save`, or `HumanFortress.Runtime.Replay`; command construction belongs in Runtime.
- App active source should also not reference `HumanFortress.Content`. Startup content validation enters through `FortressRuntimeContentLoader`, while UI registry-file lookup uses the App-owned `AppContentFileLocator` wrapper so direct Runtime imports stay confined to startup/adapter/content-location boundaries.
- `FortressRuntimeAccess` is an App-internal adapter over Runtime session ports. Runtime access is consumed through explicit App role interfaces rather than ordinary public methods on a concrete Runtime core, and GameStates no longer construct Runtime options or concrete Runtime session implementations directly.

Target direction remains:

```text
Simulation -> immutable/debug snapshots -> UI
UI -> semantic Runtime command requests -> Runtime command stage -> Simulation
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

Current public Content entry points:

- `FortressContentLoader`

Internal/friend Content implementation loaders include `CoreContentCatalogLoader`, `FortressRuntimeContentSnapshotLoader`, `ProfessionRegistryLoader`, `RuntimeContentRegistryLoader`, `ItemDefinitionCatalogLoader`, `CreatureDefinitionCatalogLoader`, and `CoreDataRegistryLoader`.

The old `.cpack` content build pipeline remains a future design archived at `docs/archive/legacy/CONTENT_BUILD_PIPELINE_FUTURE.md`.

## Current Known Gaps

- Generic runtime host/lifecycle and concrete fortress system construction now live in `HumanFortress.Runtime`.
- Runtime-owned job wrappers now exist around Jobs-owned executor cores; App still owns UI/bootstrap binding, logger callback injection, App-specific UI handlers, and fortress session flow.
- UI no longer reads live World/navigation state for main map terrain/entity rendering, frame render data, overlay frame data, Work drawer jobs/workforce/order/workshop summaries, F1/F2/F4 management drawer lists, zone/stockpile overlay/detail popups, stockpile/zone hit-testing, navigation debug overlay/path modes, tile click logging, haul/mining/construction placement previews, construction order highlight dots, debug spawn readiness, workshop panel keyboard editing, detailed workshop panel rendering, workshop overlay/material-progress rendering, workshop click-hit testing, build workshop browsing/preview, Debug menu status/items, or tile inspection popups. Those paths use Runtime-built Contracts snapshot DTO facades instead of direct concrete job-system, order, creature/item/zone/stockpile manager, construction catalog, item definition, tile/geology, visible zone/stockpile chunk/shard, live navigation cache/path object, mutable workshop state, live-placeable, `FortressMap`, or terrain/entity manager access. `FortressRuntimeAccess` hands the active Runtime core to `FortressRuntimeSessionSnapshotFacade` for read models instead of letting `GameStateManager` unpack live session internals per query. App source/project references to Jobs, Simulation, and Navigation have been removed; remaining live World exposure is the scoped fortress-map fill/bootstrap step plus Runtime-owned content injection.
- The old legacy content registry source has been deleted; normal bootstrap loads the structured runtime registry only.
- `ConstructionRegistry` and `RecipeRegistry` singleton compatibility classes have been deleted; construction/recipe/material/terrain/geology/biome definitions, terrain bit-layout DTOs, alias/migration DTOs, catalog interfaces, immutable catalog stores, runtime material/terrain/geology catalog interfaces, construction/placeable tuning types, fixed-point material primitives, and content version/snapshot/validation result types now compile from `HumanFortress.Contracts.Content.Registry`.
- `CoreDataRegistryLoader` now compiles from `HumanFortress.Content.Definitions` as an internal implementation detail, and the structured `ContentRegistry` implementation compiles from `HumanFortress.Content.Registry` as focused internal partials; the remaining content gaps are strict fail-fast policy, richer diagnostics/debug surfaces, and the future compiled pack pipeline.
- Full save/load, storyteller, combat, full snapshot rendering, and compiled content packs are not complete current systems.

## High-Signal References

- `../planning/ARCHITECTURE_REFACTOR_MASTER_PLAN.md`
- `../planning/REFACTOR_BATCH_PROGRESS.md`
- `../planning/REFACTOR_PITFALLS_AND_LESSONS.md`
- `../content/CONTENT_SYSTEM.md`
- `../simulation/WORK_AND_JOBS_SYSTEM.md`
- `../simulation/TRANSPORT_SYSTEM.md`
- `UPDATE_ORDER.md`
