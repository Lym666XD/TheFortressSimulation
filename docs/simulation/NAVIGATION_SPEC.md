id: navigation.v1
status: normative
owner: sim/navigation
last_updated: 2026-05-24

# Navigation Spec (Fortress Map)

This specification defines the fortress navigation model: data layout, ramp/stairs semantics, cost model, pathfinding API, and tuning. It is UTF-8 encoded and supersedes older non-UTF8 drafts.

The implementation is deterministic across OS/thread counts and cooperates with the fixed UPDATE_ORDER. The current coarse scheduler uses deterministic registered-system read order plus serialized writes; future chunk-partitioned read parallelism must preserve the same replay hashes.

Current implementation note (2026-06-12):

- `HumanFortress.Navigation` owns pathfinding, path-service budgets, path cache behavior, and per-chunk navigation data.
- `HumanFortress.Navigation` does not directly depend on `HumanFortress.Simulation`.
- `HumanFortress.Runtime` supplies the Simulation-backed navigation source through `SimulationNavigationSource` / `SimulationNavigationFactory`.
- Runtime post-tick applies terrain/entity diffs before rebuilding dirty navigation chunks.
- `NavigationManager.GetNavDataAt(...)` does not rebuild chunks on query; derived data is rebuilt through explicit rebuild calls.
- The older active `NAVIGATION_DESIGN.md` and `NAVIGATION_RAMP_ADDENDUM.md` documents have been merged into this spec and archived.

## 1) Goals & Non-Goals

Goals
- Deterministic, grid-based pathfinding on a single fortress map (32x32 tiles per chunk, multiple Z levels).
- Clear, data-driven passability and cost semantics derived from the L0..L7 tile stack.
- Fast cache lookups via per-chunk derived arrays with versioning.
- A navigation architecture that can grow from single-tile walking actors to varied movement profiles such as swimming, flying, large creatures, and vehicles.
- Support for future high-scale movement patterns without compromising replay determinism.

Non-Goals (v1)
- Any-angle/continuous navigation as the primary representation.
- Replacing deterministic grid search with physics-driven movement.
- Full optimal multi-agent pathfinding for every actor.
- Hard-coding one algorithm as the only legal navigation strategy.

Future layers may add region graphs, flow/vector fields, local avoidance, or specialized grid optimizations above the same authoritative navigation data.

## 2) Data Model (Authoritative)

Each chunk (32x32) maintains derived navigation caches rebuilt during RebuildDerived (commit phase):

- `NavMask[idx] : byte` - capability bits (Walk/Crawl/Swim/Fly/Standable/vertical links flags, etc.).
- `NavCost[idx] : ushort` - movement cost baseline including fluids/surface/traffic/doors.
- `UpRampMask[idx] : byte` - per-ramp base ascend mask (bits 0..7 map to N..NW). 0 means no ramp ascend.
- `DownRampDir[idx] : byte` - for standable top tiles, 0..7 if a matching ramp exists below; 255 means none.
- `ConnectivityVersion : int` - bump when topology-relevant data changes; consumers invalidate caches by version.

Rebuild triggers: L0/L2 edits; L3 fluid thresholds crossed; L7 traffic changes; never from renderer.

The chunk data describes the world, not a specific actor. Actor-specific behavior is applied later through movement profiles, clearance checks, and cost modulation.

### 2.1 Implementation Ownership

Current ownership is split by dependency direction:

- Navigation project: derived chunk data, path service, cache invalidation, deterministic A* behavior, and navigation contracts.
- Runtime project: adapts Simulation world data to navigation source contracts and triggers dirty-chunk rebuilds after diff application.
- Simulation project: owns authoritative world/tile/item/creature state and marks chunks dirty when topology-relevant data changes.
- App project: composes the concrete navigation manager and exposes debug/runtime access while the runtime boundary is still migrating.

Path solving should run against stable read-phase inputs. Authoritative world mutation should happen through runtime command targets, job executors, diff applicators, or explicit commit points, not from pathfinding code.

## 3) Passability Semantics (L0->L7 precedence)

