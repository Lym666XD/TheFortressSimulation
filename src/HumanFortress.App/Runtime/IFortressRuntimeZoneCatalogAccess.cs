using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.Runtime;

internal interface IFortressRuntimeZoneCatalogAccess
{
    SimulationZoneCatalogData GetZoneCatalogData();
}
