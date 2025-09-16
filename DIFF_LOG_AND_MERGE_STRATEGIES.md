0) Scope

Defines the diff-log format, lifecycle, deterministic ordering, and merge strategies used to safely combine many parallel proposals into a single, predictable write per tile and stage.

Applies to all simulation systems that run in Plan (read-only) phase and emit diffs; integrates with the Job Scheduler, Update Order, Chunk/Data Layout, and Rules docs.

1) Core Concepts (Normative)

Diff: an immutable proposal (no side effects) produced during Plan. Diffs are collected per chunk and tile.

Merge: a deterministic reduction of 0..N diffs → 0..1 authoritative write(s) for that tile within the current stage.

Commit: a single writer per chunk applies merged results; derived caches update incrementally for dirty tiles + neighbors.

Deterministic keys: whenever order matters, sort by:
TileKey → Priority(desc) → SystemId(asc) → LocalSeq(asc).

Write windows: a stage can only write its allowed layers; other writes are rejected (see Update Order).

No direct mutation in Plan: systems must never mutate the world outside the Merge+Apply step.

2) Diff Entry Schema (Normative)

All diffs conform to this LLM-friendly JSON shape (exact fields; extensible via args):

{
  "op": "AddFluid",
  "target": { "x": 12, "y": 7, "z": 3, "chunk": "C_10_4" },
  "layer": "L3",
  "args": { "amount": 5, "kind": "fluid.water" },
  "priority": 10,
  "systemId": "FluidsSolver",
  "localSeq": 42,
  "reason": "downhill flow",
  "trace": { "jobId": "S4-Fluids-C_10_4", "subsystem": "fluids" }
}


Field rules

op: registered operation ID (data-driven registry; no hardcoded enums).

target: owning chunk at time of emit; x/y/z are within that chunk’s bounds (halo reads allowed, but targets must be in-bounds).

layer: one of L0..L7; must be permitted by the current stage.

priority: integer; higher wins in conflicts (use narrow bands: 0, 5, 10, 15…).

systemId: stable ASCII identifier of the emitter (e.g., "HaulSys", "Collapse").

localSeq: monotonic per system per job; used only for deterministic tiebreak.

args: validates against the op schema (see Appendix A).

reason/trace: optional diagnostics; not used for ordering.

Validation on ingest

Unknown op or invalid schema → reject diff (record error event).

layer not writable in this stage → reject diff.

target out of bounds / wrong chunk → reject diff.

Rejected diffs never crash the stage; they are dropped with reason.

3) Diff Lifecycle (Normative)

Emit: Plan jobs create diffs and append to the chunk-local diff buffer (thread-safe).

Collect: end of Plan, buffers are sealed per chunk.

Bucket: group diffs by tileKey; prepare per-tile lists.

Order: sort each per-tile list by deterministic keys.

Merge: apply the Layer Strategy to reduce the list into concrete writes (and optional events/back-pressure).

Commit: apply writes once; update dirty sets + derived caches; enqueue events.

4) Strategy Primitives (Normative)

AGGREGATE.SUM: add numeric contributions (associative/commutative).

AGGREGATE.MAX/MIN: select extrema.

SELECT.PRIORITY_LAST: take the last write by Priority(desc) → SystemId → LocalSeq.

ONE_WINNER: choose exactly one claimant; others get rejection/partial events.

CLAMP(capacityFn): enforce per-tile data-driven capacity.

BACK_PRESSURE: return surplus to sources in stable order for future ticks.

BLEND: bounded linear blend for overlays (e.g., surface decals).

SET/UNSET: idempotent bit/flag manipulation.

COMPOSITE: chain primitives; e.g., SUM → CLAMP → BACK_PRESSURE.

5) Per-Layer Merge Strategies (Normative)

Each row specifies allowed ops, the strategy, and invariants. The strategy runs within one stage; cross-stage interactions follow Update Order.

5.1 L0 Terrain (topology)

Ops: Dig, Channel, BuildFloor, BuildWall, CarveRamp, PlaceStairs, RemoveStairs

Strategy: SELECT.PRIORITY_LAST

Higher priority terrain op replaces lower.

Invariants: topology changes mark tile + 6 neighbors dirty; bump ConnectivityVersion.

Notes: final validation (e.g., support) happens in Support & Collapse stage.

5.2 L1 Surface (surface/vegetation skin)

