# Materials Data Contract

## Core Principles

1. **TerrainKind owns legality** - All navigation legality decisions (walkable/standable/climbable/flyable) are owned by TerrainKind, never materials
2. **Materials provide numeric modifiers only** - Materials can only modify costs, friction, hazards, never flip illegal→legal
3. **Geology combines TerrainKind + Material** - Geology prototypes combine a terrain kind with a material and optional tuning

## Precedence Chain

```
TerrainKind (legality) → Geology Prototype (tuning) → Material (modifiers) → Fields/Fluids → Actor Capabilities
```

## Natural vs Constructed Durability

### Base Properties in Materials
- Materials define baseline `physical.hardness` and `mining.diggingTime`
- These represent the inherent properties of the material itself

### Durability Modifiers
- **Natural terrain** (terrain_bits.natural = true): Use material baseline values
- **Constructed terrain** (terrain_bits.natural = false): Apply multipliers for enhanced durability
  - Example: Constructed granite wall has 1.5x durability vs natural granite wall
  - Multipliers defined at geology prototype level or L2 construction metadata

### Implementation Approach
```json
// In geology prototype
"properties": {
  "durability_multiplier": 1.5,  // For constructed variants
  "mining_time_multiplier": 2.0  // Harder to mine when constructed
}
```

## Placeholders (Not Implemented)

### Climbable Flag
- **Status**: Reserved but not implemented
- **Location**: terrain_kinds.json navigation.climbable field
- **Note**: Ramps and stairs are walk-based Z transitions, not "climb" mechanics
- **Future**: May be used for ladders, ropes, wall climbing

### Fields/Fluids System
- **Status**: Deferred for future implementation
- **Concept**: Separate overlay system for environmental effects
- **Scope**:
  - Fluids: Water depth thresholds, swimming, drowning
  - Fields: Smoke, miasma, fire - affect visibility and hazards
- **Note**: These are NOT material properties but separate L3/L4 tile layers

## File Structure

### Registry Files
- `materials.registry.json` - Main materials database with numeric modifiers only
- `materials.authoring.json` - Optional author-friendly input format
- `geology.json` - Geology prototypes combining terrain + material
- `terrain_kinds.json` - Terrain shapes with navigation legality

### Schema Files
- `materials.registry.schema.json` - Enforces no legality fields in materials
- `material.authoring.schema.json` - Schema for authoring format
- `terrain_kinds.schema.json` - Defines navigation legality structure
- `geology.schema.json` - Geology prototype validation

## Material Navigation Properties

Materials may only define:
- `moveCostModifier` (-50 to +50) - Additive cost modifier
- `frictionModifier` (-1 to +1) - Surface friction effect
- `hazardLevel` (0-10) - Environmental damage
- `hazardType` - Type of hazard (heat, cold, poison, etc)

**Forbidden in materials:**
- walkable, standable, climbable, flyable
- blocksMovement, blocksSight
- Any boolean legality flags

## Validation Checklist

- [ ] All materials validate against materials.registry.schema.json
- [ ] No navigation legality fields in any material
- [ ] All geology prototypes reference valid materials
- [ ] TerrainKind defines all legality decisions
- [ ] Materials provide only numeric modifiers
- [ ] Natural vs constructed use same base material