FLUIDS_SOLVER_SPEC.md — v1 (Normative, Deterministic, Lock-Free)
id: fluids.v1
status: normative
owner: simulation/fluids
last_updated: 2025-09-14
depends_on:
  - CHUNK_ACTOR_PROTOCOL.md
  - DIFF_LOG_AND_MERGE_STRATEGIES.md
  - UPDATE_ORDER.md
  - material.v3
  - tuning.damage.json

0) Scope

Defines the tile-based fluid system (water, magma, oil, acid, etc.) for a DF-like fortress map:

Deterministic, multi-thread friendly, mass-conserving.

Works with per-chunk actors and diff-log + merge (no shared writes).

Data-driven: fluids and rates live in JSON registries (no hardcoded enums).

Crash-resistant: try–catch boundaries; bad content never crashes the loop.

1) Terms (Normative)

Fluid kind: a content entry (water, magma…) with physical & gameplay knobs.

Amount: integer milliliters (ml) per tile per kind (bounded).

Capacity: maximum ml a tile can hold (derived from geometry).

Head: proxy for pressure: head = z*head_per_level + amount/area_ml.

Active set: tiles that may change this tick (non-zero or with non-zero neighbors).

BorderFlux: message carrying cross-chunk fluid transfer.

2) Goals & Guarantees (Normative)

Mass conservation (± epsilon from clamping/evap).

Deterministic across OS/CPU/thread counts & seeds.

O(Active) update via an active list; sleeping tiles are skipped.

Lock-free sim path: cross-chunk via messages, intra-chunk via diffs only.

Simple model: shallow-water–like equalization with vertical gravity; supports 3D (Z).

3) Data Model (Normative)
3.1 Fluid Registry (content)

Path: /content/registries/fluids.json (see schema below)

id, tags[]

density_kg_m3, viscosity: affects transfer quota

miscible_group: "waterlike" | "oillike" | "acidlike" | "molten" (merge rules)

hazard: damage over time on units (optional)

render: color/depth curve (renderer snapshot uses it)

thermal: ignition_c?, boil_c?, extinguish_tag? (ties to items/actors)

evap_ml_s, seep_ml_s (sinks)

Mixing: kinds mix if they share miscible_group (amounts add). Otherwise immiscible: they compete for capacity via priority (see §5.5).

3.2 Per-tile state (authoritative)

Up to K channels per tile (K=2 by default, configurable in tuning) to limit memory:

channelA: { kindHandle:int16, ml:int32 }
channelB: { kindHandle:int16, ml:int32 } // optional if >0


Zero ml channels are absent.

Capacity cap_ml(tile) derived from L0 geometry (air/open/half-wall/ramp) via lookup in tuning.

3.3 Derived caches (rebuild on demand)

depth8 (0..8 for rendering), isActive bit, head (if cached), flow normal for visuals.

4) Update Order & Concurrency (Normative)

Stage: FluidsStep in your UPDATE_ORDER.

Per tick:

Plan (parallel, read-only)
For each active tile, compute outflow proposals to 6 neighbors (NESW + Up/Down) given head/viscosity/capacity.

Intra-chunk proposals → diff-log entries: {op: add_ml(kind, ±q), tile}

Cross-chunk proposals → BorderFlux messages per neighbor chunk.

Dispatch (framework): outboxes → destination mailboxes.

Drain (per chunk, deterministic)
Consume inbound BorderFlux (stable order) → translate to local diffs.

Barrier.

Merge+Apply (per chunk)
Deterministically combine diffs with SUM → CLAMP → BACK_PRESSURE (see §5.4/§5.5) and write tiles once.

Sleep/Wake
Mark tiles active if ml>0, neighbor has head deficit, or timers (evap/seep) are non-zero; otherwise sleep.

All write effects are local to Merge+Apply; Plan/Drain are read-only + diff/message append. No locks.

5) Flow Model (Normative)
5.1 Head & neighbor order

Head (per kind, per tile):
head = z * head_per_level + (amount_ml / area_ml)
head_per_level & area_ml live in tuning; no floating-point chaos—use integers or fixed-point.

Neighbor order is fixed: N, E, S, W, Down, Up for determinism.

5.2 Outflow quota (per tile, per substep)

For each neighbor n:

Δh = head(self) - head(n)
if Δh <= head_epsilon: q = 0
else q = min( max_transfer_ml(kind, Δh), free_space_ml(n), remaining_budget )


max_transfer_ml scales with Δh and viscosity (from fluid registry).

