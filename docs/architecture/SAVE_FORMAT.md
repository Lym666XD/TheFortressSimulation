SAVE_FORMAT.md — v1 (Normative, Updated)
id: save.format.v1
status: normative
owner: engine/persistence
last_updated: 2025-09-15
implementation_note: Current staged implementation guidance lives in SAVE_REPLAY_ARCHITECTURE.md.
targets:
  - Fortress/Colony mode (persistent)
  - Adventure mode (player-only personal map memory)
guarantees:
  - Deterministic restore (PRNG streams, stable orderings)
  - Crash-proof IO (per-file try–catch, atomic replace, hashes, autosave ring)
  - Content rebind by string IDs (no runtime handles in saves)
formats:
  - Payloads: MessagePack → Zstd → *.mpkz (little-endian)
  - Manifests/indices: JSON UTF-8
policies:
  - Persistent instances: player_fort, npc_settlement, dungeon
  - Ephemeral (not stored): wilderness, encounter
  - Personal map memory with decay: **adventure player only** (NPCs have none)
  - NPC knowledge: KnownLocations, SightingLog, Rumors → gradually merge into Faction Memory

0) Scope & Decisions

Chunk-sharded saving: world state is sharded by (cx, cy, cz); only dirty chunks are written.

Save-at-barrier: serialization runs at end-of-tick barrier against a read-only snapshot.

Instance policy

player_fort, npc_settlement, dungeon → Persistent (saved as delta vs seed baseline).

wilderness, encounter → Ephemeral (no local map saved on exit).

Knowledge policy

Adventure player: Personal Map Memory (decays).

NPCs: no map memory; they keep only KnownLocations / SightingLog / Rumors (lightweight), which merge into Faction Memory.

Networks (power/fluid connectivity) are rebuilt on load (not persisted in v1).

No WAL; consistency via atomic file replace and autosave ring.

1) On-Disk Layout (Authoritative)
/Saves/<slotId>/
  manifest.json                 # save header (human-readable, commit point)
  registries.sig.json           # packset signature & registry hash
  world.meta.mpkz               # world/factions/sites, world-actors (roaming NPCs), global seeds
  rng/streams.mpk               # PRNG streams & cursors (system/chunk scoped)
  chunks/
    <cx>_<cy>_<cz>.mpkz         # per-chunk authoritative snapshot
  jobs/scheduler.mpkz           # job queues, reservations, in-flight tasks
  mailboxes/<cx>_<cy>_<cz>.mpkz # undelivered envelopes ordered by (tick,sender,seq)
  instances/
    <wx>_<wy>/
      manifest.json             # per-worldtile instance registry & policies
      deltas.mpkz               # worldtile-level structural deltas (seed-agnostic)
      <type>__<seed>.mpkz       # persistent instance delta (npc_settlement / player_fort / dungeon)
  factions/
    memory.mpkz                 # shared faction memory (POIs, sightings, rumors)
  artifacts/
    ledger.mpkz                 # **single source of truth** for unique artifacts (locations/holders)
  player/
    adventure_memory.mpkz       # adventure player personal map memory (with decay)
  autosaves/
    autosave_0.zip … autosave_4.zip


Atomicity: all writes are *.tmp → fsync → rename() replace; manifest.json update is the commit.

2) Files & Payloads (Normative)
2.1 manifest.json
{
  "format_version": 1,
  "engine_build": "0.5.0",
  "created_utc": "2025-09-15T10:21:00Z",
  "last_tick": 182340,
  "packset_signature": ["base@1.0@sv4","dlc_age_iron@1.1@sv4"],
  "registry_hash": "sha256:…",
  "world_seed": 123456789,
  "fortress_id": "fort_alpha",
  "player_mode": "fortress",
  "gen": 37
}

2.2 registries.sig.json

Contains pack list (id/version/schema_version/hash) and counts by kind; used to bind string IDs and verify content set.

2.3 world.meta.mpkz

Sites index (global directory):

Sites[] = { site_id, wx, wy, type:"player_fort|npc_settlement|dungeon", owner_faction_id, seed, created_tick, tags[] }


Factions state (diplomacy, relations, trade stubs) — lightweight.

WorldActors (roaming/ambulating NPCs not tied to a map instance):

WorldActors[] = {
  guid, faction_id, kind, wx, wy, path_plan?, cargo_summary?,
  known_locations?, sightings?, rumors?
}


(NPCs have no personal map memory. These lightweight knowledge fields trickle into faction memory.)

Optional global world flags/seeds.

2.4 chunks/<cx>_<cy>_<cz>.mpkz — ChunkSnapshot

Authoritative state for terrain/items/buildables/actors/fields/reservations, mailbox cursor, PRNG cursors, and local string table.

2.5 jobs/scheduler.mpkz

Stable job queues, running jobs with progress %, worker GUIDs, reservations, paused reasons.

