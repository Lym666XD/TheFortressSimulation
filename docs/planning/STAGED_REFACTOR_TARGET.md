# HumanFortress Staged Refactor Target

Updated: 2026-07-19
Status: controlling execution plan
Active stage: Stage 6 - merge checkpoint and cross-platform CI confirmation
Next batch: merge the Stage 6 checkpoint, confirm Linux/Windows CI evidence,
then begin S7-1 owner-level scale profiling
Scope: architecture foundation, authoritative simulation contracts,
determinism, safe parallel planning, testability, canonical content identity,
and measured scale readiness

This document is the single source of truth for refactor priority, current
evidence, open contracts, stage gates, and verified progress. It replaces the
former master plan, batch progress log, pitfalls log, and optimization backlog.

Stable engineering policy belongs in `RULES.md`. Session bootstrap instructions
belong in `AGENT_PROMPT.md`. Current implementation descriptions belong in
`docs/architecture`, not here.

## 1. Controlling Decision

HumanFortress should continue from the current architecture. A rewrite is not
justified. The project graph, Runtime composition boundary, Contracts-facing App,
deterministic primitives, typed mutation paths, and experimental Runtime
snapshot export/restore slice are valuable foundations.

The correctness and ownership stages are complete in the current working tree.
Stage 6 implementation and local evidence are complete enough for a merge
checkpoint. The foundation milestone is not complete: Linux/Windows equality is
pending the first CI run for this tree, and measured Early/Mid/Target workloads
show that scale repair is required before target-scale readiness can be claimed.
Experimental restore DTOs remain development-only and must not be described as
deterministic player continuation.

Decision:

- **Go** for staged correctness and architecture hardening.
- **No-Go** for claiming production-ready full saves, deterministic replay, or
  target-scale readiness.
- **Deferred** player save/load, autosave, persistence compatibility, and
  migration to a separate future milestone. Do not expand the experimental
  Runtime persistence substrate during this refactor.
- **No-Go** for broad gameplay expansion that displaces the Stage 6 evidence
  gate or reopens a closed B0 contract without equivalent regression coverage.
- **No-Go** for speculative ECS, Actor, GPU, or parallel-write work before the
  intent/resolve/commit boundary and performance evidence exist.

No completion percentage is maintained. Progress means a contract has executable
evidence and its stage gate passed.

## 2. Target Outcome

The foundation milestone is a deterministic fixed-tick simulation with these
properties:

1. Each mutable gameplay aggregate has one authoritative session owner.
2. Input becomes semantic commands; commands enter only at a declared tick stage.
3. Planning reads immutable state and emits intents without authoritative writes.
4. Conflict resolution is deterministic and commit is transactional.
5. PostTick publishes one immutable committed checkpoint.
6. UI, replay hash, and diagnostics derive from that checkpoint.
7. Identity, reservations, ordering cursors, RNG, and job
   progress are treated as authority.
8. Canonical content identity binds every simulation-affecting definition.
9. CI and local tests prove dependency boundaries, behavior, determinism, and
   representative scale.
10. Pure planning work can run with one or multiple workers while producing the
    same accepted intents and committed hashes.

## 3. Audit Provenance And Reconciliation

The target combines three audits. Their claims are evidence snapshots, not
timeless facts.

| Source | Baseline | Execution evidence | How it is used |
| --- | --- | --- | --- |
| Claude/Fable audit, 2026-07-07 | `main@e4bf91e` | Build and then-current harness passed | Identifies concurrency, Runtime fragmentation, live reads, determinism, data-layout, and test-infrastructure risks. |
| ChatGPT 5.6 SOL audit, 2026-07-10 | `main@e4bf91e` | Static audit; no .NET SDK, zero tests executed | Supplies the B0 defect inventory and detailed architecture acceptance criteria. |
| Codex source audit and Stage 0 verification, 2026-07-11 | `refactor1@1526cb3` production baseline plus the current working tree based on `3e4ade1` | The initial full harness stopped at a stale source guard; after repairing the verification lane, build, strict initialization, and the complete harness passed | Reproduces or reclassifies findings against the current branch and adds current Runtime, lifecycle, restore, and testability gaps. |

The working branch was `refactor1@3e4ade1` when this consolidation began.
`3e4ade1` changes planning documents only, so the production verification above
still refers to the same code state.

Audit files retained as evidence:

- `docs/chatgpt5.6sol-TheFortressSimulation-main-audit-2026-07-10.zh-CN.md`
- `docs/claude-fable-HumanFortress_审计报告_2026-07-07.md`

### Deferred persistence status

- **Player save/load:** not implemented. App's public Runtime port excludes save,
  `LOAD WORLD` is WIP, and there is no manual-save or autosave workflow.
- **Internal snapshot export/restore substrate:** implemented for declared
  Runtime/Simulation/Jobs slices and exercised through test-only internal ports.
- **Continuation-correct persistence:** not implemented. Capture is not one
  committed tick and several future-affecting authority fields are absent.

All current Runtime save document versions are development-only. They have never
been released to players and carry no backward-compatibility obligation. They
may be replaced or deleted when the future persistence milestone begins. Until
then, keep the substrate test-only and do not add player entry points, new format
versions, compatibility promises, or migration scope.

### Reconciled findings

