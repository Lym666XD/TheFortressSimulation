CREATURES_SPEC.md — v1 (Unified, Multi-Limb Ready)
id: creatures.v1
status: normative (back-compatible)
owner: content/registry
last_updated: 2025-09-15
goals:
  - CDDA-level complexity; avoid DF’s ultra-detailed tissues.
  - Data-driven creatures with stable IDs and JSON Schemas (no hard-coded enums).
  - Support multi-limb / multi-wing body plans deterministically.
  - Ship non-operative placeholders for future cognition/temperament without changing gameplay.
depends_on:
  - material.v3
  - item.v3
  - tuning.damage.json
  - CHUNK_ACTOR_PROTOCOL.md
  - DIFF_LOG_AND_MERGE_STRATEGIES.md
  - UPDATE_ORDER.md

0) Scope & Guarantees

One registry defines body plans and creatures; all cross-refs use string IDs that bind to runtime handles at load time.

Determinism: hit selection, attack ordering, wounds, and job effects are seedable and independent of thread count.

Crash-proof: content load and per-tick simulation are wrapped in try–catch; bad entries are skipped and logged; the game loop must not crash.

Back-compat: existing v1 content continues to validate; new fields have safe defaults and are ignored by runtime until wired.

1) Core Principles (Normative)

Two-level design

Body Plan: the anatomy template (slots or slot groups), hit weights, natural armor, functional roles (grasp/stand/fly…).

Creature: species stats (STR/AGI/END/PER/INT/WIL), senses, gaits, attacks, behavior, reproduction, drops, plus optional placeholders (facets, aptitudes, stamina, pain, morale, courage, disease_resist).

IDs over enums everywhere (materials, items, factions, body plans). Enums in schema remain small and open to extension via IDs/tags on the content side.

Coverage & equipment: items reference slot IDs (or slot-group expressions) for wear coverage, consistent with ITEMS_SPEC.md.

Units: mass in kg, volume in ml, distances in tiles, time in seconds, action costs in AP. No ad-hoc “uses”.

2) Runtime Semantics (Normative)
2.1 Hit & Damage

Choose a body slot by normalized hit weights (torso/head carry most; limbs/wings/tails share the rest).

Combine attack’s damage mix (bash/cut/stab/…); apply slot natural armor → creature resist multipliers → equipped armor → tuning.damage curves.

Produce wounds (stagger/bleed/fracture/KO) with bounded crits on vitals (head/torso).

2.2 Limb disablement (coarse)

Broken grasp slot disables wielding for that limb; broken stand causes heavy move penalties or knockdown risk; damaged fly lowers lift.

2.3 Attacks & AP

Each natural attack has ap_cost and cooldown_s. Latch/grab apply periodic effects until the state is broken.

2.4 Encumbrance (burden)

Burden = f(carried kg, worn mass, size_kg, burden_mult, STR) → movement/AP penalties via movement tuning.

2.5 Saving

Persist only IDs (creature/body plan/attacks), light state (HP per slot, wounds, temp states, inventory GUIDs). Never store runtime handles.

3) Update Order & Concurrency (Normative)

Stage: CreaturesStep runs after fields/fluids, before projectiles.

Within chunk: systems produce diff-logs (per-tile writes) and merge deterministically in a fixed order (chunk → tile → system → systemId).

Cross chunk: use Chunk Actor mailboxes; delivery order (tick → senderChunkId → seq) is stable.

Error fences: each creature tick guarded by try–catch; on exception: pause AI for that actor, preserve state, emit a structured error.

