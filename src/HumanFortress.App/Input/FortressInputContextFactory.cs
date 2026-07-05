using HumanFortress.App.Rendering;
using HumanFortress.App.Session;
using HumanFortress.App.UI;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal sealed partial class FortressInputContextFactory
{
    private readonly FortressKeyboardRuntimePorts _keyboardRuntime;
    private readonly FortressMapRuntimePorts _mapRuntime;
    private readonly UiStore _ui;
    private readonly FortressViewState _view;
    private readonly FortressViewportState _viewport;
    private readonly FortressLoadedSessionState _loadedSession;
    private readonly FortressNavigationDebugController _navigationDebug;
    private readonly FortressTileInspectionController _tileInspection;
    private readonly Func<int> _fortressSizeProvider;
    private readonly Func<ulong> _uiTickProvider;
    private readonly Action _ensureFocus;
    private readonly FortressMouseHoverApplier _applyMouseHover;
    private readonly Action _hideTilePanel;
    private readonly Action _redrawAfterInput;
    private readonly Action _drawUi;
    private readonly Action<Point> _mapLeftClick;

    public FortressInputContextFactory(
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
        FortressMouseHoverApplier applyMouseHover,
        Action hideTilePanel,
        Action redrawAfterInput,
        Action drawUi,
        Action<Point> mapLeftClick)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _keyboardRuntime = runtime.Keyboard;
        _mapRuntime = runtime.Map;
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
        _loadedSession = loadedSession ?? throw new ArgumentNullException(nameof(loadedSession));
        _navigationDebug = navigationDebug ?? throw new ArgumentNullException(nameof(navigationDebug));
        _tileInspection = tileInspection ?? throw new ArgumentNullException(nameof(tileInspection));
        _fortressSizeProvider = fortressSizeProvider ?? throw new ArgumentNullException(nameof(fortressSizeProvider));
        _uiTickProvider = uiTickProvider ?? throw new ArgumentNullException(nameof(uiTickProvider));
        _ensureFocus = ensureFocus ?? throw new ArgumentNullException(nameof(ensureFocus));
        _applyMouseHover = applyMouseHover ?? throw new ArgumentNullException(nameof(applyMouseHover));
        _hideTilePanel = hideTilePanel ?? throw new ArgumentNullException(nameof(hideTilePanel));
        _redrawAfterInput = redrawAfterInput ?? throw new ArgumentNullException(nameof(redrawAfterInput));
        _drawUi = drawUi ?? throw new ArgumentNullException(nameof(drawUi));
        _mapLeftClick = mapLeftClick ?? throw new ArgumentNullException(nameof(mapLeftClick));
    }
}
