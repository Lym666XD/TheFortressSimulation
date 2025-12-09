using System;
using System.Collections.Generic;
using HumanFortress.Simulation.World;

namespace HumanFortress.Simulation.Rendering
{
    /// <summary>
    /// Immutable per-tick snapshot for rendering per RENDERING_SNAPSHOT.md.
    /// </summary>
    public sealed class RenderSnapshot
    {
        public WorldInfo World { get; init; }
        public ViewInfo View { get; init; }
        public TilesetInfo Tileset { get; init; }
        public List<ChunkSnapshot> Chunks { get; init; }
        public UIInfo UI { get; init; }
        public ulong Tick { get; init; }
        
        public RenderSnapshot()
        {
            World = new WorldInfo();
            View = new ViewInfo();
            Tileset = new TilesetInfo();
            Chunks = new List<ChunkSnapshot>();
            UI = new UIInfo();
        }
    }
    
    public sealed class WorldInfo
    {
        public string Id { get; init; } = "";
        public ulong Tick { get; init; }
        public uint Seed { get; init; }
    }
    
    public sealed class ViewInfo
    {
        public CameraInfo Camera { get; init; } = new();
        public ViewportInfo Viewport { get; init; } = new();
    }
    
    public sealed class CameraInfo
    {
        public ChunkKey ChunkKey { get; init; }
        public int CenterX { get; init; }
        public int CenterY { get; init; }
        public int Z { get; init; }
        public int Z0 { get; init; }
        public int ZCount { get; init; } = 1;
    }
    
    public sealed class ViewportInfo
    {
        public int TilesWidth { get; init; }
        public int TilesHeight { get; init; }
    }
    
    public sealed class TilesetInfo
    {
        public string AtlasId { get; init; } = "tileset/base";
        public int PaletteVersion { get; init; }
        public int PaletteCount { get; init; } = 4096;
    }
    
    public sealed class ChunkSnapshot
    {
        public ChunkKey ChunkId { get; init; }
        public ulong Version { get; init; }
        public List<ZSliceSnapshot> ZSlices { get; init; } = new();
        public List<Billboard> Billboards { get; init; } = new();
    }
    
    public sealed class ZSliceSnapshot
    {
        public int ZIndex { get; init; }
        public ulong Version { get; init; }
        public ushort[] TilePaletteIndex { get; init; } = Array.Empty<ushort>();
        public byte[] FluidDepth { get; init; } = Array.Empty<byte>();
        public byte[] AnimPhase { get; init; } = Array.Empty<byte>();
        public uint[] TintLight { get; init; } = Array.Empty<uint>();
        public BitArray32[] Designations { get; init; } = Array.Empty<BitArray32>();
        // Placeables overlay rectangles (world coordinates), lightweight UI-friendly layer
        public List<OverlayRect> PlaceablesOverlay { get; init; } = new();
    }
    
    public sealed class Billboard
    {
        public string Id { get; init; } = "";
        public int TileX { get; init; }
        public int TileY { get; init; }
        public int TileZ { get; init; }
        public ushort PaletteIndex { get; init; }
        public string Layer { get; init; } = "Units";
        public bool FlipX { get; init; }
        public int ScreenOffsetX { get; init; }
        public int ScreenOffsetY { get; init; }
        public int ZOrderBias { get; init; }
    }
    
    public sealed class UIInfo
    {
        public CursorInfo? Cursor { get; init; }
        public List<SelectionRange> Selection { get; init; } = new();
        public DebugInfo Debug { get; init; } = new();
    }

    public sealed class OverlayRect
    {
        public int X { get; init; }
        public int Y { get; init; }
        public int W { get; init; }
        public int H { get; init; }
        public int Z { get; init; }
        public string Kind { get; init; } = "workshop"; // e.g., "workshop" or "workshop_site"
        public string DefId { get; init; } = string.Empty; // construction id
    }
    
    public sealed class CursorInfo
    {
        public int TileX { get; init; }
        public int TileY { get; init; }
        public int TileZ { get; init; }
    }
    
    public sealed class SelectionRange
    {
        public int MinX { get; init; }
        public int MinY { get; init; }
        public int MinZ { get; init; }
        public int MaxX { get; init; }
        public int MaxY { get; init; }
        public int MaxZ { get; init; }
    }
    
    public sealed class DebugInfo
    {
        public bool ShowNav { get; init; }
        public bool ShowLOS { get; init; }
        public bool ShowSupport { get; init; }
    }
    
    public struct BitArray32
    {
        public uint Bits;
        
        public bool this[int index]
        {
            get => (Bits & (1u << index)) != 0;
            set
            {
                if (value)
                    Bits |= 1u << index;
                else
                    Bits &= ~(1u << index);
            }
        }
    }
}
