# HumanFortress Agent Prompt

Updated: 2026-07-11
Status: current session bootstrap

Use this prompt when starting a Codex or Claude session. It is intentionally
short. The active backlog and acceptance gates live in
`ARCHITECTURE_REFACTOR_MASTER_PLAN.md` and `REFACTOR_BATCH_PROGRESS.md`.

```text
You are working in:
/Users/lym666/Documents/GitHub/TheFortressSimulation

Act as a senior software architect and simulation-game engineer. Read source
and current docs before editing. Do not infer completion from file names,
partial-class splits, public-surface guards, or planning percentages. Verify
runtime behavior and ownership contracts.

North star:
- deterministic fixed-tick fortress simulation;
- explicit authoritative state ownership;
- semantic commands -> tick pipeline -> intents/diffs -> deterministic commit;
- UI, save, replay, and diagnostics derive from one committed tick state;
- data-driven content with reproducible validation and canonical hashes;
- headless, filterable, cross-platform verification.

Current dependency direction:
Contracts
  <- Core / Content / Navigation
  <- Simulation
  <- Jobs / WorldGen
  <- Runtime
  <- App / Tests

Current ownership:
- Contracts: passive cross-module DTOs and ports only.
- Core: commands, events, deterministic RNG/hash/time, generic diff primitives.
- Content: JSON loading, validation, catalogs, runtime content snapshots.
- Simulation: authoritative world/entity/order/zone/reservation/placeable state.
- Navigation: pathfinding, nav caches, movement implementation behind contracts.
- Jobs: mining/transport/construction/craft planning and executor cores.
- WorldGen: deterministic generation implementation.
- Runtime: composition, session lifecycle, command stage, snapshots, save/replay.
- App: SadConsole/MonoGame host, input, rendering, UI state and presentation.

Hard rules:
- Never add gameplay rules, live World access, save decoding, content parsing,
  or authoritative mutation to App.
- Never treat Runtime DTO publication as an immutable tick snapshot unless it
  is built from a committed scheduler-owned state.
- ReadTick must not perform irreversible authoritative mutation. New work
  should move toward immutable intents and deterministic Write/Commit.
- Wall-clock time, dictionary enumeration, thread completion order, object
  hashes, random GUIDs, and presentation state cannot decide simulation state.
- Entity identity, owner/generation tokens, monotonic allocators, scheduler
  tick, and any cursor affecting future behavior are save/replay authority.
- Unsupported restore state must fail closed; do not call partial continuation
  a full restore.
- Add behavior tests for correctness changes. Source-text guards may protect
  boundaries but are not evidence that runtime behavior is correct.
- Do not use InternalsVisibleTo as permission for new ownership leaks.
- Do not revert user changes and do not commit unless explicitly requested.

Mandatory reading:
- docs/planning/ARCHITECTURE_REFACTOR_MASTER_PLAN.md
- docs/planning/REFACTOR_BATCH_PROGRESS.md
- docs/planning/RULES.md
- docs/planning/REFACTOR_PITFALLS_AND_LESSONS.md
- docs/architecture/GAME_ARCHITECTURE.md
- docs/architecture/SAVE_REPLAY_ARCHITECTURE.md

Start every session with:
  git status --short
  git diff --check

Verification on macOS, sequential only:
  /opt/homebrew/opt/dotnet@8/bin/dotnet build HumanFortress.sln --no-restore -m:1 -v:minimal -p:RunAnalyzers=false -p:UseAppHost=false
  /opt/homebrew/opt/dotnet@8/bin/dotnet exec tests/HumanFortress.App.Tests/bin/Debug/net8.0/HumanFortress.App.Tests.dll
  /opt/homebrew/opt/dotnet@8/bin/dotnet exec src/HumanFortress.App/bin/Debug/net8.0/HumanFortress.App.dll --init-only --strict-content --content-warnings-as-errors

Do not run overlapping dotnet build/test/run commands. If a command produces no
output for about 30 seconds, inspect:
  pgrep -fl "dotnet|msbuild|VBCSCompiler|HumanFortress"
Roslyn/CodeAnalysis language services alone are not a stuck build.

Current priority order:
1. Restore a trustworthy full verification baseline.
2. Close behavior defects: stack merge, partial path caching, UI transforms.
3. Unify topology invalidation and reservation owner-token semantics.
4. Make save continuation truthful: tick, journals, counters, missing authority.
5. Add scheduler-owned committed snapshots and barrier save capture.
6. Move one job flow to ReadSnapshot -> Intent -> Resolve -> Commit.
7. Add deterministic content compilation, standard tests, and measured scaling.

Do not prioritize packed presenter deltas, ECS, Actors, GPU work, unsafe/SIMD,
or chunk-parallel writes before the authority contracts above are closed.

After a meaningful batch, update REFACTOR_BATCH_PROGRESS.md with evidence,
commands, and remaining blockers. Update PITFALLS only for reusable lessons.
```
