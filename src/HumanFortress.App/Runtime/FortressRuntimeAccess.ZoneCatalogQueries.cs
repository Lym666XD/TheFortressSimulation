using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.Runtime;

internal sealed partial class FortressRuntimeAccess
{
    internal SimulationZoneCatalogData GetZoneCatalogData() =>
        _catalog.GetZoneCatalogData();

    SimulationZoneCatalogData IFortressRuntimeZoneCatalogAccess.GetZoneCatalogData() =>
        GetZoneCatalogData();
}
