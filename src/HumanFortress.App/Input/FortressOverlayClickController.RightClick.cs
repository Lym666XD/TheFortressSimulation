using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal static partial class FortressOverlayClickController
{
    public static bool HandleRightClick(FortressOverlayClickContext context, Point local)
    {
        ArgumentNullException.ThrowIfNull(context.Ui);
        ArgumentNullException.ThrowIfNull(context.HideTilePanel);
        ArgumentNullException.ThrowIfNull(context.Redraw);

        if (context.Ui.ConstructionMaterialDialogOpen)
            return true;

        Logger.Log($"[RIGHT-CLICK-OVERLAY] Clicked at local=({local.X},{local.Y}), tilePanelOpen={context.TilePanelOpen}, QuickMenu={context.Ui.QuickMenu}, OrdersMenu={context.Ui.OrdersMenu}, ZoneMenu={context.Ui.ZoneMenu}");

        if (context.SelectionTool != null && context.SelectionTool.IsActive)
        {
            context.SelectionTool.Cancel();
            context.Ui.CancelPlacement();
            context.Redraw();
            return true;
        }

        FortressRightClickCancelInput.Handle(
            local,
            context.Ui,
            context.TilePanelOpen,
            context.UiServices?.ZonesUI,
            context.UiServices?.StockpileUI,
            context.HideTilePanel);
        context.Redraw();
        return true;
    }
}
