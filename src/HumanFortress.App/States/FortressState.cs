using System;
using System.Linq;
using SadConsole;
using SadConsole.Input;
using SadRogue.Primitives;
using HumanFortress.Core.World;
using HumanFortress.Simulation.World;
using HumanFortress.Simulation.Rendering;
using HumanFortress.Simulation.Tiles;
using HumanFortress.App.GameStates;
using HumanFortress.WorldGen;
using HumanFortress.Core.Content;

namespace HumanFortress.App.States
{
    public class FortressState : ScreenObject
    {
        public static Point EmbarkLocation { get; set; }
        public static int FortressSize { get; set; }
        
        private ScreenSurface? _mapSurface;
        private SadConsole.Console? _infoPanel;
        private SadConsole.Console? _tileInfoPanel;
        private bool _initialized = false;
        private Point _cameraPos;  // Top-left corner of view
        private Point _cursorPos;   // Cursor position in world coordinates
        private int _currentZ = 25; // Start at surface level
        private World? _world;
        private FortressMap? _fortressMap;
        private RenderSnapshotBuilder? _snapshotBuilder;
        private RenderSnapshot? _currentSnapshot;
        private int _zoomLevel = 1; // 1 = normal, 2 = zoomed in, etc.
        private Point? _lastMousePos;
        
        public FortressState()
        {
            System.Console.WriteLine("[FortressState] Constructor called - deferred initialization");
            // Defer initialization until OnCalculateRenderPosition is called
        }

        public override void Update(TimeSpan delta)
        {
            base.Update(delta);

            if (!_initialized && GameHost.Instance != null)
            {
                Initialize();
            }
        }

        private void Initialize()
        {
            try
            {
                System.Console.WriteLine("[FortressState] Initialize started");

                // Check if GameHost is available
                if (GameHost.Instance == null)
                {
                    System.Console.WriteLine("[FortressState] ERROR: GameHost.Instance is null!");
                    return; // Defer initialization
                }

                System.Console.WriteLine($"[FortressState] GameHost screen size: {GameHost.Instance.ScreenCellsX}x{GameHost.Instance.ScreenCellsY}");

                // Create a root surface
                var rootSurface = new ScreenSurface(GameHost.Instance.ScreenCellsX, GameHost.Instance.ScreenCellsY);
                rootSurface.UseMouse = false;
                rootSurface.UseKeyboard = false;
                System.Console.WriteLine("[FortressState] Root surface created");

                _mapSurface = new ScreenSurface(80, 40);
                _mapSurface.Position = new Point(2, 2);
                _mapSurface.UseMouse = true;  // Enable mouse for hovering
                _mapSurface.UseKeyboard = false;
                System.Console.WriteLine("[FortressState] Map surface created");

                _infoPanel = new SadConsole.Console(35, 20);
                _infoPanel.Position = new Point(84, 2);
                System.Console.WriteLine("[FortressState] Info panel created");

                // Add tile info panel for mouse hover
                _tileInfoPanel = new SadConsole.Console(35, 18);
                _tileInfoPanel.Position = new Point(84, 24);
                System.Console.WriteLine("[FortressState] Tile info panel created");

                // Add to root surface
                rootSurface.Children.Add(_mapSurface);
                rootSurface.Children.Add(_infoPanel);
                rootSurface.Children.Add(_tileInfoPanel);

                // Add root as the only child
                Children.Add(rootSurface);
                System.Console.WriteLine("[FortressState] UI hierarchy established");

                // Make this ScreenObject focusable
                IsFocused = true;
                UseKeyboard = true;
                UseMouse = true;  // Enable mouse for scroll and hover

                System.Console.WriteLine($"[FortressState] FortressSize = {FortressSize}");
                if (FortressSize <= 0)
                {
                    System.Console.WriteLine("[FortressState] WARNING: FortressSize is invalid, using default 2");
                    FortressSize = 2;
                }
                // Start camera and cursor in center of map
                int centerPos = (FortressSize * 32) / 2;
                _cameraPos = new Point(Math.Max(0, centerPos - 40), Math.Max(0, centerPos - 20)); // Center view
                _cursorPos = new Point(centerPos, centerPos); // Center cursor
                System.Console.WriteLine($"[FortressState] Camera position set to {_cameraPos}, cursor at {_cursorPos}");

                GenerateFortressMap();
                DrawUI();

                _initialized = true;
                System.Console.WriteLine("[FortressState] Initialize completed successfully");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[FortressState] ERROR in Initialize: {ex.Message}");
                System.Console.WriteLine($"[FortressState] Stack trace: {ex.StackTrace}");
                throw;
            }
        }
        