4) JSON Schemas (no comments)
4.1 body_plan.v1.schema.json (updated, multi-limb ready)
{
  "$id": "body_plan.v1.schema.json",
  "type": "object",
  "required": ["id"],
  "properties": {
    "id": { "type": "string" },

    "slots": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["slot_id", "hit_weight"],
        "properties": {
          "slot_id": { "type": "string", "pattern": "^[a-z0-9_]+$" },
          "hit_weight": { "type": "number", "minimum": 0 },
          "vital": { "type": "boolean" },
          "can_grasp": { "type": "boolean" },
          "can_stand": { "type": "boolean" },
          "functional_roles": { "type": "array", "items": { "type": "string", "enum": ["grasp","stand","fly","balance","bite","kick","gore"] } },
          "natural_armor": {
            "type": "object",
            "properties": {
              "bash": { "type": "number" },
              "cut": { "type": "number" },
              "stab": { "type": "number" },
              "fire": { "type": "number" },
              "acid": { "type": "number" },
              "cold": { "type": "number" },
              "electric": { "type": "number" },
              "arcane": { "type": "number" }
            },
            "additionalProperties": false
          },
          "lift_score": { "type": "number", "minimum": 0 }
        },
        "additionalProperties": false
      }
    },

    "slot_groups": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["group_id","base_name","min_count","max_count","hit_weight_each"],
        "properties": {
          "group_id": { "type": "string", "pattern": "^[a-z0-9_]+$" },
          "base_name": { "type": "string", "pattern": "^[a-z0-9_]+$" },
          "paired": { "type": "boolean" },
          "min_count": { "type": "integer", "minimum": 0 },
          "max_count": { "type": "integer", "minimum": 0 },
          "hit_weight_each": { "type": "number", "minimum": 0 },
          "functional_roles": { "type": "array", "items": { "type": "string", "enum": ["grasp","stand","fly","balance","bite","kick","gore"] } },
          "natural_armor": {
            "type": "object",
            "properties": {
              "bash": { "type": "number" },
              "cut": { "type": "number" },
              "stab": { "type": "number" },
              "fire": { "type": "number" },
              "acid": { "type": "number" },
              "cold": { "type": "number" },
              "electric": { "type": "number" },
              "arcane": { "type": "number" }
            },
            "additionalProperties": false
          },
          "lift_score_each": { "type": "number", "minimum": 0 },
          "naming": {
            "type": "object",
            "properties": {
              "index_from": { "type": "integer", "minimum": 0 },
              "sides": { "type": "array", "items": { "type": "string", "enum": ["l","r"] } },
              "format": { "type": "string" }
            },
            "additionalProperties": false
          }
        },
        "additionalProperties": false
      }
    },

    "slot_overrides": {
      "type": "object",
      "additionalProperties": {
        "type": "object",
        "properties": {
          "hit_weight": { "type": "number" },
          "natural_armor": {
            "type": "object",
            "properties": {
              "bash": { "type": "number" },
              "cut": { "type": "number" },
              "stab": { "type": "number" },
              "fire": { "type": "number" },
              "acid": { "type": "number" },
              "cold": { "type": "number" },
              "electric": { "type": "number" },
              "arcane": { "type": "number" }
            },
            "additionalProperties": false
          },
          "lift_score": { "type": "number" }
        },
        "additionalProperties": false
      }
    }
  },
  "additionalProperties": false
}


Expansion rule (informative)
If slot_groups exists, a content compiler may expand groups into concrete slots. The order is deterministic: by group_id (lex) → by index asc → side l before r. Default name format: "{base_name}_{index}_{side}" with index_from=1. Engines that haven’t implemented expansion yet can ignore slot_groups and rely on explicit slots.

