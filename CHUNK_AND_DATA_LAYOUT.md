CHUNK_AND_DATA_LAYOUT.md — Unified (Chunks · Hot SoA · Overlays · Derived · Dirty)
0) Scope

This file merges the former DATA_LAYOUT and TILE_LAYERS documents and ties them to the update order and rendering snapshot contracts.

Goals: cache-friendly, deterministic, mod-safe; numbers are targets—tune with profiling. 

DATA_LAYOUT

1) Chunk Size & Shape (Guidelines)

Default: 32×32×Zc tiles per chunk with 1-tile halo; choose Zc=4–8 unless your design mandates taller vertical coupling.

When fluids/fields are heavy or you have many cores, consider 16×16×Zc (finer load balance, more borders).
When AI/building dominates and fluids are light, consider 64×64×Zc (fewer borders, watch long tails).

Rationale: L1 fit for hot data (SoA base + common derived), moderate border/actor traffic, and good scheduling granularity.

Persist W/Zc/halo in save metadata to keep replay/compat stable across machines.

2) World Partitioning & Chunk Layout

Fixed chunks (e.g., 16×16×Zc shown below; size is configurable). Per-chunk: hot Structure-of-Arrays (SoA) + sparse overlays keyed by tile index + derived caches + dirty sets. 

DATA_LAYOUT

Keep a per-chunk ConnectivityVersion:int to cheaply invalidate nav/LOS/support caches. 

DATA_LAYOUT

Typical layout:

SoA: TileBase[] (hot)

Overlays: Furniture (L2), Fields (L4), ItemStacks (L5) (sparse/pooled) 

DATA_LAYOUT

Derived: NavMask[], NavCost[], OpacMask[], SupportMask[] (rebuilt on demand) 

DATA_LAYOUT

DirtySets: {Tiles, Neighbors} for incremental rebuilds 

DATA_LAYOUT

3) Hot Tile Base (≤ 12 bytes target, Authoritative per Tile)

Fields (packed for cache locality):

GeoMatId:uint16, TerrainBits:uint16, SurfaceBits:byte, FluidKind:byte, FluidDepth:byte, MetaBits:byte, TrafficCost:uint16.

Immutable within a tick; mutators write only during the commit phase. 

DATA_LAYOUT

Keep base small & stable; push growth into overlays. (C# example mirrors this packing.) 

TECHNICAL_SOLUTION

4) Tile Layers (L0–L7) — Responsibilities & Query Order

Prefer few, strong layers; each owns a clear responsibility and a narrow write window in the commit phase. 

TILE_LAYERS

Responsibilities (excerpt):

L0 Terrain (topology) — floor/wall/ramp/stairs; dig/channel/build mutate L0; provides support & standability. Queries: IsFloor/IsWall/IsStandable/ProvidesSupport. 

TILE_LAYERS

L1 Surface — soil/mud/vegetation skin; visual/light modifiers; never blocks movement. 

TILE_LAYERS

L2 Constructions & Furniture — blocker + passables[]; support, opacity, autotile connect groups. 

TILE_LAYERS

L3 Fluids — kind + depth 0..7; solver is budgeted per tick. 

TILE_LAYERS

L4 Fields — many per tile (id,intensity,age); soft opacity/decals/gases/fire. 

TILE_LAYERS

L5 Items — pooled stacks; block only by prototype flags; single owning stockpile. 

TILE_LAYERS

L6 Units/Vehicles — transient occupancy; species rules. 

TILE_LAYERS

L7 Meta/Markers — designations/rooms/traffic/visibility/biome/connectivity. 

TILE_LAYERS

Query precedence for nav/LOS: (1) L0/L2 hard blocks/support → (2) L3 fluid penalties → (3) L4 soft opacity → (4) L5/L6 incidental blocks. 

TILE_LAYERS

5) Overlays (Sparse) & Pooling

Furniture (L2): blocker + passables with flags; sparse per active tile. 

DATA_LAYOUT

Fields (L4): inline up to N entries then overflow to a pool; compact (Id,Intensity,Age). 

DATA_LAYOUT

Items (L5): per-tile list of stack handles into a global pooled table. 

DATA_LAYOUT

6) Derived Caches & Dirty Propagation

Rebuild only for dirty tiles and targeted neighbors: NavMask/NavCost, OpacMask, SupportMask. 

DATA_LAYOUT

Dirty propagation rules (authoritative):

L0 or L2 edit → tile + 6 neighbors. 

DATA_LAYOUT

L3 change (depth/kind) → tile only (optionally neighbors for steep slopes). 

DATA_LAYOUT

L4 change → LOS/nebulous only (no support). 

DATA_LAYOUT

