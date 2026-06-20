namespace HumanFortress.Runtime;

public sealed class FortressRuntimeLogging
{
    public static FortressRuntimeLogging None { get; } = new();

    public FortressRuntimeLogging(
        Action<string>? log = null,
        Action<string>? constructionMaterials = null)
    {
        Log = log;
        ConstructionMaterials = constructionMaterials ?? log;
    }

    public Action<string>? Log { get; }
    public Action<string>? ConstructionMaterials { get; }
}
