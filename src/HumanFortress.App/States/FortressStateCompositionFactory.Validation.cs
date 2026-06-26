using HumanFortress.App.Diagnostics;
using HumanFortress.App.Input;
using HumanFortress.App.Rendering;
using HumanFortress.App.Runtime;
using HumanFortress.App.Session;
using HumanFortress.App.UI;
using SadConsole;

namespace HumanFortress.App.States;

internal static partial class FortressStateCompositionFactory
{
    private static void ValidateCreateArguments(
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
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(viewport);
        ArgumentNullException.ThrowIfNull(loadedSession);
        ArgumentNullException.ThrowIfNull(navigationDebug);
        ArgumentNullException.ThrowIfNull(tileInspection);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(fortressSizeProvider);
        ArgumentNullException.ThrowIfNull(uiTickProvider);
        ArgumentNullException.ThrowIfNull(ensureFocus);
        ArgumentNullException.ThrowIfNull(drawUi);
    }
}
