# HumanFortress Refactor Pitfalls and Lessons

Date: 2026-06-26

This document records practical pitfalls found during the current architecture refactor. It is meant to keep future refactor work fast and predictable.

## Content DTO Pitfalls

### Serializer-facing DTO accessors are not ordinary implementation API

Do not blanket-convert every `public` accessor in `HumanFortress.Content` to
`internal`. Some small internal container/DTO types are bound by
`System.Text.Json`, where public setters are part of the serializer contract
unless a deliberate converter/source-generation policy replaces them.

For implementation-surface hardening, prefer explicit interface bridges and
internal member guards in Simulation/Jobs/WorldGen-style runtime code. Treat
Content JSON DTOs separately and prove the loader still binds data before
narrowing serializer-facing setters.

## Save/Replay Pitfalls

### Full restore must fail closed when Jobs checkpoint sections lack supported payloads

Runtime checkpoints already include Jobs-owned transport/mining/craft replay
hashes. That does not mean every save document can restore those executor
states: each non-empty section needs matching payload rows plus a Jobs-owned
restore seam. Keep `RuntimeSaveJobStateRestorePolicy` shared by inspection and
the actual full-restore Runtime port, so `CanRestoreFull` and `RestoreFull...`
are blocked with `slot.restore_plan` issues whenever a non-empty Jobs section is
missing verified payload/restorer support. Transport, mining, and craft now have
payload/restorer slices; keep the same fail-closed policy for malformed,
missing, or future job-state sections. Empty counted Jobs checkpoint sections
can be restored as empty state alongside world, RNG, and pending commands.

### Migration transform ids belong in a Runtime registry seam

Do not let `RuntimeSaveSlotMigrationPlanBuilder`, App, or save UI infer
migration paths ad hoc from manifest versions. Required transform ids such as
`slot:0->1` and `runtime_snapshot:2->3` are generated and checked by
`RuntimeSaveSlotMigrationTransformRegistry`. The first concrete transform is
`slot:0->1`, which rebuilds current slot metadata around a current-format
`runtime_snapshot.json`. The first runtime snapshot schema transform is
`runtime_snapshot:4->5`, and the next is `runtime_snapshot:5->6`. Runtime
snapshot migrations should apply adjacent transforms in order and validate the
final current-format result; v4 saves with non-empty mining checkpoint state
but no mining payload must remain blocked, while v5 saves without a saved
catalog payload must keep that saved catalog unavailable instead of fabricating
missing-content/remap data.
Migration execution should always enter through `RuntimeSaveSlotMigrator`, not
App or save UI helpers. Current compatible slots return a successful no-op
`RuntimeSaveSlotMigrationResultData`; unsupported or non-validating old runtime
snapshot formats fail closed with Runtime-authored migration issues until
concrete transforms exist.

### Content compatibility is not structural validation

`RuntimeSaveSlotInspectionData.Success` should stay about whether the slot files
and manifest are readable/valid. Content binding is a separate Runtime policy:
inspection carries `RuntimeSaveSlotContentCompatibilityData`, and restore plans
can be blocked with `slot.content` even when the JSON and slot manifest are
structurally valid. Do not make App, save UI, or migration helpers compare
content hashes, catalog counts, material hashes, or versions themselves.

Actual restore ports must enforce the same Runtime content compatibility gate
again at execution time. Pending-command restore is content-sensitive because
command payloads can reference recipes, constructions, materials, or other
content ids, so it should fail closed with `slot.content` until a real
missing-content placeholder/remap policy exists. World/full restore should also
fail closed and clean up any half-created Runtime session/content snapshot when
post-restore content binding fails.

If save UI needs better diagnostics, add Runtime-authored read-model fields
instead of moving comparison policy into App. The saved/current catalog
summaries on `RuntimeSaveSlotContentCompatibilityData` are intentionally sorted
inspection/read-model views of material names, terrain kind names,
construction ids, recipe ids, geology ids, and zone ids. They are diagnostic
payloads, not a migration table, and not permission for App to decide remaps.
Structured difference rows can explain hash/count/version mismatches and, for
format 6 saves where both sides have catalogs, exact saved-only/current-only
keys. For historical saves without saved catalog payloads, do not fake missing
id lists from current-only keys.

### Runtime command identity is not just payload identity

Normal Runtime session enqueue wraps commands in `RuntimeIdentifiedCommand` so
duplicate same-tick/same-payload commands still have distinct replay/debug ids.
When turning executed commands into persistence data, capture the optional
Runtime command identity sequence in `CommandReplayRecord`; otherwise a decoder
can rebuild the inner command payload but cannot reproduce the original wrapper
`CommandId`.

### Determinism tests must avoid scheduler wall-clock behavior

A full simulation-loop determinism check should use production Runtime
composition and the same tick pipeline, but advance it through manual
`ExecuteSingleTick()` calls. Starting the background tick thread turns the test
into a scheduler timing test and makes failures harder to diagnose. Keep the
Runtime manual-tick host seam internal, and compare both world replay hashes and
Runtime checkpoint hashes so jobs/commands/RNG state are covered.

The current Runtime registers only a coarse system list, not chunk-partitioned
read jobs with enforced non-overlap. Read-phase `Parallel.ForEach` added thread
scheduling noise without meaningful throughput, so the scheduler now runs read
systems in deterministic system order until real chunk-level scheduling exists.

### Runtime command payloads need explicit versions

Binary payloads are compact but fragile. Runtime command `Serialize()` methods
should write the shared Runtime command payload version header and the Runtime
replay factory should reject unsupported versions, malformed enum values, extra
trailing bytes, and reconstructed id mismatches. Do not let the save path depend
on undocumented field order.

### Replay restore must be batch-atomic

Do not call `CommandQueue.RestoreCommands(...)` while decoding records one by
one. Decode the full batch first, collect structured issues, and only replace
the pending queue when every record is valid. Also advance the Runtime command
identity cursor to the restored max identity sequence, or the next live enqueue
can reuse replay/debug ids from the restored log.

### App save UI should pass slot directories, not assemble Runtime documents

The App layer owns slot selection, confirmation UI, and user-facing failure
presentation. Runtime owns save snapshot authority, document validation,
document file helpers, command replay mapping, and restore semantics. App access
interfaces should expose directory-level save/validate/restore operations rather
than Runtime-internal codec/store/restorer helpers, Core command replay records,
or live Simulation/World objects.

A save slot directory is already more than one JSON document. The current
Runtime slice writes and validates `slot_manifest.json` beside
`runtime_snapshot.json`; the manifest carries the slot kind, snapshot document
name, snapshot format, Runtime metadata, checkpoint/world hashes, and row
counts. Directory restore should preflight both files through
`RuntimeSaveSnapshotDocumentStore.ValidateDirectory(...)` so App receives
structured `snapshot.document` or `slot.manifest` failures instead of decoding,
patching, or trusting individual Runtime save files.

Save-slot version compatibility is also Runtime policy, not App policy.
Contracts may expose a compatibility-result DTO for diagnostics/UI, but Runtime
must classify current, older, future, unknown-kind, and unsupported-document
slots. Legacy/current snapshot-only slots can migrate through `slot:0->1`, but
unsupported older Runtime snapshot document versions should still return
`MigrationRequired` and fail closed through structured migration issues rather
than being loaded partially or ignored.

Slot inspection is a read model, not a second loader. Future save UI/debug
surfaces should call a Runtime-owned inspection port and display the returned
validation, compatibility, and manifest DTOs. They should not read
`slot_manifest.json` or `runtime_snapshot.json` themselves, and they should not
reuse inspection success as proof that unsupported world/job payload slices can
be restored.

Likewise, restore-plan data is Runtime-authored UI/debug guidance, not a second
restore path. It is useful because App should not infer loadability from raw
manifest sections, but actual load still has to call Runtime restore ports and
let their validation/preflight path decide again at execution time.

Keep the Runtime save store split as it grows. The main
`RuntimeSaveSnapshotDocumentStore` should stay an entrypoint for write/read
/validate operations; slot inspection/read-model work belongs in the inspection
partial, and unchecked file IO/failure helpers belong in the IO partial. This
prevents future save UI support from turning the store into another mixed
codec/IO/validation/restore-policy God helper.

### Partial world restore must fail closed

When a world payload slice only supports terrain, it must fail with structured
restore issues if item, creature, reservation, stockpile, placeable, or order
sections are present. Never let a partial loader silently drop authoritative
sections just because the payload schema does not yet cover them. Each new
section should be added with a hash round-trip test before it becomes loadable.

As the payload grows, keep the supported-section message precise. The current
world payload restore supports terrain, ground items, contained items with
payload-local acyclic item container references, carried/equipped items with
payload-local creature owners, installed items with valid placement anchors,
item-local reservation tokens with valid claimant/count rows, creatures, global
reservations, stockpile zones, owned placeables/workshop state, and active
orders. Present Jobs checkpoint sections still depend on Jobs-owned payload
restore paths and must remain rejected by full restore until those
corresponding authoritative sections exist; transport, mining, and craft are
now supported payload/restore slices.

Adding payload fields without binding them into the replay hash is also a
partial-load bug. If the restorer claims hash-checked restoration, every
gameplay-affecting field in the newly supported slice needs to be exported,
restored, and included in the section hash before the slice is considered
loadable.

Keep Simulation world payload mapping split by authoritative section. The
builder's main file should assemble sections in canonical order, while
metadata/terrain, entities, stockpiles, placeables, orders, and shared
conversion helpers stay in focused partials. The restorer's main file should
own the restore order and final hash comparison, while placeable
validation/restore, conversion/failure helpers, and payload validation stay
separate. Payload validation itself should not become a dumping ground: keep
entity/reservation checks, stockpile checks, order checks, and shared
world-geometry/string helpers in focused partials.
When adding a new world payload section, add the export/import/hash fields in
the section-owned partials and update the architecture smoke guard instead of
growing a new save/load God Object or moving mapping into Runtime/App.

Do not rely only on manager-level restore validation for externally supplied
world payloads. Managers should still keep atomic "validate then replace"
semantics for their owned state, but `WorldSavePayloadRestorer.Validation`
should reject cross-section and payload-shape problems before constructing or
mutating a restored world. Duplicate entity ids, stockpile zone/member
identities, order ids, and out-of-bounds coordinates belong at this Simulation
save boundary so Runtime/App can treat the world payload contract as one
auditable restore gate.

Some payload validation depends on reconstructed terrain. Keep those checks as
explicit preflight phases, not as late failures inside the restore pass.
Placeable owner storage, anchors, footprints, construction state, and workshop
queue payloads need terrain/chunk context, but they should be validated right
after terrain reconstruction and before item/creature/reservation/stockpile
manager restore work starts.

Simulation state managers are not a dumping ground for every behavior that
touches a subsystem. Keep `ItemManager` split by catalog access, position
indexing, read queries, stack/move/remove mutations, spawn behavior, and
save/restore mapping. Keep `CreatureManager` split by catalog access, runtime
instance queries, spawn behavior, and save/restore mapping. Keep
`OrdersManager` split by haul, mining, construction/buildable, and save/restore
mapping. Keep `PlaceableInstance` split between runtime state and item/
construction factory behavior, keep `PlaceableManager` split by collision,
placement, removal, and affected-chunk queries, and keep
`ChunkPlaceableData` authoritative storage separate from derived furniture sync
behavior. The ownership stays in Simulation because these managers own
authoritative runtime state, but the files should still show which
responsibility is being changed. Do not move these helpers into Runtime/App to
avoid touching Simulation, and do not merge them back into single manager files
just because all partials share the same private dictionaries/queues.

### Replay hashes need canonical primitive encoding

Replay checkpoint hashes should be built from explicit authoritative fields
using stable primitive encodings. Avoid object `GetHashCode()`, dictionary
enumeration order, diagnostic strings, or render/UI DTOs. `ReplayHashBuilder`
owns primitive encoding only; Simulation/Runtime snapshot hash builders still
need to own the field list.

Keep aggregate world replay hashing split by authority. `WorldReplayHashBuilder`
should orchestrate aggregate and section-hash entrypoints only; terrain,
item/creature entity state, reservations, stockpile-zone configuration, and
common primitive helpers belong in focused `WorldReplayHashBuilder.*.cs`
partials. This keeps save/replay field ownership reviewable and avoids turning
the replay checkpoint into another mixed Simulation God Object.

Do not leave these checks as one-off terminal commands. The formal smoke runner
now scans save/replay/hash authority paths for object `GetHashCode()`,
dictionary `Keys`/`Values` view iteration, and production `Guid.NewGuid()`.
If a future save/replay implementation needs an exception, document why the
ordering is still canonical before weakening the guard.

The same rule applies to mailbox/drain ordering and future save-slot replay
boundaries. If an ordering key needs a chunk, entity, system, or content key,
encode the explicit primitive fields into a stable sort key; do not pack
`GetHashCode()` into the persisted/replay order, even when the current runtime
only needs a small tie-breaker.

Typed mutation diff sort keys should use the centralized Simulation helper.
Enqueue-ordered command-edit diffs should use local sequence order, while
spatial/priority diffs should call the explicit chunk/cell/priority or
stockpile/cell/priority helpers. This keeps ordering policy visible and prevents
small bit-packing differences from becoming replay-only bugs.

Do not let compatibility entity ids leak back into typed stockpile item indexes.
The stockpile item-index pipeline now uses the wider `ulong` item entity key
from diff emitters through `StockpileDiff`, `StockpileMessage`,
`ItemStackRef`, `ChunkStockpileData`, and item projection lookup. Legacy `int`
stockpile handle overloads are temporary shims for old callers/tests only; new
stockpile placement/removal diffs should use `DiffTargetEncoding.EntityKey(...)`
and item projection should resolve by entity key rather than by the truncated
32-bit id.

When a diff producer has the source GUID, do not duplicate the transition logic
at every call site. Use GUID-aware target helpers so the legacy 32-bit
compatibility `EntityId` and the wider `EntityKey` are always filled together.
Hand-written `SignedEntityId(...)` plus `EntityKey(...)` pairs are easy to
partially apply during refactors.

Legacy entity-id fallback is still behavior-affecting for old diffs. Even
though new producers should emit `EntityKey`, the fallback must not enumerate
live dictionaries and accidentally choose a colliding entity by runtime
insertion/hash behavior. Keep fallback lookup behind manager-owned deterministic
indexes and prefer fail-closed or documented stable tie-breakers if the legacy
id cannot uniquely identify an entity.

