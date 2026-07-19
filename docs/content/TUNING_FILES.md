**Tuning Files Overview**

- `content/registries/tuning.mapgen.json`
  - `surface.base_z`, `surface.sky_above`
  - `hills.enabled`, `hills.radius_min/max`, `hills.density`, `hills.max_delta_z`
  - `bands[]`: `{ name, z_min, z_max, caves: { density } }`
  - `biome_surface_floor`: per-biome floor geology id mapping

- `content/registries/tuning.cavern.json`
  - `band.mode`: `max_density`
  - `path.thickness`, `path.steps_factor`, `path.direction_bias_east`, `path.tunnel_width`
  - `rooms.radius_min`, `rooms.radius_max`, `rooms.interval_factor`
  - `shafts.count`, `moss_on_floor`

- `content/registries/tuning.ore.json`
  - `global.tiles_per_deposit`, `global.density_k`, `global.abundance_mult`, `global.size_mult`
  - `ores[]`: `{ id, allowed_host_tags[], rarity, form: vein|blob, size_mult?, thickness_mult?, radius_mult?, ... }`
  - `vein.size [min,max]`, `vein.thickness [min,max]`, `vein.orientation_bias`, `vein.branch_chance`
  - `blob.size [min,max]`, `blob.radius [min,max]`

- `content/registries/tuning.navigation.json`
  - `allow_diagonals`: enable 8-neighbor expansion (corner rules may apply)
  - `cost`: `{ base, orthogonal, diagonal, ramp_delta, stair_delta }`
  - `fluids`: `{ shallow_threshold, deep_threshold, wade_cost, swim_cost }`
  - `traffic`: `{ low, normal, high, restricted }`
  - `doors`: `{ closed_blocks, open_cost }`
  - `movement`: `{ step_delay_ticks }`
  - `budgets`: `{ max_nodes_per_search, max_paths_per_tick }`
  - `ramp_vertical_alignment_mode`: `"df"` — ramp at (x,y,z) ascends to neighbors at z+1 while (x,y,z+1) stays `OpenNoFloor`
  - `ramp_requires_highside_support`: true|false — require support at high side below
  - `diagonal_rules`: optional corner checks for diagonals
  - `surface_cost`: optional map `{ mud, snow, grass, moss, ... } -> cost adj`

- `content/registries/tuning.scheduler.json`
  - `threads`: current deterministic scheduler thread setting
  - `queue_policy`: current queue policy identifier
  - `budgets`: `{ hauling, mining, construction }`, each with deterministic `{ plan_per_tick }`
  - `backpressure`: `{ max_carryover_ticks }`
  - `hauling_limits`: active/backlog intake limits and reserve thresholds

Notes
- Cavern generation uses a single connected band at mid-Z of the densest band from `tuning.mapgen.json`.
- Floor overlays use SurfaceBits: Mud, Grass, Snow, Moss (see TILE_SPEC.md).
- Ores place after caverns and only on SolidWall host rock respecting `allowed_host_tags`.
