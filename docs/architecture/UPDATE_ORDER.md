UPDATE_ORDER.md — Unified (Stages · RNG · Multi-Threading · Commit Barrier)
id: update-order.v2
status: normative
owner: core/simulation
last_updated: 2025-09-14
version_policy: semver

Current implementation note (2026-07-09):

- Current code implements the essential read/barrier/write shape in `TickScheduler`, with read systems executed in deterministic registered-system order until real chunk-partitioned read jobs exist.
- Runtime `SimulationTickPipeline` attaches pre/post tick hooks: commands run before read; typed diff applicators and navigation dirty-chunk rebuild/cache invalidation run after write.
- The nine-stage model below is still the target contract. It is not yet a one-to-one list of concrete scheduler stages.

0) Scope

This file merges and supersedes prior UPDATE_ORDER, with hooks into Tile Layers, Data Layout, and the Rendering Snapshot contract. It defines the only places where world writes may occur and how parallelism stays deterministic. 

UPDATE_ORDER

1) Principles (Normative)

Single commit barrier per tick; everything else is read/plan. Each stage owns its RNG stream and stable iteration order. 

UPDATE_ORDER

Chunk-parallel, non-overlapping writes: schedule across chunks; aliasing prevented by explicit read/write masks. 

UPDATE_ORDER

Renderer isolation: render from the immutable snapshot only; no live reads. 

RENDERING_SNAPSHOT

2) Per-Tick Pipeline (Normative)

Exactly these nine stages. Mutators outside them are forbidden (assert in dev builds). 

UPDATE_ORDER

ApplyCommands

Consume player/AI orders; validate; enqueue diffs (plan).

Writes: L0/L2/L7. Side-effects: mark dirty tile + neighbors. 

UPDATE_ORDER

Notes: All edits to topology/furniture must bump neighbor rebuild scope later. 

DATA_LAYOUT

RebuildDerived (local)

Incremental rebuild for dirty sets: NavMask/NavCost, OpacMask, SupportMask; bump ConnectivityVersion.

Writes: derived caches only. No authoritative writes. 

UPDATE_ORDER

 

DATA_LAYOUT

Support & Collapse

Evaluate L0/L2 support rules; resolve collapses/rubble; may spawn item debris.

Writes: L0/L2/L5. 

UPDATE_ORDER

FluidsStep (budgeted)

Active-list update (spill/merge/evap/freezing hooks).

Budget: process ≤ F cells per tick. Writes: L3; may enqueue L4 steam. 

UPDATE_ORDER

FieldsStep (budgeted)

Diffuse/decay/ignite/poison mixing.

Budget: process ≤ G entries per tick. Writes: L4 (+ events). 

UPDATE_ORDER

Vegetation & Surface

Growth/decay, tracks/trample; light cosmetic gameplay effects.

Writes: L1. 

UPDATE_ORDER

Items

Aging/rot/temperature, stack merges, reservations.

Writes: L5. 

UPDATE_ORDER

EmitEvents

Publish compact event stream for UI/logic.

Writes: none (events only). 

UPDATE_ORDER

BuildRenderSnapshot

Freeze immutable snapshot for renderer/UI; no world writes beyond this point.

Snapshot contains TilePaletteIndex, FluidDepth, FieldGlyphs, Designations, Billboards; autotile/rotation resolved here; draw order fixed. 

UPDATE_ORDER

 

RENDERING_SNAPSHOT

3) Stage Contracts (Pre/Post-Conditions, Normative)

Common pre-conditions

Stage has its own RNG stream; seed WorldSeed ^ Hash(StageId); sort any order-sensitive loops by a deterministic key before sampling RNG. 

UPDATE_ORDER

Jobs declare reads/writes; plan jobs only read (emit diffs). Merge/apply jobs write the owning chunk only. 

UPDATE_ORDER

Dirty propagation rules (observed by 1–3):

L0/L2 edits ⇒ tile + 6 neighbors dirty for derived rebuild.

L3 change ⇒ tile (optionally neighbor slope handling).

L4 change ⇒ affects LOS/opacity only. 

DATA_LAYOUT

Snapshot post-conditions