Keep general diff application reviewable. `SimulationDiffApplicator` should stay
split into focused partials: the main entrypoint dispatches and logs,
terrain-owned behavior handles tile mutation, ejection, and dirty propagation,
item-owned behavior handles move/carry/uncarry plus stack merge, creature-owned
behavior handles creature movement, and target helpers own entity-key-first plus
guarded legacy lookup. If all of those concerns collapse into one file again,
entity lookup and terrain side effects become hard to audit together.

Broad read snapshots are part of deterministic behavior when they feed save,
replay hashes, jobs, or Runtime UI read models. Do not return raw dictionary
values from managers and expect every caller to remember an `OrderBy`.
Zone/stockpile owners now sort definitions, zones, shards, member chunks, and
stockpile item handles before exposing snapshots; keep future owner APIs
responsible for their own stable order.

World and placeable owner APIs are the same kind of boundary. `World.GetAllChunks()`,
dirty chunk drains, affected-chunk queries, and chunk-owned placeable snapshots
now sort at the owner by spatial/local-cell keys. Do not make every save,
Runtime snapshot, and job scanner rediscover the same ordering rule.

Construction material dictionaries look small, but requirement iteration affects
shortfall planning, item matching, consumption order, and diagnostic text.
`ConstructionSiteState` now exposes sorted requirement/delivery snapshots; new
construction, workshop, and Runtime readers should use those helpers instead of
enumerating raw material dictionaries.

Debug-looking shard snapshots can still become behavior-affecting when they
feed scheduling, replay diagnostics, or UI summaries that take the first N
entries. Transport request queue shard ids/counts now sort by shard id at the
queue boundary; keep shard owner APIs responsible for deterministic ordering
instead of relying on dictionary insertion order.

Transitional managers often start as "not important yet" and later become
behavior-affecting. `ChunkLifecycleManager` already owns heat/LOD transitions,
so heat-score decay now sorts chunk keys spatially before mutating scores. Apply
the same rule before adding unload queues, catch-up integration, or lifecycle
save/hash state.

Content bootstrap snapshots and selection lists are also ordering boundaries. If
a Content registry dictionary becomes a Runtime-applied list, sort by content id
while materializing the snapshot. If multiple content entries have the same
domain priority, add an explicit id tie-break. Runtime should not apply zones,
definitions, or tuning-derived rows in whatever order a dictionary or file
happened to enumerate them.

Contracts catalog stores are boundaries too. `GetAll*()` and tag/category index
arrays should be sorted when the store is built or read, not at every Runtime or
Jobs call site. Keep compatibility dictionary snapshots, such as material name
maps and RNG stream state maps, sorted by key even when a newer canonical row
snapshot exists.

Replay hash builders must be read-only. Do not use queue-drain APIs such as
order designation drains while hashing; use stable snapshot/read APIs instead.
Hashing should never advance simulation queues, clear pending work, mutate cache
generations, or change future tick behavior.

Private counters can still be authoritative replay state. If a future action
derives stable ids from a private sequence, RNG cursor, or generation counter,
the hash/save snapshot must include that counter even when the current visible
collection can be reconstructed. Workshop queue entry ids depend on the
workshop queue sequence, so the placeable replay hash covers the counter in
addition to the current queue entries.

If a private counter only exists to schedule periodic work, prefer deriving the
schedule from the authoritative tick instead of persisting another replay field.
`SanitizeSystem` now uses `(tick + 1) % interval` rather than an internal
counter so the low-frequency safety net resumes deterministically after
restore/replay without adding hidden scheduler state.

Compatibility save helpers that return dictionaries are not canonical replay
streams. Use sorted snapshot rows for replay/save hashing. `RngStreamManager`
still has dictionary save/restore helpers for compatibility, but its
materialized streams are now owner-lock state and deterministic checkpointing
should use `GetStateSnapshot()` plus `RngReplayHashBuilder` so stream ordering
cannot depend on dictionary enumeration.

Concurrent collections are not a determinism boundary. `ConcurrentDictionary`
and `ConcurrentQueue` protect some low-level operations, but they do not define
save/replay/hash order and their mutation callbacks can hide shared-state
updates. Orders, Jobs backlogs, the command queue, EventBus handlers,
navigation path caches/nav-data registries, world chunk storage, and RNG stream
registries now use owner-owned queues/lists/dictionaries plus explicit
sorting/sequence fields where order matters. Future concurrent work should add
a declared snapshot/merge contract rather than reintroducing concurrent
collections into authority paths.

The same rule applies to atomic primitives in authority paths. Runtime command
identity sequence advancement and transport queue counters are now owner-state
updates behind their session/queue guards. `Interlocked` is still appropriate
for App-side async diagnostics, but it should not be used to obscure who owns a
simulation-visible sequence or replay-adjacent counter.

Derived cross-indexes should not become save authority just because they live
beside authoritative data. Placeable hashes cover owned placeable instances and
their construction/workshop/door state, but exclude chunk furniture cells and
cross-chunk external refs because those are rebuildable from owned placeables
and footprints.

Long-horizon job replay snapshots should come from job-owned state, not UI
debug DTOs. Transport now exposes pending request, active job, backlog, and
scheduling hint snapshots for replay hashing; mining exposes active job,
backlog, deferred stairwell, reserved tile, and recent-completion snapshots.
Craft exposes active job and backlog snapshots, while construction-site progress
remains placeable authority because the current construction executor is
scan-based. Keep shard indexes, planner outboxes, and debug/stat rows out of
the authoritative hash unless they start affecting behavior across tick
barriers directly.

Transport checkpointing has two authoritative owners: the pending request queue
and the executor. A Runtime aggregate checkpoint must feed both snapshots into
`TransportReplayHashBuilder`; executor-only hashing silently loses pending
transport work.

Runtime snapshot metadata should be authored by Runtime, not App. UI tick
counters drive presentation effects and toasts; they are not simulation ticks
and should not be passed into Runtime read-model builders as checkpoint or
debug authority.

Runtime presenter-frame identity should also be authored by Runtime, not App.
Frame/overlay aggregate DTOs currently still transfer full snapshots, but their
publication sequence, full-snapshot payload hash, transfer mode, and optional
same-request delta-base hash come from `RuntimeFrameSnapshotPublisher`. App
renderers may use those fields for presentation caching, but they must not
compute their own frame hashes from live world state or treat presenter payload
hashes as authoritative replay/save hashes.

The same rule applies to presenter deltas. The current map-viewport delta is
Runtime-authored from final screen-cell values after terrain/entity overlay
collapse and includes changed-cell, changed-row, and changed screen-region DTOs
with per-row/region payload hashes. The current UI-overlay delta is
Runtime-authored from named section payload hashes. Their payload/base hashes,
changed cell/row/region/section lists, and `CanApplyToBase` flags are
presentation cache metadata only; replay/save hashes still belong to
Simulation/Runtime authoritative hash builders.

Pending commands are save authority too. `CommandQueue.GetExecutedCommandRecords()`
is not enough for a barrier save because future-tick commands may still be in
the pending queue. Use `CommandQueue.GetReplaySnapshot()` when a checkpoint or
save package needs pending and executed command records to match one manifest.

Runtime save manifests should name sections, not expose live owners. World
section hashes/counts come from Simulation-owned save/replay builders, command
sections come from Core replay records, content signatures come from Content
snapshots, and Runtime assembles the manifest/package. App should not collect
`World`, job-system, or `CommandQueue` internals to build save files.

Do not make App decode command replay rows. App can own slot selection and file
IO, but the document row to `CommandReplayRecord` mapping belongs in Runtime
because Runtime owns command payload schemas and replay restore. Public save
documents should stay in Contracts DTOs; Core command records should remain an
internal Runtime/load concern at the App boundary.

Do not make App restore RNG streams either. The save document may expose
primitive RNG stream rows as Contracts DTOs, but mapping them back to
`RngStreamStateSnapshot`, checking the canonical RNG hash/count, clearing stale
materialized streams, and restoring stream state belongs in Runtime/Core. App
should only receive a structured result from a Runtime save-slot/restore port.

Do not expose save/replay checkpoint ports through the ordinary App runtime
session aggregate. `FortressRuntimeSessionFactory.Create(...)` returns the
App-facing session port set, which intentionally excludes save/replay methods.
The full aggregate is internal/friend-only for Runtime tests until a dedicated
save UI boundary exists.

Full staged restore has an intentional Runtime order: validate the document,
restore the supported world payload through the Runtime session factory, restore
RNG streams after the session factory resets services, then restore pending
commands. Reversing this order silently loses RNG or pending-command state
because new session composition clears per-session services.

Keep first-pass save document IO clearly scoped. A helper that writes the
assembled Runtime save document is useful, but it is not the full slot format:
do not treat it as chunk-sharded world persistence, autosave policy, migration,
or complete load orchestration.

## App/UI Boundary Pitfalls

### Internal implementations still need explicit interface entrypoints

When hardening implementation classes inside Jobs, Navigation, or Runtime, changing a class member from `public` to `internal` is not enough if the class implements an interface. C# requires either a public implicit implementation or an explicit interface implementation.

Preferred pattern for implementation assemblies:

```text
internal method/property
  -> used by same assembly, Runtime, and friend tests where useful

explicit interface implementation
  -> used by cross-module contracts and command/runtime seams
```

Do this especially for `ITick`, navigation contracts, job diff emitters, loggers, profession adapters, command targets, and snapshot/session facades. If existing friend tests instantiate a concrete implementation, keep an internal direct method and delegate the explicit interface method to it instead of making tests cast to the interface everywhere.

### Project references are not the full boundary

A clean `.csproj` graph is necessary but not sufficient. Source imports and
friend assemblies can drift even when project references look correct. Keep the
architecture smoke runner's source-import matrix and `InternalsVisibleTo`
matrix in sync with intentional module ownership changes.

Default rule:

```text
Contracts imports only Contracts namespaces and has no package, framework, or project references.
Core/Content/Navigation import Contracts plus their own implementation namespace.
Simulation imports Contracts/Core/Simulation.
Jobs and WorldGen import Contracts/Core/Simulation plus their own namespace.
Runtime may import implementation projects but never App.
App imports App/Contracts/Runtime only, with direct Runtime imports limited to
startup, adapter, world-generation provider, and App content-file-location
boundaries.
```

If a future refactor needs to weaken one of those edges, update the test and the
architecture docs in the same change so the exception is explicit.

### Namespace cleanup is not project renaming

When moving concrete implementation namespaces to explicit `.Implementation`
namespaces, keep project and assembly names stable unless the batch is actually
renaming projects. Mechanical rewrites can accidentally turn
`HumanFortress.WorldGen.csproj` into a fake implementation project name or
weaken guard strings that are supposed to catch old root namespaces.

For these compatibility cleanup batches, update source namespaces/usings and
then explicitly re-check:

```text
ProjectReference strings
architecture smoke-test allowlists
forbidden root-namespace guard strings
friend assembly names
```

Navigation and WorldGen now use explicit concrete implementation namespaces:

```text
HumanFortress.Navigation.Implementation
HumanFortress.WorldGen.Implementation
```

Their stable cross-module DTOs/interfaces still live in Contracts, and Runtime
should remain the production composition boundary for concrete implementation
creation.

For implementation projects that already have strong internal module folders,
avoid treating the project root namespace as a convenient catch-all. Jobs now
uses focused directory namespaces for configuration, diff emitters, logging,
orchestration, profession bridges, safety, mining, construction, craft,
transport, and replay helpers. Runtime wrappers should import those focused
role namespaces explicitly; broad `using HumanFortress.Jobs;` hides whether a
file is depending on scheduler tunings, profession bridges, orchestration
contracts, or concrete diff emitters.

### Do not pass loaded-session snapshots through input controllers

`FortressLoadedSessionSnapshot` is frame-render presentation state, not a general input dependency bag. Passing that whole snapshot through mouse, keyboard, overlay-click, placement, or map-click controllers makes UI code look like it is allowed to grow new dependencies on render/session state.

Prefer explicit dependencies:

```text
UiServices?
NavigationOverlay?
HasFortressMap
FortressRuntimeAccess snapshot/query methods
```

The frame renderer is currently the only App path that should need the loaded-session snapshot. New UI/debug panels should ask Runtime for DTOs instead of adding another `LoadedSession` parameter.

### App map renderers should draw viewport DTOs only

Main map terrain/entity display now enters App through:

```text
Runtime snapshot builder
  -> SimulationMapViewportData / MapViewportCellView

FortressMapRenderer
  -> clears the SadConsole surface
  -> draws DTO glyph/color cells
  -> draws the App-owned navigation overlay from Runtime navigation DTOs
```

Do not reintroduce App-side reads of `World`, `FortressMap`, chunks, tiles, geology catalogs, item managers, creature managers, or terrain kinds inside `FortressMapRenderer` or frame/overlay render helpers. If map rendering needs another visible fact, add it to the Runtime map viewport DTO or a focused Runtime overlay DTO.

Frame-level render data should stay aggregated:

```text
SimulationFrameRenderData
  -> SimulationMapViewportData
  -> SimulationNavigationOverlayData
  -> SimulationTileInspectionData
```

Avoid splitting `FortressFrameRenderer` back into separate Runtime calls for map viewport, navigation overlay, and tile inspection. Add fields to the frame DTO when the frame needs more read-side simulation data.

Overlay-level render data should also stay aggregated:

```text
SimulationUiOverlayFrameData
  -> build catalog
  -> jobs/workshops
  -> zone/stockpile overlay/detail
  -> management drawer / Work drawer / Debug menu data
```

Avoid adding new per-panel Runtime calls directly in `FortressUiOverlayRenderer`; add fields to the overlay-frame DTO unless the query depends on immediate drag state, command input, or App diagnostics.

### Runtime public ports should not expose presentation primitives

App can keep using SadRogue `Point`/`Rectangle` inside App-owned input, rendering, and role-access interfaces because those files are presentation glue. The cross-project Runtime session ports should instead use Contracts-owned DTOs/primitives such as `RuntimePoint`, `RuntimeRect`, and focused notification records.

Current shape:

