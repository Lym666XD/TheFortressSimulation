# Work And Jobs System

Updated: 2026-06-18
Status: current implementation notes plus target scheduler constraints

This document is the active entry point for fortress work planning, job execution,
and deterministic scheduler direction. It replaces the older active documents
`JOBS_SPEC.md`, `JOB_SCHEDULER_SPEC.md`, and `UNIFIED_WORK_SCHEDULER.md`.

Use [TRANSPORT_SYSTEM.md](TRANSPORT_SYSTEM.md) for transport-specific behavior.

## Current Runtime Shape

Current fortress work is composed by Runtime factories with App-provided logging
and optional command delegates, then executed through the normal runtime tick
scheduler:

```text
GameStateManager.InitializeWorld
  -> SimulationRuntimeSessionFactory.CreateNew
  -> SimulationRuntimeHost<SimulationRuntimeSystems>
  -> SimulationRuntimeSystems.RegisterWith(TickScheduler)
  -> BuildableConstructionSystem
  -> UnifiedJobsOrchestrator
  -> SanitizeSystem
```

`HumanFortress.Runtime` owns the generic session host, command stage, pre/post
tick pipeline, navigation rebuild barrier, concrete fortress system collection,
runtime dependency groups, system factories, host factory, and startup
orchestration.

The current Runtime composition split is:

- `FortressRuntimeDependencies` loads catalogs, tunings, and workforce state.
- `FortressRuntimePlanningSystems` builds planners and the shared transport queue.
- `FortressRuntimeJobSystems` builds Runtime job shells over Jobs-owned executor cores.
- `FortressRuntimeSystemsFactory` assembles the concrete system collection.
- `SimulationRuntimeSystems` exposes the systems and registers the tick systems.

App still supplies logging callbacks, the active content snapshot, and optional
App command delegates such as auto-dig.

## Current Planners

Current planners are read-phase systems. They inspect world state and enqueue
plans or transport requests without directly applying authoritative mutations.

- `MiningSystem`
- `HaulingSystem`
- `ConstructionMaterialsPlanner`
- `ConstructionSystem`
- `BuildableConstructionSystem`
- `CraftPlanner`

Important boundaries:

- `HaulingSystem`, `ConstructionMaterialsPlanner`, and `CraftPlanner` enqueue
  transport requests into `ITransportRequestQueue`.
- `BuildableConstructionSystem` is registered as its own tick system before the
  unified jobs orchestrator.
- `CraftPlanner` now lives in `HumanFortress.Jobs.Craft`, while the concrete
  runtime composition is still App-owned.

## Current Executors

Most executor cores live in `HumanFortress.Jobs`:

- `TransportJobExecutor`
- `MiningJobExecutor`
- `ConstructionJobExecutor`
- `CraftJobExecutor`

`HumanFortress.Runtime.Jobs` owns the tick-facing composition shells:

- `TransportJobSystem`
- `MiningJobSystem`
- `ConstructionJobSystem`
- `CraftJobSystem`

`HumanFortress.Jobs` owns executor cores, diff emitters, callback loggers,
profession assignment state, scheduler/workshop tuning types, worker selection,
and concrete adapters. Profession contracts compile from
`HumanFortress.Contracts.Jobs`, and profession registry JSON loading compiles
from `HumanFortress.Content.Definitions`.

Do not add new domain logic to the Runtime shells unless a seam is missing and the
change is part of a migration.

## Current Tick Fit

Runtime tick shape:

```text
PreTick
  SimulationCommandStage executes queued commands.

Read phase
  BuildableConstructionSystem.ReadTick
  UnifiedJobsOrchestrator.ReadTick
    MiningSystem.ReadTick
    HaulingSystem.ReadTick
    ConstructionMaterialsPlanner.ReadTick
    ConstructionSystem.ReadTick
    CraftPlanner.ReadTick
  SanitizeSystem.ReadTick

Barrier

Write phase
  BuildableConstructionSystem.WriteTick
  UnifiedJobsOrchestrator.WriteTick
    planner WriteTick flushes
    TransportJobSystem.ReadTick + WriteTick
    MiningJobSystem.ReadTick + WriteTick
    ConstructionJobSystem.ReadTick + WriteTick
    CraftJobSystem.ReadTick + WriteTick
  SanitizeSystem.WriteTick

PostTick
  SimulationTickPipeline applies item pre-simulation diffs,
  simulation diffs, creature diffs, item additions,
  then rebuilds dirty navigation chunks.
```

