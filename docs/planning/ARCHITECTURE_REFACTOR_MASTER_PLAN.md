# HumanFortress Architecture Refactor Master Plan

Date: 2026-07-11
Status: active execution plan
Scope: architecture, authoritative simulation contracts, determinism, persistence, and testability

This document is the controlling plan for the remaining architecture refactor. It replaces percentage-based progress claims and historical migration narratives. Completed work is recorded only when an executable boundary or behavior proves it.

Related documents:

- `docs/planning/REFACTOR_BATCH_PROGRESS.md` records completed batches and evidence.
- `docs/planning/RULES.md` contains non-negotiable implementation rules.
- `docs/planning/REFACTOR_PITFALLS_AND_LESSONS.md` records recurring failure modes.
- `docs/architecture/GAME_ARCHITECTURE.md` describes the current module model.
- `docs/architecture/SAVE_REPLAY_ARCHITECTURE.md` describes persistence policy.

## 1. Executive Decision
HumanFortress should continue from the current architecture. A rewrite is not justified. The solution now has useful project boundaries, Runtime-owned composition, deterministic primitives, diff-based mutation paths, and an initial save/replay vertical slice.

The refactor is not close to complete in the sense that matters for a long-lived simulation game. Static dependency cleanup is substantially established, but several authoritative contracts remain open:

- UI snapshots and saves do not yet consume one atomic committed tick;
- partial navigation results can still be treated as complete paths;
- topology mutation does not yet update every collision and navigation view;
- item stack merging can corrupt indexes and merge incompatible state;
- entity identity is wider but still a collision-prone GUID projection;
- restore does not yet prove deterministic continuation for all state;
- content compatibility does not hash every mechanical input;
- reservation release is not ownership-safe;
- map input and rendering do not share one canonical transform.

Decision:

- **Go** for focused architecture hardening and correctness work.
- **No-Go** for claiming full deterministic replay or production-ready saves.
- **No-Go** for broad new simulation systems until the B0 ledger is closed or explicitly classified as unsupported.

No global completion percentage will be maintained. Progress is measured by closed contracts, behavior tests, deterministic evidence, and removal of unsupported claims.

## 2. Verification Baseline
Baseline as of 2026-07-11:

- solution build passed with `0 warnings / 0 errors`;
- strict headless content initialization passed with exit code `0`;
- the full smoke harness is **not green**;
- the latest harness run stopped with exit code `134` on a source-text guard that drifted from the implementation in `RuntimeSaveContentCatalogSummaryFactory`;
- the observed guard failure appears to be stale static matching rather than a demonstrated runtime behavior regression, but later tests did not run;
- therefore no document or change may state that full tests pass until the complete harness exits successfully.

The first active batch is to restore a trustworthy green baseline without loosening behavioral requirements.

## 3. Architecture Principles
The refactor must preserve these principles:

1. Simulation state has one authoritative owner.
2. A committed tick is the only publishable state boundary.
3. UI emits intents or commands; it does not mutate gameplay state.
4. Save, replay hash, UI, and diagnostics consume coherent committed data.
5. Determinism is a behavior with tests, not a sorting convention alone.
6. Ownership-sensitive operations require verifiable ownership tokens.
7. Unsupported persistence is rejected before mutation begins.
8. Content compatibility is based on canonical mechanical meaning.
9. Cross-project APIs use Contracts types, not implementation objects.
10. Small focused batches are preferred over broad mechanical reorganizations.

## 4. Current Dependency Graph
In this diagram, `A -> B` means project A references project B.

```text
HumanFortress.Contracts  -> (no production project references)

HumanFortress.Core       -> Contracts
HumanFortress.Content    -> Contracts
HumanFortress.Navigation -> Contracts

HumanFortress.Simulation -> Contracts, Core

HumanFortress.Jobs       -> Contracts, Core, Simulation
HumanFortress.WorldGen   -> Contracts, Core, Simulation

HumanFortress.Runtime    -> Contracts, Content, Core, Jobs,
                            Navigation, Simulation, WorldGen

HumanFortress.App        -> Contracts, Runtime

HumanFortress.App.Tests  -> App, Contracts, Content, Jobs, Runtime
```

This is the current real graph, not a proposed set of future assemblies. It is acceptable for the current refactor. New projects require a concrete ownership problem that cannot be solved within these boundaries.

## 5. Current Module Responsibilities
### Contracts
- Own stable cross-project DTOs, ports, result types, and schema versions.
- Remain independent of implementation projects and presentation frameworks.
- Avoid behavior-heavy service implementations and mutable domain ownership.

