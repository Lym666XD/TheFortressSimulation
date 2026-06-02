using System;
using SadConsole;
using SadConsole.Input;
using SadRogue.Primitives;
using HumanFortress.App.Commands;
using HumanFortress.Simulation.Placeables;
using HumanFortress.Core.World;
using HumanFortress.Simulation.World;
using HumanFortress.Simulation.Rendering;
using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.Orders;
using HumanFortress.App.Runtime;
using HumanFortress.WorldGen;
using HumanFortress.App.Rendering;
using HumanFortress.App.UI;
using HumanFortress.App.UI.Selection;
using HumanFortress.Navigation;
using HumanFortress.Simulation.Stockpile;

namespace HumanFortress.App.States
{
    public class FortressState : ScreenObject
    {
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
        private bool _overlayFromSnapshot = false; // optional path for placeables overlay
        private int _zoomLevel = 1; // 1 = normal, 2 = zoomed in, etc.
        private Point? _lastMousePos;
        private NavigationOverlay? _navOverlay;
        private NavigationManager? _navManager;
        private HumanFortress.Navigation.WorldNavigationView? _navView;
        private UiStore _ui = new UiStore();
        private ulong _uiTick = 0;
        private bool _cameraFollowCursor = false; // camera follows mouse only when true
        private bool _tilePanelOpen = false;
        private readonly FortressRuntimeAccess _runtime;
        private readonly FortressSessionContext _session;
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
        // New: world-screen mapper and drag selection tool for low-coupling selection handling
        private IWorldCoordinateMapper? _coordMapper;
        private ISelectionTool? _selectionTool;

        private Point EmbarkLocation => _session.EmbarkLocation;
        private int FortressSize => _session.FortressSize;

        public FortressState(FortressRuntimeAccess runtime, FortressSessionContext session)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _session = session ?? throw new ArgumentNullException(nameof(session));
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

            // Periodic highlight stats for construction preview/debug
            if ((_uiTick % 30UL) == 0UL)
            {
                try
                {
                    var count = _ui.GetHighlights().Count;
                    Logger.Log($"[BUILD.UI] highlight active={count} placeMode={_ui.PlaceMode}");
                }
                catch { }
            }

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
                    bool shiftHeld = keyboard != null && (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift));
                    // Selection tool: Shift+wheel adjusts z-range of active selection (non-destructive to zoom/Z)
                    if (_selectionTool != null && _selectionTool.IsActive && shiftHeld)
                    {
                        int zDelta = mouse.ScrollWheelValueChange > 0 ? 1 : -1;
                        _selectionTool.AdjustZRange(zDelta);
                        Logger.Log($"[SELECT] z-range {(zDelta>0?"+":"-")}{Math.Abs(zDelta)}");
                    }
                    else
                    {
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

                            // Update mining z-range if in MiningSecondCorner mode
                            if (_ui.PlaceMode == PlacementMode.MiningSecondCorner)
                            {
                                _ui.PlaceZMax = _currentZ;
                                // Also update selection tool z-range (without Shift key requirement)
                                if (_selectionTool != null && _selectionTool.IsActive)
                                {
                                    // Directly adjust selectionTool's z-range by delta
                                    _selectionTool.AdjustZRange(deltaWheel);
                                    Logger.Log($"[ZLEVEL-UPDATE] Mining mode: updated PlaceZMax={_currentZ} and selectionTool z-range");
                                }
                                else
                                {
                                    Logger.Log($"[ZLEVEL-UPDATE] Mining mode: updated PlaceZMax={_currentZ}");
                                }
                            }

                            Logger.Log($"[ZLEVEL-UPDATE] delta={deltaWheel} -> Z={_currentZ}");
                        }
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

                var gameHost = GameHost.Instance;
                if (gameHost == null)
                {
                    System.Console.WriteLine("[FortressState] ERROR: GameHost.Instance is null!");
                    return; // Defer initialization
                }

                System.Console.WriteLine($"[FortressState] GameHost screen size: {gameHost.ScreenCellsX}x{gameHost.ScreenCellsY}");

                var layout = FortressScreenLayoutFactory.Create(gameHost);
                _mapSurface = layout.MapSurface;
                _uiSurface = layout.UiSurface;
                _infoPanel = layout.InfoPanel;
                _tileInfoPanel = layout.TileInfoPanel;

                var interaction = FortressInteractionBootstrapper.Configure(
                    layout,
                    _runtime,
                    _ui,
                    FortressSize * 32,
                    () => _uiTick,
                    () => _world,
                    OnMapMouseMovedLocal,
                    OnMapLeftClickedLocal,
                    OnOverlayLeftClickedLocal,
                    OnOverlayRightClickedLocal,
                    OnOverlayMouseMovedLocal,
                    DrawUI
                );
                _coordMapper = interaction.CoordinateMapper;
                _selectionTool = interaction.SelectionTool;

                // Add root as the only child
                Children.Add(layout.RootSurface);
                System.Console.WriteLine("[FortressState] UI hierarchy established");

                // Make this ScreenObject focusable
                IsFocused = true;
                UseKeyboard = true;
                UseMouse = true;  // Enable mouse for scroll and hover