Ops: SetSurface, BlendSurface, Grow, Trample

Strategy: BLEND with caps (data-driven per prototype).

Invariants: L1 never blocks movement; only modifies visuals/light modifiers.

5.3 L2 Constructions/Furniture

Ops: PlaceFurniture(id, orient), RemoveFurniture(id), ToggleOpenClose

Strategy: SELECT.PRIORITY_LAST

Losers emit Rejected(op_conflict) event.

Invariants: contributes to support/opacity/passables; topology-like effects mark neighbors dirty.

5.4 L3 Fluids (kind + depth 0..7)

Ops: AddFluid(kind, Δdepth), SetDepth, Evaporate(Δ), Freeze, Melt

Strategy: COMPOSITE = AGGREGATE.SUM (by kind) → CLAMP(capacityFn) → BACK_PRESSURE

Capacity comes from tile proto (L0/L2 geometry).

Surplus is returned to sources in deterministic source order.

Invariants: depth is clamped to [0..maxDepth]; cross-kind rules (e.g., water+lava=steam) emit diffs to L4 next stage.

5.5 L4 Fields (gases/decals/fire/etc.)

Ops: AddField(id, Δintensity), SetField(id, intensity), Decay(id, Δ)

Strategy: per field type either AGGREGATE.MAX (e.g., smoke density takes max) or SUM+Decay (bounded).

Invariants: multiple fields per tile allowed; intensity range and decay rates are data-driven.

5.6 L5 Items (stacking/reservations)

Ops: Reserve(itemId, qty), Assign(itemId, qty, owner), MergeStacks, SplitStack, DropTo(tile)

Strategy: ONE_WINNER for mutually exclusive claims; AGGREGATE.SUM for stack merges; PartialAccept allowed.

Tie-break: Priority(desc) → SystemId → LocalSeq.

Rejections/partials generate typed events for job planners.

Invariants: stacks respect per-prototype caps; ownership and stockpile policies are enforced here.

5.7 L6 Units/Vehicles (occupancy)

Ops: ClaimOccupancy(unit), ReleaseOccupancy(unit), QueueEntry(unit)

Strategy: ONE_WINNER or QUEUED by speed/size when design requires.

Invariants: species-specific block rules; no permanent writes outside unit systems unless explicitly staged.

5.8 L7 Meta/Markers (designations/rooms/traffic/etc.)

Ops: SetDesignation(id), UnsetDesignation(id), SetTraffic(cost)

Strategy: SET/UNSET or SELECT.PRIORITY_LAST depending on field.

Invariants: metadata only; no direct physics.

6) Cross-Conflict Rules (Normative)

Same tile, same layer: resolved by that layer’s strategy.

Same tile, different layers: both may apply if write windows allow; but Support & Collapse later may invalidate impossible states (e.g., furniture in empty space).

Stage isolation: conflicts are not resolved across stages in the same tick; later stages see committed results only.

7) Back-Pressure & Partial Acceptance (Normative)

Back-pressure (fluids): after CLAMP, distribute surplus to sources in deterministic order. Record {source→returned} in the audit trail for replay.

Partial acceptance (items): if capacity allows subset, create Assign(q_accepted) and emit PartialAccept(q_remainder) event.

Queue stability: carryover order is keyed by (chunkId, tileKey, systemId, localSeq) so replays match.

8) Deterministic Ordering Keys (Normative)

TileKey: (chunkId << 24) | (z << 16) | (y << 8) | x.

Per-tile sort: Priority(desc) → SystemId(asc) → LocalSeq(asc).

Commit across chunks: ascending chunkId.

Messages (for cross-chunk effects) are ordered by: Tick → SenderChunkId → LocalSeq (see Actor spec).

9) Validation & Safety (Normative)

Pre-merge validation: schema, allowed layer, stage write window, target bounds.

Invariant checks: capacity ranges, stack limits, legal orientations, required neighbors (e.g., door needs frame).

Failure policy: on invalid diff → drop with ERR_DIFF_INVALID; on strategy exception → drop tile’s conflicting ops, quarantine chunk for this tick, degrade next tick; never crash.

Logging: structured entry with {seed,tick,stage,chunkId,tileKey,op,systemId,reason} (rate-limited).

10) Performance & Memory (Normative)

Compaction: where commutative, pre-combine diffs with identical (tileKey, op, args-key) to shrink lists.

