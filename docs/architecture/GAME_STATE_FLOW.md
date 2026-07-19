GAME_STATE_FLOW.md — v1.1+ (Unified, Fortress-only, Merged & Detailed)

Current implementation note (2026-07-09):

- This document still describes the target state-flow shape.
- Current content loading enters through Runtime's content-loading facade over
  the Content-owned loader for `content/` and `data/core/`; compiled `.cpack`
  loading is future design, not current implementation.
- Current save/load has a Runtime-owned vertical slice: a save directory with
  `slot_manifest.json` plus `runtime_snapshot.json`, Runtime validation and
  compatibility classification, a supported Simulation world payload restore,
  RNG stream restore, and pending-command replay restore. Chunk-sharded world
  storage, autosave rings, migrations, missing-content placeholder policy, and
  complete long-horizon job restore remain target architecture. Contained item
  restore is supported for payload-local acyclic item container graphs; carried
  and equipped item restore is supported for payload-local creature owners;
  installed item restore is supported for valid payload placement anchors; and
  item-local reservation tokens are supported when claimant/count rows validate
  against the item payload.

This file merges our current v1.1 fortress-only flow with the useful, more detailed procedures from the previous project’s state-flow document (startup, menus, worldgen stages, map navigation, fortress loop, save/load, testing). Where older content conflicted with v1.1 decisions (e.g., EncounterMap, Adventure), it has been adapted or removed. 

GAME_STATE_FLOW

Principles (unchanged)

Single playable fortress map. All incidents (raids/visitors/beasts) materialize via edge bands on that map; no EncounterMap is created; Adventure mode is out of scope.

Non-play states (Boot/MainMenu/WorldGen/LoadSave) allow bounded file I/O but do not advance simulation (TickScheduler paused; no UPDATE_ORDER).

Barriered transitions only; deterministic across OS/thread counts.

Fortress size is a square N×N chunks, N ∈ [2..16]; one chunk is 32×32×Z tiles.

0) Top-Level State Graph (final)
[Boot] 
  └─→ [MainMenu]
        ├─→ [WorldGen] ─→ [WorldMap]
        ├─→ [LoadSave] ─→ (resume into: WorldMap | FortressPlay)
        └─→ [Settings/Credits] ↩

[WorldMap]
  ├─→ [EmbarkPrep] ─→ [FortressPlay]   # enter fortress instance (N×N chunks)
  └─→ [VisitSettlement] (optional future) ─→ back to WorldMap

[FortressPlay]
  └─→ [PauseMenu] (Save/Load/Settings) ↩

1) Terms & Fixed Values

Edge bands: one or more border rings from which Storyteller spawns arrivals; sector choice is deterministic.

Snapshot rendering: simulation (50 TPS) produces immutable snapshots; SadConsole renders at ~60 FPS.

Write patterns: systems may write via Diff-Log merge or Chunk-Actor mailbox, both deterministic.

2) Application Startup Flow (merged & updated)
graph TD
    Start[App Start] --> Init[Init Core Systems]
    Init --> LoadConfig[Load Settings/Bindings/Themes]
    LoadConfig --> InitConsole[Init SadConsole (fonts, root consoles)]
    InitConsole --> Managers[Construct Managers (GSM, Save, RNG, RIM)]
    Managers --> Content[Load JSON content through FortressContentLoader]
    Content --> MainMenu[Enter Main Menu]


Procedure (deterministic, no simulation advance)

Parse args; set [STAThread] (Windows).

Create GameStateManager and app state registration. Fortress runtime services such as TickScheduler, CommandQueue, EventBus, and diff logs are owned by Runtime session/core services behind the App.Runtime session controller; GameStateManager reaches that controller through a thin GameStateRuntimeCoordinator rather than holding runtime services directly.

Initialize SadConsole (screen, font, palette, double-buffer).

Load input bindings and UI theme from registries; load compiled content packs (packset_signature shown on menu).

Enter MainMenu at barrier.

3) Main Menu State (merged details)

Enter: draw title, options; set focus to menu console.

Update: highlight selection; simple anim ticks (UI-only).

Input:

N: New Game → WorldGen

L: Load Game → LoadSave

S: Settings, C: Credits, ESC: Exit

Exit: clean menu resources.
(No simulation; I/O allowed.)

4) New Game → WorldGen (merged flow)
graph TD
  NewGame[New Game] --> Params[World Params & Seed]
  Params --> Validate{Valid?}
  Validate -->|Yes| Generate[Generate World]
  Validate -->|No| Params
  Generate --> Progress[Show Progress]
  Progress --> Done{Complete?}
  Done -->|No| Progress
  Done -->|Yes| WorldMap


Parameters UI

World name (unique), seed (manual or random), difficulty preset (placeholder).

(World size is engine-level; embark size chosen later.)

