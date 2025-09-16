ITEMS_SPEC.md — v3 (Final, Deterministic, Data-Driven)
id: item.v3
status: normative
owner: content/registry
last_updated: 2025-09-14
compat: auto-migrate from v1/v2
depends_on:
  - material.v3
  - /content/registries/tuning.damage.json

0) Scope & Goals

A compact, LLM-friendly item model for resources, armor, weapons, tools, containers, and consumables.

Deterministic & moddable: no hardcoded constants; all tuning lives in data.

Thread-safe & crash-resistant: strict validation; bad entries are skipped without crashing loads or the running game.

1) Core Principles (Normative)

IDs over enums: items and materials reference content by string IDs; runtime maps them to stable numeric handles.

Mass from materials: item mass is derived from density_solid × base_volume_ml unless a fixed mass is provided.

Encumbrance from tuning: gameplay computes encumbrance from mass/rigidity/breathability using tuning.damage.json.

Stacks: one of none | count | charges. charges use units g or ml only.

Armor = material resists × thickness + item bonus, with coverage/layering/penetration rules from tuning.

No behavior in items: job/workflow state is persisted by the Job Scheduler, not inside items.

2) Top-Level Fields (Normative)

id:string — lowercase snake_case, unique.

name?:string, desc?:string — display only.

kind: "resource" | "armor" | "weapon" | "tool" | "container" | "consumable" | "placeable".

tags:string[] — for search, filters, and recipe selectors.

Material (choose one):

fixed_material:string

material_weights:[{ id:string, weight:number }] (linear blend; weights normalized)

Geometry/Mass:

base_volume_ml:number (required for mass derivation unless fixed_mass_g present)

base_mass_g?:number (lower-bound clamp for derived mass)

fixed_mass_g?:number (absolute override for mass)

thickness_factor?:number (default 1.0; affects armor mass & mitigation scaling)

Stacking: stack block (see §4)

Bonuses/Display:

item_bonus?:{ bash?, cut?, stab?, acid?, fire?, cold?, electric?, arcane? }

value_base?:number, beauty_bonus?:number

durability_max?:int (omit to auto-derive from material durability and thickness)

Kind-specific blocks: equip (armor), weapon (weapon), container (container), use (consumable)

3) Mass & Encumbrance (Normative)

Mass derivation (runtime):

If fixed_mass_g present → use it.

Else mass_g = max( density_solid * base_volume_ml * 1e-6 * 1e3 * thickness_factor, base_mass_g ).

Encumbrance (runtime):

Computed from total item mass, material struct.rigidity, and material env.breathability using weights in tuning.damage.json → encumbrance.

Items may add equip.encumbrance_bonus (straps/fit).

Movement penalty uses tuning.damage.json → movement (inv-polynomial over total encumbrance).

4) Stacks (Normative)

mode: "none" | "count" | "charges"

max_per_stack:int ≥ 1

unit: "g" | "ml" (only for charges; no uses)

equal_when:string[] (for count stacks: identity keys that must match to merge, e.g. ["fixed_material"])

charges use reservation tokens in jobs; consumption commits at tick barriers (see Diff/Merge spec).

5) Armor (Normative)
"equip": {
  "type": "armor",
  "body_slots": ["torso","arm_l","arm_r","leg_l","leg_r","head","hand_l","hand_r","foot_l","foot_r"],
  "layer": "inner|mid|outer|shield",
  "encumbrance_bonus": 0,
  "coverage": 85 | { "torso":90, "arm_l":70, "arm_r":70, "leg_l":40, "leg_r":40 }
}


coverage may be a single percentage or a per-slot map. Unlisted slots are treated as 0%.

Mitigation per type:

R_type = k_mat * blended_material.resists[type] * thickness_factor + k_item * item_bonus[type]


Then apply penetration/coverage/caps/layer falloff from tuning.damage.json.