                System.Console.WriteLine($"[FortressState] FortressSize = {FortressSize}");
                if (FortressSize <= 0)
                {
                    System.Console.WriteLine("[FortressState] WARNING: FortressSize is invalid, using default 2");
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
                var initialization = new FortressSessionInitializer(_runtime, _session).Initialize();
                var world = initialization.World;
                _world = world;
                _fortressMap = initialization.FortressMap;
                _snapshotBuilder = initialization.SnapshotBuilder;
                _navManager = initialization.NavigationManager;

                if (world == null || initialization.UsedFallbackWorld)
                    return;

                var bindings = FortressSessionRuntimeBootstrapper.Configure(
                    world,
                    _navManager,
                    _runtime,
                    _ui,
                    () => _uiTick,
                    _session.AutoDig,
                    _currentZ,
                    _bindings,
                    _ordersRegistry,
                    AppContext.BaseDirectory);
                _navOverlay = bindings.NavigationOverlay;
                _overlayFromSnapshot = bindings.OverlayFromSnapshot;
                _stockpileManager = bindings.UiServices.StockpileManager;
                _stockpileUI = bindings.UiServices.StockpileUI;
                _ordersUI = bindings.UiServices.OrdersUI;
                _zonesUI = bindings.UiServices.ZonesUI;
                _buildUI = bindings.UiServices.BuildUI;
                _stockpileQuickUI = bindings.UiServices.StockpileQuickUI;

                // Build initial snapshot
                System.Console.WriteLine("[GenerateFortressMap] Building initial snapshot");
                RefreshSnapshot();

                System.Console.WriteLine($"[GenerateFortressMap] SUCCESS: Generated fortress map: {FortressSize}x{FortressSize} chunks at {EmbarkLocation}");
                if (initialization.WorldTile.HasValue)
                {
                    var worldTile = initialization.WorldTile.Value;
                    System.Console.WriteLine($"[GenerateFortressMap] Biome: {(BiomeType)worldTile.BiomeId}, Elevation: {worldTile.Elevation:F2}");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[GenerateFortressMap] ERROR: {ex.Message}");
                System.Console.WriteLine($"[GenerateFortressMap] Stack trace: {ex.StackTrace}");

                // Use runtime World even on error (it has loaded definitions)
                System.Console.WriteLine("[GenerateFortressMap] Using runtime World despite error");
                _world = _runtime.World;
            }
        }
        
        private void DrawUI()
        {
            if (_uiSurface == null || _mapSurface == null) return;
            if (_infoPanel != null) _infoPanel.Clear();
            // Right-side map & status info removed per request; moved into Debug menu.

            // Build tile info on demand; draw as overlay popup
            FortressMapRenderer.Render(
                _mapSurface,
                _fortressMap,
                _world,
                FortressSize,
                _cameraPos,
                _cursorPos,
                _currentZ,
                _zoomLevel,
                _ui.Context,
                _navOverlay);

            FortressUiOverlayRenderer.Render(new FortressUiOverlayRenderContext(
                _uiSurface,
                _mapSurface,
                _ui,
                _runtime,
                _world,
                _stockpileManager,
                _stockpileUI,
                _ordersUI,
                _zonesUI,
                _buildUI,
                _stockpileQuickUI,
                _currentSnapshot,
                _overlayFromSnapshot,
                _cameraPos,
                _cursorPos,
                _lastMousePos,
                _currentZ,
                _zoomLevel,
                FortressSize,
                _uiTick));

            if (_tilePanelOpen)
            {
                FortressTilePopupRenderer.Render(_uiSurface, _fortressMap, _world, _tilePanelWorld, _tilePanelZ);
            }
        }

        // Handle overlay local left-clicks for F1-F8 and Z/X/C/V buttons
        private void OnOverlayLeftClickedLocal(Point local)
        {
            if (_uiSurface == null) return;

            // Swallow overlay clicks when global modal is open
            if (_ui.ConstructionMaterialDialogOpen)
            {
                return;
            }

            int surfaceWidth = _uiSurface.Surface.Width;
            int surfaceHeight = _uiSurface.Surface.Height;

            if (_ui.DebugOpen)
            {
                if (FortressOverlayMouseInput.IsInsideDebugWindow(local, surfaceWidth, surfaceHeight))
                    return;
            }

            if (FortressOverlayMouseInput.TryHandleDockClick(local, surfaceHeight, _ui, HideTilePanel))
            {
                DrawUI();
                return;
            }

            if (FortressOverlayMouseInput.TryHandleQuickClick(local, surfaceWidth, surfaceHeight, _ui, HideTilePanel))
            {
                DrawUI();
                return;
            }

            if (FortressOverlayMouseInput.TryHandleDebugSpawnClick(local, surfaceWidth, surfaceHeight, _ui, _cursorPos, _currentZ, _uiTick))
            {
                DrawUI();
                return;
            }

            // Pass-through: if click not on overlay controls, treat as map click using lastMousePos for consistent world snapping
            if (_mapSurface != null)
            {
                var mapLocal = new Point(local.X - _mapSurface.Position.X, local.Y - _mapSurface.Position.Y);
                if (mapLocal.X >= 0 && mapLocal.X < _mapSurface.Surface.Width && mapLocal.Y >= 0 && mapLocal.Y < _mapSurface.Surface.Height)
                {
                    // Prefer using lastMousePos rather than recomputing coordinates to avoid off-by-one
                    if (_lastMousePos.HasValue)
                    {
                        var worldPos = _lastMousePos.Value;
                        // Synthesize a local that maps to this world pos to reuse the same handler code path
                        var fakeLocal = new Point((worldPos.X - _cameraPos.X) * _zoomLevel, (worldPos.Y - _cameraPos.Y) * _zoomLevel);
                        OnMapLeftClickedLocal(fakeLocal);
                    }
                    else
                    {
                        OnMapLeftClickedLocal(mapLocal);
                    }
                    return;
                }
            }
        }

