# Navigation Addendum: DF-Style Ramps (Vertical Alignment)

This addendum refines ramp semantics to match DF-style vertical alignment while keeping TerrainBits minimal and putting directional permission in derived navigation caches.

## Geometry

- Ramp resides at (x, y, z).
- The cell directly above (x, y, z+1) remains `OpenNoFloor` (empty space). No separate "slope top" geometry is placed.
- Standable top tiles are the 8 neighbors at z+1: `(x+dx, y+dy, z+1)`.
- Generation note: ramp detection/injection considers all 8 neighbors (N,NE,E,SE,S,SW,W,NW) when stamping the ramp base; the cell above the base is forced to `OpenNoFloor` to satisfy vertical alignment.

## Ascend/Descend Rules

Ascend from `(x, y, z)` to `(x+dx, y+dy, z+1)` when all of the following are true:

1) Target `(x+dx, y+dy, z+1)` is standable (OpenWithFloor or stair top).
2) Top space `(x, y, z+1)` is `OpenNoFloor`.
3) High-side support (tunable): the high side below `(x+dx, y+dy, z)` provides support (e.g., SolidWall). This can be enabled/disabled in `tuning.navigation.json`.

Descend from `(x+dx, y+dy, z+1)` to `(x, y, z)` when `(x, y, z)` is a ramp and the ramp’s derived mask allows that direction.

Diagonal constraints (recommended): when diagonals are allowed, apply a corner rule (e.g., at least one adjacent orthogonal also satisfies support/permission) to avoid illegal corner cuts.

## Derived Navigation Cache

Do not encode ramp direction or mask in TerrainBits. Instead, the per-chunk navigation cache contains:

- `UpRampMask[idx] : byte` — bits 0..7 (N,NE,E,SE,S,SW,W,NW) indicate allowed ascend directions for the ramp at local index `idx`.

`UpRampMask` is rebuilt during `RebuildDerived()` from L0/L2/L3/L1 according to the rules above. Pathfinding consumes `UpRampMask` to produce vertical neighbors. Downward moves can be validated at runtime or mirrored as needed.

## Tuning

`content/registries/tuning.navigation.json` keys (proposed):

- `ramp_vertical_alignment_mode: "df"` (default)
- `ramp_requires_highside_support: true|false`
- `allow_diagonals: true|false`, `cost.diagonal: 14`, `cost.orthogonal: 10`
- `diagonal_rules: { corner_check: true }`
- `surface_cost: { mud: 2, snow: 3, grass: 1, moss: 1 }`

## Debug Overlays (Implementation Guidance)

- `MovementCost` overlay may present fixed-point binned costs (e.g., ×10) for finer visual granularity while keeping integer tunables.
- `RampMask` overlay draws allowed ascend directions per ramp base (arrows for single-direction, `*` for multiple).

## Rendering

The renderer may show a visual slope indicator on the standable tiles at z+1 for readability. This indication is purely visual and does not affect pathfinding.
