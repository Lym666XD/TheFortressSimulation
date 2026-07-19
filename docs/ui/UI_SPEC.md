UI_SPEC v1 — HumanFortress (SadConsole)

Current implementation note (2026-07-09):

- This document is a target UI specification with partial implementation.
- Current App UI has `UiStore`, drawers, quick menus, placement controllers, input routers, and Runtime DTO based render/query paths.
- Active fortress rendering and common panel paths now read Runtime/Contracts snapshot DTOs or query DTOs rather than live `World`, concrete job systems, or direct content catalogs.
- Statements below that say views read only `RenderSnapshot` describe the target boundary in old terminology. In current code, the concrete active shapes are Runtime/Contracts snapshot DTOs, while broader packed presenter deltas and full MVU cleanup remain target work.

0) Scope & Goals

Define layout, panels, navigation, and rules for v1 UI.

Primary interaction patterns: bottom-left management dock (F1–F7) + Z/X/C quick menus + bottom drawer panels.

Deterministic MVU: views render from immutable model; user input becomes commands via a queue. (Conforms to our UI/Input model.) 

UI_AND_INPUT_MODEL

1) Global Layout (screen regions)
1.1 Map View (center)

Renders world tiles first; UI layers draw above with clipping.

No business logic in views; reads RenderSnapshot only. 

UI_AND_INPUT_MODEL

1.2 Top-Left “Alerts”

Shows ALERT and new messages only (compact, stacked).

Clicking opens F7: Log/History drawer at the relevant tab.

1.3 Bottom-Left Management Dock

A row of small icon buttons (16–24px glyphs) anchored to bottom-left:

F1 Creature Mgmt

F2 Stock Mgmt

F3 Work Mgmt

F4 Military Mgmt

F5 Country Mgmt

F6 World Map / Diplomacy

F7 Log / Messages / History

Tooltip on hover (title + key).

Activation: click or press F# → opens a Bottom Drawer Panel (see §2).

1.4 Bottom Drawer Panels (primary panels)

Appear from bottom edge; default width 100%, height ~40–60% of viewport.

Tabs across top edge of the drawer; Tab cycles next tab; Shift+Tab cycles previous.

Drawer states: Closed → Opening → Open → Closing (animated; speeds from tuning). 

UI_AND_INPUT_MODEL

Only one primary drawer open at a time; opening another closes the current.

1.5 Inspect / Tooltip (secondary overlays)

Hover tooltip throttled by ticks; inspect panel follows selection—no engine writes. 

UI_AND_INPUT_MODEL

2) Management Drawers (F1–F7) — content & tabs

All drawers: Tab cycles tabs; Right-Click closes current submenu/tool; ESC backs up one level or closes the drawer.
If you dont know how to build sub page, leave them blank.

F1 Creature Management

Tabs:

Population (list; group by role/faction; virtualized)

Needs/Health (filterable; indicators)

Schedule/Policies (readonly for v1; editing later)
Virtualization targets for big lists. 

UI_AND_INPUT_MODEL

F2 Stock Management

Tabs:

Inventory (group by item+material; aggregate counts; expandable groups)

Stockpiles (zones list, priority, links—view/edit when available)

Flows (v2) (logistics heat/throughput overlay toggle)
Large lists must meet perf budgets. 

UI_AND_INPUT_MODEL

F3 Work Management

Tabs:

Priorities (per pawn × work type table; v1 view + basic edit)

Schedules (hour grid; v1 view-only is acceptable)

Work Orders (v2)

F4 Military Management

Tabs:

Squads (compose/assign)

Loadouts (preset equipment)

Missions/Alerts

F5 Country Management

Tabs:

Nobles/Roles

Rooms/Requirements

Economy/Policies (v2)

F6 World Map / Diplomacy

Tabs:

Map (world nodes; read-only)

Factions (standing/relations)

Treaties (v2)

F7 Log / Messages / History

Tabs:

Recent (newest first; foldable by category)

All (virtualized, filters)

Rules/Filters (mute levels/categories)

All heavy tables/lists must use windowed virtualization and stable keys; no wall-clock dependencies. 

UI_AND_INPUT_MODEL

3) Quick Menus (Z/X/C) — bottom toolstrips
3.1 Orders (press Z)

Opens a compact toolstrip (or mini drawer) with categories:

Mining: dig / dig stairs / dig ramp / channel / remove digging; , = clear selections/designations (NOT cancel tool).

Lumbering: chop / , clear

