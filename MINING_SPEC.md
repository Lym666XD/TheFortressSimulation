MINING_SPEC.md — Mining Jobs (v1.1.1 → v2)
id: mining.v1.1.1
status: normative
owner: sim/jobs
last_updated: 2025-09-30

0) Scope

Defines the data model and execution pipeline for Mining jobs (dig solid walls into open space and spawn drops). Built on top of UPDATE_ORDER, Navigation, Diff‑Log, and Orders. Integrates with Hauling via deterministic item drops. v1.1.1 targets single‑writer global commit (current engine mode); v2 upgrades to per‑chunk parallel write.

1) Principles

- Deterministic: stable scans/sorts; fixed seeds; identical outputs across OS/CPU/threads.
- Read‑parallel planning, write‑serialized commit (v1.1.1); per‑chunk single‑writer parallel write (v2).
- Data‑driven: tuning and drops read from registries; no hardcoding in code.
- Locality & safety: plan only reachable digs; validate adjacency before commit; respect L0→L6 layer ordering.
- Event hygiene: state mutations via Diff only; events are observational (UI/log/telemetry).

2) Data & Tuning (registries)

- `content/registries/tuning.mining.json` (implemented structure):
  - `geology_drops`: Drop tables per geology and terrain type (wall/ramp). Each entry has:
    - `item_id`: Item to drop (boulder or ore)
    - `min/max`: Quantity range (deterministic RNG)
    - `weight`: Future use for weighted selection
  - `geology_ticks`: Base dig time in ticks per geology and terrain type (wall/ramp)
  - `tool_multipliers`: Mapping from tool tags → speed multiplier (currently: `none: 1.0`)
  - `ramp_result_terrain`: Terrain type after digging ramp (currently: `OpenWithFloor`)
  - Future: `geology_overrides`, `gem_roll`, `hazards`

- `data/core/geology_*` references are already loaded. Use geology/material ids to choose drop table.

- Drop table schema (data‑driven):
  - `entries`: array of `{ item_id, min, max, weight }` for weighted, deterministic selection.
  - `mode`: `boulder_only | ore_only | weighted`.
  - Deterministic RNG seed: `seed = Hash(geology_handle, worldX, worldY, Z)`.

3) Inputs (Orders)

- Add Mining designation to Orders registry and UI bindings (eg. Z/X/C menus): rectangle at Z or brush‑based cell selection.
- OrdersManager receives `EnqueueMining(rect, z, priority, tick)` (analogous to haul); persists in active designations.

4) Planner (MiningSystem, Read)

- Responsibility: transform designations into `PlannedDig` DTOs (no writes). Read‑safe; can run in parallel in v2.
- Steps per tick (bounded budgets):
  1) Drain up to `max_designations_per_tick` from Orders (one‑shot mode, no persistent active set).
  2) For each rect, enumerate candidate tiles in stable order (row‑major). Filter:
     - TerrainKind must be `SolidWall` OR `Ramp` (v1.2+: ramp mining support)
     - Adjacency precheck: at least one 4‑neighbor is standable (required for ramps to avoid digging under feet)
  3) Create `PlannedDig { Point Cell; int Z; ushort GeologyHandle; byte TerrainKind; int Priority; ulong Seed }`.
  4) Enqueue into outbox in stable order; do not mutate world.
- Budgets: `max_planned_digs_per_tick` cap (128); backlog stored for next tick.

5) Executor (MiningJobSystem, Write)

- Responsibility: assign workers, move to an adjacent standable cell, dig for N ticks, commit terrain change and spawn drops via Diff.
- **Tile Reservation**: Prevents multiple workers from mining the same tile (hashset of (x,y,z) tuples; released on completion).
- State machine per job:
  - `ToAdj`: find deterministic adjacent target cell (N,E,S,W ordering; first passable neighbor). Begin path with diagonal movement; replan on topology change.
  - `Digging`: once at adjacency, accumulate `progress_ticks` toward `required_ticks = CalculateRequiredTicks(geology, terrainKind)`.
    - Reads `geology_ticks.default.wall` or `.ramp` from tuning.mining.json
    - Future: tool multipliers applied here
  - `Complete`: verify tile hasn't changed (edge case protection), emit diffs, release reservation, and finish.
- Diffs emitted on completion:
  - **Verification**: Check tile is still `SolidWall` or `Ramp` before committing (handles concurrent modifications)
  - `SetTerrain(Wall/Ramp → OpenWithFloor)` at target cell; bumps connectivity (Chunk.ConnectivityVersion → triggers navigation replan)
  - `AddItem(drops)` at target cell: geology‑based drops with deterministic RNG
    - Wall: 3 boulders (default granite)
    - Ramp: 1 boulder (default granite)
    - Supports multiple drop entries per terrain type
    - Future: geology‑specific ore/boulder mapping
  - `MoveCreature` is produced during movement (already diffed by movement executor).