Evaluation order is deterministic: L0/L2 -> L3 -> L4 -> L5/L6. Earlier blockers preempt later layers.

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

Doors: closed -> L2 blocker; open -> contributes Walk with a cost delta.

### 3.2 Walk / Standable

Walk if L0 is `OpenWithFloor` or `Stairs*`, no L2 hard blocker, and fluid rules allow it. Standable is Walk plus “not a down-ramp unless approaching from the proper direction”.

### 3.3 Swimming, Flying, and Other Movement Media

The base `NavMask` may expose broad capability bits such as Swim or Fly, but exact actor semantics are intentionally left to movement profiles.

Examples of profile-level decisions include:
- whether shallow water is walkable, costly, preferred, or blocked;
- whether deep water is required, optional, or forbidden;
- whether flying actors need open air volume, ceiling clearance, or landing tiles;
- whether vertical movement is free, costly, or restricted to links such as stairs, ramps, ladders, or open space;
- whether a creature can switch between media, such as walking and swimming.

The pathfinder must remain deterministic regardless of which movement profile is used.

### 3.4 Ramps & Stairs — DF-Style Vertical Alignment

Ramp geometry follows DF-style vertical alignment:

- The ramp resides at `(x,y,z)`.
- The cell directly above `(x,y,z+1)` is `OpenNoFloor` (empty space). No slope geometry is placed at z+1.
- Standable top tiles are the 8 neighbors at z+1: `(x+dx, y+dy, z+1)`, where `(dx,dy)` in {N,NE,E,SE,S,SW,W,NW}.
- Allowed ascend directions are derived into `UpRampMask[idx]` (bits 0..7) during RebuildDerived using:
  1) Target `(x+dx,y+dy,z+1)` must be Standable (floor or stair top);
  2) Top space `(x,y,z+1)` must be `OpenNoFloor`;
  3) High-side support (tunable): tile `(x+dx,y+dy,z)` should provide support when enabled;
  4) Diagonal corner rule (tunable): when diagonals are allowed, at least one adjacent orthogonal at z+1 must also be Standable.

Neighbor expansion uses `UpRampMask` to add vertical neighbors (ramp base -> top). Descend from a top tile to the ramp base may be mirrored via a cached `DownRampDir` or validated at runtime by checking the ramp below/behind.

StairsUp/Down/UD: add vertical neighbors as usual.

Rendering may draw a visual slope cue near ramp tops for readability. That cue is purely visual and must not become pathfinding authority.

### 3.5 Footprints and Clearance

The authoritative tile data is single-cell, but not all actors are single-cell. Large creatures, carts, boats, and vehicles may require footprint or volume clearance.

Implementations may represent this through a movement profile or clearance provider. The important rule is that passability for a multi-cell actor is evaluated against the occupied footprint, and movement may also need to account for the swept space between source and destination.

Orientation may be part of the navigation state for actors that cannot rotate freely. This specification does not mandate the exact state representation.

## 4) Cost Model (Normative)

### 4.1 Base Cost (per-tile)

`NavCost[idx] = Base + TrafficAdj + FluidAdj + SurfaceAdj + DoorAdj`

All constants come from `/content/registries/tuning.navigation.json`.

### 4.2 Actor Modulation (optional)

At search time engines may multiply or adjust step costs by an actor move multiplier, encumbrance, terrain preference, medium preference, size, or movement profile. This spec does not mandate actor fields; implementations must remain deterministic.

### 4.3 Edge Weights

- Orthogonal step weight = 10
- Diagonal (if enabled) = 14 (approx. sqrt(2)x10)
- Ramp adds `+RAMP_DELTA` to the chosen edge weight (orthogonal/diagonal) for vertical motion
- Stairs add `+STAIR_DELTA` (direction-agnostic)

Implementation note: engines may use fixed-point scaling internally (e.g., x10) to increase granularity while keeping integer tunables. Public semantics remain as above.

Default is 4-neighbor; `allow_diagonals=true` enables 8-neighbor with a corner rule per tuning.

### 4.4 Traffic and Congestion

