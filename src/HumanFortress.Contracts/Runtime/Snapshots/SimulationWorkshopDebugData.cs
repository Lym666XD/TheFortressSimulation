namespace HumanFortress.Contracts.Runtime.Snapshots;

public readonly record struct WorkshopQueueEntryView(
    Guid EntryId,
    string DisplayName,
    string Prefix,
    string StatusText,
    bool IsBlocked);

public readonly record struct WorkshopSummaryView(
    Guid WorkshopGuid,
    string DefinitionId,
    string Name,
    int X,
    int Y,
    int Z,
    int FootprintW,
    int FootprintD,
    string Passability,
    IReadOnlyList<string> Tags,
    int AttachmentSlotCount,
    string? SiteMaterialProgressText,
    bool IsSite,
    int ActiveJobs,
    int AllowedWorkers,
    int MaxWorkers,
    bool AutoRequestMaterials,
    bool AutoStockpileOutputs,
    int QueueCount,
    bool HasBlockedQueue,
    IReadOnlyList<WorkshopQueueEntryView> Queue);

public readonly record struct SimulationWorkshopDebugData(
    IReadOnlyList<WorkshopSummaryView> Workshops,
    int BuiltCount,
    int SiteCount,
    int QueuedBuildableDesignations);
