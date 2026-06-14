ZONE_SPEC.md (v1)

Status: Draft for implementation
Scope: Linkless zones (no manual furniture linking), minimal but robust Phase-1 set, deterministic & perf-friendly.
Engine assumptions: Diff-Log model (parallel Read → barrier → single-writer Merge/Commit), chunked world, Nav cost masks, input is data-driven via input.bindings.json.

0) Goals & Non-Goals

Goals

Minimal, stable zone system that:

Works with our Diff-Log/Single-Writer architecture.

Requires no room enclosure detection and no manual furniture linking.

Gives clear, non-abusable mood/permission effects.

Is performant (incremental indexes, event-driven).

Historical flavor without modern terms.

Non-Goals (for v1)

No automatic room enclosure detection.

No private toilet (Privy)—only public Latrine for now.

Assembly auto-naming (Great Hall vs Plaza) is documented here but not implemented in v1.

No sleep support inside Military Grounds (barracks deferred).

No tournaments/ceremonies yet (deferred).

1) Zone Taxonomy (Phase-1 + Deferred markers)
Production

Lumbering Zone — Candidate area for tree felling tasks.

Gather Plants Zone — Candidate area for plant/fruit collection.

Fishing Zone — Candidate shoreline/water adjacency for fishing.

Sand/Clay Zone — Candidate area for sand/clay gathering (material-tag gated).

Pasture / Animal Training Zone — Animals’ allowed roaming/training area.

Deferred: Pit/Pond (requires fluid system).

Civil (Livelihood & mood)

Bedroom — Single/Multi selection in UI; mood on sleep/use.

Dormitory — Shared sleeping; lower mood than Bedroom.

Dining Hall — Mood on eating/use.

Bathhouse / Steam Room — Hygiene mood placeholder (plumbing/heat later).

Tomb — Burial/commemoration mood on use/visit.

Public (Assembly, religion, social, admin)

Assembly — Generic assembly zone.
Auto-naming doc only:

Indoor → display as Great Hall

Outdoor → display as Plaza
(Indoor/Outdoor detection via SkyExposed ratio with hysteresis—documented in §8, deferred in v1.)

Temple — Prayer/ritual mood on use.

Tavern / Inn — Social/entertainment mood on use.

Hospital (basic) — Treatment/aid mood on use.

Office (admin) — Administrative work anchor (light effect in v1).

Library (monastic reading) — Study/reading mood on use.

Deferred: Council Chamber (parliament style), Guildhall, Arena/Tournaments.

Military

Military Grounds — Unified military yard (no sleep in v1):

Melee Drills (training dummies / weapon racks).

Archery Range (targets + safe lane check).

Arena/ceremonies deferred.

Management

Burrow — Movement permission whitelist; hard veto for creatures assigned to the burrow.

Restricted Traffic — Raises nav cost (does not veto). Compatible with our Traffic MetaBits.

Stockpile

Handled by a separate system. Stockpiles may overlap any zone, with no coupling.

2) Linkless Model (No manual furniture linking)

Principles

Use-Driven Effects: Effects/mood are granted only when a corresponding action occurs (sleep/eat/pray/train/fish/use latrine). Merely standing in a zone does not grant effects.

Nearest-in-Zone Ownership: When an action is executed, we find the nearest matching furniture and assign its effect to the zone(s) that contain that furniture cell. If multiple zones contain it, resolve ties deterministically:

Higher zone priority → Larger zone area → Smaller ZoneId.

Same-Category Max: Effects are grouped by category (Sleep, Eat, Social, Religious, Hygiene, Training, Medical, Admin…). If multiple zones could apply for the same category, take the highest single effect only. Different categories may co-exist (e.g., Sleep + Eat).

Anti-abuse soft limits

Per action, at most K furniture references are considered (usually K=1; social venues may allow small K>1 such as bar + instrument).

Effects require a minimum use time (ticks) to trigger (no tap-to-farm).

3) Furniture & Action Mapping

Furniture (placeables) must expose tags (in ITEMS_SPEC):
table, chair, altar, bar, training_dummy, weapon_rack, archery_target, latrine_seat, bath_vat, sickbed/cot, lectern/bookshelf, etc.

Action → Furniture → Category examples

Sleep → bed/cot → Sleep

Eat/Drink → table+chair or bar → Eat/Social

Pray → altar → Religious

Train (melee) → training_dummy or weapon_rack → Training

Train (archery) → archery_target (safe lane) → Training

Use latrine → latrine_seat → Hygiene

