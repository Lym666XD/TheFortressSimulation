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

CREATURE_SPEC.md (v1.1)

Scope. Defines data-driven creature content and the minimal runtime instance/state needed by simulation, navigation, jobs, and saving.
Design goals. Deterministic, mod-friendly, minimal invariants, separation of registry (content truth) vs instance (runtime/save).

0) Files & Loading
content/
└─ registries/
   ├─ creatures.body_plans.json     # Body plans (equip slots, natural attacks, sizes)
   ├─ creatures.json                # Species/creature defs (references body plans)
   ├─ traits.json                   # (optional) trait defs; can be disabled
   └─ aliases.json                  # name/ID migration map (optional)


Load order: body_plans → traits → creatures → aliases applied.

All lookups by stable ID; names are for authoring, resolved at load.

Validation: JSON Schema (Draft 07) per file; cross-refs checked at load.

1) Registry: BodyPlan

A body plan defines anatomy & equipment affordances shared by multiple species.

{
  "id": "humanoid_simple",
  "displayName": "Humanoid (Simple)",
  "sizeClass": "M",               // XS,S,M,L,XL (affects mass, carry, collision)
  "baseMassKg": 70,               // reference mass used for encumbrance
  "equipSlots": [
    {"id":"head","max":1},
    {"id":"torso","max":1},
    {"id":"legs","max":1},
    {"id":"feet","max":1},
    {"id":"hands","max":2},
    {"id":"back","max":1},
    {"id":"belt","max":1}
  ],
  "carryCapacityKg": 40,          // baseline; species may scale
  "naturalAttacks": [             // optional simple block; advanced combat may replace
    {"id":"punch","damageType":"blunt","dice":"1d2","apCost":50}
  ],
  "anatomy": {                    // optional: minimal graph (future: wounds)
    "nodes": ["head","torso","left_arm","right_arm","left_leg","right_leg"],
    "edges": [["torso","head"],["torso","left_arm"],["torso","right_arm"],
              ["torso","left_leg"],["torso","right_leg"]]
  }
}


Notes

equipSlots are identifiers used by Items to declare compatible slots.

Keep simple; detailed hit locations/wounds can plug into anatomy later without breaking IDs.

2) Registry: CreatureDef (species)

Describes a species or template. Instances reference this by species_id.

{
  "id": "cre_dwarf",
  "displayName": "Dwarf",
  "tags": ["civilized","miner","underground"],
  "bodyPlanId": "humanoid_simple",

  "stats": {                      // baseline; integer 1..20 typical
    "str": 10, "agi": 8, "end": 11, "int": 8, "per": 8, "cha": 6
  },

  "senses": {                     // tiles or booleans; keep minimal
    "visionTiles": 20,
    "darkvisionTiles": 6,
    "hearing": "normal"
  },

  "gaits": {                      // movement presets; costs are tiles/tick or AP per step
    "walk": {"tilesPerTick": 1.0, "apCost": 100},
    "run":  {"tilesPerTick": 1.8, "apCost": 100}
  },

  "movementCaps": {               // shape/terrain capabilities (aligns with NAVIGATION_SPEC)
    "canWalk": true,
    "canSwim": false,
    "canFly": false,
    "canClimb": false            // reserved capability; not yet implemented
  },

  "resistances": {                // coarse damage/environment modifiers (optional)
    "blunt": 1.0, "slash": 1.0, "pierce": 1.0, "heat": 1.0, "cold": 1.0, "poison": 1.0
  },

  "ai": {                         // simple hooks (actual behavior in code/BT)
    "aggroRange": 8,
    "courage": "normal",          // cowardly/normal/brave
    "fleeThreshold": 0.15         // HP fraction to flee
  },

  "factionDefaults": {            // default alignment on spawn (can be overridden)
    "factionId": "fac_dwarves",
    "hostilityTags": ["hostile_goblins"]
  },

  "skills": {                     // coarse professional aptitudes; optional
    "mining": 2, "woodcutting": 1, "masonry": 2
  },

  "traits": ["hardy","craft_pride"], // optional; if traits system disabled, ignored

  "needs": {                      // placeholder; disabled by default
    "hunger": false, "rest": false, "recreation": false
  }
}


