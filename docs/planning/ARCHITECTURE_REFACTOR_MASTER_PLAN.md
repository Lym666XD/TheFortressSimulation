# HumanFortress Architecture Refactor Master Plan

Date: 2026-05-31
Status: working refactor plan
Sources merged:
- `docs/archive/plans/ARCHITECTURE_ISSUE_CHATGPT_SOURCE.md`
- `docs/archive/plans/ARCHITECTURE_ISSUE_CLAUDE_SOURCE.txt`
- `docs/archive/plans/HUMANFORTRESS_MAIN_BRANCH_ARCHITECTURE_AUDIT_FOR_CODEX.md` (reconciled 2026-06-12)

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

## Current Refactor Progress - 2026-07-05
- Save/replay architecture has a staged boundary rather than a full save implementation: Core owns immutable pending/executed command replay records, command queue replay snapshots, replay factory contracts, command journal hashing, and canonical replay hash primitives; Runtime owns a strict internal command replay factory, v1 command payload versioning, batch-atomic command replay restore, Runtime replay checkpoints, save manifest construction, and a save snapshot package port; Simulation owns field-specific and aggregate authoritative replay hash builders plus a minimal internal world save summary for section hashes/counts. App remains out of persistence/runtime decoding and does not assemble save authority from live world/job/command objects.
- Architecture boundary checks have moved from ad-hoc review into the formal smoke runner. Tests now enforce the approved production project-reference graph, the production source-import direction matrix, the dependency-free Contracts project rule, the Contracts/Runtime-public-port presentation-primitive ban, and the exact `InternalsVisibleTo` friend-assembly graph; forbid active App source from importing lower implementation modules or old mixed runtime facade names; keep ordinary App.Runtime usage inside adapter/port composition files; lock implementation public surface plus Core/Runtime/App public API allowlists; and scan save/replay/hash authority paths for unstable object hashes, dictionary `Keys`/`Values` view iteration, and production `Guid.NewGuid()` identity generation.
- Runtime save JSON codec/store helpers are internal/friend implementation details; App-facing persistence remains Runtime ports plus Contracts DTOs.
- Runtime session ports are split by audience: the public factory returns an App-facing aggregate that excludes save/replay checkpoint capabilities, while the full save/replay aggregate is internal/friend-only for Runtime tests and future dedicated save UI boundaries.
- The App/Core direct dependency has been removed from production code. Diagnostic event/sink/level contracts plus the transitional `DiagnosticHub` now live in `HumanFortress.Contracts.Diagnostics`, so App owns log sinks and UI presentation against Contracts while Core/Content/Simulation/WorldGen emit through the same contract bridge. Content also no longer references Core; its production reference set is Contracts only.
- The App/Content direct dependency has also been removed from production code. App-facing content load issue/path/report DTOs live in `HumanFortress.Contracts.Content.Loading`; Runtime owns the public content startup/file-location facade over the internal Content loader; App wraps registry-file lookup in `AppContentFileLocator` for UI configuration. Production App references are now Contracts + Runtime only.
- Contracts content registry files now match their namespace layout, including
  `FixedPoint` under `Contracts/Content/Registry`, reducing the remaining
  compatibility cleanup surface around old Content/Core registry names.
- Content's structured registry and definition loading are now split by
  responsibility. `ContentRegistry` partials separate material/terrain,
  biome/geology, tuning/zones/validation/hash behavior from registry
  orchestration; `CoreDataRegistryLoader` partials separate
  construction/workshop parsing, recipe parsing, and shared JSON helpers; and
  `ItemDefinitionCatalogLoader` partials separate deterministic file traversal
  from legacy item/furniture parsing and validation/name enrichment; and
  `CreatureDefinitionCatalogLoader` partials separate deterministic file
  traversal from creature stat validation.
  Architecture smoke tests lock these split families so Content compatibility
  parsing does not drift back into one mixed loader/registry God Object.
- Concrete Navigation implementation namespaces have been moved from the old
  compatibility root `HumanFortress.Navigation` namespace to
  `HumanFortress.Navigation.Implementation`. Navigation contracts remain under
  `HumanFortress.Contracts.Navigation`, and Runtime remains the only production
  composition boundary that imports concrete pathfinding/cache implementations.
- Concrete WorldGen implementation namespaces have likewise been moved from the
  root `HumanFortress.WorldGen` namespace to
  `HumanFortress.WorldGen.Implementation`. World-generation contracts remain
  under `HumanFortress.Contracts.WorldGen`; Runtime remains the only ordinary
  production composition boundary, and its concrete WorldGen imports are
  restricted to the world-generation factory/fortress-generation runner.
- `FortressGenerator` is now split by generation phase under the concrete
  WorldGen implementation namespace. Its main file keeps constructor state and
  pipeline ordering, while cavern carving, strata/surface filling, ore
  placement, and tuning JSON helper logic live in focused partials locked by
  architecture smoke tests.
- Jobs implementation sources have been aligned with their directory
  namespaces rather than living under the root `HumanFortress.Jobs` namespace:
  configuration/tuning, diff emitters, callback loggers, orchestration,
  profession bridges, safety sanitizer, mining, construction, craft, transport,
  and replay helpers each live under their focused `HumanFortress.Jobs.*`
  namespace.

Estimated architecture-foundation progress: **99%+**.

This percentage tracks the foundation/boundary cleanup from prototype structure toward a maintainable deterministic simulation architecture. It is not a gameplay feature-completion estimate and not the total refactor-program completion estimate. The wider refactor still includes UI snapshot isolation, deterministic replay hardening, save/migration, namespace cleanup, diagnostics polish, movement ownership, and performance work.

### 2026-06-12 Main-Branch Audit Reconciliation