```text
App input/rendering
  -> SadRogue Point/Rectangle

FortressRuntimeAccess
  -> maps to Contracts.Runtime primitives

Runtime session ports
  -> Contracts DTOs/primitives only

Runtime core
  -> maps back to internal SadRogue/world geometry where current implementation requires it
```

Do not "fix" a signature mismatch by importing `SadRogue.Primitives` into a public Runtime port. Add or extend a Contracts primitive/DTO, then keep the third-party geometry conversion at the App.Runtime or Runtime-internal edge.

The architecture smoke runner scans Contracts source and Runtime public
session port/factory files for SadRogue, SadConsole, MonoGame, and XNA type
names. If a public Runtime call needs geometry, color, or presentation metadata,
add a project-owned DTO in Contracts and map it at the App/Runtime adapter
boundary.

### Command targets and post-tick applicators must share the same diff log

When moving a direct mutation into an authoritative diff pipeline, wire one diff log instance through the command execution context and the post-tick pipeline. Creating one log for the command target and a second log for the applicator silently drops queued mutations.

Use the Runtime-owned `RuntimeMutationDiffLogs` bundle for command-side typed mutation logs. The active session should own that bundle through `RuntimeSessionServices`, alongside the scheduler, command queue, event bus, main diff log, and item diff log. Do not re-expand `SimulationRuntimeHost`, `SimulationRuntimeHostCore`, `SimulationCommandExecutionContext`, or `SimulationTickPipeline` into long parameter lists of individual mutation logs; that shape makes it too easy to accidentally give a command target and an applicator different instances.

Do not create a new `RuntimeMutationDiffLogs` bundle inside Runtime host composition when a session service bundle already exists. The host, command execution context, and post-tick pipeline must receive the same bundle, and `RuntimeSessionServices.ResetForNewSession()` must clear all typed command mutation logs together.

Current stockpile create/delete shape:

```text
CreateStockpileCommand
  -> IStockpileCommandTarget
  -> StockpileDiffLog.AddCreateZone(...)

DeleteStockpileCommand
  -> IStockpileCommandTarget
  -> StockpileDiffLog.AddDeleteZone(zoneId)

SimulationTickPipeline.PostTick
  -> StockpileDiffApplicator.ApplyAll(world, stockpileDiffs)
  -> StockpileDiffLog.Clear()
```

The command target can do preflight validation for user feedback and empty-command rejection, but the applicator must recheck authoritative conditions such as overlap because multiple commands in the same tick can pass preflight against the same old world state.

Applicators must also recheck any condition that earlier post-tick diffs can change. Stockpile creation now rechecks tile terrain after terrain diffs have applied; command preflight may have seen a floor that became a wall in the same tick.

Stockpile deletion follows the same rule: the command diff should carry the zone id, while `StockpileDiffApplicator` reads the current authoritative zone/member chunks at apply time. Do not capture a member-chunk snapshot in the command target; same-tick zone changes should be resolved by the applicator against the current world state.

The active stockpile applicator entry is world-level only:

```text
SimulationTickPipeline
  -> StockpileDiffLog.MergeAndSort()
  -> StockpileDiffApplicator.ApplyAll(world, stockpileDiffs)
```

Do not recreate a chunk-local `ApplyDiffs(Chunk, List<StockpileDiff>)` entry for create/delete. That old shape made it too easy to apply zone membership from stale chunk-local assumptions instead of resolving against the current authoritative world.

Typed command diff ordering is not one-size-fits-all. Order, zone, and workshop command-edit diffs intentionally preserve enqueue sequence because edits are order-sensitive. Item and stockpile diffs still use spatial/priority ordering. If this changes, update the explicit smoke coverage instead of silently normalizing every `GetSortKey()` implementation to the same policy.

SadRogue `Rectangle.MaxExtentX/MaxExtentY` is treated as inclusive in the simulation codebase. Rectangular world scans such as order/designation/item queries should iterate `<= MaxExtent*`, not `< MaxExtent*`; otherwise one-cell-high or one-cell-wide designations silently scan no cells.

Mining has the same inclusive-rectangle trap as hauling/item scans. Keep `MiningOrderRules` and `MiningSystem` cursor advancement paired: scan loops use `<= MaxExtent*`, and cursor rollover checks use `> MaxExtent*`. Add one-cell rectangle coverage when touching any designation scanner.

Keep the mining planner split by planner responsibility. The main
`MiningSystem` file should stay a shell for state, constructor, scheduler
identity, and the `PlannedDig` DTO. Tick/drain/outbox behavior, scanner/cursor
advancement, cancellation queries, and helper/log/seed/standability behavior
belong in focused partials, with `ActiveDesignation` kept as a separate cursor
state type. This makes rectangle-scan changes easier to review without mixing
Jobs executor behavior back into Simulation planner state.

Runtime command ids must not use `Guid.NewGuid()` on replay-facing command paths. Command payloads should serialize meaningful mutation inputs, and Runtime session enqueue should add deterministic sequence identity when duplicate payloads can appear in the same tick. Execution order still belongs to `CommandQueue`; command ids are for stable correlation/debugging.

Command payload serialization is part of command identity. Every field that can change execution must be written to `Serialize()`, including optional filters and tag lists. When a field is semantically a set, serialize it in deterministic order so equivalent UI selection order does not create a different id; when the field is semantically ordered, preserve the order.

`CommandQueue.RestoreCommands(...)` is replay restore, not executed-history restore. Restored commands should be queued with deterministic sequence order and should not appear in `GetExecutedCommands()` until `ExecuteCommands(...)` actually runs them. Otherwise save/replay history gets duplicate entries and future commands look executed before their tick. Validate restored command batches before clearing the existing queue, so a corrupt restore payload cannot wipe pending commands and then fail halfway through.

Do not persist live `ICommand` instances. They are executable objects with runtime-only behavior and dependencies. Persist `CommandReplayRecord` data from `GetExecutedCommandRecords()` instead, then deserialize records through a command factory/registry when the real save/replay loader exists.

Keep replay decoding ownership aligned with command ownership. `ICommandReplayFactory` belongs in Core as a narrow contract, but concrete command type lookup and payload decoding belong in Runtime because Runtime owns command implementations, command type strings, and content-aware command construction. Do not make Core reference Runtime commands to "finish" replay deserialization.

Do not let the Runtime replay factory become a new command God Object. The
main factory should own command-type dispatch, payload version validation, and
shared primitive readers only. Put concrete decoders in focused partials by
command family, such as orders, zones/stockpiles, profession/workshop, and
debug spawn. New command families should add a matching decoder partial plus
architecture smoke coverage instead of expanding the main dispatch file with
domain parsing details.

The same rule now applies to zone commands:

```text
CreateZoneCommand / UpdateZoneCellsCommand / DeleteZoneCommand
  -> IZoneCommandTarget
  -> ZoneDiffLog

SimulationTickPipeline.PostTick
  -> ZoneDiffApplicator.ApplyAll(world, zoneDiffs)
  -> ZoneDiffLog.Clear()
```

Do not let `ZoneCommandTarget` regain a concrete `World` field just to call `world.Zones.*` from command execution. If zone behavior needs richer validation or ordering, add it to `ZoneDiffLog`/`ZoneDiffApplicator` so it remains an authoritative post-tick mutation.

And to workshop queue/settings commands:

```text
UpdateWorkshopQueueCommand
  -> IWorkshopQueueCommandTarget
  -> WorkshopDiffLog

SimulationTickPipeline.PostTick
  -> WorkshopDiffApplicator.ApplyAll(world, workshopDiffs, constructions)
  -> WorkshopDiffLog.Clear()
```

Workshop diffs must preserve command enqueue order because queue edits are order-sensitive. Keep `WorkshopDiff.GetSortKey()` sequence-based unless there is a deliberate operation-level merge policy.

And to order commands:

```text
CreateMiningOrderCommand / CreateHaulOrderCommand / CreateConstructionOrderCommand / ...
  -> IOrderCommandTarget
  -> OrderDiffLog

SimulationTickPipeline.PostTick
  -> OrderDiffApplicator.ApplyAll(world, orderDiffs)
  -> OrderDiffLog.Clear()
```

Do not let `OrderCommandTarget` regain a concrete `World` field just to call `world.Orders.Enqueue*` during command execution. Order diffs intentionally preserve enqueue order so multi-command placement batches remain deterministic.

Profession assignment commands follow the same post-tick rule even though they are Runtime-owned rather than Simulation-world-owned:

```text
SetProfessionWeightCommand
  -> IProfessionAssignmentCommandTarget
  -> ProfessionAssignmentDiffLog

SimulationTickPipeline.PostTick
  -> ProfessionAssignmentDiffLog.ApplyAll()
```

The profession handler binding still happens after Runtime systems are registered, but command execution must queue a diff instead of invoking the handler directly.

### Runtime snapshot builders should not become new god objects

Snapshot builders are allowed to know about live Runtime/Simulation internals, but each builder should own one read-model family or one mapping policy. Do not put navigation overlay modes, map terrain glyph rules, entity glyph rules, workshop queue summaries, construction-material progress, and aggregate frame composition into one ever-growing file just because they all return DTOs.

Current split pattern:

```text
FortressRuntimeSnapshotBuilder
  -> base/debug/catalog entrypoints
  -> frame/overlay aggregate composition
  -> map/navigation/inspection/placement queries
  -> Work/jobs/workforce/orders/workshop read models

MapViewportSnapshotBuilder
  -> viewport orchestration
  -> terrain glyph policy
  -> visible creature/item glyph policy

NavigationOverlaySnapshotBuilder
  -> basic mode overlays
  -> structural/flow/ramp mode overlays
  -> path-cell mapping
  -> grid/nav-data helpers

WorkshopSnapshotBuilder
  -> workshop scanning
  -> summary/queue mapping
  -> construction-material progress

ManagementDrawerSnapshotBuilder
  -> creatures/items/zones

StockpileSnapshotBuilder
  -> overlay/detail/hit-test/geometry

JobsDebugSnapshotBuilder
  -> active jobs/transport debug/stats

FortressRuntimeSessionSnapshotFacade
  -> frame/map/work/session access queries
```

When a snapshot needs new data, add it near the read-model family it belongs to. If the mapping starts to look like reusable domain policy rather than presentation/read-model policy, move that policy closer to Runtime/Simulation/Content instead of hiding it in a snapshot helper.

Viewport-dependent frame and overlay aggregates should not be treated as a
single global world snapshot. Keep the request key explicit and let Runtime own
the publication/cache point. `FortressRuntimeSessionCore.Snapshots.Frame` should
construct request DTOs and delegate to `RuntimeFrameSnapshotPublisher`; it should
not directly author snapshot metadata or call aggregate facade builders. The
publisher should also attach a stable publication surface/request hash generated
with canonical primitive encoding, so same-tick different viewport/UI requests
cannot masquerade as the same published frame. This keeps the current
request-shaped frame DTOs honest while leaving room for the future
immutable/diff frame boundary.

Keep `RuntimeFrameSnapshotPublisher` as an entrypoint rather than a new frame
God Object. Main-file responsibilities are frame publication entrypoints only.
Request cache reads/writes, invalidation, and small published-frame identity
records belong in the publisher state partial. Presenter payload identity, UI
overlay section deltas, map viewport changed-cell/row/screen-region deltas, and request hashing
belong in focused publisher partials. Future packed world-chunk/panel delta slices should
follow that split instead of growing the main publisher again.

Do not use a tick-only frame cache while the background scheduler is running.
`TickScheduler.CurrentTick` advances after post-tick, so a render read earlier
in the same tick and another read after write-phase mutation can observe
different live state under the same tick number. Until immutable world-frame
publication exists, cache reuse must be limited to stopped/paused/manual-tick
reads where the live authority is not concurrently changing.

### Work drawer panels should consume one aggregate read model

The Work drawer needs jobs, workforce, order summaries, and workshop summaries in the same panel. Avoid letting each helper call a different runtime facade method from inside App UI renderers.

Current shape:

```text
Runtime snapshot builder
  -> SimulationWorkDrawerData

FortressUiOverlayRenderer
  -> fetches the aggregate when the Work drawer is open

UiWorkDrawerRenderer
  -> renders Work tabs from that DTO
```

Input paths that need a narrower read model, such as profession-weight clicks, should use a clearly named input DTO/provider instead of reaching for live systems or forcing the full drawer aggregate.

### Split App presentation by surface, not by domain ownership

SadConsole rendering helpers that take `ScreenSurface`, `ICellSurface`, `UiStore`, mouse/camera state, or UI service objects are App presentation code even when they draw simulation-derived facts. Do not move those helpers into Runtime, Jobs, Simulation, or Content just because they are large.

Current shape:

```text
Runtime snapshot DTOs
  -> App.Rendering frame/overlay coordinators
  -> App.Rendering/App.UI SadConsole glyph/panel renderers
```

Good split targets are presentation surfaces:

```text
FortressMapOverlayGlyphRenderer
FortressPlacementOverlayRenderer
UiChromeRenderer
UiManagementDrawerRenderer
UiDebugMenuRenderer
UiQuickMenuRenderer
UiWorkDrawerRenderer
UiWorkshopPanelRenderer
FortressDebugUnitOverlayRenderer
```

If a renderer needs more simulation information, add the data to a Runtime snapshot/query DTO first. Keep the App class focused on glyphs, layout, transient UI state, and command-preview visuals.

Avoid moving SadConsole renderers to Runtime, Jobs, Simulation, Content, or Contracts only because a file is large. Module ownership follows the dependency boundary: surface drawing that touches `ScreenSurface`, `ICellSurface`, `UiStore`, or App input state stays in App; simulation facts cross the boundary as Runtime DTOs.

For chrome controls, keep labels, keyboard shortcuts, and slot-to-drawer/menu mappings in `UiChromeSlots`, with geometry in `ButtonLayoutCalculator`. Do not reintroduce separate F-key or Z/X/C/V lookup arrays in renderers and input handlers.

When App UI files are still large after moving live simulation reads behind snapshots, split them by presentation surface or state domain inside App first. Partial files for `UiStore`, `UiManagementDrawerRenderer`, `UiWorkDrawerRenderer`, and `UiDebugMenuRenderer` are acceptable because they keep SadConsole/App dependencies local without pretending those UI concerns belong in Runtime, Simulation, Jobs, Content, or Contracts.

