0) Scope

This document defines the official concurrency model for the simulation core: job scheduling, read/write isolation, deterministic ordering, intra-chunk Diff-Log merges, inter-chunk Actor messaging, stage barriers, degradation, stability, and CI gates.

It integrates with: Update Order, Data Layout, Tile Layers, and the Rendering Snapshot contract.

1) Goals (Normative)

Deterministic & replayable across OS/CPU: same seed + inputs ⇒ same world/snapshot hashes. 

UPDATE_ORDER

Safe parallelism: exploit multi-core via read-parallel work, intra-chunk merges, and inter-chunk messages; one commit barrier per tick. 

UPDATE_ORDER

Crash-resistant: exceptions never tear down the loop; chunks/systems can quarantine/degrade. (See Stability policy in RULES). 

rules

Data-oriented: chunked SoA base, sparse overlays, derived caches, dirty-scoped rebuilds. 

DATA_LAYOUT

2) Terminology (Normative)

Chunk: fixed spatial partition (e.g., 16×16×Zc) and scheduling unit. 

DATA_LAYOUT

Stage: one step in the Per-Tick Update Order (e.g., ApplyCommands, FluidsStep). Only stages may write. 

UPDATE_ORDER

Diff: immutable, per-tile proposal {op,target,layer,args,priority,systemId,localSeq} collected during a stage.

Merge: deterministic reduction of diffs for a tile to a single write (or none).

Actor: per-chunk executor with a mailbox for inter-chunk messages.

Dirty set: tiles requiring derived cache rebuilds; propagation is bounded. 

DATA_LAYOUT

3) High-Level Model (Normative)
3.1 Per-Tick Skeleton

Read/Plan (parallel) → Barrier → Commit (serial per chunk, deterministic) → Emit events → Build immutable snapshot.

Renderer consumes the snapshot only; it never touches live state. 

RENDERING_SNAPSHOT

3.2 Parallel Strategy

Inside a chunk: systems do not mutate; they emit Diff-Logs. Stage end merges diffs deterministically, then writes once.

Across chunks: use Actor messages (cross-chunk moves/flows/stocking). Messages are drained in stable order before local apply.

Write windows are governed by Update Order; stages parallelize across chunks only; no overlapping write sets. 

UPDATE_ORDER

4) Job Scheduler Spec (Normative)
4.1 Job Descriptor

stageId: which stage (matches Update Order) 

UPDATE_ORDER

chunkId: ownership (single chunk per job)

reads: set of layers/aux data the job reads (e.g., {L0,L2,L3,L4,Nav,Opac})

writes: must be empty for read/plan jobs; only merge/apply jobs write (and only to the owning chunk)

budget: iteration/time limits (e.g., FluidsStep F, FieldsStep G) 

UPDATE_ORDER

4.2 Scheduling Rules

No locks on sim path. Aliasing is prevented by: (1) per-chunk ownership, (2) empty writes in plan jobs, (3) single write point in merge/apply.

The scheduler rejects any job set with overlapping writes.

Stable iteration: Chunk jobs enumerate in ascending chunkId unless load-balancing is needed; outcome must be independent from worker order.

Budgets enforced per job; over-budget work spills to next tick (back-pressure).

5) Diff-Log (Intra-Chunk) (Normative)
5.1 Diff Entry (immutable)
{
  "op": "AddFluid",            // operation ID (data-driven)
  "target": {"x":12,"y":7,"z":3,"chunk":"C_10_4"},
  "layer": "L3",
  "args": {"amount": 5, "kind":"fluid.water"},
  "priority": 10,
  "systemId": "FluidsSolver",
  "localSeq": 42,
  "reason": "downhill flow"
}


Operation IDs and argument schemas are data-driven (JSON registries), not hardcoded enums. (See Data-Driven policy.) 

rules

5.2 Deterministic Merge

Sort key (within a chunk & tile):
TileIndex → Priority(desc) → SystemId(asc) → LocalSeq(asc)

Layer merge table (excerpt):

