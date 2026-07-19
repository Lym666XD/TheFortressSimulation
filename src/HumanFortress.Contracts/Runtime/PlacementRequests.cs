namespace HumanFortress.Contracts.Runtime;

public enum RuntimeMiningAction
{
    Dig,
    DigStairwell,
    DigRamp,
    DigChannel,
    RemoveDigging
}

public enum RuntimeConstructionShape
{
    Wall,
    Floor,
    Ramp,
    Stairs
}

public readonly record struct RuntimeConstructionMaterialRequirement(
    string? Tag,
    string? DefinitionId,
    int Count);

public sealed record RuntimeMaterialFilterSpec(
    string? PreferredMaterialId,
    string CategoryKey,
    string[] Tags,
    RuntimeConstructionMaterialRequirement[] Requirements)
{
    public RuntimeMaterialFilterSpec(
        string? preferredMaterialId,
        string categoryKey,
        string[] tags)
        : this(
            preferredMaterialId,
            categoryKey,
            tags,
            Array.Empty<RuntimeConstructionMaterialRequirement>())
    {
    }
}
