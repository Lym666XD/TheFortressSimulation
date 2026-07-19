using HumanFortress.App.UI;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

internal static class FortressUiOverlayRenderer
{
    public static void Render(
        FortressUiOverlayRenderContext context,
        SimulationUiOverlayFrameData overlayData,
        SimulationPlacementPreviewFrameData placementPreviews,
        SimulationStatus simulationStatus)
    {
        ArgumentNullException.ThrowIfNull(context);

        var uiSurface = context.UiSurface;
        var ui = context.Ui;
        var viewport = context.ViewportGeometry;
        var presentedOverlayData = context.UiOverlayPresenter.Present(overlayData);
        context.UiServices?.StockpileUI?.ApplyPresetMenu(presentedOverlayData.StockpilePresets);

        Clear(uiSurface);
        UiChromeRenderer.DrawTopBar(uiSurface, simulationStatus);
        UiChromeRenderer.DrawDock(uiSurface, ui);
        UiChromeRenderer.DrawQuickIcons(uiSurface, ui);
        UiManagementDrawerRenderer.DrawDrawer(
            uiSurface,
            ui,
            context.UiTick,
            presentedOverlayData.ManagementDrawer,
            presentedOverlayData.WorkDrawer);
        UiQuickMenuRenderer.Draw(
            uiSurface,
            ui,
            context.UiServices?.OrdersUI,
            context.UiServices?.ZonesUI,
            context.UiServices?.BuildUI,
            context.UiServices?.StockpileQuickUI,
            buildCatalog: presentedOverlayData.BuildCatalog,
            zoneCatalog: presentedOverlayData.ZoneCatalog);

        FortressMapOverlayRenderer.Render(
            context,
            presentedOverlayData,
            placementPreviews,
            viewport);
        FortressToolOverlayRenderer.Render(context, presentedOverlayData, placementPreviews);
        FortressUiModalRenderer.Render(context, presentedOverlayData);
    }

    internal static void Clear(UiOverlaySurface uiSurface)
    {
        var surface = uiSurface.Surface;
        for (int y = 0; y < surface.Height; y++)
        {
            for (int x = 0; x < surface.Width; x++)
            {
                surface.SetGlyph(x, y, ' ', Color.Transparent, Color.Transparent);
            }
        }
    }

}
