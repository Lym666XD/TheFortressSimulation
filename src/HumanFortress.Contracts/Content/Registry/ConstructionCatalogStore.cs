using System;
using System.Collections.Generic;
using System.Linq;

namespace HumanFortress.Contracts.Content.Registry;

/// <summary>
/// Read-only construction definition catalog exposed to runtime/gameplay systems.
/// </summary>
public interface IConstructionCatalog
{
    int Count { get; }

    ConstructionDefinition? GetConstruction(string id);

    IEnumerable<ConstructionDefinition> GetConstructionsByCategory(string category);

    IEnumerable<ConstructionDefinition> GetAllConstructions();

    IEnumerable<string> GetAllCategories();
}

/// <summary>
/// Immutable read-only construction definition catalog snapshot.
/// </summary>
public sealed class ConstructionCatalogStore : IConstructionCatalog
{
    public static ConstructionCatalogStore Empty { get; } = new(
        new Dictionary<string, ConstructionDefinition>(StringComparer.Ordinal),
        new Dictionary<string, ConstructionDefinition[]>(StringComparer.Ordinal));

    private readonly IReadOnlyDictionary<string, ConstructionDefinition> _constructionsById;
    private readonly IReadOnlyDictionary<string, ConstructionDefinition[]> _constructionsByCategory;

    private ConstructionCatalogStore(
        IReadOnlyDictionary<string, ConstructionDefinition> constructionsById,
        IReadOnlyDictionary<string, ConstructionDefinition[]> constructionsByCategory)
    {
        _constructionsById = constructionsById;
        _constructionsByCategory = constructionsByCategory;
    }

    public int Count => _constructionsById.Count;

    public static ConstructionCatalogStore FromDefinitions(IEnumerable<ConstructionDefinition> constructions)
    {
        ArgumentNullException.ThrowIfNull(constructions);

        var byId = new Dictionary<string, ConstructionDefinition>(StringComparer.Ordinal);
        var byCategory = new Dictionary<string, List<ConstructionDefinition>>(StringComparer.Ordinal);

        foreach (var construction in constructions)
        {
            construction.Validate();
            if (byId.ContainsKey(construction.Id))
            {
                throw new InvalidOperationException($"Duplicate construction ID: {construction.Id}");
            }

            byId[construction.Id] = construction;
            if (!byCategory.TryGetValue(construction.Category, out var categoryDefinitions))
            {
                categoryDefinitions = new List<ConstructionDefinition>();
                byCategory[construction.Category] = categoryDefinitions;
            }

            categoryDefinitions.Add(construction);
        }

        var frozenCategories = new Dictionary<string, ConstructionDefinition[]>(byCategory.Count, StringComparer.Ordinal);
        foreach (var (category, definitions) in byCategory)
        {
            frozenCategories[category] = definitions.ToArray();
        }

        return new ConstructionCatalogStore(byId, frozenCategories);
    }

    public ConstructionDefinition? GetConstruction(string id)
    {
        return _constructionsById.TryGetValue(id, out var construction) ? construction : null;
    }

    public IEnumerable<ConstructionDefinition> GetConstructionsByCategory(string category)
    {
        return _constructionsByCategory.TryGetValue(category, out var constructions)
            ? constructions
            : Enumerable.Empty<ConstructionDefinition>();
    }

    public IEnumerable<ConstructionDefinition> GetAllConstructions()
    {
        return _constructionsById.Values;
    }

    public IEnumerable<string> GetAllCategories()
    {
        return _constructionsByCategory.Keys;
    }
}
