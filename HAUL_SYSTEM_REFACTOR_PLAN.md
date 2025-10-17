Haul/Transport System — v2 Refactor Plan (Deterministic, Chunk‑Parallel Ready)
id: haul-system.refactor.v2
status: design-for-implementation
owner: core/simulation (Jobs)
last_updated: 2025-10-16

0) Context & Scope

This plan updates the Haul system into a general Transport pipeline that fits the unified scheduler and the project’s Read/Barrier/Write, Diff‑Log, and per‑chunk single‑writer model.

It keeps v1 gameplay semantics deterministic while introducing clean seams for: (1) chunk‑parallel planning, (2) per‑chunk Merge+Apply, (3) request‑driven hauling for construction/workshops/furniture, and (4) future multi‑threading.

In scope (v2):
- Generalize hauling into a Transport pipeline (requests → plans → jobs → diffs).
- Introduce a request queue decoupled from any single planner; HaulJobSystem drains it.
- Expand interfaces for reservations, pathing, item/creature selection, and telemetry.
- Keep v1 determinism and UPDATE_ORDER contracts.

Out of scope (follow‑ups):
- UI telemetry panels, deep item filter UI, and worker role/roster integration (interfaces readied).


1) What We Will Build Next (from prior discussions)

Structural Construction (L0, walls/floors/ramps/stairs)
- Add construction tunings (tuning.construction.json): per‑tile material counts (block/plank), floor support policy, optional ramp plank count.
- Convert “ghost” into “construction site” placeable with site state (required/delivered/progress). Executor consumes materials and emits SetTerrain.
- Add a Construction Materials Planner that issues transport requests (site shortfalls → move items to site footprint).

Workshops (L2, built from materials)
- Load workshop construction definitions from data/core/placeable/workshops.json.
- Place workshop construction sites, deliver materials via transport requests, complete into L2 placeables (nonblocking footprints).

Furniture Install (from items)
- Planner to select eligible installable items (reusing material selection cache) and issue transport requests to the target.
- Executor installs the item as a placeable (preserves GUID/material/quality/decor) and removes/marks the item appropriately.

Transport Foundation (shared)
- Introduce a decoupled TransportRequest queue (thread‑safe) as a common ingress for any producer (construction/workshop/install/stockpile).
- Refactor HaulJobSystem into a Transport executor that drains requests, assigns workers deterministically, executes movement, and emits DiffOps.


2) Goals & Non‑Goals

Goals
- Deterministic and chunk‑friendly: identical outputs across OS/CPU and stable with thread count; Read→Barrier→Write honored.
- Decoupled intake: any subsystem can enqueue transport without referencing HaulJobSystem directly.
- Extensibility: interfaces for request policy, assignment, reservations, and pathing.
- Diagnostics: structured stats, counters, and rate‑limited logs.

Non‑Goals (for this iteration)
- No UI changes beyond logs; no role/roster gating; no advanced item filter UX.


3) High‑Level Architecture

Transport pipeline (per tick)
1) Read/Plan
   - Producers (ConstructionMaterialsPlanner, FurnitureInstallPlanner, StockpilePlanner, etc.) enqueue TransportRequest(s) into a central, thread‑safe queue.
   - Optional planner‑local outboxes per chunk (v2.1) for parallel reading.

2) Barrier
   - Seal request queue snapshot for this tick.

3) Write/Execute
   - TransportJobSystem drains requests deterministically, assigns workers, advances jobs, and emits DiffOps (MoveCreature, MarkCarried, MoveItem, UnmarkCarried).
   - DiffLog merges and applies in a single writer per chunk.

Key roles
- TransportRequestQueue (Simulation): authority for all transport intake (thread‑safe, stable ordering keys, back‑pressure/TTL).
- TransportJobSystem (App): executor and orchestrator of movement; integrates with Navigation and Reservations.
- Reservations (Simulation/World): item/creature reservations via tokens with TTL; single authority for exclusivity.
- Pathing (Navigation): injected services; seeded RNG per job.


