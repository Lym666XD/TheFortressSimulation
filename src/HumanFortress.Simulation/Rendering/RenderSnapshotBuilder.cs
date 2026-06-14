using System;
using System.Collections.Generic;
using HumanFortress.Simulation.World;
using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.Placeables;
using HumanFortress.Core.Content.Registry;

namespace HumanFortress.Simulation.Rendering
{
    /// <summary>
    /// Builds immutable render snapshots from world state per RENDERING_SNAPSHOT.md.
    /// </summary>
    public sealed class RenderSnapshotBuilder
    {
        private readonly World.World _world;
        private readonly TileRegistry _tileRegistry;
        private readonly IConstructionCatalog _constructions;
        private readonly Dictionary<(ChunkKey, int), ulong> _zVersions = new();
        
        public RenderSnapshotBuilder(World.World world, IConstructionCatalog constructions)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _constructions = constructions ?? throw new ArgumentNullException(nameof(constructions));
            _tileRegistry = new TileRegistry();
        }
        
        /// <summary>
        /// Build snapshot after simulation commits, per section 2.
        /// </summary>
        public RenderSnapshot BuildSnapshot(CameraInfo camera, ViewportInfo viewport, ulong tick)
        {
            var snapshot = new RenderSnapshot
            {
                Tick = tick,
                World = new WorldInfo
                {
                    Id = "wf-001",
                    Tick = tick,
                    Seed = 0 // TODO: Get from world
                },
                View = new ViewInfo
                {
                    Camera = camera,
                    Viewport = viewport
                },
                Tileset = new TilesetInfo
                {
                    AtlasId = "tileset/base",
                    PaletteVersion = 1,
                    PaletteCount = 4096
                },
                UI = new UIInfo()
            };
            
            // Build visible chunks
            var visibleChunks = GetVisibleChunks(camera, viewport);
            foreach (var chunkKey in visibleChunks)
            {
                var chunk = _world.GetChunk(chunkKey);
                if (chunk != null)
                {
                    var chunkSnapshot = BuildChunkSnapshot(chunk, camera.Z0, camera.ZCount);
                    snapshot.Chunks.Add(chunkSnapshot);
                }
            }
            
            return snapshot;
        }
        
        private ChunkSnapshot BuildChunkSnapshot(Chunk chunk, int z0, int zCount)
        {
            var snapshot = new ChunkSnapshot
            {
                ChunkId = chunk.Key,
                Version = chunk.LastModifiedTick
            };
            
            // Build Z slices
            for (int z = z0; z < z0 + zCount; z++)
            {
                if (z == chunk.Key.Z)
                {
                    var zSlice = BuildZSlice(chunk, z);
                    snapshot.ZSlices.Add(zSlice);
                }
            }
            
            return snapshot;
        }
        
