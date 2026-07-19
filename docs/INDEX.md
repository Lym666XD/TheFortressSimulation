# HumanFortress Documentation Index

Updated: 2026-07-19
Status: current documentation map

This is the entry point for project documentation. The codebase is in an active architecture refactor, so treat source code as authoritative when a document conflicts with implementation.

Document status terms:

- Current: best available description of the present implementation.
- Target: intended architecture or design that is not fully implemented.
- Reference: domain, research, or historical context that may inform current work.
- Snapshot: historical status at a point in time.
- Archive: superseded plans, one-off notes, and obsolete operating instructions.

## Current Architecture

- [Architecture Overview](architecture/GAME_ARCHITECTURE.md) - current project layout, runtime/content/jobs boundaries, and known gaps.
- [Game State Flow](architecture/GAME_STATE_FLOW.md) - target game-state model with current content/runtime notes.
- [Update Order](architecture/UPDATE_ORDER.md) - target stage model; current code uses `TickScheduler` read/barrier/write plus Runtime pre/post tick hooks.
- [Concurrency Model](architecture/CONCURRENCY_MODEL.md) - normative concurrency and determinism model.
- [Chunk and Data Layout](architecture/CHUNK_AND_DATA_LAYOUT.md)
- [Diff Log and Merge Strategies](architecture/DIFF_LOG_AND_MERGE_STRATEGIES.md) - target merge contract with current implementation notes.
- [Chunk Actor Protocol](architecture/CHUNK_ACTOR_PROTOCOL.md)
- [Simulation LOD Policy](architecture/SIM_LOD_POLICY.md)
- [Save Format](architecture/SAVE_FORMAT.md)
- [Save And Replay Architecture](architecture/SAVE_REPLAY_ARCHITECTURE.md) - current Runtime-owned save/replay vertical slice and remaining persistence work.
- [Error Handling Policy](architecture/ERROR_HANDLING_POLICY.md)
- [Determinism CI](architecture/DETERMINISM_CI.md)

## Runtime And Operations

- [Run And Test Guide](operations/README-RUN.md) - current source-run, test, and app argument notes.
- [Scenario Evidence](../benchmarks/README.md) - versioned determinism/scale
  profiles, artifact interpretation, reproduction commands, and dated decisions.

## Simulation Systems

- [Work And Jobs System](simulation/WORK_AND_JOBS_SYSTEM.md) - current planners, executors, App/Jobs boundary, and target scheduler constraints.
- [Transport System](simulation/TRANSPORT_SYSTEM.md) - current hauling/transport implementation notes.
- [Orders Spec](simulation/ORDERS_SPEC.md)
- [Mining System Spec](simulation/MININGSYSTEM_SPEC.md)
- [Stockpile Spec](simulation/STOCKPILE_SPEC.md)
- [Navigation Spec](simulation/NAVIGATION_SPEC.md)
- [Navigation Research](simulation/NAVIGATION_RESEARCH.md)
- [Zone Spec](simulation/ZONE_SPEC.md)
- [Creature Spec](simulation/CREATURE_SPEC.md)
- [Tile Spec](simulation/TILE_SPEC.md)
- [Field Spec](simulation/FIELD_SPEC.md)
- [Fluids Solver Spec](simulation/FLUIDS_SOLVER_SPEC.md)
- [Director Spec](simulation/DIRECTOR_SPEC.md)
- [Vehicle Spec](simulation/VEHICLE_SPEC.md)

## Content And Data

- [Content System](content/CONTENT_SYSTEM.md) - current content loading boundary and future pack pipeline notes.
- [Items Spec](content/ITEMS_SPEC.md)
- [Recipe Spec](content/RECIPE_SPEC.md)
- [Buildable Spec](content/BUILDABLE_SPEC.md)
- [Placeable Spec](content/PLACEABLE_SPEC.md)
- [Materials Spec](content/MATERIALS_SPEC.md)
- [Tiles, Materials, Geology, Terrain Architecture](content/TILES_MATERIALS_ARCHITECTURE.md)
- [Tuning Files](content/TUNING_FILES.md)

Machine-readable current sources:

- `../content/registries/`
- `../content/schemas/`
- `../data/core/items/`
- `../data/core/creatures/`
- `../data/core/workshops/`
- `../data/core/recipes/`
- `../data/core/placeable/`

## UI And Input

- [UI System](ui/UI_SYSTEM.md) - current App UI/input/rendering implementation map and remaining orchestration/presenter-boundary gaps.
- [Controls](ui/CONTROLS.md) - current player-facing control summary.
- [UI And Input Model](ui/UI_AND_INPUT_MODEL.md) - target MVU/snapshot model; active fortress UI now uses Runtime/Contracts DTOs for ordinary read paths, with remaining work around broader presenter deltas and UI model hardening.
- [Input Spec](ui/INPUT_SPEC.md) - target input contexts and actions.
- [UI Spec](ui/UI_SPEC.md) - target SadConsole layout and interaction model.
- [Rendering Snapshot](ui/RENDERING_SNAPSHOT.md) - target immutable rendering contract.

## Domain Design

- [Industries](industries/README.md) - industry-level design notes and source chain material.
- [Workshops](workshops/00_INDEX.md) - human-readable workshop layer. JSON in `data/core/workshops/` is the implementation source of truth.
- [Worldbuilding Review](worldbuilding/WORLDBUILDING_REVIEW_v1.md)

## Worldgen

- [Mapgen Pipeline](worldgen/MAPGEN_PIPELINE.md)
- [Geology Compiler Spec](worldgen/GEOLOGY_COMPILER_SPEC.md)

## Planning And Refactor Notes

- [Staged Refactor Target](planning/STAGED_REFACTOR_TARGET.md) - controlling
  audit ledger, current stage, ordered batches, and acceptance gates.
- [Project Rules](planning/RULES.md)
- [Agent Prompt](planning/AGENT_PROMPT.md)

## Status Snapshots

Historical implementation snapshots are archived, not current operating manuals:

- [Archived Status Snapshots](archive/status/README.md)

## Archive

Archived files are under [Archive](archive/README.md). Do not use archived documents as current implementation guidance unless a current document explicitly points to them.

Recently archived refactor/reference docs:

- [Archived Milestone Plan](archive/plans/MILESTONE.md) - historical strategic milestone plan; current status lives in the staged refactor target.
- [Archived 2026-07-07 Audit Report](archive/plans/HumanFortress_审计报告_2026-07-07.md) - historical external audit source; current reconciled status lives in the staged refactor target and architecture overview.
- [Archived Runtime Propagation Requirements](archive/architecture/RUNTIME_PROPAGATION_REQUIREMENTS.md) - completed/superseded TerrainBits/geology propagation checklist.
- [Archived Concurrency Research](archive/architecture/CONCURRENCY_RESEARCH.md) - background research; normative rules live in the concurrency model and project rules.
- [Archived Status Snapshot Pointer](archive/status/STATUS_SNAPSHOT_POINTER.md) - former current-tree pointer moved out of the active documentation set.
- [Archived Interview Briefing](archive/reference/HUMANFORTRESS_INTERVIEW_BRIEFING.md) - historical June 2026 project briefing; current architecture and ownership have moved on.
- [Archived Workshop JSON Drafts](archive/other/) - old `docs/other/core_workshop_*.json` copies; current machine-readable workshop data lives in `data/core/workshops`.
