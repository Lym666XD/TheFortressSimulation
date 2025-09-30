ORDERS_SPEC.md — Player Orders & Designations (v1)
id: orders.v1
status: normative
owner: UI/Sim integration
last_updated: 2025-09-30

0) Scope

Defines the data-driven model for player Orders (designations/tools), how the UI maps input → commands, and how the simulation consumes these commands deterministically. v1 includes a minimal Orders root with a Haul tool (rect selection). The spec aligns with UPDATE_ORDER.md, INPUT_SPEC.md, JOB_SCHEDULER_SPEC.md, and HAULING_POLICY.md.

1) Principles

- Data-driven: orders/tools are declared in content registries; key bindings in input.bindings.json.
- Deterministic: UI emits commands tagged with tick; ApplyCommands stage is the only ingress to sim.
- Non-blocking UI: selection and previews are UI-only until a command is posted.
- Chunk-locality: order execution is realized via chunk-parallel plan jobs and single-writer merge/apply.

2) Content & Registries

2.1 Input bindings (content/registries/input.bindings.json)

- Holds presets and per-context key → action mapping. Example (excerpt):
  - context `global`: Z/X/C open quick menus; F1..F7 open drawers.
  - context `menu.orders`:
    - `F` → `orders.select.haul`
    - `Z` → `orders.haul.rect`

2.2 Orders registry (content/registries/orders.registry.json)

- JSON schema (v1 simplified):
```
{
  "version": 1,
  "orders": {
    "haul": {
      "id": "orders.haul",
      "name": "Haul",
      "hotkey": "F",
      "tools": {
        "rect": { "id": "orders.haul.rect", "shape": "rect", "hotkey": "Z" }
      }
    }
  }
}
```

3) UI → Command Flow

3.1 Contexts and menus

- `QuickMenu=Orders` (opened via `Z` in global):
  - Shows data-driven list of order categories/tools from Orders registry, with keycaps from `input.bindings.json`.
  - Selecting Haul (`F`) then tool `Rect` (`Z`) puts UI into PlacingTool context with `PlacementMode.HaulFirstCorner`.

3.2 Rect placement (Haul)

- First click: record first corner; switch to `HaulSecondCorner`.
- Second click: compute rect; enqueue command `orders.haul.rect`:
  - Command payload: worldRect (x,y,w,h), z, priority (default 50).
  - Command tick: current UI tick; command id: GUID.
  - Command executes in ApplyCommands → enqueues designation into OrdersManager.

4) Simulation Ingress (ApplyCommands)

- ICommand: `CreateHaulOrderCommand`
  - Execute(ISimulationContext): `World.Orders.EnqueueHaul(rect,z,priority,tick)`.
  - No world mutation here; OrdersManager is a thread-safe queue.

5) Execution Coupling (Read/Write stages)

- Read: `HaulingSystem` drains bounded number of Haul designations, enumerates items in rect (same Z), selects destination stockpile cells (v1 simplified), and produces `PlannedMove` DTOs.
- Write: `HaulingSystem` hands `PlannedMove` to a job layer. In v1, `HaulJobSystem` in App consumes moves, assigns a worker, and uses Navigation pathing.

6) Determinism

- UI commands: sorted by CommandId within a tick in CommandQueue.
- Planner: iterates chunks/items by stable keys; emits moves deterministically.
- Job execution: tie-breakers by GUID; path seeds derived from GUID pairs; pathfinding uses deterministic A*.

7) Budget & Error Handling

- OrdersManager.DrainHaulDesignations(max N per tick) bounds ingress.
- Planner caps moves per tick; skips if no stockpile/destination; logs at most once per N ticks.
- Failures in job phase (no path) drop the job quietly in v1; later: retries, cooldowns.

8) UI Feedback

- Toast on order creation; Orders quick menu displays all buttons (with WIP placeholders for non-implemented orders).
- Work drawer (F3) tab 2 lists recent haul orders (rect,z); later: active job list.

9) Extension Points (v2)

- Additional orders: mining/lumber/gather with own tools (rect/brush/line).
- Zone-aware filters in Orders registry (e.g., haul only resources).
- Visual overlays for active orders and per-tile states.

