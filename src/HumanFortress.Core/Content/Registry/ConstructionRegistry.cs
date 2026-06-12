using System;
using System.Collections.Generic;
using System.Linq;

namespace HumanFortress.Core.Content.Registry;

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

/// <summary>
/// Registry for construction definitions (on-site built structures)
/// Loads from data/core/placeable/*.json (walls.json, floors.json, workshops.json, etc.)
/// </summary>
public sealed class ConstructionRegistry : IConstructionCatalog
{
    // Global instance for easy access across App/Simulation layers (mirrors other registries)
    private static ConstructionRegistry? _instance;
    public static ConstructionRegistry Instance => _instance ??= new ConstructionRegistry();

    private readonly Dictionary<string, ConstructionDefinition> _constructionsById = new();
    private readonly Dictionary<string, List<ConstructionDefinition>> _constructionsByCategory = new();

    /// <summary>
    /// Load construction definitions into registry
    /// </summary>
    public void LoadConstructions(IEnumerable<ConstructionDefinition> constructions)
    {
        foreach (var construction in constructions)
        {
            construction.Validate();
            AddConstruction(construction);
        }
    }

    private void AddConstruction(ConstructionDefinition construction)
    {
        if (_constructionsById.ContainsKey(construction.Id))
        {
            throw new InvalidOperationException($"Duplicate construction ID: {construction.Id}");
        }

        _constructionsById[construction.Id] = construction;

        if (!_constructionsByCategory.ContainsKey(construction.Category))
        {
            _constructionsByCategory[construction.Category] = new List<ConstructionDefinition>();
        }
        _constructionsByCategory[construction.Category].Add(construction);
    }

    /// <summary>
    /// Get construction definition by ID
    /// </summary>
    public ConstructionDefinition? GetConstruction(string id)
    {
        return _constructionsById.TryGetValue(id, out var construction) ? construction : null;
    }

    /// <summary>
    /// Get all construction definitions in a category
    /// </summary>
    public IEnumerable<ConstructionDefinition> GetConstructionsByCategory(string category)
    {
        return _constructionsByCategory.TryGetValue(category, out var constructions)
            ? constructions
            : Enumerable.Empty<ConstructionDefinition>();
    }

    /// <summary>
    /// Get all construction definitions
    /// </summary>
    public IEnumerable<ConstructionDefinition> GetAllConstructions()
    {
        return _constructionsById.Values;
    }

    /// <summary>
    /// Get all categories
    /// </summary>
    public IEnumerable<string> GetAllCategories()
    {
        return _constructionsByCategory.Keys;
    }

    public int Count => _constructionsById.Count;

    public void Clear()
    {
        _constructionsById.Clear();
        _constructionsByCategory.Clear();
    }
}