| Finding from external audits | Current `refactor1` assessment | Target stage |
| --- | --- | --- |
| UI and internal persistence capture read live mutable state; tile/read-model tearing is possible | Closed for active UI/replay/diagnostics through immutable PostTick checkpoints and coherent committed App frames. Test-only persistence capture remains frozen for the future milestone. | 2 / Future |
| A* uses wall-clock authority and cache invalidation is disconnected | Closed: deterministic work budgets distinguish incomplete results, incomplete routes never enter the complete cache, and committed topology changes invalidate affected paths. | 1, 3 |
| Placeables and doors are missing from navigation topology | Closed: terrain, blocker, door, placeable, and cross-chunk changes validate/commit atomically and publish topology dependencies once. | 3 |
| Stack merge corrupts indexes and ignores compatibility dimensions | Closed with conservative compatibility/capacity policy, transactional mutation, index conservation, reservation ownership checks, and behavior matrices. | 1 |
| Entity identity is truncated to 32 bits | Closed for live item/creature authority with wider entity keys, fail-closed duplicate insertion, non-retreating allocators, and replay-visible identity ledgers. | 3 |
| Internal `Full` restore drops authoritative state | Versioned development documents and partial staged restore exist, but continuation is incomplete. This substrate is explicitly deferred and must not be presented as a player feature. | Future |
| Persistence content handles can silently rebind | Active Runtime catalogs are now bidirectionally bound to canonical symbolic IDs and reversible local handles. Binding those identities into a player save format remains deferred with persistence. | 5 / Future |
| No player-facing save/load workflow | Correct current product state and explicitly outside this refactor goal. | Future |
| Reservation release is not ownership-safe | Closed with owner/job/generation token compare-and-remove semantics, stale-owner rejection, and rollback coverage. | 3 |
| Zoom rendering and click authority diverge | Closed through one Contracts-owned viewport geometry consumed by Runtime and all active App render/input/selection paths. | 1 |
| Diff ordering is incomplete and ReadTick mutation is unenforced | Closed for the active pipeline through read/plan purity guards, deterministic intent resolution, and validate-before-apply/rollback-safe tick commit. | 4 |
| Runtime is a fragmented God Object | Lifecycle, checkpoint retention/publication, projection, diagnostics, and content snapshot state now have distinct Runtime owners. The session core remains a composition facade; experimental persistence remains frozen. | 2 |
| RNG, GUID, diff, and scheduler determinism defects | Owned deterministic RNG streams, explicit GUID encoding, command sequencing, local/system diff order, scheduler identity, and transport statistics improved materially. Continue guarding these gains. | Preserve |
| No CI and weak tests | Standard MSTest discovery/filtering, independent suites, an end-to-end lane, TRX artifacts, and a 2,000-tick scenario matrix are implemented. Linux/Windows equality remains pending until the configured CI workflow runs for the merged tree. | 0, 6 |
| World/data layout and broad scans limit scale | Reproduced as a severe measured scale failure: Early, Mid, and Target profiles remain scheduler-correct but allocate and stall far beyond a playable budget. Owner-level profiling and measured repair are Stage 7 work. | 7 |

Do not reopen a resolved old finding merely because it appears in an external
report. Reproduce it against current source and behavior first.

## 4. Verified Baseline

Checkpoint: 2026-07-11, Stage 0 verified working tree based on `refactor1@3e4ade1`

| Gate | Evidence | Result | Meaning |
| --- | --- | --- | --- |
| Production audit baseline | `refactor1@1526cb3` | Pass | Current source audit baseline before the planning-only `3e4ade1` commit. |
| Patch hygiene | `git diff --check` | Pass | No whitespace errors in the verified working tree. |
| Solution build | Canonical .NET 8 build | Pass | `0 Warning(s), 0 Error(s)`. |
| Strict content initialization | Headless strict content command | Pass | Exit code `0`. |
| Filtered deterministic lane | `--suite deterministic-authority` | Pass | Exit code `0`; all deterministic authority guards reached completion. |
| Full custom harness | Direct test DLL execution | Pass | Exit code `0`; all eight registered suites and the final phase summary completed. Reverified after the complete Stage 1 implementation on 2026-07-11. |
| Runner failure contract | Per-suite exception capture | Pass | A failed suite reports its ID and stack, returns `1`, and does not prevent later suites from running. |

### Stage 5 verification checkpoint

Checkpoint: 2026-07-15, current `refactor1` working tree

| Gate | Evidence | Result | Meaning |
| --- | --- | --- | --- |
| Patch hygiene | `git diff --check` | Pass | No whitespace errors after the Stage 5 implementation. |
| Solution build | Canonical .NET 8 build | Pass | `0 Warning(s), 0 Error(s)`. |
| Focused content identity gate | `--suite content-identity` | Pass | Schema, reference, identity, catalog binding, freshness, and permutation tests completed with exit code `0`. |
| Focused authority gates | `--suite core-runtime`, `--suite mining-items-diff`, `--suite architecture-boundary`, `--suite deterministic-authority` | Pass | Runtime behavior, mutation invariants, module boundaries, and determinism remained green. |
| Full custom harness | Direct test DLL execution | Pass | Exit code `0`; every registered regression suite and the Phase A-D summary completed. |

This checkpoint verifies the working tree, not a commit. No checkpoint commit was
created as part of Stage 5.

### Stage 6 local verification checkpoint

Checkpoint: 2026-07-19, current `refactor1` working tree

| Gate | Evidence | Result | Meaning |
| --- | --- | --- | --- |
| Solution build | Canonical .NET 8 solution build | Pass | `0 Warning(s), 0 Error(s)`. |
| Strict content initialization | App `--init-only --strict-content --content-warnings-as-errors` | Pass | Strict production content composition exited `0`. |
| Standard behavior discovery | `dotnet test` with `TestCategory=discoverable` | Pass | All 12 independently discoverable behavior suites passed and emitted TRX. |
| Canonical smoke lane | `dotnet test` with `TestCategory=end-to-end` | Pass | The canonical ordered aggregate completed with exit code `0` and emitted TRX. |
| Content identity lane | `dotnet test` with `TestCategory=content-identity` | Pass | Strict schema, reference, mechanical identity, and catalog binding behavior remained green. |
| WorldGen evidence | `dotnet test` with `TestCategory=worldgen-evidence` | Pass | Fixed seeds, seed sensitivity, normalized distribution, fail-closed stages, geology handles, surface/shaft geometry, and cavern evidence passed. |
| Local determinism matrix | `dotnet test` with `TestCategory=stage6-determinism` | Pass | Seven process/configuration variants completed 2,000 ticks and matched deterministic evidence across cold repeat, warm cache, forced GC, process warm-up, tiered JIT, and transport planner workers `1`/`4`. |
| Generated fortress scenario | `generated-fortress.v1.json`, 10 ticks | Pass | Production Runtime composition completed with zero scheduler failure/quarantine and a committed final hash. |
| Scale evidence | Early, Mid, and Target scenario artifacts | Fail readiness | Authority counts and scheduler health remained valid, but allocation and latency are far outside a playable budget. See `benchmarks/decisions/2026-07-19-scale-evidence.md`. |
| Linux/Windows equality | `compare-platform-determinism` workflow job | Pending CI | Workflow and comparator are implemented; no cross-platform pass is claimed until the merged revision runs in GitHub Actions. |

This checkpoint verifies a merge candidate, not target-scale readiness and not a
GitHub CI result. Raw local timing artifacts remain outside the repository; the
versioned profiles and dated decision record retain the reproducible inputs,
environment, results, and non-claims.

