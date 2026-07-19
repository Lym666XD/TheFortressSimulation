# Live Entity Identity

Updated: 2026-07-11
Status: accepted for live item and creature authority
Decision scope: Simulation runtime identity, compact diff handles, and stale-handle behavior

## Context

Items and creatures use a full 128-bit GUID as their authoritative identity.
Diff targets and several indexes also carry `EntityKey`, the first 64 bits of
that GUID. The projection is deterministic and useful as a compact in-process
handle, but it is not injective: two different GUIDs can produce the same key.

The former item and creature indexes assigned entries with dictionary indexer
syntax. A projection collision therefore rebound an existing key without
rejecting the new GUID. Restore also cleared the indexes before rebuilding them,
and reset the deterministic allocator sequence to the restored row count. Those
behaviors allowed a stale compact handle to resolve to another entity and made
identity reuse possible after delete or restore.

Player save/load and persistence compatibility are deferred. This decision does
not add a persistence field, schema version, migration, or public save contract.

## Decision

1. The full GUID is the canonical identity. `EntityKey` is a compact live handle,
   never an independent identity source.
2. Each item and creature manager owns one `LiveEntityIdentityIndex`. It records:
   the current key-to-GUID mapping, current full GUIDs, historical key ownership,
   and retired GUID tombstones.
3. A key may bind to only one full GUID during a manager lifetime. Removing an
   entity removes its live mapping but retains its historical owner. The key
   cannot be rebound to a different GUID.
4. A retired full GUID cannot be created again in the same manager lifetime.
   This prevents both full-GUID stale references and projected stale handles
   from aliasing a new incarnation.
5. Spawn, explicit item split, and snapshot replacement validate the full GUID
   and projected key before changing `_instances`, legacy indexes, position
   indexes, or source stack quantities. Invalid batches leave the prior live
   collection intact.
6. Restore replacement validates the complete incoming identity set before it
   retires omitted entities or rebuilds manager indexes. Duplicate GUIDs,
   pairwise projection collisions, collisions with historical owners, and reuse
   of retired GUIDs fail closed.
7. Item and creature allocator sequences are monotonic high-water marks. Delete
   never decrements them; restore uses `max(current, restored count)` rather than
   assigning the restored count. Generated candidates are also checked against
   the identity ledger and skipped if they would reuse a GUID or key.
8. The old 32-bit `EntityId` remains a compatibility lookup only. Its index is a
   stable GUID-sorted multimap, so 32-bit collisions do not select by insertion
   order. New authoritative targeting uses the collision-checked 64-bit key.

## Generation And Handle Lifetime

A `World` and its manager instances define one live identity generation. Compact
entity keys are valid only while that generation is active. Runtime session/world
replacement creates new managers and is the generation boundary; callers must
discard commands, selections, cached lookups, and snapshots belonging to the old
Runtime generation.

Within a generation, tombstones make stale lookup fail with no result instead of
aliasing another entity. Across generations, Runtime generation fencing is the
authority; the compact 64-bit value alone does not prove that a handle belongs to
the active session.

## Determinism And Replay

The allocator sequence, historical key owners, and retired GUIDs affect future
command acceptance and generated identity. They are authoritative state. Item
and creature replay sections therefore append canonical identity-ledger rows:
historical bindings sort by entity key/GUID and retired GUIDs sort by GUID.
Two Worlds with identical live rows but different identity history produce
different section hashes. A rejected duplicate, projection collision, or stale
handle reuse changes neither the ledger nor the replay hash.

The deterministic allocator remains reproducible because candidate generation
uses its existing scoped monotonic sequence. Collision skipping is deterministic
for the same manager history.

## Consequences

- A 64-bit projection collision is detected rather than silently overwriting an
  existing entity lookup.
- Tombstone memory grows with entity churn for the lifetime of a World. This is
  intentional correctness state. Measure it before considering compaction.
- In-place restore cannot resurrect an entity that was already retired in the
  same World generation. The experimental restore seam must replace the World
  generation when resurrection semantics are eventually required.
- Persistence must not serialize the current ledger opportunistically. A future
  save/load milestone must define session generation and allocator continuation
  as a complete format decision.

## Rejected Alternatives

- **Last writer wins:** fast but aliases stale handles and corrupts entity-targeted
  diffs.
- **Trust 64 bits as unique:** collision probability is not a correctness proof,
  and tests can construct collisions directly.
- **Reset sequence from row count:** row count is not an allocator high-water
  mark and reuses deterministic GUIDs after deletion.
- **Expand save DTOs now:** persistence is explicitly deferred and partial format
  growth would create a false compatibility promise.
- **Process-global identity registry:** identity authority belongs to an isolated
  World/session, not global mutable state.

## Executable Evidence

`LiveEntityIdentityRegressionTests` constructs different GUIDs with identical
64-bit projections and verifies:

- item split and item/creature restore reject duplicate GUIDs and collisions
  before live/index mutation;
- failed identity mutations preserve the World replay hash;
- removed keys do not alias a new GUID;
- allocator output does not reuse pre-restore identities after an empty restore;
- identical live rows with different allocator/tombstone history have different
  stable item and creature section hashes;
- the surviving full GUID and all compact-key lookups remain consistent.
