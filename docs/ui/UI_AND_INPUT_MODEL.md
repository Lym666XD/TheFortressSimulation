id: ui.input.model.v1
status: normative
owner: app/ui
last_updated: 2025-09-15

Current implementation note (2026-07-09):

- This document describes the target UI/input architecture.
- Current SadConsole UI has `UiStore`, input services, drawers, quick menus, overlays, and placement tools.
- Active fortress map rendering, overlays, drawers, debug pages, workshop/stockpile/zone panels, tile inspection, and placement previews now consume Runtime/Contracts snapshot or query DTOs instead of live `World`, concrete job systems, or direct `ContentRegistry` reads.
- The "snapshot-only" rule below is now the normal active fortress read path, and App.Rendering now consumes Runtime-authored map-viewport and UI-overlay section deltas through presenter caches. The broader MVU model, virtualized large lists, packed presenter deltas beyond the current map/overlay slices, panel-specific redraw skipping, and richer reducer/effect structure remain target work.

applies_to:
  - SadConsole UI (panels, overlays, tooltips)
  - Input mapping & deterministic command queue
  - Data flow between Engine snapshot → UI
  - List/grid virtualization & diffing
goals:
  - Deterministic, testable UI and input → command pipeline.
  - Smooth UX with very large datasets (stockpiles, alerts) via virtualization.
  - Decoupled, hot-reload-friendly view layer; no sim logic in UI.
references:
  - GAME_ARCHITECTURE.md (App, Rendering, SnapshotBuilder)
  - SAVE_FORMAT.md (string IDs, signatures)
  - SIM_LOD_POLICY.md (camera/focus pins)
  - DETERMINISM_CI.md (replay/checkpoints)

0) Architectural Choice (MVU-first, hybrid where needed)
MVU core: a single UiStore holds the immutable UI model; user interactions dispatch UiMsg; pure Reducer functions produce the next model; View renders from model (and engine snapshot).

Hybrid allowance: complex forms (e.g., multi-field dialogs) may wrap Presenter helpers, but must still emit UiMsg and keep state in UiStore.

No business rules in View: View is pure; side effects (sound, clipboard) run via Effect middleware (see §5.3).

1) Input → Command Pipeline (Deterministic)
1.1 Devices → Actions
InputDeviceEvent (keyboard/mouse/gamepad) → Action via Bindings (data-driven, rebindable).

All device specifics are normalized (e.g., platform key codes → logical keys).

1.2 Actions → Commands
CommandMapper turns Action + current UI context into ICommand records stamped with tick.

Commands are domain-intent, e.g.: SelectTile(x,y,z), PlaceBlueprint(id, rot), OpenPanel(P_Stockpile), IssueOrder(DesignateMine, area).

1.3 Deterministic Queue
CommandQueue is the only bridge from UI to simulation.

Properties:

FIFO by tick then insertion index.

Stable serialization (used by replay & CI).

Bounded per tick (max_commands_per_tick), overflow drops lowest-priority UI-only commands deterministically (never sim-affecting ones).

Interfaces

csharp
Copy code
interface ICommand { string kind; ulong tick; ReadOnlyMemory<byte> payload; }
interface ICommandQueue { void Enqueue(ICommand cmd); bool TryDequeue(ulong tick, out ICommand cmd); }
2) Engine Snapshot → UI (Read-only)
The renderer builds an immutable RenderSnapshot each tick.

UI reads:

RenderSnapshot for map/actors/items rendering;

UiQuery (read-only adapters over engine indices: IdMap, TagIndex, ReverseRecipeIndex);

UiServices (time, localization, formatting).

No authoritative mutation is allowed from UI; only ICommand goes back to Engine.

3) UiStore, Messages, Reducers (MVU Core)
3.1 UiStore (single source of truth)
Example (trimmed):

