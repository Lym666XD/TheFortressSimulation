# Validation & Definition of Done Checklist

## Data/Documentation Validation

### Schema Validation
- [ ] materials.registry.json validates against materials.registry.schema.json
- [ ] geology.json validates against geology.schema.json
- [ ] terrain_kinds.json validates against terrain_kinds.schema.json
- [ ] geology_prototypes.json is valid JSON with all required fields

### Documentation Updates
- [x] TILE_SPEC.md updated with canonical TerrainBits layout
- [x] TILES_MATERIALS_ARCHITECTURE.md is single source of truth
- [x] GEOLOGY_COMPILER_SPEC.md contains final rules
- [x] MATERIALS_DATA_CONTRACT.md documents the contract
- [x] RUNTIME_PROPAGATION_REQUIREMENTS.md defines system updates

### Data Contract Enforcement
- [x] No walkable/standable/climbable/flyable fields in materials.registry.json
- [x] No support_flag in geology.json terrain_bits
- [x] RampDirection uses numeric 0-7 encoding
- [x] All materials referenced by geology exist (via name or alias)

## Runtime System Validation

### Tile System
- [ ] Bit packing/unpacking unit tests pass
- [ ] TerrainKind extraction (bits 0-2) correct
- [ ] RampDirection extraction (bits 3-5) correct
- [ ] Natural flag (bit 6) preserved correctly
- [ ] Smoothed/Engraved bits (7-8) handled
- [ ] Reserved bits (9-15) always zero

### Navigation System
- [ ] Walls (TerrainKind=0) never walkable/standable
- [ ] Floors (TerrainKind=1) always walkable/standable
- [ ] Air (TerrainKind=2) never standable but flyable
- [ ] Ramps (TerrainKind=3) allow directional Z-transitions
- [ ] Stairs (TerrainKind=4,5,6) allow appropriate Z-transitions
- [ ] OpenNoFloor never walkable but flyable (no support)

### Cost Calculation
- [ ] Base cost from geology.properties.nav_cost_base
- [ ] Material modifier applied additively
- [ ] Fluid depth affects cost correctly
- [ ] Traffic levels modify cost as expected
- [ ] Final cost clamped to valid range (1-999)

### Ramp Navigation
- [ ] Ascent only from opposite direction
- [ ] Descent allowed from ramp direction
- [ ] All 8 compass directions (0-7) work
- [ ] Invalid directions cause error

## Worldgen Validation

### Geology Usage
- [ ] All tiles reference valid geology prototype IDs
- [ ] No ad-hoc terrain/material combinations
- [ ] Natural flag set appropriately

### Determinism
- [ ] Golden seeds produce identical maps
- [ ] Documented any expected differences
- [ ] No non-deterministic operations

## Performance Validation

### Navigation Cache
- [ ] Rebuild time within budget (<3ms per chunk)
- [ ] Cache invalidation triggers correct
- [ ] Memory footprint acceptable

### Pathfinding
- [ ] A* node budget maintained (10000 max)
- [ ] Performance parity or better
- [ ] No regression in path quality

## Compiler Validation

### Input Processing
- [ ] Parses all authoring JSON files
- [ ] Resolves material aliases correctly
- [ ] Maps terrain kind names properly

### Output Generation
- [ ] TerrainBits use canonical layout
- [ ] RampDirection validated (0-7 for Ramps only)
- [ ] Cost normalization applied (0-255 → 0-999)
- [ ] Opacity normalization applied (0-255 → 0-100)

### Error Handling
- [ ] Unknown terrain kinds cause error
- [ ] Missing materials cause error
- [ ] Invalid ramp directions cause error
- [ ] Out-of-range values cause error

## Integration Tests

### End-to-End Scenarios
- [ ] Dwarf can walk on floors
- [ ] Dwarf cannot walk through walls
- [ ] Dwarf can use stairs to change Z-levels
- [ ] Dwarf can ascend ramps from correct direction
- [ ] Flying creatures can pass over chasms
- [ ] Swimming creatures handle fluids correctly

### Save/Load
- [ ] TerrainBits preserved correctly
- [ ] Navigation cache rebuilt on load
- [ ] No data loss or corruption

## Regression Tests

### Navigation Regression Suite
- [ ] All existing navigation tests pass
- [ ] No new pathfinding failures
- [ ] Movement costs consistent

### Worldgen Regression Suite
- [ ] Map generation completes without errors
- [ ] Terrain distribution as expected
- [ ] Material placement correct

## Documentation of Done

### Code Changes
- [ ] All obsolete code removed (support_flag, redundant bits)
- [ ] New code follows project conventions
- [ ] Comments updated where needed

### Configuration
- [ ] JSON files properly formatted
- [ ] Schema versions updated if needed
- [ ] No unused configuration remains

### Communication
- [ ] PR description complete
- [ ] Breaking changes documented
- [ ] Migration guide provided if needed

## Sign-off Criteria

- [ ] All unit tests passing
- [ ] All integration tests passing
- [ ] Performance metrics met
- [ ] Documentation complete
- [ ] Code review approved
- [ ] No critical bugs found

## Known Issues/Deferred Items

### Not Implemented
- Climbable flag (reserved for future)
- Fields/Fluids overlay system (deferred)
- Legacy save converter (no save system yet)

### Accepted Limitations
- Ramps limited to 8 cardinal/intercardinal directions
- No diagonal ramps in first iteration
- Swimming/climbing mechanics stub only
