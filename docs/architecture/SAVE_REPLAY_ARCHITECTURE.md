# Save And Replay Architecture

Updated: 2026-07-03
Status: current implementation boundary plus staged plan

This document describes how HumanFortress should approach persistence from the
current refactor state. `SAVE_FORMAT.md` remains the long-term on-disk format
target. This file is the near-term architecture bridge: what exists now, what
must not be persisted yet, and which seams should be added before a full save
loader is implemented.

## Current Decision

Do not implement full save/load yet.

The module boundaries are now mostly stable, but authoritative world schema,
stockpile maintenance, movement ownership, and long-horizon job state are still
being hardened. A full save implementation now would freeze too much unstable
internal shape into a compatibility burden.

The near-term goal is narrower:

- keep command replay data immutable and independent from live command objects;
- define where command replay decoding will live;
- expose Runtime-authored save manifests/packages without letting App assemble
  authority from `World`, job systems, or `CommandQueue`;
- document which state is authoritative vs derived;
- prepare deterministic replay tests before writing chunk/world save files.

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

`HumanFortress.Contracts.Simulation.Save.WorldSavePayloadData`

- Contracts-owned minimal world payload DTO for the current Runtime document.
- Contains schema/version, world dimensions, aggregate world replay hash,
  section hashes/counts, chunk coordinates, per-cell terrain tile fields, and
  payload rows for the currently supported world authority slices: ground item
  instances, creature instances, global item/creature reservations, stockpile
  zone definitions, owned placeables/workshop state, and active order
  designations.
- The current restore implementation supports terrain, ground item instances,
  creature instances, global reservations, stockpile zones, owned
  placeables/workshops, and active orders.
  If carried, contained, equipped, installed, or item-local reservation-token
  state is present, Runtime returns structured `world.payload` restore issues
  instead of silently dropping unsupported authoritative state.

`HumanFortress.Simulation.Save.WorldSavePayloadBuilder` /
`HumanFortress.Simulation.Save.WorldSavePayloadRestorer`

- Simulation-owned builder/restorer for authoritative terrain chunk payloads,
  ground item instances, creature instances, global reservations, stockpile
  zones, owned placeables/workshop state, and active order designations.
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
- Full restore currently composes the supported world payload through the normal
  Runtime session factory, restores RNG streams, then restores pending command
  records. Executed command records remain document/journal history and are not
  requeued as pending work.
- This is not the full `SAVE_FORMAT.md` slot implementation; chunk shards,
  autosave rings, migrations, directory fsync policy, and full load
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
- Owns session-level save/replay orchestration when this is implemented.
- Rebuilds derived services such as navigation after load.

`Simulation`

- Owns authoritative world state DTO/extraction/application helpers when world
  save begins.
- Does not parse save slots or content packs directly.

`Content`

- Owns content pack loading and registry signatures used by save validation.

`App`

- Owns save/load UI, slot selection, and user-facing failure display.
- Does not serialize live Simulation objects directly.

## Near-Term Implementation Guardrails

- Add command replay decoders one command family at a time.
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
  before full save/load is attempted.
- Runtime aggregate frame/read-model DTOs should carry Runtime-authored
  snapshot metadata. App UI frame counters are presentation state, not
  simulation checkpoint ticks.

## Open Work

- Broader authoritative world snapshot DTOs and field-specific hash builders
  for future systems not yet in `WorldReplayHashBuilder` or Jobs-owned replay
  hashes.
- Connect future gameplay systems that need mutable randomness to the
  session-owned RNG streams instead of creating local RNG cursors.
- Save slot manifest implementation aligned with `SAVE_FORMAT.md`.
- Save/load migration and placeholder policy for missing content.
