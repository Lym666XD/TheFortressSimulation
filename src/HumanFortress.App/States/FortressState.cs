using System;
using SadConsole;
using SadConsole.Input;
using SadRogue.Primitives;
using HumanFortress.Core.World;
using HumanFortress.App.Runtime;
using HumanFortress.App.Rendering;
using HumanFortress.App.UI;

namespace HumanFortress.App.States
{
    public class FortressState : ScreenObject
    {
        private bool _initialized = false;
        private readonly FortressViewState _view = new();
        private readonly FortressViewportState _viewport = new();
        private readonly FortressLoadedSessionState _loadedSession = new();
        private readonly FortressNavigationDebugController _navigationDebug = new();
        private UiStore _ui = new UiStore();
        private ulong _uiTick = 0;
        private readonly FortressTileInspectionController _tileInspection = new();
        private readonly FortressRuntimeAccess _runtime;
        private readonly FortressSessionContext _session;

        private Point EmbarkLocation => _session.EmbarkLocation;
        private int FortressSize => _session.FortressSize;

        public FortressState(FortressRuntimeAccess runtime, FortressSessionContext session)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            Logger.Log("[FortressState] Constructor called - deferred initialization");
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

            FortressConstructionHighlightDiagnostics.Log(_ui, _uiTick);
            EnsureFocused();

            if (!_initialized && GameHost.Instance != null)
            {
                Initialize();
            }

            FortressMouseWheelPoller.Poll(ApplyMouseWheelInput);
            EnsureFocused();
            DrawUI();
        }

        private void EnsureFocused()
        {
            if (!IsFocused)
                IsFocused = true;
        }

        private bool ApplyMouseWheelInput(int scrollWheelValueChange, Keyboard? keyboard)
        {
            var wheel = FortressMouseWheelInput.Handle(
                scrollWheelValueChange,
                keyboard,
                _ui,
                _view.SelectionTool,
                _viewport.ZoomLevel,
                _viewport.CurrentZ);

            if (!wheel.Changed)
                return false;

            _viewport.ApplyWheel(wheel);
            ClampCameraToWorld();
            RefreshSnapshot();
            return true;
        }

        private void Initialize()
        {
            try
            {
                Logger.Log("[FortressState] Initialize started");

                var gameHost = GameHost.Instance;
                if (gameHost == null)
                {
                    Logger.Log("[FortressState] ERROR: GameHost.Instance is null!");
                    return; // Defer initialization
                }

                Logger.Log($"[FortressState] GameHost screen size: {gameHost.ScreenCellsX}x{gameHost.ScreenCellsY}");

                var bootstrappedView = FortressViewBootstrapper.Create(this, gameHost, CreateViewBootstrapContext());
                _view.Apply(bootstrappedView);

                Logger.Log($"[FortressState] FortressSize = {FortressSize}");
                if (FortressSize <= 0)
                {
                    Logger.Log("[FortressState] WARNING: FortressSize is invalid, using default 2");
                }
                InitializeCameraAndCursor();

                GenerateFortressMap();
                DrawUI();

                _initialized = true;
                Logger.Log("[FortressState] Initialize completed successfully");
            }
            catch (Exception ex)
            {
                Logger.Error("UI.FortressState", $"[FortressState] ERROR in Initialize: {ex.Message}", ex);
                throw;
            }
        }

        private FortressViewBootstrapContext CreateViewBootstrapContext()
        {
            return new FortressViewBootstrapContext(
                _runtime,
                _ui,
                FortressSize * 32,
                () => _uiTick,
                () => _loadedSession.World,
                OnMapMouseMovedLocal,
                OnMapLeftClickedLocal,
                OnOverlayLeftClickedLocal,
                OnOverlayRightClickedLocal,
                OnOverlayMouseMovedLocal,
                DrawUI);
        }

