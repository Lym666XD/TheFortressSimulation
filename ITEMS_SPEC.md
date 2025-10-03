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

ITEMS_SPEC v4-int (Fixed-Point, Simplified) — English

Status: proposed (ready to implement)
Last updated: 2025-10-03 (Australia/Sydney)
Works with: MATERIALS_SPEC v4-min (FX = 10,000) and your Placeables spec
Runtime rule: integer-only. All dimensionless numbers are fixed-point FX=10,000 (1.0000 → 10000). No floats at runtime.

0) Design Intent & Big Decisions

No kind enum. Type is inferred from which feature blocks exist (equip, weapon, ammo, container, placeable_profile, use). tags are for filtering/UI only.

No material_weights, no material choice on the item. Material selection is a recipe concern. The item either has a fixed_material or receives its material at craft time.

Mass is authored, not derived. Authors set mass_g directly. We do not compute mass from volume×density (except optionally for standardized resources like ingots).

Armor thickness & environment protection live per-region. thickness_factor_fx and env_protect are on equip.regions[].

Encumbrance = mass effect × ergonomics × class factor. Mass uses a LUT; shape/ergonomics & small class factor live in shape_mod (top-level, shared by weapons/armor).

Decoration/Engraving/Enchanting are unified on the item root (decor). If not allowed, simply omit or set allow_* = false.

Value × Quality are merged. Authors keep quality_tier (−3..+3), but the compiler merges style/premium + quality into one value_mul_fx so runtime uses a single FX multiplier.

Ingot volumes: Gold & Silver = 50 ml, all other metals = 100 ml. You may still author mass_g directly.

Binding/soulbound: reserved as a concept; not encoded in this data structure yet.

1) Fixed-Point Policy

Global scale: FX = 10_000.

All multipliers, coefficients, and probabilities are integers scaled by FX (e.g., +25% → 12500).

Times are integers in milliseconds.

Volumes in ml; masses in g; counts in piece.

For powers/roots use LUTs + linear interpolation (no runtime floats).

2) Top-Level Item
type FX = number; // integer, 10000 == 1.0000

Item {
  id: string
  name?: string
  desc?: string
  tags: string[]

  // Material is chosen by the recipe. Item may fix it if needed:
  fixed_material?: string

  // Geometry / mass:
  base_volume_ml: number         // storage/footprint only (no mass coupling)
  mass_g?: number                // authored mass (recommended)

  // Value & Quality (merged):
  quality_tier?: number          // -3..+3 (default 0)
  value_mul_fx?: FX              // single final FX multiplier (compiler merges quality + style)

  // Stacking:
  stack: StackBlock

  // Feature blocks (presence implies type):
  equip?: EquipBlock
  weapon?: WeaponBlock
  ammo?: AmmoBlock
  container?: ContainerBlock
  use?: UseBlock
  placeable_profile?: PlaceableProfile   // see your Placeables spec

  // Shape-wide modifiers (shared by armor/weapons; used for encumbrance):
  shape_mod?: {
    ergonomics_fx?: FX          // better ergonomics < 10000 (default 10000)
    base_enc_points?: number    // encumbrance base points (additive)
    class_enc_fx?: FX           // small class factor (e.g., hammers > swords); optional
  }

  // Decoration / Engraving / Enchanting (unified):
  decor?: {
    allow_inlay?: boolean
    allow_engrave?: boolean
    allow_enchant?: boolean
    sockets?: { kind: "gem_small"|"gem_large"|"ornament"|"bossing", count: number }[]
    engrave_slots?: number
    enchant_slots?: number
    description_templates?: string[]   // text templates for generated descriptions
  }

  // Visible inscriptions (includes engraving, history, quest notes, owner mark, etc.):
  inscriptions?: Inscription[]
}

Inscription {
  type: "engrave" | "history" | "quest" | "owner_mark"
  text: string
}

2.1 Type inference (no kind)

Schema should enforce a oneOf:

Resource/misc: no equip/weapon/ammo/container/placeable_profile.

