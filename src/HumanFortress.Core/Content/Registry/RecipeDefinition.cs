using System;
using System.Collections.Generic;
using System.Linq;

namespace HumanFortress.Core.Content.Registry;

/// <summary>
/// Normalized recipe definition loaded from data/core/recipes/*.json.
/// Minimal fields required by the craft system; schema is subset of RECIPE_SPEC.md.
/// </summary>
public sealed class RecipeDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
#pragma warning disable CA1819
    public string[] Workshops { get; init; } = Array.Empty<string>();
    public RecipeIngredient[] Inputs { get; init; } = Array.Empty<RecipeIngredient>();
    public RecipeOutput[] Outputs { get; init; } = Array.Empty<RecipeOutput>();
    public string[] RequiredEnablers { get; init; } = Array.Empty<string>();
#pragma warning restore CA1819
    public int DurationTicks { get; init; } = 600;
    public string PrimarySkill { get; init; } = "craft";
    public string? Era { get; init; }

    /// <summary>Job tag used when selecting workers; defaults to skill id.</summary>
    public string JobTag => string.IsNullOrWhiteSpace(PrimarySkill) ? "craft" : PrimarySkill;
}

public sealed class RecipeIngredient
{
    public string DefId { get; init; } = string.Empty;
    public int Count { get; init; } = 1;
}

public sealed class RecipeOutput
{
    public string DefId { get; init; } = string.Empty;
    public int Count { get; init; } = 1;
}

/// <summary>
/// Runtime registry storing recipe definitions keyed by id and workshop.
/// </summary>
public sealed class RecipeRegistry
{
    private static readonly RecipeRegistry _instance = new();
    public static RecipeRegistry Instance => _instance;

    private readonly Dictionary<string, RecipeDefinition> _recipes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<RecipeDefinition>> _byWorkshop = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _recipes.Count;

    public void Clear()
    {
        _recipes.Clear();
        _byWorkshop.Clear();
    }

    public void LoadRecipes(IEnumerable<RecipeDefinition> defs)
    {
        Clear();
        foreach (var def in defs)
        {
            if (string.IsNullOrWhiteSpace(def.Id)) continue;
            _recipes[def.Id] = def;
            foreach (var ws in def.Workshops)
            {
                if (!_byWorkshop.TryGetValue(ws, out var list))
                {
                    list = new List<RecipeDefinition>();
                    _byWorkshop[ws] = list;
                }
                list.Add(def);
            }
        }
        foreach (var list in _byWorkshop.Values)
        {
            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
        }
    }

    public RecipeDefinition? GetRecipe(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        return _recipes.GetValueOrDefault(id);
    }

    public IReadOnlyList<RecipeDefinition> GetRecipesForWorkshop(string workshopId)
    {
        if (string.IsNullOrWhiteSpace(workshopId)) return Array.Empty<RecipeDefinition>();
        if (_byWorkshop.TryGetValue(workshopId, out var list))
            return list;
        return Array.Empty<RecipeDefinition>();
    }

    public IEnumerable<RecipeDefinition> GetAllRecipes() => _recipes.Values;
}
