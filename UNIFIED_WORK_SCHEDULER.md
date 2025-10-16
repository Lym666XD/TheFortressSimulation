Unified Work Scheduler — Design & Integration (v1 → v2)
id: unified-work-scheduler.design.v1
status: implemented (v1.1), design for v2
owner: core/simulation
last_updated: 2025-10-15

0) Summary

This document defines a unified job scheduling system that orchestrates how gameplay work (planning and execution) runs within our existing UPDATE_ORDER. It is fully compatible with today’s architecture and preserves determinism, while reserving clear seams to enable multi-threaded, chunk-parallel execution in a later phase.

This iteration (v1) onboards Hauling, Mining, and Construction only. Other stages (e.g., Fluids/Fields) are out of scope for now but designed for future inclusion.
Implementation note (v1.1): A single system `UnifiedJobsOrchestrator` runs planners in Read and executors in Write, preserving UPDATE_ORDER and determinism. Tunings load from `content/registries/tuning.scheduler.json` with safe defaults.

Confirmed constraints for v1:
- Scope: Hauling, Mining, Construction only.
- Zero behavior change: Executors run through a single Merge+Apply adapter (one per subsystem) under the scheduler.
- Diff: Keep global DiffLog; PostTick MergeAndSort → Apply remains authoritative.
- Configuration: “tunings-first” via a new scheduler tuning file; code defaults are fallback only.
- Telemetry: Logging only (no UI panels for now).

1) Goals & Non‑Goals

Goals
- Unify planning and execution across Hauling/Mining/Construction under a common scheduler with deterministic outcomes.
- Preserve UPDATE_ORDER (Read/Barrier/Write) and today’s DiffLog application, rendering, and UI contracts.
- Provide a clean path to v2 chunk-parallel Merge+Apply with per-chunk single-writer guarantees, without v1 regressions.

Non‑Goals (v1)
- No changes to gameplay semantics or outputs (deterministic replays must match).
- No UI telemetry surfaces; only structured logs.
- No chunk-parallel writes yet; v1 runs single-threaded inside each stage.

2) Context & Constraints (Authoritative References)

- UPDATE_ORDER.md: Read/Barrier/Write discipline; allowed write layers per stage; dirty/derived rules.
- JOB_SCHEDULER_SPEC.md: normative model for plan → barrier → merge+apply with deterministic ordering and budgets.
- NAVIGATION_SPEC.md: pathfinding determinism and cache invalidation rules (ConnectivityVersion).
- RENDERING_SNAPSHOT.md, CHUNK_AND_DATA_LAYOUT.md: read-only rendering, dirty/derived caches, per-chunk locality.

3) Architecture Overview

Top-level roles
- JobScheduler (new): Orchestrates Plan → Barrier → Merge+Apply for a stage. In v1, it runs single-threaded; in v2, it runs chunk-parallel with per-chunk single-writer.
- Job Producers (adapters): Wrap existing planners (HaulingSystem, MiningSystem, ConstructionSystem) to emit deterministic plan outputs into per-chunk outboxes managed by the scheduler.
- Job Appliers (adapters): Wrap existing executors (HaulJobSystem, MiningJobSystem, ConstructionJobSystem) as a single “Merge+Apply job per subsystem” in v1. In v2, split per chunk.
- Diff sinks: Global DiffLog (current), ItemsDiffLog (items layer). In v2, optional per-chunk sink with ordered cross-chunk commit.

Stage participation (v1)
- Read phase: Producers run (single-threaded in configured order) to fill plan outboxes.
- Barrier: seal plan outputs.
- Write phase: One Merge+Apply adapter per subsystem consumes the sealed outboxes and calls existing executors’ logic, which continues to emit DiffLog/ItemsDiff operations.
- PostTick: unchanged — GameStateManager merges and applies diffs, then rebuilds navigation caches for dirty chunks.

4) Job Model (Deterministic, LLM‑Safe)

JobDescriptor (conceptual)
{
  "jobId": "S-Jobs-Hauling-Plan-C_10_4",
  "stageId": "Jobs",
  "chunkId": "C_10_4",
  "kind": "Plan" | "MergeApply" | "ActorDrain",
  "reads": ["L0","L2","L5","Nav","Support"],
  "writes": [],
  "priority": "P1",
  "budget": { "iterations": 2048, "ms": 2 },
  "rngStream": "seed://World^Hash(Stage,Chunk,System)",
  "heatHint": { "dirtyTiles": 64, "msgs": 0 }
}

