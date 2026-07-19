RENDERING_SNAPSHOT.md — Unified (Schema · Delta · Autotile · Draw Order · SadConsole)
id: rendering-snapshot.v2
status: normative
owner: client/rendering
last_updated: 2025-09-14
version_policy: semver

Current implementation note (2026-07-09):

- The old `HumanFortress.Simulation.Rendering.RenderSnapshot` and builder have been removed from active code.
- Active fortress map, overlay, drawer, debug, workshop, stockpile/zone, tile-inspection, and placement-preview paths consume Runtime-built DTOs from `HumanFortress.Contracts.Runtime.Snapshots`.
- SadConsole drawing and transient UI state remain App-owned; App reaches Runtime through facade/query ports rather than live `World` or concrete job systems.
- Frame/overlay aggregate DTOs now carry Runtime-authored presenter-frame identity, frame render DTOs carry map-viewport changed-cell/row/screen-region deltas over final screen-cell values, App.Rendering consumes those map deltas through `FortressMapViewportPresenterCache`, UI overlay DTOs carry section-level deltas over panel/overlay sections, and App.Rendering consumes those section deltas through `FortressUiOverlayPresenterCache`. Treat the broader packed world-chunk schema below as the remaining target presenter contract; the concrete active DTO names are currently the Runtime/Contracts snapshot DTOs rather than the old Simulation rendering DTOs.

0) Scope

This file merges and supersedes prior RENDERING_SNAPSHOT and AUTOTILING_AND_ROTATION notes and binds them to Update Order and Chunk/Data Layout.

It defines the only renderer input: an immutable, per-tick snapshot built after simulation commits. Renderers (SadConsole) must not read the live world.

1) Principles (Normative)

Isolation: renderer reads snapshot only; no sim mutations or live reads.

Delta-first: rebuild dirty chunks (and dirty Z-slices) only; maintain versioning to avoid redundant uploads.

Data-driven visuals: autotiling & rotation resolved during snapshot-build from registries; world state stays compact.

Deterministic frames: given world seed + tick, two runs produce identical snapshots.

2) Snapshot Lifecycle (Per Tick)

Simulation stages run and commit.

BuildRenderSnapshot pulls authoritative data + derived caches, resolves visual variants, and emits a read-only snapshot.

Presenter (SadConsole) consumes the snapshot, doing no gameplay queries.

Old snapshot is discarded or pooled after the frame.

3) Top-Level Schema (Normative)
{
  "world": { "id": "wf-001", "tick": 123456, "seed": "0xA1B2..." },
  "view": {
    "camera": { "chunk": "C_10_4", "center": [16,16,6], "z0": 4, "zCount": 3 },
    "viewport": { "tiles": [120, 60] }     // width × height in tiles
  },
  "tileset": {
    "atlasId": "tileset/base",
    "paletteVersion": 7,                    // bump when art/indices change
    "palette": { "count": 4096 }            // sprite table size (uint16 indices)
  },
  "chunks": [
    {
      "chunkId": "C_10_4",
      "version": 421,                       // bump when any visual cell changed
      "z": [
        {
          "zIndex": 4,
          "version": 120,                   // per-Z slice version
          "tilePaletteIndex": "uint16[]",   // W×W, resolved autotile/rotation
          "fluidDepth": "uint8[]",          // W×W, 0..7 (optional if zero)
          "fieldGlyphs": "var[]",           // sparse: per-tile small list
          "designations": "bitset[]",       // L7 UI overlays (dig, forbid…)
          "tintLight": "rgba8[]",           // optional per-tile tint/lighting
          "animPhase": "uint8[]"            // optional; tick-based phase
        }
      ],
      "billboards": [
        {
          "id": "ent#u123",
          "tile": [12, 7, 5],
          "paletteIndex": 258,
          "layer": "Units",                 // draw bucket
          "flipX": false,
          "screenOffsetPx": [0, 0],
          "zOrderBias": 0
        }
      ]
    }
  ],
  "ui": {
    "cursor": { "tile": [cx, cy, cz] },
    "selection": [ /* tile ranges */ ],
    "debug": { "showNav", "showLOS", "showSupport" }
  }
}


Notes

Arrays are tight-packed; optional arrays are omitted if empty/zero to reduce bandwidth.

paletteIndex is final (autotile + rotation resolved); renderers never compute connect masks.

4) Cell Schema (Normative, per Z-slice)

For each visible chunk/Z:

tilePaletteIndex : uint16[W×W]
Final sprite index into the global tileset palette. Includes:

Terrain/furniture base frame

Autotile variant from connect mask

Rotation (pre-rotated index)

fluidDepth : uint8[W×W] (0..7)
Used for fluid shading overlay (in SadConsole: foreground color/tint or separate glyph).

fieldGlyphs : sparse
Per tile, a compact list of (fieldId, glyphIdx, intensity) entries (varint-packed).

designations : bitset
L7 gameplay/UI overlays (dig, channel, forbid, stockpile, room id highlights).

tintLight : rgba8[W×W] (optional)
Lighting/time-of-day/tints. When absent, presenter uses defaults.

animPhase : uint8[W×W] (optional)
For tiles with looped animations; phase = tick % periodFrames (deterministic).

5) Draw Order (Normative)

Orthographic buckets (front-to-back) within each tile:

Floors (L0 floor/ramp/stairs bases)

Surface (L1)

Fluids (L3 overlay by fluidDepth)

