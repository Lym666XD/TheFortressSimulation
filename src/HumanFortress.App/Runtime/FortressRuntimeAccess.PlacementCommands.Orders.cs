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

    void IFortressRuntimePlacementCommandAccess.QueueHaulOrder(Rectangle rect, int z, int priority) =>
        QueueHaulOrder(rect, z, priority);

    void IFortressRuntimePlacementCommandAccess.QueueAdvancedMiningOrder(
        Rectangle rect,
        int zMin,
        int zMax,
        RuntimeMiningAction action,
        int priority) =>
        QueueAdvancedMiningOrder(rect, zMin, zMax, action, priority);
}