        private void GenerateFortressMap()
        {
            try
            {
                System.Console.WriteLine("[GenerateFortressMap] Starting fortress generation");
                System.Console.WriteLine($"[GenerateFortressMap] FortressSize: {FortressSize}, EmbarkLocation: {EmbarkLocation}");

                // Get the world tile data for this location
                if (!WorldMapState.CurrentWorld.Success)
                {
                    System.Console.WriteLine("[GenerateFortressMap] ERROR: CurrentWorld is null");
                    _world = new World(FortressSize, 50);
                    return;
                }

                if (WorldMapState.CurrentWorld.Tiles == null)
                {
                    System.Console.WriteLine("[GenerateFortressMap] WARNING: World tiles are null, using fallback");
                    _world = new World(FortressSize, 50);
                    return;
                }

                System.Console.WriteLine($"[GenerateFortressMap] World tiles dimensions: {WorldMapState.CurrentWorld.Tiles.GetLength(0)}x{WorldMapState.CurrentWorld.Tiles.GetLength(1)}");

                if (EmbarkLocation.X >= WorldMapState.CurrentWorld.Tiles.GetLength(0) ||
                    EmbarkLocation.Y >= WorldMapState.CurrentWorld.Tiles.GetLength(1))
                {
                    System.Console.WriteLine($"[GenerateFortressMap] ERROR: EmbarkLocation {EmbarkLocation} out of bounds");
                    _world = new World(FortressSize, 50);
                    return;
                }

                var worldTile = WorldMapState.CurrentWorld.Tiles[EmbarkLocation.X, EmbarkLocation.Y];
                System.Console.WriteLine($"[GenerateFortressMap] Got world tile at {EmbarkLocation}");

                // Generate fortress using the world tile context
                System.Console.WriteLine("[GenerateFortressMap] Creating FortressGenerator");
                var generator = new FortressGenerator(
                    FortressSize,
                    worldTile,
                    EmbarkLocation,
                    (uint)(EmbarkLocation.X * 1000 + EmbarkLocation.Y)
                );

                System.Console.WriteLine("[GenerateFortressMap] Generating fortress map");
                _fortressMap = generator.Generate();
                System.Console.WriteLine($"[GenerateFortressMap] Fortress map generated: {_fortressMap.Size}x{_fortressMap.Size} chunks");

                // Convert to simulation world
                System.Console.WriteLine("[GenerateFortressMap] Converting to simulation world");
                _world = _fortressMap.ToSimulationWorld();
                System.Console.WriteLine($"[GenerateFortressMap] World created: {_world.SizeInChunks}x{_world.SizeInChunks} chunks");

                // Initialize snapshot builder
                System.Console.WriteLine("[GenerateFortressMap] Creating RenderSnapshotBuilder");
                _snapshotBuilder = new RenderSnapshotBuilder(_world);

                // Build initial snapshot
                System.Console.WriteLine("[GenerateFortressMap] Building initial snapshot");
                BuildSnapshot();

                System.Console.WriteLine($"[GenerateFortressMap] SUCCESS: Generated fortress map: {FortressSize}x{FortressSize} chunks at {EmbarkLocation}");
                System.Console.WriteLine($"[GenerateFortressMap] Biome: {(BiomeType)worldTile.BiomeId}, Elevation: {worldTile.Elevation:F2}");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[GenerateFortressMap] ERROR: {ex.Message}");
                System.Console.WriteLine($"[GenerateFortressMap] Stack trace: {ex.StackTrace}");

                // Create fallback world
                System.Console.WriteLine("[GenerateFortressMap] Creating fallback world");
                _world = new World(FortressSize > 0 ? FortressSize : 2, 50);
            }
        }
        
