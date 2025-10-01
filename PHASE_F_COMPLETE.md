# Phase F: Hauling & Stockpile — COMPLETE (v1)

## What Was Implemented

- Haul job execution: assign worker → path to item → MarkCarried → path to destination → MoveItem → UnmarkCarried.
- Central reservation to prevent double assignment; basic TTL support.
- Post‑drop consolidation (after UnmarkCarried) merges stacks at the destination tile.

## Integration with Navigation

- Uses the shared NavigationManager and the deterministic A* path solver.
- Respects ConnectivityVersion invalidation; replans on topology changes.

## Stockpile Hooks (Foundations)

- Per‑chunk stockpile data structures exist (zones/shards); capacity/slot bookkeeping is present to build upon.
- Items panel (F2) reflects consolidated stacks (`Name xN`).

## Logging

- `[HAULJOBS][tick] Assigned ...` and movement replans.
- Diffs: `MoveItem`, `MarkCarried`, `UnmarkCarried`.
- `[DIFF][Items] MergeStacksAt (uncarry) ...` emitted on successful post‑drop stacking.

## Files

```
src/HumanFortress.App/Jobs/HaulJobSystem.cs
src/HumanFortress.Simulation/Diff/SimulationDiffApplicator.cs   // MoveItem / MarkCarried / UnmarkCarried → MergeStacksAt
src/HumanFortress.Simulation/Stockpile/*                         // zone/shard data foundations
```

## Validation

- Create multiple identical items in different tiles; designate a destination; run hauling.
- After items are delivered and uncarried, the destination shows a single consolidated stack in F2.

---

Phase F Complete — Deterministic hauling loop with post‑drop stack consolidation and stockpile foundations.

