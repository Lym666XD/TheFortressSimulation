id: error.policy.v1
status: normative
owner: engine/platform
last_updated: 2025-09-15
applies_to:
  - Engine runtime (simulation, scheduler, chunk actors, fluids/fields, AI)
  - Persistence (save/load, autosave, region instances)
  - Content pipeline (schemas, registries, hot-reload)
  - Rendering (SadConsole), input, UI
  - Mods/DLC (sandboxed user content)
principles:
  - Never crash the process. Fail closed, degrade gracefully, keep saves safe.
  - Determinism first: the same inputs produce the same outcomes despite errors.
  - Contain faults to the narrowest scope (chunk, system, job), quarantine if needed.
  - Always preserve player progress (atomic saves, autosave ring, idempotent recovery).
references:
  - SAVE_FORMAT.md
  - ../content/CONTENT_SYSTEM.md
  - CONCURRENCY_MODEL.md
  - CHUNK_ACTOR_PROTOCOL.md
  - DIFF_LOG_AND_MERGE_STRATEGIES.md
  - UPDATE_ORDER.md
0) Terminology (Normative)
Error: Unexpected condition violating a pre/post-condition; requires handling.

Fault: Underlying cause (bad content, IO, mod script, logic bug).

Incident: Aggregated series of errors with shared keys (system, chunk, tick-window).

Containment Zone: Smallest unit we suspend/quarantine (job, actor, chunk, system).

Quarantine: Temporarily disable a zone; gameplay continues elsewhere.

1) Severity Levels (Normative)
Level	Name	Contracted Behavior
0	INFO	Telemetry only; no user surfacing.
1	WARN	Recoverable; degrade feature locally; soft toast optional.
2	ERROR	Operation failed; compensate; user-visible banner if impact.
3	CRITICAL	Zone quarantine; autosave; show blocking dialog with safe options.
4	FATAL*	Do not crash process. Hard-stop to main menu with save-protect; incident dump.

*FATAL is reserved for integrity breaches (e.g., save tamper, registry mismatch with no placeholder path). Even at FATAL, the process remains alive.

2) Error Taxonomy & Codes (Normative)
Persistence

E_PARSE, E_SCHEMA, E_REBIND_FAIL, E_HASH_MISMATCH, E_IO_READ, E_IO_WRITE, E_AUTOSAVE_FAIL, E_RNG_RESTORE, E_MAIL_REPLAY

Content/Mods

E_PACK_MISSING, E_PACK_DEP_CYCLE, E_PACK_CONFLICT, E_MOD_SANDBOX, E_SCRIPT_TIMEOUT, E_SCRIPT_SECURITY

Runtime

E_CHUNK_STEP, E_ACTOR_TICK, E_JOB_TICK, E_SCHEDULER_INVARIANT, E_PATHFIND, E_FIELD_FLUID, E_NETWORK_BUILD, E_PROJECTILE, E_AI_BT

Rendering/UI

E_ATLAS_MISS, E_TILESET_INVALID, E_RENDER_PIPELINE, E_INPUT_DEVICE

MapGen/Instances

E_MAPGEN_PARAMS, E_INSTANCE_LOAD, E_INSTANCE_POLICY, E_INSTANCE_SEED_DRIFT

Each event MUST carry: {code, severity, system, tick, chunk?, actor?, job?, file?, pack?, message, stack?, count}.

3) Mandatory Catch Boundaries (Normative)
The following boundaries MUST be wrapped in try { … } catch (Exception ex) { … } with containment logic:

Tick barrier (top-level frame).

Per-system step in UPDATE_ORDER (e.g., FluidsStep, CreaturesStep).

Per-chunk step for systems that shard by chunk.

Per-actor tick (AI/creatures).

Per-job tick (scheduler workers).

Cross-chunk mailbox dispatch (receive/apply).

Save/load file unit (each file as an atomic unit).

MapGen instance create/apply-delta.

Hot-reload publish of registries.

Rendering frame (SadConsole draw).

At each boundary, the handler MUST: (a) log structured error, (b) compensate (see §5), (c) decide quarantine/escalation (see §6).

4) Determinism & Logging Rules (Normative)
Handlers MUST NOT introduce randomness. Recovery decisions MUST key off deterministic inputs (tick, ids).