Rules
- Plan jobs are read-only (writes must be empty).
- Merge+Apply jobs may write to a subset of layers allowed by the stage (enforced by the scheduler in v2; asserted in v1).
- Deterministic budgets: if a job exceeds its budget, it must checkpoint cursors and re‑enqueue a continuation with a stable suffix (e.g., “..#2”).
- Stable RNG: seed = WorldSeed ^ Hash(StageId, SystemId, ChunkId[, LocalKey]). Sort before sampling in any order-sensitive loop.

5) Data Flow & APIs

5.1 Producers (Planners)
- v1 adapters call the existing planners in the same order they run today, but redirect outputs into a scheduler-provided per-chunk outbox:
  - Hauling: PlannedMove → Outbox.ByChunk()
  - Mining: PlannedDig → Outbox.ByChunk()
  - Construction: PlannedBuild → Outbox.ByChunk()
- Producers expose stable cursors (PlannerState) so budgets and requeue are deterministic.

5.2 Appliers (Executors)
- v1 adapters aggregate each subsystem’s outboxes into one Merge+Apply call per subsystem (no per-chunk split yet):
  - HaulJobSystemAdapter: drains PlannedMove in a stable order, performs assignment/pathing/movement, and emits DiffLog ops (MoveCreature/MarkCarried/MoveItem/UnmarkCarried).
  - MiningJobSystemAdapter: drains PlannedDig in a stable order (stairwell-aware sort and gates), performs movement/dig/progress, and emits DiffLog.SetTerrain + ItemsDiff.AddItem.
  - ConstructionJobSystemAdapter: drains PlannedBuild and emits DiffLog.SetTerrain (and cleans ghosts).
- v2: Each adapter will become a per-chunk Merge+Apply job; cross-chunk commit order = ascending ChunkId.

6) Scheduling & Ordering

v1 (compatibility mode)
- Execution is single-threaded within the Jobs stage.
- Order: Producers (Hauling → Mining → Construction) → Barrier → Appliers (Hauling → Mining → Construction).
- Within a producer/applier, all enumerations and dequeues follow existing stable keys (GUID/XY/Z/segment/priority) to preserve exact behavior.

v2 (future)
- Plan: Producer jobs per active chunk run in parallel (reads only).
- Merge+Apply: One writer per chunk per subsystem. Jobs run in parallel with cross-chunk commit order fixed (ascending ChunkId) to preserve determinism.
- The scheduler rejects concurrent jobs whose writes intersect on the same chunk.

7) Active Set, Budgets & Back‑Pressure

Active Set (per stage)
- Chunks with: dirty sets; subsystem outboxes non-empty; Actor inbox non-empty; or within L0/L1.

Budgets
- Global per-stage budgets (e.g., max planned items/digs/builds per tick) derive from tunings.
- Per-job budgets are enforced strictly; unfinished work checkpoints and resubmits with a stable jobId continuation.

Back‑Pressure
- Stable queues persist across replays (keyed by chunkId then FIFO). Producers do not starve each other due to weighted round‑robin by priority.

8) Determinism & Error Handling

Determinism
- Sort keys: (chunkId → tileKey → Priority(desc) → SystemId(asc) → LocalSeq(asc)).
- RNG: Stage/System/Chunk (and local keys) derive seeds. Sort before sampling.
- PostTick applies DiffLog sorted by its own stable SortKey; ItemsDiff likewise.

Failure Safety
- Job boundaries are wrapped with try/catch. On exception: drop only the offending op if possible; quarantine the subsystem or chunk for this tick; next tick may degrade to serial (v2). Log structured error {seed,tick,stage,jobId,chunkId,stack}.

9) Configuration (Tunings‑First)

File: /content/registries/tuning.scheduler.json (new)

Example (draft, v1 keys honored; v2 keys reserved):
{
  "version": 1,
  "threads": 1,                       // v1 default; v2 allows >1
  "queue_policy": "work_stealing",    // reserved for v2; v1 = single-queue
  "priorities": {
    "Jobs": { "Hauling": "P1", "Mining": "P1", "Construction": "P2" }
  },
  "budgets": {
    "hauling": { "plan_per_tick": 128, "ms": 2 },
    "mining":  { "plan_per_tick": 128, "ms": 2 },
    "construction": { "plan_per_tick": 256, "ms": 3 }
  },
  "backpressure": {
    "max_carryover_ticks": 8
  },
  "logging": {
    "level": "info",                 // info|debug
    "per_job_stats": true
  }
}

