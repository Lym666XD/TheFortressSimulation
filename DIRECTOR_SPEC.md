INCIDENT_DIRECTOR_SPEC.md — v1 (Unified, Fortress-Only)

This is the authoritative Storyteller / Incident Director spec for our game. It unifies our current fortress-only architecture (single local map, edge-band spawns, LOD cooperation, deterministic tick loop) with the stronger details from the prior “DIRECTOR_SPEC” draft (budgeting, queues, cooldowns, safety rails, persistence, executors). Where that draft referenced encounter/ambush maps, we keep those as reserved interfaces only; v1 executes all incidents in the fortress map. 

DIRECTOR_SPEC

0) Status & Scope
id: storyteller.v1
status: normative
owner: gameplay/narrative
last_updated: 2025-09-15
applies_to:
  - NarrativeDirector (Storyteller)
  - IncidentRegistry & Executors
  - RegionInstanceManager (RIM)
  - TickScheduler / SIM_LOD_POLICY / SAVE_FORMAT
principles:
  - Pacing without overload (challenge arcs, recovery windows).
  - Determinism across OS/thread counts (named RNG streams + stable orders).
  - Data-driven tuning (JSON registries; no hard-coded weights).
  - Recoverable: director state saved & restored with world.
binding_for_v1:
  - Fortress-only: incidents play out **on the fortress map** via edge-band spawners.
  - No Adventure/Encounter maps in v1 (interfaces reserved, not used).
  - Heavy incidents must target **L0/L1** chunks; LOD pin/promote when needed.

1) Core Concepts

NarrativeDirector: orchestrates pacing. Computes ThreatBudget, selects IncidentPlans, schedules, and settles results.

Incident: content template with category, gates, triggers, weights, executor id, and params.

IncidentPlan: a concrete plan {incident_id, plan_id, scheduled_tick, target, scale, seed}.

Executor: code module that enacts a plan (spawn raid, send caravan, start weather/disease, etc.).

Pacing State: cooldowns, grace periods, momentum (bias to harder/softer), active incident handles, and short history.

(Adapted and tightened from the older draft while enforcing fortress-only execution.) 

DIRECTOR_SPEC

2) Deterministic Inputs (read at tick barrier)

Colony state → population, combat power, wealth, morale/mood proxy, injuries, in-progress blueprints, secured food days.

Time since last X → per-category cooldown timers.

Calendar & Biome → season/day-night bands; biome modifiers.

Faction Relations → goodwill/wars/treaties, recent diplomacy.

Faction Memory → POIs/sightings/rumors unlock or weight incidents.

Player Focus → camera center, pinned targets, LOD distribution.

Content Flags → enabled packs/mods, difficulty preset, director style.

All values come from immutable world snapshot at the end-of-tick barrier. 

DIRECTOR_SPEC

3) Outputs

IncidentQueue (sorted): pending IncidentPlan entries ordered by (scheduled_tick, plan_id).

ActiveIncidents: runtime handles with plan state (participants/progress).

Telemetry: compact events for UI/CI (never fed back into selection logic). 

DIRECTOR_SPEC

4) Director Tick — Life Cycle (normative)

Director ticks every general.director_tick_every sim ticks (default 75).

Compute ThreatBudget B

B = Curve_Wealth(pop, wealth) ⊕ Curve_Combat(power)
  ⊕ Pressure(t_since_last_by_category)
  ⊖ Softeners(low_mood, heavy_injuries)
clamp to [Bmin, Bmax]


Store B_raw and B_final for UI/telemetry. 

DIRECTOR_SPEC

Generate Candidates
Filter incidents by Gates (biome/season/tech/cooldown/relations/LOD-capable). Evaluate Triggers (boolean) and Weights (continuous). Produce candidates {id, weight, min/max scale, category}.

Safety Rails
Reject candidates that violate safety: e.g., early grace, doctor-required diseases, sapper ban without walls, per-day caps, simultaneity cap for majors. (All data-driven; see §11 Tuning.) 

DIRECTOR_SPEC

