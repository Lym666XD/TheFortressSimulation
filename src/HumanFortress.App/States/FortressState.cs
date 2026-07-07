using System;
using SadConsole;
using HumanFortress.App.Diagnostics;
using HumanFortress.App.Input;
using HumanFortress.App.Rendering;
using HumanFortress.App.Session;
using HumanFortress.App.UI;

namespace HumanFortress.App.States
{
    internal sealed partial class FortressState : ScreenObject
    {
        private readonly FortressViewState _view = new();
        private readonly FortressViewportState _viewport = new();
        private readonly FortressLoadedSessionState _loadedSession = new();
        private readonly FortressNavigationDebugController _navigationDebug = new();
        private readonly UiStore _ui = new();
        private readonly FortressUiTickCounter _uiTicks = new();
        private readonly FortressTileInspectionController _tileInspection = new();
        private readonly IFortressDiagnosticsAccess _diagnostics = new FortressDiagnosticsAccess();
        private readonly FortressStateInputController _inputController;
        private readonly FortressViewContextFactory _viewContexts;
        private readonly FortressStateUpdateLoop _updateLoop;
        private readonly FortressSessionContext _session;

        private int FortressSize => FortressSessionSizeRules.Normalize(_session.FortressSize);

        internal FortressState(FortressStateRuntimePorts runtimePorts, FortressSessionContext session)
        {
            ArgumentNullException.ThrowIfNull(runtimePorts);
            _session = session ?? throw new ArgumentNullException(nameof(session));

            var composition = FortressStateCompositionFactory.Create(
                this,
                runtimePorts,
                _session,
                _ui,
                _view,
                _viewport,
                _loadedSession,
                _navigationDebug,
                _tileInspection,
                _diagnostics,
                () => FortressSize,
                () => _uiTicks.Current,
                () => IsFocused = true,
                DrawUI);
            _inputController = composition.InputController;
            _viewContexts = composition.ViewContexts;
            var initializer = composition.Initializer;
            _updateLoop = new FortressStateUpdateLoop(
                _ui,
                _uiTicks,
                _inputController,
                initializer,
                EnsureFocused,
                DrawUI);
            Logger.Log("[FortressState] Constructor called - deferred initialization");
            // Defer initialization until OnCalculateRenderPosition is called
        }

    }
}
