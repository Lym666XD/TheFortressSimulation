using HumanFortress.Contracts.Runtime.Snapshots;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal interface IFortressRuntimePlacementQueryAccess
{
    SimulationWorldAvailabilityData GetWorldAvailabilityData();

    ZoneHitData FindZoneAt(Point worldPosition, int z);

    StockpileHitData FindStockpileAt(Point worldPosition, int z);
}
