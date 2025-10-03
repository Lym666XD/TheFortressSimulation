# Materials Data Contract

## Core Principles

1. **TerrainKind owns legality** - All navigation legality decisions (walkable/standable/climbable/flyable) are owned by TerrainKind, never materials
2. **Materials provide numeric modifiers only** - Materials can only modify costs, friction, hazards, never flip illegal→legal
3. **Geology combines TerrainKind + Material** - Geology prototypes combine a terrain kind with a material and optional tuning

## Precedence Chain

```
TerrainKind (legality) → Geology Prototype (tuning) → Material (modifiers) → Fields/Fluids → Actor Capabilities
```

## Natural vs Constructed Durability

### Base Properties in Materials
- Materials define baseline `physical.hardness` and `mining.diggingTime`
- These represent the inherent properties of the material itself

### Durability Modifiers
- **Natural terrain** (terrain_bits.natural = true): Use material baseline values
- **Constructed terrain** (terrain_bits.natural = false): Apply multipliers for enhanced durability
  - Example: Constructed granite wall has 1.5x durability vs natural granite wall
  - Multipliers defined at geology prototype level or L2 construction metadata

### Implementation Approach
```json
// In geology prototype
"properties": {
  "durability_multiplier": 1.5,  // For constructed variants
  "mining_time_multiplier": 2.0  // Harder to mine when constructed
}
```

## Placeholders (Not Implemented)

### Climbable Flag
- **Status**: Reserved but not implemented
- **Location**: terrain_kinds.json navigation.climbable field
- **Note**: Ramps and stairs are walk-based Z transitions, not "climb" mechanics
- **Future**: May be used for ladders, ropes, wall climbing

### Fields/Fluids System
- **Status**: Deferred for future implementation
- **Concept**: Separate overlay system for environmental effects
- **Scope**:
  - Fluids: Water depth thresholds, swimming, drowning
  - Fields: Smoke, miasma, fire - affect visibility and hazards
- **Note**: These are NOT material properties but separate L3/L4 tile layers

## File Structure

### Registry Files
- `materials.registry.json` - Main materials database with numeric modifiers only
- `materials.authoring.json` - Optional author-friendly input format
- `geology.json` - Geology prototypes combining terrain + material
- `terrain_kinds.json` - Terrain shapes with navigation legality

### Schema Files
- `materials.registry.schema.json` - Enforces no legality fields in materials
- `material.authoring.schema.json` - Schema for authoring format
- `terrain_kinds.schema.json` - Defines navigation legality structure
- `geology.schema.json` - Geology prototype validation

## Material Navigation Properties

Materials may only define:
- `moveCostModifier` (-50 to +50) - Additive cost modifier
- `frictionModifier` (-1 to +1) - Surface friction effect
- `hazardLevel` (0-10) - Environmental damage
- `hazardType` - Type of hazard (heat, cold, poison, etc)

**Forbidden in materials:**
- walkable, standable, climbable, flyable
- blocksMovement, blocksSight
- Any boolean legality flags

## Validation Checklist

- [ ] All materials validate against materials.registry.schema.json
- [ ] No navigation legality fields in any material
- [ ] All geology prototypes reference valid materials
- [ ] TerrainKind defines all legality decisions
- [ ] Materials provide only numeric modifiers
- [ ] Natural vs constructed use same base material


MATERIALS_DATA_CONTRACT.md (Fixed-Point Edition)

Status: final
Owner: content/registry
Last updated: 2025-10-03 (Australia/Sydney)
Scope: Legality & navigation responsibilities, geology–material composition, and allowed numeric knobs.
Works with: MATERIALS_SPEC v4-min (Fixed-Point Edition) and Items v3 (“shape baseline × material multipliers × quality/skill/state”).

0) Rationale & Non-Goals

Rationale. Keep legality in one place (TerrainKind), keep materials simple and numeric (no booleans that toggle playfield rules), and let Geology combine terrain shape with a specific material plus tuning multipliers.

Non-Goals. No thermal simulation or burn products in materials; no environment/coverage logic here; no JSON cycles.

1) Core Principles

TerrainKind owns legality. All navigation legality (walkable / standable / climbable / flyable) is defined by TerrainKind—never by materials.

Materials provide numeric modifiers only. Materials may tweak costs, friction, hazards, processing difficulty, etc., but must not flip illegal → legal (or vice versa).

Geology = TerrainKind + Material (+ tuning). A geology prototype composes a terrain kind with a material and optional multipliers/overrides.

2) Precedence & Stacking Order
TerrainKind (legality, base params)
  → Geology Prototype (tuning/overrides)
    → Material (numeric modifiers only)
      → Fields/Fluids overlays (future)
        → Actor Capabilities (skills, gear, traits)


Stacking rules (high level):

Additive integer inputs (e.g., move cost deltas) apply after TerrainKind baselines and Geology overrides.

Multipliers are FX-integers (see §3) and apply in the order:
value = value × geology_mul_fx / FX × material_mul_fx / FX.