### Core
- Own generic scheduling, command, event, diff-ordering, deterministic RNG, replay hash, time, and low-level world primitives.
- Remain unaware of concrete jobs, UI, save slots, content definitions, and Runtime composition.

### Content
- Own JSON loading, validation, normalization, catalog construction, and content diagnostics.
- Produce immutable or read-only catalog snapshots through Contracts seams.
- Never own live session state.

### Navigation
- Own path search, path cache, navigation data, and movement execution helpers.
- Consume navigation views and deterministic budgets.
- Never become an alternate owner of world topology.

### Simulation
- Own authoritative world state: terrain, items, creatures, placeables, zones, stockpiles, orders, reservations, and their indexes.
- Own mutation validation, diff application, world replay hashing, and world save payload mapping/restoration.
- Keep index updates and multi-structure mutations transactional.

### Jobs
- Own mining, craft, construction, and transport planning/execution state.
- Read through explicit simulation/query roles and emit validated mutations.
- Own job snapshot, replay hash, and restore semantics for job-owned state.

### WorldGen
- Own deterministic initial-world generation and generation-stage algorithms.
- Produce generated state for Runtime to install into a new session.
- Never mutate an already-running session outside an explicit Runtime flow.

### Runtime
- Own composition, active-session lifecycle, tick pipeline, command execution, post-tick publication, save/restore coordination, and replay coordination.
- Adapt implementation modules to Contracts-facing App ports.
- Become the owner of a coherent committed checkpoint, not a permanent facade that accumulates every gameplay query and helper.

### App
- Own SadConsole hosting, input capture, rendering, UI state, presentation caches, and user-facing diagnostics.
- Consume Runtime ports and Contracts DTOs only.
- Never inspect or mutate live Simulation, Jobs, Navigation, or Content objects.

### Tests
- Enforce dependency and public-surface constraints.
- Prove behavior, deterministic equivalence, save continuity, and failure atomicity.
- Static source guards may supplement behavior tests, but may not substitute for them or depend on incidental variable names and formatting.

## 6. Established Boundaries
The following foundations are already real and should be preserved:

- App production references are limited to Contracts and Runtime.
- Content production references are limited to Contracts.
- Runtime is the production composition root for lower modules.
- Contracts do not depend on implementation projects.
- concrete implementation surfaces are predominantly internal/friend-scoped;
- architecture smoke checks constrain project references, imports, public API, friend assemblies, and CI workflow presence;
- command enqueue and tick-stage execution are separated from App input;
- typed diff logs and applicators exist for major mutation families;
- deterministic RNG, canonical hash helpers, and stable enumeration rules exist;
- scheduler system identity and failure isolation have test coverage;
- save/replay has a versioned Runtime-owned vertical slice;
- restore uses staging for supported world, job, RNG, and pending-command state;
- content signatures currently fail closed on detected incompatibility;
- Runtime publishes Contracts-owned frame and overlay DTOs;
- App has presenter caches for Runtime-authored map and overlay deltas;
- a GitHub Actions build/smoke workflow exists.

These are boundary accomplishments, not proof that the data captured across those boundaries is coherent, complete, or sufficiently scalable.

## 7. B0 Authoritative Contract Inventory
This section defines the contracts and their closure conditions. Current state
and evidence are owned by the B0 ledger in `REFACTOR_BATCH_PROGRESS.md` so
status is not duplicated across planning documents.

| ID | Contract | Required closure |
|---|---|---|
| B0-1 | Committed snapshot and save barrier | Simulation thread publishes one immutable committed checkpoint after PostTick. UI, save, replay hash, and debug readers use a coherent checkpoint identity. |
| B0-2 | Partial path semantics | Introduce explicit partial/exhausted semantics. Never cache partial results as complete. Prove long-path behavior, retry/continuation policy, and warm/cold cache equivalence. |
| B0-3 | Topology mutation | One topology mutation API updates collision/capability data, owner and secondary chunks, dirty sets, versions, navigation rebuild inputs, and every registered path cache. |
| B0-4 | Stack compatibility and indexes | Define a canonical compatibility key and capacity policy. Update only consumed IDs. Preserve unrelated stacks and indexes. Return deterministic merge results. |
| B0-5 | Entity identity | Adopt a collision-free session identity with generation/allocator state, or detect projection collisions and fail closed as an interim step. Restore allocator high-water state and prove lookup uniqueness. |
| B0-6 | Restore continuity | Either restore every authoritative section needed for deterministic continuation, or classify the scenario unsupported before commit. Compare uninterrupted and save/restore continuations. |
| B0-7 | Mechanical content binding | Canonicalize and hash all simulation-affecting content. Persist stable content IDs or explicit remap tables. Reject unknown or ambiguous bindings before restore. |
| B0-8 | Reservation ownership | Issue reservation tokens containing resource, owner, job/generation, and lifetime identity. Require matching tokens for renew/release/commit. Prove stale releases cannot cancel new owners. |
| B0-9 | UI coordinate transform | Use one tested world/screen transform for draw, hover, click, zoom, camera, and modal pass-through. Current click coordinates must be authoritative. |

