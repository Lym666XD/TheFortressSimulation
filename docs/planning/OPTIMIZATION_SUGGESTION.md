# Optimization Suggestions

Updated: 2026-06-13
Status: current performance backlog, merged from English and Chinese sources

This document is the active optimization backlog. It merges the older performance
review, Chinese UI refactor performance notes, architecture audits, current
navigation/job/UI specs, and stability notes.

It is not an implementation plan by itself. Treat source code and profiling data
as authoritative when they disagree with an estimate below.

## Merged Sources

Primary sources:

- `docs/archive/plans/OPTIMIZATION_SUGGESTION_SOURCE_2025.md`
- `docs/planning/RULES.md`
- `docs/planning/MILESTONE.md`
- `docs/planning/REFACTOR_PITFALLS_AND_LESSONS.md`
- `docs/simulation/WORK_AND_JOBS_SYSTEM.md`
- `docs/simulation/TRANSPORT_SYSTEM.md`
- `docs/simulation/NAVIGATION_SPEC.md`
- `docs/ui/UI_AND_INPUT_MODEL.md`
- `docs/ui/RENDERING_SNAPSHOT.md`
- `docs/worldgen/MAPGEN_PIPELINE.md`

Historical/reference sources:

- `docs/archive/ui/UI_REFACTOR_PLAN.md`
- `docs/archive/plans/ARCHITECTURE_ISSUE_CHATGPT_SOURCE.md`
- `docs/archive/plans/HUMANFORTRESS_MAIN_BRANCH_ARCHITECTURE_AUDIT_FOR_CODEX.md`
- `docs/archive/plans/TODO_CONSTRUCTION_AND_HAULING_STABILITY.md`

## Current Code Baseline

Current facts checked against code:

- `ItemManager` has `_posIndex`, `GetItemsAt(...)`, and `GetGroundItemsIn(...)`.
- `ItemManager.SpawnItem(...)` still merges existing stacks by scanning `_instances.Values` with LINQ.
- `HaulingSystem` now uses `GetGroundItemsIn(...)`, but still allocates with LINQ/`ToList()` and then enqueues into the transport pipeline.
- `PathCache` indexes traversed chunks, but LRU eviction still scans the whole cache and reverse index removal scans chunk indexes.
- `PathService` already exposes basic cache/path stats.
- `UnifiedJobsOrchestrator`, transport, mining, and craft systems expose some stats, but there is no unified perf counter surface or stress harness.
- `NavigationManager.GetNavDataAt(...)` is read-only; navigation rebuilds belong to explicit runtime/session rebuild points.
- `RenderSnapshotBuilder` exists, but active UI/rendering code still has live `World` and `FortressRuntimeAccess` reads in several panels.
- `RenderSnapshotBuilder` still computes autotile/connect masks directly from visible chunks; full delta/versioned presenter behavior is target, not complete current code.

## Optimization Rules

Hard constraints:

- Preserve determinism before speed. Stable ordering, seeded RNG, and replay parity are non-negotiable.
- Do not use wall clock, thread completion order, dictionary iteration order, or non-deterministic floating point in simulation decisions.
- Keep pathfinding budget behavior fail-soft. Correctness tests should use test-specific generous budgets; production-budget deferral needs separate tests.
- Do not reintroduce navigation query-time rebuilds. Path queries read cache; commits mark dirty chunks; runtime rebuilds explicitly.
- Do not parallelize write phases until systems declare read/write masks and a scheduler can prove non-overlap.
- Do not persist derived caches. Rebuild nav/path/render/spatial caches from authoritative state.

Engineering rule:

Measure first, then optimize. If a change cannot be checked by counters, stress tests, or replay equality, it should stay in research.

## P0: Safe, Current, High-Leverage

### 1. Unified Performance Counters And Stress Harness

Current issue:

- Some job/path stats exist, but there is no single perf surface for per-tick system cost, allocation pressure, backpressure, cache hit rate, and degradation counts.
- Existing optimization claims are mostly estimates.

Build:

- `PerfCounters` or diagnostics DTOs for tick time, read/write time, path solves, cache hits/misses, item queries, transport intake/backlog, construction material requests, snapshot build time, and UI paint time.
- Stress scenarios for early, mid, late, and pathological loads.
- Perf smoke tests with deterministic inputs.
- Initial load targets:
  - early game: about 50 entities, 2x2 chunks;
  - mid game: about 500 entities, 4x4 chunks;
  - late game: about 2,000 entities, 8x8 chunks;
  - stress: about 5,000 entities, 8x8 chunks.

