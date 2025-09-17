Authoritative tile spec aligned with our fortress-only, deterministic, multithreaded architecture.
Tiles live in chunks (default 32×32 cells per Z). Simulation runs read-parallel / write-serialized; the renderer consumes an immutable snapshot. This doc locks down types, bit layouts, APIs, invariants, and update rules so codegen tools can implement safely.

0) Terminology & Constants

Cell: a single (x,y) in a chunk layer at Z.

Chunk: fixed grid 32×32 cells per Z; addressed by ChunkKey(cx, cy, z).

LocalIndex: idx = y * 32 + x (0..1023).

Tick: fixed-step sim tick (50 TPS).

Layers: L0..L7 (terrain → meta); see §2.

Tuning constants (from /content/registries/tuning.tile.json):

{
  "chunk_size_xy": 32,
  "fluid_depth_max": 7,
  "nav_cost_base": 10,
  "nav_cost_fluid_shallow": 6,
  "nav_cost_fluid_deep": 18
}

1) Memory Model (Authoritative)
1.1 TileBase (hot base array, AoS packed)
// Size: 10 bytes; CLR commonly pads to 12 or 16; do not rely on sizeof for IO.
// Read-only by default: see §7 for mutation rules.
public readonly struct TileBase
{
    public readonly ushort GeoMatId;     // L0: geology/terrain material (IdMap index)
    public readonly ushort TerrainBits;  // L0: kind/flags (bit layout in §1.3)
    public readonly byte   SurfaceBits;  // L1: small flags (mud, grass, fertility tiers)
    public readonly byte   FluidKind;    // L3: fluid IdMap index (0 = none)
    public readonly byte   FluidDepth;   // L3: 0..7
    public readonly byte   MetaBits;     // L7: revealed/traffic/etc. (§1.3)
    public readonly ushort TrafficCost;  // cached nav cost (SoA alternative in §1.4)
}


One contiguous array per chunk: TileBase[] Tiles = new TileBase[32*32];

Designed for hot reads (nav/LOS/fluids). Write is done by atomic element replace (see §7).

1.2 Sparse Overlays (cold/variable)
// L2 — constructions/furniture
// Each occupied cell holds at most one Blocker and 0..N Passables
public sealed class FurnitureCell
{
    public FurnitureRef? Blocker;          // e.g., built wall, kiln (impassable)
    public List<FurnitureRef>? Passables;  // e.g., tables, chairs, traps (passable)
    public byte ConnectMaskNESW;           // derived for autotile
    public byte OpacityMaskNESW;           // derived for LOS
}

// L4 — fields (gases/decals), multiple per cell
public sealed class FieldCell { public ushort Id; public byte Intensity; public ushort Age; }

// L5 — items (stacks), multiple per cell; stacks pooled for memory
public readonly struct ItemStackRef { public int Handle; /* to item pool */ }


Per chunk:

Dictionary<int, FurnitureCell> Furniture;   // key = LocalIndex
Dictionary<int, List<FieldCell>> Fields;    // key = LocalIndex
Dictionary<int, List<ItemStackRef>> Items;  // key = LocalIndex


Overlays are optional (most cells absent). They change only in Write (§7).

1.3 Bit Layouts (normative)

TerrainBits (ushort)

bits 0..2   TerrainKind (0..7)
bits 3..5   RampDirection (0..7: N,NE,E,SE,S,SW,W,NW) - valid only when TerrainKind=Ramp
bit  6      Natural (1) vs Constructed (0)
bit  7      Smoothed (optional finish state)
bit  8      Engraved (optional finish state)
bits 9..15  Reserved (set 0)


TerrainKind enum (compact):

public enum TerrainKind : byte {
  SolidWall=0,      // Blocks all, provides support
  OpenWithFloor=1,  // Walkable floor, provides support
  OpenNoFloor=2,    // Empty space, flyable only
  Ramp=3,           // Z-transition via RampDirection
  StairsUp=4,       // Z-transition up
  StairsDown=5,     // Z-transition down
  StairsUD=6,       // Z-transition both ways
  Chasm=7           // Bottomless pit, flyable only
}


SurfaceBits (byte)

bit0  Mud
bit1  Grass
bit2  Snow
bit3  Ash
bits4..7 Fertility (0..15)  // coarse tier packed into 4 bits


MetaBits (byte)

bit0 Revealed
bit1 SeenThisSession
bit2 RoofNatural
bit3 RoofBuilt
bits4..5 TrafficMask (00=Normal, 01=Low, 10=High, 11=Restricted)
bit6 UIHighlight (temporary)
bit7 Reserved


Helper (mandatory):

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static byte Set2Bits(byte b, int startBit, byte twoBitValue) {
    b = (byte)(b & ~(0b11 << startBit));
    return (byte)(b | ((twoBitValue & 0b11) << startBit));
}