2.6 mailboxes/<chunk>.mpkz

Ordered envelopes {tick, sender_chunk, seq, payload}; only undelivered envelopes persisted. Replay starts at mailbox_cursor+1.

2.7 instances/<wx>_<wy>/…

manifest.json tracks seeds and policies per instance type.

Persistent types produce <type>__<seed>.mpkz storing delta vs baseline (terrain edits, buildables, items, 驻军/居民 NPC actors, etc.).

Ephemeral types (wilderness, encounter) do not produce instance files.

2.8 factions/memory.mpkz — Faction Memory

Shared memory across a faction:

FactionMemory {
  known_locations: [ POI{ id, kind, wx, wy, local_hint?, first_seen_tick, last_confirm_tick, tags[] } ],
  sightings: [ Sighting{ kind, target_id, wx, wy, tick } ],
  rumors: [ Rumor{ source, topic_id, wx?, wy?, confidence:0..1, expires_tick } ]
}


NPC knowledge merges here over time (append & dedupe).

2.9 artifacts/ledger.mpkz — Artifacts Ledger (SoT)

Single source of truth for unique artifacts, independent of where items are mirrored:

ArtifactEntry = {
  artifact_id, name?, origin?,
  holder: OneOf[
    ActorRef{actor_guid},
    ChunkRef{cx,cy,cz, tile, container_guid?},
    InstanceRef{wx,wy,type, container_guid?},
    WorldTileStash{wx,wy, note?}
  ],
  last_update_tick
}


On any transfer/exit, ledger updates once.

Ephemeral map exit: if an artifact is left behind, ledger moves it to WorldTileStash{wx,wy}; thus artifacts never vanish.

Conflict resolution: if two mirrors claim same artifact_id, the ledger wins by (last_update_tick, deterministic tiebreaker); the losing mirror is downgraded to a normal item or removed and an error is logged.

Ledger participates in the atomic commit (staging → manifest) with related chunk/instance writes to avoid tearing.

2.10 player/adventure_memory.mpkz

Adventure player’s personal map memory (with decay) and POIs; NPCs never use this.

2.11 RNG streams

Counter-based streams per (system, chunk) and (system, world) with {stream_id, counter}; restores determinism across thread counts.

3) Serialization Conventions (Normative)

IDs only: all cross-refs by string ID (materials/items/buildables/creatures/factions/recipes).

Local string tables allowed to shrink payloads; rebinding still uses the full IDs.

Stable ordering: maps serialized by lexicographic keys; arrays sorted by domain key (chunk→tile→system→id).

Numeric hygiene: no NaN/Inf; floats quantized/avoided in authoritative state; timestamps only in manifests.

4) Persist vs Rebuild (Authoritative)

Persist

Terrain & fluids depth; gameplay-affecting autotile flags.

Items & containers (id/material/qty/quality/position/container).

Buildables (orientation/material/state bits/fuel/durability).

Actors (creatures/pawns): body_plan_id, HP by slot, wounds, statuses, inventory & equipment refs, AI microstate.

Stockpile areas (geometry + filters).

Job Scheduler (queues, progress, reservations).

Mailboxes (undelivered envelopes only).

RNG cursors.

FactionMemory.

Adventure player Personal Map Memory (only the player).

Persistent instances (npc_settlement/dungeon/player_fort) deltas.

Artifacts Ledger (SoT).

WorldActors (roaming NPCs at world layer) in world.meta.

Do not persist (rebuild)

All caches & indexes (spatial hashes, path caches, stockpile cached lists, render snapshots, networks connectivity).

View cones/FoV, AI cached blackboards.

5) Region Instance Policy (Normative)
5.1 InstanceKey
{ wx:i32, wy:i32, type:"player_fort|npc_settlement|dungeon|wilderness|encounter", seed:u64 }

5.2 Lifecycle (RIM — Region Instance Manager)

Dormant → Active on enter: baseline generated from (world_seed, wx, wy, type), then worldtile deltas applied.

Exit:

Persistent types → write delta to <type>__<seed>.mpkz. NPC residents/guards are saved here as actors[].

Ephemeral types (wilderness/encounter) → tear down without saving local map.

NPCs not recruited and not moved to a persistent site revert to WorldActors in world.meta.

Artifacts left behind are promoted to WorldTileStash{wx,wy} in the ledger.

Ordinary items left behind are discarded (RimWorld-like).

6) Memory & Knowledge (Normative)

Faction Memory persists in factions/memory.mpkz (POIs, sightings, rumors). Merge policy: coalesce duplicates; expire low-confidence rumors by expires_tick.

NPCs carry only KnownLocations/SightingLog/Rumors while at world layer (in their WorldActor records). On returning to settlements/faction contact, these entries merge into Faction Memory and may be pruned from the NPC.

