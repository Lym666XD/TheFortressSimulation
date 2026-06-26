using HumanFortress.App.Rendering;
using HumanFortress.App.UI;
using HumanFortress.App.UI.Selection;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal readonly record struct FortressOverlayClickContext(
    UiStore Ui,
    int SurfaceWidth,
    int SurfaceHeight,
    bool MapSurfaceAvailable,
    Point MapSurfacePosition,
    int MapSurfaceWidth,
    int MapSurfaceHeight,
    FortressUiServices? UiServices,
    FortressViewportSnapshot Viewport,
    ulong UiTick,
    bool TilePanelOpen,
    ISelectionTool? SelectionTool,
    Action HideTilePanel,
    Action Redraw,
    Action<Point> MapLeftClick);
