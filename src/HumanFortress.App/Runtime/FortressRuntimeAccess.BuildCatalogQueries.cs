using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.Runtime;

internal sealed partial class FortressRuntimeAccess
{
    internal SimulationBuildCatalogData GetBuildCatalogData()
    {
        return _catalog.GetBuildCatalogData();
    }

    SimulationBuildCatalogData IFortressRuntimeBuildCatalogAccess.GetBuildCatalogData() => GetBuildCatalogData();
}
