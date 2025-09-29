ITEMS_SPEC.md — v3 (Final, Deterministic, Data-Driven)
id: item.v3
status: normative
owner: content/registry
last_updated: 2025-09-29
compat: auto-migrate from v1/v2
depends_on:
  - material.v3
  - /content/registries/tuning.damage.json

0) Scope & Goals

A compact, deterministic, data-driven item model for resources, armor, weapons, tools, containers, consumables, and placeables. Clean separation between definition data (authoring) and runtime instance (save).

Deterministic & moddable: no hardcoded constants; all tuning lives in data/ and registries/.

Crash-resistant: strict validation; bad entries are skipped with structured logs.

1) Core Principles (Normative)

- IDs over enums: items and materials reference content by string IDs; runtime maps them to stable numeric handles.
- Mass from materials: item mass is derived from density_solid × base_volume_ml unless a fixed mass is provided.
- Encumbrance from tuning: gameplay computes encumbrance from mass/rigidity/breathability using tuning.damage.json weights.
- Stacks: one of none | count | charges. charges use units g or ml only.
- Armor = material resists × thickness + item bonus; coverage/layering/penetration rules come from tuning.
- No behavior in items: job/workflow state lives in Job Scheduler, not in items.

1.1) Naming Rules (Normative)

- Use snake_case IDs with a stable namespace prefix, e.g. `core_weapon_dagger`, `core_armor_breastplate`, `core_container_waterskin`, `core_item_ingot_iron`.
- Do not embed specific materials in IDs or display names for multi-material craftables (weapons, furniture, tools). Use `allowed_material_tags` at author-time and carry the chosen `material_id` on instances.
- Do not encode size in IDs or display names (e.g., no "Small", "Medium", "Large"). Represent size via fields like `capacity_ml`, `base_volume_ml`, `thickness_factor`, `equip.coverage`, or `placeable.footprint`. UI may compose size presentation dynamically.
- Exception: for raw resources whose identity is the substance itself (e.g., `core_item_ingot_iron`, `core_item_ore_hematite`), including the material in the ID is acceptable.

2) Top-Level Fields (Normative)

- id:string — lowercase snake_case, unique (see naming rules above).
- name?:string, desc?:string — display only.
- kind: "resource" | "armor" | "weapon" | "tool" | "container" | "consumable" | "placeable".
- tags:string[] — for search, filters, recipe selectors.

Material (choose one authoring path):
- fixed_material:string — pinned to a specific material id.
- material_weights:[{ id:string, weight:number }] — linear blend (alloy/mixture); weights normalized.
- allowed_material_tags:string[] — multi-material craftable; material is chosen per instance (instance carries `material_id`).

Geometry/Mass:
- base_volume_ml:number (required for mass derivation unless fixed_mass_g present)
- base_mass_g?:number (lower-bound clamp for derived mass)
- fixed_mass_g?:number (absolute override for mass)
- thickness_factor?:number (default 1.0; affects armor mass & mitigation scaling)

Stacking: stack block (see §4)

Bonuses/Display:
- item_bonus?:{ bash?, cut?, stab?, acid?, fire?, cold?, electric?, arcane? }
- value_base?:number, beauty_bonus?:number
- durability_max?:int (omit to auto-derive from material durability and thickness)

Kind-specific blocks: equip (armor), weapon (weapon), container (container), use (consumable), placeable (placeable)

3) Mass & Encumbrance (Normative)

Mass derivation (runtime):
- If fixed_mass_g present — use it.
- Else mass_g = max( density_solid × base_volume_ml × 1e-6 × 1e3 × thickness_factor, base_mass_g ).

Encumbrance (runtime):
- Computed from total item mass, material struct.rigidity, and material env.breathability using weights in tuning.damage.json.
- Items may add equip.encumbrance_bonus (straps/fit). Movement penalty uses tuning curves.

4) Stacks (Normative)

- mode: "none" | "count" | "charges"
- max_per_stack:int ≥ 1
- unit: "g" | "ml" (only for charges)
- equal_when:string[] (for count stacks: identity keys that must match to merge, e.g., ["material_id"]).
- charges use reservation tokens in jobs; consumption commits at tick barriers.