L3 Fluids: SUM → ClampToCapacity → BackPressure.

L4 Fields: MAX (e.g., smoke intensity) or SUM+Decay (configurable per field type).

L2 Constructions/Furniture: LAST-WRITE-BY-PRIORITY; loser emits a rejection event.

L5 Items: RESERVATION → ONE-WINNER (by priority → systemId); partial grants allowed.

L6 Occupancy: ONE-WINNER (species rules), or queue by speed.

L1 Surface: overlay blend with caps.
(Write windows per stage follow Update Order.) 

UPDATE_ORDER

5.3 Examples (Deterministic Outcomes)

Multi-source water into one tile: diffs sum to +12, capacity 8 ⇒ write +8, return 4 as back-pressure to sources in stable order. (Replays identical.)

Two haulers reserve same stack: A wants 3, B wants 2 from 5; same priority ⇒ systemId tie-break; both succeed (3+2). If only 4 remain: A gets 3, B gets 1 with PartialAccept event.

Door vs Wall on same tile: priorities decide; loser gets Rejected with reason.

5.4 Apply (Commit)

After merge, a single writer mutates the chunk for this stage; derived caches for dirty tiles + neighbors update incrementally. 

DATA_LAYOUT

6) Actor Messaging (Inter-Chunk) (Normative)
6.1 Mailbox & Order

Each chunk has a mailbox.

Drain order within a chunk: Tick → SenderChunkId → LocalSeq.

Messages are value types (immutable), safe to replay.

6.2 Message Types (core set)
{ "type":"MoveUnitIn", "unit":"u#123", "from":"C_9_4:(15,7,3)", "to":"C_10_4:(0,7,3)", "tag":"T123" }
{ "type":"BorderFlux", "from":"C_9_4:(15,4,3)", "to":"C_10_4:(0,4,3)", "amount":2, "kind":"fluid.water" }
{ "type":"PushItem", "item":"itm.log", "qty":3, "to":"C_10_4:(0,10,3)", "job":"J42" }

6.3 Apply & Reply

Receiver applies in local rules & capacity (e.g., occupancy, stack caps).

Replies: Accept, PartialAccept, Reject(reason); senders handle back-pressure next tick.

Determinism stems from drain order + local deterministic rules.

7) Update Order Integration (Normative)

Only the listed stages may write; each has its own RNG stream and stable iteration.

Stages parallelize across chunks; do not overlap writes; enforce with job masks. 

UPDATE_ORDER

Per-tick stages & typical concurrency hooks:

ApplyCommands — plan diffs; write L0/L2/L7. (Commit once.) 

UPDATE_ORDER

RebuildDerived — read base + overlays; write caches for dirty sets. 

UPDATE_ORDER

Support & Collapse — plan diffs (L0/L2/L5); commit in tile order. 

UPDATE_ORDER

FluidsStep — budgeted plan (diffs) + merge; capacity clamp; back-pressure. 

UPDATE_ORDER

FieldsStep — budgeted plan + merge (MAX or SUM+Decay). 

UPDATE_ORDER

Vegetation & Surface — plan+merge for L1. 

UPDATE_ORDER

Items — reservations & moves (diffs) + stack merges; inter-chunk via PushItem. 

UPDATE_ORDER

EmitEvents — publish compact stream. 

UPDATE_ORDER

BuildRenderSnapshot — immutable snapshot for renderer; never writes beyond this. 

RENDERING_SNAPSHOT

8) Data Layout & Caches (Normative)

Chunked SoA hot base (≤12B target) + sparse overlays (L2/L4/L5) + derived caches (Nav/Opac/Support). 

DATA_LAYOUT

Dirty propagation bounded: L0/L2 edit → tile + 6 neighbors; L3 → tile; L4 → LOS-local. 

DATA_LAYOUT

Maintain a per-chunk ConnectivityVersion to cheaply invalidate nav/LOS. 

DATA_LAYOUT

9) Determinism & RNG (Normative)

Fixed-step tick; no wall-clock in sim.