1.4 Optional SoA Split (future-proof seam)

If profiling shows heavy write amplification, we may split hot fields into separate arrays:

GeoMatId[]      : ushort[1024]
TerrainBits[]   : ushort[1024]
SurfaceBits[]   : byte[1024]
FluidKind[]     : byte[1024]
FluidDepth[]    : byte[1024]
MetaBits[]      : byte[1024]
TrafficCost[]   : ushort[1024]


APIs remain the same via TileReader/TileMutator facades (see §6/§7).

2) Layer Responsibilities (L0..L7)

L0 Terrain — authoritative topology

Standability, support, fall, ramps, stairs.

Writes: dig, channel, smooth, build floor, build wall.

L1 Surface — skin

Soil/grass/snow/ash; fertility influences farming/footprints; no blocking.

L2 Constructions/Furniture — occupancy & connect-groups

Blocker (impassable) + Passables (tables/rails/traps).

Contributes to support/opacity/autotile.

L3 Fluids — single dominant fluid per cell

Kind (0=none), depth 0..7; shallow increases move cost, deep blocks.

L4 Fields — gases/decals

Multiple entries (id, intensity, age); LOS & damage modifiers.

L5 Items — stacks

Multiple stacks, pooled; do not block unless prototype flag.

L6 Units/Vehicles — occupiers

Runtime occupancy; soft/hard block per creature size.

L7 Meta — designations, traffic, room id, revealed

Non-visual sim/UI metadata.

Query precedence (nav/LOS): L0/L2 → L3 → L4 → L5/L6.

3) Derived Caches & Versions

Per chunk:

NavMask[idx]: byte — bitfield: BlockMove, RampUp, RampDown, StairUp, StairDown, DiagOK, etc.

NavCost[idx]: ushort — base + fluid/surface modifiers (mirrors TrafficCost if SoA).

OpacMask[idx]: byte — per-edge opacity (NESW).

SupportMask[idx]: byte — neighboring supports.

ConnectivityVersion: uint — increment when topology relevant data changes.

Invalidation radius (apply to cell + neighbors as listed):

Change	Invalidate
L0 kind/flags	idx + NESW + above/below (ramps/stairs)
L2 blocker/passables	idx + NESW
L3 fluid kind/depth	idx
L4 field add/remove	idx
L7 traffic/revealed	idx
4) Autotiling & Rotation (Deterministic)

Connectivity groups are declared on prototypes (JSON):
connect_groups: ["wall_stone"], connects_to: ["wall_stone","wall_wood"]

Mask encoding (NESW, 4 bits):

bit0: North, bit1: East, bit2: South, bit3: West  => mask 0..15


Variant table (example):

Mask	Variant
0	Solo
1,2,4,8	End (N/E/S/W)
3,6,9,12	Corner (NE/ES/SW/WN)
5,10	Straight (NS/EW)
7,11,13,14	Tee (missing W/N/E/S respectively)
15	Cross

Rotation: prototypes may specify a rotates_to chain (N→E→S→W). For symmetric tiles, provide identity chain.

Resolution order (stable):

Sample neighborhood after Write-commit (so reads are consistent).

Compute mask from L2 blocker first; if none, from L0 wall/floor where applicable.

Look up variant deterministically; then apply rotation (if any).

Emit TilePaletteIndex for snapshot.

5) Snapshot (Read-Only DTOs)

Per visible Z, per chunk:

public sealed class ChunkSnapshot
{
    public ChunkKey Key;
    public ushort Version;            // increments when snapshot changed
    public ushort[] TilePaletteIndex; // 32*32 resolved glyphs/tiles
    public byte[]   FluidDepth;       // 32*32
    public byte[]   FieldGlyph;       // 32*32 (dominant field visual)
    public byte[]   Designation;      // 32*32 (optional overlay index)
    public List<Billboard> Billboards; // items/units rendered as sprites/glyphs
}


Build policy:

Build/refresh only dirty chunks (§3 invalidations).

Stable draw order: Floor → Surface → Fluids → Constructions/Furniture → Items → Fields → Units → UI.

6) Read-Phase API (Thread-Safe, No Writes)
public interface ITileQuery
{
    // Single cell
    TileBase GetBase(ChunkKey ck, int idx);              // copy
    FurnitureCell? TryGetFurniture(ChunkKey ck, int idx);
    IReadOnlyList<FieldCell>? TryGetFields(ChunkKey ck, int idx);
    IReadOnlyList<ItemStackRef>? TryGetItems(ChunkKey ck, int idx);

    // Derived
    byte GetNavMask(ChunkKey ck, int idx);
    ushort GetNavCost(ChunkKey ck, int idx);
    byte GetOpacMask(ChunkKey ck, int idx);
    byte GetSupportMask(ChunkKey ck, int idx);

