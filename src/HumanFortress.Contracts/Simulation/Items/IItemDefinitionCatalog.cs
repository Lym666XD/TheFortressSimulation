namespace HumanFortress.Contracts.Simulation.Items;

/// <summary>
/// Read-only item definition catalog. Runtime systems should prefer this over the full item manager
/// when they only need static item data.
/// </summary>
public interface IItemDefinitionCatalog
{
    int DefinitionCount { get; }

    ItemDefinition? GetDefinition(string id);

    IEnumerable<ItemDefinition> GetAllDefinitions();

    IEnumerable<ItemDefinition> GetByKind(string kind);

    IEnumerable<string> GetAvailableKinds();

    IEnumerable<ItemDefinition> GetByTag(string tag);
}
