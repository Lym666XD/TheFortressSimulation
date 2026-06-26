# Refactor Batch Progress

This document tracks the current multi-step refactor batches so progress is visible without relying on chat history.

## Current Status Snapshot - 2026-06-26

- Latest App input/UI, Runtime snapshot/command-target, and Runtime host split batch is build verified: `/opt/homebrew/opt/dotnet@8/bin/dotnet build HumanFortress.sln --no-restore -m:1 -v:minimal -p:RunAnalyzers=false -p:UseAppHost=false` passed with `0 Warning(s), 0 Error(s)`.
- Latest source-only/no-build Runtime port hardening replaced SadRogue `Point`/`Rectangle` usage on public Runtime session ports with `HumanFortress.Contracts.Runtime` primitives (`RuntimePoint`, `RuntimeRect`) and a `RuntimeWorkshopCompletionNotification` DTO. App keeps SadRogue geometry inside App runtime/UI access interfaces and maps at `FortressRuntimeAccess`; Runtime core maps contract geometry back to its current internal SadRogue implementation details. The App bootstrap completion hook was also narrowed from a six-argument primitive delegate to an App-owned `FortressWorkshopCompletionNotification` DTO.
- Latest source-only/no-build architecture hardening removed the App-local `FortressRuntimeSessionController` passthrough. `GameStateRuntimeCoordinator` now creates the active Runtime session through `FortressRuntimeSessionFactory` and holds only `IFortressRuntimeSessionPorts`; `FortressRuntimeAccess` is an App-owned role adapter over Runtime session ports rather than concrete core methods. The App-local `FortressSimulationStatus` DTO/mapper was deleted; UI chrome now consumes `HumanFortress.Contracts.Runtime.SimulationStatus`.
- The same no-build hardening made `FortressRuntimeSessionCore` an internal Runtime implementation and moved its App-facing capabilities behind explicit Runtime session port interfaces. Runtime core command/snapshot/lifecycle methods are explicit interface implementations; ordinary public Runtime session construction is the factory plus ports.
- Simulation implementation types are now internal/friend by default. Runtime, Jobs, WorldGen, and tests retain friend access, while the stable external surface remains Contracts DTOs/interfaces plus Runtime snapshot/command ports; App still has no direct Simulation project reference.
- The same no-build batch narrowed Content's public API: `FortressRuntimeContentSnapshot`, `FortressRuntimeContentSnapshotLoader`, `CoreContentCatalogLoader`, core catalog load results, item/creature catalog load results, `RuntimeContentRegistryLoadResult`, and `ProfessionRegistryLoader` are now internal/friend surfaces for Content, Runtime, and tests. Public `FortressContentLoadResult` exposes summary counts and issues instead of the full core catalog snapshot.
- WorldGen public surface was tightened again: concrete world-generation service/data and fortress-map generation internals are internal, the App-facing generated-world read path is the contract `IGeneratedWorldData`, App creates world generation through `WorldGenerationServiceFactory`/`IWorldGenerationService`, and default world-generation seed/settings policy lives in App rather than Contracts.
- Runtime command execution no longer has the transitional `IRuntimeCommandTargetContext` aggregate. Host core, tick pipeline, and command stage pass only `ISimulationContext` plus the clock role, while individual commands require their precise role context through `RuntimeCommandContext.Require<T>()` and fail visibly instead of silently no-oping when a role is missing.
- Latest source-only/no-build command-context hardening split target-role aggregation out of `SimulationRuntimeContext`. `SimulationRuntimeContext` now owns only the simulation clock/read context, while the new internal `SimulationCommandExecutionContext` composes `ISimulationContext` with narrow command target roles for the command stage. `SimulationCommandStage`, `SimulationTickPipeline`, and host core now require `IRuntimeCommandExecutionContext` so the command pipeline cannot be constructed with a plain simulation read context that lacks target roles.
- Latest source-only/no-build boundary hardening made the concrete structured `ContentRegistry` an internal Content implementation detail. External tests/App/Runtime no longer reference `ContentRegistry.Instance`; bootstrap/debug reads use `FortressRuntimeContentSnapshotLoader` and contract catalog interfaces, while pure construction/craft tests use explicit in-memory/empty catalogs where appropriate.
- The same no-build batch moved generated-world stable DTOs/settings/service contracts (`WorldGenerationSettings`, `WorldGenerationDifficulty`, `WorldMapTileView`, `WorldTileSnapshot`, `IWorldGenerationService`) into `HumanFortress.Contracts.WorldGen`, kept concrete `WorldGenerationService`/`GeneratedWorldData` internal in `HumanFortress.WorldGen`, and added an App-owned world-generation port/adapter so App screens/session flow no longer depend directly on concrete WorldGen service/data types outside `App.WorldGeneration`.
- Runtime command execution was narrowed again: `IRuntimeCommandClockContext` no longer inherits `ISimulationContext`, and the former all-target-sounding command binding was renamed to profession-specific `IRuntimeProfessionCommandBindings`.
- Latest source-only/no-build module-surface hardening made `HumanFortress.Jobs` and concrete `HumanFortress.Navigation` implementation sources expose no ordinary `public` implementation members: Jobs callback/profession adapters, diff emitters, mining assignment/drop resolution, craft planning, sanitizer, executor helpers, buffers, trackers, and Navigation view/path/movement/cache/heap internals now use internal or explicit-interface surfaces. Cross-module access remains through Contracts interfaces plus `InternalsVisibleTo` for Runtime/tests.
- The same no-build hardening moved `SimulationStatus` to `HumanFortress.Contracts.Runtime`, moved runtime geology/zone DTOs to `HumanFortress.Contracts.Content.Registry`, and split Runtime command execution context into clock and target ports so the tick pipeline does not require command-target access while commands cast only to the target context.
- Follow-up no-build hardening narrowed additional App/Content implementation surfaces: Content registry helpers (`MaterialRegistry`, `TerrainKindRegistry`, `GeologyRegistry`, `BiomeTemplateRegistry`, `AliasResolver`, and registry diagnostics) now expose internal/explicit-interface implementation members; `GameStateRuntimeCoordinator` no longer constructs Runtime options directly; the old App-local `FortressRuntimeSessionController` passthrough has since been removed; `FortressRuntimeAccess`, GameStateManager/state wrappers, navigator, lifecycle, registry, and screen presenter are App-internal surfaces with explicit role-interface entrypoints where needed.
- Latest source-only/no-build App/Runtime boundary hardening made App implementation modules internal by default: App screen states, session context/load/bootstrap helpers, input services, NavigationOverlay, UI types/commands/selection helpers, Logger, and diagnostic snapshots no longer expose public application implementation APIs. `Program` remains the App entry point.
- The same no-build batch moved public Runtime snapshot DTOs from `HumanFortress.Runtime.Snapshots` to `HumanFortress.Contracts.Runtime.Snapshots`. Runtime keeps the snapshot builders/facades under `HumanFortress.Runtime.Snapshots`, App now consumes snapshot DTO contracts, and snapshot DTOs now use project-owned `SnapshotColor` / `SnapshotPoint` primitives so Contracts does not reference `TheSadRogue.Primitives`.
- Latest source-only/no-build boundary cleanup moved static item/creature definition contracts to `HumanFortress.Contracts.Simulation.Items` and `HumanFortress.Contracts.Simulation.Creatures`, replacing the old compatibility `HumanFortress.Simulation.*` namespaces for those contract DTOs/catalog interfaces. Simulation managers still own runtime item/creature instances and consume the contract catalog snapshots.
- Runtime command dispatch no longer uses the old `IRuntimeCommandTargets` aggregate. `SimulationRuntimeCommandTargets` remains an internal implementation holder, `SimulationRuntimeContext` exposes explicit clock/execution roles, and individual Runtime commands cast only to narrow target contexts such as `IRuntimeOrderCommandTargetContext`, `IRuntimeZoneCommandTargetContext`, `IRuntimeWorkshopCommandTargetContext`, or spawn/profession/stockpile equivalents.
- Runtime command execution no longer uses the transitional `IRuntimeCommandContext` or `IRuntimeCommandTargetContext` aggregate interfaces. `SimulationRuntimeHostCore`, `SimulationTickPipeline`, and `SimulationCommandStage` receive an explicit clock role plus `ISimulationContext`, while `SimulationRuntimeContext` implements narrow command-target roles directly.
- Latest source-only/no-build cleanup further narrowed App runtime role injection and split focused files without changing module ownership: UI enum files, main-menu rendering panels, App input/view context factories, session world-map queries, Orders submenu rendering data, GameStateManager transition/shutdown files, Runtime placement-command mapping/material policy, world-content loader logging, async diagnostic lifecycle/worker code, map-viewport terrain glyph policy, legacy log category prefixes, runtime job-system groups, auto-dig command seeding, zone overlay detail/hit-test builders, tile-inspection builders, workshop snapshot material matching rules, tick-pipeline post-tick steps, and Runtime session core lifecycle/auto-dig/world-fill/workshop-notification files.
- Latest source-only/no-build Runtime bootstrap boundary cleanup moved fortress-map generation/fill out of App.Runtime and into Runtime's session implementation: `HumanFortress.Runtime` now references `HumanFortress.WorldGen`, Runtime/Simulation/WorldGen align on `TheSadRogue.Primitives` 1.6.0, App passes `RuntimeFortressGenerationRequest`/receives `RuntimeFortressGenerationResult`, the old App-facing `GenerateFortressMap(...)` + `FillRuntimeWorld(...)` facade was removed, the Runtime content snapshot getter and public world-fill method were closed, and Program's low-level Runtime log binding now goes through an App.Runtime bridge.
- The same no-build cleanup tightened generated-world presentation DTOs: `WorldMapTileView` and `EmbarkSiteSummary` no longer expose `BiomeType` directly, World-map biome glyph/color mapping moved to `HumanFortress.App.Rendering.WorldMapTileDisplayMapper`, and WorldGen screen state no longer stores the last `WorldGenResult` after generation.
- Earlier source-only/no-build module-boundary cleanup moved generated-world UI-facing service/data types out of App.Session into `HumanFortress.WorldGen`; the latest follow-up moved the stable generated-world DTO/settings contracts into `HumanFortress.Contracts.WorldGen` and hid concrete generated-world data behind an App-owned adapter. App.Session now stores/query-wraps the generated-world port and maps a contract `WorldTileSnapshot` into `RuntimeFortressGenerationRequest`; active App source no longer references raw `WorldTile`, `BiomeType`, `WorldParams`, `WorldGenerator`, `DifficultyPreset`, or `WorldGenResult`.
- The same no-build cleanup reduced Runtime public surface: Runtime-only composition helpers (`FortressRuntimeStartup`, host/system factories, dependency/catalog/tuning/workforce/planning/job-system groups, navigation factories), Runtime snapshot builder/facade helpers, and Runtime command factory helpers are now internal. Public DTOs and session semantic command entrypoints remain the external boundary.
- Latest source-only/no-build Navigation boundary cleanup moved navigation DTO/interface contracts into `HumanFortress.Contracts.Navigation`, while concrete pathfinding/cache implementations remain in `HumanFortress.Navigation` as internal implementation types. Jobs now consumes movement through `IMovementExecutor`, Runtime job-system wrappers create concrete `MovementExecutor` instances, and `HumanFortress.Jobs` no longer references the `HumanFortress.Navigation` project.
- Latest source-only/no-build API hardening made concrete Navigation implementation types, Jobs implementation/orchestration/tuning/debug types, Runtime concrete command classes/factories, Runtime command-target interfaces/aggregation, and additional Runtime composition/logging helper members internal. `HumanFortress.Jobs` no longer exposes internals to App, and active App source still has no direct Jobs/Simulation/Navigation/Runtime.Commands references.
- Latest source-only/no-build boundary hardening continued without running a build: Content single-purpose loaders/parsers and concrete registries are now internal implementation details where possible, `ContentRegistry.Materials`/`TerrainKinds` expose Contracts read-only catalog interfaces, profession JSON loading is an internal Content facade returning `IProfessionRegistry`, Runtime session core options are internal construction helpers, `FortressState` composes App runtime access through named runtime ports, keyboard input no longer receives one broad runtime interface for workshop/navigation/simulation/build-catalog operations, and App UI consumes the Contracts `SimulationStatus` DTO directly.
- Latest source-only/no-build Runtime API hardening made internal command targets, concrete Runtime commands, job-system wrappers, the Simulation-to-Navigation adapter, Runtime helper loaders, auto-dig seeding, command factories, and snapshot builder/facade methods use internal or explicit-interface surfaces. The same pass normalized low-risk Jobs configuration/profession/orchestrator helper surfaces. Public Runtime shape is now concentrated on `FortressRuntimeSessionFactory`, Runtime session port interfaces, Runtime request/status types, and logging bootstrap rather than concrete implementation helpers; public snapshot DTOs live in Contracts.
- Previous architecture-hardening build verified the pre-hardening batch after `SimulationWorldContentLoader`, `RuntimeAutoDigSeeder`, Runtime logging/workshop-completion helper types, and concrete command target implementations were internalized. Fast solution build passed with `0 Warning(s), 0 Error(s)` before the latest source-only API hardening.
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
- Runtime host factory, generic startup orchestration, and optional startup auto-dig command seeding now live in `HumanFortress.Runtime`; App injects logging and the auto-dig setting.
- Active fortress world content application now lives in Runtime's `SimulationWorldContentLoader`; App injects logging/content-issue callbacks instead of owning the loader.
- Runtime command source files now live in `HumanFortress.Runtime/Commands` under the `HumanFortress.Runtime.Commands` namespace.
- Runtime command request entrypoints now live behind Runtime session ports implemented by the internal `FortressRuntimeSessionCore`; App-facing input/access paths issue semantic queue methods such as `QueueHaulOrder(...)`, `QueueCreatureSpawn(...)`, and `QueueAddWorkshopRecipe(...)` instead of passing `Func<ulong, ICommand>` or constructing Runtime command factories.
- App active source no longer references `HumanFortress.Core.Commands` or `HumanFortress.Runtime.Commands`; concrete command factory usage is contained inside Runtime.
- Placement request enums/DTOs now live under `HumanFortress.Contracts.Runtime`; App passes placement shape/material preferences/tags as semantic request parameters, while material-filter defaults/category mapping and Simulation order enum/material DTO conversion remain inside Runtime command code.
- Runtime composition types now use the `HumanFortress.Runtime` namespace, and tick-facing job wrappers now use `HumanFortress.Runtime.Jobs`.
- App still owns the SadConsole/platform host, logger callback binding, UI bootstrap, construction completion UI notification binding through the Runtime notification bridge, and session/bootstrap glue.
- App no longer references concrete Runtime job-system wrappers for construction completion binding; `FortressRuntimeWorkshopCompletionNotifier` is injected through Runtime composition and App sets only the UI handler through `IFortressRuntimeBootstrapAccess`.
- Optional auto-dig mining command construction now lives in Runtime's `RuntimeAutoDigSeeder`; App no longer owns the startup/after-fill mining command implementation.
- Work/jobs/profession UI paths now consume Runtime-built jobs/workforce snapshot DTOs from `HumanFortress.Contracts.Runtime.Snapshots` instead of reading concrete runtime job systems or `ProfessionAssignments` through `FortressRuntimeAccess`.
- Workshop drawer list/queue/status paths, detailed workshop panel rendering, workshop overlay/material-progress rendering, and workshop map-click hit-testing now consume Runtime-owned workshop snapshots instead of scanning live placeables and construction definitions inside the former `UiRenderer` paths or `FortressMapClickController`.
- Work drawer labor/order summary paths now consume Runtime-owned workforce/order snapshots instead of reading live creatures or order designations inside the former `UiRenderer` paths.
- Build quick-menu workshop item browsing and buildable placement preview now consume a Runtime-owned build catalog snapshot DTO instead of passing construction catalog contracts into UI/keyboard rendering paths.
- Debug menu status/items and tile inspection popups now consume Runtime-owned debug/tile inspection snapshots instead of reading item definitions, world counts, tile data, geology, ground items, or creature definitions inside UI render/input helpers.
- F1/F2/F4 management drawer lists now consume a Runtime-owned management drawer snapshot instead of scanning live creature, item, zone, or stockpile managers inside the former `UiRenderer` paths.
- Zone menu overlay/detail popup rendering and zone click hit-testing now consume Runtime-owned zone snapshots/queries instead of scanning visible chunks, zone shards, zone instances, zone definitions, or zone cells inside App UI helpers.
- Stockpile overlay rendering, stockpile click hit-testing, and stockpile edit-popup rendering now consume Runtime-owned stockpile snapshots/queries instead of holding a `StockpileManager` in `StockpileUI` or scanning stockpile chunk shards inside App UI helpers.
- Navigation debug overlay drawing modes and F10 path-debug queries now consume Runtime-owned navigation DTOs built from `NavigationManager.Source` and cached nav data instead of letting App overlay/debug controllers read live `World`, chunks, tiles, `NavigationManager`, `NavigationTuning`, `WorldNavigationView`, or `Path` objects.
- Tile click debug logging now reuses the Runtime-owned tile inspection DTO instead of reading chunks, tiles, or fortress-map geology directly in `FortressMapClickController`.
- Haul/mining/construction placement previews now consume Runtime-owned placement preview DTOs instead of reading live ground items or terrain tiles inside `OrdersUI`.
- Order highlight mining/construction legal-cell dots now reuse Runtime-owned placement preview DTOs instead of reading live terrain inside the former `UiRenderer` paths.
- Debug spawn readiness/count logging now consumes a Runtime-owned debug spawn DTO instead of carrying `LoadedSession.World` into the map-click debug controller.
- Workshop panel keyboard editing now reads queue entry ids and worker-slot state from the Runtime-owned workshop snapshot facade; App no longer resolves or mutates `WorkshopState` through a live placeable scan.
- Overlay, keyboard, mouse, map-click, and placement input contexts now carry explicit `UiServices`, `NavigationOverlay`, or `HasFortressMap` dependencies instead of the full loaded-session snapshot that contains live `World`.
- Main map terrain/entity rendering now consumes Runtime-owned `SimulationMapViewportData`; `FortressMapRenderer` only clears/draws DTO cells and no longer reads live `World`, chunks, tiles, items, creatures, `FortressMap`, or geology catalogs.
- Frame rendering now consumes a Runtime-owned `SimulationFrameRenderData` aggregate for map viewport cells, navigation overlay data, and tile inspection data instead of issuing separate frame-time Runtime queries from `FortressFrameRenderer`.
- UI overlay rendering now consumes a Runtime-owned `SimulationUiOverlayFrameData` aggregate for build catalog, jobs, workshops, stockpile overlay/detail, zone overlay/detail, management drawer, Work drawer, and Debug menu data instead of issuing separate overlay-time Runtime queries for each panel/overlay.
- Work drawer rendering now consumes a Runtime-owned `SimulationWorkDrawerData` aggregate for jobs/workforce/orders/workshops instead of letting App UI helpers call separate runtime facade methods from each work panel helper.
- App/SadConsole presentation code is now split by surface: frame overlay orchestration, chrome/topbar/dock drawing, management drawer drawing, Debug menu drawing, debug unit overlay drawing, map overlay glyph drawing, placement overlay preview, quick menus, Work drawer panels, and workshop modal rendering no longer sit inside one `UiRenderer` god class.
- App state-machine registration/navigation now lives under `HumanFortress.App.GameStates`; fortress session context/load/bootstrap result state now lives under `HumanFortress.App.Session`; keyboard/mouse/placement/debug input routing now lives under `HumanFortress.App.Input`; SadConsole viewport/layout/view bootstrap helpers live under `HumanFortress.App.Rendering`; and UI service factories live under `HumanFortress.App.UI`.
- Loaded-session state/load results no longer carry live `World` or `FortressMap` objects into frame/input code; they expose only readiness flags plus render/UI/navigation presentation state.
- `FortressRuntimeAccess` no longer exposes public `HasWorld`, a live `World` property, the old bootstrap-world getter, or a two-step fortress-map generation/fill operation; UI readiness goes through `SimulationWorldAvailabilityData`, and session initialization requests a Runtime-owned fortress generation/fill operation through `RuntimeFortressGenerationRequest`.
- `GameStateManager` no longer owns the active `SimulationRuntimeSession`, tick scheduler, command queue, event bus, diff logs, runtime content snapshot, generation content, live `World` helper, render snapshot builder creation, navigation rebuild calls, or concrete runtime session controller directly; runtime lifetime is delegated through `GameStateRuntimeCoordinator`, which now creates the Runtime session through `FortressRuntimeSessionFactory`, stores `IFortressRuntimeSessionPorts`, and hands App code only narrow `FortressRuntimeAccess` role adapters.
- Rendering, input, placement, map-click, debug-spawn, workshop-panel, build-catalog, navigation-debug, simulation-control, UI-input, and session-bootstrap paths now see progressively narrower runtime access interfaces: `IFortressRuntimeReadAccess`, `IFortressRuntimeBuildCatalogAccess`, `IFortressRuntimeUiInputAccess`, `IFortressRuntimePlacementAccess`, `IFortressRuntimeMapInspectionAccess`, `IFortressRuntimeDebugSpawnAccess`, `IFortressRuntimeWorkshopPanelAccess`, `IFortressRuntimeNavigationDebugAccess`, `IFortressRuntimeSimulationControlAccess`, `IFortressRuntimeBootstrapAccess`, and the composed `IFortressRuntimeSessionAccess` marker. `FortressStateRuntimePorts` is the composition-time bundle; ordinary helpers receive named role ports instead of the full aggregate. The concrete `FortressRuntimeAccess` remains only as the App facade created by the fortress play state.
- `FortressRuntimeAccess` and Runtime session ports are split by role/capability partial files. The Access layer now delegates semantic App role methods to Runtime port interfaces, while the internal Runtime core owns command construction, snapshot/query methods, lifecycle, and content/host composition in separate partials.
- `FortressState` no longer owns input-context construction or fortress load orchestration; `FortressInputContextFactory` and `FortressSessionLoadCoordinator` keep those App concerns out of the state object.
- Game-state SadConsole screen presentation is centralized behind `IGameScreenPresenter` / `ScreenGameState<TScreen>`; individual state wrappers now create their screen and no longer write `GameHost.Instance.Screen` directly.
- `FortressPlayGameState` now depends on `IFortressPlayRuntimeHost` instead of the whole `GameStateManager`; runtime init/access for fortress play is a narrow state-transition collaborator.
- `GameStateManager` delegates FortressPlay runtime start/stop policy to `GameStateRuntimeLifecycle`, keeping transition ordering separate from simulation lifecycle policy.
- App startup concerns now live under `HumanFortress.App.Startup`: command-line option parsing, native library preload, strict content gate, unhandled exception binding, headless init, crash-test runner, and SadConsole lifetime runner are no longer embedded in `Program`.
- `Program` no longer holds a static `GameStateManager` or SadConsole frame-update hook; `SadConsoleGameApp` owns the running state manager and `SadConsoleGameRunner` owns SadConsole create/run/dispose.
- `GameStateManager` no longer exposes the old public `InitializeWorld(...)`, `ChangeState(...)`, `CurrentState`, no-op frame `Update(...)`, `Render(...)`, or `HandleInput(...)` surface. External navigation uses `TransitionTo(...)`; the state map lives in `GameStateRegistry`.
- `UiChromeRenderer` now consumes the Contracts-owned `SimulationStatus` snapshot directly; the App-owned `FortressSimulationStatus` wrapper and mapper have been deleted.
- Fortress view/bootstrap wiring now passes a `FortressUiInteractionDataSource` to UI component setup instead of handing the rendering bootstrapper an `IFortressRuntimeUiInputAccess` facade.
- Fortress input composition now uses `FortressInputCallbackHub` for controller callback binding instead of `inputController!` null-forgiving closure cycles.
- Fortress session size validation now lives in `FortressSessionSizeRules`; session storage, embark prep, fortress runtime initialization, and fortress viewport initialization use the same normalized size rule instead of separate hard-coded `2..8` checks.
- `InputHandlerComponent` has been split so Debug overlay clicks live in `DebugMenuInputHandler` and Work/Job Allocation keyboard/mouse handling lives in `WorkAllocationInputHandler`; the SadConsole component now acts as the top-level UI input dispatcher. `FortressScreenMouseInput` is also split by chrome button hit handling, root quick-menu hit testing, and mining submenu hit handling.
- `FortressInputContextFactory` is split by context family: constructor/dependency capture, keyboard/mouse/overlay presentation contexts, and map/placement/debug-spawn contexts are separate partial files.
- `UiStore` is now split by transient UI state domain: navigation/cancel flow, drawer state, quick-menu/submenu state, selection, build/material state, workshop panel state, placement state, Debug menu state, and toast/highlight feedback live in separate partial files instead of one state god object.
- `UiManagementDrawerRenderer`, `UiWorkDrawerRenderer`, and `UiDebugMenuRenderer` have been split into focused partial renderers by chrome, content dispatch, tab content, and drawer/debug surface while staying in App.UI because they depend on SadConsole surfaces and `UiStore`.
- `UiChromeRenderer` is split by chrome surface: simulation topbar, dock/quick buttons, help/pause modals, and toast drawing.
- Quick-menu feature UI helpers are split by App presentation concern: `ZonesUI`, `OrdersUI`, and `StockpileUI` now separate menu drawing, placement/preset handling, overlay/preview drawing, detail/edit popups, and local drawing helpers instead of mixing all surfaces in one file.
- `ZonesUI` menu rendering now also separates root/submenu orchestration from individual third-level zone submenu panels.
- `BuildUI` and `FortressBuildKeyboardInput` are split by App UI concern: root/submenu rendering, construction material dialog drawing, structural keyboard handling, and workshop category/item selection now live in focused App.UI/App.Input partial files.
- `FortressMapOverlayGlyphRenderer` is split by overlay type: workshop footprint/placement previews, mining job highlights, and order highlight/preview glyph rules now live in separate App.Rendering partial files.
- `NavigationOverlay` is split into state/mode mapping, cell/path rendering, legend rendering, and color parsing. `FortressPlacementOverlayRenderer` is split into top-level placement overlay orchestration, anchored previews, workshop previews, and preview-mode mapping.
- `FortressPlacementController` is split by command family: stockpile placement/copy/create, haul order placement, and buildable/zone placement operations.
- World-map and embark-prep screens now read generated-world map information through `FortressSessionContext.TryGetWorldSize(...)` and `TryGetWorldTileView(...)` returning a primitive/name-based WorldGen `WorldMapTileView`; App no longer reads `WorldGenResult.Tiles` or raw `WorldTile`, and biome display policy sits in App.Rendering rather than state screens.
- `WorldMapState` now clamps its initial cursor and camera movement against Session-provided world dimensions instead of assuming the generated world is larger than the map viewport.
- `WorldMapState` is split into main state setup, rendering, and keyboard-input partial files so world-map UI presentation can evolve without re-growing a single state-object file.
- `WorldGenState` is split into main state setup, rendering/progress drawing, separate mouse/keyboard input, name-edit input, option mutation, drawing controls, and generation action partial files; the screen still owns world-generation UI flow, but no longer combines all UI/input/generation logic in one 550-line state file.
- `MainMenuState` is split into main state setup, rendering, input, and menu action partial files so menu presentation and transition actions no longer share one large state file.
- `MainMenuState` rendering is further split by page rendering and decorative/menu-art drawing so menu state flow stays separate from SadConsole presentation details.
- `EmbarkPrepState` is split into setup, rendering, keyboard input, and embark action partials. App UI command classes under `HumanFortress.App.UI.Commands` are now one command per file instead of one mixed command file.
- Runtime snapshot builders have also been split by read-model concern: `NavigationOverlaySnapshotBuilder` now separates public entrypoints, basic/structural overlay-mode builders, path-cell mapping, and grid/bit helpers; `FortressRuntimeSnapshotBuilder` now separates base/debug/catalog, frame aggregates, map/query snapshots, and Work/workshop snapshots; `MapViewportSnapshotBuilder` now separates viewport orchestration, terrain glyph policy, and entity glyph policy; `WorkshopSnapshotBuilder` now separates workshop scanning, summary mapping, queue mapping, and construction-material progress; `ManagementDrawerSnapshotBuilder`, `StockpileSnapshotBuilder`, `JobsDebugSnapshotBuilder`, and `FortressRuntimeSessionSnapshotFacade` are split by their own read-model/session-query families instead of growing as new god-object facades.
- Runtime concrete system assembly is split by group owner: `FortressRuntimePlanningSystems` and `FortressRuntimeJobSystems` now live in separate Runtime composition files instead of sharing one mixed system-groups file.
- Runtime dependency grouping is split by group owner: root `FortressRuntimeDependencies`, `FortressRuntimeCatalogs`, `FortressRuntimeTunings`, and `FortressRuntimeWorkforce` now live in separate Runtime files.
- Runtime session snapshot entrypoints are exposed through Runtime read/snapshot ports and implemented by split `FortressRuntimeSessionCore` partials by facade family: base/debug/catalog availability, frame/overlay data, map/navigation/placement queries, and Work/workshop queries.
- App input routing is further split by input family: keyboard router context/navigation helpers, mouse router contracts/click handling, overlay-click context/right-click/map pass-through, global shortcut drawers/tabs/debug-key detection, orders keyboard submenu/mining/haul/WIP handling, build workshop category/item selection, screen quick-menu root clicks, world-map cursor/camera movement, and main-menu mouse input now live in focused App.Input/App.States partial files.
- App state/rendering presentation splits now include compact orders menus, Work drawer workshop queue/directory/standing-order/construction-status columns, workshop placement preview rendering, World-map tile glyph/info-panel/camera-bound helpers, and FortressState focus/update/input/render lifecycle partials as separate SadConsole-facing files instead of mixing every UI surface in one renderer/state file.
- Runtime read-model facade splits now include stockpile, navigation, tile-inspection, placement-preview, and map-viewport entrypoint files under `FortressRuntimeSnapshotBuilder`, plus separate placement-preview eligibility/geometry, debug-item taxonomy, management-drawer item naming, map-viewport creature/item glyph policies, navigation overlay flow-field/ramp-mask policies, and navigation-source mapping/construction-site adapter helpers.
- Runtime command-target helpers are narrower: `StockpileCommandTarget` now separates stockpile cell collection and naming from the create command entrypoint, and `WorkshopQueueCommandTarget` separates workshop state lookup/worker-slot initialization from queue command methods.
- `SimulationRuntimeHostCore` lifecycle methods are split from system/pipeline configuration, and generic `SimulationRuntimeHost<TSystems>` accessors/start-stop lifecycle are split from constructor composition so host startup/shutdown policy is not mixed with Runtime tick-pipeline attachment.
- App diagnostics logging has a smaller root facade: the legacy log category resolver is split out of `Logger`, keeping the public App logger surface separate from compatibility message-classification policy.
- App diagnostics logging now also splits Logger initialization/close lifecycle and level-specific helper methods from the core diagnostic write path.
- App no longer has direct project references to `HumanFortress.Jobs`, `HumanFortress.Simulation`, or `HumanFortress.Navigation`; those modules are reached through Runtime, WorldGen, or DTO/query boundaries.
- UI placement command creation now maps App UI intents to Runtime request DTOs; command construction and Simulation order enum/material DTO conversion are inside Runtime rather than App input code.
- Runtime command dispatch now uses narrow target-context roles backed by `SimulationRuntimeCommandTargets`; `SimulationRuntimeContext` no longer directly implements the individual profession/item/creature/order/zone/workshop/stockpile command target interfaces, and commands no longer receive an all-target aggregate.
- Runtime command target handler binding now goes through profession-specific `IRuntimeProfessionCommandBindings`; `SimulationRuntimeContext` no longer exposes a broad all-target binding or a direct profession-specific handler setter.
- Non-registry runtime content DTOs such as `GeologyData` and `ZoneDefinitionData` now compile from `HumanFortress.Contracts.Content`; active source/test scans no longer find the historical `HumanFortress.Core.Content` namespaces.
- Current no-build static scans found no active App references to `HumanFortress.Core.Commands`, `HumanFortress.Runtime.Commands`, App command factories, direct Jobs/Simulation/Navigation project namespaces, the old `HumanFortress.Runtime.Requests` namespace, the old `HumanFortress.Core.Content` namespace, or the old navigation contract namespace. Remaining direct `HumanFortress.Runtime` App usings are limited to App.Runtime adapter/facade implementation files. This source-only batch is not build verified yet.
- Remaining high-priority architecture work is reducing transitional internal bootstrap bridges, cleaning any remaining compatibility namespaces outside the now-updated navigation and item/creature definition contracts, diff-authoritative stockpile work, and keeping session/bootstrap glue from growing new gameplay/domain logic.

