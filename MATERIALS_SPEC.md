MATERIALS_SPEC.md — v3 (Simplified, CDDA-style, with Arcane & Thermal)
id: material.v3.simplified
status: proposal (normative-on-accept)
owner: content/registry
last_updated: 2025-09-14
compat: auto-migrate from v1/v2

0) Scope

A compact, intuitive material model for crafting/armor/UI:

Resists on a 0–100 scale (bash/cut/stab/acid/fire/cold/electric/arcane).

Environment comfort/protection (insulation/waterproof/breathability/chem_protect).

Structure (durability/rigidity) for wear, buildables, and encumbrance.

Mana: mana_conductivity (0–100) and optional mana_capacity.

Density-driven mass → used by encumbrance & movement.

Thermal kept but minimal: only ignition_point_c and melt_point_c thresholds for world hazards; workshops ignore temperature (they craft directly).

No continuous physics; all damage/coverage/penetration constants live in a separate /content/registries/tuning.damage.json (included below).

1) Field Definitions (Normative)

id: string — lowercase snake_case, unique.

tags: string[] — classification (e.g., metal, alloy, cloth, wood).

density_solid: number — kg/m³, used to derive item mass.

resists: object — numbers 0..100 (values >100 allowed as “extraordinary”):

bash, cut, stab, acid, fire, cold, electric, arcane

env: object

insulation (0..100), waterproof (0..100), breathability (0..100), chem_protect (0..100)

struct: object

durability (0..100), rigidity (0..100)

work: object (pure metadata for content gating; workshops do not require temperatures)

forgeable: bool, weldable: bool, carveable: bool

mana_conductivity: number (0..100), mana_capacity?: number (0..100)

beautifulness: int (0..100), valuebaleness: number (≥0)

thermo: object (simplified thresholds; optional)

ignition_point_c?: number — catches fire at/above this ambient °C

melt_point_c?: number — softens/melts at/above this °C

Determinism: All numbers are constants; no randomization at load. Invalid entries are skipped with structured errors; the game never crashes due to content.

2) Mass & Encumbrance (Where weight comes from)

Mass is derived per item from density_solid × volume (and an item’s thickness_factor), or overridden by the item’s fixed_mass_g when present.

Encumbrance is computed in gameplay using the item’s mass, material rigidity, and breathability (formula and weights come from tuning.damage.json → encumbrance block). Materials do not store encumbrance directly.

3) Damage Binding (Where the math lives)

Damage reduction uses the material’s resists[type] plus any item bonuses on the equipment prototype:

R_type = k_mat * material.resists[type] + k_item * item_bonus[type]
d_final = max(0, d_raw - clamp(0, cap_per_hit, R_type * penetration_modifier))


All constants (k_mat, k_item, caps, coverage and penetration curves, multi-layer falloff) are in /content/registries/tuning.damage.json (see concrete JSON below). Nothing is hardcoded.

4) Thermal (Ignite/Melt) — Very Simple

World hazards (lava, fire fields, furnaces) may set a tile ambient temperature.

If ambient_c ≥ ignition_point_c for a minimum duration, the carrier Ignites (burning DoT governed by damage tuning).

If ambient_c ≥ melt_point_c for a minimum duration, the item/material Melts/Softens (apply item state tags; no fluid sim).

Workshops do not check temperatures; recipes craft directly.

Durations are defined in tuning.damage.json → thermal.