Notes

canClimb exists for future; engine currently treats it as false (no climb logic).

Keep values coarse and data-first. No per-frame logic in defs.

3) Registry: Traits (optional)
{
  "id": "hardy",
  "displayName": "Hardy",
  "description": "+10% disease resistance; -10% need for rest.",
  "modifiers": {"end": +1, "diseaseResist": 1.1}
}


Traits are small, additive, and deterministic.

If the trait system is disabled, loader accepts but runtime ignores them.

4) Runtime Instance (save-layer)

What gets created for an individual creature at runtime.

{
  "guid": "c_7f1a…",             // stable per save
  "speciesId": "cre_dwarf",
  "bodyPlanId": "humanoid_simple",// resolved snapshot for fast access

  "factionId": "fac_dwarves",     // required runtime ownership/allegiance
  "controller": "ai",             // ai | player | neutral

  "pos": {"chunk":"(cx,cy,cz)", "x":12, "y":34, "z":5},

  "hp": {"current": 60, "max": 60},          // coarse; not per-limb yet
  "stamina": 100, "pain": 0, "morale": 0,    // simple bars (your current placeholders)
  "statusEffects": [],                       // poison/bleed/etc., optional

  "inventory": {"capacityKg": 45, "items": []},
  "equipment": { "head": null, "torso": null, "legs": null, "feet": null,
                 "hands": [], "back": null, "belt": null },

  "skills": {"mining": 2, "woodcutting": 1}, // snapshot with growth
  "traits": ["hardy"],                        // resolved at spawn; immutable or rare

  "jobHandle": null,                          // link to scheduler
  "navCaps": { "canWalk": true, "canSwim": false, "canFly": false, "canClimb": false },

  "contentVersion": "1.1.0",                  // for save migration
  "aliasesApplied": ["cre_dwarf@1.0→cre_dwarf"]
}


Rules

Instance carries only mutable/runtime fields. All static data remains in the registry.

factionId is required; changes trigger cache invalidation where relevant (hostility grids, UI).

5) Determinism & Validation

Any procedural variance (e.g., starting traits) must use seed = worldSeed ⊕ speciesId ⊕ guid.

Validation on load:

IDs resolve; body plan exists; equip slots are consistent.

movementCaps must align with engine capabilities; unknown flags are ignored with warnings.

6) Testing Checklist

Spawn/serialize/deserialize round-trip equals (registry IDs and instance-only fields preserved).

Equipment validation respects body plan slots.

Faction hostility routing works (AI target selection changes when faction changes).

Disabling traits/needs leaves runtime behavior unchanged.

7) Backward Compatibility

aliases.json supports { "from": "cre_dwarf@<=1.0", "to": "cre_dwarf" }.

Instance carries a snapshot of resolved IDs in save headers; loader remaps via aliases when needed.

Appendix: Minimal Examples

creatures.body_plans.json

[
  { "id": "humanoid_simple", "displayName": "Humanoid (Simple)",
    "sizeClass":"M", "baseMassKg":70,
    "equipSlots":[{"id":"head","max":1},{"id":"torso","max":1},{"id":"legs","max":1},{"id":"feet","max":1},{"id":"hands","max":2},{"id":"back","max":1},{"id":"belt","max":1}],
    "carryCapacityKg":40,
    "naturalAttacks":[{"id":"punch","damageType":"blunt","dice":"1d2","apCost":50}]
  }
]


creatures.json

