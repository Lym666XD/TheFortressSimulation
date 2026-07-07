using HumanFortress.Contracts.Runtime;

namespace HumanFortress.App.Runtime;

internal sealed partial class FortressRuntimeAccess
{
    internal RuntimeFortressGenerationResult GenerateAndFillFortressWorld(RuntimeFortressGenerationRequest request)
    {
        return _bootstrap.GenerateAndFillFortressWorld(request);
    }

    internal void EnqueueStartupAutoDig(int currentZ)
    {
        _bootstrap.EnqueueStartupAutoDig(currentZ);
    }

    internal void SetWorkshopCompletionHandler(Action<FortressWorkshopCompletionNotification>? handler)
    {
        if (handler is null)
        {
            _bootstrap.SetWorkshopCompletionHandler(null);
            return;
        }

        _bootstrap.SetWorkshopCompletionHandler(notification =>
            handler(new FortressWorkshopCompletionNotification(
                notification.ChunkX,
                notification.ChunkY,
                notification.ChunkZ,
                notification.Footprint.ToSadRogueRectangle(),
                notification.ConstructionId,
                notification.Tick)));
    }

    RuntimeFortressGenerationResult IFortressRuntimeBootstrapAccess.GenerateAndFillFortressWorld(
        RuntimeFortressGenerationRequest request) =>
        GenerateAndFillFortressWorld(request);

    void IFortressRuntimeBootstrapAccess.EnqueueStartupAutoDig(int currentZ) => EnqueueStartupAutoDig(currentZ);

    void IFortressRuntimeBootstrapAccess.SetWorkshopCompletionHandler(
        Action<FortressWorkshopCompletionNotification>? handler) =>
        SetWorkshopCompletionHandler(handler);
}
