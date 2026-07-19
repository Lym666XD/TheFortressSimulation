# Save And Replay Architecture

Updated: 2026-07-11
Status: experimental internal substrate; player persistence deferred

This document records the experimental internal snapshot export/restore substrate
and future persistence constraints. It is not an active implementation plan and
does not describe a player save/load feature. `SAVE_FORMAT.md` remains a
long-term design reference only.

## Current Decision

Player save/load, autosave, persistence compatibility, migration, and public slot
formats are explicitly deferred until after the active architecture refactor.
App exposes no persistence port and must remain that way during the current goal.

A Runtime-owned, test-only vertical slice exists: Runtime can assemble an
experimental export directory containing `slot_manifest.json` plus a
`runtime_snapshot.json` document. The document contains manifest metadata,
command replay rows, primitive RNG stream rows, and a Simulation-owned world
payload for the currently supported authoritative slices. The document now also
carries Jobs-owned payload slices for transport pending requests, transport
active/backlog jobs, transport scheduling hints, mining active/backlog/
deferred/reserved/recent-completion jobs, and craft active/backlog jobs.
The development manifest records
the document filename, snapshot format, Runtime-authored metadata,
checkpoint/world/job hashes, and section/row counts. Runtime also owns the
first development compatibility policy for this vertical slice: known documents
are readable by internal tests, registered transforms exercise migration
mechanics, and future/unknown kinds fail closed. Runtime can validate/read a
compatible directory and restore the
supported world payload, transport/mining/craft job payloads, RNG streams, and
pending commands into a freshly composed session. Runtime also owns the current
content signature compatibility gate: mismatched or unavailable saved/current
content signatures block inspection restore plans and actual restore ports with
structured `slot.content` issues until concrete missing-content remap policy
exists. Current format 6 manifests also persist a content catalog summary beside
the content signature. None of these development versions have been released to
players or carry a backward-compatibility promise.

This is not the `SAVE_FORMAT.md` player-slot implementation. Capture is not one
committed tick, the two files are not one crash-atomic generation, and restore is
not complete deterministic continuation. Chunk shards, autosave rings, public
formats, compatibility, directory durability, missing-content policy, and player
UX all remain future persistence work.
The current world payload restore supports contained items when their
containing item exists in the same acyclic payload graph, carried/equipped
items when their owning creature exists in the same payload, installed item
placement data when the installed anchor/z/rotation is valid for the saved
world, and item-local reservation tokens whose claimant creatures and reserved
counts validate against the item payload.
App must not access this substrate or assemble restore authority from live
`World`, job systems, command queues, or Runtime-internal codec/store helpers.

While persistence is deferred:

- keep command replay data immutable and independent from live command objects;
- define where command replay decoding will live;
- keep existing export/restore APIs internal and test-only;
- document which state is authoritative vs derived;
- do not add document versions, player entry points, compatibility promises, or
  migration scope merely to extend the prototype;
- allow active replay/determinism work to reuse stable primitive hashing and
  command-record seams without treating that as save feature work.

## Existing Seams

`HumanFortress.Core.Commands.CommandReplayRecord`

- Immutable replay/save record for pending or executed commands.
- Carries `Tick`, `CommandId`, `CommandType`, optional Runtime command identity
  sequence, and payload bytes.
- Defensively copies payload bytes.
- Produced by `CommandQueue.GetPendingCommandRecords()`,
  `CommandQueue.GetExecutedCommandRecords()`, and
  `CommandQueue.GetReplaySnapshot()`.

`HumanFortress.Core.Commands.CommandQueueReplaySnapshot`

- Core-owned snapshot of pending and executed command replay records captured
  under one command-queue lock.
- Pending records are sorted by future execution order `(tick, queue sequence)`.
- The pending queue is lock-owned FIFO state; queue sequence is the replay
  tie-break, not concurrent collection enumeration order.
- Save/replay callers should use this instead of reading pending and executed
  queues separately when the two sides must match one manifest/checkpoint.

`HumanFortress.Core.Commands.CommandReplayJournalHashBuilder`