Traffic cost is a static or slowly changing preference layer, not a complete multi-agent collision system. Dynamic occupancy, crowding, and reservations should be handled by movement or scheduling systems that consume navigation results.

## 5) Pathfinding Service

### 5.1 Deterministic A*

Use Manhattan/Octile heuristic; open set keyed by `(f,h,g,localIdx)` with stable tie-breakers:
1) smaller `f=g+h`, 2) smaller `h`, 3) smaller `g`, 4) smaller `LocalIndex` (row-major 0..1023).

Node/time budgets enforce fail-soft behavior (return `Partial` with best frontier when limits are hit). All iteration orders are deterministic.

A* remains the baseline general-purpose search because it handles weighted costs, dynamic terrain, multiple Z levels, and actor-specific passability.

### 5.2 Connectivity & Cache

Caches are chunk-local. `ConnectivityVersion` bumps invalidate path cache entries that include those chunks. Region labeling can be added later; v1 relies on A* with caps.

Future cache implementations may store traversed chunks, route corridor versions, or region versions if paths cross multiple chunks. Any such mechanism must preserve deterministic lookup behavior.

### 5.3 Optional Search Layers

The navigation service may support additional strategies when they are a better fit than direct tile A*:

- Hierarchical search: region, room, portal, or chunk-level graphs for long-distance movement.
- Flow/vector fields: shared fields for many actors moving toward the same destination or area.
- Specialized grid accelerators: optimizations such as jump-style search may be used for suitable uniform regions.
- Local refinement: short tile-level searches near the source, destination, or current blockage.

These layers are optional and must fall back to the authoritative passability and cost model. They should not create a second source of truth.

### 5.4 Multi-Agent Movement

Pathfinding produces candidate routes. It does not by itself guarantee that multiple actors can execute those routes without conflict.

Dynamic conflicts such as two actors entering the same tile, head-on swaps, doorway contention, or multi-cell footprint overlap should be resolved by a deterministic movement or reservation layer.

The reservation layer may consider:
- actor priority;
- job ownership;
- path age;
- tile or edge occupancy;
- footprint and swept-volume occupancy;
- temporary waiting, yielding, or replanning.

This specification intentionally does not mandate one multi-agent pathfinding algorithm for all cases.

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
  uint TotalCost,   // Fixed-point total if engine uses scaling; otherwise 0
  uint Hash,
  ReadOnlyMemory<PathNode> Steps);

public interface IPathService {
  Path Solve(in PathRequest req, in IWorldNavigationView world); // sync in read-phase
  void BeginTick();
  void ProcessQueuedRequests(IWorldNavigationView world);
}
```

`IWorldNavigationView` exposes read-only queries for `IsValid`, `GetCapabilities`, `GetCost`, `HasStairsUp/Down`, `TryGetUpRampMask`, `TryGetDownRampDirection`, and `GetConnectivityVersion`.

Future APIs may accept richer movement profiles, footprint descriptors, or query contexts. Existing simple requests should remain valid for ordinary single-cell walking actors.

## 7) Tuning (Data-Driven)

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

Additional tuning groups may be introduced for movement profiles, flow fields, local avoidance, reservation windows, or hierarchical graph refresh policies. Keep tuning data deterministic and content-driven.

## 8) Debug & Visualization (Non-Normative Guidance)

- `MovementCost` overlay may display fixed-point binned costs (e.g., x10) for finer granularity.
- `RampMask` overlay draws allowed ascend directions per ramp base (arrows for single direction, `*` for multiple).
- `PathDisplay` overlay shows S/Path/G with step arrows; UI may also display `len` and total `cost` (scaled).
- Future overlays may show regions, portals, flow vectors, reservations, occupied footprints, or blocked replans.

## 9) Determinism & LOD Cooperation

- All reads happen in the read phase; no writes during path solving.
- LOD service must Pin/Promote sleeping chunks before pathing across them (or return `Invalid`).
- Replay gates in CI verify identical results across OS/CPU/thread counts for golden seeds.
- Parallel search is allowed only when its inputs, ordering, outputs, and commit points remain deterministic.
- Movement and job systems should resolve conflicts through explicit deterministic ordering rather than thread scheduling order.
