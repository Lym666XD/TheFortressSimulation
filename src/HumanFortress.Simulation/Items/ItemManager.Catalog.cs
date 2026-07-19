using HumanFortress.Contracts.Simulation.Items;

namespace HumanFortress.Simulation.Items;

internal sealed partial class ItemManager
{
    /// <summary>
    /// Replace the static item definition catalog with an already-loaded immutable snapshot.
    /// </summary>
    internal void SetDefinitionCatalog(ItemDefinitionCatalogStore catalog)
    {
        _definitionCatalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    /// <summary>
    /// Get all item definitions
    /// </summary>
    internal IEnumerable<ItemDefinition> GetAllDefinitions()
    {
        return _definitionCatalog.GetAllDefinitions();
    }

    /// <summary>
    /// Get item definitions by kind (resource/weapon/armor/tool/container/consumable)
    /// </summary>
    internal IEnumerable<ItemDefinition> GetByKind(string kind)
    {
        return _definitionCatalog.GetByKind(kind);
    }

    /// <summary>
    /// Get available kinds (for UI category display)
    /// </summary>
    internal IEnumerable<string> GetAvailableKinds()
    {
        return _definitionCatalog.GetAvailableKinds();
    }

    /// <summary>
    /// Get item definitions by tag
    /// </summary>
    internal IEnumerable<ItemDefinition> GetByTag(string tag)
    {
        return _definitionCatalog.GetByTag(tag);
    }

    /// <summary>
    /// Get definition by ID
    /// </summary>
    internal ItemDefinition? GetDefinition(string id)
    {
        return _definitionCatalog.GetDefinition(id);
    }

    IEnumerable<ItemDefinition> IItemDefinitionCatalog.GetAllDefinitions()
    {
        return GetAllDefinitions();
    }

    IEnumerable<ItemDefinition> IItemDefinitionCatalog.GetByKind(string kind)
    {
        return GetByKind(kind);
    }

    IEnumerable<string> IItemDefinitionCatalog.GetAvailableKinds()
    {
        return GetAvailableKinds();
    }

    IEnumerable<ItemDefinition> IItemDefinitionCatalog.GetByTag(string tag)
    {
        return GetByTag(tag);
    }

    ItemDefinition? IItemDefinitionCatalog.GetDefinition(string id)
    {
        return GetDefinition(id);
    }
}