Study/Read → bookshelf/lectern → Admin/Study

Treat/Be-Treated → sickbed/cot → Medical

The exact action enum is owned by Simulation; Zone applies category effects based on (action, usedFurnitureCell).

4) Data Model
4.1 ZoneDefinition (content)
{
  "id": "assembly",
  "category": "public",
  "display_name": "Assembly",
  "ui_hints": { "glyph": "...", "color": "#...", "keybind": "X+Z" },
  "default_policies": {
    "allows_actions": ["social", "admin", "ceremony?"],
    "nav_cost_mode": "none" // or "restricted"
  },
  "default_mood_profile": {
    "category_effects": [
      { "category": "social", "value": 2, "duration_ticks": 400 }
    ]
  },
  "requires_furniture_tags": [
    // optional, linkless lookup accelerators for candidate caches
  ],
  "notes": "Indoor/Outdoor auto-name documented; see §8."
}

4.2 ZoneInstance (runtime)
{
  "zone_id": 1001,
  "def_id": "assembly",
  "name": "Main Assembly",
  "priority": 0,
  "subtype": "auto", // "indoor" | "outdoor" when feature ships; v1 keeps "auto" as inert
  "cells": { /* per-chunk shards: bitsets or RLE */ },
  "z_mode": "single", // future: "range"
  "policies": { /* optional overrides; whitelist only */ },
  "mood_profile": { /* optional override */ },
  "tags": ["public", "assembly"],
  "owner": null,      // reserved, not used in v1
  "assigned": null,   // reserved (burrow uses separate assignment mapping)
  "enabled": true
}

4.3 Chunk Stock (ZoneShard)

ZoneShard per (ZoneId, ChunkKey):

memberCells (bitset / sparse set)

Candidate caches (by furniture role/tag for this zone type):

e.g., tables[], altars[], dummies[], targets[], latrineSeats[] (cell indices)

Derived flags: hasArcherySafeLane (per target)

Candidate caches update when:

Zone paint/erase edits cells in this chunk.

Placeable (with relevant tag) is added/moved/removed in this chunk.

Nav/traffic or blocking changes invalidate archery safe lanes.

5) Effects & Overlap Rules

Award trigger: ZoneEffects.TryAward(actor, action, usedFurnitureCell?)

Locate nearest matching furniture in actor’s chunk(s) using per-chunk tag index.

Filter to zones whose cells contain the furniture cell.

Resolve ownership by priority → area → ZoneId.

Identify effect category from action (Sleep/Eat/Social/Religious/Hygiene/Training/Medical/Admin).

Within the same category, apply only the highest single effect (value & duration).

Enforce min use time and K-cap if applicable.

OnEnter/OnExit: maintained for telemetry/flags (and for zones that give ambient “enter once” effects), but v1 relies primarily on use-driven effects to avoid polling.

Burrow:

Assignment lives outside ZoneInstance (mapping creatureId/squadId → burrowZoneId).

Path request veto: if target cell not in assigned Burrow → disallow route.

(UI: burrow assignment panel belongs to Management.)

Restricted Traffic:

Writes a traffic “Restricted” flag to MetaBits for member cells.

A* uses this to increase base cost (no hard veto).

6) Production Zones → Planner Interface

Each Production zone contributes a candidate cell set for its activity:

Lumbering → tree cells.

Gather Plants → harvestable plant cells.

Fishing → shoreline cells with water adjacency.

Sand/Clay → surface material gated cells.

Pasture/Training → allowed animal roam/training cells.

Incremental maintenance

Candidate sets are maintained by ZoneShard using chunk-local material/feature indexes.

Updates are triggered by zone paint/erase, tile write (material/feature changes), and item placement (rare).

Planner reads candidate sets only during Read phase—no full map scans.

7) Concurrency & Determinism

Read Phase: Fully parallel per chunk:

Build diffs for zone edits, candidate cache refreshes, archery lane checks.

ZoneEffects.TryAward produces no state changes—only reads and schedules mood tokens via diffs if needed.

Merge/Write Phase: Single writer per chunk:

Sort diffs deterministically: (tick, opType, zoneId, chunkKey, cellIndex, localSeq).

Apply changes to ZoneShard.memberCells, candidate caches, MetaBits (Restricted Traffic), and mood/flags.

Cross-chunk: Not required in v1 (zones may span chunks via per-chunk shards; no cross-chunk mutation contention).

8) Assembly Auto-naming (Documented, deferred)

Intent: Assembly displays as Great Hall (indoor) or Plaza (outdoor) automatically.

