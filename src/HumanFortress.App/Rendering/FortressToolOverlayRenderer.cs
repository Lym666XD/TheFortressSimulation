using HumanFortress.App.UI;
using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.Rendering;

internal static class FortressToolOverlayRenderer
{
    public static void Render(
        FortressUiOverlayRenderContext context,
        SimulationUiOverlayFrameData overlayData)
    {
        var ui = context.Ui;
        var uiSurface = context.UiSurface;
        var stockpileUI = context.UiServices?.StockpileUI;
        var zonesUI = context.UiServices?.ZonesUI;

        if (stockpileUI != null)
        {
            if (ui.Context == UiContext.PlacingTool)
            {
                FortressPlacementOverlayRenderer.Render(context, overlayData.BuildCatalog);
            }

            if (stockpileUI.EditingZoneId is int)
            {
                stockpileUI.DrawEditPopup(
                    uiSurface,
                    overlayData.StockpileDetail ?? SimulationStockpileDetailData.Empty);
            }
        }

        zonesUI?.DrawPlacementMode(uiSurface, ui, context.LastMousePosition ?? context.CursorPosition);

        if (zonesUI?.DetailPopupZoneId is int)
        {
            zonesUI.DrawDetailPopup(
                uiSurface,
                overlayData.ZoneDetail ?? SimulationZoneDetailData.Empty);
        }
    }
}
