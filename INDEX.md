This index maps concepts → markdown specs so codegen tools can jump straight to the right contract.
Legend: [N] Normative (binding) · [U] Unified (merged) · [D] Design (non-binding) · [T] TODO (planned)

0) Start Here (Core Overview)

GAME_ARCHITECTURE.md [N][U] — High-level modules, ownership, boundaries, dependencies.

GAME_STATE_FLOW.md [N][U] — Fortress-only flow (menus → worldgen → embark → play → save/load), edge-band incidents.

DEVELOPMENT_PROCEDURE.md [N] — Phase plan, gates/DoD, parallel workstreams.

1) Simulation Kernel & Concurrency

CONCURRENCY_MODEL.md [N] — Read-parallel / Write-serialized, Diff-Log + Chunk-Actor options, barriers.

UPDATE_ORDER.md (Unified) [N][U] — Fixed per-tick stages, write windows, rebuild/commit points.

SIM_LOD_POLICY.md [N] — L0/L1 active; L2+ frozen/decimated; pin/promote rules.

CHUNK_AND_DATA_LAYOUT.md [N][U] — Chunk geometry (32×32×Z), SoA hot arrays + sparse overlays, indices/masks.

2) Tiles, Fields, Fluids & Rendering

TILE_SPEC.md (Detailed) [N][U] — L0–L7 responsibilities, TileBase bit layouts, mutation rules, snapshot contracts.

FIELD_SPEC.md [N] — L4 overlay (gases/decals/aura), decay/propagation, LOS opacity, serialization.

FLUID_SPEC.md [N] — L3 single-kind depth 0..7, quantized solver, budgets, determinism.

RENDERING_SNAPSHOT.md (Unified) [N][U] — Immutable snapshot DTOs, draw order, dirty-chunk rebuild.

AUTOTILING_AND_ROTATION.md [N] — connect_groups, NESW masks, rotation chains (data-driven).

TILE_CSHARP_SKELETON.md [D] — Minimal code shapes for TileBase[], overlays, derived caches.

DATA_LAYOUT.md / TILE_LAYERS.md [D] — Earlier rationale; superseded by TILE_SPEC.md.

3) World Generation (Fortress & World Map)

MAPGEN_PIPELINE.md (Unified) [N][U] — Stage-by-stage (height/biome/geology/cavern/POI), seed hierarchy, write targets (L0/L1/L3/L4/L7).

MAPGEN_X_TILES.md [D] — Layer mapping examples; reference supplement.

4) Navigation & Movement

NAVIGATION_SPEC.md [N] — NavMask/NavCost, ramps/stairs rules, door/fluid costs, deterministic A*, budgets, LOD cooperation.

5) Jobs, Hauling & Construction

JOB_SCHEDULER_SPEC.md [N] — Task board, priorities, reservations TTL, fairness/starvation rules.

HAULING_POLICY.md [N] — Stockpile (area) pull, supply, dump, anti ping-pong, scoring, multi-pick, reservations.

BUILDABLE_SPEC.md [N] — Buildables (workshops/doors/walls) registry model, placement/rotation/IO policy.

DESIGNATIONS_AND_BLUEPRINTS.md [T] — L7 markers → jobs, mass ops, priorities (planned).

6) Storyteller / Incidents

INCIDENT_DIRECTOR_SPEC.md [N] — Threat budget, candidate selection, cooldowns/safety rails, edge-band targeting, executors, save state.

7) Creatures, Combat & AI

CREATURES_SPEC.md [N][U] — Attributes (vitals/skills/traits), body slots (CDDA-like, not hyper-detailed), multi-limb ready, extra cognitive fields (reserved).

COMBAT_SPEC.md [T] — Damage rolls, armor/coverage, encumbrance→move penalty hook, morale (MVP).

AI_CITIZEN_SPEC.md [T] — Utility buckets (work/eat/sleep/idle/flee), scheduling, visitors/caravans.

8) Content & Registries (Data-Driven, Anti-Hardcode)

CONTENT_REGISTRY_OVERVIEW.md (Normative) [N] — Load order (base→DLC→mod), string IDs↔runtime indices, conflict resolution, hot-reload, save impact.

