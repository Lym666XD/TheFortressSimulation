using HumanFortress.App.Input;
using HumanFortress.App.Rendering;
using HumanFortress.App.Session;
using HumanFortress.App.UI;

namespace HumanFortress.App.States;

internal static partial class FortressStateCompositionFactory
{
    private static FortressStateInputController CreateInputController(
        FortressInputRuntimePorts runtime,
        UiStore ui,
        FortressViewState view,
        FortressViewportState viewport,
        FortressLoadedSessionState loadedSession,
        FortressNavigationDebugController navigationDebug,
        FortressTileInspectionController tileInspection,
        Func<int> fortressSizeProvider,
        Func<ulong> uiTickProvider,
        Action ensureFocus,
        Action drawUi,
        FortressInputCallbackHub inputCallbacks)
    {
        var inputContexts = new FortressInputContextFactory(
            runtime,
            ui,
            view,
            viewport,
            loadedSession,
            navigationDebug,
            tileInspection,
            fortressSizeProvider,
            uiTickProvider,
            ensureFocus,
            inputCallbacks.ApplyMouseHover,
            inputCallbacks.HideTilePanel,
            inputCallbacks.RedrawAfterInput,
            drawUi,
            inputCallbacks.OnMapLeftClicked);

        return new FortressStateInputController(
            inputContexts,
            ui,
            view,
            viewport,
            tileInspection,
            fortressSizeProvider,
            drawUi);
    }
}
