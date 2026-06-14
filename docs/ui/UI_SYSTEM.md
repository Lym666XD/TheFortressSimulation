# UI System

Updated: 2026-06-13
Status: current implementation map plus target boundary notes

This is the active UI, input, and rendering map for the current App code. It
reconciles the target UI documents with the implementation that exists today.

## Scope

The current UI is still App-owned. SadConsole surfaces, input routing, map
interaction, placement tools, debug overlays, and runtime-facing commands are
assembled in `src/HumanFortress.App`.

The intended long-term boundary remains:

```text
Player input -> App UI state -> simulation commands
Simulation state -> immutable/query snapshots -> App rendering
```

The implementation is partway there. Input commands mostly enter through
explicit App routers and command factory helpers. Rendering has a
`RenderSnapshot` path, but the main map and many panels still read live
simulation objects.

## Current Entry Points

- `FortressFrameRenderer` composes one frame. It renders the map, UI overlay,
  and tile-inspection popup.
- `FortressMapRenderer` renders terrain and entities from live `World` data.
- `FortressUiOverlayRenderer` draws top bar, dock, drawers, quick menus, map
  overlays, placement previews, debug pages, and modal UI.
- `UiStore` owns transient UI state such as context, drawer state, quick menu,
  selected tools, placement corners, debug pages, toasts, and highlights.
- `FortressUiServices` groups feature UI helpers such as orders, zones,
  build, and stockpile UI.

`FortressRuntimeAccess` is the current App-facing runtime facade. It prevents
most UI code from reaching directly into `GameStateManager`, but it still
exposes live world/runtime objects:

- `World`
- `NavManager`
- concrete job systems
- profession assignments
- scheduler tunings
- recipe and construction catalogs
- debug job data
- command enqueue helpers
- simulation pause and speed controls

This is a useful transition layer, not the final UI/runtime boundary.

## Input Flow

`InputBindingsService` loads `content/registries/input.bindings.json` through
`FortressContentLoader.ResolveRegistryFile(...)`. It currently provides a small
named-context lookup used by menu rendering and some key handling.

Most input behavior is still routed by code:

- `FortressKeyboardInputRouter` coordinates workshop-panel keyboard input,
  construction material dialog input, camera and Z navigation, simulation
  pause/speed controls, help/debug shortcuts, drawers, navigation debug, and
  context-specific UI input.
- `FortressMouseInputRouter` handles focus, hover updates, screen-space dock
  and quick-menu clicks, map clicks, and right-click cancel behavior.
- `FortressMapClickController` resolves workshop clicks, zone detail clicks,
  stockpile clicks, and fallback tile inspection by reading current world data.
- `FortressPlacementRouter` dispatches placement clicks to stockpile, hauling,
  mining, construction, buildable workshop, zone create/delete, and stockpile
  copy controllers.

Simulation-affecting UI actions should enqueue commands instead of mutating
simulation state directly. Current placement code generally goes through
`FortressPlacementCommandFactory` and `FortressRuntimeAccess.EnqueueCurrentTickCommand(...)`.

## Rendering Flow

Current rendering is mixed:

- Main terrain rendering reads live `World` chunks and tiles in
  `FortressMapRenderer`.
- Entity rendering also reads current world data.
- Navigation overlays render from the live navigation/world state.
- UI drawers and debug pages often read live `World` and
  `FortressRuntimeAccess`.
- Workshop overlays can use `RenderSnapshot` when
  `OverlayFromSnapshot` is enabled.

`FortressRenderSnapshotService` builds snapshots using
`RenderSnapshotBuilder`, camera position, current Z, and viewport size.
`RenderSnapshotBuilder` lives in `src/HumanFortress.Simulation/Rendering` and
builds immutable visible-chunk data plus a workshop overlay slice.

The snapshot path is real but not yet the main rendering boundary. Treat
[Rendering Snapshot](RENDERING_SNAPSHOT.md) as the target contract, not a
description of complete current behavior.

## Known Live Reads

The following UI areas still depend on live simulation state and should be
converted gradually to query facades or immutable snapshots:

- map terrain and entity drawing;
- tile inspection and debug pages;
- work, jobs, workshop, construction, stockpile, and zone drawers;
- placement validation and previews;
- workshop click detection and detail panels;
- navigation debug overlays.

These reads are acceptable for the current playable prototype, but they keep UI
and simulation state coupled. New UI features should avoid widening this
surface.

## Current Rules For New UI Work

- Keep transient UI state in `UiStore` or feature UI helpers.
- Route player actions through input routers or feature controllers.
- Enqueue simulation-affecting actions as commands.
- Use `FortressRuntimeAccess` only as a transitional facade.
- Prefer content registries and catalogs over hard-coded UI data.
- Do not introduce new direct `GameStateManager` dependencies in UI code.
- If a UI feature needs simulation reads, isolate them behind a small query or
  snapshot-shaped helper so the dependency can be moved later.

## Target Direction

The next architecture step is not another input rewrite. It is to define
stable read models for the UI:

- map render snapshot for terrain, entities, placeables, overlays, and
  navigation visualization;
- work/jobs summary snapshot for drawers and debug views;
- stockpile/zone/workshop query snapshots for feature panels;
- placement validation query APIs that do not expose mutable world state;
- command-only simulation write path from UI.

Once those exist, `FortressRuntimeAccess` can shrink to command submission,
simulation status, and snapshot/query handles.

## Related Documents

- [Controls](CONTROLS.md) is the current player-facing control summary.
- [UI And Input Model](UI_AND_INPUT_MODEL.md) describes the target MVU-style
  UI/input model.
- [Input Spec](INPUT_SPEC.md) describes target input contexts and actions.
- [UI Spec](UI_SPEC.md) describes target SadConsole UI layout and interaction.
- [Rendering Snapshot](RENDERING_SNAPSHOT.md) describes the target immutable
  rendering contract.

