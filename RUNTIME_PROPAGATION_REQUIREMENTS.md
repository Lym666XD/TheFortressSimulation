# Runtime System Propagation Requirements

## Overview
This document defines mandatory changes to propagate the unified TerrainBits/Geology contract to all runtime systems.

## 1. Tile System Updates

### Bit Packing/Unpacking
- **Remove**: Support/Stair/Chasm individual bits (redundant with TerrainKind)
- **Update**: Use canonical layout from TILES_MATERIALS_ARCHITECTURE.md
  - bits 0-2: TerrainKind (0-7)
  - bits 3-5: RampDirection (0-7, valid only for Ramp)
  - bit 6: Natural (1=natural, 0=constructed)
  - bit 7: Smoothed finish state
  - bit 8: Engraved finish state
  - bits 9-15: Reserved (must be 0)

### TerrainKind Interpretation
```csharp
public static class TerrainBitOps
{
    public static TerrainKind GetKind(ushort bits) => (TerrainKind)(bits & 0x7);
    public static byte GetRampDirection(ushort bits) => (byte)((bits >> 3) & 0x7);
    public static bool IsNatural(ushort bits) => (bits & 0x40) != 0;
    public static bool IsSmoothed(ushort bits) => (bits & 0x80) != 0;
    public static bool IsEngraved(ushort bits) => (bits & 0x100) != 0;

    // Ramp direction is only valid when kind == Ramp
    public static bool HasValidRampDirection(ushort bits)
    {
        var kind = GetKind(bits);
        return kind == TerrainKind.Ramp;
    }
}
```

### Serialization/Debug Updates
- Update all visualization tools to show new bit layout
- Remove obsolete support_flag displays
- Show ramp direction only when TerrainKind=Ramp

### ConnectivityVersion Triggers
Invalidate navigation cache when any of these change:
- TerrainKind (bits 0-2)
- RampDirection (bits 3-5) for Ramp tiles
- Natural flag (bit 6) if it affects durability
- Surface finish (bits 7-8) if it affects movement cost

## 2. Fortress Map Generation (Worldgen)

### Geology Consumption
- **Use canonical geology.json**: Never create ad-hoc terrain/material combinations
- **Respect prototypes**: All tiles must reference valid geology prototype IDs
- **Natural flag handling**:
  - Natural formations: Set bit 6 = 1
  - Player constructions: Set bit 6 = 0 via L2 layer, not L0 modification

### Ramp Direction Assignment
- Let compiler set default directions in geology.json
- Worldgen may override for specific topography needs
- Validate direction is 0-7 for all Ramp tiles

### Golden Seed Updates
- Re-baseline all golden seeds after bit layout change
- Document expected differences (removed bits, new layout)
- Confirm determinism with new layout

## 3. Navigation System

### Legality Determination
```csharp
public static class NavLegality
{
    public static bool IsWalkable(TerrainKind kind)
    {
        return kind switch
        {
            TerrainKind.OpenWithFloor => true,
            TerrainKind.Ramp => true,
            TerrainKind.StairsUp => true,
            TerrainKind.StairsDown => true,
            TerrainKind.StairsUD => true,
            _ => false
        };
    }

    public static bool IsStandable(TerrainKind kind)
    {
        return kind == TerrainKind.OpenWithFloor;
    }

    public static bool IsFlyable(TerrainKind kind)
    {
        return kind != TerrainKind.SolidWall;
    }

    public static bool ProvidesSupport(TerrainKind kind)
    {
        return kind == TerrainKind.SolidWall ||
               kind == TerrainKind.OpenWithFloor;
    }
}
```