Bump ConnectivityVersion where needed so path/LOS caches cheaply invalidate. 

UPDATE_ORDER

7) Update Order — Write Windows (for Data Safety)

Only the listed stages may write; each has its own RNG stream and stable iteration order. Stages run parallel across chunks but must not overlap write sets. 

UPDATE_ORDER

 

UPDATE_ORDER

Mapping (excerpt):

ApplyCommands → writes L0/L2/L7; mark dirty. 

UPDATE_ORDER

Support & Collapse → may affect L0/L2/L5. 

UPDATE_ORDER

FluidsStep (budget F) → writes L3; may enqueue L4 steam. 

UPDATE_ORDER

FieldsStep (budget G) → writes L4 (+ events). 

UPDATE_ORDER

Vegetation & Surface → writes L1. 

UPDATE_ORDER

Items → writes L5. 

UPDATE_ORDER

BuildRenderSnapshot → read-only freeze for renderer. 

UPDATE_ORDER

8) Rendering Snapshot Coupling (Read-Only)

Renderer reads an immutable snapshot built at end of tick; no live world reads. 

RENDERING_SNAPSHOT

Snapshot contents per visible Z: TilePaletteIndex (autotile/rotation already resolved), FluidDepth, FieldGlyphs, Designations, Billboards. 

RENDERING_SNAPSHOT

Draw order: Floors → Surface → Fluids → Constructions/Furniture → Items → Fields → Units → UI overlays. 

RENDERING_SNAPSHOT

Build snapshots for dirty chunks only; keep a visibility cache and invalidate on topology changes. 

RENDERING_SNAPSHOT

Autotiling/Rotation is data-driven (connect_groups/connects_to, rotates_to), resolved in snapshot build. 

AUTOTILING_AND_ROTATION

 

AUTOTILING_AND_ROTATION

9) Deterministic Keys & Indexing

Suggested deterministic tile key: key = (chunkId << 24) | (z << 16) | (y << 8) | x.

Use stable sorts on keys wherever order impacts results (path/LOS merges, item reservations, fluid clamps).

10) Save/Load (Authoritative Only)

Serialize pure data: tile base, overlays, item stacks, field states, fluid fields, meta/markers. Do not serialize derived caches; rebuild on load. Include schemaVersion and migrations. 

DATA_LAYOUT

11) Data-Driven Registries (Anti-Hardcoding)

Terrain/Materials/Fluids/Fields/Furniture/Items should be registry-driven with string IDs and JSON schemas; loaders validate & report conflicts deterministically (base → DLC → mods). (See RULES/Modding.) 

rules

12) Performance & Memory Notes

Keep hot arrays tight; push variability into sparse overlays; favor SoA + pooling to reduce allocations/GC. 

TECHNICAL_SOLUTION

Target zero avoidable allocations on hot loops; measure deltas in CI. 

rules

13) Implementation Checklist

Chunk config

 W×W×Zc chosen; halo=1 enabled; persisted in save metadata.

 ConnectivityVersion per chunk; increment on relevant edits. 

DATA_LAYOUT

SoA base & overlays

 TileBase matches the ≤12B schema. 

DATA_LAYOUT

 L2/L4/L5 overlays are sparse/pooled; indices are per-tile lists/inline-then-pool. 

DATA_LAYOUT

Derived & dirty

 Incremental rebuild for dirty tiles + neighbors only; respect propagation rules. 

DATA_LAYOUT

Update order & writes

 Only allowed layers are written per stage; budgets F/G enforced. 

UPDATE_ORDER

Snapshot

 Renderer consumes immutable snapshot; autotile/rotation resolved at build. 

RENDERING_SNAPSHOT

Serialization

 Save authoritative only; rebuild derived on load; schemaVersion present. 

DATA_LAYOUT

14) Appendix — Quick Reference
14.1 Layer Responsibilities (recap)

L0: topology/support; L1: surface cosmetics; L2: constructions/furniture; L3: fluids; L4: fields; L5: items; L6: units; L7: meta/markers. 

TILE_LAYERS

 

TILE_LAYERS

14.2 Snapshot Draw Order

Floors → Surface → Fluids → Constructions/Furniture → Items → Fields → Units → UI. 

RENDERING_SNAPSHOT

14.3 Update Stages → Write Windows

ApplyCommands: L0/L2/L7; Fluids: L3; Fields: L4; Vegetation: L1; Items: L5; Snapshot: read-only. 

UPDATE_ORDER

 

UPDATE_ORDER

15) Changelog

[MERGE] Unified chunk/data layout from DATA_LAYOUT.md and TILE_LAYERS.md; aligned with UPDATE_ORDER and RENDERING_SNAPSHOT contracts. Added chunk sizing guidance and implementation checklist.