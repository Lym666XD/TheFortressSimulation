# Architecture Refactor Execution Ledger

Last updated: 2026-07-11

This is the active execution ledger for the HumanFortress refactor. It records verified facts, open correctness gaps, and acceptance criteria. It is not a session transcript or a list of every file moved.

The goal is explicit ownership across Content, Contracts, Core, Simulation, Jobs, Runtime, and App. File layout and dependency direction are necessary, but they do not prove authoritative state, determinism, save continuity, or test coverage.

## Status Vocabulary

Verification gates use `Pass`, `Fail`, `Blocked`, or `Not run`. `Blocked` means an earlier failure prevented later coverage; it is not a pass.

B0 ledger entries use only these states:

- `Reproduced`: the defect or contract gap is present in current source.
- `Partially fixed`: meaningful mitigation exists, but the invariant is not closed.
- `Fixed + behavior-tested`: implementation and behavior-level regression coverage exist, and the full gate reached that coverage successfully.
- `Intentionally unsupported`: the system rejects the scenario explicitly and safely instead of reporting false success.

No progress percentage is maintained. A batch is complete only when every acceptance criterion has current evidence.

## Verification Snapshot

Checkpoint date: 2026-07-11

| Gate | Evidence | Result | Interpretation |
| --- | --- | --- | --- |
| Branch | `refactor1` and `origin/refactor1` at `1526cb3` | Pass | Local and remote-tracking branch matched before this planning edit. |
| Worktree baseline | `git status --short` returned no entries | Pass | Baseline was clean before planning edits. |
| Patch hygiene | `git diff --check` | Pass | No whitespace errors at the audited checkpoint. |
| Solution build | Canonical .NET 8 solution build | Pass | `0 Warning(s), 0 Error(s)`. |
| Strict content initialization | App strict content initialization | Pass | Exit code `0`. |
| Full test harness | Direct execution of `HumanFortress.App.Tests.dll` | Fail | Exit code `134`. |
| Tests after first failure | Harness stopped at the first static guard failure | Blocked | Later tests did not execute; the suite is not green. |

The failure is in `DeterministicAuthoritySmokeTests.TestWorldSavePayloadRestoreUsesCanonicalOrdering`. The guard requires the literal `content.Materials.GetNameToIdSnapshot().Keys`; implementation now uses `.Select(static pair => pair.Key)` followed by ordinal ordering. This is likely stale source-text coverage, not a runtime regression, but that assessment does not convert the failed harness into a pass.

Do not claim full tests are green until the guard is corrected without weakening the ordering invariant and the complete harness exits normally.

Canonical commands:

```bash
git diff --check
/opt/homebrew/opt/dotnet@8/bin/dotnet build HumanFortress.sln --no-restore -m:1 -v:minimal -p:RunAnalyzers=false -p:UseAppHost=false
/opt/homebrew/opt/dotnet@8/bin/dotnet exec tests/HumanFortress.App.Tests/bin/Debug/net8.0/HumanFortress.App.Tests.dll
```

Run one build or test process at a time. If it has no output for 30 seconds, inspect active `dotnet`, `msbuild`, `VBCSCompiler`, and `HumanFortress` processes before starting another.

## Current Branch Delta From Main

Comparison captured before this planning rewrite:

- `main`: `e4bf91e`, the merge commit targeted by the external audit.
- `refactor1`: `1526cb3`.
- Merge base: `3950c37`.
- Unique commits: one on `main`, three on `refactor1`.
- Branch-side diff from the merge base: 364 files changed, 21,063 lines added, and 3,491 lines removed.

Material branch changes include:

- CI and stricter build/content gates;
- stronger App-to-Runtime and Contracts-facing boundaries;
- Runtime save documents, manifests, migration/compatibility policy, and staged restore;
- mining, craft, and transport job-state mapping and restoration;
- deterministic collection ownership, ordering, GUID encoding, command sequencing, and work budgets;
- Runtime frame publication, map/overlay deltas, and App presenter caches;
- focused Simulation and Jobs implementation splits;
- narrower public surfaces and stronger architecture guards;
- active-document cleanup with superseded material moved to `docs/archive`.

