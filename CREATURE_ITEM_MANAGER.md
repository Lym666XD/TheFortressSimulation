# Creature & Item Manager Implementation

**Status**: In Progress
**Created**: 2025-09-30
**Version**: 1.0

## Overview

Implementation of data-driven creature and item management systems for debug spawning and future gameplay features.

## Architecture

### CreatureManager
- **Location**: `HumanFortress.Simulation/Creatures/`
- **Responsibilities**:
  - Load creature definitions from `data/core/creatures/*.json`
  - Validate definitions against schema
  - Manage runtime creature instances
  - Provide spawn interface
- **Thread Safety**: Read-only after load; instance modifications use locks

### ItemManager
- **Location**: `HumanFortress.Simulation/Items/`
- **Responsibilities**:
  - Load item definitions from `data/core/items/*.json`
  - Build kind-based index (resource/weapon/armor/tool/container/consumable)
  - Validate materials against ContentRegistry
  - Manage runtime item instances
- **Thread Safety**: Read-only after load; instance modifications use locks

## Integration Points

### World.cs
- Holds singleton instances of both managers
- Managers initialized in constructor
- Reference set via `SetWorld()` / `SetDependencies()`

### GameStateManager.cs
- Loads definitions in `InitializeWorld()` method
- After World creation, before gameplay starts
- Loading errors logged but don't stop execution

### Debug UI
- **Tab 0 (Creatures)**: Mouse-clickable creature selection
- **Tab 1 (Items)**: Two-level selection (kind → specific item)
- Spawn position: Mouse hover position on map
- No keyboard shortcuts (all mouse-based)

## Data Flow

```
Startup:
  Program.Main
    └─> ContentRegistry.LoadContent()  // Materials, Terrains, etc.
    └─> GameStateManager.InitializeWorld()
         └─> World.Creatures.LoadDefinitions()
         └─> World.Items.LoadDefinitions()

Debug Spawn:
  User clicks creature/item button
    └─> UI updates selection
  User clicks map
    └─> FortressState.OnMapLeftClickedLocal()
         └─> World.Creatures.SpawnCreature() OR World.Items.SpawnItem()
              └─> Validate position (OpenWithFloor)
              └─> Create instance
              └─> TODO: Write to Chunk via Diff-Log (L5/L6)
```

## Current Limitations (TODOs)

1. **Diff-Log Integration**: Currently instances only tracked in manager, not written to Chunk layers
2. **Material Validation**: Basic validation only; full ContentRegistry integration pending
3. **Stack Merging**: Items don't auto-merge with existing stacks yet
4. **LOD System**: Creatures not integrated with Chunk LOD levels
5. **Body Plans**: Creature body_plan_id validation pending full CREATURE_SPEC implementation

## Error Handling

- **File Load**: Each JSON file wrapped in try-catch; errors logged, loading continues
- **Definition Validation**: Invalid definitions skipped, logged with reason
- **Spawn Validation**: Position/tile validation with null-safe returns
- **Thread Safety**: Concurrent reads safe; write operations use locks

## Testing

### Manual Testing
1. Start game → Enter fortress play
2. Press `F12` to open debug menu
3. Click creature/item buttons to select
4. Click map to spawn at mouse position
5. Verify console logs show successful spawn

### Build Test
```bash
dotnet build
```

## Files Changed/Created

### New Files
- `src/HumanFortress.Simulation/Creatures/CreatureManager.cs`
- `src/HumanFortress.Simulation/Creatures/CreatureDefinition.cs`
- `src/HumanFortress.Simulation/Creatures/CreatureInstance.cs`
- `src/HumanFortress.Simulation/Items/ItemManager.cs`
- `src/HumanFortress.Simulation/Items/ItemDefinition.cs`
- `src/HumanFortress.Simulation/Items/ItemInstance.cs`

### Modified Files
- `src/HumanFortress.Simulation/World/World.cs` (added manager properties)
- `src/HumanFortress.App/GameStates/GameStateManager.cs` (load definitions)
- `src/HumanFortress.App/UI/UiRenderer.cs` (updated DrawDebug method)
- `src/HumanFortress.App/UI/UiStore.cs` (added item kind selection state)
- `src/HumanFortress.App/States/FortressState.cs` (mouse click handling)

## Future Enhancements

1. **Pathfinding Integration**: Creatures use navigation system
2. **AI Behavior**: Faction-based AI for spawned creatures
3. **Item Interactions**: Creatures can pick up/equip items
4. **Save/Load**: Serialize manager state to save files
5. **UI Improvements**: Search/filter for large item catalogs

## References

- `CREATURE_SPEC.md` - Creature definition schema
- `ITEMS_SPEC.md` - Item definition schema v3
- `UPDATE_ORDER.md` - Stage pipeline and L5/L6 layer writes
- `CHUNK_ACTOR_PROTOCOL.md` - Cross-chunk messaging (future)
- `DIFF_LOG_AND_MERGE_STRATEGIES.md` - Write operations (future integration)