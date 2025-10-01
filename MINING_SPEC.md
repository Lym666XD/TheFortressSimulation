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