        private void InitializeCameraAndCursor()
        {
            _viewport.Initialize(FortressSize);
            Logger.Log($"[FortressState] Camera position set to {_viewport.CameraPosition}, cursor at {_viewport.CursorPosition}");
        }
        
        private void GenerateFortressMap()
        {
            var loaded = new FortressSessionLoader(
                _runtime,
                _session,
                _ui,
                () => _uiTick,
                HumanFortress.App.Input.InputBindingsService.Instance,
                HumanFortress.App.Input.OrdersRegistryService.Instance,
                AppContext.BaseDirectory).Load(_viewport.CurrentZ);

            _loadedSession.Apply(loaded);

            if (_loadedSession.World == null || loaded.UsedFallbackWorld)
                return;

            Logger.Log("[GenerateFortressMap] Building initial snapshot");
            RefreshSnapshot();

            Logger.Log($"[GenerateFortressMap] SUCCESS: Generated fortress map: {FortressSize}x{FortressSize} chunks at {EmbarkLocation}");
            if (loaded.WorldTile.HasValue)
            {
                var worldTile = loaded.WorldTile.Value;
                Logger.Log($"[GenerateFortressMap] Biome: {(BiomeType)worldTile.BiomeId}, Elevation: {worldTile.Elevation:F2}");
            }
        }
        
        private void DrawUI()
        {
            FortressFrameRenderer.Render(new FortressFrameRenderContext(
                _view.MapSurface,
                _view.UiSurface,
                _ui,
                _runtime,
                _loadedSession.Capture(),
                _viewport.Capture(),
                FortressSize,
                _uiTick,
                _tileInspection));
        }

        // Handle overlay local left-clicks for F1-F8 and Z/X/C/V buttons
        private void OnOverlayLeftClickedLocal(Point local)
        {
            if (_view.UiSurface == null) return;
            FortressOverlayClickController.HandleLeftClick(CreateOverlayClickContext(), local);
        }

        private void HideTilePanel()
        {
            _tileInspection.Hide();
        }

        private void RefreshSnapshot()
        {
            _loadedSession.RefreshSnapshot(
                _viewport.CameraPosition,
                _viewport.CurrentZ,
                _view.MapWidthOr(80),
                _view.MapHeightOr(40));
        }

        public override bool ProcessKeyboard(Keyboard keyboard)
        {
            var result = FortressKeyboardInputRouter.Process(CreateKeyboardInputRouterContext(), keyboard);
            _viewport.ApplyKeyboard(result);

            if (result.ShouldRedraw)
                RedrawAfterInput();

            return result.Handled;
        }

        private FortressKeyboardInputRouterContext CreateKeyboardInputRouterContext()
        {
            return new FortressKeyboardInputRouterContext(
                _runtime,
                _ui,
                _uiTick,
                _viewport.Capture(),
                _loadedSession.Capture(),
                _view.SelectionTool,
                _navigationDebug,
                _tileInspection.IsOpen,
                HideTilePanel,
                guid => FortressWorkshopPanelContextResolver.Resolve(_loadedSession.World, guid),
                presetId => FortressPlacementRouter.CreateStockpile(CreatePlacementRouterContext(), presetId));
        }

        private void RedrawAfterInput()
        {
            ClampCameraToWorld();
            RefreshSnapshot();
            DrawUI();
        }

        public override bool ProcessMouse(MouseScreenObjectState state)
        {
            var result = FortressMouseInputRouter.Process(CreateMouseInputRouterContext(), state);
            return result.ShouldCallBase ? base.ProcessMouse(state) : result.Handled;
        }

        private FortressMouseInputRouterContext CreateMouseInputRouterContext()
        {
            return new FortressMouseInputRouterContext(
                _view.MapSurface,
                _view.UiSurface,
                _loadedSession.Capture(),
                _ui,
                _viewport.CurrentZ,
                _uiTick,
                _tileInspection.IsOpen,
                () => IsFocused = true,
                ApplyMouseHover,
                HideTilePanel,
                RedrawAfterInput,
                DrawUI,
                OnMapLeftClickedLocal);
        }

