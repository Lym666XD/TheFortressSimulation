using HumanFortress.App.Rendering;
using HumanFortress.App.UI;
using HumanFortress.App.UI.Selection;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal readonly record struct FortressKeyboardInputRouterContext(
    FortressKeyboardRuntimePorts Runtime,
    UiStore Ui,
    ulong UiTick,
    FortressViewportSnapshot Viewport,
    NavigationOverlay? NavigationOverlay,
    FortressUiServices? UiServices,
    ISelectionTool? SelectionTool,
    FortressNavigationDebugController NavigationDebug,
    bool TileInspectionOpen,
    Action HideTilePanel,
    Action<string> CreateStockpile);

internal readonly record struct FortressKeyboardInputResult(
    bool Handled,
    bool ShouldRedraw,
    Point CameraPosition,
    int CurrentZ);
