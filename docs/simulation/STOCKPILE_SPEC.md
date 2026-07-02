STOCKPILE_SPEC.md — Zone-Based Storage System (Area-only, Non-entity)
id: stockpile.v1
status: normative
owner: sim/economy
last_updated: 2025-09-29

Current implementation note (2026-06-12):

- Stockpile command creation currently uses Runtime command target seams.
- `StockpileDiff` is not yet the authoritative active tick-pipeline path for all stockpile behavior.
- Hauling references should be read through [TRANSPORT_SYSTEM.md](TRANSPORT_SYSTEM.md), not the old `HAULING_POLICY` name.

applies_to:
  - World/Chunk L5 overlay (items)
  - Work And Jobs System (WORK_AND_JOBS_SYSTEM)
  - Transport System
  - Update Order (UPDATE_ORDER)
  - Navigation (NAVIGATION_SPEC)
principles:
  - Deterministic selection & assignment (stable orders, integer scoring)
  - Read-parallel propose; write-serialized commit (Diff-Log pattern)
  - Anti ping-pong: dwell time + hysteresis thresholds
  - Bounded work per tick (budgets) and graceful degradation
  - Single arbitration point (Broker) for haul job creation
scope_v1:
  - Stockpile zones (pull mode, tag-based filtering)
  - Sharded storage across chunks
  - Integration with Jobs/Transport hauling planner
  - Basic capacity management (stack counting)
out_of_scope_v1:
  - Containers/bins; conveyors; carts/vehicles
  - Accept-only/output-only modes (v2)
  - Quality/temperature filtering (v2)
  - Zone links to workshops (v2)

1) Concepts & Data Model
1.1 Stockpile Zone (global definition)
Zone is a logical entity spanning one or more chunks, defined by:

```
StockpileZone {
  zoneId: int32 (unique, deterministic)
  name: string
  homeChunk: ChunkKey (creation point, owns global properties)
  filter: StockpileFilter (compiled predicate)
  priority: int32 (0=Low, 1=Normal, 2=High, 3=Critical)
  targetStacks: int32 (desired number of stacks)
  hysteresisLow: int32 (pull when below this)
  hysteresisHigh: int32 (stop pulling when above this)
  generation: uint32 (bumped on any config change)
  createdTick: ulong
}
```

1.2 Zone Shard (per-chunk fragment)
Each chunk maintains local shards for zones overlapping it:

```
ZoneShard {
  zoneId: int32
  memberCells: BitSet (1024 bits for 32x32 chunk)
  capacity: int32 (number of cells that can hold items)
  usedSlots: int32 (current occupied cells)
  reservedSlots: int32 (cells reserved for incoming items)
  incomingCount: int32 (items in transit to this shard)
}
```

1.3 Chunk Stockpile Data
```
ChunkStockpileData {
  shards: Dictionary<int, ZoneShard>
  cellZones: int[] (1024 entries, zoneId per cell, 0=none)
  itemIndex: Dictionary<string, List<ItemHandle>> (tag → items)
  dirtyGeneration: uint32 (for cache invalidation)
}
```

1.4 Item Properties (extended)
```
ItemStackRef {
  handle: int32
  lastZoneId: int32 (stickiness tracking)
  placedTick: ulong (for dwell time)
  reserved: bool
  reservedBy: JobId?
}
```

2) UPDATE_ORDER Integration
2.1 Stage Placement
Stockpile operations occur in the **Items** stage (stage 7):
- Read phase: scan haul designations and stockpile zones, choose accepting destinations
- Transport planning: enqueue `TransportRequest` records and matching reservation diffs
- Write phase: apply reservations, update counts, place/remove items

2.2 Diff Operations
```
enum StockpileDiffOp {
  CreateZone = 1,
  DeleteZone = 2,
  AddCells = 3,
  RemoveCells = 4,
  UpdateFilter = 5,
  ReserveSlot = 7,     // Queued beside TransportRequest creation
  ReleaseSlot = 8,
  PlaceItem = 9,
  RemoveItem = 10
}

StockpileDiff {
  op: StockpileDiffOp
  targetChunk: ChunkKey
  zoneId: int32
  cellIndex: int32 (-1 if N/A)
  itemHandle: int32 (0 if N/A)
  quantity: int32
  priority: int32
  systemId: string
  localSeq: int32
}
```

