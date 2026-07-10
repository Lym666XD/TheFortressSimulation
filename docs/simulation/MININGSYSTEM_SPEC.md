MININGSYSTEM_SPEC.md – Mining System (Planner + Executor)
id: mining.system.v1.2
status: normative
owner: sim/jobs
last_updated: 2025-10-07

0) Scope

Defines the end‑to‑end mining pipeline across Orders → Planner (read) → Executor (write), including data contracts, Z‑axis conventions, stairwell specifics, cancellation, prioritization, and determinism. This replaces prior drafts (MINING_SPEC.md, miningorders_spec.md).

1) Architecture Overview

- Orders/UI (App)
  - Users create mining orders (Dig, DigRamp, DigChannel, DigStairwell, RemoveDigging) via rectangle selection across Z.
  - OrdersManager receives advanced orders; for stairwells it inverts the UI Z range (see 2.2) and enqueues a unified MiningDesignation or a MiningCancelRegion.

- MiningSystem (Simulation) – Planner, Read‑only
  - Holds persistent ActiveDesignation cursors and a set of cancellation regions.
  - Scans selection rectangles per Z and produces PlannedDig DTOs into a
    deterministic owner queue outbox (budgeted).
  - For stairwells, scans from ZMax down to ZMin (Top → Middle → Bottom) and applies terrain eligibility filters.

- MiningJobSystem (Runtime) – Executor, Write phase
  - Dequeues PlannedDig batches and assigns to workers, handling adjacency search and deterministic pathfinding.
  - Enforces vertical connectivity gates for stairwells, emits pre‑open steps, commits terrain changes and drops via Diff, and manages reservation lifetimes.
  - Honors cancellation regions by dropping planned or aborting active jobs.

- Shared subsystems
  - NavigationManager/PathService (deterministic A*), DiffLog/SimulationDiffApplicator (L0/L5 layer writes), ItemManager (drops), World (tiles/chunks/creatures).

High‑level flow

  UI → OrdersManager → MiningSystem.ReadTick → MiningSystem.WriteTick → outbox
      → MiningJobSystem.ReadTick → assign/move/dig → MiningJobSystem.WriteTick → Diff → World

2) Z‑Axis and Stairwell Semantics

2.1 Internal Z

- Internal world coordinates: Z increases upward. Lower Z values are deeper underground.

2.2 UI mapping for Stairwells (critical)

- Player scrolls “up” to indicate digging down. OrdersManager inverts the UI range only for DigStairwell.
  - Example: UI selects z=25..29 → actual internal dig range z=21..25 (downward from 25 to 21).
- Segment assignment by Z (internal): Top = ZMax, Bottom = ZMin, Middle = between.

3) Data Contracts

- OrdersManager.MiningDesignation: `(Id, Rect, ZMin, ZMax, Action, Priority, CreatedTick)`
- OrdersManager.MiningCancelRegion: `(Rect, ZMin, ZMax, Kind=AllMining)`
- MiningSystem.PlannedDig: `(Point Cell, int Z, ushort GeologyHandle, byte TerrainKind, int Priority, ulong Seed, MiningAction Action, MiningSegment Segment, int DesignationId)`
- MiningSegment: `None | Top | Middle | Bottom`
- ActiveDesignation (planner‑internal cursor): `(Id, Rect, ZMin, ZMax, CurZ, CurX, CurY, Action, Priority, CreatedTick, Done)`
- ActiveMiningJob (executor): `WorkerId, Target, Z, Adjacent, Stage(ToAdj/Digging/Complete), ProgressTicks, RequiredTicks, GeologyHandle, TerrainKind, Priority, AssignedTick, ReplanFailCount, Action, Segment, DesignationId`

4) Update Lifecycle and Priorities

- ITick priority: MiningSystem.Priority = Items; MiningJobSystem.Priority = Jobs.
- Read → Write split: Planner mutates no world state; only enqueues PlannedDig. Executor applies diffs (terrain/items) and movement.

5) MiningSystem (Planner)

5.1 ReadTick