remaining_budget is the tile’s per-substep budget from tuning (prevents oscillation).

5.3 Substeps & budgets

Run U substeps per tick (U in tuning).

Per-kind tile budget ml_per_substep_max caps how much a tile can emit in each substep.

5.4 Merge rule (SUM → CLAMP → BACK_PRESSURE)

Per tile, per kind:

SUM all inbound diffs (self outflows are negatives).

CLAMP to [0, cap_ml(tile)].

BACK_PRESSURE: if sum exceeds cap, compute overflow = sum - cap.

Same-chunk senders: attribute overflow back to contributors by deterministic proportion (their contribution order).

Cross-chunk senders: emit BorderFluxReturn messages with amountReturned.

Any leftover due to saturation is dropped only if explicitly configured in tuning (default: never drop).

5.5 Immiscible kinds (capacity competition)

If multiple kinds exist and K=2 is exceeded by inbound mixes:

Keep the dominant kind by priority (tuning) or density (heavier → bottom slot).

Spill the losing kind via back-pressure to its sources.

If both are present and K=2, order them by density (bottom/top).

5.6 Vertical exchange

Down is favored by an extra gravity bias in max_transfer_ml.

Up only if Δh is large enough (pressurized or buoyant light fluid under heavy fluid).

5.7 Sinks & sources

Evaporation: remove up to evap_ml_s * dt (per kind).

Seep: remove up to seep_ml_s * dt into porous tiles (rubble/soil flags).

Inflow: pumps/fixtures inject ml (via systems), still through diffs.

6) Cross-Chunk Messaging (Normative)

Message type: BorderFlux (and optional BorderFluxReturn) — see CHUNK_ACTOR_PROTOCOL.

Sender (Plan): batch per destination edge; cap by remaining_budget.

Receiver (Drain): convert to local diffs; if overflow during Merge+Apply, emit BorderFluxReturn for next tick.

Stable ordering: mailbox drained by (tick → senderChunkId → localSeq).

7) Integration with Items & Jobs (Normative)

Buckets/Containers: convert tile ml ↔ item charges in g/ml (unit in item spec).

Pumps: systems create diffs that move ml across tiles/chunks; they do not bypass the fluid stage.

Fire/Extinguish: if a tile holds waterlike ≥ threshold, remove Burning field; magma can set Burning on flammables (via thermal thresholds of materials).

8) Thermal & Hazards (Very Simple)

Each fluid kind may have:

thermal.ignition_c (e.g., oil), thermal.boil_c (e.g., water).

World hazards can set ambient temperature per tile.

If ambient ≥ ignition_c for ignite_min_seconds (tuning) → spawn Burning field (content ID).

If ambient ≥ boil_c → remove ml at an accelerated evap rate; optional steam field event.

Workshops craft directly; the thermal model does not gate recipes.

9) Tuning (Normative JSON)
9.1 /content/registries/tuning.fluids.json (no comments)
{
  "version": 1,
  "substeps": 2,
  "head_per_level": 1000,
  "area_ml": 1000,
  "head_epsilon": 1,
  "tile_budget_ml_per_substep": 800,
  "k_channels_max": 2,
  "capacity_ml": {
    "open": 1000,
    "half_wall": 600,
    "ramp": 800,
    "solid": 0
  },
  "priority": {
    "immiscible_order": ["molten","waterlike","oillike","acidlike"]
  },
  "return_on_overflow": true,
  "evap_global_scale": 1.0,
  "seep_global_scale": 1.0,
  "viscosity_scale": {
    "linear_a": 1.0,
    "linear_b": 0.0
  },
  "gravity_bias_down_ml": 200,
  "thermal": {
    "ignite_min_seconds": 2,
    "boil_bonus_evap_scale": 3.0
  }
}


Interpretation (engine rules)

head_per_level/area_ml define the fixed-point for head.

tile_budget_ml_per_substep caps outflow per tile per substep.

capacity_ml is looked up from the tile’s L0 geometry flag.

priority.immiscible_order decides which kind claims capacity when K would be exceeded.

viscosity_scale adjusts max_transfer_ml = (linear_a - linear_b*viscosity) * Δh, clamped ≥ 0.

gravity_bias_down_ml adds to max_transfer_ml toward Down.