Stage 0 classified every exposed failure as stale source-guard spelling rather
than production behavior regression. The affected material catalog, World
restore, entity validation, store inspection, and construction-material paths
already had behavior-level coverage. Their redundant guards now check stable
method or ordering seams instead of dictionary-view spelling, aliases, dynamic
message expansion, local variable names, or source formatting. The runner now
supports `--list`, `--help`, and repeatable `--suite <id>` while preserving the
canonical no-argument full gate.

## 5. Authority And Dependency Model

Current project references, where `A -> B` means A references B:

```text
Contracts  -> (none)
Core       -> Contracts
Content    -> Contracts
Navigation -> Contracts
Simulation -> Contracts, Core
Jobs       -> Contracts, Core, Simulation
WorldGen   -> Contracts, Core, Simulation
Runtime    -> Contracts, Content, Core, Jobs, Navigation, Simulation, WorldGen
App        -> Contracts, Runtime
Tests      -> App, Contracts, Content, Jobs, Runtime
```

This graph is acceptable and must remain CI-enforced. New assemblies require a
real ownership boundary, not a desire to reduce file size.

Required authority flow:

```text
Input -> Command -> PreTick acceptance
      -> Committed read snapshot -> Plan intents
      -> Resolve conflicts -> Commit transactions
      -> PostTick immutable checkpoint publication
      -> UI / replay / diagnostics projections
```

Module responsibilities and detailed normative rules live in `RULES.md`.

## 6. Evidence And Status Policy

### Evidence codes

- `A`: recorded by an external audit.
- `S`: reproduced or confirmed by current source inspection.
- `R`: reproduced through executable behavior.
- `T`: fixed and protected by a behavior-level regression test.
- `G`: complete local build/content/test gate passed on the stated commit.
- `X`: repeated, cross-process, cross-platform, cache-equivalence, concurrency,
  or worker-count equivalence passed as required by the contract.

An external audit alone never closes a current finding. Source-only evidence is
not behavior proof.

### B0 states

- `Reproduced`: present in current source or behavior.
- `Partially fixed`: meaningful mitigation exists, but the invariant is open.
- `Fixed + behavior-tested`: implementation and focused behavior coverage exist,
  and the complete required gate reached that coverage successfully.
- `Intentionally unsupported`: the scenario is rejected explicitly before
  partial mutation or false success.

Normal B0 closure requires at least `T+G`. Snapshot, content identity,
parallel-equivalence, and determinism contracts require the relevant `X` matrix
as well. Verification commands use `Pass`, `Fail`, `Blocked`, or
`Not run`; those labels are not B0 states.

## 7. B0 Contract Ledger

| ID | Contract | State | Current evidence | Closure evidence | Stage |
| --- | --- | --- | --- | --- | ---: |
| B0-1 | UI, replay, and diagnostics consume one immutable committed tick | Fixed + behavior-tested | `A+S+R+T+G+X`: Runtime publishes immutable generation-fenced checkpoints and coherent exact-request App frames at PostTick; App queries consume committed frame caches, and replay publication names the same committed boundary. | Checkpoint immutability, retention/base-loss recovery, late-publication rejection, coherent frame, exact-cache, projection-purity, and full-gate evidence passed. | 2 |
| B0-2 | Incomplete paths are never reported or cached as complete | Fixed + behavior-tested | `A+S+R+T+G+X`: `Partial` and per-tick `BudgetExhausted` are distinct, only destination-reaching `Found` paths enter an attempt-specific cache, retry tiers are explicit/capped/replay-hashed, and request age cannot increase work. | Focused cold/warm, exact-budget, retry, backlog, movement, replay-hash, and fail-closed experimental-mapper tests plus the complete Stage 1 gate passed on 2026-07-11. | 1 |
| B0-3 | Every walkability mutation updates topology and invalidates all affected navigation | Fixed + behavior-tested | `A+S+R+T+G+X`: terrain, blocker, door, and cross-chunk topology commits validate atomically, publish dependency/version changes once, and invalidate routes that cross affected intermediate chunks. | Focused topology transaction, cross-chunk cleanup, cache invalidation, replay-hash, and full-gate evidence passed. | 3 |
| B0-4 | Stack mutation preserves quantity, identity, compatibility, and all indexes | Fixed + behavior-tested | `A+S+R+T+G`: the old merge replaced a cell index and ignored stack policy. `ItemManager` now owns locked move/remove/split/merge, applies one conservative metadata policy, respects `StackMode` and capacity, preserves every secondary index, and rejects central-reservation/stockpile-owned identity mutation. | Focused compatibility, capacity, transfer, deep-copy, central-reservation, craft/construction selection, index-conservation tests plus the complete Stage 1 gate passed on 2026-07-11. | 1 |
| B0-5 | Live session entity identity is collision-safe and allocation never aliases an existing entity | Fixed + behavior-tested | `A+S+R+T+G+X`: item and creature insertion reject GUID/entity-key collisions atomically, allocator high-water marks cannot retreat across restore, and identity-ledger state participates in replay evidence. | Duplicate/collision, zero-mutation failure, allocator restore, identity-ledger hash, and full-gate evidence passed. | 3 |
| B0-7 | Canonical content identity covers every simulation-affecting definition | Fixed + behavior-tested | `A+S+R+T+G+X`: pinned schema validation, an explicit source-family manifest, canonical v2 serialization, collision-checked reversible handles, stable semantic-reference diagnostics, bidirectional production-catalog binding, and content/simulation permutation equivalence are enforced before Runtime composition. | Mechanical/cosmetic change policy, file/property/insertion permutation, handle reversibility, ambiguity/collision, schema/reference/freshness, catalog-binding, committed-hash, CI, and full-gate evidence passed on 2026-07-15. | 5 |
| B0-8 | Only the current reservation owner/generation can renew, release, or commit | Fixed + behavior-tested | `A+S+R+T+G`: reservation tokens use owner-locked compare-and-remove semantics; stale finalizers cannot release successor ownership, and split/finalize rollback preserves reservations. | Token matrix, stale-owner, replay ownership/generation, rollback, deferred-restore rejection, and full-gate evidence passed. | 3 |
| B0-9 | Terrain, entities, overlays, hover, and clicks use one coordinate transform | Fixed + behavior-tested | `A+S+R+T+G`: Contracts owns one SadConsole-free viewport geometry; Runtime overwrites caller bounds from the active World; terrain, entities, main overlays, hover, click, selection, camera, placement, and Z clamping consume it. | Zoom 1-4, odd surface, non-zero origin, border round-trip, current-event click, drag Z, keyboard/placement bounds, Runtime-bound override tests plus the complete Stage 1 gate passed on 2026-07-11. | 1 |

