using HumanFortress.App.UI.Selection;

namespace HumanFortress.App.Runtime;

internal sealed record FortressInteractionSetup(
    IWorldCoordinateMapper CoordinateMapper,
    ISelectionTool SelectionTool);