        private void ClampCameraToWorld()
        {
            _viewport.ClampCamera(
                FortressSize,
                _view.MapWidthOr(80),
                _view.MapHeightOr(40));
        }

        // _uiTick is advanced in the existing Update at the top of this class

        // Enhanced map-surface mouse handlers (for robust hover tracking)
        private void OnMapMouseMovedLocal(Point local)
        {
            ApplyMouseHover(local, updateSelection: true, logMapEvent: true);
        }

        // Overlay mouse move: update cursor when overlay sits on top of map
        private void OnOverlayMouseMovedLocal(Point local)
        {
            var hover = FortressMouseHoverController.ApplyOverlayHover(CreateMouseHoverControllerContext(), local);
            _viewport.ApplyHover(hover);
        }

        // Handle right-click on overlay: hierarchical back navigation
        private void OnOverlayRightClickedLocal(Point local)
        {
            if (_view.UiSurface == null) return;
            FortressOverlayClickController.HandleRightClick(CreateOverlayClickContext(), local);
        }

        private FortressOverlayClickContext CreateOverlayClickContext()
        {
            return new FortressOverlayClickContext(
                _ui,
                _view.UiWidthOr(0),
                _view.UiHeightOr(0),
                _view.HasMapSurface,
                _view.MapPositionOr(new Point(0, 0)),
                _view.MapWidthOr(0),
                _view.MapHeightOr(0),
                _loadedSession.Capture(),
                _viewport.Capture(),
                _uiTick,
                _tileInspection.IsOpen,
                _view.SelectionTool,
                HideTilePanel,
                DrawUI,
                OnMapLeftClickedLocal);
        }

        private bool ApplyMouseHover(Point mapLocal, bool updateSelection, bool logMapEvent)
        {
            var hover = FortressMouseHoverController.Apply(
                CreateMouseHoverControllerContext(),
                mapLocal,
                updateSelection,
                logMapEvent);

            _viewport.ApplyHover(hover);
            return hover.Changed;
        }

        private FortressMouseHoverControllerContext CreateMouseHoverControllerContext()
        {
            return new FortressMouseHoverControllerContext(
                _view,
                _view.MapSurface,
                _viewport.Capture(),
                FortressSize,
                _view.SelectionTool,
                _uiTick);
        }

        // Left-click on the map: handle placement modes or open tile info panel
        private void OnMapLeftClickedLocal(Point local)
        {
            FortressMapInteractionController.HandleLeftClick(CreateMapInteractionContext(), local);
        }

        private FortressMapInteractionContext CreateMapInteractionContext()
        {
            return new FortressMapInteractionContext(
                _view.HasMapSurface,
                _ui,
                _viewport.Capture(),
                FortressSize,
                CreateDebugSpawnContext(),
                CreateMapClickControllerContext(),
                CreatePlacementRouterContext());
        }

        private FortressPlacementRouterContext CreatePlacementRouterContext()
        {
            return new FortressPlacementRouterContext(
                _ui,
                _runtime,
                _loadedSession.Capture(),
                _view.SelectionTool,
                FortressSize,
                _viewport.CurrentZ,
                _uiTick,
                DrawUI);
        }

        private FortressDebugSpawnContext CreateDebugSpawnContext()
        {
            return new FortressDebugSpawnContext(
                _ui,
                _runtime,
                _loadedSession.Capture(),
                _viewport.CurrentZ,
                _uiTick,
                DrawUI);
        }

        private FortressMapClickControllerContext CreateMapClickControllerContext()
        {
            return new FortressMapClickControllerContext(
                _ui,
                _loadedSession.Capture(),
                _viewport.CurrentZ,
                _uiTick,
                _tileInspection.Open,
                DrawUI);
        }

    }
}
