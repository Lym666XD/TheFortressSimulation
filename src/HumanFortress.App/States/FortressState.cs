using System;
using SadConsole;
using SadConsole.Input;
using HumanFortress.App.Diagnostics;
using HumanFortress.App.Input;
using HumanFortress.App.Runtime;
using HumanFortress.App.Rendering;
using HumanFortress.App.Session;
using HumanFortress.App.UI;

namespace HumanFortress.App.States
{
    internal sealed partial class FortressState : ScreenObject
    {
        private bool _initialized = false;
        private readonly FortressViewState _view = new();
        private readonly FortressViewportState _viewport = new();
        private readonly FortressLoadedSessionState _loadedSession = new();
        private readonly FortressNavigationDebugController _navigationDebug = new();
        private readonly UiStore _ui = new();
        private ulong _uiTick = 0;
        private readonly FortressTileInspectionController _tileInspection = new();
        private readonly IFortressDiagnosticsAccess _diagnostics = new FortressDiagnosticsAccess();
        private readonly FortressStateInputController _inputController;
        private readonly FortressViewContextFactory _viewContexts;
        private readonly FortressStateInitializer _initializer;
        private readonly FortressSessionContext _session;

        private int FortressSize => FortressSessionSizeRules.Normalize(_session.FortressSize);

        internal FortressState(IFortressRuntimeSessionAccess runtime, FortressSessionContext session)
        {
            ArgumentNullException.ThrowIfNull(runtime);
            _session = session ?? throw new ArgumentNullException(nameof(session));
            var runtimePorts = FortressStateRuntimePorts.From(runtime);

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
                () => _uiTick,
                () => IsFocused = true,
                DrawUI);
            _inputController = composition.InputController;
            _viewContexts = composition.ViewContexts;
            _initializer = composition.Initializer;
            Logger.Log("[FortressState] Constructor called - deferred initialization");
            // Defer initialization until OnCalculateRenderPosition is called
        }

    }
}
