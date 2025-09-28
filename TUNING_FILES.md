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

Notes
- Cavern generation uses a single connected band at mid-Z of the densest band from `tuning.mapgen.json`.
- Floor overlays use SurfaceBits: Mud, Grass, Snow, Moss (see TILE_SPEC.md).
- Ores place after caverns and only on SolidWall host rock respecting `allowed_host_tags`.
