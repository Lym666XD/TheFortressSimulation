id: navigation.v1
status: normative
owner: sim/navigation
last_updated: 2025-09-29

# Navigation Spec (Fortress Map)

This specification defines the fortress navigation model: data layout, ramp/stairs semantics, cost model, pathfinding API, and tuning. It is UTF‑8 encoded and supersedes older non‑UTF8 drafts.

The implementation is deterministic across OS/thread counts and cooperates with the fixed UPDATE_ORDER (read‑parallel, write‑serialized).

## 1) Goals & Non‑Goals

Goals
- Deterministic, grid‑based pathfinding on a single fortress map (32×32 tiles per chunk, multiple Z levels).
- Clear, data‑driven passability and cost semantics derived from the L0..L7 tile stack.
- Fast cache lookups via per‑chunk derived arrays with versioning.

Non‑Goals (v1)
- Any‑angle/continuous navigation.
- Global region graphs (may be added later as a layer on top).

## 2) Data Model (Authoritative)

Each chunk (32×32) maintains derived navigation caches rebuilt during RebuildDerived (commit phase):

- `NavMask[idx] : byte` – capability bits (Walk/Crawl/Swim/Fly/Standable/vertical links flags, etc.).
- `NavCost[idx] : ushort` – movement cost baseline including fluids/surface/traffic/doors.
- `UpRampMask[idx] : byte` – per‑ramp base ascend mask (bits 0..7 map to N..NW). 0 means no ramp ascend.
- `DownRampDir[idx] : byte` – for standable top tiles, 0..7 if a matching ramp exists below; 255 means none.
- `ConnectivityVersion : int` – bump when topology‑relevant data changes; consumers invalidate caches by version.

Rebuild triggers: L0/L2 edits; L3 fluid thresholds crossed; L7 traffic changes; never from renderer.

## 3) Passability Semantics (L0→L7 precedence)

Evaluation order is deterministic: L0/L2 → L3 → L4 → L5/L6. Earlier blockers preempt later layers.

### 3.1 Capability Bits (example layout)

```
bit0 Walk        // planar motion
bit1 Crawl       // reserved (tight tunnels)
bit2 Swim        // fluid tolerance
bit3 Fly         // airborne
bit4 Standable   // valid stop for walkers
bit5 EdgeClimb   // ladders/rope (vertical)
bit6 HasRampUp   // derived: this cell can ascend to z+1 via ramp (any direction)
bit7 HasRampDown // derived: this cell can descend to z-1 via ramp behind
```

Doors: closed → L2 blocker; open → contributes Walk with a cost delta.

### 3.2 Walk / Standable

Walk if L0 is `OpenWithFloor` or `Stairs*`, no L2 hard blocker, and fluid rules allow it. Standable is Walk plus “not a down‑ramp unless approaching from the proper direction”.

### 3.3 Ramps & Stairs — DF‑Style Vertical Alignment

Ramp geometry follows DF‑style vertical alignment (see NAVIGATION_RAMP_ADDENDUM.md):

- The ramp resides at `(x,y,z)`.
- The cell directly above `(x,y,z+1)` is `OpenNoFloor` (empty space). No slope geometry is placed at z+1.
- Standable top tiles are the 8 neighbors at z+1: `(x+dx, y+dy, z+1)`, where `(dx,dy)` ∈ {N,NE,E,SE,S,SW,W,NW}.
- Allowed ascend directions are derived into `UpRampMask[idx]` (bits 0..7) during RebuildDerived using:
  1) Target `(x+dx,y+dy,z+1)` must be Standable (floor or stair top);
  2) Top space `(x,y,z+1)` must be `OpenNoFloor`;
  3) High‑side support (tunable): tile `(x+dx,y+dy,z)` should provide support when enabled;
  4) Diagonal corner rule (tunable): when diagonals are allowed, at least one adjacent orthogonal at z+1 must also be Standable.

Neighbor expansion uses `UpRampMask` to add vertical neighbors (ramp base → top). Descend from a top tile to the ramp base may be mirrored via a cached `DownRampDir` or validated at runtime by checking the ramp below/behind.

StairsUp/Down/UD: add vertical neighbors as usual.

## 4) Cost Model (Normative)

### 4.1 Base Cost (per‑tile)

