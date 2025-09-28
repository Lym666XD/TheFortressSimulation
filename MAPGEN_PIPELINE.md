0) Scope

Updates the pipeline to reflect:

Chunk = 32Ã—32Ã—Z tiles (Z configurable).

Fortress map size: SÃ—S chunks, S âˆ?{1,2,3,4} (user selectable).

World map uses a Square grid (hex interface kept but not used).

Exactly one cavern band (performance) with connected walkway/tunnels (data-driven).

Fortress size does not affect world footprint â€?like RimWorld: a site occupies one world cell regardless of S.

1) Seeds & Reproducibility (Normative)

Root WorldSeed. Derive stage streams as Seed(StageId, WorldCellId|ChunkId).

Never sample RNG inside order-unstable loopsâ€”sort, then sample.

Serialize {WorldSeed, StageSeedSpans, ContentManifest} into world/fortress outputs.

2) Registries & Config (Normative)

/content/registries/:
biome.json, geology.json (strata/ores/aquifer), river.json, road.json, landmark.json, cavern.json (one band), world.params.json.

/content/world.params.json (updated keys):

{
  "grid": { "adjacency": "Square", "allowHex": true },
  "fortress": { "chunk": {"W":32,"H":32,"Z":8}, "sizeOptions": [1,2,3,4] },
  "siteFootprint": { "mode":"SingleWorldTile" },   // S does NOT change footprint
  "scales": { "agentsExp": 0.85, "floraExp": 0.85, "fluidBudgetExp": 0.85, "fieldBudgetExp": 0.85 }
}


Strict schema validation at boot; unknown fields warn once; missing required â‡?fail to main menu (no process crash).

3) World Map (Square Grid) â€?Pipeline (Normative)
3.1 Grid & Interfaces

Square world grid (4-neighbor NESW; optional 8-neighbor for visuals/cost).

Keep Hex interface stubs in schema/IDs; current generator returns â€œnot implementedâ€?for Hex (no save format changes later).

3.2 Stages

Elevation â€?ridged/simplex blend â†?elevation, slope.

Climate â€?temperature (lat/alt lapse), rainfall, drainage.

Hydrography â€?rivers & lakes with erosion passes; export channel polylines + classes.

Biomes & Regions â€?assign by (temp, rain, elevation, drainage); store biomeId.

Factions & Roads â€?settlement placement; roads along least-cost corridors & fords.

Landmarks/Features â€?ruins, craters, sinkholes, shrines with biome/lat-band rates.

Geology Envelope â€?stone strata stack, ore bands, aquifer flags per world cell.

Tile Summary â€?cache {biome,temp,rain,elev,riverClass,stoneSet,aquifer?,landmarks[]}.

Site Placement â€?claiming a site occupies one world cell. S is recorded with the site but does not expand the footprint.

No multi-cell reservation; roads/rivers remain unchanged.

For local continuity when S>1, we will sample neighbor cells at gen time (see Â§4.2).

4) Fortress Map (Local) â€?Pipeline (Normative)
4.1 Size & Chunks

Map size: SÃ—S chunks; chunk = 32Ã—32Ã—Z tiles; S âˆ?{1,2,3,4}.

Save header persists {S, W=32, Z}; renderer & sim use identical geometry.

4.2 Surface Synthesis (with Context Sampling for S>1)

Context window: when S>1, compute a read-only neighbor ring (radius=1 world cells) to sample rivers/roads/biome edges without claiming them.

Inputs: neighbor summaries + polyline clips crossing borders.

Determinism: neighbor sampling order is fixed (clockwise from North).

Heightfield: downscale the home cell elevation; blend border rows with hinted deltas derived from neighbor cells (no ownership change).

Biomes: paint from the home cell; near edges, optionally blend light cosmetic cues if neighbor biomes differ (visual only).

Rivers & Lakes: rasterize the home cellâ€™s polylines; if a neighbor river crosses into the home border, extend a stub inside to keep continuity at the edge (length capped; cosmetic unless it truly enters).

Roads: similar stubs for visual continuity.

