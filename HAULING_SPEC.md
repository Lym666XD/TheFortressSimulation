HAULING_SPEC.md – Hauling Pipeline (v1.1.1 → v2)
id: hauling.v1.1.1
status: normative
owner: sim/economy
last_updated: 2025-09-30

0) Scope

Defines the Hauling pipeline from player orders to execution, with current v1.1.1 implementation and v2 roadmap. This supersedes HAULING_POLICY.md.

1) Principles

- Deterministic planning and execution (stable scans/sorts; seeded pathfinding).
- Read‑parallel propose; write‑serialized commit (v1.1.1); per‑chunk single writer (v2).
- Anti ping‑pong and back‑pressure: filters and budgets avoid useless shuttling and oscillations.
- Data‑driven tuning via registries.

2) Data & Authority

- Authority: `StockpileManager` holds zone definitions (id, name, priority, filter, hysteresis, member chunks).
- Views: per‑chunk `ChunkStockpileData` (zone membership per cell, shard capacity, indices).
- Items: runtime instances (guid, def, material, qty, position, z, flags reserved/carried).
- ReservationManager (v2): central item/destination reservations with TTL/cooldowns and partial quantities (Diff‑backed).

3) Planner (Read)

- Inputs: Haul designations (`OrdersManager`) over world rectangles at Z; active designations included.
- Candidate items: within rect, skip reserved, carried, and items already in any stockpile cell.
- Destination selection: choose nearest accepting zone member cell across chunks and Z (Manhattan dx+dy+dz on shard chunks); prefer stable order.
- Output: `PlannedMove { ItemGuid, From, FromZ, To, ToZ }` enqueued in a stable order to executor inbox.

4) Executor (Write)

- Assignment: deterministic (by creature GUID), HP>0, not busy; cross‑Z allowed; path requests honor diagonal rules from `tuning.navigation.json`.
- Reservation: upon assignment, object‑local `item.IsReserved=true` (migrates to Diff with ReservationManager).
- State machine:
  - ToItem: move to pickup; upon arrival emit `MarkCarried(item, worker)`.
  - ToDest: move to target; upon arrival emit `MoveItem(item→dest)` then `UnmarkCarried(item)`.
- DiffOps: `MoveCreature`, `MarkCarried`, `MoveItem`, `UnmarkCarried` (reserved for Items/L5; creature for L6).
- Backlog: unassigned moves requeued; deterministic ordering.

5) Anti Ping‑Pong & Filters

- Planner excludes items currently located in any stockpile cell (prevents infinite re‑hauling).
- Future: dwell‑time and stickiness scoring via `tuning.hauling.json` (e.g., `dwell_ticks_min`, `stickiness_penalty`).

6) Tuning (registries)

- `content/registries/tuning.navigation.json`: allows diagonals, costs, vertical deltas, budgets.
- `content/registries/tuning.hauling.json`: scoring, capacity, reservations, budgets (to be consumed by Broker/ReservationManager).
- `content/registries/tuning.stockpile.json`: zone hysteresis thresholds and defaults.

7) Broker (v2 integration)

- `StockpileHaulingBroker` generates `CreateHaulJob` diffs (atomic with reservation) using zone pull requests and item availability.
- `StockpileDiffApplicator` applies zone mutations and job‑related diffs (reserve slot, place/remove items into zone views), while Items/L5/Creatures/L6 applicators mutate runtime instances.

8) Concurrency Model & Roadmap

- v1.1.1 (current):
  - Planner: single‑threaded Read; stable deterministic enumeration and DTO emission.
  - Executor: single‑threaded Write; emits DiffOps; runtime applicator updates instances.
  - UI: Work Drawer counters for telemetry.

- v2 (next):
  - Read parallel planners per chunk with stable cursors and per‑chunk outboxes; merge + sort before Write.
  - Write per‑chunk single writers run in parallel; cross‑chunk commit order = ascending ChunkId.
  - ReservationManager via Diff (ReserveItem/ReleaseReservation/ReserveDest/ReleaseDest; TTL/partial quantities/cooldowns) replaces object‑local flags.
  - Planner uses `StockpileManager` (authority) for zones and `ChunkStockpileData` for capacity/member cells; no reconstruction from shards.

9) Testing & Determinism

- Deterministic replays: same input → identical planned moves, assignments, and resulting diffs.
- Anti ping‑pong: placing items into Zone A should not immediately schedule moves to Zone B unless filters/targets demand it and dwell/stickiness allow.
- Budgets: bounded work per tick; backlog and cursors continue next tick; stable results across runs.

10) Workshops & Roles (future)
- Hauling eligibility can be gated by a Hauling Spot/Workshop and worker role assignment.
- Executors first query Role/Roster manager for eligible workers at or assigned to nearby spots.
- Keeps deterministic ordering; reduces candidate set size; does not alter Diff/UPDATE_ORDER.
