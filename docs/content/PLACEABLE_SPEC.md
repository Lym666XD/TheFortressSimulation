PLACEABLE_SPEC.md

Version: v1.1
Status: Draft-for-implementation (fits Diff-Log, single-writer, per-chunk indices)
Last Updated: 2025-01-10

0) Purpose & Goals

This spec defines placeable world entities (furniture, workshops, utilities, traps, fixed defenses) and how they are created, indexed, interacted with, and persisted.

Design goals:

 DF-style furniture: craft as items, then install (and later uninstall → restore the same item GUID, quality, material, decorations, maker mark).

Constructions on site: walls/floors/bridges/channels/workshops are built directly from materials (not prebuilt items).

Diff-Log correctness: read phase plans & diffs, write phase merges per-chunk (single writer), cross-chunk via mailbox.

Data-driven: JSON content, zero hardcoding, tags for behaviors.

Low coupling: Zones consume per-chunk TagIndex (linkless), Jobs/Hauling operate through standard reservations.

Simplicity: Workshop materials/outputs live on workshop footprint cells (no virtual bins); interrupted crafting restarts from scratch (no WIP state).

1) Terminology

Installable: An item that can be installed into the world as a placeable (e.g., core_item_furniture_bed). Quality, material, and decorations come from the source item.

Construction: A placeable built on site from raw/processed materials (e.g., wall, floor, workshop). No quality system; always standard. Deconstruct returns 100% of materials.

Hybrid: Requires both on-site build and specific items (e.g., well = masonry + bucket + rope, trap = mechanism + weapon/cage).

PlaceableInstance: Runtime entity representing the installed/constructed thing occupying world cells.

Construction Site: Temporary placeable entity representing an in-progress construction. Stores materials_delivered and build_progress_ticks.

2) Fit With Architecture

Phases: Read → Barrier → Write (merge diffs).

Single writer per chunk: All placeable mutations for a chunk are merged deterministically in its write phase.

Per-chunk indices: Occupancy map & TagIndex for fast queries; Zones & planners read these (no global locks).

Mailbox: Cross-chunk reservations and notifications (e.g., hauling to install site).

3) Categories

Furniture (beds, chairs, tables, cabinets, chests, doors, braziers, torches, bookshelves, statues, etc.) — Installable (crafted items).

Workshops (carpentry, stoneworks, smelter, kiln, still, kitchen, forge, clothier, tanner, butcher, etc.) — Construction (5×5, no quality, deconstruct returns 100% materials).

Utilities (bridges/gates, wells/cisterns, grates/hatches) — Construction or Hybrid.

Traps (spike/cage/weapon trap bases) — Hybrid (construction + mechanism + weapon/cage items).

Fixed defenses (ballista, trebuchet, catapult) — Backend: Vehicle (is_static=true, crew_slots, mount_points, ammo); Frontend: "Defense Structures" UI category.

4) Data Model
4.1 Items: Installable profile (extends ITEMS_SPEC)