6) Weapon (Normative)
"weapon": {
  "damage": { "bash":0, "cut":0, "stab":0, "acid":0, "fire":0, "cold":0, "electric":0, "arcane":0 },
  "armor_penetration": { "bash":0, "cut":0, "stab":0, "arcane":0 },
  "attack_time_ms": 800,
  "reach_tiles": 1,
  "two_handed": false
}


Missing fields default to 0.

Penetration curves and interaction with armor live in tuning.

7) Container (Normative)
"container": {
  "capacity_ml": 1000,
  "accept_tags": ["liquid","powder"],
  "leakproof": true
}

8) Consumable (Minimal)
"use": {
  "effects": ["heal_small","warmth_small"],
  "charges_per_use": 250
}


(Effect IDs are content-driven.)

9) Integration Contracts (Normative)

Jobs/Work persistence: Items do not store job behavior. Persist job state in Job Scheduler save (recipe/workshop IDs, progress ticks, reservations, item GUID bindings). On load, rebind by GUID; missing inputs push job into deterministic retry/cancel with token rollback.

Recipes & selectors: Items must carry tags[]. Recipes should use a selector DSL for inputs (allow/deny by item IDs/tags and material IDs/tags). See RECIPES_SPEC.md.

10) Validation & Error Handling (Normative)

JSON validated against item.v3.schema.json (below). Per-file try/catch; invalid entries are skipped with structured logs (file, id, reason). The game must not crash.

On finalize, assign stable numeric handles by source tier and lexicographic ID.

11) JSON Schema — item.v3.schema.json (no comments)
{
  "$id": "item.v3.schema.json",
  "type": "object",
  "required": ["id", "kind", "tags", "stack"],
  "properties": {
    "id": { "type": "string" },
    "name": { "type": "string" },
    "desc": { "type": "string" },
    "kind": { "type": "string", "enum": ["resource","armor","weapon","tool","container","consumable","placeable"] },
    "tags": { "type": "array", "items": { "type": "string" } },
    "base_volume_ml": { "type": "number", "minimum": 0 },
    "base_mass_g": { "type": "number", "minimum": 0 },
    "fixed_mass_g": { "type": "number", "minimum": 0 },
    "fixed_material": { "type": "string" },
    "material_weights": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["id","weight"],
        "properties": {
          "id": { "type": "string" },
          "weight": { "type": "number", "minimum": 0 }
        },
        "additionalProperties": false
      }
    },
    "thickness_factor": { "type": "number", "minimum": 0 },
    "item_bonus": {
      "type": "object",
      "properties": {
        "bash": { "type": "number" },
        "cut": { "type": "number" },
        "stab": { "type": "number" },
        "acid": { "type": "number" },
        "fire": { "type": "number" },
        "cold": { "type": "number" },
        "electric": { "type": "number" },
        "arcane": { "type": "number" }
      },
      "additionalProperties": false
    },
    "stack": {
      "type": "object",
      "required": ["mode"],
      "properties": {
        "mode": { "type": "string", "enum": ["none","count","charges"] },
        "max_per_stack": { "type": "integer", "minimum": 1 },
        "unit": { "type": "string", "enum": ["g","ml"] },
        "equal_when": { "type": "array", "items": { "type": "string" } }
      },
      "additionalProperties": false
    },
    "value_base": { "type": "number", "minimum": 0 },
    "beauty_bonus": { "type": "number" },
    "durability_max": { "type": "integer", "minimum": 0 },
    "equip": {
      "type": "object",
      "properties": {
        "type": { "type": "string", "enum": ["armor"] },
        "body_slots": { "type": "array", "items": { "type": "string" } },
        "layer": { "type": "string", "enum": ["inner","mid","outer","shield"] },
        "encumbrance_bonus": { "type": "number" },
        "coverage": {
          "oneOf": [
            { "type": "number", "minimum": 0, "maximum": 100 },
            { "type": "object", "additionalProperties": { "type": "number", "minimum": 0, "maximum": 100 } }
          ]
        }
      },
      "additionalProperties": false }
    ,
    "weapon": {
      "type": "object",
      "properties": {
        "damage": {
          "type": "object",
          "properties": {
            "bash": { "type": "number" },
            "cut": { "type": "number" },
            "stab": { "type": "number" },
            "acid": { "type": "number" },
            "fire": { "type": "number" },
            "cold": { "type": "number" },
            "electric": { "type": "number" },
            "arcane": { "type": "number" }
          },
          "additionalProperties": false
        },
        "armor_penetration": {
          "type": "object",
          "properties": {
            "bash": { "type": "number" },
            "cut": { "type": "number" },
            "stab": { "type": "number" },
            "arcane": { "type": "number" }
          },
          "additionalProperties": false
        },
        "attack_time_ms": { "type": "integer", "minimum": 1 },
        "reach_tiles": { "type": "integer", "minimum": 0 },
        "two_handed": { "type": "boolean" }
      },
      "additionalProperties": false
    },
    "container": {
      "type": "object",
      "properties": {
        "capacity_ml": { "type": "number", "minimum": 0 },
        "accept_tags": { "type": "array", "items": { "type": "string" } },
        "leakproof": { "type": "boolean" }
      },
      "additionalProperties": false
    },
    "use": {
      "type": "object",
      "properties": {
        "effects": { "type": "array", "items": { "type": "string" } },
        "charges_per_use": { "type": "number", "minimum": 0 }
      },
      "additionalProperties": false
    }
  },
  "allOf": [
    { "oneOf": [ { "required": ["fixed_material"] }, { "required": ["material_weights"] } ] },
    { "if": { "properties": { "kind": { "const": "armor" } } }, "then": { "required": ["equip","base_volume_ml"] } },
    { "if": { "properties": { "kind": { "const": "weapon" } } }, "then": { "required": ["weapon","base_volume_ml"] } },
    { "if": { "properties": { "kind": { "const": "container" } } }, "then": { "required": ["container"] } }
  ],
  "additionalProperties": false
}

