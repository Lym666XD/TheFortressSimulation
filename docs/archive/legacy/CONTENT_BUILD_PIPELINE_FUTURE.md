id: content.build.v1
status: normative
owner: engine/content
last_updated: 2025-09-15
applies_to:
  - Content packs (base → DLC → mods)
  - Registries (materials, items, fluids, buildables, recipes, autotile/tileset, biomes, body-plans, tuning.*)
  - Engine loaders, hot-reload, determinism CI
goals:
  - Zero hard-coding: everything is data + schema.
  - Deterministic builds across OS/thread counts.
  - Fast runtime: precompile heavy lookups & indices.
  - Safe hot-reload & robust error isolation.
references:
  - CONTENT_REGISTRY_OVERVIEW.md (Normative)
  - SAVE_FORMAT.md (packset_signature & registry_hash)
  - SIM_LOD_POLICY.md / UPDATE_ORDER.md (for runtime ties)
0) Scope & Decisions
Inputs: human-editable JSON files validated by JSON Schemas under /content/schemas/….

Outputs: compact compiled packs (MessagePack → Zstd, *.cpack) and a human-readable manifest with signature.

Merge order: base → DLC… → mod… (later overrides earlier). Tie-breakers and add/replace semantics are data-driven.

Determinism: all discovery/merge/compile stages use sorted stable orders; hashing uses BLAKE3-256.

1) Layout & Conventions (Authoritative)
bash
Copy code
/content/
  schemas/                    # JSON Schemas (versioned)
  packs/
    base/
      manifest.json           # {id, version, schema_version, deps[]}
      materials/*.json
      items/*.json
      fluids/*.json
      buildables/*.json
      recipes/*.json
      autotile/*.json
      tilesets/*.json
      biomes/*.json
      bodyplans/*.json
      tuning.*.json
    dlc_age_iron/…
    mod_*/…
/build/content/
  compiled/
    base.cpack                # compiled registries & indices for the pack
    dlc_age_iron.cpack
    …
  compiled.manifest.json      # packset_signature, registry_hash, counts
  logs/…
Pack manifest (required):

