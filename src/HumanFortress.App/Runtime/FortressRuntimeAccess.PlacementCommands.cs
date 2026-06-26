using HumanFortress.Contracts.Runtime;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal sealed partial class FortressRuntimeAccess
{
    internal void QueueHaulOrder(Rectangle rect, int z, int priority = 50)
    {
        _placementCommands.QueueHaulOrder(rect.ToRuntimeRect(), z, priority);
    }

    internal void QueueAdvancedMiningOrder(
        Rectangle rect,
        int zMin,
        int zMax,
        RuntimeMiningAction action,
        int priority = 50)
    {
        _placementCommands.QueueAdvancedMiningOrder(
            rect.ToRuntimeRect(),
            zMin,
            zMax,
            action,
            priority);
    }

    internal void QueueConstructionOrder(
        Rectangle rect,
        int zMin,
        int zMax,
        RuntimeConstructionShape shape,
        string? preferredMaterialId,
        string[] materialTags,
        int priority = 50)
    {
        _placementCommands.QueueConstructionOrder(
            rect.ToRuntimeRect(),
            zMin,
            zMax,
            shape,
            preferredMaterialId,
            materialTags,
            priority);
    }

    internal void QueueBuildableConstructionOrder(
        string constructionId,
        Point anchor,
        int z,
        int priority = 50)
    {
        _placementCommands.QueueBuildableConstructionOrder(
            constructionId,
            anchor.ToRuntimePoint(),
            z,
            priority);
    }

    internal void QueueCreateZone(string defId, Rectangle rect, int z)
    {
        _placementCommands.QueueCreateZone(defId, rect.ToRuntimeRect(), z);
    }

    internal void QueueDeleteZone(int zoneId)
    {
        _placementCommands.QueueDeleteZone(zoneId);
    }

    internal void QueueCreateStockpile(Rectangle rect, int z, string presetId)
    {
        _placementCommands.QueueCreateStockpile(rect.ToRuntimeRect(), z, presetId);
    }

    void IFortressRuntimePlacementAccess.QueueHaulOrder(Rectangle rect, int z, int priority) =>
        QueueHaulOrder(rect, z, priority);

    void IFortressRuntimePlacementAccess.QueueAdvancedMiningOrder(
        Rectangle rect,
        int zMin,
        int zMax,
        RuntimeMiningAction action,
        int priority) =>
        QueueAdvancedMiningOrder(rect, zMin, zMax, action, priority);

    void IFortressRuntimePlacementAccess.QueueConstructionOrder(
        Rectangle rect,
        int zMin,
        int zMax,
        RuntimeConstructionShape shape,
        string? preferredMaterialId,
        string[] materialTags,
        int priority) =>
        QueueConstructionOrder(rect, zMin, zMax, shape, preferredMaterialId, materialTags, priority);

    void IFortressRuntimePlacementAccess.QueueBuildableConstructionOrder(
        string constructionId,
        Point anchor,
        int z,
        int priority) =>
        QueueBuildableConstructionOrder(constructionId, anchor, z, priority);

    void IFortressRuntimePlacementAccess.QueueCreateZone(string defId, Rectangle rect, int z) =>
        QueueCreateZone(defId, rect, z);

    void IFortressRuntimePlacementAccess.QueueDeleteZone(int zoneId) => QueueDeleteZone(zoneId);

    void IFortressRuntimePlacementAccess.QueueCreateStockpile(Rectangle rect, int z, string presetId) =>
        QueueCreateStockpile(rect, z, presetId);
}
