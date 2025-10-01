# Phase E: Mining & Items — COMPLETE

## What Was Implemented

- Mining job execution (plan → move to adjacency → dig → commit terrain change → spawn drops).
- Terrain diffs normalize geology to floor variants when converting wall/ramp → floor.
- Drop tables resolved by geology with a cached lookup (supports `core_geology_*` and terrain aliases).
- Deterministic drop quantities using a seed derived from (geology, terrain kind).
- Item spawn stacking and post‑move consolidation.

## Items: Stacking Rules

- Strict, deterministic rule: items stack when they share the same `DefinitionId` and position `(x,y,z)`.
- Spawn‑time stacking: consecutive spawns on the same tile merge immediately.
- Post‑move stacking: after MoveItem/UnmarkCarried, items at the destination tile are consolidated into a single stack.
- Diagnostics: when a spawn finds other items on the tile but no match, a `[ItemManager] STACK-CHECK` entry logs what’s present.

## Performance Optimizations

- Cached mining drop tables: O(1) lookups after start‑up; supports normalized geology keys and terrain aliases.
- Position index for items: per‑tile index accelerates consolidation from O(N) → O(k) on the destination cell.

## Logging

- `[ItemManager] SUCCESS: Stacked ...` for merges.
- `[ItemManager] MERGE: Consolidated ...` for post‑move merges.
- `[ItemManager] STACK-CHECK: ... present={...}` when no spawn‑time stack match occurs.

## Files

```
src/HumanFortress.App/Jobs/MiningJobSystem.cs        // drop cache + geology normalization aliases, deterministic RNG
src/HumanFortress.Simulation/Diff/SimulationDiffApplicator.cs  // SetTerrain normalization; MoveItem/UnmarkCarried → MergeStacksAt
src/HumanFortress.Simulation/Items/ItemManager.cs     // position index; UpdateItemPosition; MergeStacksAt; diagnostics
```

## Validation

- Use F12 to spawn identical items on a single tile → expect a single line `Name xN` in F2 panel.
- Spawn in multiple tiles, haul to one tile → after UnmarkCarried expect consolidation into `xN`.
- Dig different rocks/ores → expect geology‑appropriate `boulder_*` or `ore_*` items per tuning.

---

Phase E Complete — Mining loop, geology‑aware drops, and deterministic stacking.

