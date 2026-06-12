namespace HumanFortress.Simulation.Creatures;

/// <summary>
/// Read-only creature definition catalog. Runtime systems should prefer this over the full creature manager
/// when they only need static creature data.
/// </summary>
public interface ICreatureDefinitionCatalog
{
    int DefinitionCount { get; }

    CreatureDefinition? GetDefinition(string id);

    IEnumerable<CreatureDefinition> GetAllDefinitions();

    IEnumerable<CreatureDefinition> GetByTag(string tag);
}
