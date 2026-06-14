INPUT_SPEC v1 — HumanFortress (SadConsole)

Current implementation note (2026-06-12):

- This document is a target input specification with partial implementation.
- `InputBindingsService` currently loads `content/registries/input.bindings.json` through the Content resolver.
- Actual keyboard/mouse handling is still split across App runtime routers and UI component handlers.
- UI commands should enqueue runtime commands for simulation effects; some UI-only interactions remain local to `UiStore`.

0) Scope

Define how device events (keyboard/mouse) become Actions → Commands in our MVU pipeline; specify contexts, bindings file, mouse hover & clicks, cancel/back semantics, and WIP toasts. Deterministic and data-driven per our UI/Input model. 

UI_AND_INPUT_MODEL

1) Deterministic Pipeline (contract)

Devices → Actions via data-driven bindings (rebindable). Normalize platform key codes first. 

UI_AND_INPUT_MODEL

Actions → Commands: ICommand records stamped with tick; pushed to CommandQueue (only ingress to sim). FIFO, serializable, bounded. 

UI_AND_INPUT_MODEL

UI reads RenderSnapshot/UiQuery only; no sim mutation except via commands. 

UI_AND_INPUT_MODEL

2) Binding Files (data-driven)

Path: /content/registries/input.bindings.json (+ schema input.bindings.schema.json).

Allows multiple presets; user overrides saved in settings.json. Hot-reload supported. 

UI_AND_INPUT_MODEL

MVU store + reducers + selectors must remain pure/deterministic; all throttles in ticks, not ms. 

UI_AND_INPUT_MODEL

2.1 Logical keys & mouse

Logical keys: Z,X,C,V,F,G,R,T, F1..F9, Slash, Tilde, Question, Space, Minus, Equals, Escape, Tab, Shift+Tab, arrows, wheel.
Mouse: MouseMove, MouseLeft, MouseRight, Wheel.

3) Contexts

Bindings resolve by UI context before mapping actions:

global — always active (pause, speed, F1–F7, F9 overlays, open Z/X/C quick menus).

menu.orders, menu.zones, menu.build — quick menus open; listen to secondary keys (z x c v f g r t ,).

placing.* — a tool is active (place one; right-click cancels).

drawer.* — management drawers (F1–F7) are open; Tab cycles tabs.

(Implementation detail: UiStore holds UiContext enum; reducers switch contexts.) 

UI_AND_INPUT_MODEL

4) Mouse rules (new)
4.1 Hover → highlight the tile under cursor

Requirement: the map cursor follows the mouse; the hovered tile is visibly highlighted (same style as initial Fortress cursor).

On MouseMove over Map Surface:

Hit-test console cell → world (x,y,z) using camera/snapshot transforms.

Dispatch Msg.HoverTile(x,y,z); reducer updates Selection.Hover. (Throttled by tick, not wall clock; e.g., every N ticks.) 

UI_AND_INPUT_MODEL

Action/Command: usually UI-only; no sim command unless a tool requires live preview.

4.2 Clicks

Left-click:

On an icon/button → emit Action.UiClick(id) → OpenPanel(...) or open quick menu, or pick sub-tool (see §6–§7).

On map with a tool active → PlaceBlueprint(id, rot); single-place then return to tool root. (No chain placement in v1.)

Right-click = cancel current tool / close current submenu (does not clear designations). Emits Action.Cancel → Cmd.UiCancel.

Wheel: pass to camera Z or list scroll depending on focus (unchanged).

5) Keyboard (summary)