`NavCost[idx] = Base + TrafficAdj + FluidAdj + SurfaceAdj + DoorAdj`

All constants come from `/content/registries/tuning.navigation.json`.

### 4.2 Actor Modulation (optional)

At search time engines may multiply step costs by an actor move multiplier (encumbrance/speed). This spec does not mandate actor fields; implementations must remain deterministic.

### 4.3 Edge Weights

- Orthogonal step weight = 10
- Diagonal (if enabled) = 14 (≈ √2×10)
- Ramp adds `+RAMP_DELTA` to the chosen edge weight (orthogonal/diagonal) for vertical motion
- Stairs add `+STAIR_DELTA` (direction‑agnostic)

Implementation note: engines may use fixed‑point scaling internally (e.g., ×10) to increase granularity while keeping integer tunables. Public semantics remain as above.

Default is 4‑neighbor; `allow_diagonals=true` enables 8‑neighbor with a corner rule per tuning.

## 5) Pathfinding Service

### 5.1 Deterministic A*

Use Manhattan/Octile heuristic; open set keyed by `(f,h,g,localIdx)` with stable tie‑breakers:
1) smaller `f=g+h`, 2) smaller `h`, 3) smaller `g`, 4) smaller `LocalIndex` (row‑major 0..1023).

Node/time budgets enforce fail‑soft behavior (return `Partial` with best frontier when limits are hit). All iteration orders are deterministic.

### 5.2 Connectivity & Cache

Caches are chunk‑local. `ConnectivityVersion` bumps invalidate path cache entries that include those chunks. Region labeling can be added later; v1 relies on A* with caps.

## 6) API (Normative)

```csharp
public enum PathResultKind { Found, Partial, NoPath, Invalid }

public readonly record struct PathRequest(
  Point3 Source,
  Point3 Destination,
  MoveMode Mode,
  PathFlags Flags,
  uint Seed);

public readonly record struct PathNode(Point3 Position, ushort Cost);

public readonly record struct Path(
  PathResultKind Kind,
  int Length,
  uint TotalCost,   // Fixed‑point total if engine uses scaling; otherwise 0
  uint Hash,
  ReadOnlyMemory<PathNode> Steps);

public interface IPathService {
  Path Solve(in PathRequest req, in IWorldNavigationView world); // sync in read‑phase
  void BeginTick();
  void ProcessQueuedRequests(IWorldNavigationView world);
}
```

`IWorldNavigationView` exposes read‑only queries for `IsValid`, `GetCapabilities`, `GetCost`, `HasStairsUp/Down`, `TryGetUpRampMask`, `TryGetDownRampDirection`, and `GetConnectivityVersion`.

## 7) Tuning (Data‑Driven)

`/content/registries/tuning.navigation.json` (example):

```json
{
  "allow_diagonals": true,
  "ramp_vertical_alignment_mode": "df",
  "ramp_requires_highside_support": true,
  "diagonal_rules": { "corner_check": true },
  "cost": { "base": 10, "orthogonal": 10, "diagonal": 14, "ramp_delta": 6, "stair_delta": 8 },
  "fluids": { "shallow_threshold": 1, "deep_threshold": 6, "wade_cost": 6, "swim_cost": 18 },
  "traffic": { "low": -2, "normal": 0, "high": 2, "restricted": 8 },
  "doors": { "closed_blocks": true, "open_cost": 4 },
  "budgets": { "max_nodes_per_search": 10000, "max_ms_per_tick_pathing": 3 },
  "surface_cost": { "mud": 2, "snow": 3, "grass": 1, "moss": 1 }
}
```

## 8) Debug & Visualization (Non‑Normative Guidance)

- `MovementCost` overlay may display fixed‑point binned costs (e.g., ×10) for finer granularity.
- `RampMask` overlay draws allowed ascend directions per ramp base (arrows for single direction, `*` for multiple).
- `PathDisplay` overlay shows S/Path/G with step arrows; UI may also display `len` and total `cost` (scaled).

## 9) Determinism & LOD Cooperation

- All reads happen in the read phase; no writes during path solving.
- LOD service must Pin/Promote sleeping chunks before pathing across them (or return `Invalid`).
- Replay gates in CI verify identical results across OS/CPU/thread counts for golden seeds.