- The external audit's Content project build concern has been checked against the current branch. `HumanFortress.Content` and `HumanFortress.sln` build successfully; the apparent dependency issue is caused by transitional namespaces that now compile from `HumanFortress.Contracts`.
- The audit's warning about construction/recipe singleton registries has been resolved after the audit snapshot. The old singleton compatibility classes have been deleted, core-data loading now returns immutable construction/recipe catalog snapshots through `HumanFortress.Content`, and `ContentRegistry.ApplyCoreData(...)` swaps those snapshots without mutating global construction/recipe registries.
- The audit's Content orchestration concern is now substantially reduced: item/creature loading, construction/recipe core-data loading, runtime registry bootstrap, structured registry implementation, strict fail-fast loading, and profession registry loading now enter through `HumanFortress.Content` behind Runtime/Content boundaries. App registry-file path resolution now enters through an App wrapper over Runtime's content-location facade rather than the Content project. Active fortress world content application lives in Runtime's `SimulationWorldContentLoader`, with App only injecting logs/content issue reporting through Contracts DTOs. Runtime geology/zone DTOs, registry contracts, material/terrain/geology/profession reads, and immutable catalog facades compile from `HumanFortress.Contracts.Content.Registry` rather than concrete Content registries, and remaining work is richer debug surfaces plus future compiled-pack support.
- The audit's runtime-composition concern has been largely addressed after the audit snapshot. `HumanFortress.Runtime` now owns the generic `SimulationRuntimeHost<TSystems>`, host-core lifecycle, tick-facing job wrappers, concrete `SimulationRuntimeSystems` collection, runtime dependency/system factories, composition/startup/content/navigation helper modules, command execution/target modules, optional startup auto-dig command seeding, and construction workshop-completion notification bridge. App still injects logger callbacks, the auto-dig setting, and UI notification handlers, and it still owns UI/SadConsole lifetime.
- Recent no-build hardening further narrowed API boundaries: the old App-local `FortressRuntimeSessionController` passthrough and App-owned `FortressSimulationStatus` wrapper were deleted, `FortressRuntimeAccess` now adapts over Runtime session port interfaces created by `FortressRuntimeSessionFactory`, Runtime public session ports use Contracts-owned runtime geometry/notification DTOs rather than SadRogue presentation primitives, `FortressRuntimeSessionCore` and Runtime session options are internal helpers, Simulation implementation types are internal/friend to Runtime/Jobs/WorldGen/tests, `FortressStateRuntimePorts` makes App play-state composition explicit by caller role, the old broad keyboard runtime facade was split into module-owned input ports, mixed placement/debug-spawn/workshop-panel runtime facades were split into query and command roles, Rendering wraps read/UI-input roles behind view-owned ports, Runtime `SimulationStatus` now lives in `HumanFortress.Contracts.Runtime` and is consumed directly by App chrome, Runtime snapshot DTOs now live in `HumanFortress.Contracts.Runtime.Snapshots` with project-owned snapshot primitives, item/creature definition contracts now use `HumanFortress.Contracts.Simulation.*` namespaces, generated-world DTO/settings/service/data-query contracts now live in `HumanFortress.Contracts.WorldGen`, the concrete Content registry and runtime/core catalog loaders are internal/friend implementation surfaces, concrete Jobs/Navigation implementation projects expose no ordinary public implementation members, App implementation modules are internal except for the executable entry point, and Runtime command execution uses an explicit simulation clock/read context plus a separate command execution context with narrow target roles instead of aggregate command contexts.
- The audit's direct UI live-world concern has been largely addressed by Runtime-owned snapshot/query facades and Contracts-owned read DTOs. Current App source scans show no active direct App references to Core, Content, Jobs, Simulation, Navigation, WorldGen, Runtime.Commands, Runtime.Save, or Runtime.Replay; remaining Runtime access in App is concentrated in App.Runtime adapters, startup, world-generation provider glue, and an App-owned content-file-location wrapper. Movement/path ownership is still a later hardening area.
- Latest source-only command hardening grouped command-side mutation logs into `RuntimeMutationDiffLogs` so command targets and post-tick applicators share the same log bundle by construction. `SimulationRuntimeContext` remains a clock/read context, while `SimulationCommandExecutionContext` owns narrow command target roles for the command stage.
- Latest source-only App state hardening moved `FortressState` per-frame sequencing into `FortressStateUpdateLoop` plus `FortressUiTickCounter`, keeping the SadConsole state as a thin lifecycle/composition shell.
- Latest source-only Runtime session hardening introduced `RuntimeSessionServices` as the owner for per-session scheduler, command queue, event bus, main diff log, items diff log, and the typed `RuntimeMutationDiffLogs` bundle. Session reset now clears all typed command mutation logs, and the active Runtime host path receives the shared bundle through the service group instead of constructing an extra mutation-log bundle at host composition time.
- Latest source-only bootstrap hardening moved fortress generation request mapping/seed/content assembly into `RuntimeFortressGenerationRunner`, keeping `FortressRuntimeSessionCore` focused on session port implementation and runtime-world fill/navigation rebuild delegation.
- Latest source-only stockpile/diff hardening made stockpile delete diffs zone-id-only, with member-chunk resolution deferred to `StockpileDiffApplicator` against current authoritative world state. Stockpile diffs now enter through world-level `ApplyAll(world, diffs)` only; the unused chunk-local apply entry has been removed. Typed mutation diff logs/applicators in Simulation/Runtime now use internal implementation members, and smoke coverage documents the intentional ordering split between enqueue-ordered command-edit diffs and spatial/priority item/stockpile diffs.
- Latest build-verified stockpile/transport hardening connected stockpile item-index updates to Jobs/Transport seams. Runtime injects the session-owned `StockpileDiffLog` into transport, construction, and craft diff emitters; transport pickup/delivery/cancel paths queue stockpile remove/place/release diffs; construction/craft full-stack consumption queues stockpile remove diffs; and item-index diffs carry projected `ItemStackRef` payloads so tag indexes remain clean even when the item has already been deleted by the item diff applicator. `StockpileWorldQueries` is now the Simulation-owned cell/destination query seam, and transport destination validation rechecks stockpile filters against item projections.
- Latest build-verified reservation follow-up injects the same session-owned `StockpileDiffLog` into the hauling planner. Hauling destination selection now subtracts same-tick planned reservations from shard capacity and queues `ReserveSlot` diffs beside `TransportRequest` records; transport cancellation/failure releases those slots through the existing stockpile diff path. Transport pickup now only treats "already in stockpile" as complete for `ToStockpile` work, so non-stockpile jobs can consume stockpiled items while emitting stockpile remove-index diffs.
- Latest build/test-verified stockpile transport hardening removes the unused stockpile-local broker and `CreateHaulJob` diff op. Transport intake now reports whether it added a new pending request, the queue keeps a single pending transport intent per item with consistent shard indexes after merges, and `HaulingSystem` only writes stockpile reservation diffs for accepted new requests.
- Latest build/test-verified stockpile UI boundary hardening carries the Runtime content-backed preset menu through `SimulationUiOverlayFrameData`, so App stockpile preset selection consumes Contracts DTOs rather than hardcoded ids or Content JSON.
- Latest source-only module-boundary hardening removed App's direct `HumanFortress.WorldGen` project reference. App creates world generation through Runtime's `FortressRuntimeWorldGenerationFactory`, while the concrete `WorldGenerationServiceFactory` is internal to WorldGen/friend consumers.
- Latest source-only replay/boundary hardening removed production `Guid.NewGuid()` command identity generation. Runtime command ids are deterministic from tick/type/payload, Runtime session enqueue adds a resettable deterministic sequence wrapper for duplicate-command disambiguation, workshop commands now serialize their mutation payload, mining rectangle scans now use inclusive SadRogue `MaxExtent*` bounds, and several false-public internal helper surfaces in Simulation/App were narrowed.
- Latest source-only follow-up hardened replay diagnostics and command-context boundaries: construction command ids now include sorted material tag filters, command failure diagnostics include the queue sequence, and the remaining internal all-target command execution aggregate was deleted so host/pipeline/stage signatures depend only on `ISimulationContext` while individual commands request narrow role contexts.
- Latest source-only save/replay planning added a staged `SAVE_REPLAY_ARCHITECTURE.md` bridge and Core `ICommandReplayFactory` seam, explicitly deferring full save/load implementation until command replay, authoritative world snapshot, and deterministic hash seams are stable.

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
- Aligned Jobs implementation helper namespaces with their directories:
  configuration/tunings, concrete diff emitters, callback loggers, orchestration
  contracts/core, profession bridges, safety sanitizer, mining, construction,
  craft, transport, and replay helpers now live under focused
  `HumanFortress.Jobs.*` namespaces instead of the root Jobs namespace.
