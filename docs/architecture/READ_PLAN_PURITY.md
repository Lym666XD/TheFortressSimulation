# Read/Plan Purity And Sequential Compatibility

Status: Stage 4 transition contract, 2026-07-11

`ITick.ReadTick` is reserved for read-only planning. It may produce tick-local
immutable intents, but it must not drain queues, reserve resources, advance
cursors, mutate job state, emit authoritative diffs, or change World state.

Production registration currently has three systems:

| Registered system | Read behavior | Write behavior |
| --- | --- | --- |
| `BuildableConstructionSystem` | No-op | Runs its explicitly named legacy compatibility preparation and application. |
| `UnifiedJobsOrchestrator` | Runs only `IReadPlanStage` implementations. | Runs all declared sequential compatibility stages in the pre-existing deterministic order. |
| `SanitizeSystem` | No-op | Emits bounded sanitizer diffs. |

## Remaining Sequential Compatibility Paths

The following paths are intentionally **not** described as pure planners. Their
preparation and application both run inside serialized `WriteTick` through
`ISequentialCompatibilityStage`:

- mining designation intake/cursor advancement and mining job intake/assignment;
- hauling designation intake, stockpile request emission, and transport execution;
- construction designation/material request intake and construction execution;
- craft queue status/request mutation, assignment, and execution;
- buildable designation consumption and placeable creation.

These paths preserve the old two-pass planner order and the old adjacent
executor prepare/apply order. They remain migration debt: they are serialized
and honestly named, but are not failure-atomic intent/resolve/commit flows.

Transport is the first family targeted for replacement with immutable snapshot,
pure intent planning, stable resolution, and transactional commit. Other
families stay on the compatibility contract until migrated; renaming them to
planner or parallelizing them does not close that debt.

`ReadPlanPurityRegressionTests` protects authority version/fingerprint stability
through Read/Plan, scheduler failed-read write skipping, and the type boundary
that prevents legacy executor shells from masquerading as `ITick` systems.