3) Hauling Broker (Single Arbitration Point)
3.1 Read Phase Flow
```
1. Each chunk scans local zones (parallel)
   → Produces PullRequest[] if below hysteresisLow

2. Each chunk scans loose items (parallel)
   → Produces AvailableItem[] not in suitable zones

3. Hauling planner chooses destinations through StockpileWorldQueries
   → Rechecks filters using item projection data
   → Tracks same-tick planned reservations
   → Enqueues TransportRequest records plus ReserveSlot diffs only when the transport queue accepts a new pending request
```

3.2 PullRequest Structure
```
PullRequest {
  zoneId: int32
  targetChunk: ChunkKey
  filter: StockpileFilter (reference)
  priority: int32
  desiredStacks: int32 (targetStacks - current - incoming)
  requestId: int32 (deterministic, for tie-breaking)
}
```

3.3 Matching Algorithm (deterministic)
```csharp
// In the active HaulingSystem destination planner
foreach request in sortedRequests:
  candidates = items.Where(i =>
    request.filter.Accepts(i) &&
    !i.reserved &&
    DwellTimeExpired(i) &&
    !IsSticky(i, request.zoneId))

  foreach item in candidates.OrderBy(ScoreInteger):
    score = CalculateIntegerScore(item, request)
    if score > 0:
      if enqueue TransportRequest(item → request.zone):
        queue ReserveSlot(request.zone)
      request.desiredStacks--
      if request.desiredStacks <= 0: break
```

The transport request queue owns duplicate pending-item protection. Same-item,
same-destination requests merge quantities into the existing pending request;
same-item, competing-destination requests preserve the earlier pending request.
Stockpile reservations must follow the boolean enqueue result so merged or
rejected duplicate requests do not reserve extra slots.

4) Integer Scoring (Deterministic)
```
Score components (all integer):
  base_priority = zone.priority * 10000
  distance_cost = PathTileDistance(item, zone) * 10
  stickiness = (item.lastZoneId == zoneId) ? -5000 : 0
  dwell_bonus = min(100, (currentTick - item.placedTick) / 50)

FinalScore = base_priority - distance_cost + stickiness + dwell_bonus

Tie-breakers (complete chain):
  1. FinalScore (descending)
  2. distance_cost (ascending)
  3. zoneId (ascending)
  4. itemHandle (ascending)
  5. targetChunk.GetHashCode() (ascending)
  6. localSeq (ascending)
```

5) Anti Ping-Pong Mechanisms
5.1 Dwell Time
Items placed in a zone are ineligible for re-hauling for `dwell_ticks_min` (default: 2000) unless:
- Current zone no longer accepts the item (filter changed)
- Destination is a construction/workshop need (override)
- Emergency priority (≥ Critical)

5.2 Hysteresis Thresholds
```
Zone pull behavior:
  if (currentStacks <= hysteresisLow):
    GeneratePullRequests(targetStacks - currentStacks)
  if (currentStacks >= hysteresisHigh):
    StopPulling()
```

5.3 Stickiness Penalty
Items recently in a zone get negative score for moving to different zones.

6) Cross-Chunk Coordination
6.1 Message Types (minimal set)
```
enum StockpileMessageType {
  HaulJobAssigned = 1,   // Notify destination chunk
  HaulJobComplete = 2,   // Update source/dest
  HaulJobCancelled = 3,  // Release reservations
  ZoneConfigBatch = 4    // Batch config updates
}

StockpileMessage {
  type: StockpileMessageType
  zoneId: int32
  itemHandle: int32
  quantity: int32
  sourceChunk: ChunkKey
  destChunk: ChunkKey
  cellIndex: int32
  jobId: int32
  localSeq: int32
}
```

6.2 Mailbox Drain Order
Within each chunk: `(tick → sourceChunk.Hash → localSeq)`

7) Write Phase Processing
7.1 Diff Merge Order (deterministic)
```
Sort key within chunk:
  cellIndex → priority(desc) → op → zoneId → itemHandle → systemId → localSeq
```

7.2 Apply Rules
```csharp
void ApplyStockpileDiff(Chunk chunk, StockpileDiff diff) {
  switch(diff.op) {
    case ReserveSlot:
      // Queued beside a TransportRequest by the hauling planner
      chunk.ReserveSlot(diff.zoneId);
      break;

    case PlaceItem:
      // Validate cell still accepts
      if (!ValidateCellAccepts(diff))
        return;
      chunk.PlaceItemAt(diff.cellIndex, diff.itemHandle);
      shard.usedSlots++;
      shard.reservedSlots--;
      break;
  }
}
```

