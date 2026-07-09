id: field.spec.v1
status: normative
owner: sim/fields
last_updated: 2025-09-15
applies_to:
  - L4 “Fields” overlay (gases, fumes, weather, decals, magical auras)
  - SnapshotBuilder (dominant field glyph)
  - UPDATE_ORDER: FieldsStep
goals:
  - Deterministic, budgeted, low-cost environmental effects.
  - Data-driven prototypes (no hard-coded field types).
  - Clear deterministic read/write boundaries; future chunk-partitioned read parallelism must preserve stable diff ordering.
non_goals:
  - Full CFD or temperature continuum (v1 stays discrete & local).
  - Multi-map propagation.
1) Runtime Model (L4 Overlay)
A cell may hold 0..N field instances; each instance is (id, intensity, age).

Intensity (byte 0..255) is the only per-instance strength.

Age (ushort) increments on each field tick (for timers/fades).

Per-cell list is sparse and pooled; absent for the majority of cells.

All per-tick work is budgeted; iteration in stable order: (ChunkKey asc) → (LocalIndex asc) → (field.id asc).

Interaction surface with other layers:

LOS: field opacity is f(intensity) (prototype-defined), combined with L0/L2 opacity.

Damage/Effects: optional “contact” or “air” effect (prototype → tuning.damage table).

Fluids: water extinguishes flame; depth throttles field spread (if enabled).

Materials: flammability gate (ignite creates flame and often smoke).

2) In-Memory Data Structures
csharp
Copy code
public sealed class FieldCell
{
    public ushort Id;        // registry index for field prototype
    public byte   Intensity; // 0..255 (0 means remove)
    public ushort Age;       // ticks since spawned
}

public sealed class Chunk  // (excerpt)
{
    public Dictionary<int, List<FieldCell>> Fields = new(); // key=LocalIndex
}
The list is ordered by field.Id ascending (enforced after mutations) to keep deterministic serialization & stepping.

If the list becomes empty, remove the (idx → list) entry.

No per-instance flags; use prototype data to interpret Intensity and behavior.

