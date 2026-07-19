using HumanFortress.App.Rendering;
using HumanFortress.App.UI;
using HumanFortress.Contracts.Runtime;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal readonly record struct FortressMapInteractionContext(
    bool MapSurfaceAvailable,
    UiStore Ui,
    FortressViewportSnapshot Viewport,
    RuntimeViewportGeometry Geometry,
    FortressDebugSpawnContext DebugSpawn,
    FortressMapClickControllerContext MapClick,
    FortressPlacementRouterContext Placement);

internal static class FortressMapInteractionController
{
    public static void HandleLeftClick(FortressMapInteractionContext context, Point local)
    {
        ArgumentNullException.ThrowIfNull(context.Ui);

        if (!context.MapSurfaceAvailable)
            return;

        if (context.Ui.SuppressNextTileClick)
        {
            context.Ui.SuppressNextTileClick = false;
            return;
        }

        if (!FortressMapClickInput.TryResolveWorldPosition(
                local,
                context.Geometry,
                out var worldPos))
        {
            return;
        }

        if (FortressDebugSpawnController.TryHandleMapClick(context.DebugSpawn, worldPos))
            return;

        if (FortressMapClickController.TryHandleWorkshopCellClick(context.MapClick, worldPos))
            return;

        if (FortressPlacementRouter.TryHandleClick(context.Placement, worldPos))
            return;

        FortressMapClickController.TryHandleNormalClick(context.MapClick, worldPos);
    }
}
