# Tile Specification (v2)

Authoritative tile spec aligned with a fortress-only, deterministic architecture. Tiles live in chunks (default 32x32 cells per Z). The current simulation runs deterministic registered-system reads plus serialized writes, and future chunk-partitioned read parallelism must preserve replay-stable ordering. The renderer consumes an immutable snapshot target. This document locks down types, bit layouts, APIs, invariants, and update rules.

## 0) Terminology & Constants

- Cell: a single (x,y) in a chunk layer at Z
- Chunk: 32x32 cells; addressed by ChunkKey(cx, cy, z)
- LocalIndex: idx = y * 32 + x (0..1023)
- Tick: fixed-step sim tick (50 TPS)
- Layers: L0..L7 (terrain -> meta)

## 1) Memory Model (Authoritative)

### 1.1 TileBase (hot base array, AoS packed)

```csharp
public readonly struct TileBase
{
    public readonly ushort GeoMatId;     // L0 geology/material handle
    public readonly ushort TerrainBits;  // L0 kind/flags (v2 layout below)
    public readonly byte   SurfaceBits;  // L1 mud/grass/snow/moss/fertility
    public readonly byte   FluidKind;    // L3 fluid kind (0=none)
    public readonly byte   FluidDepth;   // L3 depth (0..7)
    public readonly byte   MetaBits;     // L7 traffic/revealed/etc.
    public readonly ushort TrafficCost;  // cached nav cost base
}
```

One contiguous array per chunk: `TileBase[] Tiles = new TileBase[32*32]`.

### 1.2 Sparse Overlays (cold/variable)

- L2 Constructions: doors/walls/workshops (blockers/passables)
- L4 Fields: gases/decals (hazards/LOS)
- L5 Items: stacked items (pooled)
- L6 Units: runtime occupiers

## 1.3 TerrainBits Layout (v2, binding)

`TerrainBits` (ushort):

- bits 0..3: `TerrainKind` (0..15)
- bit 5: `Natural` (1=natural, 0=constructed)
- bit 6: `Modifiable` (1=player tools allowed; 0=forbidden)
- others: reserved (must be 0)

Public helpers provide `Kind`, `IsNatural`, `IsModifiable`. There is no `RampDirection` or polish bits in L0.

### 1.4 TerrainKind (values align to runtime enums)

- 0: `SolidWall`      — blocks all movement; provides support
- 1: `OpenWithFloor`  — walkable/standable; provides support
- 2: `OpenNoFloor`    — empty space (flyable only); no support
- 3: `Ramp`           — DF-style ramp base; directions derived at runtime via UpRampMask
- 4: `Slope`          — reserved (visual only; not authored as geometry)
- 5: `StairsUp`       — Z up
- 6: `StairsDown`     — Z down
- 7: `StairsUD`       — Z up/down

Notes:
- `Standable` is true for `OpenWithFloor` only (not for `Slope`).
- `Modifiable` policy: only bottommost Z (z==0) has `Modifiable=0` to prevent digging into the void; all others default to 1.

## 2) DF-Style Ramp Geometry (binding)

- `(x,y,z)` is the ramp base (TerrainKind=Ramp)
- `(x,y,z+1)` is `OpenNoFloor` (air); no separate slope top at z+1
- Standable top tiles are the 8 neighbors at z+1: `(x+dx,y+dy,z+1)` with `(dx,dy)` in {N,NE,E,SE,S,SW,W,NW}
- Directional permission is not authored in L0; it is derived during RebuildDerived into `UpRampMask[idx]` where bits 0..7 map to N..NW. A direction is allowed when:
  1) target `(x+dx,y+dy,z+1)` is standable (floor/stair top)
  2) top space `(x,y,z+1)` is `OpenNoFloor`
  3) high-side support (tunable): `(x+dx,y+dy,z)` provides support (e.g., wall)
  4) diagonal corner rule (tunable): when diagonals are allowed, require at least one adjacent orthogonal at z+1 also standable

Descending from top to ramp base is validated at runtime by checking the ramp base below/behind and its `UpRampMask`.

## 3) Derived Caches & Versions

Per chunk (32x32), rebuilt during commit phase:

- `NavMask[idx] : byte` — capability bits (Walk/Standable/Swim/Fly). Bits 6/7 may be used as “has ramp up/down connectivity” markers; not directional.
- `NavCost[idx] : ushort` — precomputed cost baseline (base + traffic + fluid + surface + doors)
- `UpRampMask[idx] : byte` — ramp ascend mask (0..255)
- `ConnectivityVersion : int` — bump on topology-relevant changes; consumers invalidate by version

Rebuild on L0/L2 topology edits; L3 threshold changes; L7 traffic changes affect cost only.

## 4) Layer Responsibilities (recap)

- L0 TerrainBits (v2): minimal geometry (`Kind`, `Natural`, `Modifiable`)
- L1 SurfaceBits: mud/grass/snow/moss (cost only)
- L2 Constructions: doors/walls/workshops (blockers/passables/state)
- L3 Fluids: kind/depth (block or cost)
- L4 Fields: hazards/LOS
- L7 Meta: revealed/traffic/polish (smoothed/engraved lives here)

## 5) Autotiling & Rotation (deterministic)

Autotiling resolves NESW masks based on L2 blocker first, then L0 wall/floor. Rotation chains are data-driven. Resolution and iteration orders are fixed.

## 6) Snapshot (immutable DTOs)

Renderer consumes chunk-local snapshots per visible Z. Draw order is stable: floor -> surface -> fluids -> constructions -> items -> fields -> units -> UI.

## 7) Read/Write Contracts

Read phase (thread-safe): query-only (no writes). Write phase: single writer per chunk; all writes via atomic element replacement or local-locked overlays. Commit phase: rebuild derived caches and snapshots.

## 8) Testing & Invariants

- TerrainBits v2 bit packing/unpacking tests
- `Kind` extraction uses 4-bit mask (0xF)
- `Natural` is bit 5; `Modifiable` is bit 6; others always 0
- DF ramp rules validated by mask derivation tests (support/corner)
- Cache invalidation fires on L0/L2 topology changes; L3 thresholds; L7 traffic costs
- Determinism: identical seeds → identical snapshots across OS/CPU/thread counts

## 9) Notes

- Slope is kept as a reserved visual value for legacy tilesets only; gameplay geometry no longer relies on it
- Ramp directions at runtime come solely from `UpRampMask` (no L0 direction bits)