        private ZSliceSnapshot BuildZSlice(Chunk chunk, int z)
        {
            const int size = Chunk.SIZE_XY * Chunk.SIZE_XY;
            var tilePaletteIndex = new ushort[size];
            var fluidDepth = new byte[size];
            var animPhase = new byte[size];
            
            // Convert tiles to palette indices
            for (int y = 0; y < Chunk.SIZE_XY; y++)
            {
                for (int x = 0; x < Chunk.SIZE_XY; x++)
                {
                    int index = y * Chunk.SIZE_XY + x;
                    var tile = chunk.GetTile(x, y);
                    
                    // Resolve visual representation including autotiling
                    tilePaletteIndex[index] = ResolveTilePaletteIndex(tile, chunk, x, y);
                    fluidDepth[index] = tile.FluidDepth;
                    
                    // Animation phase based on tick
                    if (HasAnimation(tile))
                    {
                        animPhase[index] = (byte)(chunk.LastModifiedTick % 16);
                    }
                }
            }
            
            // Update version tracking
            var key = (chunk.Key, z);
            if (!_zVersions.TryGetValue(key, out var lastVersion) || lastVersion != chunk.LastModifiedTick)
            {
                _zVersions[key] = chunk.LastModifiedTick;
            }
            
            var zslice = new ZSliceSnapshot
            {
                ZIndex = z,
                Version = chunk.LastModifiedTick,
                TilePaletteIndex = tilePaletteIndex,
                FluidDepth = fluidDepth,
                AnimPhase = animPhase
            };

            // Build placeables overlay rectangles (absolute world coordinates)
            try
            {
                var pd = chunk.GetPlaceableData();
                if (pd != null)
                {
                    foreach (var p in pd.GetAllOwnedPlaceables())
                    {
                        if (p.Z != z) continue;
                        string defId = p.ConstructionSite != null ? p.ConstructionSite.TargetId : p.DefinitionId;
                        var def = _constructions.GetConstruction(defId);
                        if (def == null) continue;
                        bool isWorkshop = string.Equals(def.Category, "workshop", System.StringComparison.OrdinalIgnoreCase)
                                          || (def.PlaceableProfile.Tags != null && Array.IndexOf(def.PlaceableProfile.Tags, "workshop") >= 0);
                        if (!isWorkshop) continue;
                        var fp = p.Footprint;
                        zslice.PlaceablesOverlay.Add(new OverlayRect
                        {
                            X = p.Position.X,
                            Y = p.Position.Y,
                            W = fp.W,
                            H = fp.D,
                            Z = p.Z,
                            Kind = p.ConstructionSite != null ? "workshop_site" : "workshop",
                            DefId = def.Id
                        });
                    }
                }
            }
            catch { }

            return zslice;
        }
        
        private ushort ResolveTilePaletteIndex(TileBase tile, Chunk chunk, int x, int y)
        {
            // Get base sprite from tile type
            ushort baseIndex = _tileRegistry.GetBasePaletteIndex(tile.GeoMatId, tile.TerrainBits);
            
            // Apply autotiling if needed
            if (_tileRegistry.SupportsAutotiling(tile.GeoMatId))
            {
                byte connectMask = ComputeConnectMask(tile, chunk, x, y);
                baseIndex = _tileRegistry.GetAutotiledIndex(tile.GeoMatId, connectMask, baseIndex);
            }
            
            return baseIndex;
        }
        
        private byte ComputeConnectMask(TileBase tile, Chunk chunk, int x, int y)
        {
            byte mask = 0;
            
            // Check NESW neighbors
            if (y > 0 && ShouldConnect(tile, chunk.GetTile(x, y - 1))) mask |= 0x01; // North
            if (x < Chunk.SIZE_XY - 1 && ShouldConnect(tile, chunk.GetTile(x + 1, y))) mask |= 0x02; // East
            if (y < Chunk.SIZE_XY - 1 && ShouldConnect(tile, chunk.GetTile(x, y + 1))) mask |= 0x04; // South
            if (x > 0 && ShouldConnect(tile, chunk.GetTile(x - 1, y))) mask |= 0x08; // West
            
            return mask;
        }
        
        private bool ShouldConnect(TileBase a, TileBase b)
        {
            // Simple connection logic - tiles of same material connect
            return a.GeoMatId == b.GeoMatId && a.TerrainBits == b.TerrainBits;
        }
        
        private bool HasAnimation(TileBase tile)
        {
            // Check if this tile type has animation
            return _tileRegistry.HasAnimation(tile.GeoMatId);
        }
        