4.2 creature.v1.schema.json (updated, placeholders included)
{
  "$id": "creature.v1.schema.json",
  "type": "object",
  "required": ["id","tags","body_plan_id","size_kg","stats","senses","gaits","attacks","hp"],
  "properties": {
    "id": { "type": "string" },
    "tags": { "type": "array", "items": { "type": "string" } },
    "name": { "type": "string" },
    "desc": { "type": "string" },

    "faction_id": { "type": "string" },
    "body_plan_id": { "type": "string" },

    "size_kg": { "type": "number", "minimum": 0.1 },
    "burden_mult": { "type": "number", "minimum": 0.1 },

    "stats": {
      "type": "object",
      "required": ["STR","AGI","END","PER","INT","WIL"],
      "properties": {
        "STR": { "type": "integer", "minimum": 1, "maximum": 20 },
        "AGI": { "type": "integer", "minimum": 1, "maximum": 20 },
        "END": { "type": "integer", "minimum": 1, "maximum": 20 },
        "PER": { "type": "integer", "minimum": 1, "maximum": 20 },
        "INT": { "type": "integer", "minimum": 1, "maximum": 20 },
        "WIL": { "type": "integer", "minimum": 1, "maximum": 20 }
      },
      "additionalProperties": false
    },

    "facets": {
      "type": "object",
      "properties": {
        "creativity": { "type": "number", "minimum": 0.1, "maximum": 2.0 },
        "spatial_reasoning": { "type": "number", "minimum": 0.1, "maximum": 2.0 },
        "attention_focus": { "type": "number", "minimum": 0.1, "maximum": 2.0 },
        "learning_rate": { "type": "number", "minimum": 0.1, "maximum": 2.0 },
        "empathy": { "type": "number", "minimum": 0.1, "maximum": 2.0 },
        "discipline": { "type": "number", "minimum": 0.1, "maximum": 2.0 },
        "perception_detail": { "type": "number", "minimum": 0.1, "maximum": 2.0 }
      },
      "additionalProperties": false
    },

    "aptitudes": {
      "type": "object",
      "additionalProperties": { "type": "number", "minimum": 0.1, "maximum": 2.0 }
    },

    "senses": {
      "type": "object",
      "required": ["vision_tiles","darkvision_tiles","smell","hearing"],
      "properties": {
        "vision_tiles": { "type": "integer", "minimum": 0 },
        "darkvision_tiles": { "type": "integer", "minimum": 0 },
        "smell": { "type": "integer", "minimum": 0 },
        "hearing": { "type": "integer", "minimum": 0 }
      },
      "additionalProperties": false
    },

    "thermo": {
      "type": "object",
      "properties": {
        "homeotherm_c": { "type": "number" },
        "comfort_c_min": { "type": "number" },
        "comfort_c_max": { "type": "number" }
      },
      "additionalProperties": false
    },

    "gaits": {
      "type": "object",
      "required": ["walk","run"],
      "properties": {
        "walk": { "type": "object", "properties": { "tiles_per_s": { "type": "number" }, "ap_cost": { "type": "integer" } }, "additionalProperties": false },
        "run":  { "type": "object", "properties": { "tiles_per_s": { "type": "number" }, "ap_cost": { "type": "integer" } }, "additionalProperties": false },
        "swim": { "type": "object", "properties": { "tiles_per_s": { "type": "number" }, "ap_cost": { "type": "integer" } }, "additionalProperties": false },
        "climb":{ "type": "object", "properties": { "tiles_per_s": { "type": "number" }, "ap_cost": { "type": "integer" } }, "additionalProperties": false },
        "fly":  { "type": "object", "properties": { "tiles_per_s": { "type": "number" }, "ap_cost": { "type": "integer" } }, "additionalProperties": false }
      },
      "additionalProperties": false
    },

    "movement_flags": {
      "type": "array",
      "items": { "type": "string", "enum": ["SWIMS","CLIMBS","FLIES","MOUNT","PACK_ANIMAL"] }
    },

    "hp": {
      "type": "object",
      "required": ["max","slot_scale"],
      "properties": {
        "max": { "type": "integer", "minimum": 1 },
        "slot_scale": { "type": "object", "additionalProperties": { "type": "number", "minimum": 0 } }
      },
      "additionalProperties": false
    },

    "resist_mult": {
      "type": "object",
      "properties": {
        "bash": { "type": "number" },
        "cut": { "type": "number" },
        "stab": { "type": "number" },
        "fire": { "type": "number" },
        "acid": { "type": "number" },
        "cold": { "type": "number" },
        "electric": { "type": "number" },
        "arcane": { "type": "number" }
      },
      "additionalProperties": false
    },

    "attacks": {
      "type": "array",
      "minItems": 1,
      "items": {
        "type": "object",
        "required": ["id","verb","kind","slot","damage","ap_cost","cooldown_s"],
        "properties": {
          "id": { "type": "string" },
          "verb": { "type": "string" },
          "kind": { "type": "string", "enum": ["bite","claw","scratch","kick","gore","punch","slam","sting"] },
          "slot": { "type": "string" },
          "damage": {
            "type": "object",
            "properties": { "bash": { "type": "number" }, "cut": { "type": "number" }, "stab": { "type": "number" } },
            "additionalProperties": false
          },
          "penetration_mm": { "type": "number", "minimum": 0 },
          "contact_cm2": { "type": "number", "minimum": 0 },
          "latch": { "type": "boolean" },
          "grab": { "type": "boolean" },
          "ap_cost": { "type": "integer", "minimum": 0 },
          "cooldown_s": { "type": "number", "minimum": 0 }
        },
        "additionalProperties": false
      }
    },

    "behavior": {
      "type": "object",
      "properties": {
        "ai_tags": { "type": "array", "items": { "type": "string", "enum": ["PREDATOR","HERBIVORE","DOCILE","AGGRESSIVE","SKITTISH","PACK","PET","VERMIN_HUNTER"] } },
        "leash_to_faction": { "type": "string" },
        "tameness": { "type": "integer", "minimum": 0, "maximum": 100 }
      },
      "additionalProperties": false
    },

    "reproduction": {
      "type": "object",
      "properties": {
        "gestation_days": { "type": "integer", "minimum": 0 },
        "litter_size_min": { "type": "integer", "minimum": 0 },
        "litter_size_max": { "type": "integer", "minimum": 0 },
        "milk_ml": { "type": "integer", "minimum": 0 },
        "shear_wool_item_id": { "type": "string" },
        "shear_interval_days": { "type": "integer", "minimum": 0 }
      },
      "additionalProperties": false
    },

    "drops": {
      "type": "object",
      "properties": {
        "butcher_table": { "type": "array", "items": { "type": "object", "required": ["item_id","count"], "properties": { "item_id": { "type": "string" }, "count": { "type": "integer", "minimum": 1 } }, "additionalProperties": false } },
        "butcher_by_mass": { "type": "array", "items": { "type": "object", "required": ["material_id","kg_per_kg"], "properties": { "material_id": { "type": "string" }, "kg_per_kg": { "type": "number", "minimum": 0 } }, "additionalProperties": false } }
      },
      "additionalProperties": false
    },

    "stamina": {
      "type": "object",
      "properties": {
        "max": { "type": "integer", "minimum": 0 },
        "regen_per_s": { "type": "number", "minimum": 0 }
      },
      "additionalProperties": false
    },

    "pain": {
      "type": "object",
      "properties": {
        "tolerance": { "type": "number", "minimum": 0 },
        "shock_suscept": { "type": "number", "minimum": 0 }
      },
      "additionalProperties": false
    },

    "morale": {
      "type": "object",
      "properties": {
        "baseline": { "type": "number" },
        "decay_per_s": { "type": "number" }
      },
      "additionalProperties": false
    },

    "courage": { "type": "number" },
    "disease_resist": { "type": "number" }
  },
  "additionalProperties": false
}


