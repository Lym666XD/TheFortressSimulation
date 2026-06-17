HumanFortress — Unified Rules

(Determinism · Concurrency · Update Order · Data · Rendering · Stability)

0) Scope

This file is the single source of truth for rules that govern: design, coding, data layout, update order, rendering, testing, CI, stability, and modding.

Everything marked (Normative) is a hard requirement. Items marked (Informative) are recommended practices.

1) North Star (Normative)

Simulation first — CPU budget prioritizes simulation over rendering.

Deterministic & replayable — same seed + same inputs ⇒ same outputs (bitwise where feasible).

Low coupling, high cohesion — clear module boundaries and contracts.

Safe concurrency — parallelize reads and isolated work; enforce a single, deterministic write point.

1.1) Active Development Constraints - 2026-06-17 (Normative)

These constraints reflect the current refactor state and override older target-only wording when there is a conflict.

Current source ownership

- `HumanFortress.Contracts` owns cross-module DTOs/interfaces, including profession contracts and content contract types.
- `HumanFortress.Content` owns content path resolution, item/creature/core-data loaders, the structured runtime registry implementation, runtime content snapshot capture, strict content loading, and profession registry JSON loading.
- `HumanFortress.Core` owns foundation primitives only; do not add content registry implementation, JSON content loaders, or App/runtime composition back into Core.
- `HumanFortress.Simulation` owns authoritative world/chunk/tile/item/creature/order/stockpile state and diff applicators.
- `HumanFortress.Navigation` owns navigation algorithms and cache structures; it must remain decoupled from Simulation through adapter interfaces.
- `HumanFortress.Jobs` owns job executor cores, job helpers, job diff emitters, callback loggers, scheduler/workshop tuning types, worker selection, profession assignment state, and unified job orchestration.
- `HumanFortress.Runtime` owns the generic runtime host, tick pipeline, command stage, command target seams, Simulation-backed navigation adapter/factory, startup helpers, and tick-facing job wrappers.
- `HumanFortress.WorldGen` consumes explicit generation content; it must not read global content registries directly.
- `HumanFortress.App` owns SadConsole/MonoGame UI, UI bootstrap, concrete session composition that has not yet moved, logger callback binding, and UI/debug surfaces. Do not add new gameplay rules, content loaders, job logic, or authoritative world mutations to App.

Current compatibility debt

- Some moved types intentionally preserve old namespaces such as `HumanFortress.App.Jobs` or `HumanFortress.Core.Content.Registry` until the namespace cleanup pass.
- Transitional `InternalsVisibleTo` bridges are allowed only as migration scaffolding. Do not use them as justification for new cross-module ownership leaks.
- `HumanFortress.App/Jobs` should remain empty of active source files.

Mutation and command rules

- UI/debug actions that affect simulation state should enter through Runtime command targets or typed diff logs.
- Do not directly mutate item, creature, terrain, construction, stockpile, or profession state from App event handlers.
- Construction/craft/transport/mining item changes must go through `ItemsDiffLog` or the relevant Jobs-owned diff emitter.
- Any replacement of direct mutation with diffs needs focused regression coverage for duplicate application, rollback, and missing-resource behavior.

Content and data rules

- Runtime systems should consume explicit catalogs/tunings/snapshots, not `ContentRegistry.Instance` convenience reads.
- App may resolve active-session content only through Runtime/Content-owned facades.
- Content JSON compatibility belongs in Content loaders, not App startup code or Simulation managers.

Verification workflow

- Use .NET 8 explicitly on macOS: `/opt/homebrew/opt/dotnet@8/bin/dotnet`.
- Do not run overlapping App/test/solution builds in parallel; macOS apphost signing and shared `obj` files can race.
- Prefer sequential verification:
  - `/opt/homebrew/opt/dotnet@8/bin/dotnet build HumanFortress.sln --no-restore -m:1 -v:minimal -p:RunAnalyzers=false -p:UseAppHost=false`
  - `/opt/homebrew/opt/dotnet@8/bin/dotnet exec tests/HumanFortress.App.Tests/bin/Debug/net8.0/HumanFortress.App.Tests.dll`
  - `/opt/homebrew/opt/dotnet@8/bin/dotnet exec src/HumanFortress.App/bin/Debug/net8.0/HumanFortress.App.dll --init-only --strict-content --content-warnings-as-errors`