Behavior
- When file is present, its values override code defaults. All missing keys fall back to safe defaults.
- Changing the file at runtime is not required in v1; hot‑reload can be added later.

10) Logging (v1)

Emit structured logs for:
- Stage lifecycle: BeginPlan/Barrier/BeginApply/End.
- Job stats: {seed,tick,stageId,jobId,chunkId,kind,priority,start_ms,end_ms,iterations,outbox_size,inbox_size,cnt_diffs}.
- Back‑pressure: enqueue continuation, carryover counts.
- Errors: quarantined systems/chunks, dropped ops, degrade‑to‑serial events.

No UI surface yet. Logs feed dev triage and future perf panels.

11) Integration Plan (Phased)

Phase A (current PR; no behavior changes)
- Add JobScheduler core (single‑threaded implementation).
- Add Producer/Applier adapters for Hauling/Mining/Construction that route planner outputs to per‑chunk outboxes and wrap executors as a single Merge+Apply job per subsystem.
- Wire JobScheduler into the Jobs stage without removing current systems; internally, adapters forward to existing planner/executor calls to preserve outputs.
- Keep global DiffLog and ItemsDiffLog as-is; PostTick merge+apply and nav rebuild unchanged.
- Add tuning file and logging (no UI changes).

Phase B (optional, parallel plan only)
- Enable parallel execution of Plan jobs per active chunk (reads only). Executors remain serialized. Validate replay equality.

Phase C (chunk‑parallel Merge+Apply)
- Split appliers into per-chunk Merge+Apply jobs; enforce no overlapping writes; cross‑chunk commit order fixed.
- Optionally convert Diff application to per-chunk sinks with ordered commit, or retain centralized DiffLog sorting (deterministic either way).

12) Compatibility & Risk

- v1 runs planners/executors in the same order and with the same stable sorts as today. Outputs must be identical vs. baseline.
- Logging adds I/O but is gated by level; can be reduced in release builds.
- Scheduler in v1 is a logical orchestrator; if disabled, the existing direct plumber/executor path remains as a fallback (build flag).

13) Not Implemented Yet (Tracked, Non‑Blocking)

- Work‑stealing queues and per‑worker deques (v2).
- Actor mailbox drain jobs with stable (tick → senderChunkId → seq) order.
- Per‑chunk Diff sinks and ordered cross‑chunk commit.
- UI heatmaps and “long tail” perf panels.
- Hot‑reload for tuning.scheduler.json.
- Extending the scheduler to Fluids/Fields/Vegetation with per‑stage budgets.

14) Open Questions (Defaults if unanswered)

- Producer order inside Jobs stage: default Hauling → Mining → Construction (can be tuned). If you prefer a different order, update the tuning defaults.
- Priority classes: default P1 for Hauling/Mining and P2 for Construction (can be tuned).
- Where to host adapters: App layer (next to current JobSystems) vs. Simulation. Default: App for Appliers; Simulation for Producers.

15) Appendix — Minimal Interfaces (draft)

// Producer (Planner)
public interface IJobProducer {
  string StageId { get; }
  string SystemId { get; }
  void Plan(World world, ChunkKey chunk, PlannerState state, IPlanOutbox outbox, Budget budget, Rng rng);
}

// Applier (Executor)
public interface IJobApplier {
  string StageId { get; }
  string SystemId { get; }
  void MergeApply(World world, IReadOnlyPlanInbox inbox, IDiffSink sink, Budget budget, Rng rng);
}

// Scheduler entrypoint (v1)
public sealed class JobScheduler {
  public void RunJobsStage(
    IReadOnlyList<IJobProducer> producers,
    IReadOnlyList<IJobApplier> appliers,
    SchedulerTunings tunings,
    ulong tick)
  { /* single-threaded Plan → Barrier → Apply with logging */ }
}

Links
- See JOB_SCHEDULER_SPEC.md for the normative, chunk‑parallel model that v2 will implement.
- See UPDATE_ORDER.md for stage write windows and allowed layers.