Non-operative placeholders (explicit)
Until wired by engine feature flags, ignore at runtime (but validate & serialize):

In body plan: slot_groups, functional_roles, lift_score, group lifting/armor.

In creature: facets, aptitudes, stamina, pain, morale, courage, disease_resist.

5) Authoring & Expansion (Informative)
5.1 Canonical Body Plans

Humanoid (simple)

{
  "id": "humanoid_simple",
  "slots": [
    { "slot_id": "head",  "hit_weight": 0.12, "vital": true },
    { "slot_id": "torso", "hit_weight": 0.46, "vital": true },
    { "slot_id": "arm_l","hit_weight": 0.10, "functional_roles": ["grasp"] },
    { "slot_id": "arm_r","hit_weight": 0.10, "functional_roles": ["grasp"] },
    { "slot_id": "leg_l","hit_weight": 0.11, "functional_roles": ["stand"] },
    { "slot_id": "leg_r","hit_weight": 0.11, "functional_roles": ["stand"] }
  ]
}


Quadruped (basic)

{
  "id": "quadruped_basic",
  "slots": [
    { "slot_id": "head",  "hit_weight": 0.10, "vital": true },
    { "slot_id": "torso", "hit_weight": 0.45, "vital": true },
    { "slot_id": "leg_fl","hit_weight": 0.10, "functional_roles": ["stand"] },
    { "slot_id": "leg_fr","hit_weight": 0.10, "functional_roles": ["stand"] },
    { "slot_id": "leg_rl","hit_weight": 0.10, "functional_roles": ["stand"] },
    { "slot_id": "leg_rr","hit_weight": 0.10, "functional_roles": ["stand"] },
    { "slot_id": "tail",  "hit_weight": 0.05, "functional_roles": ["balance"] }
  ]
}


Angel (one–two wing pairs, future-ready)