[
  { "id":"cre_dwarf","displayName":"Dwarf","tags":["civilized","miner"],
    "bodyPlanId":"humanoid_simple",
    "stats":{"str":10,"agi":8,"end":11,"int":8,"per":8,"cha":6},
    "senses":{"visionTiles":20,"darkvisionTiles":6},
    "gaits":{"walk":{"tilesPerTick":1.0,"apCost":100},"run":{"tilesPerTick":1.8,"apCost":100}},
    "movementCaps":{"canWalk":true,"canSwim":false,"canFly":false,"canClimb":false},
    "factionDefaults":{"factionId":"fac_dwarves"},
    "skills":{"mining":2,"masonry":1},
    "traits":["hardy"],
    "needs":{"hunger":false,"rest":false,"recreation":false}
  }
]

Notes on what changed vs prior version

Added traits (optional) and needs (placeholders, default off).

Clarified movementCaps with climb reserved.

Formalized body plan slots and simple natural attacks.

Required factionId at runtime (instance).

Added determinism/validation/back-compat sections.

CREATURE_SPEC.md (v1.2)

Scope. Data-driven creature content & minimal runtime/save model.
Principles. Deterministic, mod-friendly, clear separation of registry (truth) vs instance (runtime), small invariants.

0) Files & Loading
content/
└─ registries/
   ├─ creatures.body_plans.json      # Parts tree, joints, functional roles, equip slots
   ├─ creatures.json                 # Species templates (attributes/skills/traits/values/appearance)
   ├─ traits.json                    # Personality/traits (optional)
   ├─ values.axes.json               # Cultural/values axes (optional)
   ├─ genes.loci.json                # Gene/allele definitions (optional; inheritance rules)
   ├─ aliases.json                   # Name/ID migration (optional)
   └─ schemas/                       # JSON Schemas (Draft 07)


Load order. traits → values.axes → genes.loci → body_plans → creatures → apply aliases.
Lookup. All by stable IDs; authoring names are resolved at load.
Compatibility. All new blocks are optional; missing blocks are treated as “no effect”.

1) Registry: BodyPlan (hierarchy + joints + coverage)
1.1 Humanoid example (ID per your convention)
{
  "id": "core_creature_humanoid",
  "displayName": "Humanoid (Detailed)",
  "sizeClass": "M",
  "baseMassKg": 70,

  "parts": [
    { "id":"head", "parent":null, "hitWeight":0.10, "vital":true,
      "functional":["see","hear","smell","speak"],
      "children":[
        {"id":"eye_l","hitWeight":0.01,"functional":["see"]},
        {"id":"eye_r","hitWeight":0.01,"functional":["see"]},
        {"id":"ear_l","hitWeight":0.01,"functional":["hear"]},
        {"id":"ear_r","hitWeight":0.01,"functional":["hear"]},
        {"id":"nose", "hitWeight":0.01,"functional":["smell"]},
        {"id":"mouth","hitWeight":0.01,"functional":["speak","bite"]}
      ]
    },
    { "id":"neck","parent":null,"hitWeight":0.03,"vital":true },

    { "id":"shoulders","parent":null,"hitWeight":0.04 },
    { "id":"chest","parent":null,"hitWeight":0.18,"vital":true, "organs":["heart","lungs"]},
    { "id":"abdomen","parent":null,"hitWeight":0.12,"organs":["stomach","liver","intestines"]},
    { "id":"pelvis","parent":null,"hitWeight":0.08,"organs":["genitals"]},

    { "id":"upper_arm_l","parent":"shoulders","hitWeight":0.04 },
    { "id":"forearm_l",  "parent":"upper_arm_l","hitWeight":0.03 },
    { "id":"hand_l",     "parent":"forearm_l","hitWeight":0.02, "functional":["grasp"] },

    { "id":"upper_arm_r","parent":"shoulders","hitWeight":0.04 },
    { "id":"forearm_r",  "parent":"upper_arm_r","hitWeight":0.03 },
    { "id":"hand_r",     "parent":"forearm_r","hitWeight":0.02, "functional":["grasp"] },

    { "id":"thigh_l","parent":"pelvis","hitWeight":0.06 },
    { "id":"shin_l", "parent":"thigh_l","hitWeight":0.05 },
    { "id":"foot_l", "parent":"shin_l","hitWeight":0.04, "functional":["stand","move"] },

    { "id":"thigh_r","parent":"pelvis","hitWeight":0.06 },
    { "id":"shin_r", "parent":"thigh_r","hitWeight":0.05 },
    { "id":"foot_r", "parent":"shin_r","hitWeight":0.04, "functional":["stand","move"] }
  ],

  "joints": [
    ["neck","head"],["shoulders","upper_arm_l"],["upper_arm_l","forearm_l"],["forearm_l","hand_l"],
    ["shoulders","upper_arm_r"],["upper_arm_r","forearm_r"],["forearm_r","hand_r"],
    ["pelvis","thigh_l"],["thigh_l","shin_l"],["shin_l","foot_l"],
    ["pelvis","thigh_r"],["thigh_r","shin_r"],["shin_r","foot_r"],
    ["shoulders","chest"],["chest","abdomen"],["abdomen","pelvis"],["neck","chest"]
  ],

  "equipSlots": [
    {"slotId":"head","max":1,"covers":["head"]},
    {"slotId":"neck","max":1,"covers":["neck"]},
    {"slotId":"shoulders","max":1,"covers":["shoulders"]},
    {"slotId":"torso","max":1,"covers":["chest","abdomen","pelvis"]},
    {"slotId":"arms","max":1,"covers":["upper_arm_l","forearm_l","upper_arm_r","forearm_r"]},   // arms slot added
    {"slotId":"hands","max":2,"covers":["hand_l","hand_r"]},
    {"slotId":"legs","max":1,"covers":["thigh_l","shin_l","thigh_r","shin_r"]},
    {"slotId":"feet","max":1,"covers":["foot_l","foot_r"]},
    {"slotId":"back","max":1,"covers":["chest"]},
    {"slotId":"belt","max":1,"covers":["pelvis"]},
    {"slotId":"rings","max":10,"covers":[]},     // optional jewelry
    {"slotId":"amulet","max":1,"covers":[]}
  ],

  "carryCapacityKg": 40,
  "naturalAttacks": [{"id":"punch","damageType":"blunt","dice":"1d2","apCost":50}]
}


