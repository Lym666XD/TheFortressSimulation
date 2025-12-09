JOB_SCHEDULER_SPEC.md — Deterministic Chunk-Parallel Scheduler (Read/Plan → Commit)
id: job-scheduler.v1
status: normative
owner: core/simulation
last_updated: 2025-09-14
version_policy: semver

0) Scope

Defines the only supported way to run simulation work across threads while keeping determinism, stability, and data locality.

Covers: job model, descriptors, queues, priorities, read/write masks, aliasing checks, budgets, stage barriers, actor mailbox draining, degradation/fail-safe, instrumentation, and CI hooks.

1) Goals (Normative)

Outcome determinism: same seed + inputs ⇒ identical world/snapshot hashes across OS/CPU.

Safety first: no overlapping writes; failures quarantine a chunk/system—never crash the loop.

Data-oriented: schedule by chunks; maximize cache locality; keep hot paths allocation-free.

LLM-friendly: clear schemas, checklists, and anchors so Codex/Claude produce compliant code.

2) Terms

Chunk: fixed spatial partition (W×W×Zc, halo=1). Scheduling unit.

Stage: one step in the per-tick pipeline (e.g., ApplyCommands, FluidsStep).

Plan job: read-only unit producing diffs or messages (never mutates world).

Merge+Apply job: the only writer for a chunk in a stage; merges diffs deterministically, then writes once.

Actor mailbox: per-chunk message queue for inter-chunk communication, drained in a stable order.

3) High-Level Model (Normative)

Per stage: Plan (parallel, read-only) → Barrier → Merge+Apply (per-chunk, deterministic) → Emit events.

Across chunks: run jobs in parallel; inside a chunk: every write funnels through one Merge+Apply job.

Across stages: a hard barrier preserves the global update order.

4) Job Descriptor (Normative)

Every job must declare the following (LLM-safe JSON shown for clarity):

{
  "jobId": "S4-Fluids-C_10_4",
  "stageId": "FluidsStep",
  "chunkId": "C_10_4",
  "kind": "Plan",
  "reads": ["L0","L2","L3","L4","Nav","Opac","Support"],
  "writes": [],
  "priority": "P1",
  "budget": {"iterations": 4096, "ms": 2},
  "rngStream": "seed://World^Hash(Stage,Chunk)",
  "affinity": "prefer-same-worker-as-prev-stage",
  "heatHint": {"dirtyTiles": 120, "msgs": 8}
}


Constraints

Plan jobs: writes must be empty.

MergeApply jobs: writes must be a subset of the stage’s allowed layers for the owning chunk.

priority classes: P0 (critical), P1 (time-sensitive), P2 (normal), P3 (background).

budget is enforced strictly; overflow work spills to next tick (back-pressure).

5) Queues & Threads (Normative)

Workers: N long-lived threads (pinned if available).

Queues:

Global priority queues: one per P0..P3 for fairness.

Per-worker deques: owners pop LIFO; other workers steal from the bottom (classic work-stealing).

Determinism rule: the scheduling order must not affect results:

Plan jobs are read-only; outcomes depend only on world state + RNG streams.

Merge+Apply jobs sort inputs by deterministic keys before writing.

6) Scheduling Algorithm (Normative)

Stage entry

Mark active chunks (dirty sets, mailboxes non-empty, or within active-area).

Enqueue one Plan job per active chunk (or per subsystem, if split is configured).

Enqueue one Actor-Drain job per chunk with inbound messages (kind=Plan, writes=[]).

Plan phase

Workers pull by P0→P1→P2→P3.

Each job respects budget; unfinished work re-enqueues a continuation with the same jobId suffix (e.g., …#2).

Outputs are Diff-Log entries and/or Actor messages (immutable).

Barrier

Wait for all Plan/Drain jobs of this stage to finish (or hit budget and requeue as carryover).

Merge+Apply phase

For each chunk that produced diffs: create one Merge+Apply job.

Inside that job:

Sort diffs by (tileKey → Priority↓ → SystemId↑ → LocalSeq↑).

Apply layer merge strategy (e.g., fluids SUM→Clamp→BackPressure).

Write once; update dirty sets & derived caches incrementally.

Commit order across chunks is ascending chunkId to keep replay parity.

Emit events

Publish compact, ordered event stream; this step is read-only for world state.

7) Read/Write Masks & Aliasing (Normative)

Registration time: every stage declares the set of allowed write layers.

Job time:

The scheduler rejects any concurrent jobs whose writes intersect on the same chunk.

Plan jobs with non-empty writes are illegal.

Actor-Drain jobs are always writes=[].

Runtime guard: if a Merge+Apply job attempts to write an unauthorized layer, it is aborted; the chunk is quarantined for this tick, and a structured error is logged.

8) Priorities & Fairness (Normative)

Default mapping

P0: Support & Collapse commits; safety-critical housekeeping.

P1: Fluids / Fields plan; Items reservations; Actor-Drain.

P2: Vegetation/Surface plan; Items post-processing.

P3: Cold-area maintenance; low-impact rebuilds.

Starvation rules

If a lower class waits > T ms (configurable), temporarily boost it by one class.

Priority never alters write ordering inside Merge+Apply (that is deterministically keyed).

9) Budgets & Back-Pressure (Normative)

Each stage defines global budgets (e.g., F fluid cells, G field entries per tick).

A job that exceeds its iteration or time budget:

Must checkpoint local cursors and re-enqueue itself for the next tick.

Must emit back-pressure stats (see §12) and not overrun the barrier.

Back-pressure queues are stable across replays (keyed by chunkId then FIFO).

10) Actor Mailboxes (Normative)

Drain order inside a chunk: (tick → senderChunkId → localSeq).