- Drain adds: convert MiningDesignation to ActiveDesignation cursors (persisting across ticks). Log summary.
- Drain cancel regions: append to internal cancellation list.
- Budgeted production (default 128 PD/tick): weighted round‑robin over active cursors by Priority desc, then Id.
- For each cursor, TryNextDigFrom:
  - Bounds: Stairwell stops once `CurZ < ZMin`; others once `CurZ > ZMax`.
  - Stairwell scan direction: Z from ZMax→ZMin (Top→Middle→Bottom), advance XY row‑major.
  - Terrain filter:
    - Allowed: SolidWall (rock), OpenWithFloor (floor). Rejected: OpenNoFloor (air).
  - Produce PlannedDig with a deterministic Seed derived from (x,y,z).

5.2 WriteTick

- Enqueue accumulated PlannedDig into the planner outbox. No world write.

5.3 Cancellation query

- `IsTileCanceled(x,y,z)` returns true if tile lies inside any active MiningCancelRegion. Executors use this to drop/abort work.

5.4 Current implementation split

- `MiningSystem.cs`: planner state, scheduler identity, constructor, and `PlannedDig` DTO.
- `MiningSystem.Tick.cs`: ReadTick/WriteTick, designation/cancel drain,
  budgeted production, and deterministic owner-queue outbox dequeue.
- `MiningSystem.Scanner.cs`: designation cursor scanning, terrain filters, segment assignment, and cursor advancement.
- `MiningSystem.Cancellation.cs`: cancellation-region lookup and executor-facing cancellation query.
- `MiningSystem.Helpers.cs`: logging, deterministic target seed, and standable-adjacency helper.
- `MiningActiveDesignation.cs`: persistent planner cursor state.

6) MiningJobSystem (Executor)

6.1 ReadTick (intake + assignment)

- Inbox capacity per tick: 16 PD.
- Source order (to avoid starvation):
  1) Dequeue from planner outbox first.
  2) Dequeue from `_backlog` while room remains.
  3) Only if room remains and `(tick % 10) == 0`, dequeue from `_deferredStairwells`.
- Drop any PlannedDig covered by cancellation (`_planner.IsTileCanceled`).
- Sorting (deterministic):
  - Primary: Priority desc.
  - Stairwell only: Segment order `Top → Middle → Bottom`, then XY, then `Z desc` (higher Z first).
  - Others: XY, then `Z asc`.
- For each PD:
  - Stairwell skip/fast‑path: If current terrain already equals expected final kind:
    - Middle (StairsUD) is not dropped; it runs as a 1‑tick job to propagate pre‑open downward.
    - Top/Bottom are dropped (no‑op).
  - Gate check (non‑Top stairwells): require vertical stairs above or below at the same XY:
    - Above (z+1): StairsDown/UD. Below (z−1): StairsUp/UD.
    - If not satisfied, defer to `_deferredStairwells` (executor retries at a 10‑tick cadence; planner stays unblocked).
  - Reservation: skip already reserved target tile; otherwise reserve (and for Channel also reserve z−1 if needed).
  - Adjacency: pick NESW, then diagonals; stairwell/channel also allow “stand on target” when tile is standable. Pathfind and assign worker deterministically.
  - If no worker/path/adjacency: requeue to `_backlog` unless the tile is canceled.

6.2 WriteTick (movement + dig)

- Movement: Update path; replan on topology change with bounded retries; on timeout release reservation and requeue (unless canceled).
- Arrival → Digging:
  - Stairwell pre‑open rule (connectivity bootstrapping): when starting Top/Middle, if z−1 is SolidWall, set to StairsUD and spawn drops for SolidWall. Never pre‑open at Bottom.
  - Progress until RequiredTicks (data‑driven by geology/terrain). Middle fast‑path uses 1 tick if tile is already UD.
- Completion:
  - Apply action‑specific terrain change and drops (only when source kind is SolidWall).
  - Release reservations; remove job from active set.

6.3 Cancellation enforcement

- ReadTick: planned digs in canceled tiles are dropped immediately.
- WriteTick: active jobs in canceled tiles are aborted on the next frame, reservations released, workers freed. Requeues skip canceled tiles.

