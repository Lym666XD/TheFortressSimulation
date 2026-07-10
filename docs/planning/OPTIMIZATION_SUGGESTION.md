# Architecture-Safe Optimization Backlog

Updated: 2026-07-10
Status: current performance and architecture-safe optimization backlog, merged from English and Chinese sources

This document is the active optimization backlog. It merges the older performance
review, Chinese UI refactor performance notes, architecture audits, current
navigation/job/UI specs, and stability notes.

It is not an implementation plan by itself. Treat source code, build results,
profiling data, and replay tests as authoritative when they disagree with an
estimate below.

The purpose of this document is not only to make the game faster. It is to make
the game faster without weakening the long-term architecture:

```text
Architecture boundaries first.
Determinism is non-negotiable.
Measure before optimizing.
Optimize hot paths only after ownership and test boundaries are clear.
```

## Merged Sources

Primary sources:

- `docs/archive/plans/OPTIMIZATION_SUGGESTION_SOURCE_2025.md`
- `docs/planning/RULES.md`
- `docs/archive/plans/MILESTONE.md`
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
- `docs/archive/plans/HumanFortress_审计报告_2026-07-07.md`
- `docs/archive/plans/TODO_CONSTRUCTION_AND_HAULING_STABILITY.md`

## Current Code Baseline

Current facts checked against code:

- `ItemManager` has `_posIndex`, `GetItemsAt(...)`, and `GetGroundItemsIn(...)`.
- `ItemManager.SpawnItem(...)` now resolves same-tile stack merging through
  `_posIndex`, selecting the first matching ground stack from the stable
  GUID-sorted tile list and only building a same-tile list for mismatch
  diagnostics.
- `HaulingSystem` now uses `GetGroundItemsIn(...)`, but still allocates with LINQ/`ToList()` and then enqueues into the transport pipeline.
- `PathCache` indexes traversed chunks, but LRU eviction still scans the whole cache and reverse index removal scans chunk indexes.
- `PathService` already exposes basic cache/path stats.
- `UnifiedJobsOrchestrator`, transport, mining, and craft systems expose some stats, but there is no unified perf counter surface or stress harness.
- `NavigationManager.GetNavDataAt(...)` is read-only; navigation rebuilds belong to explicit runtime/session rebuild points.
- Active UI/rendering code now consumes Runtime-owned snapshot DTOs for the main map, overlays, drawers, debug pages, and placement previews instead of live `World` reads.
- Runtime frame/overlay DTOs now carry Runtime-authored snapshot metadata, publication surface/request-hash metadata, and presenter-frame metadata with full-snapshot transfer mode, publication sequence, stable payload hash, and optional same-request delta-base hash. Frame render DTOs carry map-viewport changed-cell, changed-row, and changed screen-region deltas over final screen-cell values with per-row/region hashes; UI overlay DTOs carry section-level deltas over build/jobs/workshop/stockpile/zone/drawer/debug sections. App.Rendering now consumes both the map-viewport deltas and UI-overlay section deltas through presenter caches. The project still needs broader packed world-chunk/panel delta payloads and panel-specific redraw skipping before optimizing high-frequency UI redraw paths aggressively.
- Runtime owns the first save/replay directory vertical slice:
  `slot_manifest.json`, `runtime_snapshot.json`, command replay rows, primitive
  RNG rows, and a Simulation-owned world payload for supported
  terrain/entity/reservation/stockpile/placeable/order slices, including
  contained items with payload-local acyclic item container references,
  carried/equipped items whose owning creatures are present in the same payload,
  installed items with valid placement anchors, and item-local reservation
  tokens whose claimant/count rows validate against the item payload. Slot
  inspection now includes Runtime-authored compatibility, migration-plan, and
  restore-plan read models; migration transform ids are generated through a
  Runtime registry seam, and migration execution attempts now enter through a
  Runtime-owned migrator/result DTO that currently no-ops compatible slots and
  applies `slot:0->1` for legacy/current snapshot-only slot metadata while
  `runtime_snapshot:4->5` and `runtime_snapshot:5->6` handle safe older
  documents whose migrated current payload validates.
  Slot inspection and restore execution also share a Runtime-owned content
  signature compatibility gate with saved/current catalog diagnostic summaries
  and structured mismatch rows; it fails closed with `slot.content` until a
  real missing-content placeholder/remap policy exists. Transport, mining, and
  craft are the supported job payload/restore slices. The first concrete
  migration transforms, `slot:0->1`, `runtime_snapshot:4->5`, and
  `runtime_snapshot:5->6`, cover current slot metadata rebuilds, safe v4
  Runtime documents, and historical v5 documents without saved catalog payloads.
  Full save slots,
  additional runtime snapshot schema migration transforms, concrete
  missing-content remap policy, and broader deterministic replay scenarios
  remain target work.