- Stable Core-owned hash for command replay record lists.
- Hashes records in explicit list order and includes tick, command id, command
  type, Runtime command identity sequence presence/value, and payload bytes.
- Used by Runtime checkpointing for both `commands.executed` and
  `commands.pending` sections.

`HumanFortress.Core.Commands.ICommandReplayFactory`

- Core contract for converting `CommandReplayRecord` back into an executable
  `ICommand`.
- Concrete implementations should live in Runtime, not Core.
- Runtime owns command implementations, command payload schemas, and any
  content-aware payload decoding.

`HumanFortress.Runtime.Commands.RuntimeCommandReplayFactory`

- Internal Runtime implementation of `ICommandReplayFactory`.
- Decodes current Runtime command families from versioned v1 payloads.
- Rejects unknown command types, unsupported payload versions, trailing bytes,
  malformed enum values, and reconstructed command id mismatches.
- Re-applies Runtime enqueue identity through `RuntimeIdentifiedCommand` when a
  replay record includes a command identity sequence.

`HumanFortress.Runtime.Commands.RuntimeCommandReplayRestorer`

- Runtime-owned replay harness for restoring command records into the active
  session command queue.
- Decodes the full batch before mutating `CommandQueue`, so corrupt replay
  records cannot partially wipe existing pending commands.
- Advances the session command identity cursor after successful restore so
  later Runtime enqueues cannot reuse restored identity sequence values.

`HumanFortress.Core.Determinism.ReplayHashBuilder`

- Stable primitive hash builder for replay checkpoints.
- Encodes primitives with explicit little-endian/length-prefixed rules.
- Does not know world schema; Runtime/Simulation snapshot hash builders decide
  which authoritative fields are appended.

`HumanFortress.Core.Determinism.RngReplayHashBuilder`

- Stable field-specific hash builder for Core-owned RNG stream snapshots.
- Hashes `RngStreamStateSnapshot` rows sorted by stream name and the explicit
  xoshiro state words.
- Provides the canonical encoding seam. Runtime session services now own a
  session `RngStreamManager`, reset materialized streams between sessions, and
  include the stream hash in Runtime replay checkpoints.

`HumanFortress.Simulation.Replay.OrdersReplayHashBuilder`

- Simulation-owned first field-specific replay hash builder.
- Hashes authoritative order designation state with stable sorting and
  `ReplayHashBuilder` primitive encoding.
- Keeps order field selection in Simulation instead of in App or tests.

`HumanFortress.Simulation.Replay.PlaceablesReplayHashBuilder`

- Simulation-owned field-specific replay hash builder for owned placeable state.
- Hashes placeable identity/location/footprint/source/effects/condition,
  construction-site material/progress state, door state, workshop settings,
  workshop queue entries, and the workshop queue identity counter.
- Excludes derived chunk furniture cells and cross-chunk external refs that can
  be rebuilt from authoritative owned placeables.

`HumanFortress.Jobs.Replay.TransportReplayHashBuilder`

- Jobs-owned first long-horizon job replay hash builder.
- Hashes pending transport requests, active transport jobs, backlog entries,
  backlog enqueue ticks, and transport scheduling hints.
- Excludes derived shard indexes and debug/stat rows; those are rebuilt or
  recomputed from authoritative request/executor state.

`HumanFortress.Jobs.Replay.MiningReplayHashBuilder`

- Jobs-owned long-horizon mining job replay hash builder.
- Hashes active mining jobs, backlog entries, deferred stairwell queue entries,
  reserved mining tiles, and recent completion highlight state.
- Excludes UI debug rows and mining stats rows; the executor snapshot owns the
  authoritative job state instead.

`HumanFortress.Jobs.Replay.CraftReplayHashBuilder`

- Jobs-owned long-horizon craft job replay hash builder.
- Hashes active craft jobs and executor backlog entries.
- Does not hash the craft planner outbox as save authority; save checkpoints
  should occur at the tick barrier, while workshop queue authority lives in
  `PlaceablesReplayHashBuilder`.

Construction jobs

- The current construction executor is scan-based and does not own a separate
  long-horizon active/backlog queue.
