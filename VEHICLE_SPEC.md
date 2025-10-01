id: vehicle_spec.v1
status: draft
owner: sim/vehicles
last_updated: 2025-01-10

# Vehicle Spec (Multi‑Tile, Multi‑Height, Crew/Cargo/Weapons)

This spec defines a data model and runtime architecture for vehicles and siege engines that fit the current HumanFortress stack (chunks 32×32, L0..L7 layers, DiffLog, deterministic nav, Items/Jobs).

Goals
- Support visual multi‑tile vehicles first; evolve to true multi‑tile occupancy with clearance‑based navigation.
- A single unified “Vehicle” system represents carts, siege engines, siege towers, ships, and static emplacements.
- Deterministic behavior, data‑driven definitions, and minimal churn to existing subsystems.

Non‑Goals (v1)
- Full soft‑body physics, fluid dynamics on hulls, or deformable vehicles.
- Destroyable per‑sprite meshes; we model damage on “parts”.

## 1) Data Model (Content)

File: `content/registries/vehicles.json`

VehicleDefinition (JSON)
- `id : string` — unique id (e.g., core_vehicle_cart_wood)
- `name : string`
- `size : { w:int, d:int, h:int }` — footprint width/depth in tiles; height in z‑layers
- `anchor : string` — where (0,0,0) sits relative to footprint: "topleft" | "center" (default topleft)
- `size_class : string` — e.g., "small" (1x1x1), "large2x2", "tower2x2x3"; used by nav/cache keys
- `mobility_class : string` — "land" | "water" | "amphibious" | "static"
- `mass_kg : float`, `drag_coeff : float`, `turn_radius_tiles : int` (advised defaults)
- `speed : { min: float, max: float }` — nominal speed band before job multipliers
- `crew_slots : [ { id:string, role:string, required:bool, local:{x:int,y:int,z:int} } ]`
- `cargo : { volume_ml:int, mass_kg:int, slots:int }` — aggregate cargo capacity
- `hitch_points : [ { local:{x:int,y:int,z:int}, kinds:["horse","ox","dwarf"] } ]` — optional animal/human traction
- `mount_points : [ { id:string, kind:string, local:{x:int,y:int,z:int}, arc:{yaw_deg:int,pitch_deg:int}, tags:[string] } ]` — for weapons/sensors
- `component_layout : [ { part_id:string, type:string, local:{x:int,y:int,z:int}, tags:[string], hp:int } ]` — local grid of parts (Frame/Wheel/Track/Rudder/Mast/Seat/AmmoBay/Turret/Weapon/FloorPanel)
- `temporary_floor : [ { local:{x:int,y:int,z:int} } ]` — optional set of local cells treated as floor overlay (e.g., siege tower decks); runtime overlay, not L0 edits
- `capabilities : [string]` — e.g., [ "VehicleLand", "PlaceFloorOverlay", "Towable" ]
- `constraints : { min_clearance:{w:int,h:int}, max_slope:int, water_draft:int }` — nav limits per mobility class
- `ui : { glyph:int, fg:{r:int,g:int,b:int}, bg:{r:int,g:int,b:int} }` — default rendering hints (v1)

Examples (sketch)
- Horse Cart: w2×d2×h1, mobility land, crew driver=1, cargo slots, hitch for horses
- Ballista (static): w2×d2×h1, mobility static, crew gunner=1+loader=1, mount weapon=ballista
- Trebuchet (static): similar to ballista, different weapon mount
- Siege Tower: w2×d2×h3, land, crew 4, temporary_floor includes interior decks and roof, capability PlaceFloorOverlay
- Ship (galleon): wN×dM×hK, mobility water, mount cannons, draft threshold via constraints

Loading/Validation
- Loaded by `ContentRegistry` with JSON schema checks. Missing fields use conservative defaults.

## 2) Runtime Model

VehicleInstance
- `Guid` id
- `DefinitionId` : string
- `AnchorWorld` : { x:int, y:int, z:int }
- `Rotation` : byte (0..7) — 8‑dir; simplifying v1: 4‑dir
- `Crew` : map seat_id → creature_guid (nullable)
- `Cargo` : list of item guids; aggregate volume/mass tracked
- `Hitch` : list of creature guids (animals/crew)
- `Parts` : runtime HP/state per part_id
- `Flags` : isMoving, isStatic, isAnchored, isBeached

VehicleManager
- Holds definitions and instances; thread‑safe snapshots for UI
- API (sync): Get/Query; (via Diff): Add/Remove/Move/Rotate/Board/Disembark/MountWeapon/LoadAmmo/AttachHitch/DetachHitch/ToggleAnchor

Occupancy/Indexing
- Maintain a dynamic position index `(x,y,z) → VehicleGuid` per tile in the footprint (real multi‑tile mode);
- For v1 (visual‑only), no occupancy claims; later phases switch to true occupancy.

## 3) Layers & Writes