## 8. Foundation Debt Ledger

These findings are not separate B0 labels, but the foundation milestone cannot
close while they remain unclassified.

| ID | Finding | Current assessment | Stage |
| --- | --- | --- | ---: |
| F1 | ReadTick can consume or mutate authority; multi-log commit is not transactional | Closed for the active tick pipeline: read/plan purity is guarded, typed mutation families validate before apply or roll back through the tick commit envelope, and injected failures prove zero partial commit. | 4 |
| F2 | Runtime lifecycle, checkpoint, and projection authority share one partial-family core | Closed at the ownership level: lifecycle, committed-checkpoint retention/publication, and snapshot projection have distinct state owners and behavior tests. Experimental persistence remains isolated and frozen. | 2 |
| F3 | Process-global content/log callbacks weaken multi-session isolation | Closed for production composition: Runtime-owned frozen content and injected/session-owned diagnostics are isolated across independently composed sessions. | 2 |
| F4 | Scheduler shutdown has no bounded cancellation contract | Closed: lifecycle cancellation, generation fencing, bounded stop, and idempotent disposal have behavior coverage. | 2 |
| F5 | Custom smoke runner and source-text guards limit diagnosis | Closed: MSTest owns standard discovery/filtering and TRX output, 12 behavior groups run independently, the canonical ordered smoke lane remains available, and remaining text guards are limited to durable architecture seams. | 0, 6 |
| F6 | App still contains some gameplay/content policy and world-bound assumptions | Closed for active gameplay options: construction material eligibility, workshop categories, zone options, debug options, and world bounds come from Contracts/Runtime read models; App retains presentation mapping only. | 1, 5 |
| F7 | Movement has multiple progress/position owners | Closed for the migrated transport slice: planner output carries commit expectations, and movement/path cursor progress advances only after revision-CAS commit. Other job families must preserve this contract when migrated. | 4 |
| F8 | Scale, data-layout, path, planner, and WorldGen concerns lack a versioned benchmark | Reclassified from unknown to measured debt: versioned production-composition profiles and artifacts now expose severe Early/Mid/Target cost, while WorldGen fixed-seed quality fixtures and planner worker equivalence are executable. Attribution and repair remain Stage 7. | 6, 7 |

### Deferred persistence ledger

These findings are real but are not current refactor exit gates:

| ID | Deferred finding | Future closure |
| --- | --- | --- |
| PERSIST-1 | Internal capture is not one committed tick and omits future-affecting state. | Reinventory authority after the simulation transaction model stabilizes. |
| PERSIST-2 | Internal `RestoreFull` is not deterministic continuation. | Build continuation tests only when save/load becomes an active product milestone. |
| PERSIST-3 | The two-file export package is not a crash-atomic slot generation. | Design the first player slot format without compatibility obligations to current development documents. |
| PERSIST-4 | App exposes no save/load workflow. | Add public Runtime ports and player UX in the future persistence milestone. |

## 9. Stage Dependency Map

Merge work in this order:

```text
Stage 0  Trustworthy verification
   |
Stage 1  Local invariant closure
   |
Stage 2  One committed tick and isolated Runtime lifecycle
   |
Stage 3  Mutation ownership: topology, reservations, identity
   |
Stage 4  Intent -> Resolve -> Commit plus parallel-plan equivalence
   |
Stage 5  Canonical mechanical content identity
   |
Stage 6  Determinism and scale evidence
   |
Stage 7  Measured scale repair
```

Design exploration may start early. A later stage must not merge ahead of a
prerequisite contract it relies on.

PR-sized batches use stable IDs in the form `S<stage>-<ordinal>`. Do not renumber
an ID after work or evidence refers to it; split an oversized batch with an
alphabetic suffix and update `Next batch` at the top of this document.

The future persistence milestone intentionally follows this plan. Authority,
transaction, content identity, and deterministic parallel execution must be
stable before any player format or compatibility promise is designed.

## 10. Stage 0 - Trustworthy Verification

**Goal:** restore evidence that can be trusted before changing production
authority.

**Entry:** build and strict content initialization pass; the full harness exits
`134` at a likely stale source-text guard.

PR-sized batches:

1. **S0-1:** Replace the literal implementation-spelling assertion with a resilient
   structural or behavior assertion that still proves ordinal canonical ordering.
2. **S0-2:** Run the complete harness and record every newly exposed failure separately.
3. **S0-3:** Classify each failure as behavior regression, architecture regression, test
   infrastructure defect, or stale guard before changing production code.
4. **S0-4:** Establish a focused/filterable home for new invariant tests without rewriting
   the entire existing runner in this stage.
5. **S0-5:** If the complete harness exposes a genuine production regression, repair that
   invariant in a separate narrow batch and rerun the complete gate before
   advancing.

Required behavior evidence:

- equivalent material catalog insertion orders produce identical canonical
  internal-export catalog summaries;
- the full harness reaches its final summary instead of stopping at the first
  planning-known guard;
- failure output names the individual invariant that failed.

Exit gate:

- fresh canonical build: `0 warnings / 0 errors`;
- strict content initialization: exit `0`;
- complete harness: exit `0`;
- no active source guard depends on a local variable name, indentation, or an
  equivalent LINQ spelling;
- the ledger records base revision or commit, date, commands, and results.

Deferred: unrelated or broad production authority refactors and wholesale
test-framework migration. Narrow fixes required to restore a truthful baseline
remain part of Stage 0.

## 11. Stage 1 - Local Invariant Closure

**Goal:** close narrow, reproduced defects before changing larger ownership
boundaries.

**Entry:** Stage 0 is green.

PR-sized batches:

1. **S1-1:** Add stack compatibility and multi-stack-cell regression tests; implement one
   transactional merge/split/remove contract.
2. **S1-2:** Introduce explicit `Partial`/`BudgetExhausted` path semantics, define retry or
   continuation, and exclude incomplete results from the complete cache.
3. **S1-3:** Route terrain, entity, overlay, hover, click, and selection mapping through one
   canonical viewport transform.
4. **S1-4:** Consume current input-event coordinates and Runtime-authored world/Z bounds.

Required behavior evidence:

- material, quality, ownership, reservation, metadata, and capacity differences
  prevent incompatible merges;
- quantity and identity are conserved, and every unrelated stack remains
  reachable through ID and position indexes;
- budget exhaustion is not destination success and cannot poison a warm cache;
- fresh, warm-cache, and deterministic retry runs converge;
- zoom levels, camera offsets, borders, Z levels, and modal pass-through preserve
  draw/hover/click round trips.

