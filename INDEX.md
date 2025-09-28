This index maps concepts â†?markdown specs so codegen tools can jump straight to the right contract.
Legend: [N] Normative (binding) Â· [U] Unified (merged) Â· [D] Design (non-binding) Â· [T] TODO (planned)

0) Start Here (Core Overview)

GAME_ARCHITECTURE.md [N][U] â€?High-level modules, ownership, boundaries, dependencies.

GAME_STATE_FLOW.md [N][U] â€?Fortress-only flow (menus â†?worldgen â†?embark â†?play â†?save/load), edge-band incidents.

DEVELOPMENT_PROCEDURE.md [N] â€?Phase plan, gates/DoD, parallel workstreams.

1) Simulation Kernel & Concurrency

CONCURRENCY_MODEL.md [N] â€?Read-parallel / Write-serialized, Diff-Log + Chunk-Actor options, barriers.

UPDATE_ORDER.md (Unified) [N][U] â€?Fixed per-tick stages, write windows, rebuild/commit points.

SIM_LOD_POLICY.md [N] â€?L0/L1 active; L2+ frozen/decimated; pin/promote rules.

CHUNK_AND_DATA_LAYOUT.md [N][U] â€?Chunk geometry (32Ã—32Ã—Z), SoA hot arrays + sparse overlays, indices/masks.

2) Tiles, Fields, Fluids & Rendering

TILE_SPEC.md (Detailed) [N][U] â€?L0â€“L7 responsibilities, TileBase bit layouts, mutation rules, snapshot contracts.

FIELD_SPEC.md [N] â€?L4 overlay (gases/decals/aura), decay/propagation, LOS opacity, serialization.

FLUID_SPEC.md [N] â€?L3 single-kind depth 0..7, quantized solver, budgets, determinism.

RENDERING_SNAPSHOT.md (Unified) [N][U] â€?Immutable snapshot DTOs, draw order, dirty-chunk rebuild.

AUTOTILING_AND_ROTATION.md [N] â€?connect_groups, NESW masks, rotation chains (data-driven).

TILE_CSHARP_SKELETON.md [D] â€?Minimal code shapes for TileBase[], overlays, derived caches.

DATA_LAYOUT.md / TILE_LAYERS.md [D] â€?Earlier rationale; superseded by TILE_SPEC.md.

3) World Generation (Fortress & World Map)

MAPGEN_PIPELINE.md (Unified) [N][U] â€?Stage-by-stage (height/biome/geology/cavern/POI), seed hierarchy, write targets (L0/L1/L3/L4/L7).

MAPGEN_X_TILES.md [D] â€?Layer mapping examples; reference supplement.

4) Navigation & Movement

NAVIGATION_SPEC.md [N] â€?NavMask/NavCost, ramps/stairs rules, door/fluid costs, deterministic A*, budgets, LOD cooperation.

5) Jobs, Hauling & Construction

JOB_SCHEDULER_SPEC.md [N] â€?Task board, priorities, reservations TTL, fairness/starvation rules.

HAULING_POLICY.md [N] â€?Stockpile (area) pull, supply, dump, anti ping-pong, scoring, multi-pick, reservations.

BUILDABLE_SPEC.md [N] â€?Buildables (workshops/doors/walls) registry model, placement/rotation/IO policy.

DESIGNATIONS_AND_BLUEPRINTS.md [T] â€?L7 markers â†?jobs, mass ops, priorities (planned).

6) Storyteller / Incidents

INCIDENT_DIRECTOR_SPEC.md [N] â€?Threat budget, candidate selection, cooldowns/safety rails, edge-band targeting, executors, save state.

7) Creatures, Combat & AI

CREATURES_SPEC.md [N][U] â€?Attributes (vitals/skills/traits), body slots (CDDA-like, not hyper-detailed), multi-limb ready, extra cognitive fields (reserved).

COMBAT_SPEC.md [T] â€?Damage rolls, armor/coverage, encumbranceâ†’move penalty hook, morale (MVP).

AI_CITIZEN_SPEC.md [T] â€?Utility buckets (work/eat/sleep/idle/flee), scheduling, visitors/caravans.

8) Content & Registries (Data-Driven, Anti-Hardcode)

CONTENT_REGISTRY_OVERVIEW.md (Normative) [N] â€?Load order (baseâ†’DLCâ†’mod), string IDsâ†”runtime indices, conflict resolution, hot-reload, save impact.

CONTENT_BUILD_PIPELINE.md [N] â€?.cpack compile, IdMaps/TagIndex/LUTs, determinism, signatures.

Gameplay registries/specs

MATERIALS_SPEC.md [N] â€?Simplified physical + magical properties (density/mass from densityÃ—volume, flammability, magic resist/conduct), heat/ignition/melt (simplified).

