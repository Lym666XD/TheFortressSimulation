BUILDABLE_SPEC.md — v1 (Normative, Unified Furniture/Workshop/Defense)
id: buildable.v1
status: normative
owner: content/registry
last_updated: 2025-09-14
depends_on:
  - material.v3
  - item.v3
  - CHUNK_ACTOR_PROTOCOL.md
  - DIFF_LOG_AND_MERGE_STRATEGIES.md
  - UPDATE_ORDER.md
notes:
  - “Buildable” is the single base type for furniture, workshops, utilities, and defenses.
  - Stockpiles remain an Area system (non-entity). Buildables with Storage extend area capacity.

0) Scope & Goals

One data-driven definition covers furniture, workshops, utilities, and defenses via components.

Unified placement/rotation/footprint/occlusion rules; deterministic and multithread-friendly.

Behavior state lives in the relevant systems (power, jobs, networks). Buildables store configuration + minimal local toggles.

Strict validation; try–catch on load and during simulation stages; invalid content never crashes the game.

1) Core Principles (Normative)

Single source of truth: geometry (footprint/placement/blocks) is authoritative for pathing, lighting, fluids.

IDs over enums: materials, items, recipes, effects referenced by string IDs; mapped to stable numeric handles at runtime.

Components = capabilities: storage, workshop, power, defense, door, heater, cooler, etc.

Stockpiles are Areas: areas own filters/priority; storage buildables inherit area filters when placed inside.

Determinism & concurrency: all writes via diff-log; cross-chunk via actor messages; stable ordering keys.

2) Top-Level Fields (Normative)

id:string — unique, lowercase snake_case.

tags:string[] — categories for filters/recipes/AI.

name?:string, desc?:string — display-only.

footprint — size/mask/pivot for placement & rotation.

placement — anchor (floor/wall/ceiling), allowed/forbidden surfaces, adjacency rules.

rotate — allowed angles; optional mirroring.

blocks — movement/light/fluid blocking and cover%.

material — fixed or weighted materials (ties to material.v3).

durability — hit points and optional armor bonus vs damage types.

beauty?:int, room_stats?:string[] — optional room scoring hooks.

io_ports?:[] — power/fluid/link ports and rates.

render?:{} — sprite/palette/z offset (renderer snapshot reads this).

components?:{} — capability-specific configuration (see §4).

Determinism & Safety

Content load is wrapped in try–catch; bad entries are skipped with structured logs {file,id,reason}.

Runtime component updates are diffed and merged in the buildables stage; no shared writes.

3) Footprint, Placement, Rotation, Blocking (Normative)

Footprint

w,h (axis-aligned size).

Optional cells list to carve custom shapes inside the bounding box.

Optional pivot for rotation center (default 0,0).

Placement

anchor: "floor"|"wall"|"ceiling".

allowed_on / forbid_on surface tags; needs_support_strength:int.

Optional adjacency allow/forbid tags for snap/connect rules.

Rotation

allowed:[0,90,180,270] subset; optional mirror_x/mirror_y.

Blocks

movement/light/fluid booleans; cover_pct (0–100) for ranged cover.

nav_cost_delta to tweak local pathing cost.

4) Components (Normative)

Components are optional; include only what you need.

Storage

slots:int, capacity_ml:number

accept_item_tags[], none_item_tags[]

accept_material_tags[], none_material_tags[]

Inherits stockpile-area selectors when the buildable sits inside an area.

Workshop

recipe_domain:string (filters visible recipes)

attachment_slots:string[] (optional module points)

requires_enablers:string[] (power, ventilation, etc.)

workspots:[[x,y]...] worker stand cells (relative to pivot)

io_cells:{inputs:[[x,y]...], outputs:[[x,y]...]} for item IO tiles

Power

consumes_w:number, produces_w:number, battery_wh:number, standby_w:number

Electrical topology lives in the PowerNet system (not here).

Defense

kind:"trap"|"turret"|"barricade"

weapon_item_id?:string (or internal weapon table if you prefer)

range_tiles:int, arc_deg:int(0..360), trigger:"step"|"proximity"|"projectile"|"manual"

friendly_fire:boolean

Door