Input dispatchers should split by event channel or feature panel, not by lower-layer domain. Keep SadConsole component input, screen chrome hit testing, root quick-menu hit testing, submenu hit testing, Debug overlay click handling, Work allocation keyboard/mouse handling, and placement/menu hit testing in App input/UI partials. Do not move them to Runtime simply because they trigger Runtime commands; the command/query boundary is the facade call, not the mouse/keyboard routing code.

As App input files shrink, keep the same ownership rule: keyboard/mouse context records, root quick-menu hit testing, main-menu/world-map input, and SadConsole overlay pass-through logic remain App concerns. Split them by event channel or feature menu inside `HumanFortress.App.Input` / `HumanFortress.App.States`; do not move them to Contracts/Runtime just because they produce semantic Runtime requests.

When splitting SadConsole presentation or input partials, carry the exact extension-method imports with the moved code. Files that call `Print`, `SetGlyph`, `Keyboard`, `ScreenSurface`, or `Color` usually need `SadConsole`, `SadConsole.Input`, and/or `SadRogue.Primitives` locally. Do not rely on a sibling partial file's imports; C# using directives are file-scoped.

Runtime command targets should also split by operation role instead of accumulating helper logic in one target class. Keep command entrypoints, world-cell eligibility/collection, lookup/bootstrap of mutable runtime state, and display/name compatibility helpers in separate Runtime partial files. Do not move those helpers into App, and do not hide reusable domain policy inside App-side command factories.

Build, placement, and chrome presentation should follow the same App-local split rule. `BuildUI`, `FortressBuildKeyboardInput`, `NavigationOverlay`, `FortressPlacementOverlayRenderer`, `FortressPlacementController`, `UiChromeRenderer`, and UI command objects are App presentation/input orchestration. Split them by UI surface, event family, or command family; do not move SadConsole drawing, keyboard selection, or mouse placement routing into Runtime/Jobs/Simulation just because the final action queues a Runtime command.

Runtime composition files should split by system group, not by App caller. Planning systems and tick-facing job wrappers are both Runtime composition, but they should not live in one mixed "system groups" file once each group has enough constructor policy. Keep concrete factory wiring in Runtime and keep App limited to session/bootstrap adapters and callback injection.

### Contracts should define shapes, not runtime policy

Moving a request type to Contracts must not pull runtime defaults, content category mapping, or Simulation conversion policy with it. Contracts can own enums/records that express cross-boundary intent; Runtime should own how those intents become commands, material filters, content category keys, and Simulation DTOs.

Good shape:

```text
App UI intent
  -> Contracts.Runtime enum/record/request parameters
  -> Runtime command factory applies defaults/mapping/conversion
  -> Simulation command payload
```

If App needs to pass material preferences or UI tool options, pass the raw semantic values through the runtime facade and build the concrete filter inside Runtime. Do not put default material ids, category keys, or Simulation-facing conversion helpers in Contracts just because both App and Runtime can reference them.

### Generated-world UI should use Contracts DTOs through Session queries

World generation screens should use the contract `IWorldGenerationService` created through Runtime's `FortressRuntimeWorldGenerationFactory`. App screen/session code should not directly construct or store concrete WorldGen service/data/factory types, and App should not reference the `HumanFortress.WorldGen` project.

Later App screens should read through `FortressSessionContext.TryGetWorldSize(...)`, `TryGetWorldTileView(...)`, or bootstrap-only `WorldTileSnapshot` queries instead of reading `WorldGenResult.Tiles`, raw `WorldTile`, `BiomeType`, or `WorldParams`.

Keep stable generated-world shapes in `HumanFortress.Contracts.WorldGen` (`WorldGenerationSettings`, `WorldMapTileView`, `WorldTileSnapshot`). Keep SadConsole glyph/color policy in App.Rendering because it depends on presentation choices. Keep fortress-session selection, embark configuration, and Runtime request mapping in App.Session because those are App flow concerns.

### Runtime access facades should narrow by caller role

Do not pass the full concrete `FortressRuntimeAccess` into every App helper just because it is convenient. Rendering should use a read-only interface, play/input controllers should use a play-time query/command interface, and session initialization should be the only path that can see startup-only operations such as fortress-map generation/fill or auto-dig bootstrap.

Current split:

```text
IFortressRuntimeReadAccess
  -> wrapped by App.Rendering FortressViewRuntimePorts
IFortressRuntimeUiInputAccess
  -> wrapped by App.Rendering FortressViewRuntimePorts for UI callbacks
IFortressRuntimeBuildCatalogAccess
  -> wrapped by App.Input keyboard ports
IFortressRuntimePlacementQueryAccess
IFortressRuntimePlacementCommandAccess
  -> wrapped by App.Input FortressPlacementRuntimePorts
IFortressRuntimeMapInspectionAccess
  -> wrapped by App.Input map inspection ports
IFortressRuntimeDebugSpawnQueryAccess
IFortressRuntimeDebugSpawnCommandAccess
  -> wrapped by App.Input debug-spawn ports
IFortressRuntimeWorkshopPanelQueryAccess
IFortressRuntimeWorkshopPanelCommandAccess
  -> wrapped by App.Input workshop-panel ports
IFortressRuntimeNavigationDebugAccess
  -> wrapped by App.Input navigation-debug ports
IFortressRuntimeSimulationControlAccess
  -> wrapped by App.Input simulation-control ports
IFortressRuntimeBootstrapAccess
  -> wrapped by App.Session FortressSessionRuntimePorts
FortressStateRuntimePorts
  -> state-level bundle of module-owned view/input/session port groups only
```

For fortress play composition, use a named ports object such as `FortressStateRuntimePorts` at the state boundary, then pass module-owned port groups into input/render/session helpers. When a port constructor needs several runtime roles, use a named dependency object such as `FortressInputRuntimePortDependencies`; avoid long positional runs of the same `runtime` adapter. Do not recreate a broad keyboard/runtime facade that inherits unrelated workshop, navigation-debug, simulation-control, and build-catalog capabilities just because the keyboard router touches all four.

Likewise, avoid putting active runtime session ownership back into `GameStateManager`. Keep `SimulationRuntimeSession`, live `World`, content snapshot handling, navigation rebuild, fortress generation/fill, and startup auto-dig policy in Runtime/session-controller boundaries. App.Runtime may adapt logger/UI callbacks and forward request DTOs, but it should not grow new gameplay/domain logic.

Runtime snapshot builder/facade helpers are implementation details. Public boundaries should be snapshot DTOs plus Runtime session port interfaces/App.Runtime facade query methods, not direct calls from App into `FortressRuntimeSnapshotBuilder`, `FortressRuntimeSessionSnapshotFacade`, or the internal `FortressRuntimeSessionCore`.

Internal Runtime implementation classes should not keep ordinary `public` members just because they implement a public or friend-visible interface. Prefer explicit interface implementations for concrete commands, command targets, job-system wrappers, navigation-source adapters, command contexts, and small catalog adapters. Keep Runtime helper/factory/builder methods `internal` unless App or another project is meant to call that exact helper as a supported API. This keeps source scans aligned with the intended boundary and prevents internal implementation types from looking like stable extension points.

Keep App project references aligned with source ownership. If App source no
longer uses a module namespace directly, remove the direct ProjectReference
instead of keeping it as a convenience bridge. App currently should not directly
reference Jobs, Simulation, or Navigation; it should reach those systems through
Runtime/WorldGen DTOs, queries, or commands.

Command translation is a Runtime boundary, not an App-to-Simulation shortcut.
App may map App UI enums/options to stable Runtime request DTOs, but it should
not construct Runtime command objects, call `Runtime.Commands` factories, or
pass `Func<ulong, ICommand>` delegates. Semantic queue methods such as
`QueueHaulOrder(...)`, `QueueCreatureSpawn(...)`, and
`QueueAddWorkshopRecipe(...)` should cross the App facade; concrete command
construction belongs in Runtime session port implementations/Runtime command code.

Runtime concrete command classes, command factories, command target interfaces,
and `SimulationRuntimeCommandTargets` are implementation details. Keep them
internal and let tests use the Runtime friend bridge when they need direct
command-stage coverage. Do not make them public again just to simplify App
input code; add a semantic Runtime session port method or request DTO
instead.

Commands should also depend on the narrowest runtime target context they need.
Use `IRuntimeOrderCommandTargetContext`, `IRuntimeZoneCommandTargetContext`,
`IRuntimeWorkshopCommandTargetContext`, or the matching spawn/profession/
stockpile role instead of reintroducing a single all-target command context.
The tick pipeline should pass only `ISimulationContext` plus the clock role into
the command stage; individual commands should use `RuntimeCommandContext.Require<T>()`
for their specific role so a missing runtime role fails visibly instead of
silently no-oping.

Runtime session construction options and `FortressRuntimeSessionCore` are Runtime-internal helpers. App should create sessions through `FortressRuntimeSessionFactory`, keep App-specific logger/content callbacks at the `GameStateRuntimeCoordinator` composition boundary, store only `IFortressRuntimeSessionPorts`, then hand the rest of App only narrow `FortressRuntimeAccess` role interfaces.

### Workshop input should read snapshot DTOs and write commands

The workshop panel needs queue entry ids and current worker-slot state to enqueue `UpdateWorkshopQueueCommand`, but App input code should not resolve `WorkshopState` by scanning live placeables.

Current shape:

```text
Runtime snapshot builder
  -> WorkshopSummaryView / WorkshopQueueEntryView.EntryId

FortressWorkshopPanelKeyboardInput
  -> reads DTO state through FortressRuntimeAccess.GetWorkshopPanelData(...)
  -> enqueues UpdateWorkshopQueueCommand
```

Do not recreate an App-side `World.GetAllChunks()` / `PlaceableInstance.Workshop` resolver for panel input. If more mutable workshop operations are added, expose the read side as DTO fields and keep writes command-driven.

## Content Boundary Pitfalls

### Prefer one Content load result over split loaders

`HumanFortress.Content` now coordinates runtime content bootstrap plus data/core catalog loading. Runtime should consume the Content-owned entry points instead of independently calling:

```text
ItemDefinitionCatalogLoader.Load(...)
CreatureDefinitionCatalogLoader.Load(...)
CoreDataRegistryLoader.Load(...)
RuntimeContentRegistryLoader.Load(...)
```

Current first-pass shape:

```text
HumanFortress.Content
  resolves published/source content paths
  resolves registry files under content/registries
  loads legacy + structured runtime registries through FortressContentLoader / RuntimeContentRegistryLoader
  loads item/creature definitions
  loads construction/recipe core data
  returns immutable catalog snapshots and diagnostics

Runtime composition
  applies snapshots to world managers
  captures runtime catalog/tuning dependencies through FortressRuntimeContentSnapshotLoader
  exposes construction/recipe/material/terrain/geology/zone facts through FortressRuntimeContentSnapshot contract properties

App
  consumes Contracts-owned content load reports and file-resolution DTOs through Runtime/App wrappers
```

Do not introduce new App-local JSON traversal or a second content bootstrapper while this is being consolidated.

Single-purpose Content loaders and parsing helpers are implementation details. Cross-App public entry points should stay centered on Runtime facades and Contracts reports:

```text
Runtime.FortressRuntimeContentLoader
Contracts.Content.Loading.FortressContentLoadReport
```

`FortressContentLoader`, `CoreContentCatalogLoader`, `FortressRuntimeContentSnapshotLoader`,
`ProfessionRegistryLoader`, item/creature catalog result types, `RuntimeContentRegistryLoader`,
and material/registry parser helpers are internal/friend surfaces for Content, Runtime,
and tests. Contracts-owned `FortressContentLoadReport` should expose issues and summary
counts rather than full mutable or runtime catalog objects.

Concrete Content registry helpers are also implementation details. External code should depend on Contracts catalog interfaces surfaced by Content loader/snapshot facades, such as `IRuntimeMaterialCatalog`, `IRuntimeTerrainKindCatalog`, `IRuntimeGeologyCatalog`, `IConstructionCatalog`, `IRecipeCatalog`, and `IProfessionRegistry`, not on `ContentRegistry`, `MaterialRegistry`, `TerrainKindRegistry`, `GeologyRegistry`, `BiomeTemplateRegistry`, `AliasResolver`, or the concrete profession registry implementation.

### Resolve App registry files through Runtime/App wrappers

App-side convenience registries still exist for UI/input/profession presentation, but they should not hard-code the published output layout. Use:

```csharp
AppContentFileLocator.ResolveRegistryFile(baseDir, "some.registry.json")
```

instead of:

```csharp
Path.Combine(baseDir, "content", "registries", "some.registry.json")
```

`AppContentFileLocator` delegates to Runtime's content file-location facade, and Runtime delegates to the internal Content loader. This keeps published builds and source-checkout runs on the same path resolution rules without giving ordinary App UI/input code a `HumanFortress.Content` project dependency. Current App-side migrated call sites include input bindings, order display names, and workshop category mapping.

### Keep data/core JSON traversal out of App

Construction/workshop and recipe loading now enters through the Content-owned core catalog loader:

```csharp
CoreContentCatalogLoader.Load(dataCorePath)
```

Do not reintroduce App-local parsing for:

```text
data/core/workshops/core_workshop_*.json
data/core/placeable/workshops.json
data/core/recipes/*.json
```

Runtime's `SimulationWorldContentLoader` should not locate the runtime content directory itself or call App logging directly. It now calls `FortressContentLoader` and receives logging/content-issue callbacks from App; schema compatibility plus registry population belong behind the Content/structured registry boundary.

Important compatibility behavior preserved by the Content-owned core-data loader:

- new workshop files and legacy `placeable/workshops.json` are both loaded;
- duplicate construction ids are skipped and counted instead of failing startup;
- recipe files may be root arrays or `{ "recipes": [...] }` documents;
- legacy recipe aliases such as `workshop_id`, `workshop`, `duration_ticks`, and `primary_skill` still parse.

Keep `CoreDataRegistryLoader` split by catalog family while preserving those
compatibility rules. The main loader should only orchestrate `data/core`
directories; construction/workshop JSON, recipe JSON, and shared JSON helpers
belong in separate `HumanFortress.Content.Definitions` partials. Do not solve a
new construction or recipe edge case by growing one mixed data/core parser file
again, and do not move the parsing into Runtime/App just because the load path
starts during session bootstrap.

### Construction and recipe catalogs should be snapshots, not singletons