- Construction-site material/progress state lives on owned placeables and is
  already covered by `PlaceablesReplayHashBuilder`.

`HumanFortress.Simulation.Replay.WorldReplayHashBuilder`

- Simulation-owned aggregate replay checkpoint hash.
- Covers current world dimensions, existing chunk terrain tile primitives, item
  instances, creature instances, item/creature reservations, global stockpile
  zone configuration, owned placeables/workshop state, and active order
  designations.
- Excludes rebuildable stockpile chunk indexes, navigation caches, render/UI
  snapshots, diagnostics, and other derived state.
- Also exposes section-level hashes for terrain, items, creatures,
  reservations, stockpile zones, placeables, and orders so Runtime save
  manifests can name world slices without exposing live world internals.
- The builder is split by authoritative section: main orchestration plus
  terrain, item/creature entities, reservations, stockpile zones, and common
  primitive helper partials. Do not move these field lists into Runtime/App or
  collapse them back into one mixed replay hash file.

`HumanFortress.Simulation.Save.WorldSaveSnapshotBuilder`

- Simulation-owned minimal save summary for the active fortress world.
- Returns schema version, world dimensions, aggregate replay hash,
  section-level replay hashes, and section counts.
- Does not leave Simulation as an App-facing DTO and does not freeze the final
  chunk/tile payload format; it is a Runtime save-manifest input until full
  chunk-sharded save/load is implemented.

`HumanFortress.Runtime.Replay.RuntimeReplayCheckpointHashBuilder`

- Runtime-owned aggregate checkpoint hash builder.
- Returns `RuntimeReplayCheckpointData`, a Contracts DTO with aggregate hash,
  per-section hashes, and Runtime-authored checkpoint metadata.
- Sections the checkpoint so missing systems cannot be confused with present
  but empty authoritative state.
- Aggregates command replay journal hashes, `WorldReplayHashBuilder`,
  `RngReplayHashBuilder`,
  `TransportReplayHashBuilder`, `MiningReplayHashBuilder`, and
  `CraftReplayHashBuilder`.
- Command checkpointing includes both executed and pending command replay
  records so future-tick commands are not lost at a save barrier.
- Transport checkpointing includes both the pending request queue snapshot and
  executor active/backlog/scheduling state.
- Exposed through the Runtime session replay-checkpoint port; App does not
  assemble replay hashes or read replay implementation types directly.

`HumanFortress.Runtime.Save.RuntimeSaveManifestBuilder`

- Runtime-owned bridge from checkpoint data, content signatures, and the
  Simulation world save summary into a Contracts-owned manifest.
- Manifest sections include `world`, world subsection hashes/counts, `rng`,
  `commands.executed`, `commands.pending`, and current job replay sections.
- The `rng` section records the canonical RNG stream count in addition to the
  RNG hash, so a save document can validate payload rows against the manifest
  instead of only carrying an opaque summary.
- Validates that the world save summary aggregate hash matches the Runtime
  replay checkpoint world hash before returning a manifest.

`HumanFortress.Contracts.Runtime.Save.RuntimeSaveSnapshotDocumentData`

- Contracts-owned save document DTO containing the manifest plus pending and
  executed command replay records mapped to base64 payload document rows, plus
  the current `Contracts.Simulation.Save` world payload and primitive RNG
  stream state rows.
- Exposed through `CreateSaveSnapshotDocumentData()` on the Runtime session save
  snapshot port. Runtime keeps any Core `CommandReplayRecord` and
  `RngStreamStateSnapshot` bridging inside its internal save package helpers.
- The Runtime session save snapshot port also owns first-pass directory
  operations for writing `runtime_snapshot.json`, validating a save snapshot
  directory, restoring pending commands from that directory, restoring a world
  payload into a freshly composed Runtime session, and full staged restore of
  world payload + RNG streams + pending commands.
- Intended as the future App save-slot IO input; App should pass slot
  directories through its narrow Runtime save access and should not query live
  Runtime/Simulation authority or Runtime-internal file helpers to assemble it.