No side-effects on send; all effects happen only when the receiver applies.

Replies (Accept / PartialAccept / Reject(reason)) are messages too; senders handle them in the next tick.

Mailbox overflow policy: drop oldest non-critical message with a warning (rate-limited), never crash.

11) Affinity & Locality (Normative)

Prefer to run a chunk’s Plan and Merge+Apply on the same worker (hint), but results must not depend on this.

If a chunk is hot (high heatHint), pre-bind it for K stages to improve cache reuse.

Affinity can be ignored during contention; determinism derives from data + keys, not from which core executed the job.

12) Instrumentation & Telemetry (Normative)

For every job, record:

seed, tick, stageId, jobId, chunkId, kind, priority

start/end ts, cpu ms, iterations, yielded diffs, messages in/out

merge conflicts resolved, back-pressure produced, quarantined? degraded?

50/95/99 percentiles per stage and per chunk hotness charts.

UI hooks:

Heatmap overlays (ms/chunk, conflicts, mailbox size).

“Long tail” panel: slowest chunks, biggest back-pressure producers.

13) Failure Handling & Degradation (Normative)

Boundary try-catch around:

Stage orchestration; Merge+Apply; Actor-Drain; content/mod sandboxes; IO.

On exception:

Drop only the offending op/diff/message for that tile if possible.

Quarantine the chunk/system for this tick; degrade to serial next tick.

Optionally freeze the chunk read-only for 1 tick and schedule a cache rebuild.

Emit a structured error: {seed,tick,stage,jobId,chunkId,tileIndex,stack} (rate-limited).

The main loop must not crash.

14) Determinism Rules (Normative)

RNG streams: per stage/system/chunk; seed = WorldSeed ^ Hash(StageId,SystemId,ChunkId).

Sort before sampling in any order-sensitive loop.

Merge order inside a chunk:

tileKey → Priority(desc) → SystemId(asc) → LocalSeq(asc).

Commit order across chunks: ascending chunkId.

Mailbox drain: (tick → senderChunkId → localSeq).

Result independence: Changing worker counts or queue policy must not alter outputs.

15) Configuration (Normative)

threads: default = logical cores (capped to a configured max).

queue_policy: work-stealing on/off (defaults on).

priorities: mapping of stages/subsystems → P0..P3 (project defaults provided).

budgets: per stage (F,G, …).

quarantine: thresholds and cool-down.

jitter_fuzz: CI-only toggle to randomize scheduling (determinism must hold).

affinity_window: how many stages to keep a hot chunk on the same worker.

16) CI & Tests (Normative)

Determinism harness: replay golden seeds across OS/CPU/threads; assert equal hashes.

Scheduler jitter fuzz: randomize steal patterns/affinity; outputs must remain identical.

Budget gates: assert per-stage iteration/time limits (fail CI on regression).

Back-pressure stability: carryover order and totals reproduce across runs.

Soak: long-run test with periodic fault injection (forced exceptions) to validate quarantine/degrade.

17) LLM Integration Guide (Normative)

When adding a new system (for Codex/Claude):

Declare a Plan job only; never mutate world in plan.

Emit diffs with registered op IDs and complete metadata: {op,target,layer,args,priority,systemId,localSeq}.

Avoid RNG inside order-dependent loops; if needed, sort first.

Respect budgets and yield early; let the scheduler re-enqueue continuation work.

Do not spawn threads/tasks inside systems; the scheduler owns execution.

Provide a small Merge strategy entry (or reuse an existing layer strategy) if your diffs touch a new data type.

18) Checklists (Drop-in)
18.1 Job Definition Checklist

 stageId, chunkId, kind ∈ {Plan, MergeApply}, reads[], writes[] (Plan=empty), priority, budget.

 RNG stream acquired via {Stage,System,Chunk}.

 No allocations/locks/LINQ in per-tile loops; zero side-effects in Plan.

18.2 Scheduler Safety Checklist

 Reject any concurrent jobs with intersecting writes on the same chunk.

 Merge+Apply exists at most once per chunk per stage.

 Commit order across chunks is ascending chunkId.

 Actor-Drain jobs always writes=[].

18.3 Failure & Telemetry Checklist

 Boundary try-catch around Merge+Apply and Actor-Drain.

 On error: drop → quarantine → degrade → (optional) freeze.

 Log {seed,tick,stage,jobId,chunkId,tileIndex}; rate-limit duplicates.

 Expose percentile timings, conflicts, back-pressure in UI.

19) Appendix — Example Stage Timeline (Informative)

FluidsStep

Enqueue: Actor-Drain (for chunks with border flux) + Plan(Fluids) for active chunks.

Barrier → create MergeApply(Fluids) per chunk with diffs.

Commit across chunks by chunkId; inside chunk apply SUM→Clamp→BackPressure.

Items

Enqueue: Plan(ItemReservations); barrier → MergeApply(Items) (ONE-WINNER reservations); cross-chunk moves via PushItem messages next tick.
20) Execution Backlog (informative)
- Determinism harness: add jitter-fuzz replay to assert identical MergeApply outputs across OS/threads; include stable drain-order tests for transport queue shards.
- Reservation diffs: promote ReservationManager to emit/consume Diff ops (Reserve/Release Item/Creature) and enforce Items-stage write masks.
- Per-chunk MergeApply PoC: drain chunk shards from transport/mining/construction inboxes and run per-chunk MergeApply jobs; keep commit order ascending ChunkId.
- Tunings wiring: expose priorities/budgets/affinity/backpressure in `tuning.scheduler.json`, plumb through orchestrator; log carryover age and boosts.
- Telemetry hooks: structured per-job stats stream (intake/active/backlog/requeued/nopath/quarantine) with opt-in UI panel; keep log-only default.
