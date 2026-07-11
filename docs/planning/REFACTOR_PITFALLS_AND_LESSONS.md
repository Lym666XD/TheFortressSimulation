# HumanFortress Refactor Pitfalls and Lessons

Updated: 2026-07-11
Status: reusable engineering lessons, not a progress log

This document records traps that should change how future work is designed or
verified. Current status and backlog belong in `REFACTOR_BATCH_PROGRESS.md`;
target phases belong in `ARCHITECTURE_REFACTOR_MASTER_PLAN.md`; normative rules
belong in `RULES.md`.

Git history preserves the previous long-form session log. Do not regrow this
file by appending every completed edit.

## 1. Naming Is Not Evidence

Several architecture terms have been used before their full contract existed:

- `snapshot publisher` can still publish DTOs built from live mutable state;
- `ownership hardened` can mean only that a Dictionary has one writer;
- `wider entity key` can still be a collision-prone GUID projection;
- `full restore` can restore supported sections without reproducing future
  behavior;
- a partial class split can leave one object with the same responsibilities.

Before accepting a claim, ask:

1. Who owns the authoritative state?
2. At which phase can it mutate?
3. What exact version/tick does a reader observe?
4. Which fields affect future behavior?
5. How is the contract behavior-tested?

Do not use line count, file count, namespaces, public-surface allowlists, or
planning percentages as substitutes for those answers.

## 2. Partial Classes Do Not Remove God Objects

Splitting a large type into partial files improves navigation but does not
change ownership, lifecycle, or dependency direction.

Current high-risk families include Runtime session orchestration, world save
restore, content registry state, item ownership, save verification, and frame
publication. Extract a collaborator only when it receives a coherent owner
role, state boundary, and test seam.

Good extraction:

```text
SessionCore
  -> LifecycleCoordinator
  -> CommittedCheckpointOwner
  -> SaveRestoreCoordinator
  -> ReadModelPublisher
```

Weak extraction:

```text
SessionCore.Part1.cs
SessionCore.Part2.cs
SessionCore.MoreHelpers.cs
```

Avoid adding a facade whose only job is to forward every method to another
facade. Mechanical App adapters can remain thin; authoritative coordinators
must own an explicit responsibility.

## 3. Project Graphs Are Necessary but Not Sufficient

The current project-reference graph is useful and should stay acyclic. It does
not prevent semantic leaks through:

- `InternalsVisibleTo`;
- broadly shared Contracts namespaces;
- service-locator-style `World` access;
- static registries and callbacks;
- DTOs that contain policy rather than shape;
- source imports allowed only because Runtime is the composition root.

When moving code, check all of the following:

- project references;
- namespace imports;
- friend assemblies;
- public and internal API shape;
- who creates the object;
- who owns its lifetime;
- who is allowed to mutate it.

`InternalsVisibleTo` is migration scaffolding. Each use should have a concrete
consumer and must not justify new cross-module gameplay access.

## 4. Read Phase Must Actually Be Read-Only

A method named `ReadTick` is not read-only if it changes workshop queue flags,
backlogs, active jobs, reservations, movement state, statistics that affect
future behavior, or manager indexes.

Consequences of mutation during Read:

- an exception can leave half-applied authority;
- failed systems cannot be retried safely;
- deterministic parallel reads remain impossible;
- save capture can observe planning half-state;
- another planner can observe order-dependent mutations.

Target pattern:

```text
CommittedReadSnapshot
  -> planner emits immutable intents
  -> resolver validates versions/resources/ownership
  -> deterministic arbitration
  -> commit accepted changes
  -> rejected results return to job owners
```

Do not convert all systems at once. Prove one vertical slice, preferably
transport + item + reservation, before generalizing the abstraction.

## 5. Failure Isolation Is Not Transactionality

Skipping `WriteTick` after a failed `ReadTick` is useful. Catching an exception
and continuing does not undo authority already changed by that system or by an
earlier typed diff applicator.

Separate failures into:

- expected rejection: invalid request, capacity, missing content, no route;
- recoverable subsystem failure before commit: reject intents, retain last
  committed state;
- invariant breach during or after commit: stop/fault the current session and
  preserve diagnostics/checkpoint rather than continuing a corrupted world.

