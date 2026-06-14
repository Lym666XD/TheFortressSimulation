SIM_LOD_POLICY.md — v1 (Normative)
id: sim.lod.v1
status: normative
owner: engine/world
last_updated: 2025-09-15
applies_to:
  - RegionInstanceManager (RIM)
  - World/Chunk lifecycle
  - All simulation systems in UPDATE_ORDER
  - JobScheduler, Mailbox, Persistence
goals:
  - Keep large worlds responsive via graded simulation.
  - Preserve determinism across thread counts/OS.
  - Prevent oscillation via hysteresis and stable keys.
inputs:
  - Player camera & controlled parties (embark/adventure)
  - Chunk heat (recent combat, jobs, hazards, fields)
  - Pinned reasons (UI focus, work orders, incidents)
artifacts:
  - /content/registries/tuning.sim_lod.json (data-driven)
  - tuning.sim_lod.schema.json (JSON Schema)

0) Levels (L0–L4) and Hard Contracts

L0 Active — full-rate simulation; all systems run at base tick (50 TPS).

L1 Near — reduced frequency; heavy systems decimated; AI throttled; mailbox deliverable; jobs allowed with limits.

L2 Far — no actor AI/pathing; background integrators only (aging/decay/growth/counters); mailbox buffered (no delivery).

L3 Dormant — no per-chunk sim; chunk may remain in memory; mailbox buffered; dirtiness can accrue from background writes = false (no writes).

L4 Unloaded — chunk not resident; state must be on disk (if persistent) or absent (if ephemeral instance); mailbox stores undelivered envelopes on disk.

Only L0/L1 mutate authoritative state per tick. L2 mutates via coarse background integrators (bounded, deterministic). L3/L4 never mutate until promoted.

1) Deterministic Level Assignment
1.1 LOD function (pure & stable)
L = f( d = min_chunk_distance_to(camera_or_party),
       heat = HeatScore(chunk),
       pinned = PinnedReasons(chunk),
       incident = IncidentMask(chunk) )


Distance bands (in chunks): R0 < R1 < R2 → map to L0/L1/L2; beyond R2 → L3/L4.

HeatScore: decays per tick; increments on combat hits, fires, active fields, crafting throughput, job density.

PinnedReasons: (UI inspect, stockpile filter editor, blueprint preview, build-in-progress, artifact present, queued caravan, storyteller target).

IncidentMask: siege/quest site/controlled caravan → bump at least to L1.

1.2 Hysteresis (no flapping)

Up-shift thresholds: R0↑, R1↑, R2↑; down-shift thresholds: R0↓=R0↑+1, etc.

Heat hysteresis: promote to L0 if heat >= H_hot; demote only once heat <= H_cold.

Pinning timeouts: each pin carries ttl_ticks; expiring pin triggers re-evaluation.

All thresholds are read from tuning.sim_lod.json and are deterministic; no wall-clock usage.

2) System Participation by Level (Normative)
2.1 Tick decimation (per system)

Let base step be Δt=20ms. A system S at level L runs every k(S,L) ticks.

L0 Active: k=1 for all systems.

L1 Near (examples):

Fluids/Fields: k=2..3

AI/Combat: k=2

Economy/Jobs: k=2

Nav/Connectivity maintenance: k=4 (on-demand if requests exist)

L2 Far:

AI/Combat/Nav: disabled

Fluids/Fields: background integrator (bounded batches, see §3)

Economy/Jobs: disabled (no new work; reservations timeout deterministically)

Aging/Rot/Growth/Counters: enabled via background integrator

L3 Dormant, L4 Unloaded: all systems disabled.

2.2 UPDATE_ORDER deltas

At L1, systems run in normal UPDATE_ORDER with decimation multipliers.

At L2, only BackgroundSystem runs (see §3) at the end of the frame, after mailbox buffering (no delivery).

3) Background Integrators (L2 only)

Deterministic coarse updates executed by BackgroundSystem:

Aging & Rot: increment timers; apply thresholded state changes (e.g., rot stage advancement) at fixed quanta.

Plants/Crops: growth counters; no pathing/AI; capped per tick batch, round-robin spread.