- For `dotnet exec`, do not insert the `dotnet run`-style `--` separator before app arguments.
- If a build/run command has no output for roughly 30 seconds, check `pgrep -fl "[d]otnet|[H]umanFortress|[M]SBuild|[V]BCSCompiler"` and report whether anything is actually still running.
- `Microsoft.CodeAnalysis.LanguageServer` / Roslyn alone is normal editor background activity, not a stuck game/build.

Documentation workflow

- Update `docs/planning/REFACTOR_BATCH_PROGRESS.md` after each meaningful architecture batch.
- Update `docs/planning/REFACTOR_PITFALLS_AND_LESSONS.md` whenever a repeated build, runtime, ownership, or verification trap is discovered.
- Keep `docs/architecture/GAME_ARCHITECTURE.md` and `docs/planning/ARCHITECTURE_REFACTOR_MASTER_PLAN.md` aligned with actual source ownership, not aspirational ownership.
- Archived documents are historical context only unless a current document explicitly points to them.

2) Architectural Boundaries & Dependencies (Normative)

Core: tick, time, RNG, events, serialization. (No dependencies.)

ECS: entities/components/systems. (Depends on Core.)

World/Fortress: chunks/tiles/layers/regions/connectivity. (Depends on Core + ECS.)

Subsystems: nav/AI/economy/fluids/fields/vegetation/items/units. (Depend on World + ECS + Core.)

Save/Replay/Modding: read-only contracts into all above; never mutate sim state.

App/Rendering: consumes immutable snapshots only; never touches live world.

3) Determinism & Concurrency (Normative)

Fixed-step tick (e.g., 20 ms). Never couple sim to wall-clock.

RNG streams per stage/system/chunk. Seed = WorldSeed ^ Hash(StageId,SystemId,ChunkId). Do not call RNG inside an iteration whose order can vary.

Read/Write guards — read-phase receives IWorldReader; only the Commit phase can obtain IWorldWriter.

Stable orders everywhere — where order affects results, sort by a deterministic key (e.g., ChunkId → TileIndex → Priority → SystemId).

No nondeterminism — no system time, GUIDs, non-deterministic FP on sim paths; isolate platform variance.

Performance caps — hard iteration/time budgets for pathfinding, LOS, fluids, and fields that remain replay-verifiable.

4) Update Order (Per-Tick Pipeline) (Normative)

Stages may parallelize across chunks, but write sets must not overlap. A job system enforces read/write masks and alias checks.

Stages

ApplyCommands — consume orders; writes L0/L2/L7; mark tile + neighbors dirty.

RebuildDerived (local) — recompute Nav/Opac/Support for dirty sets; bump ConnectivityVersion.

Support & Collapse — resolve structural support; may affect L0/L2/L5.

FluidsStep (budgeted) — process ≤ F active cells; writes L3; may enqueue steam to L4.

FieldsStep (budgeted) — process ≤ G entries; writes L4 and events.

Vegetation & Surface — writes L1 (soil/plant growth/erosion).

Items — writes L5 (stacks/ownership/moves).

EmitEvents — publish compact event stream for UI/logic.

BuildRenderSnapshot — build immutable snapshot; no more world writes after this point.

5) Data Layout Rules (Normative)

Chunked SoA — world partitioned into fixed-size chunks (e.g., 16×16×Zc). Hot fields use Structure-of-Arrays; overlays are sparse structures keyed by tile index.

Hot tile base (target ≤ 12 bytes)

GeoMatId:uint16, TerrainBits:uint16, SurfaceBits:byte, FluidKind:byte, FluidDepth:byte, MetaBits:byte, TrafficCost:uint16.

Base is immutable within a tick; mutations occur only in Commit.

Sparse overlays

L2 constructions/furniture, L4 fields (inline small, pooled overflow), L5 item stacks (pooled handles).

Derived caches (NavMask/Cost, OpacMask, SupportMask) rebuild only for dirty tiles and targeted neighbors.

Dirty propagation

Topology edits (L0/L2) ⇒ tile + 6 neighbors dirty.

L3 (fluids) ⇒ tile (optionally neighbors for steep gradients).

L4 (fields) ⇒ local LOS/nebulous where applicable.

Save/Load — serialize authoritative data only (bases, overlays, stacks, fluids, fields, meta). Never serialize derived caches; rebuild on load. Include schemaVersion.

6) Tile Layers — Responsibilities & Write Windows (Normative)

L0 Terrain (topology) — floor/wall/edge; dig/channel/build; provides support & standability.

L1 Surface — soil/mud/vegetation skin; visual/light modifiers; does not block movement.