        private void DrawUI()
        {
            if (_infoPanel == null) return;
            _infoPanel.Clear();
            _infoPanel.Print(0, 0, "=== FORTRESS MODE ===", Color.Yellow);
            _infoPanel.Print(0, 2, $"Cursor: {_cursorPos.X},{_cursorPos.Y}", Color.White);
            _infoPanel.Print(0, 3, $"Size: {FortressSize}x{FortressSize} chunks", Color.White);
            _infoPanel.Print(0, 4, $"Z-Level: {_currentZ}/49", Color.Cyan);
            _infoPanel.Print(0, 5, $"Zoom: {_zoomLevel}x", Color.White);

            _infoPanel.Print(0, 7, "Controls:", Color.Yellow);
            _infoPanel.Print(0, 8, "WASD - Move cursor", Color.Gray);
            _infoPanel.Print(0, 9, "Shift+WASD - Fast move", Color.Gray);
            _infoPanel.Print(0, 10, "Q/E - Change Z-level", Color.Gray);
            _infoPanel.Print(0, 11, "Mouse Wheel - Z-level", Color.Gray);
            _infoPanel.Print(0, 12, "Ctrl+Wheel - Zoom", Color.Gray);
            _infoPanel.Print(0, 13, "ESC - Return to menu", Color.Gray);

            _infoPanel.Print(0, 15, "Status:", Color.Yellow);
            _infoPanel.Print(0, 16, "Simulation: Running", Color.Green);
            _infoPanel.Print(0, 17, "TPS: 50", Color.Green);

            UpdateTileInfo();
            RenderMap();
        }

        private void UpdateTileInfo()
        {
            if (_tileInfoPanel == null || _fortressMap == null) return;
            _tileInfoPanel.Clear();

            _tileInfoPanel.Print(0, 0, "=== TILE INFO ===", Color.Cyan);

            // Get tile info at cursor or mouse position
            Point checkPos = _lastMousePos ?? _cursorPos;

            if (checkPos.X >= 0 && checkPos.X < FortressSize * 32 &&
                checkPos.Y >= 0 && checkPos.Y < FortressSize * 32)
            {
                int chunkX = checkPos.X / 32;
                int chunkY = checkPos.Y / 32;
                int localX = checkPos.X % 32;
                int localY = checkPos.Y % 32;

                var chunk = _fortressMap.GetChunk(chunkX, chunkY);
                var terrain = chunk.GetTerrain(localX, localY, _currentZ);

                _tileInfoPanel.Print(0, 2, $"Position: {checkPos.X},{checkPos.Y}", Color.White);
                _tileInfoPanel.Print(0, 3, $"Chunk: {chunkX},{chunkY}", Color.Gray);
                _tileInfoPanel.Print(0, 4, $"Local: {localX},{localY}", Color.Gray);
                _tileInfoPanel.Print(0, 6, $"Terrain: {terrain}", Color.Green);

                // Add terrain description
                string desc = GetTerrainDescription(terrain);
                _tileInfoPanel.Print(0, 8, "Description:", Color.Yellow);

                // Word wrap description
                var words = desc.Split(' ');
                int line = 9;
                int col = 0;
                foreach (var word in words)
                {
                    if (col + word.Length > 33)
                    {
                        line++;
                        col = 0;
                    }
                    if (line < 17) // Don't overflow panel
                    {
                        _tileInfoPanel.Print(col, line, word + " ", Color.DarkGray);
                        col += word.Length + 1;
                    }
                }
            }
        }

        private string GetTerrainDescription(TerrainType terrain)
        {
            // Get data-driven description from content registry
            var geologyId = TerrainTypeMapper.GetGeologyId(terrain);
            var geology = ContentRegistry.Instance.GetGeology(geologyId);
            var material = geology != null ? ContentRegistry.Instance.GetMaterial(geology.Material) : null;

            if (geology != null && material != null)
            {
                var tags = string.Join(", ", geology.Tags);
                var properties = new List<string>();

                if (geology.Properties.Mineable) properties.Add("mineable");
                if (geology.Properties.Buildable) properties.Add("buildable");
                if (geology.Properties.Smoothable) properties.Add("smoothable");
                if (geology.Properties.Flammable) properties.Add("flammable");

                var durability = material.Struct.Durability;
                var value = material.Valuebaleness;

                return $"{tags}. Durability: {durability:F0}. Value: {value:F1}x. {string.Join(", ", properties)}.";
            }

            return "Unknown terrain type.";
        }
        