`HumanFortress.Contracts.Runtime.Save.RuntimeSaveSlotManifestData`

- Contracts-owned first-pass slot manifest DTO for the current save directory.
- `RuntimeSaveSlotFormat` names the current slot kind, manifest filename, and
  Runtime snapshot document filename.
- Records slot format version, Runtime snapshot format version, engine build,
  Runtime-authored snapshot metadata, checkpoint/world hashes, section count,
  RNG stream count, and executed/pending command row counts.
- `RuntimeSaveSlotCompatibilityData` is the Contracts-owned result shape for
  current/future migration UI and diagnostics. Runtime owns the policy that
  fills it.
- `RuntimeSaveSlotContentCompatibilityData` is the Contracts-owned content
  binding result nested in inspection. Runtime fills it from saved and current
  content signatures, reports whether content can be bound, and emits
  structured `slot.content` blockers until a real missing-content/remap policy
  exists.
- The content compatibility read model carries both the saved catalog summary
  from format 6 manifests, when present, and a Runtime-authored current catalog
  summary: sorted material names, terrain kind names, construction ids,
  recipe ids, geology ids, and zone ids. This supports future
  missing-content/remap diagnostics without making App compare catalog ids or
  treating either summary as remap authority. Historical migrated v5 saves may
  still have an unavailable saved catalog summary.
- Content mismatches are exposed both as legacy display strings and as
  structured `RuntimeSaveContentCompatibilityDifferenceData` rows that name the
  mismatch kind, field, saved/current values, and whether catalog key-level
  diagnostics are available. Current format 6 saves can report saved-only and
  current-only key lists when both saved/current catalog summaries are
  available; historical migrated saves without a saved catalog keep those key
  lists empty and still fail closed instead of inferring per-id remaps.
- `RuntimeSaveSlotInspectionData` is the Contracts-owned directory inspection
  read model for future save UI/debug surfaces: it carries the Runtime
  validation result, compatibility classification, content compatibility,
  migration plan, restore plan, and readable slot manifest when available.
- `RuntimeSaveSlotMigrationPlanData` is the Contracts-owned migration read
  model nested in inspection. Runtime fills it with whether migration is
  required, whether the current runtime has a registered transform path, stable
  required transform ids such as `slot:0->1`, and blocking issues with missing
  transform ids. Slot-format-only migration from legacy/current
  `runtime_snapshot.json` directories to current slot manifests is supported
  through `slot:0->1`; Runtime save format 4 can migrate through
  `runtime_snapshot:4->5` and `runtime_snapshot:5->6` only when the final
  current-format document validates, which allows empty mining checkpoint state
  and rejects non-empty mining checkpoint state with no mining payload. Runtime
  save format 5 can migrate through `runtime_snapshot:5->6`, preserving an
  unavailable saved catalog for historical documents that lack the format 6
  catalog payload.
- `RuntimeSaveSlotMigrationResultData` is the Contracts-owned result shape for
  Runtime-owned migration execution attempts. Current compatible slots return
  a successful no-op result, `slot:0->1` applies a current manifest into a
  target directory, registered runtime snapshot transforms apply into a target
  directory, and unsupported format paths return structured `slot.migration`
  issues until concrete transforms are registered.
- `RuntimeSaveSlotRestorePlanData` is the Contracts-owned restore-plan read
  model nested in inspection. Runtime fills it with the currently allowed
  pending-command/world/full restore modes and blocking issues so UI/debug
  surfaces do not infer loadability from raw validation fields. `jobs.transport`,
  `jobs.mining`, and `jobs.craft` are restorable only when the document
  contains Runtime-verified payload rows that map back to Jobs-owned snapshots.
  Empty Jobs checkpoint sections do not prevent full world + RNG +
  pending-command restore.
- It is a directory/index contract, not the final chunk-sharded save catalog or
  migration table.

`HumanFortress.Runtime.Save.RuntimeSaveSlotCompatibilityPolicy`

- Runtime-owned compatibility classifier for the current manifest/document
  slice.
