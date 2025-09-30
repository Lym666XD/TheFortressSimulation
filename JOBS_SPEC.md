JOBS_SPEC.md — Job Model & Execution (v1.1)
id: jobs.v1.1
status: normative
owner: core/simulation
last_updated: 2025-09-30

0) Scope

Defines the job model, APIs and execution pipeline for simulation work initiated by player Orders or systems. The initial vertical slice implements Haul jobs (pickup → deliver) on top of the existing Navigation and UpdateOrder contracts. This spec aligns with JOB_SCHEDULER_SPEC.md, UPDATE_ORDER.md, HAULING_POLICY.md, and NAVIGATION_SPEC.md and documents the v1.1 transition to Diff-Log writes.

1) Principles

- Deterministic outputs: stable iteration and sorting keys; seeded pathfinding per job; identical results across OS/CPU/threads.
- Read-parallel planning, single-writer commit: plan jobs in parallel across chunks; merge/apply via a single writer per chunk (v1.1 uses global write; v2 migrates to chunk-parallel merge with per-chunk single writer). No data races.
- Data-driven and budgeted: priorities, limits, and heuristics from registries; per-tick budgets; graceful back-pressure.
- Failure-safe: job failures never crash the loop; reservations and queues unwind cleanly.

2) Terms

- Designation: player intent (e.g., Orders → Haul rect) stored in OrdersManager.
- TaskTicket (Plan artifact): minimal DTO describing work to do (e.g., move item A → cell B at Z).
- ActiveJob (Exec artifact): runtime state for a job assigned to an agent, with stage machine and transient state.
- Planner: Read phase system producing TaskTickets (never mutates world).
- Executor: Write phase system that assigns agents and advances ActiveJobs; all world mutations go through Diff-Log (v1.1 plan) or controlled managers (v1 prototype).

3) High-level Flow (per tick)

ASCII Flowchart (simplified)

```
ApplyCommands  ->  (OrdersManager receives designations)
      │
      ▼
Read Phase --------------------------------------------------------------
  HaulingSystem.ReadTick(t)
    - Drain new designations (bounded) + include active designations
    - For each rect: enumerate candidate items (skip reserved/carried)
    - Choose destination cell (stockpile shard)
    - Emit TaskTickets (PlannedMove) into outbox
  [barrier]
Write Phase -------------------------------------------------------------
  HaulJobSystem.WriteTick(t)
    - Drain planned moves + backlog
    - Assign agents (same-Z, non-busy, deterministic order)
    - On assignment: reserve item (v1 prototype), enqueue MoveCreature path
    - Advance jobs: ToItem -> ToDest -> Complete
    - Mutations via Diff-Log (v1.1) or controlled managers (v1 prototype)
  [emit events]
```

Structure Diagram (key components)

```
OrdersManager
  ├─ EnqueueHaul(rect,z,priority,tick)
  ├─ DrainHaulDesignations(list,max)
  └─ GetActiveHaulsSnapshot()

HaulingSystem : ITick (Priority=UpdateOrder.Priority.Items)
  ├─ ReadTick(t)
  │   ├─ Enumerate items in rects
  │   ├─ TryFindDestination(item → (chunk,cell))
  │   └─ Enqueue PlannedMove → outbox
  └─ WriteTick(t)
      └─ Drain planned moves → planner outbox → executor inbox

HaulJobSystem : ITick (Priority=UpdateOrder.Priority.Jobs)
  ├─ ReadTick(t)
  │   ├─ Drain backlog + planned moves
  │   ├─ Assign worker (stable order; same-Z; not busy)
  │   └─ For assigned: BeginMovement(worker, to Item)
  └─ WriteTick(t)
      ├─ UpdateMovement(worker)
      │   ├─ If arrived @item:
      │   │    - Mark item carried (v1 prototype)
      │   │    - BeginMovement(worker, to dest)
      │   ├─ If arrived @dest:
      │   │    - Place item; clear transient state
      │   └─ Else if NeedsReplan → replan path
      └─ (v1.1) Emit DiffOps for item place/remove & creature move

Navigation
  ├─ DeterministicAStar.FindPath(PathRequest, IWorldNavigationView)
  ├─ MovementExecutor.BeginMovement/UpdateMovement/CancelMovement
  └─ NavigationManager.GetNavDataAt(x,y,z)  // on-demand rebuild
```

4) Concrete APIs (current implementation)

- OrdersManager (thread-safe ingress)
  - `void EnqueueHaul(Rectangle worldRect, int z, int priority, ulong tick)`
  - `int DrainHaulDesignations(ICollection<HaulDesignation> into, int maxCount)`
  - `List<HaulDesignation> GetActiveHaulsSnapshot()`

- HaulingSystem (planner)
  - `void ReadTick(ulong tick)`
    - drains designations; enumerates items; `TryFindDestination(ItemInstance, zones, out Point destWorld)`
    - enqueues `PlannedMove { Guid ItemGuid; Point From; int FromZ; Point To; int ToZ; }`
  - `void WriteTick(ulong tick)` → hands planned moves to executor inbox
  - `int DequeuePlannedMoves(int max, IList<PlannedMove> into)`

