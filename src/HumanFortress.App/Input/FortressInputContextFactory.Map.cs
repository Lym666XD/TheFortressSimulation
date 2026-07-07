namespace HumanFortress.App.Input;

internal sealed partial class FortressInputContextFactory
{
    public FortressMouseHoverControllerContext CreateMouseHover()
    {
        return new FortressMouseHoverControllerContext(
            _view,
            _view.MapSurface,
            _viewport.Capture(),
            _fortressSizeProvider(),
            _view.SelectionTool,
            _uiTickProvider());
    }

    public FortressMapInteractionContext CreateMapInteraction()
    {
        return new FortressMapInteractionContext(
            _view.HasMapSurface,
            _ui,
            _viewport.Capture(),
            _fortressSizeProvider(),
            CreateDebugSpawn(),
            CreateMapClick(),
            CreatePlacement());
    }

    private FortressPlacementRouterContext CreatePlacement()
    {
        return new FortressPlacementRouterContext(
            _ui,
            _mapRuntime.Placement,
            _loadedSession.UiServices,
            _view.SelectionTool,
            _fortressSizeProvider(),
            _viewport.CurrentZ,
            _uiTickProvider(),
            _drawUi);
    }

    private FortressDebugSpawnContext CreateDebugSpawn()
    {
        return new FortressDebugSpawnContext(
            _ui,
            _mapRuntime.DebugSpawn,
            _viewport.CurrentZ,
            _uiTickProvider(),
            _drawUi);
    }

    private FortressMapClickControllerContext CreateMapClick()
    {
        return new FortressMapClickControllerContext(
            _ui,
            _mapRuntime.MapInspection,
            _loadedSession.UiServices,
            _viewport.CurrentZ,
            _uiTickProvider(),
            _mapRuntime.MapInspection.GetWorkshopDebugData(),
            _tileInspection.Open,
            _drawUi);
    }
}
