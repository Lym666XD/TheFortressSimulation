# PR: Unify TerrainBits/Geology Contract and Propagate to Runtime

## Summary
This PR establishes the canonical data contract for tiles, materials, and geology, then propagates these changes throughout the runtime systems. The core principle: **TerrainKind owns legality, Materials provide modifiers only, Geology combines them for runtime.**

## Key Changes

### 1. Canonical TerrainBits Layout (Breaking Change)
**Old layout had redundant bits:**
- Separate bits for support_flag, stairs, chasm (redundant with TerrainKind)
- Unclear ramp direction encoding

**New canonical layout:**
```
bits 0-2:  TerrainKind (0-7)
bits 3-5:  RampDirection (0-7: N,NE,E,SE,S,SW,W,NW) - valid only for Ramp
bit 6:     Natural (1=natural, 0=constructed)
bit 7:     Smoothed (optional finish state)
bit 8:     Engraved (optional finish state)
bits 9-15: Reserved (must be 0)
```

### 2. Data Contract Enforcement

#### TerrainKind Authority
- **Owns**: walkable, standable, climbable, flyable decisions
- **Owns**: support rules, Z-transitions (ramps/stairs)
- **Values**: 0=SolidWall, 1=OpenWithFloor, 2=OpenNoFloor, 3=Ramp, 4=Slope, 5=StairsUp, 6=StairsDown, 7=StairsUD

#### Materials as Modifiers Only
- **Removed**: All walkable/standable/climbable/flyable fields from materials
- **Kept**: Physical properties (density, hardness, friction)
- **Added**: moveCostModifier, frictionModifier, hazardLevel
- **Principle**: Materials can never flip illegal→legal

#### Geology as Runtime Prototypes
- Combines TerrainKind + Material + tuning (nav_cost_base, opacity)
- Explicit natural flag (no more inferring from tags)
- RampDirection required for Ramps, forbidden for others

### 3. Ramp Direction Solution
- **No "none" value** - being a ramp is determined by TerrainKind=3
- **8 compass directions** using 3 bits (0-7)
- **Authoring shortcuts**: "auto" or omitted → compiler determines
- **Runtime requirement**: Must be 0-7 for all Ramp tiles

### 4. File Structure Changes

#### Renamed Files
- `materials_v2.json` → `materials.registry.json`
- `materials.json` → `materials.authoring.json`
- `materials.schema.json` → `materials.registry.schema.json`
- `material.schema.json` → `material.authoring.schema.json`

#### New Files
- `geology_prototypes.json` - Authoring format with explicit natural flag
- `GEOLOGY_COMPILER_SPEC.md` - Authoritative compiler specification
- `RUNTIME_PROPAGATION_REQUIREMENTS.md` - System update requirements
- `VALIDATION_DOD_CHECKLIST.md` - Complete validation checklist

#### Updated Schemas
- Removed navigation legality from materials schema
- Added proper validation rules for ramp directions
- Enforced material-as-modifier-only contract

### 5. Runtime System Updates Required

#### Tile System
- Update bit packing/unpacking to canonical layout
- Remove obsolete support_flag handling
- Update serialization and debug tools

#### Navigation System
- Legality checks use TerrainKind only
- Cost = geology.nav_cost_base + material.modifier + fluids + traffic
- Ramp navigation validates direction (0-7)

#### Worldgen
- Must use canonical geology.json (no ad-hoc combinations)
- Natural flag via bit 6, not tags
- Re-baseline golden seeds

## Cost Precedence (Clarified)

1. **terrain_kinds.navigation.baseCost** - Authoring default only
2. **geology.properties.nav_cost_base** - Runtime L0 base cost (authoritative)
3. **materials.navigation.moveCostModifier** - Additive modifier
4. **Final** = geology base + material modifier + fluid/field/traffic adjustments

## Breaking Changes

1. **TerrainBits layout changed** - Requires tile system updates
2. **Materials no longer define legality** - Navigation code must use TerrainKind
3. **support_flag removed** - Support determined by TerrainKind only
4. **File renames** - Build scripts and loaders need path updates

## Migration Guide

1. Update bit manipulation code to use new layout
2. Replace material legality checks with TerrainKind checks
3. Update worldgen to use geology prototypes
4. Re-run golden seed generation
5. Update any tools that display TerrainBits

## Testing

- [x] All JSON files validate against schemas
- [x] No illegal legality fields in materials
- [x] Geology references resolve via materials registry
- [ ] Unit tests for new bit layout
- [ ] Navigation regression suite passes
- [ ] Golden seeds re-baselined
- [ ] Performance metrics maintained

## Deferred/Not Implemented

- **Climbable**: Flag preserved but not implemented (future ladders/ropes)
- **Fields/Fluids**: Overlay system deferred to separate PR
- **Legacy converter**: No save system yet, so no converter needed

## Documentation

All changes documented in:
- TILES_MATERIALS_ARCHITECTURE.md (single source of truth for bit layout)
- GEOLOGY_COMPILER_SPEC.md (compiler rules and validation)
- MATERIALS_DATA_CONTRACT.md (material/terrain/geology contract)
- RUNTIME_PROPAGATION_REQUIREMENTS.md (system integration guide)

## Definition of Done

See VALIDATION_DOD_CHECKLIST.md for complete criteria. Key points:
- ✅ Canonical TerrainBits layout established
- ✅ Materials limited to modifiers only
- ✅ Geology prototypes with explicit natural flag
- ✅ Compiler specification complete
- ✅ Runtime propagation documented
- ⏳ Unit/integration tests updated
- ⏳ Performance validation complete
