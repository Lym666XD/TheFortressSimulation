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
- Runtime: generic host, tick pipeline, command stage, command targets, Simulation navigation adapter, startup helpers, tick-facing job wrappers.
- WorldGen: consumes explicit generation content; no direct global registry reads.
- App: SadConsole/MonoGame UI, concrete session composition still not moved, logger callback binding, UI/debug surfaces.

Hard rules:
- Do not add gameplay logic, content loading, direct world mutation, or job logic back into App.
- Do not add content registry implementation or JSON loaders back into Core.
- Simulation mutations should flow through Runtime command targets or typed diff logs.
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

Documentation:
- Update REFACTOR_BATCH_PROGRESS.md for completed architecture batches.
- Update REFACTOR_PITFALLS_AND_LESSONS.md for repeated traps.
- Keep GAME_ARCHITECTURE.md and ARCHITECTURE_REFACTOR_MASTER_PLAN.md aligned with source ownership.

Current next priorities:
1. Move concrete session/system composition out of App into Runtime.
2. Introduce UI/debug snapshot facades so UI stops reading live World/concrete systems.
3. Clean compatibility namespaces and temporary internal bridges.
4. Harden deterministic replay, explicit system order, save/migration, and diagnostics.
```