## Verified Batch: UI/Debug Snapshot Facades

Status: build verified after latest UI presentation/session-controller/runtime-access-interface split, bootstrap/play access split, command-target aggregate split, Content DTO namespace cleanup, Runtime-owned world-content loader migration, Runtime-owned auto-dig/notification bridge extraction, App module boundary cleanup, App direct Jobs/Simulation/Navigation reference removal, Runtime placement command factory extraction, and caller-role runtime facade narrowing.

### Completed

- Added Runtime-built jobs/workforce/workshop/build/debug/tile-inspection read models; DTO contracts now live in `HumanFortress.Contracts.Runtime.Snapshots` and builders live in `HumanFortress.Runtime.Snapshots`:
  - normalized job stat rows for hauling, mining, crafting, and construction status
  - scheduler stats and optional transport queue debug rows
  - active job rows and mining overlay point lists
  - profession definition and roster rows for the job allocation UI
  - order summary/recent-designation rows for Work drawer order panels
  - workshop summary and queue rows for Work drawer workshop lists/status panels, detailed workshop panel rendering, and workshop click hit-testing
  - build catalog rows for build quick-menu workshop browsing and buildable footprint preview
  - debug menu status/item rows and tile inspection popup rows
  - management drawer rows for F1 creature lists, F2 ground-item lists/kind filters, F4 zone lists, and stockpile drawer lists
  - zone overlay cells for the visible viewport/z-layer and zone detail popup rows
  - stockpile overlay cells, stockpile detail popup rows, and stockpile/zone hit-test query DTOs
  - navigation overlay cells for walkability, movement cost, traffic, connectivity, flow-field, ramp-mask debug draw modes, and F10 path-debug path cells
