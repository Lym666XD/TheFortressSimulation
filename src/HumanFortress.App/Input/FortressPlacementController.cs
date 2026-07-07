using HumanFortress.App.UI;
using HumanFortress.App.UI.Placement;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal readonly record struct FortressPlacementControllerContext(
    UiStore Ui,
    FortressPlacementRuntimePorts Runtime,
    StockpileUI? StockpileUi,
    int CurrentZ,
    ulong UiTick,
    Action Redraw);

internal static partial class FortressPlacementController
{
}