Notes

arms covers upper/forearms (bracers/armguards). hands remains for gloves and grasp.

rings/amulet are optional (good hooks for culture/values later).

No tissue layer math yet; this body graph can be extended later without breaking IDs.

Quadruped template
Provide core_creature_quadruped similarly: fore/hind limbs + tail (balance role). Not expanded here.

2) Registry: Species (attributes/skills/personality/values/appearance)
{
  "id": "cre_dwarf",
  "displayName": "Dwarf",
  "bodyPlanId": "core_creature_humanoid",
  "sapience": "sapient",                    // sapient | semi_sapient | animal | construct

  "attributes": {                           // 8 primary stats
    "STR": 10, "AGI": 8, "END": 11, "PER": 8, "INT": 8, "WIL": 10, "CHA": 6, "SPI": 7
  },

  "speciesAverages": {                      // DF-style species baseline; 1000 = human reference
    "baseline": 1000,
    "mean":  { "STR":1100, "AGI":800, "END":1150, "PER":900, "INT":950, "WIL":1100, "CHA":800, "SPI":900 },
    "stdev": { "STR":120,  "AGI":150, "END":120,  "PER":120, "INT":120, "WIL":120,  "CHA":150, "SPI":120 }
  },

  "skills": {                               // expanded skill set (see full list below)
    "Mining": 2, "Melee": 2, "Construction": 1, "Smithing": 1, "Logistics": 1
  },

  "personality": {                          // facets −2..+2 (see list below)
    "Industriousness": +2,
    "Bravery": +1,
    "Temperance": 0,
    "Curiosity": -1
  },

  "values": {                               // value axes −2..+2 (sapients only)
    "Authority": +1, "Tradition": +2, "Collectivism": +1, "Industry": +2,
    "Purity": 0, "Spiritual": 0, "Honor": +1, "Militarism": +1
  },

  "appearance": {                           // phenotype for future rendering & flavor
    "heightCm": { "mean": 135, "stdev": 6 },
    "massKg":   { "mean": 65,  "stdev": 8 },
    "skinPalette": ["#c69a78","#a67c52"],
    "hairPalette": ["#3b2b20","#6b4e2e","#b38b5a"],
    "eyePalette":  ["#3a3a3a","#2e4a6b"],
    "beardChance": 0.85,
    "styleTags": ["dwarven_craft","sturdy"],
    "portraitParams": { "browDepth":0.2, "noseWidth":0.6, "jaw":0.8 }
  },

  "factionDefaults": { "factionId": "fac_dwarves" },

  "genetics": {                             // optional: simple heredity model
    "loci": [
      { "id":"HEIGHT", "type":"polygenic", "heritability":0.6, "mutRate":0.0001 },
      { "id":"HAIR_COLOR", "type":"codominant", "alleles":["dark","brown","blond"], "heritability":0.9 },
      { "id":"EYE_COLOR",  "type":"codominant", "alleles":["dark","blue","green"],  "heritability":0.8 },
      { "id":"BEARD_DENS", "type":"additive",   "heritability":0.7 }
    ]
  }
}


