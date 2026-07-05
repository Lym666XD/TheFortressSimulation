using HumanFortress.Contracts.Runtime;
using HumanFortress.Runtime.Commands;
using HumanFortress.Runtime.Geometry;

namespace HumanFortress.Runtime;

internal sealed partial class FortressRuntimeSessionCore
{
    void IFortressRuntimeSessionPlacementCommandPort.QueueHaulOrder(RuntimeRect rect, int z, int priority)
    {
        EnqueueCurrentTickCommand(RuntimePlacementCommandFactory.CreateHaulOrder(rect.ToSadRogueRectangle(), z, priority));
    }

    void IFortressRuntimeSessionPlacementCommandPort.QueueAdvancedMiningOrder(
        RuntimeRect rect,
        int zMin,
        int zMax,
        RuntimeMiningAction action,
        int priority)
    {
        EnqueueCurrentTickCommand(RuntimePlacementCommandFactory.CreateAdvancedMiningOrder(
            rect.ToSadRogueRectangle(),
            zMin,
            zMax,
            action,
            priority));
    }

    void IFortressRuntimeSessionPlacementCommandPort.QueueConstructionOrder(
        RuntimeRect rect,
        int zMin,
        int zMax,
        RuntimeConstructionShape shape,
        string? preferredMaterialId,
        string[] materialTags,
        int priority)
    {
        var filter = RuntimePlacementCommandFactory.CreateMaterialFilter(shape, preferredMaterialId, materialTags);
        EnqueueCurrentTickCommand(RuntimePlacementCommandFactory.CreateConstructionOrder(
            rect.ToSadRogueRectangle(),
            zMin,
            zMax,
            shape,
            filter,
            priority));
    }

    void IFortressRuntimeSessionPlacementCommandPort.QueueBuildableConstructionOrder(
        string constructionId,
        RuntimePoint anchor,
        int z,
        int priority)
    {
        EnqueueCurrentTickCommand(RuntimePlacementCommandFactory.CreateBuildableConstructionOrder(
            constructionId,
            anchor.ToSadRoguePoint(),
            z,
            priority));
    }

    void IFortressRuntimeSessionPlacementCommandPort.QueueCreateZone(string defId, RuntimeRect rect, int z)
    {
        EnqueueCurrentTickCommand(RuntimePlacementCommandFactory.CreateZone(defId, rect.ToSadRogueRectangle(), z));
    }

    void IFortressRuntimeSessionPlacementCommandPort.QueueDeleteZone(int zoneId)
    {
        EnqueueCurrentTickCommand(RuntimePlacementCommandFactory.DeleteZone(zoneId));
    }

    void IFortressRuntimeSessionPlacementCommandPort.QueueCreateStockpile(RuntimeRect rect, int z, string presetId)
    {
        EnqueueCurrentTickCommand(RuntimePlacementCommandFactory.CreateStockpile(rect.ToSadRogueRectangle(), z, presetId));
    }

    void IFortressRuntimeSessionPlacementCommandPort.QueueDeleteStockpile(int zoneId)
    {
        EnqueueCurrentTickCommand(RuntimePlacementCommandFactory.DeleteStockpile(zoneId));
    }
}
