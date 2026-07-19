namespace HumanFortress.App.Rendering;

internal sealed partial class FortressViewContextFactory
{
    public FortressFrameRenderContext CreateFrame()
    {
        return new FortressFrameRenderContext(
            _view.MapSurface,
            _view.UiSurface,
            _ui,
            _runtime,
            _diagnostics,
            _loadedSession.Capture(),
            _viewport.Capture(),
            _view.MapViewportPresenter,
            _view.UiOverlayPresenter,
            _fortressSizeProvider(),
            _uiTickProvider(),
            _tileInspection);
    }
}
