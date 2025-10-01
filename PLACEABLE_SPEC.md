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

All heavy scans happen in read phase, fully parallel.

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