10) Fluid Registry Schema (Normative JSON)
10.1 /content/registries/schemas/fluid.v1.schema.json (no comments)
{
  "$id": "fluid.v1.schema.json",
  "type": "object",
  "required": ["id","tags","density_kg_m3","viscosity","miscible_group","render"],
  "properties": {
    "id": { "type": "string" },
    "tags": { "type": "array", "items": { "type": "string" } },
    "density_kg_m3": { "type": "number", "minimum": 1 },
    "viscosity": { "type": "number", "minimum": 0 },
    "miscible_group": { "type": "string", "enum": ["waterlike","oillike","acidlike","molten"] },
    "hazard": {
      "type": "object",
      "properties": {
        "dot_per_second": { "type": "number", "minimum": 0 },
        "damage_type": { "type": "string", "enum": ["acid","fire","cold","electric","arcane"] }
      },
      "additionalProperties": false
    },
    "render": {
      "type": "object",
      "properties": {
        "color_rgba": { "type": "array", "items": { "type": "integer", "minimum": 0, "maximum": 255 }, "minItems": 4, "maxItems": 4 },
        "depth8_curve": { "type": "string" }
      },
      "required": ["color_rgba"],
      "additionalProperties": false
    },
    "thermal": {
      "type": "object",
      "properties": {
        "ignition_c": { "type": "number", "minimum": 0 },
        "boil_c": { "type": "number", "minimum": 0 },
        "extinguish_tag": { "type": "string" }
      },
      "additionalProperties": false
    },
    "evap_ml_s": { "type": "number", "minimum": 0 },
    "seep_ml_s": { "type": "number", "minimum": 0 }
  },
  "additionalProperties": false
}

10.2 Example fluids (no comments)

Water

{
  "id": "fluid_water",
  "tags": ["water","neutral"],
  "density_kg_m3": 1000,
  "viscosity": 1.0,
  "miscible_group": "waterlike",
  "render": { "color_rgba": [64, 128, 255, 180], "depth8_curve": "soft" },
  "thermal": { "boil_c": 100, "extinguish_tag": "burning" },
  "evap_ml_s": 0.1,
  "seep_ml_s": 0.0
}


Magma

{
  "id": "fluid_magma",
  "tags": ["lava","hot","molten"],
  "density_kg_m3": 2800,
  "viscosity": 8.0,
  "miscible_group": "molten",
  "render": { "color_rgba": [255, 80, 16, 220], "depth8_curve": "glow" },
  "thermal": { "ignition_c": 300, "boil_c": 1200 },
  "evap_ml_s": 0.0,
  "seep_ml_s": 0.0
}


Oil

{
  "id": "fluid_oil",
  "tags": ["oil","flammable"],
  "density_kg_m3": 850,
  "viscosity": 3.0,
  "miscible_group": "oillike",
  "render": { "color_rgba": [40, 40, 40, 180] },
  "thermal": { "ignition_c": 220 },
  "evap_ml_s": 0.02,
  "seep_ml_s": 0.0
}


Acid

{
  "id": "fluid_acid",
  "tags": ["acid","hazard"],
  "density_kg_m3": 1100,
  "viscosity": 1.2,
  "miscible_group": "acidlike",
  "hazard": { "dot_per_second": 3, "damage_type": "acid" },
  "render": { "color_rgba": [120, 255, 80, 200] },
  "evap_ml_s": 0.0,
  "seep_ml_s": 0.0
}

11) Rendering Snapshot (Informative)

Renderer reads snapshot only; never touches live sim.

Each tile exposes: {depth8, top_color, foam_flag?} from a prebuilt snapshot pass.

Changes only for dirty tiles; chunk-local rebuild.

12) Error Handling & Stability (Normative)

Plan/Drain/Merge wrapped in try–catch.

Malformed content or overflow in queues:

Drop offending message/diff entries (deterministic order), quarantine the tile for the tick.

Log structured error {tick, stage, chunk, tile, kind, reason}, rate-limited.

If per-tile budget is exceeded frequently, system throttles (substeps or budgets) but never crashes.

13) Tests & CI (Normative)

Mass conservation: ∑ml + sinks − sources constant within epsilon.

Border exchange: two-chunk waterfall test equals single-chunk baseline.

Determinism fuzz: worker jitter → identical hashes.

Immiscible stress: K-limit retention is stable (priority & density tie-break deterministic).

Thermal: ignition/boiling thresholds respected; extinguish_tag clears the field at water threshold.

14) Engine Hooks (Summary)

Stage name: FluidsStep

Messages: BorderFlux, BorderFluxReturn

Diffs: add_ml(kind, ±q) per tile; all writes happen in Merge+Apply only.

Tuning files:

/content/registries/tuning.fluids.json (flow & capacity)

/content/registries/fluids.json (kinds & hazards)