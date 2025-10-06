using System;
using System.Collections.Generic;
using System.Linq;

namespace HumanFortress.Core.Content.Registry;

/// <summary>
/// Registry for construction definitions (on-site built structures)
/// Loads from data/core/placeable/*.json (walls.json, floors.json, workshops.json, etc.)
/// </summary>
public sealed class ConstructionRegistry
{
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