5) Armor (Normative)

equip block:
{
  "type": "armor",
  "body_slots": ["torso","arm_l","arm_r","leg_l","leg_r","head","hand_l","hand_r","foot_l","foot_r"],
  "layer": "inner|mid|outer|shield",
  "encumbrance_bonus": 0,
  "coverage": 85 | { "torso":90, "arm_l":70, "arm_r":70, "leg_l":40, "leg_r":40 }
}

Mitigation per type: R_type = k_mat × blended_material.resists[type] × thickness_factor + k_item × item_bonus[type].
Then apply penetration/coverage/caps/layer falloff from tuning.

6) Weapon (Normative)

weapon block:
{
  "damage": { "bash":0, "cut":0, "stab":0, "acid":0, "fire":0, "cold":0, "electric":0, "arcane":0 },
  "armor_penetration": { "bash":0, "cut":0, "stab":0, "arcane":0 },
  "attack_time_ms": 800,
  "reach_tiles": 1,
  "two_handed": false
}

7) Container (Normative)

container block:
{
  "capacity_ml": 1000,
  "accept_tags": ["liquid","powder"],
  "leakproof": true
}

Do not place size words in the display `name` (e.g., prefer "Waterskin" vs "Waterskin (Small)"). Size is represented by `capacity_ml`; UI may render human-friendly size.

8) Consumable (Minimal)

use block:
{
  "effects": ["heal_small","warmth_small"],
  "charges_per_use": 250
}

9) Integration Contracts (Normative)

- Jobs/Work persistence: Items do not store job behavior. Persist job state in Job Scheduler save (recipe/workshop IDs, progress ticks, reservations, item GUID bindings). On load, rebind by GUID; missing inputs push job into deterministic retry/cancel with token rollback.
- Recipes & selectors: Items must carry tags[]. Recipes should use a selector DSL for inputs (allow/deny by item IDs/tags and material IDs/tags).

10) Validation & Error Handling (Normative)

- JSON validated against item.v3.schema.json (below). Per-file try/catch; invalid entries are skipped with structured logs (file, id, reason). The game must not crash.
- On finalize, assign stable numeric handles by source tier and lexicographic ID.

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
    "allowed_material_tags": { "type": "array", "items": { "type": "string" } },
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
      "additionalProperties": false
    },

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
    },

    "placeable": {
      "type": "object",
      "properties": {
        "footprint": { "type": "object" },
        "orientation_mask": { "type": "array", "items": { "type": "string" } },
        "passability": { "type": "string" },
        "beauty_base": { "type": "number" },
        "comfort": { "type": "number" },
        "light_lumen": { "type": "number" },
        "heat_w": { "type": "number" }
      },
      "additionalProperties": true
    }
  },

  "allOf": [
    { "oneOf": [ { "required": ["fixed_material"] }, { "required": ["material_weights"] }, { "required": ["allowed_material_tags"] } ] },
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
  "stack": { "mode": "count", "max_per_stack": 50, "equal_when": ["material_id"] },
  "value_base": 10
}

Resource (charges stack) — Charcoal Powder
{
  "id": "core_item_powder_charcoal",
  "name": "Charcoal Powder",
  "kind": "resource",
  "tags": ["fuel","powder"],
  "base_volume_ml": 1500,
  "stack": { "mode": "charges", "max_per_stack": 100000, "unit": "g" },
  "value_base": 1
}

Armor — Leather Armor (material-tagged)
{
  "id": "core_armor_leather",
  "name": "Leather Armor",
  "kind": "armor",
  "tags": ["armor","leather"],
  "allowed_material_tags": ["leather"],
  "base_volume_ml": 5200,
  "stack": { "mode": "count", "max_per_stack": 1 },
  "equip": {
    "type": "armor",
    "body_slots": ["torso"],
    "layer": "outer",
    "encumbrance_bonus": 3,
    "coverage": 90
  }
}