Per stage/system/chunk RNG streams; seed = WorldSeed ^ Hash(StageId,SystemId,ChunkId); never call RNG in an order-varying loop. 

UPDATE_ORDER

10) Stability & Degradation (Normative)

Boundary try-catch around: stage orchestrator, chunk-actor Apply, mod sandbox, I/O.

On exception: quarantine chunk/system for this tick → drop invalid diffs/messages → degrade that chunk/system to serial next tick → optionally freeze one tick and rebuild caches.

Never crash the loop; emit structured error with {seed,tick,stage,systemId,chunkId,tileIndex}. (See RULES: Stability.) 

rules

11) Configuration (Normative)

Budgets: F per-tick fluid cells; G per-tick field entries. (Back-pressure queues carry remainder.) 

UPDATE_ORDER

Merge table & message schemas are data-driven (JSON), validated at boot.

Feature flags: enable/disable actorization per system; serialize flags into saves.

12) Testing & CI Gates (Normative)

Determinism harness: replay seeds across OS/CPU; assert world/snapshot hash equality. 

milestones

Scheduler jitter fuzz: randomize job order/affinity; outputs must remain identical.

Budgets & perf: assert stage budgets/time; CI fails on regression. 

rules

Content validation: merge table/message schemas must pass strict JSON schema checks at boot.

13) LLM-Friendly Contracts (Normative)

To keep Codex/Claude reliable when editing/adding systems:

Do not modify sections outside declared anchors.

Diff-Log: Only produce entries with registered op IDs and schema; never mutate world in plan jobs.

Actor: Use only registered message types; all cross-chunk effects must go through messages.

Ordering: Always sort by the deterministic keys before merge/apply.

RNG: Request the stage/system/chunk stream; never inline a new RNG.

Docs: When changing merge strategies or messages, update the JSON schemas + changelog.

14) Checklists (Drop-in)
14.1 Scheduler Checklist

 Every job has stageId, chunkId, reads, writes, budget.

 No overlapping writes across running jobs.

 Plan jobs only read; merge/apply jobs own the chunk.

 Stable chunk iteration (unless load balancing; results stay identical).

14.2 Diff-Log Checklist

 Ops originate from registries (no hardcoded enums).

 Diffs carry priority, systemId, localSeq.

 Merge uses layer strategy table; ties broken deterministically.

 After apply, update dirty sets + derived caches only where needed.

14.3 Actor Checklist

 Messages use registered schemas; no side effects on send.

 Receiver drains in Tick → SenderChunkId → LocalSeq order.

 Replies (Accept/Partial/Reject) handled; back-pressure respected.

 Cross-chunk unit moves/flows/items use messages—never direct writes.

14.4 Stability Checklist

 Stage/Actor boundaries wrapped in try-catch; on error: quarantine → drop → degrade → (optional) freeze.

 Structured logs include {seed,tick,stage,systemId,chunkId,tileIndex} (rate-limited).

 CI includes determinism replays + jitter fuzz + performance gates.

15) Appendices
A. Layer Merge Table (Starter)

L3 Fluids — SUM → ClampToCapacity → BackPressure (tile capacity from data).

L4 Fields — MAX or SUM+Decay (per field type).

L2 Constructions/Furniture — LAST-WRITE-BY-PRIORITY.

L5 Items — RESERVATION → ONE-WINNER (priority → systemId), supports Partial.

L6 Occupancy — ONE-WINNER or queue by speed/size.

L1 Surface — blend with caps.
(Tied to Update Order write windows.) 

UPDATE_ORDER

B. Deterministic Keys

Tiles: chunkId << 24 | z << 16 | y << 8 | x

Jobs: (stageId, chunkId); Diffs: (tileKey, priority↓, systemId↑, localSeq↑)

Messages: (tick, senderChunkId, localSeq)

C. Data Layout Reference

Chunked SoA base (≤12B); overlays for L2/L4/L5; derived caches (Nav/Opac/Support); dirty propagation bounded.