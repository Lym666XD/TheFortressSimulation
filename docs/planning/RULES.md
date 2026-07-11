# HumanFortress Engineering Rules
Status: Active engineering policy
Last rewritten: 2026-07-11

## 0. How To Read This Document
This document defines the rules used to review and change HumanFortress.
Rule labels are intentional:
- **Current Normative** describes a boundary or invariant that the current codebase supports and must not regress.
- **Current Limitation** records an observed gap. It is not permission to expand the gap.
- **Target Normative** describes the required end state. It is a delivery target, not a claim that the implementation already satisfies it.
- **Informative** explains rationale or gives examples.
A target rule becomes current only after implementation, behavior tests, and the active architecture documents agree.
When documents disagree:
1. Current source behavior and executable tests establish what exists.
2. Current Normative rules establish what new changes may do.
3. Target Normative rules establish the refactor direction.
4. Progress logs and archived documents are historical evidence only.
Do not weaken a behavior test merely to make an architecture claim pass.
First determine whether the test found a runtime defect or only a stale textual guard.

## 1. Engineering North Star
### Current Normative
- Simulation state has an explicit owner.
- Gameplay mutation enters through Runtime commands, scheduled systems, or typed mutation logs.
- Cross-module data travels through Contracts DTOs and ports.
- App is a presentation and input adapter, not a gameplay implementation module.
- Deterministic behavior uses explicit ordering, stable identity, deterministic work budgets, and owned RNG streams.
- Invalid external data fails through structured diagnostics without partially committing authority state.
- Derived state is rebuildable from authoritative state or explicitly persisted when it affects future behavior.
### Target Normative
- Every tick produces one immutable committed state boundary.
- Simulation work follows `ReadSnapshot -> Intent -> Resolve -> Commit` where parallel work is useful.
- UI, save, replay hash, and diagnostics read committed data rather than a live mutable `World`.
- A loaded save is a deterministic continuation, not merely a visually similar reconstruction.

## 2. Module Boundaries
### Current Normative
`HumanFortress.Contracts`
- Owns passive cross-module DTOs, value contracts, and interfaces.
- Must not own gameplay algorithms, mutable authority, content loading, file IO, clocks, RNG selection, or identity generation.
- Must not add `Random`, `Random.Shared`, `Stopwatch`, wall-clock probes, or `Guid.NewGuid()` helpers for simulation behavior.
`HumanFortress.Core`
- Owns foundation primitives such as ticks, commands, events, deterministic diff primitives, and RNG infrastructure; it consumes diagnostic contracts from Contracts.
- May depend on Contracts only.
- Must not own content registry implementation, world state, concrete jobs, Runtime composition, or App concerns.
`HumanFortress.Content`
- Owns JSON parsing, definition validation, structured registries, catalog construction, and content snapshot capture.
- Its implementation surface remains internal or friend-only except serializer-required accessors.
- Serializer-required public setters are not general-purpose public API.
- The external application entry to content loading is Runtime's `FortressRuntimeContentLoader`.
- App may choose the base directory and request a Runtime content load; it must not traverse content files or call Content implementation loaders directly.
`HumanFortress.Simulation`
- Owns authoritative world, chunk, terrain, item, creature, order, stockpile, zone, placeable, workshop, and reservation state.
- Owns mutation validation, diff application, authoritative save mapping, and world replay field selection.
- Must not depend on Jobs, Navigation implementation, Runtime, or App.
`HumanFortress.Navigation`
- Owns concrete pathfinding, navigation chunks, deterministic queues, and path caches.
- Exposes cross-module shapes through `HumanFortress.Contracts.Navigation`.
- Reads the world through adapter contracts; it must not take ownership of Simulation state.
`HumanFortress.Jobs`
- Owns job planning, assignment, execution, backlog, worker selection, profession state, and job replay/restore logic.
- May consume Simulation through the existing bounded friend bridge while the refactor is active.
- Must not leak concrete job executors to App.
`HumanFortress.WorldGen`
- Owns deterministic generation services and generation phases.
- Consumes explicit content/catalog contracts supplied by Runtime.
- Must not read global content singletons or move generation policy into App.
`HumanFortress.Runtime`
- Owns composition, session lifecycle, the tick host, command staging, navigation adapters, save/replay coordination, content startup, and App-facing ports.
- Is the only normal composition root for Simulation, Jobs, Navigation, WorldGen, and Content.
- Owns all save directory IO, codecs, validation, migration, staging restore, and commit policy.
- May expose Contracts DTOs and narrow Runtime facades to App, not lower-module implementations.
`HumanFortress.App`
- Owns SadConsole/MonoGame presentation, input mapping, layout, view state, startup shell, and user-facing diagnostics.
- References only Contracts and Runtime projects.
- Chooses content/save directories and presents Runtime-authored results.
- Must not perform save IO, decode save files, compare content signatures, mutate `World`, execute jobs, or implement gameplay rules.
### Current Limitation
- `InternalsVisibleTo` is still used as migration scaffolding between lower implementation modules and Runtime/tests.
- Friend access does not make an implementation type a stable public contract.
- New App friend access and new App references to lower modules are prohibited.
### Target Normative
- Replace broad friend access with narrow module-owned ports where it reduces real coupling.
- Keep concrete implementation types internal even after friend bridges shrink.
- Split `FortressRuntimeSessionCore` by state ownership, not merely by adding more partial files.