openable:boolean, auto_close_s:number, lockable:boolean

open_cost_ap:int, close_cost_ap:int

Minimal door open/closed toggle may be stored locally; AI & pathing are system-resolved.

Heater/Cooler

heat_w:number / cool_w:number (thermal field systems interpret these).

5) Integration & Persistence (Normative)

Systems own behavior state (Job Scheduler, PowerNet, FluidNet). Buildables store configuration + minimal toggles.

Serialization:

Buildable instance: {id, transform, rotation, material selection, minimal component toggles}.

System saves: {jobs, progress, reservations, network links, energy buffers} keyed by instance GUIDs.

On load, handles are rebuilt from content IDs; systems rebind by GUID; missing references fall back to safe states.

6) Update Order & Concurrency (Normative)

Stage name: BuildablesStep (after terrain, before fluids if needed by IO).

Reads world + area snapshots; plans diffs/messages; merges locally in a deterministic order (chunk → cell → component type → systemId).

Cross-chunk interactions via Chunk Actor protocol; stable mailbox ordering (tick → senderChunkId → localSeq).

7) JSON Schema — buildable.v1.schema.json (no comments)
{
  "$id": "buildable.v1.schema.json",
  "type": "object",
  "required": ["id","tags","footprint","placement","rotate","blocks","material","durability"],
  "properties": {
    "id": { "type": "string" },
    "tags": { "type": "array", "items": { "type": "string" } },
    "name": { "type": "string" },
    "desc": { "type": "string" },
    "footprint": {
      "type": "object",
      "required": ["w","h"],
      "properties": {
        "w": { "type": "integer", "minimum": 1 },
        "h": { "type": "integer", "minimum": 1 },
        "cells": { "type": "array", "items": { "type": "array", "items": { "type": "integer" }, "minItems": 2, "maxItems": 2 } },
        "pivot": { "type": "array", "items": { "type": "integer" }, "minItems": 2, "maxItems": 2 }
      },
      "additionalProperties": false
    },
    "placement": {
      "type": "object",
      "required": ["anchor","allowed_on","forbid_on"],
      "properties": {
        "anchor": { "type": "string", "enum": ["floor","wall","ceiling"] },
        "allowed_on": { "type": "array", "items": { "type": "string" } },
        "forbid_on": { "type": "array", "items": { "type": "string" } },
        "needs_support_strength": { "type": "integer", "minimum": 0 },
        "adjacency_require": { "type": "array", "items": { "type": "string" } },
        "adjacency_forbid": { "type": "array", "items": { "type": "string" } }
      },
      "additionalProperties": false
    },
    "rotate": {
      "type": "object",
      "required": ["allowed"],
      "properties": {
        "allowed": { "type": "array", "items": { "type": "integer" } },
        "mirror_x": { "type": "boolean" },
        "mirror_y": { "type": "boolean" }
      },
      "additionalProperties": false
    },
    "blocks": {
      "type": "object",
      "required": ["movement","light","fluid"],
      "properties": {
        "movement": { "type": "boolean" },
        "light": { "type": "boolean" },
        "fluid": { "type": "boolean" },
        "cover_pct": { "type": "integer", "minimum": 0, "maximum": 100 },
        "nav_cost_delta": { "type": "integer" }
      },
      "additionalProperties": false
    },
    "material": {
      "type": "object",
      "oneOf": [
        { "required": ["fixed_material"] },
        { "required": ["material_weights"] }
      ],
      "properties": {
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
        "allowed_material_tags": { "type": "array", "items": { "type": "string" } },
        "forbid_material_tags": { "type": "array", "items": { "type": "string" } }
      },
      "additionalProperties": false
    },
    "durability": {
      "type": "object",
      "required": ["hp_max"],
      "properties": {
        "hp_max": { "type": "integer", "minimum": 1 },
        "armor_bonus": {
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
        }
      },
      "additionalProperties": false
    },
    "beauty": { "type": "integer" },
    "room_stats": { "type": "array", "items": { "type": "string" } },
    "io_ports": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["type","dir"],
        "properties": {
          "type": { "type": "string", "enum": ["power","fluid","link"] },
          "dir": { "type": "string", "enum": ["N","E","S","W","U","D"] },
          "kind_tags": { "type": "array", "items": { "type": "string" } },
          "rate": { "type": "number", "minimum": 0 }
        },
        "additionalProperties": false
      }
    },
    "render": {
      "type": "object",
      "properties": {
        "sprite": { "type": "string" },
        "palette": { "type": "string" },
        "z_offset": { "type": "integer" }
      },
      "additionalProperties": false
    },
    "components": {
      "type": "object",
      "properties": {
        "storage": {
          "type": "object",
          "properties": {
            "slots": { "type": "integer", "minimum": 0 },
            "capacity_ml": { "type": "number", "minimum": 0 },
            "accept_item_tags": { "type": "array", "items": { "type": "string" } },
            "none_item_tags": { "type": "array", "items": { "type": "string" } },
            "accept_material_tags": { "type": "array", "items": { "type": "string" } },
            "none_material_tags": { "type": "array", "items": { "type": "string" } }
          },
          "additionalProperties": false
        },
        "workshop": {
          "type": "object",
          "properties": {
            "recipe_domain": { "type": "string" },
            "attachment_slots": { "type": "array", "items": { "type": "string" } },
            "requires_enablers": { "type": "array", "items": { "type": "string" } },
            "workspots": { "type": "array", "items": { "type": "array", "items": { "type": "integer" }, "minItems": 2, "maxItems": 2 } },
            "io_cells": {
              "type": "object",
              "properties": {
                "inputs": { "type": "array", "items": { "type": "array", "items": { "type": "integer" }, "minItems": 2, "maxItems": 2 } },
                "outputs": { "type": "array", "items": { "type": "array", "items": { "type": "integer" }, "minItems": 2, "maxItems": 2 } }
              },
              "additionalProperties": false
            }
          },
          "additionalProperties": false
        },
        "power": {
          "type": "object",
          "properties": {
            "consumes_w": { "type": "number", "minimum": 0 },
            "produces_w": { "type": "number", "minimum": 0 },
            "battery_wh": { "type": "number", "minimum": 0 },
            "standby_w": { "type": "number", "minimum": 0 }
          },
          "additionalProperties": false
        },
        "defense": {
          "type": "object",
          "properties": {
            "kind": { "type": "string", "enum": ["trap","turret","barricade"] },
            "weapon_item_id": { "type": "string" },
            "range_tiles": { "type": "integer", "minimum": 0 },
            "arc_deg": { "type": "integer", "minimum": 0, "maximum": 360 },
            "trigger": { "type": "string", "enum": ["step","proximity","projectile","manual"] },
            "friendly_fire": { "type": "boolean" }
          },
          "additionalProperties": false
        },
        "door": {
          "type": "object",
          "properties": {
            "openable": { "type": "boolean" },
            "auto_close_s": { "type": "number", "minimum": 0 },
            "lockable": { "type": "boolean" },
            "open_cost_ap": { "type": "integer", "minimum": 0 },
            "close_cost_ap": { "type": "integer", "minimum": 0 }
          },
          "additionalProperties": false
        },
        "heater": {
          "type": "object",
          "properties": {
            "heat_w": { "type": "number", "minimum": 0 }
          },
          "additionalProperties": false
        },
        "cooler": {
          "type": "object",
          "properties": {
            "cool_w": { "type": "number", "minimum": 0 }
          },
          "additionalProperties": false
        }
      },
      "additionalProperties": false
    }
  },
  "additionalProperties": false
}