## 8. Execution Roadmap
The phases are ordered by correctness dependency. A later phase may be explored but should not be merged ahead of an unmet earlier gate when it relies on that gate's contract.

### Phase 0: Restore a Trustworthy Baseline
Goal: make current evidence reliable before changing authoritative behavior.

PR-sized batches:

1. Replace the stale content-summary source-text assertion with a semantic or resilient structural assertion that verifies canonical ordering.
2. Run the entire harness and classify every subsequent failure as static guard drift or behavior regression.
3. Add a small machine-readable or table-based B0 ledger to progress reporting, using only the three allowed terminal states.
4. Remove remaining progress percentages and unsupported "full restore" or "snapshot isolation complete" claims from active planning documents.

Acceptance gate:

- solution build exits `0` with no warnings;
- strict headless content initialization exits `0`;
- full harness exits `0` from a fresh build;
- no static guard relies on local variable names, indentation, or equivalent implementation spelling;
- baseline commit contains no unrelated production refactor.

### Phase 1: Close Local Correctness Defects
Goal: fix defects with narrow ownership and high-value regression tests.

PR-sized batches:

1. Reproduce stack index loss and incompatible merge behavior with manager-level tests, then implement compatibility/capacity-aware transactional merging.
2. Reproduce budget-exhausted path caching, add explicit result semantics, and prevent incomplete paths from entering the success cache.
3. Create a canonical App viewport transform and route rendering, overlay, hover, and click conversion through it.
4. Add zoom/camera/border/modal tests that prove the clicked world cell equals the rendered cell under the pointer.
5. Replace hardcoded or XY-derived Z limits with Runtime-authored session bounds in wheel and drag-selection paths.

Acceptance gate:

- unrelated stacks remain queryable after every merge case;
- material/quality/ownership/reservation differences prevent invalid merges;
- partial paths cannot be observed or cached as completed routes;
- path results are identical across repeated runs and cache temperature;
- UI transform round trips are exact for supported zoom levels;
- App Z navigation/selection respects the active session depth rather than a hardcoded constant or XY map size;
- full harness remains green after each batch.

### Phase 2: Close Mutation Ownership
Goal: make topology, reservation, and identity updates safe across systems.

PR-sized batches:

1. Introduce a Simulation-owned topology change description and transaction.
2. Route terrain, placeable placement/removal, blocker, and door changes through the transaction, including cross-chunk owner/reference cleanup.
3. Publish committed topology changes once to navigation rebuild and Runtime's path-service registry; test intermediate-chunk invalidation.
4. Introduce owner/job/generation reservation tokens for one vertical slice, preferably transport item pickup and delivery.
5. Migrate remaining item and creature reservation call sites; reject stale renew/release attempts without mutating current reservations.
6. Add entity-key collision detection and fail-closed index insertion now; choose the long-term session ID/allocator representation with an ADR and one migrated entity family.

Acceptance gate:

- blockers and doors change pathability on the committed tick boundary;
- cross-chunk placeable create/remove leaves no stale references;
- all path services invalidate routes crossing any changed chunk;
- stale reservation tokens cannot release or renew another job's reservation;
- identity collision cannot silently overwrite an entity index;
- deterministic replay hashes remain equal for topology/reservation scenarios.

### Phase 3: Make Persistence Claims Honest
Goal: ensure restore is atomic, complete for declared scenarios, and capable of deterministic continuation.

PR-sized batches:

1. Inventory every authoritative state owner and map it to save section, checkpoint hash, schema version, restore order, and unsupported policy.
2. Persist and restore scheduler tick plus monotonic allocator/high-water state.
3. Add profession, generic zone, and exact movement/executor state, or reject saves containing those active states before staging mutation.
4. Define executed-command journal semantics: persist required history, reset it explicitly with a new replay epoch, or reject unsupported continuity.
5. Validate aggregate metadata, section counts, hashes, and cross-section references before committing the staged session.
6. Add uninterrupted-versus-restored continuation tests across idle, active mining, craft, transport, topology, and reservation scenarios.