5) JSON Schema (material.v3.schema.json)
{
  "$id": "material.v3.schema.json",
  "type": "object",
  "required": ["id", "tags", "density_solid", "resists", "env", "struct"],
  "properties": {
    "id": { "type": "string" },
    "tags": { "type": "array", "items": { "type": "string" } },
    "density_solid": { "type": "number", "minimum": 1 },
    "resists": {
      "type": "object",
      "required": ["bash","cut","stab","acid","fire","cold","electric","arcane"],
      "properties": {
        "bash": { "type": "number", "minimum": 0, "maximum": 200 },
        "cut": { "type": "number", "minimum": 0, "maximum": 200 },
        "stab": { "type": "number", "minimum": 0, "maximum": 200 },
        "acid": { "type": "number", "minimum": 0, "maximum": 200 },
        "fire": { "type": "number", "minimum": 0, "maximum": 200 },
        "cold": { "type": "number", "minimum": 0, "maximum": 200 },
        "electric": { "type": "number", "minimum": 0, "maximum": 200 },
        "arcane": { "type": "number", "minimum": 0, "maximum": 200 }
      },
      "additionalProperties": false
    },
    "env": {
      "type": "object",
      "required": ["insulation","waterproof","breathability","chem_protect"],
      "properties": {
        "insulation": { "type": "number", "minimum": 0, "maximum": 100 },
        "waterproof": { "type": "number", "minimum": 0, "maximum": 100 },
        "breathability": { "type": "number", "minimum": 0, "maximum": 100 },
        "chem_protect": { "type": "number", "minimum": 0, "maximum": 100 }
      },
      "additionalProperties": false
    },
    "struct": {
      "type": "object",
      "required": ["durability","rigidity"],
      "properties": {
        "durability": { "type": "number", "minimum": 0, "maximum": 100 },
        "rigidity": { "type": "number", "minimum": 0, "maximum": 100 }
      },
      "additionalProperties": false
    },
    "work": {
      "type": "object",
      "properties": {
        "forgeable": { "type": "boolean" },
        "weldable": { "type": "boolean" },
        "carveable": { "type": "boolean" }
      },
      "additionalProperties": false
    },
    "mana_conductivity": { "type": "number", "minimum": 0, "maximum": 100 },
    "mana_capacity": { "type": "number", "minimum": 0, "maximum": 100 },
    "beautifulness": { "type": "integer", "minimum": 0, "maximum": 100 },
    "valuebaleness": { "type": "number", "minimum": 0 },
    "thermo": {
      "type": "object",
      "properties": {
        "ignition_point_c": { "type": "number", "minimum": 0 },
        "melt_point_c": { "type": "number", "minimum": 0 }
      },
      "additionalProperties": false
    }
  },
  "additionalProperties": false
}

6) Example Materials (JSON, no comments)

Steel

{
  "id": "core_mat_metal_steel",
  "tags": ["metal","alloy"],
  "density_solid": 7800,
  "resists": { "bash": 65, "cut": 80, "stab": 75, "acid": 30, "fire": 70, "cold": 80, "electric": 30, "arcane": 20 },
  "env": { "insulation": 10, "waterproof": 50, "breathability": 0, "chem_protect": 25 },
  "struct": { "durability": 85, "rigidity": 85 },
  "work": { "forgeable": true, "weldable": true, "carveable": false },
  "mana_conductivity": 35,
  "mana_capacity": 10,
  "beautifulness": 55,
  "valuebaleness": 1.1,
  "thermo": { "ignition_point_c": null, "melt_point_c": 1450 }
}


Cotton

{
  "id": "core_mat_fiber_cotton",
  "tags": ["fiber","organic","cloth"],
  "density_solid": 1550,
  "resists": { "bash": 15, "cut": 10, "stab": 8, "acid": 10, "fire": 5, "cold": 25, "electric": 5, "arcane": 5 },
  "env": { "insulation": 35, "waterproof": 5, "breathability": 85, "chem_protect": 5 },
  "struct": { "durability": 25, "rigidity": 5 },
  "work": { "forgeable": false, "weldable": false, "carveable": true },
  "mana_conductivity": 5,
  "beautifulness": 35,
  "valuebaleness": 0.8,
  "thermo": { "ignition_point_c": 410, "melt_point_c": null }
}

7) Global Damage/Armor Tuning (JSON, no comments)

Place at: /content/registries/tuning.damage.json
(You can tweak values per pack/DLC/mod; engine reads these at boot.)

{
  "version": 1,
  "rng_jitter": 0,
  "k": { "mat": 0.1, "item": 1.0 },
  "caps": { "per_hit": 90, "min_damage": 0 },
  "coverage": { "curve": "step", "threshold_pct": 80 },
  "penetration": { "curve": "linear", "a": 1.0, "b": 0.0 },
  "layering": { "falloff": 0.6 },
  "arcane": { "k_mat": 0.1, "k_item": 1.0 },
  "encumbrance": { "w_mass": 0.6, "w_rigidity": 0.25, "w_breath": 0.15, "mass_ref_g": 4000, "thickness_mult": 0.5 },
  "thermal": { "ignite_min_seconds": 2, "melt_min_seconds": 5 }
}


Interpreting the tuning file (engine rules):

k.mat/k.item feed the resist→reduction formula; caps.per_hit clamps per-hit reduction.

coverage.curve="step" with threshold_pct uses a simple single-roll coverage gate (e.g., 80%).

penetration.curve="linear" can be swapped for "exp"/"poly" if you add parameters later.

layering.falloff scales the 2nd/3rd… armor layer effectiveness.

encumbrance block is used by the gameplay system when turning mass/rigidity/breathability into an item’s encumbrance.

thermal sets minimum exposure time above thresholds before Ignite or Melt states are applied.