Fields: decay fire/miasma/poison by fixed amounts; cap total energy removed per tick to budget.fields_l2.

Fluids: disabled at L2 (no flow); optional: bleed toward equilibrium using a stable clamp if configured fluids_equilibrium=true.

Temperature (if present): converge to ambient in fixed quanta; never creates chain reactions.

Counters: infestation timers, job retry cooldown clocks.

Budgets

max_tiles_per_tick and max_entities_per_tick caps ensure O(1) work per frame.

Worklists iterate in stable order (chunkKey → tileIndex asc).

Catch-up

Each chunk maintains sleep_accum_ticks; when promoting L2→L1/L0, integrator runs once applying min(sleep_accum_ticks, cap_catchup) with the same deterministic loop, then zeroes accumulator.

4) Mailbox, Jobs, and Nav under LOD
4.1 Mailbox

L0/L1: deliver and apply envelopes in order (tick, senderChunkId, seq).

L2/L3/L4: buffer only; do not deliver. The mailbox_cursor remains unchanged.

Buffer is bounded; if over capacity, drop lowest-priority envelopes by deterministic key, emit E_MAIL_REPLAY WARN (never crash).

4.2 JobScheduler

Assignments: workers may only accept tasks in L0/L1 chunks.

Reservations: a reservation targeting L2+ expires after reservation_ttl_ticks; on expiry, inputs are released deterministically.

Hauling: cross-level hauling to sleeping targets is forbidden; requests are re-queued with backoff.

4.3 Nav/Reachability

Nav caches and connectivity maintenance run only in L0/L1.

Caches for chunks demoted to L2+ are evicted by LRU; eviction order is deterministic.

5) Persistence Interaction

Dirty flags: only L0/L1 (and L2 via BackgroundSystem writes) can mark a chunk dirty.

Save barrier: L2 writes are as authoritative as L0/L1 (rare and bounded).

Dormant/Unloaded: no writes → no dirtiness; unload at RIM discretion.

Instances: non-active persistent instances (not currently entered) are Dormant by definition; “aging” is applied lazily using last_visit_tick when the instance is reactivated (deterministic catch-up).

6) Promotion/Demotion Protocol

Evaluate LOD function each frame for candidates (tiles within R2+1 of the camera and any pinned chunks; others every N frames).

Demotion:

L0→L1/L2: finalize current write phase; flush pending diffs.

L1→L2: flush mailbox deliveries; thereafter buffer only.

L2→L3: stop BackgroundSystem; clear per-level worklists.

L3→L4: serialize if dirty, free memory (ChunkRepository).

Promotion:

L4→L3: load into memory (no sim), warm minimal indices.

L3→L2: start BackgroundSystem (accumulator=0).

L2→L1/L0: run catch-up (§3), then enable decimated/full systems.

Hysteresis: promotions/demotions require passing opposite thresholds to avoid oscillation.

All steps are executed at the tick barrier; never mid-phase.

7) Tuning File (Data-Driven)

Path: /content/registries/tuning.sim_lod.json
Schema: tuning.sim_lod.schema.json

7.1 Example (tuning.sim_lod.json)
{
  "radii_in_chunks": { "R0_active": 2, "R1_near": 4, "R2_far": 6 },
  "hysteresis": { "extra_out": 1, "heat_hot": 10, "heat_cold": 3 },
  "pins": {
    "ui_focus_ttl": 600,
    "build_in_progress_ttl": 1800,
    "artifact_present_promote": "L1"
  },
  "decimation_k": {
    "AI":   { "L0": 1, "L1": 2, "L2": 0 },
    "Jobs": { "L0": 1, "L1": 2, "L2": 0 },
    "Nav":  { "L0": 1, "L1": 4, "L2": 0 },
    "Fluids": { "L0": 1, "L1": 3, "L2": 0 },
    "Fields": { "L0": 1, "L1": 2, "L2": 1 },
    "Background": { "L0": 0, "L1": 0, "L2": 1 }
  },
  "background_budgets": {
    "max_tiles_per_tick": 512,
    "max_entities_per_tick": 128,
    "fields_decay_per_tick": 0.05,
    "temperature_step_c": 0.2,
    "fluids_equilibrium": false,
    "catchup_cap_ticks": 2000
  },
  "jobs": {
    "reservation_ttl_ticks": 1500,
    "allow_cross_level_assignments": false
  },
  "mailbox": {
    "buffer_cap": 4096,
    "drop_policy": "drop_low_priority"
  },
  "evaluation": {
    "full_scan_every_ticks": 30
  }
}