Renderer will only read the snapshot; build for dirty chunks; keep a visibility cache and invalidate on topology change. 

RENDERING_SNAPSHOT

4) Writes by Layer & Stage (Normative Matrix)
Layer	Responsibility (quick)	Stages allowed to write
L0 Terrain	topology/support/standable	ApplyCommands, Support & Collapse
L1 Surface	soil/mud/vegetation skin	Vegetation & Surface
L2 Constr/Furniture	blocker/passables/connect groups	ApplyCommands, Support & Collapse
L3 Fluids	kind + depth (0..7)	FluidsStep
L4 Fields	gases/decals/fire/etc.	FieldsStep
L5 Items	stacks/ownership	Support & Collapse, Items
L6 Units	transient occupancy	(handled by unit systems; no direct stage writes here in this file)
L7 Meta/Markers	designations/rooms/traffic/visibility	ApplyCommands
(Layer responsibilities per unified layers doc.) 

TILE_LAYERS

 

TILE_LAYERS

		
5) Deterministic Ordering (Normative)

Within a stage: iterate chunks in ascending chunkId; within chunk, sort tiles by tileKey = (z<<16)|(y<<8)|x.

Merge tie-break (same tile): Priority(desc) → SystemId(asc) → LocalSeq(asc).

Event stream: stable by (tick → stageId → chunkId → tileKey → eventType).
These rules ensure same seed + same inputs ⇒ same outputs across OS/CPU. 

UPDATE_ORDER

6) RNG Streams (Normative)

Seed each stage as WorldSeed ^ Hash(StageId); within a stage, derive per-system/per-chunk sub-streams if needed.

Never call RNG inside an iteration whose order can vary; sort first, then sample. 

UPDATE_ORDER

7) Multithreading Rules (Normative)

Stages run in parallel across chunks; the scheduler must refuse any job set with overlapping writes. Plan jobs must not mutate; only the commit step writes. 

UPDATE_ORDER

Use SoA bases + sparse overlays + per-chunk ConnectivityVersion for cheap invalidation and data locality. 

DATA_LAYOUT

8) Failure Safety & Stability (Normative)

Stage orchestrator is wrapped in a boundary try–catch; on exception: quarantine the chunk/system for this tick, drop invalid diffs, degrade to serial next tick; never tear down the loop. Log {seed,tick,stage,chunkId}. (See RULES for the full policy.) 

rules

9) Rendering Coupling (Informative but Binding on Isolation)

Snapshot fields (per Z): TilePaletteIndex (autotile & rotation already resolved), FluidDepth, FieldGlyphs, Designations, Billboards.

Draw order (ortho): Floors → Surface → Fluids → Constructions/Furniture → Items → Fields → Units → UI. 

RENDERING_SNAPSHOT

10) Worldgen Alignment (Informative)

Worldgen stages follow the same discipline: stage-local RNG; write only their target layers; run a one-time post-generation commit (derived rebuild, support/collapse, fluids/fields settle, initial snapshot). 

MAPGEN_X_TILES

 

MAPGEN_X_TILES

11) Checklists (Drop-in)
11.1 Stage Definition

 Stage declares RNG stream and stable iteration policy. 

UPDATE_ORDER

 Stage defines reads/writes and budget (F/G where applicable). 

UPDATE_ORDER

 All writes occur after deterministic merge/apply in the commit step. 

UPDATE_ORDER

11.2 Dirty & Derived

 L0/L2 edit ⇒ tile + 6 neighbors dirty; L3 ⇒ tile; L4 ⇒ LOS only. 

DATA_LAYOUT

 ConnectivityVersion bumped when topology/doors change. 

DATA_LAYOUT

11.3 Snapshot

 Snapshot built for dirty chunks only; visibility cache invalidated on topology change; autotile/rotation resolved during build. 

RENDERING_SNAPSHOT

11.4 Multithreading Safety

 No overlapping write sets; plan jobs are read-only; commit jobs own the chunk. 

UPDATE_ORDER

11.5 Stability

 Boundary try–catch in stage orchestrator; quarantine/degrade on failure; structured logs include seed/tick. 

rules
