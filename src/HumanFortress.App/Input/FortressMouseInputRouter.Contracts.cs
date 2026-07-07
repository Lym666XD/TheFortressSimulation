using HumanFortress.App.UI;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal delegate bool FortressMouseHoverApplier(Point mapLocal, bool updateSelection, bool logMapEvent);

internal readonly record struct FortressMouseInputRouterContext(
    MapScreenSurface? MapSurface,
    UiOverlaySurface? UiSurface,
    bool HasFortressMap,
    FortressUiServices? UiServices,
    UiStore Ui,
    int CurrentZ,
    ulong UiTick,
    bool TileInspectionOpen,
    Action EnsureFocus,
    FortressMouseHoverApplier ApplyMouseHover,
    Action HideTilePanel,
    Action RedrawAfterInput,
    Action Redraw,
    Action<Point> MapLeftClick);

internal readonly record struct FortressMouseInputResult(bool Handled, bool ShouldCallBase)
{
    public static readonly FortressMouseInputResult Unhandled = new(false, false);
    public static readonly FortressMouseInputResult HandledResult = new(true, false);
    public static readonly FortressMouseInputResult ContinueWithBase = new(false, true);
}
