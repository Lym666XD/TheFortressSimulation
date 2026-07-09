using System;
using System.Collections.Generic;
using System.Linq;

namespace HumanFortress.Contracts.Content.Registry;

/// <summary>
/// Normalized recipe definition loaded from data/core/recipes/*.json.
/// Minimal fields required by the craft system; schema is subset of RECIPE_SPEC.md.
/// </summary>
public sealed class RecipeDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;

#pragma warning disable CA1819 // JSON DTO compatibility
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
/// Read-only recipe catalog exposed to runtime/gameplay systems.
/// </summary>
public interface IRecipeCatalog
{
    int Count { get; }

    RecipeDefinition? GetRecipe(string id);

    IReadOnlyList<RecipeDefinition> GetRecipesForWorkshop(string workshopId);

    IEnumerable<RecipeDefinition> GetAllRecipes();
}

/// <summary>
/// Immutable read-only recipe definition catalog snapshot.
/// </summary>
public sealed class RecipeCatalogStore : IRecipeCatalog
{
    public static RecipeCatalogStore Empty { get; } = new(
        new Dictionary<string, RecipeDefinition>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, RecipeDefinition[]>(StringComparer.OrdinalIgnoreCase));

    private readonly IReadOnlyDictionary<string, RecipeDefinition> _recipes;
    private readonly IReadOnlyDictionary<string, RecipeDefinition[]> _byWorkshop;

    private RecipeCatalogStore(
        IReadOnlyDictionary<string, RecipeDefinition> recipes,
        IReadOnlyDictionary<string, RecipeDefinition[]> byWorkshop)
    {
        _recipes = recipes;
        _byWorkshop = byWorkshop;
    }

    public int Count => _recipes.Count;

    public static RecipeCatalogStore FromDefinitions(IEnumerable<RecipeDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var recipes = new Dictionary<string, RecipeDefinition>(StringComparer.OrdinalIgnoreCase);
        var byWorkshop = new Dictionary<string, List<RecipeDefinition>>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions.OrderBy(static definition => definition.Id, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(definition.Id))
            {
                continue;
            }

            recipes[definition.Id] = definition;
            foreach (var workshopId in definition.Workshops)
            {
                if (!byWorkshop.TryGetValue(workshopId, out var workshopRecipes))
                {
                    workshopRecipes = new List<RecipeDefinition>();
                    byWorkshop[workshopId] = workshopRecipes;
                }

                workshopRecipes.Add(definition);
            }
        }

        var frozenByWorkshop = new Dictionary<string, RecipeDefinition[]>(byWorkshop.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (workshopId, recipesForWorkshop) in byWorkshop.OrderBy(static entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            recipesForWorkshop.Sort((a, b) =>
            {
                int cmp = string.Compare(a.Name, b.Name, StringComparison.Ordinal);
                return cmp != 0 ? cmp : string.Compare(a.Id, b.Id, StringComparison.Ordinal);
            });
            frozenByWorkshop[workshopId] = recipesForWorkshop.ToArray();
        }

        return new RecipeCatalogStore(recipes, frozenByWorkshop);
    }

    public RecipeDefinition? GetRecipe(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return _recipes.GetValueOrDefault(id);
    }

    public IReadOnlyList<RecipeDefinition> GetRecipesForWorkshop(string workshopId)
    {
        if (string.IsNullOrWhiteSpace(workshopId))
        {
            return Array.Empty<RecipeDefinition>();
        }

        return _byWorkshop.TryGetValue(workshopId, out var recipes)
            ? recipes
            : Array.Empty<RecipeDefinition>();
    }

    public IEnumerable<RecipeDefinition> GetAllRecipes()
    {
        return _recipes.Values
            .OrderBy(static recipe => recipe.Id, StringComparer.Ordinal);
    }
}
