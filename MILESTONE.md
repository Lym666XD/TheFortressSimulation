1) System-Independence Principles (Hard Rules)

Dependency Direction: App(State/Loop) → Sim(Core/Systems) → Data(Registries/Save); no reverse imports. UI never touches authoritative state; only reads RenderSnapshot and emits ICommand.

Single Write Point: Systems may propose in Read, but commit only via:

Diff-Log {op, target(tile/thing), args, systemId, priority} with stable merge order, or

Chunk-Actor mailbox (each chunk is sole writer to itself; messages ordered).

No Hidden Coupling: Cross-system effects happen only through events or jobs; never by reaching into another system’s storage.

Data-Driven Everywhere: All enums → registries (materials/items/fluids/buildables/recipes…). Loader + schema validate; engine uses IdMaps/TagIndex/LUTs.

Determinism: fixed 50 TPS; named RNG streams; stable sorts; no wall clock. Replay of commands must reproduce hashes.

Error Containment: try/catch boundaries per system/chunk/actor/tick; quarantine offender, continue the run; never crash gameplay.

Feature Flags: Any not-yet-stable subsystem guarded by tunable flags and LOD decimation.

2) Allowed Imports (Ownership & Interfaces)

App (GameStateManager, TickScheduler, CommandQueue, SaveManager, RenderManager): may depend on Data (registries loaders), never on system internals.

Sim.Core (World/Chunks/LOD, UpdateOrder): owns grid, chunk actors, time, and the write barrier.

Systems (AI/Jobs/Nav/Economy/Combat/Fields/Storyteller): read via read-only views; write only through Diff-Log / mailbox.

Data (Registries, Serializer): no dependency on Sim; pure I/O + compiled packs.

3) Contract-First Workflow (every feature)

Define Interfaces: DTOs, public queries, error codes, RNG stream names, LOD budgets.

Add Schemas/Registries (if content-driven): JSON schema + sample content.

Write Fakes: in-memory adapters for tests & headless demos.

Golden Tests: determinism replay + performance micro-benchmarks.

Integrate at Barrier: hook into UPDATE_ORDER with explicit read/write loci.

4) Phased Plan (Critical Path + Parallel Streams)
Phase A — Platform & CI Foundations (Weeks 1-2)

Goals: compile-clean solution, fixed-tick loop, determinism harness, content pipeline.

Bootstrap: solution layout, analyzers, warnings-as-errors, .NET 8, EditorConfig.

Fixed tick (50 TPS) & barriered UPDATE_ORDER skeleton; try/catch guards.

CommandQueue (tick-tagged, serializable) + replay harness.

Content Build Pipeline → .cpack (IdMaps/TagIndex/LUTs); hot-reload seam.

Save skeleton: atomic manifest, chunked .mpkz, version stamp.

CI jobs: unit, perf smoke, determinism gates (golden seeds & replays).

DoD: same seed+inputs ⇒ same snapshot hash across OS/thread counts; content loads; save/load round-trips.

Mirrors & condenses prior Sprint 0 infra but aligns to current architecture and SadConsole-primary rendering. 

milestones

Phase B — WorldGen & WorldMap (Weeks 3-4, mostly isolated)

World params + seed; region noise; biomes/resources; world.meta.mpkz.

WorldMap UI (SadConsole): navigate, inspect tile, EmbarkPrep entry.

DoD: deterministic world hashes; 256×256 map ≤ budget; embark opens setup.

Phase C — Embark & Fortress Bootstrap (Weeks 5-6, critical)

EmbarkPrep: choose N×N chunks, N∈[2..8]; starting loadout.

Fortress generator: surface + single cavern system; strata/veins; playable checks.

Chunk lifecycle (load/unload), LOD framework (L0/L1/L2/L3/L4) with budgets.

RenderSnapshot builder; SadConsole layers (map/items/entities/ui).

DoD: Enter fortress → idle sim loop (no jobs yet); 60 FPS view; stable hashes.

Phase D — Navigation & Connectivity (Weeks 7-8, parallelizable)

Walkability/opacity/support masks + ConnectivityVersion invalidation.

Deterministic A* (stable tiebreakers); caps & diagnostics; stuck detection.

Path cache & traffic costs (v1: simple).

DoD: 10 concurrent pathers; ≤10% median frame time; no infinite loops.

Brings over “pathfinding hard limits & diagnostics” from prior plan, adapted to chunk/LOD ownership. 

milestones

Phase E — Creatures, Items, Stockpiles, Zones (Weeks 9-10)

Items registry + schema (done) → runtime pools; stacking; ownership refs.

Stockpile Zones (area, filters via tags/ids); not entities; UI config.

