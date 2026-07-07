# UI System

Updated: 2026-06-25
Status: current implementation map plus target boundary notes

This is the active UI, input, and rendering map for the current App code. It
reconciles the target UI documents with the implementation that exists today.

## Scope

The current UI is still App-owned. SadConsole surfaces, input routing, map
interaction, placement tools, debug overlays, and runtime-facing commands are
assembled in `src/HumanFortress.App`, split across App-owned `GameStates`,
`Session`, `Input`, `Rendering`, and `UI` namespaces.

The intended long-term boundary remains:

```text
Player input -> App UI state -> simulation commands
Simulation state -> immutable/query snapshots -> App rendering
```

The active fortress implementation is now Runtime DTO/query based for map
rendering, overlays, drawers, debug pages, workshop panels, tile inspection, and
placement previews. Input commands enter through explicit App routers and
command factory helpers. App command translation maps UI placement intents to
Runtime command intents; Simulation order enum/material DTO conversion happens
inside Runtime command code.

## Current Entry Points

- `FortressFrameRenderer` composes one frame. It renders the map, UI overlay,
  and tile-inspection popup.
- `FortressMapRenderer` renders terrain and entities from
  `SimulationMapViewportData`.
- `FortressUiOverlayRenderer` draws top bar, dock, drawers, quick menus, map
  overlays, placement previews, debug pages, and modal UI.
- `UiStore` owns transient UI state such as context, drawer state, quick menu,
  selected tools, placement corners, debug pages, toasts, and highlights. Its
  implementation is split by UI state domain so navigation/cancel flow,
  drawers, quick menus, placement, build, workshop panel, debug, selection, and
  feedback state can evolve separately.
- `FortressUiServices` groups feature UI helpers such as orders, zones,
  build, and stockpile UI. Orders, zones, and stockpile helpers are split by
  menu, placement, overlay/preview, popup, and drawing-helper concerns.
- Zone menu rendering separates the root/category menu from individual
  third-level zone submenu panels.
- Build UI follows the same pattern: root/submenu rendering, material dialog
  drawing, structural keyboard handling, and workshop category/item selection
  are separate App.UI/App.Input partials.
- `HumanFortress.App.Input` owns keyboard, mouse, placement, map-click,
  navigation-debug, debug-spawn, and workshop-panel input controllers.
  UI component input and screen chrome hit testing are split by event channel
  and feature panel; quick-menu root/submenu hit testing stays in App input
  rather than mixed into a single component or moved below the App boundary.
- `HumanFortress.App.Session` owns fortress session context/load/bootstrap
  state. Loaded-session snapshots are presentation readiness state, not a
  general input dependency bag. World-map and embark screens read generated
  world information through Session query DTOs rather than direct tile-array
  access.

`FortressRuntimeAccess` is the current App-facing runtime facade. It delegates
to `FortressRuntimeSessionController`, which wraps Runtime's
`FortressRuntimeSessionCore` plus the remaining WorldGen/bootstrap adapter work.
This is still a transition layer, but UI-facing methods now return Runtime DTOs,
simulation status, or command enqueue handles instead of live world/runtime
objects. Input/rendering code receives role-specific runtime access interfaces
for render reads, keyboard input, UI input callbacks, placement, map
inspection, debug spawn, workshop panel editing, navigation debug, simulation
controls, and session bootstrap.

## Input Flow

`InputBindingsService` loads `content/registries/input.bindings.json` through
`FortressContentLoader.ResolveRegistryFile(...)`. It currently provides a small
named-context lookup used by menu rendering and some key handling.

Most input behavior is still routed by code:

- `FortressKeyboardInputRouter` coordinates workshop-panel keyboard input,
  construction material dialog input, camera and Z navigation, simulation
  pause/speed controls, help/debug shortcuts, drawers, navigation debug, and
  context-specific UI input. Its context/result records and navigation helper
  are split from the top-level dispatch flow.
- `FortressMouseInputRouter` handles focus, hover updates, screen-space dock
  and quick-menu clicks, map clicks, and right-click cancel behavior. Mouse
  router context records, click handlers, and overlay pass-through/right-click
  behavior are split by event channel.
- `FortressMapClickController` resolves workshop clicks, zone detail clicks,
  stockpile clicks, and fallback tile inspection through Runtime snapshot/query
  DTOs.
- `FortressPlacementRouter` dispatches placement clicks to stockpile, hauling,
  mining, construction, buildable workshop, zone create/delete, and stockpile
  copy controllers.