This is a broad architecture delta. Review it by invariant and vertical behavior, not file count. A class split or internal modifier is not proof that authority moved to the correct owner.

## Target Ownership Model

The intended dependency and authority model is:

```text
Content ----> Contracts <---- Core
                  ^            ^
                  |            |
Simulation ------>+            |
Jobs ------------>+------------+
Navigation ------>+
WorldGen -------->+
                  |
Runtime composes and owns the active session
                  |
App consumes Runtime ports and immutable read models
```

Required runtime flow:

```text
Input -> Intent/Command -> PreTick acceptance
      -> Read snapshot -> Plan -> Resolve -> Commit
      -> PostTick committed state publication
      -> UI / save / replay / diagnostics projections
```

App must not become gameplay authority. Runtime composition must not absorb gameplay policy. Simulation and Jobs must not need App callbacks for authoritative decisions. Contracts must remain data/protocol definitions, not a service locator or mutable global state container.

## Capability Matrix

| Capability | Boundary status | Authority status | Test status | Current call |
| --- | --- | --- | --- | --- |
| Project references | Strong | Clear | Architecture smoke exists | Preserve and reject dependency regressions. |
| Public implementation surface | Strong | Mostly clear | Static guards exist | Encapsulation is useful but not behavioral proof. |
| Content bootstrap | Strong | Content-owned | Strict init passes | Keep one explicit Runtime content snapshot path. |
| Content compatibility | Partial | Runtime gates restore | Codec/static coverage | Mechanical signature and symbolic handles remain incomplete. |
| Command ingress | Strong | Runtime pre-tick stage | Regression coverage | Preserve deterministic sequencing and replay identity. |
| Diff application | Partial | Simulation post-tick | Broad smoke coverage | Multi-log commit is not transactional. |
| Deterministic collections | Strong | Owner-held state | Extensive static guards | Add behavior tests where ordering changes outcomes. |
| RNG and GUID encoding | Strong | Session-owned streams | Determinism smoke | Identity collision policy remains open. |
| Navigation construction | Strong | Runtime composition | Architecture guards | One Runtime seam creates path services. |
| Navigation result semantics | Weak | Navigation-owned | Insufficient behavior tests | Partial work can still appear cacheable as `Found`. |
| Topology invalidation | Partial | Simulation should own mutation | Insufficient | Placeables/doors do not fully drive dirty topology. |
| Jobs orchestration | Partial | Jobs-owned executors | Mixed smoke/restore coverage | Read/plan/write rules are not uniform. |
| Reservation ownership | Weak | Simulation manager | Owner-token tests missing | Release lacks owner/generation compare-and-remove. |
| Item stacks and identity | Weak | Simulation item manager | Compatibility matrix missing | Merge keys and projected identity are unsafe. |
| Frame read models | Partial | Runtime publisher | Delta/presenter smoke | Publisher still reads live state on the caller thread. |
| Save capture | Partial | Runtime coordinator | Verifier/codec coverage | Sections do not come from one committed tick. |
| Save restore | Partial | Runtime staged commit | Job restore coverage | Deterministic continuation is unproven. |
| Replay hash | Partial | Runtime/Simulation builders | Ordering smoke | Must use the same committed state as save. |
| App rendering/input | Mostly bounded | Non-authoritative | Thin UI behavior coverage | Coordinate/click-state defects remain. |
| CI and test runner | Partial | Repository tooling | Workflow added | Current harness stops at a stale guard. |
| Runtime session ownership | Partial | Runtime facade | Architecture smoke | `FortressRuntimeSessionCore` still concentrates several owners. |

## Recently Completed Work

This summary records delivered architecture movement; it does not override the failed full-test gate.