Method (documented, not implemented in v1):

Maintain per-cell SkyExposed derived bit (raycast upward; roof blocks).

For a zone, compute ratio of SkyExposed cells:

≥ 60% → subtype=outdoor → display “Plaza”.

≤ 40% → subtype=indoor → display “Great Hall”.

Hysteresis (40–60%) prevents flip-flop.

Re-evaluate only when:

Zone cells change, or

Affected chunks’ ConnectivityVersion increments (roof/terrain edits).

9) Military Grounds (v1)

Zone: military_grounds
Features in v1:

Melee Drills

Uses training_dummy or weapon_rack furniture tags.

Candidate tiles computed from furniture positions (small radii).

Archery Range

Uses archery_target tag.

Safe lane check (minimal): From target backward along its facing:

Straight line length ≥ 3.

No standable tiles in lane (to prevent passersby).

Ends at the target; no blockers in between.

If any target fails lane safety, archery is disabled for that shard with a UI reason: "Unsafe lane".

Deferred: barracks/sleeping, tournaments/ceremonies.

10) Management Zones

Burrow

Assign creatures/squads to a Burrow zone (UI).

Path requests that lead outside assigned Burrow → veto at planner/path entry.

Zone edit (cells) triggers limited Nav invalidation in affected shards.

Restricted Traffic

Mark member cells as Restricted traffic in MetaBits.

A* incorporates nav cost uplift (no hard block).

Zone edit triggers local pathing cache invalidation.

11) UI & Input (essentials)

Zones are painted/erased by brushes (rectangle/paint/erase). No enclosure detection.

Linkless: UI shows “Active furniture (auto)” lists per zone (read-only), derived from candidate caches.

Overlays: Toggle to visualize zone boundaries and candidate highlights (e.g., archery lanes).

Keybinds: Controlled via input.bindings.json.
(Default menu entry points match your Z/X/C menus; details in INPUT_SPEC.)

12) Validation Rules (content & runtime)

ZoneDefinition:

category in {production, civil, public, military, management}.

default_policies can only toggle whitelisted flags (no arbitrary overrides).

default_mood_profile.category_effects[*].category in known categories.

Runtime:

Zone cells must exist on valid tiles (skip invalid).

Military archery lanes must pass safety; otherwise disabled with reason.

Candidate caches update on zone edit, furniture move, tile writes only (no periodic scans).

13) Performance Notes

Per-chunk tag index for placeables → O(1)/O(logN) nearest by small search radius.

ZoneShard candidate caches → avoid scanning entire chunk/map each tick.

Use-driven effects → no polling; triggered by action hooks.

Deterministic sort in Merge to resolve conflicts predictably.

14) Phase Plan

Phase-1 (this spec)

Production: Lumbering, Gather Plants, Fishing, Sand/Clay, Pasture/Training.

Civil: Bedroom, Dormitory, Dining Hall, Bathhouse (placeholder), Tomb.

Public: Assembly, Temple, Tavern/Inn, Hospital (basic), Office, Library.

Military: Military Grounds (Melee + Archery).

Management: Burrow, Restricted Traffic.

Stockpile: Separate spec/system (coexists).

Linkless model; no auto enclosure; Assembly auto-name documented only.

Phase-2 (deferred)

Assembly auto-name implementation (SkyExposed + hysteresis).

Council Chamber, Guildhall, Arena + Tournaments.

Bathhouse plumbing/heat requirements; sanitation loops.

Ownership/factions/assignments per zone (doors/permissions).

Advanced archery lanes (multiple, crowd barriers, schedules).

15) Appendices
A. Standard Categories (for effects)

Sleep, Eat, Social, Religious, Hygiene, Training, Medical, Admin/Study.

B. Tie-breakers when multiple zones contain the used furniture cell

Higher priority → 2) Larger area (member cell count) → 3) Smaller zone_id.

C. Diff Types

AddZone, RemoveZone, UpdateZoneCells

SetZoneProperty (priority, enabled, subtype when feature ships)

ZoneAssignBurrow (creature/squad ↔ zoneId)

SetTrafficRestricted (per-cell)

ZoneCandidateCacheRefresh (per shard)

Notes for implementation:

Keep all write operations local to single-writer chunk merges.

Use incremental invalidation (ConnectivityVersion / local queues) for Nav & candidate caches.

UI should surface reasons when features are disabled (e.g., “Archery: unsafe lane”).

Expose debug overlays for: zone boundaries, candidate furniture, archery lanes, SkyExposed (when implemented).