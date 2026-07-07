using HumanFortress.Contracts.Runtime.Snapshots;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal sealed partial class FortressRuntimeAccess
{
    internal SimulationUiOverlayFrameData GetUiOverlayFrameData(
        int currentZ,
        Rectangle viewport,
        bool showZoneOverlay,
        bool includeManagementDrawer,
        bool includeWorkDrawer,
        bool includeDebugMenu,
        int? stockpileDetailZoneId,
        int? zoneDetailId)
    {
        return _read.GetUiOverlayFrameData(
            currentZ,
            viewport.ToRuntimeRect(),
            showZoneOverlay,
            includeManagementDrawer,
            includeWorkDrawer,
            includeDebugMenu,
            stockpileDetailZoneId,
            zoneDetailId);
    }

    SimulationUiOverlayFrameData IFortressRuntimeReadAccess.GetUiOverlayFrameData(
        int currentZ,
        Rectangle viewport,
        bool showZoneOverlay,
        bool includeManagementDrawer,
        bool includeWorkDrawer,
        bool includeDebugMenu,
        int? stockpileDetailZoneId,
        int? zoneDetailId) =>
        GetUiOverlayFrameData(
            currentZ,
            viewport,
            showZoneOverlay,
            includeManagementDrawer,
            includeWorkDrawer,
            includeDebugMenu,
            stockpileDetailZoneId,
            zoneDetailId);
}
