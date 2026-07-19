using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.Runtime;

public interface IFortressRuntimeSessionCatalogQueryPort
{
    SimulationBuildCatalogData GetBuildCatalogData();

    SimulationZoneCatalogData GetZoneCatalogData();

    SimulationWorldAvailabilityData GetWorldAvailabilityData();

    string? GetDefaultRecipeForWorkshop(string? workshopId);
}