Exit gate:

- B0-2, B0-4, and B0-9 are `Fixed + behavior-tested`;
- App no longer hardcodes or derives Z depth from XY map size;
- full Stage 0 gate remains green.

**Verified 2026-07-11:** S1-1 through S1-4 are complete. B0-2,
B0-4, and B0-9 are `Fixed + behavior-tested`; the canonical build reports
`0 warnings / 0 errors`, strict content initialization exits `0`, all eight
registered suites and the final phase summary complete with exit `0`, and
`git diff --check` is clean.

Deferred: topology redesign, identity allocation, renderer redesign, and packed
presentation deltas.

## 12. Stage 2 - One Committed Tick And Runtime Isolation

**Goal:** replace caller-thread live reads with one scheduler-owned committed
state boundary and separate Runtime responsibilities by ownership.

**Entry:** Stage 1 is green and the actual PostTick commit point is documented.

PR-sized batches:

1. **S2-1:** Define a checkpoint identity containing session epoch/generation, tick,
   content signature, schema versions, and authoritative section versions/hashes.
2. **S2-2:** Publish an immutable checkpoint atomically from the simulation thread after
   all PostTick commit and deterministic bookkeeping.
3. **S2-3:** Migrate App frame/overlay/management read models to the committed checkpoint;
   remove live mutable fallbacks.
4. **S2-4:** Build replay hash and diagnostics from the same retained
   checkpoint identity.
5. **S2-5:** Define bounded retention, delta-base loss, backpressure, and full-snapshot
   recovery policy.
6. **S2-6:** Split lifecycle, checkpoint ownership, and read-model publication out
   of `FortressRuntimeSessionCore` as services with distinct state, not merely
   more partial files. Keep experimental persistence isolated and frozen.
7. **S2-7:** Add session-generation rejection for late work, cancellation-aware bounded
   stop, and idempotent disposal.
8. **S2-8:** Remove or isolate mutable process-global content registry and
   diagnostic callback authority from independently composed sessions.
9. **S2-9:** Document the command, tick/commit, checkpoint publication, and
   transport transaction flows with a small set of maintained sequence diagrams.

Required behavior evidence:

- ticks advance while render, replay hash, and diagnostics run,
  without any projection mixing tick identities;
- a previously published checkpoint never changes;
- a slow consumer cannot mutate or indefinitely block simulation;
- late work from a stopped session cannot publish into its replacement;
- a deliberately stuck/faulting system produces a bounded structured stop result;
- two composed sessions do not overwrite each other's content or diagnostics.

Exit gate:

- B0-1 is `Fixed + behavior-tested` with concurrency/repetition evidence;
- App, replay, and diagnostics do not enumerate mutable Simulation or Jobs
  objects;
- every projection names the same epoch/tick checkpoint it consumed;
- Runtime ownership is visible in state and lifecycle, not inferred from filenames;
- F2, F3, and F4 are closed with behavior evidence.

**Verified 2026-07-15:** the Stage 2 exit gate is complete. Runtime owns
generation-fenced immutable checkpoints, bounded retention and full recovery,
coherent committed App frames, PostTick replay publication, session lifecycle,
and session-isolated content/diagnostics. The focused checkpoint/App-frame tests
and the complete harness pass.

Deferred: persistence capture/restore, packed deltas, enabling broad parallel
simulation, and broad performance tuning.

## 13. Stage 3 - Mutation Ownership

**Goal:** make topology, reservation, and identity transitions safe across every
affected subsystem.

**Entry:** committed checkpoints can observe a complete mutation result.

PR-sized batches:

1. **S3-1:** Define a Simulation-owned topology change description and transaction.
2. **S3-2:** Route terrain, construction, placeable/furniture, blocker, door, and
   cross-chunk create/remove through it.
3. **S3-3:** Publish committed topology changes once to dirty/version tracking, navigation
   rebuilds, and every registered path cache.
4. **S3-4:** Introduce owner/job/generation reservation tokens for the transport item
   pickup/delivery slice.
5. **S3-5:** Migrate remaining item and creature renew/release/commit paths to token
   compare-and-remove semantics.
6. **S3-6:** Reject duplicate/colliding entity-key insertion before index mutation.
7. **S3-7:** Record an ADR for long-lived session identity, generation, allocator,
   stale-handle policy, and external GUID compatibility.

Required behavior evidence:

- a blocker or door changes pathability at the committed tick boundary;
- cached routes crossing any affected intermediate chunk are invalidated;
- cross-chunk placeable create/remove leaves no stale owner or secondary reference;
- stale reservation tokens cannot release, renew, or commit a newer owner;
- duplicate/colliding identity fails closed and cannot overwrite an existing row.

Exit gate:

- B0-3 and B0-8 are `Fixed + behavior-tested`;
- B0-5 has collision-safe live-session uniqueness and stale handles cannot alias
  a different live entity;
- replay hashes are stable across topology and reservation scenarios.

**Verified 2026-07-15:** the Stage 3 exit gate is complete. Topology commits are
atomic and versioned once, intermediate cached routes invalidate, reservation
operations are token-CAS protected, duplicate live identities fail before
mutation, allocator state cannot retreat, and the corresponding replay hashes
are stable. Focused and complete gates pass.

Deferred: Actor ownership, replacing every external GUID in one migration, and
persisting derived navigation caches.

## 14. Stage 4 - Intent, Resolve, Commit

**Goal:** establish one deterministic, failure-atomic gameplay vertical slice
and prove that its pure planning work is safe under different worker counts.

**Entry:** topology, reservation-token, stack, and live identity primitives are
stable.

First slice: transport plus item movement, carrying, stockpile delivery,
reservation, and creature movement.

PR-sized batches:

1. **S4-1:** Build an immutable read snapshot containing only fields needed by the slice.
2. **S4-2:** Make planners emit immutable intents without consuming queues or mutating
   world/reservation state.
3. **S4-3:** Resolve conflicts with stable priority, producer/system identity, entity key,
   and local sequence tie-breaks.
4. **S4-4:** Commit accepted item, index, movement, stockpile, and reservation changes as
   one Simulation-owned transaction.
5. **S4-5:** Return explicit rejection reasons and deterministic retry/backoff state.
6. **S4-6:** Preserve the current read-failure write skip through the new Plan/Resolve
   boundary and define invariant-breach session fault policy.
7. **S4-7:** Migrate a second job family only after transport equivalence and continuation
   contracts are proven.
