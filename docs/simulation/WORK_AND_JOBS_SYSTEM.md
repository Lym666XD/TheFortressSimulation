# Work And Jobs System

Updated: 2026-07-10
Status: current implementation notes plus target scheduler constraints

This document is the active entry point for fortress work planning, job execution,
and deterministic scheduler direction. It replaces the older active documents
`JOBS_SPEC.md`, `JOB_SCHEDULER_SPEC.md`, and `UNIFIED_WORK_SCHEDULER.md`.

Use [TRANSPORT_SYSTEM.md](TRANSPORT_SYSTEM.md) for transport-specific behavior.

## Current Runtime Shape

Current fortress work is composed by Runtime factories with App-provided
platform/UI callbacks, then executed through the normal runtime tick scheduler:

```text
FortressPlayGameState
  -> IFortressPlayRuntimeHost.InitializeWorld
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

App still supplies logging callbacks, UI completion handlers, session flow, and
the user setting that enables optional startup auto-dig. Runtime owns the
content snapshot load/apply path and the auto-dig command seeding logic.

## Current Planners

Current planners are read-phase systems. They inspect world state and enqueue
plans or transport requests without directly applying authoritative mutations.

- `MiningSystem`: Simulation-owned mining planner. The main file keeps planner
  state and `PlannedDig` identity; tick/drain/outbox, scan/cursor,
  cancellation, helper, and active-designation cursor behavior live in focused
  files.
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
- `CraftPlanner` now lives in `HumanFortress.Jobs.Craft`, while concrete
  runtime composition is Runtime-owned through focused composition groups.

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

Planner handoff queues inside this shape are deterministic owner queues rather
than concurrent collection enumeration contracts. Mining keeps a budgeted
PlannedDig owner queue between planner write and executor intake; buildable
construction keeps a short owner queue between read and write placement; the
older structural construction planned-build outbox has been removed because
structural construction now creates construction sites directly in its write
phase. Jobs-owned retry/backlog queues follow the same rule: mining, transport,
and craft backlog/planner queues are owner FIFO state because their order feeds
replay snapshots, restore, retry, and worker assignment.

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
`TransportJobExecutor` itself is split by read/intake, write tick,
debug/replay snapshot, scheduling hint, and helper partials so long-horizon
transport state remains Jobs-owned without regrowing a mixed executor file.

See [TRANSPORT_SYSTEM.md](TRANSPORT_SYSTEM.md) for request semantics,
assignment, pickup, delivery, reservations, and transport-specific tunings.

## Tunings

Current scheduler and work limits are loaded through content and captured during
runtime composition:

- `content/registries/tuning.scheduler.json`
- `content/registries/tuning.hauling.json`
- `content/registries/tuning.navigation.json`

`SchedulerTunings` and `WorkshopTunings` live in Jobs configuration code.
`FortressRuntimeDependencies` loads a Content-owned runtime snapshot, then
creates the tuning objects used by Runtime job wrappers and
`UnifiedJobsOrchestrator`.

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
- Runtime owns fortress-specific composition, tick-facing job wrappers, runtime
  adapters, and content-derived tuning construction.
- App owns session flow, SadConsole UI/input, logger callback binding, UI
  completion binding, and command enqueue requests through Runtime ports.
- Runtime-authored job/debug snapshot DTOs feed App job panels and scheduler
  diagnostics; App should not read concrete job executors or live job systems.
- Transport request intake is the current shared boundary for hauling-like work.

Still pending:

- centralize movement and reservation ownership;
- convert remaining direct world writes to command targets or diff applicators;
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