- Added a Jobs-owned `ConstructionJobExecutor` core that owns construction site scanning, readiness/progress, material consumption, safety relocation, completion, and stats.
- Reduced `ConstructionJobSystem` to a composition shell, then moved that wrapper source to Runtime; concrete construction diff/logging helpers are now Jobs-owned and App only binds the UI completion callback during bootstrap.
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
- Reduced `CraftJobSystem` to a composition shell, then moved that wrapper source to Runtime; craft diff emission and profession/recipe adapters are now Jobs-owned.
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
- Added the first structured diagnostics layer: Contracts diagnostic events/sinks, App async dispatcher, main timeline log, category-routed log files, in-memory ring buffer, and compatibility bridging for existing `Logger.Log`/`LogCallback` paths.
- Added the missing ore material definitions referenced by `geology.json`, clearing the remaining startup geology cross-reference errors.
- Made construction loading de-duplicate new workshop definitions against legacy `placeable/workshops.json` entries, turning duplicate workshop ids into explicit skipped diagnostics instead of a registry load error.
- Made recipe loading tolerate legacy root-array recipe files without treating them as parse failures; current recipe loading reports `errors=0`.
- Added rule-level embarkability diagnostics to `WorldTile` and displayed failure reasons in the WorldMap side panel instead of only showing `NOT EMBARKABLE`.
- Added regression coverage for embarkability diagnostics.
- Added `HumanFortress.Runtime` as the first dedicated runtime assembly and wired it into the active solution.
- Moved `SimulationCommandStage`, `SimulationStatus`, `SimulationTickPipeline`, `SimulationRuntimeContext`, Runtime command target interfaces/helpers, the Simulation-backed navigation source/factory, `SimulationRuntimeHostCore`, the generic `SimulationRuntimeSession<THost>` handle, and `SimulationRuntimeSessionFactory<THost>` into `HumanFortress.Runtime`, with App now referencing the runtime assembly for command-stage execution, status snapshots, tick-pipeline barriers, command target dispatch, shared navigation creation, scheduler/pipeline lifecycle, and session creation.
- `GameStateManager` no longer directly creates or stores separate World/Navigation/RuntimeHost fields or the active runtime session handle. The old App.Runtime `FortressRuntimeSessionController` intermediate bridge was later removed; `GameStateRuntimeCoordinator` creates the active Runtime session through `FortressRuntimeSessionFactory`, stores only `IFortressRuntimeSessionPorts`, and Runtime delegates world reset, navigation creation, queue/diff cleanup, content-loading callback invocation, and host-wrapper callback invocation to the Runtime-owned generic session factory.
- Moved the runtime host wrapper itself into `HumanFortress.Runtime` as `SimulationRuntimeHost<TSystems>`, deleting the App-owned wrapper.
- Added then moved `FortressRuntimeHostFactory` into Runtime so `GameStateManager` no longer directly constructs the generic host; App now only supplies logging and content snapshot inputs.
- Split concrete system construction out of `SimulationRuntimeSystems` into `FortressRuntimeSystemsFactory`, then moved both the system collection and concrete factory/grouping layer into Runtime.
- Split initial-worker and optional auto-dig startup hooks into `FortressRuntimeStartup`, then moved generic startup orchestration into Runtime; a later cleanup moved the auto-dig command seeding itself into Runtime's `RuntimeAutoDigSeeder`.
- Split App runtime system assembly into explicit dependency/planner/job-system groups, so catalog/tuning/profession loading, planning system creation, and App job executor shells now have separate migration points.
- Unified host-context catalog injection and concrete system assembly behind one `FortressRuntimeDependencies` instance, so runtime context and runtime systems receive the same construction/recipe catalog snapshots.
- Split runtime dependency loading again into `FortressRuntimeCatalogs`, `FortressRuntimeTunings`, and `FortressRuntimeWorkforce`, giving Content/tuning/profession ownership separate migration handles.
- Added a Content-owned runtime snapshot loader that captures construction/recipe catalogs and scheduler/workshop tuning JSON, so App runtime composition consumes a snapshot instead of directly traversing the structured content registry.
- Expanded the Content-owned runtime snapshot to expose material, terrain, geology, and zone read-only contract data needed by diagnostics/tests, then made the concrete structured `ContentRegistry` internal so cross-module callers use Content facades rather than the singleton implementation.
- Changed scheduler/workshop runtime tuning composition to parse JSON strings from that snapshot, keeping `JObject` registry access behind the Content boundary.
- Removed the Runtime command-context fallback to global content state; workshop queue command targets now receive explicit recipe/construction catalog dependencies.
- Routed App workshop/build UI construction and recipe lookups through active-session catalog facades/DTOs instead of direct `ContentRegistry.Instance` reads or UI-facing construction/recipe catalog access.
- Added a first-pass Runtime-owned jobs/workforce/orders/workshop/build/debug/tile-inspection/management-drawer/zone-overlay/detail/stockpile/navigation-overlay/navigation-path/placement-preview snapshot facade in `HumanFortress.Runtime.Snapshots`, with public DTO contracts now in `HumanFortress.Contracts.Runtime.Snapshots`, so Work drawer panels, profession allocation input, active job rows, scheduler diagnostics, order/labor summaries, workshop lists/status panels, F1/F2/F4 management drawer lists, zone/stockpile overlay/detail popups, stockpile/zone hit-testing, navigation debug overlay draw modes, F10 path-debug queries, tile click logging, haul/mining/construction placement previews, detailed workshop panel rendering, workshop overlay/material-progress rendering, workshop click-hit testing, build workshop browsing/placement preview, Debug menu status/items, tile inspection popups, and mining job highlights no longer read concrete Runtime job wrappers, `ProfessionAssignments`, construction/recipe catalogs, item definitions, live order/creature/item/zone/stockpile lists, visible zone/stockpile chunks/shards, tile/geology data, live navigation chunks/caches/path objects, or live workshop placeables through `FortressRuntimeAccess`/former `UiRenderer`/map-click/rendering helpers.
- Extended the UI/debug snapshot facade so order highlight legal-cell dots, debug spawn readiness/count logging, construction placement preview, workshop panel keyboard editing, Work drawer aggregate panels, UI overlay frame data, frame render data, and main map terrain/entity rendering now read Runtime DTOs instead of live terrain, loaded-session `World`, mutable `WorkshopState`, concrete job/order/workforce facades, or App-side tile/entity scans; input controller contexts now carry explicit `UiServices`, `NavigationOverlay`, or map-availability values instead of the full loaded-session snapshot.
- Split Runtime snapshot construction by read-model family so `FortressRuntimeSnapshotBuilder`, `NavigationOverlaySnapshotBuilder`, `MapViewportSnapshotBuilder`, `WorkshopSnapshotBuilder`, `ManagementDrawerSnapshotBuilder`, `StockpileSnapshotBuilder`, `JobsDebugSnapshotBuilder`, and `FortressRuntimeSessionSnapshotFacade` no longer concentrate every snapshot/data-mapping concern in one file; frame/overlay aggregate composition, navigation basic/structural modes, path cells, map terrain/entity glyph policy, workshop/material-progress mapping, management/stockpile/job debug read models, and session-level read queries now have separate Runtime snapshot builder partials.
- Split App/SadConsole presentation code by surface after the Runtime snapshot facade work: overlay-frame coordination, chrome/topbar/dock drawing, management drawer drawing, Debug menu drawing, map overlay glyph drawing, placement preview rendering, quick menus, Work drawer panels, and workshop modal rendering now live in focused App.Rendering/App.UI helpers instead of one growing `UiRenderer`/overlay god class.
- Removed live `World`/`FortressMap` objects from loaded-session frame/input state, fortress load results, and RuntimeAccess bootstrap getters. App frame code now consumes readiness flags plus Runtime map viewport/work drawer/world-availability DTOs, while live world access is limited to the scoped fortress-map fill/bootstrap step, content injection, and optional auto-dig startup.
- Split the App runtime facade boundary again: `GameStateRuntimeCoordinator` owns only Runtime session ports returned by `FortressRuntimeSessionFactory`, while `FortressRuntimeAccess` is the App-owned role adapter for Runtime snapshot facade calls, bootstrap requests, world-fill/bootstrap, command entrypoints, and fortress-play controls. Rendering depends on module-owned `FortressViewRuntimePorts`, input depends on module-owned keyboard/map/placement runtime ports, session loading depends on `FortressSessionRuntimePorts`, and those packages wrap narrow App runtime role interfaces so ordinary renderers/input controllers do not import `App.Runtime` directly. The old aggregate fortress-play runtime access interface has been removed, so startup-only live-world operations do not leak into ordinary UI paths.
- Moved App-only state-machine, session, input, rendering, UI-service, and diagnostics helpers into `HumanFortress.App.GameStates`, `HumanFortress.App.Session`, `HumanFortress.App.Input`, `HumanFortress.App.Rendering`, `HumanFortress.App.UI`, and `HumanFortress.App.Diagnostics`, leaving `HumanFortress.App.Runtime` focused on runtime facade/controller adapter boundaries.
- Continued splitting App UI/input/presentation by surface and event family: Build UI/input, navigation overlay drawing, placement overlay rendering, placement command controllers, input-context factory creation, chrome drawing, EmbarkPrep screen flow, and UI command objects now live in focused App partials/files while still consuming Runtime DTOs and semantic Runtime command facades.
- Split Runtime concrete system composition, dependency grouping, and session snapshot entrypoints by group: planning systems, job systems, catalogs, tunings, workforce setup, frame/overlay snapshots, map/navigation/placement queries, and Work/workshop queries now have separate Runtime files instead of shared mixed entrypoint files.
- Removed App's direct project references to Jobs, Simulation, and Navigation. App placement command creation maps UI intents to semantic Runtime facade methods/request DTOs; Simulation order enum/material DTO conversion is now inside Runtime command code.
- Split Runtime command target dispatch behind internal `SimulationRuntimeCommandTargets` plus narrow role interfaces. Commands now reach order, zone, stockpile, workshop, item/creature spawn, and profession targets through role-specific target contexts, while `SimulationRuntimeContext` keeps explicit clock/execution command roles instead of implementing every target interface directly or exposing an all-target aggregate.
- Added a read-only runtime geology catalog seam and routed map rendering, tile popups, and terrain diff application through the active runtime session geology catalog.
- Moved render snapshot workshop overlay construction lookups behind explicit construction catalog injection.
- Moved construction planner terrain-material resolution behind an App-provided `IConstructionTerrainMaterialResolver`, and moved construction tuning into the runtime content snapshot.
- Removed Jobs-owned construction/mining indirect content reads by injecting construction tuning and air-geology resolution through existing App composition/adapters.
- Moved navigation tuning into the Content-owned runtime snapshot and removed the last `HumanFortress.Navigation -> HumanFortress.Core` project dependency; `NavigationTuning` now parses injected JSON and falls back to defaults without touching content registries.
- Changed runtime session creation so content loads before the shared `NavigationManager` is created, allowing the active session navigation cache, App job path services, overlay, and path-debug tooling to use the same injected `NavigationTuning`.
- Moved placeable tuning into the runtime snapshot and injected `PlaceableTuning` through construction completion, so completed placeables no longer rely on hidden default tuning when content supplies `tuning.placeable`.
- Removed obsolete direct file/registry loading entry points for scheduler/workshop tuning; runtime tuning now flows through the Content snapshot and explicit `LoadFromJson(...)` calls.
- Added mining tuning JSON to the Content-owned runtime snapshot and changed App mining/construction terrain resolver adapters to use injected `IRuntimeGeologyCatalog` and snapshot JSON instead of direct structured-registry reads.
- Added mapgen/ore/cavern tuning JSON to the Content-owned runtime snapshot and introduced `FortressGenerationContent`, so `FortressGenerator`, `FortressMap`, and `FortressChunk` consume injected geology/tuning instead of reading the structured registry directly.
- Cached the active runtime content snapshot in `GameStateManager` during session content loading and reused it for navigation tuning, runtime dependency composition, and fortress generation content.
- Added zone definitions to the runtime content snapshot and moved structured core-data application behind `FortressRuntimeContentSnapshotLoader.ApplyCoreData(...)`, so `SimulationWorldContentLoader` no longer directly calls `ContentRegistry.Instance.ApplyCoreData(...)` or reads `ContentRegistry.Instance.Zones`.
- Moved generated-world settings/tile-view/tile-snapshot contracts into `HumanFortress.Contracts.WorldGen`; App session/screens now consume the contract `IWorldGenerationService` created through Runtime's world-generation factory while `HumanFortress.WorldGen` keeps concrete generation service/data/factory ownership internal.
- Changed runtime content bootstrap to load only the structured runtime registry; the legacy `HumanFortress.Core.Content.ContentRegistry` is no longer part of normal startup.
- Moved the structured runtime registry implementation from Core into `HumanFortress.Content/Registry`, then later changed it to the `HumanFortress.Content.Registry` namespace while keeping Core free of `Newtonsoft.Json`.
- Added a Content-owned strict load path (`FortressContentLoadException`, `ThrowIfInvalid(...)`, `LoadStrict(...)`) and App CLI flags `--strict-content` / `--content-warnings-as-errors` for CI/headless content gates.
- Added an explicit `SimulationCommandStage` for the authoritative pre-read command execution boundary, with regression coverage proving queued commands execute inside the tick pipeline before systems enter `ReadTick` and receive the real runtime tick.
- Converted profession weight changes from direct `GameStateManager -> ProfessionAssignments` mutation into `SetProfessionWeightCommand`, executed through the tick command stage with regression coverage.
- Converted debug `SpawnItemCommand` from direct `world.Items.SpawnItem` mutation into an `ItemsDiffLog.AddItem` command target, so item creation is applied by the post-tick item diff applicator. Added regression coverage for command-stage item spawning.
- Converted debug `SpawnCreatureCommand` from direct `world.Creatures.SpawnCreature` mutation into a minimal spawn-only `CreaturesDiffLog` and post-tick `CreaturesDiffApplicator`, with regression coverage for command-stage creature spawning.
- Converted mining, advanced mining, haul, structural construction, and buildable construction order commands away from direct `context.World as World` access. They now enqueue through the `IOrderCommandTarget` role on `SimulationCommandExecutionContext` and apply through `OrderDiffLog`, with regression coverage for all five command paths.
- Converted zone create/update/delete commands away from direct concrete `World` access. They now route through the `IZoneCommandTarget` role on `SimulationCommandExecutionContext` and apply through `ZoneDiffLog`, preserving shard cleanup behavior and adding regression coverage for create, add cells, remove cells, and delete.
- Converted workshop queue update commands away from direct concrete `World`, registry, and placeable lookup access. They now route through the `IWorkshopQueueCommandTarget` role on `SimulationCommandExecutionContext` and apply through `WorkshopDiffLog`, with regression coverage for recipe add, entry move/remove, queue clear, worker-slot changes, and automation toggles.
- Converted stockpile creation commands away from direct concrete `World` access. They now route through `IStockpileCommandTarget`, with regression coverage for stockpile zone creation, chunk shard assignment, overlap rejection, and command-stage execution.
- Moved Runtime command target aggregation out of App. `SimulationRuntimeContext` now lives in Runtime as the clock/read context, while `SimulationCommandExecutionContext` composes the narrow target roles used by command execution.
- Moved item spawn, creature spawn, order, zone, workshop queue, and stockpile command target helpers into Runtime. Workshop queue commands now validate through an injected `IRecipeCatalog`, while workshop state mutation applies through `WorkshopDiffLog` and `WorkshopDiffApplicator`.
- Added `docs/planning/REFACTOR_BATCH_PROGRESS.md` as the ongoing batch progress log for multi-step refactor work.
- Added Navigation-owned source/snapshot contracts (`INavigationWorldSource`, `NavigationTile`, `NavigationChunkSnapshot`) and removed the direct `HumanFortress.Navigation -> HumanFortress.Simulation` project reference.
- Moved Simulation world/tile/chunk adaptation into Runtime through `SimulationNavigationSource`, so Navigation cache building no longer consumes `Simulation.World`, `Chunk`, `TileBase`, or `TerrainKind` directly.
- Removed Navigation query-time stale-cache rebuilds: `GetNavDataAt` is now read-only, while runtime/session composition and dirty-commit post-tick logic explicitly rebuild navigation data.
- Added `HumanFortress.Contracts` as the first dedicated contracts assembly and wired it into `HumanFortress.sln`.
- Moved navigation DTO/interface contracts into `HumanFortress.Contracts.Navigation`: path requests/results, `Point3`, navigation chunk/source snapshots, movement/capability enums, `IWorldNavigationView`, `INavigationWorldSource`, `IPathService`, and `IMovementExecutor`.
- Moved concrete pathfinding/cache implementation types to `HumanFortress.Navigation.Implementation` as internal implementation types, while Jobs consumes the contracts and Runtime creates the concrete navigation services.
- Moved concrete WorldGen service/data/generator/factory and stage implementation
  types to `HumanFortress.WorldGen.Implementation` as internal implementation
  types, while App consumes `HumanFortress.Contracts.WorldGen` and Runtime
  creates the concrete generation services.
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
- Reduced `TransportJobSystem` to a composition shell, then moved that wrapper source to Runtime; transport diff emission, logging bridge, and profession adapters are now Jobs-owned.
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
- Reduced `MiningJobSystem` to a composition shell, then moved that wrapper source to Runtime; mining diff emission, drop resolution, logging bridge, and profession adapters are now Jobs-owned.
- Fixed stale mining intake/stat snapshots when no planned digs are dequeued.
- Fixed mining backlog carryover-age tracking so it is counted per queued retry item instead of by shared designation id.
- Fixed a mining channel reservation leak where worker-missing and replan-timeout paths released the target tile but not the reserved lower channel footprint; covered with a regression test.
- Moved runtime geology, geology handle assignment, tuning, and zone loading into `HumanFortress.Content.Registry.ContentRegistry`.
- Migrated worldgen, mining/construction terrain resolution, simulation diff application, and fortress map rendering/popups to read runtime geology and tuning through the structured registry.
- Moved construction/workshop and recipe JSON loading behind the Content-owned core-data loader, removing App-local parsing for `data/core/workshops`, legacy `data/core/placeable/workshops.json`, and `data/core/recipes`.
- Kept `ConstructionRegistry` and `RecipeRegistry` as transitional sub-registries under the structured registry boundary, with smoke coverage proving the unified core-data load populates known construction and recipe definitions.
- Added read-only `IConstructionCatalog` and `IRecipeCatalog` interfaces, exposed them from the structured `ContentRegistry`, and migrated runtime/gameplay construction/recipe reads away from direct `ConstructionRegistry.Instance` / `RecipeRegistry.Instance` access.
- Injected `IConstructionCatalog` into Jobs-owned construction and craft execution paths, so Jobs code no longer pulls construction definitions through a construction singleton.
- Replaced recipe-registry mutation in regression tests with in-memory test catalogs; direct construction/recipe singleton access is now contained inside the structured `ContentRegistry`.
- Added read-only `IItemDefinitionCatalog` and `ICreatureDefinitionCatalog` seams over the current Simulation managers, then migrated construction material matching/planning and profession roster naming to consume definition catalogs instead of full manager APIs.
- Extracted static item and creature JSON loading/validation out of `ItemManager` and `CreatureManager` into focused loader classes, while keeping the manager public APIs compatible during the transition.
- Fixed repeated item/creature definition loading so tag/kind indexes are cleared and rebuilt instead of accumulating duplicate entries, with regression coverage for stable reload counts.
- Added immutable item and creature definition catalog snapshot stores inside Simulation. `ItemManager` and `CreatureManager` now swap definition catalog snapshots on reload instead of owning mutable static definition/index dictionaries directly.
- Stabilized the legacy Phase D concurrent pathfinder test by separating production tick-budget behavior from the test's concurrent pathfinding assertion.
- Moved static item/creature definition DTOs, shared placeable DTOs, read-only item/creature definition catalog interfaces, and immutable item/creature catalog snapshot stores into `HumanFortress.Contracts`; a later namespace cleanup moved those definition contracts to `HumanFortress.Contracts.Simulation.Items` and `HumanFortress.Contracts.Simulation.Creatures`.
- Added `HumanFortress.Content` as a real content assembly and wired it into the active solution.
- Moved item/creature definition JSON loading, parsing, validation, normalization, and catalog snapshot creation into `HumanFortress.Content.Definitions`.
- Removed Simulation-owned item/creature definition file loading from `ItemManager` and `CreatureManager`; runtime managers now accept prebuilt catalog snapshots through `SetDefinitionCatalog(...)`.
- Changed App startup and regression tests to load item/creature catalog snapshots through `HumanFortress.Content` and inject them into the active world, keeping `Simulation` independent from `Content`.
- Added immutable construction and recipe catalog snapshots, and changed `ContentRegistry.ApplyCoreData(...)` to swap instance-owned snapshots instead of mutating `ConstructionRegistry.Instance` / `RecipeRegistry.Instance`.
- Changed craft recipe composition to inject `IRecipeCatalog` explicitly into the App craft adapter, removing one more runtime read from global content state.
- Added regression coverage proving repeated core-data loads keep construction/recipe counts and workshop/category queries stable.
- Added a unified `HumanFortress.Content.Definitions.CoreContentCatalogLoader` that returns item, creature, construction, and recipe catalog snapshots in one load result.
- Changed App startup and regression-test support to consume the unified Content load result, while `ContentRegistry.ApplyCoreData(...)` applies construction/recipe snapshots through the structured registry compatibility boundary.
- Added `HumanFortress.Content.Loading.FortressContentLoader` as the Content-owned runtime bootstrap facade for published/source path resolution, legacy/structured runtime registry loading, and optional core catalog loading.
- Added `HumanFortress.Content.Loading.RuntimeContentRegistryLoader` and removed the old Core-owned `ContentLoadCoordinator`, moving legacy/structured registry bootstrap orchestration into the Content assembly.
- Added structured content bootstrap issues with severity/code/message and strict-mode validation helpers on `FortressContentLoadResult`.
- Changed App startup and fortress world content initialization to enter content loading through `FortressContentLoader` instead of owning their own `content/` and `data/core` path discovery.
- Changed App startup and fortress world content initialization to log Content-owned bootstrap issues instead of hand-written path/registry checks.
- Changed App scheduler/workshop tunings to load from the already-loaded structured content registry during runtime composition instead of directly reading tuning JSON files.
- Centralized App registry-file path resolution through `FortressContentLoader.ResolveRegistryFile(...)` for input bindings, order display names, profession definitions, workshop category mapping, and legacy tuning compatibility loaders.
- Verified the current code with `dotnet build` and headless tests on .NET 8.