- HaulJobSystem (executor)
  - `void ReadTick(ulong tick)`
    - drains backlog + planned moves; tries `AssignWorker(PlannedMove)` (same-Z, non-busy, stable order)
    - on assignment: `MovementExecutor.BeginMovement(worker, reqToItem, path)` and reserve item
  - `void WriteTick(ulong tick)`
    - `MovementExecutor.UpdateMovement(eid)` → state machine ToItem/ToDest
    - v1 prototype: modify managers (carry & place); v1.1: emit DiffOps

- Navigation
  - `Path PathService.Solve(in PathRequest request, in IWorldNavigationView world)`
  - `void MovementExecutor.BeginMovement(uint entity, PathRequest, Path)`
  - `MovementUpdate MovementExecutor.UpdateMovement(uint entity, IWorldNavigationView world)`

5) Writes, Threads & Diff-Log (clarification)

- v1 (current prototype):
  - Write phase runs globally serial (systems ordered by UpdateOrder.Priority). No parallel writers.
  - HaulJobSystem writes via managers (not Diff-Log) for fast vertical slice.
- v1.1 (spec requirement):
  - Write is still serial, but every world mutation is expressed as DiffOps:
    - Items/L5: `AddItem | RemoveItem | MoveItem | ReserveItem | ReleaseReservation | MarkCarried | UnmarkCarried`
    - Creatures/L6: `MoveCreature`
  - DiffLog.MergeAndSort() applies ops by stable keys.
- v2 (target):
  - Read: per-chunk plan jobs in parallel. Write: per-chunk single-writer MergeApply can run in parallel across chunks; cross-chunk commit order is ascending chunkId to keep replay parity. All writes go through Diff-Log.

6) Haul Job details (v1.1)

6.1 Reservations & Carry state
- On assignment: `ReserveItem(itemGuid, workerGuid, ttl)` (v1 prototype stored in item; v1.1 as DiffOp)
- On pickup: `MarkCarried(itemGuid, workerGuid)` → renderer/UI hides ground item; follows worker
- On place: `PlaceItem(itemGuid, destCell)` + `ReleaseReservations(jobId)`

6.2 Executor state machine (functions)
- `AssignWorker(PlannedMove mv, IEnumerable<CreatureInstance> workers)`
  - Filter: same Z, HP>0, not busy; ordered by GUID
  - `TryPathToItem(worker, mv.From)`; if found → assign; `EmitReserveItem`
- `AdvanceJob(ActiveJob j)`
  - `UpdateMovement(eid)` →
    - ToItem: Arrived → `EmitMarkCarried(itemGuid, workerGuid)` → Begin path to dest
    - ToDest: Arrived → `EmitPlaceItem(itemGuid, dest)`; `EmitReleaseReservations(jobId)` → Complete

6.3 Diff-Log operations (v1.1)
- Items/L5: `AddItem`, `RemoveItem`, `MoveItem`, `ReserveItem`, `ReleaseReservation`, `MarkCarried`, `UnmarkCarried`
- Creatures/L6: `MoveCreature`
- Merge key: `tileKey → Priority(desc) → SystemId → LocalSeq`

7) Budgets & Back-pressure
- Planner: `max_designations_per_tick`, `max_planned_moves_per_tick`
- Executor: `max_jobs_started_per_tick`; MovementExecutor pacing
- Backlog: planned moves not assigned this tick go to retry queue with cooldowns to avoid starvation

8) Error Handling & Determinism
- `NoPath`: try next worker; if all fail → backlog; record event
- `NeedsReplan`: triggered on topology change; deterministic seed ensures repeatable results
- `ItemMissing` or already reserved/carried: drop ticket; planner will naturally skip next scans

9) Telemetry & UI (v1.2)
- Work Drawer Tab 2: active jobs list (JobId, Worker, Stage, From→Dest, ETA, Retries, FailReason)
- Counters: Assigned/Picked/Completed/NoPath/Requeue; average/percentiles

10) Migration Notes (v1 → v1.1 → v2)
- v1 implemented: end-to-end vertical slice; global serial writes; manager direct writes (for visibility)
- v1.1: switch to Diff-Log writes; add Reservation/Carry DiffOps; active job visualization
- v2: per-chunk MergeApply parallel writes; mailbox-driven cross-chunk coordination; stronger budgets & cooldowns

11) Reference function list (actual / planned)
- OrdersManager: `EnqueueHaul`, `DrainHaulDesignations`, `GetActiveHaulsSnapshot`
- HaulingSystem: `ReadTick`, `WriteTick`, `TryFindDestination`, `DequeuePlannedMoves`
- HaulJobSystem: `ReadTick`, `WriteTick`, `AssignWorker`, `AdvanceJob`
- Diff-Log (v1.1): `DiffLog.AddOp`, `DiffLog.MergeAndSort`, `ItemsLayer.Apply`

12) Why outputs are deterministic
- Inputs + iteration/sort keys are stable; RNG seeds derive from (Stage,System,Chunk) or (WorkerGuid,ItemGuid)
- Write order fixed (v1.1 globally serial; v2 per-chunk single-writer with ascending chunkId commit)
- Merge strategies & conflict resolution are layer-defined; no undefined last-writer effects