7) Action Semantics (apply stage)

- Dig: SolidWall|Ramp → OpenWithFloor; drop tables by geology/terrain.
- DigRamp: SolidWall → Ramp; if above is OpenWithFloor, remove floor (OpenNoFloor).
- DigChannel: OpenWithFloor → OpenNoFloor; if z−1 is SolidWall, convert below to Ramp and drop for below’s geology.
- DigStairwell: Top→StairsDown, Middle→StairsUD, Bottom→StairsUp; drops only when converting from SolidWall.

8) Determinism

- Stable enumeration (row‑major) and stable worker order (by GUID).
- Fixed seeds for RNG (`SeedFrom(x,y,z)`); planner outbox dequeue and
  assignment sorts are deterministic.
- Diff application is ordered by layer and stable chunk/indices.

9) Budgets & Backpressure

- Planner: `maxPerTick` (default 128 PD).
- Executor: inbox cap=16; deferrals retried every 10 ticks and only if there is room.

10) Error Handling

- Cancellation: PD dropped or jobs aborted; reservations released.
- No adjacency/path: requeue to backlog with bounded replans; timeout releases reservation.
- Terrain changed mid‑dig: verify before apply; abort if no longer diggable.

11) Logging Conventions (key excerpts)

- Planner: `[MINING][PLAN] Designation ...`, `PRODUCE PlannedDig ... seg=Top|Middle|Bottom`.
- Executor intake: `[MINING][t] Planned digs dequeued: N; ids=[id:count,...]`.
- Gate/deferral: `blocked: no vertical connection; defer` (10‑tick cadence).
- Skip/fast‑path: `Check skip: seg=... current=... expected=...` and `Skip stairwell seg=... already ...`.
- Cancellation: `Drop canceled target=(x,y,z)`; `Cancel job ... release worker=...`.

12) UI Notes (non‑normative)

- Selection preview: gold dot fill for eligible tiles; invalid/unaffected tiles keep original appearance.
- Mining completion yellow markers are disabled to avoid covering items/creatures; in‑progress highlights remain.

13) Stairwell Flow (ASCII)

Scan and execution order per cell (single column example):

  Z=ZMax  ── Top PD ──► Dig StairsDown
            │           \
            │            \
            ▼             └─ Pre‑open ZMax−1 to StairsUD (if SolidWall)
  Z=ZMax−1 ── Middle ──► If already UD: fast 1‑tick; else dig → StairsUD
            │            └─ Pre‑open next Z (−1) to UD when SolidWall
            ▼
  ...       ── Middle ──► Repeat until Bottom layer reachable
            ▼
  Z=ZMin   ── Bottom ──► Dig StairsUp (no pre‑open)

14) Sequence (ASCII)

  UI Order ─► OrdersManager (invert Z for stair)
            └► MiningSystem.ReadTick: add ActiveDesignation, drain cancels
                                      scan → PlannedDig (budgeted)
            └► MiningSystem.WriteTick: enqueue PlannedDig
            └► MiningJobSystem.ReadTick:
                 intake: planner → backlog → deferred(10‑tick, if room)
                 drop if canceled → sort → assign/reserve
                 gate fail → defer; no worker/path → backlog
            └► MiningJobSystem.WriteTick:
                 movement (replan/timeout) → start dig (pre‑open)
                 progress → verify → apply terrain+drops → release

15) Troubleshooting

- Stairwell stalls after two layers:
  - Ensure executor sort is stairwell‑aware (Top→Middle→Bottom; Z desc), middle fast‑path enabled, pre‑open on Top/Middle only, and deferred queue processed only if room at 10‑tick cadence.
- Cancellations not honored:
  - Verify `IsTileCanceled` is called in both intake and active job loops; check logging for “Drop canceled …” or “Cancel job …”.

16) Superseded Docs

This spec replaces previous drafts MINING_SPEC.md and miningorders_spec.md. The internal Z convention here (Z increases upward) is authoritative; UI stairwell ranges are inverted in OrdersManager to match player expectations.