- L0 Terrain: vehicles do not directly write L0 (except construction of static emplacements via commands/build system).
- L2 Construction/Furniture: static emplacements can be modeled as L2 multi‑tile with VehicleDefinition is_static=true.
- L3 Fluids: ships comply with depth constraints; they don’t write fluids.
- L5 Items: cargo/ammo are Items; loaded/unloaded via ItemsDiff.
- L6 Units: vehicles live as dynamic entities (occupancy index, movement).
- L7 Meta: designations (board/drive/load/fire), traffic, visibility.

## 4) Navigation & Movement

Mobility Classes
- Land: requires walkable floor; uses land clearance map
- Water: requires water depth ≥ draft; water clearance map (future)
- Amphibious: can satisfy either (land or water section) with speed modifiers
- Static: immobile; pathing disabled

Size & Clearance
- Path cache keys include `size_class` and `mobility_class`.
- Derive per‑chunk clearance maps alongside NavMask:
  - `Clearance_W2H1` — at (x,y,z) how many tiles of width‑2×height‑1 area fit (walkable + standable rules)
  - `Clearance_W2H2` — width‑2×height‑2 area fits (also checks z+1 top space)
- On neighbor expansion, the pathfinder consults the relevant clearance (O(1)) rather than checking k cells.

Slope/Stairs/Doors
- Ramps: when stepping from ramp base, ensure footprint critical row/column satisfies up‑ramp rules and roof at z+1 is clear when height>1.
- Stairs: optional wide‑stairs; v1 can be disallowed for large classes.
- Doors: require width/height clearance (future L2 metadata on doors).

Movement Execution
- Deterministic step updates; if occupancy index detects conflicts, movement blocks or replans.
- Rotation incurs turn cost; path expansion can model direction cost or MovementExecutor applies a turn penalty.

## 5) DiffLog Operations

New DiffOp types (arguments abbreviated):
- `AddVehicle(def_id, anchor_x,y,z, rotation)`
- `RemoveVehicle(entity_id)`
- `MoveVehicle(entity_id, x,y,z)`
- `RotateVehicle(entity_id, rotation)`
- `Board(entity_id, seat_id, creature_entity)` / `Disembark(entity_id, seat_id)`
- `MountWeapon(entity_id, mount_id, item_id)` / `LoadAmmo(entity_id, mount_id, ammo_item_id)`
- `AttachHitch(entity_id, creature_entity)` / `DetachHitch(entity_id, creature_entity)`
- `ToggleAnchor(entity_id, on_off)`

Pre/Post Conditions
- Add/Remove affect VehicleManager only; occupancy index updates atomically.
- Move/Rotate must validate clearance/occupancy; reject on conflict.
- Board/Disembark move creatures between world and vehicle seats (L6 occupancy transfer).

## 6) Jobs & Systems

VehicleOpsJobSystem
- Reads designations and plans: board, drive, tow, load, fire, anchor.
- Emits diffs in write phase; respects UpdateOrder and deterministic ordering.

Hauling/Items Integration
- Load/Unload cargo via ItemsDiff; reservations defer to central ReservationManager.

Combat (Mounts/Weapons)
- Mount points constrain arc; firing requires crew; ammo is Items; deterministic RNG for spread.

## 7) Rendering

Visual‑only v1
- Draw multi‑tile footprint and multi‑height body across z‑layers; anchor determines layout.
- No changes to pathing/collision; purely cosmetic.

Full occupancy v2
- Render from footprint tiles; handle occlusion vs terrain/items; show crew positions and mounts.

## 8) Siege Tower Floor Overlay

Temporary Floor Overlay (runtime)
- VehicleDefinition lists local cells that act like floor. WorldNavigationView augments capabilities at those positions without writing L0.
- When the vehicle moves, overlay follows anchor.

## 9) Determinism & Performance

- All movement and job sequencing uses existing deterministic tie‑breakers and UpdateOrder.
- Clearance maps derived with ChunkNavData; cache keys include size/mobility; lazy rebuild on dirty chunks.
- Occupancy checks use per‑tile indices for O(k) footprint validation.

## 10) Save/Load

- Serialize VehicleInstance (anchor, rotation, parts, crew, cargo, hitch) and reconcile Items/Creatures via GUIDs.

## 11) Phased Delivery

Phase G (visual)
- VehicleDefinition + VehicleManager + rendering footprint/height; static emplacements as is_static.

Phase H‑1 (land 2×2×1)
- Occupancy index + Clearance_W2H1; board/drive/move; hitch power; cargo load/unload; conflict handling.

Phase H‑2 (land 2×2×2)
- Clearance_W2H2; ramps/doors/stairs rules (minimal viable subset).

Phase I (water vessels)
- Water mobility clearance; docking/boarding; mounts and broadside.

Phase J (siege tower overlay)
- Temporary floor overlays; interior ladders; cross‑wall traversal.

