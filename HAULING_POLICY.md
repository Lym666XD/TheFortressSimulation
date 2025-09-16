id: hauling.v1
status: normative
owner: sim/economy
last_updated: 2025-09-15
applies_to:
  - Stockpile Zones (area-only, non-entity)
  - Items & Stacks (ITEMS_SPEC)
  - Job Scheduler (JOB_SCHEDULER_SPEC)
  - Navigation (NAVIGATION_SPEC)
principles:
  - Deterministic selection & assignment (stable orders, no wall clock)
  - Read-parallel propose; write-serialized commit (UPDATE_ORDER)
  - Anti ping-pong: no pointless item shuttling
  - Bounded work per tick (budgets) and graceful backoff
scope_v1:
  - Stockpile Pull (bring eligible items into zones)
  - Construction Supply (deliver materials to blueprints)
  - Cleanup/Dump (move forbidden/unwanted items to a dump zone)
  - Optional: Deliver to Trade Depot (if present)
out_of_scope_v1:
  - Containers/bins; conveyors; carts/vehicles (reserved)
1) Concepts & Data Model
1.1 Stockpile Zone (area system, not an entity)
Defined by a set of cells, a filter (allow/deny by tags, item_id, material_class), and an I/O policy:

mode ∈ {pull_only, accept, output_only} (v1 usually pull_only or accept).

Zone maintains free slot count and a target fill level (0..1).

Zone keeps a Generation integer; bump on any config change (invalidates offers).

1.2 Items & Stacks
Item stacks live in L5 overlay (ItemStackRef pointing to item pool).

Each stack has: item_id, material, mass, volume, quantity, flags (e.g., forbidden, owned, fresh), last_stockpile_id, placed_at_tick.

1.3 Reservations (authoritative)
ItemReservation {stack_handle, qty, job_id, ttl} — exclusive; supports partial quantities.

DestinationReservation {cell, stack_target?, volume/mass budget, job_id, ttl} — prevents two dwarves targeting same cell/stack.

TTL refreshes while job alive; lapses on timeout/failure.

1.4 Hauling Task (TaskTicket)
Minimal DTO the Job Scheduler understands:

csharp
Copy code
public readonly record struct HaulTask(
  ulong TaskId,
  HaulKind Kind,                  // Pull / Supply / Dump / Trade
  SourceSpec[] Picks,             // ordered pickups (stack_handle, qty)
  DestSpec Destination,           // cell + optional stack target
  ushort RequiredSkillTier,       // usually 0 (no skill)
  ushort Priority,                // 0..100 (from designation/zone/job)
  uint Seed);                     // deterministic tie-breaker
2) UPDATE_ORDER hooks (when things happen)
Read phase (parallel): HaulingAdvisor scans zones & items; generates TaskOffers. Job Scheduler accepts top offers (deterministic), assigns agents and posts HaulTasks.

Write phase (serialized): commit reservations; mutate stacks (split/merge); move items along path step(s); release/carry-over reservations.

Events: UI/log updates.

Snapshot: reflect new stacks/placements.

All mutations (split/merge/move) occur only in Write via Diff-Log or Chunk-Actor. Determinism: stable scans, stable sorts, named RNG unused or fixed-seed.

3) Hauling flows (v1)
3.1 Stockpile Pull (zone-driven)
Goal: fill zone to target_fill with eligible items found outside (or in wrong zones).

Offer generation (per zone):

If fill_ratio ≥ target_fill → skip.

Enumerate candidate stacks S within scan_radius that pass the zone’s filter and are not reserved, not forbidden, and not already stable in a suitable zone (stickiness, see §5).

Score each (stack → best_destination_cell) using §4; keep top K by score.

Build TaskOffers: each includes a primary pickup and, if allowed, multi-pick bundle (nearest extra pickups) bounded by capacity (§6).

3.2 Construction Supply (blueprint-driven)
For each active blueprint/material need, pick a feeder zone (or world) to source from using the same scoring and filters as stockpiles, but destination is the blueprint’s input cell.

If blueprint declares io_policy = input_only, forbid pull-outs from it.

3.3 Cleanup / Dump
Items marked forbidden=false && trash=true (or area “dump” designation) become candidates to move into a Dump zone (wide-accept filter, low priority).

3.4 Trade Depot (optional)
If depot exists and is “awaiting goods”, generate offers that bring trade_allowed=true stacks to depot cells.