`ConstructionRegistry` and `RecipeRegistry` singleton compatibility classes have been deleted. `CoreDataRegistryLoader.Load(...)` parses core data into fresh immutable snapshots, and the internal structured registry applies those snapshots behind `FortressRuntimeContentSnapshotLoader`.

Runtime/gameplay reads should use the read-only catalog surface from the Content runtime snapshot or explicit constructor-injected interfaces:

```csharp
FortressRuntimeContentSnapshot.Constructions.GetConstruction(id)
FortressRuntimeContentSnapshot.Recipes.GetRecipe(id)
```

For long-lived systems and Jobs-owned code, prefer constructor-injected interfaces:

```csharp
IConstructionCatalog
IRecipeCatalog
```

Do not add new `ConstructionRegistry.Instance.Get...`, `RecipeRegistry.Instance.Get...`, or external `ContentRegistry.Instance` reads in runtime systems. The normal read path is now:

```text
FortressContentLoader
  -> internal CoreContentCatalogLoader / FortressRuntimeContentSnapshotLoader
  -> FortressRuntimeContentSnapshot.Constructions / Recipes
```

Those properties expose immutable snapshots through read-only interfaces. Keep it that way; do not recreate the old singleton classes or public registry singleton for convenience.

Preferred direction:

```text
ContentRegistry
  owns load/validation/indexing
  swaps immutable construction/recipe catalog snapshots
  exposes read-only construction/recipe catalog interfaces

Runtime/App
  request definitions through catalog interfaces
  do not parse content files directly
```

Do not move Jobs-owned code back to `RecipeRegistry.Instance`. Craft already uses `ICraftRecipeCatalog`; future construction/craft/runtime seams should follow that pattern.

Keep the structured `ContentRegistry` split by responsibility. Load
orchestration and query/snapshot compatibility can stay in the main partial,
but JSON parser families, deterministic geology handle/index construction,
tuning/zone/alias loading, validation, and content hash generation should live
in focused `ContentRegistry.*.cs` partials under
`HumanFortress.Content.Registry`. Recombining those helpers into the main file
makes Content look like a singleton service locator again and hides which data
family owns a compatibility rule.

Runtime command targets should not keep "helpful" global fallbacks. `SimulationRuntimeContext` now requires explicit `IRecipeCatalog` and `IConstructionCatalog` dependencies for workshop queue commands. If a test or tool needs a context, pass `RecipeCatalogStore.Empty`, `ConstructionCatalogStore.Empty`, or a small in-memory catalog explicitly.

App UI helpers should also consume the active runtime session catalog facade instead of reaching for the global structured registry. Current migrated paths include:

- workshop panel keyboard default recipe lookup;
- workshop panel context resolution;
- map-click workshop detection;
- build-menu workshop category selection;
- workshop category mapping;
- workshop overlays and workshop panel title/footprint lookup.

Runtime geology display/application reads follow the same rule. Use the active `IRuntimeGeologyCatalog` from the runtime session facade for map rendering, tile popups, and terrain diff application. Do not add new direct geology lookups from `ContentRegistry.Instance` inside Runtime, Jobs, Simulation diff application, or App rendering helpers.

Construction planning/execution also uses explicit dependencies now:

```text
ConstructionSystem
  -> IConstructionTerrainMaterialResolver
  -> ConstructionTuning

ConstructionJobExecutor
  -> ConstructionTuning
  -> PlaceableTuning
```

Runtime/Content composition may bridge those calls inside the Content snapshot loader, but Simulation/JOBS-owned construction logic should not call `ConstructionTuning.LoadFromContent()` or `ContentRegistry.Instance` directly.

Runtime tuning objects should enter through the Content-owned runtime snapshot:

```text
FortressRuntimeContentSnapshot
  -> materials / terrain kinds
  -> constructions / recipes
  -> runtime geology catalog
  -> zone definitions
  -> ConstructionTuning.LoadFromJson(...)
  -> mining tuning JSON for MiningDropResolver
  -> NavigationTuning.LoadFromJson(...)
  -> PlaceableTuning.LoadFromJson(...)
  -> SchedulerTunings.LoadFromJson(...)
  -> WorkshopTunings.LoadFromJson(...)
```

Do not add new `LoadFromContent(...)` or `LoadFromRegistry(...)` convenience paths for runtime tuning. Those helpers tend to recreate hidden global-content reads and make hot reload/content reload behavior inconsistent.

Structured core-data application should also stay behind the Content-owned snapshot loader:

```text
FortressRuntimeContentSnapshotLoader.ApplyCoreData(...)
  -> internal structured registry applies core-data snapshots
  -> CaptureLoaded()
  -> returns materials / terrain / constructions / recipes / geology / zones / tuning JSON
```

Runtime's `SimulationWorldContentLoader` may inject snapshots into the active `World`, but it should not call `ContentRegistry.Instance.ApplyCoreData(...)` or read `ContentRegistry.Instance.Zones` directly. Use the returned `FortressRuntimeContentSnapshot.ZoneDefinitions` when registering zone definitions with the simulation world, and keep App-specific logging behind callbacks.

App adapter seams should also consume the active runtime snapshot instead of the singleton registry:

```text
MiningDropResolver
  -> IRuntimeGeologyCatalog
  -> tuning.mining JSON

ConstructionTerrainMaterialResolver
  -> IRuntimeGeologyCatalog
```

Do not reintroduce direct `ContentRegistry.Instance` reads in these adapters just because they live in App. They are composition edges and must reflect the same active-session snapshot as Runtime/JOBS-owned systems.

WorldGen follows the same rule. Fortress generation should receive explicit content:

```text
FortressGenerationContent
  -> IRuntimeGeologyCatalog
  -> tuning.mapgen JSON
  -> tuning.ore JSON
  -> tuning.cavern JSON

FortressGenerator / FortressMap / FortressChunk
  -> consume FortressGenerationContent or its geology catalog
  -> never read ContentRegistry.Instance directly
```

Keep `FortressGenerator` split by generation phase. The main generator should
show the ordered pipeline, while cavern carving, strata/surface filling, ore
placement, and tuning JSON helpers live in focused WorldGen implementation
partials. Do not move those helpers into Runtime just because Runtime starts
fortress generation, and do not let a new mapgen tuning feature turn the main
generator file back into a large mixed policy object.

`FortressRuntimeSessionCore` caches the active `FortressRuntimeContentSnapshot` returned by session content loading. Reuse that snapshot for navigation tuning, runtime dependency composition, and fortress generation. Runtime owns the `FortressGenerationContent` adaptation and the generate+fill operation; App should pass a primitive `RuntimeFortressGenerationRequest` from session/embark data rather than adapting content snapshots, holding `FortressMap`, or calling a separate world-fill operation. `FortressSessionInitializer`, `GameStateManager`, and WorldGen must not recapture the global registry just because the registry is already loaded.

Repeated core-data loads must replace construction/recipe snapshots, not append to mutable indexes. The current smoke tests verify construction count, recipe count, construction category queries, and workshop recipe queries stay stable after reload.

### Do not depend on full managers for static definitions

`ItemManager` and `CreatureManager` still expose definition lookup alongside runtime instances, but they no longer parse content files. Static definition storage is supplied as immutable catalog snapshots from the Content boundary.

When a system only needs definition metadata, use the read-only catalog seams:

```csharp
IItemDefinitionCatalog
ICreatureDefinitionCatalog
```

Examples already migrated:

- construction material matching in `ConstructionMaterialTracker`;
- material source planning in `ConstructionMaterialsPlanner`;
- profession roster display-name lookup in `ProfessionAssignments`.

Do not add new gameplay systems that call `world.Items.GetDefinition(...)` or `world.Creatures.GetDefinition(...)` just because a `World` is nearby. Prefer explicit catalog injection. App UI/render/debug code may still read from managers for presentation until those surfaces get their own view-model/catalog cleanup.

### Rebuild definition indexes through fresh snapshots

Repeated content loads must be idempotent. `HumanFortress.Content.Definitions` loaders parse files into fresh immutable catalog snapshots, and managers replace their current snapshot through `SetDefinitionCatalog(...)`.

Keep static item definition loading split by responsibility while preserving
that idempotence. `ItemDefinitionCatalogLoader` should keep deterministic file
enumeration and JSON options in the main partial, legacy `{ "items": [...] }`
furniture parsing plus stack/placeable/effects mapping in the parsing partial,
and normalization/name enrichment/validation in the validation partial.
`CreatureDefinitionCatalogLoader` should follow the same pattern by keeping
file traversal/options separate from creature stat validation. Do not move
parsing helpers into `ItemManager` or `CreatureManager` just because Simulation
stores runtime instances, and do not add Runtime startup shortcuts that parse
static definition JSON outside the Content loader.

Do not append to existing indexes during reload. This creates hidden duplication bugs where:

```text
DefinitionCount remains stable
GetByKind(...) / GetByTag(...) grows after every reload
```

That kind of bug is easy to miss because normal startup loads once, while tests, hot reload, content validation tools, and future mod reload flows may load repeatedly.

Current direction:

```text
loader
  parses and validates JSON into a fresh result

catalog snapshot
  builds definition/id/tag/kind indexes from the fresh result

manager
  swaps to the fresh catalog snapshot
  does not parse files or incrementally append stale static definitions
```

Do not reintroduce file IO or JSON validation into `ItemManager` or `CreatureManager`. App/runtime composition should load content snapshots and inject them.

Stockpile filtering should stay on explicit content/runtime/simulation seams. Preset JSON is loaded by Content into `StockpilePresetDefinition` contracts, Runtime maps those presets into Simulation `StockpileFilter` values, and Simulation projects `ItemInstance + ItemDefinition` into `ItemStackRef` for filter matching. Do not wire stockpile filters back to the legacy `HumanFortress.Core.Content.ContentRegistry`, and do not parse stockpile preset JSON in App or Runtime command targets.

### Move contracts by assembly before rewriting namespaces

Navigation has completed the follow-up namespace cleanup: shared navigation contracts now compile from `HumanFortress.Contracts` under `HumanFortress.Contracts.Navigation`. Use it as the example for later compatibility cleanup batches: move assembly ownership first, then rewrite namespaces once the dependency graph is stable.

Static item/creature definitions now follow the same pattern. These types compile from `HumanFortress.Contracts`:

```text
ItemDefinition
CreatureDefinition
IItemDefinitionCatalog
ICreatureDefinitionCatalog
ItemDefinitionCatalogStore
CreatureDefinitionCatalogStore
PlaceableProfile
Footprint
PassabilityMode
EffectsBlock
```

Do not move them back into Simulation/Core just because their namespace still looks old. Those namespaces are intentionally transitional until the owning modules are stable enough for a focused cleanup pass.

Preferred direction:

```text
first pass
  move assembly ownership to Contracts
  preserve namespaces
  keep builds/tests stable

later cleanup
  rename namespaces once content/runtime ownership is stable
```

Avoid making `HumanFortress.Contracts` depend on `HumanFortress.Core` or `HumanFortress.Simulation`. If a shared DTO needs a Core type, the shared type should move down into Contracts with it.

## Build and SDK Pitfalls

### Use the .NET 8 executable explicitly on macOS

On this machine, the stable build path is:

```bash
/opt/homebrew/opt/dotnet@8/bin/dotnet
```

Do not assume plain `dotnet` points to the right SDK/runtime. We saw a mismatch where the default CLI used newer .NET tooling while the app targeted `net8.0`, and the runtime failed until .NET 8 was correctly available.

Recommended build command:

```bash
/opt/homebrew/opt/dotnet@8/bin/dotnet build HumanFortress.sln --no-restore -m:1 -v:quiet -p:RunAnalyzers=false
```

### Do not parallel-build overlapping project graphs

Avoid running App build, Content build, and test-project build at the same time. Overlapping project graphs can touch the same `obj` files, including:

```text
src/HumanFortress.Content/obj/Debug/net8.0/HumanFortress.Content.dll
src/HumanFortress.Content/obj/Debug/net8.0/ref/HumanFortress.Content.dll
src/HumanFortress.App/obj/Debug/net8.0/apphost
```

On macOS this caused file-lock/copy/signing races:

- `HumanFortress.Content.dll` could not be opened for writing
- `ref/HumanFortress.Content.dll` could not be copied
- `apphost` not found during copy
- `apphost: is already signed`

Use sequential build/test commands.

When running a built DLL with `dotnet exec`, do not add the `dotnet run`-style argument separator:

```bash
# Correct
/opt/homebrew/opt/dotnet@8/bin/dotnet exec src/HumanFortress.App/bin/Debug/net8.0/HumanFortress.App.dll --init-only --strict-content --content-warnings-as-errors

# Wrong: passes a literal "--" to the app and may skip init-only parsing
/opt/homebrew/opt/dotnet@8/bin/dotnet exec src/HumanFortress.App/bin/Debug/net8.0/HumanFortress.App.dll -- --init-only --strict-content --content-warnings-as-errors
```

### Audit apparent hangs before assuming build is still running

When the Codex turn appears to be waiting for many minutes, first determine whether a backend command is actually still running. We have seen apparent long waits where no build/game process existed; only VS Code's Roslyn language server was alive. That means the delay was a session/front-end wait or an interrupted turn, not a `.NET` compile.

Use a bracketed `pgrep` pattern so the search command does not match itself:

```bash
pgrep -fl "[d]otnet|[H]umanFortress|[M]SBuild|[V]BCSCompiler"
```

Interpretation:

- only `Microsoft.CodeAnalysis.LanguageServer` / Roslyn: normal editor background process, not a stuck game/build;
- `dotnet build`, `MSBuild`, or `VBCSCompiler` lasting longer than expected: investigate build output, then stop/retry sequentially if needed;
- `dotnet exec ... HumanFortress.App.dll` without `--init-only`: likely a normal game loop, not a test/init command.

In the managed sandbox, broad `ps` may fail with `operation not permitted`; prefer `pgrep -fl` for this check.

Operational rule: if a build/run command has no output for about 30 seconds, check the process list and report the state instead of waiting indefinitely.

Important agent limitation: Codex does not have an independent wall-clock timer that wakes it up while a tool call is still pending. The "30 seconds" rule only works if commands are launched with short wait windows and the agent regains control at the tool boundary. Do not rely on the agent to notice elapsed time while a long-running tool call is hung at the session/front-end layer.

Preferred mitigation:

- split verification into short, sequential commands instead of one long command chain;
- prefer build/test commands that exit on their own;
- avoid interactive or normal game-loop commands unless explicitly testing the UI;
- if a command returns no output in the first wait window, immediately run the `pgrep` audit before continuing;
- when doing broad mechanical refactors, batch several source edits first, then run one bounded verification pass instead of compiling after every tiny edit.

For very large refactors, it is acceptable to ask the human to run the full local compile manually while the agent continues reading/designing. The agent should still run lightweight checks it can finish reliably, such as `rg` scans and `git diff --check`.

### Avoid `dotnet run` for the test script

`dotnet run --project tests/...` can silently spend time in build/analyzer paths before producing output. This looked like a hang.

Current `RunTests.sh` avoids that by doing:

1. explicit build with analyzers disabled;
2. direct DLL execution.

Current test command:

```bash
./RunTests.sh
```

### Analyzer runs are separate hygiene work

During feature/refactor verification, use:

```bash
-p:RunAnalyzers=false
```

Historical analyzer warnings and errors still exist. They should be fixed in a dedicated build-hygiene pass, not mixed into gameplay/system refactors.

## Test Architecture Pitfalls

### App `--test` and `--validate` are compatibility pointers

The preferred test entry is now:

```bash
./RunTests.sh
```

`HumanFortress.App --test` and `HumanFortress.App --validate` no longer host tests. They print compatibility messages pointing to the formal test runner:

```text
tests/HumanFortress.App.Tests
```

The legacy App `PhaseTests` harness now lives in `tests/HumanFortress.App.Tests`, and `./RunTests.sh` runs it after the focused regression/smoke batches.

### `InternalsVisibleTo` is temporary

`HumanFortress.App.Tests` currently uses:

```csharp
InternalsVisibleTo("HumanFortress.App.Tests")
```

This is acceptable while job systems still live in App. It should disappear as systems move into `HumanFortress.Jobs`, `HumanFortress.Simulation`, or focused testable assemblies.

### Avoid duplicating migrated regression tests

When a regression moves from `src/HumanFortress.App/TestRunner.cs` into `tests/`, remove the old App copy. Otherwise we get duplicate runtime, duplicate logs, and unclear ownership.

First migrated batch:

- transport finalizer reservation cleanup;
- transport no-path rollback;
- transport invalid-destination rollback;
- transport moved pickup target replan;
- transport active-slot backlog preservation;
- construction terrain completion cleanup;
- craft missing-input queue preservation;
- craft workshop input-ring consumption.

Second migrated batch:

- mining channel reservation full-footprint cleanup;
- item consumption diffs;
- split-stack pre-simulation diffs;
- item move relocation and stack merge;
- carry/un-carry diff merge behavior.

Final migrated batches:

- tick scheduler smoke checks;
- deterministic RNG and runtime ID checks;
- diff target encoding smoke checks;
- world/chunk and reservation smoke checks;
- command queue ordering/clear behavior;
- runtime command stage execution before system `ReadTick`.
- Phase A-D validation coverage for platform, world generation, fortress bootstrap, and navigation.

### Do not let wall-clock budgets drive pathfinding

The legacy Phase D concurrent pathfinder test originally used the old
wall-clock navigation budget field:

```text
max_ms_per_tick_pathing = 3
```

That made `PathService.Solve` allowed to return `Path.Invalid` and queue work
for a later tick based on CPU speed, GC pauses, debugger state, or thread
scheduling. Under slower thread scheduling, the concurrent test could report:

```text
Only 8/10 paths were found
```

That was not proof of an unreachable path; it was wall-clock budget deferral
being treated as a failure. Pathfinding now uses deterministic budgets
(`MaxNodesPerSearch` and `MaxPathsPerTick` / `max_paths_per_tick`) and active
path caches are invalidated by Runtime after dirty navigation chunks rebuild.
The old wall-clock budget field is not part of the active tuning contract. Do
not reintroduce `Stopwatch`, elapsed milliseconds, frame timers, or
machine-speed-dependent budget fields into simulation-visible path results.

## Runtime and Diagnostics Pitfalls

### Command execution belongs at the pre-read tick boundary

Do not call `CommandQueue.ExecuteCommands` from UI, game states, screen update code, or render-thread services. UI/App code should enqueue commands only.

Current runtime path:

```text
TickScheduler.PreTick
  -> Runtime-owned SimulationTickPipeline
  -> Runtime-owned SimulationCommandStage.Execute
  -> CommandQueue.ExecuteCommands
  -> system ReadTick
```

The regression coverage now proves a due command sees the real `SimulationRuntimeContext.CurrentTick` and is visible to systems during the same tick's `ReadTick`.

Profession allocation changes also go through this path now:

```text
UI work allocation input
  -> GameStateManager.EnqueueCurrentTickCommand
  -> SetProfessionWeightCommand
  -> IProfessionAssignmentCommandTarget
  -> ProfessionAssignments.SetWeight
```

`IProfessionAssignmentCommandTarget` is now a Runtime-owned seam. Runtime keeps `HumanFortress.Core.Commands.ISimulationContext` free of job-system details, while App composition supplies a weight-write callback from the Jobs-owned `ProfessionAssignments` instance during host composition.

Debug item spawning also goes through this path now:

```text
SpawnItemCommand
  -> IItemSpawnCommandTarget
  -> ItemsDiffLog.Add(AddItem)
  -> ItemsDiffApplicator.ApplyAdditions
```

Debug creature spawning also goes through this path now:

```text
SpawnCreatureCommand
  -> ICreatureSpawnCommandTarget
  -> CreaturesDiffLog.AddSpawnCreature
  -> CreaturesDiffApplicator.ApplyAll
```

The creature diff path is intentionally narrow: it supports spawn-only command migration and should not be treated as a complete creature mutation system yet.

Core order commands also no longer cast `context.World` to the concrete `World` type, and they should not directly mutate `OrdersManager`:

```text
CreateMiningOrderCommand / CreateAdvancedMiningOrderCommand / CreateHaulOrderCommand
CreateConstructionOrderCommand / CreateBuildableConstructionOrderCommand
  -> IOrderCommandTarget
  -> OrderDiffLog
  -> OrderDiffApplicator.ApplyAll
```

Runtime owns command target routing; Simulation owns the typed order diff applicator.

Zone commands also no longer cast `context.World` to `World`:

```text
CreateZoneCommand / UpdateZoneCellsCommand / DeleteZoneCommand
  -> IZoneCommandTarget
  -> ZoneDiffLog
  -> ZoneDiffApplicator.ApplyAll
```

Use `ZoneCoordinator` rather than calling `ZoneManager` directly. Deleting a zone must remove chunk shards as well as the global zone instance; otherwise stale per-chunk zone data remains behind.

Workshop queue commands also no longer cast `context.World` to `World`:

```text
UpdateWorkshopQueueCommand
  -> IWorkshopQueueCommandTarget
  -> WorkshopDiffLog
  -> WorkshopDiffApplicator.ApplyAll
```

Keep recipe lookup and placeable lookup out of the command implementation. The command should dispatch the requested operation only; Runtime owns validation/context routing and Simulation applies worker-slot/automation/queue mutations at the post-tick boundary.

Stockpile creation/deletion commands also no longer cast `context.World` to `World`:

```text
CreateStockpileCommand / DeleteStockpileCommand
  -> IStockpileCommandTarget
  -> StockpileDiffLog
  -> StockpileDiffApplicator.ApplyAll
```

Stockpile create/delete now use the active typed diff path, and create diffs carry preset-derived filter/priority data. Stockpile filter matching uses item projection and is covered by smoke tests. Stockpile item-index updates for transport pickup/delivery/cancel and construction/craft full-stack consumption now enter through Jobs-owned emitters that queue `StockpileDiffLog` operations. Those item-index diffs must carry projected `ItemStackRef` payloads so `RemoveItem` can clear tag indexes after `ItemsDiffApplicator` has already deleted the item.

Stockpile slot reservations are also a typed diff concern. `HaulingSystem` should track same-tick planned reservations before choosing more stockpile destinations and should queue `ReserveSlot` beside `TransportRequest` enqueueing. Do not call `ChunkStockpileData.TryReserveSlot(...)` from planners or App code. Transport failure/cancel paths release through `StockpileDiffLog.AddReleaseSlot(...)`.

Stockpile reservations must follow accepted transport requests. `ITransportIntake.Enqueue(...)` reports whether a new pending transport entry was added; stockpile planners should only write `ReserveSlot` after a true return. `TransportRequestQueue` owns duplicate pending-item protection, so a merged or rejected duplicate request must not get a second stockpile slot reservation.

Only `TransportReason.ToStockpile` should short-circuit pickup when an item is already in a stockpile. Construction, craft, refuel, and other non-stockpile jobs must be able to pick up stockpiled items and emit stockpile remove-index diffs, otherwise stockpiles become a dead end for the broader jobs pipeline.

The richer workshop and stockpile target behavior now lives in Runtime-owned helper classes rather than directly in `SimulationRuntimeContext`. Item spawning, creature spawning, order enqueueing, and zone mutation also now live behind Runtime-owned target helpers.

`SimulationRuntimeContext` itself is Runtime-owned and should stay a runtime command clock/target context, not a dumping ground for every target interface. Commands should resolve mutations through the target-context role; do not reintroduce `context is IOrderCommandTarget` / `context is IZoneCommandTarget` style casts or make the context implement every target interface again.

Profession assignment updates now queue through `ProfessionAssignmentDiffLog` and apply after the typed Simulation diffs. Runtime still stores the bound handler supplied by concrete system composition, but command execution should not call the handler directly.

Do not reintroduce stockpile-local haul-job diffs. `CreateZone` and `DeleteZone` are authoritative active-path operations, and item placement/removal/release paths are now integrated for Transport plus construction/craft consumption. The legacy broker `CreateHaulJob` diff was removed because reserving a slot without enqueueing a `TransportRequest` leaks capacity. New stockpile hauling behavior should enter through Jobs/Transport planning and queue stockpile reservation/index diffs beside transport requests.

### Do not let GameStateManager recreate the runtime graph by hand

`GameStateManager` previously stored separate `World`, `NavigationManager`, and `SimulationRuntimeHost` fields and assembled them directly inside `InitializeWorld`. That made it both a state machine and an implicit composition root.

Current first pass:

```text
GameStateManager
  -> Runtime-owned SimulationRuntimeSessionFactory<SimulationRuntimeHost<SimulationRuntimeSystems>>.CreateNew(...)
  -> App content-loading callback
  -> Runtime-owned FortressRuntimeHostFactory through an App callback that supplies logging/content inputs
  -> Runtime-owned SimulationRuntimeSession<SimulationRuntimeHost<SimulationRuntimeSystems>>(World, Navigation, Host)
  -> Runtime-owned generic SimulationRuntimeHost<TSystems>
  -> Runtime-owned SimulationRuntimeHostCore
```

Keep future runtime construction behind this seam. UI/state code should not directly reset schedulers, clear command queues, create navigation managers, or new up runtime hosts. Content loading and concrete system construction are still App callbacks because they depend on current content registries, job adapters, logger callbacks, UI hooks, and SadConsole-facing lifetime.

`SimulationRuntimeHostCore` owns scheduler restart, tick-system registration, pipeline attachment, and stop-time pipeline detachment. `SimulationRuntimeHost<TSystems>` owns the generic lifecycle shell. `SimulationRuntimeSystems`, `FortressRuntimeDependencies`, `FortressRuntimePlanningSystems`, `FortressRuntimeJobSystems`, `FortressRuntimeSystemsFactory`, `FortressRuntimeHostFactory`, and `FortressRuntimeStartup` now compile from Runtime. `FortressRuntimeDependencies` is split into `FortressRuntimeCatalogs`, `FortressRuntimeTunings`, and `FortressRuntimeWorkforce`. App still supplies logger callbacks and optional command delegates, such as auto-dig, because those remain App-specific.

Do not collapse those groups back into one long factory method. They are migration handles: dependencies point toward Content/Runtime, planners point toward Simulation/Jobs boundaries, and job-system shells point toward Jobs/App adapter cleanup.

`FortressRuntimeHostFactory` should create `FortressRuntimeDependencies` once and use that same instance for both `SimulationRuntimeContext` catalog injection and concrete system creation. If host construction reads content separately from systems creation, command targets and gameplay systems can accidentally observe different catalog snapshots after a future hot-reload/content-reload pass. It should receive logging as callbacks; do not reintroduce direct App `Logger` calls into Runtime source.

For runtime composition, structured registry reads should stay behind the Content-owned `FortressRuntimeContentSnapshotLoader`. `FortressRuntimeDependencies.Load(...)` should consume that snapshot, then split it into catalog/tuning/geology/workforce groups. Do not add new direct `ContentRegistry.Instance` / `JObject` tuning reads to host factory, systems factory, job-system group creation, runtime command targets, App rendering helpers, App workshop/build UI helper code, or Navigation.

Scheduler/workshop tuning loaders no longer keep direct file/registry compatibility paths. Runtime composition should use:

```text
FortressRuntimeContentSnapshot
  -> ConstructionTuning.LoadFromJson(...)
  -> NavigationTuning.LoadFromJson(...)
  -> PlaceableTuning.LoadFromJson(...)
  -> SchedulerTunings.LoadFromJson(...)
  -> WorkshopTunings.LoadFromJson(...)
```

The debug cache remains in `GameStateManager` for now because it is UI-facing state, not simulation session state. Do not move it into the runtime host until there is a structured diagnostics surface.

## Navigation Boundary Pitfalls

### Contracts assembly owns shared navigation contracts

`HumanFortress.Contracts` now contains the stable navigation DTO/interface surface:

```text
IPathService
IWorldNavigationView
INavigationWorldSource
NavigationChunkSnapshot
NavigationTile
PathRequest / Path / PathNode / Point3 / ChunkKey
MoveMode / PathFlags / NavCapability
IMovementExecutor / MovementStatus / MovementUpdate
```

These contract types live in the `HumanFortress.Contracts.Navigation` namespace. Concrete navigation implementation types such as `NavigationManager`, `NavigationTuning`, `PathService`, `WorldNavigationView`, `ChunkNavData`, `DeterministicAStar`, and `MovementExecutor` live in `HumanFortress.Navigation.Implementation` as internal implementation types.

