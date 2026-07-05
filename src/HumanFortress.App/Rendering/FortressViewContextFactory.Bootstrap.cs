using HumanFortress.App.UI;

namespace HumanFortress.App.Rendering;

internal sealed partial class FortressViewContextFactory
{
    public FortressViewBootstrapContext CreateBootstrap()
    {
        return new FortressViewBootstrapContext(
            new FortressUiInteractionDataSource(
                _runtime.UiInput.GetDebugMenuData,
                _runtime.UiInput.GetWorkforceInputData,
                _runtime.UiInput.SetProfessionWeight),
            _ui,
            _fortressSizeProvider() * 32,
            _uiTickProvider,
            _onMapMouseMoved,
            _onMapLeftClicked,
            _onOverlayLeftClicked,
            _onOverlayRightClicked,
            _onOverlayMouseMoved,
            _redraw);
    }
}