Generation stages (deterministic streams worldgen/*)

Base Terrain: elevation noise, tectonics, continental shelves.

Climate: temperature gradients, rainfall, prevailing winds.

Biomes: assign & smooth transitions; validate consistency.

Water: rivers, lakes/seas, drainage basins.

Resources: ores/veins, veg distributions, specials.

Finalize: embarkability masks, region props, (optional history stub).

Completion

Write world.meta.mpkz and initial manifest.json; transition to WorldMap.

5) WorldMap (navigation + world-layer pacing)

Data loaded

world.meta.mpkz, factions/memory.mpkz, artifacts/ledger.mpkz, minimal instances/*/manifest.json.

No chunk data loaded.

Rendering loop (UI-only)

Viewport calc; draw world tiles; info panel shows cursor tile stats (elevation/temperature/resources/suitability).

Camera: WASD (fast with Shift); cursor: arrows; Enter: open EmbarkPrep for current tile; ESC: back to menu.

Pacing (lightweight)

Storyteller cooldowns & world actors planning may run single-threaded; no local simulation.

6) EmbarkPrep (embark → fortress instance)

Choose world tile; select fortress size N×N chunks, N ∈ [2..16].

Configure starting party & loadout (placeholder).

Build InstanceKey { wx, wy, type="player_fort", seed }.

RIM.Activate(instanceKey) → generate/apply deltas; load initial chunk set; switch to FortressPlay at barrier.

7) FortressPlay (single map; full simulation)
stateDiagram-v2
    [*] --> Init
    Init --> Display
    Display --> GameLoop
    GameLoop --> Pause: ESC
    Pause --> GameLoop: Resume


Initialization

Create fortress grid by instance seed/region props; place veins, single cavern system; validate playability.

Initialize consoles/panels; center camera; set Z to surface.

Start TickScheduler; UI shows TPS/FPS/coords/Z.

Input → Commands (deterministic)

Device events → Action (bindings) → ICommand{tick,payload} via CommandMapper → CommandQueue (only write ingress; replayable).

Camera: WASD; Z-level: Q/E or scroll; cursor arrows; Space/Pause, +/- speed; ESC → Pause menu.

UPDATE_ORDER per tick (50 TPS)

Read (parallel) — systems read immutable world view; plan actions (AI, pathing, job intents, economy, combat, fluids/fields).

Barrier — synchronize; prepare write context.

Write (serialized, deterministic) — apply via Diff-Log merge (tile-scoped ops ordered by (chunk→tile→systemPriority→systemId) with explicit conflict rules) and/or Chunk-Actor mailbox (single writer per chunk; envelopes ordered by (tick,senderChunkId,seq)).

Events — dispatch gameplay/UI events; Storyteller may schedule arrivals.

Snapshot — build immutable render snapshot.

Render — SadConsole draws snapshot at ~60 FPS.

Storyteller execution (fortress-only)

Raids/Siege/Visitors/Caravans/Beast packs: spawn from edge bands; deterministic sector picking; heavy incidents target L0/L1 or pin/promote chunks first.

Weather/Disease: global/regional modifiers; no Ambush (adventure-only, deferred).

SIM_LOD_POLICY

Active only here. L0/L1 full; L2 background integrators (aging/rot/fields decay—bounded); L3/L4 frozen/unloaded; mailbox buffering outside L1.

Promotion/demotion at barrier; catch-up bounded and deterministic.

Pause menu

Pauses TickScheduler; offers Save/Load/Settings; resumes without state drift.

Exit

Barriered save of fortress delta → unload chunks → update world knowledge & artifacts ledger → return WorldMap.

8) Save System Flow (merged & aligned)
sequenceDiagram
  participant UI
  participant RuntimePorts
  participant RuntimeStore
  participant RuntimeCodec
  participant FS as FileSystem
  UI->>RuntimePorts: RequestSave(slotDirectory)
  RuntimePorts->>RuntimeStore: Create/validate Runtime save package
  RuntimeStore->>RuntimeCodec: Serialize runtime_snapshot.json
  RuntimeStore->>RuntimeCodec: Serialize slot_manifest.json
  RuntimeStore->>FS: write temp + replace document
  RuntimeStore->>FS: write temp + replace slot manifest
  RuntimePorts-->>UI: Structured save result


What we save

Current implementation: a Runtime-owned save directory with `slot_manifest.json`
and `runtime_snapshot.json`. The snapshot document contains Runtime manifest
metadata, command replay rows, primitive RNG stream rows, and the supported
Simulation world payload slices: terrain, ground/contained/carried/equipped
and installed items with payload-local item/creature/placement owners,
creatures, global reservations, stockpile zones, owned placeables/workshops,
and active orders.

Target work: chunk-sharded world storage, autosave rings, migration policy,
missing-content policy, and complete long-horizon job-state restore. Contained
item state is supported only when the containing item is present in the same
acyclic world payload graph; carried/equipped item state is supported only when
the owning creature is present in the same world payload; installed item state
is supported only when the placement anchor/z/rotation validates against the
saved world; item-local reservation-token state is supported only when token
claimants and reserved counts validate against the item payload.

When we save

Current implementation should be triggered through Runtime save ports and should
capture a Runtime-authored checkpoint. Autosave ring policy and fuller directory
fsync policy are still target work.

Faults