csharp
Copy code
record UiModel {
  PanelState Panels;
  SelectionState Selection;
  InspectState Inspect;
  ListState StockpileList;
  AlertsState Alerts;
  TooltipState Tooltip;
  CameraState Camera; // mirrors but not drives engine camera; engine follows upon command
  Preferences Prefs;  // not part of determinism/replay; saved separately
}
3.2 UiMsg (union of messages)
Examples: Msg.OpenPanel(id), Msg.Scroll(delta), Msg.FilterChanged(key,val), Msg.HoverTile(x,y,z), Msg.VirtualListWindowChanged(key, first, count).

3.3 Reducers (pure)
UiModel Reduce(UiModel m, UiMsg msg) must be pure & deterministic (no RNG, no wall clock).

Reducers are composable: feature reducers operate on their slice and are combined by CombineReducers.

3.4 Selectors (derived view models)
Pure functions that compute projections (e.g., filtered + sorted stockpile rows).

Must use stable sort keys and cached memoization keyed by inputs.

4) Virtualization (Lists, Grids, Trees)
4.1 Contract
csharp
Copy code
interface IVirtualizedList<T> {
  string key;                      // stable list identity
  int Count { get; }               // total items
  void SetWindow(int first, int count);
  IReadOnlyList<T> FetchWindow();  // returns items [first, first+count)
}
The Provider feeds data lazily from UiQuery/RenderSnapshot or prebuilt indices.

Items must have a stable key (itemKey) used for diffing.

4.2 Diffing & Rendering
Use a stable keyed diff (O(n)) within the window; avoid full list re-renders.

Rows render only when:

itemKey enters/leaves the window,

any displayed field changed (compare by version/hash).

4.3 Patterns
Stockpile/Inventory: By default, group by item id + material, aggregate count/volume; expand groups on demand.

Alerts/Log: Virtualized with time-bucket folding and filterable levels.

Zone lists: Paginate, not eager expand.

4.4 Performance Targets
10k items list: initial paint ≤ 8 ms (95p), scroll update ≤ 2 ms (95p).

UI thread budget per frame ≤ 4 ms on mid-spec CPU.

5) UI Middleware (Effects, Tooltips, Shortcuts)
5.1 Effects
Side effects (play sound, clipboard, open URL) are performed by Effect middleware that observes UiMsg and Reducer outputs.

Effects run after reducers; they must not enqueue sim-affecting commands (only UI feedback).

5.2 Tooltips & Inspect
TooltipService gathers tooltip data via UiQuery on hover; throttled (e.g., 80–120 ms) but based on tick, not wall clock.

Inspect panel subscribes to SelectionState; no direct engine reads.

5.3 Shortcuts & Chords
Multi-key chords are stateful in the UiStore (e.g., G then M for “Go to Mine”); timeout measured in ticks.

6) Panels & Navigation
Panels are state machines: Closed → Opening → Open → Closing.

Only one primary panel; others are secondary/overlay.

Panel transitions emit UiMsg and can pin chunks via LodService.Pin(chunk, ttl) when deep-inspecting (data-driven TTL).

7) Rendering & Layout (SadConsole)
Tile-first: Map renders before UI; UI panels draw in z-layers with clipping.

Virtualized text: VirtualTextBuffer wraps SadConsole surfaces to avoid reflow cost.

Theme: colors/fonts are in /content/registries/tuning.ui.json; no hard-coded color constants in code.

Scaling: DPI & font scaling are preferences; snapshot glyph metrics come from tileset registry to avoid drift.

8) Localization & Accessibility
Strings are ID-based (ui.panel.stockpile.title), pulled from LocService.

Font fallback for CJK; width cache (full/half width) for alignment.

Keyboard-only navigation path for all panels; focus ring visible.

Color-blind safe palette variants; configurable in tuning file.

9) Threading & Safety
UI runs on the main thread; all interactions with SadConsole must happen there.

RenderSnapshot is immutable and shared read-only.

UiStore updates are atomic; reducers run to completion within frame budget.

Background tasks (e.g., icon atlas preload) marshal results to main thread via UiMsg.AsyncReady.

10) Replay & CI
CommandQueue is fully serializable; replay feeds commands at recorded tick.

CI checks that for a given replay, UI model hashes at checkpoints (optional) are identical across OS/thread counts.

