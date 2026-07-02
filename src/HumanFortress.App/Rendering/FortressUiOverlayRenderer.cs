using HumanFortress.App.UI;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

internal static class FortressUiOverlayRenderer
{
    public static void Render(FortressUiOverlayRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var uiSurface = context.UiSurface;
        var ui = context.Ui;
        var viewport = CreateViewport(context);
        var overlayData = context.Runtime.GetUiOverlayFrameData(
            context.CurrentZ,
            viewport,
            showZoneOverlay: ui.QuickMenu == QuickMenuKind.Zones,
            includeManagementDrawer: NeedsManagementDrawerData(ui.OpenDrawer),
            includeWorkDrawer: ui.OpenDrawer == DrawerId.Work,
            includeDebugMenu: ui.DebugOpen,
            stockpileDetailZoneId: context.UiServices?.StockpileUI?.EditingZoneId,
            zoneDetailId: context.UiServices?.ZonesUI?.DetailPopupZoneId,
            tick: context.UiTick);
        context.UiServices?.StockpileUI?.ApplyPresetMenu(overlayData.StockpilePresets);

        ClearOverlaySurface(uiSurface);
        UiChromeRenderer.DrawTopBar(uiSurface, context.Runtime.SimulationStatus);
        UiChromeRenderer.DrawDock(uiSurface, ui);
        UiChromeRenderer.DrawQuickIcons(uiSurface, ui);
        UiManagementDrawerRenderer.DrawDrawer(
            uiSurface,
            ui,
            context.UiTick,
            overlayData.ManagementDrawer,
            overlayData.WorkDrawer);
        UiQuickMenuRenderer.Draw(
            uiSurface,
            ui,
            context.UiServices?.OrdersUI,
            context.UiServices?.ZonesUI,
            context.UiServices?.BuildUI,
            context.UiServices?.StockpileQuickUI,
            buildCatalog: overlayData.BuildCatalog);

        FortressMapOverlayRenderer.Render(context, overlayData, viewport);
        FortressToolOverlayRenderer.Render(context, overlayData);
        FortressUiModalRenderer.Render(context, overlayData);
    }

    private static Rectangle CreateViewport(FortressUiOverlayRenderContext context)
    {
        return new Rectangle(
            context.CameraPosition.X,
            context.CameraPosition.Y,
            context.MapSurface.Surface.Width,
            context.MapSurface.Surface.Height);
    }

    private static void ClearOverlaySurface(UiOverlaySurface uiSurface)
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

    private static bool NeedsManagementDrawerData(DrawerId drawer)
    {
        return drawer is DrawerId.Creature or DrawerId.Stock or DrawerId.PlacementManagement;
    }
}