Error logging MUST be rate-limited per (code, system, chunk) to ≤ 1 event/2s, aggregate counts in-memory.

Timestamps MAY be logged but MUST NOT affect simulation paths.

Do not mutate gameplay state in log formatting (pure side-effect).

5) Compensation & Fallbacks (Normative)
5.1 Persistence

Read failure → try prior autosave; if none, load with placeholders (missing_item, missing_buildable, missing_creature) and pause dependent jobs (PausedMissingContent).

Write failure → retry with exponential backoff (100ms, 300ms, 900ms, max 5), then mark AutosaveDegraded; keep in-memory snapshot queue for next attempt.

Registry rebind fail → placeholders + incident banner; saves remain valid.

5.2 Simulation

Actor tick exception → flag actor SimError, freeze AI (no movement/attacks), keep HP/state; show a small warning badge on the unit.

Job tick exception → cancel job instance, release reservations, append a human-readable reason in the job history; do not consume inputs.

Chunk step exception → set chunk to Degraded for that system; skip its substep this frame; schedule background self-check to clear flag.

Pathfind failure → return NoPath with deterministic fallback (stand still / local wander).

Fields/Fluids overflow/NaN → clamp to [0..max], emit E_FIELD_FLUID WARN, continue.

5.3 Rendering

Missing atlas/tileset → substitute “pink checker” tile with ID caption; continue sim.

Render pipeline exception → skip frame; if 3+ consecutive → disable optional effects (shadows, post) and continue.

5.4 MapGen/Instances

MapGen param error → fall back to default preset; flag world tile as GenFallbackUsed.

Persistent instance delta apply fail → load baseline only, and stash the corrupt delta as .bad (never overwrite).

Ephemeral (wilderness/encounter) always disposable: on error, tear down instance and continue world.

6) Quarantine & Escalation (Normative)
Thresholds (rolling 10s window):

Actor: ≥3 E_ACTOR_TICK → quarantine actor (SimError), auto-unequip dangerous behaviors (e.g., path spam), surface banner in unit panel.

Chunk+System: ≥3 E_CHUNK_STEP for same (chunk, system) → quarantine that system on the chunk for 5s (sim skips it).

Scheduler: any E_SCHEDULER_INVARIANT → freeze scheduler, dump queue snapshot to jobs/scheduler-incident.mpkz, show blocking dialog with options: “Resume (unsafe) / Roll back to autosave / Report”.

Save: 2 consecutive E_IO_WRITE → disable autosave for 60s, show banner, keep manual save available.

Quarantine MUST automatically clear after a successful healthy cycle, with a cool-down.

7) User-Facing Messaging (Normative)
ERROR: compact banner at screen top; includes human-readable summary and “Details…” link.

CRITICAL: modal dialog with clear remediation choices (e.g., “Resume with degraded features”, “Load last autosave”, “Open save folder”).

Messages MUST be localized by string IDs; never expose stack traces by default (available in Details).

8) Mods & Scripts (Normative)
Mods run in a sandbox with time budget (e.g., 10ms per tick per mod) and memory budget.

Budget overrun → E_SCRIPT_TIMEOUT WARN (first), then ERROR; repeated 3× → disable the mod for the session (QuarantinedMod).

Reflection/IO/network are denied by default; violations → E_SCRIPT_SECURITY CRITICAL, disable mod.

Mod content files follow the same schema validation and merge rules; a bad file never blocks other packs.

9) Content Pipeline & Hot Reload (Normative)
Each JSON file is validated in try–catch. Invalid entries are skipped and reported; pack load continues.

On hot reload, all registries swap snapshots at a barrier; systems MUST rebind IDs atomically.

If rebind raises E_REBIND_FAIL for live state (e.g., worn item slot removed):

Auto-unequip to a safe container; if container full, drop to ground at actor tile.

Pause jobs referencing removed recipes with reason.

10) Timeouts, Cancellation & Deadlocks (Normative)
All background ops (IO, compression, mapgen) MUST accept CancellationToken.

Hard time budget per frame for background pumps (e.g., 3ms main thread, 8ms IO thread).

Deadlock watchdog: if no frame progress for 5s but process alive → capture thread dumps, present CRITICAL dialog with “Return to Menu (safe-save)” option.