        private void UpdateTileInfo()
        {
            FortressTileInfoPanelRenderer.Render(
                _tileInfoPanel,
                _fortressMap,
                _world,
                _tilePanelOpen,
                _tilePanelWorld,
                _tilePanelZ,
                FortressSize);
        }

        private void HideTilePanel()
        {
            _tilePanelOpen = false;
            _tileInfoPanel?.Clear();
        }

        private (HumanFortress.Simulation.Placeables.PlaceableInstance placeable, HumanFortress.Simulation.World.Chunk chunk)? FindWorkshopAt(Point worldPos, int z)
        {
            var reg = HumanFortress.Core.Content.Registry.ConstructionRegistry.Instance;
            foreach (var chunk in _world!.GetAllChunks())
            {
                var pd = chunk.GetPlaceableData();
                if (pd == null) continue;
                foreach (var p in pd.GetAllOwnedPlaceables())
                {
                    if (p.Z != z) continue;
                    var def = reg.GetConstruction(p.DefinitionId);
                    if (def == null) continue;
                    if (!string.Equals(def.Category, "workshop", StringComparison.OrdinalIgnoreCase) &&
                        (def.PlaceableProfile.Tags == null || Array.IndexOf(def.PlaceableProfile.Tags, "workshop") < 0))
                        continue;
                    var fp = p.Footprint;
                    int x0 = p.Position.X;
                    int y0 = p.Position.Y;
                    if (worldPos.X >= x0 && worldPos.X < x0 + fp.W && worldPos.Y >= y0 && worldPos.Y < y0 + fp.D)
                    {
                        return (p, chunk);
                    }
                }
        }
            return null;
        }

        private FortressWorkshopPanelContext? FindWorkshopPanelContext(Guid guid)
        {
            if (_world == null) return null;

            var reg = HumanFortress.Core.Content.Registry.ConstructionRegistry.Instance;
            foreach (var chunk in _world.GetAllChunks())
            {
                var pd = chunk.GetPlaceableData();
                if (pd == null) continue;
                foreach (var p in pd.GetAllOwnedPlaceables())
                {
                    if (p.Guid != guid) continue;
                    var def = reg.GetConstruction(p.DefinitionId);
                    if (p.Workshop == null)
                    {
                        p.Workshop = new WorkshopState();
                        int max = Math.Max(1, def?.Io?.InputSlots ?? 1);
                        p.Workshop.ConfigureWorkers(1, max);
                    }
                    return new FortressWorkshopPanelContext(p.Workshop, def?.Id);
                }
            }
            return null;
        }

        // Count valid mining cells in selection for UI-side precheck (avoid empty orders)
        private int CountValidMiningCells(HumanFortress.Simulation.World.World world, SadRogue.Primitives.Rectangle rect, int zMin, int zMax, HumanFortress.Simulation.Orders.MiningAction action)
        {
            return HumanFortress.Simulation.Orders.MiningOrderRules.CountEligible(world, rect, zMin, zMax, action);
        }

        // Single source-of-truth rectangle helper: returns an inclusive rectangle from two corners
        private Rectangle ComputeRectInclusive(Point a, Point b)
        {
            int x = Math.Min(a.X, b.X);
            int y = Math.Min(a.Y, b.Y);
            int w = Math.Abs(a.X - b.X) + 1;
            int h = Math.Abs(a.Y - b.Y) + 1;
            return new Rectangle(x, y, w, h);
        }

        private Point ClampToWorld(Point p)
        {
            int max = FortressSize * 32 - 1;
            int cx = p.X < 0 ? 0 : (p.X > max ? max : p.X);
            int cy = p.Y < 0 ? 0 : (p.Y > max ? max : p.Y);
            return new Point(cx, cy);
        }

        private void RefreshSnapshot()
        {
            _currentSnapshot = FortressRenderSnapshotService.Build(
                _snapshotBuilder,
                _world,
                _cameraPos,
                _currentZ,
                _mapSurface?.Surface.Width ?? 80,
                _mapSurface?.Surface.Height ?? 40);
        }