        private void RenderMap()
        {
            try
            {
                if (_mapSurface == null) return;
                _mapSurface.Clear();

                if (_fortressMap == null)
                {
                    System.Console.WriteLine("[RenderMap] WARNING: FortressMap is null");
                    return;
                }

            // Calculate view bounds
            int maxWorldSize = FortressSize * 32;

            // Render visible area from fortress map data
            for (int sx = 0; sx < 80; sx++)
            {
                for (int sy = 0; sy < 40; sy++)
                {
                    // Calculate world position based on zoom
                    int worldX = _cameraPos.X + (sx / _zoomLevel);
                    int worldY = _cameraPos.Y + (sy / _zoomLevel);

                    // Check if out of bounds
                    if (worldX < 0 || worldX >= maxWorldSize ||
                        worldY < 0 || worldY >= maxWorldSize)
                    {
                        _mapSurface.SetGlyph(sx, sy, '#', Color.DarkGray);
                        continue;
                    }

                    int currentChunkX = worldX / 32;
                    int currentChunkY = worldY / 32;
                    int localX = worldX % 32;
                    int localY = worldY % 32;

                    // Get terrain from fortress map
                    var chunk = _fortressMap.GetChunk(currentChunkX, currentChunkY);
                    var terrain = chunk.GetTerrain(localX, localY, _currentZ);
                    var (glyph, color) = GetTerrainDisplay(terrain);

                    // Draw terrain or cursor
                    if (worldX == _cursorPos.X && worldY == _cursorPos.Y && _zoomLevel == 1)
                    {
                        // Draw cursor
                        _mapSurface.SetGlyph(sx, sy, 'X', Color.Yellow, Color.DarkGray);
                    }
                    else if (_zoomLevel > 1)
                    {
                        // When zoomed, draw each tile multiple times
                        for (int zx = 0; zx < _zoomLevel && sx + zx < 80; zx++)
                        {
                            for (int zy = 0; zy < _zoomLevel && sy + zy < 40; zy++)
                            {
                                if (worldX == _cursorPos.X && worldY == _cursorPos.Y &&
                                    zx == 0 && zy == 0)
                                {
                                    _mapSurface.SetGlyph(sx + zx, sy + zy, 'X', Color.Yellow, Color.DarkGray);
                                }
                                else
                                {
                                    _mapSurface.SetGlyph(sx + zx, sy + zy, glyph, color);
                                }
                            }
                        }
                    }
                    else
                    {
                        _mapSurface.SetGlyph(sx, sy, glyph, color);
                    }
                }
            }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[RenderMap] ERROR: {ex.Message}");
                System.Console.WriteLine($"[RenderMap] Stack trace: {ex.StackTrace}");
            }
        }

        private (int glyph, Color color) GetTerrainDisplay(TerrainType terrain)
        {
            // Use data-driven display from JSON
            var (g, fg, bg) = TerrainTypeMapper.GetTerrainDisplay(terrain);
            return (g, new Color(fg.R, fg.G, fg.B));
        }

        private void BuildSnapshot()
        {
            try
            {
                System.Console.WriteLine("[BuildSnapshot] Starting snapshot build");

                if (_snapshotBuilder == null)
                {
                    System.Console.WriteLine("[BuildSnapshot] WARNING: SnapshotBuilder is null");
                    return;
                }

                if (_world == null)
                {
                    System.Console.WriteLine("[BuildSnapshot] WARNING: World is null");
                    return;
                }

                var chunkX = _cameraPos.X / 32;
                var chunkY = _cameraPos.Y / 32;
                System.Console.WriteLine($"[BuildSnapshot] Camera chunk: {chunkX},{chunkY} at Z={_currentZ}");

                var camera = new CameraInfo
                {
                    ChunkKey = new ChunkKey(chunkX, chunkY, _currentZ),
                    CenterX = _cameraPos.X % 32,
                    CenterY = _cameraPos.Y % 32,
                    Z = _currentZ,
                    Z0 = _currentZ,
                    ZCount = 1
                };

                var viewport = new ViewportInfo
                {
                    TilesWidth = 80,
                    TilesHeight = 40
                };

                System.Console.WriteLine("[BuildSnapshot] Building snapshot");
                _currentSnapshot = _snapshotBuilder.BuildSnapshot(camera, viewport, 0);
                System.Console.WriteLine($"[BuildSnapshot] Snapshot built with {_currentSnapshot?.Chunks?.Count ?? 0} chunks");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[BuildSnapshot] ERROR: {ex.Message}");
                System.Console.WriteLine($"[BuildSnapshot] Stack trace: {ex.StackTrace}");
            }
        }
        
        public override bool ProcessKeyboard(Keyboard keyboard)
        {
            bool changed = false;
            int maxPos = FortressSize * 32 - 1;
            int moveSpeed = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift) ? 5 : 1;