    // Neighborhood helpers
    void GetNeighbors4(ChunkKey ck, int idx, Span<int> outIdx4); // fills NESW local indices or -1
}


Must be safe to call from multiple worker threads.

Returns copies or read-only collections; no mutation.

7) Write-Phase API (Single Writer per Chunk)

Write happens only inside the serialized Write phase of UPDATE_ORDER. There is exactly one writer per chunk (Diff-Log reducer or Chunk-Actor). All writes are atomic element replacements to the base array; overlays are mutated with local locks (not contended because single writer per chunk).

public interface ITileWriteContext
{
    // Replace-base helpers (copy-update)
    void SetTerrain(ChunkKey ck, int idx, ushort geoMatId, ushort terrainBits);
    void SetSurface(ChunkKey ck, int idx, byte surfaceBits);
    void SetFluid(ChunkKey ck, int idx, byte fluidKind, byte fluidDepth);
    void SetMetaBits(ChunkKey ck, int idx, byte metaBits);
    void SetTrafficCost(ChunkKey ck, int idx, ushort cost);

    // Overlay mutators
    ref FurnitureCell EnsureFurniture(ChunkKey ck, int idx); // returns ref for in-place edits
    void RemoveFurniture(ChunkKey ck, int idx);
    List<FieldCell> GetOrCreateFields(ChunkKey ck, int idx);
    List<ItemStackRef> GetOrCreateItems(ChunkKey ck, int idx);

    // Dirty propagation
    void InvalidateNav(ChunkKey ck, int idx, bool neighbors4 = true, bool zNeighbors = false);
    void InvalidateOpac(ChunkKey ck, int idx, bool neighbors4 = true);
    void InvalidateSupport(ChunkKey ck, int idx, bool neighbors4 = true, bool zNeighbors = false);
}


Copy-update pattern (normative):

[MethodImpl(MethodImplOptions.AggressiveInlining)]
static TileBase WithFluid(in TileBase t, byte kind, byte depth)
    => new TileBase(t.GeoMatId, t.TerrainBits, t.SurfaceBits, kind, depth, t.MetaBits, t.TrafficCost);

// Usage in Write phase:
var old = q.GetBase(ck, idx);
var nw  = WithFluid(in old, newKind, newDepth);
ctx.SetFluid(ck, idx, nw.FluidKind, nw.FluidDepth);
ctx.InvalidateNav(ck, idx); // if depth affects cost


Mutators must call appropriate invalidations (§3). The reducer/actor enforces this in debug builds.

8) Update Order Hooks (Where tiles change)

Within Write:

Apply designations/commands → L0/L2/L7 edits (dig/build/traffic).

Rebuild derived caches for invalidated cells (nav/opac/support).

Support/Collapse: evaluate cells with SupportFlag=1 lacking supports; resolve collapses (L0/L2 edits; drop items to L5).

Fluids step (L3): quantized, deterministic; update depth/kind and nav cost.

Fields step (L4): intensities age/decay; interactions (ignite/clear).

Vegetation/weather (L1/L4): skin flags; field creation/removal.

Snapshot build for dirty chunks.

Each sub-step has a budget (cells/tick) and processes in stable order (ChunkKey ascending, then LocalIndex ascending).

9) Nav/LOS/Support Semantics (Precise)

Standable if:

L0 is OpenWithFloor or Stairs*, and no L2 Blocker marked BlocksMove, and (FluidDepth <= shallow threshold or unit can swim).

Passable if:

Not a SolidWall, not Chasm, not L2.Blocker.BlocksMove, and fluid rules allow.

Support:

A cell with SupportFlag=1 must have: (a) L0 floor beneath (z-1) or (b) L2 support component beneath or (c) support from stairs/columns. Else it collapses:

L0 becomes OpenNoFloor or debris per rule; L2 removals propagate; L5 items drop.

LOS:

Blocked if OpacMaskNESW is set on any edge traversed; fields (smoke) may add soft opacity.

TrafficCost:

Base (10) + FluidCost + TrafficMaskAdj (+2 high, -2 low, +8 restricted) + SurfaceAdj. Capped to ushort. NavCost is recomputed on invalidation.

10) Fluids (L3) — Minimal Deterministic Step

Single kind per cell; depth 0..7.

Sources/sinks handled at system level; tile step is capacity-limited greedy push to lower “potential” neighbors in NESW order (stable):

Potential = depth + source_bias - sink_bias.

Move quanta = min(available, neighborCapacity, quantumMl) (quantized).

Iterations per tick: I_L0 (active chunks), I_L1 (reduced), none at L2+ (freeze or optional equilibrium clamp).

