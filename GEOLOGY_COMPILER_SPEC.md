# Geology Compiler Specification (FINAL)

## Overview
The geology compiler transforms `geology_prototypes.json` (authoring format) into `geology.json` (runtime format).
This is the authoritative specification for the data contract between TerrainKind (legality), Materials (modifiers), and Geology (runtime tiles).

## TerrainKind Name Mapping

The compiler must map between terrain_kinds.json names and geology.json enum values:

| terrain_kinds.json | geology.json (enum) |
|-------------------|-------------------|
| `solid_wall`      | `SolidWall`       |
| `open_floor`      | `OpenWithFloor`   |
| `open_space`      | `OpenNoFloor`     |
| `ramp`            | `Ramp`            |
| `stairs_up`       | `StairsUp`        |
| `stairs_down`     | `StairsDown`      |
| `stairs_updown`   | `StairsUD`        |
| `chasm`           | `Chasm`           |

**Error Handling**: Unknown terrain kind names must cause compilation to fail with clear error message.

## Value Normalization

### Navigation Cost (navCostBase)
- **Input Range**: 0-255 (prototype)
- **Output Range**: 0-999 (runtime)
- **Formula**: `runtime = prototype * 3.9`
- **Special Values**:
  - 255 (max) → 999 (impassable)
  - 0 → 0 (no additional cost)

### Opacity
- **Input Range**: 0-255 (prototype)
- **Output Range**: 0-100 (runtime percentage)
- **Formula**: `runtime = round(prototype * 100 / 255)`
- **Special Values**:
  - 255 → 100 (fully opaque, blocks sight)
  - 0 → 0 (fully transparent)

## Natural Flag Priority

1. **Explicit boolean** (`natural: true/false` in prototype) takes precedence
2. **Tag inference** (if boolean not present):
   - Contains "natural" tag → `natural: true`
   - Contains "constructed" tag → `natural: false`
   - Neither tag → compiler warning, default to `true`

## Ramp Direction Handling

- **Field Name**: Use `rampDirection` consistently
- **Encoding** (3 bits, values 0-7):
  - 0: `north`
  - 1: `northeast`
  - 2: `east`
  - 3: `southeast`
  - 4: `south`
  - 5: `southwest`
  - 6: `west`
  - 7: `northwest`

### Rules:
1. **If terrainKind != "ramp"**: `rampDirection` must NOT appear in output
2. **If terrainKind == "ramp"**: `rampDirection` MUST be one of the 8 directions
3. **Authoring shortcuts**:
   - Omitted field → compiler infers direction at build time
   - `"auto"` → compiler determines best direction based on context
4. **Validation**: Unknown direction values are compilation errors

## Cost Precedence Clarification

1. **terrain_kinds.navigation.baseCost** - Authoring default only (used if prototype omits cost)
2. **geology.properties.nav_cost_base** - Runtime L0 base cost (authoritative)
3. **materials.navigation.moveCostModifier** - Additive modifier (never flips illegal→legal)
4. **Final cost** = geology.nav_cost_base + material.moveCostModifier + fluid/field modifiers

## Material Resolution

1. Try exact match with material name in materials.registry.json
2. Try aliases if no exact match
3. Error if material cannot be resolved

## Compiler Output Structure

```json
{
  "id": "<prototype_id>",
  "tags": [...],
  "material": "<resolved_material_id>",
  "terrain_bits": {
    "kind": "<mapped_terrain_kind>",
    "natural": <boolean>,
    "rampDirection": "<direction_if_ramp>"
  },
  "display": {
    // Generated or copied from prototype
  },
  "properties": {
    "mineable": <boolean>,
    "buildable": <boolean>,
    "smoothable": <boolean>,
    "nav_cost_base": <normalized_0_999>,
    "opacity": <normalized_0_100>
  }
}
```

## Validation Rules

1. **Required Fields**: id, terrainKind, material must be present
2. **Material exists**: Material must resolve via registry or aliases
3. **TerrainKind valid**: Must map to known enum value
4. **Cost ranges**: navCostBase 0-255, opacity 0-255 in prototype
5. **Ramp validation**: If terrainKind is "ramp", rampDirection required
6. **Natural flag**: Prefer explicit boolean over tag inference

## Support Flag Removal

**IMPORTANT**: `support_flag` has been removed from terrain_bits. Support is now determined entirely by TerrainKind definitions in terrain_kinds.json. The compiler must NOT generate support_flag in output.

## Example Transformations

### Example 1: Wall (no ramp direction)
**Input** (geology_prototypes.json):
```json
{
  "id": "granite_wall",
  "terrainKind": "solid_wall",
  "material": "granite",
  "natural": true,
  "navCostBase": 255,
  "opacity": 255
}
```

**Output** (geology.json):
```json
{
  "id": "terrain_granite_wall",
  "material": "core_mat_stone_granite",
  "terrain_bits": {
    "kind": "SolidWall",
    "natural": true
  },
  "properties": {
    "nav_cost_base": 999,
    "opacity": 100
  }
}
```

### Example 2: Ramp (with direction)
**Input** (geology_prototypes.json):
```json
{
  "id": "granite_ramp",
  "terrainKind": "ramp",
  "material": "granite",
  "natural": false,
  "rampDirection": "auto",
  "navCostBase": 150,
  "opacity": 0
}
```

**Output** (geology.json):
```json
{
  "id": "terrain_granite_ramp",
  "material": "core_mat_stone_granite",
  "terrain_bits": {
    "kind": "Ramp",
    "natural": false,
    "rampDirection": "north"
  },
  "properties": {
    "nav_cost_base": 585,
    "opacity": 0
  }
}
```

## Compiler Implementation Checklist

1. ✅ Parse authoring files (geology_prototypes.json, materials.registry.json, terrain_kinds.json)
2. ✅ Resolve material names/aliases via mapping table
3. ✅ Generate TerrainBits using canonical layout (bits 0-2: kind, 3-5: rampDir, 6: natural, 7-8: finish)
4. ✅ Handle rampDirection: "auto"/missing → infer; validate 0-7; only for Ramp kinds
5. ✅ Apply unit normalization (navCostBase 0-255 → 0-999; opacity 0-255 → 0-100)
6. ✅ Fill display/autotile defaults as required
7. ✅ Validate illegal combinations (walls never walkable; air never standable)
8. ✅ Emit runtime geology.json (no support_flag, no redundant Stair/Chasm bits)

## Error Messages

- `Unknown terrain kind: {name}` - Invalid terrainKind
- `Material not found: {name}` - Unresolvable material
- `Ramp requires direction` - Missing rampDirection for ramp terrainKind
- `Non-ramp terrain cannot have rampDirection: {kind}` - rampDirection on non-ramp
- `Invalid ramp direction: {value}` - Not one of the 8 compass directions
- `Invalid nav cost: {value}` - Out of range 0-255
- `Invalid opacity: {value}` - Out of range 0-255