using HumanFortress.App.Rendering;
using HumanFortress.App.UI;
using HumanFortress.Contracts.Runtime;
using SadConsole;
using SadConsole.Input;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal sealed partial class FortressStateInputController
{
    private readonly FortressInputContextFactory _inputContexts;
    private readonly UiStore _ui;
    private readonly FortressViewState _view;
    private readonly FortressViewportState _viewport;
    private readonly FortressTileInspectionController _tileInspection;
    private readonly Func<int> _fortressSizeProvider;
    private readonly Action _drawUi;

    public FortressStateInputController(
        FortressInputContextFactory inputContexts,
        UiStore ui,
        FortressViewState view,
        FortressViewportState viewport,
        FortressTileInspectionController tileInspection,
        Func<int> fortressSizeProvider,
        Action drawUi)
    {
        _inputContexts = inputContexts ?? throw new ArgumentNullException(nameof(inputContexts));
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
        _tileInspection = tileInspection ?? throw new ArgumentNullException(nameof(tileInspection));
        _fortressSizeProvider = fortressSizeProvider ?? throw new ArgumentNullException(nameof(fortressSizeProvider));
        _drawUi = drawUi ?? throw new ArgumentNullException(nameof(drawUi));
    }

    public void HideTilePanel()
    {
        _tileInspection.Hide();
    }

    public void RedrawAfterInput()
    {
        ClampCameraToWorld();
        _drawUi();
    }

    private void ClampCameraToWorld()
    {
        _viewport.ClampCamera(new RuntimeRect(
            _view.MapPositionOr(new Point(0, 0)).X,
            _view.MapPositionOr(new Point(0, 0)).Y,
            _view.MapWidthOr(80),
            _view.MapHeightOr(40)));
    }
}