json
Copy code
{ "id":"base", "version":"1.0", "schema_version":4, "deps": [] }
2) Build Stages (Deterministic Pipeline)
Stage A — Discover & Order
Scan /content/packs/*/manifest.json.

Resolve dependencies; compute a stable topological order.

Freeze packset_signature: "<id>@<version>@sv<schema_version>" sorted by pack order.

Stage B — Parse & Validate (in parallel)
Parse all JSON, validate against the corresponding schema (*.schema.json).

Errors (schema/parse): mark entry as invalid; continue; build fails at the end if any required registry has fatal errors (see §10).

Stage C — Normalize
Trim/normalize strings; default values applied per schema; resolve relative paths; canonicalize arrays (sort tags, remove dupes).

Enforce ID format: namespace:token or token (auto-namespace to pack id). IDs are case-sensitive, ASCII-safe.

Stage D — Merge (base→DLC→mod)
Entities keyed by id. Default merge policy is replace-by-id (later wins).

Additive fields (e.g., tags, allowed_materials) use set-union merge.

List overrides support directives in the overriding entry:

"$op":"replace" — replace entire list

"$op":"append" — append unique entries (stable order)

"$delete":["fieldA","fieldB"] — delete fields from base

Field-level merge hints are defined in schemas ($merge:"additive|replace|map").

Stage E — Cross-Refs & Lint
Verify referential integrity: item ⇄ materials, recipes ⇄ items/fluids/buildables, body-plan slots ⇄ items coverage, autotile rule ids ⇄ tilesets.

Domain lint: duplicates, cycles (where banned), unreachable recipes, orphaned tags, impossible coverage.

Stage F — Compile (Indices & LUTs)
IdMap per registry: string id → u16/u24 runtime index (stable, compact).

TagIndex: tag → bitset(ids) for fast queries.

RecipeReverseIndex:

inputs: item_id or tag → recipes[]

workshops: workshop_id → recipes[]

products: product_id → recipes[]

CraftGraph: DAG of recipes by dependency; detect cycles (warn or error per schema).

MaterialLUTs: dense arrays for hot fields (e.g., density, hardness, resistances, flammability, magic_resist, mana_conductance).

ItemLUTs: weight (via material density or explicit), volume, encumbrance, armor coverage masks → precomputed integers/bitmasks.

AutotileCompiled: rotation/mask tables, adjacency bitmasks, rule priority; tileset glyph mapping.

Biome Tables: weighted spawns/resources by tier/season; pre-sampled alias tables if configured.

BodyPlanExpander: resolves slot groups (multi-limb/wing variants) into canonical slot IDs; builds slot_masks used by armor coverage & damage.

All compiled data uses little-endian fixed encodings and sorted maps.

Stage G — Emit Artifacts
*.cpack (MessagePack→Zstd):

registries (materials/items/fluids/buildables/recipes/…)

all indices/LUTs (IdMap, TagIndex, ReverseIndex, CraftGraph, AutotileCompiled, …)

pack metadata (id, version, schema_version, content hash)

compiled.manifest.json (human-readable):

json
Copy code
{
  "packset_signature": ["base@1.0@sv4","dlc_age_iron@1.1@sv4"],
  "registry_hash": "sha256:abcd…",
  "counts": { "materials": 513, "items": 1320, "buildables":210, "fluids":22, "recipes": 480 }
}
3) Runtime Contracts (Binding)
Loader reads compiled.manifest.json → verifies packset_signature and registry_hash (SAVE_FORMAT).

For each .cpack (in order): load registries & indices; compose final IdMaps (later packs may add or override ids).

Stable numeric IDs:

New IDs are appended at the end; removed IDs are retired but their slot stays reserved for the session (hot-reload-safe).

Save/Load always uses string IDs; numeric IDs are session-internal.

4) Hot Reload (Barrier-Swapped)
Watch /content/packs/**. On change:

Rebuild affected packs and registries (incremental).

Produce new .cpack and a new snapshot of the composed registries.

At the tick barrier, atomically swap registry snapshots; issue a Rebind Pass to systems.

Rebind failures (e.g., item slot removed): auto-unequip to safe container → if none, drop to ground; pause jobs referencing removed recipes with reason PausedMissingContent.

Id stability in-session: do not repack earlier packs’ numeric indices; append only.

5) Determinism Requirements
Directory enumeration, file lists, and JSON maps are processed in lexicographic order.

All merges & indices use stable sort keys (id, and where equal, pack order then path).

Hashes are computed on canonicalized representations (no whitespace/surrogate variations).

Parallel parse is allowed; merge/compile happens on deterministic single-threaded steps or with ordered reducers.

6) Schemas & Versions (Normative)
Each schema file carries a "$schema_version"; pack manifest.schema_version must match.

Backwards-compatible additions are allowed with defaults; removals/renames bump schema major.

The builder rejects packs whose schema_version is unsupported, with E_SCHEMA_VERSION.

7) Merge Semantics Matrix (Normative)
Registry	Key	Default Merge	Additive Fields (Union)	Special Rules
materials	id	replace	tags	LUT fields must keep units; numeric ranges enforced
items	id	replace	tags, allowed_materials	coverage masks normalized; encumbrance formula precompiled
fluids	id	replace	tags	temperature flags simplified model (ignite/melt only)
buildables	id	replace	tags, ports, recipes_enabled	ports merged by port.id (replace) unless $op given
recipes	id	replace	input_tags, input_items (append)	workshop requirements merged (union); costs recomputed
autotile	id	replace	rules (append then stable-sort)	rule priority determines order; duplicates removed
tilesets	id	replace	glyph_overrides (append)	must map to SadConsole glyph indices
biomes	id	replace	spawns/resources (append)	distributions normalized
bodyplans	id	replace	slot_groups (append)	canonical slot ids must be unique

$op directives can override defaults per entry.

8) Indices & LUTs (Normative Details)
IdMap: id→index + index→id (string table); indices are u16/u24 chosen by max count per registry.

TagIndex: for each registry kind, map tag to a bitset (packed bytes) of members for O(1) AND/OR queries.

RecipeReverseIndex:

by_input_item[id] → recipe_id[]

by_input_tag[tag] → recipe_id[]

by_output[id] → recipe_id[]

by_workshop[id] → recipe_id[]
Arrays sorted by recipe_id to keep determinism and speed.

CraftGraph: nodes = recipe_id, edges from inputs→producers; cycles flagged.

AutotileCompiled: for each rule, precompute neighbor-mask, rotation LUT, priority tier; emit compact tables used by tile mesher.

Coverage/Armor LUT: precompute per-item slot_mask (bitset of body slots), per-creature required_slots; used by equipment checks & damage coverage.

Material LUT: dense arrays for numeric fields (density, hardness, resistances, flammability, magic_resist, mana_conductance), clamped & unit-checked.

9) Tooling & CLI (Binding)
content build — full rebuild; emits *.cpack + compiled.manifest.json.

content lint — schema + semantic lint; prints a table of warnings/errors.

content watch — incremental rebuild on file change; hot-reload handshake.

content diff --old A --new B — explains changes at the registry/id/field level.

content ids --kind items --grep "steel" — search IdMap.

Exit codes: non-zero on any ERROR; zero with WARNs allowed (configurable gate in CI).

10) Error Policy (Normative)
ERROR (build fails): invalid JSON, schema mismatch, duplicate id within same pack, missing required ref (e.g., recipe references unknown item), illegal numeric range, non-unique canonical slot ids.

WARN (entry skipped or auto-fixed): duplicate id across packs (later wins), unknown tags (pruned), unreachable recipe (no valid inputs), empty autotile rule set.

RECOVER: On hot-reload, if an ERROR would occur, keep previous snapshot active and report; never hot-swap to a broken snapshot.

All errors are reported with {pack, path, id, code, message} and a stable sort order.

11) Performance Targets (Informative but Binding in CI)
Full base build ≤ 2s on mid-spec CPU; incremental change to one file ≤ 150ms (95p).

.cpack total size ≤ 20 MB for base content (target).

Memory footprint for loaded registries + indices ≤ 64 MB (target).

12) Security & Sandbox (Mods)
No file IO or process execution from content; builder reads JSON only.

Embedded code is not supported in v1 (no script execution in pipeline).

Path traversal is rejected; only files inside the pack root are allowed.

13) Change Control
Changing merge policies or index encodings requires bumping id: content.build.v<minor> and updating dependent loaders.

Schemas evolve with explicit schema_version; packs that lag behind must be rejected or migrated by a dedicated offline migrator (separate tool).

14) Worked Example (Trimmed)
Before
base/items/armor_breastplate.json:

json
Copy code
{ "id":"armor_breastplate", "tags":["armor","torso"], "coverage":["torso"], "encumbrance":8 }
mod_lightweight/items/armor_breastplate.json:

json
Copy code
{ "id":"armor_breastplate", "encumbrance":6, "tags":["light_tuned"] }
Merged (conceptual)

json
Copy code
{ "id":"armor_breastplate",
  "tags":["armor","torso","light_tuned"],    // union
  "coverage":["torso"],                      // unchanged
  "encumbrance":6 }                          // replaced
Compiled indices will include:

items.IdMap["armor_breastplate"] → 142

TagIndex.items["armor"] bitset includes 142

CoverageLUT[142] = mask(torso)

EncumbranceLUT[142] = 6

15) Engine Integration Checklist
Loader accepts *.cpack + manifest; swaps snapshots at tick barrier.

Systems use IdMaps/TagIndex/LUTs instead of scanning registries.

Save/Load uses string IDs; compiled.manifest.json copied to save folder as registries.sig.json.

Error UI shows a compact, localized report with links to offending content paths.