using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.Runtime;

internal sealed partial class FortressRuntimeAccess
{
    internal SimulationWorldAvailabilityData GetWorldAvailabilityData()
    {
        return _snapshots.GetWorldAvailabilityData();
    }

    SimulationWorldAvailabilityData IFortressRuntimeBootstrapAccess.GetWorldAvailabilityData() =>
        GetWorldAvailabilityData();

    SimulationWorldAvailabilityData IFortressRuntimePlacementQueryAccess.GetWorldAvailabilityData() =>
        GetWorldAvailabilityData();
}
