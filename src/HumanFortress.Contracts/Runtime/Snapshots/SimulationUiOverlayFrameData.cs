namespace HumanFortress.Contracts.Runtime.Snapshots;

public static class SimulationUiOverlayFrameDeltaSchema
{
    public const int CurrentVersion = 2;
}

public static class SimulationUiOverlayFrameSection
{
    public const string BuildCatalog = "build-catalog";
    public const string Jobs = "jobs";
    public const string Workshops = "workshops";
    public const string StockpilePresets = "stockpile-presets";
    public const string StockpileOverlay = "stockpile-overlay";
    public const string StockpileDetail = "stockpile-detail";
    public const string ZoneOverlay = "zone-overlay";
    public const string ZoneDetail = "zone-detail";
    public const string ZoneCatalog = "zone-catalog";
    public const string ManagementDrawer = "management-drawer";
    public const string WorkDrawer = "work-drawer";
    public const string DebugMenu = "debug-menu";
}

public readonly record struct SimulationUiOverlaySectionHashData(
    string Section,
    string PayloadHash);

public readonly record struct SimulationUiOverlayFrameDeltaData(
    int SchemaVersion,
    bool IsAvailable,
    bool CanApplyToBase,
    string PayloadHash,
    string PayloadHashAlgorithm,
    string? BasePayloadHash,
    IReadOnlyList<SimulationUiOverlaySectionHashData> SectionHashes,
    IReadOnlyList<string> ChangedSections)
{
    public static SimulationUiOverlayFrameDeltaData Unavailable { get; } = new(
        SimulationUiOverlayFrameDeltaSchema.CurrentVersion,
        false,
        false,
        string.Empty,
        SimulationSnapshotPayloadHashAlgorithm.JsonSha256V1,
        null,
        Array.Empty<SimulationUiOverlaySectionHashData>(),
        Array.Empty<string>());

    public static SimulationUiOverlayFrameDeltaData FullSnapshot(
        string payloadHash,
        IReadOnlyList<SimulationUiOverlaySectionHashData> sectionHashes,
        IReadOnlyList<string> changedSections)
    {
        return new SimulationUiOverlayFrameDeltaData(
            SimulationUiOverlayFrameDeltaSchema.CurrentVersion,
            true,
            false,
            payloadHash,
            SimulationSnapshotPayloadHashAlgorithm.JsonSha256V1,
            null,
            sectionHashes,
            changedSections);
    }

    public static SimulationUiOverlayFrameDeltaData Delta(
        string payloadHash,
        string basePayloadHash,
        IReadOnlyList<SimulationUiOverlaySectionHashData> sectionHashes,
        IReadOnlyList<string> changedSections)
    {
        return new SimulationUiOverlayFrameDeltaData(
            SimulationUiOverlayFrameDeltaSchema.CurrentVersion,
            true,
            true,
            payloadHash,
            SimulationSnapshotPayloadHashAlgorithm.JsonSha256V1,
            basePayloadHash,
            sectionHashes,
            changedSections);
    }
}

public readonly record struct SimulationUiOverlayFrameData(
    SimulationBuildCatalogData BuildCatalog,
    SimulationJobsDebugData? Jobs,
    SimulationWorkshopDebugData Workshops,
    SimulationStockpilePresetMenuData StockpilePresets,
    SimulationStockpileOverlayData StockpileOverlay,
    SimulationStockpileDetailData? StockpileDetail,
    SimulationZoneOverlayData ZoneOverlay,
    SimulationZoneDetailData? ZoneDetail,
    SimulationManagementDrawerData? ManagementDrawer,
    SimulationWorkDrawerData? WorkDrawer,
    SimulationDebugMenuData? DebugMenu,
    SimulationSnapshotMetadata Metadata = default,
    SimulationSnapshotPublicationData Publication = default,
    SimulationSnapshotPresenterFrameData PresenterFrame = default,
    SimulationUiOverlayFrameDeltaData Delta = default,
    SimulationZoneCatalogData ZoneCatalog = default);
