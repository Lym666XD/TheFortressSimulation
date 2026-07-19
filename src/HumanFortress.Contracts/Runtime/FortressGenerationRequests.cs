namespace HumanFortress.Contracts.Runtime;

public sealed record RuntimeFortressGenerationRequest(
    int FortressSize,
    int EmbarkX,
    int EmbarkY,
    ushort BiomeId,
    float Elevation,
    float Temperature,
    float Rainfall,
    float Drainage,
    byte RiverClass,
    bool HasAquifer,
    IReadOnlyList<ushort> StoneSet,
    IReadOnlyList<int> LandmarkIds,
    uint? GenerationSeed = null);

public enum RuntimeFortressGenerationStatus
{
    Success,
    MissingGenerationContent,
    MissingRuntimeWorld
}

public sealed record RuntimeFortressGenerationResult(
    RuntimeFortressGenerationStatus Status,
    int FortressMapSize)
{
    public bool Succeeded => Status == RuntimeFortressGenerationStatus.Success;
}