- `HumanFortress.Runtime` owns the core tick pipeline, tick-facing job wrappers, runtime host/session core, command target graph, concrete system composition, and Runtime snapshot/query builders; App owns SadConsole/session UI adapters and logging/bootstrap callbacks.
- `HumanFortress.Jobs` contains real job executors, tunings, orchestration, diff emitters, adapters, job loggers, and profession assignment state; profession registry file loading now lives in `HumanFortress.Content`.
- `HumanFortress.Content` owns content definition loading, runtime registry bootstrap, the structured registry implementation, runtime content snapshot capture, and the first strict content failure policy; final compatibility naming is still transitional.
- The old legacy content registry and `ContentLoadCoordinator` path have been retired; normal bootstrap loads the structured registry only.

## What Counts As Optimization

Optimization means:

- lower CPU cost;
- lower allocation pressure;
- fewer live-world scans;
- clearer data ownership;
- deterministic replay parity;
- better debug/profiling visibility;
- less coupling that blocks safe parallelism;
- moving hot queries from broad scans to stable indexes;
- replacing repeated UI/world polling with snapshots or query DTOs.

Optimization does not mean:

- micro-optimizing before measuring;
- adding unsafe/SIMD/GPU work first;
- bypassing command/diff/commit boundaries;
- moving gameplay logic into App for convenience;
- adding more live-world reads to UI panels;
- parallelizing writes without read/write masks;
- changing content/save IDs for short-term speed;
- persisting derived caches that should be rebuilt from authoritative state.

## Optimization Rules

Hard constraints:

- Preserve determinism before speed. Stable ordering, seeded RNG, and replay parity are non-negotiable.
- Do not use wall clock, thread completion order, dictionary iteration order, unstable hash ordering, or non-deterministic floating point in simulation decisions.
- Keep pathfinding budget behavior fail-soft. Correctness tests should use test-specific generous budgets; production-budget deferral needs separate tests.
- Do not reintroduce navigation query-time rebuilds. Path queries read cache; commits mark dirty chunks; runtime rebuilds explicitly.
- Do not parallelize write phases until systems declare read/write masks and a scheduler can prove non-overlap.
- Do not persist derived caches. Rebuild nav/path/render/spatial caches from authoritative state.
- Do not add new gameplay rules to `HumanFortress.App`.
- Do not make UI components depend on live `World` when a snapshot/query DTO can serve the use case.
- Do not create new per-system movement authorities when a runtime-wide movement service is the target.

Engineering rule:

Measure first, then optimize. If a change cannot be checked by counters, stress tests, or replay equality, it should stay in research.

## Codex / Claude Guardrails

When an AI coding agent works from this document:

Do not:

- edit broad areas without a scoped task;
- make formatting-only changes while touching optimization code;
- add gameplay logic to `HumanFortress.App`;
- add new UI reads of live `World`;
- create a new `PathService` or `MovementExecutor` per new system;
- introduce `new Random()` or wall-clock seeded randomness;
- use `GetHashCode()` or dictionary iteration order for authoritative ordering;
- optimize by changing public content IDs, save IDs, or tile bit layout;
- parallelize `WriteTick` or MergeApply based on intuition;
- persist navigation/path/render caches;
- bypass `CommandQueue`, DiffLogs, or deterministic commit stages for convenience.

