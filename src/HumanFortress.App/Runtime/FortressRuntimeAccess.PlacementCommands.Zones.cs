using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal sealed partial class FortressRuntimeAccess
{
    internal void QueueCreateZone(string defId, Rectangle rect, int z)
    {
        _placementCommands.QueueCreateZone(defId, rect.ToRuntimeRect(), z);
    }

    internal void QueueDeleteZone(int zoneId)
    {
        _placementCommands.QueueDeleteZone(zoneId);
    }

    void IFortressRuntimePlacementCommandAccess.QueueCreateZone(string defId, Rectangle rect, int z) =>
        QueueCreateZone(defId, rect, z);

    void IFortressRuntimePlacementCommandAccess.QueueDeleteZone(int zoneId) => QueueDeleteZone(zoneId);
}
