using HumanFortress.Contracts.Runtime;

namespace HumanFortress.Runtime;

public interface IFortressRuntimeSessionLifecyclePort
{
    void InitializeWorld(int sizeInChunks, int maxZ);
    bool StopIfRunning();
    void StartFortressPlay(bool enqueueAutoDig);
}

public interface IFortressRuntimeSessionBootstrapPort
{
    RuntimeFortressGenerationResult GenerateAndFillFortressWorld(RuntimeFortressGenerationRequest request);
    void EnqueueStartupAutoDig(int currentZ);
    void SetWorkshopCompletionHandler(Action<RuntimeWorkshopCompletionNotification>? handler);
}
