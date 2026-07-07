using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal sealed partial class FortressRuntimeAccess
{
    internal void QueueCreateStockpile(Rectangle rect, int z, string presetId)
    {
        _placementCommands.QueueCreateStockpile(rect.ToRuntimeRect(), z, presetId);
    }

    internal void QueueDeleteStockpile(int zoneId)
    {
        _placementCommands.QueueDeleteStockpile(zoneId);
    }

    void IFortressRuntimePlacementCommandAccess.QueueCreateStockpile(Rectangle rect, int z, string presetId) =>
        QueueCreateStockpile(rect, z, presetId);

    void IFortressRuntimePlacementCommandAccess.QueueDeleteStockpile(int zoneId) => QueueDeleteStockpile(zoneId);
}