Notes

speciesAverages: use mean/stdev around the human 1000 reference; when spawning, sample and map to your internal 1..20 (or other) scales.

appearance: even if not rendered yet, it’s saveable and can drive events/preferences.

genetics: lightweight placeholder; breeding combines parental alleles/traits; heritability tunes gene vs environment.

3) Attributes / Skills / Personality / Values (expanded sets)
3.1 Primary attributes (8)

STR, AGI, END, PER, INT, WIL, CHA, SPI
Derived stats (HP, stamina, move AP, aim/hit/dodge, carry, work speed, learn rate…) come from attributes + skills + equipment.

3.2 Skills (≈20; merged from DF/RimWorld/CDDA)

Combat

Melee, Ranged, Shields, Polearms (optional), Dodge, Riding (optional)

Production & Engineering

Mining, Construction, Masonry, Carpentry, Smithing, Tailoring, Stonecraft, Woodcraft, Pottery, Glassworking, Chemistry/Alchemy (optional), Engineering (traps/mechanisms)

Livelihood & Logistics

Cooking, Brewing, Farming, Herbalism/Foraging, AnimalHandling, Medicine, Logistics (hauling/organization)

Social & Academic

Social, Artistic, Intellectual, Magic (optional; pairs well with SPI)

Detailed recipes/weapon families stay in recipe tags to avoid exploding the skill tree.

3.3 Personality facets (−2..+2; suggest 14–18)

Industriousness, Bravery, Composure, Altruism, Curiosity, Cautiousness, Temperance, Cheerfulness, RiskTaking, Greed, Honesty, Empathy, Pride, Vengefulness, Loyalty, Ambition, Pacifism/Aggression (pick one axis)

3.4 Value axes (−2..+2; sapients only)

Authority↔Liberty, Tradition↔Progress, Collectivism↔Individualism, Ascetic↔Hedonist, Purity↔Tolerance, Nature↔Industry, Spiritual↔Pragmatic, Honor↔Cunning, Egalitarian↔Hierarchy, Pacifism↔Militarism

Non-sapients: no values; keep a small subset of personality (bravery/aggression/tamability…).