        private List<ChunkKey> GetVisibleChunks(CameraInfo camera, ViewportInfo viewport)
        {
            var visible = new List<ChunkKey>();
            
            // Calculate chunk range from viewport
            int chunksWide = (viewport.TilesWidth / Chunk.SIZE_XY) + 2;
            int chunksHigh = (viewport.TilesHeight / Chunk.SIZE_XY) + 2;
            
            int centerChunkX = camera.ChunkKey.ChunkX;
            int centerChunkY = camera.ChunkKey.ChunkY;
            
            for (int dx = -chunksWide / 2; dx <= chunksWide / 2; dx++)
            {
                for (int dy = -chunksHigh / 2; dy <= chunksHigh / 2; dy++)
                {
                    for (int z = camera.Z0; z < camera.Z0 + camera.ZCount; z++)
                    {
                        visible.Add(new ChunkKey(centerChunkX + dx, centerChunkY + dy, z));
                    }
                }
            }
            
            return visible;
        }
    }
    
    /// <summary>
    /// Registry for tile visual data.
    /// </summary>
    public sealed class TileRegistry
    {
        private readonly Dictionary<ushort, TileVisualData> _visualData = new();
        
        public TileRegistry()
        {
            // Initialize with basic tile types
            RegisterTileVisuals();
        }
        
        private void RegisterTileVisuals()
        {
            // Map terrain types to palette indices
            _visualData[0] = new TileVisualData { BasePaletteIndex = 0, SupportsAutotiling = false }; // Air
            _visualData[1] = new TileVisualData { BasePaletteIndex = 219, SupportsAutotiling = true }; // Stone
            _visualData[2] = new TileVisualData { BasePaletteIndex = 46, SupportsAutotiling = false }; // Grass
            _visualData[3] = new TileVisualData { BasePaletteIndex = 247, SupportsAutotiling = false }; // Sand
            _visualData[4] = new TileVisualData { BasePaletteIndex = 42, SupportsAutotiling = false }; // Snow
            _visualData[5] = new TileVisualData { BasePaletteIndex = 176, SupportsAutotiling = false }; // Mud
            _visualData[6] = new TileVisualData { BasePaletteIndex = 219, SupportsAutotiling = true }; // Rock
            _visualData[7] = new TileVisualData { BasePaletteIndex = 46, SupportsAutotiling = false }; // CavernFloor
            _visualData[8] = new TileVisualData { BasePaletteIndex = 15, SupportsAutotiling = true }; // OreVein
            // Geological strata types
            _visualData[9] = new TileVisualData { BasePaletteIndex = 219, SupportsAutotiling = true }; // Granite
            _visualData[10] = new TileVisualData { BasePaletteIndex = 219, SupportsAutotiling = true }; // Marble
            _visualData[11] = new TileVisualData { BasePaletteIndex = 219, SupportsAutotiling = true }; // Basalt
            _visualData[12] = new TileVisualData { BasePaletteIndex = 219, SupportsAutotiling = true }; // Sandstone
            _visualData[13] = new TileVisualData { BasePaletteIndex = 219, SupportsAutotiling = true }; // Limestone
            _visualData[14] = new TileVisualData { BasePaletteIndex = 219, SupportsAutotiling = true }; // Shale
        }
        
        public ushort GetBasePaletteIndex(ushort geoMatId, ushort terrainBits)
        {
            if (_visualData.TryGetValue(geoMatId, out var data))
            {
                return data.BasePaletteIndex;
            }
            return 63; // Default '?' tile
        }
        
        public bool SupportsAutotiling(ushort geoMatId)
        {
            return _visualData.TryGetValue(geoMatId, out var data) && data.SupportsAutotiling;
        }
        
        public bool HasAnimation(ushort geoMatId)
        {
            return _visualData.TryGetValue(geoMatId, out var data) && data.HasAnimation;
        }
        
        public ushort GetAutotiledIndex(ushort geoMatId, byte connectMask, ushort baseIndex)
        {
            // Simple autotiling - walls use different characters based on connections
            if (geoMatId == 1 || geoMatId == 6) // Stone/Rock walls
            {
                return connectMask switch
                {
                    0b0000 => 219,  // Isolated pillar
                    0b0101 => 196,  // Horizontal line
                    0b1010 => 179,  // Vertical line
                    0b0110 => 218,  // Corner NE
                    0b0011 => 191,  // Corner SE
                    0b1001 => 217,  // Corner SW
                    0b1100 => 192,  // Corner NW
                    0b0111 => 195,  // T-junction E
                    0b1011 => 180,  // T-junction S
                    0b1101 => 194,  // T-junction W
                    0b1110 => 193,  // T-junction N
                    0b1111 => 197,  // Cross
                    _ => 219        // Default
                };
            }
            
            return baseIndex;
        }
        
        private class TileVisualData
        {
            public ushort BasePaletteIndex { get; init; }
            public bool SupportsAutotiling { get; init; }
            public bool HasAnimation { get; init; }
        }
    }
}
