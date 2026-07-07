using HumanFortress.App.Rendering;
using HumanFortress.App.Session;
using HumanFortress.App.UI;
using SadConsole;

namespace HumanFortress.App.States;

internal static partial class FortressStateCompositionFactory
{
    private static FortressSessionLoadCoordinator CreateSessionLoadCoordinator(
        FortressSessionRuntimePorts runtime,
        FortressSessionContext session,
        FortressLoadedSessionState loadedSession,
        UiStore ui,
        Func<ulong> uiTickProvider,
        string baseDirectory)
    {
        return new FortressSessionLoadCoordinator(
            runtime,
            session,
            loadedSession,
            ui,
            uiTickProvider,
            baseDirectory);
    }

    private static FortressStateInitializer CreateInitializer(
        ScreenObject owner,
        FortressViewState view,
        FortressViewportState viewport,
        FortressViewContextFactory viewContexts,
        FortressSessionLoadCoordinator sessionLoadCoordinator,
        Func<int> fortressSizeProvider,
        Action drawUi)
    {
        return new FortressStateInitializer(
            owner,
            view,
            viewport,
            viewContexts,
            sessionLoadCoordinator,
            fortressSizeProvider,
            drawUi);
    }
}