Acceptance:

- A stress run reports per-system timings and counts without external profiler setup.
- Replay hashes remain equal with counters enabled.
- Budget tests do not fail because production pathing budget legitimately deferred work.

### 2. Item Stack Merge Uses Position Index

Current issue:

- `ItemManager.SpawnItem(...)` still does:

```text
_instances.Values -> Where(...) -> OrderBy(...) -> ToList()
```

even though `_posIndex` already exists.

Build:

- Use `_posIndex` for same-tile lookup.
- Keep deterministic stack choice by sorting or selecting the lowest GUID only among items already found at that tile.
- Avoid allocating `List<>` on the hot path unless a snapshot API explicitly needs one.

Acceptance:

- Spawning/adding 1,000 stackable items on a populated map stays within a documented budget.
- Existing item merge/split/move regression tests still pass.

### 3. PathCache O(1) Or Amortized LRU

Current issue:

- `PathCache.EvictLRU()` scans `_cache`.
- `RemoveFromIndex(...)` scans `_chunkIndex`.

Build:

- Add a deterministic LRU structure, such as a linked list plus dictionary, or a bounded queue with periodic compaction.
- Track `key -> traversed chunks` so eviction can remove the key from known chunk sets without scanning every chunk index.
- Keep corridor-version validation for traversed chunks.

Acceptance:

- Cache eviction cost is not proportional to cache size in normal operation.
- Dirty chunk invalidation removes all affected paths.
- Cache hit/miss and eviction counts are visible in diagnostics.

### 4. UI/Runtime Live-Read Reduction

Current issue:

- UI target docs say snapshot-only, but current panels still read live `World`, concrete job systems, and runtime facades.
- This is both an architecture gap and a performance problem when large lists/panels redraw frequently.

Build:

- Add small query/debug DTOs for high-traffic panels: work overview, stock/items, jobs, zones, and debug pages.
- Keep UI local state in `UiStore`; use runtime/query facades for read-only snapshots.
- Virtualize large item/creature/alert lists.

Acceptance:

- Large stock/item list scroll stays within UI budget.
- Panels no longer need broad live-world scans for common display rows.
- UI queries use stable sort keys.

## P1: Medium Scope, Good Return

### 5. Transport And Construction Request Stability

Current issue:

- Dense construction/transport patterns can create request churn, repeated invalid/no-path attempts, or hard-to-debug stalls.

Build:

- Track in-flight material counts per site.
- Add deterministic TTL backoff for repeated `(item, destination)` failures.
- Bound drop-cell/ring scans and cache per-site shortfall state.
- Emit counters for suppressed requests, no-drop cells, invalid replans, unstuck/eject/sanitize events.

Acceptance:

- Dense multi-site construction keeps making progress.
- Planner enqueue counts stabilize instead of spamming identical requests.
- Logs/counters identify starvation without manual trace reading.

### 6. Render Snapshot Delta And Autotile Cache

Current issue:

- `RenderSnapshotBuilder` computes connect masks from chunk tiles during snapshot build.
- The target snapshot docs call for dirty chunks, changed rows, and presenter deltas, but active implementation is not fully there.

Build:

- Cache autotile/connect masks by chunk/Z version.
- Emit changed chunks/Z/row spans instead of treating all visible data as fresh.
- Avoid full presenter clears when only row spans changed.

Acceptance:

- Snapshot build cost scales with dirty visible chunks, not total visible map size.
- Autotile cache invalidates on L0/L2 topology changes.
- Snapshot hash/delta tests prove deltas reproduce full snapshots.

### 7. Runtime-Wide Navigation And Movement Services

Current issue:

- Historical audits warned against fragmented path budgets, caches, movement state, and movement statistics.
- Current code is better, but movement execution still lives in job-specific shells/executors.

Build:

- Keep `PathService`/path budget ownership centralized.
- Move toward a runtime `MovementSystem` that receives move intents, owns active movement state, resolves reservations/conflicts, and emits movement diffs.
- Job systems request movement; they should not each become independent movement authorities.

Acceptance:

- One place reports path budget/cache/movement stats.
- Multi-agent movement conflicts have one deterministic resolver.
- Jobs preserve current behavior while moving behind the shared service.