Score & Select
Convert weights into a stable lottery using RNG stream Storyteller/Global seeded by (world_seed, director_epoch). Select 0..N until budget B is consumed; pick scale via curve scale=f(B, colony) with deterministic tiebreakers. 

DIRECTOR_SPEC

Schedule
Assign scheduled_tick = now + jitter, with jitter from Storyteller/Schedule (bounded). Determine Target:

Fortress-only: pick edge-band sector(s) deterministically from a discretized ring (N sectors), biased by approach logic and camera focus.

Heavy incidents require L0/L1 targets; if target chunk is L2+, PinLOD(chunk, ttl) before execution (see §10). 

DIRECTOR_SPEC

Enqueue
Push IncidentPlan to IncidentQueue; update category cooldowns and director momentum.

Execute Due Plans
For every plan with scheduled_tick ≤ now:

Resolve per-plan seed = H(world_seed, plan_id) and target.

Call IIncidentExecutor.Execute(plan). Executors may:

send Mailbox messages to chunk actors,

spawn actors/jobs,

start weather/disease modifiers,

(Reserved only; not used in v1) request RIM to create an ephemeral encounter instance.
Move plan to ActiveIncidents until completion/timeout.

Settle
Record outcome (loss/gains), adjust momentum (bias), emit FactionMemory updates, refresh cooldowns. 

DIRECTOR_SPEC

5) Categories & Executors (data-driven)

v1 supported categories:

Raid (skirmish/siege-lite/sapper-lite)

Beast (manhunter pack / mega-fauna)

Caravan (visitor/trader/tribute)

Refugee (joiner / chased)

Disease (flu / plague-lite; bounded spread)

Weather (heatwave/cold snap/sandstorm; multipliers)

Infestation (cavern emergent)

Quest (reserved; no Adventure in v1)

Disaster (fire start / cave-in-lite; tightly rate-limited)

Each category defines shared gates, cooldowns, safety rails, executors, and UI strings. 

DIRECTOR_SPEC