- Current slot/runtime snapshot versions are readable. Legacy directories that
  contain a current-format `runtime_snapshot.json` but lack `slot_manifest.json`
  are reported as `MigrationRequired` with source slot format `0`; older
  runtime snapshot documents are also reported as migration-required and can
  migrate only through registered adjacent transforms such as
  `runtime_snapshot:4->5` and `runtime_snapshot:5->6`. Future versions,
  unknown slot kinds, unsupported snapshot document names, and older
  unregistered runtime snapshot schemas fail closed.
- Validation emits structured `slot.compatibility` issues, so App does not need
  to inspect manifest internals or decide whether a slot is loadable.

`HumanFortress.Runtime.Save.RuntimeSaveSlotContentCompatibilityPolicy`

- Runtime-owned content binding classifier for the current manifest/document
  slice.
- It compares the saved content signature against the active Runtime content
  signature: content version, aggregate content hash, material content hash,
  and catalog-shape counts for material, terrain, construction, recipe,
  geology, and zone data.
- Runtime attaches the saved catalog summary from the manifest, when available,
  and a sorted current catalog key summary to the Contracts read model so
  diagnostics can explain which saved/current content keys differ. The saved
  manifest remains an exact signature/count gate; catalog summaries are
  diagnostic payloads and do not implement remapping.
- Runtime-authored structured difference rows are the future remap diagnostic
  seam. They can tell UI/debug surfaces which signature or catalog-count field
  failed and whether saved/current catalog keys are available. Format 6 saves
  can include saved-only/current-only key lists; migrated historical saves with
  no saved catalog continue to report saved key sets as unavailable.
- Matching signatures allow restore plans and restore ports to proceed. Missing
  saved/current signatures or mismatches return `slot.content` blockers and
  `RequiresMissingContentPolicy: true`.
- This is the current fail-closed seam for future placeholder/remap behavior.
  App/save UI may display the Contracts DTO, but it must not compare content
  hashes or decide whether a save can be loaded with substituted content.

`HumanFortress.Runtime.Save.RuntimeSaveSlotMigrationPlanBuilder`

- Runtime-owned migration read-model builder for the current manifest/document
  slice.
- A compatible current slot reports no migration requirement. An older slot
  reports `RequiresMigration`, asks the Runtime migration transform registry for
  required transform ids, returns `CanMigrate: true` only when the registry can
  satisfy every required transform, and otherwise emits a structured
  `slot.migration` blocking issue. The current concrete transform is
  `slot:0->1` for rebuilding current slot metadata around a current Runtime
  snapshot document, plus registered adjacent runtime snapshot transforms such
  as `runtime_snapshot:4->5` and `runtime_snapshot:5->6` when the final
  current-format payload validates.
- This is the read-model builder, not the executor. It exists so App/save
  UI/debug surfaces can display migration state without comparing format
  versions or guessing load policy.

`HumanFortress.Runtime.Save.RuntimeSaveSlotMigrationTransformRegistry`

- Runtime-owned transform-path seam for save-slot and runtime snapshot format
  migrations.
- The current registry registers `slot:0->1`, `runtime_snapshot:4->5`, and
  `runtime_snapshot:5->6`.
  `slot:0->1` reads an existing current-format `runtime_snapshot.json` and
  writes a new target directory through Runtime's normal slot writer so
  `slot_manifest.json` is rebuilt from the document. Runtime snapshot
  transforms currently bump adjacent document format versions, then validate
  the resulting current-format document before writing; this keeps non-empty
  mining checkpoint state without mining payloads blocked instead of silently
  dropping state, and keeps historical v5 missing saved-catalog payloads
  explicit as unavailable rather than fabricating remap data. The same
  registry still centralizes transform-id generation, coverage checks,
  missing-transform diagnostics, and future transform application entrypoints
  so runtime snapshot schema migrations can be added without moving
  version/path logic into App or inspection callers.
- Required transform ids are returned in Runtime execution order, not
  lexicographic order: adjacent runtime snapshot document transforms first,
  followed by slot-manifest rebuild when a legacy slot manifest is present.
  Migration results report the transforms actually applied in that same order.