## 3. Authority State And Ownership
### Current Normative
- Every mutable gameplay collection has one declared module and session owner.
- Authority includes visible entities and hidden behavior-affecting state.
- Hidden authority includes ID cursors, command sequences, RNG streams, queue order, retry/backlog order, reservation state, and low-frequency schedule state.
- Owner locks protect a state transition; concurrent containers do not replace an ownership model.
- Dictionary enumeration is never simulation order.
- Owner snapshot methods materialize explicitly sorted rows.
- A rebuildable cache is derived state only if rebuilding it cannot alter gameplay results.
- Static mutable gameplay counters and process-global job statistics are prohibited.
- Runtime session services own scheduler, command queue, mutation logs, RNG streams, and command identity sequence as one session unit.
### Target Normative
- Each authority aggregate exposes an immutable read snapshot and a single commit API.
- Commit validation is performed before authority mutation.
- Cross-aggregate transactions either commit together at the tick barrier or do not commit.
- Ownership is visible in types and APIs, not inferred from call order.

## 4. Tick, Read, And Write Rules
### Current Normative
- Runtime executes queued commands in the pre-tick stage.
- `ITick.ReadTick` runs before `ITick.WriteTick`.
- The current coarse system read phase is sequential and deterministic.
- Systems are ordered by `Priority`, then ordinal `SystemId`.
- Lower numeric priority executes first and wins priority conflicts throughout the codebase.
- The write phase is serialized.
- Typed mutation logs are merged and applied in deterministic post-tick order.
- Navigation dirty chunks are rebuilt after committed simulation diffs.
- A system that fails its read phase does not execute its write phase for that tick.
- Do not reintroduce `Parallel.ForEach` over the current coarse system list.
- Locks around current owner reads and writes are allowed while immutable committed snapshots are not yet complete.
- A lock must protect a documented owner boundary and must not establish ordering through timing.
### Current Limitation
- Some reads still observe live session state under coarse locks.
- Some current `ReadTick` implementations mutate workshop, backlog, active-job, reservation, movement, or statistics state; this blocks safe parallel reads and must not be copied into new systems.
- Some subsystem mutation APIs still rely on phase discipline rather than a uniform transaction type.
- Post-tick diff application is a commit-like barrier, but there is not yet one immutable committed-state object for every consumer.
### Target Normative
- Systems read only an immutable snapshot for tick `N`.
- Systems emit intents without mutating authority during planning.
- A deterministic resolver validates conflicts, identity, reservation, and conservation rules.
- One commit stage applies the accepted intent set and publishes tick `N`.
- Parallel read/plan work is allowed only for proven non-overlapping partitions with deterministic collection and merge order.
- Work partition count, thread timing, and machine speed must not change results.

## 5. Determinism And Ordering
### Current Normative
- The determinism contract is same content signature, seed, initial authority, command records, and tick count producing the same checkpoint hash.
- Simulation-visible budgets are counts, not elapsed milliseconds.
- Pathfinding uses node/search/request budgets, never `Stopwatch` or frame time.
- Queue authority uses explicit FIFO storage and stable tie-breakers.
- Text ordering uses ordinal comparison unless a content contract explicitly says otherwise.
- Spatial rows define their full sort tuple, including `Z`, chunk coordinates, local index, and entity key where relevant.
- Replay hashes use canonical primitive encoding and explicit field order.
- Never use object `GetHashCode()`, culture-sensitive formatting, dictionary order, or platform-endian encoding as replay authority.
- RNG is obtained from session-owned named deterministic streams.
- Runtime command payloads include all execution-affecting inputs and an explicit payload version.
- Duplicate command payloads remain distinct through the session-owned command identity sequence.
- Lower numeric priority always wins.
- Equal priority is resolved by explicit system order, ordinal stable ID, local sequence, or a domain-specific canonical tuple.
- Documentation and UI labels must not describe a larger number as higher execution/conflict priority.
### Target Normative
- Determinism tests cover long-running sessions, save/load continuation, and different scheduling configurations.
- Replay checkpoint sections identify divergence by authority owner.
- All behavior-affecting derived state is either included in the checkpoint or proven canonical when rebuilt.