8) Minimal Examples (no comments)
A) Storage Furniture — Wooden Shelf
{
  "id": "build_shelf_wood",
  "tags": ["furniture","storage","wood"],
  "name": "Wooden Shelf",
  "footprint": { "w": 1, "h": 1 },
  "placement": { "anchor": "floor", "allowed_on": ["floor"], "forbid_on": ["water"], "needs_support_strength": 1, "adjacency_require": [], "adjacency_forbid": [] },
  "rotate": { "allowed": [0,90,180,270] },
  "blocks": { "movement": false, "light": false, "fluid": false, "cover_pct": 10, "nav_cost_delta": 0 },
  "material": { "fixed_material": "core_mat_wood_oak" },
  "durability": { "hp_max": 80 },
  "beauty": 5,
  "components": {
    "storage": {
      "slots": 12,
      "capacity_ml": 0,
      "accept_item_tags": ["book","scroll"],
      "none_item_tags": ["rotten"],
      "accept_material_tags": [],
      "none_material_tags": []
    }
  }
}

B) Workshop — Smeltery (with power and fluid IO)
{
  "id": "build_workshop_smeltery",
  "tags": ["workshop","metallurgy","furnace"],
  "name": "Smeltery",
  "footprint": { "w": 3, "h": 2 },
  "placement": { "anchor": "floor", "allowed_on": ["floor","stone"], "forbid_on": ["water"], "needs_support_strength": 2, "adjacency_require": [], "adjacency_forbid": [] },
  "rotate": { "allowed": [0,90,180,270] },
  "blocks": { "movement": true, "light": false, "fluid": false, "cover_pct": 40, "nav_cost_delta": 5 },
  "material": { "fixed_material": "core_mat_stone_granite" },
  "durability": { "hp_max": 600 },
  "io_ports": [
    { "type": "power", "dir": "W", "rate": 2000 },
    { "type": "fluid", "dir": "E", "kind_tags": ["waterlike"], "rate": 500 }
  ],
  "components": {
    "power": { "consumes_w": 1500, "produces_w": 0, "battery_wh": 0, "standby_w": 50 },
    "workshop": {
      "recipe_domain": "metallurgy",
      "attachment_slots": ["hearth","roaster","still"],
      "requires_enablers": [],
      "workspots": [[1,-1]],
      "io_cells": { "inputs": [[0,0],[1,0]], "outputs": [[2,1]] }
    }
  }
}

