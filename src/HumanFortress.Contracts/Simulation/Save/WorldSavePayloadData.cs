namespace HumanFortress.Contracts.Simulation.Save;

public static class WorldSavePayloadFormat
{
    public const int CurrentVersion = 2;
}

public readonly record struct WorldSavePayloadData(
    int SchemaVersion,
    int SizeInChunks,
    int SizeInTiles,
    int MaxZ,
    string ReplayHash,
    WorldSaveSectionHashesData SectionHashes,
    WorldSaveCountsData Counts,
    WorldSaveChunkPayloadData[] Chunks,
    WorldSaveItemPayloadData[] Items,
    WorldSaveCreaturePayloadData[] Creatures,
    WorldSaveItemReservationPayloadData[] ItemReservations,
    WorldSaveCreatureReservationPayloadData[] CreatureReservations,
    WorldSaveStockpileZonePayloadData[] StockpileZones,
    WorldSavePlaceablePayloadData[] Placeables,
    WorldSaveMiningOrderPayloadData[] MiningOrders,
    WorldSaveHaulOrderPayloadData[] HaulOrders,
    WorldSaveConstructionOrderPayloadData[] ConstructionOrders,
    WorldSaveBuildableOrderPayloadData[] BuildableOrders);

public readonly record struct WorldSaveSectionHashesData(
    string TerrainHash,
    string ItemsHash,
    string CreaturesHash,
    string ReservationsHash,
    string StockpileZonesHash,
    string PlaceablesHash,
    string OrdersHash);

public readonly record struct WorldSaveCountsData(
    int ChunkCount,
    int TileCount,
    int ItemCount,
    int CreatureCount,
    int ItemReservationCount,
    int CreatureReservationCount,
    int StockpileZoneCount,
    int OwnedPlaceableCount,
    int MiningOrderCount,
    int HaulOrderCount,
    int ConstructionOrderCount,
    int BuildableOrderCount);

public readonly record struct WorldSaveChunkPayloadData(
    int ChunkX,
    int ChunkY,
    int Z,
    WorldSaveTilePayloadData[] Tiles);

public readonly record struct WorldSaveTilePayloadData(
    ushort GeoMatId,
    ushort TerrainBits,
    byte SurfaceBits,
    byte FluidKind,
    byte FluidDepth,
    byte MetaBits,
    ushort TrafficCost);

public readonly record struct WorldSavePointData(
    int X,
    int Y);

public readonly record struct WorldSaveRectangleData(
    int X,
    int Y,
    int Width,
    int Height);

public readonly record struct WorldSaveChunkKeyData(
    int ChunkX,
    int ChunkY,
    int Z);

public readonly record struct WorldSavePlacementData(
    WorldSavePointData AnchorWorld,
    int Z,
    int Rotation,
    string? StateId);

public readonly record struct WorldSaveItemReservationTokenData(
    Guid JobGuid,
    Guid? ClaimantCreatureGuid,
    int ReservedCount,
    ulong ExpiresAtTick,
    string ReservationType);

public readonly record struct WorldSaveItemImprovementData(
    string Type,
    string? MaterialId,
    int QualityTier,
    Guid? CreatedBy,
    string? Description);

public readonly record struct WorldSaveItemPerishableData(
    ulong CreatedAtTick,
    int FreshDurationTicks,
    int SpoilDurationTicks,
    float CurrentFreshness);

public readonly record struct WorldSaveItemPayloadData(
    Guid Guid,
    string DefinitionId,
    string? MaterialId,
    int StackCount,
    WorldSavePointData Position,
    int Z,
    Guid? ContainedBy,
    Guid? CarriedBy,
    Guid? EquippedBy,
    WorldSavePlacementData? InstalledAt,
    string? OwnerFactionId,
    Guid? OwnerCreatureGuid,
    int UsePolicy,
    bool Forbidden,
    WorldSaveItemReservationTokenData[] ReservationTokens,
    int QualityTier,
    bool Artifact,
    string? ArtifactName,
    string ConditionState,
    int? DurabilityCurrent,
    int? DurabilityMax,
    Guid? CraftedBy,
    string? MakerFactionId,
    string? StyleTag,
    WorldSaveItemImprovementData[]? Improvements,
    WorldSaveItemPerishableData? Perishable,
    ulong SpawnedAtTick);

public readonly record struct WorldSaveCreaturePayloadData(
    Guid Guid,
    string DefinitionId,
    string FactionId,
    WorldSavePointData Position,
    int Z,
    int HP,
    int MaxHP,
    ulong SpawnedAtTick);