ITEMS_SPEC.md [N] â€?Item proto (tags, slots coverage, weight/encumbrance model, quality hooks, recipe tags).

FLUID_SPEC.md [N] â€?Fluid prototypes (id, phase, density/viscosity, color, interaction flags).

FIELD_SPEC.md [N] â€?Field prototypes (category, render priority, decay/propagate/effects).

BUILDABLE_SPEC.md [N] â€?Furniture/workshops footprint, rotation, blockers, IO policy.

RECIPE_SPEC.md [N] â€?Work orders (tags, material classes, skill/bench reqs), yields, batch/repeat.

Tuning & global LUTs

TUNING_FILES.md [U] ¡ª Overview of tuning.mapgen.json / tuning.cavern.json / tuning.ore.json keys.

/content/registries/tuning.damage.json [N] â€?Damage constants: k_mat, k_item, penetration/coverage curves, caps.

/content/registries/tuning.navigation.json [N] ¡ª Move costs, ramps/stairs deltas, fluid thresholds, node/time budgets. (Planned; current defaults in code)

/content/registries/tuning.storyteller.json [N] â€?Director tick, curves, cooldowns, rails, weights/scales.

/content/registries/tuning.fields.json [N] â€?LOS sum mode vs dominant, budgets, LOD freeze.

Schemas (JSON)

/content/schemas/world.params.schema.json [T]

/content/schemas/biome.schema.json [T]

/content/schemas/geology.schema.json [T]

/content/schemas/cavern.schema.json [T] (single-layer v1)

/content/schemas/autotile.schema.json [T]

/content/schemas/tileset.schema.json [T]

/content/schemas/fluid_lut.schema.json [T]

/content/schemas/furniture.schema.json [T]

/content/schemas/item.schema.json [N] (aligned to ITEMS_SPEC)

9) Persistence, Errors & Determinism

SAVE_FORMAT.md (Normative) [N] â€?Bundle layout, chunks, registries signatures, world actors/factions/sites/artifacts ledgers, memories (player vs NPC knowledge).

ERROR_HANDLING_POLICY.md (Normative) [N] â€?try/catch boundaries per system/chunk/tick, quarantine, user-safe degradations.

DETERMINISM_CI.md (Normative) [N] â€?Replay gates, cross-OS parity, golden seeds, chaos injection rules.

10) UI & Input

UI_AND_INPUT_MODEL.md [N] â€?MVU store, command bus, virtualization, input bindings (SadConsole primary).

SADCONSOLE-MIGRATION.md [D] â€?Renderer specifics & tips (legacy-to-current).

11) Legacy / Superseded (kept for reference)

architecture.md / TECHNICAL_SOLUTION.md / old GAME_ARCHITECTURE.md â€?Reference only; look at unified docs above.

MAPGEN_X_TILES.md, DATA_LAYOUT.md, TILE_LAYERS.md â€?Background; superseded by TILE_SPEC & MAPGEN_PIPELINE.

12) Quick â€œIf youâ€™re implementing X, read Yâ€?

Pathfinding â†?NAVIGATION_SPEC.md + TILE_SPEC.md (NavMask/NavCost rules).

Hauling â†?HAULING_POLICY.md + JOB_SCHEDULER_SPEC.md + ITEMS_SPEC.md.

Incidents â†?INCIDENT_DIRECTOR_SPEC.md + SIM_LOD_POLICY.md.

Build/Place â†?BUILDABLE_SPEC.md + TILE_SPEC.md (L2 blockers & autotile).

Fields/Fluids â†?FIELD_SPEC.md / FLUID_SPEC.md + UPDATE_ORDER.md.

Saving â†?SAVE_FORMAT.md + CONTENT_REGISTRY_OVERVIEW.md.

Rendering â†?RENDERING_SNAPSHOT.md + TILE_SPEC.md + UI_AND_INPUT_MODEL.md.

13) Folder Hints (suggested)
/docs/
  arch/            (architecture, state flow, concurrency, LOD)
  sim/             (tiles, nav, fluids, fields, jobs, hauling, storyteller)
  content/         (registries specs, schemas, tuning)
  persistence/     (save format, determinism CI, error policy)
  ui/              (rendering snapshot, UI/input)
  worldgen/        (mapgen pipeline)

14) Open TODOs youâ€™ll see referenced

DESIGNATIONS_AND_BLUEPRINTS.md [T]

COMBAT_SPEC.md [T]

AI_CITIZEN_SPEC.md [T]

JSON Schemas listed in Â§8 (world/biome/geology/cavern/autotile/tileset/fluid_lut/furniture).
