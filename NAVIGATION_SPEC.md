id: navigation.v1
status: normative
owner: sim/navigation
last_updated: 2025-09-15
applies_to:
  - Nav caches (NavMask/NavCost), connectivity versioning
  - Pathfinding service (requests, caching, A* search, tie-breakers)
  - Movement execution (walk/ramps/stairs/fluids/doors) contracts
principles:
  - Deterministic across OS/thread counts (stable orders, no wall-clock)
  - Read-parallel / write-serialized cooperation with UPDATE_ORDER
  - Cache-friendly hot data, sparse cold data
1) Goals & Non-Goals
Goals

Single-map fortress navigation with deterministic results for identical inputs.

Clear passability semantics from the L0–L7 tile stack (terrain→meta). 
TILE_LAYERS


Cache-friendly lookups via per-chunk NavMask[] and NavCost[], invalidated by ConnectivityVersion. 
DATA_LAYOUT


Non-Goals (v1)

Any-angle/continuous navigation (grid only).

Complex dynamic avoidance (execution does local yielding; pathing is quasi-static).

Global region graph (optional P1; v1 uses chunk-local caches + cheap checks).

2) Data Model (Authoritative)
Each chunk (32×32) maintains derived navigation caches rebuilt during RebuildDerived (commit phase):

NavMask[idx] : byte/ushort — capability bits (Walk/Crawl/Swim/Fly/Standable/EdgeClimb, etc.).

NavCost[idx] : ushort — movement cost baseline including fluids/surface/traffic.

ConnectivityVersion : int — bump when topology-relevant data changes; consumers invalidate caches by version. 
DATA_LAYOUT

 
UPDATE_ORDER


When rebuilt: after L0/L2 edits; after L3 depth/kind if thresholds crossed; after L4 opacity (LOS only) or L7 traffic changes; never from the renderer. 
DATA_LAYOUT


3) Passability Semantics (L0→L7 precedence)
When computing passability and cost, layers are consulted in this order (deterministic): L0/L2 → L3 → L4 → L5/L6. Hard blockers in earlier layers preempt later ones. 
TILE_LAYERS


3.1 Capabilities (bit layout)
NavMask packs capability bits (exact enum is binding for v1):

cpp
Copy code
bit0 Walk        // 4-neighbor planar motion
bit1 Crawl       // reserved (tight tunnels)
bit2 Swim        // allow motion when FluidDepth >= threshold
bit3 Fly         // airborne (ignores many L0/L2 tests)
bit4 Standable   // valid “stop” tile for walkers
bit5 EdgeClimb   // ladders/rope (treated as vertical neighbor)
bits6..15 Reserved
A door closed acts as L2 Blocker (no Walk); open contributes Walk with extra cost. Buildables supply these flags via L2 furniture metadata.

3.2 Walk / Standable
A cell is Walk if:

L0 is OpenWithFloor or Stairs*, and no L2 Blocker with BlocksMove, and (if FluidDepth > shallow_threshold) the actor either has Swim or the fluid policy permits wading.
Standable is Walk plus “not a ramp down edge unless coming from proper direction”.

3.3 Ramps & Stairs (vertical neighbors)
Ramp: Walk from (x,y,z) to (x+dx,y+dy,z+dz) when RampDir matches and target is OpenWithFloor (or stair top).

StairsUp/Down/UD: add vertical neighbors accordingly.

Vertical steps use dedicated edge costs (see §4.3).

3.4 Fluids (L3)
Single dominant fluid per tile, depth 0..7. Depth contributes to cost or blocks if above actor tolerance. 
TILE_LAYERS


3.5 Fields (L4)
Do not block walk; may add hazard cost (smoke/miasma/fire) read from tuning. (LOS handled elsewhere.)

3.6 Items (L5) & Units (L6)
Items never block unless item prototype sets BulkyBlocksMove.

Units are runtime occupiers; pathing treats them as passable (execution handles yielding). 
TILE_LAYERS