L2 Constructions & Furniture — hard/soft blockers; passables[]; support; opacity; autotile connect groups.

L3 Fluids — kind + depth (0..7); solver is budgeted per tick; interacts with L4 (steam/gas).

L4 Fields — multiple per tile (id, intensity, age); soft opacity/decals/gases/fire.

L5 Items — pooled stacks; blocking per prototype; single owning stockpile at a time.

L6 Units/Vehicles — transient occupancy; species-specific block rules.

L7 Meta/Markers — designations, rooms, traffic, visibility, biome, connectivity version.

7) Rendering Rules (Normative)

Renderer isolation — renders from the immutable snapshot only; never reads live world.

Snapshot contents per Z — TilePaletteIndex (autotile/rotation already resolved), FluidDepth, FieldGlyphs, Designations, Billboards (items/units).

Autotiling/Rotation — computed during snapshot build from connect_groups/connects_to and rotates_to.

Draw order — Floors → Surface → Fluids → Furniture → Items → Fields → Units → UI overlays.

Work only on dirty chunks; always apply view frustum/viewport culling.

8) Runtime Stability & Exception Handling (Normative)

Crash-proof orchestration

Wrap each stage in a top-level try-catch.

On exception: quarantine the offending chunk/system for this tick, drop invalid diffs/messages, degrade to serial on next tick for that chunk/system. Never tear down the main loop.

Catch at boundaries, not inner loops

Boundaries: stage orchestrator, chunk-actor Apply, mod sandbox, I/O. Inner loops: prefer result codes; avoid exceptions for control flow.

Classification & policy

Expected/recoverable: content validation failures, over-capacity, missing assets → clip/drop & audit.

Unexpected/bug: null refs, OOB, race assertions → quarantine + error event + metrics spike; optionally enable determinism replay for next N ticks.

Fail-safe sequence

Drop invalid op → Skip system on that chunk → Degrade that chunk to serial → Freeze that chunk for 1 tick (read-only) and schedule rebuild.

Logging/telemetry

Rate-limit identical errors (1 per root cause per second).

Structured log fields: {seed, tick, stage, systemId, chunkId, tileIndex, rngPositions, last32Ops}.

catch (Exception ex) must either rethrow wrapped outside hot path or emit a structured error event visible to UI/debug overlay.

External calls

File/network/OS: timeout + bounded retries + circuit breaker; convert to domain errors.

9) Data-Driven & Anti-Hardcoding (Normative)

No magic numbers in simulation. Tunables live in versioned JSON/TOML under /content, validated at load.

Enum policy

Keep only intrinsic engine enums (e.g., Direction, Axis, LayerId).

Domain sets that may expand (materials, fluids, fields, jobs, biomes, furniture…) use a Registry with string IDs (e.g., "fluid.water", "mat.granite"), loaded from JSON.

Saves persist string IDs, never raw numeric enums.

Registries

Each has a schema, loader, validator, conflict resolver (base → DLC → mods), and a dense integer index for runtime.

Deterministic override order; conflicts are reported with exact source locations.

Provide defaults and feature flags to keep old saves playable as data grows.

JSON quality gates

Strict schema validation at boot (fail to main menu, not crash the process).

Unknown fields produce warnings; missing required fields reject the entry with clear diagnostics.

Use source-generated serializers for speed and safety.

10) Coding Rules (Normative, Mature)

Determinism & Time

Fixed-step loop only; no wall-clock in sim; profiling outside sim path only.

Separate RNG streams per stage/system/chunk; never RNG inside order-dependent loops.

Mutability & Access

Sim code receives IWorldReader; only Commit may obtain IWorldWriter.

No global mutable singletons; use DI/constructor injection for systems.

Exceptions

Do not use exceptions for control flow in hot paths.

Boundary try-catch logs include {seed, tick, stage, systemId, chunkId} and a compact diff/message tail.

Nullability & Contracts

C# nullable enabled; WarningsAsErrors on.

Public APIs fully documented; validate inputs; Debug.Assert for impossible states; throw domain exceptions only at boundaries.

Memory & Allocation

Zero avoidable allocations in per-tile loops; prefer Span<T>, pooling, stackalloc, value types without boxing.

No LINQ/regex/reflection on hot paths; avoid closure captures in loops.

Pooled containers must document ownership and lifetime; return to pool in finally.

Concurrency

No locks on the main sim path. Use diff-logs (intra-chunk) and messages (inter-chunk).

Every job declares read/write masks; scheduler rejects overlapping write sets.