- Assignment policy: deterministic sort of planned digs; stable order of workers (by GUID); HP>0; busy workers excluded; tile reservation checked before assignment.
- Back‑pressure: unassigned or `NoPath` jobs return to backlog (no TTL currently).

6) Layers, Ordering & Safety

- Layer writes: L0 (terrain) before L5 (items) before L6 (units). Mining writes L0 and L5; movement writes L6.
- Within a chunk, Applicator orders diffs by layer‑priority and stable keys; across chunks, v2 commits in ascending ChunkId.
- Connectivity: `SetTerrain` updates `Chunk.ConnectivityVersion` so Navigation invalidates caches; movement replan uses `NeedsReplan`.

7) Determinism

- Stable enumeration of designations and tiles (row‑major, chunk key ascending).
- Stable worker order by GUID.
- Fixed seeds for RNG (drop rolls): `Seed = Hash(geology_handle, worldX, worldY, z)`; table selection by stable weights.
- Write ordering: global serial (v1.1.1) or per‑chunk single writer (v2) with fixed cross‑chunk order.

8) Budgets

- Planner: `max_designations_per_tick`, `max_planned_digs_per_tick`.
- Executor: `max_mining_jobs_started_per_tick`; per‑tick dig progress increments (pacing with MovementExecutor’s step pacing if needed).
- Backlog cooldowns: `no_path_cooldown_ticks`, `blocked_cooldown_ticks`.

9) Error Handling

- Already Open: drop task.
- No adjacency standable: backlog with cooldown; recheck after terrain changes.
- No path: try next worker; else backlog; counter++.
- Terrain changed by others: verify before commit; if not a wall anymore, drop task.

10) Integration with Hauling

- Dropped items are regular Items/L5; Hauling planner will pick them up in later ticks if designated or if stockpile pulls.
- Planner naturally skips items already in stockpiles (implemented on Hauling side).

11) Events & Telemetry

- Publish `MiningCompletedEvent { Tick, Cell, Z, GeologyHandle, Worker }` to EventBus after commit for UI/log only; no state writes in event handlers.
- Counters: Completed/NoPath/Requeued; per‑tick time stats. Expose in Work Drawer.

12) v2 Concurrency Roadmap

- Planner: read‑parallel per chunk with per‑thread outboxes; merge and stable sort before write.
- Write: per‑chunk single writer applies Mining+Hauling+… diffs with layer ordering; cross‑chunk commit by ascending ChunkId.

13) Future Extensions

- Skill XP: write `ModifyAttribute` diffs from a SkillsSystem that consumes a MiningResultLog (populated by the executor).
- Hazards: collapse check (support neighbors), dust fields (L4), damage (L6) via diffs.
- Tools: richer multipliers and wear.
- Multi‑tile features: veins, strata‑driven variability.
- Workshops & Roles (future): Introduce Mining Workshop spots that host/limit mining roles; executors filter candidate workers by role/workshop eligibility before assignment; workshops can add local speed multipliers without affecting Diff/UPDATE_ORDER.

14) Minimal DTOs (for reference)

```csharp
public readonly record struct PlannedDig(
    SadRogue.Primitives.Point Cell,
    int Z,
    ushort GeologyHandle,
    byte TerrainKind,  // NEW: distinguish Wall vs Ramp
    int Priority,
    ulong Seed);

public enum MiningStage { ToAdj, Digging, Complete }

public sealed class ActiveMiningJob {
    public Guid WorkerId { get; init; }
    public SadRogue.Primitives.Point Target { get; init; }
    public int Z { get; init; }
    public SadRogue.Primitives.Point Adjacent { get; set; }
    public MiningStage Stage { get; set; }
    public int ProgressTicks { get; set; }
    public int RequiredTicks { get; set; }
    public ushort GeologyHandle { get; init; }       // NEW
    public TerrainKind TerrainKind { get; init; }    // NEW
}
```

15) Architecture Overview & Stairwell Deep-Dive (Critical)

15.1 Z-Axis Conventions (Internal vs. Player UI)

- Internal world coordinates: Z increases upward; lower Z values are deeper.
- Player expectation in the UI: Scrolling "up" means digging down (deeper layers).
- Mapping for DigStairwell only: OrdersManager inverts the UI zMin..zMax range into an internal range that digs downward from the starting Z.
  - Example: Player selects z=25..29 (start at ground 25, drag upward). Orders become actual z=21..25 internally (digging down from 25 to 21).
- Segment indices follow internal Z: Top = highest Z in designation (ZMax); Bottom = lowest Z (ZMin); Middle = between.

15.2 Data Flow

- UI -> OrdersManager: Advanced mining orders enqueued with stairwell Z inversion applied.
- MiningSystem (Planner, Read): Scans rectangles per Z layer; emits immutable PlannedDig DTOs into a planner outbox. For stairwells, it starts at ZMax and scans downward to prioritize Top, then Middle, then Bottom.
- MiningJobSystem (Executor, Write): Dequeues PlannedDig, assigns workers, moves, digs, and applies diffs (terrain writes + drops + movement).
- Diff Applicator: Mutates world tiles/items with deterministic ordering.

