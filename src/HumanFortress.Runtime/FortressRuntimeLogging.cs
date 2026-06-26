namespace HumanFortress.Runtime;

internal sealed class FortressRuntimeLogging
{
    internal static FortressRuntimeLogging None { get; } = new();

    internal FortressRuntimeLogging(
        Action<string>? log = null,
        Action<string>? constructionMaterials = null,
        FortressRuntimeWorkshopCompletionNotifier? workshopCompletion = null)
    {
        Log = log;
        ConstructionMaterials = constructionMaterials ?? log;
        WorkshopCompletion = workshopCompletion;
    }

    internal Action<string>? Log { get; }
    internal Action<string>? ConstructionMaterials { get; }
    internal FortressRuntimeWorkshopCompletionNotifier? WorkshopCompletion { get; }
}