- On 2026-07-11 the six active planning files were rewritten from 7,265 lines of overlapping status/history into about 2,100 lines with one owner per kind of fact: target plan, execution evidence, rules, reusable lessons, measured optimization candidates, and agent bootstrap. Percentage claims and obsolete current/previous batch narratives were removed; the docs-only patch passes `git diff --check`.
- App gameplay access was reduced to Contracts- and Runtime-facing ports; ordinary App paths no longer import live Simulation, Jobs, Navigation, Content, or WorldGen implementations.
- Runtime session ports, command targets, content snapshots, world generation, job composition, and snapshot facades replaced multiple App-owned composition paths.
- Lower implementation surfaces were narrowed to internal/friend access with architecture guards.
- Wall-clock path/scheduler budgets became deterministic work counts, and authority collections gained explicit stable ordering.
- Command, event, RNG, world, order, path-cache, and job queue state moved toward owner-held state rather than callback-mutated concurrent collections.
- Deterministic GUID encoding became explicit little-endian; scoped derivation replaced position-only helpers.
- Lower modules gained injectable diagnostic sinks; transport statistics became executor/session-owned.
- Save work added versioned documents, manifests, compatibility checks, migration planning, content summaries, validation, and staging-session commit.
- Mining, craft, and transport gained snapshot mapping and restoration helpers, including empty pre-start state handling.
- Replay/save builders and large Simulation/Jobs files were split by domain responsibility.
- Frame publication was split into state, request hash, map delta, overlay delta, and presenter projections; App gained presenter caches.
- Active architecture/save documents were refreshed, obsolete records archived, and `.github/workflows/dotnet-ci.yml` added.

These changes establish useful boundaries. They do not prove atomic snapshots, continuation-correct saves, transactional jobs, or complete topology invalidation.

## B0 Delta Ledger

| ID | Invariant or defect | State | Current evidence | Exit condition |
| --- | --- | --- | --- | --- |
| B0-1 | UI, save, replay, and diagnostics consume one immutable committed tick | Partially fixed | DTO publisher/caches exist, but publication and save assemble live state in separate reads. | Simulation atomically publishes versioned PostTick state used by every projection. |
| B0-2 | An incomplete path is never reported or cached as complete | Partially fixed | Wall-clock authority was removed, but node-budget exhaustion can still return a partial route as `Found`; `PathService` caches it. | Add partial/failure semantics and behavior-test retry/cache exclusion. |
| B0-3 | Every walkability mutation invalidates affected navigation topology | Partially fixed | Cross-chunk references improved, but placeable dirty state does not reliably enter World dirty chunks. | One mutation API invalidates terrain, doors, furniture, construction, and paths. |
| B0-4 | Item stacks merge only when every defining property is compatible | Reproduced | Merge is primarily definition-based and position indexing can overwrite a cell view. | Canonical stack key plus behavior tests for every compatibility dimension. |
| B0-5 | Entity identity is collision-safe and restore preserves allocator monotonicity | Partially fixed | 64-bit GUID projection reduces risk, but collisions can overwrite and allocator state is not restored. | Monotonic IDs or generations, duplicate rejection, and high-water restore tests. |
| B0-6 | Full restore provides deterministic continuation or refuses the claim | Partially fixed | Staging and job restore exist; tick, zones, professions, allocator, exact movement, and journal continuity remain open. | Prove save/load continuation or return `Intentionally unsupported`. |
| B0-7 | Save compatibility binds all mechanical content canonically | Partially fixed | Signatures/catalog policy exist; property hashing is incomplete and tile payloads retain numeric geology/material handles. | Canonical mechanical hash and stable symbolic handles. |
| B0-8 | Only the current reservation owner can release a resource | Partially fixed | State is owner-held/stably snapshotted, but release lacks an owner/generation token. | Tokenized acquire and compare-and-remove release with stale-owner tests. |
| B0-9 | Rendering, overlays, hover, and clicks share one canonical transform | Reproduced | Entity/overlay zoom mapping differs from terrain mapping, and click resolution can prefer stale `LastMousePosition`. | One transform implementation with multi-zoom camera/click behavior tests. |

B0 entries cannot close through source-text assertions alone. Static guards protect shape; closure requires a behavior test that reaches the runtime path.

## Ordered Next Batches

### Batch 0 - Restore the Verification Gate

Scope:

- Replace the stale `.Keys` literal guard with a resilient check while preserving explicit ordinal ordering.
- Run patch hygiene, build, strict initialization, and the complete harness sequentially; record every newly exposed failure.

Acceptance:

- Build has no warnings/errors, strict initialization exits normally, and the harness reaches its final summary and exits normally.
- No test is weakened merely to accommodate variable names or formatting.