- Added Runtime-owned map viewport and work drawer aggregate read models in `HumanFortress.Runtime.Snapshots`:
  - screen-space map glyph/color cells for terrain, cursor, visible creatures, and visible ground items
  - world availability rows for UI/input readiness checks
  - work drawer aggregate rows bundling jobs, workforce, order summaries, and workshop summaries for the Work drawer
- Added Runtime-owned frame/overlay aggregate read models in `HumanFortress.Runtime.Snapshots`:
  - frame render aggregate rows bundling map viewport, navigation overlay, and tile inspection data
  - UI overlay frame rows bundling build catalog, jobs, workshop overlays/panels, zone/stockpile overlay/detail data, management drawer data, Work drawer data, and Debug menu data
- Added focused Runtime-owned snapshot builders for build catalog, debug menu, tile inspection, jobs, workforce, orders, workshops, management drawers, zone overlay/detail data, and stockpile overlay/detail data; `FortressRuntimeSnapshotBuilder` is now only a thin facade over those builders.
- Changed `GameStateManager` to delegate UI/debug snapshot construction to Runtime-owned builders instead of aggregating that logic in App or exposing concrete job wrappers, `ProfessionAssignments`, `UnifiedJobsOrchestrator`, or scheduler/workshop tunings as public App-facing properties.
- Removed the old jobs debug cache so mining overlay and active-job panels read a fresh per-frame snapshot rather than a stale cached debug bundle.
- Changed `FortressRuntimeAccess` to expose snapshot/query DTO methods such as `GetJobsDebugData(...)`, `GetWorkDrawerData(...)`, `GetWorldAvailabilityData()`, and `GetWorkshopDebugData()` while no longer exposing concrete transport/mining/construction/craft job systems, `ProfessionAssignments`, or live world readiness properties.
- Changed Work drawer rendering, active jobs rendering, construction status rendering, scheduler diagnostics, mining job/completion highlights, and job allocation keyboard/mouse input to consume the DTO facade.
- Changed Work drawer workshop list, active queue notes, workshop directory, and construction status panels to consume workshop snapshot DTOs instead of calling `World.GetAllChunks()` / `GetAllOwnedPlaceables()` inside the former `UiRenderer` paths.
- Changed Work drawer labor overview, dwarf roster, order summary, and active jobs recent-designation sections to consume workforce/order DTOs instead of reading `world.Creatures` or `world.Orders` inside the former `UiRenderer` paths.
- Changed Work drawer panels to consume a single `SimulationWorkDrawerData` aggregate supplied by the overlay renderer, removing separate App UI calls to workforce/order/job/workshop facade methods while rendering Work tabs.
- Changed `FortressUiOverlayRenderer` to fetch one `SimulationUiOverlayFrameData` per frame and pass the relevant DTOs down to focused App renderers (`UiManagementDrawerRenderer`, `UiDebugMenuRenderer`, `UiWorkDrawerRenderer`, stockpile UI, zone UI, workshop overlay/panel rendering, and quick-menu rendering).
- Changed detailed workshop panel rendering, workshop overlay/material-progress rendering, and workshop map-click hit-testing to consume workshop snapshot DTOs instead of walking live placeables and construction definitions.
- Changed build quick-menu workshop item browsing, build keyboard workshop selection, and buildable placement preview to consume a Runtime-owned build catalog DTO instead of passing `IConstructionCatalog`/`ConstructionDefinition` into UI/keyboard rendering paths.
- Changed Debug menu status/items rendering, Debug item paging, and Debug item mouse selection to consume a Runtime-owned debug DTO instead of reading item definitions or world metric counts from UI code; Debug item paging now renders directly in `UiDebugMenuRenderer` instead of a post-render overlay.
- Changed tile inspection popup rendering to consume a Runtime-owned tile inspection DTO instead of passing live `World`, `FortressMap`, or `IRuntimeGeologyCatalog` into `FortressTilePopupRenderer`.
- Changed F1 creature list, F2 ground-item list/kind filters, F4 zone list, and stockpile drawer list rendering to consume a Runtime-owned management drawer DTO instead of passing live `World` or `StockpileManager` into `UiManagementDrawerRenderer.DrawDrawer`.
- Changed zone overlay/detail popup rendering and zone click/delete hit-testing to consume Runtime-owned zone DTO/query methods instead of passing live `World` into `ZonesUI` or reading `World.Zones` in App click helpers.
- Changed stockpile overlay/edit popup rendering and stockpile click/copy hit-testing to consume Runtime-owned stockpile DTO/query methods instead of passing live `World` or `StockpileManager` into `StockpileUI`.
- Changed navigation debug overlay drawing and F10 path-debug queries to consume Runtime-owned navigation DTOs instead of passing live `World` into `NavigationOverlay`, storing `NavigationManager`/`NavigationTuning` inside the overlay renderer, or creating `WorldNavigationView`/`DeterministicAStar` inside the App debug controller. The snapshot builder uses the existing Navigation-owned world source interface and nav cache, so Runtime owns the read/query path while App only maps DTO cells to SadConsole glyphs/colors.
- Removed `NavigationManager` from the loaded-session UI snapshot/result path; `FortressLoadedSessionState` no longer carries navigation internals for debug input handling.
- Added tile meta flags to `SimulationTileInspectionData` and changed tile click debug logging to consume `GetTileInspectionData(...)` instead of reading live `World`, `Chunk`, `TileBase`, or `FortressMap` data in the click controller.
- Added Runtime-owned placement preview DTOs/builders for haul ground-item previews and mining terrain eligibility previews; `OrdersUI.RenderPlacementPreview(...)` now draws DTO cells instead of reading live `World`, ground items, tiles, or simulation terrain kinds.
- Extended Runtime-owned placement preview DTOs/builders to cover construction wall/floor/ramp eligibility and changed construction placement preview plus construction order highlights to read those DTO cells instead of using UI-side terrain checks.
- Removed live `World` from `FortressPlacementControllerContext`; zone/stockpile placement guards now use the runtime facade's world-availability predicate instead of carrying the world through placement input code.
- Removed the old `WorldProvider` dependency from the view/interaction bootstrap path that only existed for Debug item selection.
- Added `SimulationDebugSpawnData` so debug spawn input checks world availability and logs definition counts through a Runtime snapshot facade.
- Changed workshop panel keyboard editing to read `WorkshopSummaryView` via `FortressRuntimeAccess.GetWorkshopPanelData(...)`; `WorkshopQueueEntryView` now carries `EntryId`, and the App-owned live `FortressWorkshopPanelContextResolver` was removed.
- Changed overlay, keyboard, mouse, map-click, and placement input contexts to depend on explicit UI/navigation/map-availability values instead of passing `FortressLoadedSessionSnapshot` through input controllers.
- Removed live `World` from `FortressUiOverlayRenderContext`; overlay rendering now uses Runtime snapshots/queries and the same-frame map viewport DTO for map-readiness checks.
- Added Runtime-owned `SimulationMapViewportData` and moved main map terrain/entity display selection into Runtime snapshot builders. `FortressMapRenderer` is now a pure App drawer over DTO cells plus navigation overlay rendering.
- Added Runtime-owned `SimulationFrameRenderData` and changed `FortressFrameRenderer` to fetch map viewport, navigation overlay, and tile inspection read models through one frame query.
- Removed old single-purpose overlay/frame facade methods from `FortressRuntimeAccess` after the aggregate frame/overlay DTOs replaced them: management drawer, zone overlay/detail, stockpile overlay/detail, jobs debug, Work drawer, navigation overlay, and map viewport.
- Removed live `World` and `FortressMap` objects from `FortressLoadedSessionState`, `FortressLoadedSessionSnapshot`, and `FortressSessionLoadResult`; App frame/input paths now receive readiness flags instead of simulation/worldgen objects.
- Replaced public `FortressRuntimeAccess.HasWorld`, the live `World` facade property, and the internal bootstrap-world getter with `SimulationWorldAvailabilityData` for UI readiness. A later cleanup moved the remaining fortress-map generation/fill step behind a Runtime-owned `RuntimeFortressGenerationRequest`.
- Removed unused live `World`/camera/z parameters from the former quick-menu renderer path and the dormant `WorkDrawerOverlay.DrawWorkSchedulerOverlay(...)` hook.
- Split `FortressUiOverlayRenderer` into focused App.Rendering helpers for map overlays, placement previews, tool popups, and modal/debug rendering; the coordinator now fetches the overlay-frame DTO and delegates presentation work.
- Split the former `UiRenderer` Work drawer, quick menu, workshop panel, map overlay glyph, chrome/topbar/dock, management drawer, Debug menu, and debug unit overlay methods into `UiWorkDrawerRenderer`, `UiQuickMenuRenderer`, `UiWorkshopPanelRenderer`, `FortressMapOverlayGlyphRenderer`, `UiChromeRenderer`, `UiManagementDrawerRenderer`, `UiDebugMenuRenderer`, and `FortressDebugUnitOverlayRenderer`. These remain in App/UI or App/Rendering because they depend on SadConsole surfaces and `UiStore`, while simulation facts still enter as Runtime DTOs.
- Routed dock/quick-button drawing, keyboard shortcuts, and mouse hit-testing through `UiChromeSlots` plus `ButtonLayoutCalculator`, and centralized Debug item category label/enum mapping in `DebugLayoutCalculator` so rendering and input stay aligned.
- Added `FortressRuntimeSessionSnapshotFacade` in Runtime so App.Runtime session glue passes the active runtime session to Runtime-owned read-model builders instead of unpacking live `World`, navigation, geology, construction, or recipe catalog objects for each UI/debug query.
- Removed `Geology`, `GenerationContent`, construction catalog, recipe catalog, live bootstrap-world, render snapshot builder creation, and navigation rebuild exposure from `FortressRuntimeAccess`; session initialization now sends a `RuntimeFortressGenerationRequest` and Runtime generates/fills the fortress world internally.
- Added `FortressRuntimeSessionController` in App.Runtime as an intermediate session-controller adapter over Runtime core, Runtime snapshot facade calls, bootstrap request forwarding, and fortress-play startup. Later hardening removed that passthrough and then hid the concrete core; `GameStateRuntimeCoordinator` now creates Runtime session ports through `FortressRuntimeSessionFactory` and `FortressRuntimeAccess` remains the App role adapter.
- Added App runtime access interfaces for the remaining facade boundary: render contexts depend on `IFortressRuntimeReadAccess`, while input, placement, map-inspection, debug-spawn, workshop-panel, navigation-debug, simulation-control, and session-bootstrap paths use narrower role interfaces. `IFortressRuntimeSessionAccess` only composes those roles at creation time, and the old broad play facade has been removed.
- Changed `FortressState` composition so ordinary frame/input calls receive only the role interfaces they need while `IFortressRuntimeBootstrapAccess` is reserved for the session loader/initializer path.
- Moved active world content application from App to Runtime's `SimulationWorldContentLoader`; App now injects logging and content issue callbacks instead of owning the loader.
- Moved optional startup/after-fill auto-dig mining command construction into Runtime's `RuntimeAutoDigSeeder`.
- Added `FortressRuntimeWorkshopCompletionNotifier` so App binds construction completion UI notifications without referencing concrete Runtime job-system wrappers or a static `ConstructionJobSystem` hook.
- Removed the legacy App `FortressRenderSnapshotService` / `OverlayFromSnapshot` bridge; loaded-session state/load results no longer carry `RenderSnapshotBuilder` or `RenderSnapshot`, and workshop overlays render from Runtime workshop DTOs.
- Moved tick scheduler, command queue, event bus, and diff logs out of `GameStateManager` and into Runtime's session implementation; `FortressRuntimeAccess` now delegates simulation status, command enqueue, pause, and speed controls through Runtime session ports.
- Moved navigation cache rebuild mutation out of `FortressRuntimeSessionSnapshotFacade` into Runtime's `SimulationRuntimeSessionNavigation`, keeping the session snapshot facade read/query focused.
- Split Runtime command target dispatch behind `SimulationRuntimeCommandTargets` plus narrow target-context role interfaces. Runtime commands now resolve order, zone, stockpile, workshop, spawn, and profession targets through role-specific command contexts instead of relying on `SimulationRuntimeContext` implementing every target interface directly or exposing an all-target aggregate.
- Narrowed `FortressRuntimeAccess`/`GameStateManager` construction, geology, recipe, navigation, and runtime-session read-model properties so UI-facing paths no longer expose those concrete contracts publicly.
- Added App-owned `UiConstructionShape` and moved stockpile preset menu options out of Simulation DTOs; App UI state/rendering now maps construction shape to Simulation only at the command/placement-preview boundary.
- Added Runtime-owned `FortressRuntimeLogBindings` so App still supplies logger callbacks but no longer owns the list of lower-layer Simulation/Navigation static callback targets.
- Split App diagnostics out of the runtime facade: `IFortressRuntimeReadAccess` no longer exposes `DiagnosticSnapshot`, and frame/modal rendering receives an App diagnostics provider separately.
- Logging, category callback creation, and content-issue logging are injected by App composition into Runtime core; the removed App controller no longer exists as a logging bridge.
- Moved App-only diagnostics helpers into `App.Diagnostics`, screen layout/viewport/view/tile-inspection presentation state into `App.Rendering`, placement geometry into `App.UI.Placement`, and App state-machine navigator/registration into `App.GameStates` as part of keeping App.Runtime focused on runtime access/session adapter boundaries.
- Added Runtime-owned `FortressRuntimeSessionCore` for active session ownership, scheduler/queue/event/diff services, Runtime content snapshot capture, snapshot facade calls, runtime startup, auto-dig seeding, workshop completion notification binding, and fortress-map generation/fill. The old App `FortressRuntimeSessionController` thin adapter was later deleted, and a later hardening pass made the core internal behind `FortressRuntimeSessionFactory`/session ports.
- Moved App fortress session context/load/bootstrap state into `HumanFortress.App.Session`, input controllers into `HumanFortress.App.Input`, UI service factories into `HumanFortress.App.UI`, and view/layout/bootstrap helpers into `HumanFortress.App.Rendering`; `HumanFortress.App.Runtime` now contains only runtime access interfaces/facades plus the session-controller adapter.
- Added `GameStateRuntimeCoordinator` so `GameStateManager` delegates runtime session-controller construction, runtime access creation, fortress world initialization, and fortress-play start/stop instead of owning the concrete controller directly.
- Added Runtime-owned placement command intent types and `RuntimePlacementCommandFactory`; App placement input now maps UI enums/options to Runtime command intents without referencing `HumanFortress.Simulation.Orders`.
- Removed App's direct project references to Jobs, Simulation, and Navigation after static scans found no App source using those namespaces.
- Split the play-time runtime facade into smaller caller-role interfaces for keyboard input, UI input callbacks, placement, map inspection, debug spawn, workshop panel editing, navigation debug, simulation controls, and command enqueueing.
- Fixed the build-exposed Runtime workshop completion delegate binding by avoiding null-propagation over a method group.
- Removed the build-exposed stale `FortressState.RefreshSnapshot()` helper from the old render-snapshot path; loaded-session state now only exposes readiness/presentation state through `Capture()`.
- Fixed the duplicated Craft stats lines in the scheduler diagnostics column while touching that panel.
- Left session/bootstrap glue, optional auto-dig startup, and construction completion UI callback binding for later batches. Later no-build cleanup moved auto-dig seeding and construction completion notification bridging into Runtime.