        public override bool ProcessKeyboard(Keyboard keyboard)
        {
            bool changed = false;

            if (FortressWorkshopPanelKeyboardInput.Handle(keyboard, _runtime, _ui, _uiTick, FindWorkshopPanelContext))
            {
                UpdateCameraToFollowCursor();
                RefreshSnapshot();
                DrawUI();
                return true;
            }

            // Global modal: construction material dialog handling (swallows other inputs)
            if (_ui.ConstructionMaterialDialogOpen)
            {
                changed = FortressConstructionMaterialDialogInput.Handle(keyboard, _ui, _currentZ, _uiTick);

                if (changed)
                {
                    UpdateCameraToFollowCursor();
                    RefreshSnapshot();
                    DrawUI();
                }
                return true; // swallow inputs while modal is open
            }

            var navigationInput = FortressKeyboardNavigationInput.Handle(keyboard, _cameraPos, _currentZ, _selectionTool);
            _cameraPos = navigationInput.CameraPosition;
            _currentZ = navigationInput.CurrentZ;
            changed |= navigationInput.Changed;

            changed |= FortressSimulationKeyboardInput.Handle(keyboard, _runtime, _ui, _uiTick);

            changed |= FortressGlobalUiKeyboardInput.HandleHelpAndDebug(keyboard, _ui);

            if (FortressGlobalUiKeyboardInput.TryHandleDrawerShortcut(keyboard, _ui, HideTilePanel))
            {
                changed = true;
            }
            else if (keyboard.IsKeyPressed(Keys.F9))
            {
                CycleNavigationOverlay();
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
                changed |= FortressDebugMenuInput.Handle(keyboard, _ui);
            }

            // Handle context-specific keys first
            // Orders Quick Menu
            else if (_ui.Context == UiContext.QuickMenu && _ui.QuickMenu == QuickMenuKind.Orders)
            {
                changed |= FortressOrdersKeyboardInput.Handle(keyboard, _ui, _currentZ, _uiTick);
            }
            // Zones Quick Menu
            else if (_ui.Context == UiContext.QuickMenu && _ui.QuickMenu == QuickMenuKind.Zones)
            {
                changed |= FortressZonesKeyboardInput.Handle(keyboard, _ui, _zonesUI, _currentZ, _uiTick);
            }
            // Build Quick Menu
            else if (_ui.Context == UiContext.QuickMenu && _ui.QuickMenu == QuickMenuKind.Build)
            {
                changed |= FortressBuildKeyboardInput.Handle(keyboard, _ui, _currentZ, _uiTick);
            }
            // Stockpile Quick Menu
            else if (_ui.Context == UiContext.QuickMenu && _ui.QuickMenu == QuickMenuKind.Stockpile)
            {
                changed |= FortressStockpileKeyboardInput.Handle(keyboard, _ui, _currentZ, _uiTick);
            }
            else if (_ui.Context == UiContext.PlacingTool)
            {
                changed |= FortressStockpilePresetKeyboardInput.Handle(keyboard, _ui, _stockpileUI, CreateStockpile);
            }
            // UI: quick menus Z/X/C/V (only in global context) - close tile panel when opening
            else if (_ui.Context == UiContext.Global)
            {
                changed |= FortressGlobalUiKeyboardInput.HandleGlobalQuickMenus(keyboard, _ui, HideTilePanel);
            }

            changed |= FortressGlobalUiKeyboardInput.HandleDrawerTabs(keyboard, _ui);

            changed |= FortressEscapeKeyboardInput.Handle(keyboard, _ui, _tilePanelOpen, _stockpileUI, HideTilePanel);

            // Final redraw after handling all UI keys (ensures ESC/Debug/help reflect immediately)
            if (changed)
            {
                UpdateCameraToFollowCursor();
                RefreshSnapshot();
                DrawUI();
            }

            return true;
        }

        private void CycleNavigationOverlay()
        {
            if (_navOverlay == null)
            {
                _ui.AddToast("Overlay: unavailable", _uiTick + 150);
                return;
            }

            _navOverlay.CycleMode();
            if (_navOverlay.CurrentMode == NavigationOverlay.OverlayMode.FlowField)
                _navOverlay.SetTarget(_cursorPos);
            _ui.AddToast($"Overlay: {_navOverlay.CurrentMode}", _uiTick + 150);
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

            // Swallow mouse interactions when global modal is open
            if (_ui.ConstructionMaterialDialogOpen)
            {
                return true;
            }

            // Ensure keyboard focus stays on FortressState while handling mouse
            if (!IsFocused) IsFocused = true;

            // Keep keyboard focus on this state so panels remain interactive after clicks
            this.IsFocused = true;

            // Get mouse position relative to map surface
            var mousePos = new Point(state.SurfaceCellPosition.X - _mapSurface.Position.X,
                                     state.SurfaceCellPosition.Y - _mapSurface.Position.Y);

            var hover = FortressMouseHoverInput.Handle(
                mousePos,
                _mapSurface.Surface.Width,
                _mapSurface.Surface.Height,
                _cameraPos,
                _zoomLevel,
                FortressSize,
                _currentZ,
                _lastMousePos,
                _cursorPos);
            _lastMousePos = hover.LastMousePosition;
            _cursorPos = hover.CursorPosition;
            changed |= hover.Changed;
            if (hover.Changed)
                UpdateTileInfo();

            var wheel = FortressMouseWheelInput.Handle(
                state.Mouse.ScrollWheelValueChange,
                GameHost.Instance?.Keyboard,
                _ui,
                _zoomLevel,
                _currentZ);
            _zoomLevel = wheel.ZoomLevel;
            _currentZ = wheel.CurrentZ;
            changed |= wheel.Changed;

            if (changed)
            {
                UpdateCameraToFollowCursor();
                RefreshSnapshot();
                DrawUI();
            }

            // First: screen-level dock buttons (bottom-left of console)
            if (state.Mouse.LeftClicked)
            {
                int screenWidth = GameHost.Instance?.ScreenCellsX ?? _uiSurface?.Surface.Width ?? _mapSurface.Surface.Width;
                int screenHeight = GameHost.Instance?.ScreenCellsY ?? _uiSurface?.Surface.Height ?? _mapSurface.Surface.Height;
                if (FortressScreenMouseInput.TryHandleDockClick(state.SurfaceCellPosition, screenHeight, _ui, HideTilePanel)
                    || FortressScreenMouseInput.TryHandleQuickIconClick(state.SurfaceCellPosition, screenWidth, screenHeight, _ui, HideTilePanel)
                    || FortressScreenMouseInput.TryHandleQuickMenuClick(state.SurfaceCellPosition, screenWidth, screenHeight, _ui, _currentZ, _uiTick))
                {
                    DrawUI();
                    return true;
                }
            }

            // Handle mouse clicks for UI (map-relative)
            if (state.Mouse.LeftClicked)
            {
                var cell = state.SurfaceCellPosition - _mapSurface.Position;
                if (cell.X >= 0 && cell.X < _mapSurface.Surface.Width && cell.Y >= 0 && cell.Y < _mapSurface.Surface.Height)
                {
                    if (_ui.Context == UiContext.Global && !_ui.WorkshopPanelOpen)
                    {
                        OnMapLeftClickedLocal(cell);
                    }
                    return true;
                }
            }

            if (state.Mouse.RightClicked)
            {
                FortressRightClickCancelInput.Handle(state.SurfaceCellPosition, _ui, _tilePanelOpen, _zonesUI, _stockpileUI, HideTilePanel);
                Logger.Log("[RIGHT-CLICK] Handled successfully, redrawing UI");
                DrawUI();
                return true;
            }

            return base.ProcessMouse(state);
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
            ApplyMouseHover(local, updateSelection: true, updateTileInfo: true, logMapEvent: true);
        }