File: content/registries/items/*.json (existing)

Each installable item adds a placeable_profile:

{
  "id": "core_item_furniture_bed",
  "kind": "furniture",
  "installable": true,
  "allowed_material_tags": ["wood","stone","metal"],
  "base_volume_ml": 80000,
  "base_mass_g": 15000,
  "stack": { "mode": "none" },
  "placeable_profile": {
    "footprint": { "w": 2, "d": 1, "h": 1 },
    "anchor": "topleft",
    "orientation_mask": ["N","E","S","W"],
    "passability": "nonblocking",         // blocking | doorway | nonblocking
    "requires_floor": true,
    "clearance_h": 1,                      // vertical clearance above floor
    "blocks_light": false,
    "tags": ["furniture","sleep","bed"],   // feeds TagIndex & Zone candidates
    "effects": { "beauty": 2, "comfort": 3, "light_lumen": 0, "heat_w": 0 }
  }
}


Notes

The item remains the single source of material, quality (−3..+3), decorations, maker mark, durability stage, etc.

Doors/windows/grates are best modeled as installable items with passability: "doorway".

4.2 Constructions registry

File: content/registries/constructions.json

Defines constructions built on site (no source item):

{
  "id": "core_construction_wall_stone",
  "name": "Stone Wall",
  "category": "construction",
  "footprint": { "w": 1, "d": 1, "h": 1 },
  "orientation_mask": ["N","E","S","W"],
  "passability": "blocking",
  "requires_floor": false,
  "clearance_h": 1,
  "blocks_light": true,
  "materials_required": [
    { "tag": "stone_block", "count": 4 }
  ],
  "build_time_ticks": 600,
  "required_skill": "construction",
  "skill_level_min": 0,
  "effects": { "beauty": 1 }
}


Workshop example (5×5, no quality):

{
  "id": "core_construction_workshop_carpentry",
  "name": "Carpentry Workshop",
  "category": "workshop",
  "footprint": { "w": 5, "d": 5, "h": 1 },
  "orientation_mask": ["N","E","S","W"],
  "passability": "nonblocking",          // workers walk on workshop cells
  "requires_floor": true,
  "clearance_h": 2,                       // needs headroom
  "blocks_light": false,
  "materials_required": [
    { "tag": "wood_log", "count": 10 },
    { "tag": "stone_block", "count": 5 }
  ],
  "build_time_ticks": 2400,
  "required_skill": "construction",
  "skill_level_min": 2,
  "effects": { "beauty": 0 },
  "workshop_profile": {
    "recipes": ["core_recipe_wooden_chair", "core_recipe_wooden_table", "core_recipe_wooden_door"],
    "work_skill": "carpentry",
    "max_queued_recipes": 10
  }
}


Hybrid examples (well base, trap base) live here too, referencing required item slots in hybrid_requirements (e.g., bucket, rope, mechanism, weapon, cage).

{
  "id": "core_construction_trap_spike",
  "name": "Spike Trap",
  "category": "trap",
  "footprint": { "w": 1, "d": 1, "h": 1 },
  "passability": "nonblocking",           // creatures walk over it (then trigger)
  "requires_floor": true,
  "materials_required": [
    { "tag": "mechanism", "count": 1 }
  ],
  "hybrid_requirements": [
    { "item_tag": "weapon_trap", "count": 1 }  // installed weapon item
  ],
  "build_time_ticks": 800,
  "trap_profile": {
    "trigger": "pressure",               // pressure | manual | timed
    "reset_time_ticks": 100,
    "damage_source": "installed_weapon"  // uses installed weapon's damage
  }
}


4.3 PlaceableInstance (runtime)
{
  "guid": "p_6b1f...",

  "kind": "installable",            // installable | construction | construction_site | hybrid
  "def_id": "core_item_furniture_bed",  // for installable: item def id
  "construction_id": null,              // for constructions/hybrids/sites: construction def id
  "source_item_guid": "i_93de...",      // installable only; null for constructions

  "anchor_world": { "x": 120, "y": 85, "z": 10 },
  "rotation": 1,                        // 0=N,1=E,2=S,3=W
  "owner_faction_id": null,
  "owner_creature_guid": null,
  "use_policy": "public",               // public | faction | private

  "quality_tier": 0,                    // mirrors item (installable) or N/A (construction)
  "condition_state": "Pristine",
  "enabled": true,

  "workshop_state": null,               // optional for workshops (queued recipes, active job)
  "trap_state": null,                   // optional for traps (armed/last_trigger)
  "construction_site_state": null       // optional for construction_site kind
}


Workshop state (for completed workshops):

"workshop_state": {
  "queued_recipes": ["core_recipe_wooden_chair", "core_recipe_wooden_table"],
  "current_job_guid": null,             // managed by JobScheduler
  "paused": false
}


Note: Workshop materials and output items live on the workshop's footprint cells (5×5 = 25 cells). Haulers place materials anywhere on the footprint; crafters reserve and consume them; outputs spawn on any free footprint cell.

Trap state:

"trap_state": {
  "armed": true,
  "last_trigger_tick": 0,
  "installed_items": ["i_weapon_guid"]  // for hybrid traps
}


Construction site state:

"construction_site_state": {
  "target_construction_id": "core_construction_wall_stone",
  "materials_required": {
    "stone_block": 4
  },
  "materials_delivered": {
    "stone_block": 2                    // 2/4 delivered (items on site cell)
  },
  "build_progress_ticks": 150,
  "total_build_ticks": 600,
  "builder_guid": null                  // current builder (if any)
}


Chunk indices

For each chunk we maintain:

OccupancyMap: (x,y,z) → placeable_guid

TagIndex: tag → [placeable_guid] (e.g., "table", "altar", "bookshelf")

Optional typed lists: Workshops, Traps, ConstructionSites

5) Operations (Diffs)

Installable furniture:
DesignateInstall(def_or_item_filter, world_pos, rotation)
ReserveItem(item_guid)                // scheduler internal
HaulToInstallSite(item_guid, pos)
InstallItem(item_guid, pos, rotation)

DesignateUninstall(placeable_guid)
UninstallToItem(placeable_guid)

Constructions (including workshops):
DesignateConstruct(construction_id, pos, rotation)
CreateConstructionSite(construction_id, pos, rotation)
HaulMaterialToSite(material_item_guid, site_guid)
BuildConstruction(site_guid, builder_guid)
CompleteConstruction(site_guid)

DesignateDeconstruct(placeable_guid)
Deconstruct(placeable_guid)          // returns 100% materials for constructions

Workshop operations:
AddRecipeToQueue(workshop_guid, recipe_id)
RemoveRecipeFromQueue(workshop_guid, index)
PauseWorkshop(workshop_guid)
ResumeWorkshop(workshop_guid)

Trap operations:
ArmTrap(trap_guid)
DisarmTrap(trap_guid)
TriggerTrap(trap_guid, creature_guid)


Sorting & merge are deterministic (by chunk, then cell, then priority/system id/local seq).

6) Workflows
6.1 Install furniture (from item)

Player: Build → Install → pick a spot (and optional filter: material/quality/nearest).

Read phase (Jobs):

Find an eligible item (respecting filters), create a reservation;

Emit HaulToInstallSite and InstallItem.

Write phase:

Haul completes → item reaches site;

InstallItem: create PlaceableInstance, write NavMask per passability, update TagIndex, remove the item from world but store its GUID in source_item_guid.

Uninstall: reverse the process; delete placeable, respawn the same item GUID, with same material/quality/decorations.

6.2 Construct on site (including workshops)

Step 1: Designate
Player: Build → Construct → pick construction type & spot

Write phase:
CreateConstructionSite diff → spawn temporary PlaceableInstance with kind="construction_site"

Construction site occupies the target cells (blocks other constructions, but passability depends on construction type).

Step 2: Haul materials
Read phase (HaulingJobSystem):
Scan all construction_sites
For each site, check materials_required vs materials_delivered
If shortfall: generate HaulMaterialToSite jobs

Haulers carry materials to construction site cells (anywhere on footprint).

Write phase:
Items delivered to site cells are NOT consumed yet; they remain as items on the ground.
Construction site tracks materials_delivered count (by scanning items on footprint cells with matching tags).

Step 3: Build
Read phase (ConstructionJobSystem):
Scan construction_sites with sufficient materials
Assign builder (if available)

Builder walks to site, works for build_time_ticks.

Write phase:
BuildConstruction increments build_progress_ticks.
If interrupted (builder walks away, combat, etc.): progress resets to 0 (DF-style, no WIP state).

Step 4: Complete
When build_progress_ticks >= total_build_ticks:

Write phase:
CompleteConstruction diff:
Consume material items (remove from world).
Delete construction_site PlaceableInstance.
Create final PlaceableInstance (kind="construction" or "workshop").
Update OccupancyMap, TagIndex, NavMask.

Deconstruct: Player designates deconstruct; builder works; construction is removed; 100% of materials_required are spawned as items at the site.

6.3 Workshop crafting

Workshop materials live on footprint cells (5×5 = 25 cells).

Step 1: Queue recipes
Player selects workshop (press [q]), adds recipes to queue.

Write phase:
AddRecipeToQueue diff updates workshop_state.queued_recipes.

Step 2: Haul materials
Read phase (WorkshopJobSystem):
For each workshop with queued recipes:
Check footprint cells for required materials (by scanning items on those cells).
If shortfall: generate haul jobs (target = any free cell on workshop footprint).

Haulers carry materials to workshop footprint (any cell, not necessarily empty; items can stack/overlap).

Materials are reserved (item.IsReserved=true) for the active recipe.

Step 3: Craft
Read phase (WorkshopJobSystem):
If materials sufficient and crafter available:
Create CraftJob (references workshop_guid, recipe_id, material_item_guids).

Crafter walks to workshop (stands on adjacent cell or on footprint cell).

Crafter works for recipe.craft_time_ticks.

If interrupted (combat, break, etc.): crafting progress resets to 0; materials remain reserved; crafter can retry or another crafter can take over.

Write phase:
CraftJob completes:
Consume material items (remove from world or decrement stack).
Spawn output item(s) on any free footprint cell (or adjacent if full).
Remove recipe from queue (if one-shot) or keep if repeat.

Step 4: Haul outputs
HaulingJobSystem detects items on workshop footprint (that are not reserved for active recipes).

Haulers carry outputs to stockpiles.

6.4 Hybrids (well, trap)

Combination of both: site + required item slots (e.g., well needs rope & bucket items; trap base needs mechanism + weapon/cage).

Step 1: Designate
CreateConstructionSite for hybrid construction.

Step 2: Haul materials + items
Haul materials (stone blocks, etc.) and required items (rope, bucket, mechanism, weapon) to site cells.

Step 3: Build
Builder works; consumes materials and "installs" the required items (items are removed from world, GUIDs stored in hybrid_state.installed_items).

Step 4: Complete
Spawn final PlaceableInstance (kind="hybrid") with hybrid_state referencing installed items.

Deconstruct: returns materials + spawns the installed items (with original GUIDs/quality).

7) Navigation, Zones & Effects

On install/construct:

NavMask updated by passability (blocking/doorway/nonblocking).

TagIndex updated; Zones build their candidate caches from TagIndex (linkless, data-driven).

Effects (beauty/comfort/light/heat) contribute to room scoring and to OnEnter/OnUse mood awards (handled by ZoneEffects; no per-tick polling).

Workshop footprint cells:
Passability="nonblocking" (workers and haulers walk on workshop cells).

NavMask treats workshop cells as walkable (no blocking).

Items on workshop cells do NOT block movement (standard item passability rules).

8) Jobs, Hauling & Reservations

All placeable jobs follow the common pattern:

Read phase: scan chunks in parallel, compute offers, create diffs;

Mailbox for cross-chunk item reservations (sender chunk id + local seq ensure determinism);

Write phase: per-chunk merge and apply diffs; no data races.

Workshop material reservation:
When WorkshopJobSystem plans a CraftJob, it reserves required materials (item.IsReserved=true, item.ReservedBy=workshop_guid or job_guid).

Reserved items cannot be hauled away by other jobs.

If crafting is interrupted, materials remain reserved until:
Crafter resumes, or
Timeout (e.g., 200 ticks) → unreserve → materials become available for hauling again.

Output items on workshop footprint are NOT reserved (haulers freely take them).

9) UI Contract (v1)

Build menu split into categories:

[I]nstall (from crafted items):
Bed, Chair, Table, Door, Cabinet, Chest, Torch, Brazier, Statue, Armor Stand, Weapon Rack, etc.

Filter options: Nearest / Material / Min Quality

[C]onstruct (on site):
Wall, Floor, Ramp, Stairs, Bridge, Gate, Fortification, Channel

[W]orkshop (on site, 5×5):
Carpentry, Stoneworker, Smelter, Kiln, Forge, Still, Kitchen, Loom, Tanner, Butcher, Clothier, Jeweler, etc.

[T]rap (hybrid):
Spike Trap, Cage Trap, Weapon Trap

[D]efense (backend: Vehicle is_static; frontend: UI category):
Ballista, Trebuchet, Catapult

Workshop UI (select workshop, press [q]):

┌─────────────────────────────────┐
│ Carpentry Workshop              │
│                                 │
│ Queued Recipes:                 │
│   1. Wooden Chair (repeat)      │
│   2. Wooden Table x3            │
│                                 │
│ [a] Add Recipe                  │
│ [r] Remove Recipe               │
│ [p] Pause/Resume                │
│ [x] Deconstruct                 │
└─────────────────────────────────┘


Construction site UI (hover or select):

┌─────────────────────────────────┐
│ Stone Wall (25%)                │
│ Materials: 2/4 Stone Blocks     │
│ Builder: Urist McMason          │
└─────────────────────────────────┘


10) Persistence

Save the PlaceableInstance list (by chunk):

For installables: keep source_item_guid (so uninstall restores the exact item).

For constructions: store construction_id, condition stage.

For construction_sites: store target_construction_id, materials_delivered count, build_progress_ticks.

For workshops: store queued_recipes, current_job_guid (job system handles job persistence separately).

Indices (OccupancyMap, TagIndex) are derived on load.

Content versioning handled by registries; aliases resolve renamed ids.

11) Validation & Errors

Placement checks:

Floor/support requirements, clearance, no occupancy conflicts, valid rotation mask.

Install errors:

No eligible item found (emit UI toast), reservation stolen (retry next tick), cross-chunk delivery timeout (planner backoff).

Construct errors:

Missing materials; site blocked; invalid terrain (e.g., over chasm without support).

Workshop crafting errors:

Missing materials (wait for haul).

Interrupted crafting (reset progress; materials remain reserved for timeout period).

No free footprint cell for output (crafter waits or tries adjacent cells).

12) Performance & Concurrency

Per-chunk indices → O(1) occupancy, O(k) by tag.

All heavy scans happen in read phase. The current coarse scheduler runs systems
in deterministic registered-system order; future chunk-partitioned read
parallelism must preserve stable scan/diff ordering.

Write phase is bounded by number of diffs; stable sort keys ensure determinism.

No global locks; cross-chunk only via mailbox batches.

Workshop footprint item scan: O(25) per workshop (5×5 cells) → acceptable even for many workshops.

13) v1 Scope (MVP)

Installables: bed, chair, table, door, cabinet, chest, torch, brazier, statue, armor_stand, weapon_rack.

Constructions: wall, floor, ramp, stairs, bridge, gate, fortification, channel.

Workshops: carpentry, stoneworker, forge, smelter (5×5, no quality, deconstruct returns 100% materials).

Hybrids: well (bucket+rope), spike trap (mechanism+weapon).

Defense structures: ballista (backend: Vehicle is_static; UI: "Defense Structures" category).

UI: Install/Construct/Workshop/Trap/Defense tabs, uninstall action, simple filters, workshop recipe queue UI.

Jobs: Install/Uninstall/Construct/Deconstruct/Craft pipelines; reservations + hauling.

Indices: OccupancyMap + TagIndex per chunk; Zones consume TagIndex.

Construction sites: temporary placeable entities tracking materials_delivered and build_progress_ticks.

14) Examples
14.1 Installable furniture (bed item)
{
  "id": "core_item_furniture_bed",
  "name": "Bed",
  "kind": "furniture",
  "installable": true,
  "allowed_material_tags": ["wood","stone","metal"],
  "base_volume_ml": 80000,
  "base_mass_g": 15000,
  "stack": { "mode": "none" },
  "placeable_profile": {
    "footprint": { "w": 2, "d": 1, "h": 1 },
    "anchor": "topleft",
    "orientation_mask": ["N","E","S","W"],
    "passability": "nonblocking",
    "requires_floor": true,
    "clearance_h": 1,
    "tags": ["furniture","sleep","bed"],
    "effects": { "beauty": 2, "comfort": 3, "light_lumen": 0, "heat_w": 0 }
  }
}

14.2 Construction (stone wall)
{
  "id": "core_construction_wall_stone",
  "name": "Stone Wall",
  "category": "construction",
  "footprint": { "w": 1, "d": 1, "h": 1 },
  "orientation_mask": ["N","E","S","W"],
  "passability": "blocking",
  "requires_floor": false,
  "clearance_h": 1,
  "blocks_light": true,
  "materials_required": [
    { "tag": "stone_block", "count": 4 }
  ],
  "build_time_ticks": 600,
  "required_skill": "construction",
  "skill_level_min": 0,
  "effects": { "beauty": 1 }
}

14.3 Workshop (carpentry, 5×5)
{
  "id": "core_construction_workshop_carpentry",
  "name": "Carpentry Workshop",
  "category": "workshop",
  "footprint": { "w": 5, "d": 5, "h": 1 },
  "orientation_mask": ["N","E","S","W"],
  "passability": "nonblocking",
  "requires_floor": true,
  "clearance_h": 2,
  "blocks_light": false,
  "materials_required": [
    { "tag": "wood_log", "count": 10 },
    { "tag": "stone_block", "count": 5 }
  ],
  "build_time_ticks": 2400,
  "required_skill": "construction",
  "skill_level_min": 2,
  "effects": { "beauty": 0 },
  "workshop_profile": {
    "recipes": ["core_recipe_wooden_chair", "core_recipe_wooden_table", "core_recipe_wooden_door"],
    "work_skill": "carpentry",
    "max_queued_recipes": 10
  }
}

14.4 Hybrid (spike trap)
{
  "id": "core_construction_trap_spike",
  "name": "Spike Trap",
  "category": "trap",
  "footprint": { "w": 1, "d": 1, "h": 1 },
  "passability": "nonblocking",
  "requires_floor": true,
  "materials_required": [
    { "tag": "stone_block", "count": 2 }
  ],
  "hybrid_requirements": [
    { "item_tag": "mechanism", "count": 1 },
    { "item_tag": "weapon_trap", "count": 1 }
  ],
  "build_time_ticks": 800,
  "required_skill": "mechanic",
  "skill_level_min": 1,
  "trap_profile": {
    "trigger": "pressure",
    "reset_time_ticks": 100,
    "damage_source": "installed_weapon"
  }
}

14.5 Defense structure (ballista - backend Vehicle, frontend UI)
{
  "id": "core_vehicle_ballista_static",
  "name": "Ballista",
  "size": { "w": 3, "d": 2, "h": 2 },
  "anchor": "topleft",
  "mobility_class": "static",
  "crew_slots": [
    { "id": "gunner", "role": "gunner", "required": true, "local": {"x":1,"y":1,"z":0} },
    { "id": "loader", "role": "loader", "required": false, "local": {"x":2,"y":1,"z":0} }
  ],
  "mount_points": [
    { "id": "weapon", "kind": "ballista_bolt", "local": {"x":1,"y":0,"z":1}, "arc": {"yaw_deg":90,"pitch_deg":30} }
  ],
  "cargo": { "volume_ml": 50000, "mass_kg": 100, "slots": 20 },
  "capabilities": ["VehicleStatic", "MountedWeapon"],
  "ui": {
    "category": "Defense Structures",
    "glyph": "╬",
    "fg": {"r":80,"g":80,"b":80}
  }
}

Note: Ballista is constructed via the same construction site workflow, but the final entity is a VehicleInstance (is_static=true) instead of PlaceableInstance. UI presents it under "Defense Structures" category.

15) Future Hooks (non-blocking)

Workshop work_stations (explicit standing points) if needed for crowded rooms or multi-crafter workshops.

Door behaviors (locking/forbid/permission filters) via use_policy.

Fine-grained room scoring aggregation (beauty/comfort/heat from placeables).

Workshop quality bonuses (tool cabinets, nearby light, room cleanliness) affecting output quality.

Advanced trap logic (linked triggers, cascading traps, reusable ammo).

Multi-tile furniture (large tables, long benches) using the same footprint system.

16) Appendix: Decision Log

Workshop size: 5×5 (confirmed).

Workshop quality: None (constructions are standard; only installable furniture has quality from crafting).

Workshop materials: Live on footprint cells (no virtual bins); haulers place anywhere on footprint; crafters reserve and consume; outputs spawn on any free footprint cell.

Interrupted crafting: Progress resets to 0 (DF-style); no WIP item entity; materials remain reserved for timeout period.

Construction site: Temporary PlaceableInstance (kind="construction_site") storing materials_delivered count and build_progress_ticks; occupies target cells; blocks other constructions.

Construction deconstruct: Returns 100% of materials_required as items.

Mounted weapons: Backend uses Vehicle (is_static=true); frontend UI category "Defense Structures".

Workshop footprint passability: nonblocking (workers and haulers walk on workshop cells).


PLACEABLES SPEC — v1.2 (English, updated)

Status: Ready for implementation
Last Updated: 2025-10-03 (Australia/Sydney)
Fits: Fortress-style deterministic read/write pipeline, per-chunk indices & mailbox; chunk-partitioned read parallelism remains a future scheduler target.
Upgrades: Replaces v1.1; removes obsolete fields, adds state machines, destruction rules, trap layering, and cache invalidation contracts

0) Purpose & Scope

This spec defines placeable world entities (furniture, workshops, utilities, traps, fixed defenses) and how they are authored, spawned, indexed, updated, and persisted. It aligns with the existing install/construct workflows, single-writer-per-chunk policy, and data-driven content model. 

PLACEABLE_SPEC

1) Design Principles

DF-style installables: Craft items first, then install them; uninstall restores the same item GUID along with quality, material, decorations, and maker mark. 

PLACEABLE_SPEC

On-site constructions: Walls/floors/bridges/workshops are built from materials in place (no quality); deconstruct returns 100% of materials. 

PLACEABLE_SPEC

Determinism: Read→Barrier→Write; single writer per chunk; cross-chunk via mailbox; indices are per-chunk. 

PLACEABLE_SPEC

Data-driven: JSON, tags, no hardcoding; Zones read TagIndex, Jobs/Hauling use standard reservations. 

PLACEABLE_SPEC

Simplicity: Workshop materials and outputs live on the footprint cells; interrupted work resets to 0, no WIP entities. 

PLACEABLE_SPEC

2) Layer Integration (with TILE_SPEC v2)

Authoritative layers are defined by the tile system (L0..L7). Placeables live where they affect topology and passability. 

TILE_SPEC

L0 TerrainBits — Terrain geometry/legality (walls, floor, open, ramps, stairs). Not authored by placeables. Changing L0 bumps connectivity. 

TILE_SPEC

L2 Constructions — All placeables with blockers/passables/state (doors, furniture, workshops, bridges, wells, device traps). Changing L2 bumps connectivity & nav caches. 

TILE_SPEC

L3 Fluids — Fluids & thresholds (indirectly affect nav). 

TILE_SPEC

L4 Fields — Hazards/LOS/decals (field traps like caltrops scatter, oil slick, smoke). L4 does not change topology; use local cost/visibility penalties. 

TILE_SPEC

L5 Items — Stacks dropped on cells (separate from L2). 

TILE_SPEC

L7 Meta — Revealed/traffic/polish/engraved visual flags (not topology). 

TILE_SPEC

Cache rebuild rules (binding):

Rebuild NavMask / NavCost / UpRampMask on L0/L2 topology edits; L3 threshold changes; L7 traffic changes affect cost only. Bump ConnectivityVersion on topology changes. 

TILE_SPEC

3) Categories

Furniture — beds, chairs, tables, cabinets, chests, doors, braziers/torches, bookshelves, statues; Installable items. 

PLACEABLE_SPEC

Workshops — carpentry, stoneworks, smelter, kiln, still, kitchen, forge, clothier, tanner, butcher; Constructions (5×5). 

PLACEABLE_SPEC

Utilities — bridges/gates, wells/cisterns, grates/hatches; Construction or Hybrid. 

PLACEABLE_SPEC

Traps — Device traps (L2) as Hybrids; Field traps (L4) as fields/decals. 

PLACEABLE_SPEC

Fixed defenses — ballista/trebuchet/catapult; front-end category, back-end uses Vehicle(is_static=true). 

PLACEABLE_SPEC

4) Data Model
4.1 Installable items (extends ITEMS_SPEC v4-int)

Installable items provide a placeable_profile on the item. Material/quality/decorations/maker mark come from the item and are preserved on uninstall (same item GUID). Do not define material filters on the placeable; recipes decide materials. 

PLACEABLE_SPEC

placeable_profile (on the item):

{
  "placeable_profile": {
    "footprint": { "w": 2, "d": 1, "h": 1 },
    "anchor": "topleft",
    "orientation_mask": ["N","E","S","W"],
    "passability": "blocking|doorway|nonblocking",
    "requires_floor": true,
    "clearance_h": 1,
    "blocks_light": false,
    "tags": ["furniture","sleep","bed"],
    "effects": { "beauty": 2, "comfort": 3, "light_lumen": 0, "heat_w": 0 }
  }
}


Notes: the item remains the single source of material, quality (−3..+3), decorations, maker mark, etc. 

PLACEABLE_SPEC

Breaking change from v1.1: allowed_material_tags under installable items is removed. It existed in v1.1 examples but is now deprecated; material selection belongs to the recipe layer. 

PLACEABLE_SPEC

4.2 Constructions registry

Standalone constructions (no source item): walls, floors, workshops, utilities, bridges. Example fields: footprint, passability, floor/clearance, materials_required, build_time, required_skill, effects, and optional workshop/trap profiles. 

PLACEABLE_SPEC

{
  "id": "core_construction_wall_stone",
  "name": "Stone Wall",
  "category": "construction",
  "footprint": { "w": 1, "d": 1, "h": 1 },
  "orientation_mask": ["N","E","S","W"],
  "passability": "blocking",
  "requires_floor": false,
  "clearance_h": 1,
  "blocks_light": true,
  "materials_required": [{ "tag": "stone_block", "count": 4 }],
  "build_time_ticks": 600,
  "required_skill": "construction",
  "skill_level_min": 0,
  "effects": { "beauty": 1 }
}


PLACEABLE_SPEC

4.3 Hybrids (well, trap base, etc.)

Hybrid constructions require on-site materials plus specific items (installed into the hybrid). On deconstruct, return materials and respawn the installed items (original GUIDs). 

PLACEABLE_SPEC

4.4 PlaceableInstance (runtime)

Runtime instance occupying cells; key fields include kind, definition ids, origin item GUID, world anchor/rotation, ownership, policy, quality/condition, workshop/trap states, etc.

{
  "guid": "p_6b1f",
  "kind": "installable|construction|construction_site|hybrid",
  "def_id": "core_item_furniture_bed",       // installable
  "construction_id": null,                   // constructions/hybrids/sites
  "source_item_guid": "i_93de",              // installable only

  "anchor_world": { "x":120, "y":85, "z":10 },
  "rotation": 1,                             // 0=N,1=E,2=S,3=W
  "owner_faction_id": null,
  "owner_creature_guid": null,
  "use_policy": "public|faction|private",

  "quality_tier": 0,                         // mirrors item (installable)
  "condition_state": "Pristine",
  "enabled": true,

  "state_id": "closed|open|…",               // see §5 State Machine

  "workshop_state": null,                    // optional
  "trap_state": null,                        // optional
  "destruction_state": null                  // optional: current hp/destroyed flag
}


PLACEABLE_SPEC

5) State Machine (doors/windows/lamps/bridges)

Certain placeables define named states with passability/visibility and explicit transitions (with latency). On transition, L2 topology or cost may change → see §10.

"states": {
  "closed": { "passability": "blocking", "blocks_light": true },
  "open":   { "passability": "doorway",  "blocks_light": false }
},
"transitions": [
  { "from": "closed", "to": "open",  "verb": "open",  "latency_ticks": 10 },
  { "from": "open",   "to": "closed","verb": "close", "latency_ticks": 10 }
]


Designed to match door-like entities; authoring “doorway” passability for open state. 

PLACEABLE_SPEC

Transitions may specify SFX/VFX hooks; they always resolve deterministically within the chunk write/commit phases.

6) Traps
6.1 Device traps (L2)

Implemented as hybrid constructions with installed items (e.g., mechanism+weapon).

Live on L2 (they occupy the tile and can be de/constructed/destroyed).

Example (pressure spike trap): footprint, passability (walk-over), trigger, reset, and damage source defined by installed weapon. 

PLACEABLE_SPEC

6.2 Field traps (L4)

Pure field/decals such as caltrops scatter, oil spills, smoke.

Live on L4; do not change topology. Provide move penalties, visibility modifiers, and/or tick/step damage. (Author as fields system assets; referenced by placeables if needed.)

7) Destruction (Building-Destroyer compatibility)

Add a destruction block to placeables that can be damaged or smashed by enemies:

"destruction": {
  "is_destroyable": true,
  "destroyer_immune_tags": ["well","bridge","road","trap"],  // any tag grants immunity
  "durability_points": 1200,                                 // structure HP
  "on_destroy": { "drop_rules": "salvage_some", "fx": ["debris_small","dust_puff"] }
}


Enemies with a “building_destroyer” capability may target destroyable placeables unless they carry an immune tag (e.g., wells/bridges/roads/traps).

When destroyed: clear L2 occupancy, apply on_destroy drops to L5, and bump caches as per §10.

8) Effects

Minimal, data-driven fields (expandable later):

"effects": {
  "beauty": int,
  "comfort": int,
  "light_lumen": int,
  "heat_w": int
}


Note: Room scoring / cleanliness / facility auras are handled by Zones in this project and not part of placeable effects at this time.

9) Authoring Registries

Installables live in Items (content/registries/items/*.json with placeable_profile). They inherit material/quality/decor/inscriptions from the item and restore the same item GUID on uninstall. 

PLACEABLE_SPEC

Constructions live in content/registries/constructions.json. Workshops are also authored here (5×5, no quality). 

PLACEABLE_SPEC

10) Operations (Diffs) & Workflows
10.1 Operations (API surface)

Installable furniture: DesignateInstall → ReserveItem → HaulToInstallSite → InstallItem ; DesignateUninstall → UninstallToItem. 

PLACEABLE_SPEC

Constructions: DesignateConstruct → CreateConstructionSite → HaulMaterialToSite → BuildConstruction → CompleteConstruction; DesignateDeconstruct → Deconstruct (returns 100% materials). 

PLACEABLE_SPEC

Workshops: AddRecipeToQueue/Remove/Pause/Resume etc. 

PLACEABLE_SPEC

Traps: ArmTrap/DisarmTrap/TriggerTrap. 

PLACEABLE_SPEC

State: RequestTransition(placeable_guid, to_state) (validated against transitions).

Destruction: ApplyDamage(placeable_guid, amount, source); destroy if durability_points ≤ 0.

10.2 Install workflow (from item)

Player picks spot (optionally filter by material/quality/nearest).

Write: haul completes → InstallItem spawns the PlaceableInstance, writes passability into L2, updates TagIndex, removes the item from world but stores source_item_guid. Uninstall reverses the process. 

PLACEABLE_SPEC

10.3 Construct workflow (on-site)

Create a construction site (temporary PlaceableInstance); it occupies cells and tracks materials_delivered by scanning items on footprint cells. If interrupted, progress resets to 0 (DF-style). On completion, consume materials, delete site, spawn final PlaceableInstance, update OccupancyMap/TagIndex/NavMask. 

PLACEABLE_SPEC

10.4 Workshop workflow

Materials live on the 5×5 footprint; haulers place anywhere on footprint; crafters reserve and consume; outputs spawn on any free footprint cell (or adjacent if full). If interrupted, progress resets; reservations time out. 

PLACEABLE_SPEC

10.5 Hybrid workflow (well, trap base)

Site + required items; builder installs items (removed from world, GUIDs stored); completion spawns hybrid; deconstruct returns materials and respawns installed items. 

PLACEABLE_SPEC

10.6 State & Cache Invalidation

On state transition that changes passability/blocks_light (e.g., door open/close), treat as L2 topology/cost update and rebuild caches per-chunk; bump ConnectivityVersion. 

TILE_SPEC

11) Validation & Constraints

No allowed_material_tags under installables (moved to Recipes). If present in legacy data, loader should warn and ignore. 

PLACEABLE_SPEC

Footprint must fit within chunk/world bounds; anchor respects OpenWithFloor support rules (no placement on OpenNoFloor). 

TILE_SPEC

Rotation uses orientation_mask. Autotiling resolves NESW based on L2 blocker first, then L0. 

TILE_SPEC

States must be closed sets; all transitions validated.

Destruction: if is_destroyable=false or tags intersect destroyer_immune_tags, AI “building destroyer” must skip.

Indices: Every write that changes L2 occupancy or passability must update OccupancyMap and TagIndex. 

PLACEABLE_SPEC

12) Examples (updated)
12.1 Installable Door (L2, state machine)
{
  "id": "core_item_door_wood",
  "tags": ["furniture","door"],
  "placeable_profile": {
    "footprint": { "w": 1, "d": 1, "h": 1 },
    "anchor": "topleft",
    "orientation_mask": ["N","E","S","W"],
    "passability": "blocking",
    "requires_floor": true,
    "clearance_h": 2,
    "blocks_light": true,
    "tags": ["door"]
  },
  "states": {
    "closed": { "passability": "blocking", "blocks_light": true },
    "open":   { "passability": "doorway",  "blocks_light": false }
  },
  "transitions": [
    { "from":"closed","to":"open","verb":"open","latency_ticks":10 },
    { "from":"open","to":"closed","verb":"close","latency_ticks":10 }
  ],
  "destruction": { "is_destroyable": true, "destroyer_immune_tags": [], "durability_points": 250 }
}

12.2 Device Trap (L2 Hybrid)
{
  "id": "core_construction_trap_spike",
  "category": "trap",
  "footprint": { "w": 1, "d": 1, "h": 1 },
  "passability": "nonblocking",
  "requires_floor": true,
  "materials_required": [{ "tag":"mechanism","count":1 }],
  "hybrid_requirements": [{ "item_tag":"weapon_trap","count":1 }],
  "build_time_ticks": 800,
  "trap_profile": { "trigger":"pressure", "reset_time_ticks":100, "damage_source":"installed_weapon" },
  "destruction": { "is_destroyable": true, "durability_points": 120 }
}


PLACEABLE_SPEC

12.3 Field Trap (L4, hazards/LOS)
{
  "id": "field_caltrops_scatter",
  "layer": "L4",
  "field_kind": "caltrops",
  "intensity": 3,
  "los_block": false,
  "move_penalty": 100,
  "tick_damage": { "mode":"stab", "per_step": 2 }
}

12.4 Workshop (5×5, on-site construction)
{
  "id": "core_construction_workshop_carpentry",
  "name": "Carpentry Workshop",
  "category": "workshop",
  "footprint": { "w": 5, "d": 5, "h": 1 },
  "orientation_mask": ["N","E","S","W"],
  "passability": "nonblocking",
  "requires_floor": true,
  "clearance_h": 2,
  "blocks_light": false,
  "materials_required": [
    { "tag": "wood_log", "count": 10 },
    { "tag": "stone_block", "count": 5 }
  ],
  "build_time_ticks": 2400,
  "required_skill": "construction",
  "skill_level_min": 2,
  "effects": { "beauty": 0 },
  "workshop_profile": {
    "recipes": ["core_recipe_wooden_chair","core_recipe_wooden_table","core_recipe_wooden_door"],
    "work_skill": "carpentry",
    "max_queued_recipes": 10
  }
}


PLACEABLE_SPEC

13) Changelog (v1.2 vs v1.1)

Removed: allowed_material_tags from installable item examples; materials are recipe-level (legacy data will be ignored with a warning). 

PLACEABLE_SPEC

Added: State machine support (states/transitions) for doors/windows/lamps/bridges; transitions have latency and update L2.

Added: Destruction block (is_destroyable, destroyer_immune_tags, durability_points, on_destroy) for Building-Destroyer style AI.

Clarified: Trap layering — device traps are L2 hybrids; field traps are L4 hazards (no topology changes).

Bounded: Cache invalidation & ConnectivityVersion bump rules tied to L0/L2 edits and L3 thresholds. 

TILE_SPEC

Affirmed: Install/uninstall preserves same item GUID and all per-item properties (material, quality, decorations). 

PLACEABLE_SPEC

14) Compliance Checklist

 All installable items define placeable_profile on the item; no material filters on placeables. 

PLACEABLE_SPEC

 All L2 entities specify passability and (if needed) states/transitions.

 Device traps are authored as L2 hybrids; field traps as L4 fields.

 Constructions list materials_required, build_time_ticks, and effects; workshops use 5×5 footprints. 

PLACEABLE_SPEC

 Every L0/L2 topology change bumps ConnectivityVersion; rebuild NavMask/NavCost/UpRampMask during commit. 

TILE_SPEC

 Uninstall restores the same item GUID with material/quality/decor. 

PLACEABLE_SPEC

This v1.2 is drop-in over v1.1: existing content remains valid except the deprecated allowed_material_tags on installables. New features (state/destruction) are additive.

---

## 15) Implementation Edge Cases & Resolutions (v1.3 Addendum)

Status: Normative
Last Updated: 2025-10-03

This section documents critical edge cases discovered during implementation planning and their binding resolutions.

### 15.1 Cross-Chunk Placeables

**Problem**: A 5×5 workshop may span multiple chunk boundaries (chunks are 32×32).

**Resolution**:
- **Allowed with anchor ownership**: Placeables may span chunks; the anchor chunk owns the PlaceableInstance.
- **OccupancyMap**: Anchor chunk stores the full PlaceableInstance; other chunks store occupancy references pointing to anchor.
- **TagIndex**: Only anchor chunk indexes the placeable by tags.
- **Write phase**: Only anchor chunk's writer modifies the PlaceableInstance; spanning chunks update occupancy in their own write phases.

**Implementation note**: Cross-chunk queries (e.g., "find all workshops in area") must check all involved chunks' occupancy maps.

### 15.2 Construction Site Material Reservation

**Problem**: Materials delivered to construction site cells might be hauled away before construction completes.

**Resolution**:
- **Immediate reservation**: When HaulingSystem delivers materials to a construction site, those items are immediately reserved (`item.IsReserved = true`, `item.ReservedBy = construction_site_guid`).
- **Reservation TTL**: Use ReservationManager with appropriate TTL (e.g., `currentTick + 10000`).
- **materials_delivered tracking**: Construction site scans footprint cells and counts reserved items matching required tags.
- **Builder workflow**: Builder consumes reserved items; if materials go missing (reservation expired or item destroyed), construction fails and site remains as incomplete.

**Fail-safe**: If construction is designated for deconstruct before completion, release all material reservations.

### 15.3 Workshop Footprint Item Management

**Problem**: Workshop footprints (5×5 = 25 cells) hold both input materials and output items; how to distinguish reserved vs ready-to-haul?

**Resolution**:
- **Item stacking**: Items can stack infinitely on a single cell (current design), so 25 cells cannot "fill up."
- **Reservation semantics**:
  - Materials reserved for active recipe: `item.IsReserved = true`, `item.ReservedBy = workshop_guid or job_guid`.
  - Finished outputs: `item.IsReserved = false`.
- **HaulingSystem**: Only hauls unreserved items from workshop footprint.
- **Overflow handling**: Outputs spawn on any free footprint cell; if all cells have items (stacked), spawn on same cell (stacking).

**Edge case - wrong material**: Recipe validation happens at craft-time; if reserved item doesn't match recipe requirements (e.g., tag matches but specific properties don't), crafting fails and materials are unreserved after timeout.

### 15.4 Door Lock State (Simplified State Machine)

**Problem**: Complex state machines (closed/opening/open/closing) with latency and conflict resolution add unnecessary complexity.

**Resolution - Simplified Model**:
- **Two effective states**:
  - `locked = false` → passability = `"doorway"` (creatures can path through; movement system handles "opening" implicitly)
  - `locked = true` → passability = `"blocking"` (impassable)
- **No transition latency**: Lock/unlock operations are instant (applied in write phase).
- **Operations**:
  - `LockDoor(placeable_guid)` → sets `door_state.locked = true`, updates L2 passability, rebuilds NavMask.
  - `UnlockDoor(placeable_guid)` → sets `door_state.locked = false`, updates L2 passability, rebuilds NavMask.
- **Conflict resolution**: Multiple lock/unlock requests in same tick are idempotent (last writer wins by stable diff sort).

**PlaceableInstance door state**:
```json
{
  "door_state": {
    "locked": false,
    "owner_creature_guid": null,  // optional: for "forbid" mechanics
    "access_policy": "public"      // "public" | "faction" | "private"
  }
}
```

**Deprecated**: The complex state machine with `states`, `transitions`, and `latency_ticks` from §5 is replaced by this simplified lock/unlock model for doors. Bridges/traps may still use multi-state machines if needed, but doors use boolean lock only.

### 15.5 Hybrid Construction Atomicity

**Problem**: Hybrid constructions (e.g., traps with mechanism + components) require both materials and specific items; can builder start work if only partial requirements are met?

**Resolution - All-or-Nothing**:
- **Ready condition**: Construction site is `ready_to_build` only when:
  - All `materials_required` are delivered and reserved.
  - All `hybrid_requirements` items are delivered and reserved.
- **Builder assignment**: ConstructionJobSystem only assigns builders to sites where `ready_to_build == true`.
- **Haul coordination**: HaulingJobSystem generates haul jobs for all missing materials/items until site is ready.
- **Atomicity**: Builder consumes all materials and items in a single operation on completion; no partial consumption.

**Edge case - item stolen during build**: If a reserved hybrid item is destroyed/stolen after build starts, build fails, progress resets to 0, and site returns to "waiting for materials" state.

**Example - Spike Trap**:
```json
{
  "materials_required": [{ "tag": "mechanism", "count": 1 }],
  "hybrid_requirements": [{ "item_tag": "spike_component", "count": 1 }],
  "ready_to_build": false  // Must have 1 mechanism + 1 spike_component
}
```

Note: Trap design simplified to use generic components instead of weapons (§15.9).

### 15.6 Uninstall Item GUID Handling

**Problem**: Uninstalling furniture should restore "the same item" with quality/material/decorations, but original GUID might conflict or content definitions might have changed.

**Resolution - Generate New GUID, Preserve Properties**:
- **New GUID**: Uninstall always generates a fresh `Guid.NewGuid()` to avoid collisions.
- **Preserved properties**: Copy from PlaceableInstance:
  - `material` (string ID, e.g., `"core_mat_metal_iron"`)
  - `quality_tier` (int −3..+3)
  - `decorations` (array, if saved)
  - `maker_mark` (string, if saved)
  - `inscriptions` (string, if saved)
- **Content migration**: Use ItemRegistry.ResolveItem with alias support; if item def is missing/renamed, log warning and use fallback item def.
- **Player perception**: Players see identical item properties, don't notice GUID change.

**PlaceableInstance required save fields**:
```json
{
  "source_item_guid": "...",           // reference only (not restored)
  "source_item_def_id": "core_item_furniture_bed",
  "source_item_material": "core_mat_wood_oak",
  "source_item_quality": 2,
  "source_item_decorations": [...],
  "source_item_maker": "Urist McMason"
}
```

### 15.7 NavMask Rebuild Batching

**Problem**: Multiple L2 topology changes in a single tick (e.g., 100 doors lock/unlock, workshops constructed/deconstructed) could trigger redundant NavMask rebuilds.

**Resolution - Per-Chunk-Commit Batching**:
- **Write phase**: ChunkWriter applies all diffs in stable sorted order; tracks `topologyChanged` flag.
- **Commit phase**: After all diffs applied, if `topologyChanged == true`:
  1. Rebuild `NavMask[1024]` for entire chunk.
  2. Rebuild `NavCost[1024]` for entire chunk.
  3. Rebuild `UpRampMask[1024]` for ramp cells.
  4. Bump `ConnectivityVersion` once.
- **Topology-affecting diffs**:
  - `InstallPlaceable` / `UninstallPlaceable`
  - `CompleteConstruction` / `DeconstructPlaceable`
  - `LockDoor` / `UnlockDoor` (changes passability)
  - `DestroyPlaceable`
- **Performance**: 100 diffs in one chunk → 1 rebuild, not 100.

**Pseudocode**:
```csharp
public void ApplyDiffs(List<DiffOp> diffs, ulong currentTick)
{
    bool topologyChanged = false;
    foreach (var diff in diffs.OrderBy(StableSort))
    {
        ApplyDiff(diff);
        if (AffectsTopology(diff)) topologyChanged = true;
    }
    if (topologyChanged)
    {
        RebuildNavMask();
        RebuildNavCost();
        RebuildUpRampMask();
        ConnectivityVersion++;
    }
}
```

### 15.8 Construction Site Passability

**Problem**: Should a construction site (temporary placeable) inherit the target construction's passability, or use a fixed passability?

**Resolution - Fixed Nonblocking**:
- **All construction sites**: `passability = "nonblocking"` regardless of target construction type.
- **Rationale**:
  - Allows builders and haulers to walk onto site cells.
  - Prevents creatures from being "trapped" when site spawns.
  - Builder can work from any footprint cell or adjacent.
- **Occupancy**: Construction site still marks cells as occupied in OccupancyMap (prevents overlapping constructions).
- **Visual**: UI shows construction site sprite/glyph; creatures can walk over it.

**Edge case - wall construction site**: Even though final wall is `blocking`, the construction site itself is `nonblocking` during build.

### 15.9 Trap Simplification

**Problem**: Original spec required hybrid traps with weapons (mechanism + weapon_trap item); complex to manage weapon damage/durability.

**Resolution - Component-Based Traps**:
- **Spike trap example**:
```json
{
  "id": "core_construction_trap_spike",
  "materials_required": [{ "tag": "mechanism", "count": 1 }],
  "hybrid_requirements": [{ "item_tag": "spike_component", "count": 1 }],
  "trap_profile": {
    "trigger": "pressure",
    "reset_time_ticks": 100,
    "damage": { "type": "stab", "amount": 15 }  // fixed damage, not from weapon
  }
}
```
- **No weapon items**: Use generic components with fixed damage values.
- **Simpler**: No weapon durability, quality, or material variance in v1 traps.

**Future**: Can add weapon-based traps later if needed.

### 15.10 Material Overflow on Deconstruct

**Problem**: Deconstructing a 5×5 workshop returns many material items; where do they spawn if footprint cells are occupied?

**Resolution - Random Placement with Stacking**:
- **Spawn strategy**: For each material item to spawn:
  1. Pick random cell from footprint (uniform distribution).
  2. Spawn item on that cell (items stack infinitely, so no "full" cells).
- **No overflow queue**: With infinite stacking, overflow is not possible.
- **Determinism**: Use seeded RNG (e.g., `hash(placeable_guid + material_index)`) for deterministic placement.

**Example**:
```csharp
foreach (var (materialTag, count) in materials_to_return)
{
    for (int i = 0; i < count; i++)
    {
        var cellIndex = SeededRandom(placeable.Guid, materialTag, i) % footprint.Count;
        var worldPos = footprint[cellIndex];
        SpawnItem(materialTag, worldPos);
    }
}
```

### 15.11 Content Versioning for Placeables

**Problem**: Loading old save games where item/construction definitions have been renamed, deleted, or changed.

**Resolution - Alias System + Fallback**:
- **Item resolution**:
```csharp
public ItemDefinition? ResolveItem(string id, ContentVersion? saveVersion = null)
{
    // Direct lookup
    if (_itemsById.TryGetValue(id, out var item)) return item;

    // Alias lookup
    if (_aliases.TryGetValue(id, out var newId))
    {
        Log($"[ItemRegistry] Migrated '{id}' → '{newId}'");
        return _itemsById.GetValueOrDefault(newId);
    }

    // Fallback
    Log($"[ItemRegistry] Missing item '{id}', using fallback");
    return GetFallbackItem("missing_furniture");
}
```
- **Construction resolution**: Same pattern with ConstructionRegistry.
- **Graceful degradation**: Missing definitions replaced with fallback placeables (visible but functional).
- **Save compatibility**: String IDs allow migration; numeric handles never saved.

**Fallback items**:
- `missing_furniture` (generic furniture sprite, no effects)
- `missing_construction` (generic wall sprite, blocking)
- `missing_workshop` (5×5 nonblocking, no recipes)

### 15.12 Destruction and Cache Invalidation

**Problem**: When a placeable is destroyed (by Building Destroyer AI), how to handle cache invalidation and item drops?

**Resolution**:
- **Write phase**: `DestroyPlaceable` diff applied:
  1. Remove from OccupancyMap.
  2. Remove from TagIndex.
  3. Clear L2 passability.
  4. Mark `topologyChanged = true`.
  5. Spawn salvage items (per `on_destroy.drop_rules`).
- **Commit phase**: Rebuild NavMask/NavCost, bump ConnectivityVersion.
- **Immunity check**: Before emitting destroy diff, validate placeable doesn't have immune tags.

**on_destroy drop rules**:
- `"none"`: No salvage.
- `"salvage_some"`: Return 30% of materials as items.
- `"salvage_all"`: Return 100% (same as deconstruct).

### 15.13 Faction Ownership and Access Control

**Problem**: Placeables (doors, furniture, workshops) need ownership tracking and access control for multi-faction gameplay.

**Resolution - Simplified Faction-Only Ownership**:

**PlaceableInstance ownership fields**:
```json
{
  "owner_faction_id": "faction_player",     // null = neutral/unclaimed
  "use_policy": "public"                    // "public" | "faction" | "forbidden"
}
```

**Ownership semantics**:
- **owner_faction_id**: Which faction owns/built this placeable.
  - Set when construction completes (builder's faction).
  - Set when item is installed (installer's faction).
  - `null` for neutral/wild placeables (ruins, natural features).
  - All placeables are faction-level only; no personal ownership.

- **use_policy**: Access control rules.
  - `"public"`: Anyone can use (default for most furniture/workshops).
  - `"faction"`: Only owner faction members can use.
  - `"forbidden"`: No one can use (for doors: blocks all pathing; for workshops: no recipe queue).

**Access validation**:
```csharp
public bool CanUse(Guid creatureGuid, PlaceableInstance placeable)
{
    // Forbidden: no one can use
    if (placeable.use_policy == "forbidden") return false;

    // Public: always allow
    if (placeable.use_policy == "public") return true;

    // Faction policy: check creature's faction
    if (placeable.use_policy == "faction")
    {
        var creature = GetCreature(creatureGuid);
        return creature.FactionId == placeable.owner_faction_id;
    }

    return false;
}
```

**Door lock and access**:
```json
{
  "owner_faction_id": "faction_player",
  "use_policy": "faction",
  "door_state": {
    "locked": false
  }
}
```

- **Unlocked + public**: Passability = `"doorway"`, anyone can path through.
- **Unlocked + faction**: Passability = `"doorway"` for owner faction; enemy creatures see as `"blocking"`.
- **Unlocked + forbidden**: Passability = `"blocking"` for everyone (acts as permanent obstruction).
- **Locked**: Always `"blocking"` for everyone (including owner faction).

**Pathfinding integration**:
```csharp
// When computing NavMask for a creature
public byte GetDoorPassability(PlaceableInstance door, Guid creatureGuid)
{
    if (door.door_state.locked) return Passability.Blocking;
    if (!CanUse(creatureGuid, door)) return Passability.Blocking;
    return Passability.Doorway;
}
```

**Workshop ownership**:
```json
{
  "owner_faction_id": "faction_player",
  "use_policy": "faction",      // Only player faction crafters can queue recipes
  "workshop_state": {
    "queued_recipes": [...],
    "current_job_guid": null
  }
}
```

- Crafter assignment checks: `CanUse(crafter.Guid, workshop)` before creating CraftJob.
- Enemy factions cannot use captured workshops unless ownership is transferred (conquest mechanic, future).

**Furniture access (beds, chairs, tables)**:
- **Simplified**: No personal ownership; rooms/beds assigned via separate zone system (not placeable-level).
- `use_policy = "faction"` → only faction members can sleep in beds, sit in chairs.
- Room assignment system (future) can track "preferred bed per creature" but ownership stays at faction level.

**Claiming and transfer**:
- **Initial ownership**: Set to builder/installer's faction on completion.
- **Claiming neutral placeables**: Designate "Claim" action → creature walks to placeable → sets owner_faction_id to creature's faction.
- **Conquest transfer**: When faction is defeated, all their placeables become neutral (owner_faction_id = null) or transfer to victor (future).

**Uninstall/deconstruct ownership**:
- Only owner faction can designate uninstall/deconstruct.
- Neutral placeables (owner_faction_id = null) can be claimed by anyone before deconstruct.
- Spawned items inherit owner_faction_id (prevent stealing from friendly factions).
- Deconstructed materials spawn as owned by the faction that issued the order.

**Edge cases**:
1. **Faction eliminated**: All faction-owned placeables become neutral (owner_faction_id = null, use_policy = "public").

2. **Door forbid mechanic**: Player sets `use_policy = "forbidden"` to prevent all pathing through door (haulers, enemies, everyone).
   - Useful for traffic control without locking.
   - Can be toggled back to "public" or "faction" anytime.

3. **Multi-faction fortresses**: Each faction builds their own workshops/doors with faction ownership; access control prevents conflicts.

4. **Allied factions**: Use `use_policy = "public"` to allow allied faction members to use workshops/pass through doors.

**UI implications**:
- Placeable info panel shows: "Owner: Player Faction" or "Unclaimed".
- Door designation UI: `[L] Lock/Unlock`, `[F] Forbid`, `[A] Allow (Public)`, `[R] Restrict (Faction)`.
- Workshop designation UI: `[O] Set Owner Faction` (conquest/claiming).
- Bed/furniture: No personal ownership UI; room assignment handled separately.

**Save/load**:
- `owner_faction_id` saved as string ID (e.g., `"faction_player"`).
- Faction registry resolves IDs on load; missing factions default to neutral (owner_faction_id = null).

### 15.14 Future: Multi-Z Placeables

**Problem**: Current footprint `{ "w": 5, "d": 5, "h": 1 }` supports single-layer only; future buildings may span multiple Z levels.

**Resolution - Reserved for Later**:
- **v1 restriction**: All placeables have `h = 1`.
- **Future support**: When `h > 1`:
  - Occupancy spans Z layers `[anchor.z, anchor.z + h)`.
  - Each Z layer updates its own L2 and NavMask.
  - Vertical connectivity handled by stairs/ramps (separate from placeable).
- **Schema**: `h` field already present; loaders should validate `h == 1` for v1.

---

## 16) Summary of Binding Decisions

| Topic | Decision |
|-------|----------|
| Cross-chunk placeables | Allowed; anchor chunk owns PlaceableInstance |
| Construction material reservation | Immediate reservation on delivery |
| Workshop item stacking | Infinite stacking; no overflow issues |
| Door state machine | Simplified: locked boolean only, no latency |
| Hybrid construction atomicity | All-or-nothing: all materials + items required before build |
| Uninstall GUID handling | Generate new GUID, preserve properties |
| NavMask rebuild batching | Per-chunk-commit: batch all changes, rebuild once |
| Construction site passability | Always nonblocking |
| Trap design | Component-based with fixed damage (no weapons) |
| Deconstruct material spawn | Random placement with stacking |
| Content versioning | Alias system + fallback items |
| Destruction | Topology change + salvage drops |
| Faction ownership | Faction-only: owner_faction_id + use_policy (no personal ownership) |
| Door access control | Public/faction/forbidden policy + locked state |
| Workshop ownership | Faction-level only; crafter assignment checks CanUse |
| Furniture ownership | Faction-level; room/bed assignment via separate zone system |
| Ownership transfer | Claim neutral, conquest (future); faction eliminated → neutral |
| Multi-Z placeables | Reserved for future (v1: h=1 only) |

---

PLACEABLE_SPEC v1.3 - Ready for Implementation
