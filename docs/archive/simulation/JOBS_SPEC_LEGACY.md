JOBS_SPEC.md – Job Model & Execution (v1.1.1)
id: jobs.v1.1.1
status: normative
owner: core/simulation
last_updated: 2025-09-30

Current implementation note (2026-06-12):

- This document is an older jobs/hauling contract. The current transport implementation is documented in [TRANSPORT_SYSTEM.md](../../simulation/TRANSPORT_SYSTEM.md).
- Old references to `HaulJobSystem` are legacy. Current code uses `TransportJobSystem` as an App wrapper around `HumanFortress.Jobs.Transport.TransportJobExecutor`.
- Mining, construction, and craft executor cores have also moved substantially into `HumanFortress.Jobs`; App keeps concrete wrappers and adapters.

0) Scope

Defines the job model, APIs and execution pipeline for simulation work initiated by player Orders or systems. The initial vertical slice implements Haul jobs (pickup → deliver) on top of Navigation and UPDATE_ORDER. This version documents the v1.1.1 transition to Diff‑Log writes for hauling and cross‑Z destination selection, and clarifies filters to prevent infinite shuttling.

1) Principles

- Deterministic outputs: stable iteration and sorting keys; seeded pathfinding per job; identical results across OS/CPU/threads.
- Read‑parallel planning, single‑writer commit: plan jobs in parallel across chunks; merge/apply via a single writer per chunk (v1.1.1 uses global write; v2 migrates to chunk‑parallel merge with per‑chunk single writer). No data races.
- Data‑driven and budgeted: priorities, limits, and heuristics from registries; per‑tick budgets; graceful back‑pressure.
- Failure‑safe: job failures never crash the loop; reservations and queues unwind cleanly.

2) Terms

- Designation: player intent (e.g., Orders → Haul rectangle) stored in OrdersManager.
- TaskTicket (Plan artifact): minimal DTO describing work to do (e.g., move item A → cell B at Z).
- ActiveJob (Exec artifact): runtime state for a job assigned to an agent, with stage machine and transient state.
- Planner: Read phase system producing TaskTickets (never mutates world).
- Executor: Write phase system that assigns agents and advances ActiveJobs; mutations go through Diff‑Log (v1.1.1) or controlled managers.

3) Flow per Tick (summary)

```
ApplyCommands  ->  (OrdersManager receives designations)
      ↓
Read Phase --------------------------------------------------------------
  HaulingSystem.ReadTick(t)
    - Drain new designations (bounded) + include active designations
    - For each rect: enumerate candidate items
        skip: reserved, carried, already-in-stockpile
    - Choose destination cell (stockpile shard; cross‑Z allowed)
    - Enqueue PlannedMove DTOs (stable order)
  [barrier]
Write Phase -------------------------------------------------------------
  TransportJobSystem (t)
    - Drain planned moves + backlog
    - Assign agents (HP>0, not busy, deterministic; cross‑Z allowed)
    - On assignment: reserve item (temporary object‑local)
    - Advance: ToItem → MarkCarried → ToDest → MoveItem+UnmarkCarried → Complete
    - Emit DiffOps: MoveCreature / MarkCarried / MoveItem / UnmarkCarried
  DiffLog
    - Merge+Sort by stable key; apply in deterministic order

3.1 Structural Construction (Executor outline)

- Planner (`ConstructionSystem`) emits `PlannedBuild{ cell, z, target_kind, geology_handle, shape }` and places ghosts (L2).
- Executor (`ConstructionJobSystem`) consumes `PlannedBuild` and writes `DiffOp(SetTerrain)` with packed args `(kind + geology_handle)`; then removes the ghost at anchor.
- Applicator normalizes geology kind, updates tiles, marks chunks dirty for navigation.
```

4) APIs (current)

- OrdersManager (thread‑safe ingress)
  - `void EnqueueHaul(Rectangle worldRect, int z, int priority, ulong tick)`
  - `int DrainHaulDesignations(ICollection<HaulDesignation> into, int maxCount)`
  - `List<HaulDesignation> GetActiveHaulsSnapshot()`

- HaulingSystem (planner)
  - `void ReadTick(ulong tick)`
    - drains designations; enumerates items; `TryFindDestination(ItemInstance, zones, out Point destWorld, out int destZ)`
    - enqueues `PlannedMove { Guid ItemGuid; Point From; int FromZ; Point To; int ToZ; }`
  - `void WriteTick(ulong tick)` → hands planned moves to executor inbox
  - `int DequeuePlannedMoves(int max, IList<PlannedMove> into)`