### Current Known Runtime/Architecture Issues

- `TransportJobSystem`, `MiningJobSystem`, `ConstructionJobSystem`, and `CraftJobSystem` are now Runtime-owned composition wrappers around Jobs-owned executors.
- Concrete job diff emitters, callback loggers, profession adapters, mining drop resolution, construction terrain-material resolution, and craft recipe adaptation now live outside App.
- Remaining App job/runtime responsibilities are logger callback binding, construction UI completion handler binding through the Runtime notification bridge, fortress session flow, and UI/debug access surfaces.
- `CraftPlanner` now lives in `HumanFortress.Jobs.Craft`. Jobs-owned craft code no longer directly reads `RecipeRegistry.Instance`; App bridges existing recipe content through `ICraftRecipeCatalog`.
- Navigation no longer has a project dependency on Simulation or Core, and navigation DTO/interface contracts now compile from `HumanFortress.Contracts.Navigation`.
- `HumanFortress.Jobs` now owns transport, mining, construction, and craft executor cores plus their state/helper/stats/debug slices. It also owns scheduler/workshop tuning types, worker-selection strategy, profession assignment/selection state, and the low-frequency `SanitizeSystem`; job executors consume navigation through contracts rather than referencing the concrete Navigation project.
- `HumanFortress.Jobs` implementation/orchestration/tuning/debug types are now internal. Runtime and tests retain friend access; App no longer has a Jobs internals bridge or direct Jobs project reference.
- `HumanFortress.Runtime` now owns the public session factory/ports plus focused internal runtime implementation modules. Runtime composition/system/dependency helpers live in `HumanFortress.Runtime.Composition`, host/tick-pipeline helpers in `HumanFortress.Runtime.Host`, session handles/services in `HumanFortress.Runtime.Session`, mutation log bundles in `HumanFortress.Runtime.Diff`, active-session content bootstrap and stockpile preset mapping in `HumanFortress.Runtime.Content`, geometry adapters in `HumanFortress.Runtime.Geometry`, fortress-generation runner glue in `HumanFortress.Runtime.WorldGeneration`, Simulation-backed navigation adapters in `HumanFortress.Runtime.Navigation`, startup/autodig helpers in `HumanFortress.Runtime.Startup`, and command execution/target helpers in `HumanFortress.Runtime.Commands`. Public Runtime session ports use Contracts runtime geometry/notification DTOs, with SadRogue geometry kept behind App.Runtime/Runtime-internal mappers. `GameStateRuntimeCoordinator` holds Runtime session ports rather than the concrete internal core; App still owns content/logging callback invocation, logger callback binding, construction UI completion handler binding, fortress session flow, and UI/SadConsole lifetime. The catalog/tuning/geology/generation dependency group now consumes a Content-owned runtime snapshot rather than directly reading the structured registry from App composition, Runtime command targets no longer fall back to global construction/recipe registry reads, and navigation/placeable tuning is exposed through active-session runtime dependencies.
- Command execution now has an explicit pre-read runtime stage. Profession weight changes emit `ProfessionAssignmentDiffLog` entries, debug item spawning emits `ItemsDiffLog` additions, debug creature spawning emits a minimal spawn-only `CreaturesDiffLog` operation, order commands emit `OrderDiffLog` entries, workshop queue/settings commands emit `WorkshopDiffLog` entries, zone create/update/delete commands emit `ZoneDiffLog` entries, stockpile creation/deletion emit `StockpileDiffLog` entries, and those typed diffs are applied post-tick. Runtime command targets and post-tick applicators share those logs through `RuntimeMutationDiffLogs` instead of a long per-log constructor chain. Runtime command implementations route through the runtime command target context instead of direct concrete World casts or App runtime internals. The command source files now compile from `HumanFortress.Runtime/Commands` under the `HumanFortress.Runtime.Commands` namespace, with command-stage/context helpers in `Commands/Execution`, target role interfaces/implementations in `Commands/Targets`, and replay decoding split by command family in focused `RuntimeCommandReplayFactory.*.cs` partials. Remaining command-boundary work is replacing any remaining direct manager mutations with authoritative typed diffs where each subsystem has an applicator path.
- `StockpileDiffLog` is now wired into the tick pipeline for create-zone and delete-zone commands. Stockpile preset JSON is loaded by Content into contract definitions, Runtime maps those presets to Simulation filters, create-zone diffs carry filter/priority data, and the post-tick applicator applies those rules authoritatively. Stockpile item filtering now uses a Simulation item-projection seam for definition id/tags/materials, the active hauling destination path respects zone filters and shard capacity including same-tick planned reservations, and transport/construction/craft paths queue stockpile item-index and reservation diffs through Jobs-owned emitters/planners. The legacy stockpile-local hauling broker and `CreateHaulJob` diff have been removed; remaining stockpile work is richer long-horizon reservation policy and maintenance behavior rather than App or stockpile-local job mutation.
- `ContentRegistry` has been collapsed to the structured `HumanFortress.Content.Registry.ContentRegistry` runtime path. The old `HumanFortress.Core.Content.ContentRegistry` source has been deleted. Runtime geology/zone DTOs now compile from `HumanFortress.Contracts.Content`; construction/recipe definitions/catalog interfaces/catalog stores, `IRuntimeMaterialCatalog`, `IRuntimeTerrainKindCatalog`, `IRuntimeGeologyCatalog`, `ConstructionTuning`, and `PlaceableTuning` now compile from `HumanFortress.Contracts.Content.Registry`. The structured registry still owns runtime geology loading, deterministic geology handles, tuning files, zone definitions, material/terrain registry implementation, and construction/recipe snapshot application, while concrete registry helpers stay internal. Its implementation is now split into focused Content partials for load orchestration/query/snapshot compatibility, material/terrain parsing, biome/geology parsing plus deterministic geology indexing, and tuning/zones/alias/validation/hash behavior. Mining/construction App adapters, rendering, navigation/placeable tuning, and WorldGen now consume injected active-session snapshot dependencies instead of reading the structured registry directly.
- `ConstructionRegistry` and `RecipeRegistry` singleton compatibility classes have been deleted. `ContentRegistry` owns immutable construction/recipe catalog snapshots and exposes them through `IConstructionCatalog` / `IRecipeCatalog`.
- Creature and item definition loading/validation now lives in `HumanFortress.Content.Definitions`. `ItemManager` and `CreatureManager` no longer parse files; they consume immutable catalog snapshots supplied by App/runtime composition.
- Static item/creature/construction/recipe/material/terrain/geology/biome definition DTOs, terrain bit-layout DTOs, alias/migration DTOs, shared placeable/geology/zone DTOs, fixed-point material primitives, tuning contract types, content version/snapshot/validation result types, runtime geology catalog interface, definition catalog interfaces, and immutable catalog snapshot stores now compile from `HumanFortress.Contracts`; content registry contracts now use `HumanFortress.Contracts.Content.Registry`.
- Content loading now has a first unified `HumanFortress.Content` boundary for item, creature, construction, recipe snapshots, structured runtime registry bootstrap and implementation, strict bootstrap diagnostics, App registry-file resolution, runtime catalog/tuning/geology/zone snapshot capture, Content-owned core-data loading, internal/friend profession registry loading, and structured core-data application. Runtime construction/mining/navigation/placeable/scheduler/workshop tuning and WorldGen mapgen/ore/cavern tuning now enter through internal/friend Content snapshots, while public `FortressContentLoadResult` exposes issues and summary counts instead of full catalog objects. Remaining content work is richer debug surfaces, compatibility naming cleanup, and the future compiled content-pack pipeline.
- Simulation-owned world save payload build/restore is now split by authoritative section. `WorldSavePayloadBuilder` keeps only canonical section assembly in its main file, while metadata/terrain, entities, stockpiles, placeables, orders, and shared conversion helpers live in focused partials. `WorldSavePayloadRestorer` keeps restore flow, terrain reconstruction, supported-section restore order, and final replay-hash verification in its main file, while payload validation, placeable validation/restore, and conversion/failure helpers live in focused partials. Runtime consumes this through the save/restore session ports; App does not build or decode world payload sections.
- Simulation authoritative managers are being reduced from single-file service objects into focused state owners. `ItemManager` now separates catalog access, position indexing, read queries, stack/move/remove mutations, spawn behavior, and save/restore validation/conversion into partial files while keeping state and deterministic item GUID allocation in the main file. `CreatureManager` now separates catalog access, runtime instance queries, spawn behavior, and save/restore validation/conversion into partial files while keeping state, world binding, and deterministic creature GUID allocation in the main file. `OrdersManager` now separates haul, mining, construction/buildable, and save/restore behavior into partial files while keeping queue state and logging in the main file. Placeable authority is split inside `HumanFortress.Simulation.Placeables`: `PlaceableInstance` keeps runtime state separate from item/install and construction factory behavior, small placeable component types live in their own files, `ChunkPlaceableData` keeps authoritative storage separate from derived furniture sync, and `PlaceableManager` separates collision, cross-chunk placement, removal, and affected-chunk queries. These remain Simulation-owned authority; Runtime/App consume them through existing world/session seams rather than owning item/creature/order/placeable state.
- Startup content diagnostics are improved and current legacy content loads without registry errors. The structured registry now loads successfully and reports remaining validation warnings explicitly.
- Logging now has a first-pass structured async diagnostics pipeline with category/level events, a main timeline log, category log files, and a ring-buffer sink. The Debug Status tab can show first-pass diagnostic counts and latest Content issue from a `DiagnosticSnapshot`. The secondary content registry, WorldGen progress/error paths, stockpile diff errors, and key Simulation orders/jobs fallbacks now route through diagnostic helpers. Remaining work is mostly command-line/test output, no-logger fallback helpers, and a dedicated diagnostics/debug UI.
- The world map embarkability UI now shows first-pass rule failures for the current tile. It still needs richer terrain/biome/geology diagnostics if embark rules become more complex.
- A formal regression-test project now exists and the former App `TestRunner` plus `PhaseTests` coverage has moved into `tests/HumanFortress.App.Tests`; App `--test` and `--validate` are compatibility pointers to `./RunTests.sh`.
- Historical analyzer warnings remain; they are not currently blocking runtime but should be cleaned during build-hygiene work.