8. **S4-8:** Inventory every remaining `ReadTick` implementation and relocate any authority
   mutation to a declared PreTick/Commit path, or move it into an explicitly
   named sequential compatibility stage outside Read/Plan with regression
   coverage. A full intent migration is not required merely to remove mutation
   from Read/Plan.
9. **S4-9:** Add a validate-before-apply or rollback-safe tick commit envelope for
   remaining typed mutation logs so a later log failure cannot leave earlier
   aggregates committed, even before every job family emits the new intent type.
10. **S4-10:** Run the migrated planner through a deterministic worker-count
    adapter with `1` and `N` workers, collect results independently of completion
    order, and keep the production default serial until Stage 6 measurements
    justify enabling parallel execution.

Required behavior evidence:

- input collection order and worker scheduling do not change winners;
- a rejected movement intent does not advance executor-private position, path
  step, wait, or stuck progress ahead of authoritative World position;
- every injected failure point leaves prior item/index/reservation/movement state
  unchanged;
- a representative failure in each typed log family leaves the entire prior tick
  state unchanged rather than preserving earlier log-family commits;
- a failed read or plan stage emits no accepted commit;
- quantity, identity, and ownership are conserved;
- equal inputs produce equal accepted/rejected intent sets and replay hashes;
- worker count, task completion order, and forced scheduling variation do not
  change intents, rejection reasons, committed state, or hashes.

Exit gate:

- the migrated slice writes authority only through Commit;
- F1 is closed globally for Read/Plan mutation and failure-atomic tick commit;
- one planner passes repeated `1-worker == N-worker` equivalence through the
  production composition boundary;
- transport behavior remains equivalent for supported scenarios;
- in-flight state crossing a tick is explicitly owned and included in committed
  replay evidence, or is guaranteed tick-local.

**Verified 2026-07-15:** the Stage 4 transport slice is complete. Planning owns
no mutable Runtime services, resolver ordering is exhaustive and permutation
stable, `1/2/4` workers with forced delays produce equivalent plans, commit
expectations fail closed, and rollback restores every affected authority owner.
Read/Plan purity and the tick mutation envelope are behavior-tested; the full
harness passes.

Deferred: enabling broad parallel execution by default, full intent/resolver
migration of every job family, chunk-parallel commit, ECS, and Actor
infrastructure.

## 15. Stage 5 - Canonical Mechanical Content Identity

**Goal:** make content validation reproducible and bind every
simulation-affecting definition to one canonical identity independent of file,
insertion, or runtime handle order.

**Entry:** live authority, transaction, and deterministic planning contracts are
stable enough to identify every mechanical input they consume.

PR-sized batches:

1. **S5-1:** Select and pin a standards-compliant JSON Schema validation path with
   deterministic base-URI resolution and issue ordering.
2. **S5-2:** Define canonical mechanical serialization for every
   simulation-affecting definition field and catalog relationship.
3. **S5-3:** Define stable symbolic IDs and collision-checked compiled local
   handles; keep diagnostics reversible to canonical IDs.
4. **S5-4:** Separate cosmetic-only fields from mechanical compatibility and hash
   policy.
5. **S5-5:** Move remaining App-authored gameplay/content policy, including
   construction material eligibility, behind Content/Runtime read models.
6. **S5-6:** Add generated-output freshness, schema, semantic-reference, and
   canonical-hash gates to CI.
7. **S5-7:** Run the same simulation fixture against permuted content file and
   insertion orders, asserting identical compiled catalogs and committed hashes.

Required behavior evidence:

- invalid syntax, schema, references, duplicates, and ambiguous IDs fail closed
  with stable file/path/category diagnostics;
- file and insertion order cannot change compiled handles or simulation results;
- changing any mechanical field changes the canonical mechanical signature;
- cosmetic-only changes follow the declared non-mechanical policy;
- every local handle resolves back to one canonical ID and collisions fail before
  Runtime composition;
- App contains presentation mappings, not gameplay eligibility or world policy.

Exit gate:

- B0-7 is `Fixed + behavior-tested` with order/change/repetition evidence;
- strict mode performs real schema and cross-reference validation;
- generated/compiled content identity is reproducible in CI;
- no current gameplay result depends on source-file enumeration or accidental
  numeric catalog assignment.

### Stage 5 completion evidence

**Verified 2026-07-15:** S5-1 through S5-7 and the Stage 5 exit gate are
complete in the current working tree.

| Batch | Delivered evidence |
| --- | --- |
| S5-1 | `JsonSchema.Net` `9.2.2` is pinned; the adapter resolves only the local deterministic registry and emits canonically ordered issues. Active source families are bound to real Draft-07 schemas through one manifest. |
| S5-2 | Canonical mechanical serializer/policy v2 covers active source families, normalizes declared unordered sets, and treats unknown fields conservatively as mechanical. |
| S5-3 | Canonical symbolic IDs compile to contiguous collision-checked local handles; every handle reverses to exactly one ID, including object-map and attachment namespaces. |
| S5-4 | Cosmetic fields and unordered collections are declared per family. Recipe display names are resolved for presentation and do not enter World or job replay authority. |
| S5-5 | Construction requirements are structured; construction material eligibility, workshop categories, zones, debug options, and world bounds are delivered through Content/Runtime read models. App-owned fallback policy and unused App registry loading were removed. |
| S5-6 | Strict Runtime startup runs schema, semantic-reference, freshness, identity, and bidirectional catalog-binding gates before composition. CI runs the focused content identity gate before the complete smoke runner on Ubuntu and Windows. |
| S5-7 | File order, JSON property order, catalog insertion order, and active-source permutation preserve canonical signatures, reversible handles, compiled production catalogs, and the committed Runtime hash. |

#### Active source policy

The source-family manifest is the authority for classification, schema binding,
and activation. Every repository JSON source is either active or explicitly
excluded; unclassified active files fail closed.

The active recipe family is `data/core/recipes/core_*.json`. It currently
contains three validated recipes in `core_recipes.json`. The older
`recipes.*.json` family is explicitly classified as `inactive-draft-source`
because its unresolved item/workshop relationships do not satisfy the active
content contract. This deliberately retires unusable draft recipes; it is not a
migration or content-preservation claim.

Duplicate legacy workshop sources and currently unconsumed registry/tuning
families are likewise explicitly excluded rather than silently enumerated or
hashed. Professions and stockpile presets used by Runtime are loaded once by
Content and frozen into the Runtime content snapshot. Promoting an excluded
family requires a real schema, semantic references, Runtime consumption, and the
same permutation/change-sensitivity evidence as every other active family.