Incident Registry — /content/registries/incidents/*.incident.json

{
  "id": "raid_bandits_sapper",
  "category": "Raid",
  "gates":   { "biomes": ["temperate","boreal"], "season": ["spring","summer"], "tech_min": 0 },
  "triggers": { "min_pop": 4, "min_walls": 20 },
  "weight": 1.0,
  "executor": "exec.raid.sapper",
  "params": { "entry_style": "random_edges", "use_sappers": true, "siege_chance": 0.20 }
}

6) Determinism

Named RNG streams:

Storyteller/Global (candidate selection)

Storyteller/Schedule (timing jitter)

Incident/<incident_id> (executor-local randomness)

Stable orders for selection, targetting, and participant lists (sort by category/id/plan_id/sector).

No wall-clock/OS calls; tick counters only.

CI: replay reproduces identical IncidentQueue/outcome hashes. 

DIRECTOR_SPEC

7) Persistence (SAVE_FORMAT integration)

Stored under world.meta.mpkz → NarrativeState (or storyteller.state.mpkz).

Fields (conceptual):

NarrativeState {
  epoch: u64,
  last_tick: u64,
  cooldowns: { category: next_allowed_tick },
  momentum: { threat_bias: f32, calm_bias: f32 },
  active_incidents: [ { plan_id, incident_id, started_tick, target, state_blob } ],
  recent_history:  [ { tick, incident_id, result, loss, gains } ]  // ring buffer
}


On load: missing incidents/executors → discard plan with WARN, do not crash. 

DIRECTOR_SPEC

8) Execution Interfaces (code entities)
// Director
public interface INarrativeDirector {
  void Tick(ulong now, in WorldSnapshot snapshot);
  ThreatBudget ComputeBudget(in WorldSnapshot s);
  IReadOnlyList<Candidate> GenerateCandidates(in WorldSnapshot s);
  IReadOnlyList<IncidentPlan> SelectAndSchedule(
      in IReadOnlyList<Candidate> candidates,
      in ThreatBudget budget, ulong now);
}

// Plan DTO
public readonly record struct IncidentPlan(
  ulong PlanId, string IncidentId, ulong ScheduledTick,
  TargetSpec Target, float Scale, uint Seed);

// Executor
public interface IIncidentExecutor {
  bool CanExecute(in IncidentPlan plan, in WorldSnapshot s);
  void Execute(in IncidentPlan plan, IWorldWriter w, IDirectorServices svc);
}


DirectorServices (injected): RIM, JobScheduler, PathService, FactionService, ArtifactLedger, Calendar, LodService.
IWorldWriter: mailbox to chunk actors, spawn/despawn jobs/actors, apply map modifiers (weather/disease). 

DIRECTOR_SPEC

9) Edge-Bands & Targeting (fortress-only binding)

The fortress map border is discretized into N sectors (configurable; default 16).

Sector choice is deterministic: (sectorIndex = H(seed, category, now) % N) with bias rules (avoid repeating last sector, respect wind/approach).

Multi-wave raids allocate adjacent sectors (wrap-around) with per-wave delays.

Spawn points choose L0/L1 chunks close to the sector; if target chunk LOD is > L1, pin/promote via LodService.Pin(chunk, ttl), then execute when promotion is observed. (Or defer the plan until chunks are hot.) 

DIRECTOR_SPEC

10) LOD Cooperation (hard rule)

Heavy incidents (raid/siege/beast) must run on L0/L1 chunks.

If chosen area is L2+, the plan either (a) pins the area (ttl seconds) and waits; or (b) defers execution by a fixed tick delta.

Weather/Disease may run offscreen if designed as background integrators with budgets. (v1 keeps them simple.) 

DIRECTOR_SPEC

11) Tuning Files (data-driven)

Location: /content/registries/tuning.storyteller.json

{
  "general": { "director_tick_every": 75, "max_active_major": 1, "early_grace_ticks": 20000 },
  "budget": {
    "wealth_curve": [[0,0.0],[5000,0.3],[20000,0.7],[60000,1.0]],
    "combat_curve": [[0,0.2],[1000,0.5],[4000,1.0]],
    "pressure_per_1000ticks": 0.02,
    "softeners": { "low_mood": 0.3, "heavy_injuries": 0.4 }
  },
  "cooldowns": { "Raid": 8000, "Beast": 6000, "Disease": 12000, "Weather": 15000, "Quest": 10000 },
  "safety": { "no_siege_before_ticks": 40000, "require_doctor_for_disease": true, "max_disasters_per_day": 1 },
  "weights": { "Raid": 1.0, "Beast": 0.6, "Caravan": 0.8, "Refugee": 0.4, "Quest": 0.5 },
  "scales": { "Raid": { "min": 0.3, "max": 1.5, "curve": [[0,0.3],[0.5,0.8],[1.0,1.5]] } }
}


(All numeric curves are piece-wise linear; clamped.) 

DIRECTOR_SPEC

12) Safety Rails (normative)

Grace: no major threats before early_grace_ticks.

Caps: per-day category caps; at most max_active_major simultaneous majors.

Prereqs: require doctors>0 for disease; prohibit sapper without walls≥threshold; require food_days≥X for trade embargo logic.

Anti-Spike: after large loss, bias to neutral/positive incidents for comfort_ticks.

Terrain/Build checks: prefer skirmish over siege if siege invalid.
(All data-driven, validated pre-schedule.) 

DIRECTOR_SPEC

13) Errors & Recovery

Missing executor → skip plan, log E_INCIDENT_EXECUTOR_MISSING; do not consume cooldown.

Executor throws → fail closed: despawn spawned actors, release jobs/reservations; quarantine executor with cooldown_on_error.

RIM failures (reserved API) → convert to rumor/POI; never crash. 

DIRECTOR_SPEC

14) CI & Determinism Gates

Replay tests: fixed seed run creates identical IncidentQueue/outcome hashes at checkpoints.

Cross-thread/OS parity: same plans and results.

Chaos: injected exceptions and jitter must not violate safety rails or crash.

Content fuzz: perturb weights within schema ranges; selection remains deterministic for given inputs. 

DIRECTOR_SPEC

15) Non-Goals (v1)

No multi-map sieges running simultaneously.

No ambush/quest maps (Adventure deferred).

No ML pacing; hand-tuned curves only. 

DIRECTOR_SPEC