## 6. Entity Identity
### Current Normative
- Full GUID identity remains authoritative for current items, creatures, placeables, jobs, commands, and reservations where defined.
- Deterministic GUID generation uses explicit scoped inputs and portable byte encoding.
- Position alone is not a sufficient identity scope.
- A source GUID or explicit domain scope participates in derived identity generation.
- The current 64-bit `EntityKey` is an index/diff projection, not proof of globally collision-free identity.
- New persistence or mutation contracts must not replace a full authority identity with a 32-bit truncated value.
- Index lookups are manager-owned; applicators do not scan unordered live collections to resolve identity.
### Current Limitation
- Some compatibility paths retain 32-bit IDs.
- The 64-bit GUID projection can collide and some indexes do not yet fail loudly on collision.
### Target Normative
- Entity handles include a stable ID plus generation, or use the full canonical identity where cost is acceptable.
- Duplicate or colliding authority identity is an invariant breach detected before commit/restore.
- Allocator high-water marks and generations are persisted and replay-hashed.
- Stale handles cannot resolve to a different entity after deletion/reuse.

## 7. World Topology And Navigation
### Current Normative
- Simulation owns terrain and placeable topology; Navigation owns derived traversability and path caches.
- Navigation is created through Runtime navigation services and a Simulation-backed adapter.
- Runtime registers path services so dirty-chunk invalidation reaches every active path cache.
- Path request queues preserve FIFO order when a per-tick budget is exhausted.
- A budget-exhausted search must not be treated as authoritative proof that no path exists.
- Cache keys include every behavior-affecting input, and eviction order is deterministic.
- Cross-chunk placeable references are resolved through Simulation owner queries.
### Current Limitation
- Terrain dirty propagation is stronger than placeable/door topology propagation.
- Navigation mapping still has paths that derive primarily from tile data.
- Partial path results can be misclassified/cached as complete success in the current implementation.
### Target Normative
- Every traversability-changing mutation emits one topology-change record.
- Terrain, doors, stairs, bridges, furniture, construction completion, and placeable removal use the same topology invalidation path.
- Navigation rebuild and path-cache invalidation consume the committed topology-change set.
- Path results distinguish `Found`, `NotFound`, `BudgetExceeded`, `Invalidated`, and `InvalidRequest`.
- Only complete results may enter a reusable path cache.
- Movement validates that its path revision still matches committed topology before applying a step.

## 8. Reservations
### Current Normative
- Simulation owns reservation state.
- Reserve/release mutation occurs in the serialized write/commit path.
- Reservation snapshots are materialized in stable identity order.
- Concurrent dictionary callbacks must not mutate reservation authority.
- Resource availability checks and reservation writes must use the same authority owner.
- A failed reserve does not partially decrement availability.
### Current Limitation
- Current release paths do not consistently prove owner/job/generation identity.
- A resource ID alone is insufficient protection against stale or duplicate release.
### Target Normative
- A reservation token identifies resource, owner/job, generation, quantity, and lifecycle state.
- Acquire, consume, transfer, cancel, and release are explicit validated transitions.
- Only the current token owner may consume or release.
- Duplicate release, over-consumption, and stale generation are rejected before mutation.
- Job cancellation and restore reconcile all token state without leaking or fabricating resources.

## 9. Items, Stacks, And Conservation
### Current Normative
- Item identity and stack quantity are authoritative Simulation state.
- A stack merge is valid only when every stack-affecting attribute is compatible.
- Definition ID alone is not sufficient evidence that two stacks are compatible.
- Position indexes may reference every item at a cell; adding an item must not silently replace an unrelated index entry.
- Split, merge, move, carry, install, consume, and remove validate source identity and quantity.
- Quantities never become negative or exceed the applicable stack limit.
- No item mutation may silently destroy or duplicate quantity.
- Save/export and restore preserve containment, ownership, placement, reservation, and stack-affecting state.
### Current Limitation
- Current merge logic still relies too heavily on definition identity.
- Current cell indexing can overwrite a previous same-cell entry.
### Target Normative
- One item transaction API validates a complete mutation set before commit.
- Item conservation tests compare pre-state, accepted inputs/outputs, and post-state by definition and identity.
- Stack compatibility is a named policy covering quality, material, improvements, perishability, ownership, containment, and reservations.
- Multi-item cell occupancy is represented explicitly rather than by accidental last-writer behavior.