Acceptance gate:

- every manifest section has a single owner and restore policy;
- unsupported state fails before active-session replacement;
- failed restore leaves the active session unchanged;
- restored tick and monotonic identities cannot regress or be reused;
- N ticks + save/restore + M ticks equals uninterrupted N+M ticks for all supported scenarios, including authoritative hashes and job state;
- "full restore" is used only if the declared scenario set passes this gate.

### Phase 4: Publish One Committed Tick
Goal: remove live-world reads from UI and persistence authority.

PR-sized batches:

1. Define an immutable committed checkpoint identity containing session epoch, tick, content signature, and authoritative section versions/hashes.
2. Publish it atomically from the simulation thread after all PostTick commits, navigation updates, and deterministic bookkeeping.
3. Move frame and overlay read-model production to committed state, retaining presenter-specific DTOs and caches outside the authoritative model.
4. Move save package and replay checkpoint construction to the same committed checkpoint, with explicit snapshot retention/backpressure policy.
5. Split `FortressRuntimeSessionCore` ownership into lifecycle coordination, committed-checkpoint ownership, save/restore coordination, and read-model publication. File splitting alone does not satisfy this batch.
6. Add concurrency stress tests that publish, render, hash, and save while ticks advance, asserting coherent checkpoint identities and no torn collections.
7. Replace unbounded scheduler shutdown with cancellation-aware bounded stop semantics and a structured timeout/fault result.
8. Remove process-global mutable content/log callback dependencies from normal and staged Runtime sessions, or isolate them behind an explicit compatibility owner.

Acceptance gate:

- App-facing reads never enumerate mutable Simulation or Jobs objects;
- save sections and hashes all identify the same session epoch and tick;
- no UI frame combines data from different committed ticks;
- publication is atomic and old snapshots are immutable;
- bounded retention prevents an App stall from blocking simulation forever;
- stopping or replacing a session cannot wait forever, and late work from an old session generation cannot publish into the new one;
- two composed test sessions do not overwrite each other's content or diagnostic authority;
- concurrency tests pass repeatedly without timing-based assertions.

### Phase 5: Establish Intent, Resolve, Commit
Goal: make multi-system work deterministic and transaction-oriented before parallelism or major feature growth.

PR-sized batches:

1. Define `WorldReadSnapshot -> Intent -> Resolve -> Commit` contracts for the transport/item/reservation slice.
2. Produce intents without mutating world state or global reservations.
3. Resolve conflicts with stable priorities and tie-break keys.
4. Commit accepted intents through Simulation-owned transactions/diffs and emit explicit rejection reasons for retry/backoff.
5. Extend save/replay hashing to in-flight intents only if they persist across tick boundaries; otherwise enforce tick-local lifetime.
6. Migrate one additional job family only after the first slice passes equivalence and failure-atomicity tests.

Acceptance gate:

- read/plan stages cannot mutate authoritative world state;
- equal inputs produce identical accepted/rejected intent sets;
- one failed commit cannot leave partial item/index/reservation mutation;
- transport behavior remains equivalent for supported existing scenarios;
- replay and restore tests cover conflict resolution and retry state.

### Phase 6: Content, Tests, and Performance Readiness
Goal: finish the foundation required for safe feature expansion and measured optimization.

PR-sized batches:

1. Replace structural JSON checks with the selected standards-compliant JSON Schema validation path and deterministic diagnostics.
2. Define canonical mechanical content serialization and extend the content signature to every simulation-affecting field.
3. Replace numeric cross-save material coupling with stable IDs or versioned, validated remap tables.
4. Move gameplay/content policy still authored in App, such as construction material eligibility, behind Content/Runtime read models while retaining presentation-only mappings in App.
5. Introduce conventional filterable test projects or a compatible test-host layer while retaining required architecture and long determinism scenarios.
6. Add representative benchmarks for tick time, snapshot publication, save capture, pathfinding, entity lookup, and allocation pressure.
7. Optimize only measured bottlenecks without weakening the authoritative contracts established above.

Acceptance gate:

- invalid content fails with stable file/path/schema diagnostics;
- mechanical content changes always change compatibility signatures;
- cosmetic-only changes follow an explicit compatibility policy;
- test failures propagate reliable nonzero exit codes and can be filtered;
- benchmark inputs and budgets are versioned and reproducible;
- no optimization reintroduces live reads, unstable ordering, or shared mutation during read/plan phases.