Landmarks: instantiate only those owned by the home cell; neighbor landmarks never spawn inside.

4) Fortress Map (Local) ¡ª Pipeline (Normative)
4.3 Subsurface Synthesis (single cavern band)

Strata: apply home-cell geology envelope across the whole fortress map (biome-driven strata stack).

Aquifer: mark columns by geology flags (light/heavy).

Cavern Band ¡Á1: pick the band with highest caves.density; choose mid-Z as floor level; generate a connected walkway from the map edge with periodic rooms and widened tunnels (data-driven via tuning.cavern.json). Floor uses host-rock floor variant; optional moss overlay on the cavern floor.

Guarantee ¡Ý2 natural entrances to surface (shafts at edges connected via widened horizontal tunnels at cavern Z).

Magma (optional): small magma pockets (no global sea).

POIs/Ores: after cavern carving, stamp ore deposits per tuning.ore.json; shapes include vein/blob; respect allowed_host_tags; write to L0 only on SolidWall hosts.

4.4 Commit

Write authoritative layers (L0/L1/L2/L3/L4/L7).

Before first sim tick, run one-time RebuildDerived (nav/support/opacity).

5) Scaling with S (Normative)

Define TilesTotal = SÃ—SÃ—32Ã—32Ã—Z. Scale budgets/subsystems by TilesTotal^exp (defaults 0.85):

Fluids (F), Fields (G) per-tick quotas

Initial agents/animals cap, flora density, attempt counts

Pathfinding/search: scale max distance âˆ?S, but branch/backtrack caps âˆ?S^0.85.

Rendering: snapshot/delta remains chunk-local; larger S touches more chunks, but only dirty ones rebuild.

6) Determinism & Stability (Normative)

Stage boundaries wrapped in tryâ€“catch; on bad content/calculation, skip that unit with a structured log; never crash.

Outputs include version & seedSpan for replay audits.

7) Parallelization (Normative)

World: parallel per world cell; single commit at end.

Fortress: parallel per column/chunk; no shared writesâ€”final data goes through Diff-Log â†?Merge â†?Commit.

8) Files & Persistence (Normative)

World file: planet params, world tiles summary, site list (each with {cellId, S}), content manifest.

Fortress seed: {cellId, S, chunkW=32, Z, geology envelope, RNG offsets}; no derived caches.

Save also records {grid.adjacency, paletteVersion} for replay parity.

9) Testing & CI (Normative)

Determinism: identical seeds â‡?identical world/fortress hashes (across OS/CPU/threads).

Hydrology sanity: inflow/outflow within epsilon; border stubs never create illegal loops.

Geology sanity: strata order valid; aquifer flags consistent.

Context sampling: enabling S>1 yields identical results regardless of neighbor enumeration (thanks to fixed clockwise order).

Stability fuzz: random invalid entries donâ€™t crash; errors are surfaced.

S-scaling: S=1,2,3,4 meet budgets.

10) Minimal LLM Template
{
  "task": "GenerateFortress(S)",
  "inputs": ["WorldSeed","WorldCell(home)","WorldNeighbors(r=1 summaries)","Registries","Params{W=32,Z,exp=0.85}"],
  "steps": ["SurfaceFromHome","ContextSampleNeighbors","RiversRoadsClamp","LandmarksFromHome",
            "StrataAquiferFromHome","CavernOneBand","POIs","Commit","RebuildDerivedOnce"],
  "outputs": ["Chunks SxS (32x32xZ)","InitialSnapshot"],
  "rules": ["Deterministic PRNG","Square grid only (hex stub)","Single cavern band","Scale budgets by TilesTotal^exp","Do NOT expand world footprint"]
}


Write authoritative layers (L0/L1/L2/L3/L4/L7).

Before first sim tick, run one-time RebuildDerived (nav/support/opacity).

Tuning files:
- content/registries/tuning.mapgen.json (surface base_z/sky_above, hills, bands, biome_surface_floor)
- content/registries/tuning.cavern.json (connected cavern parameters)
- content/registries/tuning.ore.json (ore abundance/size, per-ore host tags & shape)

