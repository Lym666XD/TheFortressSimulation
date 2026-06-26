using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.Runtime;

internal interface IFortressRuntimeBootstrapAccess
{
    SimulationWorldAvailabilityData GetWorldAvailabilityData();
    RuntimeFortressGenerationResult GenerateAndFillFortressWorld(RuntimeFortressGenerationRequest request);
    void EnqueueStartupAutoDig(int currentZ);
    void SetWorkshopCompletionHandler(Action<FortressWorkshopCompletionNotification>? handler);
}