### Updated Near-Term Order

1. Tighten Content hygiene: improve debug visibility and plan final namespace/compiled-pack cleanup now that structured registry implementation and strict fail-fast loading are Content-owned.
2. Finish App boundary cleanup around runtime composition: replace compatibility namespaces, narrow App-provided delegates, keep session/input/rendering/UI helpers in their App submodules, and move any remaining non-UI startup/content glue behind Runtime or Content seams.
3. Normalize compatibility namespaces and remove transitional `InternalsVisibleTo` bridges where modules now have correct source ownership.
4. Normalize diff priority semantics with explicit documentation and regression tests.
5. Continue UI/debug snapshot facades and bootstrap cleanup so remaining session/render-snapshot/bootstrap glue stops exposing live `World`/navigation internals to UI code. Runtime-owned jobs/workforce/orders/workshop/build/debug/tile-inspection/management-drawer/zone-overlay/detail/stockpile/navigation-overlay/navigation-path/placement-preview/debug-spawn/map-viewport/work-drawer/frame-render/overlay-frame/session-snapshot facades are in place, and their first large builders have been split by read-model family.
6. Centralize path/movement ownership into runtime-wide navigation/movement services after Jobs/Runtime boundaries are stable.
7. Split the monolithic test executable into focused project-level test assemblies when module boundaries are stable enough.