`HumanFortress.Runtime.Save.RuntimeSaveSlotMigrator`

- Runtime-owned migration execution seam for save directories.
- It preflights through `InspectDirectory(...)`, returns a successful no-op
  `RuntimeSaveSlotMigrationResultData` for current compatible slots, applies
  `slot:0->1` into a target directory for legacy snapshot-only/current-document
  slots, applies registered runtime snapshot transforms such as
  `runtime_snapshot:4->5` and `runtime_snapshot:5->6`, and returns structured
  migration issues for unsupported or non-validating runtime snapshot format
  paths.
- It is the only place future transform application should enter; App/save UI
  should pass source/target directories through Runtime ports and display the
  Contracts result.

`HumanFortress.Runtime.Save.RuntimeSaveSlotRestorePlanBuilder`

- Runtime-owned loadability classifier for the current supported restore modes.
- A valid, compatible current slot can advertise pending-command and world
  restore support independently only when the saved content signature can bind
  to the current Runtime content. `CanRestoreFull` is blocked when non-empty or
  uncounted job checkpoint sections have no matching Runtime-verified payload
  rows and supported Jobs-owned restorer.
- Malformed, migration-required, future, content-incompatible, or incomplete
  slots return a blocked plan with structured issues.
- This is not a restore executor and not a migration transform. Actual load
  still enters through Runtime restore ports and their preflight path.

`HumanFortress.Runtime.Save.RuntimeSaveJobStateRestorePolicy`

- Runtime-owned fail-closed policy for matching Jobs-owned replay hashes with
  Runtime-verified job-state payload rows and Jobs-owned restore seams.
- The same policy is used by save-slot inspection and the full-restore Runtime
  port, so a slot with a non-empty job checkpoint section is not advertised or
  executed as a full restore unless matching job-state payloads and restore
  logic are supported. Transport, mining, and craft are the currently supported
  job slices:
  the Runtime save document carries pending transport requests, transport
  active jobs/backlog/scheduling hints, mining active/backlog/deferred/
  reserved/recent-completion jobs, and craft active/backlog jobs, while Jobs
  owns the restore seams.

`HumanFortress.Runtime.Save.RuntimeSaveSnapshotDocumentStore`

- Runtime-owned directory store for the current save vertical slice.
- Writes `runtime_snapshot.json` and `slot_manifest.json` atomically enough for
  the current local-file helper, while keeping codec and validation behavior
  inside Runtime.
- The store is split by responsibility: the main file keeps public store
  entrypoints, the inspection partial owns slot inspection/read-model assembly,
  and the IO partial owns unchecked file reads, durable writes, and structured
  directory failures.
- `InspectDirectory(...)` is the canonical Runtime-owned read path for a slot
  summary. It reads the document and manifest, applies the compatibility policy,
  computes content compatibility, migration plans, and restore plans, and
  returns a Contracts inspection DTO without exposing the Runtime codec/store
  helpers.
- `ValidateDirectory(...)` validates the document plus slot manifest and returns
  structured `snapshot.document`, `slot.manifest`, or `slot.compatibility`
  issues by reusing the inspection path instead of requiring App to inspect
  individual files.
- Restore paths preflight the directory through the same Runtime validation
  seam before reading the unchecked document payload. Pending-command,
  world-payload, and full staged restore ports also enforce the Runtime content
  compatibility gate at execution time, so inspection remains guidance rather
  than a second load authority.

`HumanFortress.Contracts.Simulation.Save.WorldSavePayloadData`

- Contracts-owned minimal world payload DTO for the current Runtime document.
- Contains schema/version, world dimensions, aggregate world replay hash,
  section hashes/counts, chunk coordinates, per-cell terrain tile fields, and
  payload rows for the currently supported world authority slices: ground,
  contained, carried, equipped, and installed item instances, creature
  instances, global item/creature reservations, stockpile zone definitions,
  owned placeables/workshop state, and active order designations.