4) Runtime Instance (save layer)
{
  "guid": "c_7f1a…",
  "speciesId": "cre_dwarf",
  "bodyPlanId": "core_creature_humanoid",
  "sapience": "sapient",

  "factionId": "fac_dwarves",
  "controller": "ai",

  "pos": {"chunk":"(cx,cy,cz)", "x":12,"y":34,"z":5},

  "attributes": {"STR":11,"AGI":8,"END":12,"PER":9,"INT":8,"WIL":11,"CHA":6,"SPI":8},
  "skills": {"Mining":2,"Melee":2},                // instance snapshot; grows over time
  "personality": {"Industriousness":2,"Bravery":1},
  "values": {"Tradition":2,"Industry":2},          // omit for non-sapients

  "appearance": {
    "heightCm": 136, "massKg": 66,
    "skin":"#a67c52","hair":"#6b4e2e","eyes":"#3a3a3a",
    "portraitParams": {"browDepth":0.22,"jaw":0.78}
  },

  "genome": {                                      // optional compact DNA record
    "seed": "G:abcd…", "loci":[["HEIGHT","…"],["HAIR_COLOR","…"]]
  },

  "hp":{"current":60,"max":60},
  "stamina":100,"pain":0,"morale":0,

  "inventory":{"capacityKg":45,"items":[]},
  "equipment":{"head":null,"neck":null,"shoulders":null,"torso":null,"arms":null,"hands":[],
               "legs":null,"feet":null,"back":null,"belt":null,"rings":[], "amulet":null},

  "jobHandle": null,
  "navCaps":{"canWalk":true,"canSwim":false,"canFly":false,"canClimb":false},

  "contentVersion":"1.2.0","aliasesApplied":[]
}


Rules

Keep a single HP/Stamina bar for MVP; later connect wounds to parts.

appearance/genome are persisted even if unrevealed in rendering yet.

Random generation uses seed = worldSeed ⊕ speciesId ⊕ guid.

5) Determinism & Validation

All randomness (appearance/genes/facets/values) uses stable seeds; cross-platform deterministic.

Validate: equipSlots.covers[] must reference valid parts; unknown skills/facets/values log warnings but load.

Non-sapients: if values present, loader ignores and warns (keeps data coherent).

6) Testing Checklist

Hit distribution roughly matches hitWeight; (future) armor coverage reduces exposure.

Attributes sampled from speciesAverages produce expected mean/stdev after mapping.

Genetics: child distributions skew toward parents when heritability > 0.

Serialization: appearance/genes/facets/values round-trip intact; disabling values/genes does not break loading.

7) Optional / Future (does not affect compatibility)

Tissue layers & organ lethality (heart/lungs/brain…)

Coverage/penetration and multi-layer armor

Faction culture/ideology binding to values

Titles/lineage/social graphs (CK/M&B flavor)

Cybernetics/mutations (Qud/CDDA flavor) as traits/genes extensions

Appendices (for implementation)

Full skill set (suggested)
Melee, Ranged, Shields, Polearms, Dodge, Riding,
Mining, Construction, Masonry, Carpentry, Smithing, Tailoring, Stonecraft, Woodcraft, Pottery, Glassworking, Chemistry/Alchemy, Engineering,
Cooking, Brewing, Farming, Herbalism, AnimalHandling, Medicine, Logistics,
Social, Artistic, Intellectual, Magic

Personality facets (suggested)
Industriousness, Bravery, Composure, Altruism, Curiosity, Cautiousness, Temperance, Cheerfulness, RiskTaking, Greed, Honesty, Empathy, Pride, Vengefulness, Loyalty, Ambition, Pacifism/Aggression (pick one axis)

Value axes (suggested)
Authority↔Liberty, Tradition↔Progress, Collectivism↔Individualism, Ascetic↔Hedonist, Purity↔Tolerance, Nature↔Industry, Spiritual↔Pragmatic, Honor↔Cunning, Egalitarian↔Hierarchy, Pacifism↔Militarism

Open questions (please confirm)

Lethality rules: For MVP, ok to treat head/neck/chest as high-risk multipliers and add organ-level kills later?

Armor coverage precision: Short-term equipSlots.covers[]; do we plan multi-layer coverage/exposure/penetration later?

Genetics scope: Start with genome.seed + a few visible loci (height/hair/eye/beard), defer complex alleles?

Culture & style: Should styleTags tie to faction/values to influence default apparel/jewelry/building aesthetics?

If this looks good, I can also provide tiny example files for creatures.body_plans.json and creatures.json that validate against Draft-07 schemas and match this spec, so Codex can wire them up quickly.