4) Data & Interfaces (C#‑style, LLM‑safe)

4.1 Contracts (Simulation)
// Classification of why we move items
public enum TransportReason { ToStockpile, ToConstructionSite, ToInstallSite, ToWorkshopInput, ToWorkshopOutput, Cleanup, Misc }

public readonly record struct TransportRequest(
    Guid ItemGuid,
    SadRogue.Primitives.Point From,
    int FromZ,
    SadRogue.Primitives.Point To,
    int ToZ,
    TransportReason Reason,
    int Priority,                // 0 (highest) .. 100
    string RequestorId,          // subsystem id (e.g., "Orders.Construction")
    ulong CreatedTick,
    ulong Seed                   // stable per request (e.g., FNV(ItemGuid,To))
);

public interface ITransportIntake
{
    void Enqueue(in TransportRequest request);
}

public interface ITransportRequestQueue : ITransportIntake
{
    int Drain(int max, IList<TransportRequest> into);     // stable order
    int Count { get; }
}

public interface IReservationService
{
    bool TryReserveItem(Guid itemId, string systemId, ulong ttlTick, string jobId);
    void ReleaseItem(Guid itemId);
    bool TryReserveCreature(Guid creatureId, string systemId, ulong ttlTick, string jobId);
    void ReleaseCreature(Guid creatureId);
}

// Optional policy extension points (swap without touching executor)
public interface ITransportAssignmentPolicy
{
    // Deterministically chooses a worker for a request; returns null if none
    Guid? SelectWorker(in TransportRequest req, IEnumerable<HumanFortress.Simulation.Creatures.CreatureInstance> candidates, uint seed);
}

public interface ITransportBackpressurePolicy
{
    bool ShouldCarryOver(in TransportRequest req, int carryCount, int backlogCount);
}

public interface ITransportLogger
{
    void OnIntake(ulong tick, int dequeued, int backlog);
    void OnAssigned(ulong tick, Guid worker, Guid item, SadRogue.Primitives.Point3 from, SadRogue.Primitives.Point3 to);
    void OnComplete(ulong tick, Guid worker, Guid item);
    void OnRequeued(ulong tick, Guid item, string reason);
    void OnNoPath(ulong tick, Guid item, SadRogue.Primitives.Point3 goal);
}

4.2 Executor surface (App)
public sealed class TransportJobSystem : HumanFortress.Core.Time.ITick
{
    public int Priority => HumanFortress.Core.Simulation.UpdateOrder.Priority.Jobs;
    public string SystemId => "Jobs.Transport"; // replaces/absorbs HaulJobSystem

    // Dependencies injected in ctor:
    // world, requestQueue, pathService, navigationView, reservationService,
    // assignmentPolicy, backpressurePolicy, diffLog, tunings

    public void ReadTick(ulong tick);
    public void WriteTick(ulong tick);

    // Diagnostics and controls
    public JobStatsSnapshot GetLastStats();
    public int GetBacklogCount();
}

4.3 Planning helpers (Simulation)
// Example producer that scans construction sites and enqueues missing material requests
public interface ITransportProducer : HumanFortress.Core.Time.ITick { }


5) Determinism & Chunk‑Parallel Model

Ordering keys (stable, v2):
- Intake drain order: (CreatedTick → Priority ascending → RequestorId lexicographic → ItemGuid).
- Assignment order: candidates sorted by (CreatureGuid) and filtered deterministically; RNG seeded by (WorkerGuid ^ ItemGuid) for path tie‑breakers only.
- Diff emission order: executor accumulates per‑chunk diffs and emits with keys (tileKey → Priority↓ → SystemId↑ → LocalSeq↑) matching DiffLog policy.

Read/Write masks
- Producers are strictly read‑only (no writes; may enqueue requests only).
- Transport executor may write L5 Items (Move/Carry toggles) and L6 Creatures (MoveCreature) only.

Chunk boundaries
- In v2, executor remains global serial per stage; it still computes chunk‑local targets. In v3, split executor into per‑chunk Merge+Apply jobs by draining chunk‑partitioned inboxes with single‑writer guarantees.

