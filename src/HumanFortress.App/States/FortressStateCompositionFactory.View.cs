using HumanFortress.App.Diagnostics;
using HumanFortress.App.Input;
using HumanFortress.App.Rendering;
using HumanFortress.App.Session;
using HumanFortress.App.UI;

namespace HumanFortress.App.States;

internal static partial class FortressStateCompositionFactory
{
    private static FortressViewContextFactory CreateViewContexts(
        FortressViewRuntimePorts runtime,
        IFortressDiagnosticsAccess diagnostics,
        UiStore ui,
        FortressViewState view,
        FortressViewportState viewport,
        FortressLoadedSessionState loadedSession,
        FortressTileInspectionController tileInspection,
        Func<int> fortressSizeProvider,
        Func<ulong> uiTickProvider,
        FortressStateInputController inputController,
        Action drawUi)
    {
        return new FortressViewContextFactory(
            runtime,
            diagnostics,
            ui,
            view,
            viewport,
            loadedSession,
            tileInspection,
            fortressSizeProvider,
            uiTickProvider,
            inputController.OnMapMouseMoved,
            inputController.OnMapLeftClicked,
            inputController.OnOverlayLeftClicked,
            inputController.OnOverlayRightClicked,
            inputController.OnOverlayMouseMoved,
            drawUi);
    }
}
