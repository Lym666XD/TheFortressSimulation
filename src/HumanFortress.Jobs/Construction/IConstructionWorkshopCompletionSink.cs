using SadRogue.Primitives;

namespace HumanFortress.Jobs.Construction;

internal interface IConstructionWorkshopCompletionSink
{
    void NotifyWorkshopComplete(int x, int y, int z, Rectangle footprint, string constructionId, ulong tick);
}
