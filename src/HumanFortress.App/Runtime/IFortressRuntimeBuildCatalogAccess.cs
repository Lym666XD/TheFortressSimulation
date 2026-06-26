using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.Runtime;

internal interface IFortressRuntimeBuildCatalogAccess
{
    SimulationBuildCatalogData GetBuildCatalogData();
}