Player persistence remains outside this stage. The canonical identity is not a
player save-format promise, and no save/load, compatibility, or migration scope
was added.

Deferred: binding content into a player save format, mod hot reload, compiled
content packs unless measured, and arbitrary-version migration.

## 16. Stage 6 - Determinism And Scale Evidence

**Goal:** turn architecture, determinism, WorldGen, and safe-parallelism claims
into repeatable local/CI artifacts, then hand measured bottlenecks to Stage 7.

**Entry:** every active B0 is closed or explicitly unsupported; deferred
persistence findings are not part of this gate.

Implementation status at the 2026-07-19 merge checkpoint:

1. **S6-1 - locally complete:** Migrate fragile source guards and high-value behavior groups into
   standard discoverable/filterable test projects while retaining an end-to-end
   smoke lane.
2. **S6-2 - implemented, CI result pending:** Add fresh/warm-cache, repeated-process, GC-pressure,
   tiered-compilation, and supported-OS hash matrices over versioned command
   journals.
3. **S6-3 - locally complete:** Add a production-composition headless scenario runner with
   deterministic seed, workload tier, tick count, command stream, counters, and
   final hashes.
4. **S6-4 - baseline complete, attribution deferred:** Record tick distributions,
   allocations, path/cache work, job/backlog counts, dirty topology, checkpoint
   bytes, working set, and scheduler health. Setup/per-stage timing and sampling
   profiles are the first Stage 7 batch.
5. **S6-5 - decision recorded:** No performance optimization was admitted from
   intuition. The only parallel-default decision remains serial because four
   planner workers matched hashes but did not show credible benefit and allocated
   more in the local sample.
6. **S6-6 - measured order established:** Early/Mid/Target evidence now orders
   attribution work ahead of item/index/path/layout rewrites; implementation
   moves to Stage 7.
7. **S6-7 - locally complete:** Give provisional WorldGen algorithms honest names or replace them
   with validated coherent algorithms, then protect fixed seeds with
   determinism, distribution, connectivity, and quality fixtures.
8. **S6-8 - local decision complete:** Run a deterministic parallelism decision gate for each candidate:
   compare `1` and `N` workers, measure speedup/overhead, and enable parallel
   execution by default only when hashes match and the target workload benefits.

Initial workload tiers:

| Tier | Workers | Items | Active + queued jobs | Map | Purpose |
| --- | ---: | ---: | ---: | --- | --- |
| Early | 50 | 10,000 | 500 | 4x4 chunks | Normal startup play |
| Mid | 250 | 50,000 | 3,000 | 8x8 chunks | Sustained colony |
| Target | 1,000 | 100,000 | 10,000 | 16x16 chunks, multi-Z | Design-target evidence |
| Stress | Configurable | Configurable | Configurable | Pathological topology/path fixture | Failure and scaling limits |

Required counters:

- tick p50/p95/p99/max and per-stage deterministic work counts;
- allocation bytes per tick and checkpoint publication;
- path requests, expansions, partials, cache hits/misses/evictions;
- dirty chunks, topology versions, and navigation rebuilds;
- planner scanned/accepted/deferred/starved counts and oldest wait age;
- job, backlog, reservation, and movement counts by family;
- full/delta snapshot bytes and presenter redraw scope;
- peak working set and final committed hashes.

Wall-clock measurements are diagnostics only. They never decide simulation work,
ordering, retry, or success.

Current gate result:

- **Pass locally:** behavior suites are independently filterable and emit TRX;
  the 2,000-tick matrix matches across repeated processes, cold/warm cache,
  forced GC, process warm-up, tiered compilation, and planner workers `1`/`4`.
- **Pass locally:** versioned scenario/journal inputs, strict artifact schemas,
  scheduler-health failure gates, deterministic comparators, and dated decision
  records exist. F5 is closed and F8 is measured rather than speculative.
- **Conservative default:** the parallel transport planner stays disabled by
  default because equivalence passed but benefit did not.
- **Pending external evidence:** Linux/Windows artifact equality and workflow
  portability require the first GitHub Actions run for the merged revision.
- **Failed readiness:** Early/Mid/Target latency and allocation are not acceptable.
  Stage 6 has produced the evidence; Stage 7 must repair the measured owners.
- **Not yet scheduled:** a nightly 20-repeat/platform lane remains future CI
  capacity work and is not claimed by this checkpoint.

The Stage 6 code is mergeable when the local validation section remains green.
Stage 6 is closed only after Linux/Windows CI comparison passes; that closure
does not imply Stage 7 or the foundation exit gate has passed.

## 17. Stage 7 - Measured Scale Repair

**Goal:** make the production-composed simulation scale by removing measured
cost from its actual owners without weakening committed authority, replay
evidence, or deterministic ordering.

**Entry:** the Stage 6 merge checkpoint is locally green and its versioned scale
profiles reproduce the cost. Begin implementation after Linux/Windows
determinism CI confirms the merged tree.

Ordered batches:

1. **S7-1:** Add setup-phase and owner/stage timing plus allocation attribution
   to the scenario runner, and capture a sampling/allocation trace for Early.
   Diagnostics must not affect work, ordering, or success.
2. **S7-2:** Identify the dominant owner from traces and add a focused
   correctness/hash test that protects its authority contract before changing it.
3. **S7-3:** Remove the first measured repeated full-state allocation or scan,
   record before/after Early evidence, and reject the change if hashes diverge.
4. **S7-4:** Advance through Mid only after Early improves materially; repeat
   owner attribution instead of assuming the same hotspot dominates.
5. **S7-5:** Run Target only after projected cost and shorter probes make a full
   run practical. Record bounded runtime, allocation, and working-set budgets for
   the reference environment.
6. **S7-6:** Revisit path/index/work-queue/data-layout candidates in measured
   order. Parallelism remains a final option, never the first response to
   excessive allocation or repeated whole-state work.

Stage 7 exit gate:

- setup, tick-stage, checkpoint/hash, planner, and projection costs can be
  attributed without changing deterministic evidence;
- each optimization has a named owner, before/after artifact, behavior/hash
  protection, and rollback path;
- Early and Mid complete within explicit reference budgets established by S7-1;
- Target completes in a practical bounded run and no longer allocates tens of
  gigabytes per tick;
- no optimization introduces App live reads, alternate authority, unstable
  iteration, wall-clock decisions, or parallel authoritative writes;
- all Stage 6 determinism and standard behavior gates remain green.