Query indices for UI (grouped inventory virtualized).

DoD: create stockpile; items appear; filters work; snapshot shows piles.

Phase F — Job Scheduler & Hauling Loop (Weeks 11-12, core vertical slice)

Task board: produce/consume jobs; priorities; reservations (TTL).

Hauling: pull → carry → drop; reachability checks; retry logic.

AI Read-phase propose, Write-phase commit via Diff-Log.

DoD: place stockpile → dwarves haul nearby items deterministically.

Adopts the “task board, fairness, starvation avoidance” goals from prior Sprint 2.1 but routes all writes through our barrier model. 

milestones

Phase G — Buildables & Construction (Weeks 13-14)

Buildables registry (workshops/doors/walls); placement/rotation; costs.

Construction designation → material reservation → build job → placement.

Tileset/autotiling hookup (NESW masks; rotation LUTs).

DoD: player designates a wall/workshop; dwarves gather & build deterministically.

Phase H — Storyteller (Fortress-Only) (Weeks 15-16)

Pacing model (threat, wealth, time); candidate selection; edge-band spawners.

Incidents: visitors/caravans/raid (basic); weather modifier. No ambush.

LOD pin/TTL for targeted chunks.

DoD: scripted raid arrives from border; executes goals without desync.

Phase I — Combat MVP (Weeks 17-18, contained)

Damage model (blunt/edge/pierce), armor coverage & encumbrance LUTs.

Simple AI behaviors; event log; medical stubs.

DoD: small skirmish completes deterministically; saves reload into same outcome.

Phase J — Persistence & Replay Hardening (Weeks 19-20)

Autosave ring; crash-safe bundles (replay + diff).

Save migrations scaffolding; content signature checks.

DoD: long replay passes on CI; migration tests pass; no save corruption.

Phase K — Perf/LOD Tuning & HUD (Weeks 21-22)

Budgets per system; counters; hot paths allocation checks.

L1 decimation; L2 background integrators (aging/rot/fields decay).

Perf HUD (AI/LOS/Path/Items/Fluids budgets).

DoD: all budgets respected on target spec; degradations visible & tunable.

Phase L — Modding & Content Polish (Weeks 23-…) (parallel)

Registries breadth (materials/items/buildables/recipes/tilesets/biomes).

Mod loading order & conflict reporting; hot-reload with rollback.

Docs & samples.

5) Parallel Workstreams (to maximize independence)

WS-Content: schemas + base content + pipeline (safe to run from Phase A onward).

WS-UI: MVU store, panels, virtualization, input bindings; independent of sim internals; bind to snapshots.

WS-Save: serializer, migrations, atomic commit; operates on DTOs, not sim stores.

WS-Perf/CI: determinism harness, replay runner, perf smoke; runs continuously.

6) Per-Module Definition of Done (Gate Checklist)

All:

API contracts + XML docs; unit tests; deterministic replay; perf microbench.

No writes outside Write-phase; all writes via Diff-Log or mailbox.

LOD behavior defined (L0/L1/L2); error boundaries present; logs categorized.

World/Chunks:

Dirty tracking; masks caches with versioning; mailbox ownership per chunk.

Nav:

Stable tiebreakers; caps; stuck backoff; metrics to HUD.

Jobs/AI:

Proposals are idempotent; reservations TTL; starvation tests.

Economy/Items:

Tag filters indexed; no O(n) scans on hot paths; grouping for UI.

Storyteller:

Named RNG streams; edge-band spawn; LOD pin; rollback on failure.

Combat:

RNG stream per chunk/encounter; coverage LUTC; bounded iterations.

Save/Load:

Versioned; migration tests; atomic manifest; corruption fallback.

7) Risks & Mitigations

Cross-system leakage → Strict package boundaries, code owners, import lints.

Non-determinism → Replay CI gate; stable sorts; banned wall clock/parallel writes.

Performance debt → Budgets + HUD + time-slicing; L1/L2 decimation early.

Content drift → Schemas; content build determinism; compiled snapshots; signatures in save.

8) Milestone Demos (Vertical Slices)

VS-1 (Phase C+D+E+F): Embark → create stockpile → dwarves haul → save/reload replay.

VS-2 (Phase G): Build a workshop → craft a simple item via recipe.

VS-3 (Phase H+I): Trigger a raid from edge band → small combat → save/reload → same outcome.

VS-4 (Phase K): Perf HUD shows budgets; LOD throttle under load.

9) Traceability to Previous Plan

We kept the strong CI/determinism, path caps, actor/hauling loop, SadConsole foundation, and staged rendering/input abstractions, but constrained scope to one fortress map with edge-band incidents, and routed all writes through the barrier patterns required by our architecture. 

milestones