Weapon — Dagger (material-agnostic)
{
  "id": "core_weapon_dagger",
  "name": "Dagger",
  "kind": "weapon",
  "tags": ["weapon","melee","metal"],
  "allowed_material_tags": ["metal"],
  "base_volume_ml": 300,
  "stack": { "mode": "count", "max_per_stack": 1, "equal_when": ["def_id","material_id","quality_tier","condition_state","improvements_hash"] },
  "weapon": {
    "damage": { "cut": 4, "stab": 6, "bash": 1 },
    "armor_penetration": { "stab": 3 },
    "attack_time_ms": 700,
    "reach_tiles": 1,
    "two_handed": false
  }
}

Placeable — Bed (wood-capable)
{
  "id": "core_placeable_bed",
  "name": "Bed",
  "kind": "placeable",
  "tags": ["furniture"],
  "allowed_material_tags": ["wood"],
  "base_mass_g": 15000,
  "stack": { "mode": "none" },
  "placeable": { "footprint": {"w":2,"h":1,"z":1}, "orientation_mask": ["N","E","S","W"], "passability": "solid", "beauty_base": 2, "comfort": 3, "light_lumen": 0, "heat_w": 0 }
}

Consumable — Fresh Meat (perishable)
{
  "id": "core_item_meat_fresh",
  "name": "Fresh Meat",
  "kind": "consumable",
  "tags": ["food","perishable"],
  "fixed_mass_g": 1500,
  "stack": { "mode": "charges", "max_per_stack": 100000, "unit": "g", "equal_when": ["def_id","freshness_bucket","material_id"] },
  "use": { "effects": ["nutrition_medium"], "charges_per_use": 250 }
}

13) Files & Loading

- data/core/items/*.json — item definitions (core dataset)
- data/mods/**/items/*.json — mod item definitions
- content/registries/*.json — registries (contracts, body plans, tuning, geology, materials registry); not item data
- content/schemas/*.json — JSON Schemas (validation)

Load order: registries (quality_tiers, improvement_rules, weapon_mod_slots, tuning, geology, materials) → items data (data/core then mods) → aliases.

14) Runtime Item Instance (save-layer; snake_case)

{
  "guid": "i_c9d2...",
  "def_id": "core_weapon_dagger",
  "material_id": "core_mat_metal_copper",  // null if fixed_material in def; otherwise chosen per instance
  "stack_count": 1,                          // or charges if stack.mode == "charges"

  // Ownership & availability
  "owner_faction_id": null,
  "owner_creature_guid": null,
  "use_policy": "public",                   // public|faction|private
  "forbidden": false,
  "reservation_tokens": [],

  // Quality & condition
  "quality_tier": 0,                         // -3..+3
  "artifact": false,
  "condition_state": "Pristine",

  // Maker & style (only if quality_tier >= +3 or artifact)
  "crafted_by": null,
  "maker_faction_id": null,
  "style_tag": null,

  // Improvements (only if quality_tier >= +3 or artifact)
  "improvements": [],

  // Perishable (food/drink only; omitted if non-perishable)
  "perishable": null,

  // Location (world or nested)
  "pos": null,
  "contained_by": null,

  "content_version": "3.0.0",
  "aliases_applied": []
}

Stack identity suggestions (used by def.stack.equal_when):
- Crafted gear: ["def_id","material_id","quality_tier","condition_state","improvements_hash"]
- Raw resources: ["def_id","material_id"]
- Food/drink: ["def_id","freshness_bucket","material_id"]

15) Perishables (policy)

- Only a small subset of foods rot. Default: non-perishable (no instance.perishable block).
- Opt-in by tag or def-level policy. Suggested authoring: add tag "perishable" to such food items; engine sets instance.perishable with freshness_state and spoil timings only for those items.
- Stacks for perishables include freshness_bucket in equal_when.

16) Backward Compatibility

- Keep legacy fields; map them onto v3 where obvious. Unknown fields → warnings; illegal enums → errors.
- aliases.json may map old IDs to new core_* IDs.
- All extension blocks are optional; missing blocks default to no effect.

