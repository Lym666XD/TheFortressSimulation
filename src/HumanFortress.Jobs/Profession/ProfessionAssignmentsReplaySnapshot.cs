namespace HumanFortress.Jobs.Profession;

internal readonly record struct ProfessionValueStateSnapshot(
    string Id,
    int Value);

internal readonly record struct ProfessionWorkerStateSnapshot(
    Guid WorkerId,
    IReadOnlyList<ProfessionValueStateSnapshot> Weights,
    IReadOnlyList<ProfessionValueStateSnapshot> SkillLevels);

internal readonly record struct ProfessionAssignmentsReplaySnapshot(
    IReadOnlyList<ProfessionWorkerStateSnapshot> Workers)
{
    internal static ProfessionAssignmentsReplaySnapshot Empty { get; } = new(
        Array.Empty<ProfessionWorkerStateSnapshot>());
}