12) Minimal Examples (no comments)
Resource (count stack) — Iron Ingot
{
  "id": "core_item_ingot_iron",
  "name": "Iron Ingot",
  "kind": "resource",
  "tags": ["metal","ingot"],
  "fixed_material": "core_mat_metal_iron",
  "base_volume_ml": 120,
  "base_mass_g": 900,
  "stack": { "mode": "count", "max_per_stack": 50, "equal_when": ["fixed_material"] },
  "value_base": 10
}

Resource (charges stack) — Charcoal Powder
{
  "id": "core_item_powder_charcoal",
  "name": "Charcoal Powder",
  "kind": "resource",
  "tags": ["fuel","powder"],
  "fixed_material": "core_mat_carbon_charcoal",
  "base_volume_ml": 1500,
  "stack": { "mode": "charges", "max_per_stack": 100000, "unit": "g" },
  "value_base": 1
}

Armor — Long Wool Coat (multi-slot coverage map)
{
  "id": "core_item_armor_long_coat_wool",
  "name": "Long Wool Coat",
  "kind": "armor",
  "tags": ["armor","cloth","coat"],
  "fixed_material": "core_mat_fiber_wool",
  "base_volume_ml": 5200,
  "stack": { "mode": "none" },
  "equip": {
    "type": "armor",
    "body_slots": ["torso","arm_l","arm_r","leg_l","leg_r"],
    "layer": "outer",
    "encumbrance_bonus": 3,
    "coverage": { "torso": 90, "arm_l": 70, "arm_r": 70, "leg_l": 40, "leg_r": 40 }
  }
}

Weapon — Iron Longsword
{
  "id": "core_item_weapon_longsword_iron",
  "name": "Iron Longsword",
  "kind": "weapon",
  "tags": ["weapon","sword"],
  "fixed_material": "core_mat_metal_iron",
  "base_volume_ml": 950,
  "stack": { "mode": "none" },
  "weapon": {
    "damage": { "cut": 18, "stab": 6, "bash": 2 },
    "armor_penetration": { "cut": 4, "stab": 6 },
    "attack_time_ms": 900,
    "reach_tiles": 1,
    "two_handed": false
  }
}