Armor/Wearable: has equip.

Weapon: has weapon.

Ammo: has ammo.

Container: has container.

Placeable: has placeable_profile.

3) Stacks
StackBlock {
  mode: "none" | "count" | "charges"
  unit?: "piece" | "g" | "ml"    // required for charges; count uses "piece"
  max_per_stack: number
  equal_when: string[]           // e.g., ["material_id","quality","condition"]
  requires_pristine?: boolean
  require_no_mods?: boolean
  require_no_enchant?: boolean
  requires_empty?: boolean       // containers must be empty to stack
}

4) Encumbrance (with authored mass)

Inputs:

mass_g (authored),

shape_mod.ergonomics_fx,

shape_mod.class_enc_fx,

shape_mod.base_enc_points.

Process:

M_mass_fx = LUT_mass( mass_g / m_ref_g )       // FX, LUT + linear interpolation
M_enc_fx  = M_mass_fx × ergonomics_fx × class_enc_fx / FX^2
enc_points_final = base_enc_points × M_enc_fx / FX


Typical m_ref_g = 1000. If you ever want to ignore mass, set LUT_mass ≡ FX and drive feel entirely by ergonomics/class factors.

5) EquipBlock (Armor / Wearables)

You asked for regions as an array, with layer per region and environment protection per region.

We do not track “different material layers per region” (that explodes complexity). Material is per item instance (or fixed), not per layer.

type BodyRegionId =
  "HEAD"|"FACE"|"NECK"|"TORSO"|"ARM_L"|"ARM_R"|"HAND_L"|"HAND_R"|
  "LEG_L"|"LEG_R"|"FOOT_L"|"FOOT_R";

type EquipLayer = "INNER"|"MID"|"OUTER"|"CLOAK"|"STRIPE"|"AURA";

EquipBlock {
  // optional slot shortcut; most logic is region-driven anyway:
  slot?: "HEAD"|"TORSO"|"LEGS"|"HANDS"|"FEET"|"SHIELD"|"RING"|"AMULET"|"..."

  regions: EquipRegion[]
}

EquipRegion {
  id: BodyRegionId
  coverage_pct: number              // 0..100
  layer: EquipLayer                 // where this item sits on that body part
  layers?: number                   // UI/crafting label only; not a physics stack
  thickness_factor_fx?: FX          // default 10000; per-region thickness
  env_protect?: number              // 0..100; with 100% coverage means full protection for that part
}

5.1 Armor protection math (summary)

For each region:

R_type(region) =
  Base_R_item
× R_type_mat_fx(material)     // from MATERIALS v4-min
× thickness_factor_fx(region)
× shape_mod_effects?          // if you model them as FX multipliers
/ FX^n


Combine regions by your coverage/overlap rules (area weights, diminishing returns, etc.).

6) WeaponBlock & Attack Profiles
WeaponBlock {
  hands?: 1 | 2
  reach_tiles?: number
  handling_base_fx?: FX
  accuracy_base_fx?: FX
  attacks: AttackProfile[]
}

AttackProfile {
  // melee
  mode: "bash" | "cut" | "stab" | "shoot"
  base_damage?: number
  ap_hint_fx?: FX
  windup_ms?: number
  recovery_ms?: number

  // ranged (when mode === "shoot"):
  uses_ammo?: string
  draw_time_ms?: number
  cycle_time_ms?: number
  reload_time_ms?: number
  aim_time_ms?: number
  recoil_base_fx?: FX
  spread_base_moa?: number

  verbs?: string[]
  trait_tags?: string[]
}

6.1 Weapon final stats (integer)
D_final[mode] = base_damage × M_mode_fx(material, mass) / FX
t_final_ms    = (windup_ms + recovery_ms) × M_t_fx(material, mass) / FX
hit_final     = accuracy_base_fx × M_hit_fx(material, mass) / FX


The M_*_fx come from materials v4-min (hardness/toughness/rigidity + density if you use it), but your mass is authored now (not derived).