Do:

- inspect real source code before acting;
- preserve behavior unless the task explicitly says otherwise;
- add tests or counters with each optimization when practical;
- separate architecture boundary fixes from hot-path micro-optimizations;
- prefer small, buildable, reviewable changes;
- report uncertainty when source and documentation disagree.

## P-1: Architecture Preconditions Before Optimization

These are not performance optimizations by themselves. They are boundary fixes that should happen before broad optimization work, because otherwise performance changes may reinforce the wrong ownership model.

### A. Tighten Content Project Diagnostics And Build Correctness

Current issue:

- `HumanFortress.Content` now owns the active content loading path, structured registry implementation, and first strict-mode diagnostics API.
- Content source should not become dependent on Simulation runtime types as a long-term pattern.
- Historical namespaces are still preserved for source compatibility.

Build:

- Run a clean `dotnet build HumanFortress.sln` before optimizing content hot paths.
- Verify `HumanFortress.Content` project references match its source dependencies.
- Keep raw definition DTOs in Contracts when they cross module boundaries, and keep concrete loading/registry implementation in Content.
- Keep runtime catalogs and live instances in World/Simulation.
- Use `--init-only --strict-content --content-warnings-as-errors` as the current CI/release smoke path.

Acceptance:

- `HumanFortress.Content` builds from a clean checkout.
- Content definition loading does not require App or SadConsole.
- Structured registry load failure can fail CI in strict mode.
- Runtime receives a coherent content/registry snapshot rather than silently mixing partially-loaded registries.

### B. Keep Concrete Runtime Composition Out Of App

Current issue:

- Concrete runtime session/system composition has moved to `HumanFortress.Runtime`. The remaining risk is regression: new startup, debug, or UI features should not move gameplay system wiring back into App.

Build:

- Keep concrete simulation session host/composition classes in Runtime when they are not SadConsole/UI-specific.
- Keep App responsible for platform startup, state transitions, SadConsole host setup, input, and presentation.
- Runtime should own session lifecycle, system wiring, command queue, scheduler, navigation, jobs, and debug snapshot services.

Acceptance:

- A headless runtime path can start a simulation without referencing `HumanFortress.App`.
- Runtime can be tested without SadConsole/MonoGame.
- App no longer owns the concrete simulation composition root.

### C. Move App/Jobs Tick Wrappers Out Of App

Current issue:

- Real job executors, tunings, orchestration, diff emitters, adapters, callback loggers, and profession assignment state now live in `HumanFortress.Jobs`; tick-facing job wrappers now live in `HumanFortress.Runtime`; profession registry file loading now lives in `HumanFortress.Content`. App runtime bootstrap still binds the construction UI completion callback.

Build:

- Keep any remaining startup/session glue behind Runtime/Content contracts.
  Treat compatibility-namespace regression guards and temporary internal
  assembly bridge cleanup as separate build-hygiene tasks, not performance
  work.
- Keep only job UI/debug panels and input bindings in App.

Acceptance:

- Job systems can be registered and tested without referencing App.
- App no longer exposes concrete job systems to UI except through debug/query DTOs.

### D. Convert Runtime Access To Snapshot / Command / Debug Facade

Current issue:

- `FortressRuntimeAccess` has been narrowed into App role adapters over Runtime session ports. Main UI/read paths now use Contracts snapshot DTOs rather than live `World`, navigation, or concrete job systems, and aggregate frame/overlay data now flows through a Runtime publisher/cache with Runtime-authored presenter-frame identity, map-viewport changed-cell/row/screen-region deltas, and UI overlay section deltas. The remaining risk is regression and the lack of broader packed world-chunk/panel delta payloads plus presenter-side consumption for high-frequency rendering and UI panels.

Build:

- Keep focused runtime query/debug DTOs as the only UI data path.
- Keep command submission through a command sink.
- Keep rendering through Runtime/Contracts snapshot DTOs and the future versioned presenter data.
- Avoid adding bootstrap-only live-world seams to normal render/input/panel paths.

Acceptance:

- High-traffic UI panels can render common rows without scanning live world managers from App.
- UI reads stable snapshots or query DTOs, not concrete mutable managers.
- Debug panels have deterministic, stable sort keys.

### E. Standardize Test Infrastructure

Current issue:

- Existing smoke/regression tests are useful, but the project still needs standard test discovery and CI reporting.

Build:

- Move toward xUnit, NUnit, or MSTest module-specific test projects.
- Keep existing executable smoke tests only as a temporary bridge.
- Add a deterministic replay harness.

Acceptance:

- `dotnet test` works on a clean machine.
- Core/runtime/navigation/jobs/content tests do not require SadConsole.
- Replay hash tests can run headlessly.

### F. Define Diff Priority And Mutation Boundaries

Current issue:

- Diff systems must share one priority convention.
- Public world mutation APIs still rely heavily on convention.
- System precedence remains hardcoded in places.

Build:

- Define whether lower or higher priority wins globally.
- Add tests for same-tick conflict ordering.
- Introduce a stable system order table or registry.
- Plan `WorldReadView` / `WorldWriteContext` boundaries for future work.

Acceptance:

- Same-tick conflicts are resolved by documented stable keys.
- New systems do not need to invent their own priority semantics.
- World mutation outside command/diff/commit paths is easier to detect in review/tests.

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
- Counters can be consumed by debug UI without introducing new authoritative world writes.

### 2. Item Stack Merge Uses Position Index

Status: implemented in the source-only final hardening batch.

Implemented:

- Same-tile lookup uses `_posIndex`.
- Deterministic stack choice comes from the stable GUID-sorted tile list.
- The stack-match path does not allocate a broad `_instances.Values` LINQ list;
  a same-tile list is only built for mismatch diagnostics.

Acceptance:

- Spawning/adding 1,000 stackable items on a populated map stays within a documented budget.
- Existing item merge/split/move regression tests still pass.
- Replay hashes remain unchanged for equivalent inputs.

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
- Cache ordering does not depend on dictionary iteration order.

### 4. UI/Runtime Live-Read Reduction

Current issue:

- The broad active fortress UI live-read problem has been reduced: common map,
  overlay, drawer, debug, workshop, stockpile/zone, tile-inspection, and
  placement-preview paths consume Runtime/Contracts snapshot/query DTOs rather
  than live `World`, concrete job systems, or direct content catalog reads.
- The remaining optimization problem is presenter-side scale:
  App now consumes Runtime-authored map-viewport and UI-overlay section deltas
  through presenter caches, but broad packed world-chunk/panel deltas,
  panel-specific redraw skipping, virtualized large lists, and measured redraw
  budgets are still needed so high-frequency UI paths do less work without
  moving diff computation into App.

Build:

- Keep adding small query/debug DTOs only where a UI surface still needs a
  stable read model.
- Keep UI local state in `UiStore`; use Runtime/Contracts snapshot/query DTOs
  for simulation facts.
- Extend presenter-side redraw specialization beyond the current
  changed-cell/row/region map cache and overlay-section cache into panel deltas
  before optimizing with App-side comparisons.
- Virtualize large item/creature/alert lists and measure redraw work.

Acceptance:

- Large stock/item list scroll stays within UI budget.
- Panels do not need broad live-world scans for common display rows, and
  regression scans keep it that way.
- UI queries use stable sort keys.
- No new broad `World` access is added to UI render code.

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

- Runtime map/frame snapshot builders currently compute terrain/entity cell views during snapshot build.
- Active Runtime/Contracts DTOs now carry presenter-frame identity,
  map-viewport changed cells, changed rows, fixed-size screen regions, and UI
  overlay section deltas. The remaining gap is broader packed
  world-chunk/panel payloads, presenter-side delta application, and autotile or
  terrain view caching.

Build:

