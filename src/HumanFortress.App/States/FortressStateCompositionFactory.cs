using HumanFortress.App.Diagnostics;
using HumanFortress.App.Input;
using HumanFortress.App.Rendering;
using HumanFortress.App.Session;
using HumanFortress.App.UI;
using SadConsole;

namespace HumanFortress.App.States;

internal sealed record FortressStateComposition(
    FortressStateInputController InputController,
    FortressViewContextFactory ViewContexts,
    FortressStateInitializer Initializer);

internal static partial class FortressStateCompositionFactory
{
    public static FortressStateComposition Create(
        ScreenObject owner,
        FortressStateRuntimePorts runtime,
        FortressSessionContext session,
        UiStore ui,
        FortressViewState view,
        FortressViewportState viewport,
        FortressLoadedSessionState loadedSession,
        FortressNavigationDebugController navigationDebug,
        FortressTileInspectionController tileInspection,
        IFortressDiagnosticsAccess diagnostics,
        Func<int> fortressSizeProvider,
        Func<ulong> uiTickProvider,
        Action ensureFocus,
        Action drawUi)
    {
        ValidateCreateArguments(
            owner,
            runtime,
            session,
            ui,
            view,
            viewport,
            loadedSession,
            navigationDebug,
            tileInspection,
            diagnostics,
            fortressSizeProvider,
            uiTickProvider,
            ensureFocus,
            drawUi);

        var inputCallbacks = new FortressInputCallbackHub();
        var inputController = CreateInputController(
            runtime.Input,
            ui,
            view,
            viewport,
            loadedSession,
            navigationDebug,
            tileInspection,
            fortressSizeProvider,
            uiTickProvider,
            ensureFocus,
            drawUi,
            inputCallbacks);
        inputCallbacks.Bind(inputController);

        var viewContexts = CreateViewContexts(
            runtime.View,
            diagnostics,
            ui,
            view,
            viewport,
            loadedSession,
            tileInspection,
            fortressSizeProvider,
            uiTickProvider,
            inputController,
            drawUi);

        var sessionLoadCoordinator = CreateSessionLoadCoordinator(
            runtime.Session,
            session,
            loadedSession,
            ui,
            uiTickProvider,
            AppContext.BaseDirectory);

        var initializer = CreateInitializer(
            owner,
            view,
            viewport,
            viewContexts,
            sessionLoadCoordinator,
            fortressSizeProvider,
            drawUi);

        return new FortressStateComposition(
            inputController,
            viewContexts,
            initializer);
    }
}