No ad-hoc Task.Run inside stage code; the orchestrator owns scheduling.

Collections & Ordering

Any outcome-relevant collection must be explicitly sorted with deterministic keys.

Implement IEquatable<T> and stable GetHashCode() for value-like structs.

Numerics & Culture

Use integer/fixed-point for gameplay invariants; floats for visuals/continuous decays only.

Always use CultureInfo.InvariantCulture for parsing/formatting in logs/saves.

Error Codes & Events

Centralize domain error codes (ERR_*). Emit typed events for UI/telemetry on failures instead of silent fallthrough.

Configuration

All user-tunable options (tick rate, budgets, debug overlays) are config-driven and hot-reload safe.

Experimental systems are gated by feature flags; flags serialize into saves for replayability.

Logging & Metrics

Structured logs (JSON) with seed/tick first. Metrics: per-stage time, conflict rate, back-pressure, message throughput, degradation counts.

Rate-limit duplicates; collapse traces in release; keep full traces in dev builds.

Serialization

Saves include schemaVersion, build hash, content hashes, registry manifests.

Persist only authoritative data; rebuild all derived caches on load.

Style & Hygiene

Enforce .editorconfig (naming, spacing, braces, file-scoped namespaces).

Roslyn analyzers + StyleCop/FxCop; no warnings permitted in CI.

Unit tests: serialization round-trip; determinism (golden seeds); merge strategies; registry load/validation.

APIs & Modding

Public APIs minimal and stable; breaking changes require an ADR and version bump.

Sandbox scripting has no file/network access unless whitelisted and version-gated.

11) Persistence & Migrations (Normative)

Chunked binary saves with compression; versioned schema and migrations.

Maintain golden saves for forward/backward compatibility in CI.

Never persist derived caches; rebuild deterministically after load.

12) Testing & CI Gates (Normative)

Determinism harness — replays must produce the same world/snapshot hashes across OS/architectures.

Scheduler jitter fuzz — randomize job order/affinity; results must remain identical.

Budgets — assert per-stage time/iteration budgets; CI fails on regressions.

Content schema — validate all registries and report conflicts deterministically.

Golden seeds & golden saves — shipped with the repo; CI fails if they drift.

13) Modding & Sandbox (Normative)

Data-driven raws for entities/recipes/biomes/worldgen with validation & conflict reports.

Script sandbox (Lua/Roslyn) runs with tight permissions; no I/O unless explicitly granted.

Mod load order: base → DLC → mods; deterministic overrides with diagnostics.

14) PR Review Checklist (Normative)

Determinism preserved? Where do writes commit?

Stable iteration/sort keys in all outcome-dependent loops?

RNG streams separated and seeded?

No allocations/locks/LINQ on hot paths?

Path/LOS/fluids/fields within budgets?

Error handling converts to domain events? Logs include seed/tick?

APIs minimal? ADR updated? Docs & schema updated? Tests added/updated?

15) Practical Checklists (Drop-in)
15.1 Exception Handling

 Stage orchestrator/actor Apply/mod sandbox wrapped in try-catch.

 On exception: quarantine chunk/system → drop invalid diffs → degrade to serial → emit UI event.

 Log fields: {seed, tick, stage, systemId, chunkId, tileIndex, rngPositions, last32Ops}.

 Rate-limit duplicates; show one concise on-screen message.

15.2 Data-Driven

 No magic numbers in sim; values in /content/*.json|toml.

 Extensible sets use Registry with string IDs; numeric handles are runtime-only.

 Schema versioned; loader validates and reports conflicts deterministically.

 Old saves load with defaults/feature flags; deprecations logged once.

15.3 Coding Hygiene

 Nullable enabled; warnings as errors; analyzers green.

 No locks/LINQ/reflection/allocations in hot loops.

 Deterministic sorts wherever order matters.

 Public inputs validated; authoritative state mutated only in Commit.

16) Glossary (Informative)

Authoritative data: minimal state required to reconstruct the world.

Derived caches: acceleration structures recomputed from authoritative data.

Dirty set: tiles/chunks scheduled for derived cache rebuild.

Back-pressure: deferred work (e.g., excess fluids) carried to future ticks.

Quarantine: isolate a faulty chunk/system without crashing the game.

ChangeLog

v2: Consolidated baseline rules (determinism, update order, data layout, layers, rendering) and added Runtime Stability & Exception Handling, Data-Driven & Anti-Hardcoding, and an expanded Coding Rules (Mature) set with practical checklists.