- Cache terrain/autotile/connect-mask view data by chunk/Z version.
- Extend deltas toward changed chunks/Z/row spans and panel payloads where they
  reduce measured redraw/build cost.
- Avoid full presenter clears when Runtime-authored deltas prove only row spans,
  screen regions, or panel sections changed.

Acceptance:

- Snapshot build cost scales with dirty visible chunks, not total visible map size.
- Autotile cache invalidates on L0/L2 topology changes.
- Snapshot hash/delta tests prove deltas reproduce full snapshots.

### 7. Runtime-Wide Navigation And Movement Services

Current issue:

- Historical audits warned against fragmented path budgets, caches, movement state, and movement statistics.
- Current code is better: Runtime-owned `RuntimeNavigationServices` creates
  per-job `PathService`, `WorldNavigationView`, and `MovementExecutor`
  contract bundles and registers path services for dirty-chunk cache
  invalidation. Movement execution still lives in job-specific
  shells/executors, so the future consolidation target remains a runtime-wide
  movement intent service.

Build:

- Keep `PathService`/path budget creation and cache registration centralized in
  Runtime.Navigation.
- Move toward a runtime `MovementSystem` that receives move intents, owns active movement state, resolves reservations/conflicts, and emits movement diffs.
- Job systems request movement; they should not each become independent movement authorities.

Acceptance:

- One place reports path budget/cache/movement stats.
- Multi-agent movement conflicts have one deterministic resolver.
- Jobs preserve current behavior while moving behind the shared service.
- No new job system creates an independent movement authority.

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
- Adding new gameplay logic to App.
- Adding new per-job movement executors.
- Reintroducing legacy+structured content registry coexistence.
- Optimizing before the Content/Runtime/Jobs/UI boundary work is understood.

## Validation Matrix

| Area | Test |
| --- | --- |
| Runtime | Headless/manual-tick Runtime smoke runs production composition without starting SadConsole or the background scheduler. |
| Jobs | Job executor cores and replay hashes can run in tests without SadConsole/MonoGame; tick wrappers remain Runtime-owned. |
| Content | `HumanFortress.Content` builds cleanly; structured content load can fail CI in strict mode. |
| Items | Spawn/merge 1,000 stackable items on a populated map under budget; existing split/move tests pass. |
| Path cache | Fill cache beyond capacity; eviction count and time stay bounded; dirty chunk invalidates traversed paths. |
| Transport | Dense construction/stockpile stress run shows bounded request churn and continuous progress. |
| UI | 10k item rows grouped/virtualized; scroll update stays under documented budget; no broad live-world scan for common rows. |
| Snapshot | Request-keyed Runtime frame publication plus full-snapshot presenter identity is covered; map viewport changed-cell/row/screen-region deltas and UI overlay section deltas equal full rebuild; future packed world-chunk/panel deltas, presenter-side consumption, and autotile cache invalidation remain target work. |
| Diff | Same-tick conflicts resolve through documented stable priority and system order rules. |
| Movement | One movement resolver owns active movement state and reports movement/path stats. |
| Determinism | Replay hashes unchanged before/after optimization with the same seed/input stream. |
| Budgets | Production budget deferral is tested separately from path correctness. |

## Priority Summary

| Priority | Work |
| --- | --- |
| P-1 | Content dependency/build verification, runtime composition out of App, App/Jobs wrappers out of App, RuntimeAccess snapshot facade, standard tests, diff priority semantics. |
| P0 | Perf counters/stress harness, item spawn position-index merge, PathCache LRU/reverse-index fix, UI live-read reduction. |
| P1 | Transport request stability, snapshot delta/autotile cache, runtime-wide movement service, compiled hot tag queries. |
| P2 | Navigation search layers, scheduler stage graph, hot/cold data layout, worldgen scaling checks. |
| Research | ECS migration, GPU pathfinding, unsafe SIMD navigation. |

## One-Line Principle

Optimization work is only successful if the optimized code is faster, deterministic, testable, and closer to the intended architecture boundaries than before.