### Batch 1 - Close Small Reproduced Correctness Defects

Scope:

- Enforce canonical item stack compatibility and honest partial-path/cache semantics.
- Unify App world/screen transforms and consume current click event data.
- Replace hardcoded and XY-derived Z bounds with Runtime-authored session depth.

Acceptance:

- Table-driven item tests cover every stack property and multi-stack cell indexing.
- Budget exhaustion never enters the complete path cache; deterministic retries converge.
- Rendering, overlays, selection, and clicks share one transform tested at multiple zoom levels.
- Wheel and drag-selection Z limits match the active world depth.

### Batch 2 - Unify Topology and Reservation Mutation

Scope:

- Add one Simulation-owned topology mutation path for terrain, doors, furniture, construction, and placeables.
- Replace resource-only reservation release with owner/generation tokens and decide the long-lived entity ID contract.

Acceptance:

- Topology changes invalidate every affected chunk/path exactly once at commit.
- Stale jobs cannot release newer reservations; duplicate identity fails closed; restore preserves allocator monotonicity.

### Batch 3 - Make Save Claims Honest

Scope:

- Restore tick before pending commands and close zones, professions, allocator, exact movement, journal, metadata, and hash continuity.
- Return an explicit unsupported result for any authority slice not safely restorable.

Acceptance:

- Save at tick T, restore, run N ticks, and match an uninterrupted branch by canonical state/hash.
- Corrupt, duplicate, incompatible, or mechanically changed content fails before active-session replacement.
- No `Full` result is emitted when a required section is missing or ignored.

### Batch 4 - Publish One Committed Tick

Scope:

- Split lifecycle, checkpoint, save/restore, and read-model ownership out of `FortressRuntimeSessionCore` by responsibility.
- Publish immutable PostTick state and derive UI, save, replay, and diagnostics from it.
- Add cancellation-aware bounded stop and session-generation rejection for late work.
- Isolate normal and staged sessions from process-global mutable content/log callback state.

Acceptance:

- Every projection carries one tick/version and cannot observe live mutation.
- Concurrent render/save stress never mixes ticks; App has no mutable-world fallback.
- Session stop/replacement is bounded, and isolated sessions cannot overwrite each other's content or diagnostics authority.

### Batch 5 - Establish Plan, Resolve, Commit Vertically

Scope:

- Implement one transport/item/movement/reservation slice as `WorldReadSnapshot -> Intent -> Resolve -> Commit`.
- Make read-phase mutation impossible and define failure containment for each stage and multi-log commit.

Acceptance:

- Competing intents use deterministic tie-breakers; failed read/plan systems do not commit.
- Item transfer, movement, and reservation commit atomically or leave prior state unchanged.

### Batch 6 - Harden Schemas, Tests, and Performance Evidence

Scope:

- Add real JSON Schema validation, canonical mechanical hashes, standard filterable behavior tests, deterministic-repeat/continuation tests, and high-load benchmarks.
- Define target hardware, fixture, tick latency distribution, memory, and publication budgets before optimization.

Acceptance:

- CI runs and filters behavior tests without depending only on source-text inspection.
- Identical seeds/journals produce identical committed hashes across repeated runs.
- Performance claims include reproducible fixtures and recorded measurements.

## Definition of Done for Every Batch

- Owner and authoritative state transition are explicit.
- Contracts do not expose live mutable collections.
- Deterministic ordering and tie-breaks are defined at authority boundaries.
- Failure modes fail closed and do not overstate success.
- Behavior tests cover the invariant; static guards are supplementary.
- Canonical build, content, and full-harness gates pass.
- Active architecture/planning documents are updated and unrelated user changes preserved.

## Historical Record

This file intentionally omits the former multi-thousand-line sequence of session-level completion notes.

Use `git log -- docs/planning/REFACTOR_BATCH_PROGRESS.md` for document evolution, `git log`/`git show` for implementation checkpoints, and `docs/archive` for superseded plans and reports.

When a batch closes, summarize only its verified outcome, update the matrix and B0 ledger, and replace the next-batch section instead of appending another transcript.
