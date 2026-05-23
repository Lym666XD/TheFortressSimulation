# Navigation System Design

## Architecture Overview

The navigation system follows the specifications in `NAVIGATION_SPEC.md`. The design goal is to keep the core deterministic and data-driven while allowing higher-level movement systems to evolve over time.

The baseline implementation is grid/chunk based. It should remain suitable for ordinary single-cell walking actors, but the surrounding architecture should not assume that all actors are identical, ground-bound, or collision-free.

## Core Components

### 1. Navigation Data (Per Chunk)

Per-chunk navigation data is derived from authoritative map data during the rebuild/commit stage. It should remain read-only during path solving.

```csharp
// Illustrative only. The exact shape may evolve.
public class ChunkNavData
{
    public byte[] NavMask;            // Capability bits per tile (Walk/Swim/Fly/etc.)
    public ushort[] NavCost;          // Movement costs per tile
    public int ConnectivityVersion;   // Topology change tracking
}
```

The chunk data represents the world, not a specific actor. Actor-specific movement rules should be layered on top.

### 2. Navigation Capabilities

Navigation capabilities describe broad tile affordances such as walking, swimming, flying, standing, or vertical links. They should be treated as inputs to movement-profile evaluation rather than final pass/fail answers for every actor.

```csharp
[Flags]
public enum NavCapability : byte
{
    None = 0,
    Walk = 1 << 0,
    Crawl = 1 << 1,
    Swim = 1 << 2,
    Fly = 1 << 3,
    Standable = 1 << 4,
    EdgeClimb = 1 << 5,
}
```

### 3. Path Service Interface

The path service owns path queries, budgets, caching, and deterministic search behavior. The current request form is intentionally simple and remains appropriate for common single-cell actors.

```csharp
public interface IPathService
{
    Path Solve(in PathRequest request, in WorldSnapshot snapshot);
}
```

Future versions may accept richer query context, but they should preserve compatibility with simple requests where practical.

### 4. Deterministic A* Baseline

A deterministic weighted A* remains the general-purpose baseline because it can handle:

- non-uniform tile costs;
- terrain changes;
- multi-Z movement;
- ramps and stairs;
- profile-specific passability;
- replay verification.

Implementation expectations:

- deterministic tie-breakers;
- stable neighbor ordering;
- integer or fixed-point costs;
- node and time budgets;
- fail-soft behavior when limits are reached.

A* should be kept as a clear, dependable fallback even if additional search layers are introduced.

### 5. Path Caching

Path caching should reduce repeated work without becoming a second source of truth.

Baseline cache inputs:

- source and destination;
- movement mode or profile key;
- flags;
- topology/connectivity versions.

Future cache implementations may track route corridors, traversed chunks, region versions, or profile-specific constraints. Cache validation should remain deterministic and conservative.

## Extension Strategy

The navigation system should support multiple search strategies selected by context. No single algorithm needs to solve every movement problem.

### Hierarchical Search

Large maps should eventually benefit from a higher-level graph over chunks, rooms, regions, portals, roads, or other stable connectivity features. Tile-level A* can then be reserved for local refinement.

This is expected to be more important than simply adding more worker threads to raw tile search.

### Flow / Vector Fields

Flow or vector fields are appropriate when many actors move toward the same destination, region, or tactical objective. They are not a replacement for individual job pathfinding.

Good candidates include:

- large enemy waves;
- evacuation or panic movement;
- crowd movement toward a shared zone;
- repeated traffic toward a common facility.

Flow fields should be derived from the authoritative cost/passability model and invalidated when relevant topology changes.

### Specialized Grid Accelerators

Specialized grid accelerators, including jump-style search, may be useful in suitable flat, uniform, or low-variance regions. They should be treated as optional optimizations, not as the default source of truth.

When a region has weighted costs, vertical links, unusual actor profiles, dynamic blockers, or large footprints, the system should be able to fall back to the general deterministic search path.

### Movement Profiles

Actor movement should be described by profile-like data rather than hard-coded assumptions inside the search algorithm.

A movement profile may influence:

- allowed movement media, such as ground, water, or air;
- terrain preferences and cost modifiers;
- vertical movement permissions;
- required clearance;
- footprint or volume size;
- whether orientation matters;
- whether a destination must be standable, swimmable, landable, or otherwise valid.

The exact representation is intentionally left open.

### Flying and Aquatic Movement

Flying and aquatic actors should not require separate world data sources. They should reinterpret the same authoritative map through movement-profile rules.

Examples of design questions to resolve in implementation:

- whether flying actors need clear air volume or landing tiles;
- whether aquatic actors require a minimum water depth;
- whether amphibious actors may switch movement media;
- whether vertical movement is free, costly, or link-based;
- how profile-specific costs interact with terrain and traffic.

### Large Actors and Vehicles

Large creatures and vehicles require footprint or volume clearance. Their navigation state may include orientation if turning is constrained.

The pathfinder should be able to ask whether a candidate move is valid for the actor footprint instead of assuming a single occupied tile.

Collision and reservation systems may also need to reserve target footprints and swept movement space.

### Multi-Agent Movement and Reservations

Pathfinding should produce candidate routes. It should not be responsible for all dynamic multi-agent conflicts.

A separate deterministic movement or reservation layer should resolve:

- tile occupancy conflicts;
- head-on swaps;
- doorway contention;
- temporary blockers;
- multi-cell footprint overlap;
- job-site or item contention.

The reservation layer should use explicit deterministic ordering. It should not rely on thread scheduling order.

## Integration Points

### Read Phase (Parallel)

Path solving may run during AI/job read phases if all inputs are immutable for the duration of the phase.

Allowed work:

- compute candidate paths;
- evaluate reachability;
- score jobs by travel cost;
- propose movement intents.

### Resolve / Write Phase (Serialized)

State changes should be applied through deterministic commit points.

Typical work:

- accept or reject movement intents;
- apply reservations;
- assign jobs;
- move actors;
- rebuild derived navigation data after topology changes.

No pathfinding worker should directly mutate authoritative world state.

## Implementation Phases

### Phase 1: Core Data Structures
1. Navigation capability representation.
2. Per-chunk navigation masks and costs.
3. Path request/result structures.

### Phase 2: Navigation Mask Building
1. Extract base walkability from terrain.
2. Apply furniture and blocker layers.
3. Apply fluid thresholds and surface costs.
4. Rebuild derived vertical links.

### Phase 3: Deterministic Baseline Search
1. Stable open-set ordering.
2. Deterministic neighbor expansion.
3. Path reconstruction and hashing.
4. Node/time limits.

### Phase 4: Caching and Profiling
1. Conservative path cache.
2. Connectivity-version tracking.
3. Debug statistics and performance profiling.

### Phase 5: Movement Execution
1. Basic path following.
2. Blocked/stuck detection.
3. Replan requests.
4. Deterministic movement intent handling.

### Phase 6: Scalable Navigation Extensions
1. Region or portal graph exploration.
2. Shared flow/vector field experiments.
3. Movement-profile expansion for air, water, and larger actors.
4. Reservation-aware movement and job interaction.

## Testing Guidance

Tests should emphasize determinism and invariants before raw performance.

Recommended test areas:

- mask generation from representative terrain stacks;
- ramp/stair connectivity;
- repeated A* results under stable seeds;
- cache invalidation after topology changes;
- equivalent results across thread counts;
- blocked movement and replanning;
- profile-specific passability cases;
- multi-actor conflict resolution once reservations exist.

Performance tests should use realistic traffic patterns rather than only isolated shortest-path queries.
