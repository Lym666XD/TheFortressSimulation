# Transport And Hauling System

Updated: 2026-06-12
Status: current implementation notes

This document merges the old hauling spec, transport architecture notes, and hauling refactor plan into one current description. The old `HaulJobSystem` terminology is obsolete for current code.

For the broader planner/executor boundary and scheduler direction, read [WORK_AND_JOBS_SYSTEM.md](WORK_AND_JOBS_SYSTEM.md).

## Current Shape

The current hauling path is a transport pipeline:

```text
Orders / gameplay producers
  -> HaulingSystem and other planners
  -> TransportRequestQueue
  -> App TransportJobSystem wrapper
  -> HumanFortress.Jobs.Transport.TransportJobExecutor
  -> DiffLog / ItemsDiffLog
  -> post-tick diff applicators
```

Current important classes:

- `HumanFortress.Simulation.Orders.HaulingSystem`
- `HumanFortress.Simulation.Jobs.TransportRequestQueue`
- `HumanFortress.App.Jobs.TransportJobSystem`
- `HumanFortress.Jobs.Transport.TransportJobExecutor`
- `HumanFortress.Jobs.Transport.TransportAssignmentHandler`
- `HumanFortress.Jobs.Transport.TransportPickupHandler`
- `HumanFortress.Jobs.Transport.TransportDeliveryHandler`
- `HumanFortress.Jobs.Transport.TransportReplanHandler`
- `HumanFortress.App.Jobs.TransportDiffEmitter`

`HumanFortress.App.Jobs.TransportJobSystem` is now a composition shell. Most transport execution behavior lives in `HumanFortress.Jobs.Transport`.

## Producers

Current producer examples:

- `HaulingSystem` creates stockpile/item transport requests.
- `ConstructionMaterialsPlanner` creates site material transport requests.
- `CraftPlanner` creates workshop input transport requests.

Requests flow into `ITransportRequestQueue`.

The queue provides deterministic intake and backlog behavior. Producers should not reach into transport executor internals.

## Execution

Transport execution assigns workers, paths to pickup, marks items carried, moves to destination, drops or merges items, releases reservations, and records stats.

Typical job stages:

```text
Request intake
  -> worker assignment
  -> path to item
  -> pickup / split stack / mark carried
  -> path to destination
  -> delivery / move item / unmark carried
  -> finalization and reservation cleanup
```

Transport writes are routed through diff seams rather than direct world mutation.

Common diff operations:

- `MoveCreature`
- `MarkCarried`
- `MoveItem`
- `UnmarkCarried`
- item split/add/remove operations through item diffs where appropriate

## Update Order Fit

Current tick fit:

```text
PreTick
  command stage executes queued commands

Read phase
  planners enqueue transport requests
  transport executor drains intake/backlog and assigns work

Barrier

Write phase
  active transport jobs step movement and emit diffs

PostTick
  items and simulation diffs are merged/applied
  dirty navigation chunks are rebuilt
```

This is compatible with the target `UPDATE_ORDER.md`, but it is not yet a full per-chunk MergeApply scheduler.

## Determinism Requirements

Transport must preserve:

- stable request ordering;
- stable worker candidate ordering;
- deterministic path seeding and path-service behavior;
- bounded backlog/carryover behavior;
- deterministic rollback on no-path, moved pickup targets, invalid destinations, missing workers, and disappearing inputs;
- reservation cleanup on all failure paths.

Regression tests currently cover several of these cases in `tests/HumanFortress.App.Tests`.

## Tunings

Transport and scheduler limits are loaded through the structured content registry and App tuning adapters:

- `content/registries/tuning.scheduler.json`
- `content/registries/tuning.hauling.json`
- `content/registries/tuning.navigation.json`

`SchedulerTunings` currently lives in App and is passed into runtime system composition.

## Current Boundaries And Gaps

Current:

- Transport executor core is Jobs-owned.
- App owns concrete diff emitters, logger/profession adapters, and `TransportJobSystem` wrapper.
- Runtime composition wires transport through `FortressRuntimeSystemsFactory` and `FortressRuntimeSystemGroups`, then exposes it through `SimulationRuntimeSystems`.

Still pending:

- move remaining App-owned job wrappers/composition pieces to Runtime or Jobs;
- make movement/path ownership more centralized;
- add a runtime-wide movement/reservation service when boundaries stabilize;
- consider per-chunk request sharding and chunk-parallel MergeApply after deterministic behavior is fully covered.

## Superseded Terms

These names in older docs are historical:

- `HaulJobSystem`
- `HAULING_POLICY.md`
- direct `PlannedMove` outbox as the primary transport boundary

Use `TransportJobSystem`, `TransportRequestQueue`, and `TransportJobExecutor` for current work.
