using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Time;

namespace HumanFortress.Runtime;

public interface IFortressRuntimeSessionLifecyclePort
{
    void InitializeWorld(int sizeInChunks, int maxZ);
    TickSchedulerStopResult Stop(TimeSpan timeout);
    bool StopIfRunning();
    void StartFortressPlay(bool enqueueAutoDig);
}

public interface IFortressRuntimeSessionBootstrapPort
{
    RuntimeFortressGenerationResult GenerateAndFillFortressWorld(RuntimeFortressGenerationRequest request);
    void EnqueueStartupAutoDig(int currentZ);
    void SetWorkshopCompletionHandler(Action<RuntimeWorkshopCompletionNotification>? handler);
}