## Current Critical Findings

### P0: Runtime Ownership Is in the Wrong Place

`GameStateManager` no longer directly creates the active `World`, navigation manager, runtime host graph, runtime startup sequence, tick scheduler, command queue, event bus, diff logs, active runtime session handle, or concrete runtime session controller. World/session reset and navigation creation sit behind Runtime's `SimulationRuntimeSessionFactory<SimulationRuntimeHost<SimulationRuntimeSystems>>`; the internal `FortressRuntimeSessionCore` holds the session/runtime-control handles behind `IFortressRuntimeSessionPorts` returned by `FortressRuntimeSessionFactory`; App-specific logging/content issue/UI notification callbacks enter at `GameStateRuntimeCoordinator`; and `FortressRuntimeAccess` exposes only App role interfaces to state/render/input helpers.

It still owns or coordinates app state transitions, the seed manager, and fortress-play enter/exit lifecycle calls; runtime-facing UI/session controls are exposed through narrow App.Runtime access interfaces.

That is better than the original god-object shape. The remaining cleanup is to keep `GameStateRuntimeCoordinator` thin and eventually move any non-UI startup/content glue behind Runtime or Content seams so `GameStateManager` remains pure app-state transitions.

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

- `DiffLog` now uses a stable FNV-derived system hash instead of `SystemId.GetHashCode()`, but deterministic replay coverage is still thin and the long-term contract should prefer explicit numeric system order over hash-derived ordering;
- many authoritative IDs use `Guid.NewGuid()`;
- command IDs are still random GUIDs, though `CommandQueue` now orders by target tick plus enqueue sequence rather than `CommandId`; the long-term fix is an explicit replay/command sequence id assigned by the queue or replay loader;
- App world-generation UI randomization no longer uses `Environment.TickCount` or direct `new Random()` calls. Explicit user-randomized seeds now come from `WorldGenerationSettingsDefaults`, while Core defaults use a fixed seed and world generation remains deterministic after the seed is chosen;
- `RngStreamManager` is constructed but not used by most systems;
- `SimulationRuntimeContext.CurrentTick` is now set by the runtime command stage, but command timestamp propagation still needs broader replay coverage;
- diff applicators cover only part of the authoritative state;
- `EventBus` infrastructure exists but is effectively unused.