- The current restore implementation supports terrain, ground item instances,
  contained item instances with payload-local acyclic container references,
  carried and equipped item instances with payload-local creature owners,
  installed item placement data with valid anchors, item-local reservation
  tokens with valid claimant/count rows, creature instances, global
  reservations, stockpile zones, owned placeables/workshops, and active orders.

`HumanFortress.Simulation.Save.WorldSavePayloadBuilder` /
`HumanFortress.Simulation.Save.WorldSavePayloadRestorer`

- Simulation-owned builder/restorer for authoritative terrain chunk payloads,
  ground, contained, carried, equipped, and installed item instances, creature instances,
  global reservations, stockpile zones, owned placeables/workshop state, and
  active order designations.
- The restorer creates a new Simulation `World`, writes tiles through chunk
  write APIs, restores supported runtime authority through Simulation managers,
  recomputes the aggregate `WorldReplayHash`, and only succeeds when the
  restored hash matches the saved hash.
- Runtime composes the restored `World` through the normal session factory so
  content, navigation, host systems, command queues, and caches are reset at the
  Runtime boundary rather than in App code.

`HumanFortress.Runtime.Save.RuntimeSaveSnapshotDocumentCodec`

- Runtime-owned JSON codec for the document DTO.
- Provides one serialization policy for future App slot IO without making App
  know Core command replay record internals.
- Validates RNG and command document rows on serialization/deserialization,
  including RNG stream names/duplicates, command type, payload base64, payload
  length, and Runtime command identity sequence values.

`HumanFortress.Runtime.Save.RuntimeSaveSnapshotDocumentCommandMapper`

- Runtime-internal mapper from document command rows back to
  `CommandReplayRecord` lists.
- Keeps future replay restore decoding in Runtime rather than making App convert
  document DTOs to Core command records.

`HumanFortress.Runtime.Save.RuntimeSaveSnapshotDocumentRngMapper` /
`HumanFortress.Runtime.Save.RuntimeSaveSnapshotRngRestorer`

- Runtime-internal bridge from primitive document RNG rows back to Core-owned
  `RngStreamStateSnapshot` values.
- Document validation recomputes `RngReplayHashBuilder` over the rows and
  checks both manifest checkpoint hash/count and the `rng` manifest section.
- Runtime full restore clears materialized session streams and restores the
  document rows after world composition resets session services, keeping RNG
  restore out of App and out of the Simulation world payload.

`HumanFortress.Runtime.Save.RuntimeSaveSnapshotDocumentStore`

- Runtime-internal first-pass file helper for writing/reading the assembled
  `runtime_snapshot.json` document. It is intentionally reached through the
  Runtime session save snapshot port rather than App/UI code.
- Writes through a temp file and flushes the temp stream before replacing the
  target.
- Runs Runtime document validation on write and read, so malformed manifest
  hash/count metadata is rejected at the file boundary.
- Directory validation/restore ports convert missing/corrupt document reads into
  structured `snapshot.document` validation issues instead of exposing IO/JSON
  exceptions as App-facing load failures.
- Full restore composes the supported world payload through a staging Runtime
  session/services pair, restores supported transport/mining/craft job payload
  rows, restores RNG streams, then restores pending command records on the
  staging services. The active Runtime session is replaced only after every
  supported restore slice succeeds, so a late job/RNG/command restore failure
  does not leave the current session half-restored. Executed command records
  remain document/journal history and are not requeued as pending work.
- This is not the full `SAVE_FORMAT.md` slot implementation; chunk shards,
  autosave rings, migration transforms, directory fsync policy, and full load
  orchestration remain future persistence work.

`HumanFortress.Contracts.Runtime.Snapshots.SimulationSnapshotMetadata`

- Version/tick metadata for aggregate Runtime read-model DTOs.
- `SimulationFrameRenderData` and `SimulationUiOverlayFrameData` carry the
  current snapshot schema version and Runtime-authored simulation tick.
- App must not author Runtime snapshot ticks from UI frame counters.

`CommandQueue.RestoreCommands(...)`

- Restores executable commands to the pending queue for replay.
- Does not add restored commands to executed history until they actually run.
- Validates restore input before clearing existing pending state.

## Command Replay Flow

Near-term replay should be:

```text
Runtime command enqueue
  -> CommandQueue
  -> command remains pending until due
  -> CommandQueue.GetReplaySnapshot()
  -> pending command replay persistence where needed
  -> due command executes at tick boundary
  -> executed command replay log persistence

Replay load
  -> read pending CommandReplayRecord entries
  -> Runtime replay restorer decodes full record batch
  -> CommandQueue.RestoreCommands(decoded commands)
  -> Runtime replay restorer advances session command identity cursor
  -> tick pipeline executes restored commands normally
```

Do not persist live `ICommand` instances. They are executable objects, not save
data. Save files should store pending/executed replay records and future
authoritative world snapshots.

## Full Save Format Relationship

`SAVE_FORMAT.md` describes the eventual chunk-sharded persistent layout. The
first implementation should not attempt all of it. The staged path should be:

1. Command replay log MVP.
2. Small deterministic replay harness using command records.
3. Minimal authoritative world snapshot for fortress mode.
4. Save/load round-trip hash test.
5. Atomic slot layout, autosave ring, migration, and UI.

## Persisted vs Rebuilt

Persist authoritative state only:

- world seed, generation settings, and content/registry signatures;
- tile base/terrain and gameplay-affecting tile state;
- item, creature, placeable, zone, stockpile, order, reservation, and job state;
- command replay records where the run needs replay/audit;
- RNG stream/counter state.

Do not persist derived state:

- navigation caches;
- render/UI snapshots;
- debug overlays;
- transient UI selection, camera, hover state, and open popups;
- stockpile cached indexes when they can be rebuilt from authoritative items and
  zones;
- diagnostics logs as replay authority.

## Module Ownership

`Core`

- Owns command queue primitives, replay record contracts, and the replay factory
  interface.
- Does not know concrete Runtime command types.

`Runtime`

- Owns concrete command replay factory/registry implementations.
- Owns current session-level save/replay orchestration, including directory
  validation/inspection, slot compatibility policy, restore-plan read models,
  supported world payload restore, supported transport/mining/craft job payload
  restore, RNG stream restore, and pending-command replay restore.
- Rebuilds derived services such as navigation after load.

`Simulation`

- Owns authoritative world state DTO/extraction/application helpers for the
  current supported terrain/entity/reservation/stockpile/placeable/order world
  payload slices.
- Does not parse save slots or content packs directly.

`Content`

- Owns content pack loading and registry signatures used by save validation.

`App`

- Owns save/load UI, slot selection, and user-facing failure display.
- Does not serialize live Simulation objects directly.

## Near-Term Implementation Guardrails

- Add command replay decoders for new command families one command family at a time.
- Payload decoders must validate length, version, and known ids before creating
  commands.
- Runtime command payloads use `RuntimeCommandPayload.CurrentVersion`; format
  changes must either keep the decoder backward-compatible or intentionally
  bump save/replay migration policy.
- Unknown command type should fail the replay load with a structured error, not
  silently skip.
- Content ids in command payloads remain strings; runtime handles must not enter
  replay records or save files.
- Golden replay tests should compare deterministic snapshot hashes from
  `ReplayHashBuilder`-backed authoritative snapshots after fixed tick counts
  before expanding the full save-slot and migration layer.
- Runtime aggregate frame/read-model DTOs should carry Runtime-authored
  snapshot and publication metadata. App UI frame counters are presentation
  state, not simulation checkpoint ticks or frame identity.

## Open Work

- Broader authoritative world snapshot DTOs and field-specific hash builders
  for future systems not yet in `WorldReplayHashBuilder` or Jobs-owned replay
  hashes.
- Connect future gameplay systems that need mutable randomness to the
  session-owned RNG streams instead of creating local RNG cursors.
- Full `SAVE_FORMAT.md` alignment: chunk-sharded slot catalog, migration
  transforms/table, autosave ring policy, directory fsync policy, and broader
  payload/replay scenario sections beyond the
  current `slot_manifest.json` + `runtime_snapshot.json` slice.
- Save/load migration and concrete placeholder/remap policy for missing content.
