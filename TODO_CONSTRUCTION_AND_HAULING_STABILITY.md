**Construction & Hauling Stability — Current Issues, Fixes, and Next Steps**

- Scope: L0 structural construction (wall/floor/ramp/stairs), material hauling to construction sites, worker navigation around sites.
- Audience: engineering team working on stability and UX parity with RimWorld/DF.

**Context**
- Systems involved
  - `ConstructionSystem` (planner) reads construction designations and spawns L2 construction sites.
  - `ConstructionJobSystem` (executor) advances sites, consumes delivered materials, emits `SetTerrain` on completion.
  - `ConstructionMaterialsPlanner` enqueues material transport requests for sites.
  - `TransportJobSystem` (executor) assigns workers to transport jobs, moves items via pathfinding.
  - `WorldNavigationView` mediates pathfinding against world/nav caches.
  - `SimulationDiffApplicator` applies diffs (including terrain changes) and now performs post‑set terrain ejections.

**Symptoms Observed (from logs)**
- Selection mismatch (fixed):
  - UI shows `Rect=2x3` but `[ORDERS.CONSTR] planned=2` with per-cell missing the last row/column.
- Over-delivery (fixed):
  - `[BUILD.EXEC] delivered={stone_block:14} req={stone_block:1}` → moved whole stacks.
- Stall after first sites done (multi-layer walls):
  - Repeated `[CM-PLAN] scanned_sites=… enqueued=…`, but `[BUILD.EXEC] delivered={}` forever and no `Picked/Completed` transport logs.
  - In previous sessions: `Replan kind=Invalid` from positions that just became walls (workers/items in walls).

**Root Causes Identified**
- Selection traversal used exclusive end (`< MaxExtent`) against inclusive `MaxExtentX/Y` → lost last col/row. (FIXED)
- No quantity in hauling → executor moved entire stacks to sites. (FIXED)
- Pre-completion race: worker standing on anchor, completion sets terrain to wall, worker trapped → `Invalid` replans, global stall. (MITIGATED)
- Planner drop cells were allowed near/at anchors of other sites → material/worker churn among adjacent sites. (MITIGATED)
- Navigation blocking: fully disallowing anchors for `IsWalkable` partitioned narrow corridors (multi-layer shells) → dead zones where transport cannot reach drop cells. (MITIGATED by allowing walk-through but not stand-on.)

**Changes Implemented (current branch)**
- Selection parity and observability
  - `ConstructionSystem`: iterate `y <= MaxExtentY` and `x <= MaxExtentX` to match UI. Added per-cell logs when area ≤ 12:
    - `[ORDERS.CONSTR.CELL] (x,y,z) OK/SKIP reason=...`.
  - `FortressState`: logs UI endpoints and rect → `[BUILD.UI] First=(x1,y1) Second=(x2,y2) Rect=(X,Y,WxH) Z=...`.

- Per‑need hauling
  - `TransportRequestQueue`: request `Quantity` added; merge dedup sums quantities.
  - `ConstructionMaterialsPlanner`: enqueue requests with `Quantity=shortfall`, split across stacks; ring search for drop cells (radius up to 3), skip site anchors.
  - `TransportJobSystem`: on pickup split stacks via `ItemManager.SplitStack(count)`; merge at destination.

- Pre-completion relocation & eject (RimWorld/DF‑style prevention + remedy)
  - `ConstructionJobSystem`: before completion, atomic relocation (try move → recheck → complete), and move residual items off anchor; exclude site anchors as landing cells.
  - `SimulationDiffApplicator.ApplySetTerrain`: after setting non-walkable terrain, eject any creatures/items currently on that tile to nearest safe cells (r≤3), skip site anchors; merge stacks; logs `[EJECT]`.
  - Periodic `SanitizeSystem`: every 40 ticks, relocate up to N stuck creatures/items out of non-walkable tiles; logs `[SANITIZE]`.

- Navigation alignment
  - `WorldNavigationView`: anchors are non-standable but walkable (workers can pass through but won’t choose anchors as endpoints). Prevents corridor partitioning in multi-layer builds.

- Transport stabilization
  - `TransportJobSystem`: invalid‑replan UNSTUCK after 2 repeats (radius 3); reserve items centrally (skip requests for reserved items; reserve item upon assignment; release on failure).

**Current Gaps (to be addressed next)**
- Multi-layer shells still may stall in dense patterns under stress:
  - Planner thrash: high churn of enqueued requests when outer rings block practical droppoints intermittently.
  - NoPath/Invalid sources: repeated attempts against unstable sources/targets (no TTL ban list yet).
  - Build sequencing: no outer-first prioritization; inner sites may starve or induce detours.
- Metrics & insight are basic:
  - No counters for UNSTUCK/EJECT/SANITIZE frequency per tick/region.
  - No per-site state dashboard (ready/shortfall/delivered/in-flight jobs).