3) Performance Policy — Fixed-Point Math (No floats at runtime)

Global scale: FX = 10_000 (1.0000 → 10000).

All dimensionless values & multipliers are stored and computed as FX integers.

Only densities/masses use physical units (e.g., kg/m³, mg/mL).

Authoring may use human-readable numbers; the compiler must scale and cache FX integers.

Suggested helpers (pseudocode): fx_mul(a,b), fx_div(a,b), fx_from_float(x), fx_from_pct(p), fx_dev(g_fx = g - FX/2).
For roots/powers use LUTs + linear interpolation; avoid runtime floats.

4) Responsibilities by Layer
4.1 TerrainKind (owns legality)

Defines: walkable, standable, optionally reserved climbable, flyable, blocking flags, base move cost, base friction, base durability/mining baselines for the shape (e.g., floor/rock wall).

Must not look into material booleans to decide legality.

4.2 Geology Prototypes (compose + tune)

Compose a terrain_kind_id with a material_id.

Provide tuning/overrides (FX integers unless noted):

durability_mul_fx (default 10000).

mining_time_mul_fx (default 10000).

Optional navigation overrides:

moveCost_add (integer additive, system units).

friction_mul_fx (FX multiplier).

hazard_override (rare; see §6).

Use-case: Distinguish natural vs constructed variants by multipliers (e.g., constructed walls are tougher/harder to mine).

4.3 Materials (numeric modifiers only)

Allowed navigation properties in materials:

moveCost_add — integer additive (e.g., −2..+8).

friction_mul_fx — FX multiplier (e.g., 10000 = 1.0).

hazardLevel — small integer (0..10).

hazardType — enum ("heat", "cold", "acid", "shock", "miasma", etc.).

Other allowed numeric knobs (non-navigation):

work.process_difficulty_mul_fx (FX) — affects mining/cutting/crafting time & skill DC (see §7).

Forbidden in materials: any legality booleans (walkable, standable, climbable, flyable, blocksMovement, blocksSight, …).

All other combat/crafting/electric/magic fields are specified by MATERIALS_SPEC v4-min; this contract only constrains their role w.r.t. navigation & geology.

4.4 Fields / Fluids (future)

A separate overlay system for water/smoke/fire/etc. Not part of materials. See §8 placeholders.

5) Natural vs Constructed Durability

Design intent: Natural formations use the material’s baseline processing difficulty; constructed variants are deliberately tougher/harder to mine.

Formulas (FX, no floats):

// Final durability points on a tile:
durability_final =
  base_durability_points
  × geology.durability_mul_fx / FX;

// Final mining time (ms):
mining_time_final_ms =
  base_mining_time_ms
  × geology.mining_time_mul_fx / FX
  × material.work.process_difficulty_mul_fx / FX;


Notes

“Base” comes from TerrainKind (shape‐driven) or a geology baseline table.

The material contributes difficulty via process_difficulty_mul_fx (not a hard legality gate).

Constructed variants typically set durability_mul_fx > 10000 and mining_time_mul_fx > 10000.

Example (Geology tuning)

{
  "id": "geo_wall_granite_constructed",
  "terrain_kind": "rock_wall",
  "material": "granite",
  "tuning": {
    "durability_mul_fx": 15000,      // 1.5× tougher than natural
    "mining_time_mul_fx": 20000      // 2.0× harder to mine
  }
}

6) Material Navigation Properties (Allowed vs Forbidden)

Allowed in materials:

moveCost_add (integer additive; e.g., −50..+50 if your pathfinder uses that range).

friction_mul_fx (FX multiplier; recommended range ~8000..12000).

hazardLevel (0..10, integer).

hazardType (enum; optional if hazardLevel = 0).

Optional resolution (how they stack):

move_cost_final =
  base_move_cost
  + geology.moveCost_add
  + material.moveCost_add;

friction_final_fx =
  base_friction_fx
  × geology.friction_mul_fx / FX
  × material.friction_mul_fx / FX;

// Hazards may sum or pick max, then get scaled by system curves:
hazard_level_raw =
  max( base_hazard_level, geology.hazardLevel_override ?? 0, material.hazardLevel );
hazard_type = geology.hazardType_override ?? material.hazardType ?? base_hazard_type;


Forbidden in materials (hard rule):

Any boolean legality or LOS flags:
walkable, standable, climbable, flyable, blocksMovement, blocksSight, blocksProjectiles, …

7) Processing Difficulty → Mining / Crafting

Materials may include a processing difficulty multiplier:

Field: work.process_difficulty_mul_fx (FX; default 10000 = 1.0).

Mining/Chopping time:
time = base_time × process_difficulty_mul_fx / FX.

Crafting time (multi-material):
craft_time = base_time × GM(process_difficulty_mul_fx[]) / FX (geometric mean).

Skill gates/checks (example):
DC += ((process_difficulty_mul_fx - FX) × skill_req_scale_fx) / FX (tuned constant).

Flags like forgeable/weldable/carveable are capability gates for recipes/tools—not legality and not navigation.