Be careful with `ChunkKey`: Simulation world chunks and navigation contracts both define a `ChunkKey`. Files that import both `HumanFortress.Contracts.Navigation` and `HumanFortress.Simulation.World` should use explicit namespaces or aliases where both meanings appear.

Jobs should depend on navigation contracts only. Runtime job-system wrappers
should receive path/world-view/movement contract bundles from
`RuntimeNavigationServices`; that Runtime.Navigation seam creates concrete
`PathService`, `WorldNavigationView`, and `MovementExecutor` instances and
registers path services for dirty-chunk cache invalidation before injecting
`IPathService`, `IWorldNavigationView`, and `IMovementExecutor` into Jobs-owned
executors.

Any project that implements test doubles or directly consumes these contracts should reference `HumanFortress.Contracts` explicitly. Do not rely on transitive references through App.

### Keep Simulation types out of Navigation

`HumanFortress.Navigation` no longer references `HumanFortress.Simulation` or `HumanFortress.Core`. Do not pass `World`, `Chunk`, `TileBase`, `TerrainKind`, content registries, or Core tuning loaders into Navigation internals.

Use the Contracts-owned source/snapshot contracts instead:

```text
INavigationWorldSource
NavigationChunkSnapshot
NavigationTile
NavigationTileKind
```

The current Simulation adapter lives in `HumanFortress.Runtime` as `SimulationNavigationSource`. Keep it there unless a more explicit world-navigation adapter package is introduced; do not move Simulation type knowledge back into Navigation.

Navigation tuning follows the same dependency rule:

```text
Content snapshot
  -> NavigationTuning.LoadFromJson(...)
  -> SimulationNavigationFactory.Create(world, rebuildAll, tuning)
  -> NavigationManager(...)
  -> PathService(...)
```

Do not reintroduce `NavigationTuning.LoadFromContent()` or a Core/Content project reference from `HumanFortress.Navigation`.

## App Composition Pitfalls

### Avoid null-forgiving callback cycles in state/input composition

When a state-level composition factory needs callbacks into a controller that is created later in the same method, do not capture `controller!` in lambdas. Use an explicit callback hub or binder that fails with a clear "used before binding" error. This keeps initialization order auditable and prevents hidden null-reference traps during future constructor changes.

### Keep GameState wrappers as adapters, not runtime service locators

State wrappers should create their screen and call narrow state-transition collaborators. Do not pass the whole `GameStateManager` into a state just to reach runtime init/access methods, and do not repeat direct `GameHost.Instance.Screen` writes in every wrapper. Centralize SadConsole presentation behind an App-owned presenter.

### Do not keep no-op frame hooks as architectural placeholders

The App state-machine wrappers are not the SadConsole screens themselves. If no `GameState` wrapper overrides a frame `Update(...)`, do not keep a `Program -> GameStateManager.Update -> GameState.Update` hook as a placeholder. It suggests the wrong owner for frame work and makes future UI changes harder to reason about. Let SadConsole drive `ScreenObject.Update(TimeSpan)` and keep the state machine focused on transitions.

### Centralize session-size rules

Fortress session size is used by embark UI, runtime world initialization, viewport math, and placement bounds. Do not duplicate min/max checks in each caller. Use Contracts `FortressSessionSizeLimits` plus App `FortressSessionSizeRules` so logging, session storage, runtime initialization, and UI viewport sizing stay consistent.

### Query-time rebuild must stay removed

`NavigationManager.GetNavDataAt` is read-only. Do not reintroduce stale-cache rebuilds from path queries.

The intended ownership is:

```text
simulation commit -> collect dirty chunks -> rebuild navigation -> path queries read cache only
```

For isolated job-system tests or temporary private navigation managers, rebuild explicitly at composition time instead of rebuilding from `GetNavDataAt`.

### Content loading diagnostics must fail loudly and specifically

We saw startup output like:

```text
[ContentRegistry] Loaded: 0 materials, 17 geology entries, 19 zone definitions
[ContentRegistry] 18 errors during loading
```

Root causes found and fixed in the first pass:

- old `HumanFortress.Core.Content.ContentRegistry` only looked for legacy `materials.json`, while the repo now ships `materials.authoring.json`;
- summarized errors hid the actual missing references;
- content loading happened before file logging was initialized;
- `geology.json` referenced four ore material ids that did not exist;
- construction loaded both new workshop files and legacy `placeable/workshops.json`, causing duplicate ids;
- recipe loading treated legacy root-array files as parse errors.

Current first-pass behavior:

- content loading is logged to both console and the async diagnostic pipeline;
- App creates a full timeline log at `fortress_debug.log` plus category logs under `logs/`;
- the old registry material-loading bug was removed with the old registry source; the structured registry currently reports 83 materials in startup logs;
- `RuntimeContentRegistryLoader` now loads only the structured runtime registry behind `FortressContentLoader`;
- structured registry loading now supports top-level array material files and resets validation state before reload;
- geology cross-reference errors are clear and currently clean;
- construction duplicates are explicitly skipped and counted;
- recipe loading reports `errors=0`.

Remaining architecture risk: there is now one normal runtime registry source model, the structured registry, and production direct reads are concentrated in Content bootstrap/snapshot capture/application. The old legacy registry source has been deleted, and the structured registry implementation now compiles from `HumanFortress.Content.Registry`. The remaining risk is policy rather than namespace ownership: strict content-load failure rules and richer diagnostics/debug surfaces should be handled without adding new singleton reads. Former non-registry runtime DTO compatibility names have moved into Contracts registry namespaces, and architecture smoke/source scans should keep the old compatibility namespaces from returning.

Runtime geology and zone JSON DTOs now compile from
`HumanFortress.Contracts.Content.Registry`. The zone loader uses explicit
`System.Text.Json` property mappings, which prevents `zones.json` snake_case
fields such as `display_name`, `ui_hints`, and `default_policies` from silently
deserializing to defaults.

Item and creature definition loading has now moved into `HumanFortress.Content.Definitions`, and Simulation managers consume snapshots instead of reading files. Construction/recipe loading now also produces immutable snapshots owned by the structured registry and exposed through `FortressRuntimeContentSnapshot`. Construction/recipe definitions and catalog interfaces/stores compile from Contracts. The remaining registry-unification risk is strict-mode diagnostics and compatibility naming, not Core-owned registry implementation or App-local loading orchestration.

### Diagnostics should be async, categorized, and non-authoritative

The first structured diagnostics pass added:

```text
module code
  -> IDiagnosticSink / Logger compatibility facade
  -> async dispatcher
  -> fortress_debug.log
  -> logs/content.log, runtime.log, simulation.log, jobs.log, navigation.log, ui.log, core.log
```

Do not let simulation systems write files directly. Emit a diagnostic event or use a temporary compatibility callback. The dispatcher owns file IO and sequence assignment.

`HumanFortress.Simulation` now has a small `SimulationDiagnostics` helper for transitional systems that still expose static `LogCallback` bridges. Use that helper instead of adding new `Console.WriteLine` calls inside Simulation code.

Do not use diagnostics for authoritative replay. Logs are for observability and debugging; deterministic replay still belongs to command/event/save streams.

`DiagnosticHub` is a transitional bridge for Core systems that are not yet constructed with dependencies. New runtime-owned services should prefer an injected `IDiagnosticSink`.

Core infrastructure is now partway through that transition: `CommandQueue`,
`EventBus`, and `TickScheduler` accept optional `IDiagnosticSink` constructor
dependencies, and Runtime session services pass the active sink when they
create the default scheduler/queue/bus. Keep the hub only as a compatibility
fallback for ad hoc construction; do not add new direct `DiagnosticHub.Error`
calls to Runtime-composed infrastructure.

WorldGen now follows the same pattern for Runtime-created paths:
`WorldGenerationService`, `WorldGenerator`, `FortressGenerator`, and
`FortressMap` accept an optional `IDiagnosticSink`, while Runtime composition
passes the active sink. Keep `DiagnosticHub` only as fallback for ad hoc
construction. Simulation and Content static diagnostics helpers now also expose
injectable sink seams, with Runtime assigning the active sink at content load
and simulation log-callback binding. The remaining bridge is compatibility
fallback, not direct lower-module hub emission.

`Contracts` should stay log-free.

### Embarkability UI needs rule-level diagnostics

The world map showed every tile as `NOT EMBARKABLE`. The UI did not expose which rule failed.

First pass implemented:

- `WorldTile.GetEmbarkabilityFailures()` exposes the current rule failures;
- WorldMap side panel shows the first failure reasons under `NOT EMBARKABLE`;
- regression coverage verifies low elevation, high elevation, and river-class failures.

Before changing world-gen or embark rules further, keep diagnostics showing:

- selected tile coordinates;
- terrain/biome/geology summary;
- each embarkability rule;
- pass/fail reason.

### Console logging is too noisy for tests

Current tests print a lot of `ItemManager` and creature spawn logs. That is acceptable short-term, but it slows reading and hides failures.

Long-term fix:

- structured logger;
- categories and levels;
- test mode default level;
- capture logs only on failure.

## Refactor Process Pitfalls

### Move job systems in slices, not whole executors

`HumanFortress.Jobs` now exists, but the first transport extraction deliberately moved only low-risk state/helper types:

```text
ActiveJob
JobStage
TransportBacklogBuffer
TransportJobFinalizer
TransportJobStatsSnapshot
TransportStatsTracker
TransportIntakeFilter
ITransportJobLogger
ITransportWorkerCandidateSource
TransportAssignmentHandler
ITransportMovementDiffEmitter
TransportReplanHandler
ITransportItemDiffEmitter
ITransportJobCompletionSink
TransportPickupHandler
TransportDeliveryHandler
TransportActiveJobRunner
TransportActiveJobView
TransportActiveJobDebugView
TransportDebugSnapshot
TransportJobExecutor
```

This is intentional. Moving the full transport executor in one pass would drag pathing, world access, diffs, professions, logging, and debug snapshots across the boundary at once.

Use this order for future transport movement:

```text
state/contracts -> stats snapshots -> intake/filtering -> assignment/replan -> pickup/delivery -> active runner -> debug DTOs -> executor core -> App composition shell
```

The stats snapshot is now a top-level `TransportJobStatsSnapshot` in Jobs, and
the counters are owned by each executor's `TransportStatsTracker`. Avoid
reintroducing nested DTOs on `TransportJobSystem` or static global transport
stats; nested public DTOs make later assembly movement harder, while static
stats leak across sessions.

Transport active/debug snapshot DTOs now live in Jobs (`TransportActiveJobView`, `TransportActiveJobDebugView`, `TransportDebugSnapshot`). Keep public debug contracts near the executor that owns the data; do not put them back as nested App types.

`TransportIntakeFilter` now owns request readiness/de-dup filtering in Jobs. Keep it focused on domain state checks (`item exists`, `item on ground`, `not reserved`) and do not add UI logging or executor side effects to it.

`TransportAssignmentHandler` now lives in Jobs. Profession weighting is behind `ITransportWorkerCandidateSource`, and logging is behind `ITransportJobLogger`. Keep those seams narrow; do not pass App globals or UI-facing services back into Jobs-owned handlers.

`TransportReplanHandler` now lives in Jobs. It only depends on `ITransportMovementDiffEmitter.MoveCreature` instead of the full App `TransportDiffEmitter`. Preserve that narrow dependency: replan should not learn how to split stacks, mark carry state, or move items.

`TransportPickupHandler` and `TransportDeliveryHandler` now live in Jobs. They depend on `ITransportItemDiffEmitter` for item/carry/split diffs and `ITransportJobCompletionSink` for profession progress. Keep destination validation in Simulation and keep App-specific profession objects behind the completion sink.

`TransportActiveJobRunner` now lives in Jobs. It should remain a coordinator over movement update, replan, pickup, delivery, and missing-worker cleanup. It depends on separate movement and item/carry diff interfaces; do not collapse those back into a monolithic concrete emitter.

`TransportJobExecutor` now owns the transport tick core in Jobs: request drain/backlog, assignment throttle, active write tick, scheduling hints, debug snapshots, and replay snapshots. Keep the main executor file focused on constructor state/dependency assembly; read/intake, write ticks, snapshot/replay/debug read models, scheduling hints, and helper lookups belong in focused partials. The Runtime-owned `TransportJobSystem` should stay a composition shell over narrow Jobs interfaces.

### Mining extraction now follows the same shell/core pattern

The mining executor has the same ownership rule as transport: Jobs owns the tick core and concrete job adapters/emitters; Runtime owns the tick-facing wrapper; App only supplies composition-time logger/content/UI callbacks.

Jobs-owned mining slices now include:

```text
ActiveMiningJob
MiningStage
MiningBacklogBuffer
MiningDeferredStairwellBuffer
MiningTileReservationTracker
MiningPathSeed
MiningJobStatsSnapshot
MiningDebugSnapshot
MiningDebugSnapshotBuilder
MiningDigOrdering
MiningAdjacencyFinder
MiningIntakeCoordinator
MiningStairwellGate
MiningReadJobProcessor
MiningAssignmentHandler
MiningResultApplier
MiningActiveJobRunner
MiningJobExecutor
```

Keep App dependencies behind the narrow mining seams:

```text
IMiningJobLogger
IMiningWorkerCandidateSource
IMiningWorkCostResolver
IMiningDropResolver
IMiningDiffEmitter
IMiningJobCompletionSink
```

### UI state should not store Simulation enum/DTO types

App UI state can mirror a Simulation concept, but the stored type should be an
App-owned presentation option unless it is already a stable contract DTO. Map to
Simulation enums and command DTOs only at the command factory or runtime query
boundary. This keeps menu rendering, input selection, and debug highlight labels
from becoming accidental Simulation API consumers.

The same rule applies to simple menu DTOs: stockpile preset menu options are UI
options until the command boundary only needs a preset id.

### App supplies log callbacks; Runtime owns lower-layer callback targets

App lifetime code can decide which logger implementation to use, but it should
not maintain the list of every lower-layer Simulation/Navigation static
callback target. Keep that list in Runtime composition (`FortressRuntimeLogBindings`)
and let App pass a category-to-callback factory.

Do not make Jobs-owned mining code depend directly on App globals or App-owned concrete services. The tick-facing `MiningJobSystem` wrapper should remain only a composition shell, and executor dependencies should continue crossing through the narrow mining interfaces above. Source-owned Jobs/Runtime helpers may still have the old namespace until the compatibility cleanup pass, but they should not regain App assembly ownership.

