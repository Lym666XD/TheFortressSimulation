# Architecture-Safe Optimization Backlog

Updated: 2026-07-11
Status: measured optimization backlog, subordinate to authority correctness

This document lists performance work that is safe to consider after the
relevant ownership and correctness contracts are closed. It is not permission
to optimize code merely because it looks expensive.

The active implementation order lives in
`ARCHITECTURE_REFACTOR_MASTER_PLAN.md`. When this document conflicts with the
master plan or `RULES.md`, the master plan and rules win.

## Decision Rule

An optimization is ready only when all of the following are true:

1. The authoritative owner and mutation phase are explicit.
2. A behavior or replay test protects correctness.
3. A repeatable workload demonstrates a bottleneck.
4. The change has a measurable budget and before/after result.
5. Equivalent inputs produce the same authoritative result.

Optimizations must not:

- add live World reads to App, rendering, save, or diagnostics;
- use wall-clock time to choose simulation-visible work;
- depend on dictionary order, thread completion order, or unstable hashes;
- bypass command, intent/diff, resolver, or commit ownership;
- persist derived navigation, render, spatial, or presenter caches;
- introduce another per-job path or movement authority;
- change content/save IDs or tile layout without a migration contract;
- hide unsupported state behind a successful restore result.

## Current Baseline

The current branch already has useful foundations:

- stable owner-ordered snapshots for many collections;
- indexed item/creature lookup by a wider entity key;
- item position indexes and chunk-local world storage;
- deterministic path node/request budgets;
- path-cache reverse indexes and Runtime-owned invalidation registration;
- Runtime/Contracts frame DTOs with map and overlay deltas;
- Jobs-owned executor cores and replay snapshots;
- a manual-tick deterministic Runtime smoke scenario;
- a first Linux/Windows build and smoke CI workflow.

These foundations do not close the following correctness gaps:

- frame DTOs are still assembled from live mutable state;
- save capture is not a scheduler-owned tick barrier;
- stack merge can corrupt a tile index and merge incompatible items;
- partial paths can be classified and cached as complete;
- placeable/door topology is missing from navigation mapping;
- reservation release is not owner/generation safe;
- restore does not reproduce a non-zero-tick continuation;
- content schema and mechanical hashes are incomplete.

Do not optimize around those defects. Fix them first.

## Measurement Harness

### Required scenarios

Use deterministic seeds and recorded command streams.

| Tier | Workers | Items | Active + queued jobs | Map | Purpose |
| --- | ---: | ---: | ---: | --- | --- |
| Early | 50 | 10,000 | 500 | 4x4 chunks | normal startup play |
| Mid | 250 | 50,000 | 3,000 | 8x8 chunks | sustained colony |
| Target | 1,000 | 100,000 | 10,000 | 16x16 chunks | design target |
| Stress | configurable | configurable | configurable | multi-Z | pathological topology/path/save load |

The exact fixture may initially be smaller, but its scale and seed must be
versioned so results remain comparable.

### Required counters

- tick p50/p95/p99 and maximum;
- per-stage and per-system work counts;
- allocation bytes per tick and snapshot publication;
- path requests, expansions, partials, cache hits/misses/evictions;
- dirty topology chunks and navigation rebuilds;
- planner scanned/accepted/deferred/starved counts;
- active/backlog/reservation counts by job family;
- snapshot full/delta bytes and presenter redraw cells/sections;
- save capture duration, serialized bytes, and peak working set;
- final replay/checkpoint hashes for one-thread and future N-thread runs.

Wall-clock metrics are diagnostic only. They must never feed simulation
budgets, ordering, backoff, or success/failure decisions.

## P0: Instrumentation After Correctness Gates

### 1. Standard stress runner

Build a headless scenario runner over production Runtime composition. It should
accept a seed, workload tier, tick count, and command stream, then emit machine-
readable counters plus final hashes.

Acceptance:

- identical inputs produce identical authoritative hashes;
- counters can be disabled without changing results;
- CI runs a short smoke tier; longer tiers run on demand;
- failed invariants or timeouts return a non-zero exit code.

### 2. Allocation and snapshot diagnostics

Measure Runtime snapshot construction, App presenter application, and save
capture separately. Do not treat a DTO delta as an optimization until the full
payload allocation and presenter redraw costs are lower.

Acceptance:

- full and delta payload byte counts are reported;
- delta application is proven equivalent to a full rebuild;
- the measured hot surface, not a generic facade, owns the optimization.

### 3. Planner fairness diagnostics

Record stable cursor position, scanned candidates, accepted intents, rejection
reasons, and oldest-wait age. This must precede changing scan budgets.

Acceptance:

- bounded planners prove that later items receive service;
- save/load preserves behavior-affecting cursors;
- dictionary or insertion order cannot become the fairness protocol.

## P1: Likely High-Return Work

### 4. Item and resource indexes

After stack compatibility and conservation are fixed, profile:

- tag/material availability indexes;
- chunk/local ground-item indexes;
- construction/craft requirement brokers;
- workshop GUID and active-site indexes.

