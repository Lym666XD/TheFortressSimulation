using HumanFortress.Contracts.Simulation.Items;

namespace HumanFortress.Simulation.Items;

internal sealed partial class ItemManager
{
    /// <summary>
    /// Replace the static item definition catalog with an already-loaded immutable snapshot.
    /// </summary>
    public void SetDefinitionCatalog(ItemDefinitionCatalogStore catalog)
    {
        _definitionCatalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    /// <summary>
    /// Get all item definitions
    /// </summary>
    public IEnumerable<ItemDefinition> GetAllDefinitions()
    {
        return _definitionCatalog.GetAllDefinitions();
    }

    /// <summary>
    /// Get item definitions by kind (resource/weapon/armor/tool/container/consumable)
    /// </summary>
    public IEnumerable<ItemDefinition> GetByKind(string kind)
    {
        return _definitionCatalog.GetByKind(kind);
    }

    /// <summary>
    /// Get available kinds (for UI category display)
    /// </summary>
    public IEnumerable<string> GetAvailableKinds()
    {
        return _definitionCatalog.GetAvailableKinds();
    }

    /// <summary>
    /// Get item definitions by tag
    /// </summary>
    public IEnumerable<ItemDefinition> GetByTag(string tag)
    {
        return _definitionCatalog.GetByTag(tag);
    }

    /// <summary>
    /// Get definition by ID
    /// </summary>
    public ItemDefinition? GetDefinition(string id)
    {
        return _definitionCatalog.GetDefinition(id);
    }
}
