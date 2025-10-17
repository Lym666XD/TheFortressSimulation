Haul/Transport System — Architecture & Data Flow (v2)
status: implemented (core), pending producers expansion
owner: core/simulation (Jobs)
last_updated: 2025-10-16

0) Overview

The Haul system is refactored into a Transport pipeline composed of:
- Producers (Construction/Install/Stockpile planners): Read-only systems that enqueue requests.
- TransportRequestQueue: a thread-safe, deterministic intake shared by all producers.
- TransportJobSystem: the executor that assigns workers, moves items, and emits DiffOps.

1) Key Components

- Simulation.Jobs.TransportRequestQueue
  - Interface: ITransportIntake/ITransportRequestQueue
  - Ordering: CreatedTick → Priority (asc) → RequestorId → ItemGuid
  - De-dup: (ItemGuid, To, ToZ)

- App.Jobs.TransportJobSystem (replaces HaulJobSystem)
  - ReadTick: drain queue/backlog, assign workers deterministically, create ActiveJobs
  - WriteTick: step movement, emit MoveCreature/MarkCarried/MoveItem/UnmarkCarried diffs
  - Reservations: refresh TTL for workers while active; release on complete

- Simulation.Orders.HaulingSystem (planner)
  - Now enqueues TransportRequests (Reason=ToStockpile) via ITransportIntake
  - Legacy outbox retained for compatibility (unused once all executors migrate)

2) Update Order Fit

UPDATE_ORDER (excerpt)
Read Phase:
  - Planners (e.g., HaulingSystem) build TransportRequests; enqueue to queue (no writes)
Barrier
Write Phase:
  - TransportJobSystem drains requests, executes movement and emits DiffOps to DiffLog
PostTick:
  - DiffLog merges and applies; world updated; navigation cache rebuild for dirty chunks

3) Class Diagram (simplified)

  +-------------------------------+          +------------------------------+
  | Simulation.Orders.* Planners |          | Simulation.Jobs              |
  | - HaulingSystem (Read)      |  Enqueue  | + TransportRequestQueue      |
  | - (Construction Planner)    +---------> |   - Enqueue(TransportRequest)|
  | - (Install Planner)         |           |   - Drain(max)               |
  +-------------------------------+          +------------------------------+
                                                      |
                                                      | Drain
                                                      v
                                           +-------------------------------+
                                           | App.Jobs.TransportJobSystem   |
                                           | - ReadTick: assign workers    |
                                           | - WriteTick: move & emit diffs|
                                           +-------------------------------+
                                                      |
                                                      | DiffOps
                                                      v
                                           +-------------------------------+
                                           | Core.Simulation.DiffLog       |
                                           | - Merge+Sort+Apply            |
                                           +-------------------------------+

4) Data Flow (ASCII)

Player → OrdersManager → (planner) HaulingSystem.ReadTick
  → TransportRequest { ItemGuid, From, To, Reason=ToStockpile, Priority, RequestorId, CreatedTick }
  → TransportRequestQueue.Enqueue

Barrier

TransportJobSystem.ReadTick
  → Drain N requests (sorted)
  → For each: pick worker (deterministic), reserve creature, path to item
  → _active jobs += { worker, item, dest, stage=ToItem }

TransportJobSystem.WriteTick
  → For each active job: step movement
     - On arrive at item: MarkCarried + path to dest (stage=ToDest)
     - On arrive at dest: MoveItem + UnmarkCarried + release reservations

DiffLog.MergeAndApply (PostTick)
  → Items/L5 & Creatures/L6 updated; caches rebuilt as needed

5) Determinism & Chunk Parallel Readiness

- Stable queue ordering; no dependency on thread scheduling.
- Single writer per chunk via DiffLog; executor never performs direct world writes.
- Next step: per-chunk request sharding to feed chunk-parallel Merge+Apply.

6) Interfaces (summary)

- ITransportIntake: Enqueue(TransportRequest)
- ITransportRequestQueue: Drain(max, into) + Count
- TransportReason: ToStockpile | ToConstructionSite | ToInstallSite | ToWorkshopInput | ToWorkshopOutput | Cleanup | Misc

7) Counters & Telemetry

- TransportJobSystem.JobStatsSnapshot: {Intake, Active, Backlog, CompletedDelta, RequeuedDelta, NoPathDelta, CarryoverOld}
- Logs: assignment, replan, completion, and no-path events (rate limited by tick modulo in quiet periods)

