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

public sealed record RuntimeMaterialFilterSpec(
    string? PreferredMaterialId,
    string CategoryKey,
    string[] Tags);