8) Capacity & Indexing
8.1 Capacity Model (v1)
- Each cell can hold 1 stack
- Stack size determined by item definition
- Capacity = number of member cells

8.2 Local Item Index (per chunk)
```
ChunkItemIndex {
  byTag: Dictionary<string, List<ItemHandle>>
  byZone: Dictionary<int, List<ItemHandle>>
  loose: List<ItemHandle> (not in any zone)

  // Updated only in Write phase
  void OnItemPlaced(item, cell, zoneId)
  void OnItemRemoved(item, cell)
}
```

9) UI Commands (ApplyCommands stage)
9.1 Zone Creation
```
CreateStockpileZone {
  cells: List<(ChunkKey, BitSet)> // Can span chunks
  preset: string // References stockpile_presets.json
  name: string
  priority: int32
}
```

9.2 Zone Editing
```
ModifyStockpileZone {
  zoneId: int32
  operation: Add/Remove/UpdateFilter/SetPriority
  cells: List<(ChunkKey, BitSet)>?
  newFilter: StockpileFilter?
  newPriority: int32?
}
```

10) Budgets & Performance
```json
// tuning.stockpile.json
{
  "budgets": {
    "max_zones_per_chunk": 32,
    "max_cells_scanned_per_tick": 2048,
    "max_pull_requests_per_zone": 20,
    "max_haul_jobs_per_tick": 50,
    "broker_time_budget_ms": 2
  },
  "thresholds": {
    "default_hysteresis_low": 0.7,
    "default_hysteresis_high": 0.9,
    "dwell_ticks_min": 2000,
    "stickiness_penalty": -5000
  },
  "scoring": {
    "priority_multiplier": 10000,
    "distance_cost_per_tile": 10,
    "dwell_bonus_max": 100
  }
}
```

11) Determinism Guarantees
- All scoring uses integers only
- Complete tie-breaker chain specified
- Chunk iteration in ascending ChunkKey order
- Diff merge uses stable sort
- No HashSet/Dictionary enumeration without explicit ordering
- RNG not used (deterministic heuristics only)

12) Error Handling
- Invalid zone references: log and skip
- Capacity exceeded: silent discard with event
- Cross-chunk conflicts: first-writer-wins by ChunkKey order
- Quarantine zones with repeated failures

13) Testing Requirements
13.1 Determinism
- Fixed seed + same inputs = identical zone fills across runs
- Multi-threading doesn't affect outcomes

13.2 Anti Ping-Pong
- Item placed in zone A doesn't immediately move to zone B
- Hysteresis prevents oscillation at boundary

13.3 Performance
- 100 zones with 1000 items: hauling destination planning remains bounded per tick
- Parallel chunk scanning scales linearly

13.4 Correctness
- No double-reservation of items
- Capacity limits respected
- Filter changes applied atomically

14) Phase E Deliverables
14.1 Core Implementation
- [ ] StockpileZone and ZoneShard data structures
- [ ] ChunkStockpileData integration
- [ ] StockpileDiff operations
- [x] Jobs/Transport-backed stockpile hauling planner
- [ ] Integration with Items stage

14.2 UI Integration
- [ ] CreateStockpileZone command
- [ ] Zone visualization overlay
- [ ] Basic filter presets

14.3 Testing
- [ ] Unit tests for scoring/matching
- [ ] Integration test: zone fill
- [ ] Determinism replay test
- [ ] Performance benchmarks

15) Future Extensions (v2)
- Accept-only/output-only modes
- Workshop links (give-to/take-from)
- Quality/temperature filtering
- Container support (bins/barrels)
- Zone priorities by item type
- Rebalancer for cross-zone optimization

16) LLM Integration Notes
When implementing stockpile systems:
- Always use integer scoring, no floats
- Respect Read/Write phase separation
- Use Diff-Log pattern, never direct mutation
- Sort all collections before iteration
- Provide complete tie-breaker chains
- Update counts only in Write phase
- Validate conditions atomically in Apply

This document is normative for stockpile behavior. Any implementation must preserve determinism, respect UPDATE_ORDER stages, and maintain compatibility with the current transport system and WORK_AND_JOBS_SYSTEM.