4) Cost Model (Normative)
4.1 Base cost
NavCost[idx] is precomputed:

cpp
Copy code
NavCost = Base10
        + TrafficAdj(Meta.TrafficMask)             // L7
        + FluidAdj(FluidKind, FluidDepth)          // L3
        + SurfaceAdj(SurfaceBits)                  // L1
        + DoorAdj(L2 door state)                   // L2
TrafficAdj: Normal=0, Low=-2, High=+2, Restricted=+8 (clamped ≥1).

All constants come from /content/registries/tuning.navigation.json (data-driven).

4.2 Actor-specific modulation
At search time, per-actor encumbrance/speed multiplies step costs:

ini
Copy code
StepCost = EdgeWeight * (NavCost[start] + NavCost[dest]) / 2 * Actor.MoveMultiplier
MoveMultiplier = 1 + EncumbranceK * Encumbrance (encumbrance from equipment spec), clamped. (Damage/needs modifiers may alter this later.)

4.3 Edge weights
Orthogonal step weight = 10.

Diagonal (if enabled) = 14 (approx √2*10).

Ramp adds +RAMP_DELTA to cost when moving between Z.

Stairs uses +STAIR_DELTA and ignores ramp direction.

Default v1 uses 4-neighbor (no diagonals). A tuning flag allow_diagonals enables 8-neighbor with no corner-cutting (both adjacent orthos must be Walk).

5) Pathfinding Service
5.1 Deterministic A* (binding)
A* over the grid using Manhattan heuristic for 4-neighbor or octile for 8-neighbor; never admissible violations.

Tie-breakers (stable):

Smaller f = g + h

Smaller h

Smaller g

Smaller LocalIndex (row-major; 0..1023)

Open set is a binary heap keyed by (f,h,g,idx) in that order; no HashSet iteration dependence. The service runs in the read phase and consumes NavMask/NavCost only. Architectural path/caching integration matches the prior diagrams. 
GAME_ARCHITECTURE

 
GAME_ARCHITECTURE


5.2 Node limits & fail-soft
Hard cap max_nodes_per_search. If exceeded, return Partial with the best frontier node (lowest f), plus a reason NodeCapHit. Job/AI can step toward frontier and re-issue next tick.

Time budget per tick: max_ms_per_tick_pathing; the service processes requests FIFO until the budget is exhausted; remaining requests roll to next tick (determinism preserved by stable queue order).

5.3 Connectivity quick-reject
If source and goal chunks differ in ConnectivityVersion history during planning, the service falls back to A* (v1). P1 may add per-mode RegionId labeling to early-out “disconnected” queries; for now, we rely on A* with closed-set size cap.

5.4 API (normative)
csharp
Copy code
public enum PathResultKind { Found, Partial, NoPath, Invalid }

public readonly record struct PathRequest(
  ChunkKey SrcCk, int SrcIdx,
  ChunkKey DstCk, int DstIdx,
  MoveMode Mode, PathFlags Flags, uint Seed);

public readonly record struct Path(
  PathResultKind Kind, int Length, uint Hash,
  ReadOnlyMemory<(ChunkKey ck, int idx)> Steps);

public interface IPathService {
  Path Solve(in PathRequest req, in WorldSnapshot snap); // sync in read-phase
}
MoveMode: Walk/Crawl/Swim/Fly.

Flags: AvoidHazard, PreferRoad (traffic Low), AllowDoors, etc.

Hash helps cache & determinism CI.

Cache: LRU keyed by (srcCk,srcIdx,dstCk,dstIdx,mode,flags,ConnVerHash); invalid on any involved chunk version bump. (Cache wiring shown in architecture docs.) 
GAME_ARCHITECTURE


6) Neighbor Expansion (exact rules)
Given (ck, idx):

Produce 4 neighbors (N,E,S,W). If allow_diagonals, also produce (NE,SE,SW,NW) with no corner-cutting.

For each neighbor:

Read NavMask of destination; require Walk (or Swim if Mode=Swim) and Standable if the neighbor may be a stop.

If ramp edge, validate RampDir.

If stairs, add vertical neighbor(s) and apply stair cost.

If door and Flags.AllowDoors, accept with DoorAdj; otherwise treat as blocker.

Reject if FluidDepth exceeds actor tolerance and mode is not Swim.

All reads are from chunk-local caches (no overlay traversal on the hot path). Derived caches are built earlier in the update order. 
UPDATE_ORDER


7) Execution & Replanning
Movement execution samples the next 1–3 steps from the path each tick.

If execution detects mask change (ConnectivityVersion bump on any step): mark path stale, request replan.

If blocked by units (L6), execution uses local yielding / brief wait; path is not recomputed unless stale for topology reasons.

The AI/job flow already includes “Replan Path” on block/timeout. 
GAME_ARCHITECTURE


8) LOD Cooperation (hard rule)
Path solving is intended for active (L0/L1) zones.

If source or destination chunk is L2+, either:
(a) the caller asks the LOD service to Pin/Promote the target chunks first, or
(b) the path service returns Invalid with reason TargetZoneSleeping.
This mirrors the director/incident LOD cooperation and avoids heavy work in sleeping zones. 
DIRECTOR_SPEC


9) Serialization & Save
NavMask/NavCost and any in-memory path caches are derived and not serialized; rebuild after load.

The only persisted inputs are the tile stack and designations; commit sweep after load rebuilds Derived and Snapshot. 
DATA_LAYOUT

 
MAPGEN_X_TILES


10) Tuning (data-driven)
/content/registries/tuning.navigation.json (example)

json
Copy code
{
  "allow_diagonals": false,
  "orth_cost": 10,
  "diag_cost": 14,
  "ramp_delta": 6,
  "stair_delta": 8,
  "fluid": { "shallow": 1, "deep_block_threshold": 6, "wade_cost": 6, "swim_cost": 18 },
  "traffic": { "low": -2, "normal": 0, "high": 2, "restricted": 8 },
  "doors": { "open_extra": 4, "closed_blocks": true },
  "hazard_weights": { "smoke": 2, "miasma": 4, "flame": 12 },
  "max_nodes_per_search": 10000,
  "max_ms_per_tick_pathing": 3
}
11) Determinism & CI Gates
Stable iteration orders, stable heap ordering, no dependence on dictionary order.

Repeated seeds & same world state → identical path Hash.

Cross-thread parity: caches guarded only by ConnectivityVersion.

CI scenario suite: walls toggling mid-run, floods raising depths, door spam; Found/Partial/NoPath hashes must match across runs. (Update order & barrier model is already specified.) 
UPDATE_ORDER


12) Tests (must-haves)
Semantics: every L0/L2/L3 combination → expected NavMask/NavCost.

Ramps/Stairs: directed ramps accept only correct approach; stairs up/down behave as vertical neighbors.

Fluids: shallow adds cost; deep blocks unless Swim; Swim uses swim_cost.

Doors: closed blocks; open adds cost when allowed.

A*: synthetic mazes verify node caps, tie-breakers, and determinism.

Caching: version bump invalidates cache; no stale reuse.

LOD: sleeping zones return Invalid unless pinned.

13) Extension Points (v2)
RegionId component labeling & global region graph for instant disconnect tests.

Flow fields for repeated goals (stockpiles, main halls).

Size-aware navigation (2×1 creatures), ladders/ropes as explicit edge records.

Per-actor hazard sensitivity and threat maps from the Incident Director.

Appendix A — Where this hooks into the engine
Data & caches live in chunk (NavMask/NavCost/ConnectivityVersion). 
TILE_CSHARP_SKELETON


RebuildDerived updates them during the commit pipeline. 
UPDATE_ORDER


Pathfinding sits in the AI/Job flow with request→cache→A*→result, as diagrammed in the architecture docs. 
GAME_ARCHITECTURE

 
GAME_ARCHITECTURE