### Cost Calculation
```csharp
public static class NavCost
{
    public static ushort CalculateFinalCost(
        ushort geologyBaseCost,     // From geology.properties.nav_cost_base
        short materialModifier,      // From material.navigation.moveCostModifier
        byte fluidDepth,            // From L3
        byte trafficLevel)          // From L7
    {
        int cost = geologyBaseCost;
        cost += materialModifier;

        // Fluid modifiers
        if (fluidDepth > 0)
        {
            cost += fluidDepth <= 3 ? 6 : 18; // shallow vs deep
        }

        // Traffic modifiers
        cost += trafficLevel switch
        {
            0 => -2, // Low traffic
            1 => 0,  // Normal
            2 => 2,  // High traffic
            3 => 8,  // Restricted
            _ => 0
        };

        return (ushort)Math.Max(1, Math.Min(999, cost));
    }
}
```

### Ramp Navigation
```csharp
public static class RampNav
{
    // Direction encoding: 0=N, 1=NE, 2=E, 3=SE, 4=S, 5=SW, 6=W, 7=NW
    private static readonly (int dx, int dy)[] Directions =
    {
        (0, -1),  // North
        (1, -1),  // Northeast
        (1, 0),   // East
        (1, 1),   // Southeast
        (0, 1),   // South
        (-1, 1),  // Southwest
        (-1, 0),  // West
        (-1, -1)  // Northwest
    };

    public static bool CanAscendRamp(int fromX, int fromY, int rampX, int rampY, byte rampDirection)
    {
        // Must approach from opposite direction (low side)
        var (dx, dy) = Directions[rampDirection];
        int expectedFromX = rampX - dx;
        int expectedFromY = rampY - dy;

        return fromX == expectedFromX && fromY == expectedFromY;
    }

    public static (int x, int y, int z) GetRampDestination(int rampX, int rampY, int rampZ, byte rampDirection)
    {
        var (dx, dy) = Directions[rampDirection];
        return (rampX + dx, rampY + dy, rampZ + 1);
    }
}
```

### Remove Obsolete Logic
- Delete any code checking individual Support/Stair/Chasm bits
- Remove support_flag checks (now determined by TerrainKind)
- Clean up redundant stair direction logic (encoded in TerrainKind itself)

## 4. System Integration Points

### Chunk RebuildDerived()
```csharp
public void RebuildDerived()
{
    for (int idx = 0; idx < 1024; idx++)
    {
        var tile = Tiles[idx];
        var terrainBits = tile.TerrainBits;
        var kind = TerrainBitOps.GetKind(terrainBits);

        // Get geology prototype
        var geology = GeologyRegistry.Get(tile.GeoMatId);

        // Get material modifiers
        var material = MaterialRegistry.Get(geology.MaterialId);

        // Build NavMask based on TerrainKind
        NavMask[idx] = BuildNavMask(kind);

        // Calculate NavCost
        NavCost[idx] = NavCost.CalculateFinalCost(
            geology.NavCostBase,
            material.MoveCostModifier,
            tile.FluidDepth,
            GetTrafficLevel(tile.MetaBits));
    }

    ConnectivityVersion++;
}
```

### Pathfinding Updates
- Use TerrainKind for all legality checks
- Apply cost precedence: geology base → material modifier → fluids → traffic
- Handle ramps with directional validation

## 5. Validation Requirements

### Unit Tests
- Bit packing/unpacking for all TerrainKind values
- Ramp direction validation (0-7 only for Ramps)
- Natural flag preservation
- Cost calculation with all modifier combinations

### Integration Tests
- Walls block all movement
- Floors allow walking/standing
- Ramps allow directional Z-transitions
- Air (OpenNoFloor) allows flying only
- Materials never override TerrainKind legality

### Performance Tests
- Navigation cache rebuild time
- Pathfinding node budget compliance
- Memory footprint with new bit layout

## 6. Migration Checklist

1. ✅ Update bit constants in Tile system
2. ✅ Modify TerrainBits packing/unpacking functions
3. ✅ Update worldgen to use canonical geology
4. ✅ Refactor navigation legality checks
5. ✅ Implement new cost calculation
6. ✅ Update ramp navigation logic
7. ✅ Remove obsolete support_flag code
8. ✅ Update unit tests
9. ✅ Re-baseline golden seeds
10. ✅ Validate performance metrics
