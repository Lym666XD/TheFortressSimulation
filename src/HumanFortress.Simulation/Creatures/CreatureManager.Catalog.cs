using HumanFortress.Contracts.Simulation.Creatures;

namespace HumanFortress.Simulation.Creatures;

internal sealed partial class CreatureManager
{
    /// <summary>
    /// Replace the static creature definition catalog with an already-loaded immutable snapshot.
    /// </summary>
    internal void SetDefinitionCatalog(CreatureDefinitionCatalogStore catalog)
    {
        _definitionCatalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    /// <summary>
    /// Get all creature definitions
    /// </summary>
    internal IEnumerable<CreatureDefinition> GetAllDefinitions()
    {
        return _definitionCatalog.GetAllDefinitions();
    }

    /// <summary>
    /// Get creature definitions by tag
    /// </summary>
    internal IEnumerable<CreatureDefinition> GetByTag(string tag)
    {
        return _definitionCatalog.GetByTag(tag);
    }

    /// <summary>
    /// Get definition by ID
    /// </summary>
    internal CreatureDefinition? GetDefinition(string id)
    {
        return _definitionCatalog.GetDefinition(id);
    }

    IEnumerable<CreatureDefinition> ICreatureDefinitionCatalog.GetAllDefinitions()
    {
        return GetAllDefinitions();
    }

    IEnumerable<CreatureDefinition> ICreatureDefinitionCatalog.GetByTag(string tag)
    {
        return GetByTag(tag);
    }

    CreatureDefinition? ICreatureDefinitionCatalog.GetDefinition(string id)
    {
        return GetDefinition(id);
    }
}
