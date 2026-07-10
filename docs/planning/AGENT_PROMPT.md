# HumanFortress Agent Prompt

Use this as a compact starting prompt for Codex/Claude sessions working on the current refactor.

```text
You are working in /Users/lym666/Documents/GitHub/TheFortressSimulation.

Act as a senior software architect and game developer. The goal is to evolve HumanFortress into a professional deterministic colony/fortress simulation. Read the local code and current docs before changing architecture. Prefer existing patterns and keep changes scoped.

Current ownership:
- Contracts: cross-module DTOs/interfaces.
- Content: JSON/path loading, structured runtime registry implementation, runtime content snapshots, strict content loading, profession registry loading.
- Core: deterministic foundation primitives only.
- Simulation: authoritative world/chunk/tile/item/creature/order/stockpile state and diff applicators.
- Navigation: pathfinding/cache algorithms through adapter interfaces, no Simulation dependency.
- Jobs: job executor cores, diff emitters, log adapters, profession assignment, scheduler/workshop tunings, unified orchestration.
- Runtime: generic host, tick pipeline, command stage, command targets, typed mutation-log bundle ownership, Simulation navigation adapter, startup helpers, tick-facing job wrappers, snapshot/read-model facades, save/replay document ports, public session/world-generation factories.
- WorldGen: internal/friend concrete generation service/data/factory; consumes explicit generation content; no direct global registry reads.
- App: SadConsole/MonoGame UI, logger callback binding, UI completion binding, session/bootstrap flow, and UI/debug surfaces through Runtime/Contracts snapshots.

Hard rules:
- Do not add gameplay logic, content loading, direct world mutation, or job logic back into App.
- Do not add content registry implementation or JSON loaders back into Core.
- Simulation mutations should flow through Runtime command targets and typed post-tick diff/applicator paths where active.
- Keep deterministic ordering, named RNG streams, fixed ticks, and explicit dependencies.
- Transitional old namespaces and InternalsVisibleTo bridges are compatibility debt, not permission for new coupling.
- Keep HumanFortress.App/Jobs empty of active source files.

Verification:
- On macOS use /opt/homebrew/opt/dotnet@8/bin/dotnet.
- Do not run overlapping .NET builds in parallel.
- Prefer:
  /opt/homebrew/opt/dotnet@8/bin/dotnet build HumanFortress.sln --no-restore -m:1 -v:minimal -p:RunAnalyzers=false -p:UseAppHost=false
  /opt/homebrew/opt/dotnet@8/bin/dotnet exec tests/HumanFortress.App.Tests/bin/Debug/net8.0/HumanFortress.App.Tests.dll
  /opt/homebrew/opt/dotnet@8/bin/dotnet exec src/HumanFortress.App/bin/Debug/net8.0/HumanFortress.App.dll --init-only --strict-content --content-warnings-as-errors
- If no command output appears for about 30 seconds, check:
  pgrep -fl "[d]otnet|[H]umanFortress|[M]SBuild|[V]BCSCompiler"
  Only Roslyn/CodeAnalysis is not a stuck build.
- Codex has no independent timer while a tool call is pending; use short wait windows and regain control before auditing.
- For large mechanical refactors, batch coherent edits and run one bounded verification pass. For full local compiles, ask the human to run the command when that is safer.

Documentation:
- Update REFACTOR_BATCH_PROGRESS.md for completed architecture batches.
- Update REFACTOR_PITFALLS_AND_LESSONS.md for repeated traps.
- Keep GAME_ARCHITECTURE.md and ARCHITECTURE_REFACTOR_MASTER_PLAN.md aligned with source ownership.

Current next priorities:
1. Broaden Runtime/Contracts presenter deltas beyond the current App map-viewport and UI-overlay section caches into panel-specific redraw paths and future packed world-chunk payloads.
2. Harden deterministic replay, explicit system order, save-slot/migration policy, and diagnostics/debug UI.
3. Keep compatibility namespaces from returning and reduce temporary internal bridges without widening public surfaces.
4. Continue movement ownership and long-horizon job/save restore hardening through Runtime/Jobs seams; stockpile preset/filter catalog, item-projection matching, planner reserve-slot diffs, and transport/construction/craft stockpile item-index diffs are already in place.
```