### Verification

- Source scan found no UI-facing `FortressRuntimeAccess` reads of concrete transport/mining/construction/craft job systems, `ProfessionAssignments`, `UnifiedJobsOrchestrator`, or scheduler tunings.
- Source scan found no former `UiRenderer` workshop drawer helper using the deleted `CollectWorkshops` live-placeable scan; workshop drawer data now enters through `GetWorkshopDebugData()`.
- Source scan found no Work drawer helper reading active/recent order snapshots or live creature lists directly from App UI renderers; those reads now enter through `GetOrdersDebugData()` and `GetWorkforceDebugData()`.
- Source scan found `FortressMapClickController` no longer depends on construction catalogs or live placeable scans for workshop hit-testing.
- Source scan found no UI/keyboard/overlay path under the former `UiRenderer` paths, `WorkshopCategoryMapper`, `FortressUiOverlayRenderer`, `FortressBuildKeyboardInput`, `FortressContextKeyboardInput`, `FortressKeyboardInputRouter`, or `FortressState` still consuming `IConstructionCatalog`/`ConstructionDefinition`.
- Source scan found no former `UiRenderer` workshop overlay path using `World`, `PlaceableInstance`, `GetAllOwnedPlaceables()`, or the old material-progress helper methods; overlay material progress now enters through `GetWorkshopDebugData()`.
- Source scan found no `WorldProvider` path in view/interaction bootstrap or `InputHandlerComponent`, and no Debug menu item/status code reading `world.Items.GetAllDefinitions()`, world chunk counts, or item/creature definition counts outside Runtime snapshot builders.
- Source scan found no `World`, `FortressMap`, geology catalog, item definition, or creature definition reads in `FortressTileInspectionController` / `FortressTilePopupRenderer`; tile popup data now enters through `GetTileInspectionData(...)`.
- Source scan found no `UiManagementDrawerRenderer.DrawDrawer` caller passing live `World` or `StockpileManager`, and no F1/F2/F4 drawer helper scanning `world.Creatures`, `world.Items`, `world.Zones`, or `stockpileManager.GetAllZones()`.
- Source scan found `ZonesUI` no longer references live `World`, scans chunks/zone shards, or reads zone manager data; zone overlay/detail/hit data now enter through `GetZoneOverlayData(...)`, `GetZoneDetailData(...)`, and `FindZoneAt(...)`.
- Source scan found `StockpileUI` no longer references live `World`, `StockpileManager`, stockpile zones, or chunk stockpile data; stockpile overlay/detail/hit data now enter through `GetStockpileOverlayData(...)`, `GetStockpileDetailData(...)`, and `FindStockpileAt(...)`.
- Source scan found `NavigationOverlay` no longer references live `World`, `Chunk`, `NavigationManager`, `ChunkNavData`, `NavCapability`, `NavigationTuning`, or `HumanFortress.Contracts.Navigation.Path`, and no caller passes `World` into `NavigationOverlay.RenderOverlay(...)`.
- Source scan found `FortressNavigationDebugController`, `FortressKeyboardInputRouter`, `FortressLoadedSessionState`, `FortressSessionLoadResult`, and `FortressSessionRuntimeBootstrapper` no longer reference `NavigationManager`, `NavigationTuning`, `WorldNavigationView`, `DeterministicAStar`, or `PathRequest`; F10 path-debug now enters through `FindNavigationDebugPath(...)`.
- Source scan found `FortressMapClickController` no longer references live `World`, `Chunk`, `FortressMap`, `GetChunk(...)`, or `GetTile(...)`; tile click logging now enters through `GetTileInspectionData(...)`.
- Source scan found `OrdersUI.RenderPlacementPreview(...)` no longer references live `World`, `GetTile(...)`, `TerrainKind`, or ground item managers; haul/mining/construction preview eligibility now enters through `GetPlacementPreviewData(...)`.
- Source scan found the old order-highlight renderer path no longer has an App-side construction terrain helper; mining/construction highlight dots now enter through `GetPlacementPreviewData(...)`.
- Source scan found no App references to `FortressWorkshopPanelContext`, `FortressWorkshopPanelContextResolver`, or `WorkshopState`; workshop keyboard input reads snapshot DTOs and enqueues commands by DTO `EntryId`.
- Source scan found the loaded-session snapshot is no longer carried through mouse, keyboard, overlay-click, map-click, or placement controller contexts; the full snapshot remains isolated to frame rendering.
- Source scan found `FortressUiOverlayRenderContext` no longer carries live `World`, and `UiQuickMenuRenderer.Draw(...)` / `WorkDrawerOverlay.DrawWorkSchedulerOverlay(...)` no longer accept live `World`.
- Source scan found `FortressMapRenderer` no longer references live `World`, `FortressMap`, chunks, tiles, item/creature managers, terrain kinds, or geology catalogs; main map terrain/entity rendering enters App as `SimulationMapViewportData`.
- Source scan found `FortressFrameRenderer` now uses `GetFrameRenderData(...)` instead of separate navigation overlay, map viewport, and tile-inspection Runtime calls.
- Source scan found `FortressUiOverlayRenderer` now uses `GetUiOverlayFrameData(...)` for overlay/drawer/debug read models; remaining direct Runtime queries are placement preview queries driven by active drag/highlight state and App diagnostics.
- Source scan found `FortressLoadedSessionState`, `FortressLoadedSessionSnapshot`, and `FortressSessionLoadResult` no longer carry live `World` or `FortressMap` objects into frame/input code.
- Source scan found Work drawer helpers no longer call separate workforce/order/job/workshop runtime facade methods; Work drawer rendering consumes `SimulationWorkDrawerData`.
- Source scan found old `UiRenderer.DrawQuickMenu`, `DrawWorkshopPanel`, `DrawWorkshopsOverlay`, `DrawWorkshopPlacementPreview`, `DrawMiningJobHighlights`, `DrawMiningCompletedHighlights`, and `DrawOrderHighlights` call paths no longer exist; App presentation has been split into focused App.UI/App.Rendering helpers while continuing to consume Runtime DTOs.
- Source scan found active source no longer contains a `UiRenderer` class, `DebugPageOverlayRenderer`, or `DrawDebugUnits` path; presentation now delegates to `UiChromeRenderer`, `UiManagementDrawerRenderer`, `UiDebugMenuRenderer`, `UiWorkDrawerRenderer`, `UiQuickMenuRenderer`, `UiWorkshopPanelRenderer`, `FortressMapOverlayGlyphRenderer`, and `FortressDebugUnitOverlayRenderer`.
- Source scan found App no longer references `FortressRuntimeSnapshotBuilder` directly and `FortressRuntimeAccess` no longer exposes `GetBootstrapWorld()`, render snapshot builder creation, navigation rebuild methods, or a two-step fortress-map fill operation. Active runtime session ownership, scheduler/queue/event/diff services, content injection, auto-dig seeding, construction-completion notification bridging, navigation rebuilds, and WorldGen-backed fortress-map generation/fill now sit behind Runtime session ports implemented by the internal core.
- Source scan found `FortressRuntimeAccess` concrete references are now limited to its implementation and the fortress-play creation/session-bootstrap path; App rendering uses `IFortressRuntimeReadAccess`, while input/placement/map-click/debug/workshop controllers use smaller caller-role runtime access interfaces instead of the full play facade.
- Source scan found no active App source or App project reference to `HumanFortress.Simulation`, `HumanFortress.Jobs`, or `HumanFortress.Navigation`; placement command translation now enters Runtime through `RuntimePlacementCommandFactory`.
- Source scan found `IFortressRuntimeReadAccess` no longer references App diagnostics types or exposes logger snapshots.
- Source scan found App UI no longer uses `HumanFortress.Simulation.Orders.ConstructionShape` or `HumanFortress.Simulation.Stockpile.StockpilePreset`; those are now App UI types/options until command/runtime query mapping.
- Source scan found no Runtime command still casting `ISimulationContext` to individual command target interfaces, and `SimulationRuntimeContext` implements explicit clock/target command context roles rather than seven command target interfaces.
- Source scan found concrete Runtime command target implementations, Runtime auto-dig seeding, world-content loading, Runtime logging helper state, Runtime composition helpers, Runtime command factories, and Runtime snapshot builder/facade helpers are no longer public API.
- `git diff --check` passed after moving the snapshot DTOs/builders out of App, splitting the Runtime snapshot builder, splitting App presentation helpers by UI surface, adding `FortressRuntimeSessionController`, narrowing App runtime access interfaces, removing the legacy App render-snapshot bridge, moving scheduler/command/event/diff ownership into Runtime's session core, moving world-content loading/auto-dig seeding to Runtime, hiding construction completion binding behind a Runtime notifier, splitting App diagnostics from runtime reads, moving lower-layer log binding knowledge into Runtime, moving UI-only geometry/layout helpers out of App.Runtime, moving session/input/view/UI-service helpers into their App submodules, removing App direct Jobs/Simulation/Navigation references, and adding Runtime placement command intent mapping.
- First fast solution build caught `FortressRuntimeSystemGroups` using null propagation on the `WorkshopCompletion.Notify` method group; this was fixed by capturing the notifier and passing an explicit nullable delegate.
- Second fast solution build caught a stale `FortressState.RefreshSnapshot()` call path after loaded-session state was narrowed; the unused helper was removed.
- `HumanFortress.sln` fast build then passed with `0 Warning(s), 0 Error(s)` using .NET 8, `--no-restore`, single MSBuild worker, analyzers disabled, and `UseAppHost=false`.
- Follow-up architecture-hardening fast build passed with `0 Warning(s), 0 Error(s)` after generated-world DTOs moved to WorldGen and Runtime implementation helpers were internalized.

