using HumanFortress.App.UI;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal readonly record struct FortressMapClickControllerContext(
    UiStore Ui,
    FortressMapInspectionRuntimePorts Runtime,
    FortressUiServices? UiServices,
    int CurrentZ,
    ulong UiTick,
    SimulationWorkshopDebugData Workshops,
    Action<Point, int> OpenTilePanel,
    Action Redraw);

internal static partial class FortressMapClickController
{
    public static bool TryHandleNormalClick(FortressMapClickControllerContext context, Point worldPos)
    {
        var ui = context.Ui;
        if (ui.Context != UiContext.PlacingTool
            && context.UiServices?.StockpileUI != null
            && context.UiServices.StockpileUI.TryOpenStockpileAt(
                worldPos,
                context.Runtime.FindStockpileAt(worldPos, context.CurrentZ)))
        {
            context.Redraw();
            return true;
        }

        if (ui.Context == UiContext.Global && ui.QuickMenu == QuickMenuKind.Zones)
        {
            var zoneHit = context.Runtime.FindZoneAt(worldPos, context.CurrentZ);
            if (zoneHit.HasZone)
            {
                context.UiServices?.ZonesUI.OpenDetailPopup(zoneHit.ZoneId);
                context.Redraw();
                return true;
            }
        }

        context.OpenTilePanel(worldPos, context.CurrentZ);
        Logger.Log($"[CLICK] Open TilePanel at world=({worldPos.X},{worldPos.Y},{context.CurrentZ})");
        LogTileInfo(context, worldPos);
        context.Redraw();
        return true;
    }
}
