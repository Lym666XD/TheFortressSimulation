# Tiles, Materials, Geology & Terrain Architecture (v2)

Updated: 2026-06-12
Status: current boundary summary

This is the active content/simulation boundary note for TerrainKind, geology
handles, material modifiers, and tile-layer responsibilities. It absorbs the
current parts of the older `MATERIALS_DATA_CONTRACT.md`.

## System Overview

- Registries: `terrain_kinds.json` (navigation legality), `materials.registry.json` (numeric modifiers), `geology.json` (prototype combos)
- Runtime tile stack L0..L7: L0 Terrain/Geology, L1 Surface, L2 Constructions, L3 Fluids, L4 Fields, L5 Items, L6 Units, L7 Meta/Traffic
- Derived caches per-chunk: NavMask/NavCost/ConnectivityVersion (+ UpRampMask for ramps)

## Data Contract

Terrain legality and material behavior are intentionally separate:

```text
TerrainKind
  -> geology prototype
  -> material numeric modifiers
  -> fluids/fields overlays
  -> actor movement profile
```

Rules:

- `TerrainKind` owns legality such as walkable, standable, ramp, stair, and open-space behavior.
- Materials must not define boolean movement legality such as walkable, standable, flyable, blocks movement, or blocks sight.
- Materials may provide numeric modifiers such as cost, friction, hazard, density, mass, durability, or processing difficulty.
- Geology combines a terrain kind with a material and optional tuning/display data.
- Runtime tiles store a geology/material handle plus compact terrain bits; pathfinding consumes derived navigation caches, not raw authoring JSON.

Current files:

- `content/registries/terrain_kinds.json`
- `content/registries/materials.authoring.json`
- `content/registries/materials.registry.json`
- `content/registries/geology.json`
- `content/schemas/terrain_kinds.schema.json`
- `content/schemas/material.authoring.schema.json`
- `content/schemas/materials.registry.schema.json`
- `content/schemas/geology.schema.json`

Current code still has transitional legacy and structured content registries.
Use [CONTENT_SYSTEM.md](CONTENT_SYSTEM.md) for loader ownership.

## Bit Layout Detail (Canonical v2)

```
TileBase (32 bits total):
+----------------------+----------------------+
|   GeoMatId (16)      |   TerrainBits (16)   |
+----------------------+----------------------+
TerrainBits:
  bits 0..3 : TerrainKind (0..15)
  bit 5     : Natural (1=natural, 0=constructed)
  bit 6     : Modifiable (1=player tools allowed; 0=forbidden)
  others    : Reserved (must be 0)
```

TerrainKind values (aligned to enums):
- 0: SolidWall      — blocks all, provides support
- 1: OpenWithFloor  — walkable/standable, provides support
- 2: OpenNoFloor    — empty space (flyable only), no support
- 3: Ramp           — DF‑style ramp base; directions derived at runtime via UpRampMask
- 4: Slope          — reserved (visual only; no longer authored as geometry)
- 5: StairsUp       — Z up
- 6: StairsDown     — Z down
- 7: StairsUD       — Z up/down

Notes:
- Ramp directions are not stored in TerrainBits; UpRampMask is rebuilt from topology (see ../simulation/NAVIGATION_SPEC.md).
- Smoothed/Engraved are not stored in TerrainBits；放到 L7（Meta）或 L2（构件）避免引发不必要的导航重建。

## DF‑Style Ramps

- `(x,y,z)` is the ramp base; `(x,y,z+1)` is `OpenNoFloor` (air).
- Standable top tiles are 8 neighbors at z+1. UpRampMask[idx] (bits 0..7 = N..NW) encodes which directions are allowed based on:
  1) target standable, 2) top space open, 3) high‑side support (tunable), 4) diagonal corner rule (tunable).
- Descend symmetry validated at runtime by checking the ramp base below/behind.

## Modifiable Flag Policy

- `IsModifiable=0` only for the bottommost Z (z==0) to prevent digging into the void.
- All other Z use `IsModifiable=1` by default；工具（dig/channel/buildwall/buildstair）以此为 gate。

## Layer Responsibilities

- L0 TerrainBits (v2): Kind + Natural + Modifiable（最小几何）
- L1 SurfaceBits: Mud/Grass/Snow/Moss（成本影响，不改变几何）
- L2 Constructions: doors/walls/workshops（Blocker/Passable/状态）
- L3 Fluids: kind/depth（阻断或成本）
- L4 Fields: fire/smoke/etc（危险/LOS）
- L7 Meta: revealed/traffic/polish（如 smoothed/engraved 等）

## Derived Caches & Update Order

- RebuildDerived after L0/L2 topology edits, L3 threshold changes, or L7 traffic changes（成本）。
- Pathfinding只读 NavMask/NavCost/UpRampMask；不直接读取 L0/L2 原始数据。