15.3 Planner Rules (Stairwell)

- Scan order: CurZ starts at ZMax and decrements per completed XY slice.
- Terrain filter per layer:
  - Allowed: SolidWall (rock) and OpenWithFloor (floor) – both can host stairs.
  - Rejected: OpenNoFloor – cannot place stairs in mid-air.
- Segment assignment by Z:
  - Top (Z == ZMax) -> expected final terrain: StairsDown.
  - Bottom (Z == ZMin) -> StairsUp.
  - Middle -> StairsUD.

15.4 Executor Queueing & Prioritization

- Inbox capacity per tick: 16 PlannedDig.
- Source order (to avoid starvation):
  1) Dequeue from planner outbox first.
  2) Dequeue from _backlog while room remains.
  3) Only if room remains and every 10 ticks, dequeue from _deferredStairwells.
- Sorting before assignment (deterministic):
  - Primary: Priority descending.
  - Stairwell only: Segment order Top -> Middle -> Bottom, then XY stable, then Z descending (higher Z first).
  - Other actions: XY stable, then Z ascending.

15.5 Stairwell Connectivity Gate & Pre-Open

- Gate check (non-Top segments): requires a stair above or below at the same XY to ensure vertical approachability prior to digging.
  - Above (z+1): StairsDown or StairsUD.
  - Below (z-1): StairsUp or StairsUD.
- Pre-open rule (connectivity bootstrapping): When a worker starts digging a stair segment at Z, if z-1 is SolidWall, set it to StairsUD to establish the next connection.
  - Applies to Top and Middle only (never Bottom) to avoid pre-opening outside the selected range.
  - Drops are only spawned when converting from SolidWall (guarded in Apply phase), so pre-open does not duplicate drops where the tile is already open.
- Middle "already satisfied" fast-path: If a Middle tile is already StairsUD (e.g., from the previous segment's pre-open), it still produces a tiny, 1-tick job rather than being dropped. This keeps the downward pre-open chain advancing to the next layer.

15.6 Skip / Idempotency Semantics

- If the target tile already matches its expected final terrain, the PlannedDig may be dropped as a no-op. Exception: Middle stair segments are not dropped; they fast-complete to propagate pre-open further downward.
- Drops are spawned only when the current terrain is SolidWall.

15.7 Known Pitfalls & Fixes (Post-mortem)

- "Stairwell digs only two layers"
  - Cause: Deferred queue starving fresh stair segments and stairwell ordering erased by generic Z-ascending job sort. Bottom/Middle repeatedly failed the gate and re-entered deferred, blocking Middle layers needed for connectivity.
  - Fixes: Prioritize planner then backlog then deferred (10-tick cadence, only if room). Add stairwell-aware sort (Top->Middle->Bottom, Z descending). Keep Middle jobs even if UD (1-tick fast-path) to push the pre-open chain.
- Pre-open outside selection
  - Cause: Pre-opening on Bottom segments created stairs below the designated Z range.
  - Fix: Disable pre-open for Bottom segments; apply only to Top/Middle.
- Traceability loss in backlog
  - Cause: Re-queued jobs were pushed with DesignationId=0.
  - Fix: Preserve original DesignationId in all requeues.

15.8 UI Highlights & Lifetimes (Yellow Marks)

- Recent order rectangle highlight (yellow/orange border): expires quickly; TTL reduced from 100 to 30 UI ticks.
- Mining completion highlight (gold dot per tile): TTL reduced from 100 to 25 simulation ticks.
- Goal: keep feedback visible but avoid long-lived overlays during large digs.

15.9 Troubleshooting & Log Queries

- Verify stairwell production order:
  - Search: "PRODUCE PlannedDig" with seg=Top|Middle|Bottom.
- Check assignment flow and counts:
  - "Planned digs dequeued" (IDs and batch sizes).
- Gate/deferral signals:
  - "blocked: no vertical connection; defer" (every 10 ticks at most; not starving planner).
- Skip/fast-complete signals:
  - "Check skip: seg=... current=... expected=...", "Skip stairwell seg=... already ...".

15.10 Validation Checklist (Stairwell)

- Top at ZMax completes and pre-opens ZMax-1.
- Middle at ZMax-1 runs (1 tick if already UD) and pre-opens ZMax-2.
- Repeats until Bottom at ZMin; no pre-open at Bottom; final terrain chain is Down / UD* / Up.
- No jobs are starved by deferred entries; planner drains every tick under budget.

15.11 Documentation Consistency Note

- This spec supersedes prior drafts that stated "Z increases downward." The engine defines Z increasing upward internally; the stairwell UI range is inverted in OrdersManager to match player expectations.