{
  "id": "angelic_base",
  "slots": [
    { "slot_id": "head",  "hit_weight": 0.12, "vital": true },
    { "slot_id": "torso", "hit_weight": 0.42, "vital": true },
    { "slot_id": "arm_l","hit_weight": 0.10, "functional_roles": ["grasp"] },
    { "slot_id": "arm_r","hit_weight": 0.10, "functional_roles": ["grasp"] },
    { "slot_id": "leg_l","hit_weight": 0.13, "functional_roles": ["stand"] },
    { "slot_id": "leg_r","hit_weight": 0.13, "functional_roles": ["stand"] }
  ],
  "slot_groups": [
    { "group_id": "wing", "base_name": "wing", "paired": true, "min_count": 1, "max_count": 2, "hit_weight_each": 0.04, "functional_roles": ["fly","balance"], "lift_score_each": 1.0,
      "naming": { "index_from": 1, "sides": ["l","r"], "format": "{base}_{index}_{side}" } }
  ]
}


Demon (extra arms, horns, tail, future-ready)

{
  "id": "demonic_base",
  "slots": [
    { "slot_id": "head",  "hit_weight": 0.12, "vital": true, "natural_armor": { "cut": 0.1, "stab": 0.1 } },
    { "slot_id": "torso", "hit_weight": 0.40, "vital": true },
    { "slot_id": "leg_l","hit_weight": 0.14, "functional_roles": ["stand"] },
    { "slot_id": "leg_r","hit_weight": 0.14, "functional_roles": ["stand"] },
    { "slot_id": "tail",  "hit_weight": 0.06, "functional_roles": ["balance"] },
    { "slot_id": "horns","hit_weight": 0.02, "functional_roles": ["gore"] }
  ],
  "slot_groups": [
    { "group_id": "arm", "base_name": "arm", "paired": true, "min_count": 2, "max_count": 3, "hit_weight_each": 0.06, "functional_roles": ["grasp"],
      "naming": { "index_from": 1, "sides": ["l","r"], "format": "{base}_{index}_{side}" } }
  ]
}

5.2 Minimal Creatures

Dog

{
  "id": "cre_dog",
  "tags": ["animal","domestic","canine"],
  "name": "Dog",
  "body_plan_id": "quadruped_basic",
  "size_kg": 30.0,
  "burden_mult": 1.0,
  "stats": { "STR": 6, "AGI": 8, "END": 6, "PER": 7, "INT": 3, "WIL": 5 },
  "senses": { "vision_tiles": 16, "darkvision_tiles": 2, "smell": 10, "hearing": 8 },
  "gaits": { "walk": { "tiles_per_s": 2.5, "ap_cost": 10 }, "run": { "tiles_per_s": 6.0, "ap_cost": 16 } },
  "hp": { "max": 60, "slot_scale": { "head": 0.2, "torso": 0.5, "leg_fl": 0.075, "leg_fr": 0.075, "leg_rl": 0.075, "leg_rr": 0.075 } },
  "attacks": [
    { "id": "bite", "verb": "bite", "kind": "bite", "slot": "head", "damage": { "bash": 6, "stab": 8 }, "penetration_mm": 8, "contact_cm2": 6, "latch": true, "ap_cost": 12, "cooldown_s": 1.0 }
  ],
  "behavior": { "ai_tags": ["DOCILE","VERMIN_HUNTER"], "tameness": 80 }
}


Angel (two wing pairs; wings are placeholders until flight is wired)

{
  "id": "cre_angel_seraph",
  "tags": ["humanoid","angelic"],
  "name": "Seraph",
  "body_plan_id": "angelic_base",
  "size_kg": 75.0,
  "burden_mult": 0.9,
  "stats": { "STR": 10, "AGI": 12, "END": 10, "PER": 10, "INT": 12, "WIL": 14 },
  "senses": { "vision_tiles": 20, "darkvision_tiles": 6, "smell": 2, "hearing": 10 },
  "gaits": { "walk": { "tiles_per_s": 2.8, "ap_cost": 10 }, "run": { "tiles_per_s": 6.5, "ap_cost": 16 }, "fly": { "tiles_per_s": 8.0, "ap_cost": 14 } },
  "hp": { "max": 120, "slot_scale": { "head": 0.15, "torso": 0.50 } },
  "attacks": [
    { "id": "smite", "verb": "smite", "kind": "slam", "slot": "arm_r", "damage": { "bash": 15 }, "penetration_mm": 0, "contact_cm2": 40, "ap_cost": 16, "cooldown_s": 2.0 }
  ],
  "facets": { "discipline": 1.2, "attention_focus": 1.1, "empathy": 1.1 },
  "behavior": { "ai_tags": ["AGGRESSIVE","PACK"], "tameness": 0 }
}


