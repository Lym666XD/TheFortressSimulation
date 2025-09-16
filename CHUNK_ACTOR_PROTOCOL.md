0) Scope

Defines the actor-based inter-chunk protocol used by the simulation: mailbox semantics, message schemas, ordering keys, reliability, failure policy, and CI gates.

Integrates with: Update Order (stages), Job Scheduler (Plan/Barrier/Merge+Apply), Diff-Log & Merge (intra-chunk writes), and Chunk/Data Layout.

1) Goals (Normative)

Deterministic & replayable across OS/CPU/thread counts.

Thread-safe & lock-free on the sim path: all cross-chunk effects go through messages, not shared writes.

Crash-resistant: malformed/overflow messages never crash the loop; chunks/systems can quarantine/degrade.

LLM-friendly: small, explicit schemas; stable ordering rules.

2) Terms (Normative)

Actor (Chunk): execution unit that owns authoritative data for its spatial region.

Mailbox: per-chunk, per-stage inbound queue of immutable messages.

Outbox: per-chunk, per-stage append-only buffer of messages to other chunks.

Drain: step that consumes mailbox messages in a stable order before local Apply.

Tick: fixed simulation step; stage is a sub-step in the tick pipeline.

3) Lifecycle (Normative)

Within a stage S (e.g., FluidsStep, Items):

Plan (parallel, read-only)

Each active chunk runs a Plan job. It may:

Read local state (and halo)

Append diffs (for local writes later)

Append messages to its Outbox[S] for other chunks

Dispatch (in-stage)

The scheduler moves each outbox to the destination chunk’s Mailbox[S] (no reordering).

Drain (per chunk, deterministic)

Each chunk runs an Actor-Drain job (still kind=Plan, writes=[]) to consume all Mailbox[S] messages in stable order and translate them to local diffs, acks, or re-emitted messages.

Barrier

Wait until all Plan + Drain jobs finish (or yield with back-pressure).

Merge+Apply (per chunk)

Deterministic merge of diffs (see Diff-Log & Merge) → single write pass for chunk’s allowed layers in stage S.

Emit reply messages queued for the receiver stage (usually same stage S in the next tick unless specified otherwise).

Emit Events

Publish typed events for UI/logic; no world writes.

Result: same-tick cross-chunk causality within a stage is achieved via Plan→Dispatch→Drain before local Apply. Cross-tick causality (replies) is explicit.

4) Ordering & Keys (Normative)

Mailbox drain order (inside one chunk & stage):
Tick → SenderChunkId → LocalSeq
where LocalSeq is the sender’s per-stage, per-chunk monotonic counter.

Commit order across chunks: ascending chunkId.

Per-tile merge order (after Drain → diffs exist):
TileKey → Priority(desc) → SystemId(asc) → LocalSeq(asc) (see Diff-Log spec).

5) Message Envelope (Normative)
{
  "msgId": "T120:S4:C_09_04:000123",
  "tick": 120,
  "stageId": "FluidsStep",
  "type": "BorderFlux",
  "from": { "chunk": "C_09_04" },
  "to":   { "chunk": "C_10_04" },
  "localSeq": 123,
  "priority": 10,
  "payload": { /* type-specific */ }
}


Fields

msgId = deterministic composite "T{tick}:S{stage}:C_{sender}:{localSeq}".

tick = emit tick; messages are stage-local unless a reply indicates next-tick handling.

stageId = stage responsible for consuming the message.

type = registered message type (see §7).

from/to.chunk = source/destination chunk IDs.

localSeq = per-sender, per-stage, monotonic.

priority = queue class (P0..P3); does not change consume order inside a mailbox (ordering is fixed above); used only for scheduling fairness.

payload = type-specific JSON (validated).

Validation

Unknown type or schema mismatch ⇒ drop with ERR_MSG_INVALID (log + event).

to == from or wrong stage ⇒ drop with ERR_MSG_ROUTE.

