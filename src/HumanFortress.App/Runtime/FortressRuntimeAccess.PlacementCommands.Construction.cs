using HumanFortress.Contracts.Runtime;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal sealed partial class FortressRuntimeAccess
{
    internal void QueueConstructionOrder(
        Rectangle rect,
        int zMin,
        int zMax,
        RuntimeConstructionShape shape,
        string? resultMaterialId,
        RuntimeConstructionMaterialRequirement[] materialRequirements,
        int priority = 50)
    {
        _placementCommands.QueueConstructionOrder(
            rect.ToRuntimeRect(),
            zMin,
            zMax,
            shape,
            resultMaterialId,
            materialRequirements,
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

    void IFortressRuntimePlacementCommandAccess.QueueConstructionOrder(
        Rectangle rect,
        int zMin,
        int zMax,
        RuntimeConstructionShape shape,
        string? resultMaterialId,
        RuntimeConstructionMaterialRequirement[] materialRequirements,
        int priority) =>
        QueueConstructionOrder(rect, zMin, zMax, shape, resultMaterialId, materialRequirements, priority);

    void IFortressRuntimePlacementCommandAccess.QueueBuildableConstructionOrder(
        string constructionId,
        Point anchor,
        int z,
        int priority) =>
        QueueBuildableConstructionOrder(constructionId, anchor, z, priority);
}