### Construction extraction uses callback-only App ownership

Construction is simpler than transport/mining because it does not own pathing or worker assignment. Diff emission and logging bridges are now Jobs-owned, while App ownership is limited to binding the UI workshop-completion callback during bootstrap.

Jobs-owned construction slices now include:

```text
ConstructionRequirementMatcher
ConstructionTargetMapper
ConstructionFootprintCells
ConstructionMaterialTracker
ConstructionSiteProgress
ConstructionSiteSafety
ConstructionCompletionApplier
ConstructionCompletionCoordinator
ConstructionJobExecutor
```

Keep App dependencies behind the narrow construction seams:

```text
IConstructionJobLogger
IConstructionDiffEmitter
IConstructionWorkshopCompletionSink
```

Do not pass `Logger`, concrete UI services, or a static `ConstructionJobSystem` UI hook directly into Jobs-owned construction code. App should bind UI notifications through `IFortressRuntimeBootstrapAccess.SetWorkshopCompletionHandler(...)`; Runtime should route that through `FortressRuntimeWorkshopCompletionNotifier`, while the Runtime-owned `ConstructionJobSystem` remains only a composition shell over narrow Jobs interfaces and callback injection.

The former `InternalsVisibleTo("HumanFortress.App")` bridge in `HumanFortress.Jobs` has been removed. Do not add it back; App should use Runtime facades and Contracts snapshot DTOs, not Jobs internals.

Jobs implementation types should stay internal. Runtime and tests currently use
friend access for executor cores, tunings, orchestration probes, debug snapshots,
and adapters. If a shape must become a long-term public cross-module contract,
move that shape to `HumanFortress.Contracts` first instead of exposing the Jobs
implementation type.

### Craft extraction needs three explicit seams

Craft looks small from the outside, but it crosses planning, content lookup, material logistics, worker assignment, movement, item diffs, and workshop queue state. Moving it safely required separating those concerns instead of dragging App types into Jobs.

Jobs-owned craft slices now include:

```text
PlannedCraftJob
ActiveCraftJob
CraftJobStatsSnapshot
ActiveCraftJobView
CraftWorkshopLocator
CraftInputCounter
CraftMaterialReadinessChecker
CraftTransportRequestEmitter
CraftPlanner
CraftMaterialConsumer
CraftOutputEmitter
CraftAssignmentHandler
CraftActiveJobRunner
CraftJobFinalizer
CraftJobExecutor
```

Keep App dependencies behind the narrow craft seams:

```text
ICraftJobPlanner
ICraftRecipeCatalog
ICraftDiffEmitter
ICraftWorkerCandidateSource
```

Do not pass `RecipeRegistry` or App globals deeper into Jobs-owned craft code. `CraftJobSystem` is now a Runtime-owned composition shell, while `CraftDiffEmitter`, `ProfessionAssignments`, and `WorkerSelectionStrategy` are Jobs-owned compatibility-namespace types.

`CraftPlanner` now lives in Jobs. The important lesson is that Planner could move only after recipe lookup was hidden behind `ICraftRecipeCatalog`; otherwise Jobs-owned craft code would keep reaching into the global `RecipeRegistry.Instance` singleton.

### Diff-based systems need regression tests before movement

Any direct world mutation replaced by diffs must have a small regression first or immediately after.

Useful examples already added:

- construction material consumption emits item remove diff;
- construction residual relocation emits `MoveItem`;
- transport split-stack rollback is non-mutating on failure;
- craft input failure preserves queue entry.

### Do not rederive chunk/local targets in job emitters

Job diff emitters should not hand-roll:

```text
chunkX = worldX / Chunk.SIZE_XY
localX = worldX % Chunk.SIZE_XY
localIndex = Chunk.LocalIndex(localX, localY)
```

Use `WorldCellTargetEncoding` instead, then pass the resulting `WorldCellTarget` directly to `ItemsDiffLog` where possible. The older `ChunkKey + localIndex` item-diff overloads remain for compatibility, while general `DiffLog` still uses `DiffTarget`.

General `DiffLog` ordering should not recover module precedence from string
prefix checks in Core. Use the `SystemOrder` field supplied by the producing
module and `LocalSeq` when same-system ordering matters. Core can sort and
merge by those numeric fields, but it should not know that `Jobs.Mining` beats
`Jobs.Transport`, and it should not use small hash fragments as the effective
system-order contract.

Entity-scoped general `DiffLog` operations must not merge solely by a truncated
32-bit GUID projection. `DiffTarget.EntityKey` is the wider compatibility
bridge for producers that still identify entities by GUID. Keep `EntityId`
only for old callers until they migrate, and add collision regression coverage
when touching move/carry diff paths.

Movement executor state has the same collision risk as diff merge state. If
two workers share a legacy 32-bit GUID projection, a `Dictionary<uint, ...>`
movement map lets one worker overwrite another worker's path state. Navigation
contracts and Jobs movement paths should use the wider `ulong` entity key; only
explicit compatibility bridges should still read the old 32-bit handle.

`TileBase` is a multi-field value type. Until the world renderer/runtime reads
an immutable frame snapshot or chunks move to packed/seqlock tile storage, live
chunk tile reads and tile-copy snapshots must synchronize with write-phase
replacement. Otherwise a render/debug/read path can observe a mixture of old
and new tile fields that never existed as authoritative Simulation state.

### Planner and executor must share domain rules

Craft exposed a real bug:

- planner treated workshop footprint plus adjacent ring as available input area;
- consumer only consumed from the footprint.

Result: jobs could be planned as ready but fail at consumption time.

Rule: if planner and executor both reason about the same gameplay concept, extract that rule into shared helper code.

### Do not move projects before dependencies are inverted

Transport, mining, construction, and craft executor cores now live in `HumanFortress.Jobs`, and craft planning has also moved there. Runtime now owns the tick-facing job wrappers. App still owns concrete session/runtime glue that depends on UI lifetime, logger callbacks, and bootstrap wiring.

Moving them too early would drag App/runtime/navigation dependencies into the wrong assemblies. Invert Navigation and stabilize contracts first.

### UI snapshots are not App-owned just because UI consumes them

The App may own SadConsole rendering, input routing, and session glue, but UI/debug read models that aggregate live Runtime/Simulation/Jobs data should not live in `HumanFortress.App.Runtime`.

Current rule:

```text
Contracts.Runtime.Snapshots
  owns public snapshot DTO contracts consumed by App/UI

Runtime.Snapshots
  owns builders/facades that aggregate Runtime/Simulation/Jobs state into
  the contract DTOs

App.Runtime
  calls snapshot facades and adapts them to SadConsole UI/input flows
```

Do not move job/workforce/order/workshop/build/debug/tile-inspection DTOs or aggregation helpers back into App for convenience. If a snapshot builder starts growing into a God Object, split it by read-model domain inside `HumanFortress.Runtime.Snapshots` instead of making `GameStateManager` or `FortressRuntimeAccess` responsible for the mapping.

When a read model needs multiple live session parts, add a Runtime-owned session facade such as `FortressRuntimeSessionSnapshotFacade` instead of unpacking `_runtimeSession.World`, `.Navigation`, `.Host.Geology`, `.Host.Constructions`, or `.Host.Recipes` in App. Keep live world use in App limited to scoped bootstrap operations like fortress-map fill and optional startup debug seeding.

`HumanFortress.Contracts` should stay renderer-agnostic. Runtime snapshot DTOs
use project-owned `SnapshotColor` and `SnapshotPoint` primitives; Runtime maps
from SadRogue types while building read models, and App maps back to SadRogue
types at drawing boundaries. Do not add `TheSadRogue.Primitives` back to
Contracts just because a DTO is consumed by SadConsole.

### Architecture boundaries should be executable checks

Manual source scans are useful during refactor, but the stable rule should live
in tests once it becomes a boundary. The current smoke runner enforces that App
does not import lower implementation modules, App.Runtime imports stay confined
to adapter/port composition files, old mixed runtime facade names do not return,
and production project references match the approved module graph.

When changing project references, update the architecture smoke test in the same
patch as the csproj change. A new reference should represent an intentional
module-boundary decision, not a convenient compile fix.

The same applies to public types. Content, Jobs, Navigation, Simulation, and
WorldGen are internal/friend implementation assemblies; App should expose only
`Program`; Runtime should expose only the approved factories/bootstrap helpers
and App-facing session port interfaces; Core should expose only approved
foundation types for commands, events, ticks, deterministic RNG, replay hashes,
diffs, and world primitives. If a new public type is genuinely needed, update
the smoke test allowlist with a boundary explanation in the same patch.

### Cross-module diagnostics are contracts

Diagnostic event DTOs, severity levels, sink interfaces, and the transitional
process-wide diagnostic hub are consumed by App, Core, Content, Simulation, and
WorldGen. Keeping them in Core forced App and Content to reference Core for a
logging concern. These types now live in `HumanFortress.Contracts.Diagnostics`.

Do not add gameplay, command, random, or scheduler dependencies to the
diagnostic contract namespace. App should implement sinks and UI presentation
against Contracts; lower modules should only emit diagnostic events through the
contract sink/hub.

### App-facing content load data is a contract, not Content implementation

App needs to present content load failures, strict-mode blocking issues, and a
few UI registry file paths. It should not reference `HumanFortress.Content` for
those jobs. Content owns actual JSON loading and registry/catalog assembly;
Runtime owns the public startup/file-location facade; App consumes
`HumanFortress.Contracts.Content.Loading` report/path/issue DTOs.

If a new App feature needs a content-backed UI file, route file location through
an App-owned wrapper such as `AppContentFileLocator` instead of importing
`FortressContentLoader` directly. If it needs gameplay/content facts, add a
Runtime snapshot/query DTO rather than parsing Content JSON in App.

### Runtime root is not the internal helper bucket

The Runtime project is allowed to compose lower implementation modules, but its
root namespace should not become a dumping ground for every helper created
during that composition. Public factories/ports and `FortressRuntimeSessionCore`
facade partials can stay in `HumanFortress.Runtime`; internal helper families
should use focused namespaces and folders.

Current examples:

```text
HumanFortress.Runtime.Composition     -> system/dependency/logging composition
HumanFortress.Runtime.Host            -> host, tick pipeline, command clock context
HumanFortress.Runtime.Session         -> session handles and session services
HumanFortress.Runtime.Diff            -> Runtime-owned mutation log bundles
HumanFortress.Runtime.Content         -> content bootstrap adapters and stockpile presets
HumanFortress.Runtime.Geometry        -> Runtime internal geometry adapters
HumanFortress.Runtime.WorldGeneration -> fortress generation runner glue
HumanFortress.Runtime.Navigation      -> Simulation-backed navigation adapters
HumanFortress.Runtime.Startup         -> startup/autodig helpers
HumanFortress.Runtime.Commands        -> command implementations, execution, targets
```

When moving a helper out of the root namespace, move the physical file in the
same batch and add/update architecture smoke coverage for both path and
namespace. If a helper needs `RuntimeMutationDiffLogs`,
`RuntimeSessionServices`, or the stockpile preset catalog, make that dependency
explicit with `HumanFortress.Runtime.Diff`, `.Session`, or `.Content` imports
instead of leaving the helper in the root namespace for convenience.

### Keep build verification short and explicit

Fast checks:

```bash
/opt/homebrew/opt/dotnet@8/bin/dotnet build src/HumanFortress.App/HumanFortress.App.csproj --no-restore --no-dependencies -m:1 -v:quiet -p:RunAnalyzers=false
./RunTests.sh
```

Full check:

```bash
/opt/homebrew/opt/dotnet@8/bin/dotnet build HumanFortress.sln --no-restore -m:1 -v:quiet -p:RunAnalyzers=false
git diff --check
```

If a command produces no output for too long, investigate instead of waiting indefinitely.

### Stable snapshots must include write-side indexes

It is not enough for broad read APIs to sort dictionary values on return. If a
manager-owned index later drives an authoritative mutation, the index itself
must have stable semantics. `ItemManager` position indexes now keep GUIDs sorted
on insert, and stack consolidation picks definition groups plus merge targets by
stable keys. Do not reintroduce "first item in list wins" behavior unless the
list order is itself explicit authoritative state and is saved/hashed.

### Ordered debug previews should not be dictionaries

`IReadOnlyDictionary` is fine for lookup-style DTOs, but it is a poor contract
for ordered debug/UI previews. The transport shard debug path now emits shard
count row DTOs from Jobs through Runtime/Contracts so App rendering can
`Take(3)` from a stable sequence. When UI wants top-N, first-N, or table order,
use row lists with a declared sort key instead of relying on dictionary
insertion/enumeration behavior.

### Restore must canonicalize external save payload order

Save builders can produce canonical arrays, but restore code must not assume an
external JSON document kept that order. Terrain chunks restore in Z/Y/X order,
material string-int rows and improvement rows normalize during conversion, and
Runtime save manifest verification rejects blank/duplicate section names before
lookup. If a future restore path accepts arrays for authoritative state, sort by
the owner key before mutating live state unless the array order is itself part
of the saved authority.

### Concurrent collections are not authority order

`ConcurrentQueue`/`ConcurrentBag` can be useful as a short-lived transport
mechanism, but their enumeration order should not become save, replay, hash, or
debug UI authority. `OrdersManager` now keeps ingress queues, recent previews,
and active designations as guarded owner-state collections and sorts active
snapshots at the source. If a future manager needs concurrent ingress, drain it
into deterministic owner state before exposing broad snapshots. Planner outbox
queues are the same: use ordinary owner queues for deterministic read/write
handoffs, and delete dead outboxes instead of preserving empty compatibility
queues that imply a false concurrency contract. Jobs backlog/retry queues are
also replay-affecting owner state, so preserve FIFO order directly with
`Queue<T>` and snapshot/restore row order instead of depending on concurrent
collection snapshots. `CommandQueue` follows the same rule at the replay
ingress boundary: enqueue under a lock, assign explicit sequence identity, and
snapshot pending commands by `(tick, sequence)` rather than treating a
concurrent queue snapshot as authority. `EventBus` handler lists also need a
declared order because future post-commit gameplay subscribers may observe it;
copy registration-ordered handler lists under a lock rather than mutating lists
through concurrent-dictionary callbacks.