Adventure Player: maintains Personal Map Memory with decay in player/adventure_memory.mpkz. NPCs never get this.

7) Versioning & Migration (Normative)

format_version monotonically increases; loader dispatches by version.

Content rebinding by string IDs; missing → safe placeholders (missing_item/buildable/creature) and pause affected jobs.

Structural migrations are offline transforms; balance changes are not migrations.

8) Determinism (Normative)

PRNG: counter-based streams namespaced by (system, scope); per-chunk/world cursors restored before stepping.

Ordering: diffs merge by (chunk → tile → system_priority → systemId); mailbox replay by (tick, senderChunkId, seq).

Seeds: instance_seed = H(world_seed, wx, wy, type); substreams derive as H(instance_seed, system_name).

9) Reliability & IO Contracts (Normative)

Per-file try–catch on save/load; errors produce structured reports and never crash the process.

Integrity: each *.mpkz carries CRC-32C; manifests store SHA-256 of critical files. Validation failure → rollback to latest autosave.

Two-phase commit for multi-file ops (e.g., artifact transfer across chunks): write all participants to staging/…, flush, then atomically update manifest.json (bump gen).

Autosave ring: keep last 5 ZIP bundles.

10) Load Pipeline (Normative)

Read manifest.json; verify packset_signature & registry_hash.

Load registries; bind string IDs → runtime handles.

Restore RNG streams.

Load world.meta (Sites, WorldActors).

Parallel load persistent instances (apply deltas), chunks, jobs, mailboxes.

Load FactionMemory, Artifacts Ledger, Adventure Personal Memory.

Rebuild derived indexes (spatial, stockpile, pathing).

Validate cross-refs; substitute placeholders; pause broken jobs with reasons.

Resume tick at last_tick + 1.

11) Error Classes (Normative)

E_PARSE, E_SCHEMA, E_HASH_MISMATCH, E_IO_READ, E_IO_WRITE, E_AUTOSAVE_FAIL, E_RNG_RESTORE, E_REBIND_FAIL, E_MAIL_REPLAY, E_INSTANCE_LOAD, E_ARTIFACT_CONFLICT

All are non-fatal; loader/degrader must salvage progress and report succinctly.

12) CI Gates (Normative)

Golden determinism runs (pre/post save equality, cross-threads/OS).

Fuzz resilience (truncations/bitflips) → no crash; rollback works.

Artifact transfers across chunks/instances verified as atomic (ledger and mirrors consistent).

WorldActors round-trips (enter persistent vs ephemeral) are consistent.

13) Field Reference (Condensed)
13.1 ChunkSnapshot (payload idea)
key:{cx,cy,cz}
terrain:{tiles[], fluids8[], flags[]}
items:[{id,material_id?,qty,quality?,pos|container_guid,tags[]}...]
buildables:[{id,rot,mat?,state_bits,fuel?,hp?}...]
actors:[{id,body_plan_id,hp_by_slot{},statuses[],inv_guids[],ai_memo?}...]
reservations:[{job_guid,target,holder,qty}]
fields:[{id,strength,decay}]
mailbox_cursor:u64
prng_cursors:[{system_id,counter}]
string_table:StrTab

13.2 WorldActors (world.meta)
{ guid, faction_id, kind, wx, wy, path_plan?, cargo_summary?,
  known_locations?, sightings?, rumors? }

13.3 FactionMemory (factions/memory.mpkz)
known_locations:[{id,kind,wx,wy,local_hint?,first_seen_tick,last_confirm_tick,tags[]}]
sightings:[{kind,target_id,wx,wy,tick}]
rumors:[{source,topic_id,wx?,wy?,confidence,expires_tick}]

13.4 Artifacts Ledger (artifacts/ledger.mpkz)
{ artifact_id, name?, origin?, holder: ActorRef|ChunkRef|InstanceRef|WorldTileStash, last_update_tick }

13.5 Instance manifest (per world-tile)
{ wx, wy, instances:[
    {type:"player_fort",seed:78123,policy:"persistent"},
    {type:"dungeon",seed:445566,policy:"persistent"}
  ],
  policies:{ wilderness:"ephemeral", encounter:"ephemeral" }
}

14) Implementation Notes (Binding for IO)

All save/write work is done by a single IO worker per slot; systems emit immutable snapshots.

Large arrays are chunked during pack/deflate to control spikes.

Loader tolerates missing optional files (e.g., no instance files for ephemeral types).

When tearing down ephemeral maps, only knowledge updates occur (FactionMemory, Adventure memory) and artifact ledger updates; no local map files are written.

15) Future Extensions (Reserved)

factions/state.mpkz (richer diplomacy/trade history).

instances/.../*.sum.json (summaries) — unused in v1 due to explicit “no storage” for wilderness/encounter.

networks/*.mpkz (connectivity caches) for faster loads in v2.

ironman.lock (single-slot ironman enforcement).