CONTENT_BUILD_PIPELINE.md [N] — .cpack compile, IdMaps/TagIndex/LUTs, determinism, signatures.

Gameplay registries/specs

MATERIALS_SPEC.md [N] — Simplified physical + magical properties (density/mass from density×volume, flammability, magic resist/conduct), heat/ignition/melt (simplified).

ITEMS_SPEC.md [N] — Item proto (tags, slots coverage, weight/encumbrance model, quality hooks, recipe tags).

FLUID_SPEC.md [N] — Fluid prototypes (id, phase, density/viscosity, color, interaction flags).

FIELD_SPEC.md [N] — Field prototypes (category, render priority, decay/propagate/effects).

BUILDABLE_SPEC.md [N] — Furniture/workshops footprint, rotation, blockers, IO policy.

RECIPE_SPEC.md [N] — Work orders (tags, material classes, skill/bench reqs), yields, batch/repeat.

Tuning & global LUTs

/content/registries/tuning.damage.json [N] — Damage constants: k_mat, k_item, penetration/coverage curves, caps.

/content/registries/tuning.navigation.json [N] — Move costs, ramps/stairs deltas, fluid thresholds, node/time budgets.

/content/registries/tuning.storyteller.json [N] — Director tick, curves, cooldowns, rails, weights/scales.

/content/registries/tuning.fields.json [N] — LOS sum mode vs dominant, budgets, LOD freeze.

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

SAVE_FORMAT.md (Normative) [N] — Bundle layout, chunks, registries signatures, world actors/factions/sites/artifacts ledgers, memories (player vs NPC knowledge).

ERROR_HANDLING_POLICY.md (Normative) [N] — try/catch boundaries per system/chunk/tick, quarantine, user-safe degradations.

DETERMINISM_CI.md (Normative) [N] — Replay gates, cross-OS parity, golden seeds, chaos injection rules.

10) UI & Input

UI_AND_INPUT_MODEL.md [N] — MVU store, command bus, virtualization, input bindings (SadConsole primary).

SADCONSOLE-MIGRATION.md [D] — Renderer specifics & tips (legacy-to-current).

11) Legacy / Superseded (kept for reference)

architecture.md / TECHNICAL_SOLUTION.md / old GAME_ARCHITECTURE.md — Reference only; look at unified docs above.

MAPGEN_X_TILES.md, DATA_LAYOUT.md, TILE_LAYERS.md — Background; superseded by TILE_SPEC & MAPGEN_PIPELINE.

12) Quick “If you’re implementing X, read Y”

Pathfinding → NAVIGATION_SPEC.md + TILE_SPEC.md (NavMask/NavCost rules).

Hauling → HAULING_POLICY.md + JOB_SCHEDULER_SPEC.md + ITEMS_SPEC.md.

Incidents → INCIDENT_DIRECTOR_SPEC.md + SIM_LOD_POLICY.md.

Build/Place → BUILDABLE_SPEC.md + TILE_SPEC.md (L2 blockers & autotile).

Fields/Fluids → FIELD_SPEC.md / FLUID_SPEC.md + UPDATE_ORDER.md.

Saving → SAVE_FORMAT.md + CONTENT_REGISTRY_OVERVIEW.md.

Rendering → RENDERING_SNAPSHOT.md + TILE_SPEC.md + UI_AND_INPUT_MODEL.md.

13) Folder Hints (suggested)
/docs/
  arch/            (architecture, state flow, concurrency, LOD)
  sim/             (tiles, nav, fluids, fields, jobs, hauling, storyteller)
  content/         (registries specs, schemas, tuning)
  persistence/     (save format, determinism CI, error policy)
  ui/              (rendering snapshot, UI/input)
  worldgen/        (mapgen pipeline)

14) Open TODOs you’ll see referenced

DESIGNATIONS_AND_BLUEPRINTS.md [T]

COMBAT_SPEC.md [T]

AI_CITIZEN_SPEC.md [T]

JSON Schemas listed in §8 (world/biome/geology/cavern/autotile/tileset/fluid_lut/furniture).