            // Move cursor with WASD
            if (keyboard.IsKeyPressed(Keys.W))
            {
                _cursorPos = new Point(_cursorPos.X, Math.Max(0, _cursorPos.Y - moveSpeed));
                changed = true;
            }
            else if (keyboard.IsKeyPressed(Keys.S))
            {
                _cursorPos = new Point(_cursorPos.X, Math.Min(maxPos, _cursorPos.Y + moveSpeed));
                changed = true;
            }
            else if (keyboard.IsKeyPressed(Keys.A))
            {
                _cursorPos = new Point(Math.Max(0, _cursorPos.X - moveSpeed), _cursorPos.Y);
                changed = true;
            }
            else if (keyboard.IsKeyPressed(Keys.D))
            {
                _cursorPos = new Point(Math.Min(maxPos, _cursorPos.X + moveSpeed), _cursorPos.Y);
                changed = true;
            }
            else if (keyboard.IsKeyPressed(Keys.Q))
            {
                _currentZ = Math.Max(0, _currentZ - 1);
                changed = true;
            }
            else if (keyboard.IsKeyPressed(Keys.E))
            {
                _currentZ = Math.Min(49, _currentZ + 1);
                changed = true;
            }

            // Update camera to follow cursor if it moved
            if (changed)
            {
                UpdateCameraToFollowCursor();
                BuildSnapshot();
                DrawUI();
            }

            if (keyboard.IsKeyPressed(Keys.Escape))
            {
                GameStateManager.Instance.ChangeState(GameStateType.MainMenu);
            }

            return true;
        }

        public override bool ProcessMouse(MouseScreenObjectState state)
        {
            if (_mapSurface == null || _fortressMap == null) return false;

            bool changed = false;

            // Get mouse position relative to map surface
            var mousePos = state.SurfaceCellPosition;

            // Adjust for map surface position
            mousePos = new Point(mousePos.X - _mapSurface.Position.X, mousePos.Y - _mapSurface.Position.Y);

            // Check if mouse is over the map
            if (mousePos.X >= 0 && mousePos.X < 80 && mousePos.Y >= 0 && mousePos.Y < 40)
            {
                // Calculate world position from mouse
                int worldX = _cameraPos.X + (mousePos.X / _zoomLevel);
                int worldY = _cameraPos.Y + (mousePos.Y / _zoomLevel);
                int maxPos = FortressSize * 32 - 1;

                if (worldX >= 0 && worldX <= maxPos && worldY >= 0 && worldY <= maxPos)
                {
                    _lastMousePos = new Point(worldX, worldY);
                    UpdateTileInfo();
                }
            }
            else
            {
                _lastMousePos = null;
            }

            // Handle mouse wheel for Z-level changes or zoom
            if (state.Mouse.ScrollWheelValueChange != 0)
            {
                // Check if Ctrl is held using the Keyboard state
                var keyboard = GameHost.Instance?.Keyboard;
                bool ctrlHeld = keyboard != null &&
                    (keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl));

                if (ctrlHeld)
                {
                    // Ctrl+Scroll for zoom
                    int delta = state.Mouse.ScrollWheelValueChange > 0 ? 1 : -1;
                    _zoomLevel = Math.Max(1, Math.Min(4, _zoomLevel + delta));
                    changed = true;
                }
                else
                {
                    // Regular scroll for Z-level
                    int delta = state.Mouse.ScrollWheelValueChange > 0 ? 1 : -1;
                    _currentZ = Math.Max(0, Math.Min(49, _currentZ + delta));
                    changed = true;
                }
            }

            if (changed)
            {
                UpdateCameraToFollowCursor();
                BuildSnapshot();
                DrawUI();
            }

            return base.ProcessMouse(state);
        }

        private void UpdateCameraToFollowCursor()
        {
            // Keep cursor in view
            int viewWidth = 80 / _zoomLevel;
            int viewHeight = 40 / _zoomLevel;
            int maxCameraPos = FortressSize * 32 - viewWidth;

            // Check if cursor is outside view and adjust camera
            if (_cursorPos.X < _cameraPos.X + 10)
            {
                _cameraPos = new Point(Math.Max(0, _cursorPos.X - 10), _cameraPos.Y);
            }
            else if (_cursorPos.X >= _cameraPos.X + viewWidth - 10)
            {
                _cameraPos = new Point(Math.Min(maxCameraPos, _cursorPos.X - viewWidth + 11), _cameraPos.Y);
            }

            if (_cursorPos.Y < _cameraPos.Y + 5)
            {
                _cameraPos = new Point(_cameraPos.X, Math.Max(0, _cursorPos.Y - 5));
            }
            else if (_cursorPos.Y >= _cameraPos.Y + viewHeight - 5)
            {
                _cameraPos = new Point(_cameraPos.X, Math.Min(maxCameraPos, _cursorPos.Y - viewHeight + 6));
            }
        }
    }
}