- TransportJobSystem (executor)
  - `void ReadTick(ulong tick)`
    - drains backlog + planned; assigns workers deterministically
  - `void WriteTick(ulong tick)`
    - MovementExecutor.UpdateMovement; emits DiffOps (see §6)

- Navigation
  - `Path PathService.Solve(in PathRequest request, in IWorldNavigationView world)`
  - `void MovementExecutor.BeginMovement(uint entity, PathRequest, Path)`
  - `MovementUpdate MovementExecutor.UpdateMovement(uint entity, IWorldNavigationView world)`

5) Writes, Threads & Diff‑Log

- v1 (prototype): global serial write; direct manager writes.
- v1.1.1 (current): global serial write; hauling emits DiffOps:
  - Items/L5: `MoveItem`, `MarkCarried`, `UnmarkCarried` (Reserve/Release coming with ReservationManager)
  - Creatures/L6: `MoveCreature`
  - DiffLog.MergeAndSort() applies ops; runtime state updated by applicators.
- v2 (target): Read parallel per chunk; Write per‑chunk single writer MergeApply in parallel; cross‑chunk commit order = ascending ChunkId.

6) Haul Job details (v1.1.1)

6.1 Filters & anti‑ping‑pong
- Planner skips items that are:
  - reserved or carried; or
  - currently placed on any stockpile cell (prevents infinite re‑hauling between zones).

6.2 Reservations & carry
- On assignment: temporary object‑local reservation (`item.IsReserved=true; ReservedBy=worker`).
- On pickup: emit `MarkCarried(itemGuid, workerGuid)`.
- On place: emit `MoveItem(itemGuid→destCell)` and `UnmarkCarried(itemGuid)`; `ReleaseReservation` will be added with ReservationManager.

6.3 Executor state machine
- `AssignWorker`: filter HP>0, not busy; deterministic order; cross‑Z allowed.
- `AdvanceJob`: UpdateMovement → ToItem (Arrived: MarkCarried, path to dest) → ToDest (Arrived: MoveItem, UnmarkCarried) → Complete.

6.4 Diff operations
- Items/L5: `MoveItem`, `MarkCarried`, `UnmarkCarried`.
- Creatures/L6: `MoveCreature`.

7) Budgets & Back‑pressure
- Planner: `max_designations_per_tick`, `max_planned_moves_per_tick`.
- Executor: `max_jobs_started_per_tick`; Movement pacing.
- Backlog: planned moves not assigned go to retry queue; deterministic order preserved.

8) Error Handling & Determinism
- `NoPath`: try next worker; else backlog; counters tracked.
- `NeedsReplan`: topology change leads to deterministic replans.
- `ItemMissing` / reserved / carried: drop ticket; planner skips on subsequent scans.
- `AlreadyInStockpile`: skipped by planner.

9) Telemetry & UI (v1.2)
- Work Drawer counters: Assigned/Completed/NoPath/Requeued.
- Active jobs list (future): JobId, Worker, Stage, From→Dest, ETA, Retries, FailReason.

10) Migration Roadmap (v1 → v1.1.1 → v2)
- v1: prototype end‑to‑end; direct writes.
- v1.1.1: Diff‑Log for hauling; cross‑Z destinations; diagonal moves per tuning; planner filter for stockpiled items; counters.
- v2: ReservationManager via Diff; StockpileManager as authority source for planner; per‑chunk MergeApply parallel write; read‑parallel planners.

11) Determinism guarantees
- Stable iteration/sort; seeded pathfinding: `SeedFrom(workerGuid,itemGuid)`.
- Fixed write order (global serial now; chunk‑serial later with fixed cross‑chunk commit order).
- Applicators perform conflict‑free merges based on stable keys.

12) Workshops & Roles (future)
- Add data‑driven Workshops/Spots (e.g., Mining Workshop, Hauling Spot, Lumber Workshop, Farming Workshop) that advertise supported job kinds and capacity.
- Introduce Role/Roster manager mapping workers to roles and assigned workshops.
- Executors filter candidate workers by role/workshop eligibility before assignment; deterministic ordering preserved.
- Workshops may provide local bonuses (speed/quality) and shifts; performance gains by shrinking candidate sets; UPDATE_ORDER and Diff model remain unchanged.

13) Debug Item Spawn & Items Drawer (ergonomics)

- Debug spawn validates `IsWalkable` tiles (ramps/stairs allowed) instead of requiring `OpenWithFloor`.
- Items drawer (F2) displays concrete names; generic resources append the material name, e.g., `Boulder (Granite)`, `Block (Basalt)`.
