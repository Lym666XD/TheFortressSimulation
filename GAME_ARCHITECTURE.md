GAME_ARCHITECTURE.md — v1.1 (Unified, Fortress-Only, SadConsole-Primary)

This document is the authoritative architecture for our new game. It merges our current fortress-only, deterministic, SadConsole-primary design with useful patterns from the previous project’s architecture (flow diagrams, loop framing, and component factoring), adapted to today’s decisions (no Adventure/Encounter maps; single fortress map; edge-band spawns). 

GAME_ARCHITECTURE

1) Goals & Non-Goals
Goals

Deterministic simulation at fixed 50 TPS, reproducible across OS/thread counts.

Read-parallel / write-serialized execution with explicit barriers and stable orders.

Single playable fortress map: square N×N chunks (N ∈ [2..8]), chunk = 32×32×Z.

Edge-band incidents: raids/visitors/beasts materialize on the fortress map borders (no extra maps).

Data-driven content: JSON + Schemas → compiled packs (.cpack), zero hard-coding.

Robust save/load: atomic commits, migrations, replayable commands.

SadConsole-primary renderer; clean seams for future adapters.

Non-Goals (v1)

No Adventure state; no EncounterMap creation.

No multi-renderer requirement; Unity/others remain optional future adapters.

No network/online play; single-player determinism is the priority.

2) High-Level Module Map
graph TB
  subgraph Presentation (SadConsole Primary)
    UI[Panels & Overlays (MVU + Virtualized Lists)]
    R[Renderer (SadConsole)]
  end

  subgraph Application
    GSM[GameStateManager]
    TS[TickScheduler (50 TPS)]
    CQ[CommandQueue (deterministic)]
    EB[EventBus]
    SAVE[SaveManager]
  end

  subgraph Simulation Core
    WOR[World/Chunks/LOD]
    NAV[Navigation/Pathing]
    JOB[Job Scheduler]
    AI[Agents & Utility AI]
    ECO[Economy/Hauling]
    COM[Combat]
    FFX[Fields/Fluids (simplified v1)]
    DIR[Storyteller/Incident Director]
  end

  subgraph Data & Content
    REG[Registries (compiled .cpack)]
    LUT[Index/LUTs/IdMaps]
    SER[Serializer (mpkz)]
  end

  UI --> CQ
  GSM --> TS
  TS --> WOR
  TS --> NAV
  TS --> JOB
  TS --> AI
  TS --> ECO
  TS --> COM
  TS --> FFX
  TS --> DIR
  EB --> UI

  R --> UI
  SAVE --> SER
  REG --> LUT
  WOR --> LUT


We retain the clean separation shown in the earlier project diagrams (UI/Input → Commands → Tick → Snapshot → Renderer), but scope it to a single map and SadConsole for v1. 

GAME_ARCHITECTURE

3) Game State Flow (Binding)

Boot → MainMenu → (WorldGen | LoadSave) → WorldMap → EmbarkPrep → FortressPlay → PauseMenu

WorldMap: no local simulation; world-layer pacing only.

FortressPlay: full sim on one map; all incidents enter via edge bands.

All transitions at end-of-tick barrier; simulation paused in non-play states.

(See GAME_STATE_FLOW.md v1.1 for the detailed state machine.)

4) Execution Pipeline & UPDATE_ORDER
graph LR
  IN[Input → Actions → CommandMapper] --> CQ[CommandQueue (tick-tagged)]
  CQ --> READ[Read Phase (parallel, immutable view)]
  READ --> BAR[Barrier]
  BAR --> WRITE[Write Phase (serialized, deterministic)]
  WRITE --> EVT[Game/UI Events]
  EVT --> SNAP[SnapshotBuilder (immutable)]
  SNAP --> REN[SadConsole Render]

Per-Tick UPDATE_ORDER (authoritative)

Read (parallel) – AI planning, job proposes, path queries, pre-evals; no writes.

Barrier – synchronize & seal reads.

Write (serialized) – two legal write paths:

Diff-Log per-tile ops {op, target, args, systemId, priority} → merged by stable key (chunk → tile → systemPriority → systemId) with explicit conflict rules.

Chunk-Actor mailbox: each chunk is the sole writer to itself; envelopes ordered by (tick, senderChunkId, seq).

Events – post-commit gameplay/UI events.

Snapshot – immutable render snapshot for this tick.

Render – SadConsole at ~60 FPS.

Determinism contracts

Stable iteration orders; named RNG streams; no wall-clock.

All queues bounded with deterministic drop policies.

Any exception is caught at system/chunk/actor boundaries (policy below).

5) World / Chunk / LOD

Fortress size: N×N chunks, N ∈ [2..8]; chunk = 32×32×Z tiles.

Edge bands: border rings for arrivals; sector pick is deterministic.

SIM_LOD_POLICY (L0/L1/L2/L3/L4):

L0/L1: active; decimation per system at L1.

L2: background integrators only (aging/rot/fields decay)—bounded & deterministic.

L3/L4: frozen/unloaded; mailbox buffers only.

Promotions/demotions at barrier, with hysteresis and bounded catch-up.

6) Storyteller / Incidents (Fortress-Only Binding)

Director ticks on a cadence (e.g., every 75 sim ticks).

Computes ThreatBudget, picks candidates, schedules IncidentPlans.

Execution happens in-place on the fortress map:

Raids/Siege/Visitors/Caravans/Beasts spawn in edge bands; heavy incidents target L0/L1 or pin/promote targets first.

Weather/Disease are map modifiers.

Ambush (adventure-only) is not used in v1.

Fully deterministic: named RNG streams, stable selection orders.

(See INCIDENT_DIRECTOR_SPEC.md.)