        // Overlay mouse move: update cursor when overlay sits on top of map
        private void OnOverlayMouseMovedLocal(Point local)
        {
            if (_mapSurface == null || _uiSurface == null) return;
            var mapLocal = new Point(local.X - _mapSurface.Position.X, local.Y - _mapSurface.Position.Y);
            if (mapLocal.X < 0 || mapLocal.Y < 0 || mapLocal.X >= _mapSurface.Surface.Width || mapLocal.Y >= _mapSurface.Surface.Height)
                return;

            ApplyMouseHover(mapLocal, updateSelection: false, updateTileInfo: false, logMapEvent: false);
        }

        // Handle right-click on overlay: hierarchical back navigation
        private void OnOverlayRightClickedLocal(Point local)
        {
            // Swallow right-click when global modal is open
            if (_ui.ConstructionMaterialDialogOpen) return;
            Logger.Log($"[RIGHT-CLICK-OVERLAY] Clicked at local=({local.X},{local.Y}), tilePanelOpen={_tilePanelOpen}, QuickMenu={_ui.QuickMenu}, OrdersMenu={_ui.OrdersMenu}, ZoneMenu={_ui.ZoneMenu}");

            // Cancel active selection (if any)
            if (_selectionTool != null && _selectionTool.IsActive)
            {
                _selectionTool.Cancel();
                _ui.CancelPlacement();
                DrawUI();
                return;
            }

            FortressRightClickCancelInput.Handle(local, _ui, _tilePanelOpen, _zonesUI, _stockpileUI, HideTilePanel);
            DrawUI();
        }

        private void ApplyMouseHover(Point mapLocal, bool updateSelection, bool updateTileInfo, bool logMapEvent)
        {
            if (_mapSurface == null) return;

            var hover = FortressMouseHoverInput.Handle(
                mapLocal,
                _mapSurface.Surface.Width,
                _mapSurface.Surface.Height,
                _cameraPos,
                _zoomLevel,
                FortressSize,
                _currentZ,
                _lastMousePos,
                _cursorPos);

            _lastMousePos = hover.LastMousePosition;
            _cursorPos = hover.CursorPosition;
            if (!hover.Changed)
                return;

            if (updateSelection && _selectionTool != null && _selectionTool.IsActive)
            {
                _selectionTool.Update(_cursorPos);
            }

            if (logMapEvent && _uiTick % 10 == 0)
                Logger.Log($"[MOUSE-EVT] Hover world=({_cursorPos.X},{_cursorPos.Y},{_currentZ}) local=({mapLocal.X},{mapLocal.Y}) camera=({_cameraPos.X},{_cameraPos.Y}) zoom={_zoomLevel}");

            if (updateTileInfo)
                UpdateTileInfo();
        }