4) Scoring (deterministic)
A candidate (stack s → dest d) gets a score:

ini
Copy code
value_score   = V_item(s) * Q(s) * k_value
distance_cost = PathDistance(s.pos, d.pos) * k_distance
urgency_bonus = U(zone or blueprint) * k_urgency
fresh_bonus   = (s.flags.fresh ? k_fresh : 0)
stickiness    = (s.last_stockpile_id == zone.id ? k_stick : 0)  // discourage useless moves
hazard_pen    = HazardAlongPath(s.pos, d.pos) * k_hazard        // optional

Score = value_score + urgency_bonus + fresh_bonus + stickiness - distance_cost - hazard_pen
All k_* constants are data-driven (tuning.hauling.json).

PathDistance uses cached A* cost estimate (heuristic only) in Read; final move uses full path in execution.

Tie-breakers: higher Score → prefer; then smaller distance_cost; then stack_handle asc; then dest.idx asc.

Value function (default):

ini
Copy code
V_item = base_value(item_id) * material_mult(material) * quality_mult(if any)
If no economy yet, base_value=1 (effectively: “fill nearest first”).

5) Anti ping-pong (hard rules)
Dwell time: item placed into a zone cell gains placed_at_tick = now; it is ineligible for re-haul into another zone for dwell_ticks_min unless:

current zone no longer accepts it (filter changed), or

destination is a blueprint need, or

task priority ≥ threshold (emergency).

Stickiness: when two zones both accept an item, add negative weight to moving away from last_stockpile_id.

No cross-zone rebalancing in v1 (feature flag off). A future rebalancer (v2) would run rarely with strict hysteresis.

6) Capacity, multi-pick, and carry limits
6.1 Carry capacity
ini
Copy code
carry_limit_mass   = max(0, Actor.Strength * k_str_to_mass - base_gear_mass)
carry_limit_volume = Actor.BodySlotsFree * k_slot_vol             // coarse proxy
move_mult = 1 + k_enc * (carried_mass / max(1, carry_limit_mass)) // used by NAV
6.2 Multi-pick bundling
Allowed if allow_multi_pick=true and candidate pickups are within bundle_radius from the primary path (projected path or k-NN to primary).

Greedy add items by Score per added cost until:

mass/volume would exceed carry limits, or

added detour would exceed bundle_detour_max, or

max_bundle_items reached.

6.3 Stacking & merging
At destination, if an existing compatible stack is present, merge (respect max_stack_qty).

Otherwise place new stack if cell policy allows; else fallback to another eligible cell in the same zone (deterministic order).

7) Reservations, TTLs, retries
When assigning a task (Write phase):

Reserve each (stack_handle, qty) and the destination (cell, space).

TTL:

ItemReservation TTL = k_item_ttl_ticks; DestinationReservation TTL = k_dest_ttl_ticks.

Refresh TTL when the assignee reports progress; lapses → reservations auto-release.

Retry / backoff (deterministic):

If path fails → exponential backoff per source (k_backoff_base), with cap; enqueue a new offer later.

If destination full on arrival → try next eligible cell; if none → mark ZoneFull and cooldown the zone (zone_cooldown_ticks).

8) Pathing & execution
Planning (Read) uses heuristic distance; full A* path is computed by the agent on start (Read).

Execution (Write) advances along 1–3 steps per tick; if ConnectivityVersion on any future step changes → Replan.

Encountered units are handled by execution’s yielding, not by replan, unless timeout.

9) LOD cooperation
Hauling tasks are targeted to L0/L1 chunks.

If either the source or destination is sleeping (L2+):

Option A (preferred): the advisor simply does not generate offers there.

Option B: ask LOD to Pin/Promote for a short TTL; only then generate a task.

Active tasks encountering a sleeping dest mid-run must fail fast with TargetSleeping and trigger zone cooldown.

10) Determinism & budgets
All scans and sorts use stable keys; no RNG needed in v1.

Per-tick advisor budget: max_cells_scanned, max_offers_per_zone, max_tasks_accepted.

If budgets hit, zones continue next tick from stable cursors (serialized cursors per zone).

11) Error handling (policy excerpts)
Missing stack / changed quantity at commit → task aborts; release reservations; backoff source.

Destination cell invalid (zone changed) → pick next eligible cell; if none, abort with ZoneChanged.

