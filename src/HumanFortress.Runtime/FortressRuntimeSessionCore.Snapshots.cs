using HumanFortress.Contracts.Runtime.Snapshots;
using HumanFortress.Runtime.Snapshots;

namespace HumanFortress.Runtime;

internal sealed partial class FortressRuntimeSessionCore
{
    SimulationBuildCatalogData IFortressRuntimeSessionCatalogQueryPort.GetBuildCatalogData()
    {
        return FortressRuntimeSessionSnapshotFacade.BuildCatalogSnapshot(_runtimeSession);
    }

    SimulationZoneCatalogData IFortressRuntimeSessionCatalogQueryPort.GetZoneCatalogData()
    {
        return FortressRuntimeSessionSnapshotFacade.BuildZoneCatalogSnapshot(_runtimeSession);
    }

    SimulationDebugMenuData IFortressRuntimeSessionSnapshotPort.GetDebugMenuData()
    {
        return FortressRuntimeSessionSnapshotFacade.BuildDebugMenuSnapshot(_runtimeSession);
    }

    SimulationDebugSpawnData IFortressRuntimeSessionSnapshotPort.GetDebugSpawnData()
    {
        return FortressRuntimeSessionSnapshotFacade.BuildDebugSpawnSnapshot(_runtimeSession);
    }

    SimulationWorldAvailabilityData IFortressRuntimeSessionCatalogQueryPort.GetWorldAvailabilityData()
    {
        return FortressRuntimeSessionSnapshotFacade.BuildWorldAvailabilitySnapshot(_runtimeSession);
    }
}