Pooling: diff buffers are pooled per worker; merge lists use struct nodes to avoid allocations.

Budgets: per stage iterations/ms enforced; long lists may yield and continue next tick (see Job Scheduler).

11) LLM Integration Rules (Normative)

Emit only: systems in Plan must only emit diffs and/or messages; never mutate world.

Registered ops: use only ops whose schemas exist in the registry; don’t invent new fields.

Sort before merge: rely on scheduler to sort; do not hand-sort differently in systems.

No RNG in order-sensitive loops: if randomness is required for emitting diffs, sort targets first, then sample.

Events: when your diff could be rejected/partial, emit typed events (e.g., Rejected(op_conflict), PartialAccept(q)).

12) CI & Tests (Normative)

Golden seeds: diffs → merges → commits produce identical hashes across OS/CPU (determinism harness).

Merge property tests:

L3: SUM → CLAMP(cap) → BACK_PRESSURE conserves mass: in = write + returned.

L5: reservations never over-assign; winners are deterministic under tied priorities.

L2: priority-last is idempotent given identical inputs.

Jitter fuzz: randomize the Plan scheduling and per-chunk merge order of diffs (post-sort results must remain identical).

Schema validation: reject invalid op payloads; tests include negative cases.

13) Checklists (Drop-in)
13.1 Adding a New Diff Op

 Add registry entry: opId, allowed layer, JSON schema for args, default priority.

 Define merge behavior via primitives (SUM/MAX/… or composite).

 Write invariants (ranges, capacityFn).

 Unit tests: success, overflow (clamp/back-pressure), invalid schema, determinism under shuffle.

13.2 Implementing a Merge

 Bucket by tileKey; sort by deterministic keys.

 Apply strategy; enforce invariants; produce at most one write per layer per tile per stage (unless strategy explicitly returns multiple).

 Update dirty sets and derived for affected tiles.

 Emit events (Rejected, PartialAccept, OverflowReturned).

13.3 Debugging a Conflict

 Turn on audit trail (store last 32 diffs per tile).

 Inspect ordered diffs + chosen winner(s) + returned amounts.

 Verify layer write window and capacityFn.

 Replay with scheduler jitter; outcome must match.

14) Worked Examples (Informative)

Fluids: three sources to one sink

Diffs: +5, +4, +3 water → SUM=+12 → CLAMP(cap=8) → write +8, BACK_PRESSURE returns 4 to sources by (Priority↓, SystemId↑, LocalSeq↑).

Items: two haulers reserve 5 logs

Diffs: A: +3, B: +2 from the same stack; equal priority → ONE_WINNER with tiebreak; both succeed (exact fit).

If only 4 remain: A gets 3, B gets 1 + PartialAccept(1).

Furniture vs Furniture

Diffs: Place door (prio 15) vs Place wall (prio 10) on same tile → SELECT.PRIORITY_LAST picks door; loser emits Rejected(op_conflict).

15) Appendix A — Op Registry Schema (Starter)
{
  "$id": "op.schema.json",
  "type": "object",
  "properties": {
    "op": { "type": "string" },
    "layer": { "type": "string", "enum": ["L0","L1","L2","L3","L4","L5","L6","L7"] },
    "args": { "type": "object" },
    "priority": { "type": "integer" }
  },
  "required": ["op","layer","args","priority"],
  "additionalProperties": false
}


Examples (payloads)

AddFluid: { "op":"AddFluid", "layer":"L3", "args":{ "amount":int, "kind":"fluid.id" }, "priority":10 }

PlaceFurniture: { "op":"PlaceFurniture", "layer":"L2", "args":{ "id":"furn.id", "orient":"N|E|S|W" }, "priority":15 }

Reserve: { "op":"Reserve", "layer":"L5", "args":{ "item":"item.id", "qty":int }, "priority":20 }

16) Appendix B — Capacity Functions (Informative)

Fluids: capacityFn(tile) = baseCapacity(L0/L2) × modifiers(L1, flags); typically 0..7.

Items: capacityFn(tile, itemProto) = stackCap(proto) × slotPolicy(tile).

Fields: cap = perFieldMaxIntensity; Decay applies after clamp.

17) Appendix C — Event Types (Starter)

Rejected { op, reason, tileKey, systemId }

PartialAccept { op, accepted, leftover, tileKey, systemId }

OverflowReturned { kind, amount, toSource, tileKey }

CapacityExceeded { cap, requested }