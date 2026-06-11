using HumanFortress.Jobs.Construction;
using SadRogue.Primitives;

namespace HumanFortress.App.Jobs;

internal sealed class AppConstructionJobLogger : IConstructionJobLogger
{
    public static readonly AppConstructionJobLogger Instance = new();

    private AppConstructionJobLogger()
    {
    }

    public void Log(string message)
    {
        Logger.Log(message);
    }
}

internal sealed class AppConstructionWorkshopCompletionSink : IConstructionWorkshopCompletionSink
{
    public void NotifyWorkshopComplete(int x, int y, int z, Rectangle footprint, string constructionId, ulong tick)
    {
        ConstructionJobSystem.UiNotifyWorkshopComplete?.Invoke(x, y, z, footprint, constructionId, tick);
    }
}