3) Prototypes & Schemas (Data-Driven)
Registry file: /content/registries/fields/*.json
Schema: /content/schemas/field.schema.json (v1)

3.1 field.schema.json (normative fields)
json
Copy code
{
  "$schema_version": 1,
  "type": "object",
  "required": ["id", "category", "render", "rules"],
  "properties": {
    "id": { "type": "string", "pattern": "^[a-z0-9_.:-]+$" },
    "tags": { "type": "array", "items": {"type":"string"} },

    "category": {
      "type": "string",
      "enum": ["gas","smoke","flame","miasma","decal","aura","weather"]
    },

    "render": {
      "type": "object",
      "required": ["glyph", "color"],
      "properties": {
        "glyph": { "type": "string" },          // tileset key or SadConsole glyph
        "color": { "type": "string" },          // #RRGGBB or palette key
        "priority": { "type": "integer", "minimum": 0, "maximum": 255 }, // dominant selection
        "opacity_per_intensity": {
          "type": "object",
          "required": ["k", "max"],
          "properties": {
            "k":   { "type": "number", "minimum": 0 },    // linear coeff
            "max": { "type": "number", "minimum": 0, "maximum": 1 }
          }
        }
      }
    },

    "rules": {
      "type": "object",
      "required": ["decay_per_tick"],
      "properties": {
        "decay_per_tick": { "type": "integer", "minimum": 0, "maximum": 255 },
        "min_intensity":  { "type": "integer", "minimum": 0, "maximum": 255, "default": 1 },
        "max_intensity":  { "type": "integer", "minimum": 1, "maximum": 255, "default": 255 },

        "propagate": {
          "type": "object",
          "properties": {
            "enabled": { "type": "boolean", "default": false },
            "quantum": { "type": "integer", "minimum": 1, "maximum": 16, "default": 4 },  // units moved
            "threshold":{ "type": "integer", "minimum": 0, "maximum": 255, "default": 8 }, // only if > this
            "neighbors":{ "type": "string", "enum": ["4","8"], "default": "4" },
            "budget_per_tick":{ "type":"integer", "minimum": 0, "default": 256 }           // cells/tick
          }
        },

        "effects": {
          "type": "object",
          "properties": {
            "contact_damage_id": { "type": "string" }, // id in tuning.damage.json
            "air_damage_id":     { "type": "string" },
            "ignites_flammable": { "type": "boolean", "default": false },
            "extinguished_by":   { "type": "string", "enum": ["none","water","any_fluid"], "default": "water" }
          }
        }
      }
    }
  }
}
3.2 Example Prototypes
json
Copy code
{ "id":"field.smoke", "tags":["air","los"],
  "category":"smoke",
  "render":{"glyph":"fx_smoke","color":"#808080","priority":80,
            "opacity_per_intensity":{"k":0.008,"max":0.6}},
  "rules":{"decay_per_tick":2, "min_intensity":1, "max_intensity":180,
           "propagate":{"enabled":true, "quantum":3, "threshold":12, "neighbors":"4", "budget_per_tick":128}}
}
json
Copy code
{ "id":"field.flame",
  "category":"flame",
  "render":{"glyph":"fx_flame","color":"#FF7A00","priority":200,
            "opacity_per_intensity":{"k":0.0,"max":0.0}},
  "rules":{"decay_per_tick":1,
           "effects":{"contact_damage_id":"burn.small","ignites_flammable":true,"extinguished_by":"water"}}
}
json
Copy code
{ "id":"field.miasma",
  "category":"miasma",
  "render":{"glyph":"fx_miasma","color":"#7D2BD4","priority":120,
            "opacity_per_intensity":{"k":0.004,"max":0.4}},
  "rules":{"decay_per_tick":1,
           "effects":{"air_damage_id":"toxin.light"}}
}
json
Copy code
{ "id":"decal.blood",
  "category":"decal",
  "render":{"glyph":"decal_blood","color":"#8B0000","priority":20,
            "opacity_per_intensity":{"k":0.0,"max":0.0}},
  "rules":{"decay_per_tick":0, "min_intensity":1, "max_intensity":255}
}
4) Snapshot Rules
Dominant field per cell = argmax by render.priority, tiebreaker by intensity then field.id.

Snapshot writes a single FieldGlyph[idx] (dominant) for cost reasons.

Opacity for LOS = min(sum_i k_i * intensity_i, max_i), but clamped to improve stability. We often approximate with dominant only if performance requires (tunable).

5) UPDATE_ORDER: FieldsStep (Write Phase)
Position: after FluidsStep, before Vegetation/Items aging and SnapshotBuild.

Inputs: read-only tile view, fluid depth/kind, material flags, field lists.
Outputs: updated field lists (intensity/age), potential L1/L3 edits via simple interactions.

5.1 Deterministic Loop Shape (hard)
csharp
Copy code
for ck in ChunksSorted:            // stable by key
  if !ChunkIsActiveOrBuffered(ck): continue (freeze on L2+)
  for idx in 0..1023:
    list = Fields[idx] (sorted by field.Id)
    for f in list:
      Step(f) with prototype[rules], fluids, materials
    if propagate.enabled: try_spread(idx) with per-field budget counters
    purge any f with Intensity < min_intensity
RNG, if any, must use named stream rng/fields seeded by (world_seed, ck, idx, field.Id) to be reproducible (prefer no RNG in v1).

5.2 Local Step Step(f)
f.Intensity = max(0, f.Intensity - decay_per_tick)

f.Age++

Extinguish (if configured):

If field.flame and (FluidKind!=0 && extinguished_by != "none") → Intensity=0 (and optionally FluidDepth-- by 1).

Ignite (if configured):

If prototype ignites_flammable=true and tile/material is flammable → spawn/boost field.flame (see §6) and optionally spawn field.smoke. Use cap max_intensity.

Damage application (optional v1):

If contact_damage_id or air_damage_id present, enqueue per-cell effect (not applied here; the Combat/Effects system reads a compact “hits” buffer later in the tick).

5.3 Propagation (optional, simple & quantized)
If propagate.enabled and f.Intensity > threshold:

Compute neighbor order NESW (or 8) fixed; for each neighbor, move up to quantum intensity units if neighbor has strictly lower intensity of same field id.

Decrement per-field global budget; stop when exhausted.

Movement is pure transfer (sum conserved), so decay remains the only sink.

Propagation must not look at wall-clock; only tick budget.

6) Creation / Merge / Removal Semantics
CreateOrAccumulate(ck, idx, fieldId, amount)

If a field with same fieldId exists on cell: Intensity = min(max, Intensity + amount); reset Age=0 if desired (prototype flag in future).

Else add new FieldCell{Id=fieldId, Intensity=clamp(amount), Age=0} keeping the list sorted by Id.

If the prototype is decal and the cell already holds same decal → accumulate; if different decal types should be mutually exclusive (e.g., snow vs ash), modders can express via tags and “cleanup-on-spawn” scripts in a later version (v1: allow coexistence).

Removal

When Intensity < min_intensity → remove from list.

If list becomes empty → remove (idx → list) entry from dictionary.

7) APIs
7.1 Read-Phase (thread-safe)
csharp
Copy code
public interface IFieldQuery
{
    IReadOnlyList<FieldCell>? TryGet(ChunkKey ck, int idx);
    byte DominantOpacity(ChunkKey ck, int idx);  // fast path for LOS
}
7.2 Write-Phase (single-writer per chunk)
csharp
Copy code
public interface IFieldWriteContext
{
    List<FieldCell> GetOrCreate(ChunkKey ck, int idx); // returns pooled list (caller keeps no reference)
    void Remove(ChunkKey ck, int idx, ushort fieldId);
    void CreateOrAccumulate(ChunkKey ck, int idx, ushort fieldId, byte add);
}
All mutators must keep lists sorted by Id and respect budgets. After mutation, callers are responsible for invalidating LOS/opac caches for idx.

8) LOS / Opacity Integration
Per-cell opacity = clamp( Σ_i k_i * intensity_i , max ) from prototype’s opacity_per_intensity.

For speed, an approximation mode may use dominant field only; switch controlled by /content/registries/tuning.fields.json.

On any change to field list or intensity, invalidate OpacMask[idx] (the LOS system recomputes edge-opacity using L0/L2 + field opacity).

9) Interactions (Simplified v1)
Water extinguishes flame: If FluidKind == water and field.flame present → flame intensity to 0 (remove), water depth -1 (floor 0).

Flame ignites flammable: If tile material flammable > 0 and field.flame intensity ≥ small threshold → spawn/boost field.smoke and optionally set SurfaceBits.Ash.

Miasma: If enabled, contributes to “air” damage ticks read by Effects/Combat later; no direct HP work in Fields system.

All tables (flammable, damage constants) come from materials registry and tuning.damage.json (global).

10) Serialization (Chunk-Level)
rust
Copy code
fields.count : u16
repeat fields.count times:
  idx : u16
  n   : u8    // number of FieldCell entries at idx (sorted by Id asc)
  repeat n times:
    id        : u16
    intensity : u8
    age       : u16
Serialize (idx → list) pairs in ascending idx.

Lists are serialized in ascending field.Id.

Use zstd at chunk file level.

Backwards-compatible additions must preserve order and unknown fields must be ignored.

11) Budgets & Performance
fields_budget_cells_per_tick (global) and optional per-field budget_per_tick.

Chunk LOD: L0/L1 tick normally; L2 may freeze fields entirely or apply coarse decay only (configurable). L3/L4 frozen.

Avoid allocations in inner loops; use pooled lists; reuse iterators.

Dominant glyph computation happens in SnapshotBuilder, not in field loop.

12) Determinism
No wall-clock; no non-deterministic collections.

Stable iteration order and quantized units for propagation (if enabled).

Any RNG uses named stream rng/fields seeded on (world_seed, ck, idx, fieldId); prefer no RNG v1.

Same seed + same commands ⇒ identical field lists and snapshot glyphs (CI enforced).

13) Testing (Must-Haves)
Decay: fixed prototype → intensity follows expected sequence.

Extinguish/Ignite: water removes flame; flammable tile + flame yields smoke/ash.

Propagation: bucket tests (single emitter) conserve sum; respects budget; neighbor order stable.

Serialization: round-trip keeps order & values; cross-OS parity.

Snapshot: dominant selection (priority, then intensity, then id) stable.

Perf: large empty map—no hotspots; dense cloud with budget—frame within budget.

14) Tuning Files
/content/registries/tuning.fields.json (example):

json
Copy code
{
  "los_approx_mode": "dominant_only",  // or "sum_clamped"
  "fields_budget_cells_per_tick": 4096,
  "lod_freeze_level": 2                // L2+ freeze
}
15) Extension Points (v2 ideas)
Temperature coupling & heat rise.

Multi-field chemical reactions (tables).

Wind/advection driven spread (world-layer weather).

Per-instance flags (e.g., sticky, corrosive).

Secondary dominant glyph layers (multi-pass visuals).

16) Worked Examples
Spawn smoke when a torch is doused

csharp
Copy code
// Write phase, after water spill set FluidDepth>0
ctxFields.CreateOrAccumulate(ck, idx, IdOf("field.smoke"), 20);
invalidateLOS(idx);
Flame tick

csharp
Copy code
foreach (var f in list) {
  if (f.Id == IdOf("field.flame")) {
    // extinguish by water
    if (q.GetBase(ck, idx).FluidKind == WaterId) { f.Intensity = 0; continue; }
    // decay
    f.Intensity = (byte)Math.Max(0, f.Intensity - 1);
    f.Age++;
    // ignite flammable
    if (IsFlammableTile(ck, idx)) {
       ctxFields.CreateOrAccumulate(ck, idx, IdOf("field.smoke"), 8);
       SetSurfaceAsh(ck, idx);
    }
  }
}
This spec is binding for the Fields system. Any deviation (e.g., spreading with randomness, multi-pass visuals, or per-instance flags) must be introduced behind feature flags and maintain determinism, budgets, and snapshot contracts.