Virtualized lists must not depend on wall clock or “randomized” ordering.

11) Error Handling (UI Boundaries)
Any exception in Reducer/View is caught; show non-blocking toast and auto-recover to last valid UiModel.

Commands that fail validation at the simulation boundary return Rejected(reason); UI displays a banner; the run continues.

12) Data Files (Tuning & Bindings)
/content/registries/tuning.ui.json

panel open/close speeds, tooltip delay (in ticks), virtual list row heights, budgets per frame.

/content/registries/input.bindings.json

device → action bindings; multiple presets; user overrides saved in settings.json.

Schemas: tuning.ui.schema.json, input.bindings.schema.json.

13) Public APIs (Binding)
csharp
Copy code
// Input
interface IActionBindingService { Action? Map(InputDeviceEvent e); }
interface ICommandMapper { ICommand[] Map(Action a, UiModel m); }
interface ICommandQueue { void Enqueue(ICommand cmd); }

// MVU
record UiModel { /* ... */ }
interface IReducer { UiModel Reduce(UiModel m, UiMsg msg); }
interface IUiStore {
  UiModel Model { get; }
  void Dispatch(UiMsg msg);
  event Action<UiModel> OnChanged; // for views
}

// Virtualization
interface IVirtualizedList<T> { /* see §4.1 */ }

// Queries
interface IUiQuery {
  IEnumerable<ItemRow> EnumerateItems(Filter f, Sort s);
  ItemRow? TryGetByKey(ItemKey k);
  // similar for recipes/zones/alerts...
}
14) Determinism Requirements (Hard)
Stable sort keys for all lists (id, creationTick as tiebreaker).

No DateTime.Now, frame time deltas, or non-deterministic RNG in reducers/selectors.

All throttles/debounces expressed in ticks, not milliseconds.

CommandMapper must be pure given (Action, UiModel); no hidden state.

15) Performance & Budgets
Reducers total ≤ 1.5 ms per frame (95p).

Views paint ≤ 2.5 ms per frame (95p) with 2–3 panels open.

Large stockpile (10k items, grouped) scroll update ≤ 2 ms (95p).

Budgets are enforced; if exceeded, fallback to coarser rendering (reduced row decorations) next frame.

16) Hot Reload
Changing /content/registries/tuning.ui.json or /input.bindings.json triggers a UI-only hot reload; UiStore remains, reducers rebind constants; current panel focus retained.

Renderer tileset/theme updates swap at barrier.

17) Build Quick Menu & Structural Placement (New)

- Quick chord: `C` opens Build menu. L2 submenus: `Z=Structural`, `X=Functional`, `C=Workshop`, `V=Civil Furniture`, `F=Utility Furniture`.
- Structural (L3): `Z=Wall`, `X=Floor`, `C=Ramp`, `V=Stairs (WIP)`.
- Placement state machine: `ConstructionFirstCorner → ConstructionSecondCorner` on current Z only (multi‑Z stairs reserved).
- After second corner, UI posts `orders.construction.rect` with `{world_rect, z_min=z_max=currentZ, shape, filter}`.
- Ghosts: Read phase places L2 non‑blocking construction ghosts per cell for visualization/claiming; Write phase applies L0 changes.

18) Material Filter (Form‑first)

- Invocation: Optional panel at Structural L3 before confirm; if absent, planner uses last‑used or default material.
- Form‑first: choose required form (e.g., `stone_block` for L0); Material is optional (specific id or tags).
- Memory: `MaterialSelectionService` remembers last choice by `category_key` (`l0.wall/floor/ramp/stairs`).
- L0 behavior (v1): does not consume items; the filter only maps to a geology handle (material+kind) for the terrain look. A future `ConsumeMaterials=true` mode will generate haul lists.

19) Debug Menu & Items Drawer UX (New)

- Debug item spawn now validates against `tile.IsWalkable` instead of `OpenWithFloor`, allowing ramps/stairs tiles.
- Items (F2) drawer shows concrete names; generic resources such as "Boulder/Block/Log/Plank" append material, e.g., `Boulder (Granite)`.