Fix:

1. Lock deterministic diff ordering behind tests, and eventually replace hash-derived system ordering with explicit numeric system order.
2. Replace authoritative GUIDs with deterministic `EntityId`, `ItemId`, `PlaceableId`, `CommandSeq`.
3. Use named RNG streams for all sim branches.
4. Move commands into the tick pipeline.
5. Add a headless deterministic replay test.

### P0: Job System Composition Still Leaks Through App

Mining, hauling, construction, and crafting executor cores now live in `HumanFortress.Jobs`, along with scheduler/workshop tunings, worker-selection strategy, profession assignment state, concrete job diff emitters, profession/craft adapters, callback job loggers, mining drop/tuning resolution, construction terrain-material resolution, the sanitizer safety net, and `UnifiedJobsOrchestrator`.

The remaining App leak is no longer concrete system construction. Runtime owns the host factory, runtime system groups, tick-facing transport/mining/construction/craft wrappers under `HumanFortress.Runtime.Jobs`, optional auto-dig command seeding, and the construction completion notification bridge. App still passes logger callbacks, content snapshots, the auto-dig setting, and UI notification handlers. Profession registry file loading lives under `HumanFortress.Content`.

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

### P1: Content Registry Unification Is Mostly Done

The old split between:

- `HumanFortress.Core.Content.ContentRegistry`
- `HumanFortress.Content.Registry.ContentRegistry`

