using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal interface IFortressRuntimePlacementAccess
{
    SimulationWorldAvailabilityData GetWorldAvailabilityData();

    ZoneHitData FindZoneAt(Point worldPosition, int z);
    StockpileHitData FindStockpileAt(Point worldPosition, int z);

    void QueueHaulOrder(Rectangle rect, int z, int priority = 50);
    void QueueAdvancedMiningOrder(
        Rectangle rect,
        int zMin,
        int zMax,
        RuntimeMiningAction action,
        int priority = 50);
    void QueueConstructionOrder(
        Rectangle rect,
        int zMin,
        int zMax,
        RuntimeConstructionShape shape,
        string? preferredMaterialId,
        string[] materialTags,
        int priority = 50);
    void QueueBuildableConstructionOrder(string constructionId, Point anchor, int z, int priority = 50);
    void QueueCreateZone(string defId, Rectangle rect, int z);
    void QueueDeleteZone(int zoneId);
    void QueueCreateStockpile(Rectangle rect, int z, string presetId);
    void QueueDeleteStockpile(int zoneId);
}