7) Systems (Brief Contracts)

Navigation/Pathing – Chunk-aware grids; caches maintained only at L0/L1; deterministic A* with stable tiebreakers.

Job Scheduler – Proposes from stockpiles/needs; respects LOD (no assignment into sleeping chunks); deterministic priorities; reservations TTL.

AI – Utility-based selectors; plans in Read; enacts in Write via diffs or chunk mail.

Economy/Hauling – Stockpile advisor, hauling policy (value×distance×urgency); traffic throttles to avoid cascades.

Combat – Deterministic resolution, named RNG stream per chunk/encounter; no offscreen fights in v1.

Fields/Fluids – Simplified v1: fields decay; fluids either full at L0/L1 or frozen at L2 (optional equilibrium clamp).

Networks – Unified runtime deferred to v2; v1 stores only device states (battery SoC, valve toggles), no saved graphs.

8) Rendering & UI

SnapshotBuilder creates immutable render snapshots once per tick; SadConsole consumes.

UI model uses MVU: UiStore + Reducers + Selectors; Virtualized lists for 10k-scale stockpiles & alerts.

Input → Command: device events → Actions → ICommand { tick, payload } → CommandQueue (only ingress; replayable).

Data (colors/fonts/strings/bindings) are data-driven via registries; no magic constants.

(See UI_AND_INPUT_MODEL.md.)

9) Content Build & Registries

Source JSON + Schemas → Content Build Pipeline → compiled packs (.cpack) with IdMaps/TagIndex/LUTs.

Engine loads packs at Boot/MainMenu; save files record packset_signature for compatibility.

Hot-reload swaps compiled snapshots at the barrier; rebinds systems safely.

(See CONTENT_BUILD_PIPELINE.md.)

10) Save/Load & Replay

End-of-tick barrier saves; single IO worker; chunked .mpkz blobs + atomic manifest replace.

Persist: dirty chunks, instance delta, jobs, mailboxes, RNG streams, faction memory, artifact ledger, world meta.

Load validates signatures/checksums, migrates if needed, restores world/meta, loads only required chunks, rebuilds indices, resumes at last_tick+1.

Replay: the serialized CommandQueue can be fed back to reproduce runs in CI.

(See SAVE_FORMAT.md.)

11) Error Handling (Normative)

Boundaries: state tick, per-system step, per-chunk step, per-actor tick, mailbox dispatch, save/load unit, mapgen, hot-reload, render frame.

On exception: catch → quarantine offender (system/chunk/actor) for N ticks → log structured error → continue.

Never crash gameplay loop; on critical faults, route to MainMenu with last safe save intact.

All engine exceptions have category codes and stable messages to support CI and bug triage.

(See ERROR_HANDLING_POLICY.md.)

12) Determinism & CI

Checkpoints at state transitions: canonical snapshot hash + storyteller/queue hashes.

Cross-thread/OS replay parity required.

Chaos runs inject exceptions around saves/transitions; autosave and atomic commits must hold.

(See DETERMINISM_CI.md.)

13) Public Namespaces & Key Interfaces (Sketch)
Game.App
  GameStateManager      // Enter/Exit/Update + barriered transitions
  TickScheduler         // 50 TPS fixed step
  CommandQueue          // deterministic, serializable ingress
  EventBus              // post-commit events to UI/systems
  SaveManager           // barrier saves, atomic manifest

Game.Sim
  World                 // chunks, tiles, Z, LOD, chunk actors
  Systems               // AI, Jobs, Economy, Combat, Fields/Fluids
  UpdateOrder           // Read/Barrier/Write/Events/Snapshot

Game.Data
  Registries            // compiled .cpack + IdMaps/TagIndex/LUTs
  Serializer            // mpkz blobs, migrations

14) Performance Targets (Initial)

Tick: 50 TPS (20 ms budget). Render: 60 FPS.

WorldGen (typical): < 15 s (256×256 world).

Fortress transition (enter/exit): < 100 ms after IO.

Save/Load: < 2 s typical.

UI: virtualized lists scroll updates ≤ 2 ms (95p).

Memory: compiled registries + indices ≤ 64 MB (base content).

15) Extensibility (Seams, Not Commitments)

Renderer adapters: keep the snapshot contract so a future Unity/3D client can subscribe without touching simulation (the previous project’s IPC/adapter ideas fit here if revived). 

GAME_ARCHITECTURE

Networks runtime v2, Adventure mode, Encounter instances: reserved—architectural seams already exist, but not implemented in v1.

16) Cross-Docs Index

GAME_STATE_FLOW.md v1.1 — states, transitions, fortress-only loop.

SIM_LOD_POLICY.md — levels, budgets, buffering, catch-up.

INCIDENT_DIRECTOR_SPEC.md — pacing, candidate selection, edge-band execution.

CONTENT_BUILD_PIPELINE.md — schemas, merges, compiled packs.

SAVE_FORMAT.md — files, atomic commit, RNG, signatures.

UI_AND_INPUT_MODEL.md — MVU, command queue, virtualization.

JOB_SCHEDULER_SPEC.md, DIFF_LOG_AND_MERGE_STRATEGIES.md, CHUNK_ACTOR_PROTOCOL.md — write paths & scheduling.

Appendix: Fortress-Only Loop (Mermaid)
graph TD
  A[Device Input] --> B[Action Mapping]
  B --> C[CommandMapper → ICommand{tick}]
  C --> D[CommandQueue]
  D --> E[Read (parallel)]
  E --> F[Barrier]
  F --> G[Write (serialized: Diff-Log / Chunk-Actor)]
  G --> H[Events]
  H --> I[SnapshotBuilder]
  I --> J[SadConsole Render]
  J --> D


End of file.