public readonly record struct WorldSaveItemReservationPayloadData(
    Guid ItemId,
    Guid HolderId,
    ulong ExpireTick);

public readonly record struct WorldSaveCreatureReservationPayloadData(
    Guid WorkerId,
    string HolderSystem,
    string? JobId,
    ulong ExpireTick);

public readonly record struct WorldSaveStockpileFilterPayloadData(
    int Mode,
    string[] Tags,
    string[] ItemIds,
    string[] Materials);

public readonly record struct WorldSaveStockpileZonePayloadData(
    int ZoneId,
    string Name,
    WorldSaveChunkKeyData HomeChunk,
    WorldSaveStockpileFilterPayloadData Filter,
    int Priority,
    int TargetStacks,
    int HysteresisLow,
    int HysteresisHigh,
    uint Generation,
    ulong CreatedTick,
    WorldSaveChunkKeyData[] MemberChunks);

public readonly record struct WorldSaveMaterialFilterPayloadData(
    string? PreferredMaterialId,
    string CategoryKey,
    string[] Tags,
    WorldSaveMaterialRequirementPayloadData[]? Requirements = null);

public readonly record struct WorldSaveMaterialRequirementPayloadData(
    string? Tag,
    string? DefinitionId,
    int Count);

public readonly record struct WorldSaveFootprintData(
    int W,
    int D,
    int H);

public readonly record struct WorldSaveEffectsData(
    int Beauty,
    int Comfort,
    int LightLumen,
    int HeatW);

public readonly record struct WorldSaveStringIntData(
    string Key,
    int Value);

public readonly record struct WorldSaveConstructionSitePayloadData(
    string TargetId,
    WorldSaveStringIntData[] MaterialsRequired,
    WorldSaveStringIntData[] MaterialsDelivered,
    int BuildProgressTicks,
    int TotalBuildTicks);

public readonly record struct WorldSaveDoorStatePayloadData(
    bool IsOpen,
    bool IsLocked);

public readonly record struct WorldSaveWorkshopQueueEntryPayloadData(
    Guid EntryId,
    string RecipeId,
    string DisplayName,
    int Status,
    bool HasPendingRequests,
    ulong LastRequestTick,
    Guid? ActiveWorkerId,
    bool IsScheduled,
    string? BlockingReason);

public readonly record struct WorldSaveWorkshopPayloadData(
    bool AutoRequestMaterials,
    bool AutoStockpileOutputs,
    int AllowedWorkers,
    int MaxWorkers,
    int ActiveJobs,
    ulong NextEntrySequence,
    WorldSaveWorkshopQueueEntryPayloadData[] Queue);

public readonly record struct WorldSavePlaceablePayloadData(
    WorldSaveChunkKeyData OwnerChunk,
    int OwnerLocalIndex,
    Guid Guid,
    int Kind,
    string DefinitionId,
    WorldSavePointData Position,
    int Z,
    WorldSaveFootprintData Footprint,
    Guid? SourceItemGuid,
    string? SourceItemMaterial,
    int SourceItemQuality,
    WorldSaveItemImprovementData[]? SourceItemDecorations,
    Guid? SourceItemMaker,
    WorldSaveEffectsData Effects,
    int Passability,
    bool IsGhost,
    WorldSaveConstructionSitePayloadData? ConstructionSite,
    WorldSaveWorkshopPayloadData? Workshop,
    WorldSaveDoorStatePayloadData? DoorState,
    string? OwnerFactionId,
    Guid? OwnerCreatureGuid,
    bool Forbidden,
    int HitPoints,
    int MaxHitPoints);

public readonly record struct WorldSaveMiningOrderPayloadData(
    int Id,
    WorldSaveRectangleData Rect,
    int ZMin,
    int ZMax,
    int Action,
    int Priority,
    ulong CreatedTick);

public readonly record struct WorldSaveHaulOrderPayloadData(
    WorldSaveRectangleData WorldRect,
    int Z,
    int Priority,
    ulong CreatedTick);

public readonly record struct WorldSaveConstructionOrderPayloadData(
    WorldSaveRectangleData WorldRect,
    int ZMin,
    int ZMax,
    int Shape,
    WorldSaveMaterialFilterPayloadData Filter,
    int Priority,
    ulong CreatedTick);

public readonly record struct WorldSaveBuildableOrderPayloadData(
    string ConstructionId,
    WorldSavePointData Anchor,
    int Z,
    int Priority,
    ulong CreatedTick);
