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
using HumanFortress.App.Rendering;
using HumanFortress.App.UI;
using HumanFortress.Navigation;
using HumanFortress.Simulation.Stockpile;
using ChunkKey = HumanFortress.Simulation.World.ChunkKey;

namespace HumanFortress.App.States
{
    public class FortressState : ScreenObject
    {
        public static Point EmbarkLocation { get; set; }
        public static int FortressSize { get; set; }
        
        private MapScreenSurface? _mapSurface;
        private UiOverlaySurface? _uiSurface; // overlay drawn on top of map and panels
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
        private NavigationOverlay? _navOverlay;
        private NavigationManager? _navManager;
        private HumanFortress.Navigation.WorldNavigationView? _navView;
        private UiStore _ui = new UiStore();
        private ulong _uiTick = 0;
        private bool _cameraFollowCursor = false; // camera follows mouse only when true
        private bool _enhancedMouseHooked = false;
        private bool _tilePanelOpen = false;
        private Point _tilePanelWorld = new Point(0,0);
        private int _tilePanelZ = 0;
        private Point? _pathStart = null;
        private int _pathStartZ = 0;
        private StockpileManager? _stockpileManager;
        private StockpileUI? _stockpileUI;
        private OrdersUI? _ordersUI;
        private ZonesUI? _zonesUI;
        private BuildUI? _buildUI;
        private StockpileQuickUI? _stockpileQuickUI;
        private HumanFortress.App.Input.InputBindingsService _bindings = HumanFortress.App.Input.InputBindingsService.Instance;
        private HumanFortress.App.Input.OrdersRegistryService _ordersRegistry = HumanFortress.App.Input.OrdersRegistryService.Instance;

        public FortressState()
        {
            System.Console.WriteLine("[FortressState] Constructor called - deferred initialization");
            // Defer initialization until OnCalculateRenderPosition is called
        }

        public override void OnFocused()
        {
            base.OnFocused();
            Logger.Log("[FOCUS] FortressState focused");
        }

        public override void OnFocusLost()
        {
            base.OnFocusLost();
            Logger.Log("[FOCUS] FortressState lost focus -> reclaim");
            IsFocused = true; // reclaim focus immediately
        }

        public override void Update(TimeSpan delta)
        {
            base.Update(delta);
            _uiTick++;

            // Keep keyboard focus on this state so keyboard input remains active after mouse clicks
            if (!IsFocused)
                IsFocused = true;

            if (!_initialized && GameHost.Instance != null)
            {
                Initialize();
            }

            // Wheel zoom/Z-level handling here to ensure it works regardless of which child captures mouse
            try
            {
                var keyboard = GameHost.Instance?.Keyboard;
                var mouse = GameHost.Instance?.Mouse;
                if (mouse != null && mouse.ScrollWheelValueChange != 0)
                {
                    bool ctrlHeld = keyboard != null && (keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl));
                    int deltaWheel = mouse.ScrollWheelValueChange > 0 ? 1 : -1;
                    if (ctrlHeld)
                    {
                        _zoomLevel = Math.Max(1, Math.Min(4, _zoomLevel + deltaWheel));
                        Logger.Log($"[ZOOM-UPDATE] delta={deltaWheel} -> zoom={_zoomLevel}");
                    }
                    else
                    {
                        // Reverse scroll axis for Z-level
                        _currentZ = Math.Max(0, Math.Min(49, _currentZ - deltaWheel));
                        Logger.Log($"[ZLEVEL-UPDATE] delta={deltaWheel} -> Z={_currentZ}");
                    }
                }
            }
            catch { /* fallback if host mouse API differs */ }

            // Always render UI overlays; keep this state focused for keyboard
            if (!IsFocused) IsFocused = true; // keep keyboard input active after mouse clicks
            DrawUI();
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
                // Allow mouse to bubble to children; keyboard handled by FortressState
                rootSurface.UseMouse = true;
                rootSurface.UseKeyboard = false;
                System.Console.WriteLine("[FortressState] Root surface created");

                // Map surface sized to fit within screen, leaving 2-cell margins
                int screenW = GameHost.Instance.ScreenCellsX;
                int screenH = GameHost.Instance.ScreenCellsY;
                int mapW = Math.Max(20, screenW - 4); // use nearly full width
                int mapH = Math.Max(8, screenH - 4); // leave 2 cells top/bottom for UI
                _mapSurface = new MapScreenSurface(mapW, mapH);
                _mapSurface.Position = new Point(2, 2);
                _mapSurface.UseMouse = true;  // Enable mouse for hovering
                _mapSurface.UseKeyboard = false;
                _mapSurface.MouseMovedLocal += OnMapMouseMovedLocal;
                _mapSurface.LeftClickedLocal += OnMapLeftClickedLocal;
                _enhancedMouseHooked = true;
                System.Console.WriteLine("[FortressState] Map surface created");
                Logger.Log($"[INIT] MapSurface size={_mapSurface.Surface.Width}x{_mapSurface.Surface.Height} pos={_mapSurface.Position}");

                _infoPanel = new SadConsole.Console(35, 20);
                _infoPanel.Position = new Point(84, 2);
                _infoPanel.UseKeyboard = false;
                _infoPanel.FocusOnMouseClick = false;
                System.Console.WriteLine("[FortressState] Info panel created");

                // Add tile info panel for mouse hover
                _tileInfoPanel = new SadConsole.Console(35, 18);
                int tileInfoY = Math.Max(2, screenH - _tileInfoPanel.Height - 2);
                _tileInfoPanel.Position = new Point(84, tileInfoY);
                _tileInfoPanel.UseKeyboard = false;
                _tileInfoPanel.FocusOnMouseClick = false;
                System.Console.WriteLine("[FortressState] Tile info panel created");

                // Create UI overlay surface (full screen) drawn last (on top)
                _uiSurface = new UiOverlaySurface(GameHost.Instance.ScreenCellsX, GameHost.Instance.ScreenCellsY);
                // Overlay actively handles clicks for dock/quick/debug
                _uiSurface.UseMouse = true;
                _uiSurface.UseKeyboard = true; // Enable keyboard for InputHandlerComponent
                _uiSurface.FocusOnMouseClick = false; // don't steal focus

                // NEW: Use InputHandlerComponent for all UI input (replaces old click handlers)
                var uiStateManager = new UI.UIStateManager(_ui);
                var inputHandler = new UI.Components.InputHandlerComponent(
                    uiStateManager,
                    GameHost.Instance.ScreenCellsX,
                    GameHost.Instance.ScreenCellsY
                );
                _uiSurface.SadComponents.Add(inputHandler);
                Logger.Log($"[INIT] Added InputHandlerComponent to UiOverlay");

                // Keep legacy handlers for map clicks (InputHandlerComponent handles UI buttons, then returns false)
                _uiSurface.LeftClickedLocal += OnOverlayLeftClickedLocal;
                _uiSurface.RightClickedLocal += OnOverlayRightClickedLocal;
                _uiSurface.MouseMovedLocal += OnOverlayMouseMovedLocal; // Still needed for hover
                Logger.Log($"[INIT] UiOverlay size={_uiSurface.Surface.Width}x{_uiSurface.Surface.Height}");
                // Add to root surface
                rootSurface.Children.Add(_mapSurface);
                // Do not add info/tile panels as static right-side consoles; we'll draw a floating
                // tile popup on the overlay when requested to maximize map area.
                rootSurface.Children.Add(_uiSurface); // on top

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
                    _world = GameStateManager.Instance.World;
                    return;
                }

                if (WorldMapState.CurrentWorld.Tiles == null)
                {
                    System.Console.WriteLine("[GenerateFortressMap] WARNING: World tiles are null, using fallback");
                    _world = GameStateManager.Instance.World;
                    return;
                }

                System.Console.WriteLine($"[GenerateFortressMap] World tiles dimensions: {WorldMapState.CurrentWorld.Tiles.GetLength(0)}x{WorldMapState.CurrentWorld.Tiles.GetLength(1)}");