This is compatible with the target update-order model, but it is not a full
per-chunk MergeApply scheduler yet.

## Transport Boundary

Transport is the current shared movement/material-delivery path:

```text
producers
  -> ITransportRequestQueue
  -> Runtime TransportJobSystem
  -> Jobs TransportJobExecutor
  -> DiffLog / ItemsDiffLog
```

Current producers include hauling, construction material delivery, and craft
input delivery. Producers should not depend on transport executor internals.

See [TRANSPORT_SYSTEM.md](TRANSPORT_SYSTEM.md) for request semantics,
assignment, pickup, delivery, reservations, and transport-specific tunings.

## Tunings

Current scheduler and work limits are loaded through content and captured during
runtime composition:

- `content/registries/tuning.scheduler.json`
- `content/registries/tuning.hauling.json`
- `content/registries/tuning.navigation.json`

`SchedulerTunings` currently lives in App. `FortressRuntimeDependencies` loads a
Content-owned runtime snapshot, then creates the tuning objects used by the App
composition shells and `UnifiedJobsOrchestrator`.

Current honored scheduler concerns include:

- per-subsystem intake budgets;
- backpressure carryover limits;
- transport active-job limits;
- worker selection policy;
- hauling throttling when mining backlog pressure is high.

## Determinism Rules

Current work systems must preserve:

- stable planner and executor order;
- stable request queue order;
- stable worker candidate order;
- deterministic path seeds and path-service inputs;
- bounded intake and backlog carryover;
- diff-only authoritative mutation for terrain, item, creature, and movement
  changes where the current subsystem has a diff seam;
- deterministic cleanup for no-path, moved targets, missing items, disappearing
  workers, invalid destinations, and reservation failure paths.

Avoid relying on dictionary iteration order, wall-clock time, thread scheduling,
or logger side effects for simulation decisions.

## Current Boundaries And Gaps

Current:

- Runtime owns the generic host, command stage, and pre/post tick pipeline.
- Jobs owns the main executor cores for transport, mining, construction, and
  crafting.
- App owns fortress-specific composition, job shells, adapters, tunings, and UI
  access to job debug state.
- Transport request intake is the current shared boundary for hauling-like work.

Still pending:

- remove compatibility namespaces once clean seams exist;
- centralize movement and reservation ownership;
- convert remaining direct world writes to command targets or diff applicators;
- add a formal runtime/debug snapshot facade for UI job panels;
- add broader determinism tests for replay parity, scheduler jitter, and
  backlog/carryover stability;
- implement per-chunk MergeApply only after current single-threaded behavior is
  fully covered.

## Target Scheduler Direction

The long-term scheduler target is still:

```text
Plan jobs
  read-only, chunk-local where possible
  -> barrier
MergeApply jobs
  one writer per chunk/stage
  sorted diffs
  deterministic cross-chunk commit order
```

Target-only pieces that are not current code:

- work-stealing queues;
- per-worker deques;
- actor mailbox drain jobs;
- per-chunk diff sinks for all job systems;
- scheduler jitter CI across thread counts;
- UI heatmaps and long-tail scheduler panels.

Treat these as design constraints for future extraction, not as existing runtime
behavior.

## Archived Source Documents

Older source documents were kept as historical reference under `docs/archive`:

- `docs/archive/simulation/JOBS_SPEC_LEGACY.md`
- `docs/archive/simulation/JOB_SCHEDULER_SPEC_TARGET.md`
- `docs/archive/simulation/UNIFIED_WORK_SCHEDULER_DESIGN.md`

They may contain useful rationale, but this file and the current source code are
the active guide for implementation work.
