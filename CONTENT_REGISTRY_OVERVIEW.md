id: content.registry.v1
status: normative
owner: content/infra
last_updated: 2025-09-14
depends_on:
  - material.v3
  - item.v3
  - buildable.v1
  - fluids.v1
notes:
  - Goal: ban hard-coded gameplay constants. Everything loads via JSON Schemas and string IDs, layered base→DLC→mods.
0) Scope & Guarantees
Layered loading: base → dlc[*] → mods[*] (deterministic order).

Deterministic overrides by string id with tombstone support ("_delete": true).

Strict validation: all content validated against JSON Schemas; load uses try–catch per file; invalid entries are skipped (never crash).

Stable handles: runtime numeric handles are derived deterministically from (kind, packTier, id). Save data must not store raw handles—store string IDs (plus optional pack/namespace) and rebind on load.

Hot-reload (dev) supported: rebuild registries and rebind via IDs; running world remains valid or falls back to safe placeholders.

Back-compat: content packs declare a schema_version; the loader applies known migrations or rejects gracefully.

1) Content Layers & Load Order (Normative)
Order (lowest precedence first):

base/ (engine-shipped)

dlc/ (sorted by DLC id)

mods/ (sorted by modload.txt list; else lexicographic)

Per pack structure:

bash
Copy code
pack.json            # { id, version, schema_version, dependencies[], conflicts[] }
content/registries/  # materials.json, items.json, buildables.json, fluids.json, recipes.json, ...
content/schemas/     # optional pack-local schemas (rare)
assets/              # sprites, atlases, audio, ...
Override rules:

Replace-by-id by default: last writer wins.

Patch-by-id when an object contains "extends":"other_id"; deep-merge with field replace semantics (no arithmetic merges).

Delete-by-id when object includes "_delete": true.

Arrays inside objects are replaced wholesale unless the schema says otherwise (see §3.3).

2) Validation & Loading Pipeline (Normative)
Stages (per pack):

Discover files (*.json), sort by path.

Parse with try–catch; malformed JSON → skip file; record structured error {pack, file, reason}.

Schema resolve: pick schema by filename or embedded $schema; validate.

Defaults: apply schema defaults.

Cross-ref check: verify foreign keys (e.g., fixed_material exists).

Linking: convert string IDs to stable handles (do not persist handles).

Publish: produce immutable registry snapshots for systems.

Determinism keys:

All sets ordered by (packTier asc, id lex asc).

Any hash/snapshot is content-hash of normalized JSON (canonical order, no whitespace).

3) Merge Semantics (Normative)
3.1 Object identity
Identity key: id:string (must be unique within a content kind).

Optional namespace is implicit via pack id; collisions across packs are resolved by layer order.

3.2 Replace vs patch vs delete
Replace: last layer object with same id replaces previous.

Patch: object has "extends": "<id>" → start from resolved parent then field-replace from child.

Delete: "_delete": true in last layer removes the object entirely.

3.3 Array fields
Default: replace array.

If schema declares "merge": "append_unique" (via custom keyword) then append by string key (e.g., tag sets).

If schema declares "merge": "by_id" then merge array elements by their id, using the same replace/patch rules.

(Custom keywords are interpreted by our loader; JSON Schema remains valid.)

4) Runtime Handles & Save-Game Binding (Normative)
4.1 Handle assignment (runtime-only)
For each kind (materials/items/buildables/fluids/recipes/biomes/…), assign handles in this stable order:

group by packTier (0 base, 1 dlc, 2+ mods),

within tier, sort by id lex,

assign handle = ordinal.

Systems use handles for fast access; saves never store handles.

4.2 Save data storage
Persist string IDs for any content reference; on load, resolve to current handles.

If an ID is missing:

Substitute a safe placeholder (missing_item, missing_buildable, missing_material).

Jobs referencing missing recipes enter PausedMissingContent.

Log {save_id, missing_id, kind} once per session.

4.3 Packset signature & hash
Save stores packset_signature = [pack.id@version@schema_version...] and a registry hash.

On load: if signature differs, attempt ID-based rebind and run semantic checks (e.g., removed body slot invalidates worn item → auto-unequip to container).

If rebind fails critically, refuse the save with a clear message (no crash).

5) Hot Reload (Dev Mode)
Re-run pipeline for changed packs → new registry snapshots.

Systems receive SwapSnapshot(kind, new_snapshot) at barriers; they must:

Re-map handles to IDs,

Validate live state (drop/convert illegal states),

Keep determinism (same tick order & seeds).

6) Error Handling (Normative)
Every file load and registry publish wrapped in try–catch.

Error levels: error (skip offending entry), warn (default fallback), info (migration).