has been collapsed. The old legacy source has been deleted, normal bootstrap loads the structured registry only, and the structured registry implementation now lives under `HumanFortress.Content.Registry`.

Remaining fix:

1. Keep public content loading behind `FortressContentLoader`; Runtime/tests may use internal/friend snapshot loaders, but App/UI should not depend on those loader internals.
2. [Done first pass] Add strict fail-fast mode for CI/release bootstraps.
3. Expose better content diagnostics in logs/debug UI.
4. Plan the future compiled content-pack pipeline after diagnostics/debug surfaces are stable.
5. Keep runtime systems on immutable snapshots and injected catalog interfaces.

### P1: Rendering Snapshot / Presenter Boundary Still Needs Hardening

Active fortress rendering now consumes Runtime-built snapshot DTO contracts for the main map, overlays, drawers, debug pages, workshop UI, and placement previews instead of live `World` reads or the legacy App `RenderSnapshotBuilder` bridge. The older `HumanFortress.Simulation.Rendering.RenderSnapshot` implementation has been removed; PhaseTests now smoke the Runtime frame snapshot port instead. App UI state keeps its own construction shape and stockpile menu options, diagnostics are passed through an App diagnostics provider instead of the runtime facade, and App maps UI construction shape to Simulation only at the command/preview boundary. The remaining work is turning the Runtime/Contracts snapshot DTO family into a clearer versioned presenter/diff boundary.

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
- `WorldCellTargetEncoding` now centralizes Simulation world-cell target encoding for job diff emitters that still need `ChunkKey + localIndex`.
- `ItemsDiffLog` now accepts `WorldCellTarget` directly for add/remove/split operations.
- Mining diff emission is isolated behind `MiningDiffEmitter`.
- Mining drop/tuning lookup and caching is isolated behind `MiningDropResolver`; its source ownership has moved to `HumanFortress.Jobs/Mining` and it no longer depends on Newtonsoft JSON APIs.
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

Status: executor-core move is complete for the current transport/mining/construction/craft slice. Runtime owns the tick-facing job wrappers, Jobs owns executor cores and adapters, Content owns profession registry loading, and App retains concrete session/system composition plus UI/debug surfaces.

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
- Transport, mining, construction, and craft executor cores now live in `HumanFortress.Jobs`; craft planning, diff emitters, callback loggers, profession/craft adapters, scheduler/workshop tuning types, and profession assignment state also live outside App. Runtime owns the tick-facing job wrappers. App still owns concrete session/runtime composition and UI/debug surfaces.

### Phase 6: Split Content

1. [Done] Pick the modern structured registry model and retire the old legacy registry source.
2. [Done] Move structured registry implementation to `HumanFortress.Content`.
3. [Mostly done] Load through the Content facade and active runtime snapshot.
4. [Mostly done] Inject immutable registry snapshots/catalog interfaces into systems.
5. [Ongoing] Remove remaining global singleton reliance where practical and improve strict diagnostics/debug UI.

### Phase 7: Snapshot-Driven UI and Rendering

1. Runtime publishes render/presentation snapshots after commit.
2. [First pass] UI panels read Runtime-owned snapshots, not `World`, for Work drawer jobs/workforce/orders/workshops, F1/F2/F4 management drawer lists, zone/stockpile overlay/detail popups, stockpile/zone hit-testing, workshop panel/overlay/click paths, build workshop browsing/preview, Debug menu status/items, and tile inspection popups. The larger snapshot builders and App presentation/input routers are being split by read-model family, event channel, and UI surface so the facade layer does not become a new god object.
3. Rendering owns palette/glyph visual mapping.
4. Debug tools consume explicit debug snapshots.

### Phase 8: Save/Load and Determinism CI

1. Add headless runner.
2. Add canonical snapshot hash.
3. Add fixed seed scenario.
4. [First pass] Add save/load round-trip: Runtime save documents now carry a
   Simulation-owned world payload for terrain, ground items, creatures, global
   reservations, stockpile zones, owned placeables/workshop state, and active
   order designations, plus primitive RNG stream rows and pending/executed
   command replay document rows. Runtime can restore the supported world
   sections into a freshly composed session, then restore RNG streams and
   pending commands through a Runtime-owned full restore entrypoint. Full load
   still needs carried/contained/equipped/installed item state, item-local
   reservation tokens, and long-horizon job-state payload restore before this
   milestone is complete.
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
- Save/load can round-trip a minimal fortress state. Current status: terrain
  chunks, ground items, creatures, global reservations, stockpile zones, and
  active order plus owned placeable/workshop payloads round-trip by hash, and
  Runtime full restore now also restores RNG streams plus pending command replay
  rows. Unsupported item location modes and job-state payloads still fail
  closed until their restore slices exist.

## Practical First Pull Requests

Recommended PR order:

1. [Done] Clean solution and obsolete play-state code.
2. [In progress] Add `FortressRuntime` shell and move world/tick ownership.
3. [Done first pass] Move command execution into tick.
4. [Done first pass] Replace direct mining order enqueue with command.
5. [Done first pass] Fix command execution `CurrentTick`.
6. [Partially done] Replace unstable diff/target ordering hazards; continue determinism audit.
7. [Mostly done] Add headless regression checks; former App `TestRunner` and `PhaseTests` coverage has moved into `tests/HumanFortress.App.Tests`.
8. [Mostly done] Invert Navigation dependency; direct Navigation -> Simulation reference, query-time rebuilds, old navigation contract namespace, and Jobs -> Navigation implementation references have been removed.
9. [Mostly done] Move job executor cores out of App; remaining work is planner/runtime/content seams and cleanup of transitional App adapters.
10. [Mostly done] Split Content; remaining work is diagnostics/debug UI, compatibility naming, and compiled-pack planning.

Immediate next PR-sized work:

1. Continue structured logging/debug diagnostics cleanup and expose content diagnostics in a UI/debug surface.
2. Carve a real Runtime composition boundary out of `GameStateManager` and App runtime helpers.
3. Split the current monolithic App test project into focused module-level test projects when dependencies make that practical.