## Verified Batch: Content Registry Namespace Cleanup

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
- Moved runtime geology and zone JSON DTOs to `HumanFortress.Contracts`; a later namespace cleanup moved them under `HumanFortress.Contracts.Content` and removed active `HumanFortress.Core.Content` source/test references.
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
  - Changed `GameStateManager` to pass explicit runtime logging and the then App-owned auto-dig command delegate into Runtime. A later no-build cleanup moved the auto-dig command seeding into Runtime's `RuntimeAutoDigSeeder`.
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
- Runtime command targets and App UI helpers should consume active-session catalog facts through Runtime snapshot/query DTOs or explicit runtime dependencies; do not add fallback reads to `ContentRegistry.Instance` for construction/recipe UI convenience paths, and do not re-expose construction/recipe catalogs through `FortressRuntimeAccess`.
- Jobs/Runtime/Simulation execution paths should receive catalog/tuning/geology dependencies explicitly. `IRuntimeGeologyCatalog`, `ConstructionTuning`, and `PlaceableTuning` now compile from Contracts; App/Content may bridge to the transitional structured registry, but Jobs/Runtime/Simulation should not reach for it directly.
- Navigation and placeable tunings now follow the same rule as construction/scheduler/workshop tunings: Content captures JSON once, App composition parses it, and runtime systems receive explicit objects.
- Mining drop/tick tuning now follows the same snapshot rule. Do not add new mining resolver reads from `ContentRegistry.Instance`.
- WorldGen now follows the same active-session snapshot rule: fortress generation receives explicit `FortressGenerationContent` with geology plus mapgen/ore/cavern tuning. Do not add new `ContentRegistry.Instance` reads to `FortressGenerator`, `FortressMap`, or `FortressChunk`.
- Runtime world-content loading should call `FortressRuntimeContentSnapshotLoader.ApplyCoreData(...)` and consume the returned snapshot; do not add direct `ContentRegistry.Instance.ApplyCoreData(...)` or `ContentRegistry.Instance.Zones` reads back into `SimulationWorldContentLoader`, and do not add App logging calls directly to that Runtime helper.
- The remaining production direct structured-registry reads are now concentrated in Content bootstrap/snapshot capture/application and the Content-owned structured registry internals themselves.
- Normal startup now loads the structured runtime registry only. Do not re-add legacy registry loading to `RuntimeContentRegistryLoader`.
- The legacy `HumanFortress.Core.Content.ContentRegistry` source has been deleted. Do not reintroduce a second runtime registry source model or the old construction/recipe singleton registries.
- `HumanFortress.Navigation` must not regain a Core/Content reference for tuning convenience. Pass `NavigationTuning` into `NavigationManager`, `PathService`, overlay/debug helpers, or runtime factories.
- `GameStateManager` still owns UI-facing state transitions, but scheduler/command/diff/session controls now live behind Runtime session ports implemented by the internal session core, and runtime-facing UI/session controls flow through `GameStateRuntimeCoordinator`/`FortressRuntimeAccess` role interfaces plus Runtime-owned snapshot builders. Continue shrinking the remaining live World/navigation bootstrap bridges instead of adding gameplay reads back to App.

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

- Later batches deleted `ConstructionRegistry` / `RecipeRegistry` singleton compatibility classes and moved construction/recipe definitions plus catalog stores to Contracts.
- Later batches also made the concrete structured `ContentRegistry` internal. UI/App/runtime callers should use `FortressRuntimeContentSnapshotLoader`, Content loader facades, Runtime snapshots, or explicit injected catalog interfaces rather than `ContentRegistry.Instance`.
- The remaining content work is richer diagnostics/debug surfaces, future compiled-pack support, and removing any leftover historical compatibility language from older docs.

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
- Added a Runtime-owned command-context seam over `ISimulationContext` for command-stage tick ownership; this has since been split into explicit clock and target roles.
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
- Runtime still has a direct `TheSadRogue.Primitives` package reference for internal command/snapshot/world geometry implementation details; public Runtime session ports now use Contracts runtime geometry primitives instead of exposing SadRogue types.
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