7) Ammo
AmmoBlock {
  caliber_id?: string                // optional for firearms
  compatible_weapons?: string[]      // usually omit; rely on uses_ammo matching
  projectile_mass_g?: number
  projectile_length_mm?: number
  base_damage: { bash?: number, cut?: number, stab?: number }
  armor_penetration_fx?: FX
  velocity_class?: "low"|"med"|"high"|"very_high"
}

8) Containers
ContainerBlock {
  capacity_ml: number
  accept_tags?: string[]     // e.g., ["liquid","powder","grain"]
  leakproof?: boolean
  pressure_ok?: boolean      // gas/pressure capable (future)
  food_safe?: boolean
}

9) Use (Consumables)
UseBlock {
  effects: string[]          // e.g., "heal_small","warmth_small","stamina_boost"
  charges_per_use: number
}

10) Placeable Profile

Reference your PLACEABLE_SPEC.md. The item’s placeable_profile is the installable profile; when installed it becomes a placed instance that inherits material/quality/inscriptions etc. from the item.

PlaceableProfile {
  // shape/footprint/orientation/passability per your Placeables spec
  // effects (beauty/comfort/etc.) live here as defined there
}

11) Value & Quality (merged)

quality_tier ∈ {-3..+3}.

Compiler merges quality and any style/premium factors into one value_mul_fx (e.g., −30%..+50%).

Runtime price:

final_value = base_value × value_mul_fx(material) × value_mul_fx(item) / FX^2


(If you prefer, apply material first then item, but it’s one combined FX on the item side.)

12) Durability → Stat Falloff (optional)

Each item class may register LUTs for HP% → multipliers:

damage_mul_fx(hp_pct)

tohit_mul_fx(hp_pct)

encumbrance_bonus(hp_pct) (additive or FX depending on your design)

Evaluate by table + linear interpolation (integers).

13) Ingots (standard volumes)

Gold/Silver: base_volume_ml = 50

All other metals: base_volume_ml = 100

Mass either authored (mass_g) or derived at load time using material density×volume (optional, off by default).

14) Reserved (not in data yet)

Binding / Soulbound: ("soulbound"|"account"|"none") reserved concept; omitted from schema for now.

Spell channels / arcane focus: not included. If needed later, add fields like spell_amp_slots or focus_gain_fx on the item or shape_mod.

15) Examples
15.1 Short Sword
{
  "id": "core_item_weapon_sword_short",
  "name": "Short Sword",
  "tags": ["weapon","melee","blade","one_handed"],
  "fixed_material": "core_mat_metal_steel_tempered",
  "mass_g": 900,
  "base_volume_ml": 900,
  "quality_tier": 0,
  "value_mul_fx": 10000,
  "stack": { "mode": "count", "unit": "piece", "max_per_stack": 8,
             "equal_when": ["material_id","quality","condition"] },
  "weapon": {
    "reach_tiles": 1,
    "handling_base_fx": 10000,
    "accuracy_base_fx": 10000,
    "attacks": [
      { "mode": "cut",  "base_damage": 16, "ap_hint_fx": 9000, "windup_ms": 350, "recovery_ms": 350, "verbs": ["slash","cut"] },
      { "mode": "stab", "base_damage": 12, "ap_hint_fx": 11000,"windup_ms": 300, "recovery_ms": 300, "verbs": ["stab","thrust"] }
    ]
  },
  "shape_mod": { "ergonomics_fx": 9500, "base_enc_points": 3, "class_enc_fx": 9500 },
  "decor": { "allow_inlay": true, "allow_engrave": true, "allow_enchant": true,
             "sockets":[{"kind":"gem_small","count":1}], "engrave_slots":1, "enchant_slots":1 }
}

