using HumanFortress.App.Diagnostics;
using HumanFortress.App.Input;
using HumanFortress.App.Rendering;
using HumanFortress.App.Runtime;
using HumanFortress.App.Session;
using HumanFortress.App.UI;

namespace HumanFortress.App.States;

internal static partial class FortressStateCompositionFactory
{
    private static FortressViewContextFactory CreateViewContexts(
        FortressStateRuntimePorts runtime,
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
            runtime.Read,
            runtime.UiInput,
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