Do not build an index without declaring its source authority, invalidation
events, rebuild path, save policy, and deterministic ordering.

Acceptance:

- item quantity and identity conservation tests remain green;
- index rebuild from authoritative state is deterministic;
- hot queries no longer copy or scan the entire instance table;
- index maintenance cost is included in the measurement.

### 5. Path cache maintenance

Current LRU eviction and reverse-index removal contain linear scans. Replace
them only after counters show material cost.

Candidate design:

- dictionary plus deterministic linked LRU order;
- `cache key -> traversed chunks` reverse ownership;
- targeted removal without scanning all chunk sets;
- collision-safe request identity rather than a 32-bit request hash alone.

Acceptance:

- partial paths never enter the complete-path cache;
- terrain/placeable/door changes invalidate every traversed path;
- eviction order is stable and collision-safe;
- cache warmth cannot change the number of simulation-visible requests served.

### 6. Stable work queues and resource brokers

Replace repeated global scans with explicit owner queues/cursors only after the
intent/commit boundary is established.

Candidates:

- construction material shortfall queue;
- craft readiness queue;
- stockpile maintenance queue;
- transport retry/backoff queue;
- dirty workshop/site queue.

Acceptance:

- no starvation under bounded budgets;
- retry order and cursors are save/replay authority;
- duplicate work is idempotent or rejected with a deterministic reason;
- queue state has focused replay/continuation tests.

### 7. Committed read-model publication

This is first a correctness boundary, then a performance opportunity. The
scheduler publishes one immutable committed version; Runtime builds or retains
surface-specific projections from that version.

After the boundary exists, consider:

- dirty chunk/Z map projections;
- packed immutable terrain/entity rows;
- panel-specific revision numbers;
- virtualized item/creature/workshop lists;
- cached glyph/autotile output keyed by authoritative topology revision.

Acceptance:

- App never compares or reads live lower-layer objects;
- a presented frame has one committed tick/version;
- snapshot cost scales with changed visible authority where practical;
- slow presentation cannot block or mutate the simulation.

### 8. Runtime-wide movement authority

Current Runtime centralizes navigation service creation, but job executors still
own independent movement progress. Consolidation is ready only after movement
state and reservation semantics are specified.

Target responsibilities:

- receive immutable movement intents;
- arbitrate tile/entity reservations deterministically;
- own active movement progress and pacing;
- emit accepted/rejected movement results;
- serialize every progress field affecting continuation;
- expose one path/movement diagnostic surface.

Acceptance:

- jobs do not directly own competing movement authorities;
- save-at-T/load/run-N equals uninterrupted execution;
- conflict resolution has stable producer and entity tie-breaks;
- navigation caches remain derived and rebuildable.

## P2: Strategic Work, Profiling Required

### 9. Hierarchical navigation

Consider region/portal graphs, shared-destination flow fields, or local
refinement only when path expansions dominate the target-tier budget.

Rules:

- all layers are derived from committed topology;
- exact A* remains a correctness fallback;
- dynamic blockers and vertical links have explicit invalidation;
- specialized results cannot silently weaken path semantics.

### 10. Hot/cold and SoA layout

Small, measured hot/cold splits are allowed. A full ECS conversion is not a
default destination.

Good candidates after profiling:

- packed immutable tile/nav read data;
- hot position/state arrays with stable handles;
- pooled A* scratch buffers;
- compact compiled tag handles with a stable content manifest.

Every handle requires collision rules, generation/reuse policy, save binding,
and diagnostics back to canonical IDs.

### 11. Stage DAG and chunk-parallel resolve

Parallel resolve/commit requires:

- explicit stage dependencies;
- declared read/write sets;
- immutable input snapshots;
- chunk/resource ownership;
- deterministic cross-chunk transfer protocol;
- merge/conflict property tests;
- one-thread/N-thread replay equality.

Do not parallelize the current coarse `ReadTick`/`WriteTick` list. Several
current `ReadTick` implementations still mutate authority.

### 12. World generation scaling

World generation can be optimized independently when its content inputs,
output ordering, and hashes are stable. Measure phase budgets, allocations,
and output hashes across size tiers before adding parallelism.

## Explicit Non-Goals

Do not schedule these in the current architecture hardening phase:

- broad ECS rewrite;
- Actor-per-chunk runtime;
- GPU pathfinding or simulation;
- unsafe/SIMD-first rewrites;
- parallel authoritative writes;
- persisted derived caches;
- packed presenter work without profiling;
- optimizations that make save format or content identity less explicit.

## Optimization Change Template

Every optimization PR should state:

```text
Owner:
Authoritative source:
Derived state/index:
Invalidation/rebuild path:
Workload and seed:
Before measurement:
After measurement:
Correctness tests:
Replay/hash comparison:
Fallback/rollback:
```

## Final Principle

An optimization is successful only when it is measured, deterministic,
rebuildable where derived, behavior-tested, and closer to the intended
ownership model than the code it replaces.