RNG policy
- Stable seed: Seed = FNV32(ItemGuid, To) or FNV32(WorkerGuid, ItemGuid) for worker‑selection/path sampling; no time‑based randomness.


6) Scheduling, Budgets & Back‑pressure

Tunables (content/registries/tuning.scheduler.json → jobs.hauling)
- plan_per_tick: max requests drained per tick (global cap; later per‑chunk caps).
- ms budget per Read/Write: executor checkpoints and carries over requests on overflow.
- carryover_max_ticks: how many ticks a request can be deferred before priority boost.

Back‑pressure rules
- If a request cannot be assigned (no worker/no path), requeue to backlog with annotated reason; apply priority boost on repeated carryover.
- TTL/expiry optional: after N ticks (config), request is dropped with a structured warning.


7) Reservations & Conflict‑Free Writes

Reservations
- On Read assignment: reserve creature (ttl extended each Write) and attempt to reserve the item; if reserve fails, requeue request.
- On pickup: emit MarkCarried(item, carrier) diff and keep reservation until placement.
- On place: MoveItem + UnmarkCarried diffs; release both reservations.

Conflict checks
- Before pickup, revalidate that item is still present, not carried, and not already in target stockpile/site; otherwise drop or requeue (deterministic outcome).


8) Extensibility Hooks

Policy injection
- Assignment: distance/health/role filters; replaceable without changing executor core.
- Back‑pressure: carryover thresholds, starvation boosts.
- Destination resolvers (future): stockpile sharding, site footprint selection, workshop IO cells.

Observation & Telemetry
- ITransportLogger for structured logs; counters exposed via JobStatsSnapshot: {Intake, Active, Backlog, Completed, NoPath, Requeued, CarryoverOld}.
- Events (future): compact per‑tick event stream for UI panels.


9) Migration Strategy

Phase 1 — Introduce Request Queue & Transport executor
- Add TransportRequestQueue (thread‑safe, stable ordering) under Simulation.
- Rename/replace HaulJobSystem → TransportJobSystem; wire into UnifiedJobsOrchestrator where HaulJobSystem was used.
- Keep legacy HaulingSystem planner intact; adapt it to enqueue TransportRequest instead of private outbox.

Phase 2 — Producers
- Implement ConstructionMaterialsPlanner (Read only) that scans construction sites and enqueues missing material requests.
- Provide a minimal FurnitureInstallPlanner (Read only).

Phase 3 — Chunk partitioning (optional for v2)
- Maintain per‑chunk request shards (ByChunk(cx,cy,z)) for cheaper splits in v3 chunk‑parallel Merge+Apply.

Phase 4 — Decommission old paths
- Remove direct HaulingSystem → HaulJobSystem coupling and outbox; all producers route via TransportRequestQueue.


10) Safety, Testing, and CI Gates

Safety
- No cross‑chunk writes in executor beyond DiffLog; reservations are local authority; all writes go through DiffOps.

Determinism tests
- Fixed seeds produce identical diffs across runs/threads/OS; stable order unit tests for queue drain and assignment.

Performance/pressure tests
- Large request bursts, long paths, and no‑path cases; ensure carryover stats match budgets and never deadlock.


11) Open Questions (Defaults if unanswered)

- Priority classes mapping for TransportReason (default: Sites/Install P1; Stockpile P2; Cleanup P3).
- Request TTL (default: disabled; requeue indefinitely with priority boost after 8 ticks carryover).
- Per‑chunk executor split timing (adopt after v2 producers are stable).


Appendix A) Minimal File/Type Additions (summary)

- Simulation/Jobs/TransportRequestQueue.cs: ITransportRequestQueue + impl (locking + stable order + back‑pressure fields).
- App/Jobs/TransportJobSystem.cs: executor replacing HaulJobSystem (same UpdateOrder priority), with injection points: assignmentPolicy, backpressurePolicy, logger.
- Simulation/Jobs/Producers/*.cs: ConstructionMaterialsPlanner, FurnitureInstallPlanner (Read only, enqueue requests).
- Extend World.Reservations → IReservationService interface surface (adapter present for current impl).