Messages are immutable; receivers must not mutate payloads.

6) Reliability, TTL, Idempotency (Normative)

Transport: in-process memory; dispatch is reliable under normal operation.

Overflow policy (see §12): drop oldest non-critical messages with a rate-limited warning; never crash.

TTL: default this stage in this tick. A reply schedules a new message for the next tick (explicit).

Idempotency: receivers keep a sliding window of (senderChunkId, localSeq) for the current tick to dedup accidental duplicates (no-op if seen).

7) Message Types (Normative starter set)
7.1 Units & Movement

MoveUnitIn

{ "type":"MoveUnitIn",
  "payload": { "unit":"u#123", "from": {"chunk":"C_09_04","tile":[15,7,3]},
               "toTile":[0,7,3], "moveTag":"T120.step42" } }


Consume in: Units (or your Items stage if combined).
Receiver behavior: if toTile standable & free by species: create local diffs to add unit (L6), emit MoveAccepted; else MoveRejected(reason).
Replies:

MoveAccepted { unit, at:[x,y,z] }

MoveRejected { unit, reason }
Sender on reply: if accepted → remove from old tile; if rejected → replan/path.

7.2 Fluids (border exchange)

BorderFlux

{ "type":"BorderFlux",
  "payload": { "fromTile":[15,4,3], "toTile":[0,4,3], "kind":"fluid.water", "amount": 2 } }


Consume in: FluidsStep.
Receiver behavior: add +amount to local inbound pool (diffs), merge later with SUM→CLAMP→BACK_PRESSURE.
Optional reply:

BorderFluxReturn { toTile, fromTile, kind, amountReturned }
(sent next tick if clamp overflow occurs; deterministic per merge order).

7.3 Items & Stocking

PushItem

{ "type":"PushItem",
  "payload": { "item":"itm.log", "qty":3, "toTile":[0,10,3], "job":"J42" } }


Consume in: Items.
Receiver behavior: attempt to add to stack(s) per policy/capacity.
Replies (same stage or next tick):

AcceptItem { item, qty }

PartialAcceptItem { item, accepted, leftover }

RejectItem { item, reason }
Sender on reply: handle leftovers (keep, reroute, or defer).

Additional types can be registered (e.g., ReserveTile, SyncRoomId), but must specify stageId, schemas, and deterministic rules.

8) Stage Bindings (Normative)

Each message type declares its consumer stage (stageId).

Allowed consumer stages (by default):

FluidsStep: BorderFlux, BorderFluxReturn

Items: PushItem, item replies

Units (or combined Items/Units stage): MoveUnitIn, move replies

A stage must Drain its mailbox before local Merge+Apply so inbound effects are considered.

9) How Messages Affect Writes (Normative)

Messages never write directly. Receivers convert messages into diffs that go through the normal deterministic merge and single commit of that stage.

Cross-chunk conflicts reduce to local conflicts resolved by layer strategies (e.g., L6 occupancy ONE_WINNER, L5 stack caps).

10) Budgets & Back-Pressure (Normative)

Drain budget: actor-drain jobs observe the stage budget (e.g., max messages per chunk). Excess messages carry over (stable FIFO).

Back-pressure: if a receiver cannot apply (e.g., capacity full), it must reply with a typed message (Partial/Reject/Return) in deterministic order; senders react next tick.

11) Affinity & Locality (Informative)

Prefer running Plan + Drain + Merge+Apply of the same chunk on the same worker to reuse caches. This is a hint only; determinism must not depend on affinity.

12) Overflow & Failure Policy (Normative)

Mailbox size is bounded per chunk/stage. On overflow:

Drop oldest non-critical message (types may mark critical:true in registry).

Emit ERR_MAILBOX_OVERFLOW { chunkId, stageId, droppedType, count } (rate-limited).

Try–catch boundaries: Drain and Merge+Apply are wrapped. On exception:

Drop offending message or tile’s conflicting diffs,

Quarantine this chunk/system for the tick,

Degrade to serial next tick (see RULES & Scheduler).
The main loop must not crash.

13) Registry & Schemas (Normative)

Message types are registered in /content/actors/messages.json with:

id, stageId, critical (bool), JSON schema for payload,

validation function (bounds, existence checks),

reply map (which replies are legal).

Boot validation is strict; unknown fields warn; schema failures reject at load.

14) Determinism Rules (Normative)

Envelope keys: (tick, stageId, senderChunkId, localSeq) are monotonic and unique per sender.

Drain order: exact Tick → SenderChunkId → LocalSeq.

No RNG in Drain that depends on mailbox iteration order; if random tie-break is needed, sort first by a deterministic key, then sample from a stage RNG stream.

15) Telemetry (Normative)

For each chunk & stage record:

mailbox.size, msgs.in/out, msgs.dropped, msgs.criticalDropped

Drain CPU ms, diffs produced, replies sent, partial/reject counts

Percentiles (50/95/99) and per-type heat

UI overlays:

Mailbox size heatmap, top senders/receivers, drop alerts.

16) CI & Tests (Normative)

Determinism replay: same seed+inputs ⇒ same message order and same world/snapshot hashes.

Jitter fuzz: randomize worker/steal patterns; outputs identical.

Schema tests: positive & negative payloads.

Overflow tests: bounded mailbox ⇒ oldest non-critical dropped; determinism holds.

Soak with faults: injected exceptions in Drain/Merge+Apply ⇒ quarantine & degrade; loop persists.

17) Worked Flows (Informative)
17.1 Cross-chunk movement (same tick)

C_A Plan decides unit crosses east boundary → emits MoveUnitIn(to=C_B, toTile=[0,7,3]).

Dispatch places it into C_B.Mailbox[Units].

C_B Drain consumes in order, checks occupancy/standable → creates diffs to place unit; emits MoveAccepted.

Barrier → C_B Merge+Apply writes L6.

Next tick: C_A receives MoveAccepted and removes unit locally.

17.2 Fluid border (with overflow return)

C_A Plan emits BorderFlux(to=C_B, amount=2 water).

C_B Drain aggregates inbound to an inbound pool (diffs).

Merge: SUM→CLAMP(cap=8); overflow +1 ⇒ emits BorderFluxReturn(amountReturned=1) for next tick.

C_A next tick receives return and re-adds to its pool.

17.3 Item stocking (partial accept)

C_A Plan emits PushItem(log×3 → C_B:(0,10,3)).

C_B Drain tries stacking; capacity 2 ⇒ emit PartialAcceptItem(accepted=2,leftover=1).

C_A next tick receives reply; keeps 1 or forwards to another chunk.

18) LLM Integration Rules (Normative)

Use only registered message types; never invent fields.

Do not write world state while handling a message; instead emit diffs or reply messages.

Always respect drain order; if a batch is produced, sort it deterministically before emitting diffs.

If capacity/validations fail, reply with typed messages (Partial/Reject/Return) instead of silently dropping.

19) Checklists (Drop-in)
19.1 Adding a Message Type

 Define id, stageId, schema, critical, replies[] in the registry.

 Write validation (bounds, existence, layer permissions).

 Implement receiver logic → diffs (no direct writes).

 Add unit tests: valid, invalid, overflow, determinism under shuffle.

19.2 Mailbox Implementation

 Stable drain order Tick → SenderChunkId → LocalSeq.

 Bounded capacity + overflow policy.

 Dedup window for (sender, localSeq) per tick.

 Telemetry counters.

19.3 Failure Safety

 Drain wrapped in try-catch; on error → drop, quarantine, degrade.

 Log {seed,tick,stage,chunkId,type,msgId} (rate-limited).

 CI jitter fuzz keeps outputs identical.