Constructions/Furniture (L2)

Items (L5 stacks)

Fields (L4 decals/gases/FX)

Units (L6 billboards)

UI overlays (L7 designations, cursor, selection)

Presenter must render in this order to match the simulation’s visual contract.

6) Autotiling & Rotation (Normative)
6.1 Data-Driven Inputs

Each visual prototype (terrain/furniture/road/pipe/rail) declares:

connect_groups: ["wall", "doorframe", "road", ...]

connects_to: ["same", "tags:stone|wood", "id:door_frame", ...]

variants: { maskNESW → paletteIndex }

rotates_to: { "N":idxN, "E":idxE, "S":idxS, "W":idxW } (when rotation is discrete)

6.2 Snapshot-Build Rules

Compute neighbor mask in grid space (NESW; optionally diagonals for roads/rails).

Pick variant from variants using the mask; if multiple, take the highest-priority group.

Compute final rotation: either choose the rotated frame (rotates_to) or bake rotation into the palette index (preferred).

Write the resolved paletteIndex to tilePaletteIndex[]. No autotile data is stored in the world.

7) Delta & Versioning (Normative)

Maintain:

chunk.version — increments when any Z-slice changed.

z.version — increments when cells in that Z changed.

The builder emits delta lists per frame:

changedChunks[], and for each, changedZ[] + byte ranges dirty within each cell array (row spans).

Presenter applies only the changed chunks/slices/rows; no full-surface clears.

8) Visibility & Culling (Normative)

Perform viewport culling by chunk and Z before sending arrays to presenter.

Maintain a visibility/LOS cache per camera; invalidate on topology (L0/L2) changes.

Optional fog-of-war bitset per Z; presenter darkens unseen tiles but must not hide gameplay overlays unless configured.

9) SadConsole Presenter Contract (Normative)

Create a cell surface sized to the viewport; one cell per tile.

For each cell:

Map tilePaletteIndex → glyph index / tile image in the atlas.

Apply tintLight and fluidDepth overlay (depth-to-color LUT is data-driven).

Draw fieldGlyphs as additional passes (minimal overdraw).

Draw billboards (items/units) after tile layers, respecting zOrderBias.

Draw designations/cursor/selection last.

Batching: group by atlas/material; avoid per-cell API calls (use spans/bulk updates).

No logic: Presenter does not read pathing/support/physics; it renders what snapshot says.

10) Memory & Sizing (Informative, targets)

For W=32 (1024 tiles / Z):

tilePaletteIndex: 2 KB / Z

fluidDepth: 1 KB / Z

tintLight: 4 KB / Z (optional)

Typical Z slice: ~7 KB (without fieldGlyphs/billboards).

One chunk with Zc=8: ~56 KB base visuals. Dirtied rows reduce transfer further.

11) Animation & Timing (Normative)

Tile animations use tick-based phases (no wall-clock drift).

animPhase = tick % period; variant selection is deterministic.

Billboard animations (units/items) follow their own tick-based state and write final paletteIndex each frame.

12) Input → Command Bus (Isolation)

User input (mouse/keyboard) never calls sim directly.

Presenter emits commands/events → simulation command queue (processed at ApplyCommands).

This preserves replayability and avoids sim/render coupling.

13) Content & Schemas (Normative)

Tileset palette and autotile/rotation tables are registry-driven (/content/*.json):

tileset.schema.json: palette pages, sprite indices, frame ranges.

autotile.schema.json: connect groups, masks, variant map, rotation map.

fluid_lut.schema.json: depth → color/tint.

Boot-time validation is strict; version IDs are embedded in the snapshot (paletteVersion).

14) Failure Safety (Normative)

Snapshot builder runs under boundary try–catch:

On error: mark the offending chunk/Z as unchanged for this frame, emit a structured error event, and continue.

Never break the main loop due to rendering data issues.

15) Testing & CI Gates (Normative)

Determinism harness: same seed+inputs ⇒ identical snapshot hashes.

Delta integrity: applying deltas to last frame reproduces the full rebuilt snapshot.

Bounds tests: no out-of-range palette indices; mask/rotation tables fully covered.

Performance budgets: snapshot build ≤ 25% frame on mid-spec; presenter ≤ 25%.

16) Checklists (Drop-in)
16.1 Builder Checklist

 Use authoritative layers only; no side effects.

 Resolve autotile & rotation from registries; write final paletteIndex.

 Emit deltas for changed rows only; bump chunk.version and z.version.

 Apply visibility/LOS/fog correctly; avoid culling gameplay overlays.

16.2 Presenter Checklist (SadConsole)

 Never read live world; consume snapshot only.

 Respect draw order & buckets; batch by atlas.

 Apply fluidDepth LUT and tintLight if present.

 Render billboards after tiles; apply zOrderBias.

 Input goes to command bus, not the sim.

17) Appendix — Minimal LLM Template (for Codex/Claude)
{
  "task": "BuildRenderSnapshot",
  "inputs": ["WorldReader","DerivedCaches","Registries(autotile, rotation, tileset)"],
  "outputs": ["Snapshot{chunks[z]{tilePaletteIndex, fluidDepth, fieldGlyphs, designations, tintLight, animPhase}, billboards}"],
  "rules": [
    "No live-world writes",
    "Resolve autotile/rotation to final paletteIndex",
    "Delta+versioning per chunk and per z",
    "Draw order buckets fixed"
  ]
}