15.2 Milanese Breastplate (regions + layers + env protection)
{
  "id": "core_item_armor_breastplate_milanese",
  "name": "Milanese Breastplate",
  "tags": ["armor","plate"],
  "fixed_material": "core_mat_metal_steel_tempered",
  "mass_g": 8500,
  "base_volume_ml": 9500,
  "stack": { "mode": "count", "unit": "piece", "max_per_stack": 6,
             "equal_when": ["material_id","quality","condition"] },
  "equip": {
    "regions": [
      { "id": "TORSO", "coverage_pct": 100, "layer": "OUTER", "layers": 1, "thickness_factor_fx": 11000, "env_protect": 10 },
      { "id": "NECK",  "coverage_pct": 30,  "layer": "OUTER", "layers": 1, "thickness_factor_fx": 10500, "env_protect": 5  }
    ]
  },
  "shape_mod": { "ergonomics_fx": 9000, "base_enc_points": 8, "class_enc_fx": 10500 },
  "decor": { "allow_inlay": true, "allow_engrave": true,
             "sockets":[{"kind":"bossing","count":1}], "engrave_slots":1 }
}

15.3 Longbow & Arrow
{
  "id": "core_item_weapon_bow_long",
  "name": "Longbow",
  "tags": ["weapon","ranged","bow","two_handed"],
  "fixed_material": "core_mat_wood_yew",
  "mass_g": 1200,
  "base_volume_ml": 3000,
  "stack": { "mode": "count", "unit": "piece", "max_per_stack": 4,
             "equal_when": ["material_id","quality","condition"] },
  "weapon": {
    "attacks": [
      { "mode": "shoot", "uses_ammo": "arrow",
        "draw_time_ms": 800, "aim_time_ms": 300, "recoil_base_fx": 9000,
        "verbs": ["loose","shoot"] }
    ]
  },
  "shape_mod": { "ergonomics_fx": 9000, "base_enc_points": 4, "class_enc_fx": 10000 }
}

{
  "id": "core_ammo_arrow",
  "name": "Arrow",
  "tags": ["ammo","arrow"],
  "fixed_material": "core_mat_wood_poplar",
  "mass_g": 30,
  "base_volume_ml": 50,
  "stack": { "mode": "count", "unit": "piece", "max_per_stack": 100,
             "equal_when": ["material_id","quality"] },
  "ammo": {
    "projectile_mass_g": 25,
    "base_damage": { "stab": 10 },
    "armor_penetration_fx": 10500,
    "velocity_class": "med"
  }
}

15.4 Ingots (standard volumes)
{
  "id": "core_item_ingot_gold",
  "name": "Gold Ingot",
  "tags": ["metal","ingot","precious"],
  "fixed_material": "core_mat_metal_gold",
  "base_volume_ml": 50,
  "mass_g": 965,
  "stack": { "mode": "count", "unit": "piece", "max_per_stack": 100,
             "equal_when": ["fixed_material"] }
}

{
  "id": "core_item_ingot_iron",
  "name": "Iron Ingot",
  "tags": ["metal","ingot","iron"],
  "fixed_material": "core_mat_metal_iron",
  "base_volume_ml": 100,
  "mass_g": 785,
  "stack": { "mode": "count", "unit": "piece", "max_per_stack": 50,
             "equal_when": ["fixed_material"] }
}

16) Validation Checklist

Integer-only runtime: all multipliers are FX integers (10000 = 1.0).

No kind; type is inferred by block presence; schema oneOf enforces this.

No material_weights on items; materials chosen by recipes (or via fixed_material).

equip.regions[] is an array, holds layer, thickness_factor_fx, env_protect, and coverage_pct.

Encumbrance uses authored mass_g + shape_mod ergonomics/class.

Decoration is top-level (decor); omit or set allow_* = false if forbidden.

Value×Quality compiled into one value_mul_fx.

Ingots use 50 ml (gold/silver) and 100 ml (others) as standard volumes (optional mass derivation).

Durability falloff LUTs are per-class (optional).

If you want, I can now draft a JSON Schema (draft-07) for this spec (with oneOf type inference, FX integer checks, and full validation on equip.regions[]) and a v3→v4-int migration map (speed→windup/recovery, move thickness to regions, add mass_g, remove kind/material_weights, unify container fields, etc.).