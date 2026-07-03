using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Replay;
using HumanFortress.Contracts.Runtime.Save;
using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.Runtime;

public interface IFortressRuntimeSessionPorts :
    IFortressRuntimeSessionLifecyclePort,
    IFortressRuntimeSessionBootstrapPort,
    IFortressRuntimeSessionReadPort,
    IFortressRuntimeSessionSnapshotPort,
    IFortressRuntimeSessionReplayCheckpointPort,
    IFortressRuntimeSessionSaveManifestPort,
    IFortressRuntimeSessionSaveSnapshotPort,
    IFortressRuntimeSessionPlacementCommandPort,
    IFortressRuntimeSessionDebugCommandPort,
    IFortressRuntimeSessionSimulationControlPort,
    IFortressRuntimeSessionProfessionCommandPort,
    IFortressRuntimeSessionWorkshopCommandPort
{
}

public interface IFortressRuntimeSessionLifecyclePort
{
    void InitializeWorld(int sizeInChunks, int maxZ);
    bool StopIfRunning();
    void StartFortressPlay(bool enqueueAutoDig);
}

public interface IFortressRuntimeSessionBootstrapPort
{
    RuntimeFortressGenerationResult GenerateAndFillFortressWorld(RuntimeFortressGenerationRequest request);
    void EnqueueStartupAutoDig(int currentZ);
    void SetWorkshopCompletionHandler(Action<RuntimeWorkshopCompletionNotification>? handler);
}

public interface IFortressRuntimeSessionReadPort
{
    SimulationStatus SimulationStatus { get; }

    SimulationUiOverlayFrameData GetUiOverlayFrameData(
        int currentZ,
        RuntimeRect viewport,
        bool showZoneOverlay,
        bool includeManagementDrawer,
        bool includeWorkDrawer,
        bool includeDebugMenu,
        int? stockpileDetailZoneId,
        int? zoneDetailId);

    SimulationFrameRenderData GetFrameRenderData(
        bool includeMapViewport,
        int fortressSize,
        RuntimePoint cameraPosition,
        RuntimePoint cursorPosition,
        int currentZ,
        int zoomLevel,
        int viewWidth,
        int viewHeight,
        int cursorGlyph,
        SimulationNavigationOverlayMode navigationMode,
        RuntimePoint? selectedNavigationTarget,
        RuntimePoint tileInspectionWorldPosition,
        int tileInspectionZ);

    SimulationPlacementPreviewData GetPlacementPreviewData(
        RuntimePoint first,
        RuntimePoint second,
        int z,
        SimulationPlacementPreviewMode mode);
}

public interface IFortressRuntimeSessionSnapshotPort
{
    SimulationBuildCatalogData GetBuildCatalogData();
    SimulationDebugMenuData GetDebugMenuData();
    SimulationDebugSpawnData GetDebugSpawnData();
    SimulationWorldAvailabilityData GetWorldAvailabilityData();
    ZoneHitData FindZoneAt(RuntimePoint worldPosition, int z);
    StockpileHitData FindStockpileAt(RuntimePoint worldPosition, int z);

    SimulationNavigationPathData FindNavigationDebugPath(
        RuntimePoint start,
        int startZ,
        RuntimePoint destination,
        int destinationZ);

    SimulationTileInspectionData GetTileInspectionData(RuntimePoint tileWorldPosition, int tileZ);
    WorkforceDebugData GetWorkforceInputData();
    SimulationWorkshopDebugData GetWorkshopDebugData();
    WorkshopSummaryView? GetWorkshopPanelData(Guid workshopId);
    string? GetDefaultRecipeForWorkshop(string? workshopId);
}

public interface IFortressRuntimeSessionReplayCheckpointPort
{
    RuntimeReplayCheckpointData GetReplayCheckpointData();
    string GetReplayCheckpointHash();
}

public interface IFortressRuntimeSessionSaveManifestPort
{
    RuntimeSaveManifestData GetSaveManifestData();
}

public interface IFortressRuntimeSessionSaveSnapshotPort
{
    RuntimeSaveSnapshotDocumentData CreateSaveSnapshotDocumentData();
    void WriteSaveSnapshotDocument(string directory);
    RuntimeSaveSnapshotDocumentData ReadSaveSnapshotDocument(string directory);
    RuntimeSaveSnapshotDocumentValidationResultData ValidateSaveSnapshotDirectory(string directory);
    RuntimeSaveSnapshotDocumentValidationResultData ValidateSaveSnapshotDocument(
        RuntimeSaveSnapshotDocumentData document);
    RuntimeSaveSnapshotRestoreResultData RestorePendingCommandsFromSaveSnapshotDocument(
        RuntimeSaveSnapshotDocumentData document);
    RuntimeSaveSnapshotRestoreResultData RestorePendingCommandsFromSaveSnapshotDirectory(
        string directory);
    RuntimeSaveWorldSnapshotRestoreResultData RestoreWorldFromSaveSnapshotDocument(
        RuntimeSaveSnapshotDocumentData document);
    RuntimeSaveWorldSnapshotRestoreResultData RestoreWorldFromSaveSnapshotDirectory(
        string directory);
    RuntimeSaveFullSnapshotRestoreResultData RestoreFullFromSaveSnapshotDocument(
        RuntimeSaveSnapshotDocumentData document);
    RuntimeSaveFullSnapshotRestoreResultData RestoreFullFromSaveSnapshotDirectory(
        string directory);
}

public interface IFortressRuntimeSessionPlacementCommandPort
{
    void QueueHaulOrder(RuntimeRect rect, int z, int priority = 50);

    void QueueAdvancedMiningOrder(
        RuntimeRect rect,
        int zMin,
        int zMax,
        RuntimeMiningAction action,
        int priority = 50);

    void QueueConstructionOrder(
        RuntimeRect rect,
        int zMin,
        int zMax,
        RuntimeConstructionShape shape,
        string? preferredMaterialId,
        string[] materialTags,
        int priority = 50);

    void QueueBuildableConstructionOrder(
        string constructionId,
        RuntimePoint anchor,
        int z,
        int priority = 50);

    void QueueCreateZone(string defId, RuntimeRect rect, int z);
    void QueueDeleteZone(int zoneId);
    void QueueCreateStockpile(RuntimeRect rect, int z, string presetId);
    void QueueDeleteStockpile(int zoneId);
}

public interface IFortressRuntimeSessionDebugCommandPort
{
    void QueueCreatureSpawn(string creatureId, RuntimePoint position, int z, string factionId);
    void QueueItemSpawn(string itemId, RuntimePoint position, int z, int quantity = 1);
}

public interface IFortressRuntimeSessionSimulationControlPort
{
    SimulationStatus ToggleSimulationPause();
    SimulationStatus CycleSimulationSpeedDown();
    SimulationStatus CycleSimulationSpeedUp();
}

public interface IFortressRuntimeSessionProfessionCommandPort
{
    void SetProfessionWeight(Guid workerId, string professionId, int weight);
}

public interface IFortressRuntimeSessionWorkshopCommandPort
{
    void QueueAddWorkshopRecipe(Guid workshopId, string recipeId);
    void QueueRemoveWorkshopQueueEntry(Guid workshopId, Guid entryId);
    void QueueMoveWorkshopQueueEntry(Guid workshopId, Guid entryId, int moveOffset);
    void QueueSetWorkshopWorkerSlots(Guid workshopId, int workerSlots);
    void QueueToggleWorkshopAutoSupply(Guid workshopId);
    void QueueToggleWorkshopAutoStockpile(Guid workshopId);
}