        // Left-click on the map: handle placement modes or open tile info panel
        private void OnMapLeftClickedLocal(Point local)
        {
            if (_mapSurface == null) return;
            if (_ui.SuppressNextTileClick)
            {
                _ui.SuppressNextTileClick = false;
                return;
            }
            if (!FortressMapClickInput.TryResolveWorldPosition(local, _cameraPos, _zoomLevel, FortressSize, _lastMousePos, out var worldPos))
                return;

            int worldX = worldPos.X; int worldY = worldPos.Y;

            if (TryHandleDebugSpawnClick(worldPos))
                return;

            if (TryHandleWorkshopClick(worldX, worldY, _currentZ))
            {
                return;
            }

            if (TryHandlePlacementClick(worldPos, worldX, worldY))
                return;

            // Check if clicking on a stockpile for editing
            if (_ui.Context != UiContext.PlacingTool && _stockpileUI != null && _world != null && _stockpileUI.HandleStockpileClick(worldPos, _currentZ, _world))
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

            // If clicking a workshop, open its panel
            if (_world != null)
            {
                var w = FindWorkshopAt(worldPos, _currentZ);
                if (w != null)
                {
                    _ui.OpenWorkshopPanel(w.Value.placeable.Guid, w.Value.placeable.Position, w.Value.placeable.Z);
                    _ui.AddToast("Workshop details", _uiTick + 120);
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

        private bool TryHandleDebugSpawnClick(Point worldPos)
        {
            if (!_ui.DebugOpen || _world is not { } world)
                return false;

            Logger.Log($"[DEBUG] Debug menu open, tab={_ui.DebugMenuTab}, world=true");

            if (_ui.DebugMenuTab == 1) // Creatures tab
            {
                Logger.Log($"[DEBUG] Attempting creature spawn: id={_ui.DebugSelectedCreature}, pos=({worldPos.X},{worldPos.Y},{_currentZ})");
                Logger.Log($"[DEBUG] Creature definitions count: {world.Creatures.DefinitionCount}");

                _runtime.EnqueueCurrentTickCommand(tick => new SpawnCreatureCommand(
                    tick,
                    _ui.DebugSelectedCreature,
                    worldPos,
                    _currentZ,
                    "player"));
                _ui.AddToast($"Spawn queued: {_ui.DebugSelectedCreature}", _uiTick + 100);
                DrawUI();
                return true;
            }

            if (_ui.DebugMenuTab == 2) // Items tab
            {
                Logger.Log($"[DEBUG] Attempting item spawn: id={_ui.DebugSelectedItem}, pos=({worldPos.X},{worldPos.Y},{_currentZ})");
                Logger.Log($"[DEBUG] Item definitions count: {world.Items.DefinitionCount}");

                _runtime.EnqueueCurrentTickCommand(tick => new SpawnItemCommand(
                    tick,
                    _ui.DebugSelectedItem,
                    worldPos,
                    _currentZ,
                    quantity: 1));
                _ui.AddToast($"Spawn queued: {_ui.DebugSelectedItem}", _uiTick + 100);
                DrawUI();
                return true;
            }

            return false;
        }

        private bool TryHandlePlacementClick(Point worldPos, int worldX, int worldY)
        {
            if (_ui.Context != UiContext.PlacingTool)
                return false;

            if (FortressPlacementClickInput.TryHandleFirstCorner(_ui, worldPos, _currentZ, FortressSize, _uiTick, _selectionTool))
            {
                DrawUI();
                return true;
            }

            if (_ui.PlaceMode == PlacementMode.StockpileSecondCorner)
            {
                if (_ui.PlaceFirstCorner.HasValue && worldPos != _ui.PlaceFirstCorner.Value)
                {
                    _ui.PlaceSecondCorner = worldPos;
                    _ui.PlaceMode = PlacementMode.StockpilePresetSelect;
                    Logger.Log($"[STOCKPILE] Second corner at ({worldX},{worldY},{_currentZ})");
                    DrawUI();
                }
                return true;
            }

            if (_ui.PlaceMode == PlacementMode.HaulSecondCorner)
            {
                if (_ui.PlaceFirstCorner.HasValue && worldPos != _ui.PlaceFirstCorner.Value)
                {
                    _ui.PlaceSecondCorner = worldPos;

                    // Compute inclusive rectangle using single helper
                    var rect = ComputeRectInclusive(_ui.PlaceFirstCorner.Value, _ui.PlaceSecondCorner.Value);
                    _runtime.EnqueueCurrentTickCommand(tick =>
                        new HumanFortress.App.Commands.CreateHaulOrderCommand(tick, rect, _currentZ, priority: 50));
                    _ui.AddToast("Haul order created", _uiTick + 120);
                    Logger.Log($"[UI] Select first=({_ui.PlaceFirstCorner.Value.X},{_ui.PlaceFirstCorner.Value.Y},{_currentZ}) second=({worldPos.X},{worldPos.Y},{_currentZ}) -> rect=({rect.X},{rect.Y},{rect.Width}x{rect.Height})");
                    _ui.CancelPlacement();
                    DrawUI();
                }
                return true;
            }

            if (_ui.PlaceMode == PlacementMode.MiningSecondCorner)
            {
                var cl = ClampToWorld(worldPos);
                if (_ui.PlaceFirstCorner.HasValue && cl != _ui.PlaceFirstCorner.Value)
                {
                    _ui.PlaceSecondCorner = cl;
                    _ui.PlaceZMax = _currentZ; // Save second click's Z as zMax (fallback when no z-adjust)
                    // Prefer selection tool result if active
                    Selection3D sel = default;
                    if (_selectionTool != null && _selectionTool.IsActive)
                    {
                        sel = _selectionTool.Complete();
                    }
                    // Compute inclusive rectangle using tool or helper
                    bool useTool = sel.XY.Width > 1 || sel.XY.Height > 1;
                    var rect = useTool
                        ? sel.XY
                        : ComputeRectInclusive(_ui.PlaceFirstCorner.Value, _ui.PlaceSecondCorner.Value);

                    // z-range: prefer tool's z when using tool; else fallback to UI fields
                    int zMin = useTool ? Math.Min(sel.ZMin, sel.ZMax) : Math.Min(_ui.PlaceZMin, _ui.PlaceZMax);
                    int zMax = useTool ? Math.Max(sel.ZMin, sel.ZMax) : Math.Max(_ui.PlaceZMin, _ui.PlaceZMax);
                    var uiAction = _ui.SelectedMiningAction;
                    var simAction = uiAction switch
                    {
                        HumanFortress.App.UI.MiningAction.Dig => HumanFortress.Simulation.Orders.MiningAction.Dig,
                        HumanFortress.App.UI.MiningAction.DigStairwell => HumanFortress.Simulation.Orders.MiningAction.DigStairwell,
                        HumanFortress.App.UI.MiningAction.DigRamp => HumanFortress.Simulation.Orders.MiningAction.DigRamp,
                        HumanFortress.App.UI.MiningAction.DigChannel => HumanFortress.Simulation.Orders.MiningAction.DigChannel,
                        HumanFortress.App.UI.MiningAction.RemoveDigging => HumanFortress.Simulation.Orders.MiningAction.RemoveDigging,
                        _ => HumanFortress.Simulation.Orders.MiningAction.Dig
                    };

                    // Single-layer stairwell validation: show toast but still enqueue (planner will skip)
                    if (simAction == HumanFortress.Simulation.Orders.MiningAction.DigStairwell && zMin == zMax)
                    {
                        _ui.AddToast($"Stairwell must dig between multiple levels", _uiTick + 180);
                        Logger.Log($"[UI] Single-layer stairwell rejected at UI (z={zMin})");
                    }

                    Logger.Log($"[DEBUG] Creating mining order command zMin={zMin} zMax={zMax} rect=({rect.X},{rect.Y},{rect.Width}x{rect.Height})");
                    _runtime.EnqueueCurrentTickCommand(tick => new CreateAdvancedMiningOrderCommand(
                        tick,
                        rect,
                        zMin,
                        zMax,
                        uiAction,
                        priority: 50));

                    int totalCells = rect.Width * rect.Height;
                    _ui.AddToast($"Mining order created ({totalCells} tiles)", _uiTick + 120);
                    Logger.Log($"[UI] Select first=({_ui.PlaceFirstCorner.Value.X},{_ui.PlaceFirstCorner.Value.Y},{_currentZ}) second=({worldPos.X},{worldPos.Y},{_currentZ}) -> rect=({rect.X},{rect.Y},{rect.Width}x{rect.Height})");
                    Logger.Log($"[MINING] UI enqueued action={simAction} rect=({rect.X},{rect.Y},{rect.Width}x{rect.Height}) z={zMin}..{zMax}");
                    // Highlight for shorter time; encode action in kind for renderer fill policy
                    _ui.AddHighlight($"mining:{uiAction}", rect, zMin, zMax, _uiTick + 30);
                    _ui.CancelPlacement();
                    DrawUI();
                }
                return true;
            }

            if (_ui.PlaceMode == PlacementMode.ConstructionSecondCorner && _ui.PlaceFirstCorner.HasValue)
            {
                var cl = ClampToWorld(worldPos);
                if (cl != _ui.PlaceFirstCorner.Value)
                {
                    var rect = ComputeRectInclusive(_ui.PlaceFirstCorner.Value, cl);
                    // Build on current Z only (multi-Z stairs WIP)
                    int zMin = _currentZ;
                    int zMax = _currentZ;

                    // Material filter: default to stone (granite) for L0 construction
                    var filter = new HumanFortress.Simulation.Orders.MaterialFilterSpec
                    {
                        PreferredMaterialId = _ui.ConstructionPreferredMaterialId ?? "core_mat_stone_granite",
                        CategoryKey = _ui.SelectedConstructionShape switch
                        {
                            HumanFortress.Simulation.Orders.ConstructionShape.Wall => "l0.wall",
                            HumanFortress.Simulation.Orders.ConstructionShape.Floor => "l0.floor",
                            HumanFortress.Simulation.Orders.ConstructionShape.Ramp => "l0.ramp",
                            HumanFortress.Simulation.Orders.ConstructionShape.Stairs => "l0.stairs",
                            _ => "l0.unknown"
                        },
                        Tags = _ui.ConstructionSelectedTags.ToArray()
                    };

                    // Enqueue construction command
                    _runtime.EnqueueCurrentTickCommand(tick =>
                        new HumanFortress.App.Commands.CreateConstructionOrderCommand(
                            tick,
                            rect,
                            zMin,
                            zMax,
                            _ui.SelectedConstructionShape,
                            filter,
                            priority: 50));

                    // Detailed UI submission log for selection debugging
                    Logger.Log($"[BUILD.UI] First=({_ui.PlaceFirstCorner.Value.X},{_ui.PlaceFirstCorner.Value.Y}) Second=({cl.X},{cl.Y}) Rect=({rect.X},{rect.Y},{rect.Width}x{rect.Height}) Z={zMin}..{zMax}");
                    Logger.Log($"[BUILD.UI] Enqueue construction shape={_ui.SelectedConstructionShape} rect=({rect.X},{rect.Y},{rect.Width}x{rect.Height}) z={zMin}..{zMax} tags=[{string.Join('|', _ui.ConstructionSelectedTags)}]");
                    _ui.AddToast($"[BUILD] Enqueued {_ui.SelectedConstructionShape} {rect.Width}x{rect.Height} at z={_currentZ}", _uiTick + 150);
                    _ui.CancelPlacement();
                    DrawUI();
                }
                return true;
            }

            if (_ui.PlaceMode == PlacementMode.BuildableConfirmAnchor && _ui.PlaceFirstCorner.HasValue)
            {
                var anchor = _ui.PlaceFirstCorner.Value;
                if (_ui.SelectedBuildableConstructionId != null)
                {
                    _runtime.EnqueueCurrentTickCommand(tick =>
                        new HumanFortress.App.Commands.CreateBuildableConstructionOrderCommand(
                            tick,
                            _ui.SelectedBuildableConstructionId,
                            anchor,
                            _currentZ,
                            priority: 50));
                    Logger.Log($"[BUILD.UI] Enqueue workshop id={_ui.SelectedBuildableConstructionId} pos=({anchor.X},{anchor.Y}) z={_currentZ}");
                    _ui.CancelPlacement();
                    DrawUI();
                }
                return true;
            }

            if (_ui.PlaceMode == PlacementMode.ZoneSecondCorner && _ui.PlaceFirstCorner.HasValue)
            {
                // Create zone using command (inclusive rectangle)
                int minX = Math.Min(_ui.PlaceFirstCorner.Value.X, worldPos.X);
                int minY = Math.Min(_ui.PlaceFirstCorner.Value.Y, worldPos.Y);
                int maxX = Math.Max(_ui.PlaceFirstCorner.Value.X, worldPos.X);
                int maxY = Math.Max(_ui.PlaceFirstCorner.Value.Y, worldPos.Y);
                var rect = new SadRogue.Primitives.Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);

                if (_ui.SelectedZoneDefId != null && _world != null)
                {
                    _runtime.EnqueueCurrentTickCommand(tick =>
                        new HumanFortress.App.Commands.CreateZoneCommand(
                            tick,
                            _ui.SelectedZoneDefId,
                            $"{_ui.SelectedZoneDefId}_zone",
                            rect,
                            _currentZ));
                    _ui.AddToast($"Created zone at ({rect.X},{rect.Y})", _uiTick + 150);
                }

                _ui.CancelPlacement();
                DrawUI();
                return true;
            }

            if (_ui.PlaceMode == PlacementMode.ZoneDelete)
            {
                // Get zone at clicked position
                int zoneId = _world?.Zones.GetZoneAtPosition(worldPos.X, worldPos.Y, _currentZ) ?? 0;
                if (zoneId > 0)
                {
                    _runtime.EnqueueCurrentTickCommand(tick =>
                        new HumanFortress.App.Commands.DeleteZoneCommand(tick, zoneId));
                    _ui.AddToast($"Deleted zone #{zoneId}", _uiTick + 150);
                }
                else
                {
                    _ui.AddToast("No zone at this location", _uiTick + 100);
                }

                DrawUI();
                return true;
            }

            if (_ui.PlaceMode == PlacementMode.StockpileCopy)
            {
                // Check if clicking on a stockpile
                if (_stockpileUI != null && _world != null && _stockpileUI.HandleStockpileClick(worldPos, _currentZ, _world))
                {
                    _ui.AddToast("Stockpile settings copied", _uiTick + 150);
                    _ui.CancelPlacement();
                    DrawUI();
                }
                return true;
            }

            return false;
        }

        private void CreateStockpile(string presetId)
        {
            if (_world == null || !_ui.PlaceFirstCorner.HasValue || !_ui.PlaceSecondCorner.HasValue)
                return;

            var corner1 = _ui.PlaceFirstCorner.Value;
            var corner2 = _ui.PlaceSecondCorner.Value;
            int minX = Math.Min(corner1.X, corner2.X);
            int maxX = Math.Max(corner1.X, corner2.X);
            int minY = Math.Min(corner1.Y, corner2.Y);
            int maxY = Math.Max(corner1.Y, corner2.Y);
            var rect = new SadRogue.Primitives.Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);

            _runtime.EnqueueCurrentTickCommand(tick =>
                new CreateStockpileCommand(tick, rect, _currentZ, presetId));

            int selectedCells = rect.Width * rect.Height;
            _ui.AddToast($"Stockpile order queued ({selectedCells} tiles)", _uiTick + 150);
            Logger.Log($"[STOCKPILE.UI] Enqueue preset={presetId} rect=({rect.X},{rect.Y},{rect.Width}x{rect.Height}) z={_currentZ}");
        }

        private bool TryHandleWorkshopClick(int worldX, int worldY, int worldZ)
        {
            if (_world == null) return false;
            int chunkX = worldX / HumanFortress.Simulation.World.Chunk.SIZE_XY;
            int chunkY = worldY / HumanFortress.Simulation.World.Chunk.SIZE_XY;
            int localX = worldX % HumanFortress.Simulation.World.Chunk.SIZE_XY;
            int localY = worldY % HumanFortress.Simulation.World.Chunk.SIZE_XY;
            var chunk = _world.GetChunk(new HumanFortress.Simulation.World.ChunkKey(chunkX, chunkY, worldZ));
            if (chunk == null) return false;
            var pd = chunk.GetPlaceableData();
            if (pd == null) return false;
            if (!pd.TryGetOwnedAt(HumanFortress.Simulation.World.Chunk.LocalIndex(localX, localY), out var placeable))
                return false;

            var registry = HumanFortress.Core.Content.Registry.ConstructionRegistry.Instance;
            string defId = placeable.ConstructionSite?.TargetId ?? placeable.DefinitionId;
            var def = registry.GetConstruction(defId);
            if (def == null) return false;
            bool isWorkshop = string.Equals(def.Category, "workshop", StringComparison.OrdinalIgnoreCase)
                || (def.PlaceableProfile.Tags != null && Array.IndexOf(def.PlaceableProfile.Tags, "workshop") >= 0);
            if (!isWorkshop) return false;

            _ui.OpenWorkshopPanel(placeable.Guid, new Point(placeable.Position.X, placeable.Position.Y), placeable.Z);
            DrawUI();
            return true;
        }

    }
}