PreTick, barriers, post-tick publication, and multi-log commit need explicit
top-level policy. Quarantine must not turn an integrity violation into silent
partial state.

## 6. DTO Publication Is Not a Tick Snapshot

A snapshot is coherent only if all fields derive from one committed simulation
version. Copying collections under separate locks, copying references to
mutable entities, or labelling data with `CurrentTick` after the fact does not
provide that guarantee.

Correct publication flow:

```text
PostTick commit completes
  -> scheduler-owned committed version is captured
  -> immutable authority/read model is atomically published
  -> Runtime creates surface projections
  -> App reads latest published reference only
```

UI, save, replay hashing, and diagnostics do not need the same DTO schema, but
they must agree on the same committed tick/version when they claim one logical
snapshot.

Do not solve live-read races with more presentation caching. Presenter deltas
reduce transfer/redraw work; they do not establish authority coherence.

## 7. Save Capture Needs a Barrier

Reading command queue, RNG, world, jobs, and checkpoint hashes in separate
calls can mix ticks even if every individual collection is thread-safe.

The scheduler owner should capture one immutable checkpoint package after
commit. File serialization and durable I/O can happen asynchronously from that
package.

A save package should bind:

- schema and engine versions;
- committed tick/version;
- content signature and canonical catalog identity;
- all authoritative section payloads;
- section counts and hashes;
- aggregate hash derived from those exact sections;
- pending and historical command journal policy;
- allocator/cursor high-water state.

The verifier must recompute canonical relationships. Checking that each section
matches a user-supplied section hash is insufficient if metadata or the
aggregate hash can be changed independently.

## 8. Restore Success Must Mean Continuation

World reconstruction and deterministic continuation are different claims.

Continuation requires every state value that can affect future behavior,
including:

- scheduler tick;
- pending command order and command identity sequence;
- executed journal, or an explicit design that removes it from continuation
  authority while preserving audit history elsewhere;
- RNG streams;
- job active/backlog/deferred state;
- exact movement progress/pacing when it affects timing;
- profession weights/skills and generic zone instances;
- reservations with owner/generation identity;
- monotonic entity/order/zone/queue allocators and cursors;
- hidden retry/fairness counters that affect later decisions.

Required test:

```text
run seed/input to tick T
  -> capture save
  -> branch A continues N ticks
  -> branch B restores then runs N ticks
  -> compare section and aggregate hashes at multiple checkpoints
```

Run the test with non-zero T, executed and pending commands, deleted entities,
active movement, backlogs, reservations, zones, and profession state.

If a section is unsupported, return `Unsupported` or a structured partial mode.
Never return full success and silently reset it.

## 9. Staging Restore Must Not Mutate Global State

Restoring into a temporary session and swapping only on success is the correct
transaction shape. It is undermined if staging reloads a mutable process-wide
content singleton, overwrites static log callbacks, or otherwise changes the
active session before commit.

Session-specific dependencies should be immutable or instance-owned:

- content catalog snapshot;
- diagnostic sink and callbacks;
- RNG manager;
- command queue and mutation logs;
- navigation/movement services;
- job state;
- published read models.

Global fallback hooks may exist for legacy manual construction, but Runtime
composition should not depend on them.

## 10. Monotonic Identity Is Authority

Restoring an allocator from `live instance count` or current maximum ID is not
equivalent to restoring its high-water mark. Deleted identities can be reused,
causing pending commands, audit records, or external references to bind to a
new entity.

Persist allocator state explicitly. Validate on restore that:

- saved identities are unique;
- compact keys do not collide;
- generation/high-water is not below any restored identity;
- newly allocated IDs cannot reuse a prior session identity unless reuse is an
  explicit generated-handle design;
- the allocator advances deterministically after restore.

Hash projections are not identity. A 32-bit or 64-bit GUID prefix may be a
cache/index key only when collisions are detected and resolved against the full
identity.

## 11. Stack Merge Is a Conservation Transaction

Stack merge cannot be defined as `same DefinitionId`. Compatibility can include
material, quality, condition, durability, ownership, access flags, reservation
state, artifact/provenance/improvements, perishable state, and stack cap.

A correct merge:

1. Computes a documented compatibility key.
2. Selects targets in stable identity order.
3. Respects capacity and produces deterministic remainder stacks.
4. Removes only consumed identities from every index.
5. Preserves unrelated items in the same cell.
6. Returns an explicit result for accepted quantities and rejections.
7. Proves total quantity and surviving identity/index consistency.

Tests must include mixed definitions, mixed materials, reserved and artifact
items, capacity overflow, and other singleton items sharing the tile.

## 12. Reservation Release Needs Compare-and-Remove

Single-writer dictionaries solve collection races, not logical ownership.

Unsafe sequence:

```text
job A reservation expires
job B acquires the resource
late cleanup from job A releases by resource ID
job B loses its reservation
```

Acquire should return an unforgeable owner token containing resource, job/owner,
and generation. Refresh and release must compare that token with current state.

System name alone is not job identity. Two jobs in one system must not refresh
or release each other's reservations.

## 13. Topology Has One Mutation Contract

Terrain, construction completion, furniture/placeables, and door state all
affect walkability. Updating only `TileBase`, only a FurnitureCell, or only a
chunk-local dirty tile leaves navigation and path caches inconsistent.

A topology mutation should declare affected cells and atomically coordinate:

- authoritative terrain/placeable/door state;
- derived blocker/passability view;
- tile and neighbor dirty sets;
- chunk connectivity version;
- World dirty-chunk publication;
- navigation rebuild;
- invalidation of every path cache that traverses affected chunks.

Cross-chunk placeables must update and remove every secondary reference. Door
open/close tests should prove that an existing cached path is invalidated and a
new solve observes the state.

## 14. Partial Path Is a Different Result

Exhausting a deterministic node budget is not success to the original
destination. A partial path must use `PathResultKind.Partial`, name its reached
frontier, and define retry/continuation behavior.

Never put a partial result in the complete-path cache under the original
destination key. Callers must not interpret `PathComplete` as destination
arrival without checking the requested destination.

Path cache identity must also be collision-safe. A small request hash should be
paired with the full request or a canonical wide key and equality check.

## 15. Fairness Requires Persistent Order

Sorting a collection before taking the first N entries provides determinism but
can still starve later work. Bounded planners need a persistent cursor or an
explicit queue with a defined rotation/retry protocol.

Any cursor, enqueue tick, retry count, or age that changes future service order
is authoritative save/replay state.

Measure and test:

- oldest waiting age;
- every eligible item eventually receives service;
- save/load preserves the next serviced item;
- warm caches do not change simulation-visible request budgets.

## 16. Content Validation Must Be Reproducible

Loading schema files into `JsonDocument` is not schema validation. Strict mode
should run a real version-pinned validator and fail closed for the core pack.

The content pipeline should perform:

1. JSON syntax validation.
2. JSON Schema validation with reproducible base URI resolution.
3. Cross-reference and semantic validation.
4. Canonical ordering and runtime snapshot generation.
5. Mechanical-property hashes over every simulation-affecting field.
6. Stable ID/local-handle tables for save binding.
7. CI verification that generated output is current.

Catalog counts and partial hashes cannot detect same-count ID replacement or
mechanical changes. Numeric runtime handles must be rebound through canonical
IDs or a saved local ID table.

External audit numbers should not become gates until the exact command,
validator version, base directory, and output artifact are committed.

## 17. App Configuration Can Still Become Gameplay Policy

Removing App project references to Simulation/Content is not enough if App
hardcodes material eligibility, Z limits, content category mappings, or other
domain decisions.

Distinguish:

- presentation policy: glyphs, layout, labels, local shortcuts;
- gameplay/content policy: material tags, construction eligibility, world
  bounds, recipes, zone rules.

The first belongs in App. The second must come from Runtime/Content/Contracts
read models. App should not parse gameplay content JSON merely through a path
facade.

## 18. Bounded Shutdown Is Part of Architecture

An unbounded `Thread.Join()` with no cancellation contract can hang tests,
session replacement, and application shutdown indefinitely.

Lifecycle design should provide:

- cancellation or stop request visible to systems;
- bounded wait and a structured timeout result;
- session fault state and diagnostics;
- no lock held while joining;
- tests with a deliberately non-returning/faulting system;
- idempotent stop and dispose.

Do not kill arbitrary editor Roslyn processes when investigating a stuck test.