Gather: gather plants / remove plant / , clear

Masonry: smooth / engrave / carve track (transport) / carve fortification

Creature: hunt / kill (enemy by default) / tame / rescue / , clear

Haul: haul / emergency haul / , clear

Other: lock/disallow / unlock/allow / dump / undump / mark-melt / unmark-melt / clean / , clear

Debug: move-here (self-faction)

Rules:

After choosing a tool, single-place only (no chain/brush in v1).

Right-Click = cancel current tool (keeps the quick menu open).

ESC backs to Orders root; ESC again closes toolstrip.

3.2 Zones (press X)

Categories (each supports , Remove Zone):

Logistics: stockpile / garbage dump / , remove

Production: lumber zone / fruit&veg / fishing / sand&clay / water / pit&pond / pen&pasture&training / , remove

Civil: bedroom (single/multi) / dormitory / dining hall / bathroom / shower room / tomb / , remove

Public: throne/parliament / plaza / temple / tavern/inn / office / library / guildhall / hospital / , remove

Military: barracks / archery range / melee training / arena / , remove

Management: burrow / restricted traffic area / , remove

3.3 Build / Construction (press C)

Structural: wall / floor / ramp / stairs / road / dirt road / fortification / pillar / , remove structure

Mechanisms/Devices: door / hatch / …

Workshops (grouped pages):

Mining: miner workshop / metallurgy workshop / alkali kiln / lime concrete yard

Lumbering: lumber hut

Farming: brewery / butchery / pasture shed / tanning / kitchen / compost

Industry: stoneworks / woodworks / metalworks / glassworks / pottery / chemistry lab / paper mill / tailoring

Crafts: craftworks / precision works / firearms workshop / alchemy / enchanter

Furniture

Civil: bed / chair / table / chest / cabinet / statue / bookcase / display

Utility: basin / toilet / bathtub / burial / slab / altar / brazier

Placement rule (v1): click places one and returns to build root; no shift-chain yet.

4) Overlays & Debug

F9 cycles overlays (walk/stand, components/regions, ramp direction, fluids, hazards… sequence defined in tuning).

/ or ~ toggles Debug menu (spawn/move etc., minimal v1).

Overlay name appears in a small label near the bottom-right corner of the map.

5) Navigation & Focus Rules

Right-Click = cancel current tool / close current submenu; stays inside the current drawer unless at root.

ESC = back up one level; from drawer root → closes drawer; from no drawer → opens Main Menu.

Tab cycles drawer tabs; Shift+Tab cycles backwards.

Dock F1–F7: pressing an already-open F# toggles drawer close.

6) Visual & Layout Metrics (SadConsole)

Tile-first rendering; UI panels draw in z-ordered layers with clipping. 

UI_AND_INPUT_MODEL

Drawer size: height 40–60% of viewport; full width; content area uses virtualized lists/grids where applicable. 

UI_AND_INPUT_MODEL

Theme: colors/fonts pulled from /content/registries/tuning.ui.json; no hard-coded color constants. 

UI_AND_INPUT_MODEL

DPI/Font scaling via preferences; icons are atlas-backed.

7) Determinism & Performance (UI contracts)

MVU store + reducers are pure/deterministic; no wall-clock in reducers/selectors; throttles by ticks. 

UI_AND_INPUT_MODEL

CommandQueue is the sole bridge to sim; FIFO per tick; serializable (replay). 

UI_AND_INPUT_MODEL

Virtualization required for creature/stock/log lists; perf budgets from the model (e.g., 10k rows). 

UI_AND_INPUT_MODEL

8) Accessibility & Localization

All strings are ID-based (e.g., ui.mgmt.creature.title).

Keyboard-only path for every drawer; visible focus ring.

CJK font fallback and color-blind palette variants. 

UI_AND_INPUT_MODEL

9) Acceptance Criteria (v1)

Bottom-left dock shows 7 icons (F1–F7), each opens a bottom drawer with tabs; Tab cycles tabs.

Top-left shows only Alert + latest messages; history is in F7.

Right-Click cancels current tool/close submenu; ESC steps back/close drawer/open main menu at top level.

Z/X/C open Orders/Zones/Build toolstrips; inside, tools map as listed; , clears designations/selections (not cancel tool).

Single-place placement (no chain) for v1.

F9 cycles overlays; `/~ toggles debug; Space, -, = control time.

Lists in F1/F2/F7 use virtualization and remain responsive in large data sets.
