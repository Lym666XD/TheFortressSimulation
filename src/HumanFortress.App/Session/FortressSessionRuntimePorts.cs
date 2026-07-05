using HumanFortress.App.Runtime;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.Session;

internal sealed class FortressSessionRuntimePorts
{
    private readonly IFortressRuntimeBootstrapAccess _bootstrap;

    internal FortressSessionRuntimePorts(IFortressRuntimeBootstrapAccess bootstrap)
    {
        _bootstrap = bootstrap ?? throw new ArgumentNullException(nameof(bootstrap));
    }

    internal SimulationWorldAvailabilityData GetWorldAvailabilityData()
    {
        return _bootstrap.GetWorldAvailabilityData();
    }

    internal RuntimeFortressGenerationResult GenerateAndFillFortressWorld(RuntimeFortressGenerationRequest request)
    {
        return _bootstrap.GenerateAndFillFortressWorld(request);
    }

    internal void EnqueueStartupAutoDig(int currentZ)
    {
        _bootstrap.EnqueueStartupAutoDig(currentZ);
    }

    internal void SetWorkshopCompletionHandler(Action<FortressSessionWorkshopCompletionNotification>? handler)
    {
        if (handler is null)
        {
            _bootstrap.SetWorkshopCompletionHandler(null);
            return;
        }

        _bootstrap.SetWorkshopCompletionHandler(notification =>
            handler(new FortressSessionWorkshopCompletionNotification(
                notification.ChunkX,
                notification.ChunkY,
                notification.ChunkZ,
                notification.Footprint,
                notification.ConstructionId,
                notification.SimulationTick)));
    }
}