### 8. Compiled Tag Queries For Hot Paths

Current issue:

- Item/creature/material tags remain string-based for many queries.
- The old 2025 suggestion overstates immediate bitflag gains, but the direction is valid for frequent filters.

Build:

- Keep JSON/source tags as strings.
- Compile stable tag handles or compact bitsets at content-load time for hot tags.
- Preserve mod/save compatibility with a manifest or registry mapping.
- Start with frequently queried core tags; leave rare tags as strings until measured.

Acceptance:

- Hot filters use integer/bitset checks.
- Debug output can still print original tag names.
- Save/load uses stable IDs or remappable manifests, not transient bit positions.

## P2: Strategic Work

### 9. Navigation Search Layers

Current direction:

- A* remains the deterministic baseline.
- Optional layers may include hierarchical regions, portals, flow/vector fields for shared destinations, and local refinement.

Do:

- Add only when path counters show A* is a bottleneck.
- Keep optional layers derived from authoritative nav data.
- Fall back to baseline A* when weights, vertical links, movement profiles, or dynamic blockers make specialized paths unsafe.

Avoid:

- GPU pathfinding as a near-term task.
- Unsafe SIMD before simple cache/query fixes and measurements.

### 10. Scheduler Stage Graph And Chunk-Parallel MergeApply

Current direction:

- The target model is read-only Plan jobs, barrier, deterministic MergeApply, one writer per chunk/stage.

Do:

- Add explicit read/write masks and system dependencies before parallel writes.
- Start with instrumentation and validation.
- Use chunk-parallel MergeApply only after replay equality and conflict tests exist.

Avoid:

- Parallelizing `WriteTick` based only on intuition about independent systems.

### 11. Hot/Cold Data Layout And ECS-Style Storage

Current direction:

- Data-oriented layout is a valid long-term path for large maps and thousands of entities.

Do:

- Apply small hot/cold splits only where profiling shows cache misses or allocation pressure.
- Consider stable runtime handles for high-frequency string IDs after content load, while keeping source JSON and saves ID-based.
- Consider compiled terrain/nav flags only as derived data; do not replace the canonical `TerrainBits` layout without a migration plan.
- Consider pooling A* scratch structures only after path counters show allocation pressure.
- Keep authoritative IDs and save format stable.

Avoid:

- Full ECS migration until current architecture boundaries, tests, and profiling justify the rewrite.
- Changing public content IDs, save IDs, or tile bit layout purely for micro-optimization.

### 12. Worldgen Scaling Budgets

Current direction:

- Fortress generation scales budgets by `TilesTotal^0.85`.
- World/fortress generation can parallelize per world cell or chunk/column with a single deterministic commit.

Do:

- Add S=1..4 budget checks.
- Rebuild derived caches once after generation.
- Keep generated outputs hashable for replay audits.

## Not Recommended Now

- Broad ECS rewrite.
- GPU pathfinding.
- Unsafe SIMD-first navigation rewrite.
- Parallel write phase without read/write masks.
- Persisting any derived cache.
- Making UI performance worse by adding more live-world reads while snapshot/query facades are the stated target.

## Validation Matrix

| Area | Test |
| --- | --- |
| Items | Spawn/merge 1,000 stackable items on a populated map under budget; existing split/move tests pass. |
| Path cache | Fill cache beyond capacity; eviction count and time stay bounded; dirty chunk invalidates traversed paths. |
| Transport | Dense construction/stockpile stress run shows bounded request churn and continuous progress. |
| UI | 10k item rows grouped/virtualized; scroll update stays under documented budget. |
| Snapshot | Delta snapshot equals full rebuild; autotile cache invalidates on topology edits. |
| Determinism | Replay hashes unchanged before/after optimization with the same seed/input stream. |
| Budgets | Production budget deferral is tested separately from path correctness. |

## Priority Summary

| Priority | Work |
| --- | --- |
| P0 | Perf counters/stress harness, item spawn position-index merge, PathCache LRU/reverse-index fix, UI live-read reduction. |
| P1 | Transport request stability, snapshot delta/autotile cache, runtime-wide movement service, compiled hot tag queries. |
| P2 | Navigation search layers, scheduler stage graph, hot/cold data layout, worldgen scaling checks. |
| Research | ECS migration, GPU pathfinding, unsafe SIMD navigation. |
