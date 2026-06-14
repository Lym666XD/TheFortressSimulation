# Archive

Updated: 2026-06-12
Status: historical reference

Files in this directory are not current implementation guidance. They are kept only when they preserve design history, future-direction notes, or debugging context that is not fully captured by current docs.

## Current Replacements

- Use [../INDEX.md](../INDEX.md) as the documentation entry point.
- Use [../architecture/GAME_ARCHITECTURE.md](../architecture/GAME_ARCHITECTURE.md) for current architecture shape.
- Use [../content/CONTENT_SYSTEM.md](../content/CONTENT_SYSTEM.md) for content loading.
- Use [../simulation/WORK_AND_JOBS_SYSTEM.md](../simulation/WORK_AND_JOBS_SYSTEM.md) for current work planning, job execution, and scheduler boundaries.
- Use [../simulation/TRANSPORT_SYSTEM.md](../simulation/TRANSPORT_SYSTEM.md) for hauling/transport.
- Use [../ui/UI_SYSTEM.md](../ui/UI_SYSTEM.md) for current UI/input/rendering implementation notes.
- Use [../ui/CONTROLS.md](../ui/CONTROLS.md) for current player-facing controls.

## Retained Files

- `legacy/CONTENT_BUILD_PIPELINE_FUTURE.md` - future compiled-content-pack design. Current runtime still loads JSON directly.
- `legacy/CHATGPT_PROCESS_CHAIN_OTHER_DRAFT.md` - older process-chain draft with notes not identical to the retained industry source.
- `content/MATERIALS_DATA_CONTRACT_ARCHIVED.md` - older material/geology legality contract. Current boundary summary is `../content/TILES_MATERIALS_ARCHITECTURE.md`.
- `plans/ARCHITECTURE_ISSUE_CHATGPT_SOURCE.md` - historical source review merged into `../planning/ARCHITECTURE_REFACTOR_MASTER_PLAN.md`.
- `plans/ARCHITECTURE_ISSUE_CLAUDE_SOURCE.txt` - historical source review merged into `../planning/ARCHITECTURE_REFACTOR_MASTER_PLAN.md`.
- `plans/HUMANFORTRESS_MAIN_BRANCH_ARCHITECTURE_AUDIT_FOR_CODEX.md` - external architecture audit reconciled into `../planning/ARCHITECTURE_REFACTOR_MASTER_PLAN.md`.
- `plans/OPTIMIZATION_SUGGESTION_SOURCE_2025.md` - older detailed performance review merged into `../planning/OPTIMIZATION_SUGGESTION.md`.
- `plans/TODO_CONSTRUCTION_AND_HAULING_STABILITY.md` - construction/transport stability debugging notes with unique log patterns and mitigations.
- `simulation/JOBS_SPEC_LEGACY.md` - older hauling-first job model. Replaced by `../simulation/WORK_AND_JOBS_SYSTEM.md` and `../simulation/TRANSPORT_SYSTEM.md`.
- `simulation/JOB_SCHEDULER_SPEC_TARGET.md` - target chunk-parallel scheduler model. Replaced by the target section in `../simulation/WORK_AND_JOBS_SYSTEM.md`.
- `simulation/CREATURE_ITEM_MANAGER_IMPLEMENTATION_2025.md` - old implementation note for manager/debug-spawn work. Current creature/item definitions are covered by `../simulation/CREATURE_SPEC.md` and `../content/ITEMS_SPEC.md`.
- `simulation/NAVIGATION_DESIGN_ARCHIVED.md` - older navigation design narrative. Current ownership and extension notes are merged into `../simulation/NAVIGATION_SPEC.md`.
- `simulation/NAVIGATION_RAMP_ADDENDUM_ARCHIVED.md` - older ramp-only addendum. Current ramp rules are merged into `../simulation/NAVIGATION_SPEC.md`.
- `simulation/UNIFIED_WORK_SCHEDULER_DESIGN.md` - prior unified scheduler integration plan. Replaced by current orchestrator notes in `../simulation/WORK_AND_JOBS_SYSTEM.md`.
- `status/` - phase-completion snapshots, zone status summaries, tracking notes, and DOD checklists retained as historical records.
- `ui/UI_ARCHITECTURE_ANALYSIS.md` - old but still useful UI debt analysis.
- `ui/INPUT_MAPPING_DESIGN_ARCHIVED.md` - older full input-mapping rewrite design. Current facts are in `../ui/UI_SYSTEM.md`; target action/context ideas remain in `../ui/INPUT_SPEC.md`.
- `ui/UI_REFACTOR_PLAN.md` - old UI target-plan reference; current UI facts remain in `../ui/`.

## Deleted In 2026-06-12 Cleanup

The following archive documents were removed because they were one-off instructions, duplicate controls, obsolete run/build notes, or plans already merged into current docs:

- `operations/BUILD_README_LEGACY.md`
- `operations/LAUNCH_GAME_LEGACY.md`
- `patches/APPLY_INPUT_HANDLER_PATCH.md`
- `patches/MOUSE_CLICK_FIX.md`
- `patches/PR_DESCRIPTION.md`
- `plans/DOC_ORGANIZATION_PROPOSAL_SUPERSEDED.md`
- `plans/HAUL_SYSTEM_ARCHITECTURE_LEGACY.md`
- `plans/HAUL_SYSTEM_REFACTOR_PLAN.md`
- `ui/CONTROLS_LEGACY.md`
- `ui/QUICK_START_UI_REFACTOR.md`
- `ui/UI_REFACTOR_PROGRESS.md`
- `ui/UI_REFACTOR_SUMMARY_CN.md`