No oscillation by using monotonic transfers and quantization (e.g., 1 unit = 5 ml).

11) Fields (L4) — Minimal Deterministic Step

Each tick: Intensity -= decay (clamped), Age++.

Interactions (very limited v1):

If Field=Flame and tile has Flammable material → set Ash on SurfaceBits and create Smoke field with intensity X.

If Water depth >0 and Flame present → extinguish flame, reduce water depth by 1.

All loops in stable order (idx ascending), budgeted.

12) Serialization (Chunk Tiles)

Base array: write 1024 entries in row-major. Use MessagePack for struct tuples or custom packed writer.

Overlays: write counts first, then (idx, payload…) pairs.

Compression: zstd at the chunk-file level.

Determinism: encode maps in sorted order; do not depend on CLR hash order.

13) Testing & Invariants

Unit tests (must pass):

Terrain bit ops: round-trips & masks.

MetaBits: set/clear/read; 2-bit Traffic writes.

Autotile: known neighborhoods → expected variants (goldens).

Cache invalidation: L0/L2 edits change NavMask/OpacMask predictably.

Fluids: bucket tests (pour 8 units → expected distribution).

Fields: decay & interactions deterministic.

Snapshot: same inputs → same TilePaletteIndex & hashes.

Property tests:

Random dig/build sequences never produce illegal states (e.g., standable+chasm).

Support: removing a support eventually collapses dependent constructions, leaving no “floating blockers”.

14) Example Workflows

Dig a wall (L0 → OpenWithFloor)

var t = q.GetBase(ck, idx);
ushort bits = t.TerrainBits;
bits = (ushort)((bits & ~0b111) | (ushort)TerrainKind.OpenWithFloor); // set kind
ctx.SetTerrain(ck, idx, t.GeoMatId, bits);
ctx.InvalidateNav(ck, idx, neighbors4:true);
ctx.InvalidateOpac(ck, idx, neighbors4:true);
ctx.InvalidateSupport(ck, idx, neighbors4:true, zNeighbors:true);


Place a built wall (L2 Blocker)

ref var f = ref ctx.EnsureFurniture(ck, idx);
f.Blocker = new FurnitureRef { Id=BuiltWallId, Rot=rot, BlocksMove=true, Opaque=true, ConnectGroup=CG_WallStone };
ctx.InvalidateNav(ck, idx, neighbors4:true);
ctx.InvalidateOpac(ck, idx, neighbors4:true);


Set traffic to Restricted

var t = q.GetBase(ck, idx);
var meta = t.MetaBits;
meta = Set2Bits(meta, 4, 0b11); // bits 4..5
ctx.SetMetaBits(ck, idx, meta);
ctx.SetTrafficCost(ck, idx, (ushort)(t.TrafficCost + 8));

15) Performance Guidance

Favor sequential writes (tiles[idx] = new TileBase(...)) over field-by-field mutations.

Keep overlays absent by default; allocate lists lazily and pool them.

Build snapshots only for dirty chunks.

Avoid LINQ/allocations in inner loops; pre-size lists.

16) Safety & Determinism Rules (Hard)

Read phase: no writes; only ITileQuery.

Write phase: single writer per chunk; only ITileWriteContext.

All iteration orders are fixed: (ChunkKey asc) → (LocalIndex asc).

No wall-clock or OS timers; use tick count only.

Catch and quarantine any exception at system/chunk boundary; never crash the loop.

17) Extension Points (Reserved)

MetaBits2 (byte) if flags run out.

Multi-fluid mixing table (off by default).

Heat/temperature coupling (v2).

Diagonal nav policies (bit in NavMask).

18) Minimal Implementations (Signatures to Generate)
public sealed class Chunk
{
    public readonly ChunkKey Key;
    public TileBase[] Tiles = new TileBase[32*32];
    public Dictionary<int, FurnitureCell> Furniture = new();
    public Dictionary<int, List<FieldCell>> Fields = new();
    public Dictionary<int, List<ItemStackRef>> Items = new();

    // Derived
    public byte[] NavMask = new byte[32*32];
    public ushort[] NavCost = new ushort[32*32];
    public byte[] OpacMask = new byte[32*32];
    public byte[] SupportMask = new byte[32*32];

    public uint ConnectivityVersion;
    public ushort SnapshotVersion;
}

public readonly record struct ChunkKey(int Cx, int Cy, int Z);

19) Codegen Notes for Codex/Claude

Prefer generating pure functions for bit operations, autotile masks, and cache updates.

Expose facades TileReader (wraps ITileQuery) and TileMutator (wraps ITileWriteContext) to keep call sites clean.

Generate tests from the examples in §14; use a fixed RNG seed.

Avoid reflection/attributes in inner loops; no dynamic allocations in hot paths.