namespace HumanFortress.Contracts.Runtime.Checkpoints;

public readonly record struct RuntimeContentIdentityData(
    int SchemaVersion,
    string Signature,
    string MechanicalHash,
    string HashAlgorithm);

public readonly record struct RuntimeCheckpointSectionIdentityData(
    string SectionId,
    int SchemaVersion,
    string PayloadHash,
    string HashAlgorithm,
    int PayloadLength);

public readonly record struct RuntimeCheckpointIdentityData(
    ulong SessionGeneration,
    ulong RuntimeTick,
    RuntimeContentIdentityData Content,
    string AggregateHash,
    string HashAlgorithm,
    IReadOnlyList<RuntimeCheckpointSectionIdentityData> Sections);
