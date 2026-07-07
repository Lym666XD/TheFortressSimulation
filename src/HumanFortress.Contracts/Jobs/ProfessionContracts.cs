namespace HumanFortress.Contracts.Jobs;

public sealed record ProfessionDefinition(string Id, string Name, string[] JobTags, bool IsDefault);

public interface IProfessionRegistry
{
    IReadOnlyList<ProfessionDefinition> Definitions { get; }
    ProfessionDefinition DefaultProfession { get; }
    IReadOnlyList<ProfessionDefinition> GetProfessionsForJob(string jobTag);
}