8) Placeholders / Deferred Systems
8.1 Climbable Flag

Status: reserved, not implemented.

Location: terrain_kinds.json → navigation.climbable.

Note: ramps/stairs are walk-based Z transitions, not “climb”.

Future: ladders, ropes, wall-climb.

8.2 Fields / Fluids

Status: deferred.

Concept: overlay system for environmental effects.

Scope examples:

Fluids: water depth, swimming, drowning, currents.

Fields: smoke, miasma, fire—affect visibility & hazard.

Important: these are not material properties and not part of this contract.

9) Thermal & Burning Policy (for World/Fire layer, not Materials)

If a world/fire module is introduced later, thermal parameters belong there, as FX integers (e.g., specific_heat_*_fx, latent_heat_fx, heat_of_combustion_fx, flammability_class).

No burn products: when burned, items are destroyed (to avoid JSON graph cycles).

Materials do not define burn outputs in this contract.

10) File & Schema Layout
10.1 Registry Files

materials.registry.json — main materials database; numeric modifiers only (no legality).

materials.authoring.json — optional author-friendly input; compiler scales to FX.

geology.json — geology prototypes (TerrainKind + Material + tuning).

terrain_kinds.json — terrain shapes with legality.

10.2 Schema Files

materials.registry.schema.json — forbids legality fields in materials; allows numeric modifiers and process difficulty.

material.authoring.schema.json — authoring shape; compiler transforms to registry form.

terrain_kinds.schema.json — legality and base parameters for shapes.

geology.schema.json — composition + tuning (FX multipliers).

11) Validation Checklist

 All materials validate against materials.registry.schema.json.

 No navigation legality booleans in any material.

 All geology prototypes reference valid terrain_kind_id and material_id.

 TerrainKind defines all legality decisions.

 Materials provide only numeric modifiers (e.g., moveCost_add, friction_mul_fx, hazardLevel/Type, process_difficulty_mul_fx).

 Natural vs constructed variants use the same base material; differences come from Geology multipliers.

 All multipliers and dimensionless values use FX integers; no floats at runtime.

 Optional: compiler derives UI-only mining hardness tiers from material mechanics (not authored as physical.hardness).

12) Examples
12.1 Minimal Material (navigation knobs + processing)
{
  "id": "granite",
  "tags": ["stone"],
  "density_solid": 2750,

  "// mechanics per MATERIALS_SPEC v4-min (authoring; compiler scales)": {},
  "work": {
    "forgeable": false,
    "weldable": false,
    "carveable": true,
    "process_difficulty_mul_fx": 14000
  },

  // Navigation numeric modifiers (allowed):
  "moveCost_add": 1,
  "friction_mul_fx": 11000,
  "hazardLevel": 0
}

12.2 TerrainKind (legality & base params)
{
  "id": "rock_wall",
  "navigation": {
    "walkable": false,
    "standable": false,
    "climbable": false,  // reserved; currently ignored
    "flyable": true
  },
  "base": {
    "move_cost": 0,
    "friction_fx": 10000,
    "durability_points": 1200,
    "mining_time_ms": 6000
  }
}

12.3 Geology (natural vs constructed)
{
  "id": "geo_wall_granite_natural",
  "terrain_kind": "rock_wall",
  "material": "granite",
  "tuning": {
    "durability_mul_fx": 10000,
    "mining_time_mul_fx": 10000
  }
}

{
  "id": "geo_wall_granite_constructed",
  "terrain_kind": "rock_wall",
  "material": "granite",
  "tuning": {
    "durability_mul_fx": 15000,   // tougher
    "mining_time_mul_fx": 20000,  // harder to mine
    "moveCost_add": 0,            // optional overrides
    "friction_mul_fx": 10000
  }
}


Runtime examples

durability_final(constructed) = 1200 × 15000 / 10000 = 1800

mining_time_final_ms(natural) =
  6000 × 10000/10000 × 14000/10000  = 8400 ms

mining_time_final_ms(constructed) =
  6000 × 20000/10000 × 14000/10000  = 16800 ms

13) Common Pitfalls & How to Avoid Them

Putting legality booleans in materials. Don’t—schemas forbid it.

Mixing floats and FX. Don’t—convert at compile time; runtime uses FX only.

Double-applying friction. Choose one additive or multiplier representation at each layer; this contract uses multipliers (FX) for friction.

Authoring “physical.hardness” directly. Don’t—let the compiler derive any UI hardness tier from the material mechanics (see MATERIALS_SPEC v4-min).

Encoding environment/coverage here. Keep environment/coverage/lining on the item side (armor shapes), not materials.

Burn products. Not allowed—burned items are destroyed to avoid dependency cycles.

14) Conformance Quick-Check

Materials: numeric modifiers only; no legality booleans.

TerrainKind: full legality; base shape params.

Geology: composition + FX multipliers; handles natural vs constructed differences.

Runtime: integer-only FX math; LUTs for roots/powers.

Optional future systems (fields/fluids/thermal) live outside materials.