**Proposed Next Steps (DF/RimWorld‑aligned)**
- A) Sequenced Planning (outer → inner) & Concurrency Gating
  - Add a heuristic distance ring from “outside air/floor” and schedule outer sites first; gate inner sites from `ready-for-completion` until outer ring completed.
  - Cap ready sites per radius (e.g., within R cells, at most M concurrent ready sites).
  - Acceptance: continuous progress on nested walls without stalls; no enclosed areas before ejections needed.

- B) Request Stability & Backoff
  - Add TTL banlist for (item GUID, dest cell) on repeated `NoPath`/`Invalid` to avoid thrash for T ticks.
  - Per-site shortfall snapshot: avoid re-enqueueing identical requests when an in-flight request already covers shortfall (track in-flight counts by tag per site).
  - Acceptance: `[CM-PLAN] enqueued` stabilizes (no constant 1:1 spam), Active intake correlates with actual site progress.

- C) Destination Selection Refinement
  - Expand drop search dynamically (r up to 4..5) in constrained corridors; prefer cells with lower congestion (few items/creatures recently).
  - Penalize droppoints close to site anchors of other active sites.
  - Acceptance: delivered no longer stuck at `{}` for prolonged periods; progress increments under crowded patterns.

- D) Observability
  - Structured counters per tick:
    - CM‑PLAN: scanned_sites, enqueued_count, suppressed_by_inflight, no_drop_count.
    - TRANS-JOBS: intake, assigned, picked, completed, replans, invalid, unstuck_count.
    - BUILD.EXEC: per-site delivered delta, progress delta; EJECT/SANITIZE counts.
  - Lightweight per-site debug overlay (site color by state; tooltip lists shortfall/in-flight/ready).
  - Acceptance: easy to identify starvation or partitioning from logs alone.

- E) UX/Policy Alignment
  - Keep anchors walk-through (not standable) to prevent partitioning; anchor-screening for drop/relocation remains.
  - Consider optional “construct from adjacent only” policy later (closer to DF) once sequencing is robust.

**Acceptance Criteria**
- Selection parity
  - For 2×3, 3×2, 1×N, M×1 at non-boundaries: `[ORDERS.CONSTR] planned = W×H` and per-cell OK count matches; site spawn coordinates match preview dots.
- Single/Double wall stability
  - With 3–10 workers, outer→inner double walls complete without stalls; minimal EJECT/SANITIZE occurrences; no persistent `delivered={}`.
- Dense multi-site ring
  - Continuous progress (no global stall > 30s): `[BUILD.EXEC] progress` increase observed; CM‑PLAN enqueued not constant spam; transport picks/completes flow regularly.
- No over-delivery
  - Delivered materials per site stay within small margin of shortfall; no whole-stack dumps.

**Risks & Considerations**
- Performance
  - Additional scans (rings/TTLs) must be bounded; ensure low per-tick costs; cache per-site in-flight state.
- Determinism
  - Use seed-based pseudo-random ordering for ejection/drop selections to keep runs reasonably reproducible.
- Navigation Rebuilds
  - Continue to rely on `MarkTileDirty` and `BumpConnectivityVersion` after site placement and SetTerrain.
- Content Registry
  - Material→geology kind resolution must remain consistent; log `GeoFail` per-cell for small rects to catch data issues.

**Appendix: Log Glossary**
- `[BUILD.UI]` UI submission endpoints/rect.
- `[ORDERS.CONSTR.CELL]` Per-cell candidate checks when area ≤ 12 (OK/SKIP with reason).
- `[ORDERS.CONSTR]` Planner summary per designation.
- `[CM-PLAN]` Material planner: scanned_sites / enqueued.
- `[TRANS-JOBS]` Transport executor: intake/active/backlog and key events (`Assigned`, `Picked`, `Completed`, `UNSTUCK`).
- `[BUILD.EXEC]` Per-site delivered/required/progress.
- `[BUILD.COMPLETE]` Terrain set event.
- `[EJECT]` Post-SetTerrain ejection of creatures/items from blocking tiles.
- `[SANITIZE]` Periodic cleanup of stuck creatures/items.

**File References (recently modified)**
- `src/HumanFortress.Simulation/Orders/ConstructionSystem.cs`
- `src/HumanFortress.App/States/FortressState.cs`
- `src/HumanFortress.Simulation/Jobs/ConstructionMaterialsPlanner.cs`
- `src/HumanFortress.App/Jobs/TransportJobSystem.cs`
- `src/HumanFortress.Simulation/Items/ItemManager.cs`
- `src/HumanFortress.Navigation/WorldNavigationView.cs`
- `src/HumanFortress.App/Jobs/ConstructionJobSystem.cs`
- `src/HumanFortress.Simulation/Diff/SimulationDiffApplicator.cs`
- `src/HumanFortress.App/Jobs/SanitizeSystem.cs`