## 18. Foundation Exit Gate

The architecture foundation milestone is complete only when:

1. The dependency graph in Section 5 is CI-enforced.
2. Every B0 is `Fixed + behavior-tested` or `Intentionally unsupported` with
   fail-closed behavior.
3. App gameplay reads use immutable committed read models only.
4. Replay, diagnostics, and UI name and consume one committed checkpoint.
5. Read/Plan mutation is prohibited and at least one core gameplay slice uses
   deterministic intent/resolve/transactional commit.
6. Topology changes update every authoritative and derived view exactly once.
7. Stack, identity, quantity, and reservation ownership are conserved.
8. Mechanical content signatures cover all simulation-affecting content and
   compiled handles cannot silently rebind.
9. Runtime lifecycle/checkpoint/projection owners are separate and session
   shutdown/replacement is bounded.
10. At least one production-composed planner proves repeated
    `1-worker == N-worker` hashes; parallel execution is enabled only with
    measured benefit.
11. The complete build, strict-content, behavior, and determinism gates pass in
    CI.
12. Representative workload artifacts exist; no unbounded whole-world work is
    accepted without an explicit measured budget.
13. Active documents distinguish current behavior, limitations, targets, and
    unsupported scenarios without percentages or stale migration narratives.

## 19. Cross-Stage Non-Goals

Unless a later measured decision supersedes this list, do not schedule:

- a full ECS rewrite;
- Actor-per-chunk or mailbox infrastructure;
- GPU pathfinding or simulation;
- unsafe/SIMD-first rewrites;
- parallel authoritative writes or parallelizing the current coarse ReadTick list;
- persisted navigation, rendering, spatial, or presenter caches;
- packed/SoA storage as a prerequisite for correctness;
- packed presenter deltas before committed checkpoint ownership exists;
- broad combat, fluid, economy, storyteller, or AI feature expansion;
- splitting every folder or partial family into another assembly;
- player save/load, autosave, persistence compatibility/migration, cloud saves,
  multiplayer networking, or mod hot reload;
- optimization from intuition without a repeatable workload.

## 20. PR Contract

Each refactor PR should close one principal invariant or one vertical migration
step and state:

```text
Contract/B0:
Problem and evidence before:
Authoritative owner before/after:
Behavior test added first:
Production change:
Replay/content impact:
Unsupported cases:
Verification commands and results:
Deferred work:
```

PR requirements:

- preserve unrelated user changes;
- keep deterministic order and tie-breaks explicit;
- reject invalid or unsupported input before partial mutation;
- do not mix formatting, namespace cleanup, and authority behavior changes;
- static guards may protect durable boundaries but cannot close a B0;
- update this ledger only with evidence produced by commands that completed;
- do not commit unless the user explicitly requests it.

## 21. Validation Commands

Run .NET commands serially. Never overlap build, test, or App processes against
the same output graph.

Lightweight checks:

```bash
git status --short
git diff --check
git diff --stat
```

Build:

```bash
/opt/homebrew/opt/dotnet@8/bin/dotnet build HumanFortress.sln \
  --no-restore -m:1 -v:minimal \
  -p:RunAnalyzers=false -p:UseAppHost=false
```

Strict headless content initialization:

```bash
/opt/homebrew/opt/dotnet@8/bin/dotnet exec \
  src/HumanFortress.App/bin/Debug/net8.0/HumanFortress.App.dll \
  --init-only --strict-content --content-warnings-as-errors
```

Standard test discovery:

```bash
/opt/homebrew/opt/dotnet@8/bin/dotnet test \
  tests/HumanFortress.App.Tests/HumanFortress.App.Tests.csproj \
  --no-build --configuration Debug --list-tests
```

Local merge gates emit TRX and run separately so one failed group does not hide
later evidence:

```bash
/opt/homebrew/opt/dotnet@8/bin/dotnet test \
  tests/HumanFortress.App.Tests/HumanFortress.App.Tests.csproj \
  --no-build --configuration Debug \
  --filter "TestCategory=content-identity" \
  --logger "trx;LogFileName=content-identity.trx" \
  --results-directory artifacts/test-results

/opt/homebrew/opt/dotnet@8/bin/dotnet test \
  tests/HumanFortress.App.Tests/HumanFortress.App.Tests.csproj \
  --no-build --configuration Debug \
  --filter "TestCategory=discoverable" \
  --logger "trx;LogFileName=discoverable.trx" \
  --results-directory artifacts/test-results

/opt/homebrew/opt/dotnet@8/bin/dotnet test \
  tests/HumanFortress.App.Tests/HumanFortress.App.Tests.csproj \
  --no-build --configuration Debug \
  --filter "TestCategory=end-to-end" \
  --logger "trx;LogFileName=end-to-end.trx" \
  --results-directory artifacts/test-results

/opt/homebrew/opt/dotnet@8/bin/dotnet test \
  tests/HumanFortress.App.Tests/HumanFortress.App.Tests.csproj \
  --no-build --configuration Debug \
  --filter "TestCategory=stage6-determinism" \
  --logger "trx;LogFileName=stage6-determinism.trx" \
  --results-directory artifacts/test-results
```

See `benchmarks/README.md` for scenario reproduction and comparison commands.
Do not include the Target profile in the ordinary local merge gate until Stage 7
makes its execution bounded.

If a .NET command has no output for about 30 seconds, inspect processes before
starting another:

```bash
pgrep -fl "dotnet|msbuild|VBCSCompiler|HumanFortress"
```

VS Code Roslyn language-service processes alone are not evidence that a build or
test is still progressing.

## 22. Document Ownership And History

Active planning documents:

- `STAGED_REFACTOR_TARGET.md`: audit reconciliation, evidence, ledger, stages,
  gates, and current priority.
- `RULES.md`: stable current/target engineering policy, review heuristics,
  reusable failure lessons, and optimization admission rules.
- `AGENT_PROMPT.md`: short session bootstrap that points to the two documents
  above.

Current implementation descriptions remain in:

- `docs/architecture/GAME_ARCHITECTURE.md`
- `docs/architecture/SAVE_REPLAY_ARCHITECTURE.md`
- `docs/architecture/DETERMINISM_CI.md`

Detailed history remains available through Git and `docs/archive`. For removed
planning files, use `git log --all -- <old-path>` and `git show <commit>:<path>`.
Do not recreate a session-by-session progress transcript in this file. After a
meaningful batch, update the relevant ledger row, verification baseline, active
stage, and next PR-sized batch in place.
