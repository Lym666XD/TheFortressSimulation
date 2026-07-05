using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal sealed partial class FortressInputContextFactory
{
    public FortressKeyboardInputRouterContext CreateKeyboard()
    {
        return new FortressKeyboardInputRouterContext(
            _keyboardRuntime,
            _ui,
            _uiTickProvider(),
            _viewport.Capture(),
            _loadedSession.NavigationOverlay,
            _loadedSession.UiServices,
            _view.SelectionTool,
            _navigationDebug,
            _tileInspection.IsOpen,
            _hideTilePanel,
            presetId => FortressPlacementRouter.CreateStockpile(CreatePlacement(), presetId));
    }

    public FortressMouseInputRouterContext CreateMouse()
    {
        return new FortressMouseInputRouterContext(
            _view.MapSurface,
            _view.UiSurface,
            _loadedSession.HasFortressMap,
            _loadedSession.UiServices,
            _ui,
            _viewport.CurrentZ,
            _uiTickProvider(),
            _tileInspection.IsOpen,
            _ensureFocus,
            _applyMouseHover,
            _hideTilePanel,
            _redrawAfterInput,
            _drawUi,
            _mapLeftClick);
    }

    public FortressOverlayClickContext CreateOverlayClick()
    {
        return new FortressOverlayClickContext(
            _ui,
            _view.UiWidthOr(0),
            _view.UiHeightOr(0),
            _view.HasMapSurface,
            _view.MapPositionOr(new Point(0, 0)),
            _view.MapWidthOr(0),
            _view.MapHeightOr(0),
            _loadedSession.UiServices,
            _viewport.Capture(),
            _uiTickProvider(),
            _tileInspection.IsOpen,
            _view.SelectionTool,
            _hideTilePanel,
            _drawUi,
            _mapLeftClick);
    }
}