## 19. Static Source Guards Are Fragile

Source-text `Contains(...)` tests are useful for narrow forbidden-token or file-
placement rules. They are poor evidence for runtime correctness and break on
harmless refactors.

Use the right mechanism:

- project graph and public API: MSBuild metadata or API approval tests;
- namespaces/dependencies: Roslyn analyzer or syntax-tree inspection;
- runtime behavior: normal unit/integration/property tests;
- determinism/continuation: headless scenario and hash comparison;
- generated content: compiler output diff and schema tool;
- a truly forbidden token: small source guard with a precise failure message.

### 2026-07-11 incident

The full harness stopped because a static guard expected
`GetNameToIdSnapshot().Keys` while production intentionally changed to
`Select(pair => pair.Key).OrderBy(...)`. Build and strict init passed, but later
tests never ran. One giant boolean also hid which clause failed.

Lesson:

- each invariant should have its own assertion and message;
- guards must assert the intended semantic property, not old formatting;
- a stopped harness is not a fully green suite even when the failure is stale.

## 20. Test Runner Shape Affects Debuggability

The executable smoke runner has valuable broad coverage, but a single exception
prevents later groups from running and offers no filter, standard result file,
or per-test timeout.

Migration strategy:

1. Keep the existing runner as an end-to-end smoke gate.
2. Put new correctness cases in focused standard test projects.
3. Migrate fragile groups incrementally, not in one framework rewrite.
4. Add filters, TRX/JUnit output, timeouts, and coverage.
5. Keep headless production-composition determinism scenarios as integration
   tests even after unit tests exist.

Do not mix test-framework migration with large authority-model changes.

## 21. Build and SDK Pitfalls

On macOS use the explicit .NET 8 binary:

```bash
/opt/homebrew/opt/dotnet@8/bin/dotnet
```

Do not run overlapping builds/tests against the same output graph. AppHost and
shared `obj/bin` files can race or remain locked.

Recommended sequence:

```bash
/opt/homebrew/opt/dotnet@8/bin/dotnet build HumanFortress.sln --no-restore -m:1 -v:minimal -p:RunAnalyzers=false -p:UseAppHost=false
/opt/homebrew/opt/dotnet@8/bin/dotnet exec tests/HumanFortress.App.Tests/bin/Debug/net8.0/HumanFortress.App.Tests.dll
/opt/homebrew/opt/dotnet@8/bin/dotnet exec src/HumanFortress.App/bin/Debug/net8.0/HumanFortress.App.dll --init-only --strict-content --content-warnings-as-errors
```

For `dotnet exec`, do not add the `dotnet run` separator `--` before arguments.

If a command yields no output for about 30 seconds, inspect processes before
assuming it is still working:

```bash
pgrep -fl "dotnet|msbuild|VBCSCompiler|HumanFortress"
```

Roslyn/CodeAnalysis language services alone are normal editor activity.

## 22. Documentation Must Have One Owner per Fact

Repeated status prose across master plan, progress log, rules, architecture,
and lessons inevitably drifts.

Use this ownership:

- `ARCHITECTURE_REFACTOR_MASTER_PLAN.md`: goals, phases, acceptance gates;
- `REFACTOR_BATCH_PROGRESS.md`: current evidence and active ledger;
- `RULES.md`: normative constraints;
- `REFACTOR_PITFALLS_AND_LESSONS.md`: reusable lessons;
- `OPTIMIZATION_SUGGESTION.md`: measured performance candidates;
- `GAME_ARCHITECTURE.md`: current implementation map;
- `SAVE_REPLAY_ARCHITECTURE.md`: current persistence boundary.

Do not publish total completion percentages. Use capability states and test
evidence. Historical batch narratives belong in git history or `docs/archive`,
not at the top of current planning files.

## 23. Refactor Batch Template

Every architecture batch should answer:

```text
Problem reproduced:
Authoritative owner:
Current mutation/read path:
Target contract:
Files/modules in scope:
Behavior tests added first:
Migration/compatibility impact:
Verification commands and results:
Known unsupported cases:
Follow-up intentionally deferred:
```

Prefer small vertical slices that change behavior, ownership, tests, and docs
together. Avoid broad mechanical movement followed by a claim that the contract
is complete.
