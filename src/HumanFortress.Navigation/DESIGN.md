# Navigation System Design

## Architecture Overview

The navigation system follows the specifications in NAVIGATION_SPEC.md with these core components:

### 1. Navigation Data (Per Chunk)
```csharp
// Per chunk navigation caches
public class ChunkNavData
{
    public byte[] NavMask;        // Capability bits per tile (Walk/Swim/Fly/etc)
    public ushort[] NavCost;      // Movement costs per tile
    public int ConnectivityVersion;  // Topology change tracking

    // Rebuild from tile data during RebuildDerived phase
    public void RebuildFromTiles(TileBase[] tiles, Dictionary<int, FurnitureCell> furniture);
}
```

### 2. Navigation Capabilities (Bit Layout)
```csharp
[Flags]
public enum NavCapability : byte
{
    None = 0,
    Walk = 1 << 0,        // 4-neighbor planar motion
    Crawl = 1 << 1,       // Reserved for tight tunnels
    Swim = 1 << 2,        // Allow motion when FluidDepth >= threshold
    Fly = 1 << 3,         // Airborne (ignores many L0/L2 tests)
    Standable = 1 << 4,  // Valid "stop" tile for walkers
    EdgeClimb = 1 << 5,  // Ladders/rope (vertical neighbor)
}
```

### 3. Path Service Interface
```csharp
public interface IPathService
{
    Path Solve(in PathRequest request, in WorldSnapshot snapshot);
}

public readonly record struct PathRequest(
    ChunkKey SrcChunk, int SrcIdx,
    ChunkKey DstChunk, int DstIdx,
    MoveMode Mode,
    PathFlags Flags,
    uint Seed);

public readonly record struct Path(
    PathResultKind Kind,
    int Length,
    uint Hash,
    ReadOnlyMemory<PathNode> Steps);
```

### 4. Deterministic A* Implementation
- Binary heap with deterministic tie-breakers: (f, h, g, localIndex)
- Manhattan heuristic for 4-neighbor movement
- Node expansion follows exact rules from spec
- Hard cap on nodes explored (10,000 default)
- Time budget per tick (3ms default)

### 5. Path Caching
- LRU cache keyed by: (src, dst, mode, flags, connectivityVersionHash)
- Invalidated when any involved chunk's ConnectivityVersion changes
- Thread-safe for concurrent reads

### 6. Integration Points

#### Read Phase (Parallel)
- PathService.Solve() runs during AI/Job read phase
- Consumes NavMask/NavCost from chunks (read-only)
- Multiple pathfinders can run concurrently

#### Write Phase (Serialized)
- Navigation data rebuilt during RebuildDerived stage
- ConnectivityVersion bumped on topology changes
- No direct writes from pathfinding

## Implementation Phases

### Phase 1: Core Data Structures
1. NavCapability enum and NavMask operations
2. ChunkNavData with NavMask/NavCost arrays
3. PathRequest/Path/PathNode structures

### Phase 2: Navigation Mask Building
1. Extract walkability from TileBase (L0 terrain)
2. Apply furniture blockers (L2)
3. Apply fluid depths (L3)
4. Calculate movement costs

### Phase 3: A* Pathfinder
1. Binary heap with deterministic ordering
2. Node expansion with exact neighbor rules
3. Path reconstruction
4. Node/time limits

### Phase 4: Caching & Optimization
1. LRU path cache
2. ConnectivityVersion tracking
3. Performance profiling

### Phase 5: Testing
1. Unit tests for mask generation
2. Determinism tests for A*
3. Performance tests with 10 concurrent pathfinders
4. Integration tests with game loop