Global:
Space pause, -/_ slower, = / + faster; F9 cycle overlays; `/~ debug; ? key help; ESC back/open main menu; Right-Click cancel tool/close submenu.
F1..F7 open bottom drawer pages; Z/X/C open quick menus (Orders/Zones/Build).

In quick menus: z x c v f g r t pick sub-tool; , = clear current designations/selection (not cancel tool).

In drawers: Tab/Shift+Tab cycle tabs; ESC closes drawer.

(Use input.bindings.json to store these defaults; rebind allowed.) 

UI_AND_INPUT_MODEL

6) Bottom-left Management Dock (F1–F7) — clickable

Each icon is a button with tooltip (title + [F#]).

Keyboard: press F# → Action.OpenPanel(panelId) → Cmd.OpenPanel(...).

Mouse: click icon does the same. One primary drawer at a time. 

UI_AND_INPUT_MODEL

Drawer is a bottom drawer with tabs; Tab cycles; Right-Click closes current submenu; ESC backs/close.

7) Bottom-center Quick Launch (Z/X/C) — clickable

Three small round icons near bottom center: Orders (Z), Zones (X), Build (C).

Keyboard: Z/X/C open respective quick menu.

Mouse: clicking the icon triggers the same Action.OpenQuickMenu(kind).

Inside quick menu:

Press secondary key or click the tool button to select tool.

Right-Click cancels current tool (keeps quick menu open); ESC backs to quick menu root → ESC again closes.

8) , (comma) — Clear Designations (critical semantics)

In any designation context (mining/lumbering/gather/zones/etc.), , = clear all current selections & marked tiles of that mode.

Emits Action.ClearDesignations(kind) → Cmd.ClearDesignations(kind, area | currentSelection).

This does not act like cancel; Right-Click remains the cancel tool. (Both keys can coexist—no conflicts.)

9) Unimplemented (WIP) buttons → Toast

Clicking a WIP button should show a non-blocking toast (e.g., “This feature is coming soon”) and auto-dismiss after N ticks (configurable in tuning.ui.json). Use Effect middleware to display, not reducers. 

UI_AND_INPUT_MODEL

Toasts are already part of our error/UI boundary policy (non-crashing). Reuse that path. 

UI_AND_INPUT_MODEL

10) Default bindings (excerpt)

/content/registries/input.bindings.json (trimmed):

{
  "version": 1,
  "presets": {
    "default": {
      "global": {
        "Space": "sim.toggle_pause",
        "Minus": "sim.speed_down",
        "Equals": "sim.speed_up",
        "F9": "overlay.cycle",
        "Slash": "debug.toggle",
        "Tilde": "debug.toggle",
        "Question": "help.show",
        "Escape": "ui.back",
        "MouseRight": "ui.cancel",
        "F1": "ui.open_panel.creature",
        "F2": "ui.open_panel.stock",
        "F3": "ui.open_panel.work",
        "F4": "ui.open_panel.military",
        "F5": "ui.open_panel.country",
        "F6": "ui.open_panel.world",
        "F7": "ui.open_panel.log",
        "Z": "ui.open_menu.orders",
        "X": "ui.open_menu.zones",
        "C": "ui.open_menu.build"
      },

      "menu.orders": {
        "Z": "orders.select.mining",
        "X": "orders.select.lumbering",
        "C": "orders.select.gather",
        "V": "orders.select.masonry",
        "B": "orders.select.creature",
        "N": "orders.select.haul",
        "G": "orders.select.other",
        ",": "orders.designations.clear"
      },

      "menu.zones.civil": {
        "Z": "zones.civil.bedroom",
        "X": "zones.civil.dormitory",
        "C": "zones.civil.dining",
        "V": "zones.civil.bathroom",
        "F": "zones.civil.showers",
        "G": "zones.civil.tomb",
        ",": "zones.remove"
      }
    }
  }
}

shortkeys

orders(z)
 	-mining order (z)
 		--dig(z)
 		--dig stairwell(x)
 		--dig ramp(c)
 		--dig channel(v)
 		--remove digging(f)
 		--cancel order(,)
 	-lumbering order (x)
 		-- lumber(z)
 		--cancel order(,)
 	-gather order(c)
 		--gather plant(z)
 		--remove plant(x)
 		--cancel order(,)
 	-mansonry order(v)
 		--smooth(z)
 		--engrave(x)
 		--track(c)
 		--carve gap(v)
 		--cancel order(,)
 	-haul order(f)
 		--haul(z)
 		--emergency haul(x)
 		--cancel order(,)

 	-creature order(v)
 		--hunting(z)
 		--kill(x)
 		--tame(c)
 		--rescue(v)
 		--cancel order(,)
 	-other order(g)
 		--lock/disallow(z)
 		--unlock/allow(x)
 		--dump(c)
 		--remove dump(v)
 		--melt(f)
 		--remove melt(t)
 		--clean(r)
 		--cancel order(,)
 	-debug order(/)
 		--debug move(z) all self faction creatures move to this place.
 	-cancel order(,)
zones(x)
 	-production zone(z)
 		--lumbering zone(z)
 		--gathering fruit/veg zone(x)
 		--fishing zone(c)
 		--gathering sand/clay zone(v)
 		--water zone(f)
 		--pit/pond(g)
 		--Pen/Pasture/animal training(r)
 		--remove zone(,)
 	-civil zone(x)
 		--bedroom(z)（single/multi）
 		--dormitory(x)
 		--dining hall(c)
 		--bathroom(v)
 		--shower room(f)
 		--tomb(g)
 		--remove zone(,)
 	-public zone(c)
 		--throne room/parliament room(z)
 		--plaza(x) (former meeting area)
 		--temple (c)
 		--tavern/inn(v)
 		--office(f)
 		--library(g)
 		--guildhall(r)
 		--hospital(t)
 		--remove zone(,)
 	-military zone(v)
 		--barracks(z)
 		--archery range(x)
 		--chivalry training(c)
 		--arena(v)
 		--remove zone(,)
 	-management zone(f)
 		--burrow zone(z)
 		--banning traffic area(x)
 		--remove zone(,)


construction(c)
 	-structural(z)
 		--wall(z)
 		--floor(x)
 		--ramp(c)
 		--stair(v)
 		--road(f)
 		--dirt road(g)
 		--fortification(r)
 		--pillar(t)(former support)

 		--remove structure(,)
 	-functional structure(x)
 		--door(x)
 		--hatch(c)
 		--

 	-workshop(c)
 		--mining(z)
 			---miner workshop(z)
 			---metallurgy workshop(x)
 			---fuel alkali work(c)
 			---lime concrete yard(v)
 		--lumbering(v)
 			---lumber hut(z)
 		--farming(c)
 			---agri-brew workshop(z)
 			---butchery(x)
 			---pasture shed(c)
 			---tanning(v)
 			---kitchen(f)
 			---compost(g)

 		--industry(x)
 			---stoneworks(z)
 			---woodworks(x)
 			---metalworks(c)
 			---glass house(v)
 			---pottery(f)
 			---chemistry lab(g)
 			---paperwork(r)
 			---tailoring(t)
 		--crafts(f)
 			---craft works(z)
 			---precise works(x)
 			---firearm(c)
 			---alchemy(v)
 			---enchanter(f)
 	-civil furniture(v)
 		--bed(z)
 		--chair(x)
 		--table(c)
 		--chest(v)
 		--cabinet(f)
 		--statue(g)
 		--bookcase(r)
 		--display(t)
 	-utility furniture(f)
 		--basin(z)
 		--toilet(x)
 		--bathtub(c)
 		--burial(v)
 		--slab(f)
 		--altar(g)
 		--brazier(r)
 	-civil/diplomacy(g)
-military and defence(t)
Stockpiles(v)
 	-stockpile (z)
 		--stockpile(z)
 		--garbage dump(x)
 
 		--remove zone(,)




(Other sub-contexts mirror your menu tree. Hot-reload respected.) 

UI_AND_INPUT_MODEL

11) Command mapping (examples)

ui.open_panel.stock → OpenPanel(P_StockMgmt) (drawer).

ui.open_menu.orders → OpenQuickMenu(Q_Orders).

orders.mining.dig (click or press) → BeginTool(DesignateMine).

MouseLeft on map while DesignateMine active → PlaceBlueprint(DesignateMine, cell).

orders.designations.clear (,) → ClearDesignations(DesignateKind, selection).

ui.cancel (MouseRight) → CancelToolOrCloseSubmenu().

overlay.cycle (F9) → CycleOverlay().

All commands are enqueued with current tick into CommandQueue and replayable in CI. 

UI_AND_INPUT_MODEL

12) Focus & Back/Cancel rules

Right-Click: cancel current tool or close current submenu (does not clear designations).

ESC: step back one level; from drawer root → close drawer; from no drawer/tool → open Main Menu.

Tab/Shift+Tab in drawers: cycle tabs.

Pressing an already-open F# toggles that drawer closed.

13) Performance & determinism notes

Hover/tooltip/throttle measured in ticks, not milliseconds. 

UI_AND_INPUT_MODEL

Large lists (F1/F2/F7) must use virtualization and meet UI budgets. 

UI_AND_INPUT_MODEL

Render loop consumes RenderSnapshot; input path never directly mutates sim. 

GAME_STATE_FLOW

14) Acceptance checklist (v1)

Mouse hover highlights the tile under cursor (cursor tracks mouse over map). Msg.HoverTile updates UI; visible highlight updates each tick. 

UI_AND_INPUT_MODEL

F1–F7 dock icons are clickable and open bottom drawers with tabs; Tab cycles; Right-Click/ESC behave as specified. 

UI_AND_INPUT_MODEL

Z/X/C quick-launch icons are clickable; inside quick menus, tools are clickable; Right-Click cancels tool; , clears designations.

WIP buttons show a toast for a few seconds (ticks), then auto-dismiss; does not crash or block input. 

UI_AND_INPUT_MODEL

All inputs route Device → Action → ICommand and into the deterministic queue; no direct sim mutation from UI. 

UI_AND_INPUT_MODEL
