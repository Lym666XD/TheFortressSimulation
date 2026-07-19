using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.Rendering;

internal sealed class FortressUiOverlayPresenterCache
{
    private string? _payloadHash;
    private SimulationBuildCatalogData _buildCatalog;
    private SimulationJobsDebugData? _jobs;
    private SimulationWorkshopDebugData _workshops;
    private SimulationStockpilePresetMenuData _stockpilePresets;
    private SimulationStockpileOverlayData _stockpileOverlay;
    private SimulationStockpileDetailData? _stockpileDetail;
    private SimulationZoneOverlayData _zoneOverlay;
    private SimulationZoneDetailData? _zoneDetail;
    private SimulationZoneCatalogData _zoneCatalog;
    private SimulationManagementDrawerData? _managementDrawer;
    private SimulationWorkDrawerData? _workDrawer;
    private SimulationDebugMenuData? _debugMenu;

    internal SimulationUiOverlayFrameData Present(SimulationUiOverlayFrameData overlayData)
    {
        var delta = overlayData.Delta;
        if (CanApplyDelta(delta))
        {
            ApplyDelta(overlayData, delta);
            _payloadHash = delta.PayloadHash;
            return Compose(overlayData);
        }

        Seed(overlayData, delta.IsAvailable ? delta.PayloadHash : null);
        return Compose(overlayData);
    }

    internal void Reset()
    {
        _payloadHash = null;
        _buildCatalog = default;
        _jobs = null;
        _workshops = default;
        _stockpilePresets = default;
        _stockpileOverlay = default;
        _stockpileDetail = null;
        _zoneOverlay = default;
        _zoneDetail = null;
        _zoneCatalog = default;
        _managementDrawer = null;
        _workDrawer = null;
        _debugMenu = null;
    }

    private bool CanApplyDelta(SimulationUiOverlayFrameDeltaData delta)
    {
        return delta.IsAvailable
            && delta.SchemaVersion == SimulationUiOverlayFrameDeltaSchema.CurrentVersion
            && delta.CanApplyToBase
            && !string.IsNullOrWhiteSpace(delta.BasePayloadHash)
            && string.Equals(delta.BasePayloadHash, _payloadHash, StringComparison.Ordinal)
            && string.Equals(delta.PayloadHashAlgorithm, SimulationSnapshotPayloadHashAlgorithm.JsonSha256V1, StringComparison.Ordinal);
    }

    private void Seed(
        SimulationUiOverlayFrameData overlayData,
        string? payloadHash)
    {
        _payloadHash = string.IsNullOrWhiteSpace(payloadHash) ? null : payloadHash;
        _buildCatalog = overlayData.BuildCatalog;
        _jobs = overlayData.Jobs;
        _workshops = overlayData.Workshops;
        _stockpilePresets = overlayData.StockpilePresets;
        _stockpileOverlay = overlayData.StockpileOverlay;
        _stockpileDetail = overlayData.StockpileDetail;
        _zoneOverlay = overlayData.ZoneOverlay;
        _zoneDetail = overlayData.ZoneDetail;
        _zoneCatalog = overlayData.ZoneCatalog;
        _managementDrawer = overlayData.ManagementDrawer;
        _workDrawer = overlayData.WorkDrawer;
        _debugMenu = overlayData.DebugMenu;
    }

    private void ApplyDelta(
        SimulationUiOverlayFrameData overlayData,
        SimulationUiOverlayFrameDeltaData delta)
    {
        foreach (var section in delta.ChangedSections.Distinct(StringComparer.Ordinal))
        {
            switch (section)
            {
                case SimulationUiOverlayFrameSection.BuildCatalog:
                    _buildCatalog = overlayData.BuildCatalog;
                    break;
                case SimulationUiOverlayFrameSection.Jobs:
                    _jobs = overlayData.Jobs;
                    break;
                case SimulationUiOverlayFrameSection.Workshops:
                    _workshops = overlayData.Workshops;
                    break;
                case SimulationUiOverlayFrameSection.StockpilePresets:
                    _stockpilePresets = overlayData.StockpilePresets;
                    break;
                case SimulationUiOverlayFrameSection.StockpileOverlay:
                    _stockpileOverlay = overlayData.StockpileOverlay;
                    break;
                case SimulationUiOverlayFrameSection.StockpileDetail:
                    _stockpileDetail = overlayData.StockpileDetail;
                    break;
                case SimulationUiOverlayFrameSection.ZoneOverlay:
                    _zoneOverlay = overlayData.ZoneOverlay;
                    break;
                case SimulationUiOverlayFrameSection.ZoneDetail:
                    _zoneDetail = overlayData.ZoneDetail;
                    break;
                case SimulationUiOverlayFrameSection.ZoneCatalog:
                    _zoneCatalog = overlayData.ZoneCatalog;
                    break;
                case SimulationUiOverlayFrameSection.ManagementDrawer:
                    _managementDrawer = overlayData.ManagementDrawer;
                    break;
                case SimulationUiOverlayFrameSection.WorkDrawer:
                    _workDrawer = overlayData.WorkDrawer;
                    break;
                case SimulationUiOverlayFrameSection.DebugMenu:
                    _debugMenu = overlayData.DebugMenu;
                    break;
            }
        }
    }

    private SimulationUiOverlayFrameData Compose(SimulationUiOverlayFrameData overlayData)
    {
        return overlayData with
        {
            BuildCatalog = _buildCatalog,
            Jobs = _jobs,
            Workshops = _workshops,
            StockpilePresets = _stockpilePresets,
            StockpileOverlay = _stockpileOverlay,
            StockpileDetail = _stockpileDetail,
            ZoneOverlay = _zoneOverlay,
            ZoneDetail = _zoneDetail,
            ZoneCatalog = _zoneCatalog,
            ManagementDrawer = _managementDrawer,
            WorkDrawer = _workDrawer,
            DebugMenu = _debugMenu
        };
    }
}
