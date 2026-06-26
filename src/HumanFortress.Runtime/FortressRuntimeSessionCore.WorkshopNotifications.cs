using HumanFortress.Contracts.Runtime;

namespace HumanFortress.Runtime;

internal sealed partial class FortressRuntimeSessionCore
{
    void IFortressRuntimeSessionBootstrapPort.SetWorkshopCompletionHandler(
        Action<RuntimeWorkshopCompletionNotification>? handler)
    {
        if (handler is null)
        {
            _workshopCompletionNotifier.SetHandler(null);
            return;
        }

        _workshopCompletionNotifier.SetHandler((x, y, z, footprint, constructionId, tick) =>
            handler(new RuntimeWorkshopCompletionNotification(
                x,
                y,
                z,
                footprint.ToRuntimeRect(),
                constructionId,
                tick)));
    }
}