Long-running tasks MUST heartbeat; missing 3 heartbeats → cancel and compensate.

11) Memory Pressure & OOM (Normative)
Memory watchdog monitors GC and commit size.

On pressure:

Drop render caches and path caches.

Evict cold chunks (unloaded; save if dirty).

Reduce atlas LOD, disable heavy effects.

If still critical → hard-stop to menu with safe-save (FATAL), never crash.

12) Structured Error Event Schema (Normative)
json
Copy code
{
  "code": "E_JOB_TICK",
  "severity": "ERROR",
  "system": "Scheduler",
  "tick": 182340,
  "chunk": { "cx": 12, "cy": -3, "cz": 0 },
  "actor_id": "cre_dog#A1F3",
  "job_guid": "7d4f-...-a92c",
  "file": "jobs/scheduler.mpkz",
  "pack": null,
  "message": "Null reservation target",
  "stack": "Optional trimmed stack or hash",
  "count": 1
}
Events are aggregated in-memory; periodic flush to a rotating log file (logs/session.ndjson).

PII-free, deterministic keys, UTC timestamps.

13) C# Patterns (Normative Snippets)
13.1 Catch boundary with quarantine

csharp
Copy code
try
{
    system.Step(chunk, snapshot, dt);
}
catch (Exception ex)
{
    var ev = ErrorEvt.For(ex, code: "E_CHUNK_STEP")
        .With(system, chunk)
        .WithSeverity(Severity.ERROR);
    ErrorBus.Raise(ev);

    ChunkHealth.MarkDegraded(chunk, system, cooldownSeconds: 5);
}
13.2 Actor tick fence

csharp
Copy code
try
{
    actor.Tick(ctx);
}
catch (Exception ex)
{
    ErrorBus.Raise(ErrorEvt.For(ex, "E_ACTOR_TICK").With(actor).Error());
    actor.Flags |= ActorFlags.SimError;
    actor.AI.Pause(reason: "Exception");
}
13.3 Safe save

csharp
Copy code
public async Task<bool> TryWriteFileAsync(string path, ReadOnlyMemory<byte> data, CancellationToken ct)
{
    var tmp = path + ".tmp";
    try
    {
        await File.WriteAllBytesAsync(tmp, data.ToArray(), ct);
        FileUtils.FSync(tmp);
        FileUtils.AtomicReplace(tmp, path);
        return true;
    }
    catch (Exception ex)
    {
        ErrorBus.Raise(ErrorEvt.For(ex, "E_IO_WRITE").Critical().WithFile(path));
        FileUtils.TryDelete(tmp);
        return false;
    }
}
14) Testing & CI Gates (Normative)
Golden path: run 10 minutes, save, reload, run 10 minutes; byte-equal counters and identical outcomes under thread jitter.

Fuzz: random truncate/bitflip of any *.mpkz/manifest; loader must not crash; fallback triggers.

Chaos: inject exceptions at each catch boundary; verify quarantine and recovery; ensure no handle leaks.

Mods: budget/timeouts enforced; malicious API calls denied.

Rendering: simulate missing atlas; ensure pink-checker fallback and continued sim.

15) Developer Mode vs Release (Normative)
Dev: assertions enabled; some boundaries may rethrow to debugger after logging.

Release: assertions compiled out; never rethrow past boundary. Diagnostics available via the in-game console errors.dump.

16) Policy Compliance Matrix (Normative)
Area	Catch	Deterministic	Quarantine	Placeholder	Autosave
Registry Load	✓	n/a	Pack skip	✓	n/a
Save/Load	✓	✓	Slot lock	n/a	✓
Scheduler	✓	✓	Freeze & dump	✓	✓
Actor Tick	✓	✓	SimError	✓	✓
Chunk Step	✓	✓	Per-system	✓	✓
Mailbox	✓	✓	Drop & flag sender	n/a	✓
MapGen	✓	✓	Tile fallback	n/a	✓
Rendering	✓	n/a	Disable effects	✓	n/a

17) Non-Goals (Informative)
We do not guarantee recovery from arbitrary binary corruption beyond autosave rollback.

We do not attempt to auto-fix mod logic; we only sandbox and disable offenders.