## 10. Snapshot Publication And Read Models
### Current Normative
- Contracts owns App-facing snapshot DTOs and metadata shapes.
- Runtime owns snapshot construction, publication sequence, full/delta metadata, and presenter cache policy.
- App consumes Runtime read models; it does not assemble presentation state from `World`.
- Snapshot rows use stable ordering authored by the state owner or Runtime mapper.
- A delta declares its base publication sequence and cannot be applied to a different base.
- UI snapshot metadata is diagnostic/read-model data, not simulation authority.
- Coarse sequential capture and owner locks are allowed as the current transition mechanism.
### Current Limitation
- The current publisher can still read live session state on the calling/UI thread.
- Separate snapshot and save reads are not yet guaranteed to describe one atomic committed tick.
### Target Normative
- The simulation thread publishes an immutable committed-state handle after post-tick commit.
- Publication includes tick, schema version, session generation, sequence, and stable content/session identity.
- UI, save, replay hash, and diagnostics derive their own DTOs from the same committed tick handle.
- Presentation polling never holds a simulation owner lock for expensive mapping.
- A full snapshot is always available after delta-base loss or schema mismatch.

## 11. Save, Replay, And Content Binding
### Current Normative
- Runtime owns save snapshot creation, slot manifests, codecs, directory IO, compatibility classification, migration, validation, restore plans, and restore execution.
- App selects a directory and displays Runtime-authored DTOs; App never reads or patches `slot_manifest.json` or `runtime_snapshot.json`.
- Save writes enter Runtime's durable temp-file/document writer. The current two-file slot slice is not a whole-slot atomic generation commit and must not be described as one.
- Restore preflights and validates all supplied sections before committing a new active session.
- Full restore is staged; a failed world, jobs, RNG, command, or content step leaves the current session unchanged.
- Unknown, malformed, future, unsupported, or mechanically incompatible content fails closed with structured issues.
- Slot inspection is a read model, not restore authority.
- A restore plan is guidance; execution repeats validation.
- Command replay restore decodes the complete batch before replacing the pending queue.
- Executed history is not restored as pending work unless replay semantics explicitly request it.
- Replay/save rows and manifest sections use canonical stable ordering and hashes.
- Mechanical content compatibility decisions are Runtime-owned.
- Numeric catalog IDs are not assumed stable across content sets without a verified binding/remap policy.
- Empty job sections may restore as empty only when manifest counts and payload policy agree.
### Current Limitation
- Current full restore does not yet prove complete deterministic continuation for every authority field.
- Tick, allocator high-water state, ordinary zones/profession state, exact movement state, and executed journal semantics require explicit closure or proof.
- Current content signatures do not yet cover every mechanical property.
- Some payload fields still bind to numeric material/catalog identifiers.
### Target Normative
- A save captures one committed tick and every field that can affect future simulation.
- Restore reinstates tick, RNG, queues, cursors, allocator/generation state, jobs, reservations, and content bindings exactly.
- A post-restore checkpoint hash equals the saved checkpoint before simulation resumes.
- Content signatures hash canonical mechanical definitions, not presentation-only fields or file enumeration order.
- Content references persist stable keys and resolve through an explicit versioned binding table.
- Unsupported partial restore modes say `Unsupported`; they never silently drop authority sections.
- Replay from a checkpoint plus command records converges on the uninterrupted-session hash.

## 12. App And UI
### Current Normative
- UI actions map to semantic Runtime commands/facade calls.
- App does not mutate Simulation managers, job executors, queues, or content registries.
- App-local state is limited to presentation concerns such as selection, camera, drawers, focus, and transient interaction state.
- The same world-to-screen transform is used for terrain, entities, overlays, hit testing, and selection.
- Input handlers use the current event coordinates, not stale cached mouse coordinates.
- UI cannot infer save compatibility, migration transforms, content remaps, or restore support.
- UI may degrade gracefully when an optional read model is unavailable.
- Debug actions that alter gameplay follow the same Runtime command boundary as normal actions.
- App startup remains thin; parsing, native preload, strict content gate, headless init, and lifetime helpers stay focused.
### Target Normative
- Every gameplay screen renders only immutable Runtime snapshots.
- UI commands carry the snapshot tick/session generation needed to detect stale intents where relevant.
- Rendering cadence and dropped frames cannot affect simulation results.
- Coordinate transform behavior has desktop/mobile/zoom regression coverage appropriate to the App technology.

