using HumanFortress.App.Diagnostics;
using HumanFortress.App.Runtime;
using HumanFortress.App.Session;
using HumanFortress.App.UI;
using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

internal sealed partial class FortressViewContextFactory
{
    private readonly IFortressRuntimeReadAccess _readRuntime;
    private readonly IFortressRuntimeUiInputAccess _uiInputRuntime;
    private readonly IFortressDiagnosticsAccess _diagnostics;
    private readonly UiStore _ui;
    private readonly FortressViewState _view;
    private readonly FortressViewportState _viewport;
    private readonly FortressLoadedSessionState _loadedSession;
    private readonly FortressTileInspectionController _tileInspection;
    private readonly Func<int> _fortressSizeProvider;
    private readonly Func<ulong> _uiTickProvider;
    private readonly Action<Point> _onMapMouseMoved;
    private readonly Action<Point> _onMapLeftClicked;
    private readonly Action<Point> _onOverlayLeftClicked;
    private readonly Action<Point> _onOverlayRightClicked;
    private readonly Action<Point> _onOverlayMouseMoved;
    private readonly Action _redraw;

    public FortressViewContextFactory(
        IFortressRuntimeReadAccess readRuntime,
        IFortressRuntimeUiInputAccess uiInputRuntime,
        IFortressDiagnosticsAccess diagnostics,
        UiStore ui,
        FortressViewState view,
        FortressViewportState viewport,
        FortressLoadedSessionState loadedSession,
        FortressTileInspectionController tileInspection,
        Func<int> fortressSizeProvider,
        Func<ulong> uiTickProvider,
        Action<Point> onMapMouseMoved,
        Action<Point> onMapLeftClicked,
        Action<Point> onOverlayLeftClicked,
        Action<Point> onOverlayRightClicked,
        Action<Point> onOverlayMouseMoved,
        Action redraw)
    {
        _readRuntime = readRuntime ?? throw new ArgumentNullException(nameof(readRuntime));
        _uiInputRuntime = uiInputRuntime ?? throw new ArgumentNullException(nameof(uiInputRuntime));
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
        _loadedSession = loadedSession ?? throw new ArgumentNullException(nameof(loadedSession));
        _tileInspection = tileInspection ?? throw new ArgumentNullException(nameof(tileInspection));
        _fortressSizeProvider = fortressSizeProvider ?? throw new ArgumentNullException(nameof(fortressSizeProvider));
        _uiTickProvider = uiTickProvider ?? throw new ArgumentNullException(nameof(uiTickProvider));
        _onMapMouseMoved = onMapMouseMoved ?? throw new ArgumentNullException(nameof(onMapMouseMoved));
        _onMapLeftClicked = onMapLeftClicked ?? throw new ArgumentNullException(nameof(onMapLeftClicked));
        _onOverlayLeftClicked = onOverlayLeftClicked ?? throw new ArgumentNullException(nameof(onOverlayLeftClicked));
        _onOverlayRightClicked = onOverlayRightClicked ?? throw new ArgumentNullException(nameof(onOverlayRightClicked));
        _onOverlayMouseMoved = onOverlayMouseMoved ?? throw new ArgumentNullException(nameof(onOverlayMouseMoved));
        _redraw = redraw ?? throw new ArgumentNullException(nameof(redraw));
    }
}