All errors include {pack, file, id?, kind?, stage, reason, line?} and are rate-limited.

One bad pack must not prevent others from loading.

7) Recommended ID Policy (Normative)
^[a-z0-9_]+$, English, lowercase.

Reserved prefixes: core_ (engine), dlc_, test_, dev_.

Avoid human-facing localized strings in IDs.

Keep stable IDs across updates to preserve save-game compatibility.

8) What Lives in Registries (Non-exhaustive)
materials.json, items.json, buildables.json, fluids.json, recipes.json, biomes.json, geology.json, caverns.json, tilesets.json, autotiles.json, world.params.json, fluid_lut.json.

Each file validated by a schema in /content/schemas/*.schema.json (see below).

9) CI Gates (Normative)
Schema validation passes (no unknown fields).

Cross-reference integrity (no dangling IDs).

Deterministic snapshot hash across OS/CPU/threads.

Round-trip save/load with pack changes rebinds without crashes (golden saves).

End of file.

/content/schemas — JSON Schemas (no comments)
Notes:

You already have item.v3.schema.json and buildable.v1.schema.json. I include small wrappers that $ref them so your schema directory is consistent.

All schemas below omit comments and defaults unless essential. You can extend later.

world.params.schema.json
json
Copy code
{
  "$id": "world.params.schema.json",
  "type": "object",
  "required": ["id","schema_version","world_seed","fortress_chunk_size","worldgen"],
  "properties": {
    "id": { "type": "string" },
    "schema_version": { "type": "integer", "minimum": 1 },
    "world_seed": { "type": "integer" },
    "fortress_chunk_size": {
      "type": "object",
      "required": ["x","y","z"],
      "properties": {
        "x": { "type": "integer", "enum": [32] },
        "y": { "type": "integer", "enum": [32] },
        "z": { "type": "integer", "minimum": 1 }
      },
      "additionalProperties": false
    },
    "map_sizes": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["chunks_x","chunks_y"],
        "properties": {
          "chunks_x": { "type": "integer", "enum": [1,2,3,4] },
          "chunks_y": { "type": "integer", "enum": [1,2,3,4] }
        },
        "additionalProperties": false
      }
    },
    "worldgen": {
      "type": "object",
      "required": ["width","height","sea_level","temp","rain","drainage"],
      "properties": {
        "width": { "type": "integer", "minimum": 64 },
        "height": { "type": "integer", "minimum": 64 },
        "sea_level": { "type": "integer" },
        "temp": { "type": "object", "properties": { "octaves": { "type": "integer" }, "scale": { "type": "number" } }, "additionalProperties": false },
        "rain": { "type": "object", "properties": { "octaves": { "type": "integer" }, "scale": { "type": "number" } }, "additionalProperties": false },
        "drainage": { "type": "object", "properties": { "octaves": { "type": "integer" }, "scale": { "type": "number" } }, "additionalProperties": false }
      },
      "additionalProperties": false
    }
  },
  "additionalProperties": false
}
biome.schema.json
json
Copy code
{
  "$id": "biome.schema.json",
  "type": "object",
  "required": ["id","tags","climate","tile_weights"],
  "properties": {
    "id": { "type": "string" },
    "tags": { "type": "array", "items": { "type": "string" } },
    "climate": {
      "type": "object",
      "required": ["temp_c","rain_mm","elevation_m"],
      "properties": {
        "temp_c": { "type": "array", "items": { "type": "number" }, "minItems": 2, "maxItems": 2 },
        "rain_mm": { "type": "array", "items": { "type": "number" }, "minItems": 2, "maxItems": 2 },
        "elevation_m": { "type": "array", "items": { "type": "number" }, "minItems": 2, "maxItems": 2 }
      },
      "additionalProperties": false
    },
    "tile_weights": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["tile_id","weight"],
        "properties": {
          "tile_id": { "type": "string" },
          "weight": { "type": "number", "minimum": 0 }
        },
        "additionalProperties": false
      }
    },
    "spawns": {
      "type": "object",
      "properties": {
        "plants": { "type": "array", "items": { "type": "string" } },
        "animals": { "type": "array", "items": { "type": "string" } }
      },
      "additionalProperties": false
    }
  },
  "additionalProperties": false
}
geology.schema.json
json
Copy code
{
  "$id": "geology.schema.json",
  "type": "object",
  "required": ["id","layers"],
  "properties": {
    "id": { "type": "string" },
    "layers": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["depth_from","depth_to","stone_material_id"],
        "properties": {
          "depth_from": { "type": "integer" },
          "depth_to": { "type": "integer" },
          "stone_material_id": { "type": "string" },
          "ore_veins": {
            "type": "array",
            "items": {
              "type": "object",
              "required": ["material_id","chance","cluster"],
              "properties": {
                "material_id": { "type": "string" },
                "chance": { "type": "number", "minimum": 0, "maximum": 1 },
                "cluster": { "type": "object", "properties": { "min": { "type": "integer" }, "max": { "type": "integer" } }, "additionalProperties": false }
              },
              "additionalProperties": false
            }
          }
        },
        "additionalProperties": false
      }
    }
  },
  "additionalProperties": false
}
cavern.schema.json (single-layer)
json
Copy code
{
  "$id": "cavern.schema.json",
  "type": "object",
  "required": ["id","depth_range","noise","rooms","corridors","lakes"],
  "properties": {
    "id": { "type": "string" },
    "depth_range": { "type": "array", "items": { "type": "integer" }, "minItems": 2, "maxItems": 2 },
    "noise": {
      "type": "object",
      "properties": {
        "frequency": { "type": "number" },
        "octaves": { "type": "integer" },
        "threshold": { "type": "number" }
      },
      "additionalProperties": false
    },
    "rooms": { "type": "object", "properties": { "density": { "type": "number" }, "size": { "type": "object", "properties": { "min": { "type": "integer" }, "max": { "type": "integer" } }, "additionalProperties": false } }, "additionalProperties": false },
    "corridors": { "type": "object", "properties": { "density": { "type": "number" }, "width": { "type": "integer" } }, "additionalProperties": false },
    "lakes": { "type": "object", "properties": { "chance": { "type": "number" }, "max_radius": { "type": "integer" } }, "additionalProperties": false }
  },
  "additionalProperties": false
}
autotile.schema.json
json
Copy code
{
  "$id": "autotile.schema.json",
  "type": "object",
  "required": ["id","rule_set","neighborhood","tileset_id","mapping"],
  "properties": {
    "id": { "type": "string" },
    "rule_set": { "type": "string", "enum": ["blob","wang","marching_squares"] },
    "neighborhood": { "type": "string", "enum": ["von_neumann","moore"] },
    "tileset_id": { "type": "string" },
    "mapping": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["mask","frame"],
        "properties": {
          "mask": { "type": "integer", "minimum": 0 },
          "frame": { "type": "integer", "minimum": 0 }
        },
        "additionalProperties": false
      }
    }
  },
  "additionalProperties": false
}
tileset.schema.json
json
Copy code
{
  "$id": "tileset.schema.json",
  "type": "object",
  "required": ["id","atlas","tile_px","grid","frames"],
  "properties": {
    "id": { "type": "string" },
    "atlas": { "type": "string" },
    "tile_px": { "type": "object", "properties": { "w": { "type": "integer" }, "h": { "type": "integer" } }, "additionalProperties": false },
    "grid": { "type": "object", "properties": { "cols": { "type": "integer" }, "rows": { "type": "integer" } }, "additionalProperties": false },
    "frames": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["index","tags"],
        "properties": {
          "index": { "type": "integer", "minimum": 0 },
          "tags": { "type": "array", "items": { "type": "string" } }
        },
        "additionalProperties": false
      }
    },
    "animations": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["name","frame_indices","ms_per_frame"],
        "properties": {
          "name": { "type": "string" },
          "frame_indices": { "type": "array", "items": { "type": "integer" } },
          "ms_per_frame": { "type": "integer", "minimum": 1 }
        },
        "additionalProperties": false
      }
    }
  },
  "additionalProperties": false
}
fluid_lut.schema.json
json
Copy code
{
  "$id": "fluid_lut.schema.json",
  "type": "object",
  "required": ["id","depth8_to_rgba"],
  "properties": {
    "id": { "type": "string" },
    "depth8_to_rgba": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["depth8","rgba"],
        "properties": {
          "depth8": { "type": "integer", "minimum": 0, "maximum": 8 },
          "rgba": { "type": "array", "items": { "type": "integer", "minimum": 0, "maximum": 255 }, "minItems": 4, "maxItems": 4 }
        },
        "additionalProperties": false
      }
    },
    "foam_threshold": { "type": "integer", "minimum": 0, "maximum": 8 }
  },
  "additionalProperties": false
}
furniture.schema.json (wrapper to buildable)
json
Copy code
{
  "$id": "furniture.schema.json",
  "allOf": [
    { "$ref": "buildable.v1.schema.json" }
  ]
}
item.schema.json (wrapper to item.v3)
json
Copy code
{
  "$id": "item.schema.json",
  "allOf": [
    { "$ref": "item.v3.schema.json" }
  ]
}