## 13. Session Isolation And Lifecycle
### Current Normative
- One Runtime session owns one scheduler, world, command queue, mutation-log set, RNG stream registry, job state set, navigation registry, and snapshot publication sequence.
- Starting a new session resets all session-owned queues, logs, counters, RNG streams, caches, and publisher state together.
- Stopping detaches tick handlers and background work before owned state is discarded.
- A staged restore is not visible until every required section validates and restores successfully.
- Session-owned data must not be stored in mutable statics.
- Background work from an old session must not publish into a new session.
- Public session facades reject operations when no compatible active session exists.
### Current Limitation
- The structured content registry and several logging callbacks still use process-global mutable/static compatibility paths; staging and multi-session tests must treat them as isolation debt.
- Scheduler shutdown still uses an unbounded join without a system cancellation contract.
### Target Normative
- Every session receives a generation token carried by commands, snapshots, async results, and save staging work.
- Late work with an old generation is discarded deterministically.
- Lifecycle coordination, committed checkpoint ownership, save/restore coordination, and read-model publication are separate Runtime services.
- Multiple isolated sessions can run in tests without shared mutable authority.

## 14. Failure And Error Policy
### Current Normative
Recoverable failures include:
- Invalid user commands or selections.
- Missing/invalid content files reported during bootstrap.
- Corrupt, incompatible, unsupported, or future save slots.
- Save IO failures.
- Expected path failure or deterministic budget exhaustion.
- Optional UI/debug read-model unavailability.
Recoverable failures must:
- Return a typed result or structured issue list at the owning boundary.
- Include stable issue category/context for diagnostics.
- Avoid partial authority mutation.
- Allow App to present the failure without inspecting lower implementation details.
Invariant breaches include:
- Duplicate/colliding authority identity accepted into an owner index.
- Negative item quantity or broken conservation after commit.
- Stale reservation token consumption or impossible ownership transition.
- Hash mismatch after an owner claims successful canonical restore.
- Mutation outside the declared write/commit boundary.
- A supposedly atomic transaction that partially commits.
Invariant breaches must:
- Be logged with tick, session, owner, and relevant stable identity.
- Abort or quarantine the affected tick/session operation rather than report success.
- Never be converted into a default value that allows invalid authority to continue silently.
- Be covered by a regression test before the incident is considered resolved.
### Current Limitation
- `TickScheduler` currently catches system exceptions and quarantines repeated failures.
- Quarantine is a containment mechanism, not proof that authority remains valid after an invariant breach.
### Target Normative
- Commit APIs distinguish validation failures from internal invariant breaches.
- Runtime can fail a staging session or halt an unsafe active session while preserving diagnostic evidence.
- Recovery policy is explicit per aggregate instead of relying on a broad catch-and-continue convention.

## 15. Testing And CI
### Current Normative
- Behavior tests are the primary evidence for gameplay correctness.
- Architecture text guards protect stable boundaries but must not depend on incidental variable names, indentation, or whole-method text.
- A textual guard should assert a durable forbidden dependency or required API seam.
- Determinism tests use manual `ExecuteSingleTick()` through production Runtime composition.
- Do not use the background scheduler thread for deterministic behavior tests.
- Save/restore tests cover malformed input, atomic failure, hash round trip, and current-session preservation.
- Item/reservation/topology fixes require focused behavior tests, not only source scans.
- CI runs on supported platforms and treats warnings/errors consistently with repository policy.
- Never claim full tests are green unless the complete harness completed successfully in the current verification context.
Required local verification sequence:

```sh
git status --short
git diff --check
/opt/homebrew/opt/dotnet@8/bin/dotnet build HumanFortress.sln --no-restore -m:1 -v:minimal -p:RunAnalyzers=false -p:UseAppHost=false
/opt/homebrew/opt/dotnet@8/bin/dotnet exec tests/HumanFortress.App.Tests/bin/Debug/net8.0/HumanFortress.App.Tests.dll
/opt/homebrew/opt/dotnet@8/bin/dotnet exec src/HumanFortress.App/bin/Debug/net8.0/HumanFortress.App.dll --init-only --strict-content --content-warnings-as-errors
```