C) Defense — Wooden Barricade
{
  "id": "build_barricade_wood",
  "tags": ["defense","barricade","wood"],
  "name": "Wooden Barricade",
  "footprint": { "w": 1, "h": 1 },
  "placement": { "anchor": "floor", "allowed_on": ["floor","soil"], "forbid_on": ["water"], "needs_support_strength": 1, "adjacency_require": [], "adjacency_forbid": [] },
  "rotate": { "allowed": [0,90,180,270] },
  "blocks": { "movement": true, "light": false, "fluid": true, "cover_pct": 70, "nav_cost_delta": 15 },
  "material": { "fixed_material": "core_mat_wood_oak" },
  "durability": { "hp_max": 250 },
  "components": {
    "defense": { "kind": "barricade", "friendly_fire": false }
  }
}

D) Defense — Ballista Turret
{
  "id": "build_turret_ballista",
  "tags": ["defense","turret","siege"],
  "name": "Ballista Turret",
  "footprint": { "w": 2, "h": 2 },
  "placement": { "anchor": "floor", "allowed_on": ["floor","stone"], "forbid_on": ["water"], "needs_support_strength": 2, "adjacency_require": [], "adjacency_forbid": [] },
  "rotate": { "allowed": [0,90,180,270] },
  "blocks": { "movement": true, "light": false, "fluid": false, "cover_pct": 50, "nav_cost_delta": 10 },
  "material": { "material_weights": [ { "id": "core_mat_wood_oak", "weight": 0.6 }, { "id": "core_mat_metal_steel", "weight": 0.4 } ] },
  "durability": { "hp_max": 500 },
  "components": {
    "defense": {
      "kind": "turret",
      "weapon_item_id": "core_item_weapon_ballista",
      "range_tiles": 18,
      "arc_deg": 120,
      "trigger": "proximity",
      "friendly_fire": false
    },
    "power": { "consumes_w": 0, "produces_w": 0, "battery_wh": 0, "standby_w": 0 }
  }
}

9) Notes on Stockpiles (Informative)

Stockpiles are areas with filters and priority; they do not store items themselves.

Storage buildables inside an area inherit area filters (and may add stricter local filters).

Capacity = floor stack caps (from tuning) + sum of storage buildables’ capacities in the area.

10) CI Gates (Normative)

Schema validation must pass for all buildables.

Geometry/rotation consistency tests for footprint masks.

Deterministic merge tests under thread jitter for component updates.

Save/load rebind tests for Job Scheduler and PowerNet links.