## 9. Cross-Cutting PR Rules
Every architecture PR must:

- state the invariant being changed;
- identify the authoritative owner before and after the change;
- include a reproducer for a defect or a contract test for new behavior;
- keep deterministic ordering explicit at authoritative boundaries;
- include save/replay impact when state crosses a tick boundary;
- reject invalid or unsupported input before partial mutation;
- update active docs in the same PR when a contract changes;
- avoid mixing namespace cleanup, formatting churn, and behavior changes;
- remain small enough to review and revert independently;
- record exact validation commands and results.

A new abstraction is justified only when it clarifies ownership, removes real duplication, or creates a testable contract. Moving code into another partial file does not by itself reduce a God Object.

## 10. Explicit Non-Goals
The following are not current refactor goals:

- migrating the simulation to ECS;
- introducing Actor-model ownership or message infrastructure;
- parallelizing simulation systems before intent/resolve/commit is proven;
- GPU compute for simulation, navigation, or save processing;
- packed/SoA world storage as a prerequisite for correctness;
- packed binary presenter deltas before committed snapshot authority exists;
- adding speculative AI, economy, combat, fluid, or storyteller subsystems;
- splitting every folder into a new assembly;
- replacing deterministic GUID compatibility in one unreviewable migration;
- delivering cloud saves, mod hot reload, or multiplayer networking;
- broad rendering redesign unrelated to canonical coordinate transforms;
- optimizing from intuition without a repeatable benchmark.

These may become valid later. None should delay closure of the B0 ledger.

## 11. Definition of Done
The architecture refactor foundation is complete only when all of the following are true:

1. The production dependency graph matches Section 4 and is CI-enforced.
2. App gameplay reads come exclusively from immutable committed read models.
3. UI commands cannot mutate live world state outside the tick pipeline.
4. Save and replay hash are derived from one identified committed tick.
5. Every B0 ledger entry is `Fixed + behavior-tested` or intentionally unsupported with fail-closed behavior.
6. Supported save scenarios restore atomically and continue deterministically.
7. Identity and allocator state cannot collide, regress, or silently overwrite.
8. Reservation release/renew/commit requires matching ownership identity.
9. Topology changes atomically update collision, navigation, versions, and cache invalidation.
10. Item mutations preserve all indexes and enforce stack compatibility.
11. Mechanical content signatures cover every simulation-affecting value.
12. Rendering, hover, and input use one tested coordinate transform.
13. The complete build, strict initialization, and test suite are green in CI.
14. Determinism tests compare equivalent fresh, cached, and restored sessions.
15. Runtime session responsibilities are separated by ownership, not just by partial files.
16. Representative performance benchmarks exist and show no unbounded per-frame or per-tick whole-world work on target scenarios.
17. Active architecture documents describe current behavior without completion percentages, stale migration history, or unsupported claims.

## 12. Validation Commands
Run .NET commands serially. Do not run multiple builds, tests, or App instances in parallel.

Lightweight checks:

```bash
git status --short
git diff --check
git diff --stat
```

Solution build:

```bash
/opt/homebrew/opt/dotnet@8/bin/dotnet build HumanFortress.sln \
  --no-restore -m:1 -v:minimal \
  -p:RunAnalyzers=false -p:UseAppHost=false
```

Full smoke harness:

```bash
/opt/homebrew/opt/dotnet@8/bin/dotnet exec \
  tests/HumanFortress.App.Tests/bin/Debug/net8.0/HumanFortress.App.Tests.dll
```

Strict headless content initialization:

```bash
/opt/homebrew/opt/dotnet@8/bin/dotnet exec \
  src/HumanFortress.App/bin/Debug/net8.0/HumanFortress.App.dll \
  --init-only --strict-content --content-warnings-as-errors
```

If a .NET command has no output for 30 seconds, inspect processes before starting another command:

```bash
pgrep -fl "dotnet|msbuild|VBCSCompiler|HumanFortress"
```

VS Code Roslyn language-service processes are not evidence that the test harness is still progressing.

## 13. Immediate Next Batches
Execute in this order:

1. Fix the stale deterministic content-summary guard and run the full harness.
2. Record all remaining harness failures before changing production behavior.
3. Add behavior tests for stack index preservation and path budget exhaustion.
4. Fix stack merge semantics and incomplete-path cache semantics in separate batches.
5. Unify the App viewport transform and current-click authority.
6. Begin topology transaction design only after the baseline remains green.

The next checkpoint should report contracts closed, tests added, and exact verification evidence. It should not report an estimated completion percentage.