Dotnet execution rules:
- Do not run multiple build/test/App dotnet commands in parallel.
- Wait for each dotnet process to exit before starting the next.
- If a dotnet command has no output for about 30 seconds, inspect processes first:

```sh
pgrep -fl "dotnet|msbuild|VBCSCompiler|HumanFortress"
```

- VS Code Roslyn language-service processes alone do not prove the build is stuck.
- Do not kill processes or rerun concurrently until the owning process is identified.
### Target Normative
- Replace the custom smoke executable with standard discoverable/filterable test projects without losing current coverage.
- Add long-horizon deterministic continuation tests and performance baselines.
- Run architecture, behavior, determinism, content, and save compatibility suites as distinct CI gates.

## 16. Documentation And Refactor Workflow
### Current Normative
- `RULES.md` records engineering policy, not batch-by-batch accomplishment claims.
- `ARCHITECTURE_REFACTOR_MASTER_PLAN.md` records outcomes, workstreams, gates, and priority.
- `REFACTOR_BATCH_PROGRESS.md` records verified evidence and current status.
- Architecture documents describe the current system and clearly label future design.
- Historical status snapshots and obsolete plans move to `docs/archive`; do not rewrite them as current truth.
- A progress percentage must be backed by explicit exit criteria. Avoid impressionistic `98%` or `99%` claims.
- Each audit finding is recorded as `reproduced`, `partially fixed`, `fixed + behavior-tested`, or `intentionally unsupported`.
- Source-only work is labeled source-only until build/test verification finishes.
- Documentation updates do not upgrade an implementation target to completed status.
- When moving/deleting many tracked and untracked documents, inspect `git status` carefully; use `git add -A` only when the user requests a commit and all moves belong to its scope.
- Never revert unrelated user changes.
- Do not commit unless the user explicitly requests it.
### Required Change Sequence
1. Read the owning implementation and its behavior tests.
2. State the authority owner and invariant being changed.
3. Reproduce the defect or add a failing focused test when practical.
4. Make the smallest coherent implementation change.
5. Add or update behavior coverage.
6. Run `git diff --check` and the narrowest relevant test.
7. Run the required build/full harness/content gate when the batch is ready.
8. Update active planning documents with actual verification evidence.
### Target Normative
- Planning is organized around authority contracts and executable gates, not file counts.
- Completed goals are removed from active plans or summarized briefly; detailed history lives in the archive/progress log.
- Large refactors ship as vertical slices that leave the repository buildable and behavior-tested.

## 17. Review Checklist
Before approving a gameplay or architecture change, verify:
- The authority owner is explicit.
- Module dependency direction is unchanged or intentionally improved.
- App still depends only on Contracts and Runtime.
- Content enters through `FortressRuntimeContentLoader`.
- Save directory IO remains in Runtime.
- Reads and writes occur in allowed tick phases.
- Lower numeric priority wins and tie-breaks are explicit.
- No wall-clock value affects simulation-visible results.
- No unordered collection enumeration establishes authority order.
- Identity is full-width/scoped and collision handling is explicit.
- Item quantity and reservation ownership are conserved.
- Topology mutations invalidate navigation and path caches.
- Snapshot/save/replay data names its committed tick or current limitation.
- Restore is validated and staged before active-session commit.
- Recoverable failures are structured; invariant breaches are not swallowed.
- Behavior tests cover the changed invariant.
- Architecture guards test durable seams rather than formatting.
- Verification claims name the commands that actually completed.

## 18. Immediate Architecture Priorities
These are Target Normative delivery priorities, not current completion claims.
1. Restore a green full harness and classify each audit finding with behavior evidence.
2. Fix stack compatibility/index conservation, partial-path classification/cache policy, and App coordinate/input defects.
3. Establish unified topology mutation and owner/generation reservation tokens.
4. Close deterministic save continuation fields or return explicit `Unsupported` for incomplete modes.
5. Publish immutable committed tick state and derive UI/save/replay reads from it.
6. Introduce the first `ReadSnapshot -> Intent -> Resolve -> Commit` vertical slice for transport/item/reservation work.
7. Complete canonical mechanical content hashing, schema validation, standard test discovery, and performance baselines.
Do not introduce ECS, actor-model infrastructure, or broad parallel scheduling merely to satisfy a target diagram.
Adopt them only if measured workload and ownership boundaries justify the added complexity.
