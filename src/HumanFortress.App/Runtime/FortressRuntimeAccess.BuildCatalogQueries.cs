using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.Runtime;

internal sealed partial class FortressRuntimeAccess
{
    internal SimulationBuildCatalogData GetBuildCatalogData()
    {
        return _snapshots.GetBuildCatalogData();
    }

    SimulationBuildCatalogData IFortressRuntimeBuildCatalogAccess.GetBuildCatalogData() => GetBuildCatalogData();
}