7.2 Schema (excerpt)
{
  "$id": "tuning.sim_lod.schema.json",
  "type": "object",
  "required": ["radii_in_chunks", "decimation_k", "background_budgets"],
  "properties": {
    "radii_in_chunks": {
      "type": "object",
      "required": ["R0_active", "R1_near", "R2_far"],
      "properties": {
        "R0_active": { "type": "integer", "minimum": 0 },
        "R1_near":   { "type": "integer", "minimum": 1 },
        "R2_far":    { "type": "integer", "minimum": 1 }
      }
    },
    "hysteresis": {
      "type": "object",
      "properties": {
        "extra_out":  { "type": "integer", "minimum": 0 },
        "heat_hot":   { "type": "integer", "minimum": 0 },
        "heat_cold":  { "type": "integer", "minimum": 0 }
      }
    },
    "decimation_k": { "type": "object", "additionalProperties": { "type": "object" } },
    "background_budgets": {
      "type": "object",
      "required": ["max_tiles_per_tick", "max_entities_per_tick", "catchup_cap_ticks"],
      "properties": {
        "max_tiles_per_tick": { "type": "integer", "minimum": 1 },
        "max_entities_per_tick": { "type": "integer", "minimum": 1 },
        "fields_decay_per_tick": { "type": "number", "minimum": 0 },
        "temperature_step_c": { "type": "number", "minimum": 0 },
        "fluids_equilibrium": { "type": "boolean" },
        "catchup_cap_ticks": { "type": "integer", "minimum": 0 }
      }
    },
    "jobs": {
      "type": "object",
      "properties": {
        "reservation_ttl_ticks": { "type": "integer", "minimum": 1 },
        "allow_cross_level_assignments": { "type": "boolean" }
      }
    },
    "mailbox": {
      "type": "object",
      "properties": {
        "buffer_cap": { "type": "integer", "minimum": 64 },
        "drop_policy": { "type": "string", "enum": ["drop_low_priority", "drop_oldest"] }
      }
    },
    "evaluation": {
      "type": "object",
      "properties": { "full_scan_every_ticks": { "type": "integer", "minimum": 1 } }
    }
  },
  "additionalProperties": false
}

8) Code Entities (Binding)

World.SleepPolicy — pure function façade around tuning.sim_lod.json.

ChunkActivityTracker — maintains HeatScore and recent flags.

LodService — owns per-chunk level, hysteresis, promotion/demotion at barrier.

BackgroundSystem — executes integrators in UPDATE_ORDER at the end.

MailboxService — mode-aware (deliver vs buffer) with deterministic queue.

JobScheduler — obeys LOD (assignment filter; TTL).

Nav.PathService — disabled outside L0/L1; evicts caches on demotion.

Persistence.SaveManager — respects dirty flags from L0/L1/L2 only.

RIM.RegionInstanceManager — treats non-active instances as Dormant; applies lazy catch-up on activation using last_visit_tick.

9) Determinism Requirements

All LOD decisions derive only from deterministic inputs (positions in chunks, heat counters, pins, incidents) with stable thresholds.

Background integrators use stable iteration orders and bounded budgets.

Mailbox buffering preserves envelope order; no delivery at L2+.

RNG streams: background integrators use named streams (e.g., Background/L2/<cx,cy,cz>).

10) Error & CI

If a decimation or background step throws, quarantine the chunk for that system (5s), log E_CHUNK_STEP.

CI determinism suite must include runs that toggle chunks across L0↔L2 based on scripted camera paths; hashes must remain equal.

Add a dev toggle: lod.override = L0|L1|L2|auto per chunk for debugging.