                if (EmbarkLocation.X >= WorldMapState.CurrentWorld.Tiles.GetLength(0) ||
                    EmbarkLocation.Y >= WorldMapState.CurrentWorld.Tiles.GetLength(1))
                {
                    System.Console.WriteLine($"[GenerateFortressMap] ERROR: EmbarkLocation {EmbarkLocation} out of bounds");
                    _world = GameStateManager.Instance.World;
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

                // Use GameStateManager's World and fill it with terrain data
                System.Console.WriteLine("[GenerateFortressMap] Getting World from GameStateManager");
                _world = GameStateManager.Instance.World;

                if (_world == null)
                {
                    System.Console.WriteLine("[GenerateFortressMap] ERROR: GameStateManager.World is null");
                    return;
                }

                System.Console.WriteLine($"[GenerateFortressMap] World obtained: {_world.SizeInChunks}x{_world.SizeInChunks} chunks");
                System.Console.WriteLine($"[GenerateFortressMap] Creature definitions loaded: {_world.Creatures.DefinitionCount}");
                System.Console.WriteLine($"[GenerateFortressMap] Item definitions loaded: {_world.Items.DefinitionCount}");

                // Fill terrain data into the existing World
                System.Console.WriteLine("[GenerateFortressMap] Filling world with terrain data");
                _fortressMap.FillWorld(_world);
                System.Console.WriteLine($"[GenerateFortressMap] World filled with terrain data");

                // Initialize snapshot builder
                System.Console.WriteLine("[GenerateFortressMap] Creating RenderSnapshotBuilder");
                _snapshotBuilder = new RenderSnapshotBuilder(_world);

                // Initialize navigation manager (shared from GameStateManager)
                System.Console.WriteLine("[GenerateFortressMap] Using shared NavigationManager");
                _navManager = GameStateManager.Instance.NavManager ?? new NavigationManager(_world);
                // Rebuild after world is filled with terrain
                _navManager.RebuildAll();

                // Initialize navigation overlay
                System.Console.WriteLine("[GenerateFortressMap] Creating NavigationOverlay");
                _navOverlay = new NavigationOverlay();
                _navOverlay.SetNavigationManager(_navManager);

                // Initialize stockpile system and UI classes
                System.Console.WriteLine("[GenerateFortressMap] Wiring StockpileManager & UI classes");
                _stockpileManager = _world.Stockpiles;
                _stockpileUI = new StockpileUI(_stockpileManager);
                _ordersUI = new OrdersUI();
                _zonesUI = new ZonesUI();
                _buildUI = new BuildUI();
                _stockpileQuickUI = new StockpileQuickUI();

                // Load input bindings and orders registry (data-driven UI wiring)
                var baseDir = AppContext.BaseDirectory;
                _bindings.Load(baseDir);
                _ordersRegistry.Load(baseDir);

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

                // Use GameStateManager's World even on error (it has loaded definitions)
                System.Console.WriteLine("[GenerateFortressMap] Using GameStateManager.World despite error");
                _world = GameStateManager.Instance.World;
            }
        }
        
        private void DrawUI()
        {
            if (_uiSurface == null || _mapSurface == null) return;
            if (_infoPanel != null) _infoPanel.Clear();
            // Right-side map & status info removed per request; moved into Debug menu.

            // Build tile info on demand; draw as overlay popup
            RenderMap();

            // Render UI overlays on top of map surface
            if (_uiSurface != null)
            {
                _uiSurface.Clear();
                UiRenderer.DrawTopBar(_uiSurface, _uiTick, GameStateManager.Instance.TickScheduler);
                UiRenderer.DrawDockScreen(_uiSurface, _ui, _uiTick); // moved to bottom-most row
                UiRenderer.DrawQuickIconsScreen(_uiSurface, _ui, _uiTick); // one row above bottom
                UiRenderer.DrawDrawer(_uiSurface, _ui, _uiTick, _stockpileManager, _world);
                UiRenderer.DrawQuickMenu(_uiSurface, _ui, _uiTick, _ordersUI, _zonesUI, _buildUI, _stockpileQuickUI);

                // Draw orders & stockpile specific UI
                if (_stockpileUI != null)
                {
                    // Draw placement mode UI
                    if (_ui.Context == UiContext.PlacingTool)
                    {
                        var mouseWorld = _lastMousePos ?? _cursorPos;
                        // Orders haul placement prompt
                        _ordersUI?.DrawPlacementMode(_uiSurface, _ui, mouseWorld);
                        // Stockpile placement prompt
                        _stockpileUI.DrawPlacementMode(_uiSurface, _ui, mouseWorld);

                        // Draw placement preview on map
                        if (_ui.PlaceFirstCorner.HasValue &&
                            (_ui.PlaceMode == PlacementMode.StockpileSecondCorner || _ui.PlaceMode == PlacementMode.HaulSecondCorner))
                        {
                            var viewport = new Rectangle(_cameraPos.X, _cameraPos.Y,
                                _mapSurface.Surface.Width, _mapSurface.Surface.Height);
                            _stockpileUI.RenderPlacementPreview(_mapSurface,
                                _ui.PlaceFirstCorner.Value, mouseWorld, viewport, true);
                        }
                    }

                    // Draw edit popup if open
                    _stockpileUI.DrawEditPopup(_uiSurface);

                    // Render stockpile overlays on map
                    if (_world != null)
                    {
                        var mapViewport = new Rectangle(_cameraPos.X, _cameraPos.Y,
                            _mapSurface.Surface.Width, _mapSurface.Surface.Height);
                        _stockpileUI.RenderOverlay(_mapSurface, _world, _currentZ, mapViewport);
                    }
                }

                // Render zone overlays (only when zone menu is open)
                if (_world != null && _zonesUI != null)
                {
                    var mapViewport = new Rectangle(_cameraPos.X, _cameraPos.Y,
                        _mapSurface.Surface.Width, _mapSurface.Surface.Height);
                    bool showZoneOverlay = _ui.QuickMenu == QuickMenuKind.Zones;

                    _zonesUI.RenderOverlay(_mapSurface, _world, _currentZ, mapViewport, showZoneOverlay);

                    // Draw zone placement preview
                    if (_ui.PlaceMode == PlacementMode.ZoneSecondCorner && _ui.PlaceFirstCorner.HasValue)
                    {
                        var mouseWorld = _lastMousePos ?? _cursorPos;
                        _zonesUI.RenderPlacementPreview(_mapSurface,
                            _ui.PlaceFirstCorner.Value, mouseWorld, mapViewport, true);
                    }
                }

                // Draw zone placement mode prompt
                _zonesUI?.DrawPlacementMode(_uiSurface, _ui, _lastMousePos ?? _cursorPos);

                // Draw zone detail popup
                if (_zonesUI?.IsDetailPopupOpen() == true && _world != null)
                {
                    _zonesUI.DrawDetailPopup(_uiSurface, _world);
                }

                // Controls/help moved to docs; no on-screen help overlay.
                UiRenderer.DrawDebug(_uiSurface, _ui, _cursorPos, _currentZ, _zoomLevel, _cameraPos, FortressSize);
                UiRenderer.DrawDebugUnits(_uiSurface, _ui, _cameraPos.X, _cameraPos.Y, _currentZ);
                UiRenderer.DrawPause(_uiSurface, _ui);
                UiRenderer.DrawToasts(_uiSurface, _ui, _uiTick);

                if (_tilePanelOpen)
                {
                    DrawTilePopup(_uiSurface);
                }
            }
        }

        // Handle overlay local left-clicks for F1–F8 and Z/X/C buttons
        private void OnOverlayLeftClickedLocal(Point local)
        {
            if (_uiSurface == null) return;

            // Dock F1–F8 at bottom-left (bottom row)
            int dockY = _uiSurface.Surface.Height - 1;
            int dockXStart = 1;
            int dockBtnW = 5;
            int dockGap = 1;
            if (local.Y == dockY && local.X >= dockXStart)
            {
                int rel = local.X - dockXStart;
                int slot = rel / (dockBtnW + dockGap);

                // Explicit mapping: slot -> DrawerId (F1-F8 order)
                var slotMap = new DrawerId[]
                {
                    DrawerId.Creature,              // F1 (slot 0)
                    DrawerId.Stock,                 // F2 (slot 1)
                    DrawerId.Work,                  // F3 (slot 2)
                    DrawerId.PlacementManagement,   // F4 (slot 3)
                    DrawerId.Military,              // F5 (slot 4)
                    DrawerId.Country,               // F6 (slot 5)
                    DrawerId.World,                 // F7 (slot 6)
                    DrawerId.Log                    // F8 (slot 7)
                };

                if (slot >= 0 && slot < slotMap.Length)
                {
                    _ui.OpenPanel(slotMap[slot]);
                    Logger.Log($"[CLICK-OVERLAY] Dock slot={slot} -> drawer={_ui.OpenDrawer}");
                    DrawUI();
                    return;
                }
            }

            // Quick ZXCV buttons (bottom row, same as F1-F8, centered)
            int quickY = _uiSurface.Surface.Height - 1; // Same row as F1-F8
            if (local.Y == quickY)
            {
                int center = _uiSurface.Surface.Width / 2;
                int buttonWidth = 5;
                int gap = 2;

                // 4 buttons: Z X C V (same calculation as UiRenderer.DrawQuickIconsScreen)
                int totalWidth = (buttonWidth * 4) + (gap * 3);
                int startX = center - totalWidth / 2;

                // Calculate button positions (exactly matching UiRenderer)
                int xZ = startX;
                int xX = startX + buttonWidth + gap;
                int xC = startX + (buttonWidth + gap) * 2;
                int xV = startX + (buttonWidth + gap) * 3;

                var ranges = new (int start, int end, QuickMenuKind kind)[]
                {
                    (xZ, xZ + buttonWidth - 1, QuickMenuKind.Orders),      // Z
                    (xX, xX + buttonWidth - 1, QuickMenuKind.Zones),       // X
                    (xC, xC + buttonWidth - 1, QuickMenuKind.Build),       // C
                    (xV, xV + buttonWidth - 1, QuickMenuKind.Stockpile),   // V
                };

                foreach (var r in ranges)
                {
                    if (local.X >= r.start && local.X <= r.end)
                    {
                        _ui.OpenQuickMenu(r.kind);
                        Logger.Log($"[CLICK-OVERLAY] Quick kind={r.kind} x=[{r.start},{r.end}] -> qmenu={_ui.QuickMenu}");
                        DrawUI();
                        return;
                    }
                }
            }

                // Debug menu spawn button (panel is centered and semi-transparent)
                if (_ui.DebugOpen)
                {
                int surfW = _uiSurface.Surface.Width;
                int surfH = _uiSurface.Surface.Height;
                int width = Math.Min((int)(surfW * 0.7), surfW - 4);
                int height = Math.Min((int)(surfH * 0.6), surfH - 4);
                int x0 = (surfW - width) / 2;
                int y0 = (surfH - height) / 2;
                int btnX = x0 + 2; int btnY = y0 + 2; int btnW = 22;
                if (local.Y == btnY && local.X >= btnX && local.X < btnX + btnW)
                {
                    Logger.Log($"[DEBUG] Spawn Dwarf click at cursor=({_cursorPos.X},{_cursorPos.Y},{_currentZ}) [overlay]");
                    _ui.AddDebugDwarf(_cursorPos, _currentZ);
                    _ui.AddToast("Spawned dwarf (debug marker)", _uiTick + 100);
                    DrawUI();
                    return;
                }
            }

            // Pass-through: if click not on overlay controls, treat as map click for tile info
            if (_mapSurface != null)
            {
                var mapLocal = new Point(local.X - _mapSurface.Position.X, local.Y - _mapSurface.Position.Y);
                if (mapLocal.X >= 0 && mapLocal.X < _mapSurface.Surface.Width && mapLocal.Y >= 0 && mapLocal.Y < _mapSurface.Surface.Height)
                {
                    OnMapLeftClickedLocal(mapLocal);
                    return;
                }
            }
        }

        private void UpdateTileInfo()
        {
            if (_tileInfoPanel == null || _fortressMap == null || !_tilePanelOpen) return;
            _tileInfoPanel.Clear();

            _tileInfoPanel.Print(0, 0, "=== TILE INFO ===", Color.Cyan);

            // Selected world position
            Point checkPos = _tilePanelWorld;

            if (checkPos.X >= 0 && checkPos.X < FortressSize * 32 &&
                checkPos.Y >= 0 && checkPos.Y < FortressSize * 32)
            {
                int chunkX = checkPos.X / 32;
                int chunkY = checkPos.Y / 32;
                int localX = checkPos.X % 32;
                int localY = checkPos.Y % 32;

                var chunk = _fortressMap.GetChunk(chunkX, chunkY);
                var geologyId = chunk.GetGeologyId(localX, localY, _tilePanelZ);

                _tileInfoPanel.Print(0, 2, $"Position: {checkPos.X},{checkPos.Y}", Color.White);
                _tileInfoPanel.Print(0, 3, $"Chunk: {chunkX},{chunkY}", Color.Gray);
                _tileInfoPanel.Print(0, 4, $"Local: {localX},{localY}", Color.Gray);
                _tileInfoPanel.Print(0, 6, $"Terrain: {geologyId}", Color.Green);

                // Add terrain description
                string desc = GetTerrainDescription(geologyId);
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

        private void HideTilePanel()
        {
            _tilePanelOpen = false;
            _tileInfoPanel?.Clear();
        }

        private string GetTerrainDescription(string geologyId)
        {
            // Get data-driven description from content registry
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
            int viewW = _mapSurface.Surface.Width;
            int viewH = _mapSurface.Surface.Height;
            for (int sx = 0; sx < viewW; sx++)
            {
                for (int sy = 0; sy < viewH; sy++)
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

                    // Get tile from simulation world
                    if (_world != null)
                    {
                        var chunkKey = new HumanFortress.Simulation.World.ChunkKey(currentChunkX, currentChunkY, _currentZ);
                        var simChunk = _world.GetChunk(chunkKey);

                        int glyph;
                        Color color;

                        if (simChunk != null)
                        {
                            var tile = simChunk.GetTile(localX, localY);
                            var display = GetTileDisplay(tile);
                            glyph = display.glyph;
                            color = display.color;
                        }
                        else
                        {
                            // Fallback for chunks that don't exist
                            glyph = '#';
                            color = Color.DarkGray;
                        }

                    // Draw terrain or cursor
                    if (worldX == _cursorPos.X && worldY == _cursorPos.Y && _zoomLevel == 1)
                    {
                        // Draw cursor
                        _mapSurface.SetGlyph(sx, sy, 'X', Color.Yellow, Color.DarkGray);
                    }
                    else if (_zoomLevel > 1)
                    {
                        // When zoomed, draw each tile multiple times
                        for (int zx = 0; zx < _zoomLevel && sx + zx < viewW; zx++)
                        {
                            for (int zy = 0; zy < _zoomLevel && sy + zy < viewH; zy++)
                            {
                                bool isCursor = (worldX == _cursorPos.X && worldY == _cursorPos.Y);
                                _mapSurface.SetGlyph(sx + zx, sy + zy,
                                    isCursor ? 'X' : glyph,
                                    isCursor ? Color.Yellow : color,
                                    isCursor ? Color.DarkGray : Color.Black);
                            }
                        }
                    }
                    else
                    {
                        _mapSurface.SetGlyph(sx, sy, glyph, color);
                    }
                }
                else
                {
                    // No world data available
                    _mapSurface.SetGlyph(sx, sy, '?', Color.DarkGray);
                }
            }
            }

            // Render creatures and items on top of terrain
            if (_world != null && _mapSurface != null)
            {
                RenderEntities(viewW, viewH);
            }

            // Render navigation overlay if active
            if (_navOverlay != null && _world != null && _mapSurface != null)
            {
                var viewport = new Rectangle(_cameraPos.X, _cameraPos.Y, viewW, viewH);
                _navOverlay.RenderOverlay(_mapSurface, _world, _currentZ, viewport);
            }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[RenderMap] ERROR: {ex.Message}");
                System.Console.WriteLine($"[RenderMap] Stack trace: {ex.StackTrace}");
            }
        }

        private (int glyph, Color color) GetTileDisplay(TileBase tile)
        {
            // Content-driven display: prefer geology display colors; shape by TerrainKind
            var geology = ContentRegistry.Instance.GetGeologyByHandle(tile.GeoMatId);

            // Default fallback colors if geology not found
            var fg = geology != null
                ? new Color(geology.Display.Foreground.R, geology.Display.Foreground.G, geology.Display.Foreground.B)
                : Color.Gray;

            // Choose glyph based on terrain kind. For floor/wall/air, use geology glyph if available.
            int glyph;
            switch (tile.Kind)
            {
                case HumanFortress.Simulation.Tiles.TerrainKind.SolidWall:
                    glyph = geology?.Display.Glyph ?? '#';
                    break;
                case HumanFortress.Simulation.Tiles.TerrainKind.OpenWithFloor:
                    // Surface overlays: Snow > Grass > Mud > Moss
                    if (tile.HasSnow)
                    {
                        glyph = '*';
                        fg = Color.White;
                    }
                    else if (tile.HasGrass)
                    {
                        glyph = ',';
                        fg = Color.Green;
                    }
                    else if (tile.HasMud)
                    {
                        glyph = '~';
                        fg = Color.DarkGoldenrod;
                    }
                    else if (tile.HasMoss)
                    {
                        glyph = ',';
                        fg = new Color(0, 120, 0);
                    }
                    else
                    {
                        // For floors, prefer a dedicated floor glyph instead of geology's wall glyph
                        glyph = '.';
                    }
                    break;
                case HumanFortress.Simulation.Tiles.TerrainKind.OpenNoFloor:
                    glyph = ' ';
                    break;
                case HumanFortress.Simulation.Tiles.TerrainKind.Ramp:
                    // Generic ramp base glyph (direction is derived at runtime; overlay shows arrows)
                    glyph = '^';
                    break;
                case HumanFortress.Simulation.Tiles.TerrainKind.StairsUp:
                    glyph = '<';
                    break;
                case HumanFortress.Simulation.Tiles.TerrainKind.StairsDown:
                    glyph = '>';
                    break;
                case HumanFortress.Simulation.Tiles.TerrainKind.StairsUD:
                    glyph = 'X';
                    break;
                default:
                    glyph = geology?.Display.Glyph ?? '?';
                    break;
            }

            return (glyph, fg);
        }

        private void RenderEntities(int viewW, int viewH)
        {
            // Render all creatures on current Z level
            var creatures = _world.Creatures.GetAllInstances()
                .Where(c => c.Z == _currentZ && c.HP > 0)
                .ToList();

            foreach (var creature in creatures)
            {
                int screenX = creature.Position.X - _cameraPos.X;
                int screenY = creature.Position.Y - _cameraPos.Y;

                // Check if creature is in viewport
                if (screenX >= 0 && screenX < viewW && screenY >= 0 && screenY < viewH)
                {
                    var (glyph, color) = GetCreatureDisplay(creature);
                    _mapSurface.SetGlyph(screenX, screenY, glyph, color, Color.Black);
                }
            }

            // Render all items on current Z level
            // Render all items on current Z level
            var items = _world.Items.GetAllInstances()
                .Where(i => i.Z == _currentZ && !i.IsCarried)
                .ToList();

            foreach (var item in items)
            {
                int screenX = item.Position.X - _cameraPos.X;
                int screenY = item.Position.Y - _cameraPos.Y;

                // Check if item is in viewport
                if (screenX >= 0 && screenX < viewW && screenY >= 0 && screenY < viewH)
                {
                    // Only render items if no creature is on this tile
                    bool hasCreature = creatures.Any(c => c.Position.X == item.Position.X && c.Position.Y == item.Position.Y);
                    if (!hasCreature)
                    {
                        var (glyph, color) = GetItemDisplay(item);
                        _mapSurface.SetGlyph(screenX, screenY, glyph, color, Color.Black);
                    }
                }
            }
        }
        private (int glyph, Color color) GetCreatureDisplay(HumanFortress.Simulation.Creatures.CreatureInstance creature)
        {
            // Get definition for better display info
            var def = _world.Creatures.GetDefinition(creature.DefinitionId);

            // Use first letter of name as default glyph
            int glyph = '@'; // Default humanoid
            Color color = Color.White;

            if (def != null)
            {
                // Use first letter of creature type name
                glyph = char.ToUpper(def.Name[0]);

                // Color by tags
                if (def.Tags.Contains("civilized"))
                    color = Color.Cyan;
                else if (def.Tags.Contains("hostile"))
                    color = Color.Red;
                else if (def.Tags.Contains("wildlife"))
                    color = Color.Green;
                else
                    color = Color.White;
            }

            return (glyph, color);
        }

        private (int glyph, Color color) GetItemDisplay(HumanFortress.Simulation.Items.ItemInstance item)
        {
            var def = _world.Items.GetDefinition(item.DefinitionId);

            int glyph = '?'; // Default unknown
            Color color = Color.Gray;

            if (def != null)
            {
                // Use Kind to determine glyph
                glyph = def.Kind.ToLower() switch
                {
                    "resource" => '*',
                    "weapon" => '/',
                    "armor" => '[',
                    "tool" => '&',
                    "container" => 'U',
                    "consumable" => '%',
                    _ => '?'
                };

                // Color by material or kind
                color = def.Kind.ToLower() switch
                {
                    "resource" => Color.Brown,
                    "weapon" => Color.Silver,
                    "armor" => Color.LightGray,
                    "tool" => Color.Yellow,
                    "container" => Color.DarkGoldenrod,
                    "consumable" => Color.Green,
                    _ => Color.Gray
                };
            }

            return (glyph, color);
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
                    ChunkKey = new HumanFortress.Simulation.World.ChunkKey(chunkX, chunkY, _currentZ),
                    CenterX = _cameraPos.X % 32,
                    CenterY = _cameraPos.Y % 32,
                    Z = _currentZ,
                    Z0 = _currentZ,
                    ZCount = 1
                };

                var viewport = new ViewportInfo
                {
                    TilesWidth = _mapSurface?.Surface.Width ?? 80,
                    TilesHeight = _mapSurface?.Surface.Height ?? 40
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

            // Move camera with WASD (continuous while held)
            if (keyboard.IsKeyDown(Keys.W))
            {
                _cameraPos = new Point(_cameraPos.X, _cameraPos.Y - moveSpeed);
                changed = true;
            }
            else if (keyboard.IsKeyDown(Keys.S))
            {
                _cameraPos = new Point(_cameraPos.X, _cameraPos.Y + moveSpeed);
                changed = true;
            }
            else if (keyboard.IsKeyDown(Keys.A))
            {
                _cameraPos = new Point(_cameraPos.X - moveSpeed, _cameraPos.Y);
                changed = true;
            }
            else if (keyboard.IsKeyDown(Keys.D))
            {
                _cameraPos = new Point(_cameraPos.X + moveSpeed, _cameraPos.Y);
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

            // Simulation control: pause/speed
            if (keyboard.IsKeyPressed(Keys.Space))
            {
                var scheduler = GameStateManager.Instance.TickScheduler;
                scheduler.TogglePause();
                string status = scheduler.IsPaused ? "PAUSED" : $"Running at {scheduler.SpeedMultiplier:F2}x";
                _ui.AddToast(status, _uiTick + 100);
                Logger.Log($"[SIM] Space -> Pause toggled: {scheduler.IsPaused}");
                changed = true;
            }
            else if (keyboard.IsKeyPressed(Keys.OemMinus) || keyboard.IsKeyPressed(Keys.Subtract))
            {
                var scheduler = GameStateManager.Instance.TickScheduler;
                scheduler.CycleSpeedDown();
                _ui.AddToast($"Speed: {scheduler.SpeedMultiplier:F2}x", _uiTick + 100);
                Logger.Log($"[SIM] Speed decreased to {scheduler.SpeedMultiplier:F2}x");
                changed = true;
            }
            else if (keyboard.IsKeyPressed(Keys.OemPlus) || keyboard.IsKeyPressed(Keys.Add))
            {
                var scheduler = GameStateManager.Instance.TickScheduler;
                scheduler.CycleSpeedUp();
                _ui.AddToast($"Speed: {scheduler.SpeedMultiplier:F2}x", _uiTick + 100);
                Logger.Log($"[SIM] Speed increased to {scheduler.SpeedMultiplier:F2}x");
                changed = true;
            }

            // UI: help panel
            if (keyboard.IsKeyPressed(Keys.H))
            {
                _ui.ToggleHelp();
                changed = true;
            }

            // UI: debug menu - support ` / ~ via OEM detection or F12 fallback
            if (keyboard.IsKeyPressed(Keys.F12) || (keyboard.KeysPressed.Count > 0 && keyboard.KeysPressed.Any(k =>
                {
                    var name = k.Key.ToString();
                    return name.Contains("OemTilde", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("Oem3", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("OemGrave", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("Oem8", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("Oem7", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("Backquote", StringComparison.OrdinalIgnoreCase);
                })))
            {
                _ui.ToggleDebug();
                Logger.Log($"[DEBUG] Toggle debug menu -> { _ui.DebugOpen }");
                changed = true;
            }

            // UI: dock panels F1..F8
            if (keyboard.IsKeyPressed(Keys.F1)) { _ui.OpenPanel(DrawerId.Creature); Logger.Log($"[KEY] F1 -> Drawer={_ui.OpenDrawer}"); changed = true; }
            else if (keyboard.IsKeyPressed(Keys.F2)) { _ui.OpenPanel(DrawerId.Stock); Logger.Log($"[KEY] F2 -> Drawer={_ui.OpenDrawer}"); changed = true; }
            else if (keyboard.IsKeyPressed(Keys.F3)) { _ui.OpenPanel(DrawerId.Work); Logger.Log($"[KEY] F3 -> Drawer={_ui.OpenDrawer}"); changed = true; }
            else if (keyboard.IsKeyPressed(Keys.F4)) { _ui.OpenPanel(DrawerId.PlacementManagement); Logger.Log($"[KEY] F4 -> Drawer={_ui.OpenDrawer}"); changed = true; }
            else if (keyboard.IsKeyPressed(Keys.F5)) { _ui.OpenPanel(DrawerId.Military); Logger.Log($"[KEY] F5 -> Drawer={_ui.OpenDrawer}"); changed = true; }
            else if (keyboard.IsKeyPressed(Keys.F6)) { _ui.OpenPanel(DrawerId.Country); Logger.Log($"[KEY] F6 -> Drawer={_ui.OpenDrawer}"); changed = true; }
            else if (keyboard.IsKeyPressed(Keys.F7)) { _ui.OpenPanel(DrawerId.World); Logger.Log($"[KEY] F7 -> Drawer={_ui.OpenDrawer}"); changed = true; }
            else if (keyboard.IsKeyPressed(Keys.F8)) { _ui.OpenPanel(DrawerId.Log); Logger.Log($"[KEY] F8 -> Drawer={_ui.OpenDrawer}"); changed = true; }
            else if (keyboard.IsKeyPressed(Keys.F9))
            {
                // Cycle navigation overlay modes
                _navOverlay?.CycleMode();
                _ui.AddToast($"Overlay: {_navOverlay?.CurrentMode}", _uiTick + 150);
                changed = true;
            }
            else if (keyboard.IsKeyPressed(Keys.F10))
            {
                var ctrl = keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl);
                if (ctrl)
                {
                    _navOverlay?.ClearPath();
                    _pathStart = null;
                    _ui.AddToast("Path cleared", _uiTick + 120);
                }
                else
                {
                    HandlePathToolF10();
                }
                changed = true;
            }

            // Handle debug menu keys when open
            if (_ui.DebugOpen)
            {
                if (keyboard.IsKeyPressed(Keys.Tab))
                {
                    _ui.DebugMenuTab = (_ui.DebugMenuTab + 1) % 3; // 3 tabs: Status/Creatures/Items
                    changed = true;
                }
                else if (keyboard.IsKeyPressed(Keys.D0))
                {
                    _ui.DebugMenuTab = 0; // Status tab
                    changed = true;
                }
                else if (keyboard.IsKeyPressed(Keys.D1))
                {
                    if (_ui.DebugMenuTab == 2) // Items tab - select stone
                        _ui.DebugSelectedItem = "core_item_stone_generic";
                    else
                        _ui.DebugMenuTab = 1; // Switch to creatures tab
                    changed = true;
                }
                else if (keyboard.IsKeyPressed(Keys.D2))
                {
                    if (_ui.DebugMenuTab == 2) // Items tab - select iron
                        _ui.DebugSelectedItem = "core_item_ingot_iron";
                    else
                        _ui.DebugMenuTab = 2; // Switch to items tab
                    changed = true;
                }
                else if (keyboard.IsKeyPressed(Keys.D3) && _ui.DebugMenuTab == 2)
                {
                    _ui.DebugSelectedItem = "core_item_wood_log";
                    changed = true;
                }
                else if (keyboard.IsKeyPressed(Keys.D4) && _ui.DebugMenuTab == 2)
                {
                    _ui.DebugSelectedItem = "core_tool_mining_pickaxe";
                    changed = true;
                }
                else if (keyboard.IsKeyPressed(Keys.D5) && _ui.DebugMenuTab == 2)
                {
                    _ui.DebugSelectedItem = "core_weapon_sword_short";
                    changed = true;
                }
                else if (_ui.DebugMenuTab == 1) // Creatures tab
                {
                    // Creature selection
                    if (keyboard.IsKeyPressed(Keys.D))
                    {
                        _ui.DebugSelectedCreature = "core_race_dwarf";
                        changed = true;
                    }
                    else if (keyboard.IsKeyPressed(Keys.H))
                    {
                        _ui.DebugSelectedCreature = "core_race_human";
                        changed = true;
                    }
                    else if (keyboard.IsKeyPressed(Keys.G))
                    {
                        _ui.DebugSelectedCreature = "core_race_goblin";
                        changed = true;
                    }
                    else if (keyboard.IsKeyPressed(Keys.E))
                    {
                        _ui.DebugSelectedCreature = "core_race_elf";
                        changed = true;
                    }
                    else if (keyboard.IsKeyPressed(Keys.O))
                    {
                        _ui.DebugSelectedCreature = "core_race_orc";
                        changed = true;
                    }
                }
            }

            // Handle context-specific keys first
            // Orders Quick Menu
            else if (_ui.Context == UiContext.QuickMenu && _ui.QuickMenu == QuickMenuKind.Orders)
            {
                HandleOrdersMenu(keyboard, ref changed);
            }
            // Zones Quick Menu
            else if (_ui.Context == UiContext.QuickMenu && _ui.QuickMenu == QuickMenuKind.Zones)
            {
                HandleZonesMenu(keyboard, ref changed);
            }
            // Build Quick Menu
            else if (_ui.Context == UiContext.QuickMenu && _ui.QuickMenu == QuickMenuKind.Build)
            {
                HandleBuildMenu(keyboard, ref changed);
            }
            // Stockpile Quick Menu
            else if (_ui.Context == UiContext.QuickMenu && _ui.QuickMenu == QuickMenuKind.Stockpile)
            {
                HandleStockpileMenu(keyboard, ref changed);
            }
            else if (_ui.Context == UiContext.PlacingTool)
            {
                // Handle preset selection
                if (_ui.PlaceMode == PlacementMode.StockpilePresetSelect)
                {
                    for (int i = 1; i <= 9; i++)
                    {
                        if (keyboard.IsKeyPressed((Keys)(Keys.D1 + i - 1)))
                        {
                            var presetId = _stockpileUI?.HandlePresetSelection(i);
                            if (presetId != null)
                            {
                                CreateStockpile(presetId);
                                _ui.CancelPlacement();
                                changed = true;
                            }
                            break;
                        }
                    }
                    if (keyboard.IsKeyPressed(Keys.Enter))
                    {
                        CreateStockpile("all");
                        _ui.CancelPlacement();
                        changed = true;
                    }
                }
            }
            // UI: quick menus Z/X/C/V (only in global context)
            else if (_ui.Context == UiContext.Global)
            {
                if (keyboard.IsKeyPressed(Keys.Z)) { _ui.OpenQuickMenu(QuickMenuKind.Orders); Logger.Log($"[KEY] Z -> QMenu={_ui.QuickMenu}"); changed = true; }
                else if (keyboard.IsKeyPressed(Keys.X)) { _ui.OpenQuickMenu(QuickMenuKind.Zones); Logger.Log($"[KEY] X -> QMenu={_ui.QuickMenu}"); changed = true; }
                else if (keyboard.IsKeyPressed(Keys.C)) { _ui.OpenQuickMenu(QuickMenuKind.Build); Logger.Log($"[KEY] C -> QMenu={_ui.QuickMenu}"); changed = true; }
                else if (keyboard.IsKeyPressed(Keys.V)) { _ui.OpenQuickMenu(QuickMenuKind.Stockpile); Logger.Log($"[KEY] V -> QMenu={_ui.QuickMenu}"); changed = true; }
            }

            // Overlay cycle F9
            if (keyboard.IsKeyPressed(Keys.F9) && _navOverlay != null)
            {
                _navOverlay.CycleMode();
                if (_navOverlay.CurrentMode == NavigationOverlay.OverlayMode.FlowField)
                    _navOverlay.SetTarget(_cursorPos);
                changed = true;
            }

            // Drawer tab cycling
            if (_ui.Context == UiContext.Drawer)
            {
                if (keyboard.IsKeyPressed(Keys.Tab) && !(keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift)))
                {
                    _ui.TabNext(); changed = true;
                }
                else if (keyboard.IsKeyPressed(Keys.Tab) && (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift)))
                {
                    _ui.TabPrev(); changed = true;
                }
            }

            if (keyboard.IsKeyPressed(Keys.Escape))
            {
                // Priority order: tile panel -> stockpile edit -> placement mode -> submenus -> main menus
                if (_tilePanelOpen)
                {
                    HideTilePanel();
                    changed = true;
                }
                else if (_stockpileUI != null)
                {
                    // Always close edit popup first
                    _stockpileUI.CloseEditPopup();

                    // Then handle placement or menu states
                    if (_ui.Context == UiContext.PlacingTool)
                    {
                        _ui.CancelPlacement();
                    }
                    else
                    {
                        _ui.Back();
                    }
                    changed = true;
                }
                // If no UI open, toggle pause overlay instead of exiting immediately
                else if (_ui.OpenDrawer == DrawerId.None && _ui.QuickMenu == QuickMenuKind.None && !_ui.HelpOpen && !_ui.DebugOpen)
                {
                    _ui.TogglePause();
                    Logger.Log("[UI] ESC -> Toggle Pause overlay");
                    changed = true;
                }
                else
                {
                    _ui.Back();
                    Logger.Log($"[UI] ESC -> Back; drawer={_ui.OpenDrawer} qmenu={_ui.QuickMenu} help={_ui.HelpOpen} debug={_ui.DebugOpen}");
                    changed = true;
                }
            }

            // Final redraw after handling all UI keys (ensures ESC/Debug/help reflect immediately)
            if (changed)
            {
                UpdateCameraToFollowCursor();
                BuildSnapshot();
                DrawUI();
            }

            return true;
        }

        private void HandlePathToolF10()
        {
            if (_world == null || _navManager == null || _navOverlay == null) return;
            // Ensure nav view
            _navView ??= new HumanFortress.Navigation.WorldNavigationView(_navManager, _world);

            if (_pathStart == null)
            {
                _pathStart = _cursorPos;
                _pathStartZ = _currentZ;
                _ui.AddToast($"Start set at ({_pathStart.Value.X},{_pathStart.Value.Y},{_pathStartZ})", _uiTick + 150);
                _navOverlay.CurrentMode = HumanFortress.App.Rendering.NavigationOverlay.OverlayMode.PathDisplay;
                return;
            }

            // Compute path from previously set Start ??current cursor
            var tuning = HumanFortress.Navigation.NavigationTuning.LoadFromContent();
            var astar = new HumanFortress.Navigation.DeterministicAStar(tuning);
            var flags = tuning.AllowDiagonals ? HumanFortress.Navigation.PathFlags.AllowDiagonal : HumanFortress.Navigation.PathFlags.None;
            var req = new HumanFortress.Navigation.PathRequest(
                new HumanFortress.Navigation.Point3(_pathStart.Value.X, _pathStart.Value.Y, _pathStartZ),
                new HumanFortress.Navigation.Point3(_cursorPos.X, _cursorPos.Y, _currentZ),
                HumanFortress.Navigation.MoveMode.Walk,
                flags,
                0);
            var path = astar.FindPath(req, _navView);
            _navOverlay.CurrentMode = HumanFortress.App.Rendering.NavigationOverlay.OverlayMode.PathDisplay;
            _navOverlay.SetPath(path);
            // TotalCost is fixed-point (FP=10). Show with one decimal.
            double totalCost = path.TotalCost / 10.0;
            _ui.AddToast($"Path: {path.Kind} len={path.Length} cost={totalCost:F1}", _uiTick + 180);
        }

        public override bool ProcessMouse(MouseScreenObjectState state)
        {
            if (_mapSurface == null || _fortressMap == null) return false;

            bool changed = false;

            // Ensure keyboard focus stays on FortressState while handling mouse
            if (!IsFocused) IsFocused = true;

            // Keep keyboard focus on this state so panels remain interactive after clicks
            this.IsFocused = true;

            // Get mouse position relative to map surface
            var mousePos = new Point(state.SurfaceCellPosition.X - _mapSurface.Position.X,
                                     state.SurfaceCellPosition.Y - _mapSurface.Position.Y);

            // Check if mouse is over the map
            if (mousePos.X >= 0 && mousePos.X < _mapSurface.Surface.Width && mousePos.Y >= 0 && mousePos.Y < _mapSurface.Surface.Height)
            {
                // Calculate world position from mouse
                int worldX = _cameraPos.X + (mousePos.X / _zoomLevel);
                int worldY = _cameraPos.Y + (mousePos.Y / _zoomLevel);
                int maxPos = FortressSize * 32 - 1;

                if (worldX >= 0 && worldX <= maxPos && worldY >= 0 && worldY <= maxPos)
                {
                    _lastMousePos = new Point(worldX, worldY);
                    _cursorPos = _lastMousePos.Value; // follow mouse
                    Logger.Log($"[MOUSE] Hover tile world=({_cursorPos.X},{_cursorPos.Y},{_currentZ})");
                    changed = true;
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
                    Logger.Log($"[ZOOM] Ctrl+Wheel delta={delta} -> zoom={_zoomLevel}");
                    changed = true;
                }
                else
                {
                    // Regular scroll for Z-level
                    int delta = state.Mouse.ScrollWheelValueChange > 0 ? 1 : -1;
                    _currentZ = Math.Max(0, Math.Min(49, _currentZ - delta));
                    Logger.Log($"[ZLEVEL] Wheel delta={delta} (reversed) -> Z={_currentZ}");
                    changed = true;
                }
            }

            if (changed)
            {
                UpdateCameraToFollowCursor();
                BuildSnapshot();
                DrawUI();
            }

            // First: screen-level dock buttons (bottom-left of console)
            if (state.Mouse.LeftClicked)
            {
                if (HandleDockClicksScreen(state.SurfaceCellPosition))
                    return true;
                if (HandleQuickClicksScreen(state.SurfaceCellPosition))
                    return true;
                if (HandleQuickMenuClicksScreen(state.SurfaceCellPosition))
                    return true;
            }

            // Handle mouse clicks for UI (map-relative)
            if (state.Mouse.LeftClicked)
            {
                // Hit-test dock and quick icons by map-surface coordinates
                var cell = state.SurfaceCellPosition - _mapSurface.Position;
                int yDock = _mapSurface.Surface.Height - 1;
                if (cell.Y == yDock - 1) // moved up one row
                {
                    // F1-F8 buttons (correct order: F1=Creature, F2=Stock, F3=Work, F4=PlacementManagement, F5=Military, F6=Country, F7=World, F8=Log)
                    if (cell.X >= 0 && cell.X < 5) { _ui.OpenPanel(DrawerId.Creature); }              // F1
                    else if (cell.X < 10) { _ui.OpenPanel(DrawerId.Stock); }                          // F2
                    else if (cell.X < 15) { _ui.OpenPanel(DrawerId.Work); }                           // F3 (FIXED)
                    else if (cell.X < 20) { _ui.OpenPanel(DrawerId.PlacementManagement); }            // F4 (FIXED)
                    else if (cell.X < 25) { _ui.OpenPanel(DrawerId.Military); }                       // F5
                    else if (cell.X < 30) { _ui.OpenPanel(DrawerId.Country); }                        // F6
                    else if (cell.X < 35) { _ui.OpenPanel(DrawerId.World); }                          // F7
                    else if (cell.X < 40) { _ui.OpenPanel(DrawerId.Log); }                            // F8
                    Logger.Log($"[CLICK] Dock click at cell=({cell.X},{cell.Y}) -> drawer={_ui.OpenDrawer}");
                    DrawUI();
                    return true;
                }

                int center = _mapSurface.Surface.Width / 2;
                int quickY = yDock;
                if (cell.Y == quickY)
                {
                    if (cell.X >= center - 8 && cell.X < center - 8 + 9) { _ui.OpenQuickMenu(QuickMenuKind.Orders); }
                    else if (cell.X >= center + 2 && cell.X < center + 2 + 8) { _ui.OpenQuickMenu(QuickMenuKind.Zones); }
                    else if (cell.X >= center + 12 && cell.X < center + 12 + 8) { _ui.OpenQuickMenu(QuickMenuKind.Build); }
                    DrawUI();
                    return true;
                }

                // If click inside the map and not on UI, open tile panel
                if (cell.X >= 0 && cell.X < _mapSurface.Surface.Width && cell.Y >= 0 && cell.Y < _mapSurface.Surface.Height)
                {
                    OnMapLeftClickedLocal(cell);
                    return true;
                }
            }

            if (state.Mouse.RightClicked)
            {
                Logger.Log($"[RIGHT-CLICK] Detected at screen=({state.SurfaceCellPosition.X},{state.SurfaceCellPosition.Y}), tilePanelOpen={_tilePanelOpen}, QuickMenu={_ui.QuickMenu}, OrdersMenu={_ui.OrdersMenu}, ZoneMenu={_ui.ZoneMenu}");
                bool handled = false;

                // Priority 1: Close tile panel if open
                if (_tilePanelOpen)
                {
                    Logger.Log("[RIGHT-CLICK] Closing tile panel");
                    HideTilePanel();
                    handled = true;
                }
                // Priority 2: Close zone detail popup if open
                else if (_zonesUI?.IsDetailPopupOpen() == true)
                {
                    Logger.Log("[RIGHT-CLICK] Closing zone detail popup");
                    _zonesUI.CloseDetailPopup();
                    handled = true;
                }
                // Priority 3: Try closing stockpile edit popup
                else if (_stockpileUI != null)
                {
                    Logger.Log("[RIGHT-CLICK] Trying stockpile popup close");
                    // Always try to close stockpile popup first
                    _stockpileUI.CloseEditPopup();

                    // Then handle QuickMenu navigation
                    if (_ui.QuickMenu != QuickMenuKind.None)
                    {
                        // If in L3 submenu, go back to L2
                        if (_ui.OrdersMenu != OrdersSubmenu.None)
                        {
                            Logger.Log("[RIGHT-CLICK] Closing OrdersMenu submenu");
                            _ui.CloseOrdersSubmenu();
                            handled = true;
                        }
                        else if (_ui.ZoneMenu != ZoneSubmenu.None)
                        {
                            Logger.Log("[RIGHT-CLICK] Closing ZoneMenu submenu");
                            _ui.CloseZoneSubmenu();
                            handled = true;
                        }
                        else if (_ui.BuildMenu != BuildSubmenu.None)
                        {
                            Logger.Log("[RIGHT-CLICK] Closing BuildMenu submenu");
                            _ui.CloseBuildSubmenu();
                            handled = true;
                        }
                        else if (_ui.StockpileMenu != StockpileSubmenu.None)
                        {
                            Logger.Log("[RIGHT-CLICK] Closing StockpileMenu submenu");
                            _ui.CloseStockpileSubmenu();
                            handled = true;
                        }
                        // If in L2 (no submenu), close the QuickMenu entirely
                        else
                        {
                            Logger.Log("[RIGHT-CLICK] Closing QuickMenu entirely (was in L2)");
                            _ui.CancelPlacement();
                            handled = true;
                        }
                    }
                    else
                    {
                        Logger.Log("[RIGHT-CLICK] General cancel (no QuickMenu)");
                        _ui.Cancel();
                        handled = true;
                    }
                }
                // Priority 4: Step back from L3 submenu to L2 menu
                else if (_ui.QuickMenu != QuickMenuKind.None)
                {
                    // If in L3 submenu, go back to L2
                    if (_ui.OrdersMenu != OrdersSubmenu.None)
                    {
                        Logger.Log("[RIGHT-CLICK] Priority 4: Closing OrdersMenu submenu");
                        _ui.CloseOrdersSubmenu();
                        handled = true;
                    }
                    else if (_ui.ZoneMenu != ZoneSubmenu.None)
                    {
                        Logger.Log("[RIGHT-CLICK] Priority 4: Closing ZoneMenu submenu");
                        _ui.CloseZoneSubmenu();
                        handled = true;
                    }
                    else if (_ui.BuildMenu != BuildSubmenu.None)
                    {
                        Logger.Log("[RIGHT-CLICK] Priority 4: Closing BuildMenu submenu");
                        _ui.CloseBuildSubmenu();
                        handled = true;
                    }
                    else if (_ui.StockpileMenu != StockpileSubmenu.None)
                    {
                        Logger.Log("[RIGHT-CLICK] Priority 4: Closing StockpileMenu submenu");
                        _ui.CloseStockpileSubmenu();
                        handled = true;
                    }
                    // If in L2 (no submenu), close the QuickMenu entirely
                    else
                    {
                        Logger.Log("[RIGHT-CLICK] Priority 4: Closing QuickMenu entirely (was in L2)");
                        _ui.CancelPlacement();
                        handled = true;
                    }
                }
                // Priority 5: General cancel (close drawer, etc.)
                else
                {
                    Logger.Log("[RIGHT-CLICK] Priority 5: General cancel");
                    _ui.Cancel();
                    handled = true;
                }

                if (handled)
                {
                    Logger.Log("[RIGHT-CLICK] Handled successfully, redrawing UI");
                    DrawUI();
                    return true;
                }
                else
                {
                    Logger.Log("[RIGHT-CLICK] Not handled!");
                }
            }

            return base.ProcessMouse(state);
        }

        // Click handling for bottom-left dock drawn at screen coordinates
        private bool HandleDockClicksScreen(Point screenCell)
        {
            int screenW = GameHost.Instance?.ScreenCellsX ?? 0;
            int screenH = GameHost.Instance?.ScreenCellsY ?? 0;
            int y = screenH - 1; // moved to bottom row
            if (screenCell.Y != y) return false;

            int xStart = 1;
            int buttonWidth = 5; // must match UiRenderer.DrawDockScreen
            int gap = 1;

            // Explicit mapping: button index -> DrawerId (F1-F8 order)
            var buttonMap = new DrawerId[]
            {
                DrawerId.Creature,              // F1 (index 0)
                DrawerId.Stock,                 // F2 (index 1)
                DrawerId.Work,                  // F3 (index 2)
                DrawerId.PlacementManagement,   // F4 (index 3)
                DrawerId.Military,              // F5 (index 4)
                DrawerId.Country,               // F6 (index 5)
                DrawerId.World,                 // F7 (index 6)
                DrawerId.Log                    // F8 (index 7)
            };

            int x = screenCell.X;
            for (int i = 0; i < buttonMap.Length; i++)
            {
                int start = xStart + i * (buttonWidth + gap);
                int end = start + buttonWidth - 1;
                if (x >= start && x <= end)
                {
                    _ui.OpenPanel(buttonMap[i]);
                    Logger.Log($"[CLICK] DockScreen i={i} cell=({screenCell.X},{screenCell.Y}) -> drawer={_ui.OpenDrawer}");
                    DrawUI();
                    return true;
                }
            }

            return false;
        }

        // Click handling for bottom-center quick icons drawn at screen coordinates
        private bool HandleQuickClicksScreen(Point screenCell)
        {
            int screenW = GameHost.Instance?.ScreenCellsX ?? 0;
            int screenH = GameHost.Instance?.ScreenCellsY ?? 0;
            int y = screenH - 1; // bottom row, same as F1-F8 (matches UiRenderer.DrawQuickIconsScreen)

            Logger.Log($"[CLICK-DEBUG] HandleQuickClicksScreen: screenCell=({screenCell.X},{screenCell.Y}), screenH={screenH}, y={y}");

            if (screenCell.Y != y)
            {
                Logger.Log($"[CLICK-DEBUG] Y mismatch: screenCell.Y={screenCell.Y} != y={y}");
                return false;
            }

            int center = screenW / 2;
            int buttonWidth = 5; // must match UiRenderer
            int gap = 2;

            // 4 buttons: Z X C V (totalWidth = 4*5 + 3*2 = 26)
            int totalWidth = (buttonWidth * 4) + (gap * 3);
            int startX = center - totalWidth / 2;

            var buttons = new (int start, int end, QuickMenuKind kind)[]
            {
                (startX, startX + buttonWidth - 1, QuickMenuKind.Orders),
                (startX + buttonWidth + gap, startX + buttonWidth + gap + buttonWidth - 1, QuickMenuKind.Zones),
                (startX + (buttonWidth + gap) * 2, startX + (buttonWidth + gap) * 2 + buttonWidth - 1, QuickMenuKind.Build),
                (startX + (buttonWidth + gap) * 3, startX + (buttonWidth + gap) * 3 + buttonWidth - 1, QuickMenuKind.Stockpile),
            };

            Logger.Log($"[CLICK-DEBUG] Button ranges: Z=[{buttons[0].start},{buttons[0].end}], X=[{buttons[1].start},{buttons[1].end}], C=[{buttons[2].start},{buttons[2].end}], V=[{buttons[3].start},{buttons[3].end}]");

            foreach (var b in buttons)
            {
                if (screenCell.X >= b.start && screenCell.X <= b.end)
                {
                    Logger.Log($"[CLICK] QuickIconsScreen HIT: kind={b.kind} cell=({screenCell.X},{screenCell.Y})");

                    // If clicking current menu, toggle it off; otherwise switch to new menu
                    if (_ui.QuickMenu == b.kind)
                    {
                        _ui.CancelPlacement();
                    }
                    else
                    {
                        _ui.OpenQuickMenu(b.kind);
                    }
                    Logger.Log($"[CLICK] QuickIconsScreen result: qmenu={_ui.QuickMenu}");
                    DrawUI();
                    return true;
                }
            }

            Logger.Log($"[CLICK-DEBUG] No button hit for X={screenCell.X}");
            return false;
        }

        // Click handling for quick menu items (L1, L2, L3)
        private bool HandleQuickMenuClicksScreen(Point screenCell)
        {
            if (_ui.QuickMenu == QuickMenuKind.None) return false;

            int screenW = GameHost.Instance?.ScreenCellsX ?? 0;
            int screenH = GameHost.Instance?.ScreenCellsY ?? 0;
            int centerX = screenW / 2;

            // L1 menu positions (root popups) - buttons at screenH-1, menus end at screenH-2
            if (_ui.QuickMenu == QuickMenuKind.Orders && _ui.OrdersMenu == OrdersSubmenu.None)
            {
                // Orders root: height=8, y = screenH - 9
                int x = (screenW - 30) / 2;
                int y = screenH - 9;
                // Check if click is inside popup (rows 1-6 are clickable menu items)
                if (screenCell.X >= x + 2 && screenCell.X < x + 30 && screenCell.Y >= y + 1 && screenCell.Y <= y + 6)
                {
                    int row = screenCell.Y - y;
                    if (row == 1) { _ui.OpenOrdersSubmenu(OrdersSubmenu.Mining); }
                    else if (row == 2) { _ui.OpenOrdersSubmenu(OrdersSubmenu.Lumbering); }
                    else if (row == 3) { _ui.OpenOrdersSubmenu(OrdersSubmenu.Gather); }
                    else if (row == 4) { _ui.OpenOrdersSubmenu(OrdersSubmenu.Masonry); }
                    else if (row == 5) { _ui.OpenOrdersSubmenu(OrdersSubmenu.Haul); }
                    else if (row == 6) { _ui.OpenOrdersSubmenu(OrdersSubmenu.Creature); }
                    else if (row == 7) { _ui.OpenOrdersSubmenu(OrdersSubmenu.Other); }
                    DrawUI();
                    return true;
                }
            }
            else if (_ui.QuickMenu == QuickMenuKind.Zones && _ui.ZoneMenu == ZoneSubmenu.None)
            {
                // Zones root: height=8, y = screenH - 9
                int x = (screenW - 30) / 2;
                int y = screenH - 9;
                if (screenCell.X >= x + 2 && screenCell.X < x + 30 && screenCell.Y >= y + 1 && screenCell.Y <= y + 6)
                {
                    int row = screenCell.Y - y;
                    if (row == 1) { _ui.OpenZoneSubmenu(ZoneSubmenu.Production); }
                    else if (row == 2) { _ui.OpenZoneSubmenu(ZoneSubmenu.Civil); }
                    else if (row == 3) { _ui.OpenZoneSubmenu(ZoneSubmenu.Public); }
                    else if (row == 4) { _ui.OpenZoneSubmenu(ZoneSubmenu.Military); }
                    else if (row == 5) { _ui.OpenZoneSubmenu(ZoneSubmenu.Management); }
                    DrawUI();
                    return true;
                }
            }
            else if (_ui.QuickMenu == QuickMenuKind.Build && _ui.BuildMenu == BuildSubmenu.None)
            {
                // Build root: height=8, y = screenH - 9
                int x = (screenW - 30) / 2;
                int y = screenH - 9;
                if (screenCell.X >= x + 2 && screenCell.X < x + 30 && screenCell.Y >= y + 1 && screenCell.Y <= y + 6)
                {
                    int row = screenCell.Y - y;
                    if (row == 1) { _ui.OpenBuildSubmenu(BuildSubmenu.Structural); }
                    else if (row == 2) { _ui.OpenBuildSubmenu(BuildSubmenu.FunctionalStructure); }
                    else if (row == 3) { _ui.OpenBuildSubmenu(BuildSubmenu.Workshop); }
                    else if (row == 4) { _ui.OpenBuildSubmenu(BuildSubmenu.CivilFurniture); }
                    else if (row == 5) { _ui.OpenBuildSubmenu(BuildSubmenu.UtilityFurniture); }
                    DrawUI();
                    return true;
                }
            }
            else if (_ui.QuickMenu == QuickMenuKind.Stockpile && _ui.StockpileMenu == StockpileSubmenu.None)
            {
                // Stockpile root: height=6, y = screenH - 7
                int x = (screenW - 30) / 2;
                int y = screenH - 7;
                if (screenCell.X >= x + 2 && screenCell.X < x + 30 && screenCell.Y >= y + 1 && screenCell.Y <= y + 3)
                {
                    int row = screenCell.Y - y;
                    if (row == 1) { _ui.OpenStockpileSubmenu(StockpileSubmenu.Stockpile); }
                    DrawUI();
                    return true;
                }
            }

            return false;
        }

        private void UpdateCameraToFollowCursor()
        {
            // Compute bounds
            int viewWidth = Math.Max(1, (_mapSurface?.Surface.Width ?? 80) / _zoomLevel);
            int viewHeight = Math.Max(1, (_mapSurface?.Surface.Height ?? 40) / _zoomLevel);
            int worldSize = FortressSize * 32;
            int maxCameraX = Math.Max(0, worldSize - viewWidth);
            int maxCameraY = Math.Max(0, worldSize - viewHeight);

            if (_cameraFollowCursor)
            {
                // Center camera on cursor
                int targetCameraX = _cursorPos.X - viewWidth / 2;
                int targetCameraY = _cursorPos.Y - viewHeight / 2;
                targetCameraX = Math.Max(0, Math.Min(maxCameraX, targetCameraX));
                targetCameraY = Math.Max(0, Math.Min(maxCameraY, targetCameraY));
                _cameraPos = new Point(targetCameraX, targetCameraY);
            }
            else
            {
                // Only clamp camera to bounds (no following)
                int cx = Math.Max(0, Math.Min(maxCameraX, _cameraPos.X));
                int cy = Math.Max(0, Math.Min(maxCameraY, _cameraPos.Y));
                _cameraPos = new Point(cx, cy);
            }
        }

        // _uiTick is advanced in the existing Update at the top of this class

        // Enhanced map-surface mouse handlers (for robust hover tracking)
        private void OnMapMouseMovedLocal(Point local)
        {
            if (_mapSurface == null) return;
            if (local.X < 0 || local.Y < 0 || local.X >= _mapSurface.Surface.Width || local.Y >= _mapSurface.Surface.Height)
                return;

            int worldX = _cameraPos.X + (local.X / _zoomLevel);
            int worldY = _cameraPos.Y + (local.Y / _zoomLevel);
            int maxPos = FortressSize * 32 - 1;
            if (worldX < 0 || worldY < 0 || worldX > maxPos || worldY > maxPos)
                return;

            _lastMousePos = new Point(worldX, worldY);
            _cursorPos = _lastMousePos.Value; // follow mouse
            if (_uiTick % 10 == 0)
                Logger.Log($"[MOUSE-EVT] Hover world=({_cursorPos.X},{_cursorPos.Y},{_currentZ}) local=({local.X},{local.Y}) camera=({_cameraPos.X},{_cameraPos.Y}) zoom={_zoomLevel}");
            UpdateTileInfo();
        }

        // Overlay mouse move: update cursor when overlay sits on top of map
        private void OnOverlayMouseMovedLocal(Point local)
        {
            if (_mapSurface == null || _uiSurface == null) return;
            var mapLocal = new Point(local.X - _mapSurface.Position.X, local.Y - _mapSurface.Position.Y);
            if (mapLocal.X < 0 || mapLocal.Y < 0 || mapLocal.X >= _mapSurface.Surface.Width || mapLocal.Y >= _mapSurface.Surface.Height)
                return;

            int worldX = _cameraPos.X + (mapLocal.X / _zoomLevel);
            int worldY = _cameraPos.Y + (mapLocal.Y / _zoomLevel);
            int maxPos = FortressSize * 32 - 1;
            if (worldX < 0 || worldY < 0 || worldX > maxPos || worldY > maxPos)
                return;

            _lastMousePos = new Point(worldX, worldY);
            _cursorPos = _lastMousePos.Value;
        }

        // Handle right-click on overlay: hierarchical back navigation
        private void OnOverlayRightClickedLocal(Point local)
        {
            Logger.Log($"[RIGHT-CLICK-OVERLAY] Clicked at local=({local.X},{local.Y}), tilePanelOpen={_tilePanelOpen}, QuickMenu={_ui.QuickMenu}, OrdersMenu={_ui.OrdersMenu}, ZoneMenu={_ui.ZoneMenu}");

            // Priority 1: Close tile panel if open
            if (_tilePanelOpen)
            {
                Logger.Log("[RIGHT-CLICK-OVERLAY] Closing tile panel");
                HideTilePanel();
                DrawUI();
                return;
            }

            // Priority 2: Close zone detail popup if open
            if (_zonesUI?.IsDetailPopupOpen() == true)
            {
                Logger.Log("[RIGHT-CLICK-OVERLAY] Closing zone detail popup");
                _zonesUI.CloseDetailPopup();
                DrawUI();
                return;
            }

            // Priority 3: Close stockpile edit popup if open
            if (_stockpileUI != null)
            {
                Logger.Log("[RIGHT-CLICK-OVERLAY] Trying stockpile popup close");
                _stockpileUI.CloseEditPopup();
            }

            // Priority 4: Step back from L3 submenu to L2, or L2 to close QuickMenu
            if (_ui.QuickMenu != QuickMenuKind.None)
            {
                // If in L3 submenu, go back to L2
                if (_ui.OrdersMenu != OrdersSubmenu.None)
                {
                    Logger.Log("[RIGHT-CLICK-OVERLAY] Closing OrdersMenu submenu (L3 -> L2)");
                    _ui.CloseOrdersSubmenu();
                    DrawUI();
                    return;
                }
                else if (_ui.ZoneMenu != ZoneSubmenu.None)
                {
                    Logger.Log("[RIGHT-CLICK-OVERLAY] Closing ZoneMenu submenu (L3 -> L2)");
                    _ui.CloseZoneSubmenu();
                    DrawUI();
                    return;
                }
                else if (_ui.BuildMenu != BuildSubmenu.None)
                {
                    Logger.Log("[RIGHT-CLICK-OVERLAY] Closing BuildMenu submenu (L3 -> L2)");
                    _ui.CloseBuildSubmenu();
                    DrawUI();
                    return;
                }
                else if (_ui.StockpileMenu != StockpileSubmenu.None)
                {
                    Logger.Log("[RIGHT-CLICK-OVERLAY] Closing StockpileMenu submenu (L3 -> L2)");
                    _ui.CloseStockpileSubmenu();
                    DrawUI();
                    return;
                }
                // If in L2 (no submenu), close the QuickMenu entirely
                else
                {
                    Logger.Log("[RIGHT-CLICK-OVERLAY] Closing QuickMenu entirely (L2 -> Global)");
                    _ui.CancelPlacement();
                    DrawUI();
                    return;
                }
            }

            // Priority 5: General cancel (close drawer, etc.)
            Logger.Log("[RIGHT-CLICK-OVERLAY] General cancel");
            _ui.Cancel();
            DrawUI();
        }

        // Left-click on the map: handle placement modes or open tile info panel
        private void OnMapLeftClickedLocal(Point local)
        {
            if (_mapSurface == null) return;
            // Map-local to world
            int worldX = _cameraPos.X + (local.X / _zoomLevel);
            int worldY = _cameraPos.Y + (local.Y / _zoomLevel);
            int maxPos = FortressSize * 32 - 1;
            if (worldX < 0 || worldY < 0 || worldX > maxPos || worldY > maxPos) return;

            Point worldPos = new Point(worldX, worldY);

            // Handle debug spawn
            if (_ui.DebugOpen && _world != null)
            {
                Logger.Log($"[DEBUG] Debug menu open, tab={_ui.DebugMenuTab}, world={_world != null}");

                if (_ui.DebugMenuTab == 1) // Creatures tab
                {
                    Logger.Log($"[DEBUG] Attempting creature spawn: id={_ui.DebugSelectedCreature}, pos=({worldX},{worldY},{_currentZ})");
                    Logger.Log($"[DEBUG] Creature definitions count: {_world.Creatures.DefinitionCount}");

                    var guid = _world.Creatures.SpawnCreature(_ui.DebugSelectedCreature, worldPos, _currentZ, "player", _uiTick);
                    if (guid.HasValue)
                    {
                        Logger.Log($"[DEBUG] SUCCESS: Spawned creature '{_ui.DebugSelectedCreature}' guid={guid} at ({worldX},{worldY},{_currentZ})");
                        _ui.AddToast($"Spawned {_ui.DebugSelectedCreature}", _uiTick + 100);
                    }
                    else
                    {
                        Logger.Log($"[DEBUG] FAILED: Could not spawn creature at ({worldX},{worldY},{_currentZ})");
                        _ui.AddToast("Spawn failed - check log", _uiTick + 100);
                    }
                    DrawUI();
                    return;
                }
                else if (_ui.DebugMenuTab == 2) // Items tab
                {
                    Logger.Log($"[DEBUG] Attempting item spawn: id={_ui.DebugSelectedItem}, pos=({worldX},{worldY},{_currentZ})");
                    Logger.Log($"[DEBUG] Item definitions count: {_world.Items.DefinitionCount}");

                    var guid = _world.Items.SpawnItem(_ui.DebugSelectedItem, worldPos, _currentZ, 1, _uiTick);
                    if (guid.HasValue)
                    {
                        Logger.Log($"[DEBUG] SUCCESS: Spawned item '{_ui.DebugSelectedItem}' guid={guid} at ({worldX},{worldY},{_currentZ})");
                        _ui.AddToast($"Spawned {_ui.DebugSelectedItem}", _uiTick + 100);
                    }
                    else
                    {
                        Logger.Log($"[DEBUG] FAILED: Could not spawn item at ({worldX},{worldY},{_currentZ})");
                        _ui.AddToast("Spawn failed - check log", _uiTick + 100);
                    }
                    DrawUI();
                    return;
                }
            }

            // Handle placement modes
            if (_ui.Context == UiContext.PlacingTool)
            {
                if (_ui.PlaceMode == PlacementMode.StockpileFirstCorner)
                {
                    _ui.PlaceFirstCorner = worldPos;
                    _ui.PlaceMode = PlacementMode.StockpileSecondCorner;
                    Logger.Log($"[STOCKPILE] First corner at ({worldX},{worldY},{_currentZ})");
                    DrawUI();
                    return;
                }
                else if (_ui.PlaceMode == PlacementMode.StockpileSecondCorner)
                {
                    if (_ui.PlaceFirstCorner.HasValue && worldPos != _ui.PlaceFirstCorner.Value)
                    {
                        _ui.PlaceSecondCorner = worldPos;
                        _ui.PlaceMode = PlacementMode.StockpilePresetSelect;
                        Logger.Log($"[STOCKPILE] Second corner at ({worldX},{worldY},{_currentZ})");
                        DrawUI();
                    }
                    return;
                }
                else if (_ui.PlaceMode == PlacementMode.HaulFirstCorner)
                {
                    _ui.PlaceFirstCorner = worldPos;
                    _ui.PlaceMode = PlacementMode.HaulSecondCorner;
                    Logger.Log($"[HAUL] First corner at ({worldX},{worldY},{_currentZ})");
                    DrawUI();
                    return;
                }
                else if (_ui.PlaceMode == PlacementMode.HaulSecondCorner)
                {
                    if (_ui.PlaceFirstCorner.HasValue && worldPos != _ui.PlaceFirstCorner.Value)
                    {
                        _ui.PlaceSecondCorner = worldPos;

                        // Compute rectangle and enqueue haul order command
                        int minX = Math.Min(_ui.PlaceFirstCorner.Value.X, _ui.PlaceSecondCorner.Value.X);
                        int maxX = Math.Max(_ui.PlaceFirstCorner.Value.X, _ui.PlaceSecondCorner.Value.X);
                        int minY = Math.Min(_ui.PlaceFirstCorner.Value.Y, _ui.PlaceSecondCorner.Value.Y);
                        int maxY = Math.Max(_ui.PlaceFirstCorner.Value.Y, _ui.PlaceSecondCorner.Value.Y);
                        var rect = new SadRogue.Primitives.Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
                        var cmd = new HumanFortress.App.Commands.CreateHaulOrderCommand(
                            GameStateManager.Instance.TickScheduler.CurrentTick, rect, _currentZ, priority: 50);
                        GameStateManager.Instance.EnqueueCommand(cmd);
                        _ui.AddToast("Haul order created", _uiTick + 120);
                        Logger.Log($"[HAUL] Rect=({rect.X},{rect.Y},{rect.Width}x{rect.Height}) z={_currentZ}");
                        _ui.CancelPlacement();
                        DrawUI();
                    }
                    return;
                }
                else if (_ui.PlaceMode == PlacementMode.MiningFirstCorner)
                {
                    _ui.PlaceFirstCorner = worldPos;
                    _ui.PlaceMode = PlacementMode.MiningSecondCorner;
                    Logger.Log($"[MINING] First corner at ({worldX},{worldY},{_currentZ})");
                    DrawUI();
                    return;
                }
                else if (_ui.PlaceMode == PlacementMode.MiningSecondCorner)
                {
                    if (_ui.PlaceFirstCorner.HasValue && worldPos != _ui.PlaceFirstCorner.Value)
                    {
                        _ui.PlaceSecondCorner = worldPos;
                        int minX = Math.Min(_ui.PlaceFirstCorner.Value.X, _ui.PlaceSecondCorner.Value.X);
                        int maxX = Math.Max(_ui.PlaceFirstCorner.Value.X, _ui.PlaceSecondCorner.Value.X);
                        int minY = Math.Min(_ui.PlaceFirstCorner.Value.Y, _ui.PlaceSecondCorner.Value.Y);
                        int maxY = Math.Max(_ui.PlaceFirstCorner.Value.Y, _ui.PlaceSecondCorner.Value.Y);
                        var rect = new SadRogue.Primitives.Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
                        var cmd = new HumanFortress.App.Commands.CreateMiningOrderCommand(
                            GameStateManager.Instance.TickScheduler.CurrentTick, rect, _currentZ, priority: 50);
                        GameStateManager.Instance.EnqueueCommand(cmd);
                        _ui.AddToast("Mining order created", _uiTick + 120);
                        Logger.Log($"[MINING] Rect=({rect.X},{rect.Y},{rect.Width}x{rect.Height}) z={_currentZ}");
                        _ui.CancelPlacement();
                        DrawUI();
                    }
                    return;
                }
                else if (_ui.PlaceMode == PlacementMode.ZoneFirstCorner)
                {
                    _ui.PlaceFirstCorner = worldPos;
                    _ui.PlaceMode = PlacementMode.ZoneSecondCorner;
                    _ui.AddToast("Select second corner", _uiTick + 100);
                    DrawUI();
                    return;
                }
                else if (_ui.PlaceMode == PlacementMode.ZoneSecondCorner && _ui.PlaceFirstCorner.HasValue)
                {
                    // Create zone using command
                    var rect = SadRogue.Primitives.Rectangle.GetUnion(
                        new SadRogue.Primitives.Rectangle(_ui.PlaceFirstCorner.Value, 1, 1),
                        new SadRogue.Primitives.Rectangle(worldPos, 1, 1));

                    if (_ui.SelectedZoneDefId != null && _world != null)
                    {
                        var cmd = new HumanFortress.App.Commands.CreateZoneCommand(
                            GameStateManager.Instance.TickScheduler.CurrentTick,
                            _ui.SelectedZoneDefId,
                            $"{_ui.SelectedZoneDefId}_zone",
                            rect,
                            _currentZ);

                        GameStateManager.Instance.EnqueueCommand(cmd);
                        _ui.AddToast($"Created zone at ({rect.X},{rect.Y})", _uiTick + 150);
                    }

                    _ui.CancelPlacement();
                    DrawUI();
                    return;
                }
                else if (_ui.PlaceMode == PlacementMode.ZoneDelete)
                {
                    // Get zone at clicked position
                    int zoneId = _world?.Zones.GetZoneAtPosition(worldPos.X, worldPos.Y, _currentZ) ?? 0;
                    if (zoneId > 0)
                    {
                        var cmd = new HumanFortress.App.Commands.DeleteZoneCommand(
                            GameStateManager.Instance.TickScheduler.CurrentTick,
                            zoneId);

                        GameStateManager.Instance.EnqueueCommand(cmd);
                        _ui.AddToast($"Deleted zone #{zoneId}", _uiTick + 150);
                    }
                    else
                    {
                        _ui.AddToast("No zone at this location", _uiTick + 100);
                    }

                    DrawUI();
                    return;
                }
                else if (_ui.PlaceMode == PlacementMode.StockpileCopy)
                {
                    // Check if clicking on a stockpile
                    if (_stockpileUI != null && _world != null && _stockpileUI.HandleStockpileClick(worldPos, _currentZ, _world))
                    {
                        _ui.AddToast("Stockpile settings copied", _uiTick + 150);
                        _ui.CancelPlacement();
                        DrawUI();
                    }
                    return;
                }
            }
            // Check if clicking on a stockpile for editing
            else if (_stockpileUI != null && _world != null && _stockpileUI.HandleStockpileClick(worldPos, _currentZ, _world))
            {
                DrawUI();
                return;
            }

            // In normal mode, clicking a zone cell opens detail popup
            if (_ui.Context == UiContext.Global && _ui.QuickMenu == QuickMenuKind.Zones)
            {
                int zoneId = _world?.Zones.GetZoneAtPosition(worldPos.X, worldPos.Y, _currentZ) ?? 0;
                if (zoneId > 0)
                {
                    _zonesUI?.OpenDetailPopup(zoneId);
                    DrawUI();
                    return;
                }
            }

            _tilePanelWorld = worldPos;
            _tilePanelZ = _currentZ;
            _tilePanelOpen = true;
            Logger.Log($"[CLICK] Open TilePanel at world=({_tilePanelWorld.X},{_tilePanelWorld.Y},{_tilePanelZ})");

            // Dump TILE INFO (L0..L7) to log for debugging
            try
            {
                if (_world != null)
                {
                    int chunkX = _tilePanelWorld.X / 32;
                    int chunkY = _tilePanelWorld.Y / 32;
                    int localX = _tilePanelWorld.X % 32;
                    int localY = _tilePanelWorld.Y % 32;
                    var key = new HumanFortress.Simulation.World.ChunkKey(chunkX, chunkY, _tilePanelZ);
                    var simChunk = _world.GetChunk(key);
                    if (simChunk != null)
                    {
                        var tile = simChunk.GetTile(localX, localY);
                        // WorldMap geology id from generator
                        string geoIdWorld = _fortressMap?.GetChunk(chunkX, chunkY)?.GetGeologyId(localX, localY, _tilePanelZ) ?? "?";
                        Logger.Log($"[TILE] L0 geology={geoIdWorld} kind={tile.Kind} nat={(tile.IsNatural?1:0)} mod={(tile.IsModifiable?1:0)}");
                        Logger.Log($"[TILE] L1 surface mud={tile.HasMud} grass={tile.HasGrass} snow={tile.HasSnow} fert={tile.Fertility}");
                        Logger.Log($"[TILE] L3 fluid kind={tile.FluidKind} depth={tile.FluidDepth}");
                        Logger.Log($"[TILE] L7 meta revealed={tile.IsRevealed} forbid={tile.IsForbidden} traffic={tile.TrafficLevel} blood={tile.HasBlood}");
                    }
                }
            }
            catch { }
            DrawUI();
        }

                private void CreateStockpile(string presetId)
        {
            if (_stockpileManager == null || !_ui.PlaceFirstCorner.HasValue || !_ui.PlaceSecondCorner.HasValue)
                return;

            var corner1 = _ui.PlaceFirstCorner.Value;
            var corner2 = _ui.PlaceSecondCorner.Value;

            // Create zone
            var zoneId = _stockpileManager.CreateZone($"Stockpile {_stockpileManager.GetAllZones().Count() + 1}",
                new HumanFortress.Simulation.World.ChunkKey(corner1.X / 32, corner1.Y / 32, _currentZ), _uiTick);

            // Calculate rectangle bounds
            int minX = Math.Min(corner1.X, corner2.X);
            int maxX = Math.Max(corner1.X, corner2.X);
            int minY = Math.Min(corner1.Y, corner2.Y);
            int maxY = Math.Max(corner1.Y, corner2.Y);

            // Group cells by chunk; skip invalid tiles or overlap
            var cellsByChunk = new Dictionary<HumanFortress.Simulation.World.ChunkKey, List<int>>();
            int skippedInvalid = 0;
            int skippedOverlap = 0;
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    int chunkX = x / 32;
                    int chunkY = y / 32;
                    int localX = x % 32;
                    int localY = y % 32;
                    var chunkKey = new HumanFortress.Simulation.World.ChunkKey(chunkX, chunkY, _currentZ);

                    if (_world != null)
                    {
                        var chunk = _world.GetChunk(chunkKey);
                        if (chunk != null)
                        {
                            var tile = chunk.GetTile(localX, localY);
                            if (tile.Kind != HumanFortress.Simulation.Tiles.TerrainKind.OpenWithFloor)
                            {
                                skippedInvalid++;
                                continue; // Only create stockpiles on OpenWithFloor
                            }

                            var stockpileData = chunk.GetStockpileData();
                            if (stockpileData != null)
                            {
                                int cellIndex = localY * 32 + localX;
                                if (stockpileData.GetZoneAtCell(cellIndex) != 0)
                                {
                                    skippedOverlap++;
                                    continue; // Skip cells already in a stockpile
                                }
                            }
                        }
                    }

                    if (!cellsByChunk.ContainsKey(chunkKey))
                        cellsByChunk[chunkKey] = new List<int>();

                    cellsByChunk[chunkKey].Add(localY * 32 + localX);
                }
            }

            // Apply to per-chunk data
            foreach (var kvp in cellsByChunk)
            {
                if (_world != null)
                {
                    var chunk = _world.GetChunk(kvp.Key);
                    if (chunk != null)
                    {
                        chunk.EnsureStockpileData();
                        var stockpileData = chunk.GetStockpileData();
                        if (stockpileData != null)
                        {
                            stockpileData.CreateOrUpdateShard(zoneId, kvp.Key);
                            stockpileData.AddCellsToZone(zoneId, kvp.Value);
                        }
                    }
                }
            }

            // Update zone membership for overlay rendering
            _stockpileManager.GetZone(zoneId)?.UpdateMemberChunks(cellsByChunk.Keys);

            var totalCells = cellsByChunk.Values.Sum(list => list.Count);
            var skipMessage = "";
            if (skippedInvalid > 0) skipMessage += $"{skippedInvalid} invalid, ";
            if (skippedOverlap > 0) skipMessage += $"{skippedOverlap} overlap, ";
            if (!string.IsNullOrEmpty(skipMessage))
                _ui.AddToast($"Created stockpile #{zoneId} ({totalCells} tiles, {skipMessage.TrimEnd(',', ' ')} skipped)", _uiTick + 150);
            else
                _ui.AddToast($"Created stockpile #{zoneId} ({totalCells} tiles)", _uiTick + 150);
            Logger.Log($"[STOCKPILE] Created zone {zoneId} with {totalCells} cells");
        }// Draw tile info as a floating popup showing TileBase layers L0..L7 at the selected Z
        private void DrawTilePopup(ScreenSurface overlay)
        {
            if (_fortressMap == null || _world == null) return;
            var surf = overlay.Surface;
            int w = 42;
            int h = 28;
            int x0 = surf.Width - w - 2;
            int y0 = 2;
            var bg = new Color(10, 10, 10, 220);

            for (int yy = y0; yy < y0 + h; yy++)
                for (int xx = x0; xx < x0 + w; xx++)
                    surf.SetGlyph(xx, yy, ' ', Color.White, bg);

            surf.Print(x0 + 2, y0, "=== TILE INFO ===", Color.Cyan);
            surf.Print(x0 + 2, y0 + 1, $"Pos: ({_tilePanelWorld.X},{_tilePanelWorld.Y},{_tilePanelZ})", Color.White);

            int chunkX = _tilePanelWorld.X / 32;
            int chunkY = _tilePanelWorld.Y / 32;
            int localX = _tilePanelWorld.X % 32;
            int localY = _tilePanelWorld.Y % 32;

            var key = new HumanFortress.Simulation.World.ChunkKey(chunkX, chunkY, _tilePanelZ);
            var simChunk = _world.GetChunk(key);
            if (simChunk != null)
            {
                var tile = simChunk.GetTile(localX, localY);
                var geology = ContentRegistry.Instance.GetGeologyByHandle(tile.GeoMatId);
                string geoId = geology?.Id ?? $"#${tile.GeoMatId}";

                int line = 3;

                // === TERRAIN ===
                surf.Print(x0 + 2, line++, "--- Terrain ---", Color.Yellow);
                surf.Print(x0 + 2, line++, $"Kind: {tile.Kind}", tile.Kind == HumanFortress.Simulation.Tiles.TerrainKind.OpenWithFloor ? Color.Green : Color.White);
                surf.Print(x0 + 2, line++, $"Geology: {geoId.Replace("core_geology_", "").Replace("core_terrain_", "")}", Color.Gray);
                surf.Print(x0 + 2, line++, $"Natural: {tile.IsNatural}  Modifiable: {tile.IsModifiable}", Color.DarkGray);
                line++;

                // === SURFACE ===
                surf.Print(x0 + 2, line++, "--- Surface ---", Color.Yellow);
                surf.Print(x0 + 2, line++, $"Mud: {tile.HasMud}  Grass: {tile.HasGrass}  Snow: {tile.HasSnow}", Color.Gray);
                surf.Print(x0 + 2, line++, $"Fertility: {tile.Fertility}", Color.DarkGray);
                line++;

                // === ITEMS (L5) ===
                surf.Print(x0 + 2, line++, "--- Items ---", Color.Yellow);
                var items = _world.Items.GetAllInstances()
                    .Where(i => i.Position.X == _tilePanelWorld.X && i.Position.Y == _tilePanelWorld.Y && i.Z == _tilePanelZ)
                    .Take(5)
                    .ToList();
                if (items.Count > 0)
                {
                    foreach (var item in items)
                    {
                        var def = _world.Items.GetDefinition(item.DefinitionId);
                        string itemName = def?.Name ?? item.DefinitionId;
                        surf.Print(x0 + 2, line++, $"  {itemName} x{item.StackCount}", Color.LightGreen);
                    }
                    if (items.Count > 5)
                        surf.Print(x0 + 2, line++, $"  ... +{items.Count - 5} more", Color.DarkGray);
                }
                else
                {
                    surf.Print(x0 + 2, line++, "  (none)", Color.DarkGray);
                }
                line++;

                // === CREATURES (L6) ===
                surf.Print(x0 + 2, line++, "--- Creatures ---", Color.Yellow);
                var creatures = _world.Creatures.GetAllInstances()
                    .Where(c => c.Position.X == _tilePanelWorld.X && c.Position.Y == _tilePanelWorld.Y && c.Z == _tilePanelZ)
                    .Take(3)
                    .ToList();
                if (creatures.Count > 0)
                {
                    foreach (var creature in creatures)
                    {
                        var def = _world.Creatures.GetDefinition(creature.DefinitionId);
                        string name = def?.Name ?? creature.DefinitionId;
                        surf.Print(x0 + 2, line++, $"  {name} HP:{creature.HP}/{creature.MaxHP}", Color.LightBlue);
                    }
                }
                else
                {
                    surf.Print(x0 + 2, line++, "  (none)", Color.DarkGray);
                }
                line++;

                // === FLUIDS (L3) ===
                surf.Print(x0 + 2, line++, "--- Fluids ---", Color.Yellow);
                surf.Print(x0 + 2, line++, $"Kind: {tile.FluidKind}  Depth: {tile.FluidDepth}", Color.Gray);
            }

            surf.Print(x0 + 2, y0 + h - 1, "ESC to close", Color.DarkGray);
        }

        // Handle Orders menu input (L2 and L3)
        private void HandleOrdersMenu(Keyboard keyboard, ref bool changed)
        {
            if (_ui.OrdersMenu == OrdersSubmenu.None)
            {
                // L2 menu: select submenu
                if (keyboard.IsKeyPressed(Keys.Z)) { _ui.OpenOrdersSubmenu(OrdersSubmenu.Mining); changed = true; }
                else if (keyboard.IsKeyPressed(Keys.X)) { _ui.OpenOrdersSubmenu(OrdersSubmenu.Lumbering); changed = true; }
                else if (keyboard.IsKeyPressed(Keys.C)) { _ui.OpenOrdersSubmenu(OrdersSubmenu.Gather); changed = true; }
                else if (keyboard.IsKeyPressed(Keys.V)) { _ui.OpenOrdersSubmenu(OrdersSubmenu.Masonry); changed = true; }
                else if (keyboard.IsKeyPressed(Keys.F)) { _ui.OpenOrdersSubmenu(OrdersSubmenu.Haul); changed = true; }
                else if (keyboard.IsKeyPressed(Keys.B)) { _ui.OpenOrdersSubmenu(OrdersSubmenu.Creature); changed = true; }
                else if (keyboard.IsKeyPressed(Keys.G)) { _ui.OpenOrdersSubmenu(OrdersSubmenu.Other); changed = true; }
            }
            else
            {
                // L3 menu: handle specific submenu actions
                switch (_ui.OrdersMenu)
                {
                    case OrdersSubmenu.Mining:
                        if (keyboard.IsKeyPressed(Keys.Z)) { _ui.StartPlacement(PlacementMode.MiningFirstCorner, _currentZ); changed = true; }
                        else if (keyboard.IsKeyPressed(Keys.X)) { _ui.AddToast("Dig stairwell: WIP", _uiTick + 120); changed = true; }
                        else if (keyboard.IsKeyPressed(Keys.C)) { _ui.AddToast("Dig ramp: WIP", _uiTick + 120); changed = true; }
                        else if (keyboard.IsKeyPressed(Keys.V)) { _ui.AddToast("Dig channel: WIP", _uiTick + 120); changed = true; }
                        else if (keyboard.IsKeyPressed(Keys.F)) { _ui.AddToast("Remove digging: WIP", _uiTick + 120); changed = true; }
                        else if (keyboard.IsKeyPressed(Keys.OemComma)) { _ui.CancelPlacement(); changed = true; }
                        break;
                    case OrdersSubmenu.Lumbering:
                        if (keyboard.IsKeyPressed(Keys.Z)) { _ui.AddToast("Lumber: WIP", _uiTick + 120); changed = true; }
                        else if (keyboard.IsKeyPressed(Keys.OemComma)) { _ui.CancelPlacement(); changed = true; }
                        break;
                    case OrdersSubmenu.Gather:
                        if (keyboard.IsKeyPressed(Keys.Z)) { _ui.AddToast("Gather plant: WIP", _uiTick + 120); changed = true; }
                        else if (keyboard.IsKeyPressed(Keys.X)) { _ui.AddToast("Remove plant: WIP", _uiTick + 120); changed = true; }
                        else if (keyboard.IsKeyPressed(Keys.OemComma)) { _ui.CancelPlacement(); changed = true; }
                        break;
                    case OrdersSubmenu.Masonry:
                        if (keyboard.IsKeyPressed(Keys.Z)) { _ui.AddToast("Smooth: WIP", _uiTick + 120); changed = true; }
                        else if (keyboard.IsKeyPressed(Keys.X)) { _ui.AddToast("Engrave: WIP", _uiTick + 120); changed = true; }
                        else if (keyboard.IsKeyPressed(Keys.C)) { _ui.AddToast("Track: WIP", _uiTick + 120); changed = true; }
                        else if (keyboard.IsKeyPressed(Keys.V)) { _ui.AddToast("Carve gap: WIP", _uiTick + 120); changed = true; }
                        else if (keyboard.IsKeyPressed(Keys.OemComma)) { _ui.CancelPlacement(); changed = true; }
                        break;
                    case OrdersSubmenu.Haul:
                        if (keyboard.IsKeyPressed(Keys.Z)) { _ui.StartPlacement(PlacementMode.HaulFirstCorner, _currentZ); changed = true; }
                        else if (keyboard.IsKeyPressed(Keys.X)) { _ui.AddToast("Emergency haul: WIP", _uiTick + 120); changed = true; }
                        else if (keyboard.IsKeyPressed(Keys.OemComma)) { _ui.CancelPlacement(); changed = true; }
                        break;
                    case OrdersSubmenu.Creature:
                        if (keyboard.IsKeyPressed(Keys.Z)) { _ui.AddToast("Hunting: WIP", _uiTick + 120); changed = true; }
                        else if (keyboard.IsKeyPressed(Keys.X)) { _ui.AddToast("Kill: WIP", _uiTick + 120); changed = true; }
                        else if (keyboard.IsKeyPressed(Keys.C)) { _ui.AddToast("Tame: WIP", _uiTick + 120); changed = true; }
                        else if (keyboard.IsKeyPressed(Keys.V)) { _ui.AddToast("Rescue: WIP", _uiTick + 120); changed = true; }
                        else if (keyboard.IsKeyPressed(Keys.OemComma)) { _ui.CancelPlacement(); changed = true; }
                        break;
                    case OrdersSubmenu.Other:
                        if (keyboard.IsKeyPressed(Keys.Z)) { _ui.AddToast("Lock/disallow: WIP", _uiTick + 120); changed = true; }
                        else if (keyboard.IsKeyPressed(Keys.X)) { _ui.AddToast("Unlock/allow: WIP", _uiTick + 120); changed = true; }
                        else if (keyboard.IsKeyPressed(Keys.C)) { _ui.AddToast("Dump: WIP", _uiTick + 120); changed = true; }
                        else if (keyboard.IsKeyPressed(Keys.V)) { _ui.AddToast("Remove dump: WIP", _uiTick + 120); changed = true; }
                        else if (keyboard.IsKeyPressed(Keys.F)) { _ui.AddToast("Melt: WIP", _uiTick + 120); changed = true; }
                        else if (keyboard.IsKeyPressed(Keys.T)) { _ui.AddToast("Remove melt: WIP", _uiTick + 120); changed = true; }
                        else if (keyboard.IsKeyPressed(Keys.R)) { _ui.AddToast("Clean: WIP", _uiTick + 120); changed = true; }
                        else if (keyboard.IsKeyPressed(Keys.OemComma)) { _ui.CancelPlacement(); changed = true; }
                        break;
                }
            }
        }

        // Handle Zones menu input (L2 and L3)
        private void HandleZonesMenu(Keyboard keyboard, ref bool changed)
        {
            if (_ui.ZoneMenu == ZoneSubmenu.None)
            {
                // L2 menu: select submenu (Z=Production, X=Civil, C=Public, V=Military, F=Management)
                if (keyboard.IsKeyPressed(Keys.Z)) { _ui.OpenZoneSubmenu(ZoneSubmenu.Production); changed = true; }
                else if (keyboard.IsKeyPressed(Keys.X)) { _ui.OpenZoneSubmenu(ZoneSubmenu.Civil); changed = true; }
                else if (keyboard.IsKeyPressed(Keys.C)) { _ui.OpenZoneSubmenu(ZoneSubmenu.Public); changed = true; }
                else if (keyboard.IsKeyPressed(Keys.V)) { _ui.OpenZoneSubmenu(ZoneSubmenu.Military); changed = true; }
                else if (keyboard.IsKeyPressed(Keys.F)) { _ui.OpenZoneSubmenu(ZoneSubmenu.Management); changed = true; }
            }
            else
            {
                // L3 menu: handle zone creation
                char[] zoneKeys = { 'z', 'x', 'c', 'v', 'f', 'g', 'r', 't' };
                foreach (var c in zoneKeys)
                {
                    if (keyboard.IsKeyPressed((Keys)char.ToUpper(c)))
                    {
                        var defId = _zonesUI?.GetZoneDefIdFromKey(_ui.ZoneMenu, c);
                        if (defId != null)
                        {
                            _ui.SelectedZoneDefId = defId;
                            _ui.StartPlacement(PlacementMode.ZoneFirstCorner, _currentZ);
                            _ui.AddToast($"Placing {defId} zone - select first corner", _uiTick + 150);
                            changed = true;
                        }
                        break;
                    }
                }

                // Remove zone mode
                if (keyboard.IsKeyPressed(Keys.OemComma))
                {
                    _ui.StartPlacement(PlacementMode.ZoneDelete, _currentZ);
                    _ui.AddToast("Click zone to delete", _uiTick + 150);
                    changed = true;
                }
            }
        }

        // Handle Build menu input (L2 only, no L3 for now)
        private void HandleBuildMenu(Keyboard keyboard, ref bool changed)
        {
            if (_ui.BuildMenu == BuildSubmenu.None)
            {
                // L2 menu: select submenu
                if (keyboard.IsKeyPressed(Keys.Z)) { _ui.OpenBuildSubmenu(BuildSubmenu.Structural); changed = true; }
                else if (keyboard.IsKeyPressed(Keys.X)) { _ui.OpenBuildSubmenu(BuildSubmenu.FunctionalStructure); changed = true; }
                else if (keyboard.IsKeyPressed(Keys.C)) { _ui.OpenBuildSubmenu(BuildSubmenu.Workshop); changed = true; }
                else if (keyboard.IsKeyPressed(Keys.V)) { _ui.OpenBuildSubmenu(BuildSubmenu.CivilFurniture); changed = true; }
                else if (keyboard.IsKeyPressed(Keys.F)) { _ui.OpenBuildSubmenu(BuildSubmenu.UtilityFurniture); changed = true; }
            }
            else
            {
                // L2 selected but no L3 yet - show WIP toast for any key
                if (keyboard.IsKeyPressed(Keys.Z) || keyboard.IsKeyPressed(Keys.X) || keyboard.IsKeyPressed(Keys.C) ||
                    keyboard.IsKeyPressed(Keys.V) || keyboard.IsKeyPressed(Keys.F) || keyboard.IsKeyPressed(Keys.G) ||
                    keyboard.IsKeyPressed(Keys.R) || keyboard.IsKeyPressed(Keys.T) || keyboard.IsKeyPressed(Keys.OemComma))
                {
                    _ui.AddToast("Build feature: WIP", _uiTick + 120);
                    changed = true;
                }
            }
        }

        // Handle Stockpile menu input
        private void HandleStockpileMenu(Keyboard keyboard, ref bool changed)
        {
            if (_ui.StockpileMenu == StockpileSubmenu.None)
            {
                // L2 menu
                if (keyboard.IsKeyPressed(Keys.Z)) { _ui.OpenStockpileSubmenu(StockpileSubmenu.Stockpile); changed = true; }
                else if (keyboard.IsKeyPressed(Keys.X)) { _ui.AddToast("Garbage dump: WIP", _uiTick + 120); changed = true; }
                else if (keyboard.IsKeyPressed(Keys.OemComma)) { _ui.AddToast("Remove zone: WIP", _uiTick + 120); changed = true; }
            }
            else
            {
                // L3 menu: Stockpile submenu
                if (keyboard.IsKeyPressed(Keys.Z))
                {
                    _ui.StartPlacement(PlacementMode.StockpileFirstCorner, _currentZ);
                    changed = true;
                }
                else if (keyboard.IsKeyPressed(Keys.OemComma))
                {
                    _ui.AddToast("Remove stockpile: WIP", _uiTick + 120);
                    changed = true;
                }
            }
        }
    }
}