Demon (three arm pairs; extra arms are placeholders until grasp-count checks are wired)

{
  "id": "cre_demon_marauder",
  "tags": ["humanoid","demonic"],
  "name": "Marauder",
  "body_plan_id": "demonic_base",
  "size_kg": 110.0,
  "burden_mult": 1.2,
  "stats": { "STR": 14, "AGI": 9, "END": 13, "PER": 8, "INT": 9, "WIL": 12 },
  "senses": { "vision_tiles": 16, "darkvision_tiles": 12, "smell": 6, "hearing": 8 },
  "gaits": { "walk": { "tiles_per_s": 2.2, "ap_cost": 10 }, "run": { "tiles_per_s": 5.2, "ap_cost": 16 } },
  "hp": { "max": 160, "slot_scale": { "head": 0.12, "torso": 0.50 } },
  "attacks": [
    { "id": "gore", "verb": "gore", "kind": "gore", "slot": "horns", "damage": { "stab": 18 }, "penetration_mm": 18, "contact_cm2": 5, "ap_cost": 18, "cooldown_s": 2.5 },
    { "id": "smash", "verb": "smash", "kind": "slam", "slot": "arm_1_r", "damage": { "bash": 16 }, "penetration_mm": 0, "contact_cm2": 60, "ap_cost": 16, "cooldown_s": 1.8 }
  ],
  "behavior": { "ai_tags": ["AGGRESSIVE","PREDATOR"], "tameness": 0 }
}

6) Determinism & Error Handling (Normative)

Deterministic keys:

Hit selection uses a canonical list of slots (explicit slots plus expanded groups) sorted lexicographically by slot_id.

Attack selection within a tick uses (cooldown_remaining, attack.id) with stable tie-breakers.

Cross-chunk message order is (tick, senderChunkId, seq).

try–catch fences:

Loader: each file and each entry; on error → skip entry, log {pack, file, id?, stage, reason} (rate-limited).

Runtime: each creature tick; on exception → pause AI for that actor, mark SimError, keep state, continue frame.

7) Save-Game Binding (Normative)

Save stores string IDs for all content references and a packset signature; runtime handles are re-bound on load.

Missing IDs at load time map to safe placeholders (missing_creature, etc.); references in jobs become paused with a visible reason.

8) CI Gates (Normative)

Schema validation OK for all body plans and creatures.

Cross-ref integrity (body_plan exists; item/material IDs exist; attack slots exist).

Determinism under thread jitter for: hit distribution, multi-arm/wings naming, attack cooldown ordering.

Save-load rebind passes with packset changes.

Bounds checks: HP split sums ≈ 1.0 tolerance; gait speeds > 0 when defined; no NaN/Inf.

9) Field Reference (Quick)

Body Plan

slots[]: explicit parts; hit_weight ≥ 0; optional vital, functional_roles, natural_armor, lift_score.

slot_groups[] (placeholder): define variable multiplicities (e.g., wings 1–2 pairs, arms 2–3 pairs); expanded deterministically if engine supports.

slot_overrides{slot_id→…}: tweak a generated slot.

Creature

stats: STR/AGI/END/PER/INT/WIL.

senses: vision/darkvision/smell/hearing (tiles/scalars).

gaits: walk/run (required), optional swim/climb/fly.

movement_flags: SWIMS/CLIMBS/FLIES/MOUNT/PACK_ANIMAL.

hp: {max, slot_scale{slotId→weight}}.

resist_mult: per-damage-type multipliers.

attacks[]: natural attacks with damage mix, penetration/contact, AP, cooldown, latch/grab.

behavior: AI tags, faction leash, tameness.

reproduction: gestation, litters, milk/shear.

drops: butcher outputs (table/by_mass).

Placeholders (non-operative until wired): facets, aptitudes, stamina, pain, morale, courage, disease_resist.