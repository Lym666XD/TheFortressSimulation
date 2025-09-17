using System;
using System.Collections.Generic;
using HumanFortress.Simulation.World;
using HumanFortress.Simulation.Tiles;
using HumanFortress.Core.Content;

namespace HumanFortress.WorldGen
{
    using TerrainKind = HumanFortress.Simulation.Tiles.TerrainKind;
    /// <summary>
    /// Represents the generated fortress map data.
    /// </summary>
    public class FortressMap
    {
        private readonly FortressChunk[,] _chunks;
        private readonly int _size;
        private readonly int _maxZ;
        
        public int Size => _size;
        public int MaxZ => _maxZ;
        
        public FortressMap(int size, int maxZ)
        {
            _size = size;
            _maxZ = maxZ;
            _chunks = new FortressChunk[size, size];
            
            // Initialize chunks
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    _chunks[x, y] = new FortressChunk(x, y, maxZ);
                }
            }
        }
        
        public FortressChunk GetChunk(int x, int y)
        {
            if (x < 0 || x >= _size || y < 0 || y >= _size)
                throw new ArgumentOutOfRangeException($"Chunk coordinates {x},{y} out of range");
            
            return _chunks[x, y];
        }
        
        /// <summary>
        /// Convert to World for simulation.
        /// </summary>
        public World ToSimulationWorld()
        {
            try
            {
                System.Console.WriteLine($"[ToSimulationWorld] Converting fortress map to world: {_size}x{_size} chunks, MaxZ={_maxZ}");
                var world = new World(_size, _maxZ);

                int chunksProcessed = 0;
                int tilesProcessed = 0;

                // Transfer terrain data to simulation world
                for (int cx = 0; cx < _size; cx++)
                {
                    for (int cy = 0; cy < _size; cy++)
                    {
                        var fortressChunk = _chunks[cx, cy];
                        if (fortressChunk == null)
                        {
                            System.Console.WriteLine($"[ToSimulationWorld] WARNING: Null chunk at {cx},{cy}");
                            continue;
                        }

                        // Create chunks for each Z level that has content
                        for (int z = 0; z < _maxZ; z++)
                        {
                            var chunkKey = new ChunkKey(cx, cy, z);
                            var simChunk = world.GetOrCreateChunk(chunkKey);

                            // Copy terrain data
                            for (int lx = 0; lx < 32; lx++)
                            {
                                for (int ly = 0; ly < 32; ly++)
                                {
                                    var terrain = fortressChunk.GetTerrain(lx, ly, z);
                                    var tile = ConvertTerrainToTile(terrain);
                                    simChunk.SetTile(lx, ly, tile, 0);
                                    tilesProcessed++;
                                }
                            }
                        }
                        chunksProcessed++;
                    }
                }

                System.Console.WriteLine($"[ToSimulationWorld] Conversion complete: {chunksProcessed} chunks, {tilesProcessed} tiles processed");
                return world;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[ToSimulationWorld] ERROR: {ex.Message}");
                System.Console.WriteLine($"[ToSimulationWorld] Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        private TileBase ConvertTerrainToTile(TerrainType terrain)
        {
            // Convert terrain types to proper material IDs
            ushort geoMatId = terrain switch
            {
                TerrainType.Air => MaterialIdRegistry.Air,
                TerrainType.Stone => MaterialIdRegistry.GenericStone,
                TerrainType.Rock => MaterialIdRegistry.Granite,
                TerrainType.Grass => MaterialIdRegistry.Grass,
                TerrainType.Sand => MaterialIdRegistry.Sand,
                TerrainType.Snow => MaterialIdRegistry.Snow,
                TerrainType.Mud => MaterialIdRegistry.Mud,
                TerrainType.OreVein => MaterialIdRegistry.IronOre,
                TerrainType.CavernFloor => MaterialIdRegistry.GenericStone,
                TerrainType.Granite => MaterialIdRegistry.Granite,
                TerrainType.Marble => MaterialIdRegistry.Marble,
                TerrainType.Basalt => MaterialIdRegistry.Basalt,
                TerrainType.Sandstone => MaterialIdRegistry.Sandstone,
                TerrainType.Limestone => MaterialIdRegistry.Limestone,
                TerrainType.Shale => MaterialIdRegistry.Shale,
                _ => MaterialIdRegistry.GenericStone
            };
            ushort terrainBits = 0;
            byte surfaceBits = 0;

            // Set terrain kind based on terrain type
            TerrainKind kind;
            switch (terrain)
            {
                case TerrainType.Air:
                    kind = TerrainKind.OpenNoFloor;
                    break;
                case TerrainType.Stone:
                case TerrainType.Rock:
                case TerrainType.OreVein:
                case TerrainType.Granite:
                case TerrainType.Marble:
                case TerrainType.Basalt:
                case TerrainType.Sandstone:
                case TerrainType.Limestone:
                case TerrainType.Shale:
                    kind = TerrainKind.SolidWall;
                    break;
                case TerrainType.Grass:
                    kind = TerrainKind.OpenWithFloor;
                    surfaceBits |= 2; // HasGrass
                    break;
                case TerrainType.Sand:
                case TerrainType.Mud:
                case TerrainType.CavernFloor:
                    kind = TerrainKind.OpenWithFloor;
                    if (terrain == TerrainType.Mud)
                        surfaceBits |= 1; // HasMud
                    break;
                case TerrainType.Snow:
                    kind = TerrainKind.OpenWithFloor;
                    surfaceBits |= 4; // HasSnow
                    break;
                default:
                    kind = TerrainKind.OpenWithFloor;
                    break;
            }

            // Set terrain bits with kind in lower 3 bits
            terrainBits = (ushort)((int)kind & 0x7);

            // Mark natural terrain
            terrainBits |= (1 << 6); // IsNatural

            return new TileBase(
                geoMatId: geoMatId,
                terrainBits: terrainBits,
                surfaceBits: surfaceBits,
                fluidKind: 0,
                fluidDepth: 0,
                metaBits: 0,
                trafficCost: 100
            );
        }
    }
    
    /// <summary>
    /// Represents a single chunk in the fortress map.
    /// </summary>
    public class FortressChunk
    {
        private readonly TerrainType[,,] _terrain;
        private readonly int _x;
        private readonly int _y;
        private readonly int _maxZ;
        
        public int X => _x;
        public int Y => _y;
        
        public FortressChunk(int x, int y, int maxZ)
        {
            _x = x;
            _y = y;
            _maxZ = maxZ;
            _terrain = new TerrainType[32, 32, maxZ];
            
            // Initialize all as stone
            for (int lx = 0; lx < 32; lx++)
            {
                for (int ly = 0; ly < 32; ly++)
                {
                    for (int z = 0; z < maxZ; z++)
                    {
                        _terrain[lx, ly, z] = TerrainType.Stone;
                    }
                }
            }
        }
        
        public void SetTerrain(int x, int y, int z, TerrainType terrain)
        {
            if (x >= 0 && x < 32 && y >= 0 && y < 32 && z >= 0 && z < _maxZ)
            {
                _terrain[x, y, z] = terrain;
            }
        }
        
        public TerrainType GetTerrain(int x, int y, int z)
        {
            if (x >= 0 && x < 32 && y >= 0 && y < 32 && z >= 0 && z < _maxZ)
            {
                return _terrain[x, y, z];
            }
            return TerrainType.Stone;
        }
    }
}