Validation/write errors should remain structured Runtime save issues for App to
present. App should not inspect live `World`, job systems, command queues, or
Runtime-internal save codecs to recover a failed save.

9) Load Game Flow (merged & aligned)
graph TD
    Menu[Load Menu] --> List[Enumerate Saves]
    List --> Pick[Select Save]
    Pick --> Verify[Verify & Checksum]
    Verify -->|OK| Read[Read Files]
    Read --> Version{Schema/Pack Match?}
    Version -->|Migrate| Migrate[Run Migrations]
    Version -->|Match| Restore[Restore State]
    Migrate --> Restore
    Restore --> Resume[Enter WorldMap or FortressPlay]


Validation

Current implementation validates both `slot_manifest.json` and
`runtime_snapshot.json` through Runtime and reports structured
`snapshot.document` or `slot.manifest` issues. CRC, full migration, and
pack/content placeholder policy remain target work.

Restore

Runtime validates the save directory, composes a fresh session, restores the
supported Simulation world payload, restores RNG streams after session services
reset, restores pending commands, rebuilds derived navigation/cache state, and
then resumes play through the normal Runtime/App session boundary.

10) Game Loop Timing (merged diagram)
graph TD
    T0[Tick Start] --> Paused{Paused?}
    Paused -->|Yes| RenderOnly[Render Only]
    Paused -->|No| Read[Read (deterministic system order)]
    Read --> Barrier[Barrier]
    Barrier --> Write[Write (Serialized)]
    Write --> Events[Events]
    Events --> Snapshot[Build Snapshot]
    Snapshot --> Render[Render Frame]
    RenderOnly --> Render
    Render --> TEnd[Tick End]


Target: 50 TPS deterministic tick, 60 FPS render; UI virtualized lists to keep
frame times under budget. Future chunk-partitioned read parallelism must remain
deterministic and non-overlapping.

11) State Transition Matrix (updated)
From	To	Trigger	Notes
Startup	MainMenu	Auto	Core init & content load complete
MainMenu	WorldGen	N	New world
MainMenu	LoadSave	L	Load existing save
MainMenu	Settings/Credits	S/C	UI only
MainMenu	Exit	ESC	Quit
WorldGen	WorldMap	Auto	Generation complete
WorldGen	MainMenu	ESC	Cancel
WorldMap	EmbarkPrep	Enter	On valid tile
EmbarkPrep	FortressPlay	Confirm	RIM activates instance
FortressPlay	PauseMenu	ESC	Pause
PauseMenu	FortressPlay	Resume	Unpause
PauseMenu	MainMenu	Exit to menu	Saves at barrier
LoadSave	FortressPlay/WorldMap	Select save	Resume point
12) Error Handling Boundaries (merged + our policies)

Catch & quarantine at: state tick, per-system step, per-chunk step, per-actor tick, mailbox dispatch, save/load unit, mapgen, hot-reload, render frame.

Local failure ⇒ isolate offender; continue loop; emit structured error event; surface toast.

Critical failure ⇒ route to MainMenu via ErrorSafeGuard with last safe save intact.

13) Performance Targets (merged & tightened)

Startup < 2s; menu input latency < 16ms.

WorldGen (256×256): < 15s; memory < 500MB.

Gameplay: 50 TPS / 60 FPS; fortress transitions < 100ms (after IO); save/load < 2s typical.

Resource limits (initial): entity cap tuned per profile; fortress size N×N, N∈[2..16]; save size target < 100MB.

14) Testing Checklist (merged & adapted)

Transitions: every path + cancel/error branches.

WorldGen: deterministic per seed; embarkability always present.

FortressPlay: camera/Z/cursor; edge-band spawns; LOD promotions; job/AI basic loops.

Save/Load: exact restoration (snapshot hash & RNG) across OS/thread counts.

Determinism CI: replay command logs; compare canonical snapshot hashes at checkpoints; cross-thread parity.

Perf: TPS/FPS targets; memory ceilings; no leaks; big stockpile lists remain smooth via virtualization.

15) Implementation Roadmap (merged + our priorities)

Core Flow & State Machine (Boot→Menu→WorldMap→Fortress)

WorldGen (deterministic stages; progress UI)

Fortress Rendering & Controls (camera/Z/cursor; snapshot path)

Tick/UPDATE_ORDER (Read/Barrier/Write/Events/Snapshot; Diff-Log/Actor)

Save/Load (atomic, ring autosave, migration stubs)

Storyteller (Fortress-only) (edge-band spawn executors)

SIM_LOD_POLICY integration & budgets

UI Virtualization & Command replay

Determinism CI gates & chaos tests

16) Change Log vs. v1.1 (what this merge adds)

Brought in explicit startup/menu/worldgen/map navigation/fortress loop/save/load procedures and diagrams from the prior project, reworded to fit fortress-only flow. 

GAME_STATE_FLOW

Kept our edge-band incident model, LOD, snapshot rendering, deterministic write patterns, and 2–8 chunk fortress size.

Removed legacy EncounterMap and Adventure branches; adjusted any references accordingly.

End of file.