Exceptions are caught at system boundaries; offending zone/source quarantined for zone_quarantine_ticks.

12) Tuning (data-driven)
/content/registries/tuning.hauling.json (example):

json
Copy code
{
  "scoring": {
    "k_value": 1.0,
    "k_distance": 0.05,
    "k_urgency": 0.5,
    "k_fresh": 0.1,
    "k_stick": -0.3,
    "k_hazard": 0.2
  },
  "capacity": {
    "k_str_to_mass": 5.0,
    "k_slot_vol": 25.0,
    "k_enc": 0.5,
    "allow_multi_pick": true,
    "bundle_radius": 6,
    "bundle_detour_max": 10,
    "max_bundle_items": 4
  },
  "reservations": {
    "item_ttl_ticks": 800,
    "dest_ttl_ticks": 800,
    "zone_cooldown_ticks": 600,
    "backoff_base_ticks": 150
  },
  "advisor_budget": {
    "max_cells_scanned": 2048,
    "max_offers_per_zone": 16,
    "max_tasks_accepted": 32
  },
  "anti_pingpong": {
    "dwell_ticks_min": 2000,
    "rebalancer_enabled": false
  }
}
13) Pseudocode (advisor → scheduler)
csharp
Copy code
void GenerateOffersForZone(Zone z)
{
  if (z.FillRatio >= z.TargetFill) return;

  var candidates = ScanCandidates(z);            // deterministic iteration over cells
  foreach (var s in candidates)
  {
    if (!FilterAllows(z, s) || s.Flags.Forbidden) continue;
    if (IsStickyInPlace(s, z)) continue;

    var d = ChooseBestDestinationCell(z, s);     // stable order by idx
    if (d == null) continue;

    var score = Score(s, d, z);
    var offer = BuildOffer(s, d, score);         // may bundle extra picks
    zoneOffers.Add(offer);
    if (zoneOffers.Count >= K_maxOffers) break;
  }
  SubmitTopOffers(zoneOffers.OrderByScoreStable());
}
14) APIs
14.1 Read (thread-safe)
csharp
Copy code
public interface IHaulingQuery {
  IEnumerable<(int idx, ItemStackRef s)> EnumerateZoneCandidates(ZoneId z);
  bool ZoneAccepts(ZoneId z, in ItemStackRef s);
  float HeuristicCost(in GridPos a, in GridPos b);         // NAV estimate
  bool PathExistsRough(in GridPos a, in GridPos b);        // cheap reject
  (GridPos cell, bool ok)? FirstFreeCell(ZoneId z, ItemProto p, Material m);
}
14.2 Write (single writer per chunk)
csharp
Copy code
public interface IHaulingCommit {
  bool ReserveItem(ItemHandle h, int qty, JobId job, int ttl);
  bool ReserveDestination(GridPos cell, ItemProto p, Material m, JobId job, int ttl);
  bool SplitStack(ItemHandle h, int takeQty, out ItemHandle newHandle);
  void MergeOrPlace(ItemHandle h, GridPos cell);
  void ReleaseReservations(JobId job);
}
15) Tests (must-haves)
Determinism: fixed seed + same world → identical offers & accepted tasks.

Anti ping-pong: two zones both accept iron bars → after placing bars in Zone A, no immediate move to Zone B.

Capacity: multi-pick never exceeds carry limits; encumbrance affects move multiplier.

Reservations: race of two dwarves targeting same stack resolves deterministically; TTL expiry releases properly.

Failures: path break / dest full → correct fallback or cooldown.

Budgets: advisor respects caps and continues with cursors; outcome stable across runs.

16) Extension points (v2)
Containers/Bins in stockpiles; carts/vehicles (capacity objects).

Global Rebalancer with hysteresis (rare, out-of-band).

Hazard-aware routing (integrate Incident threat maps).

Work-in-progress outputs: workshop output_only buffers.

Rationale (why these choices)
Zones as areas keep memory low and UI simple; the logic lives in Advisor/Scheduler rather than per-entity brains.

Deterministic scoring + reservations eliminate classic DF/RW ping-pong and “two haulers, one item” races.

Read-propose / Write-commit matches our engine’s concurrency model, making correctness testable and replayable.

This document is normative. Any optimization must preserve determinism, budgets, reservations, and UPDATE_ORDER placement.