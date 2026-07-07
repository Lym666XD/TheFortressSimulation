using HumanFortress.Contracts.Simulation.Creatures;

namespace HumanFortress.Simulation.Creatures;

internal sealed partial class CreatureManager
{
    /// <summary>
    /// Replace the static creature definition catalog with an already-loaded immutable snapshot.
    /// </summary>
    public void SetDefinitionCatalog(CreatureDefinitionCatalogStore catalog)
    {
        _definitionCatalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    /// <summary>
    /// Get all creature definitions
    /// </summary>
    public IEnumerable<CreatureDefinition> GetAllDefinitions()
    {
        return _definitionCatalog.GetAllDefinitions();
    }

    /// <summary>
    /// Get creature definitions by tag
    /// </summary>
    public IEnumerable<CreatureDefinition> GetByTag(string tag)
    {
        return _definitionCatalog.GetByTag(tag);
    }

    /// <summary>
    /// Get definition by ID
    /// </summary>
    public CreatureDefinition? GetDefinition(string id)
    {
        return _definitionCatalog.GetDefinition(id);
    }
}
