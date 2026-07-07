using HumanFortress.Contracts.Runtime.Replay;
using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.Contracts.Runtime.Save;

public static class RuntimeSaveFormat
{
    public const int CurrentVersion = 2;
}

public readonly record struct RuntimeSaveManifestData(
    int FormatVersion,
    string EngineBuild,
    SimulationSnapshotMetadata Metadata,
    RuntimeSaveContentSignatureData Content,
    RuntimeReplayCheckpointData Checkpoint,
    IReadOnlyList<RuntimeSaveManifestSectionData> Sections);

public readonly record struct RuntimeSaveContentSignatureData(
    bool HasContent,
    string ContentVersion,
    string ContentHash,
    string MaterialContentHash,
    int MaterialCount,
    int TerrainKindCount,
    int ConstructionCount,
    int RecipeCount,
    int GeologyCount,
    int ZoneCount)
{
    public static RuntimeSaveContentSignatureData Unavailable { get; } = new(
        HasContent: false,
        ContentVersion: string.Empty,
        ContentHash: string.Empty,
        MaterialContentHash: string.Empty,
        MaterialCount: 0,
        TerrainKindCount: 0,
        ConstructionCount: 0,
        RecipeCount: 0,
        GeologyCount: 0,
        ZoneCount: 0);
}

public readonly record struct RuntimeSaveManifestSectionData(
    string Name,
    bool Present,
    string? Hash,
    bool RequiredForFortressMode,
    long? RecordCount = null);