Simulation-affecting UI actions should enqueue commands instead of mutating
simulation state directly. App input code now maps presentation choices to
Runtime request DTOs and calls semantic runtime facade methods such as
`QueueHaulOrder(...)`, `QueueAdvancedMiningOrder(...)`, or
`QueueCreateStockpile(...)`. Runtime owns concrete command construction and the
command queue boundary.

## Rendering Flow

Current rendering is Runtime DTO based for the active fortress screen:

- Main terrain/entity rendering consumes `SimulationMapViewportData`.
- Frame rendering consumes `SimulationFrameRenderData`.
- Navigation, zone, stockpile, workshop, debug, and placement overlays consume
  Runtime-owned snapshot/query DTOs.
- Navigation overlay rendering, placement overlay preview drawing, placement
  command controllers, chrome drawing, and UI command objects are split by App
  presentation/input concern and still sit above the Runtime facade.
- Orders/build/workshop drawer renderers and quick-menu/root-click handlers are
  split by surface or feature menu inside App.UI/App.Input; they stay out of
  Runtime because they depend on SadConsole, `UiStore`, and transient UI state.
- Runtime snapshot builders are split by read-model family, so App renderers
  receive DTOs from focused navigation/map/workshop/management/stockpile/jobs/
  frame/work/session builders rather than one growing read-side god object.
- App still owns SadConsole drawing, UI state, input routing, and semantic
  command-request orchestration.

The older App `FortressRenderSnapshotService` / `OverlayFromSnapshot` bridge has
been removed. Treat [Rendering Snapshot](RENDERING_SNAPSHOT.md) as the target
contract for future presenter/versioned rendering work, not a description of the
current active App rendering path.

## Remaining Coupling

The active fortress UI no longer reads live `World` or concrete job systems for
map rendering, overlays, drawers, debug pages, stockpile/zone/workshop panels,
or placement previews. Those paths consume Runtime-owned DTOs and query
facades.

The remaining coupling is mostly orchestration:

- App owns `UiStore`, SadConsole surfaces, input routing, and command enqueue
  calls.
- App runtime bootstrap still passes `FortressMap` data into the runtime world
  fill step.
- `FortressRuntimeSessionController` remains the transitional session adapter
  that touches the live runtime session.
- App `Session`/`GameStates` still orchestrate screen flow and fortress-play
  enter/exit, with runtime lifetime delegated through `GameStateRuntimeCoordinator`.
- Placement previews still query Runtime during active drags/highlight redraws,
  but receive DTOs rather than mutable world objects.
- App no longer directly references Jobs, Simulation, or Navigation projects;
  UI and input reach those systems only through Runtime/WorldGen boundaries.

## Current Rules For New UI Work

- Keep transient UI state in `UiStore` or feature UI helpers.
- Route player actions through input routers or feature controllers.
- Enqueue simulation-affecting actions as commands.
- Use the runtime access interfaces only as transitional facades.
- Prefer content registries and catalogs over hard-coded UI data.
- Do not introduce new direct `GameStateManager` dependencies in UI code.
- Keep App UI state on App-owned enums/options; map to Simulation contracts only
  inside Runtime command/query boundaries.
- If a UI feature needs simulation reads, isolate them behind a small query or
  snapshot-shaped helper so the dependency can be moved later.

## Target Direction

The next architecture step is not another input rewrite. It is to harden and
version the Runtime read models now used by the UI:

- map render snapshot for terrain, entities, placeables, overlays, and
  navigation visualization;
- work/jobs summary snapshot for drawers and debug views;
- stockpile/zone/workshop query snapshots for feature panels;
- placement validation query APIs that do not expose mutable world state;
- command-only simulation write path from UI.

As those models stabilize, `FortressRuntimeAccess` can shrink further to
command submission, simulation status, and snapshot/query handles with fewer
transitional composite interfaces.

## Related Documents

- [Controls](CONTROLS.md) is the current player-facing control summary.
- [UI And Input Model](UI_AND_INPUT_MODEL.md) describes the target MVU-style
  UI/input model.
- [Input Spec](INPUT_SPEC.md) describes target input contexts and actions.
- [UI Spec](UI_SPEC.md) describes target SadConsole UI layout and interaction.
- [Rendering Snapshot](RENDERING_SNAPSHOT.md) describes the target immutable
  rendering contract.
